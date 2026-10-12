using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using MelonLoader;
using Menace.ModpackLoader;
using UnityEngine;

[assembly: AssemblyVersion("0.3.1.0")]
[assembly: AssemblyFileVersion("0.3.1.0")]
[assembly: AssemblyInformationalVersion("0.3.1")]

namespace MenaceCoverDestructionRebalance;

/// <summary>
/// Modpack Loader host for Cover Destruction Rebalance 0.3.1.
/// Keeps the accepted general rebalance (1 stays 1, 2 -> 1, 3 -> 2,
/// gunslinger 3 -> 1) and keeps light/medium infantry mortar HE from destroying half-cover.
/// </summary>
public sealed class Plugin : IModpackPlugin
{
    private const string SpecialHeavyToLightSkill = "active.laser_rifle_gunslinger";

    private static readonly HashSet<string> MortarCoverImmuneSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "active.launch_light_mortar_frag",
        "active.launch_medium_mortar_frag",
    };

    private MelonLogger.Instance? _logger;
    private bool _applied;
    private int _attempts;
    private static MelonLogger.Instance? _runtimeLogger;
    private static bool _mortarRuntimeGuardInstalled;
    private static bool _mortarRuntimeGuardErrorReported;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _runtimeLogger = logger;
        InstallMortarRuntimeGuard(harmony);
        TryApplyPatches("initialization");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (!_applied)
            TryApplyPatches($"scene {buildIndex} ({sceneName})");
    }

    public void OnUpdate()
    {
        // No per-frame work. Intentionally silent after the one-time template pass.
    }

    public void OnUnload()
    {
        // Template values live for the process lifetime. Nothing to unhook.
    }

    private static void InstallMortarRuntimeGuard(global::HarmonyLib.Harmony harmony)
    {
        if (_mortarRuntimeGuardInstalled)
            return;

        try
        {
            MethodInfo? onTileHit = typeof(AttackHandler).GetMethod(
                nameof(AttackHandler.OnTileHit),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Skill), typeof(Tile), typeof(Tile), typeof(Vector3) },
                modifiers: null);

            MethodInfo? prefix = typeof(Plugin).GetMethod(
                nameof(MortarCoverGuardPrefix),
                BindingFlags.Static | BindingFlags.NonPublic);

            if (onTileHit == null || prefix == null)
            {
                _runtimeLogger?.Error(
                    $"Could not install mortar half-cover runtime guard. OnTileHit={onTileHit != null}, prefix={prefix != null}.");
                return;
            }

            harmony.Patch(onTileHit, prefix: new global::HarmonyLib.HarmonyMethod(prefix));
            _mortarRuntimeGuardInstalled = true;
        }
        catch (Exception ex)
        {
            _runtimeLogger?.Error($"Failed to install mortar half-cover runtime guard: {ex}");
        }
    }

    // Startup template edits are useful, but Modpack Loader/game initialization can rebuild or
    // replace skill handler objects afterwards. Enforce the mortar exception again at the exact
    // runtime point before AttackHandler performs half-cover destruction. No state is restored:
    // each Attack handler template belongs to its skill, and these two skills are intentionally
    // permanent DestroyHalfCover=None for the whole process.
    private static void MortarCoverGuardPrefix(AttackHandler __instance, Skill __0)
    {
        if (__instance == null || __0 == null)
            return;

        try
        {
            string skillId = __0.GetID() ?? string.Empty;
            if (!MortarCoverImmuneSkills.Contains(skillId))
                return;

            Attack? template = __instance.GetTemplate();
            if (template != null && template.DestroyHalfCover != HalfCoverClass.None)
                template.DestroyHalfCover = HalfCoverClass.None;
        }
        catch (Exception ex)
        {
            if (_mortarRuntimeGuardErrorReported)
                return;

            _mortarRuntimeGuardErrorReported = true;
            _runtimeLogger?.Error($"Mortar half-cover runtime guard failed: {ex}");
        }
    }

    private void TryApplyPatches(string trigger)
    {
        if (_applied)
            return;

        _attempts++;

        try
        {
            Type? loaderType = FindRuntimeType(
                "Il2CppMenace.Tools.DataTemplateLoader",
                "Menace.Tools.DataTemplateLoader");
            Type? skillType = FindRuntimeType(
                "Il2CppMenace.Tactical.Skills.SkillTemplate",
                "Menace.Tactical.Skills.SkillTemplate");

            if (loaderType == null || skillType == null)
            {
                if (_attempts >= 3)
                {
                    Error(
                        $"Required MENACE template types were not found after {_attempts} attempt(s). " +
                        $"loader={(loaderType == null ? "missing" : loaderType.FullName)}, " +
                        $"skill={(skillType == null ? "missing" : skillType.FullName)}.");
                }
                return;
            }

            MethodInfo? getAllDefinition = loaderType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.Name == "GetAll" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 0);

            if (getAllDefinition == null)
            {
                Error("DataTemplateLoader.GetAll<T>() was not found.");
                return;
            }

            MemberInfo? eventHandlersMember = FindReadableMember(skillType, "EventHandlers");
            if (eventHandlersMember == null)
            {
                Error("SkillTemplate.EventHandlers was not exposed as a readable field/property/getter.");
                return;
            }

            MethodInfo? getId = skillType.GetMethod(
                "GetID",
                BindingFlags.Public | BindingFlags.Instance);

            object? skills = getAllDefinition.MakeGenericMethod(skillType).Invoke(null, null);
            if (skills == null)
                return;

            int skillCount = 0;

            foreach (object skill in EnumerateItems(skills))
            {
                if (skill == null)
                    continue;

                skillCount++;
                string skillId = "<unknown>";
                try
                {
                    skillId = Convert.ToString(getId?.Invoke(skill, null)) ?? "<unknown>";
                }
                catch
                {
                    // The rebalance itself does not depend on an ID except for one special case.
                }

                object? handlers = ReadMember(eventHandlersMember, skill);
                foreach (object handler in EnumerateItems(handlers))
                {
                    if (handler == null)
                        continue;

                    MemberInfo? destroyMember = FindWritableMember(handler.GetType(), "DestroyHalfCover");
                    if (destroyMember == null)
                        continue;

                    object? currentValue = ReadMember(destroyMember, handler);
                    if (currentValue == null)
                        continue;

                    int current = Convert.ToInt32(currentValue);
                    int target = current;

                    // Light/medium infantry mortar HE should suppress and damage troops without
                    // erasing field fortifications. Sandbags and low walls are represented by MENACE
                    // half-cover, so disable half-cover destruction for those two mortar skills only.
                    if (MortarCoverImmuneSkills.Contains(skillId))
                    {
                        target = 0;
                    }
                    else if (current == 2)
                    {
                        target = 1;
                    }
                    else if (current == 3)
                    {
                        if (string.Equals(skillId, SpecialHeavyToLightSkill, StringComparison.Ordinal))
                        {
                            target = 1;
                        }
                        else
                        {
                            target = 2;
                        }
                    }

                    if (target == current)
                        continue;

                    Type valueType = GetMemberValueType(destroyMember);
                    object replacement = valueType.IsEnum
                        ? Enum.ToObject(valueType, target)
                        : Convert.ChangeType(target, valueType);
                    WriteMember(destroyMember, handler, replacement);
                }
            }

            if (skillCount <= 0)
                return;

            _applied = true;
            // Successful one-time template application is intentionally silent.
            // Keep routine logging at zero; only real failures are reported.
        }
        catch (Exception ex)
        {
            Error($"Failed to apply cover-destruction rebalance during {trigger}: {ex}");
        }
    }

    private static MemberInfo? FindReadableMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        FieldInfo? field = type.GetField(name, flags);
        if (field != null)
            return field;

        PropertyInfo? property = type.GetProperty(name, flags);
        if (property != null && property.GetGetMethod(true) != null)
            return property;

        return type.GetMethod("get_" + name, flags, null, Type.EmptyTypes, null);
    }

    private static MemberInfo? FindWritableMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        FieldInfo? field = type.GetField(name, flags);
        if (field != null && !field.IsInitOnly)
            return field;

        PropertyInfo? property = type.GetProperty(name, flags);
        if (property != null && property.GetSetMethod(true) != null)
            return property;

        return null;
    }

    private static object? ReadMember(MemberInfo member, object instance)
    {
        if (member is FieldInfo field)
            return field.GetValue(instance);
        if (member is PropertyInfo property)
            return property.GetValue(instance, null);
        if (member is MethodInfo method)
            return method.Invoke(instance, null);
        throw new NotSupportedException("Unsupported member type: " + member.MemberType);
    }

    private static void WriteMember(MemberInfo member, object instance, object value)
    {
        if (member is FieldInfo field)
        {
            field.SetValue(instance, value);
            return;
        }
        if (member is PropertyInfo property)
        {
            property.SetValue(instance, value, null);
            return;
        }
        throw new NotSupportedException("Member is not writable: " + member.Name);
    }

    private static Type GetMemberValueType(MemberInfo member)
    {
        if (member is FieldInfo field)
            return field.FieldType;
        if (member is PropertyInfo property)
            return property.PropertyType;
        if (member is MethodInfo method)
            return method.ReturnType;
        throw new NotSupportedException("Unsupported member type: " + member.MemberType);
    }

    private static Type? FindRuntimeType(params string[] fullNames)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (string fullName in fullNames)
        {
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type? type = assembly.GetType(fullName, false);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Some dynamic/interop assemblies may reject type lookup while still loading.
                }
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateItems(object? collection)
    {
        if (collection == null)
            yield break;

        if (collection is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item != null)
                    yield return item;
            }
            yield break;
        }

        MethodInfo? getEnumerator = collection.GetType().GetMethod(
            "GetEnumerator",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);
        object? enumerator = getEnumerator?.Invoke(collection, null);
        if (enumerator == null)
            yield break;

        Type enumeratorType = enumerator.GetType();
        MethodInfo? moveNext = enumeratorType.GetMethod(
            "MoveNext",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);
        PropertyInfo? current = enumeratorType.GetProperty(
            "Current",
            BindingFlags.Public | BindingFlags.Instance);

        if (moveNext == null || current == null)
            yield break;

        while (Convert.ToBoolean(moveNext.Invoke(enumerator, null)))
        {
            object? value = current.GetValue(enumerator, null);
            if (value != null)
                yield return value;
        }
    }

    private void Error(string message) => _logger?.Error(message);
}
