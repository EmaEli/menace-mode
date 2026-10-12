namespace MenaceModSuite.Shared;

/// <summary>
/// Optional, versioned, managed-state bridge to Third Person Camera 0.3.4+.
/// The camera is a separate MelonLoader DLL, so we cannot add a compile-time
/// dependency. Resolve its public static getter once and invoke the cached
/// delegate (not MethodInfo.Invoke or assembly scanning) in IMGUI callbacks.
///
/// Third Person Camera's automatic and manual paths share the same _active
/// flag. This deliberately does NOT infer cinematic mode from camera height,
/// weapon targeting or movement; unrelated gameplay never gets suppressed.
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
                // A mod may be torn down as a scene/application is closing.
                // Recover silently; never fail the tactical GUI or spam logs.
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
                {
                    _readActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                }
                // Camera 0.3.3 has no public contract; it still uses the
                // native-HUD Harmony fallback until upgraded to 0.3.4.
                return;
            }
        }
        catch
        {
            // No camera installed / not initialized / reflection unavailable:
            // no hard dependency and no logs on a normal installation.
        }
    }
}
