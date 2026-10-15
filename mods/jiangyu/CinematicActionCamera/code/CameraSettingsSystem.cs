using System.Collections;
using System.Reflection;
using Jiangyu.Sdk;
using MenaceModSettings;

namespace MenaceThirdPersonCamera.Settings;

// Camera updates/hooks remain owned by exactly one MelonMod. No second camera is created.
public sealed class CameraSettingsSystem : JiangyuSystem
{
    private const string Group = "Cinematic Action Camera";
    private object _migration;

    public override void OnInit()
    {
        ModSettings.Register(Group, s =>
        {
            s.AddHeader("Automatic cinematic camera");
            s.AddToggle("AutomaticMode", "Enable automatic cinematic camera (F7 toggles for this session)", false);
            s.AddSlider("MovementChance", "Movement chance - friendly units and visible enemies (%)", 0, 100, 80);
            s.AddSlider("FiringChance", "Firing chance - friendly infantry and vehicles (%)", 0, 100, 100);
            s.AddToggle("FollowVisibleEnemyMovement", "Also follow moving enemies currently visible", true);
            s.AddHeader("Vehicle camera");
            s.AddSlider("VehicleDistance", "Distance behind a vehicle", 4, 25, 8.5f);
            s.AddSlider("VehicleHeight", "Height above a vehicle", 1.5f, 12, 3.8f);
            s.AddHeader("Camera orbit");
            s.AddSlider("OrbitSensitivity", "Mouse orbit sensitivity", 0.05f, 2, 0.35f);
        });
        _migration = Context.Coroutines.Start(ImportPreferences());
    }

    private IEnumerator ImportPreferences()
    {
        // Bounded startup grace period. No log spam or indefinite per-frame reflection.
        for (int attempt = 0; attempt < 120; attempt++)
        {
            IDictionary<string, object> values = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var camera = assembly.GetType("MenaceThirdPersonCamera.ThirdPersonCameraMod", false);
                if (camera == null) continue;
                try { values = camera.GetMethod("GetLegacySettingsValues")?.Invoke(null, null) as IDictionary<string, object>; }
                catch { }
                break;
            }
            if (values != null)
            {
                // Modern JSON > recognized legacy ModSettings group > MelonPreferences.
                ModSettings.ImportMissing(Group, values);
                yield break;
            }
            yield return null;
        }
    }

    public override void OnUnload()
    {
        if (_migration != null) Context.Coroutines.Stop(_migration);
        _migration = null;
    }
}