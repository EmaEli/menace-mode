#if JIANGYU
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Objectives;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tools;
using UnityEngine;

// Stand-ins for the subset of the Modpack Loader's Menace.SDK that the suite modules use
// (template field access, faction names, mission status). Kept in the Menace.SDK namespace
// so the shared module sources compile unchanged under Jiangyu. Not compiled into the
// legacy modpack DLLs, which get the real SDK from Menace.ModpackLoader.dll.
namespace Menace.SDK;

internal enum FactionType
{
    Neutral = 0, Player = 1, PlayerAI = 2, Civilian = 3, AlliedLocalForces = 4,
    EnemyLocalForces = 5, Pirates = 6, Wildlife = 7, Constructs = 8, RogueArmy = 9,
}

internal static class TacticalController
{
    public static string GetFactionName(FactionType faction) => faction.ToString();
}

internal static class Inventory
{
    // Adds one instance of an item template (by template id) to the campaign's owned items.
    // Result strings follow the SDK convention: "Spawned: <id>" on success, otherwise a reason.
    public static string SpawnItem(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName)) return "Error: empty template name";
        try
        {
            var owned = Il2CppMenace.States.StrategyState.Get()?.OwnedItems;
            if (owned == null) return "Error: no strategy state";
            foreach (UnityEngine.Object? obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Il2CppMenace.Items.BaseItemTemplate>()))
            {
                var template = obj?.TryCast<Il2CppMenace.Items.BaseItemTemplate>();
                if (template == null) continue;
                if (!string.Equals(template.GetID(), templateName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(template.name, templateName, StringComparison.OrdinalIgnoreCase))
                    continue;
                return owned.AddItem(template, false) != null
                    ? "Spawned: " + templateName
                    : "Error: AddItem returned null for " + templateName;
            }
            return "Error: template not found: " + templateName;
        }
        catch (Exception e) { return "Error: " + e.Message; }
    }
}

internal static class GameState
{
    public static void RunDelayed(int frames, Action callback)
        => MelonLoader.MelonCoroutines.Start(Delay(frames, callback));

    private static System.Collections.IEnumerator Delay(int frames, Action callback)
    {
        for (int i = 0; i < frames; i++) yield return null;
        callback();
    }
}

internal static class Mission
{
    // Same numbering as ObjectiveState (Hidden/Ongoing/Completed/Failed).
    public const int STATUS_PENDING = 0;
    public const int STATUS_ACTIVE = 1;
    public const int STATUS_COMPLETE = 2;
    public const int STATUS_FAILED = 3;

    public static int GetStatus()
    {
        try
        {
            var objectives = TacticalManager.Get()?.GetMission()?.Objectives;
            return objectives == null ? STATUS_PENDING : (int)objectives.GetPrimaryObjectivesState();
        }
        catch { return STATUS_PENDING; }
    }
}

internal readonly struct GameObj : IEquatable<GameObj>
{
    public IntPtr Pointer { get; }

    public GameObj(IntPtr pointer) => Pointer = pointer;

    public static GameObj Null => default;
    public bool IsNull => Pointer == IntPtr.Zero;

    public IntPtr ReadPtr(uint offset) => IsNull ? IntPtr.Zero : Marshal.ReadIntPtr(Pointer, (int)offset);
    public int ReadInt(uint offset) => IsNull ? 0 : Marshal.ReadInt32(Pointer, (int)offset);
    public IntPtr ReadPtr(string fieldName, uint fallbackOffset) => ReadPtr(FieldOffset(fieldName) ?? fallbackOffset);
    public int ReadInt(string fieldName, uint fallbackOffset) => ReadInt(FieldOffset(fieldName) ?? fallbackOffset);

    // DataTemplate ID (the SDK's GetName); null for non-template objects.
    public string? GetName()
    {
        if (IsNull) return null;
        try { return new DataTemplate(Pointer).GetID(); }
        catch { return null; }
    }

    public string? GetTypeName()
    {
        if (IsNull) return null;
        IntPtr klass = IL2CPP.il2cpp_object_get_class(Pointer);
        if (klass == IntPtr.Zero) return null;
        string? ns = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(klass));
        string? name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass));
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    private uint? FieldOffset(string fieldName)
    {
        if (IsNull) return null;
        IntPtr klass = IL2CPP.il2cpp_object_get_class(Pointer);
        IntPtr field = klass == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_get_field_from_name(klass, fieldName);
        return field == IntPtr.Zero ? null : IL2CPP.il2cpp_field_get_offset(field);
    }

    // Most-derived Il2CppInterop proxy for the object, as the SDK does.
    public object? ToManaged()
    {
        if (IsNull) return null;
        try
        {
            Type? type = GetTypeName() is { } name ? Il2CppTypes.Resolve("Il2Cpp" + name) : null;
            if (type != null && Activator.CreateInstance(type, Pointer) is { } typed) return typed;
        }
        catch { }
        return new Il2CppSystem.Object(Pointer);
    }

    public bool Equals(GameObj other) => Pointer == other.Pointer;
    public override bool Equals(object? obj) => obj is GameObj other && Equals(other);
    public override int GetHashCode() => Pointer.GetHashCode();
    public static bool operator ==(GameObj left, GameObj right) => left.Pointer == right.Pointer;
    public static bool operator !=(GameObj left, GameObj right) => left.Pointer != right.Pointer;
}

