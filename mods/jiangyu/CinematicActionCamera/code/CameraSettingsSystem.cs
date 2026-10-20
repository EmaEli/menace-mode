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
            s.AddToggle("FollowVisibleEnemyMovement", "Also follow moving enemies currently visible", true);
            s.AddSlider("MinimumMovementTiles", "Minimum move distance (tiles; 0 disables, 3 skips 1-2 tile moves)", 0, 50, 0);
            s.AddHeader("Visible enemy movement camera");
            s.AddSlider("EnemyInfantryMovementChance", "Enemy infantry (including jetpack) movement chance (%)", 0, 100, 80);
            s.AddSlider("EnemyVehicleMovementChance", "Enemy vehicle movement chance (%)", 0, 100, 80);
            s.AddSlider("EnemyWalkerMovementChance", "Enemy walker movement chance (%)", 0, 100, 80);
            s.AddHeader("Infantry squad leader camera");
            s.AddSlider("MovementChance", "Infantry movement chance (%)", 0, 100, 80);
            s.AddSlider("FiringChance", "Infantry firing chance (%)", 0, 100, 100);
            s.AddSlider("Distance", "Distance behind an infantry squad leader", 1.25f, 12, 3.2f);
            s.AddSlider("Height", "Height above an infantry squad leader", 0.6f, 6, 1.75f);
            s.AddHeader("Vehicle camera (excluding walkers)");
            s.AddSlider("VehicleMovementChance", "Vehicle movement chance (%)", 0, 100, 80);
            s.AddSlider("VehicleFiringChance", "Vehicle firing chance (%)", 0, 100, 100);
            s.AddSlider("VehicleDistance", "Distance behind a vehicle", 4, 25, 8.5f);
            s.AddSlider("VehicleHeight", "Height above a vehicle", 1.5f, 12, 3.8f);
            s.AddHeader("Walker camera");
            s.AddSlider("WalkerMovementChance", "Walker movement chance (%)", 0, 100, 80);
            s.AddSlider("WalkerFiringChance", "Walker firing chance (%)", 0, 100, 100);
            s.AddSlider("WalkerDistance", "Distance behind a walker", 4, 25, 8.5f);
            s.AddSlider("WalkerHeight", "Height above a walker", 1.5f, 12, 3.8f);
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
                // Split shared values after importing them. Explicit new keys,
                // including 0%, always win over inherited defaults.
                ModSettings.ImportMissing(Group, SplitDefaults(
                    ModSettings.Get(Group, "MovementChance"), ModSettings.Get(Group, "FiringChance"),
                    ModSettings.Get(Group, "VehicleDistance"), ModSettings.Get(Group, "VehicleHeight")));
                ModSettings.ImportMissing(Group, SplitEnemyDefaults(
                    ModSettings.Get(Group, "MovementChance"), ModSettings.Get(Group, "VehicleMovementChance"),
                    ModSettings.Get(Group, "WalkerMovementChance")));
                yield break;
            }
            yield return null;
        }
    }

    private static IDictionary<string, object> SplitDefaults(object movement, object firing, object distance, object height)
    {
        return new Dictionary<string, object>
        {
            ["VehicleMovementChance"] = movement,
            ["VehicleFiringChance"] = firing,
            ["WalkerMovementChance"] = movement,
            ["WalkerFiringChance"] = firing,
            ["WalkerDistance"] = distance,
            ["WalkerHeight"] = height,
        };
    }

    private static IDictionary<string, object> SplitEnemyDefaults(object infantryMovement, object vehicleMovement, object walkerMovement)
    {
        return new Dictionary<string, object>
        {
            ["EnemyInfantryMovementChance"] = infantryMovement,
            ["EnemyVehicleMovementChance"] = vehicleMovement,
            ["EnemyWalkerMovementChance"] = walkerMovement,
        };
    }

    public override void OnUnload()
    {
        if (_migration != null) Context.Coroutines.Stop(_migration);
        _migration = null;
    }
}