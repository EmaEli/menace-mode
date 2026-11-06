using Jiangyu.Sdk;

namespace AIPerformance;

public sealed class AIPerformanceSystem : JiangyuSystem
{
    private readonly Plugin _plugin = new();
    private HarmonyLib.Harmony _harmony;

    public override void OnInit()
    {
        _harmony = new HarmonyLib.Harmony("luca.aiperformance");
        _plugin.Initialize(Context.Log.Info, Context.Log.Warn, Context.Log.Error, _harmony);
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
        => _plugin.OnSceneLoaded(buildIndex, sceneName);

    public override void OnUnload()
    {
        _plugin.OnUnload();
        _harmony?.UnpatchSelf();
    }
}