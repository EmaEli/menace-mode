using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Read the real production IL, without loading AIPerformance or any IL2CPP type.
internal static class StartupContracts
{
    internal static void Check(PEReader pe, MetadataReader metadata, Action<bool, string> check)
    {
        var plugin = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t =>
            metadata.GetString(t.Namespace) == "AIPerformance" && metadata.GetString(t.Name) == "Plugin");
        var setup = plugin.GetMethods().Select(metadata.GetMethodDefinition).Single(m =>
            metadata.GetString(m.Name) == "PatchContactAdvance");
        var il = pe.GetMethodBody(setup.RelativeVirtualAddress).GetILBytes()!;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => op.Value);
        bool prepares = false;
        for (int offset = 0; offset < il.Length;)
        {
            short value = il[offset++];
            if (value == 0xfe) value = unchecked((short)(0xfe00 | il[offset++]));
            var operand = opcodes[value].OperandType;
            if (operand == OperandType.InlineMethod)
            {
                EntityHandle target = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                if (target.Kind == HandleKind.MethodSpecification)
                    target = metadata.GetMethodSpecification((MethodSpecificationHandle)target).Method;
                if (target.Kind == HandleKind.MemberReference)
                {
                    var member = metadata.GetMemberReference((MemberReferenceHandle)target);
                    if (member.Parent.Kind == HandleKind.TypeReference)
                    {
                        var owner = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                        prepares |= metadata.GetString(owner.Namespace) == "System.Runtime.CompilerServices" &&
                            metadata.GetString(owner.Name) == "RuntimeHelpers" &&
                            metadata.GetString(member.Name) == "PrepareMethod";
                    }
                }
            }
            offset += operand switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                _ => 4
            };
        }
        check(!prepares, "contact initialization must not force RuntimeHelpers.PrepareMethod through the loader JIT detour (startup crash)");
    }
}