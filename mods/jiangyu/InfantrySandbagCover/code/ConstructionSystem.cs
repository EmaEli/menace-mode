using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Jiangyu.Sdk;
using MenaceModSettings;

namespace InfantrySandbagCover;

public sealed class ConstructionSystem : JiangyuSystem
{
    internal const string SkillId = "mod.infantry_sandbag_cover";
    private const string Group = "Infantry Sandbag Cover";
    private const string TurnsKey = "ConstructionTurns";
    private static ConstructionSystem _owner;
    private readonly ConstructionState _work = new();
    private readonly Dictionary<long, Actor> _actors = new();
    private readonly Dictionary<long, Attempt> _pending = new();
    private readonly ConstructionOverlay _overlay = new();
    private Harmony _harmony;
    private object _poll;
    private int _mainThread, _required = ConstructionState.DefaultTurns;
    private bool _ready, _failed, _tactical;

    internal sealed class Attempt
    {
        internal Actor Actor;
        internal Tile Tile;
        internal int Round, Cost, BeforeAP, BeforeCover;
        internal bool Final, Applied, Refunded;
    }

    public override void OnInit()
    {
        _owner = this;
        _mainThread = Environment.CurrentManagedThreadId;
        ModSettings.Register(Group, s => s.AddNumber(TurnsKey,
            "Construction turns (80 AP per round; new sites)", 1, ConstructionState.MaxTurns, ConstructionState.DefaultTurns));
        ReloadSettings();
        ModSettings.Changed += SettingChanged;
        PrepareJit();
        _harmony = new Harmony("luca.infantrysandbagcover");
        // Install the guard first. If a later detour fails, this skill remains blocked.
        Patch(typeof(Skill), "Use", nameof(BeforeUse), nameof(AfterUse), nameof(UseFailed), typeof(Tile), typeof(UsageParameter));
        try
        {
            Patch(typeof(Skill), "ApplyToTile", nameof(BeforeApply), nameof(AfterApply), nameof(ApplyFailed), typeof(Tile), typeof(UsageParameter));
            Patch(typeof(Skill), "IsUsable", null, nameof(Usable));
            Patch(typeof(Skill), "IsUsable", null, nameof(Usable), null, typeof(TargetUsageParams));
            Patch(typeof(TacticalManager), "InvokeOnMovement", null, nameof(Moved), null,
                typeof(Actor), typeof(Tile), typeof(Tile), typeof(MovementAction), typeof(Entity));
            _ready = true;
            _poll = Context.Coroutines.Start(Poll());
            ModGui.Add(Draw);
        }
        catch (Exception e) { Fail(e); }
    }

    private void Patch(Type type, string method, string prefix, string postfix, string finalizer = null, params Type[] args)
    {
        var target = AccessTools.Method(type, method, args) ?? throw new MissingMethodException(type.FullName, method);
        HarmonyMethod Hook(string name) => name == null ? null : new HarmonyMethod(typeof(ConstructionSystem), name);
        _harmony.Patch(target, prefix: Hook(prefix), postfix: Hook(postfix), finalizer: Hook(finalizer));
    }

    private static bool IsOurs(Skill skill) => skill != null && skill.GetID() == SkillId;
    private bool MainThread => Environment.CurrentManagedThreadId == _mainThread;
    private static int Round => TacticalManager.Get()?.GetRound() ?? -1;
    private static long Key(Actor actor) => actor.Pointer.ToInt64();
    private static int CoverMask(Tile tile)
    {
        int mask = 0;
        for (int i = 0; i < 4; i++) if (tile.HasHalfCoverInDir((Direction)(i * 2))) mask |= 1 << i;
        return mask;
    }

    private bool Available(Skill skill, out Actor actor, out Tile tile)
    {
        actor = null;
        tile = null;
        if (!_ready || !MainThread || !_tactical) return false;
        actor = skill.GetActor();
        if (actor == null || !actor.IsInfantry() || !actor.IsPlayerOrPlayerAI()
            || actor.GetHitpoints() <= 0 || actor.IsMoving() || actor.IsContainedWithinAnotherEntity()) return false;
        tile = actor.GetTile();
        return tile != null && CoverMask(tile) != 15
            && _work.CanWork(Key(actor), tile.GetX(), tile.GetZ(), Round, actor.TilesMovedThisTurn);
    }

    internal static void Usable(Skill __instance, ref bool __result)
    {
        if (!__result || !IsOurs(__instance)) return;
        try { __result = _owner != null && _owner.Available(__instance, out _, out _); }
        catch (Exception e) { __result = false; _owner?.Fail(e); }
    }

    internal static bool BeforeUse(Skill __instance, ref Tile __0, UsageParameter __1, ref bool __result, out Attempt __state)
    {
        __state = null;
        if (!IsOurs(__instance)) return true;
        __result = false;
        var owner = _owner;
        try
        {
            if (owner == null || (__1 & (UsageParameter.Fake | UsageParameter.NoApConsumed)) != 0
                || owner._pending.ContainsKey(__instance.Pointer.ToInt64())
                || !owner.Available(__instance, out var actor, out var tile)) return false;
            __0 = tile;
            __state = new Attempt { Actor = actor, Tile = tile, Round = Round,
                Cost = __instance.GetActionPointCost(), BeforeAP = actor.GetActionPoints(), BeforeCover = CoverMask(tile) };
            owner._pending[__instance.Pointer.ToInt64()] = __state;
            owner._actors[Key(actor)] = actor;
            return true; // Native checks, AP charging, acted state and scheduling are preserved.
        }
        catch (Exception e) { owner?.Fail(e); return false; }
    }

    internal static void AfterUse(Skill __instance, bool __result, Attempt __state)
    {
        if (__state == null || __result) return;
        if (!__state.Applied) _owner?.Refund(__state);
        _owner?._pending.Remove(__instance.Pointer.ToInt64());
    }

