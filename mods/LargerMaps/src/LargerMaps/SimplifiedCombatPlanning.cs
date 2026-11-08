using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AIPerformance;

/// <summary>
/// A scene-scoped change to MENACE's own AI ability-planning flag. Only maps
/// of at least 64x64 and an enabled companion setting can modify templates.
/// No global FindPath hooks, per-turn work, or native AI algorithm patching.
/// </summary>
public sealed partial class Plugin
{
    private sealed class SkillPlanningRestore
    {
        internal object Config;
        internal bool Original;
    }

    private static readonly List<SkillPlanningRestore> _combatPlanningRestores = new();
    private static readonly HashSet<string> _simplifiedAttackIds = new(StringComparer.Ordinal);
    private static bool _combatPlanningActive;
    private static bool _combatPlanningWarned;

    private static bool SimplifiedCombatPlanningEnabled()
        => TargetSize >= ZoneReferenceSize && ReadBool("SimplifyCombatAIPlanning", true);

    private static void ApplySimplifiedCombatPlanning()
    {
        if (!SimplifiedCombatPlanningEnabled() || _combatPlanningActive) return;
        int inspected = 0, attacks = 0, buffs = 0, lookupHits = 0;
        var processedSkillIds = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            Type loader = FindType("Il2CppMenace.Tools.DataTemplateLoader") ??
                          FindType("Menace.Tools.DataTemplateLoader");
            Type skillTemplate = FindType("Il2CppMenace.Tactical.Skills.SkillTemplate") ??
                                 FindType("Menace.Tactical.Skills.SkillTemplate");
            if (loader == null || skillTemplate == null)
            {
                CombatPlanningWarnOnce("skill-template API not found; vanilla AI retained");
                return;
            }

            MethodInfo getById = FindSkillTemplateIdLookup(loader, skillTemplate);
            // Try the game's full collection first. If enumeration yields nothing
            // or is interrupted, supplement it with the supported ID lookups below.
            try
            {
                MethodInfo getAll = null;
                foreach (MethodInfo method in loader.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name == "GetAll" && method.IsGenericMethodDefinition &&
                        method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 0)
                    { getAll = method; break; }
                }
                if (getAll != null)
                {
                    object templates = getAll.MakeGenericMethod(skillTemplate).Invoke(null, null);
                    if (templates != null)
                    {
                        foreach (object template in EnumerateSkillTemplates(templates))
                        {
                            if (template == null) continue;
                            string id = Convert.ToString(GetMember(template, "name") ?? GetMember(template, "Name") ?? "");
                            if (!string.IsNullOrEmpty(id) && !processedSkillIds.Add(id)) continue;
                            inspected++;
                            TryModifySkillPlanning(template, null, ref attacks, ref buffs);
                        }
                    }
                }
            }
            catch
            {
                // A failed or partial GetAll<T> iteration is supplemented below.
            }

            // Always supplement the full enumeration. This covers known game skills
            // even if an interop enumerator silently stops after yielding some entries.
            if (getById != null)
            {
                foreach (string id in KnownCombatSkillIds)
                    TryModifyKnownId(getById, id, false, processedSkillIds, ref inspected, ref lookupHits, ref attacks, ref buffs);
                foreach (string id in KnownSupportSkillIds)
                    TryModifyKnownId(getById, id, true, processedSkillIds, ref inspected, ref lookupHits, ref attacks, ref buffs);
            }

