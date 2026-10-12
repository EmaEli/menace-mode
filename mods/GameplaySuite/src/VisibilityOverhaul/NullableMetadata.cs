// Build compatibility for the Unity interop references supplied with MENACE.
// UnityEngine.CoreModule.dll exposes nullable metadata types that can confuse
// Roslyn when nullable analysis is enabled. These source-local definitions are
// metadata-only and never invoke Unity or IL2CPP.
#nullable disable

namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.All,
        AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : global::System.Attribute
    {
        public readonly byte[] NullableFlags;

        public NullableAttribute(byte flag)
        {
            NullableFlags = new byte[] { flag };
        }

        public NullableAttribute(byte[] flags)
        {
            NullableFlags = flags;
        }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.All,
        AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : global::System.Attribute
    {
        public readonly byte Flag;

        public NullableContextAttribute(byte flag)
        {
            Flag = flag;
        }
    }
}