    internal static Exception UseFailed(Exception __exception, Skill __instance, Attempt __state)
    {
        if (__exception != null && __state != null)
        {
            _owner?.Refund(__state);
            _owner?._pending.Remove(__instance.Pointer.ToInt64());
            _owner?.Fail(__exception);
        }
        return __exception;
    }

    internal static bool BeforeApply(Skill __instance, Tile __0, UsageParameter __1, out Attempt __state)
    {
        __state = null;
        if (!IsOurs(__instance)) return true;
        var owner = _owner;
        // Never let untracked, fake, delayed-after-scene or duplicate applications create cover.
        if (owner == null || !owner.MainThread || !owner._ready
            || !owner._pending.TryGetValue(__instance.Pointer.ToInt64(), out var attempt)) return false;
        __state = attempt;
        owner._pending.Remove(__instance.Pointer.ToInt64());
        try
        {
            var actor = attempt.Actor;
            var tile = actor.GetTile();
            if ((__1 & UsageParameter.Fake) != 0 || tile == null || __0 == null || actor.IsMoving()
                || actor.IsContainedWithinAnotherEntity() || actor.GetHitpoints() <= 0 || Round != attempt.Round
                || tile.Pointer != attempt.Tile.Pointer || __0.Pointer != tile.Pointer || CoverMask(tile) == 15
                || !owner._work.CanWork(Key(actor), tile.GetX(), tile.GetZ(), Round, actor.TilesMovedThisTurn))
            { owner.Refund(attempt); return false; }
            attempt.BeforeCover = CoverMask(tile);
            attempt.Final = owner._work.BeginWork(Key(actor), tile.GetX(), tile.GetZ(), Round, actor.TilesMovedThisTurn, owner._required);
            attempt.Applied = true;
            return attempt.Final; // Intermediate rounds skip *all* placement/environment effects.
        }
        catch (Exception e) { owner.Refund(attempt); owner.Fail(e); return false; }
    }

    internal static void AfterApply(Skill __instance, Attempt __state)
    {
        if (__state == null || !__state.Applied || !__state.Final) return;
        try
        {
            if ((CoverMask(__state.Tile) & ~__state.BeforeCover) != 0)
                _owner?._work.Complete(Key(__state.Actor), __state.Round);
            else _owner?.Refund(__state); // Keep previous progress and permit a retry.
        }
        catch (Exception e) { _owner?.Refund(__state); _owner?.Fail(e); }
    }

    internal static Exception ApplyFailed(Exception __exception, Attempt __state)
    {
        if (__exception != null && __state != null) { _owner?.Refund(__state); _owner?.Fail(__exception); }
        return __exception;
    }

    private void Refund(Attempt attempt)
    {
        if (attempt.Refunded || !MainThread) return;
        attempt.Refunded = true;
        int current = attempt.Actor.GetActionPoints();
        int debit = Math.Min(Math.Max(0, attempt.Cost), Math.Max(0, attempt.BeforeAP - current));
        if (debit > 0) attempt.Actor.SetActionPoints(current + debit);
    }

    internal static void Moved(Actor __0, Tile __1, Tile __2)
    {
        var owner = _owner;
        if (owner == null || !owner.MainThread || __0 == null || __1 == null || __2 == null
            || __1.Pointer == __2.Pointer || !owner._actors.ContainsKey(Key(__0))) return;
        owner._work.Moved(Key(__0), Round);
    }

    private IEnumerator Poll()
    {
        while (true)
        {
            if (_tactical && _ready)
            {
                try { Refresh(); } catch (Exception e) { Fail(e); }
            }
            yield return null;
        }
    }

    internal void Refresh()
    {
        foreach (long key in _work.Actors)
        {
            if (!_actors.TryGetValue(key, out var actor)) continue;
            if (actor.GetHitpoints() <= 0 || actor.IsContainedWithinAnotherEntity())
            { _work.Remove(key); _actors.Remove(key); continue; }
            var site = _work.Get(key);
            var tile = actor.GetTile();
            if (site.Completed > 0 && (tile == null || tile.GetX() != site.X || tile.GetZ() != site.Z
                || actor.IsMoving() || actor.TilesMovedThisTurn > 0)) _work.Moved(key, Round);
        }
        _overlay.Refresh(_work, _actors);
    }

    private void Draw() { if (_tactical && _ready) _overlay.Draw(_work); }
    private void ReloadSettings() => _required = ModSettings.Get<int>(Group, TurnsKey);
    private void SettingChanged(string group, string key, object value) { if (group == Group && key == TurnsKey) ReloadSettings(); }
    private void Fail(Exception e)
    {
        _ready = false;
        if (_failed) return;
        _failed = true;
        Context.Log.Warn("Sandbag construction disabled safely: " + e.GetType().Name + ": " + e.Message);
        _overlay.Clear();
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _tactical = sceneName == "Tactical";
        _work.Clear(); _actors.Clear(); _pending.Clear(); _overlay.Clear();
    }

    public override void OnUnload()
    {
        _ready = false;
        ModSettings.Changed -= SettingChanged;
        ModGui.Remove(Draw);
        if (_poll != null) Context.Coroutines.Stop(_poll);
        _work.Clear(); _actors.Clear(); _pending.Clear(); _overlay.Clear();
        _harmony?.UnpatchSelf();
        if (_owner == this) _owner = null;
    }

    private static void PrepareJit()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var type in typeof(ConstructionSystem).Assembly.GetTypes())
            if (!type.ContainsGenericParameters)
                foreach (var method in type.GetMethods(flags))
                    if (!method.IsAbstract && !method.ContainsGenericParameters)
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
    }
}