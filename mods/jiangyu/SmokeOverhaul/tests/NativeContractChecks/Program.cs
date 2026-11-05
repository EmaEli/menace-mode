using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

// Metadata only: no IL2CPP type initialization/native execution outside MENACE.
if (args.Length != 2) throw new ArgumentException("Usage: <Assembly-CSharp.dll> <SmokeOverhaul Plugin.cs>");
using var pe = new PEReader(File.OpenRead(args[0]));
var reader = pe.GetMetadataReader();
var names = new SignatureNames();
var types = reader.TypeDefinitions.GroupBy(h => reader.GetString(reader.GetTypeDefinition(h).Name))
    .ToDictionary(group => group.Key, group => group.First());
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Smoke native contract mismatch: " + name);
    checks++;
}
string Normalize(string name) => name.Replace("Il2CppSystem.Collections.Generic.", "")
    .Replace("bool", "Boolean").Replace("void", "Void").Replace("int", "Int32").Replace("float", "Single");
string source = File.ReadAllText(args[1]);
source = source.Split("public void Initialize(")[1].Split("public void OnSceneLoaded(")[0];
var hooks = Regex.Matches(source,
    @"AccessTools.Method\(\s*typeof\(([^)]+)\),\s*nameof\([^.)]+\.([^)]+)\),\s*(Type.EmptyTypes|new\[\]\s*\{[^}]*\})\s*\)");
Check(hooks.Count == 12 && hooks.Count == Regex.Matches(source, @"AccessTools.Method\(").Count,
    "all production hook declarations parsed");
foreach (Match hook in hooks)
{
    string typeName = hook.Groups[1].Value, methodName = hook.Groups[2].Value;
    string[] parameters = Regex.Matches(hook.Groups[3].Value, @"typeof\(([^)]+)\)")
        .Select(m => Normalize(m.Groups[1].Value)).ToArray();
    string result = methodName switch { "Use" or "Execute" or "Add" => "Boolean", "GetActionPointCost" => "Int32", "GetHitchance" => "HitChance", _ => "Void" };
    Check(types.ContainsKey(typeName), "type " + typeName);
    Check(reader.GetTypeDefinition(types[typeName]).GetMethods().Any(h =>
    {
        var m = reader.GetMethodDefinition(h);
        var signature = m.DecodeSignature(names, (object?)null);
        return reader.GetString(m.Name) == methodName && !m.Attributes.HasFlag(MethodAttributes.Static) &&
            signature.ReturnType == result && signature.ParameterTypes.SequenceEqual(parameters);
    }), typeName + "." + methodName);
}
void Member(string typeName, string memberName, string expected, bool writable = false)
{
    Check(types.ContainsKey(typeName), "type " + typeName);
    var type = reader.GetTypeDefinition(types[typeName]);
    Check(type.GetProperties().Any(h =>
    {
        var p = reader.GetPropertyDefinition(h);
        var a = p.GetAccessors();
        return reader.GetString(p.Name) == memberName && p.DecodeSignature(names, (object?)null).ReturnType == expected &&
            !a.Getter.IsNil && (!writable || !a.Setter.IsNil);
    }), typeName + "." + memberName);
}
bool arrayLayout = reader.GetTypeDefinition(types["ChangeProperty"]).GetProperties()
    .Any(h => reader.GetString(reader.GetPropertyDefinition(h).Name) == "Properties");
if (arrayLayout) Member("ChangeProperty", "Properties", "Il2CppStructArray<PropertyChange>", true);
else
{
    Member("ChangeProperty", "PropertyType", "EntityPropertyType");
    Member("ChangeProperty", "Amount", "Int32", true);
    Member("ChangeProperty", "AmountMult", "Single", true);
    Member("ChangeProperty", "ValueProvider", "IValueProvider", true);
}
Member("SpawnTileEffect", "EffectToSpawn", "TileEffectTemplate");
Member("SpawnTileEffect", "ChancePerTileFromCenter", "Int32", true);
Member("Cooldown", "AIOnly", "Boolean");
Member("SkillTemplate", "EventHandlers", "List<SkillEventHandlerTemplate>");
Member("TileEffectTemplate", "Type", "TileEffectType");
Member("TileEffectTemplate", "BlockLineOfSight", "Boolean", true);
Member("TileEffectTemplate", "RemoveAfterRounds", "Int32", true);
Member("TileEffectTemplate", "HasTimeLimit", "Boolean");
Member("TileEffectTemplate", "SpawnObjectOrEffect", "GameObject");
Member("TileEffectTemplate", "AmountOfPrefabsSpawned", "Int32");
Member("TileEffectHandler", "m_WorldObject", "GameObject");
Console.WriteLine($"PASS: {checks} smoke native metadata contracts; {hooks.Count} hook signatures; no native methods executed.");

sealed class SignatureNames : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string t, ArrayShape shape) => t + "[]";
    public string GetByReferenceType(string t) => t + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => "function";
    public string GetGenericInstantiation(string t, ImmutableArray<string> args) =>
        t.Split('`')[0] + "<" + string.Join(",", args) + ">";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string m, string t, bool required) => t;
    public string GetPinnedType(string t) => t;
    public string GetPointerType(string t) => t + "*";
    public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
    public string GetSZArrayType(string t) => t + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte kind) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte kind) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte kind) =>
        r.GetTypeSpecification(h).DecodeSignature(this, c);
}