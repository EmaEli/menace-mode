using System.Reflection;
using CoordinatedFire.Core;
using HarmonyLib;
using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using UnityEngine;

namespace CoordinatedFire;

internal sealed class Runtime : IVolleyPort, IDisposable
{
    private readonly global::HarmonyLib.Harmony _harmony;
    private readonly Action<string> _log;
    private readonly Action<string> _error;
    private readonly VolleyController _volley;
    private bool _enableAI, _enableVehicles, _syncPlayerFire;
    private Actor? _actor, _firingActor;
    private Skill? _squad, _leader, _firingSquad, _firingLeader;
    private Tile? _target;
    private VolleyPlan? _armed, _transaction;
    private int _readDepth, _nativeDepth, _round;
    private bool _settling, _faulted, _transactionIsAI, _roundCheckDeferred;
    private bool _apDebitIntercepted, _apReconciled, _suppressFee;
    private int _launchAp, _launchCost, _clampedApGain;
    private UsageParameter _childUsage = UsageParameter.Default;
    private int _squadIndex, _leaderIndex, _vehiclePairIndex;
    private double _nextRefresh;
    private float _lastReal;
    public static Runtime? Instance { get; private set; }
    public Decision Current { get; private set; } = Decision.Deny("Select an infantry squad or enabled vehicle in a mission.");
    public string Status { get; private set; } = "Ready for a local test; runtime behavior is not certified.";
    public string SquadName => Native.Title(_squad, _actor);
    public string LeaderName => Native.Title(_leader, _actor);
    public bool AIEnabled => _enableAI;
    public bool VehicleEnabled => _enableVehicles;
    public bool SynchronizedFireEnabled => _syncPlayerFire;
    public bool SynchronizedLaunchEnabled => _syncPlayerFire && !_transactionIsAI;
    public double SynchronizedLaunchDelay => 0.10;
    public bool IsVehicleMode => _enableVehicles && Native.Valid(_actor) && _actor!.IsVehicle();
    public void SetAIEnabled(bool enabled) => _enableAI = enabled;
    public void SetSynchronizedFireEnabled(bool enabled) => _syncPlayerFire = enabled;
    public void SetVehicleEnabled(bool enabled)
    {
        if (_enableVehicles == enabled) return;
        if (!enabled && IsVehicleMode && Armed) CancelTargeting("Vehicle Coordinated Fire disabled in Mod Settings.");
        _enableVehicles = enabled;
        _nextRefresh = 0;
    }
    public bool InMission { get; private set; }
    public bool Armed => _armed != null;
    public bool IsActive => _volley.IsActive;
    public bool Locked => _volley.IsActive || _settling;
    public int ReservedCost => _transaction?.Cost ?? 0;
    public double Now { get; private set; }
    public long Frame { get; private set; }
    public bool IsBusy => Native.Valid(_firingActor) &&
        (_firingActor!.GetSkills().IsBusy() || TacticalManager.IsSkillBusy());
    private bool NativeRead => _readDepth != 0;
    private bool Preview(Skill skill) => !NativeRead && _armed != null && Native.Key(skill) == _armed.First.SkillKey;

    public Runtime(double timeout, bool enableAI, bool enableVehicles, bool synchronizedPlayerFire, Action<string> log, Action<string> error, global::HarmonyLib.Harmony harmony)
    {
        if (Instance != null) throw new InvalidOperationException("Only one Coordinated Fire runtime is allowed.");
        _log = log;
        _error = error;
        _enableAI = enableAI;
        _enableVehicles = enableVehicles;
        _syncPlayerFire = synchronizedPlayerFire;
        _harmony = harmony ?? throw new ArgumentNullException(nameof(harmony));
        _volley = new(this, timeout);
        Instance = this;
        try { InstallPatches(); }
        catch { _harmony.UnpatchSelf(); Instance = null; throw; }
        _lastReal = Time.realtimeSinceStartup;
    }