// Static events mirroring the SDK's TacticalEventHooks; the Harmony patches are installed
// on first subscription and raise raw pointers like the SDK does.
internal static class TacticalEventHooks
{
    private static readonly object Gate = new();
    private static global::HarmonyLib.Harmony? _harmony;

    private static event Action<IntPtr>? ElementMalfunction;
    private static event Action<IntPtr>? EntitySpawned;
    private static event Action<IntPtr, IntPtr, IntPtr>? SkillUsed;
    private static event Action<IntPtr, int>? ObjectiveStateChanged;

    public static event Action<IntPtr> OnElementMalfunction
    {
        add { EnsurePatched(); ElementMalfunction += value; }
        remove => ElementMalfunction -= value;
    }

    public static event Action<IntPtr> OnEntitySpawned
    {
        add { EnsurePatched(); EntitySpawned += value; }
        remove => EntitySpawned -= value;
    }

    public static event Action<IntPtr, IntPtr, IntPtr> OnSkillUsed
    {
        add { EnsurePatched(); SkillUsed += value; }
        remove => SkillUsed -= value;
    }

    public static event Action<IntPtr, int> OnObjectiveStateChanged
    {
        add { EnsurePatched(); ObjectiveStateChanged += value; }
        remove => ObjectiveStateChanged -= value;
    }

    private static void EnsurePatched()
    {
        lock (Gate)
        {
            if (_harmony != null) return;
            _harmony = new global::HarmonyLib.Harmony("menacemodsuite.sdkshim.tacticalevents");
            Type self = typeof(TacticalEventHooks);
            _harmony.Patch(AccessTools.Method(typeof(TacticalManager), nameof(TacticalManager.InvokeOnElementMalfunction)),
                postfix: new HarmonyMethod(self, nameof(MalfunctionPostfix)));
            _harmony.Patch(AccessTools.Method(typeof(TacticalManager), nameof(TacticalManager.InvokeOnEntitySpawned)),
                postfix: new HarmonyMethod(self, nameof(EntitySpawnedPostfix)));
            _harmony.Patch(AccessTools.Method(typeof(Skill), nameof(Skill.Use), new[] { typeof(Tile), typeof(UsageParameter) }),
                postfix: new HarmonyMethod(self, nameof(SkillUsePostfix)));
            _harmony.Patch(AccessTools.Method(typeof(Objective), nameof(Objective.SetState)),
                postfix: new HarmonyMethod(self, nameof(ObjectiveSetStatePostfix)));
        }
    }

    private static void MalfunctionPostfix(Element _element)
    {
        if (_element != null) ElementMalfunction?.Invoke(_element.Pointer);
    }

    private static void EntitySpawnedPostfix(Entity _entity)
    {
        if (_entity != null) EntitySpawned?.Invoke(_entity.Pointer);
    }

    private static void SkillUsePostfix(Skill __instance, Tile _targetTile, bool __result)
    {
        if (!__result || __instance == null) return;
        IntPtr user = IntPtr.Zero;
        try { user = __instance.GetEntity()?.Pointer ?? IntPtr.Zero; } catch { }
        SkillUsed?.Invoke(user, __instance.Pointer, _targetTile?.Pointer ?? IntPtr.Zero);
    }

