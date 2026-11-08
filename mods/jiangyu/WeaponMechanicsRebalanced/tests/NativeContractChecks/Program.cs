using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Inspect metadata only; never initialize or execute native proxies outside MENACE.
if (args.Length != 2) throw new ArgumentException("Usage: <Assembly-CSharp.dll> <WMR.dll>");
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
    if (!ok) throw new Exception("Mortar native contract mismatch: " + label);
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
void Member(string type, string name, string expected, bool writable)
{
    Check(types.ContainsKey(type), type);
    Check(reader.GetTypeDefinition(types[type]).GetProperties().Any(h =>
    {
        var p = reader.GetPropertyDefinition(h);
        var a = p.GetAccessors();
        return reader.GetString(p.Name) == name && p.DecodeSignature(names, (object?)null).ReturnType == expected
            && !a.Getter.IsNil && (!writable || !a.Setter.IsNil);
    }), type + "." + name);
}
const string skill = "Il2CppMenace.Tactical.Skills.Skill";
const string template = "Il2CppMenace.Tactical.Skills.SkillTemplate";
const string attack = "Il2CppMenace.Tactical.Skills.Effects.Attack";
Method(skill, "ApplyToTile", "Void", "Tile", "UsageParameter");
Method(skill, "Scatter", "Tile", "Tile");
Method(skill, "GetMaxRange", "Int32");
Method(skill, "GetAvailableScatterTiles", "HashSet<Tile>", "Tile");
Method("Il2CppMenace.Tactical.Tile", "HasLineOfSightTo", "Boolean", "Tile", "LineOfSightFlags");
Method("Il2CppMenace.Tactical.Actor", "HasLineOfSightTo", "Boolean", "Entity", "Tile", "Tile");
Method(skill, "ApplySuppression", "Void", "Tile", "Tile", "Single", "Boolean", "List<Tile>");
Method(skill, "GetAffectedTiles", "List<Tile>", "Tile", "Tile");
Method("Il2CppMenace.Tactical.Actor", "OnTurnStart", "Void");
Method("Il2CppMenace.Tactical.Skills.Effects.AttackHandler", "OnTileHit", "Void", "Skill", "Tile", "Tile", "Vector3");
Method("Il2CppMenace.Tactical.Entity", "ApplySuppression", "Void", "Single", "Boolean", "Entity", "Skill");
foreach (string name in new[] { "Scatter", "ScatterChance", "ActionPointCost", "Uses" })
    Member(template, name, "Int32", true);
Member(template, "MarkAsActed", "Boolean", true);
Member(template, "Repetitions", "UInt16", true);
Member(template, "EventHandlers", "List<SkillEventHandlerTemplate>", false);
const string aiAttack = "Il2CppMenace.Tactical.AI.Behaviors.Attack";
const string behavior = "Il2CppMenace.Tactical.AI.Behavior";
const string skillBehavior = "Il2CppMenace.Tactical.AI.SkillBehavior";
const string agent = "Il2CppMenace.Tactical.AI.Agent";
Method(aiAttack, "OnEvaluate", "Int32", "Actor");
Method(aiAttack, "OnExecute", "Boolean", "Actor");
Method(agent, "PickBehavior", "Behavior");
Method(agent, "Execute", "Boolean");
Method(agent, "GetState", "State");
Member(agent, "m_ActiveBehavior", "Behavior", true);
Member(agent, "IsTurnDone", "Boolean", false);
Member(agent, "FlaggedForDeactivation", "Boolean", false);
Member(behavior, "m_Score", "Int32", true);
Check(reader.GetTypeDefinition(types["Il2CppMenace.Tactical.TacticalManager"]).GetMethods().Any(h =>
{
    var m = reader.GetMethodDefinition(h);
    var s = m.DecodeSignature(names, (object?)null);
    return reader.GetString(m.Name) == "IsSkillBusy" && m.Attributes.HasFlag(MethodAttributes.Static)
        && s.ReturnType == "Boolean" && s.ParameterTypes.Length == 0;
}), "TacticalManager.IsSkillBusy static guard");
Method(agent, "GetBehaviors", "List<Behavior>");
Method(agent, "GetActor", "Actor");
Method(agent, "GetFaction", "AIFaction");
Method(agent, "IsDeploymentPhase", "Boolean");
Method(agent, "IsSleeping", "Boolean");
Method(behavior, "GetAgent", "Agent");
Method(behavior, "GetScore", "Int32");
Method(skillBehavior, "GetSkill", "Skill");
Member(aiAttack, "m_Candidates", "List<Data>", false);
Member(skillBehavior, "m_TargetTile", "Tile", true);
Member(skillBehavior, "m_AdditionalRadius", "Int32", false);
foreach (string name in new[] { "m_DeployBeforeExecuting", "m_SetupBeforeExecuting", "m_RotateBeforeExecuting", "m_DontActuallyExecute", "m_IsExecuted" })
    Member(skillBehavior, name, "Boolean", false);
Member("Il2CppMenace.Tactical.TacticalManager", "m_Round", "Int32", false);
Method("Il2CppMenace.Tactical.AI.AIFaction", "IsAlliedWithPlayer", "Boolean");
foreach (string name in new[] { "IsOutOfUses", "IsAffordable", "IsUsable" }) Method(skill, name, "Boolean");
Method(skill, "IsUsableOn", "Boolean", "Tile", "Tile");
Method("Il2CppMenace.Tactical.Tile", "HasActor", "Boolean");
Method("Il2CppMenace.Tactical.Entity", "IsAlliedWith", "Boolean", "Int32");
foreach (string name in new[] { "DamageMult", "DamageToArmorDurabilityMult", "SuppressionDropoffAOE", "AccuracyBonus", "AccuracyMult" })
    Member(attack, name, "Single", true);
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
Check(nativeReferences > 0, "WMR native references present");
Console.WriteLine($"PASS: {checks} mortar native metadata checks; {nativeReferences} binary type references; no native code executed.");

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