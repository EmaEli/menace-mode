#nullable enable
using System;
using System.Collections;
using System.Reflection;
using Jiangyu.Sdk;

namespace MenaceModSuite.Shared;

// Jiangyu host binding shared by every GameplaySuite module. The gameplay code is the
// module's Plugin (IHostedPlugin); this class only adapts the JiangyuSystem lifecycle:
// a mod-owned Harmony instance, Context.Log behind ModLog, a per-frame coroutine for
// the module's OnUpdate callback and MenaceModSettings.ModGui for its OnGUI callback
// (only when the module declares its own OnGUI).
public abstract class SuiteSystem<TPlugin> : JiangyuSystem where TPlugin : class, IHostedPlugin, new()
{
    private readonly TPlugin _plugin = new();
    private HarmonyLib.Harmony? _harmony;
    private object? _pollHandle;
    private Action? _draw;

    protected abstract string HarmonyId { get; }

    protected TPlugin Plugin => _plugin;

    public override void OnInit()
    {
        _harmony = new HarmonyLib.Harmony(HarmonyId);
        var log = new ModLog(Context.Log.Info, Context.Log.Warn, Context.Log.Error);
        _plugin.Initialize(log, _harmony);
        _pollHandle = Context.Coroutines.Start(PollEveryFrame());
        if (typeof(TPlugin).GetMethod("OnGUI", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null) != null)
        {
            _draw = _plugin.OnGUI;
            MenaceModSettings.ModGui.Add(_draw);
        }
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
        => _plugin.OnSceneLoaded(buildIndex, sceneName);

    public override void OnUnload()
    {
        if (_draw != null)
        {
            MenaceModSettings.ModGui.Remove(_draw);
            _draw = null;
        }
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
