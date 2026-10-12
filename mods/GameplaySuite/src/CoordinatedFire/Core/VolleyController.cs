using System;

namespace CoordinatedFire.Core;

public interface IVolleyPort
{
    double Now { get; }
    long Frame { get; }
    bool IsBusy { get; }
    bool SynchronizedLaunchEnabled { get; }
    double SynchronizedLaunchDelay { get; }
    bool ValidateFollowUp(out string reason);
    bool ValidateSynchronizedFollowUp(out string reason);
    bool Launch(AttackSnapshot attack, bool synchronizedFollowUp = false);
    void Finish(bool charge, string reason, bool success);
}

public enum VolleyPhase { Idle, First, Second, Concurrent, Draining }

/// <summary>
/// Event-driven orchestration only. The adapter owns native objects and a reserved AP fee.
/// Default behavior is fully sequential and unchanged. Optional synchronized launch mode
/// makes one guarded attempt to start the second native attack after the first has begun.
/// If MENACE rejects that early launch cleanly, the controller falls back to the proven
/// sequential path. Completion always waits for native completion evidence and a non-busy
/// container before the transaction is settled.
/// </summary>
public sealed class VolleyController
{
    private readonly IVolleyPort _port;
    private readonly double _timeout;
    private VolleyPlan? _plan;

    // Current sequential child state (also used for the first child before a sync attempt).
    private bool _started, _completed, _accepted, _anyAccepted;
    private long _current, _frame;
    private double _deadline;
    private string _abort = "";

    // Optional synchronized second-child state.
    private bool _syncAttempted, _syncSecondLaunched, _syncSecondStarted, _syncSecondCompleted, _syncSecondAccepted;
    private bool _drainWaitForFirstCompletion;
    private double _syncDue;

    public VolleyPhase Phase { get; private set; }
    public bool IsActive => Phase != VolleyPhase.Idle;
    public long CurrentSkill => _current;

    public VolleyController(IVolleyPort port, double timeout)
    {
        _port = port;
        if (!double.IsFinite(timeout) || timeout <= 0) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
    }

    public bool Begin(VolleyPlan plan)
    {
        if (IsActive) return false;
        _plan = plan;
        _anyAccepted = false;
        _abort = "";
        _syncAttempted = _syncSecondLaunched = _syncSecondStarted = _syncSecondCompleted = _syncSecondAccepted = false;
        _drainWaitForFirstCompletion = false;
        _syncDue = 0;
        StartSequential(plan.First, VolleyPhase.First);
        return _accepted || _started || _completed;
    }

    private void StartSequential(AttackSnapshot attack, VolleyPhase phase)
    {
        Phase = phase;
        _current = attack.SkillKey;
        _started = _completed = _accepted = false;
        _frame = _port.Frame;
        _deadline = _port.Now + _timeout;
        try
        {
            // Events may arrive synchronously inside Launch. Do not reset them afterwards.
            bool result = _port.Launch(attack, synchronizedFollowUp: false);
            _accepted = result || _started || _completed;
            _anyAccepted |= _accepted;
            if (!result) Cancel("Native attack rejected; no follow-up was forced.");
        }
        catch (Exception e)
        {
            // Launch may have spent ammo before throwing. Conservatively charge the fee;
            // never retry an attack whose outcome is unknown.
            _accepted = _anyAccepted = true;
            Cancel("Native call failed; attack outcome is uncertain: " + e.Message);
        }
    }

    private void TryStartSynchronizedSecond()
    {
        if (_plan == null || _syncAttempted || Phase != VolleyPhase.First) return;
        _syncAttempted = true;

        if (!_port.ValidateSynchronizedFollowUp(out var reason))
        {
            // A real game-state change (target died, weapon invalid, etc.) means there is
            // no legitimate follow-up to fire. Drain the first child safely.
            // We already have explicit native-start evidence for the first child. Even if
            // MENACE reports no busy flag on this exact frame, do not release the transaction
            // until that first native action has completed (or the normal safety timeout
            // expires). This prevents AP/UI cleanup racing a still-running native attack.
            _drainWaitForFirstCompletion = true;
            Cancel("Follow-up cancelled: " + reason);
            return;
        }

        _syncSecondLaunched = true;
        _syncSecondStarted = _syncSecondCompleted = _syncSecondAccepted = false;
        try
        {
            bool result = _port.Launch(_plan.Second, synchronizedFollowUp: true);
            _syncSecondAccepted = result || _syncSecondStarted || _syncSecondCompleted;
            _anyAccepted |= _syncSecondAccepted;

            // The adapter may have detected partial execution (for example ammo spent
            // despite a false return) and moved us to Draining while Launch was on-stack.
            // Never overwrite that conservative cancellation state.
            if (Phase == VolleyPhase.Draining) return;

            if (_syncSecondAccepted)
            {
                // Both children now belong to this transaction. Do not settle until both
                // native completion events have arrived and MENACE reports no skill busy.
                Phase = VolleyPhase.Concurrent;
                _deadline = _port.Now + _timeout;
                _current = _plan.Second.SkillKey;
            }
            else
            {
                // Clean rejection means MENACE did not accept overlapping Skill.Use. This
                // is expected on builds/skills that do not tolerate it; keep the first child
                // running and fall back to the original sequential follow-up after it ends.
                _syncSecondLaunched = false;
                _current = _plan.First.SkillKey;
            }
        }
        catch (Exception e)
        {
            // An exception during the overlapping attempt is not safe to retry because the
            // second child may have partially executed. Drain and charge conservatively.
            _syncSecondAccepted = _anyAccepted = true;
            Cancel("Synchronized native call failed; attack outcome is uncertain: " + e.Message);
        }
    }

