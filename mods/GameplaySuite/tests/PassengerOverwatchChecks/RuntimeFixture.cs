using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace MenaceOverwatch;

internal enum OverwatchMode { Squad, Leader, Both, Vehicle, AlphaStrike }

// Native/UI stand-ins only. The Python harness inserts the actual production methods.
internal sealed partial class Runtime
{
    private sealed class Watch
    {
        public Actor Shooter = null!;
        public Skill Primary = null!;
        public Skill? Secondary;
        public OverwatchMode Mode;
        public int Threshold, AimPenalty, CostPerReaction, RemainingReactions, InitialReactions;
        public bool PlayerSide, Pending, ExpireAfterPending;
        public long ContainerKey;
    }
    private readonly Dictionary<long, Watch> _watches = new();
    private string _reason = "";
    private bool _debugLogging;
    private bool _disposed;
    public bool InMission = true;
    public int RemovedMarkers;
    private Action<string> _debugLog = _ => { }, _warningLog = _ => { };
    private Actor? _selected;
    private long _passengerSelectionHostKey;
    public bool SelectingPassenger => _passengerSelectionHostKey != 0;
    public bool HasPassengerChoice => PassengerOverwatch.GetPassenger(TacticalManager.Get().GetActiveActor()!) is { Player: true };
    private void RefreshSelection(TacticalManager manager) => _selected = ResolvePlayerActor(manager);
    public Actor? Selection { get { RefreshSelection(TacticalManager.Get()); return _selected; } }
    public int Count => _watches.Count;
    public int Reactions(Actor actor) => _watches[Native.Key(actor)].RemainingReactions;
    private void RemoveNativeHudMarker(long key) => RemovedMarkers++;
    public void Maintain() => CleanupInvalidWatches();
    public void MarkPending(Actor actor) => _watches[Native.Key(actor)].Pending = true;
    public bool PendingExpired(Actor actor) => _watches[Native.Key(actor)].ExpireAfterPending;
    public bool Reserve(Actor actor, Skill primary, Skill? secondary = null, bool ai = false) =>
        Arm(actor, primary, secondary, OverwatchMode.Squad, 60, 10, !ai, primary.Cost,
            primary.Cost > 0 ? actor.AP / primary.Cost : 0, ai);
}