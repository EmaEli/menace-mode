using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Inspect proxy metadata without initializing or executing any IL2CPP type.
if (args.Length != 1) throw new ArgumentException("Usage: <Assembly-CSharp.dll>");
using var pe = new PEReader(File.OpenRead(args[0]));
var reader = pe.GetMetadataReader();
var names = new SignatureNames();
var types = reader.TypeDefinitions.Where(h => reader.GetTypeDefinition(h).GetDeclaringType().IsNil).ToDictionary(h =>
{
    var type = reader.GetTypeDefinition(h);
    return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
});
int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("AI performance native contract mismatch: " + label);
    checks++;
}
TypeDefinition Type(string name)
{
    Check(types.ContainsKey(name), name);
    return reader.GetTypeDefinition(types[name]);
}
string Property(TypeDefinition type, string member, string? expected = null, bool writable = false)
{
    var found = type.GetProperties().Where(h => reader.GetString(reader.GetPropertyDefinition(h).Name) == member).ToArray();
    Check(found.Length == 1, member);
    var property = reader.GetPropertyDefinition(found[0]);
    var accessors = property.GetAccessors();
    string result = property.DecodeSignature(names, (object?)null).ReturnType;
    Check(!accessors.Getter.IsNil && (!writable || !accessors.Setter.IsNil) &&
        (expected == null || result == expected), member + " accessor/type");
    return result;
}
var faction = Type("Il2CppMenace.Tactical.AI.AIFaction");
Check(faction.GetMethods().Count(h =>
{
    var method = reader.GetMethodDefinition(h);
    var signature = method.DecodeSignature(names, (object?)null);
    return reader.GetString(method.Name) == "RunWithTimeout" && method.Attributes.HasFlag(MethodAttributes.Static) &&
        signature.ReturnType == "Task" && signature.ParameterTypes.SequenceEqual(new[] { "Action", "Actor" });
}) == 1, "static Task RunWithTimeout(Action, Actor)");
var template = Type("Il2CppMenace.Tactical.Skills.SkillTemplate");
Property(template, "IsAttack", "Boolean");
Property(template, "IsLineOfFireNeeded", "Boolean");
Property(template, "AIConfig", "SkillBehavior");
Property(Type("Il2CppMenace.Tactical.AI.Data.SkillBehavior"), "InfluencesPositioning", "Boolean", true);
var config = Type("Il2CppMenace.Tactical.AI.AIConfig");
Property(config, "WEIGHTS", "AIWeightsTemplate");
var weights = Type("Il2CppMenace.Tactical.AI.AIWeightsTemplate");
Property(weights, "NearTileLimit", "Int32", true);
Property(weights, "CullTilesDistances", "Int32", true);
var loader = Type("Il2CppMenace.Tools.DataTemplateLoader");
Check(loader.GetMethods().Any(h =>
{
    var method = reader.GetMethodDefinition(h);
    var signature = method.DecodeSignature(names, (object?)null);
    return reader.GetString(method.Name) == "Get" && method.Attributes.HasFlag(MethodAttributes.Static) &&
        method.GetGenericParameters().Count == 1 && signature.ReturnType == "!!0" &&
        signature.ParameterTypes.SequenceEqual(new[] { "String", "Boolean" });
}), "Get<T>(string, bool) ID fallback");
Console.WriteLine($"PASS: {checks} AI performance native metadata contracts; no native methods executed.");

sealed class SignatureNames : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string t, ArrayShape shape) => t + "[]";
    public string GetByReferenceType(string t) => t + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => "function";
    public string GetGenericInstantiation(string t, ImmutableArray<string> args) => t.Split('`')[0] + "<" + string.Join(",", args) + ">";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string m, string t, bool required) => t;
    public string GetPinnedType(string t) => t;
    public string GetPointerType(string t) => t + "*";
    public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
    public string GetSZArrayType(string t) => t + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte kind) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte kind) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte kind) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}