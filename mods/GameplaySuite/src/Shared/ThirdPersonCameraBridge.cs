using System.Reflection;

namespace MenaceModSuite.Shared;

/// <summary>
/// Optional managed-state contract with Cinematic Action Camera (formerly Third
/// Person Camera 0.3.4+). Both automatic and F6 views use the same active flag.
/// Restores MEN-006 without a compile-time dependency on the MelonLoader mod.
/// </summary>
internal static class ThirdPersonCameraBridge
{
    private const string CameraType = "MenaceThirdPersonCamera.ThirdPersonCameraMod";
    private const string ActiveProperty = "IsCinematicViewActive";
    private static Func<bool>? _readActive;
    private static float _nextProbeAt;

    internal static bool IsActive
    {
        get
        {
            var reader = _readActive;
            if (reader == null)
            {
                TryBind();
                reader = _readActive;
            }

            if (reader == null) return false;
            try { return reader(); }
            catch
            {
                // A camera being torn down must not disable the tactical HUD.
                _readActive = null;
                _nextProbeAt = UnityEngine.Time.realtimeSinceStartup + 5f;
                return false;
            }
        }
    }

    private static void TryBind()
    {
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (now < _nextProbeAt) return;
        _nextProbeAt = now + 5f;

        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(CameraType, throwOnError: false);
                if (type == null) continue;

                var getter = type.GetProperty(ActiveProperty,
                    BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                if (getter != null && getter.IsStatic && getter.ReturnType == typeof(bool)
                    && getter.GetParameters().Length == 0)
                    _readActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                return;
            }
        }
        catch
        {
            // Camera absent/not ready: retry slowly, silently and without a hard dependency.
        }
    }
}