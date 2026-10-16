using MenaceModSettings;

internal static class SettingsChecks
{
    public static void Run(Action<bool, string> check)
    {
        const string group = "Cinematic Action Camera";
        ModSettings.Register(group, b =>
        {
            b.AddToggle("AutomaticMode", "Auto", false);
            b.AddSlider("MovementChance", "Chance", 0, 100, 80);
            b.AddSlider("VehicleDistance", "Distance", 4, 25, 8.5f);
        });
        // Explicitly saving a default must protect it against later legacy imports.
        ModSettings.Set(group, "AutomaticMode", false);
        ModSettings.ImportMissing(group, new Dictionary<string, object>
        {
            ["AutomaticMode"] = true, ["MovementChance"] = 0, ["VehicleDistance"] = 999,
            ["Unregistered"] = 123,
        });
        check(!ModSettings.Get<bool>(group, "AutomaticMode"), "saved false must beat legacy true");
        check(ModSettings.Get<float>(group, "MovementChance") == 0, "legacy zero retained");
        check(ModSettings.Get<float>(group, "VehicleDistance") == 25, "legacy slider clamped");
        check(!ModSettings.TryGet<int>(group, "Unregistered", out _), "unknown keys not imported");
        int saves = SettingsStore.Saves;
        ModSettings.ImportMissing(group, new Dictionary<string, object> { ["MovementChance"] = 90 });
        check(ModSettings.Get<float>(group, "MovementChance") == 0 && SettingsStore.Saves == saves, "import idempotence");
        ModSettings.Register(group, b => b.AddSlider("MovementChance", "Chance", 0, 100, 80));
        check(ModSettings.Get<float>(group, "MovementChance") == 0, "re-register preserves stored values");
        var old = LegacySettings.Read("{\"Third Person Camera\":{\"MovementChance\":0,\"AutomaticMode\":false,\"unknown\":{}},\"Cinematic Action Camera\":{\"MovementChance\":42}}", group);
        check((double)old["MovementChance"] == 42 && !(bool)old["AutomaticMode"], "legacy group aliases and precedence");
        check(!old.ContainsKey("unknown"), "nested/unrecognized structures skipped");
        check(LegacySettings.Read("[]", group).Count == 0, "unknown root shape ignored");
        check(LegacySettings.Read("{\"SquaddieProbe\":{\"Enabled\":true}}", "Squaddie Veterancy").ContainsKey("Enabled"), "veterancy alias retained");
    }
}

namespace MenaceModSettings
{
    // In-memory stand-in: tests never read/write actual preferences or user saves.
    internal static class SettingsStore
    {
        public static int Saves;
        public static Dictionary<string, Dictionary<string, object>> Load() => new();
        public static Dictionary<string, object> LegacyValues(string group) => new();
        public static void Save(Dictionary<string, Dictionary<string, object>> data) => Saves++;
    }
}