    public void NativeStarted(long skill)
    {
        if (!IsActive || _plan == null) return;

        if (_syncSecondLaunched && skill == _plan.Second.SkillKey)
        {
            _syncSecondStarted = _syncSecondAccepted = _anyAccepted = true;
            return;
        }

        if (skill != _plan.First.SkillKey && !(Phase == VolleyPhase.Second && skill == _current)) return;
        if (skill == _current)
        {
            _started = _anyAccepted = true;
            if (Phase == VolleyPhase.First && _port.SynchronizedLaunchEnabled && !_syncAttempted)
                _syncDue = _port.Now + Math.Max(0, _port.SynchronizedLaunchDelay);
        }
    }

    public void NativeCompleted(long skill)
    {
        if (!IsActive || _plan == null) return;

        if (_syncSecondLaunched && skill == _plan.Second.SkillKey)
        {
            _syncSecondCompleted = _syncSecondAccepted = _anyAccepted = true;
            return;
        }

        if (skill == _plan.First.SkillKey && Phase is VolleyPhase.First or VolleyPhase.Concurrent or VolleyPhase.Draining)
        {
            _completed = _anyAccepted = true;
            return;
        }

        if (Phase == VolleyPhase.Second && skill == _current)
            _completed = _anyAccepted = true;
    }

    public void Cancel(string reason)
    {
        if (!IsActive) return;
        if (_abort.Length == 0) _abort = reason;
        Phase = VolleyPhase.Draining;
        // Never tear down an AP exemption while a native attack is still busy.
        // A scene transition explicitly abandons all references instead.
    }

    public void Tick()
    {
        if (!IsActive || _port.Frame <= _frame) return;

        if (Phase != VolleyPhase.Draining && _port.Now >= _deadline)
            Cancel("Native completion timed out. Follow-up cancelled; waiting for the current attack to finish.");

        // Safest synchronized mode: only manual/player transactions opt into this via the
        // adapter. Wait for explicit native start evidence, then use a small vanilla-like
        // stagger. If the early second Use is rejected cleanly, nothing is forced.
        if (Phase == VolleyPhase.First && _port.SynchronizedLaunchEnabled && _started && !_syncAttempted
            && _port.Now >= _syncDue)
            TryStartSynchronizedSecond();

        if (Phase == VolleyPhase.Draining)
        {
            if (_port.IsBusy) return;
            if (_drainWaitForFirstCompletion && !_completed && _port.Now < _deadline) return;
            Complete(false, _abort);
            return;
        }

        if (Phase == VolleyPhase.Concurrent)
        {
            if (_port.IsBusy) return;
            if (!_accepted || !_completed || !_syncSecondAccepted || !_syncSecondCompleted) return;
            Complete(true, "Both native attacks completed (synchronized launch).");
            return;
        }

        if (_port.IsBusy) return;
        if (!_accepted || !_completed) return;

        if (Phase == VolleyPhase.First)
        {
            if (!_port.ValidateFollowUp(out var reason))
            {
                Complete(false, "Follow-up cancelled: " + reason);
                return;
            }
            StartSequential(_plan!.Second, VolleyPhase.Second);
        }
        else Complete(true, "Both native attacks completed.");
    }

    private void Complete(bool success, string reason)
    {
        bool charge = _anyAccepted;
        // Clear first: Finish may release a deferred round check and re-enter patched code.
        Reset();
        _port.Finish(charge, reason, success);
    }

    public void AbandonScene() => Reset();

    private void Reset()
    {
        Phase = VolleyPhase.Idle;
        _plan = null;
        _current = 0;
        _started = _completed = _accepted = _anyAccepted = false;
        _syncAttempted = _syncSecondLaunched = _syncSecondStarted = _syncSecondCompleted = _syncSecondAccepted = false;
        _drainWaitForFirstCompletion = false;
        _syncDue = 0;
        _abort = "";
    }
}
