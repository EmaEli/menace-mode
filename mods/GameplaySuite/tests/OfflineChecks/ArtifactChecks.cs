using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

internal static class ArtifactChecks
{
    // Metadata-only inspection. Never loads a game/mod assembly into the test process.
    public static void Run(Action<bool, string> check, string repository)
    {
        string packages = Path.Combine(Path.GetFullPath(repository), "mods", "jiangyu");
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "mods", "GameplaySuite", "tools", "distribution.json")));
        string[] modules = catalog.RootElement.GetProperty("modules").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var available = new Dictionary<string, Version>(StringComparer.Ordinal)
        {
            ["Jiangyu"] = Version.Parse(catalog.RootElement.GetProperty("jiangyuVersion").GetString()!)
        };
        foreach (string name in modules)
        {
            using var info = JsonDocument.Parse(File.ReadAllText(Path.Combine(packages, name, "jiangyu.json")));
            available[name] = Version.Parse(info.RootElement.GetProperty("version").GetString()!);
        }
        foreach (string module in modules)
        {
            string root = Path.Combine(packages, module);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "jiangyu.json")));
            string version = manifest.RootElement.GetProperty("version").GetString()!;
            check(manifest.RootElement.GetProperty("name").GetString() == module, module + " source identity mismatch");
            using var compiled = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "compiled", "jiangyu.json")));
            check(compiled.RootElement.GetProperty("version").GetString() == version, module + " compiled manifest mismatch");
            check(compiled.RootElement.GetProperty("name").GetString() == module, module + " compiled identity mismatch");
            var depends = manifest.RootElement.GetProperty("depends").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            check(depends.SetEquals(compiled.RootElement.GetProperty("depends").EnumerateArray().Select(x => x.GetString()!)),
                module + " compiled dependencies mismatch");
            foreach (string requirement in depends)
            {
                string[] parts = requirement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                check(parts.Length == 3 && parts[1] == ">=", module + " unsupported dependency syntax");
                check(available.TryGetValue(parts[0], out var supplied) && supplied >= Version.Parse(parts[2]),
                    module + " dependency unavailable/too old: " + requirement);
            }
            check(compiled.RootElement.GetProperty("compiledForJiangyu").GetString() == available["Jiangyu"].ToString(),
                module + " payload compiled for a different SDK");
            string projectPath = Path.Combine(root, "code", module + ".csproj");
            if (!File.Exists(projectPath))
            {
                check(File.Exists(Path.Combine(root, "compiled", "templates.json")), module + " data payload missing");
                Console.WriteLine($"{module}: data package {version} verified.");
                continue;
            }
            var project = XDocument.Load(projectPath);
            // SDK projects default AssemblyName to the project file's name.
            string assembly = project.Descendants("AssemblyName").SingleOrDefault()?.Value
                ?? Path.GetFileNameWithoutExtension(projectPath);
            check(project.Descendants("Version").Single().Value == version, module + " project/manifest mismatch");

            string dll = Path.Combine(root, "compiled", "code", assembly + ".dll");
            check(File.Exists(dll), module + " built DLL missing");
            string normalized = version.Split('.').Length == 3 ? version + ".0" : version;
            string fileVersion = FileVersionInfo.GetVersionInfo(dll).FileVersion ?? "";
            string assemblyVersion = AssemblyName.GetAssemblyName(dll).Version!.ToString();
            check(fileVersion == normalized, module + " actual DLL file version mismatch");
            check(assemblyVersion == (module == "MenaceModSettings" ? "1.0.0.0" : normalized),
                module + " actual DLL assembly identity mismatch");
            using (var pe = new PEReader(File.OpenRead(dll)))
            {
                MetadataReader metadata = pe.GetMetadataReader();
                var references = metadata.AssemblyReferences.Select(metadata.GetAssemblyReference).ToArray();
                check(references.All(r => !metadata.GetString(r.Name).Contains("ModpackLoader", StringComparison.OrdinalIgnoreCase)),
                    module + " still has a binary dependency on ModpackLoader");
                foreach (var reference in references.Where(r => metadata.GetString(r.Name) == "MenaceModSettings"))
                {
                    check(reference.Version == new Version(1, 0, 0, 0), module + " settings ABI mismatch");
                    check(depends.Any(d => d.StartsWith("MenaceModSettings >= ", StringComparison.Ordinal)),
                        module + " binary settings dependency is undeclared");
                }
            }

            if (module is "CoordinatedFire" or "Overwatch")
            {
                check(manifest.RootElement.GetProperty("depends").EnumerateArray()
                    .Any(x => x.GetString() == "MenaceModSettings >= 0.4.1"), module + " missing shared guard dependency");
            }
            Console.WriteLine($"{module}: manifest={version}, assembly={assemblyVersion}, file={fileVersion} verified.");
        }
        string cameraRoot = Path.Combine(repository, "mods", "ThirdPersonCamera");
        string cameraVersion = XDocument.Load(Path.Combine(cameraRoot, "ThirdPersonCamera.csproj"))
            .Descendants("Version").Single().Value;
        string cameraDll = Path.Combine(cameraRoot, "bin", "Release", "MenaceThirdPersonCamera.dll");
        check(File.Exists(cameraDll), "camera DLL missing");
        check(AssemblyName.GetAssemblyName(cameraDll).Version!.ToString() == cameraVersion + ".0", "camera DLL assembly mismatch");
        check(FileVersionInfo.GetVersionInfo(cameraDll).FileVersion == cameraVersion + ".0", "camera DLL file version mismatch");
        Console.WriteLine($"Camera runtime: {cameraVersion} metadata verified.");
    }
}