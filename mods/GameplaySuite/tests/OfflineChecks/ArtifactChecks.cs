using System.Diagnostics;
using System.Reflection;
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
        foreach (string module in catalog.RootElement.GetProperty("modules").EnumerateArray().Select(x => x.GetString()!))
        {
            string root = Path.Combine(packages, module);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "jiangyu.json")));
            string version = manifest.RootElement.GetProperty("version").GetString()!;
            check(manifest.RootElement.GetProperty("name").GetString() == module, module + " source identity mismatch");
            using var compiled = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "compiled", "jiangyu.json")));
            check(compiled.RootElement.GetProperty("version").GetString() == version, module + " compiled manifest mismatch");
            check(compiled.RootElement.GetProperty("name").GetString() == module, module + " compiled identity mismatch");
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