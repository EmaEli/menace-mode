// Polyfill. The Il2CppInterop proxy UnityEngine.CoreModule.dll exposes a public
// System.Runtime.CompilerServices.NullableAttribute without the (byte) constructor,
// so newer Roslyn versions (.NET SDK 8+) fail with CS0656 when they need to emit
// nullable metadata (e.g. for static lambdas). Defining the attributes in-source makes
// the compiler use these instead of the proxy types.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Field |
                    AttributeTargets.GenericParameter | AttributeTargets.Parameter |
                    AttributeTargets.Property | AttributeTargets.ReturnValue,
                    AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;
        public NullableAttribute(byte flag) { NullableFlags = new[] { flag }; }
        public NullableAttribute(byte[] flags) { NullableFlags = flags; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Interface |
                    AttributeTargets.Method | AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public readonly byte Flag;
        public NullableContextAttribute(byte flag) { Flag = flag; }
    }
}