    private MethodInfo Require(Type type, string name, Type result, bool isStatic, params Type[] args)
    {
        var m = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
            (isStatic ? BindingFlags.Static : BindingFlags.Instance), null, args, null);
        if (m == null || m.ReturnType != result) throw new MissingMethodException(
            $"Unsupported game API: {type.FullName}.{name} -> {result.Name}");
        return m;
    }
    private void Patch(Type type, string name, Type result, bool isStatic, Type[] args,
        string? prefix = null, string? postfix = null)
    {
        HarmonyMethod? Method(string? n) => n == null ? null : new HarmonyMethod(
            typeof(Hooks).GetMethod(n, BindingFlags.Static | BindingFlags.Public)!
        ) { priority = Priority.Last };
        _harmony.Patch(Require(type, name, result, isStatic, args), Method(prefix), Method(postfix), null, null, null);
    }
    private void InstallPatches()
    {
        var none = Type.EmptyTypes;
        Patch(typeof(Skill), nameof(Skill.Use), typeof(bool), false,
            new[] { typeof(Tile), typeof(UsageParameter) }, prefix: nameof(Hooks.Use));
        Patch(typeof(Skill), nameof(Skill.GetMinRange), typeof(int), false, none, postfix: nameof(Hooks.MinRange));
        Patch(typeof(Skill), nameof(Skill.GetMaxRange), typeof(int), false, none, postfix: nameof(Hooks.MaxRange));
        Patch(typeof(Skill), nameof(Skill.IsUsable), typeof(bool), false, none, postfix: nameof(Hooks.Ready));
        Patch(typeof(Skill), nameof(Skill.IsUsable), typeof(bool), false,
            new[] { typeof(TargetUsageParams) }, postfix: nameof(Hooks.Ready));
        Patch(typeof(Skill), nameof(Skill.IsAffordable), typeof(bool), false, none, postfix: nameof(Hooks.Ready));
        Patch(typeof(Skill), nameof(Skill.IsUsableOn), typeof(bool), false,
            new[] { typeof(Tile), typeof(Tile) }, postfix: nameof(Hooks.Target));
        Patch(typeof(Skill), nameof(Skill.IsUsableOn), typeof(bool), false,
            new[] { typeof(Tile), typeof(Tile), typeof(TargetUsageParams) }, postfix: nameof(Hooks.Target));
        Patch(typeof(Skill), nameof(Skill.IsApplicableTo), typeof(bool), false,
            new[] { typeof(Tile), typeof(Entity) }, postfix: nameof(Hooks.Target));
        Patch(typeof(TacticalManager), nameof(TacticalManager.InvokeOnSkillUse), typeof(void), false,
            new[] { typeof(Actor), typeof(Skill), typeof(Tile) }, postfix: nameof(Hooks.Started));
        Patch(typeof(TacticalManager), nameof(TacticalManager.InvokeOnAfterSkillUse), typeof(void), false,
            new[] { typeof(Skill) }, postfix: nameof(Hooks.Completed));
        Patch(typeof(Actor), nameof(Actor.SetActionPoints), typeof(void), false,
            new[] { typeof(int), typeof(bool) }, prefix: nameof(Hooks.SetActionPoints));
        Patch(typeof(Actor), nameof(Actor.SetActionPointsUnsafe), typeof(void), false,
            new[] { typeof(int) }, prefix: nameof(Hooks.SetActionPointsUnsafe));
        Patch(typeof(TacticalManager), nameof(TacticalManager.TryCheckRound), typeof(bool), false, none,
            prefix: nameof(Hooks.Round));
        Patch(typeof(TacticalState), nameof(TacticalState.EndTurn), typeof(void), false, none,
            prefix: nameof(Hooks.EndTurn));
        Patch(typeof(TacticalState), nameof(TacticalState.TrySelectSkill), typeof(bool), false,
            new[] { typeof(Skill) }, prefix: nameof(Hooks.Select));
        Patch(typeof(TacticalState), nameof(TacticalState.HandleMouseInput), typeof(void), false, none,
            prefix: nameof(Hooks.Mouse));
        Patch(typeof(TacticalState), nameof(TacticalState.HandleKeyInput), typeof(void), false, none,
            prefix: nameof(Hooks.Keyboard));
        _log("18 native patches bound via Modpack Loader Harmony. Child attacks use native NoApConsumed so ammo/uses remain native while the max AP fee is settled once; exact AP-debit interception remains as a defensive fallback. AI conversion accepts normal/audio/instant-resolve usage flags and logs rejected AI attacks.");
    }

    public void Update()
    {
        Frame++;
        float real = Time.realtimeSinceStartup;
        double delta = Math.Max(0, real - _lastReal);
        _lastReal = real;
        InMission = TacticalManager.IsMissionRunning();
        if (!InMission)
        {
            if (IsActive || Armed || Native.Valid(_actor)) SceneChanged();
            return;
        }
        var manager = TacticalManager.Get();
        if (!Native.Valid(manager)) return;
        if (manager.IsPaused()) return; // Do not time out while a player is reading the pause menu.
        Now += delta;
        if (IsActive)
        {
            bool wrongFactionTurn = _transactionIsAI ? manager.IsPlayerTurn() : !manager.IsPlayerTurn();
            if (wrongFactionTurn || manager.IsFinished() || manager.GetRound() != _round
                || !Native.Same(manager.GetActiveActor(), _firingActor))
                _volley.Cancel("The active actor/turn changed.");
            _volley.Tick();
            return;
        }
        if (Armed && !Native.Same(TacticalState.Get().GetSelectedSkill(),
            _armed!.First.SkillKey == Native.Key(_squad) ? _squad : _leader))
            CancelTargeting("Combined targeting cancelled.", clearNative: false);
        if (Now < _nextRefresh) return;
        _nextRefresh = Now + 0.25;
        Refresh();
    }

    private T Read<T>(Func<T> action)
    {
        _readDepth++;
        try { return action(); }
        finally { _readDepth--; }
    }
    private Skill Choose(List<Skill> choices, ref int index)
    {
        index = Math.Clamp(index, 0, choices.Count - 1);
        return choices[index];
    }

    public void Refresh()
    {
        if (IsActive) return;
        if (_faulted) { Current = Decision.Deny("Runtime disabled after an error. See Latest.log."); return; }
        var manager = TacticalManager.Get();
        if (!Native.Valid(manager) || !manager.IsPlayerTurn() || manager.IsPaused())
        { Current = Decision.Deny("Available during your turn."); return; }
        var actor = manager.GetActiveActor();
        if (!Native.Same(actor, _actor))
        {
            CancelTargeting("Selection changed.", clearNative: true);
            _squadIndex = _leaderIndex = _vehiclePairIndex = 0;
            _squad = _leader = null;
        }
        _actor = actor;
        if (!Native.Valid(actor) || !Native.IsPlayerControlled(actor))
        { Current = Decision.Deny("Select a player unit."); return; }
        bool vehicleMode = actor.IsVehicle() && _enableVehicles;
        if (!actor.IsInfantry() && !vehicleMode)
        { Current = Decision.Deny(actor.IsVehicle() ? "Vehicle Coordinated Fire is disabled in Mod Settings." : "Select a player infantry squad."); return; }
        if (Armed && Native.Valid(_squad) && Native.Valid(_leader))
        {
            Current = Read(() => Evaluate(actor, _squad!, _leader!));
            return; // Never replace the chosen weapon mode while targeting.
        }
        Read(() =>
        {
            if (vehicleMode)
            {
                var pairs = Native.VehiclePairs(actor);
                if (pairs.Count == 0)
                {
                    _squad = _leader = null;
                    Current = Decision.Deny("Vehicle needs two distinct supported weapon attacks.");
                    return false;
                }
                _vehiclePairIndex = Math.Clamp(_vehiclePairIndex, 0, pairs.Count - 1);
                (_squad, _leader) = pairs[_vehiclePairIndex];
                Current = Evaluate(actor, _squad, _leader);
                return true;
            }

            var squads = Native.Attacks(actor, WeaponRole.Squad);
            var leaders = Native.Attacks(actor, WeaponRole.Leader);
            if (squads.Count == 0 || leaders.Count == 0)
            {
                _squad = squads.FirstOrDefault(); _leader = leaders.FirstOrDefault();
                Current = Decision.Deny("Equip both a squad weapon and a supported single-target special weapon.");
                return false;
            }
            _squad = Choose(squads, ref _squadIndex);
            _leader = Choose(leaders, ref _leaderIndex);
            Current = Evaluate(actor, _squad, _leader);
            return true;
        });
    }

    private Decision Evaluate(Actor actor, Skill squad, Skill leader, bool allowAI = false)
    {
        if (!SameAPConsumer(actor, squad) || !SameAPConsumer(actor, leader))
            return Decision.Deny("Attacks with a different AP consumer are not supported.");
        return Rules.Evaluate(Native.ReadActor(actor), Native.ReadAttack(actor, squad), Native.ReadAttack(actor, leader),
            allowAI, allowVehicles: _enableVehicles);
    }
    private static bool SameAPConsumer(Actor actor, Skill skill)
    {
        var consumer = skill.GetAPConsumer();
        return !Native.Valid(consumer) || Native.Same(consumer, actor);
    }
    public void Cycle(WeaponRole role)
    {
        if (Locked || Armed || !Native.Valid(_actor)) return;
        if (IsVehicleMode)
        {
            var pairs = Native.VehiclePairs(_actor!);
            if (pairs.Count < 2) return;
            int delta = role == WeaponRole.Squad ? -1 : 1;
            _vehiclePairIndex = (_vehiclePairIndex + delta + pairs.Count) % pairs.Count;
            _nextRefresh = 0;
            Refresh();
            return;
        }
        var count = Native.Attacks(_actor!, role).Count;
        if (count < 2) return;
        if (role == WeaponRole.Squad) _squadIndex = (_squadIndex + 1) % count;
        else _leaderIndex = (_leaderIndex + 1) % count;
        Refresh();
    }
    public void Arm()
    {
        if (Locked || _faulted) return;
        Refresh();
        if (!Current.Allowed || Current.Plan == null) { Say(Current.Reason); return; }
        _armed = Current.Plan;
        var first = _armed.First.SkillKey == Native.Key(_squad) ? _squad! : _leader!;
        if (!TacticalState.Get().TrySelectSkill(first))
        { _armed = null; Say("The native targeting interface rejected this attack."); return; }
        Say("Select a target valid for BOTH weapons. Right-click or Escape cancels.");
    }
    public void CancelTargeting(string reason, bool clearNative = true)
    {
        bool wasArmed = Armed;
        _armed = null;
        if (wasArmed && clearNative && InMission)
        {
            var state = TacticalState.Get();
            if (Native.Valid(state)) state.CancelCurrentAction();
        }
        if (wasArmed) Say(reason);
    }

    private bool BeforeUse(Skill skill, Tile target, UsageParameter usage, ref bool result)
    {
        if (_nativeDepth != 0) return true;

        if (_armed != null && Native.Key(skill) == _armed.First.SkillKey)
            return BeforePlayerCombinedUse(skill, target, usage, ref result);

        if (_enableAI && !Locked && _armed == null && TryBeginAICombinedUse(skill, target, usage, ref result))
            return false;

        return true;
    }

    private bool BeforePlayerCombinedUse(Skill skill, Tile target, UsageParameter usage, ref bool result)
    {
        result = false;
        if (_faulted || !Native.Valid(_actor) || !Native.Valid(_squad) || !Native.Valid(_leader)) return false;
        if (usage != UsageParameter.Default)
        {
            // Do not reinterpret reaction fire, fake previews, free shots or other mods' actions.
            Say("Combined fire only accepts a normal player attack (Default usage).");
            return false;
        }
        var decision = Read(() => Evaluate(_actor!, _squad!, _leader!));
        if (!decision.Allowed || decision.Plan == null) { Say(decision.Reason); return false; }
        if (!Read(() => Native.CanTarget(_squad!, _actor!, target) && Native.CanTarget(_leader!, _actor!, target)))
        { Say("Both weapons must be able to hit this tile using their native targeting rules."); return false; }
        BeginTransaction(_actor!, _squad!, _leader!, target, decision.Plan, isAI: false, UsageParameter.Default);
        _armed = null;
        Say($"Salvo committed: {_transaction!.Cost} AP reserved; native ammo consumption for each attack.");
        result = _volley.Begin(_transaction);
        return false;
    }

    private static bool IsSafeAIUsage(UsageParameter usage)
    {
        int value = (int)usage;
        int allowed = (int)UsageParameter.LowerAudioVolume
            | (int)UsageParameter.AutoAudioVolume
            | (int)UsageParameter.InstantResolve;
        int blocked = (int)UsageParameter.Free
            | (int)UsageParameter.IgnoreUsabilityCheck
            | (int)UsageParameter.Fake;
        return (value & blocked) == 0 && (value & ~allowed) == 0;
    }

    private bool TryBeginAICombinedUse(Skill chosen, Tile target, UsageParameter usage, ref bool result)
    {
        if (_faulted || !InMission || IsActive || _settling) return false;
        var manager = TacticalManager.Get();
        if (!Native.Valid(manager) || manager.IsPlayerTurn() || manager.IsPaused() || manager.IsFinished()) return false;
        var actor = Native.ResolveActorForSkill(chosen);
        if (!Native.Valid(actor) || Native.IsPlayerControlled(actor!) || !Native.HasAttack(actor, chosen))
            return false;
        bool vehicleMode = actor.IsVehicle() && _enableVehicles;
        bool infantryMode = actor.IsInfantry();
        if (!infantryMode && !vehicleMode) return false;

        if (!Native.Supported(chosen))
        {
            _log($"AI attack observed but not eligible for Coordinated Fire: skill={chosen.GetID()}, usage={usage}, supported=false.");
            return false;
        }
        if (!IsSafeAIUsage(usage))
        {
            _log($"AI attack observed but not converted: skill={chosen.GetID()}, usage={usage} ({(int)usage}); usage flags are not yet safe for combined fire.");
            return false;
        }

        WeaponRole role = Native.Role(chosen);
        List<Skill> others;
        if (vehicleMode)
        {
            long chosenItem = Native.WeaponTemplateKey(chosen);
            others = Native.VehicleAttacks(actor).Where(other => Native.WeaponTemplateKey(other) != chosenItem).ToList();
        }
        else
        {
            if (role == WeaponRole.Unknown)
            {
                _log($"AI infantry attack observed but role is unsupported: skill={chosen.GetID()}, usage={usage}.");
                return false;
            }
            var otherRole = role == WeaponRole.Squad ? WeaponRole.Leader : WeaponRole.Squad;
            others = Native.Attacks(actor, otherRole);
        }
        if (others.Count == 0)
        {
            _log($"AI attack observed but no compatible partner exists: skill={chosen.GetID()}, usage={usage}, vehicle={vehicleMode}.");
            return false;
        }

        string lastReason = "no compatible partner";
        for (int i = 0; i < others.Count; i++)
        {
            var other = others[i];
            var squad = vehicleMode || role == WeaponRole.Squad ? chosen : other;
            var leader = vehicleMode || role == WeaponRole.Leader ? other : chosen;
            var decision = Read(() => Evaluate(actor, squad, leader, allowAI: true));
            if (!decision.Allowed || decision.Plan == null)
            {
                lastReason = decision.Reason;
                continue;
            }
            if (!Read(() => Native.CanTarget(squad, actor, target) && Native.CanTarget(leader, actor, target)))
            {
                lastReason = "the selected target is not legal for both weapons";
                continue;
            }
            if (!AIPolicy.CanReplaceWithoutOwningTurnEnd(actor.GetActionPoints(), decision.Plan.Cost))
            {
                _log($"AI attack kept vanilla: skill={chosen.GetID()}, usage={usage} ({(int)usage}); combined fee {decision.Plan.Cost} would exhaust the actor's remaining {actor.GetActionPoints()} AP, so native AI turn hand-off is preserved.");
                return false;
            }
            BeginTransaction(actor, squad, leader, target, decision.Plan, isAI: true, usage);
            _log($"AI {(vehicleMode ? "Vehicle " : "")}Coordinated Fire: {squad.GetID()} + {leader.GetID()} on tile; usage={usage} ({(int)usage}); fee={decision.Plan.Cost}.");
            result = _volley.Begin(decision.Plan);
            return true;
        }
        _log($"AI attack kept vanilla: skill={chosen.GetID()}, usage={usage} ({(int)usage}); reason={lastReason}.");
        return false; // Let the AI execute its original attack unchanged.
    }

    private void BeginTransaction(Actor actor, Skill squad, Skill leader, Tile target, VolleyPlan plan, bool isAI, UsageParameter usage)
    {
        _transaction = plan;
        _firingActor = actor; _firingSquad = squad; _firingLeader = leader; _target = target;
        _round = TacticalManager.Get().GetRound();
        _transactionIsAI = isAI;
        _childUsage = usage;
        _apDebitIntercepted = _apReconciled = _suppressFee = false;
        _launchAp = _launchCost = _clampedApGain = 0;
    }

    public bool Launch(AttackSnapshot attack, bool synchronizedFollowUp = false)
    {
        var skill = attack.SkillKey == Native.Key(_firingSquad) ? _firingSquad! : _firingLeader!;
        if (!Native.Valid(skill) || !Native.Valid(_target)) throw new InvalidOperationException("Attack reference disappeared.");
        _launchAp = _firingActor!.GetActionPoints();
        // Read the live child cost immediately before launch. Temporary effects such as
        // Lim's Run & Gun / Full Send can expire after the first child attack and change
        // the second skill's AP cost mid-volley, making the original plan snapshot stale.
        _launchCost = Math.Max(0, skill.GetActionPointCost());
        _apDebitIntercepted = false;
        _apReconciled = false;
        int beforeAmmo = skill.GetUses();
        _nativeDepth++;
        bool accepted;
        // Safest overlap attempt: never bypass MENACE usability/busy checks. If MENACE does
        // not accept a second native Use while the first is active, the controller falls
        // back to the proven sequential path after the first action completes.
        // Coordinated Fire owns the single combined AP fee. Ask MENACE natively not to
        // consume AP for each child while leaving ammo/limited-use consumption untouched.
        // Do NOT use UsageParameter.Free here: Free also includes NoUsesConsumed.
        // This also avoids stale AP-debit expectations when a one-shot AP-cost modifier
        // (for example Run & Gun / Full Send) is consumed by the first child attack.
        var launchUsage = _childUsage | UsageParameter.NoApConsumed;
        try { accepted = skill.Use(_target!, launchUsage); }
        finally { _nativeDepth--; }
        int afterAP = _firingActor.GetActionPoints();
        int afterAmmo = skill.GetUses();
        if (!accepted && afterAmmo < beforeAmmo)
        {
            // Resource consumption is evidence of a partially executed action even without an event.
            _volley.NativeStarted(attack.SkillKey);
            _volley.Cancel("Native call rejected after resource consumption; no retry.");
        }
        else if (synchronizedFollowUp && !accepted)
            _log("Synchronized follow-up was cleanly rejected by MENACE; falling back to sequential fire after the first native action finishes.");
        _log($"Use {attack.Id}: accepted={accepted}; synchronized={synchronizedFollowUp}; usage={launchUsage}; uses {beforeAmmo}->{afterAmmo}; AP {_launchAp}->{afterAP}; expectedChildCost={_launchCost}; debitIntercepted={_apDebitIntercepted}; fee={ReservedCost}");
        return accepted;
    }

    private int CurrentNativeCost()
    {
        if (_transaction == null || !_volley.IsActive) return 0;
        // AP writes happen synchronously inside Skill.Use. During a launch, _launchCost is
        // therefore the authoritative child cost even when both native attacks overlap.
        if (_nativeDepth != 0) return Math.Max(0, _launchCost);
        return _volley.CurrentSkill == _transaction.First.SkillKey
            ? _transaction.First.ActionPointCost
            : _volley.CurrentSkill == _transaction.Second.SkillKey ? _transaction.Second.ActionPointCost : 0;
    }

    private void InterceptActionPointWrite(Actor actor, ref int requested, bool nativeClamps)
    {
        if (!IsActive || _settling || !Native.Same(actor, _firingActor)) return;
        int current = actor.GetActionPoints();

        // Native SetActionPoints clamps positive AP gains. During NoApConsumed child
        // attacks, Berserk can hit that cap before the salvo fee has been paid.
        // Preserve only the clipped portion of these gains for fee settlement.
        // SetActionPointsUnsafe does not clamp and must not contribute to this pool.
        if (nativeClamps && requested > current && _transaction is { Cost: > 0 })
        {
            try
            {
                // MENACE clamps to the actor's current, buff-adjusted EntityProperties.
                var properties = actor.GetCurrentProperties();
                if (Native.Valid(properties))
                {
                    int clipped = Fee.ClippedGain(current, requested, properties.GetActionPoints());
                    _clampedApGain = Fee.AccumulateClippedGain(_clampedApGain, clipped, _transaction.Cost);
                }
            }
            catch (Exception e)
            {
                _log("Native AP cap unavailable for deferred refund: " + e.Message);
            }
        }

        int cost = CurrentNativeCost();
        if (cost <= 0) return;
        int adjusted = Fee.SuppressNativeDebit(current, requested, cost);
        if (adjusted == requested) return;
        _log($"Suppressed native child AP debit: {current}->{requested} (child cost {cost}).");
        requested = adjusted;
        _apDebitIntercepted = true;
    }

    private void ReconcileNativeAP()
    {
        if (_apReconciled || !Native.Valid(_firingActor) || _launchCost <= 0) { _apReconciled = true; return; }
        _apReconciled = true;
        int current = _firingActor!.GetActionPoints();
        if (_apDebitIntercepted) return;
        // Fallback for a game path that bypasses SetActionPoints/Unsafe. Only repair an
        // exact observed vanilla debit; never guess around unrelated AP changes.
        if (current == _launchAp - _launchCost)
        {
            RestoreLaunchActionPoints(current, $"direct native child AP debit (refund {_launchCost})");
            return;
        }

        // Some long-running/setup weapon skills apply their turn-flow side effects only when
        // the animation finishes. In that path MENACE can zero the actor's AP asynchronously
        // even though Skill.Use was launched with NoApConsumed (the immediate Use log still
        // shows AP unchanged). While Coordinated Fire owns input/turn flow, a live, unstunned
        // active actor in the same round cannot legitimately spend AP elsewhere, so recover
        // this specific zeroing instead of permanently faulting the runtime.
        if (current == 0 && _launchAp > 0 && CanRecoverOwnedAsyncApZero())
        {
            RestoreLaunchActionPoints(current, "owned asynchronous native AP zero");
            return;
        }

        if (current < _launchAp)
        {
            _suppressFee = true;
            _faulted = true;
            _error($"AP accounting uncertain after child attack: launch={_launchAp}, current={current}, expected child cost={_launchCost}. Final fee suppressed; new salvos disabled.");
        }
    }

    private bool CanRecoverOwnedAsyncApZero()
    {
        if (!Native.Valid(_firingActor) || _transaction == null || _settling) return false;
        try
        {
            var manager = TacticalManager.Get();
            if (!Native.Valid(manager) || manager.IsFinished() || manager.GetRound() != _round) return false;
            if (!Native.Same(manager.GetActiveActor(), _firingActor)) return false;
            if (_transactionIsAI ? manager.IsPlayerTurn() : !manager.IsPlayerTurn()) return false;
            var actor = _firingActor!;
            return actor.IsAlive() && !actor.IsDying() && !actor.IsLeavingMap()
                && !actor.IsStunned() && actor.GetMoraleState() != MoraleState.Fleeing;
        }
        catch { return false; }
    }

    private void RestoreLaunchActionPoints(int current, string reason)
    {
        if (!Native.Valid(_firingActor)) return;
        var actor = _firingActor!;
        actor.SetActionPoints(_launchAp, true);
        if (actor.IsTurnDone()) actor.SetTurnDone(false);
        actor.UpdateActorState();
        actor.GetSkills().ScheduleUpdate();
        actor.GetSkills().UiNeedsUpdate = true;
        _log($"Reconciled {reason}: {current}->{actor.GetActionPoints()}.");
    }

    public bool ValidateFollowUp(out string reason)
        => ValidateFollowUpCore(allowOwnedBusy: false, out reason);

    public bool ValidateSynchronizedFollowUp(out string reason)
    {
        bool valid = ValidateFollowUpCore(allowOwnedBusy: true, out reason);
        if (valid)
            _log("Synchronized follow-up preflight passed; attempting the second native attack after the 0.10s stagger.");
        else
            _log("Synchronized follow-up preflight rejected: " + reason);
        return valid;
    }

    private bool ValidateFollowUpCore(bool allowOwnedBusy, out string reason)
    {
        ReconcileNativeAP();
        string issue = "";
        bool valid = Read(() =>
        {
            var actor = _firingActor;
            if (!Native.Valid(actor) || !Native.Same(actor, TacticalManager.Get().GetActiveActor()))
            { issue = "Unit changed."; return false; }
            var a = Native.ReadActor(actor!);
            bool wrongController = _transactionIsAI ? a.IsPlayerControlled : !a.IsPlayerControlled;
            // During a synchronized player volley the first child is expected to make the
            // actor/skill container busy. Ignore only that owned busy state; every other
            // legality check is still repeated immediately before the second launch.
            if (!a.IsAlive || a.IsRouting || a.IsStunned || a.IsMoving || (!allowOwnedBusy && a.IsBusy) || wrongController)
            { issue = "Unit can no longer fire."; return false; }
            if (a.ActionPoints < _transaction!.Cost)
            { issue = "The reserved AP are no longer available."; return false; }
            var second = _transaction.Second.SkillKey == Native.Key(_firingSquad) ? _firingSquad! : _firingLeader!;
            // AttackSnapshot.WeaponKey stores the equipped WEAPON TEMPLATE key. Compare
            // template-to-template here; comparing GetItem() (the runtime item instance)
            // against that template pointer falsely reports a weapon change after every
            // first shot and prevents both synchronized and sequential follow-ups.
            if (!Native.HasAttack(actor!, second) || Native.WeaponTemplateKey(second) != _transaction.Second.WeaponKey)
            { issue = "The equipped weapon/fire mode changed."; return false; }
            if (!Native.Valid(_target) || !Native.CanTarget(second, actor!, _target!))
            { issue = "Second weapon/target no longer legal (ammo, death, line of fire, deployment or status)."; return false; }
            return true;
        });
        reason = issue;
        return valid;
    }

    public void Finish(bool charge, string reason, bool success)
    {
        ReconcileNativeAP();
        var actor = _firingActor;
        int cost = _transaction?.Cost ?? 0;
        bool suppressed = _suppressFee;
        bool wasAI = _transactionIsAI;
        bool endActorTurn = false;
        _settling = true;
        try
        {
            if (charge && !suppressed && Native.Valid(actor))
            {
                int before = actor!.GetActionPoints();
                int after = Fee.AfterPreservingClippedGains(before, cost, _clampedApGain);
                actor.SetActionPoints(after, true);
                actor.UpdateActorState();
                actor.GetSkills().ScheduleUpdate();
                actor.GetSkills().UiNeedsUpdate = true;
                _log($"AP fee committed once: {before}->{actor.GetActionPoints()} (fee {cost}, preserved clipped grants {_clampedApGain}).");
            }

            if (Native.Valid(actor))
            {
                // Skill.Use can ask MENACE to revisit turn flow after a native attack.
                // That is fine when the actor is actually out of actions, but replaying
                // TryCheckRound unconditionally made a successful combined salvo behave
                // like End Turn even when AP remained.
                bool apSpent = actor!.IsActionPointsSpent();
                bool turnDone = actor.IsTurnDone();
                if (TurnFlow.ShouldRestoreTurn(apSpent, turnDone))
                {
                    actor.SetTurnDone(false);
                    turnDone = actor.IsTurnDone();
                    _log($"Restored actor turn after salvo: AP={actor.GetActionPoints()}, IsActionPointsSpent={apSpent}, IsTurnDone={turnDone}.");
                }
                endActorTurn = TurnFlow.ShouldEndActorTurn(apSpent, turnDone);
                _log($"Post-salvo turn state: AP={actor.GetActionPoints()}, IsActionPointsSpent={apSpent}, IsTurnDone={turnDone}, deferredRoundCheck={_roundCheckDeferred}, endActorTurn={endActorTurn}.");
            }

            Say((success ? "Salvo complete. " : "Salvo interrupted. ") + reason +
                (suppressed ? " AP accounting uncertain; gameplay disabled for this session." : ""));
        }
        finally
        {
            _transaction = null; _firingActor = null; _firingSquad = _firingLeader = null; _target = null;
            _suppressFee = false; _settling = false; _transactionIsAI = false; _childUsage = UsageParameter.Default;
            _apDebitIntercepted = _apReconciled = false; _launchAp = _launchCost = _clampedApGain = 0;
            bool hadDeferredRoundCheck = _roundCheckDeferred;
            _roundCheckDeferred = false;
            if (TacticalManager.IsMissionRunning())
            {
                if (!wasAI)
                {
                    if (endActorTurn)
                    {
                        // TryCheckRound alone does not finish/deselect the active player actor.
                        // Let MENACE run its normal EndTurn path now that the coordinated
                        // transaction is unlocked; EndTurn will clear the active actor and
                        // perform the appropriate round/faction check.
                        _log($"Ending player actor through TacticalState.EndTurn after salvo (deferred={hadDeferredRoundCheck}).");
                        TacticalState.Get().EndTurn();
                    }
                    else
                    {
                        TacticalState.Get().ClearCurrentAction();
                        if (hadDeferredRoundCheck)
                            _log("Discarded deferred round check because the actor still has usable AP.");
                    }
                }
                else if (endActorTurn)
                {
                    // This should now be unreachable for normal AI conversion because terminal
                    // AI attacks are deliberately left vanilla. Keep a best-effort manager check
                    // for unexpected game-side state changes, but log it loudly for diagnosis.
                    _log($"WARNING: AI salvo unexpectedly ended the actor's action budget; releasing manager round check (deferred={hadDeferredRoundCheck}).");
                    TacticalManager.Get().TryCheckRound();
                }
                else if (hadDeferredRoundCheck)
                    _log("Discarded deferred AI round check because the actor still has usable AP.");
            }
        }
        _nextRefresh = 0;
    }

    private bool BeforeRoundCheck(ref bool result)
    {
        if (!Locked) return true;
        _roundCheckDeferred = true;
        result = false;
        _log("Deferred native TryCheckRound while Coordinated Fire owns the action.");
        return false;
    }

    private bool BothReady() => Native.Valid(_actor) && Native.Valid(_squad) && Native.Valid(_leader)
        && Read(() => Evaluate(_actor!, _squad!, _leader!).Allowed);
    private bool BothTarget(Tile target) => BothReady() && Read(() =>
        Native.CanTarget(_squad!, _actor!, target) && Native.CanTarget(_leader!, _actor!, target));

    public void SceneChanged()
    {
        // Never dereference or debit actors from an unloaded mission.
        _volley.AbandonScene();
        _armed = _transaction = null;
        _actor = _firingActor = null;
        _squad = _leader = _firingSquad = _firingLeader = null;
        _target = null; _settling = _suppressFee = _transactionIsAI = _roundCheckDeferred = false;
        _childUsage = UsageParameter.Default;
        _apDebitIntercepted = _apReconciled = false; _launchAp = _launchCost = _clampedApGain = 0;
        _readDepth = _nativeDepth = 0;
        Current = Decision.Deny("Select an infantry squad or enabled vehicle in a mission.");
        _nextRefresh = 0;
    }
    public void Fail(Exception e)
    {
        bool first = !_faulted;
        _faulted = true;
        try { CancelTargeting("Runtime error; see Latest.log."); }
        catch { _armed = null; }
        if (IsActive) _volley.Cancel("Runtime error: " + e.Message);
        Status = "Runtime error; new salvos disabled. See Latest.log.";
        if (first) _error(e.ToString());
    }
    private void Say(string message) { Status = message; _log(message); }
    public void Dispose()
    {
        SceneChanged();
        Instance = null;
        _harmony.UnpatchSelf();
    }

    // Positional Harmony arguments intentionally avoid unstable native parameter names.
    public static class Hooks
    {
        public static bool Use(Skill __instance, Tile __0, UsageParameter __1, ref bool __result)
        {
            var r = Instance;
            if (r == null) return true;
            try { return r.BeforeUse(__instance, __0, __1, ref __result); }
            catch (Exception e) { r.Fail(e); __result = false; return false; }
        }
        public static void SetActionPoints(Actor __instance, ref int __0)
        {
            var r = Instance;
            if (r == null) return;
            try { r.InterceptActionPointWrite(__instance, ref __0, nativeClamps: true); }
            catch (Exception e) { r.Fail(e); }
        }
        public static void SetActionPointsUnsafe(Actor __instance, ref int __0)
        {
            var r = Instance;
            if (r == null) return;
            try { r.InterceptActionPointWrite(__instance, ref __0, nativeClamps: false); }
            catch (Exception e) { r.Fail(e); }
        }
        public static void MinRange(Skill __instance, ref int __result)
        { var r = Instance; if (r?.Preview(__instance) == true) __result = Math.Max(__result, r._armed!.MinRange); }
        public static void MaxRange(Skill __instance, ref int __result)
        { var r = Instance; if (r?.Preview(__instance) == true) __result = Math.Min(__result, r._armed!.MaxRange); }
        public static void Ready(Skill __instance, ref bool __result)
        {
            var r = Instance;
            if (!__result || r?.Preview(__instance) != true) return;
            try { __result = r.BothReady(); }
            catch (Exception e) { __result = false; r.Fail(e); }
        }
        public static void Target(Skill __instance, Tile __0, ref bool __result)
        {
            var r = Instance;
            if (!__result || r?.Preview(__instance) != true) return;
            try { __result = r.BothTarget(__0); }
            catch (Exception e) { __result = false; r.Fail(e); }
        }
        public static void Started(Actor __0, Skill __1, Tile __2)
        { var r = Instance; if (r != null && Native.Same(__0, r._firingActor)) r._volley.NativeStarted(Native.Key(__1)); }
        public static void Completed(Skill __0)
        { Instance?._volley.NativeCompleted(Native.Key(__0)); }
        public static bool Round(ref bool __result)
        {
            var r = Instance;
            if (r == null) return true;
            return r.BeforeRoundCheck(ref __result);
        }
        public static bool EndTurn() => Instance?.Locked != true;
        public static bool Select(ref bool __result)
        { if (Instance?.Locked != true) return true; __result = false; return false; }
        public static bool Mouse()
        { return Instance?.Locked != true; }
        public static bool Keyboard()
        {
            var r = Instance;
            if (r == null) return true;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (r.Armed)
                {
                    r.CancelTargeting("Combined targeting cancelled.");
                    return false; // Consume Escape once; do not also open the pause menu.
                }
                return true; // Keep the native pause/menu escape route available.
            }
            return !r.Locked;
        }
    }
}