    private static void ObjectiveSetStatePostfix(Objective __instance, ObjectiveState _state)
    {
        if (__instance != null) ObjectiveStateChanged?.Invoke(__instance.Pointer, (int)_state);
    }
}

internal static class Templates
{
    private static readonly Dictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public static GameObj Find(string templateTypeName, string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName)) return GameObj.Null;
        Type? type = ResolveTemplateType(templateTypeName);
        if (type == null) return GameObj.Null;
        try
        {
            foreach (UnityEngine.Object? obj in Resources.FindObjectsOfTypeAll(Il2CppType.From(type)))
            {
                DataTemplate? template = obj?.TryCast<DataTemplate>();
                if (template == null) continue;
                if (string.Equals(template.GetID(), instanceName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(template.name, instanceName, StringComparison.OrdinalIgnoreCase))
                    return new GameObj(template.Pointer);
            }
        }
        catch { }
        return GameObj.Null;
    }

    public static GameObj[] FindAll(string templateTypeName)
    {
        Type? type = ResolveTemplateType(templateTypeName);
        if (type == null) return Array.Empty<GameObj>();
        var result = new List<GameObj>();
        try
        {
            foreach (UnityEngine.Object? obj in Resources.FindObjectsOfTypeAll(Il2CppType.From(type)))
            {
                DataTemplate? template = obj?.TryCast<DataTemplate>();
                if (template != null) result.Add(new GameObj(template.Pointer));
            }
        }
        catch { }
        return result.ToArray();
    }

    public static Sprite? FindSprite(string spriteName) => MenaceModSuite.Shared.SpriteLookup.Find(spriteName);

    public static object? ReadField(GameObj template, string fieldName)
    {
        object? managed = template.ToManaged();
        if (managed == null) return null;
        try
        {
            return FindMember(managed.GetType(), fieldName) switch
            {
                PropertyInfo p => p.GetValue(managed),
                FieldInfo f => f.GetValue(managed),
                _ => null,
            };
        }
        catch { return null; }
    }

    public static bool WriteField(GameObj template, string fieldName, object value)
    {
        object? managed = template.ToManaged();
        if (managed == null) return false;
        try
        {
            switch (FindMember(managed.GetType(), fieldName))
            {
                case PropertyInfo p when p.CanWrite: p.SetValue(managed, Coerce(value, p.PropertyType)); return true;
                case FieldInfo f when !f.IsInitOnly: f.SetValue(managed, Coerce(value, f.FieldType)); return true;
                default: return false;
            }
        }
        catch { return false; }
    }

    private static object Coerce(object value, Type target)
    {
        if (target.IsEnum && value is IConvertible && value is not string) return Enum.ToObject(target, value);
        return target.IsPrimitive && value is IConvertible ? Convert.ChangeType(value, target) : value;
    }

    private static MemberInfo? FindMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        for (Type? t = type; t != null; t = t.BaseType)
        {
            MemberInfo? m = (MemberInfo?)t.GetProperty(name, flags) ?? t.GetField(name, flags);
            if (m != null) return m;
        }
        return null;
    }

    private static Type? ResolveTemplateType(string name)
    {
        lock (TypeCache)
        {
            if (TypeCache.TryGetValue(name, out Type? cached)) return cached;
            Type? found = Il2CppTypes.All().FirstOrDefault(t => t.Name == name && typeof(DataTemplate).IsAssignableFrom(t));
            TypeCache[name] = found;
            return found;
        }
    }
}

// Lookup of Il2CppInterop proxy types across the loaded Il2Cpp* assemblies.
internal static class Il2CppTypes
{
    private static readonly Dictionary<string, Type?> FullNameCache = new(StringComparer.Ordinal);

    public static IEnumerable<Type> All()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Il2Cpp", StringComparison.Ordinal) == true)
            .SelectMany(SafeTypes);

    public static Type? Resolve(string fullName)
    {
        lock (FullNameCache)
        {
            if (FullNameCache.TryGetValue(fullName, out Type? cached)) return cached;
            Type? found = All().FirstOrDefault(t => t.FullName == fullName);
            FullNameCache[fullName] = found;
            return found;
        }
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}
#endif