            if (attacks + buffs == 0)
            {
                RestoreSimplifiedCombatPlanning();
                CombatPlanningWarnOnce("no eligible skill templates updated (enumerated=" +
                    inspected + ", ID-lookup hits=" + lookupHits + "); vanilla AI retained");
                return;
            }
            _combatPlanningActive = true;
            _log.Msg($"{ModName}: simplified large-map combat planning: " +
                     $"{attacks} attack / {buffs} buff skills adjusted; " +
                     $"enumerated={inspected}, ID-lookup hits={lookupHits}. " +
                     "Native pathfinding and player movement unchanged.");
        }
        catch (Exception ex)
        {
            RestoreSimplifiedCombatPlanning();
            CombatPlanningWarnOnce("combat AI setup failed (" + ex.GetType().Name + "); vanilla AI retained");
        }
    }

    private static MethodInfo FindSkillTemplateIdLookup(Type loader, Type skillTemplate)
    {
        try
        {
            foreach (MethodInfo method in loader.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Get" || !method.IsGenericMethodDefinition ||
                    method.GetGenericArguments().Length != 1) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(bool))
                    return method.MakeGenericMethod(skillTemplate);
            }
        }
        catch { }
        return null;
    }

    private static void TryModifyKnownId(MethodInfo getById, string id, bool allowBuff,
        HashSet<string> processedSkillIds,
        ref int inspected, ref int lookupHits, ref int attacks, ref int buffs)
    {
        if (!processedSkillIds.Add(id)) return;
        try
        {
            object template = getById.Invoke(null, new object[] { id, false });
            if (template == null) return;
            lookupHits++;
            inspected++;
            TryModifySkillPlanning(template, allowBuff ? id : null, ref attacks, ref buffs);
        }
        catch
        {
            // IDs may be removed between game builds. Never fail mission load.
        }
    }

    private static void TryModifySkillPlanning(object template, string knownBuffId,
        ref int attacks, ref int buffs)
    {
        object config = null;
        bool wrote = false;
        try
        {
            config = GetMember(template, "AIConfig");
            if (config == null) return;
            object old = GetMember(config, "InfluencesPositioning");
            if (old == null || !Convert.ToBoolean(old)) return;

            bool isAttack = Convert.ToBoolean(GetMember(template, "IsAttack") ?? false);
            string typeName = config.GetType().FullName ?? "";
            // Scene setup primes GetID's lazy Unity-name cache on the main thread.
            // Worker scoring reads m_ID and must never call UnityEngine.Object.name.
            string templateId = knownBuffId ?? Convert.ToString(template.GetType().GetMethod("GetID",
                                        BindingFlags.Public | BindingFlags.Instance)?.Invoke(template, null) ?? GetMember(template, "name") ??
                                        GetMember(template, "Name") ?? "");
            bool isBuff = typeName.EndsWith(".Behaviors.Data.Buff", StringComparison.Ordinal) ||
                          typeName.EndsWith(".AI.Data.Buff", StringComparison.Ordinal) ||
                          templateId == "active.rogue_officer_whistle" ||
                          templateId == "active.rogue_ammo_runner_refill_ammo" ||
                          templateId == "active.use_target_designator" ||
                          templateId == "active.mark_target_constructs";
            if (!isAttack && !isBuff) return;

            if (!TrySetMember(config, "InfluencesPositioning", false)) return;
            wrote = true;
            if (Convert.ToBoolean(GetMember(config, "InfluencesPositioning") ?? true))
            {
                TrySetMember(config, "InfluencesPositioning", true);
                return;
            }

            _combatPlanningRestores.Add(new SkillPlanningRestore { Config = config, Original = true });
            wrote = false; // Scene-scoped restore now owns this native flag.
            if (isAttack) { attacks++; _simplifiedAttackIds.Add(templateId); }
            else buffs++;
        }
        catch
        {
            if (wrote && config != null)
            {
                try { TrySetMember(config, "InfluencesPositioning", true); } catch { }
            }
            // One incompatible interop skill must never prevent later IDs.
        }
    }

    private static void RestoreSimplifiedCombatPlanning()
    {
        foreach (SkillPlanningRestore entry in _combatPlanningRestores)
        {
            try { TrySetMember(entry.Config, "InfluencesPositioning", entry.Original); }
            catch { /* Never block tactical scene exit. */ }
        }
        _combatPlanningRestores.Clear();
        _simplifiedAttackIds.Clear();
        _combatPlanningActive = false;
    }

    private static IEnumerable<object> EnumerateSkillTemplates(object collection)
    {
        if (collection is IEnumerable managed)
        {
            foreach (object item in managed) yield return item;
            yield break;
        }

        // Most IL2CPP wrappers expose an enumerator without implementing the
        // CLR IEnumerable contract. Retain this fast enumeration path.
        MethodInfo enumMethod = collection.GetType().GetMethod("GetEnumerator",
            BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (enumMethod != null)
        {
            object enumerator = enumMethod.Invoke(collection, null);
            if (enumerator != null)
            {
                MethodInfo moveNext = enumerator.GetType().GetMethod("MoveNext",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (moveNext != null)
                {
                    try
                    {
                        while (Convert.ToBoolean(moveNext.Invoke(enumerator, null)))
                            yield return GetMember(enumerator, "Current");
                    }
                    finally
                    {
                        if (enumerator is IDisposable disposable) disposable.Dispose();
                    }
                    yield break;
                }
            }
        }

        int count = ToInt(GetMember(collection, "Count") ?? GetMember(collection, "Length"), -1);
        PropertyInfo indexer = collection.GetType().GetProperty("Item", new[] { typeof(int) });
        if (count >= 0 && count <= 10000 && indexer != null)
            for (int i = 0; i < count; ++i)
                yield return indexer.GetValue(collection, new object[] { i });
    }

    private static void CombatPlanningWarnOnce(string message)
    {
        if (_combatPlanningWarned) return;
        _combatPlanningWarned = true;
        _log.Warning($"{ModName}: simplified combat AI: {message}.");
    }
}
