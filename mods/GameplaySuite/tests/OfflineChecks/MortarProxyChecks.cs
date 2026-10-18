using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Metadata only: verifies proxy call paths, not rendering or native execution.
internal static class MortarProxyChecks
{
    internal static void Run(Action<bool, string> check, string proxies, string module)
    {
        using var gui = new Image(Path.Combine(proxies, "UnityEngine.IMGUIModule.dll"));
        using var core = new Image(Path.Combine(proxies, "UnityEngine.CoreModule.dll"));
        using var wmr = new Image(module);
        var drawTexture = gui.Find("UnityEngine.GUI", "DrawTexture", 2);
        check(gui.ReachesStub(drawTexture, new()), "positive control: DrawTexture reaches unsupported proxy stub");
        var label = gui.Find("UnityEngine.GUI", "Label", 3, "content");
        check(!gui.ReachesStub(label, new()), "replacement GUI.Label must not reach an unsupported stub");
        check(gui.Calls(label).Any(c => c.Name.EndsWith("::il2cpp_runtime_invoke")),
            "replacement Label overload is a native wrapper, not a managed convenience chain");
        var add = core.Find("UnityEngine.GameObject", "AddComponent", 1);
        check(!core.ReachesStub(add, new()), "non-generic AddComponent proxy is available");
        check(core.Calls(add).Any(c => c.Name.EndsWith("::il2cpp_runtime_invoke")),
            "non-generic AddComponent invokes native Unity");
        var line = wmr.Find("MenaceWeaponMechanicsRebalanced.Plugin", "DrawGuiLine", 3);
        var lineCalls = wmr.Calls(line).ToArray();
        check(lineCalls.Any(c => c.Name == "UnityEngine.GUI::Label"), "compiled line uses GUI.Label");
        check(lineCalls.All(c => c.Name != "UnityEngine.GUI::DrawTexture"), "compiled line avoids broken DrawTexture chain");
        var world = wmr.Find("MenaceWeaponMechanicsRebalanced.Plugin", "AddMortarWorldLine", 4);
        var componentCalls = wmr.Calls(world).Where(c => c.Name == "UnityEngine.GameObject::AddComponent").ToArray();
        check(componentCalls.Length == 1, "world line creates exactly one component per child");
        check(componentCalls.All(c => c.Handle.Kind != HandleKind.MethodSpecification),
            "compiled world line must not invoke stripped native generic AddComponent<T>");
        Console.WriteLine("Mortar proxy call-path checks passed (not a game/rendering test).");
    }

    private sealed class Image : IDisposable
    {
        private readonly FileStream _file;
        private readonly PEReader _pe;
        private readonly MetadataReader _md;
        private static readonly Dictionary<ushort, OpCode> Codes = typeof(OpCodes).GetFields()
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(c => unchecked((ushort)c.Value));

        internal Image(string path)
        {
            _file = File.OpenRead(path);
            _pe = new PEReader(_file);
            _md = _pe.GetMetadataReader();
        }

        internal MethodDefinitionHandle Find(string type, string name, int parameters, string? named = null)
        {
            var t = _md.TypeDefinitions.Single(h => TypeName(h) == type);
            return _md.GetTypeDefinition(t).GetMethods().Single(h =>
            {
                var m = _md.GetMethodDefinition(h);
                var ps = m.GetParameters().Select(_md.GetParameter).Where(p => p.SequenceNumber > 0).ToArray();
                return _md.GetString(m.Name) == name && ps.Length == parameters
                    && (named == null || ps.Any(p => _md.GetString(p.Name) == named));
            });
        }

        internal IEnumerable<(EntityHandle Handle, string Name)> Calls(MethodDefinitionHandle method)
        {
            foreach (var (code, token) in Instructions(method))
                if (code.OperandType == OperandType.InlineMethod)
                {
                    var h = MetadataTokens.EntityHandle(token);
                    yield return (h, MethodName(h));
                }
        }

        internal bool ReachesStub(MethodDefinitionHandle method, HashSet<MethodDefinitionHandle> seen)
        {
            if (!seen.Add(method)) return false;
            if (Instructions(method).Any(i => i.Code == OpCodes.Ldstr &&
                _md.GetUserString(MetadataTokens.UserStringHandle(i.Token & 0xFFFFFF)) == "Method unstripping failed"))
                return true;
            return Calls(method).Select(c => LocalMethod(c.Handle))
                .Any(h => !h.IsNil && ReachesStub(h, seen));
        }

        private MethodDefinitionHandle LocalMethod(EntityHandle h)
        {
            if (h.Kind == HandleKind.MethodDefinition) return (MethodDefinitionHandle)h;
            if (h.Kind == HandleKind.MethodSpecification)
                return LocalMethod(_md.GetMethodSpecification((MethodSpecificationHandle)h).Method);
            if (h.Kind != HandleKind.MemberReference) return default;
            var reference = _md.GetMemberReference((MemberReferenceHandle)h);
            string owner = TypeName(reference.Parent);
            var type = _md.TypeDefinitions.FirstOrDefault(t => TypeName(t) == owner);
            if (type.IsNil) return default;
            var signature = _md.GetBlobBytes(reference.Signature);
            return _md.GetTypeDefinition(type).GetMethods().Single(m =>
                _md.GetString(_md.GetMethodDefinition(m).Name) == _md.GetString(reference.Name)
                && _md.GetBlobBytes(_md.GetMethodDefinition(m).Signature).SequenceEqual(signature));
        }

        private IEnumerable<(OpCode Code, int Token)> Instructions(MethodDefinitionHandle method)
        {
            var m = _md.GetMethodDefinition(method);
            if (m.RelativeVirtualAddress == 0) yield break;
            byte[] bytes = _pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;
            for (int at = 0; at < bytes.Length;)
            {
                ushort key = bytes[at++];
                if (key == 0xFE) key = (ushort)(0xFE00 | bytes[at++]);
                var code = Codes[key];
                int size = code.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, at),
                    _ => 4
                };
                int token = size >= 4 ? BitConverter.ToInt32(bytes, at) : 0;
                at += size;
                yield return (code, token);
            }
        }

        private string MethodName(EntityHandle h) => h.Kind switch
        {
            HandleKind.MethodDefinition => TypeName(_md.GetMethodDefinition((MethodDefinitionHandle)h).GetDeclaringType())
                + "::" + _md.GetString(_md.GetMethodDefinition((MethodDefinitionHandle)h).Name),
            HandleKind.MemberReference => TypeName(_md.GetMemberReference((MemberReferenceHandle)h).Parent)
                + "::" + _md.GetString(_md.GetMemberReference((MemberReferenceHandle)h).Name),
            HandleKind.MethodSpecification => MethodName(_md.GetMethodSpecification((MethodSpecificationHandle)h).Method),
            _ => h.Kind.ToString()
        };

        private string TypeName(EntityHandle h) => h.Kind switch
        {
            HandleKind.TypeDefinition => _md.GetString(_md.GetTypeDefinition((TypeDefinitionHandle)h).Namespace)
                + "." + _md.GetString(_md.GetTypeDefinition((TypeDefinitionHandle)h).Name),
            HandleKind.TypeReference => _md.GetString(_md.GetTypeReference((TypeReferenceHandle)h).Namespace)
                + "." + _md.GetString(_md.GetTypeReference((TypeReferenceHandle)h).Name),
            _ => h.Kind.ToString()
        };

        public void Dispose() { _pe.Dispose(); _file.Dispose(); }
    }
}