using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace MenaceWeaponRangesRebalanced;

public sealed partial class Plugin
{
    // Store values, not native wrappers: wrappers/actors may be destroyed on scene changes.
    private static readonly Dictionary<long, (string Id, int Original, int Applied)> RuntimeGrenadeRanges = new();
    private static bool _runtimeGrenadeWarning;

    private static void InstallThrownGrenadeRuntimePatches(global::HarmonyLib.Harmony harmony)
    {
        void Patch(Type type, string method, Type[] args, string hook, bool postfix = false)
        {
            var target = AccessTools.Method(type, method, args)
                ?? throw new MissingMethodException(type.FullName, method);
            var patch = new HarmonyMethod(typeof(Plugin), hook) { priority = Priority.Last };
            harmony.Patch(target, prefix: postfix ? null : patch, postfix: postfix ? patch : null);
        }

        // GetMaxRange is a tiny native field getter and may be inlined. Patching only its
        // return value would leave native target checks reading the old m_MaxRange cache.
        Patch(typeof(Skill), nameof(Skill.GetMaxRange), Type.EmptyTypes, nameof(SyncRuntimeThrownGrenadeRange));
        Patch(typeof(Skill), nameof(Skill.IsInRange), new[] { typeof(int), typeof(int) }, nameof(SyncRuntimeThrownGrenadeRange));
        Patch(typeof(Skill), nameof(Skill.IsInRange), new[] { typeof(Tile), typeof(Tile), typeof(bool) }, nameof(SyncRuntimeThrownGrenadeRange));
        Patch(typeof(Skill), nameof(Skill.SetRanges), new[] { typeof(int), typeof(int), typeof(int) }, nameof(ThrownGrenadeSetRangesPostfix), true);
        Patch(typeof(SkillContainer), nameof(SkillContainer.Update), Type.EmptyTypes, nameof(ThrownGrenadeContainerUpdatePostfix), true);
    }

    private static void SyncRuntimeThrownGrenadeRange(Skill __instance)
        => ApplyRuntimeThrownGrenadeRange(__instance, recalculated: false);

    private static void ThrownGrenadeSetRangesPostfix(Skill __instance)
        => ApplyRuntimeThrownGrenadeRange(__instance, recalculated: true);

    private static void ThrownGrenadeContainerUpdatePostfix(SkillContainer __instance)
    {
        try { SyncRuntimeThrownGrenadeContainer(__instance); }
        catch (Exception ex) { WarnRuntimeThrownGrenadeFailure(ex); }
    }

    private static void ApplyRuntimeThrownGrenadeRange(Skill skill, bool recalculated, bool restoreOnly = false)
    {
        try
        {
            if (skill == null || skill.Pointer == IntPtr.Zero) return;
            string id = skill.GetID() ?? string.Empty;
            if (!ThrownGrenadeRangeRules.IsThrownGrenadeSkill(id)) return;

            lock (RuntimeGrenadeRanges)
            {
                long key = skill.Pointer.ToInt64();
                int current = skill.m_MaxRange;
                bool owned = RuntimeGrenadeRanges.TryGetValue(key, out var saved)
                    && saved.Id == id && !recalculated && current == saved.Applied;
                int original = owned ? saved.Original : current;
                bool enabled = !restoreOnly && _enabled && _limitThrownGrenadeRange;
                var decision = ThrownGrenadeRangeRules.Evaluate(original, current,
                    enabled, owned, _thrownGrenadeMaxRange);
                if (decision.DesiredRange != current)
                {
                    skill.m_MaxRange = decision.DesiredRange;
                    DebugLog($"[THROWN_GRENADE_RUNTIME] skill={id} cachedMax={current} nativeMax={skill.m_MaxRange} cap={_thrownGrenadeMaxRange} enabled={enabled}.");
                }
                if (decision.ShouldOwn)
                    RuntimeGrenadeRanges[key] = (id, original, decision.DesiredRange);
                else
                    RuntimeGrenadeRanges.Remove(key);
            }
        }
        catch (Exception ex) { WarnRuntimeThrownGrenadeFailure(ex); }
    }

    private static void SyncRuntimeThrownGrenadeContainer(SkillContainer container, bool restoreOnly = false)
    {
        if (container == null || container.Pointer == IntPtr.Zero) return;
        var skills = container.GetAllSkills();
        if (skills == null) return;
        foreach (var entry in skills)
        {
            var skill = entry?.TryCast<Skill>();
            if (skill != null) ApplyRuntimeThrownGrenadeRange(skill, recalculated: false, restoreOnly: restoreOnly);
        }
    }

    private static void RefreshRuntimeThrownGrenadeRanges(bool restoreOnly = false)
    {
        try
        {
            var manager = TacticalManager.Get();
            if (manager == null || manager.Pointer == IntPtr.Zero) return;
            var factions = manager.GetFactions();
            if (factions == null) return;
            foreach (var faction in factions)
            {
                if (faction == null) continue;
                var actors = faction.GetActors();
                if (actors == null) continue;
                foreach (var actor in actors)
                    if (actor != null) SyncRuntimeThrownGrenadeContainer(actor.GetSkills(), restoreOnly);
            }
        }
        catch (Exception ex) { WarnRuntimeThrownGrenadeFailure(ex); }
    }

    private static void WarnRuntimeThrownGrenadeFailure(Exception ex)
    {
        if (_runtimeGrenadeWarning) return;
        _runtimeGrenadeWarning = true;
        Warn("Thrown-grenade runtime range synchronization failed: " + ex.Message);
    }
}