using HarmonyLib;
using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.UI;
using Il2CppMenace.UI.Tactical;
using UnityEngine;
using UnityEngine.UIElements;

namespace MenaceOverwatch;

internal enum OverwatchMode
{
    Squad,
    Leader,
    Both,
    Vehicle,
    AlphaStrike
}

internal sealed class Runtime : IDisposable
{
    private sealed class Watch
    {
        public Actor Shooter = null!;
        public Skill Primary = null!;
        public Skill? Secondary;
        public OverwatchMode Mode;
        public int Threshold;
        public int AimPenalty;
        public bool PlayerSide;
        public bool Pending;
        public bool ExpireAfterPending;
        public int CostPerReaction;
        public int RemainingReactions;
        public int InitialReactions;
        public readonly Dictionary<long, long> LastReactedTileByTarget = new();
    }

    private sealed class Reaction
    {
        public Watch Watch = null!;
        public Actor Target = null!;
        public Tile Tile = null!;
        public int PrimaryChance;
        public int SecondaryChance;
        public bool PrimaryEligible;
        public bool SecondaryEligible;
        public string Trigger = "movement FINISH";
        public float EarliestFireAt;
        public int Stage;
        public bool AnyShotAccepted;
    }

    private sealed class PendingActionTrigger
    {
        public Actor Actor = null!;
        public readonly HashSet<long> SkillKeys = new();
        public string FirstSkillId = "-";
        public string LastSkillId = "-";
        public int SkillUses;
        public float FirstSeenAt;
        public float LastSeenAt;
        public bool SawBusy;
        public bool CompletionSeen;
    }

    private sealed class PlayerSelection
    {
        public OverwatchMode Mode;
        public int SquadIndex;
        public int LeaderIndex;
        public int VehicleIndex;
    }

    private sealed class NativeHudMarker
    {
        public VisualElement Strip = null!;
        public long HudKey;
        public int Reactions;
        public bool PlayerSide;
        public bool HighContrast;
    }

    private readonly Action<string> _debugLog;
    private readonly Action<string> _warningLog;
    private readonly Action<string> _errorLog;
    private readonly global::HarmonyLib.Harmony _harmony;
    private readonly Dictionary<long, Watch> _watches = new();
    private readonly Queue<Reaction> _reactions = new();
    private readonly Dictionary<long, PendingActionTrigger> _pendingActionTriggers = new();
    private readonly Dictionary<long, PlayerSelection> _playerSelections = new();
    private readonly Dictionary<long, NativeHudMarker> _nativeHudMarkers = new();
    private readonly Dictionary<string, Sprite> _markerSprites = new();
    private Actor? _selected;
    private Skill? _squad;
    private Skill? _leader;
    private Skill? _vehicle;
    private Skill? _alphaStrike;
    private int _squadIndex;
    private int _leaderIndex;
    private int _vehicleIndex;
    private OverwatchMode _mode = OverwatchMode.Squad;
    private Actor? _lastAiActor;
    private bool _lastAiFinalized;
    private bool _aiEnabled;
    private bool _actionTriggersEnabled;
    private bool _vehicleEnabled;
    private bool _aiVehicleEnabled;
    private bool _showWorldMarkers;
    private bool _highContrastIndicators;
    private bool _debugLogging;
    private int _playerThreshold;
    private int _aiThreshold;
    private int _aimPenalty;
    private bool _coordinatedFireInstalled;
    private bool _alphaStrikeInstalled;
    private bool _disposed;
    private float _nextRefresh;
    private float _nextMarkerRefresh;
    private string _reason = "Select an infantry unit or vehicle.";

    // Hit-chance penalty context. A native reaction is executed with Free because MENACE
    // sets the reacting unit to 0 AP during the opponent turn. The context stays active
    // until the tactical skill system becomes idle, so the native resolution path sees
    // the same reduced FinalValue that was shown/checked when the reaction was queued.
    private long _penaltySkillKey;
    private int _penaltyAmount;
    private bool _penaltyContextActive;
    private bool _penaltyGlobal;
    private bool _penaltySawBusy;
    private float _penaltyActivatedAt;

    // Reaction shots are native Skill.Use calls too. Suppress their skill-use events so an
    // Overwatch shot cannot recursively provoke another Overwatch shot. Keep this context
    // independent of the aim-penalty context so recursion is still blocked at 0 penalty.
    private long _reactionActionActorKey;
    private bool _reactionActionContextActive;
    private bool _reactionActionSawBusy;
    private float _reactionActionActivatedAt;

