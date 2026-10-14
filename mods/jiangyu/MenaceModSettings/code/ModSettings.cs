using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MenaceModSettings;

/// <summary>A registered group: its definitions in declaration order and the current values.</summary>
public sealed class SettingsGroup
{
    public string Name { get; }
    public IReadOnlyList<SettingDefinition> Definitions { get; internal set; }
    internal Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);

    internal SettingsGroup(string name, IReadOnlyList<SettingDefinition> definitions)
    {
        Name = name;
        Definitions = definitions;
    }

    internal SettingDefinition Find(string key) =>
        Definitions.FirstOrDefault(d => d.Key != null && d.Key == key);
}

/// <summary>
/// Settings registry shared by every mod that depends on MenaceModSettings. The surface mirrors
/// the legacy Modpack Loader <c>ModSettings</c> (Register / Get / Set), so a module ports by
/// swapping the using. Values live in one JSON file for the whole game install, not per save.
/// </summary>
public static class ModSettings
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, SettingsGroup> Groups = new(StringComparer.Ordinal);
    private static Dictionary<string, Dictionary<string, object>> _stored;

    /// <summary>Raised after a value changes: (group, key, new value).</summary>
    public static event Action<string, string, object> Changed;

    /// <summary>Groups in registration order, for the settings window.</summary>
    public static IReadOnlyList<SettingsGroup> AllGroups
    {
        get { lock (Gate) return Groups.Values.ToList(); }
    }

    /// <summary>
    /// Declare (or re-declare, on hot reload) a group. Stored values that match a definition are
    /// coerced to it; the rest fall back to the declared default.
    /// </summary>
    public static void Register(string group, Action<SettingsBuilder> build)
    {
        if (string.IsNullOrEmpty(group) || build == null)
            return;
        var builder = new SettingsBuilder();
        build(builder);
        lock (Gate)
        {
            EnsureLoaded();
            if (!Groups.TryGetValue(group, out var g))
                Groups[group] = g = new SettingsGroup(group, builder.Items);
            else
                g.Definitions = builder.Items;

            _stored.TryGetValue(group, out var saved);
            foreach (var def in g.Definitions)
            {
                if (def.Key == null)
                    continue;
                object raw = saved != null && saved.TryGetValue(def.Key, out var v) ? v : def.Default;
                g.Values[def.Key] = def.Coerce(raw);
            }
        }
    }

    public static object Get(string group, string key)
    {
        lock (Gate)
        {
            if (Groups.TryGetValue(group, out var g) && g.Values.TryGetValue(key, out var v))
                return v;
        }
        throw new KeyNotFoundException($"ModSettings: '{group}/{key}' is not registered.");
    }

    public static T Get<T>(string group, string key) => Convert<T>(Get(group, key));

    public static bool TryGet<T>(string group, string key, out T value)
    {
        lock (Gate)
        {
            if (Groups.TryGetValue(group, out var g) && g.Values.TryGetValue(key, out var v))
            {
                try { value = Convert<T>(v); return true; }
                catch { }
            }
        }
        value = default;
        return false;
    }

    /// <summary>Set, coerce to the definition, persist and notify. Unknown keys are ignored.</summary>
    public static void Set(string group, string key, object value)
    {
        object coerced;
        lock (Gate)
        {
            if (!Groups.TryGetValue(group, out var g))
                return;
            var def = g.Find(key);
            if (def == null)
                return;
            coerced = def.Coerce(value);
            if (g.Values.TryGetValue(key, out var old) && Equals(old, coerced))
                return;
            g.Values[key] = coerced;
            EnsureLoaded();
            if (!_stored.TryGetValue(group, out var saved))
                _stored[group] = saved = new Dictionary<string, object>(StringComparer.Ordinal);
            saved[key] = coerced;
            SettingsStore.Save(_stored);
        }
        Changed?.Invoke(group, key, coerced);
    }

    /// <summary>Restore every value of a group to its declared default.</summary>
    public static void ResetGroup(string group)
    {
        List<SettingDefinition> defs;
        lock (Gate)
        {
            if (!Groups.TryGetValue(group, out var g))
                return;
            defs = g.Definitions.Where(d => d.Key != null).ToList();
        }
        foreach (var def in defs)
            Set(group, def.Key, def.Default);
    }

    private static void EnsureLoaded() => _stored ??= SettingsStore.Load();

    private static T Convert<T>(object value)
    {
        if (value is T typed)
            return typed;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (target.IsEnum)
            return (T)Enum.Parse(target, value.ToString(), ignoreCase: true);
        return (T)System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
