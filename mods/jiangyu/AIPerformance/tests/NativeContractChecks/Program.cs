using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Inspect proxy metadata without initializing or executing any IL2CPP type.
if (args.Length is < 1 or > 2) throw new ArgumentException("Usage: <Assembly-CSharp.dll> [AIPerformance.dll]");
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
foreach (string member in new[] { "DistanceToZones", "DistanceToAdvanceZones", "UtilityThreshold" })
    Property(weights, member, "Single");
void Method(TypeDefinition type, string member, string result, params string[] parameters)
{
    Check(type.GetMethods().Count(h =>
    {
        var method = reader.GetMethodDefinition(h);
        var signature = method.DecodeSignature(names, (object?)null);
        return reader.GetString(method.Name) == member && signature.ReturnType == result &&
            signature.ParameterTypes.SequenceEqual(parameters);
    }) == 1, member + " signature");
}
var agent = Type("Il2CppMenace.Tactical.AI.Agent");
Method(agent, "PickBehavior", "Behavior");
Method(agent, "GetZones", "List<Zone>");
Method(agent, "GetActor", "Actor");
Method(agent, "GetRole", "RoleData");
Method(agent, "GetFaction", "AIFaction");
Method(agent, "GetBehaviors", "List<Behavior>");
Method(agent, "IsSleeping", "Boolean");
Method(agent, "IsDeploymentPhase", "Boolean");
var behavior = Type("Il2CppMenace.Tactical.AI.Behavior");
Method(behavior, "GetAgent", "Agent");
Method(behavior, "GetUtilityThreshold", "Single");
var movement = Type("Il2CppMenace.Tactical.AI.Behaviors.Move");
Method(movement, "HasUtility", "Boolean", "Dictionary<Tile,TileScore>");
Method(movement, "GetTargetTile", "TileScore");
foreach (string member in new[] { "IsMovementDone", "HasMovedThisTurn", "IsDelayingMovement", "IsInsideContainerAndInert" })
    Method(movement, member, "Boolean");
Method(faction, "HasKnownOpponent", "Boolean");
var zones = Type("Il2CppMenace.Tactical.AI.Behaviors.Criterions.ConsiderZones");
Method(zones, "PostProcess", "Void", "Actor", "Dictionary<Tile,TileScore>");
var manager = Type("Il2CppMenace.Tactical.TacticalManager");
Method(manager, "GetFaction", "BaseFaction", "Int32");
var actor = Type("Il2CppMenace.Tactical.Actor");
Method(actor, "SetDetectedByFaction", "Void", "Int32");
Method(actor, "IsDetectedByFaction", "Boolean", "Int32");
var entity = Type("Il2CppMenace.Tactical.Entity");
Property(entity, "ID", "Int32");
Method(entity, "GetFaction", "FactionType");
Method(entity, "HasTag", "Boolean", "TagType");
Method(entity, "IsInfantry", "Boolean");
Method(entity, "IsVehicle", "Boolean");
Method(Type("Il2CppMenace.Tactical.AI.BaseFaction"), "IsAlliedWith", "Boolean", "Int32");
Check(manager.GetMethods().Count(h =>
{
    var method = reader.GetMethodDefinition(h);
    var signature = method.DecodeSignature(names, (object?)null);
    return reader.GetString(method.Name) == "OnInit" && signature.ParameterTypes.Length == 1 && signature.ReturnType == "Void";
}) == 1, "unique TacticalManager.OnInit/1");
Property(manager, "m_Round", "Int32");
var role = Type("Il2CppMenace.Tactical.AI.Data.RoleData");
Property(role, "AvoidOpponents", "Boolean");
foreach (string member in new[] { "ZoneImportanceWeight", "UtilityScale", "UtilityThresholdScale" })
    Property(role, member, "Single");
Property(role, "Move", "Single");
var zone = Type("Il2CppMenace.Tactical.AI.Zone");
Property(zone, "Area", "RectInt");
Property(zone, "Priority", "Single");
Property(zone, "Order", "ZoneOrder");
var score = Type("Il2CppMenace.Tactical.AI.Data.TileScore");
Property(score, "Tile", "Tile");
Property(score, "DistanceScore", "Single", true);
Property(score, "UtilityScore", "Single", true);
foreach (string member in new[] { "SafetyScore", "UtilityByAttacksScore", "UtilityByAttacksScoreCandidate" })
    Property(score, member, "Single");
Property(Type("Il2CppMenace.Tactical.AI.Behaviors.Attack"), "m_Candidates");
var geometry = Type("Il2CppMenace.Tools.RectIntExtensions");
Method(geometry, "GetDistanceTo", "Int32", "RectInt", "Tile");
Method(geometry, "IsInside", "Boolean", "RectInt", "Tile");
var loader = Type("Il2CppMenace.Tools.DataTemplateLoader");
Check(loader.GetMethods().Any(h =>
{
    var method = reader.GetMethodDefinition(h);
    var signature = method.DecodeSignature(names, (object?)null);
    return reader.GetString(method.Name) == "Get" && method.Attributes.HasFlag(MethodAttributes.Static) &&
        method.GetGenericParameters().Count == 1 && signature.ReturnType == "!!0" &&
        signature.ParameterTypes.SequenceEqual(new[] { "String", "Boolean" });
}), "Get<T>(string, bool) ID fallback");
if (args.Length == 2)
{
    using var built = new PEReader(File.OpenRead(args[1]));
    var metadata = built.GetMetadataReader();
    Check(!metadata.AssemblyReferences.Any(h => metadata.GetString(metadata.GetAssemblyReference(h).Name) == "LargerMaps"),
        "AIPerformance has no LargerMaps assembly dependency");
    Check(metadata.TypeDefinitions.Any(h => metadata.GetString(metadata.GetTypeDefinition(h).Name) == "AdvanceRecoveryRuntime"),
        "recovery runtime belongs to AIPerformance.dll");
    Check(metadata.TypeDefinitions.Any(h => metadata.GetString(metadata.GetTypeDefinition(h).Name) == "ContactConvergenceRuntime"),
        "contact runtime included in AIPerformance.dll");
}
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