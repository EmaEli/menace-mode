namespace MenaceModSuite.Shared;

// Host-agnostic module lifecycle. Each module's Plugin implements this; the legacy
// Menace.ModpackLoader binding (Plugin.Modpack.cs) and the Jiangyu binding
// (Shared/Jiangyu/SuiteSystem.cs) adapt their host callbacks to it. The optional
// callbacks default to no-ops, as on IModpackPlugin.
public interface IHostedPlugin
{
    void Initialize(ModLog log, HarmonyLib.Harmony harmony);
    void OnSceneLoaded(int buildIndex, string sceneName) { }
    void OnUpdate() { }
    void OnUnload() { }
}
