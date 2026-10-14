using System;
using System.Collections;
using Jiangyu.Sdk;

namespace LargerMaps;

// Jiangyu host binding. All gameplay logic is in the shared Plugin partial class.
public sealed class LargerMapsSystem : JiangyuSystem
{
    private const string HarmonyId = "luca.largermaps";

    private readonly Plugin _plugin = new();
    private HarmonyLib.Harmony _harmony;
    private object _pollHandle;

    public override void OnInit()
    {
        _harmony = new HarmonyLib.Harmony(HarmonyId);
        var log = new ModLog(Context.Log.Info, Context.Log.Warn, Context.Log.Error);
        _plugin.Initialize(log, _harmony, RunDelayed, Context.Version);
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _plugin.OnSceneLoaded(buildIndex, sceneName);
        if (_pollHandle == null && Plugin.NeedsUpdatePolling)
            _pollHandle = Context.Coroutines.Start(PollEveryFrame());
    }

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

    private void RunDelayed(int frames, Action action)
        => Context.Coroutines.Start(AfterFrames(frames, action));

    private static IEnumerator AfterFrames(int frames, Action action)
    {
        for (int i = 0; i < frames; i++) yield return null;
        action();
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
