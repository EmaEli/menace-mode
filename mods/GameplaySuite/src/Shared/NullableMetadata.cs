// Build compatibility for the Unity interop references supplied with MENACE.
// UnityEngine.CoreModule.dll contains a public NullableAttribute type without
// the constructors Roslyn expects. A source-local definition takes precedence.
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
