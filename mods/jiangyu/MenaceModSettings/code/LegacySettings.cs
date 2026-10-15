using System.Text.Json;

namespace MenaceModSettings;

// Only the recognized group -> scalar-key object format is imported. Unknown structures
// are skipped, never guessed/flattened. Originals and unrelated groups are never written.
internal static class LegacySettings
{
    public static Dictionary<string, object> Read(string json, string group)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return values;
        string alias = group switch
        {
            "Cinematic Action Camera" => "Third Person Camera",
            "Squaddie Veterancy" => "SquaddieProbe",
            _ => group,
        };
        foreach (string name in new[] { alias, group })
        {
            if (!document.RootElement.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object) continue;
            foreach (var entry in node.EnumerateObject())
            {
                object value = entry.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => entry.Value.GetDouble(),
                    JsonValueKind.String => entry.Value.GetString(),
                    _ => null,
                };
                if (value != null) values[entry.Name] = value;
            }
        }
        return values;
    }
}