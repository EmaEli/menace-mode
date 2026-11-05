using System.Reflection;
using System.Reflection.Emit;
using MenaceThirdPersonCamera;

internal static class AutoFocusChecks
{
    private const string Current = "Il2CppMenace.Tactical.TacticalCameraController";
    private const string Legacy = "Il2CppMenace.States.TacticalCameraController";

    internal static int Run()
    {
        int checks = 0;
        void Check(bool value) { if (!value) throw new Exception("Autofocus regression"); checks++; }
        foreach (string name in new[] { Current, Legacy })
        {
            foreach (bool original in new[] { false, true })
            {
                var assembly = Fixture((name, typeof(bool), true, true));
                var native = assembly.GetType(name).GetProperty("SkipAutoFocus");
                native.SetValue(null, original);
                var focus = new CameraAutoFocus(assembly);
                bool saved = focus.Value;
                Check(saved == original);
                focus.Value = true;
                Check((bool)native.GetValue(null));
                focus.Value = saved;
                Check((bool)native.GetValue(null) == original);
            }
        }
        var both = Fixture((Current, typeof(bool), true, true), (Legacy, typeof(bool), true, true));
        new CameraAutoFocus(both).Value = true;
        Check((bool)both.GetType(Current).GetProperty("SkipAutoFocus").GetValue(null));
        Check(!(bool)both.GetType(Legacy).GetProperty("SkipAutoFocus").GetValue(null));
        foreach (var assembly in new[] { Fixture(), Fixture((Current, typeof(int), true, true)),
            Fixture((Current, typeof(bool), false, true)), Fixture((Current, typeof(bool), true, false)) })
        {
            bool rejected = false;
            try { _ = new CameraAutoFocus(assembly); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected);
        }
        return checks;
    }

    private static Assembly Fixture(params (string Name, Type ValueType, bool Writable, bool Static)[] types)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CameraFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Fixture");
        foreach (var type in types)
        {
            var builder = module.DefineType(type.Name, TypeAttributes.Public);
            var field = builder.DefineField("value", type.ValueType, FieldAttributes.Private | (type.Static ? FieldAttributes.Static : 0));
            var property = builder.DefineProperty("SkipAutoFocus", PropertyAttributes.None, type.ValueType, Type.EmptyTypes);
            var attributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | (type.Static ? MethodAttributes.Static : 0);
            var getter = builder.DefineMethod("get_SkipAutoFocus", attributes, type.ValueType, Type.EmptyTypes);
            var il = getter.GetILGenerator();
            if (!type.Static) il.Emit(OpCodes.Ldarg_0);
            il.Emit(type.Static ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            if (type.Writable)
            {
                var setter = builder.DefineMethod("set_SkipAutoFocus", attributes, typeof(void), new[] { type.ValueType });
                il = setter.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                if (!type.Static) il.Emit(OpCodes.Ldarg_1);
                il.Emit(type.Static ? OpCodes.Stsfld : OpCodes.Stfld, field);
                il.Emit(OpCodes.Ret);
                property.SetSetMethod(setter);
            }
            builder.CreateType();
        }
        return assembly;
    }
}