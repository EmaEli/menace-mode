using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Metadata only: no proxy type initialization or native execution outside MENACE.
if (args.Length != 2) throw new ArgumentException("Usage: <Assembly-CSharp.dll> <SquaddieProbe.dll>");
using var pe = new PEReader(File.OpenRead(args[0]));
var reader = pe.GetMetadataReader();
var names = new SignatureNames();
string FullName(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
var types = reader.TypeDefinitions.Where(h => reader.GetTypeDefinition(h).GetDeclaringType().IsNil).ToDictionary(h =>
{
    var t = reader.GetTypeDefinition(h);
    return FullName(reader.GetString(t.Namespace), reader.GetString(t.Name));
});
int checks = 0;
void Check(bool ok, string label)
{
    if (!ok) throw new Exception("Vehicle retreat native contract mismatch: " + label);
    checks++;
}
void Method(string type, string name, string result, bool isStatic, params string[] parameters)
{
    Check(types.ContainsKey(type), type);
    Check(reader.GetTypeDefinition(types[type]).GetMethods().Any(h =>
    {
        var m = reader.GetMethodDefinition(h);
        var s = m.DecodeSignature(names, (object?)null);
        return reader.GetString(m.Name) == name && m.Attributes.HasFlag(MethodAttributes.Static) == isStatic
            && s.ReturnType == result && s.ParameterTypes.SequenceEqual(parameters);
    }), type + "." + name);
}
void Member(string type, string name, string expected)
{
    Check(types.ContainsKey(type), type);
    Check(reader.GetTypeDefinition(types[type]).GetProperties().Any(h =>
    {
        var p = reader.GetPropertyDefinition(h);
        return reader.GetString(p.Name) == name && p.DecodeSignature(names, (object?)null).ReturnType == expected
            && !p.GetAccessors().Getter.IsNil;
    }), type + "." + name);
}
const string tactical = "Il2CppMenace.Tactical.";
const string skills = tactical + "Skills.";
Method(tactical + "PathMover", "StartMovement", "Void", false, "Path", "MovementAction", "Boolean");
Member(tactical + "PathMover", "m_Element", "Element");
Member(tactical + "PathMover", "m_Actor", "Actor");
Method(tactical + "Element", "IsAlive", "Boolean", false);
Method(tactical + "Entity", "IsAlive", "Boolean", false);
Method(tactical + "Entity", "GetSkills", "SkillContainer", false);
Method(tactical + "Entity", "GetTile", "Tile", false);
foreach (string name in new[] { "IsDying", "IsTurnDone", "IsLeavingMap", "IsMoving", "IsStunned" })
    Method(tactical + "Actor", name, "Boolean", false);
Method(tactical + "TacticalManager", "IsSkillBusy", "Boolean", true);
Method(skills + "SkillContainer", "IsBusy", "Boolean", false);
Method(skills + "SkillContainer", "GetSkillByID", "BaseSkill", false, "String", "Item");
foreach (string name in new[] { "IsOutOfUses", "IsUsable", "IsAffordable" })
    Method(skills + "Skill", name, "Boolean", false);
Method(skills + "Skill", "IsUsableOn", "Boolean", false, "Tile", "Tile");
Method(skills + "Skill", "Use", "Boolean", false, "Tile", "UsageParameter");
using var runtime = new PEReader(File.OpenRead(args[1]));
var binary = runtime.GetMetadataReader();
int nativeReferences = 0;
foreach (var handle in binary.TypeReferences)
{
    var type = binary.GetTypeReference(handle);
    if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;
    var assembly = binary.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
    if (binary.GetString(assembly.Name) != "Assembly-CSharp") continue;
    Check(types.ContainsKey(FullName(binary.GetString(type.Namespace), binary.GetString(type.Name))), "runtime native type reference");
    nativeReferences++;
}
Check(nativeReferences > 0, "veterancy native references present");
Console.WriteLine($"PASS: {checks} vehicle retreat native metadata checks; {nativeReferences} binary type references; no native code executed.");

sealed class SignatureNames : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string t, ArrayShape s) => t + "[]";
    public string GetByReferenceType(string t) => t + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => "function";
    public string GetGenericInstantiation(string t, ImmutableArray<string> a) => t.Split('`')[0] + "<" + string.Join(",", a) + ">";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string m, string t, bool required) => t;
    public string GetPinnedType(string t) => t;
    public string GetPointerType(string t) => t + "*";
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString();
    public string GetSZArrayType(string t) => t + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}