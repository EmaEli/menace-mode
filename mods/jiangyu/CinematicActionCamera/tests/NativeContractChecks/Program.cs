using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

int checks = AutoFocusChecks.Run();
if (args.Length != 2) throw new ArgumentException("Usage: <Assembly-CSharp proxy.dll> <camera runtime.dll>");
// Metadata only: never load or execute the native game proxies outside MENACE.
using var pe = new PEReader(File.OpenRead(args[0]));
var reader = pe.GetMetadataReader();
var names = new SignatureNames();
string FullName(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
var types = reader.TypeDefinitions.Where(h => reader.GetTypeDefinition(h).GetDeclaringType().IsNil).ToDictionary(h =>
{
    var t = reader.GetTypeDefinition(h);
    return FullName(reader.GetString(t.Namespace), reader.GetString(t.Name));
});
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Camera native contract mismatch: " + name);
    checks++;
}
string controllerName = new[] { "Il2CppMenace.Tactical.TacticalCameraController", "Il2CppMenace.States.TacticalCameraController" }
    .FirstOrDefault(types.ContainsKey);
Check(controllerName != null, "current or legacy controller exists");
var controller = reader.GetTypeDefinition(types[controllerName]);
Check(controller.GetProperties().Any(h =>
{
    var p = reader.GetPropertyDefinition(h);
    var a = p.GetAccessors();
    return reader.GetString(p.Name) == "SkipAutoFocus" && p.DecodeSignature(names, (object)null).ReturnType == "Boolean"
        && !a.Getter.IsNil && !a.Setter.IsNil
        && new[] { a.Getter, a.Setter }.All(m =>
        {
            var flags = reader.GetMethodDefinition(m).Attributes;
            return flags.HasFlag(MethodAttributes.Static) && flags.HasFlag(MethodAttributes.Public);
        });
}), "controller has public static Boolean read/write SkipAutoFocus");
void Method(string typeName, string name, string result, params string[] parameters)
{
    Check(types.ContainsKey(typeName), "type " + typeName);
    Check(reader.GetTypeDefinition(types[typeName]).GetMethods().Any(h =>
    {
        var m = reader.GetMethodDefinition(h);
        var signature = m.DecodeSignature(names, (object)null);
        return reader.GetString(m.Name) == name && !m.Attributes.HasFlag(MethodAttributes.Static)
            && signature.ReturnType == result && signature.ParameterTypes.SequenceEqual(parameters);
    }), typeName + "." + name);
}
Method("Il2CppMenace.Tactical.TacticalManager", "InvokeOnMovement", "Void", "Actor", "Tile", "Tile", "MovementAction", "Entity");
Method("Il2CppMenace.Tactical.TacticalManager", "InvokeOnMovementFinished", "Void", "Actor", "Tile");
Method("Il2CppMenace.Tactical.TacticalManager", "InvokeOnAttackTileStart", "Void", "Actor", "Skill", "Tile", "Single");
Method("Il2CppMenace.Tactical.Skills.Effects.JetPackHandler", "OnApply", "Void", "Actor", "Tile", "Tile", "Tile", "Element", "Boolean");
using var runtime = new PEReader(File.OpenRead(args[1]));
var binary = runtime.GetMetadataReader();
int nativeReferences = 0;
foreach (var handle in binary.TypeReferences)
{
    var type = binary.GetTypeReference(handle);
    if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;
    var assembly = binary.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
    if (binary.GetString(assembly.Name) != "Assembly-CSharp") continue;
    string name = FullName(binary.GetString(type.Namespace), binary.GetString(type.Name));
    Check(types.ContainsKey(name), "runtime native type reference " + name);
    Check(!name.EndsWith(".TacticalCameraController"), "controller is resolved without a hard binary reference");
    nativeReferences++;
}
Check(nativeReferences > 0, "camera contains native type references");
Console.WriteLine($"PASS: {checks} camera checks (18 autofocus behavior checks, native hooks and {nativeReferences} binary type references); {controllerName}; no game code executed.");

sealed class SignatureNames : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string t, ArrayShape shape) => t + "[]";
    public string GetByReferenceType(string t) => t + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => "function";
    public string GetGenericInstantiation(string t, ImmutableArray<string> args) => t.Split('`')[0] + "<" + string.Join(",", args) + ">";
    public string GetGenericMethodParameter(object c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object c, int i) => "!" + i;
    public string GetModifiedType(string m, string t, bool required) => t;
    public string GetPinnedType(string t) => t;
    public string GetPointerType(string t) => t + "*";
    public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
    public string GetSZArrayType(string t) => t + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte kind) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte kind) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte kind) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}