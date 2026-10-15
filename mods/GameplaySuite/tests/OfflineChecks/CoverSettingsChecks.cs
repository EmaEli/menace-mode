using MenaceModSettings;
using StrongerCover;

internal static class CoverSettingsChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Actual production settings classes; only the backing store is the existing
        // in-memory stand-in. No game templates, preferences or sidecars are loaded.
        CoverSettings.Register();
        check(CoverSettings.Enabled, "StrongerCover default enabled");
        check(Math.Abs(CoverSettings.Building - 0.30f) < 0.001f, "cover building default 30 percent");
        check(!CoverSettings.VerboseLogging, "cover debug default off");
        check(!CoverSettings.TryGetGarrisonBuildingBase(out float fallback) && fallback == 0.40f,
            "missing garrison uses 40 percent fallback");
        ModSettings.Register(CoverSettings.GarrisonGroup,
            b => b.AddSlider(CoverSettings.GarrisonBuildingAccuracyKey, "Base", 5, 100, 40));
        check(CoverSettings.TryGetGarrisonBuildingBase(out float initial) && initial == 0.40f,
            "registered garrison base available");
        ModSettings.Set(CoverSettings.GarrisonGroup, CoverSettings.GarrisonBuildingAccuracyKey, 65f);
        check(CoverSettings.TryGetGarrisonBuildingBase(out float changed) && Math.Abs(changed - 0.65f) < 0.001f,
            "garrison base changes are not replaced with cover default");
        ModSettings.Set(CoverSettings.Group, "BuildingCover", 35f);
        ModSettings.Set(CoverSettings.Group, "EnableGameplay", false);
        CoverSettings.Reload();
        check(!CoverSettings.Enabled, "cover disable setting observed");
        check(Math.Abs(CoverSettings.Building - 0.35f) < 0.001f, "configured cover value retained while disabled");
        foreach (string key in new[] { "EnableGameplay", "BunkerCover", "ForestCover", "OpenVehicleCover", "WatchtowerCover", "BuildingCover" })
            check(CoverSettings.AffectsInsideCover(key), "cover update key: " + key);
        check(!CoverSettings.AffectsInsideCover("VerboseLogging"), "debug changes do not rewrite cover");
        check(CoverSettings.Percent(-20) == 0.01f && CoverSettings.Percent(200) == 1f, "cover percentage bounds");
    }
}