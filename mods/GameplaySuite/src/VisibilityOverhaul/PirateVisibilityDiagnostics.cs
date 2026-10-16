using System;
using System.Collections.Generic;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace VisibilityOverhaul;

// Read-only, bounded evidence collection for issue #46. No native visibility
// query is initiated here; the LOS postfix only observes MENACE's own result.
internal static class PirateVisibilityDiagnostics
{
    private const int PirateFaction = 6;
    private const int MaxPirateSamples = 3;
    private const int MaxPropertySnapshots = 24;
    private const int MaxEvents = 100;

    private static readonly Dictionary<IntPtr, ActorPropertySnapshot> PropertySnapshots = new();
    private static readonly Queue<IntPtr> PropertySnapshotOrder = new();
    private static readonly Dictionary<IntPtr, PirateSample> Samples = new();
    private static readonly HashSet<string> ReportedErrors = new();
    private static Action<string>? _log;
    private static int _eventCount;
    private static bool _eventLimitReported;

    public static bool Enabled => RuntimeSettings.DebugLogging && RuntimeSettings.PirateVisibilityDiagnostics;

    public static void SetLogger(Action<string> logger) => _log = logger;

    public static void Start(string version, string gameVersion, string settings)
    {
        if (!Enabled) return;
        Reset();
        Write($"start voVersion={version} gameVersion={gameVersion} gameBuild=unavailable settings=({settings}) " +
              "missionSeed=unavailable activeMods=unavailable limits=3-pirates/100-events");
    }

    public static void Reset()
    {
        PropertySnapshots.Clear();
        PropertySnapshotOrder.Clear();
        Samples.Clear();
        ReportedErrors.Clear();
        _eventCount = 0;
        _eventLimitReported = false;
    }

    public static Entity? ResolveEntity(Skill skill)
    {
        if (!Enabled) return null;
        try { return skill.GetEntity(); }
        catch (Exception ex) { ReportErrorOnce("resolve-entity", ex); return null; }
    }

    public static Actor? ResolveActor(Entity? entity)
    {
        if (!Enabled || entity == null) return null;
        try { return entity.TryCast<Actor>(); }
        catch (Exception ex) { ReportErrorOnce("resolve-actor", ex); return null; }
    }

    public static PropertyValues CaptureBefore(EntityProperties properties, Actor actor)
    {
        if (!Enabled || !IsRelevantActor(actor, out _)) return default;
        try
        {
            return new PropertyValues(
                properties.Vision,
                properties.Detection,
                properties.Concealment,
                Read(() => properties.GetVision(), "GetVision"),
                Read(() => properties.GetDetection(), "GetDetection"),
                Read(() => properties.GetConcealment(), "GetConcealment"));
        }
        catch (Exception ex) { ReportErrorOnce("property-before", ex); return default; }
    }

    public static void CaptureAfter(
        Actor? actor,
        EntityProperties properties,
        PropertyValues before,
        int concealmentModifier,
        IntPtr propertiesPointer,
        long updateCall,
        bool visionMutationApplied,
        bool concealmentEvaluated,
        bool concealmentMutationApplied)
    {
        if (!Enabled || actor == null || actor.Pointer == IntPtr.Zero || !IsRelevantActor(actor, out int faction)) return;
        try
        {
            bool infantry = actor.IsInfantry();
            var after = ReadValues(properties);
            int? moved = infantry ? (int?)Math.Max(0, actor.TilesMovedThisTurn) : null;
            int? x = null;
            int? z = null;
            try
            {
                var tile = actor.GetTile();
                if (tile != null && tile.Pointer != IntPtr.Zero)
                {
                    x = tile.GetX();
                    z = tile.GetZ();
                }
            }
            catch (Exception ex) { ReportErrorOnce("actor-tile", ex); }

            var snapshot = new ActorPropertySnapshot(
                faction, infantry, moved, x, z, before, after, concealmentModifier,
                propertiesPointer, updateCall, visionMutationApplied, concealmentEvaluated, concealmentMutationApplied);
            if (!PropertySnapshots.ContainsKey(actor.Pointer))
            {
                if (PropertySnapshots.Count >= MaxPropertySnapshots && PropertySnapshotOrder.Count > 0)
                    PropertySnapshots.Remove(PropertySnapshotOrder.Dequeue());
                PropertySnapshotOrder.Enqueue(actor.Pointer);
            }
            PropertySnapshots[actor.Pointer] = snapshot;
        }
        catch (Exception ex) { ReportErrorOnce("property-after", ex); }
    }

