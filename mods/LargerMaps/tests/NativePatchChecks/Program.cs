using System.Collections;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using LargerMaps;

// Exercise the actual production selection/RVA/immediate logic without executable
// memory writes or a game process. Optional binary checks read original bytes only.
const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
int checks = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception(message);
}
object Field(object value, string name) => value.GetType().GetField(name, Fields)!.GetValue(value)!;
object Call(object value, string name, object arg) => value.GetType().GetMethod(name, Fields)!.Invoke(value, new[] { arg })!;
int Hex(JsonElement value) => Convert.ToInt32(value.GetString(), 16);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(args.Length > 0 ? args[0] : ".",
    "mods/LargerMaps/tests/pathfinding-sites.json")));
var data = manifest.RootElement;
var type = typeof(ExtendedPathfinding);
var builds = ((IEnumerable)type.GetField("SupportedBuilds", Static)!.GetValue(null)!).Cast<object>().ToArray();
Check(builds.Length == data.GetProperty("builds").GetArrayLength(), "build count");
byte[] binary = args.Length > 1 ? File.ReadAllBytes(args[1]) : null;
string binaryHash = binary == null ? null : Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant();
bool matchedBinary = false;
foreach (var build in builds)
{
    string version = (string)Field(build, "Version");
    var recorded = data.GetProperty("builds").EnumerateArray().Single(b => b.GetProperty("game_version").GetString() == version);
    Check((string)Field(build, "Sha256") == recorded.GetProperty("sha256").GetString(), version + " hash");
    var rvas = recorded.GetProperty("method_rvas");
    var expectedSites = data.GetProperty("sites").EnumerateArray()
        .Where(s => rvas.TryGetProperty(s.GetProperty("method").GetString()!, out _)).ToArray();
    var sites = ((IEnumerable)type.GetMethod("SitesFor", Static)!.Invoke(null, new[] { build })!).Cast<object>().ToArray();
    Check(sites.Length == expectedSites.Length, version + " selected plan count");
    bool validateBinary = binaryHash == (string)Field(build, "Sha256");
    matchedBinary |= validateBinary;
    using var pe = validateBinary ? new PEReader(new MemoryStream(binary!)) : null;
    var positions = new HashSet<int>();
    for (int i = 0; i < sites.Length; i++)
    {
        var site = sites[i];
        var expected = expectedSites[i];
        string purpose = expected.GetProperty("purpose").GetString()!;
        Check((string)Field(site, "Purpose") == purpose, version + " purpose");
        string method = expected.GetProperty("method").GetString()!;
        Check(Field(site, "Method").ToString() == method, version + " method");
        int delta = Hex(expected.GetProperty(version == "v0.7.18+22828" && expected.TryGetProperty("delta_v0718", out _) ? "delta_v0718" : "delta"));
        int rva = (int)Call(site, "Rva", build);
        Check(rva == Hex(rvas.GetProperty(method)) + delta, version + " resolved RVA " + purpose);
        int offset = (int)Field(site, "Offset");
        Check(offset == expected.GetProperty("immediate_offset").GetInt32(), "immediate offset");
        byte[] original = (byte[])Field(site, "Original");
        Check(Convert.ToHexString(original) == expected.GetProperty("expected_instruction_hex").GetString(), "instruction");
        Check(positions.Add(rva + offset), "no duplicate writes");
        foreach (int size in new[] { 72, 80, 88, 96 })
        {
            byte value = (byte)Call(site, "ForMap", size);
            int expectedValue = purpose.EndsWith("-loop") || purpose.StartsWith("mission-bound-") ? size : Hex(expected.GetProperty("replacement_80"));
            Check(value == expectedValue, version + " immediate " + purpose + " " + size);
        }
        if (validateBinary)
        {
            var section = pe!.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva + original.Length <= s.VirtualAddress + s.SizeOfRawData);
            int fileOffset = rva - section.VirtualAddress + section.PointerToRawData;
            Check(binary!.AsSpan(fileOffset, original.Length).SequenceEqual(original), version + " real instruction " + purpose);
        }
    }
}
Check(binary == null || matchedBinary, "supplied binary must match a supported hash");
Console.WriteLine($"PASS: {checks} production native-plan checks; real binary {(binary == null ? "not supplied" : binaryHash)}. No native writes executed.");