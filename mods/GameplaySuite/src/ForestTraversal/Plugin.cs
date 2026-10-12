using System;
using System.Reflection;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tools;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("0.1.2.0")]
[assembly: AssemblyFileVersion("0.1.2.0")]
[assembly: AssemblyInformationalVersion("0.1.2")]

namespace MenaceForestTraversal;

public sealed class Plugin : IModpackPlugin
{
    private const string ModName = "Forest Traversal";
    private const string Version = "0.1.2";
    private const string DenseForestTemplateId = "environment_backbone_forestdense";
    private const int RetryEveryUpdates = 120;
    private const int MaxAttempts = 30;

    private static MelonLogger.Instance? _log;
    private static int _updateCounter;
    private static int _attempts;
    private static bool _finished;
    private static bool _enabled = true;
    private static bool _debug;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("Gameplay");
            settings.AddToggle(
                "EnableDenseForestTraversal",
                "Infantry can traverse Dense Forest",
                true);
            settings.AddHeader("Diagnostics");
            settings.AddToggle(
                "DebugLogging",
                "Enable diagnostic logging (OFF by default)",
                false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;
        InstallStructureCreateHook(harmony);
        TryApplyTemplate("initialize");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _finished = false;
        _attempts = 0;
        _updateCounter = 0;
        TryApplyTemplate($"scene:{sceneName}");
    }

    public void OnUpdate()
    {
        if (_finished) return;
        _updateCounter++;
        if (_updateCounter < RetryEveryUpdates) return;
        _updateCounter = 0;
        TryApplyTemplate("update-retry");
    }

    public void OnGUI() { }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, ModName, StringComparison.Ordinal)) return;
        ReloadSettings();
        _finished = false;
        _attempts = 0;
        TryApplyTemplate($"setting:{key}");
    }

    private static void ReloadSettings()
    {
        _enabled = ReadBool("EnableDenseForestTraversal", true);
        _debug = ReadBool("DebugLogging", false);
    }

    private static bool ReadBool(string key, bool fallback)
    {
        try
        {
            object? value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToBoolean(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static void InstallStructureCreateHook(HarmonyLib.Harmony harmony)
    {
        try
        {
            MethodInfo? create = AccessTools.Method(
                typeof(Structure),
                nameof(Structure.Create),
                new[] { typeof(EntityTemplate), typeof(Tile), typeof(int), typeof(int) });

            MethodInfo? prefix = AccessTools.Method(typeof(Plugin), nameof(StructureCreatePrefix));

            if (create == null || prefix == null)
            {
                DebugLog("Structure.Create(EntityTemplate, Tile, int, int) was not found; relying on template patch/retries.");
                return;
            }

            harmony.Patch(create, prefix: new HarmonyMethod(prefix));
            DebugLog("Hooked Structure.Create to reinforce Dense Forest traversal state before spawn.");
        }
        catch (Exception ex)
        {
            _log?.Warning($"{ModName}: failed to install Structure.Create hook: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void StructureCreatePrefix(EntityTemplate __0)
    {
        try
        {
            ApplyIfDenseForest(__0, "Structure.Create");
        }
        catch (Exception ex)
        {
            if (_debug)
                _log?.Warning($"{ModName}: Structure.Create prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TryApplyTemplate(string reason)
    {
        _attempts++;

        EntityTemplate? template = null;
        try
        {
            template = DataTemplateLoader.Get<EntityTemplate>(DenseForestTemplateId, false);
        }
        catch (Exception ex)
        {
            DebugLog($"DataTemplateLoader.Get failed during {reason}: {ex.GetType().Name}: {ex.Message}");
        }

        if (template == null)
        {
            try
            {
                EntityTemplate[] loadedTemplates = Resources.FindObjectsOfTypeAll<EntityTemplate>();
                for (int i = 0; i < loadedTemplates.Length; i++)
                {
                    EntityTemplate candidate = loadedTemplates[i];
                    if (candidate != null && IsDenseForest(candidate))
                    {
                        template = candidate;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Resources fallback failed during {reason}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (template == null)
        {
            if (_attempts >= MaxAttempts)
            {
                _finished = true;
                _log?.Warning($"{ModName}: Dense Forest template was not resolved after {MaxAttempts} attempts. The modpack data patch remains the primary fallback.");
            }
            return;
        }

        ApplyIfDenseForest(template, reason);
        _finished = true;
    }

    private static bool ApplyIfDenseForest(EntityTemplate? template, string reason)
    {
        if (template == null || !IsDenseForest(template)) return false;

        bool currentTraversable = template.IsTraversableByInfantry;
        EntityContainerType currentContainer = template.ContainerType;
        EntityContainerType wantedContainer = _enabled ? EntityContainerType.Forest : EntityContainerType.None;

        if (currentTraversable == _enabled && currentContainer == wantedContainer)
        {
            DebugLog($"{DenseForestTemplateId}: traversal/container state already correct ({reason}).");
            return true;
        }

        template.IsTraversableByInfantry = _enabled;
        template.ContainerType = wantedContainer;

        bool traversableReadback = template.IsTraversableByInfantry;
        EntityContainerType containerReadback = template.ContainerType;
        if (traversableReadback != _enabled || containerReadback != wantedContainer)
        {
            _log?.Error($"{ModName}: Dense Forest write did not stick (traversable wanted={_enabled}, readback={traversableReadback}; container wanted={wantedContainer}, readback={containerReadback}).");
            return false;
        }

        DebugLog($"{DenseForestTemplateId}: IsTraversableByInfantry {currentTraversable}->{traversableReadback}, ContainerType {currentContainer}->{containerReadback} ({reason}). Vehicles remain blocked; native Forest/structure/container costs remain authoritative.");
        return true;
    }

    private static bool IsDenseForest(EntityTemplate template)
    {
        try
        {
            return string.Equals(template.GetID(), DenseForestTemplateId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void DebugLog(string message)
    {
        if (_debug) _log?.Msg($"{ModName} {Version}: {message}");
    }
}
