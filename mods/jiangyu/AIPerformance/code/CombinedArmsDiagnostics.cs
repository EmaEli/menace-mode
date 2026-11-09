using System.Collections.Generic;

namespace AIPerformance;

internal enum SupportSnapshotReason
{
    Eligible,
    MissingAgent,
    MissingActor,
    MissingRole,
    IneligibleFaction,
    RoleAvoidsOpponents,
    MovementDisabled,
    Sleeping,
    DeploymentPhase,
    Dying,
    Moving,
    Contained,
    NoActionPoints,
    Stunned,
    LowMorale,
    Suppressed,
    Xeno,
    Drone,
    MissingTile,
    UnrecognizedCrewedVehicle,
    NotInfantry,
    MachineInfantry,
    IndirectFire,
    MissingZones,
    InvalidOrders,
    DisabledOrder,
    InsideAdvanceZone,
    InfantryWithoutAdvanceOrder,
    NoDirectFireWeapon,
    Count
}

internal enum SupportCandidateGate
{
    Ready,
    MapMismatch,
    FactionNotPublished,
    RoundMismatch,
    AssetNotPublished,
    AgentMismatch,
    OrderMismatch,
    AssetIsInfantry
}

internal readonly record struct SupportCandidateDiagnostics(
    SupportCandidateGate Gate, int Nearby, int Compatible, int SlotBusy,
    int Returned, int Truncated, long AssignedRequest);

// Deduplicates worker-thread diagnostics and caps their total per mission.
internal sealed class CombinedArmsDiagnosticBudget
{
    internal const int MaxLines = 256;
    private readonly object _gate = new();
    private readonly HashSet<(int Round, long Faction, long Actor, string Phase)> _seen = new();
    private int _count;

    internal bool TryAcquire(int round, long faction, long actor, string phase)
    {
        if (round < 1 || phase == null) return false;
        lock (_gate)
        {
            if (_count >= MaxLines || !_seen.Add((round, faction, actor, phase))) return false;
            _count++;
            return true;
        }
    }

    internal void Reset()
    {
        lock (_gate) { _seen.Clear(); _count = 0; }
    }
}