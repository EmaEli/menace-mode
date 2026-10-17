using System.Reflection;
using System.Reflection.Emit;
using MenaceModSuite.Shared;

// Only the clock is substituted; the actual production bridge is compiled into this runner.
namespace UnityEngine
{
    internal static class Time
    {
        public static float realtimeSinceStartup;
    }
}

internal static class CinematicHudChecks
{
    internal static void Run(Action<bool, string> check)
    {
        UnityEngine.Time.realtimeSinceStartup = 0;
        check(!ThirdPersonCameraBridge.IsActive, "no camera: HUD remains visible");

        // Load a camera contract AFTER the first lookup, as with optional/delayed mods.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("OfflineCameraContract"), AssemblyBuilderAccess.Run);
        var builder = assembly.DefineDynamicModule("Camera").DefineType(
            "MenaceThirdPersonCamera.ThirdPersonCameraMod", TypeAttributes.Public);
        var active = builder.DefineField("Active", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
        var fail = builder.DefineField("Fail", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
        var getter = builder.DefineMethod("get_IsCinematicViewActive",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName,
            typeof(bool), Type.EmptyTypes);
        var il = getter.GetILGenerator();
        var read = il.DefineLabel();
        il.Emit(OpCodes.Ldsfld, fail);
        il.Emit(OpCodes.Brfalse_S, read);
        il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Throw);
        il.MarkLabel(read);
        il.Emit(OpCodes.Ldsfld, active);
        il.Emit(OpCodes.Ret);
        var property = builder.DefineProperty("IsCinematicViewActive", PropertyAttributes.None, typeof(bool), null);
        property.SetGetMethod(getter);
        var camera = builder.CreateType()!;
        var activeField = camera.GetField("Active")!;
        var failField = camera.GetField("Fail")!;
        activeField.SetValue(null, true);

        UnityEngine.Time.realtimeSinceStartup = 1;
        check(!ThirdPersonCameraBridge.IsActive, "missing-camera lookup is throttled");
        UnityEngine.Time.realtimeSinceStartup = 5;
        check(ThirdPersonCameraBridge.IsActive, "late camera contract binds and hides HUD");
        for (int transition = 0; transition < 4; transition++)
        {
            activeField.SetValue(null, false);
            check(!ThirdPersonCameraBridge.IsActive, "camera exit restores HUD immediately");
            activeField.SetValue(null, true);
            check(ThirdPersonCameraBridge.IsActive, "manual/automatic entry hides HUD immediately");
        }

        failField.SetValue(null, true);
        check(!ThirdPersonCameraBridge.IsActive, "throwing camera cannot break HUD");
        failField.SetValue(null, false);
        UnityEngine.Time.realtimeSinceStartup = 6;
        check(!ThirdPersonCameraBridge.IsActive, "failed camera retry is throttled");
        UnityEngine.Time.realtimeSinceStartup = 10;
        check(ThirdPersonCameraBridge.IsActive, "camera recovers after failed getter");
        activeField.SetValue(null, false);
        check(!ThirdPersonCameraBridge.IsActive, "no stale cinematic state after restore");
    }
}