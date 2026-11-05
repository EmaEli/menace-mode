using System;
using System.Reflection;

namespace MenaceThirdPersonCamera
{
    // 0.7.18 moved the controller from States to Tactical. Avoid a hard type
    // reference: even a guarded direct access can fail when the caller is JITted.
    internal sealed class CameraAutoFocus
    {
        private readonly PropertyInfo _skipAutoFocus;

        internal CameraAutoFocus(Assembly gameAssembly)
        {
            Type controller = gameAssembly.GetType("Il2CppMenace.Tactical.TacticalCameraController", false)
                ?? gameAssembly.GetType("Il2CppMenace.States.TacticalCameraController", false);
            _skipAutoFocus = controller?.GetProperty("SkipAutoFocus", BindingFlags.Public | BindingFlags.Static);
            if (_skipAutoFocus == null || _skipAutoFocus.PropertyType != typeof(bool) ||
                _skipAutoFocus.GetGetMethod() == null || _skipAutoFocus.GetSetMethod() == null ||
                !_skipAutoFocus.GetGetMethod().IsStatic || !_skipAutoFocus.GetSetMethod().IsStatic ||
                _skipAutoFocus.GetIndexParameters().Length != 0)
                throw new NotSupportedException("Camera controller requires a public static Boolean SkipAutoFocus property.");
        }

        internal bool Value
        {
            get => (bool)_skipAutoFocus.GetValue(null);
            set => _skipAutoFocus.SetValue(null, value);
        }
    }
}