    public static void ObserveVisibilityCheck(Actor observer, Entity target, Tile fromTile, Tile atTile, bool result)
    {
        if (!Enabled || observer == null || target == null) return;
        try
        {
            var pirate = target.TryCast<Actor>();
            if (pirate == null || pirate.Pointer == IntPtr.Zero) return;

            int observerFaction = (int)observer.GetFaction();
            if (!IsAlliedFaction(observerFaction) || (int)pirate.GetFaction() != PirateFaction || !pirate.IsInfantry()) return;

            int fromX = 0, fromZ = 0, atX = 0, atZ = 0;
            bool coordinatesAvailable = TryCoordinates(fromTile, out fromX, out fromZ) &&
                                        TryCoordinates(atTile, out atX, out atZ);
            int tileSpan = coordinatesAvailable ? Math.Max(Math.Abs(atX - fromX), Math.Abs(atZ - fromZ)) : -1;

            if (!Samples.TryGetValue(pirate.Pointer, out var sample))
            {
                if (Samples.Count >= MaxPirateSamples) return;
                sample = new PirateSample(observer.Pointer, tileSpan);
                Samples.Add(pirate.Pointer, sample);
            }
            else if (sample.Observer != observer.Pointer)
            {
                // Keep the smallest coordinate-span allied observer seen in MENACE's checks.
                if (tileSpan < 0 || (sample.ObserverDistance >= 0 && tileSpan >= sample.ObserverDistance)) return;
                sample.Observer = observer.Pointer;
                sample.ObserverDistance = tileSpan;
                sample.LastResult = null;
                sample.LastTileSpan = -1;
            }
            else
            {
                sample.ObserverDistance = tileSpan;
            }

            bool initial = !sample.LastResult.HasValue;
            bool predicateChanged = !initial && sample.LastResult!.Value != result;
            bool enteredAdjacentSpan = sample.LastTileSpan >= 2 && tileSpan == 1;
            if (result && !sample.FirstVisibleTileSpan.HasValue && tileSpan >= 0)
                sample.FirstVisibleTileSpan = tileSpan;

            if (initial || predicateChanged || enteredAdjacentSpan)
            {
                string reason = initial ? "initial" : predicateChanged ? "predicate-transition" : "entered-tile-span-1";
                EmitEvent($"reason={reason} observer=0x{observer.Pointer.ToInt64():X} observerFaction={FactionName(observerFaction)}/{observerFaction} " +
                          $"target=0x{pirate.Pointer.ToInt64():X} targetFaction=Pirates/{PirateFaction} targetType=infantry " +
                          $"observerTile={TileText(fromTile)} targetTile={TileText(atTile)} " +
                          $"coordinateDelta={DeltaText(coordinatesAvailable, atX - fromX, atZ - fromZ)} coordinateTileSpan={(tileSpan >= 0 ? tileSpan.ToString() : "unavailable")} actualDistance=unavailable " +
                          $"nativeHasLineOfSightTo={result} previousPredicate={BoolText(sample.LastResult)} " +
                          $"firstNativeVisibleTileSpan={Number(sample.FirstVisibleTileSpan)} " +
                          $"derivedOpenFieldRange={DerivedOpenFieldRange(observer.Pointer, pirate.Pointer)} " +
                          "geometricLOS=unavailable (native predicate may combine LOS and spotting) actualRange=unavailable " +
                          "terrain=unavailable cover=unavailable finalUiVisibility=unavailable turn=unavailable stance=unavailable " +
                          $"observerStats=({DescribeProperties(observer.Pointer)}) targetStats=({DescribeProperties(pirate.Pointer)})");
                if (predicateChanged) sample.Transitions++;
            }

            sample.LastResult = result;
            sample.LastTileSpan = tileSpan;
        }
        catch (Exception ex) { ReportErrorOnce("visibility-check", ex); }
    }

    public static void ObserveFinalVisibility(Actor target, Tile tile, bool hidden)
    {
        if (!Enabled || target == null || target.Pointer == IntPtr.Zero || !Samples.TryGetValue(target.Pointer, out var sample)) return;
        try
        {
            bool tileAvailable = TryCoordinates(tile, out int x, out int z);
            if (!tileAvailable) { x = 0; z = 0; }
            bool sameTile = sample.FinalTileAvailable == tileAvailable &&
                            (!tileAvailable || (sample.FinalTileX == x && sample.FinalTileZ == z));
            bool initial = !sample.LastFinalHidden.HasValue || !sameTile;
            bool changed = !initial && sample.LastFinalHidden!.Value != hidden;
            if (initial || changed)
            {
                bool visible = !hidden;
                string reason = initial ? "initial-final-visibility" : "final-visibility-transition";
                EmitEvent($"reason={reason} target=0x{target.Pointer.ToInt64():X} observer=0x{sample.Observer.ToInt64():X} " +
                          $"tile={(tileAvailable ? $"({x},{z})" : "unavailable")} finalVisibility={(visible ? "visible" : "hidden")} " +
                          $"previousFinalVisibility={(sample.LastFinalHidden.HasValue && sameTile ? (sample.LastFinalHidden.Value ? "hidden" : "visible") : "unavailable")} " +
                          $"nativeHasLineOfSightTo={BoolText(sample.LastResult)} geometricLOS=unavailable turn=unavailable " +
                          $"observerStats=({DescribeProperties(sample.Observer)}) targetStats=({DescribeProperties(target.Pointer)})");
            }
            sample.FinalTileAvailable = tileAvailable;
            sample.FinalTileX = x;
            sample.FinalTileZ = z;
            sample.LastFinalHidden = hidden;
        }
        catch (Exception ex) { ReportErrorOnce("final-visibility", ex); }
    }

