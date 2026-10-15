namespace MenaceModSuite.Shared;

// Host-agnostic module lifecycle. Each module's Plugin implements this; the Jiangyu
// binding (Shared/Jiangyu/SuiteSystem.cs) adapts the host callbacks to it. The
// optional callbacks default to no-ops.
public interface IHostedPlugin
{
    void Initialize(ModLog log, HarmonyLib.Harmony harmony);
    void OnSceneLoaded(int buildIndex, string sceneName) { }
    void OnUpdate() { }
    void OnGUI() { }
    void OnUnload() { }
}
