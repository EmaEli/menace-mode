using System.Text;

namespace CoordinatedFire.Core;

public enum AIDiagnosticCounter
{
    HookCalls,
    NativeReentryCalls,
    CallsOnPlayerTurn,
    CallsOnAITurn,
    DiagnosticContextUnavailable,
    AIEvaluationAttempts,
    AIDisabled,
    RuntimeLocked,
    PlayerTargetingArmed,
    RuntimeFaulted,
    OutsideMission,
    ExistingVolleyActive,
    Settling,
    ManagerUnavailable,
    PlayerTurnGate,
    Paused,
    MissionFinished,
    ActorUnresolved,
    PlayerControlledActor,
    ChosenSkillNotOwned,
    VehicleModeDisabled,
    UnsupportedActorMode,
    UnsupportedChosenSkill,
    UnsafeUsageFlags,
    UnknownWeaponRole,
    NoCompatiblePartner,
    PairEligibilityRejected,
    ActorBusyOrMoving,
    PlayerUsageParamsRejected,
    TargetRejected,
    FinalAPGuard,
    VolleyCandidateAccepted,
    VolleyBeginAccepted,
    VolleyBeginRejected,
    FirstChildStarted,
    FirstChildCompleted,
    SecondChildStarted,
    SecondChildCompleted,
    VolleyFinishedSuccessfully,
    VolleyInterrupted,
    Count
}

/// <summary>Explicit, in-memory-only and bounded counters for a focused AI test.</summary>
public sealed class AIDiagnosticCapture
{
    public const int MaxCount = 9999;
    private readonly int[] _counts = new int[(int)AIDiagnosticCounter.Count];

    public bool IsCapturing { get; private set; }

    public void Start()
    {
        Array.Clear(_counts, 0, _counts.Length);
        IsCapturing = true;
    }

    public void Record(AIDiagnosticCounter counter)
    {
        int index = (int)counter;
        if (!IsCapturing || index < 0 || index >= _counts.Length) return;
        if (_counts[index] < MaxCount) _counts[index]++;
    }

    public int GetCount(AIDiagnosticCounter counter)
    {
        int index = (int)counter;
        return index >= 0 && index < _counts.Length ? _counts[index] : 0;
    }

    public string StopAndFormat()
    {
        bool wasCapturing = IsCapturing;
        IsCapturing = false;
        var text = new StringBuilder();
        text.AppendLine($"AI_CAPTURE captureWasActive={wasCapturing}; perCounterLimit={MaxCount}");
        for (int i = 0; i < _counts.Length; i++)
            text.Append("AI_CAPTURE ").Append((AIDiagnosticCounter)i).Append('=')
                .AppendLine(_counts[i].ToString());
        return text.ToString();
    }
}