    public static void LogSummary(long visionAdjustments, long concealmentAdjustments)
    {
        if (!Enabled || (Samples.Count == 0 && _eventCount == 0)) return;
        string samples = string.Empty;
        foreach (var pair in Samples)
        {
            if (samples.Length > 0) samples += "; ";
            samples += $"pirate=0x{pair.Key.ToInt64():X},observer=0x{pair.Value.Observer.ToInt64():X},transitions={pair.Value.Transitions},firstVisibleTileSpan={Number(pair.Value.FirstVisibleTileSpan)}";
        }
        Write($"summary events={_eventCount} eventLimitReached={_eventLimitReported} samples={Samples.Count} " +
              $"visionRebuildsAdjusted={visionAdjustments} concealmentRebuildsAdjusted={concealmentAdjustments} [{samples}]");
    }

    private static PropertyValues ReadValues(EntityProperties properties)
    {
        return new PropertyValues(
            properties.Vision,
            properties.Detection,
            properties.Concealment,
            Read(() => properties.GetVision(), "GetVision"),
            Read(() => properties.GetDetection(), "GetDetection"),
            Read(() => properties.GetConcealment(), "GetConcealment"));
    }

    private static int? Read(Func<int> getter, string operation)
    {
        try { return getter(); }
        catch (Exception ex) { ReportErrorOnce(operation, ex); return null; }
    }

    private static bool TryCoordinates(Tile tile, out int x, out int z)
    {
        x = 0;
        z = 0;
        if (tile == null || tile.Pointer == IntPtr.Zero) return false;
        try
        {
            x = tile.GetX();
            z = tile.GetZ();
            return true;
        }
        catch (Exception ex) { ReportErrorOnce("tile-coordinates", ex); return false; }
    }

    private static string TileText(Tile tile) => TryCoordinates(tile, out int x, out int z) ? $"({x},{z})" : "unavailable";

    private static string DeltaText(bool available, int dx, int dz) => available ? $"({dx},{dz})" : "unavailable";

    private static bool IsRelevantActor(Actor actor, out int faction)
    {
        faction = -1;
        if (actor == null || actor.Pointer == IntPtr.Zero) return false;
        try
        {
            faction = (int)actor.GetFaction();
            return IsAlliedFaction(faction) || (faction == PirateFaction && actor.IsInfantry());
        }
        catch (Exception ex) { ReportErrorOnce("actor-classification", ex); return false; }
    }

    private static string DescribeProperties(IntPtr actor)
    {
        if (!PropertySnapshots.TryGetValue(actor, out var snapshot)) return "properties=unavailable";
        return $"faction={FactionName(snapshot.Faction)}/{snapshot.Faction},infantry={snapshot.Infantry},moved={Number(snapshot.Moved)},stance=unavailable,tile=({Number(snapshot.X)},{Number(snapshot.Z)})," +
               $"visionRaw={Pair(snapshot.Before.Vision, snapshot.After.Vision)},visionEff={Pair(snapshot.Before.VisionEffective, snapshot.After.VisionEffective)}," +
               $"detectionRaw={Pair(snapshot.Before.Detection, snapshot.After.Detection)},detectionEff={Pair(snapshot.Before.DetectionEffective, snapshot.After.DetectionEffective)}," +
               $"concealmentRaw={Pair(snapshot.Before.Concealment, snapshot.After.Concealment)},concealmentEff={Pair(snapshot.Before.ConcealmentEffective, snapshot.After.ConcealmentEffective)}," +
               $"properties=0x{snapshot.PropertiesPointer.ToInt64():X},skillUpdateCall={snapshot.UpdateCall}," +
               $"voVisionApplicationsInUpdateCall={(snapshot.VisionMutationApplied ? 1 : 0)}," +
               $"voConcealmentEvaluationsInUpdateCall={(snapshot.ConcealmentEvaluated ? 1 : 0)}," +
               $"voConcealmentApplicationsInUpdateCall={(snapshot.ConcealmentMutationApplied ? 1 : 0)}," +
               $"voConcealmentModifier={snapshot.ConcealmentModifier}";
    }

