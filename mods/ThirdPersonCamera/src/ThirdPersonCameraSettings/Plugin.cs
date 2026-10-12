using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

namespace MenaceThirdPersonCamera.Settings
{
    // Only registers settings. Actual camera behavior remains in the standalone
    // MenaceThirdPersonCamera.dll so it works without ModpackLoader too.
    public sealed class Plugin : IModpackPlugin
    {
        private const string MenuName = "Cinematic Action Camera";

        public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
        {
            ModSettings.Register(MenuName, settings =>
            {
                settings.AddHeader("Automatic cinematic camera");
                settings.AddToggle("AutomaticMode", "Enable automatic cinematic camera (F7 toggles for this session)", false);
                settings.AddSlider("MovementChance", "Movement chance - friendly units and visible enemies (%)", 0f, 100f, 80f);
                settings.AddSlider("FiringChance", "Firing chance - friendly infantry and vehicles (%)", 0f, 100f, 100f);
                settings.AddToggle("FollowVisibleEnemyMovement", "Also follow moving enemies currently visible", true);

                settings.AddHeader("Vehicle camera");
                settings.AddSlider("VehicleDistance", "Distance behind a vehicle", 4f, 25f, 8.5f);
                settings.AddSlider("VehicleHeight", "Height above a vehicle", 1.5f, 12f, 3.8f);

                settings.AddHeader("Camera orbit");
                settings.AddSlider("OrbitSensitivity", "Mouse orbit sensitivity", 0.05f, 2f, 0.35f);
            });
        }

        // Required by IModpackPlugin. Camera lifecycle is owned by the
        // standalone MelonLoader mod; this companion only publishes settings.
        public void OnSceneLoaded(int buildIndex, string sceneName)
        {
        }
    }
}