    internal Runtime(global::HarmonyLib.Harmony harmony, Action<string> debugLog, Action<string> warningLog, Action<string> errorLog, bool debugLogging, bool aiEnabled,
        bool actionTriggersEnabled, bool vehicleEnabled, bool aiVehicleEnabled, bool showWorldMarkers, bool highContrastIndicators,
        int playerThreshold, int aiThreshold,
        int aimPenalty, bool coordinatedFireInstalled, bool alphaStrikeInstalled)
    {
        _harmony = harmony;
        _debugLog = debugLog;
        _warningLog = warningLog;
        _errorLog = errorLog;
        _debugLogging = debugLogging;
        _aiEnabled = aiEnabled;
        _actionTriggersEnabled = actionTriggersEnabled;
        _vehicleEnabled = vehicleEnabled;
        _aiVehicleEnabled = aiVehicleEnabled;
        _showWorldMarkers = showWorldMarkers;
        _highContrastIndicators = highContrastIndicators;
        _playerThreshold = playerThreshold;
        _aiThreshold = aiThreshold;
        _aimPenalty = aimPenalty;
        _coordinatedFireInstalled = coordinatedFireInstalled;
        _alphaStrikeInstalled = alphaStrikeInstalled;
        Hooks.Owner = this;

        var movementStarted = AccessTools.Method(typeof(TacticalManager), "InvokeOnMovement",
            new[] { typeof(Actor), typeof(Tile), typeof(Tile), typeof(MovementAction), typeof(Entity) });
        if (movementStarted != null)
            _harmony.Patch(movementStarted, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MovementStarted)));
        else
            _warningLog("TacticalManager.InvokeOnMovement(...) was not found; movement-commit expiration is unavailable.");

        var movementFinished = AccessTools.Method(typeof(TacticalManager), "InvokeOnMovementFinished",
            new[] { typeof(Actor), typeof(Tile) });
        if (movementFinished == null)
            throw new MissingMethodException("TacticalManager.InvokeOnMovementFinished(Actor, Tile) was not found.");
        _harmony.Patch(movementFinished, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MovementFinished)));

        // Record native ATTACK skill actions, then provoke Overwatch only after MENACE has finished
        // the attack and the tactical skill system is idle. Movement has its own final-tile path.
        // Utility/state-transition skills (reload, stance, embark/eject passengers, etc.) are not
        // reaction triggers; this avoids entering Overwatch while MENACE mutates tactical rosters.
        var skillUse = AccessTools.Method(typeof(TacticalManager), "InvokeOnSkillUse",
            new[] { typeof(Actor), typeof(Skill), typeof(Tile) });
        if (skillUse != null)
            _harmony.Patch(skillUse, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.SkillUse)));
        else
            _warningLog("TacticalManager.InvokeOnSkillUse(Actor, Skill, Tile) was not found; attack-triggered Overwatch is unavailable.");

        var afterSkillUse = AccessTools.Method(typeof(TacticalManager), "InvokeOnAfterSkillUse",
            new[] { typeof(Skill) });
        if (afterSkillUse != null)
            _harmony.Patch(afterSkillUse, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.AfterSkillUse)));

        var getHitChance = AccessTools.Method(typeof(Skill), "GetHitchance",
            new[] { typeof(Tile), typeof(Tile), typeof(EntityProperties), typeof(EntityProperties), typeof(bool), typeof(Entity), typeof(bool) });
        if (getHitChance == null)
            throw new MissingMethodException("Skill.GetHitchance(...) was not found.");
        _harmony.Patch(getHitChance, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.HitChancePostfix)));

        if (_debugLogging) _debugLog("Overwatch bound to movement-start, movement-finished and native skill-use events: selecting/cycling units no longer expires a watch; a watch expires when its unit actually commits movement or an active skill. Reaction provocation is whitelisted to movement completion and native attack skills only; utility/transport actions are ignored. Overwatch reaction shots never recursively trigger Overwatch.");
    }

    public bool InMission { get; private set; }
    public string Status => _reason;
    public string SquadName => Native.Title(_squad);
    public string LeaderName => Native.Title(_leader);
    public string VehicleName => Native.Title(_vehicle);
    public string AlphaStrikeName => Native.Title(_alphaStrike);
    public OverwatchMode Mode => _mode;
    public bool Armed => Native.Valid(_selected) && _watches.ContainsKey(Native.Key(_selected));
    public bool CarriedIntoFreshActivation
    {
        get
        {
            if (!Native.Valid(_selected)) return false;
            long key = Native.Key(_selected);
            if (!_watches.TryGetValue(key, out var watch) || !watch.PlayerSide || watch.Pending) return false;
            try
            {
                // Immediately after arming, AP is forced to zero and MENACE is ending that
                // activation.  A carried watch becomes renewable only after MENACE gives the
                // actor a genuinely fresh activation with AP again.
                return !_selected!.IsTurnDone() && _selected.GetActionPoints() > 0;
            }
            catch { return false; }
        }
    }
    public bool CanEditCurrentPlan => !Armed || CarriedIntoFreshActivation;
    public int ArmedCount => _watches.Count;
    public bool HasSquad => Native.Valid(_squad);
    public bool HasLeader => Native.Valid(_leader);
    public bool HasVehicle => Native.Valid(_vehicle);
    public bool HasAlphaStrike => Native.Valid(_alphaStrike);
    public bool BothAvailable => _coordinatedFireInstalled && HasSquad && HasLeader;
    public bool IsSelectedVehicle => Native.Valid(_selected) && Native.IsVehicleActor(_selected!);
    public bool AlphaStrikeAvailable => IsSelectedVehicle && HasAlphaStrike;
    public bool HasSelectedPlayerActor
    {
        get
        {
            try
            {
                var manager = TacticalManager.Get();
                if (!Native.Valid(manager) || !manager.IsPlayerTurn()) return false;
                var actor = manager.GetActiveActor();
                if (!Native.Valid(actor) || !Native.IsPlayerControlled(actor)) return false;
                return Native.IsInfantryActor(actor) || Native.IsVehicleActor(actor);
            }
            catch { return false; }
        }
    }

    public void Configure(bool aiEnabled, bool actionTriggersEnabled, bool vehicleEnabled, bool aiVehicleEnabled,
        bool showWorldMarkers, bool highContrastIndicators, int playerThreshold, int aiThreshold, int aimPenalty, bool coordinatedFireInstalled, bool alphaStrikeInstalled, bool debugLogging)
    {
        _debugLogging = debugLogging;
        _aiEnabled = aiEnabled;
        _actionTriggersEnabled = actionTriggersEnabled;
        if (!_actionTriggersEnabled) _pendingActionTriggers.Clear();
        _vehicleEnabled = vehicleEnabled;
        _aiVehicleEnabled = aiVehicleEnabled;
        _showWorldMarkers = showWorldMarkers;
        if (_highContrastIndicators != highContrastIndicators)
        {
            _highContrastIndicators = highContrastIndicators;
            // Force only the reticle artwork to rebuild. MENACE remains fully in charge
            // of the parent UnitHUD opacity and spent-unit presentation.
            foreach (var marker in _nativeHudMarkers.Values) marker.Reactions = -1;
        }
        _playerThreshold = playerThreshold;
        _aiThreshold = aiThreshold;
        _aimPenalty = Math.Max(0, aimPenalty);
        _coordinatedFireInstalled = coordinatedFireInstalled;
        _alphaStrikeInstalled = alphaStrikeInstalled;
        if (_mode == OverwatchMode.Both && !BothAvailable) _mode = HasSquad ? OverwatchMode.Squad : OverwatchMode.Leader;
        if (_mode == OverwatchMode.AlphaStrike && !HasAlphaStrike) _mode = HasVehicle ? OverwatchMode.Vehicle : OverwatchMode.AlphaStrike;
    }

    public void Update()
    {
        if (_disposed) return;
        InMission = TacticalManager.IsMissionRunning();
        if (!InMission) { Reset(); return; }
        var manager = TacticalManager.Get();
        if (!Native.Valid(manager) || manager.IsPaused() || manager.IsFinished()) return;

        bool skillBusy = TacticalManager.IsSkillBusy();

        // Keep the accuracy modifier alive across asynchronous native resolution. Some attacks
        // return from Skill.Use before their busy state becomes visible, so do not clear merely
        // because the immediately following frame is idle. Clear after a busy->idle cycle, with
        // a short safety timeout for attacks that resolve synchronously.
        if (_penaltyContextActive)
        {
            if (skillBusy) _penaltySawBusy = true;
            else if (_penaltySawBusy || Time.realtimeSinceStartup - _penaltyActivatedAt > 1.5f) ClearPenaltyContext();
        }

        if (_reactionActionContextActive)
        {
            if (skillBusy) _reactionActionSawBusy = true;
            else if (_reactionActionSawBusy || Time.realtimeSinceStartup - _reactionActionActivatedAt > 1.5f) ClearReactionActionContext();
        }

        // Process completed action events before watch housekeeping. A final action can end an
        // opposing phase in the same frame, and its valid Overwatch reaction must not be lost.
        ProcessPendingActionTriggers();
        CleanupInvalidWatches();
        ObserveAI(manager);
        ProcessReaction();

        if (Time.realtimeSinceStartup >= _nextMarkerRefresh)
        {
            _nextMarkerRefresh = Time.realtimeSinceStartup + 0.12f;
            RefreshNativeHudMarkers();
        }

        if (Time.realtimeSinceStartup >= _nextRefresh)
        {
            _nextRefresh = Time.realtimeSinceStartup + 0.2f;
            RefreshSelection(manager);
        }
    }

    private static UITacticalHUD? GetHUD()
    {
        try { return TacticalState.Get()?.GetUI()?.GetHUD(); }
        catch { return null; }
    }

    private void RefreshNativeHudMarkers()
    {
        if (!_showWorldMarkers)
        {
            ClearNativeHudMarkers();
            return;
        }

        var wanted = new HashSet<long>();
        foreach (var kv in _watches)
        {
            var w = kv.Value;
            long shooterKey = Native.Key(w.Shooter);
            if (shooterKey == 0 || w.RemainingReactions <= 0 || !Native.IsCombatReady(w.Shooter)) continue;

            // Never leak hidden enemy information through the indicator. Friendly watches
            // remain visible normally; hostile watches only appear while their actor/model
            // is actually visible to the player.
            if (!w.PlayerSide && !Native.IsVisibleToPlayer(w.Shooter)) continue;

            wanted.Add(shooterKey);
            int reactions = Math.Max(1, w.RemainingReactions);
            if (!TryRefreshNativeHudMarker(w, reactions))
                RemoveNativeHudMarker(shooterKey);
        }

        foreach (long key in _nativeHudMarkers.Keys.ToList())
        {
            if (!wanted.Contains(key)) RemoveNativeHudMarker(key);
        }
    }

    private bool TryRefreshNativeHudMarker(Watch w, int reactions)
    {
        long shooterKey = Native.Key(w.Shooter);
        var actorHud = FindActorUnitHud(w.Shooter);
        if (!Native.Valid(actorHud)) return false;

        try
        {
            // MENACE's overhead UnitHUD already owns the hitpoint bar and follows the unit
            // correctly through camera pans/zoom.  Put our tiny reaction row directly beside
            // that native bar instead of approximating screen coordinates ourselves.
            var hitpointsBar = actorHud!.m_HitpointsBar;
            var host = hitpointsBar?.parent;
            if (host == null) return false;

            long hudKey = Native.Key(actorHud);
            NativeHudMarker? marker = null;
            if (_nativeHudMarkers.TryGetValue(shooterKey, out var existing)
                && existing.HudKey == hudKey)
            {
                marker = existing;
            }
            else
            {
                RemoveNativeHudMarker(shooterKey);

                // Reuse a row if this pooled HUD already contains one from an earlier bind.
                VisualElement? strip = null;
                for (int i = 0; i < host.childCount; i++)
                {
                    var child = host.ElementAt(i);
                    if (child != null && child.name == "OverwatchReticles")
                    {
                        strip = child;
                        break;
                    }
                }

                if (strip == null)
                {
                    strip = new VisualElement();
                    strip.name = "OverwatchReticles";
                    strip.pickingMode = PickingMode.Ignore;
                    strip.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
                    strip.style.justifyContent = new StyleEnum<Justify>(Justify.FlexStart);
                    strip.style.alignSelf = new StyleEnum<Align>(Align.FlexStart);
                    strip.style.flexShrink = 0f;
                    strip.style.marginTop = new StyleLength(1f);

                    int insertAt = host.IndexOf(hitpointsBar);
                    if (insertAt >= 0) host.Insert(insertAt + 1, strip);
                    else host.Add(strip);
                }

                marker = new NativeHudMarker
                {
                    Strip = strip,
                    HudKey = hudKey,
                    Reactions = -1,
                    PlayerSide = w.PlayerSide,
                    HighContrast = _highContrastIndicators
                };
                _nativeHudMarkers[shooterKey] = marker;
                if (_debugLogging) _debugLog($"Overwatch native HUD reticle row shown: shooter={shooterKey} side={(w.PlayerSide ? "player" : "enemy")} hud={hudKey}.");
            }

            if (marker.Reactions != reactions || marker.PlayerSide != w.PlayerSide || marker.HighContrast != _highContrastIndicators)
            {
                RebuildNativeHudReticles(marker.Strip, w.PlayerSide, reactions);
                marker.Reactions = reactions;
                marker.PlayerSide = w.PlayerSide;
                marker.HighContrast = _highContrastIndicators;
            }

            return true;
        }
        catch (Exception e)
        {
            if (!_nativeHudMarkers.ContainsKey(shooterKey))
                if (_debugLogging) _debugLog("Overwatch native HUD reticles unavailable: " + e.Message);
            return false;
        }
    }

    private void RebuildNativeHudReticles(VisualElement strip, bool playerSide, int reactions)
    {
        strip.Clear();
        int displayCount = Math.Clamp(reactions, 1, 8);
        const int nativeSize = 11;
        const int gap = 1;
        var sprite = GetSingleReticleSprite(playerSide, _highContrastIndicators);
        if (sprite == null) return;

        for (int i = 0; i < displayCount; i++)
        {
            var reticle = new VisualElement();
            reticle.name = "OverwatchReticle";
            reticle.pickingMode = PickingMode.Ignore;
            reticle.style.width = new StyleLength(nativeSize);
            reticle.style.height = new StyleLength(nativeSize);
            reticle.style.flexShrink = 0f;
            reticle.style.marginRight = new StyleLength(i + 1 < displayCount ? gap : 0f);
            reticle.style.backgroundImage = new StyleBackground(sprite);
            strip.Add(reticle);
        }
    }

    private UnitHUD? FindActorUnitHud(Actor actor)
    {
        if (!Native.Valid(actor)) return null;

        try
        {
            var hud = GetHUD();
            var hudList = hud?.m_HUDList;
            int count = hudList?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var unitHud = hudList![i]?.TryCast<UnitHUD>();
                if (unitHud == null) continue;
                var owner = unitHud.GetActor();
                if (Native.Same(owner, actor)) return unitHud;
            }
        }
        catch (Exception e)
        {
            if (_debugLogging) _debugLog("Overwatch native HUD lookup failed: " + e.Message);
        }

        return null;
    }

    private void RemoveNativeHudMarker(long shooterKey)
    {
        if (!_nativeHudMarkers.TryGetValue(shooterKey, out var marker)) return;
        try { marker.Strip.RemoveFromHierarchy(); }
        catch { }
        _nativeHudMarkers.Remove(shooterKey);
    }

    private void ClearNativeHudMarkers()
    {
        foreach (long key in _nativeHudMarkers.Keys.ToList()) RemoveNativeHudMarker(key);
        _nativeHudMarkers.Clear();
    }

    private Sprite? GetSingleReticleSprite(bool playerSide, bool highContrast)
    {
        string key = (playerSide ? "P-single" : "E-single") + (highContrast ? "-HC" : "-standard");
        if (_markerSprites.TryGetValue(key, out var cached) && cached != null) return cached;

        try
        {
            const int texSize = 32;
            var texture = new Texture2D(texSize, texSize);
            ClearTexture(texture);
            DrawReticle(texture, 16, 16, playerSide, highContrast);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texSize, texSize), new Vector2(0.5f, 0.5f));
            _markerSprites[key] = sprite;
            return sprite;
        }
        catch (Exception e)
        {
            if (_debugLogging) _debugLog("Overwatch single-reticle sprite creation failed: " + e.Message);
            return null;
        }
    }

    private static void ClearTexture(Texture2D texture)
    {
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
    }

    private static void DrawReticle(Texture2D texture, int cx, int cy, bool playerSide, bool highContrast)
    {
        Color accent = playerSide
            ? new Color(0.08f, 0.72f, 0.90f, 1f)
            : new Color(0.82f, 0.12f, 0.08f, 1f);
        Color dark = new Color(0.005f, 0.005f, 0.007f, 1f);

        if (!highContrast)
        {
            // Original compact cyan/red style with a dark outline.
            DrawRing(texture, cx, cy, 9, 3, dark);
            DrawRing(texture, cx, cy, 9, 1, accent);
            DrawRect(texture, cx - 2, cy - 15, cx + 2, cy - 8, dark);
            DrawRect(texture, cx - 2, cy + 8, cx + 2, cy + 15, dark);
            DrawRect(texture, cx - 15, cy - 2, cx - 8, cy + 2, dark);
            DrawRect(texture, cx + 8, cy - 2, cx + 15, cy + 2, dark);
            DrawRect(texture, cx, cy - 14, cx, cy - 9, accent);
            DrawRect(texture, cx, cy + 9, cx, cy + 14, accent);
            DrawRect(texture, cx - 14, cy, cx - 9, cy, accent);
            DrawRect(texture, cx + 9, cy, cx + 14, cy, accent);
            DrawDisk(texture, cx, cy, 2, dark);
            DrawDisk(texture, cx, cy, 1, accent);
            return;
        }

        // High-contrast mode deliberately makes the dark silhouette the dominant part of
        // the 11px HUD icon. MENACE may dim the whole UnitHUD after a unit acts; because
        // parent opacity multiplies child opacity, the reticle cannot opt out of that fade.
        // A near-black, thicker silhouette remains legible on bright terrain even at the
        // native spent-unit opacity, while the small faction-coloured accents preserve side.
        DrawRing(texture, cx, cy, 10, 5, dark);
        DrawRing(texture, cx, cy, 8, 1, accent);

        DrawRect(texture, cx - 3, cy - 15, cx + 3, cy - 8, dark);
        DrawRect(texture, cx - 3, cy + 8, cx + 3, cy + 15, dark);
        DrawRect(texture, cx - 15, cy - 3, cx - 8, cy + 3, dark);
        DrawRect(texture, cx + 8, cy - 3, cx + 15, cy + 3, dark);

        DrawRect(texture, cx, cy - 14, cx, cy - 10, accent);
        DrawRect(texture, cx, cy + 10, cx, cy + 14, accent);
        DrawRect(texture, cx - 14, cy, cx - 10, cy, accent);
        DrawRect(texture, cx + 10, cy, cx + 14, cy, accent);

        DrawDisk(texture, cx, cy, 3, dark);
        DrawDisk(texture, cx, cy, 1, accent);
    }

    private static void DrawRing(Texture2D texture, int cx, int cy, int radius, int thickness, Color color)
    {
        int outer2 = radius * radius;
        int inner = Math.Max(0, radius - thickness);
        int inner2 = inner * inner;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) continue;
                int dx = x - cx, dy = y - cy;
                int d2 = dx * dx + dy * dy;
                if (d2 <= outer2 && d2 >= inner2) texture.SetPixel(x, y, color);
            }
        }
    }

    private static void DrawDisk(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        int r2 = radius * radius;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) texture.SetPixel(x, y, color);
            }
        }
    }

    private static void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
    {
        minX = Math.Max(0, minX); minY = Math.Max(0, minY);
        maxX = Math.Min(texture.width - 1, maxX); maxY = Math.Min(texture.height - 1, maxY);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                texture.SetPixel(x, y, color);
    }

    private void RefreshSelection(TacticalManager manager)
    {
        if (!manager.IsPlayerTurn()) { _reason = "Available during your turn."; return; }
        var actor = manager.GetActiveActor();
        if (!Native.Same(actor, _selected))
        {
            RememberSelection();
            _selected = actor;
            _squad = _leader = _vehicle = _alphaStrike = null;
            LoadSelection(actor);
        }
        if (!Native.Valid(actor) || !Native.IsPlayerControlled(actor))
        { _reason = "Select a player infantry squad or vehicle."; return; }

        bool infantry = Native.IsInfantryActor(actor);
        bool vehicle = Native.IsVehicleActor(actor);
        if (!infantry && !vehicle) { _reason = "Selected actor does not support Overwatch."; return; }
        if (vehicle && !_vehicleEnabled) { _reason = "Vehicle Overwatch is disabled in Mod Settings."; return; }

        Watch? carriedWatch = null;
        if (Armed)
        {
            var w = _watches[Native.Key(actor)];
            if (!CarriedIntoFreshActivation)
            {
                _reason = $"Overwatch armed: {w.RemainingReactions}/{w.InitialReactions} reaction(s) remaining.";
                return;
            }

            // This is a watch carried from an earlier activation.  Keep it alive while the
            // player merely selects/cycles the unit, but refresh the current weapon plan so
            // the player may deliberately replace it with a fresh Overwatch reservation.
            carriedWatch = w;
        }

        if (vehicle)
        {
            var vehicleAttacks = Native.VehicleAttacks(actor, false);
            var alphaAttacks = Native.VehicleAttacks(actor, true);
            if (vehicleAttacks.Count > 0)
            {
                _vehicleIndex = Math.Clamp(_vehicleIndex, 0, vehicleAttacks.Count - 1);
                _vehicle = vehicleAttacks[_vehicleIndex];
            }
            else _vehicle = null;
            _alphaStrike = alphaAttacks.FirstOrDefault(s => Native.CanArm(actor, s, out _)) ?? alphaAttacks.FirstOrDefault();
            _squad = _leader = null;

            if (_mode != OverwatchMode.Vehicle && _mode != OverwatchMode.AlphaStrike) _mode = OverwatchMode.Vehicle;
            if (_mode == OverwatchMode.Vehicle && !HasVehicle) _mode = HasAlphaStrike ? OverwatchMode.AlphaStrike : OverwatchMode.Vehicle;
            if (_mode == OverwatchMode.AlphaStrike && !HasAlphaStrike) _mode = HasVehicle ? OverwatchMode.Vehicle : OverwatchMode.AlphaStrike;

            if (!HasVehicle && !HasAlphaStrike)
            {
                _reason = "No supported ranged vehicle attack available.";
                return;
            }
            RememberSelection();
            RefreshPlanReason(actor, carriedWatch);
            return;
        }

        var squads = Native.Attacks(actor, WeaponRole.Squad);
        var leaders = Native.Attacks(actor, WeaponRole.Leader);
        if (squads.Count > 0)
        {
            _squadIndex = Math.Clamp(_squadIndex, 0, squads.Count - 1);
            _squad = squads[_squadIndex];
        }
        else _squad = null;
        if (leaders.Count > 0)
        {
            _leaderIndex = Math.Clamp(_leaderIndex, 0, leaders.Count - 1);
            _leader = leaders[_leaderIndex];
        }
        else _leader = null;
        _vehicle = _alphaStrike = null;

        if (_mode != OverwatchMode.Squad && _mode != OverwatchMode.Leader && _mode != OverwatchMode.Both) _mode = OverwatchMode.Squad;
        if (_mode == OverwatchMode.Squad && !HasSquad) _mode = HasLeader ? OverwatchMode.Leader : OverwatchMode.Squad;
        if (_mode == OverwatchMode.Leader && !HasLeader) _mode = HasSquad ? OverwatchMode.Squad : OverwatchMode.Leader;
        if (_mode == OverwatchMode.Both && !BothAvailable) _mode = HasSquad ? OverwatchMode.Squad : OverwatchMode.Leader;

        if (!HasSquad && !HasLeader) { _reason = "No supported infantry/special ranged attack available."; return; }
        RememberSelection();
        RefreshPlanReason(actor, carriedWatch);
    }

    private void RefreshPlanReason(Actor actor, Watch? carriedWatch)
    {
        bool validPlan = BuildPlan(actor, out _, out _, out _, out int reactions, out string planReason);
        if (carriedWatch == null)
        {
            _reason = planReason;
            return;
        }

        if (validPlan)
        {
            _reason = $"Overwatch carried: {carriedWatch.RemainingReactions}/{carriedWatch.InitialReactions} reaction(s) remain. " +
                      $"Re-arm for {reactions} reaction(s) to replace it and end this unit's turn.";
        }
        else
        {
            _reason = $"Overwatch carried: {carriedWatch.RemainingReactions}/{carriedWatch.InitialReactions} reaction(s) remain. " +
                      $"Re-arm unavailable: {planReason}";
        }
    }

    private Watch? SelectedCarriedWatch()
    {
        if (!CarriedIntoFreshActivation || !Native.Valid(_selected)) return null;
        return _watches.TryGetValue(Native.Key(_selected), out var watch) ? watch : null;
    }

    private void LoadSelection(Actor? actor)
    {
        _squadIndex = _leaderIndex = _vehicleIndex = 0;
        _mode = Native.Valid(actor) && Native.IsVehicleActor(actor!) ? OverwatchMode.Vehicle : OverwatchMode.Squad;
        if (!Native.Valid(actor)) return;

        if (_playerSelections.TryGetValue(Native.Key(actor!), out var saved))
        {
            _mode = saved.Mode;
            _squadIndex = Math.Max(0, saved.SquadIndex);
            _leaderIndex = Math.Max(0, saved.LeaderIndex);
            _vehicleIndex = Math.Max(0, saved.VehicleIndex);
            if (_debugLogging) _debugLog($"Restored player Overwatch selection: actor={Native.Key(actor)} mode={_mode} squadIndex={_squadIndex} leaderIndex={_leaderIndex} vehicleIndex={_vehicleIndex}.");
        }
    }

    private void RememberSelection()
    {
        if (!Native.Valid(_selected)) return;
        try
        {
            if (!Native.IsPlayerControlled(_selected!)) return;
            _playerSelections[Native.Key(_selected)] = new PlayerSelection
            {
                Mode = _mode,
                SquadIndex = Math.Max(0, _squadIndex),
                LeaderIndex = Math.Max(0, _leaderIndex),
                VehicleIndex = Math.Max(0, _vehicleIndex)
            };
        }
        catch { }
    }

    public bool CanArmCurrent
    {
        get
        {
            if (!Native.Valid(_selected)) return false;
            if (Armed && !CarriedIntoFreshActivation) return false;
            return BuildPlan(_selected!, out _, out _, out _, out _, out _);
        }
    }

    public int PlannedReactions
    {
        get
        {
            if (!Native.Valid(_selected)) return 0;
            if (Armed && !CarriedIntoFreshActivation) return 0;
            return BuildPlan(_selected!, out _, out _, out _, out int reactions, out _) ? reactions : 0;
        }
    }

    public int PlannedCost
    {
        get
        {
            if (!Native.Valid(_selected)) return 0;
            if (Armed && !CarriedIntoFreshActivation) return 0;
            return BuildPlan(_selected!, out _, out _, out int cost, out _, out _) ? cost : 0;
        }
    }

    public void SelectSquad()
    {
        if (!Native.Valid(_selected) || !CanEditCurrentPlan) return;
        var list = Native.Attacks(_selected!, WeaponRole.Squad);
        if (list.Count == 0) return;
        if (_mode == OverwatchMode.Squad) _squadIndex = (_squadIndex + 1) % list.Count;
        _mode = OverwatchMode.Squad;
        _squad = list[Math.Clamp(_squadIndex, 0, list.Count - 1)];
        RememberSelection();
        RefreshPlanReason(_selected!, SelectedCarriedWatch());
    }

    public void SelectLeader()
    {
        if (!Native.Valid(_selected) || !CanEditCurrentPlan) return;
        var list = Native.Attacks(_selected!, WeaponRole.Leader);
        if (list.Count == 0) return;
        if (_mode == OverwatchMode.Leader) _leaderIndex = (_leaderIndex + 1) % list.Count;
        _mode = OverwatchMode.Leader;
        _leader = list[Math.Clamp(_leaderIndex, 0, list.Count - 1)];
        RememberSelection();
        RefreshPlanReason(_selected!, SelectedCarriedWatch());
    }

    public void SelectBoth()
    {
        if (!CanEditCurrentPlan || !BothAvailable || !Native.Valid(_selected)) return;
        _mode = OverwatchMode.Both;
        RememberSelection();
        RefreshPlanReason(_selected!, SelectedCarriedWatch());
    }

    public void SelectVehicle()
    {
        if (!Native.Valid(_selected) || !CanEditCurrentPlan || !Native.IsVehicleActor(_selected!)) return;
        var list = Native.VehicleAttacks(_selected!, false);
        if (list.Count == 0) return;
        if (_mode == OverwatchMode.Vehicle) _vehicleIndex = (_vehicleIndex + 1) % list.Count;
        _mode = OverwatchMode.Vehicle;
        _vehicle = list[Math.Clamp(_vehicleIndex, 0, list.Count - 1)];
        RememberSelection();
        RefreshPlanReason(_selected!, SelectedCarriedWatch());
    }

    public void SelectAlphaStrike()
    {
        if (!Native.Valid(_selected) || !CanEditCurrentPlan || !Native.IsVehicleActor(_selected!) || !HasAlphaStrike) return;
        _mode = OverwatchMode.AlphaStrike;
        RememberSelection();
        RefreshPlanReason(_selected!, SelectedCarriedWatch());
    }

    public void TogglePlayer()
    {
        if (!Native.Valid(_selected)) return;
        long key = Native.Key(_selected);

        if (_watches.TryGetValue(key, out var existingWatch))
        {
            if (!CarriedIntoFreshActivation)
            {
                _reason = $"Overwatch already armed: {existingWatch.RemainingReactions}/{existingWatch.InitialReactions} reaction(s) remaining.";
                return;
            }

            // A watch carried into a fresh activation can be deliberately renewed.  Validate
            // the new plan first, then let Arm replace the dictionary entry atomically.  If
            // validation fails, the carried watch remains untouched.
            if (!BuildPlan(_selected!, out var renewedPrimary, out var renewedSecondary, out int renewedCost, out int renewedReactions, out _reason)) return;
            RememberSelection();
            int oldRemaining = existingWatch.RemainingReactions;
            int oldInitial = existingWatch.InitialReactions;
            if (Arm(_selected!, renewedPrimary!, renewedSecondary, _mode, _playerThreshold, _aimPenalty, true, renewedCost, renewedReactions))
            {
                _reason = $"Overwatch re-armed with {renewedReactions} reaction(s).";
                if (_debugLogging) _debugLog($"Overwatch renewed: shooter={key} oldRemaining={oldRemaining}/{oldInitial} newReactions={renewedReactions} mode={_mode}.");
            }
            return;
        }

        if (!BuildPlan(_selected!, out var primary, out var secondary, out int cost, out int reactions, out _reason)) return;
        RememberSelection();
        Arm(_selected!, primary!, secondary, _mode, _playerThreshold, _aimPenalty, true, cost, reactions);
    }

    private bool BuildPlan(Actor actor, out Skill? primary, out Skill? secondary, out int cost, out int reactions, out string reason)
    {
        primary = null; secondary = null; cost = reactions = 0; reason = "Ready.";
        if (!Native.Valid(actor)) { reason = "No selected unit."; return false; }

        switch (_mode)
        {
            case OverwatchMode.Squad:
                primary = _squad;
                break;
            case OverwatchMode.Leader:
                primary = _leader;
                break;
            case OverwatchMode.Both:
                if (!_coordinatedFireInstalled) { reason = "Both requires Coordinated Fire to be installed."; return false; }
                primary = _squad;
                secondary = _leader;
                break;
            case OverwatchMode.Vehicle:
                if (!_vehicleEnabled || !Native.IsVehicleActor(actor)) { reason = "Vehicle Overwatch is disabled or unavailable."; return false; }
                primary = _vehicle;
                break;
            case OverwatchMode.AlphaStrike:
                if (!_vehicleEnabled || !Native.IsVehicleActor(actor)) { reason = "Vehicle Overwatch is disabled or unavailable."; return false; }
                primary = _alphaStrike;
                if (!Native.Valid(primary)) { reason = "No Alpha Strike action is currently available on this vehicle."; return false; }
                break;
        }

        if (!Native.Valid(primary)) { reason = "Selected Overwatch weapon is unavailable."; return false; }
        if (!Native.CanArm(actor, primary!, out reason)) return false;
        if (Native.Valid(secondary) && !Native.CanArm(actor, secondary!, out reason)) return false;

        cost = primary!.GetActionPointCost();
        if (Native.Valid(secondary)) cost = Math.Max(cost, secondary!.GetActionPointCost());
        if (cost <= 0) { reason = "Overwatch requires a positive AP attack cost."; return false; }

        reactions = actor.GetActionPoints() / cost;
        if (_mode == OverwatchMode.Both && Native.Valid(secondary))
        {
            // A BOTH reaction may fire either weapon or both. Do not let the weapon with less
            // ammo prematurely cap the whole watch; continue while at least one selected weapon
            // can still supply a reaction.
            int ammoBound = Math.Max(Native.AmmoBound(primary), Native.AmmoBound(secondary!));
            reactions = Math.Min(reactions, ammoBound);
        }
        else
        {
            reactions = Math.Min(reactions, Native.AmmoBound(primary));
        }
        if (reactions <= 0) { reason = $"Not enough AP/ammo for one {cost} AP reaction."; return false; }

        string modeText = _mode switch
        {
            OverwatchMode.Squad => "infantry weapon",
            OverwatchMode.Leader => "special weapon",
            OverwatchMode.Both => "both infantry weapons",
            OverwatchMode.AlphaStrike => "Alpha Strike",
            _ => "vehicle weapon"
        };
        reason = $"{modeText}: {reactions} reaction(s) from {actor.GetActionPoints()} AP; arming ends this unit's turn.";
        return true;
    }

    private bool Arm(Actor actor, Skill primary, Skill? secondary, OverwatchMode mode, int threshold,
        int aimPenalty, bool playerSide, int cost, int reactions, bool aiReservation = false)
    {
        int validationAp = Math.Max(0, actor.GetActionPoints());
        string why;
        bool primaryOk = aiReservation
            ? Native.CanReserveAI(actor, primary, validationAp, out why)
            : Native.CanArm(actor, primary, out why);
        if (!primaryOk) { _reason = why; return false; }
        if (Native.Valid(secondary))
        {
            bool secondaryOk = aiReservation
                ? Native.CanReserveAI(actor, secondary!, validationAp, out why)
                : Native.CanArm(actor, secondary!, out why);
            if (!secondaryOk) { _reason = why; return false; }
        }

        int apBefore = actor.GetActionPoints();
        _watches[Native.Key(actor)] = new Watch
        {
            Shooter = actor,
            Primary = primary,
            Secondary = secondary,
            Mode = mode,
            Threshold = Math.Clamp(threshold, 1, 100),
            AimPenalty = Math.Max(0, aimPenalty),
            PlayerSide = playerSide,
            CostPerReaction = cost,
            RemainingReactions = reactions,
            InitialReactions = reactions
        };

        // Player Overwatch commits all remaining AP immediately. AI Overwatch is reserved only
        // after MENACE has already ended/switched that AI activation, so mutating the inactive
        // actor's AP/state here is unnecessary and risks perturbing native AI turn finalization.
        if (!aiReservation)
        {
            actor.SetActionPoints(0, true);
            actor.UpdateActorState();
            actor.GetSkills().ScheduleUpdate();
            actor.GetSkills().UiNeedsUpdate = true;
        }

        _reason = $"Overwatch armed with {reactions} reaction(s).";
        string actorType = Native.IsVehicleActor(actor) ? "vehicle" : "infantry";
        string apState = aiReservation ? $"{apBefore} reserved-at-end" : $"{apBefore}->0";
        if (_debugLogging) _debugLog($"Overwatch armed: shooter={Native.Key(actor)} actorType={actorType} mode={mode} primary={primary.GetID()} secondary={(Native.Valid(secondary) ? secondary!.GetID() : "-")} alphaStrike={Native.IsAlphaStrikeSkill(primary)} AP={apState} costPerReaction={cost} reactions={reactions} threshold={Math.Clamp(threshold, 1, 100)}% aimPenalty={Math.Max(0, aimPenalty)} playerSide={playerSide}.");

        if (playerSide)
        {
            try
            {
                if (_debugLogging) _debugLog("Overwatch ending player actor through TacticalState.EndTurn after committing all remaining AP.");
                TacticalState.Get().EndTurn();
            }
            catch (Exception e) { _warningLog("Could not end the player actor cleanly: " + e); }
        }
        return true;
    }

    private void ObserveAI(TacticalManager manager)
    {
        if (!_aiEnabled)
        {
            _lastAiActor = null;
            _lastAiFinalized = false;
            return;
        }

        // The old code evaluated AI Overwatch only after an actor had already become
        // turn-done/inactive, then reused the player-turn usability check. MENACE rejects that
        // check for an inactive actor, so AI Overwatch could never arm. Finalize each AI actor
        // exactly once and use the dedicated end-of-activation reservation validation below.
        if (manager.IsPlayerTurn())
        {
            if (Native.Valid(_lastAiActor) && !_lastAiFinalized)
            {
                TryArmAI(_lastAiActor!, "AI phase ended");
                _lastAiFinalized = true;
            }
            _lastAiActor = null;
            _lastAiFinalized = false;
            return;
        }

        var current = manager.GetActiveActor();
        if (!Native.Valid(_lastAiActor))
        {
            _lastAiActor = current;
            _lastAiFinalized = false;
        }
        else if (!Native.Same(current, _lastAiActor))
        {
            if (!_lastAiFinalized)
                TryArmAI(_lastAiActor!, "active actor changed");
            _lastAiActor = current;
            _lastAiFinalized = false;
        }

        if (Native.Valid(_lastAiActor) && !_lastAiFinalized && _lastAiActor!.IsTurnDone())
        {
            TryArmAI(_lastAiActor!, "actor marked turn-done");
            _lastAiFinalized = true;
        }
    }

    private void TryArmAI(Actor actor, string trigger)
    {
        if (!Native.Valid(actor) || Native.IsPlayerControlled(actor) || _watches.ContainsKey(Native.Key(actor))) return;

        // Only hostile factions should reserve AI Overwatch. Civilians, Player AI and Allied
        // Local Forces also take AI-controlled activations but are allies of the player.
        int faction = Native.FactionId(actor);
        if (faction >= 1 && faction <= 4) return;

        bool infantry = Native.IsInfantryActor(actor);
        bool vehicle = Native.IsVehicleActor(actor);
        int ap = Math.Max(0, actor.GetActionPoints());
        long key = Native.Key(actor);

        if (!infantry && !vehicle)
        {
            if (_debugLogging) _debugLog($"AI Overwatch skipped: actor={key} trigger={trigger} AP={ap} reason=unsupported actor type.");
            return;
        }
        if (vehicle && (!_vehicleEnabled || !_aiVehicleEnabled))
        {
            if (_debugLogging) _debugLog($"AI Overwatch skipped: actor={key} trigger={trigger} actorType=vehicle AP={ap} reason=AI vehicle Overwatch is disabled.");
            return;
        }

        var pool = vehicle
            ? Native.VehicleAttacks(actor, false, includeHidden: true)
                .Concat(_alphaStrikeInstalled ? Native.VehicleAttacks(actor, true, includeHidden: true) : Enumerable.Empty<Skill>())
                .ToList()
            : Native.Attacks(actor, WeaponRole.Squad)
                .Concat(Native.Attacks(actor, WeaponRole.Leader))
                .ToList();

        var attacks = pool
            .Where(s => Native.CanReserveAI(actor, s, ap, out _))
            .OrderByDescending(s => s.GetMaxRange())
            .ThenBy(s => s.GetActionPointCost())
            .ToList();

        if (attacks.Count == 0)
        {
            string why = pool.Count == 0 ? "no supported ranged attacks" : "no attack can be reserved with the remaining AP/ammo";
            if (pool.Count > 0 && !Native.CanReserveAI(actor, pool[0], ap, out string detail)) why += $" ({detail})";
            if (_debugLogging) _debugLog($"AI Overwatch skipped: actor={key} trigger={trigger} actorType={(vehicle ? "vehicle" : "infantry")} AP={ap} candidates={pool.Count} reason={why}.");
            return;
        }

        var skill = attacks[0];
        int cost = skill.GetActionPointCost();
        int reactions = Math.Min(ap / Math.Max(1, cost), Native.AmmoBound(skill));
        if (reactions <= 0)
        {
            if (_debugLogging) _debugLog($"AI Overwatch skipped: actor={key} trigger={trigger} AP={ap} skill={skill.GetID()} cost={cost} reason=no reaction budget.");
            return;
        }

        OverwatchMode mode = vehicle
            ? (Native.IsAlphaStrikeSkill(skill) ? OverwatchMode.AlphaStrike : OverwatchMode.Vehicle)
            : (Native.Role(skill) == WeaponRole.Leader ? OverwatchMode.Leader : OverwatchMode.Squad);

        if (Arm(actor, skill, null, mode, _aiThreshold, _aimPenalty, false, cost, reactions, aiReservation: true))
            if (_debugLogging) _debugLog($"AI Overwatch armed: actor={key} trigger={trigger} actorType={(vehicle ? "vehicle" : "infantry")} skill={skill.GetID()} alphaStrike={Native.IsAlphaStrikeSkill(skill)} cost={cost} reactions={reactions} threshold={_aiThreshold}%.");
        else
            if (_debugLogging) _debugLog($"AI Overwatch failed to arm: actor={key} trigger={trigger} AP={ap} skill={skill.GetID()} reason={_reason}.");
    }

    private bool RemoveWatch(long shooterKey)
    {
        bool removed = _watches.Remove(shooterKey);
        if (removed) RemoveNativeHudMarker(shooterKey);
        return removed;
    }

    private void CleanupInvalidWatches()
    {
        // Watches are intentionally NOT tied to selection/activation anymore. Merely cycling
        // through units with Tab must leave Overwatch intact. A watch survives into the unit's
        // next turn until that unit commits an actual movement or active skill.
        var remove = new List<long>();
        foreach (var kv in _watches)
        {
            var w = kv.Value;
            if (!Native.Valid(w.Shooter) || !Native.IsCombatReady(w.Shooter) || w.RemainingReactions <= 0)
                remove.Add(kv.Key);
        }
        foreach (long key in remove) RemoveWatch(key);
    }

    private void ExpireWatchOnCommit(Actor actor, string reason)
    {
        if (!Native.Valid(actor)) return;
        long key = Native.Key(actor);
        if (!_watches.TryGetValue(key, out var watch)) return;

        if (watch.Pending)
        {
            watch.ExpireAfterPending = true;
            if (_debugLogging) _debugLog($"Overwatch commit detected while a reaction is pending: shooter={key} reason={reason}; watch will expire after the queued reaction resolves.");
            return;
        }

        int remaining = watch.RemainingReactions;
        if (RemoveWatch(key))
            if (_debugLogging) _debugLog($"Overwatch expired on committed action: shooter={key} reason={reason} remaining={remaining}/{watch.InitialReactions}.");
    }

    internal void OnMovementCommitted(Actor actor)
    {
        if (_disposed || !InMission || !Native.Valid(actor)) return;
        ExpireWatchOnCommit(actor, "movement committed");
    }

    internal void OnMovementFinished(Actor mover, Tile destination)
    {
        if (_disposed || !InMission || !Native.Valid(mover)) return;
        Tile finalTile = mover.GetTile();
        if (!Native.Valid(finalTile)) finalTile = destination;
        if (!Native.Valid(finalTile)) return;

        QueueReactionsForTrigger(mover, finalTile, "movement FINISH", dedupeMovementTile: true);
    }

    internal void OnSkillUse(Actor actor, Skill skill, Tile targetTile)
    {
        if (_disposed || !InMission || !Native.Valid(actor) || !Native.Valid(skill)) return;

        long actorKey = Native.Key(actor);
        bool reactionUse = _reactionActionContextActive && actorKey == _reactionActionActorKey;

        bool movementSkill;
        try
        {
            // Background/passive skills are not deliberate commitments. Active skill uses are.
            if (!skill.IsActive() || skill.IsBackgroundOnly()) return;
            movementSkill = skill.IsMovementSkill();
        }
        catch { return; }

        // A normal action by a watched unit cancels its carried Overwatch at commit time.
        // Reaction fire itself is a native Skill.Use call too, so explicitly exclude it.
        if (!reactionUse)
        {
            string committedId = "active skill";
            try { committedId = skill.GetID() ?? committedId; } catch { }
            ExpireWatchOnCommit(actor, movementSkill ? $"movement skill {committedId}" : $"skill {committedId}");
        }

        if (reactionUse || !_actionTriggersEnabled || movementSkill) return;

        // Provocation whitelist: only actual native ATTACK skills may create an action trigger.
        // Do not use a blacklist here. MENACE and other mods can add utility/transport/state
        // actions (for example vehicle passenger ejection) whose resolution mutates actors,
        // containers or turn state. Waiting for every such action and then entering reaction
        // evaluation is both semantically odd and unsafe. Movement is already handled by the
        // dedicated movement-finished trigger, so this path is deliberately attack-only.
        bool attackSkill;
        try { attackSkill = skill.IsAttack(); }
        catch { return; }
        if (!attackSkill) return;

        long skillKey = Native.Key(skill);
        string skillId = "unknown";
        try { skillId = skill.GetID() ?? "unknown"; } catch { }
        float now = Time.realtimeSinceStartup;

        if (!_pendingActionTriggers.TryGetValue(actorKey, out var pending))
        {
            pending = new PendingActionTrigger
            {
                Actor = actor,
                FirstSkillId = skillId,
                LastSkillId = skillId,
                FirstSeenAt = now,
                LastSeenAt = now
            };
            _pendingActionTriggers[actorKey] = pending;
        }

        pending.Actor = actor;
        pending.SkillKeys.Add(skillKey);
        pending.LastSkillId = skillId;
        pending.SkillUses++;
        pending.LastSeenAt = now;
        pending.CompletionSeen = false;
        if (TacticalManager.IsSkillBusy()) pending.SawBusy = true;
    }

    internal void OnAfterSkillUse(Skill skill)
    {
        if (_disposed || !_actionTriggersEnabled || !Native.Valid(skill)) return;
        long skillKey = Native.Key(skill);
        foreach (var pending in _pendingActionTriggers.Values)
        {
            if (pending.SkillKeys.Contains(skillKey))
                pending.CompletionSeen = true;
        }
    }

    private void ProcessPendingActionTriggers()
    {
        if (!_actionTriggersEnabled || _pendingActionTriggers.Count == 0) return;

        float now = Time.realtimeSinceStartup;
        bool busy = TacticalManager.IsSkillBusy();
        if (busy)
        {
            foreach (var pending in _pendingActionTriggers.Values) pending.SawBusy = true;
            return;
        }

        var remove = new List<long>();
        foreach (var kv in _pendingActionTriggers.ToList())
        {
            long actorKey = kv.Key;
            var pending = kv.Value;
            if (!Native.Valid(pending.Actor)) { remove.Add(actorKey); continue; }

            // Collapse child ATTACK skill uses from one composite attack (for example Coordinated Fire
            // or Alpha Strike) into a single provocation. Their native child Use calls are normally
            // close together, so wait briefly after the last one and until native skill resolution
            // is idle. For unusual instantaneous skills with no busy/after event, use a safe fallback.
            if (now - pending.LastSeenAt < 0.18f) continue;
            if (!pending.CompletionSeen && !pending.SawBusy && now - pending.FirstSeenAt < 0.60f) continue;

            Tile tile;
            try { tile = pending.Actor.GetTile(); }
            catch { remove.Add(actorKey); continue; }
            if (!Native.Valid(tile)) { remove.Add(actorKey); continue; }

            string detail = pending.SkillUses > 1
                ? $"ATTACK {pending.FirstSkillId}..{pending.LastSkillId} ({pending.SkillUses} native uses)"
                : $"ATTACK {pending.LastSkillId}";
            if (_debugLogging) _debugLog($"Overwatch attack trigger completed: actor={actorKey} skill={pending.LastSkillId} nativeUses={pending.SkillUses} tile={Native.Key(tile)}.");
            QueueReactionsForTrigger(pending.Actor, tile, detail, dedupeMovementTile: false);
            remove.Add(actorKey);
        }

        foreach (long key in remove) _pendingActionTriggers.Remove(key);
    }

    private void QueueReactionsForTrigger(Actor target, Tile tile, string trigger, bool dedupeMovementTile)
    {
        if (!Native.Valid(target) || !Native.Valid(tile)) return;

        long targetKey = Native.Key(target);
        long tileKey = Native.Key(tile);
        bool targetPlayer = Native.IsPlayerControlled(target);
        bool isMovement = dedupeMovementTile;

        foreach (var w in _watches.Values.ToList())
        {
            if (w.Pending || w.PlayerSide == targetPlayer || w.RemainingReactions <= 0) continue;

            // Player-controlled Overwatch is hostile-only. Civilians, Player AI and Allied Local
            // Forces are non-player-controlled actors and must never provoke player reaction fire.
            if (w.PlayerSide && Native.IsPlayerAlliedTarget(target, out int targetFaction))
            {
                if (_debugLogging) _debugLog($"Overwatch ignored player-allied target: trigger={trigger} shooter={Native.Key(w.Shooter)} target={targetKey} faction={targetFaction} tile={tileKey}.");
                continue;
            }

            if (dedupeMovementTile && w.LastReactedTileByTarget.TryGetValue(targetKey, out long previousTile) && previousTile == tileKey)
                continue;

            bool primaryEligible = EvaluateSkill(w, w.Primary, target, tile, out int primaryChance, out string primaryReason);

            int secondaryChance = -1;
            string secondaryReason = "not selected";
            bool secondaryEligible = Native.Valid(w.Secondary)
                && EvaluateSkill(w, w.Secondary!, target, tile, out secondaryChance, out secondaryReason);

            bool anyEligible = w.Mode == OverwatchMode.Both
                ? primaryEligible || secondaryEligible
                : primaryEligible;

            if (!anyEligible)
            {
                string context = isMovement ? "final-tile" : "action";
                if (w.Mode == OverwatchMode.Both && Native.Valid(w.Secondary))
                {
                    if (_debugLogging) _debugLog($"Overwatch {context} candidate rejected for BOTH: trigger={trigger} shooter={Native.Key(w.Shooter)} target={targetKey} tile={tileKey} neither weapon qualifies; primary={w.Primary.GetID()} ({primaryReason}); secondary={w.Secondary!.GetID()} ({secondaryReason}).");
                }
                else
                {
                    if (_debugLogging) _debugLog($"Overwatch {context} candidate rejected: trigger={trigger} shooter={Native.Key(w.Shooter)} target={targetKey} mode={w.Mode} skill={w.Primary.GetID()} tile={tileKey} reason={primaryReason}.");
                }
                continue;
            }

            if (w.Mode == OverwatchMode.Both && Native.Valid(w.Secondary) && (!primaryEligible || !secondaryEligible))
            {
                if (_debugLogging) _debugLog($"Overwatch BOTH graceful trigger: trigger={trigger} shooter={Native.Key(w.Shooter)} target={targetKey} tile={tileKey} primaryEligible={primaryEligible} primaryReason={primaryReason}; secondaryEligible={secondaryEligible} secondaryReason={secondaryReason}.");
            }

            w.Pending = true;
            if (dedupeMovementTile) w.LastReactedTileByTarget[targetKey] = tileKey;
            _reactions.Enqueue(new Reaction
            {
                Watch = w,
                Target = target,
                Tile = tile,
                PrimaryChance = primaryChance,
                SecondaryChance = secondaryChance,
                PrimaryEligible = primaryEligible,
                SecondaryEligible = secondaryEligible,
                Trigger = trigger,
                // Always let native movement/skill/visibility bookkeeping unwind before calling
                // Skill.Use for the reaction.
                EarliestFireAt = Time.realtimeSinceStartup + 0.08f
            });
            if (_debugLogging) _debugLog($"Overwatch queued on {trigger}: shooter={Native.Key(w.Shooter)} target={targetKey} mode={w.Mode} tile={tileKey} primaryChance={primaryChance}% secondaryChance={secondaryChance}% remaining={w.RemainingReactions} moving={target.IsMoving()}.");
        }
    }

    private bool EvaluateSkill(Watch w, Skill skill, Actor target, Tile tile, out int effectiveChance, out string reason)
    {
        effectiveChance = -1;
        if (!Native.CanReact(w.Shooter, skill, target, tile, out reason)) return false;
        if (!Native.TryHitChance(w.Shooter, skill, target, tile, w.AimPenalty, out int nativeChance, out effectiveChance, out string detail))
        { reason = "native hit chance failed: " + detail; return false; }
        if (effectiveChance < w.Threshold)
        {
            reason = $"effective hit chance {effectiveChance}% below threshold {w.Threshold}% (native={nativeChance}%); {detail}";
            return false;
        }
        reason = $"native={nativeChance}% effective={effectiveChance}%; {detail}";
        return true;
    }

    private void ProcessReaction()
    {
        if (_reactions.Count == 0 || TacticalManager.IsSkillBusy()) return;
        if (_penaltyContextActive) ClearPenaltyContext();

        var r = _reactions.Peek();
        if (Time.realtimeSinceStartup < r.EarliestFireAt) return;
        var w = r.Watch;
        if (!Native.Valid(w.Shooter) || !Native.Valid(r.Target) || !Native.Valid(r.Tile))
        { if (_debugLogging) _debugLog("Queued Overwatch reaction dropped: actor/target/final tile disappeared."); FinishReaction(r, false); return; }
        if (w.RemainingReactions <= 0)
        { FinishReaction(r, false); return; }

        Skill? skill = r.Stage == 0 ? w.Primary : w.Secondary;
        int chance = r.Stage == 0 ? r.PrimaryChance : r.SecondaryChance;
        bool queuedEligible = r.Stage == 0 ? r.PrimaryEligible : r.SecondaryEligible;
        if (!Native.Valid(skill))
        {
            FinishReaction(r, r.AnyShotAccepted);
            return;
        }

        // Each selected weapon carries its own eligibility result from movement finish. In BOTH
        // mode, skip only an ineligible weapon and still let the other eligible weapon resolve.
        // Keep this separate from the numeric chance because a below-threshold evaluation still
        // has a perfectly valid (but insufficient) positive hit chance.
        if (!queuedEligible)
        {
            if (_debugLogging) _debugLog($"Queued Overwatch weapon skipped: trigger={r.Trigger} stage={r.Stage} skill={skill!.GetID()} tile={Native.Key(r.Tile)} reason=weapon did not qualify when the trigger was evaluated.");
            if (r.Stage == 0 && Native.Valid(w.Secondary)) { r.Stage = 1; return; }
            FinishReaction(r, r.AnyShotAccepted);
            return;
        }

        // Revalidate the SAME final tile. We deliberately do not follow the target to any
        // later tile; that was the source of the early/missed first reaction in alpha.10.
        if (!Native.CanReact(w.Shooter, skill!, r.Target, r.Tile, out string reject))
        {
            if (_debugLogging) _debugLog($"Queued Overwatch shot skipped before firing: stage={r.Stage} skill={skill!.GetID()} finalTile={Native.Key(r.Tile)} reason={reject}.");
            if (r.Stage == 0 && Native.Valid(w.Secondary)) { r.Stage = 1; return; }
            FinishReaction(r, r.AnyShotAccepted);
            return;
        }

        bool ok = false;
        int beforeAmmo = skill!.GetUses();
        int apBefore = w.Shooter.GetActionPoints();
        try
        {
            ActivateReactionActionContext(w.Shooter);
            ActivatePenaltyContext(skill!, w.AimPenalty, Native.IsAlphaStrikeSkill(skill!));
            var usage = UsageParameter.Free | UsageParameter.IgnoreUsabilityCheck;
            ok = skill!.Use(r.Tile, usage);

            int afterAmmo = skill.GetUses();
            if (ok && !Native.IsAlphaStrikeSkill(skill) && skill.HasLimitedUses() && beforeAmmo > 0 && afterAmmo == beforeAmmo)
            {
                skill.SetUses(Math.Max(0, beforeAmmo - 1));
                w.Shooter.GetSkills().ScheduleUpdate();
                w.Shooter.GetSkills().UiNeedsUpdate = true;
                afterAmmo = skill.GetUses();
                if (_debugLogging) _debugLog($"Overwatch ammo reconciliation consumed one use: {beforeAmmo}->{afterAmmo}.");
            }

            if (_debugLogging) _debugLog($"Overwatch native Use returned {ok}; trigger={r.Trigger} stage={r.Stage} skill={skill.GetID()} effectiveChance={chance}% penalty={w.AimPenalty} tile={Native.Key(r.Tile)} uses={beforeAmmo}->{afterAmmo} AP={apBefore}->{w.Shooter.GetActionPoints()}.");
        }
        catch (Exception e)
        {
            ClearPenaltyContext();
            ClearReactionActionContext();
            _errorLog("Reaction fire failed: " + e);
        }

        r.AnyShotAccepted |= ok;
        if (r.Stage == 0 && Native.Valid(w.Secondary) && Native.IsCombatReady(r.Target))
        {
            r.Stage = 1;
            return;
        }
        FinishReaction(r, r.AnyShotAccepted);
    }

    private void FinishReaction(Reaction r, bool consumed)
    {
        if (_reactions.Count > 0) _reactions.Dequeue();
        r.Watch.Pending = false;
        if (r.Watch.ExpireAfterPending)
        {
            long key = Native.Key(r.Watch.Shooter);
            int remaining = r.Watch.RemainingReactions;
            RemoveWatch(key);
            if (_debugLogging) _debugLog($"Overwatch expired after pending reaction because the shooter committed another action: shooter={key} remaining={remaining}/{r.Watch.InitialReactions}.");
            return;
        }
        if (!consumed) return;

        r.Watch.RemainingReactions = Math.Max(0, r.Watch.RemainingReactions - 1);
        _reason = $"Overwatch fired. {r.Watch.RemainingReactions} reaction(s) remaining.";
        if (_debugLogging) _debugLog($"Overwatch reaction completed: shooter={Native.Key(r.Watch.Shooter)} mode={r.Watch.Mode} remaining={r.Watch.RemainingReactions}/{r.Watch.InitialReactions}.");

        bool primaryEmpty = r.Watch.Primary.HasLimitedUses() && r.Watch.Primary.GetUses() <= 0;
        bool secondaryEmpty = Native.Valid(r.Watch.Secondary) && r.Watch.Secondary!.HasLimitedUses() && r.Watch.Secondary.GetUses() <= 0;
        bool noSelectedAmmo = r.Watch.Mode == OverwatchMode.Both && Native.Valid(r.Watch.Secondary)
            ? primaryEmpty && secondaryEmpty
            : primaryEmpty;
        if (r.Watch.RemainingReactions <= 0 || noSelectedAmmo)
            RemoveWatch(Native.Key(r.Watch.Shooter));
    }

    private void ActivateReactionActionContext(Actor actor)
    {
        _reactionActionActorKey = Native.Key(actor);
        _reactionActionContextActive = _reactionActionActorKey != 0;
        _reactionActionSawBusy = false;
        _reactionActionActivatedAt = Time.realtimeSinceStartup;
    }

    private void ClearReactionActionContext()
    {
        _reactionActionActorKey = 0;
        _reactionActionContextActive = false;
        _reactionActionSawBusy = false;
        _reactionActionActivatedAt = 0f;
    }

    private void ActivatePenaltyContext(Skill skill, int amount, bool globalForComposite)
    {
        _penaltySkillKey = Native.Key(skill);
        _penaltyAmount = Math.Max(0, amount);
        _penaltyGlobal = globalForComposite;
        _penaltyContextActive = _penaltyAmount > 0 && (_penaltySkillKey != 0 || _penaltyGlobal);
        _penaltySawBusy = false;
        _penaltyActivatedAt = Time.realtimeSinceStartup;
    }

    private void ClearPenaltyContext()
    {
        _penaltyContextActive = false;
        _penaltySkillKey = 0;
        _penaltyAmount = 0;
        _penaltyGlobal = false;
        _penaltySawBusy = false;
        _penaltyActivatedAt = 0f;
    }

    internal void ApplyHitChancePenalty(Skill skill, ref HitChance result)
    {
        if (!_penaltyContextActive || _penaltyAmount <= 0) return;
        if (!_penaltyGlobal && Native.Key(skill) != _penaltySkillKey) return;
        float before = result.FinalValue;
        result.FinalValue = Math.Max(0f, before - _penaltyAmount);
    }

    public void SceneChanged() => Reset();

    private void Reset()
    {
        ClearNativeHudMarkers();
        _watches.Clear();
        _reactions.Clear();
        _pendingActionTriggers.Clear();
        _playerSelections.Clear();
        _selected = null;
        _squad = _leader = _vehicle = _alphaStrike = null;
        _lastAiActor = null;
        _lastAiFinalized = false;
        _squadIndex = _leaderIndex = _vehicleIndex = 0;
        _mode = OverwatchMode.Squad;
        ClearPenaltyContext();
        ClearReactionActionContext();
        _reason = "Select an infantry unit or vehicle.";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hooks.Owner = null;
        _harmony.UnpatchSelf();
        Reset();
    }

    private static class Hooks
    {
        internal static Runtime? Owner;

        public static void MovementStarted(Actor __0)
        {
            try { Owner?.OnMovementCommitted(__0); }
            catch (Exception e) { Owner?._errorLog("Movement-start hook error: " + e); }
        }

        public static void MovementFinished(Actor __0, Tile __1)
        {
            try { Owner?.OnMovementFinished(__0, __1); }
            catch (Exception e) { Owner?._errorLog("Movement-finished hook error: " + e); }
        }

        public static void SkillUse(Actor __0, Skill __1, Tile __2)
        {
            try { Owner?.OnSkillUse(__0, __1, __2); }
            catch (Exception e) { Owner?._errorLog("Skill-use hook error: " + e); }
        }

        public static void AfterSkillUse(Skill __0)
        {
            try { Owner?.OnAfterSkillUse(__0); }
            catch (Exception e) { Owner?._errorLog("After-skill-use hook error: " + e); }
        }

        public static void HitChancePostfix(Skill __instance, ref HitChance __result)
        {
            try { Owner?.ApplyHitChancePenalty(__instance, ref __result); }
            catch (Exception e) { Owner?._errorLog("Hit-chance penalty hook error: " + e); }
        }
    }
}
