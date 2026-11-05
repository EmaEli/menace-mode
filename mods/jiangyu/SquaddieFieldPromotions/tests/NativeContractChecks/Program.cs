using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

// Read metadata only: do not load/initialize IL2CPP types outside the game.
if (args.Length != 2) throw new ArgumentException("Usage: <Assembly-CSharp.dll> <SFP code directory>");
using var pe = new PEReader(File.OpenRead(args[0]));
var reader = pe.GetMetadataReader();
var names = new SignatureNames();
var types = reader.TypeDefinitions.GroupBy(h => reader.GetString(reader.GetTypeDefinition(h).Name))
    .ToDictionary(group => group.Key, group => group.First());
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Native contract mismatch: " + name);
    checks++;
}
string Normalize(string name) => name.Replace("UnityEngine.", "").Replace("Il2CppSystem.Collections.Generic.", "").Replace("bool", "Boolean")
    .Replace("void", "Void").Replace("int", "Int32").Replace("float", "Single").Replace("string", "String");
var source = File.ReadAllText(Path.Combine(args[1], "PromotionSystem.cs"));
var contracts = Regex.Matches(source,
    "Require(Static)?\\(typeof\\(([^)]+)\\), \"([^\"]+)\", (Type.EmptyTypes|new\\[\\] \\{[^}]*\\}|[A-Za-z_][A-Za-z0-9_]*), typeof\\(([^)]+)\\)\\);");
Check(contracts.Count >= 19 && contracts.Count == Regex.Matches(source, @"Require(?:Static)?\(typeof\(").Count,
    "all production preflight declarations were found");
foreach (Match contract in contracts)
{
    var type = reader.GetTypeDefinition(types[contract.Groups[2].Value]);
    string methodName = contract.Groups[3].Value;
    string parameters = contract.Groups[4].Value;
    if (parameters != "Type.EmptyTypes" && !parameters.StartsWith("new[]"))
    {
        var declaration = Regex.Match(source, @"var " + Regex.Escape(parameters) + @" = new\[\] \{([^}]+)\};");
        Check(declaration.Success, "parameter declaration: " + parameters);
        parameters = declaration.Groups[1].Value;
    }
    string[] expected = Regex.Matches(parameters, @"typeof\(([^)]+)\)")
        .Select(m => Normalize(m.Groups[1].Value)).ToArray();
    bool found = type.GetMethods().Any(h =>
    {
        var method = reader.GetMethodDefinition(h);
        if (reader.GetString(method.Name) != methodName ||
            method.Attributes.HasFlag(MethodAttributes.Static) != contract.Groups[1].Success) return false;
        var signature = method.DecodeSignature(names, (object?)null);
        return signature.ReturnType == Normalize(contract.Groups[5].Value) &&
            signature.ParameterTypes.SequenceEqual(expected);
    });
    Check(found, contract.Groups[2].Value + "." + methodName + "(" + string.Join(",", expected) + ")");
}
string MemberType(string typeName, string member)
{
    var type = reader.GetTypeDefinition(types[typeName]);
    foreach (var h in type.GetProperties())
    {
        var p = reader.GetPropertyDefinition(h);
        if (reader.GetString(p.Name) == member) return p.DecodeSignature(names, (object?)null).ReturnType;
    }
    foreach (var h in type.GetFields())
    {
        var f = reader.GetFieldDefinition(h);
        if (reader.GetString(f.Name) == member) return f.DecodeSignature(names, (object?)null);
    }
    throw new Exception("Missing native member: " + typeName + "." + member);
}
Check(MemberType("ChangeProperty", "Properties").Contains("PropertyChange"), "nested property-change array");
Check(MemberType("PropertyChange", "PropertyType") == "EntityPropertyType", "property enum");
Check(MemberType("PropertyChange", "Amount") == "Int32", "additive amount");
Check(MemberType("PropertyChange", "AmountMult") == "Single", "multiplicative amount");
Check(MemberType("ChangeProperty", "ValueProvider") == "IValueProvider", "value-provider override");
Check(MemberType("ChangeProperty", "TooltipPlaceholderIndex") == "Int32", "tooltip override");
Check(MemberType("ChangeProperty", "IncludePlusSign") == "Boolean", "tooltip sign");
Console.WriteLine($"PASS: {checks} native metadata contracts; no native methods executed.");

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