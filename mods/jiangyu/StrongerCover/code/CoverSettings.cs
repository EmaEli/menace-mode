using MenaceModSettings;

namespace StrongerCover;

/// <summary>
/// The mod's options, declared in the MenaceModSettings window and cached here as plain
/// fields for the GetCoverMult hot path. Keys, labels, ranges and defaults are those of the
/// legacy GameplaySuite module (2.2.3) so an existing tuning carries over unchanged.
/// </summary>
internal static class CoverSettings
{
    public const string Group = "StrongerCover";
    public const string GarrisonGroup = "Garrisonable Buildings";
    public const string GarrisonBuildingAccuracyKey = "BuildingAccuracy";

    public static bool Enabled = true;
    public static bool RespectCoverUsage = true;
    public static bool SquadSizeScaling = true;
    public static bool ExcludeWildlife = true;
    public static bool ExcludeConstructs = true;
    public static bool VerboseLogging;
    public static bool IndirectFireCover = true;
    public static bool IndirectAverageSurroundingCover = true;
    public static bool IndirectInsideCover = true;
    public static int MinimumCoverDistance = 2;

    /// <summary>Index = CoverType (None, Light, Medium, Heavy); value = hit-chance multiplier.</summary>
    public static readonly float[] CoverMultipliers = { 1.0f, 0.80f, 0.60f, 0.40f };
    public static float SmallSquad = 0.70f;
    public static float MediumSquad = 0.80f;
    public static float LargeSquad = 0.90f;
    public static float Bunker = 0.20f;
    public static float Forest = 0.55f;
    public static float OpenVehicle = 0.60f;
    public static float Watchtower = 0.30f;
    public static float Building = 0.30f;

    public static void Register()
    {
        ModSettings.Register(Group, s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("EnableGameplay", "Enable StrongerCover", true);
            s.AddSlider("MinimumCoverDistance", "Minimum cover distance", 1f, 6f, 2f, 1f);
            s.AddToggle("RespectCoverUsage", "Respect defender Cover Usage", true);

            s.AddHeader("Directional Cover");
            s.AddSlider("LightCover", "Light cover hit chance (%)", 10f, 100f, 80f, 5f);
            s.AddSlider("MediumCover", "Medium cover hit chance (%)", 10f, 100f, 60f, 5f);
            s.AddSlider("HeavyCover", "Heavy cover hit chance (%)", 10f, 100f, 40f, 5f);

            s.AddHeader("Inside Cover");
            s.AddSlider("BunkerCover", "Bunker hit chance (%)", 5f, 100f, 20f, 5f);
            s.AddSlider("ForestCover", "Forest / vegetation hit chance (%)", 5f, 100f, 55f, 5f);
            s.AddSlider("OpenVehicleCover", "Open vehicle hit chance (%)", 5f, 100f, 60f, 5f);
            s.AddSlider("WatchtowerCover", "Heavy watchtower hit chance (%)", 5f, 100f, 30f, 5f);
            s.AddSlider("BuildingCover", "Garrisoned building hit chance (%)", 5f, 100f, 30f, 5f);

            s.AddHeader("Indirect Fire");
            s.AddToggle("EnableIndirectFireCover", "Enable indirect-fire cover", true);
            s.AddToggle("IndirectAverageSurroundingCover", "Use averaged surrounding cover", true);
            s.AddToggle("IndirectInsideCover", "Use inside cover protection", true);

            s.AddHeader("Squad Size");
            s.AddToggle("SquadSizeScaling", "Enable squad-size scaling", true);
            s.AddSlider("SmallSquad", "1-3 soldiers hit chance (%)", 40f, 100f, 70f, 5f);
            s.AddSlider("MediumSquad", "4-5 soldiers hit chance (%)", 40f, 100f, 80f, 5f);
            s.AddSlider("LargeSquad", "6-7 soldiers hit chance (%)", 40f, 100f, 90f, 5f);
            s.AddToggle("ExcludeWildlife", "Exclude Wildlife from squad scaling", true);
            s.AddToggle("ExcludeConstructs", "Exclude Constructs from squad scaling", true);

            s.AddHeader("Diagnostics");
            s.AddToggle("VerboseLogging", "Debug logging", false);
        });
        Reload();
    }

    public static void Reload()
    {
        Enabled = Get("EnableGameplay", true);
        RespectCoverUsage = Get("RespectCoverUsage", true);
        SquadSizeScaling = Get("SquadSizeScaling", true);
        ExcludeWildlife = Get("ExcludeWildlife", true);
        ExcludeConstructs = Get("ExcludeConstructs", true);
        VerboseLogging = Get("VerboseLogging", false);
        IndirectFireCover = Get("EnableIndirectFireCover", true);
        IndirectAverageSurroundingCover = Get("IndirectAverageSurroundingCover", true);
        IndirectInsideCover = Get("IndirectInsideCover", true);
        MinimumCoverDistance = Math.Clamp((int)Math.Round(Get("MinimumCoverDistance", 2f)), 1, 6);

        CoverMultipliers[1] = Percent(Get("LightCover", 80f));
        CoverMultipliers[2] = Percent(Get("MediumCover", 60f));
        CoverMultipliers[3] = Percent(Get("HeavyCover", 40f));
        Bunker = Percent(Get("BunkerCover", 20f));
        Forest = Percent(Get("ForestCover", 55f));
        OpenVehicle = Percent(Get("OpenVehicleCover", 60f));
        Watchtower = Percent(Get("WatchtowerCover", 30f));
        Building = Percent(Get("BuildingCover", 30f));
        SmallSquad = Percent(Get("SmallSquad", 70f));
        MediumSquad = Percent(Get("MediumSquad", 80f));
        LargeSquad = Percent(Get("LargeSquad", 90f));
    }

    /// <summary>Keys whose change must be pushed into the live InsideCoverTemplate assets.</summary>
    public static bool AffectsInsideCover(string key) =>
        key is "EnableGameplay" or "BunkerCover" or "ForestCover" or "OpenVehicleCover" or "WatchtowerCover" or "BuildingCover";

    /// <summary>
    /// Garrisonable Buildings owns the base building profile. When its group is registered
    /// (the module is installed and ported), its BuildingAccuracy is the value to restore.
    /// </summary>
    public static bool TryGetGarrisonBuildingBase(out float value)
    {
        if (ModSettings.TryGet<float>(GarrisonGroup, GarrisonBuildingAccuracyKey, out float percent))
        {
            value = Percent(percent);
            return true;
        }
        value = 0.40f;
        return false;
    }

    public static float Percent(float value) => Math.Clamp(value / 100f, 0.01f, 1.0f);

    private static T Get<T>(string key, T fallback) =>
        ModSettings.TryGet<T>(Group, key, out var value) ? value : fallback;
}
