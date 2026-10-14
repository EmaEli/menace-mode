#if JIANGYU
#nullable enable
using System;
using System.Collections;
using Jiangyu.Sdk;

namespace MenaceModSuite.Shared;

// Jiangyu host binding shared by every GameplaySuite module. The gameplay code is the
// module's Plugin (IHostedPlugin); this class only adapts the JiangyuSystem lifecycle:
// a mod-owned Harmony instance, Context.Log behind ModLog and a per-frame coroutine in
// place of the legacy OnUpdate callback. Not compiled into the legacy modpack DLLs.
public abstract class SuiteSystem<TPlugin> : JiangyuSystem where TPlugin : class, IHostedPlugin, new()
{
    private readonly TPlugin _plugin = new();
    private HarmonyLib.Harmony? _harmony;
    private object? _pollHandle;

    protected abstract string HarmonyId { get; }

    protected TPlugin Plugin => _plugin;

    public override void OnInit()
    {
        _harmony = new HarmonyLib.Harmony(HarmonyId);
        var log = new ModLog(Context.Log.Info, Context.Log.Warn, Context.Log.Error);
        _plugin.Initialize(log, _harmony);
        _pollHandle = Context.Coroutines.Start(PollEveryFrame());
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
        => _plugin.OnSceneLoaded(buildIndex, sceneName);

    public override void OnUnload()
    {
        if (_pollHandle != null)
        {
            Context.Coroutines.Stop(_pollHandle);
            _pollHandle = null;
        }
        _plugin.OnUnload();
        try { _harmony?.UnpatchSelf(); }
        catch (Exception e) { Context.Log.Warn("Harmony unpatch failed: " + e.Message); }
    }

    private IEnumerator PollEveryFrame()
    {
        while (true)
        {
            _plugin.OnUpdate();
            yield return null;
        }
    }
}
#endif
