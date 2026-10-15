using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Jiangyu.Sdk;
using MenaceModSettings;

namespace StrongerCover;

/// <summary>
/// Lifecycle: declares the options, installs the Skill.GetCoverMult prefix and keeps the live
/// InsideCoverTemplate assets in step with the settings while a tactical mission is loaded.
/// </summary>
public sealed class StrongerCoverSystem : JiangyuSystem
{
    private const string SkillType = "Il2CppMenace.Tactical.Skills.Skill";
    private const string TacticalScene = "Tactical";

    private static StrongerCoverSystem _instance;
    private bool _inTactical;
    private bool _refreshPending;
    private float? _pendingGarrisonBase;
    private bool _patchFailureLogged;

    public override void OnInit()
    {
        _instance = this;
        CoverSettings.Register();
        ModSettings.Changed += OnSettingChanged;

        Context.Hooks.Subscribe<MissionStartedContext>(_ => OnMissionStarted());
        Context.Hooks.Subscribe<MissionFinishedContext>(_ => _inTactical = false);
        Context.Patches.Prefix(SkillType, "GetCoverMult", GetCoverMultPrefix);
        PrepareJit();
        Debug("initialized: directional cover, inside-cover templates, indirect-fire cover, squad-size scaling.");
    }

    // GetCoverMult is also called from MENACE's AI criterion tasks, which run on IL2CPP
    // thread-pool threads. A lazy JIT there crashes inside MonoMod's JIT hook, so compile
    // the whole cover path up front on the main thread.
    private static void PrepareJit()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var type in new[] { typeof(StrongerCoverSystem), typeof(CoverRules), typeof(Il2Cpp), typeof(CoverSettings) })
            foreach (var method in type.GetMethods(all))
                if (!method.IsAbstract && !method.ContainsGenericParameters)
                    try { RuntimeHelpers.PrepareMethod(method.MethodHandle); } catch { }
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _inTactical = string.Equals(sceneName, TacticalScene, StringComparison.Ordinal);
        if (_inTactical)
            Context.Coroutines.Start(DelayedApply());
    }

    public override void OnUnload()
    {
        ModSettings.Changed -= OnSettingChanged;
        if (_inTactical)
            InsideCoverTemplates.Restore();
        if (_instance == this)
            _instance = null;
    }

    private void OnMissionStarted()
    {
        _inTactical = true;
        InsideCoverTemplates.Apply(this);
    }

    // The scene is loaded a few frames before the mission's assets are all live, so wait as the
    // legacy module did; MissionStarted covers the normal path and this the fallback.
    private IEnumerator DelayedApply()
    {
        for (int i = 0; i < 10; i++)
            yield return null;
        if (_inTactical)
            InsideCoverTemplates.Apply(this);
    }

    private void OnSettingChanged(string group, string key, object value)
    {
        if (string.Equals(group, CoverSettings.Group, StringComparison.Ordinal))
        {
            CoverSettings.Reload();
            if (_inTactical && CoverSettings.AffectsInsideCover(key))
                InsideCoverTemplates.Apply(this);
            Debug($"setting changed: {key}={value}.");
            return;
        }

        // Garrisonable Buildings owns the base building profile; StrongerCover only overrides
        // AccuracyMult while enabled. Remember the new base and re-assert our value a frame
        // later so handler order cannot leave the base value active.
        if (string.Equals(group, CoverSettings.GarrisonGroup, StringComparison.Ordinal)
            && key == CoverSettings.GarrisonBuildingAccuracyKey)
        {
            try { _pendingGarrisonBase = CoverSettings.Percent(Convert.ToSingle(value)); }
            catch { _pendingGarrisonBase = null; }
            if (!_refreshPending)
            {
                _refreshPending = true;
                Context.Coroutines.Start(RefreshNextFrame());
            }
        }
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        _refreshPending = false;
        if (!_inTactical)
            yield break;
        if (_pendingGarrisonBase is { } baseAccuracy)
        {
            InsideCoverTemplates.UpdateBuildingBase(baseAccuracy);
            _pendingGarrisonBase = null;
        }
        InsideCoverTemplates.Apply(this);
    }

    private static void GetCoverMultPrefix(PatchInfo info)
    {
        if (!CoverSettings.Enabled)
            return;
        try
        {
            var skill = Il2Cpp.As<Skill>(info.Instance);
            var from = Il2Cpp.As<Tile>(info.Args[0]);
            var targetTile = Il2Cpp.As<Tile>(info.Args[1]);
            var targetEntity = Il2Cpp.As<Entity>(info.Args[2]);
            var defender = Il2Cpp.As<EntityProperties>(info.Args[3]);
            bool pretendInside = info.Args[4] is bool b && b;

            if (CoverRules.TryResolve(skill, from, targetTile, targetEntity, defender, pretendInside, out float result))
            {
                info.Result = result;
                info.Skip = true;
            }
        }
        catch (Exception e)
        {
            // Fall through to vanilla for this call. One line per session: the method runs
            // for every preview, AI query and shot, and a repeating fault would flood the log.
            var self = _instance;
            if (self != null && !self._patchFailureLogged)
            {
                self._patchFailureLogged = true;
                self.Context.Log.Warn("GetCoverMult prefix failed, vanilla cover used: " + e.Message);
            }
        }
    }

    internal void Debug(string message)
    {
        if (CoverSettings.VerboseLogging)
            Context.Log.Info(message);
    }

    internal void Warn(string message)
    {
        if (CoverSettings.VerboseLogging)
            Context.Log.Warn(message);
    }
}