    private static string DerivedOpenFieldRange(IntPtr observer, IntPtr target)
    {
        if (!PropertySnapshots.TryGetValue(observer, out var observerStats) ||
            !PropertySnapshots.TryGetValue(target, out var targetStats) ||
            !observerStats.After.VisionEffective.HasValue ||
            !observerStats.After.DetectionEffective.HasValue ||
            !targetStats.After.ConcealmentEffective.HasValue)
            return "unavailable";

        int concealmentPenalty = Math.Max(0, targetStats.After.ConcealmentEffective.Value - observerStats.After.DetectionEffective.Value);
        return Math.Max(1, observerStats.After.VisionEffective.Value - concealmentPenalty).ToString();
    }

    private static void EmitEvent(string message)
    {
        if (_eventCount >= MaxEvents)
        {
            if (!_eventLimitReported)
            {
                _eventLimitReported = true;
                Write($"event limit reached ({MaxEvents}); suppressing further transitions");
            }
            return;
        }
        _eventCount++;
        Write($"event={_eventCount} {message}");
    }

    private static bool IsAlliedFaction(int faction) => faction >= 1 && faction <= 4;

    private static string FactionName(int faction) => faction switch
    {
        1 => "Player",
        2 => "PlayerAI",
        3 => "Civilian",
        4 => "AlliedLocalForces",
        5 => "EnemyLocalForces",
        6 => "Pirates",
        7 => "Wildlife",
        8 => "Constructs",
        9 => "RogueArmy",
        _ => "unknown"
    };

    private static string Pair(int? before, int? after) =>
        before.HasValue && after.HasValue ? $"{before.Value}->{after.Value}" : "unavailable";

    private static string Number(int? value) => value.HasValue ? value.Value.ToString() : "unavailable";

    private static string BoolText(bool? value) => value.HasValue ? value.Value.ToString().ToLowerInvariant() : "unavailable";

    private static void ReportErrorOnce(string operation, Exception exception)
    {
        string key = operation + ":" + exception.GetType().Name;
        if (!Enabled || !ReportedErrors.Add(key)) return;
        Write($"error operation={operation} type={exception.GetType().Name}; further matching errors suppressed");
    }

    private static void Write(string message) => _log?.Invoke("[PirateVisibility] " + message);

    internal readonly struct PropertyValues
    {
        public readonly int? Vision;
        public readonly int? Detection;
        public readonly int? Concealment;
        public readonly int? VisionEffective;
        public readonly int? DetectionEffective;
        public readonly int? ConcealmentEffective;

        public PropertyValues(int vision, int detection, int concealment, int? visionEffective, int? detectionEffective, int? concealmentEffective)
        {
            Vision = vision;
            Detection = detection;
            Concealment = concealment;
            VisionEffective = visionEffective;
            DetectionEffective = detectionEffective;
            ConcealmentEffective = concealmentEffective;
        }
    }

    private sealed class ActorPropertySnapshot
    {
        public readonly int Faction;
        public readonly bool Infantry;
        public readonly int? Moved;
        public readonly int? X;
        public readonly int? Z;
        public readonly PropertyValues Before;
        public readonly PropertyValues After;
        public readonly int ConcealmentModifier;
        public readonly IntPtr PropertiesPointer;
        public readonly long UpdateCall;
        public readonly bool VisionMutationApplied;
        public readonly bool ConcealmentEvaluated;
        public readonly bool ConcealmentMutationApplied;

        public ActorPropertySnapshot(
            int faction, bool infantry, int? moved, int? x, int? z, PropertyValues before, PropertyValues after,
            int concealmentModifier, IntPtr propertiesPointer, long updateCall, bool visionMutationApplied,
            bool concealmentEvaluated, bool concealmentMutationApplied)
        {
            Faction = faction;
            Infantry = infantry;
            Moved = moved;
            X = x;
            Z = z;
            Before = before;
            After = after;
            ConcealmentModifier = concealmentModifier;
            PropertiesPointer = propertiesPointer;
            UpdateCall = updateCall;
            VisionMutationApplied = visionMutationApplied;
            ConcealmentEvaluated = concealmentEvaluated;
            ConcealmentMutationApplied = concealmentMutationApplied;
        }
    }

    private sealed class PirateSample
    {
        public IntPtr Observer;
        public int ObserverDistance;
        public bool? LastResult;
        public int LastTileSpan = -1;
        public int? FirstVisibleTileSpan;
        public int Transitions;
        public bool? LastFinalHidden;
        public bool FinalTileAvailable;
        public int FinalTileX;
        public int FinalTileZ;

        public PirateSample(IntPtr observer, int observerDistance)
        {
            Observer = observer;
            ObserverDistance = observerDistance;
        }
    }
}