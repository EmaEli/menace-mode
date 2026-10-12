using HarmonyLib;
using Il2CppMenace.UI;

namespace MenaceModSuite.Shared;

/// <summary>
/// Tracks the native tactical HUD visibility. Third Person Camera already calls
/// UITactical.SetUIVisibility(false/true) when entering/leaving cinematic mode.
/// The IMGUI panels owned by other modpacks must follow the same signal.
/// Each mod DLL compiles its own copy of this class and installs an isolated
/// Harmony postfix, so standalone and Gameplay Suite installations both work.
/// </summary>
internal static class TacticalHudVisibility
{
    private static global::HarmonyLib.Harmony? _patchHarmony;
    private static bool _nativeHudVisible = true;

    internal static bool IsVisible => _nativeHudVisible;

    internal static bool TryInstall(string patchOwnerId, Action<string> warning)
    {
        if (_patchHarmony != null) return true;

        global::HarmonyLib.Harmony? harmony = null;
        try
        {
            MethodInfo? target = typeof(UITactical).GetMethod(
                nameof(UITactical.SetUIVisibility),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);

            if (target == null)
                throw new MissingMethodException(nameof(UITactical), nameof(UITactical.SetUIVisibility));

            // Keep this hook outside each gameplay runtime's Harmony instance:
            // Runtime.Dispose() may UnpatchSelf() when gameplay is toggled.
            harmony = new global::HarmonyLib.Harmony(patchOwnerId);
            harmony.Patch(target,
                postfix: new HarmonyMethod(typeof(TacticalHudVisibility), nameof(VisibilityPostfix)));
            _patchHarmony = harmony;
            return true;
        }
        catch (Exception exception)
        {
            try { harmony?.UnpatchSelf(); } catch { /* Cleanup must not fail the mod. */ }
            warning("Tactical HUD visibility compatibility hook unavailable: " +
                    exception.GetType().Name + ": " + exception.Message);
            return false;
        }
    }

    // A previous scene's cinematic suppression must not leak into a new scene.
    internal static void OnSceneLoaded() => _nativeHudVisible = true;

    internal static void Uninstall()
    {
        try { _patchHarmony?.UnpatchSelf(); }
        finally
        {
            _patchHarmony = null;
            _nativeHudVisible = true;
        }
    }

    // __0 binds the sole argument of the native static method, independent of
    // any parameter-name changes in IL2CPP generated proxies.
    private static void VisibilityPostfix(bool __0) => _nativeHudVisible = __0;
}
