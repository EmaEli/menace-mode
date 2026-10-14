using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jiangyu.Sdk;

namespace MenaceModSettings;

/// <summary>
/// JSON persistence for every group, in one file under MelonLoader's UserData folder:
/// <c>UserData/MenaceModSettings/settings.json</c>, shaped <c>{ "Group": { "Key": value } }</c>.
/// Values are kept as plain JSON scalars; the definition coerces them on read.
/// </summary>
internal static class SettingsStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string FilePath
    {
        get
        {
            string userData = null;
            try { userData = MelonUserDataDir(); }
            catch { }
            if (string.IsNullOrEmpty(userData))
                userData = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "UserData");
            return Path.Combine(userData, "MenaceModSettings", "settings.json");
        }
    }

    // Kept out of line so a missing MelonLoader assembly surfaces as a catchable exception at
    // the call site rather than failing the JIT of the caller.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string MelonUserDataDir() => MelonLoader.Utils.MelonEnvironment.UserDataDirectory;

    /// <summary>Read the whole file. Missing or unreadable file yields an empty map.</summary>
    public static Dictionary<string, Dictionary<string, object>> Load()
    {
        var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        var path = FilePath;
        if (!File.Exists(path))
            return result;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root == null)
                return result;
            foreach (var (group, node) in root)
            {
                if (node is not JsonObject values)
                    continue;
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var (key, value) in values)
                    map[key] = ToScalar(value);
                result[group] = map;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"settings.json could not be read, defaults will be used: {ex.Message}");
        }
        return result;
    }

    /// <summary>Write the whole map back, atomically (temp file + move).</summary>
    public static void Save(Dictionary<string, Dictionary<string, object>> data)
    {
        var path = FilePath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var root = new JsonObject();
            foreach (var (group, values) in data)
            {
                var obj = new JsonObject();
                foreach (var (key, value) in values)
                    obj[key] = FromScalar(value);
                root[group] = obj;
            }
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, root.ToJsonString(WriteOptions));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error($"settings.json could not be written: {ex.Message}");
        }
    }

    private static object ToScalar(JsonNode node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<bool>(out var b)) return b;
        if (value.TryGetValue<double>(out var d)) return d;
        if (value.TryGetValue<string>(out var s)) return s;
        return null;
    }

    private static JsonNode FromScalar(object value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        float f => JsonValue.Create(Math.Round(f, 4)),
        double d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(value.ToString()),
    };
}
