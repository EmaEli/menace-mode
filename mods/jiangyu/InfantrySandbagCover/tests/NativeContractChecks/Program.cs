using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Metadata only: never load or initialize game proxies in a test process.
if (args.Length != 2) throw new ArgumentException("Usage: <Assembly-CSharp.dll> <InfantrySandbagCover.dll>");
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
    if (!ok) throw new Exception("Sandbag native contract mismatch: " + label);
    checks++;
}
void Method(string type, string name, string result, params string[] parameters)
{
    Check(types.ContainsKey(type), type);
    Check(reader.GetTypeDefinition(types[type]).GetMethods().Any(h =>
    {
        var m = reader.GetMethodDefinition(h);
        var s = m.DecodeSignature(names, (object?)null);
        return reader.GetString(m.Name) == name && !m.Attributes.HasFlag(MethodAttributes.Static)
            && s.ReturnType == result && s.ParameterTypes.SequenceEqual(parameters);
    }), type + "." + name);
}
void Property(string type, string name, string expected)
{
    Check(types.ContainsKey(type), type);
    Check(reader.GetTypeDefinition(types[type]).GetProperties().Any(h =>
    {
        var p = reader.GetPropertyDefinition(h);
        return reader.GetString(p.Name) == name && p.DecodeSignature(names, (object?)null).ReturnType == expected
            && !p.GetAccessors().Getter.IsNil;
    }), type + "." + name);
}
const string skill = "Il2CppMenace.Tactical.Skills.Skill";
const string actor = "Il2CppMenace.Tactical.Actor";
const string entity = "Il2CppMenace.Tactical.Entity";
const string manager = "Il2CppMenace.Tactical.TacticalManager";
Method(skill, "Use", "Boolean", "Tile", "UsageParameter");
Method(skill, "ApplyToTile", "Void", "Tile", "UsageParameter");
Method(skill, "ClearBusy", "Void", "UsageParameter");
Method(skill, "IsUsable", "Boolean");
Method(skill, "IsUsable", "Boolean", "TargetUsageParams");
Method(skill, "GetActionPointCost", "Int32");
Method(actor, "IsMoving", "Boolean");
Method(actor, "SetActionPoints", "Void", "Int32", "Boolean");
Property(actor, "TilesMovedThisTurn", "Int32");
foreach (string name in new[] { "IsPlayerOrPlayerAI", "IsContainedWithinAnotherEntity" }) Method(entity, name, "Boolean");
Method(entity, "GetHitpoints", "Int32");
Method(manager, "GetRound", "Int32");
Method(manager, "InvokeOnMovement", "Void", "Actor", "Tile", "Tile", "MovementAction", "Entity");
Method("Il2CppMenace.Tactical.BaseTile", "HasHalfCoverInDir", "Boolean", "Direction");
Method("Il2CppMenace.Tactical.Skills.Effects.SpawnHalfCoverHandler", "OnApply", "Void", "Actor", "Tile", "Tile", "Tile", "Element", "Boolean");
Property("Il2CppMenace.Tactical.Skills.SkillEventHandler", "ParentSkill", "Skill");
Method("Il2CppMenace.Tactical.Skills.Effects.DestroyEnvironmentFeaturesHandler", "OnTileHit", "Void", "Skill", "Tile", "Tile", "Vector3");
Property("Il2CppMenace.Tactical.Skills.Effects.SpawnHalfCover", "CoverConfig", "CoverConfig");
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
Check(nativeReferences > 0, "runtime native references present");
Console.WriteLine($"PASS: {checks} sandbag native metadata checks; {nativeReferences} binary type references; no native code executed.");

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