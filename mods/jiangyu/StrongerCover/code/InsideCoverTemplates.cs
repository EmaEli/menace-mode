using Il2CppInterop.Runtime;
using Il2CppMenace.Tactical.Skills;
using UnityEngine;

namespace StrongerCover;

/// <summary>
/// Pushes the inside-cover sliders into the live InsideCoverTemplate assets (bunker, forest,
/// open vehicle, heavy watchtower, garrisoned building). The vanilla values are remembered on
/// first touch so disabling the mod or unloading restores them.
/// </summary>
internal static class InsideCoverTemplates
{
    private static readonly Dictionary<string, float> Original = new(StringComparer.OrdinalIgnoreCase);

    public static void Apply(StrongerCoverSystem owner)
    {
        try
        {
            var classified = Classify(out var unmatched);
            if (classified.Count == 0)
            {
                owner.Warn("No InsideCoverTemplate instances are currently available.");
                return;
            }

            int changed = 0;
            changed += Write(classified, "bunker", CoverSettings.Bunker, owner) ? 1 : 0;
            changed += Write(classified, "forest", CoverSettings.Forest, owner) ? 1 : 0;
            changed += Write(classified, "vehicle", CoverSettings.OpenVehicle, owner) ? 1 : 0;
            changed += Write(classified, "watchtower", CoverSettings.Watchtower, owner) ? 1 : 0;
            changed += Write(classified, "building", CoverSettings.Building, owner) ? 1 : 0;

            owner.Debug($"inside cover: {changed} profile(s) written, enabled={CoverSettings.Enabled}, "
                + $"garrisonable building profile={(classified.ContainsKey("building") ? "present" : "not found")}.");
            foreach (var item in unmatched)
                owner.Debug($"inside cover: unknown template left unchanged: '{item.name}'.");
        }
        catch (Exception e)
        {
            owner.Warn("Could not update InsideCoverTemplate values: " + e.Message);
        }
    }

    /// <summary>Garrisonable Buildings changed its base value: that is now what "vanilla" means for the building profile.</summary>
    public static void UpdateBuildingBase(float baseAccuracy)
    {
        try
        {
            foreach (var template in All())
                if (ClassifyName(template.name) == "building")
                    Original[template.name] = Math.Clamp(baseAccuracy, 0.01f, 1.0f);
        }
        catch { }
    }

    public static void Restore()
    {
        if (Original.Count == 0)
            return;
        try
        {
            foreach (var template in All())
                if (Original.TryGetValue(template.name, out float original))
                    template.AccuracyMult = original;
        }
        catch { }
    }

    private static bool Write(Dictionary<string, InsideCoverTemplate> classified, string category, float configured, StrongerCoverSystem owner)
    {
        if (!classified.TryGetValue(category, out var template))
            return false;
        string name = template.name ?? category;

        if (!Original.ContainsKey(name))
        {
            // The live building profile may already reflect StrongerCover (Garrisonable Buildings
            // has a compatibility fallback), so prefer its declared base over the live value.
            Original[name] = category == "building" && CoverSettings.TryGetGarrisonBuildingBase(out float buildingBase)
                ? buildingBase
                : template.AccuracyMult;
        }

        float value = configured;
        if (!CoverSettings.Enabled && Original.TryGetValue(name, out float original))
            value = original;

        template.AccuracyMult = value;
        float readBack = template.AccuracyMult;
        if (Math.Abs(readBack - value) > 0.0005f)
        {
            owner.Warn($"inside cover '{name}': wrote AccuracyMult={value:0.00}, game reports {readBack:0.00}.");
            return false;
        }
        return true;
    }

    private static Dictionary<string, InsideCoverTemplate> Classify(out List<InsideCoverTemplate> unmatched)
    {
        var classified = new Dictionary<string, InsideCoverTemplate>(StringComparer.OrdinalIgnoreCase);
        unmatched = new List<InsideCoverTemplate>();
        foreach (var template in All())
        {
            string category = ClassifyName(template.name ?? string.Empty);
            if (category.Length == 0) unmatched.Add(template);
            else classified[category] = template;
        }

        // One leftover next to the three known outdoor profiles is the vegetation profile under a
        // name this list does not know yet.
        if (!classified.ContainsKey("forest") && unmatched.Count == 1 &&
            classified.ContainsKey("bunker") && classified.ContainsKey("vehicle") && classified.ContainsKey("watchtower"))
        {
            classified["forest"] = unmatched[0];
            unmatched.Clear();
        }
        return classified;
    }

    // Shipped assets: cover_inside_bunker, cover_inside_forest, cover_inside_vehicle_open,
    // cover_inside_vehicle_closed, cover_inside_watchtower_heavy; Garrisonable Buildings adds
    // cover_inside_building. The closed vehicle profile is recognised but left at vanilla.
    private static string ClassifyName(string name)
    {
        string value = name.ToLowerInvariant();
        if (value.Contains("bunker")) return "bunker";
        if (value.Contains("forest") || value.Contains("vegetation") || value.Contains("woods")) return "forest";
        if (value.Contains("watchtower") || value.Contains("watch_tower") || value.Contains("tower_heavy")) return "watchtower";
        if (value.Contains("cover_inside_building") || value.Contains("garrisonable_building")) return "building";
        if (value.Contains("vehicle") && value.Contains("open")) return "vehicle";
        if (value.Contains("vehicle") && value.Contains("closed")) return "closed_vehicle";
        return string.Empty;
    }

    private static IEnumerable<InsideCoverTemplate> All()
    {
        var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<InsideCoverTemplate>());
        if (objects == null)
            yield break;
        foreach (var obj in objects)
        {
            var template = obj?.TryCast<InsideCoverTemplate>();
            if (template != null)
                yield return template;
        }
    }
}
