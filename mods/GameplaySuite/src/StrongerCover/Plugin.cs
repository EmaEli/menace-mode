using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("2.2.3.0")]
[assembly: AssemblyFileVersion("2.2.3.0")]

namespace MenaceStrongerCover;

public sealed class Plugin : IModpackPlugin
{
    private const string SettingsGroup = "StrongerCover";
    private const string Version = "2.2.3";
    private const string GarrisonSettingsGroup = "Garrisonable Buildings";

    private static MelonLogger.Instance? _logger;
    private static global::HarmonyLib.Harmony? _harmony;
    private static bool _patchesApplied;
    private static bool _insideCoverSeen;
    private static bool _inTacticalScene;

    private static MethodInfo? _getDistanceTo;
    private static MethodInfo? _getDirectionTo;
    private static MethodInfo? _getCover;
    private static Type? _directionType;
    private static MemberInfo? _isIgnoringCover;
    private static MethodInfo? _isIgnoringCoverInsideForTarget;
    private static MethodInfo? _skillGetTemplate;
    private static MemberInfo? _coverEffectivenessMult;
    private static MethodInfo? _isInfantry;
    private static MethodInfo? _getElements;
    private static PropertyInfo? _elementsCount;
    private static MethodInfo? _getFactionId;
    private static MethodInfo? _skillGetId;
    private static MethodInfo? _getContainerEntity;
    private static MethodInfo? _getEntityTemplate;
    private static MethodInfo? _tileGetEntity;
    private static MemberInfo? _providesCoverInside;
    private static MemberInfo? _insideAccuracyMult;

    private static bool _enabled = true;
    private static bool _respectCoverUsage = true;
    private static bool _squadSizeScaling = true;
    private static bool _excludeWildlife = true;
    private static bool _excludeConstructs = true;
    private static bool _verboseLogging;
    private static bool _indirectFireCover = true;
    private static bool _indirectAverageSurroundingCover = true;
    private static bool _indirectInsideCover = true;
    private static bool _insideCoverRefreshPending;
    private static float? _pendingBuildingBaseAccuracy;
    private static int _minimumCoverDistance = 2;

    private static readonly float[] CoverMultipliers = { 1.0f, 0.80f, 0.60f, 0.40f };
    private static float _smallSquadMultiplier = 0.70f;
    private static float _mediumSquadMultiplier = 0.80f;
    private static float _largeSquadMultiplier = 0.90f;
    private static float _bunkerMultiplier = 0.20f;
    private static float _forestMultiplier = 0.55f;
    private static float _openVehicleMultiplier = 0.60f;
    private static float _watchtowerMultiplier = 0.30f;
    private static float _buildingMultiplier = 0.30f;

    private static readonly Dictionary<string, float> OriginalInsideCover = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> InsideGameValues = new(StringComparer.OrdinalIgnoreCase);
    private static float _nextInsideDiagnosticRefresh;
    private static string _lastIndirectSkillId = "-";
    private static float _lastIndirectSurrounding = 1.0f;
    private static float _lastIndirectInside = 1.0f;
    private static float _lastIndirectFinal = 1.0f;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("EnableGameplay", "Enable StrongerCover", true);
            s.AddSlider("MinimumCoverDistance", "Minimum cover distance", 1f, 6f, 2f);
            s.AddToggle("RespectCoverUsage", "Respect defender Cover Usage", true);

            s.AddHeader("Directional Cover");
            s.AddSlider("LightCover", "Light cover hit chance (%)", 10f, 100f, 80f);
            s.AddSlider("MediumCover", "Medium cover hit chance (%)", 10f, 100f, 60f);
            s.AddSlider("HeavyCover", "Heavy cover hit chance (%)", 10f, 100f, 40f);

            s.AddHeader("Inside Cover");
            s.AddSlider("BunkerCover", "Bunker hit chance (%)", 5f, 100f, 20f);
            s.AddSlider("ForestCover", "Forest / vegetation hit chance (%)", 5f, 100f, 55f);
            s.AddSlider("OpenVehicleCover", "Open vehicle hit chance (%)", 5f, 100f, 60f);
            s.AddSlider("WatchtowerCover", "Heavy watchtower hit chance (%)", 5f, 100f, 30f);
            s.AddSlider("BuildingCover", "Garrisoned building hit chance (%)", 5f, 100f, 30f);

            s.AddHeader("Indirect Fire");
            s.AddToggle("EnableIndirectFireCover", "Enable indirect-fire cover", true);
            s.AddToggle("IndirectAverageSurroundingCover", "Use averaged surrounding cover", true);
            s.AddToggle("IndirectInsideCover", "Use inside cover protection", true);

            s.AddHeader("Squad Size");
            s.AddToggle("SquadSizeScaling", "Enable squad-size scaling", true);
            s.AddSlider("SmallSquad", "1-3 soldiers hit chance (%)", 40f, 100f, 70f);
            s.AddSlider("MediumSquad", "4-5 soldiers hit chance (%)", 40f, 100f, 80f);
            s.AddSlider("LargeSquad", "6-7 soldiers hit chance (%)", 40f, 100f, 90f);
            s.AddToggle("ExcludeWildlife", "Exclude Wildlife from squad scaling", true);
            s.AddToggle("ExcludeConstructs", "Exclude Constructs from squad scaling", true);

            s.AddHeader("Diagnostics");
            s.AddToggle("VerboseLogging", "Debug logging", false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel(SettingsGroup, DrawDebug);
        Log($"{Version}: initialized. Configurable directional cover, inside-cover templates, 360-degree indirect-fire cover, squad-size scaling, and Garrisonable Buildings compatibility enabled.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _inTacticalScene = string.Equals(sceneName, "Tactical", StringComparison.Ordinal);
        if (!_inTacticalScene) return;
        MelonCoroutines.Start(DelayedTacticalSetup());
    }

    public void OnUpdate()
    {
        // Match the pattern used by our other mods: ModSettings remains the source of truth.
        // Cached values are refreshed cheaply so changes made through the in-game settings UI
        // are reflected even if another settings provider bypasses OnSettingChanged.
        ReloadSettings();

        // Garrisonable Buildings loads later than StrongerCover. If its base building
        // accuracy changes, re-apply our stronger building value on the following frame
        // so event subscription order cannot leave the base value active.
        if (_insideCoverRefreshPending && _inTacticalScene)
        {
            _insideCoverRefreshPending = false;
            if (_pendingBuildingBaseAccuracy.HasValue)
            {
                UpdateOriginalBuildingBase(_pendingBuildingBaseAccuracy.Value);
                _pendingBuildingBaseAccuracy = null;
            }
            ApplyInsideCoverTemplates(forceLog: _verboseLogging);
        }
    }

    private static IEnumerator DelayedTacticalSetup()
    {
        for (int i = 0; i < 10; i++) yield return null;
        BindAndPatch();
        ApplyInsideCoverTemplates(forceLog: _verboseLogging);
    }

    private static void BindAndPatch()
    {
        if (_patchesApplied) return;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (gameAssembly == null)
        {
            Error("Assembly-CSharp not found; directional cover changes are unavailable.");
            return;
        }

        var skillType = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "Skill");
        var tileType = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "Tile");
        var skillTemplateType = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "SkillTemplate");
        var entityPropertiesType = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "EntityProperties");
        var entityType = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "Entity");

        if (skillType == null || tileType == null)
        {
            Error($"Required tactical types not found. Skill={skillType != null}, Tile={tileType != null}.");
            return;
        }

        _getDistanceTo = tileType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m =>
            {
                if (m.Name != "GetDistanceTo") return false;
                var p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType.IsAssignableFrom(tileType);
            });
        _getDirectionTo = tileType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m =>
            {
                if (m.Name != "GetDirectionTo") return false;
                var p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType.IsAssignableFrom(tileType);
            });
        _getCover = tileType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetCover" && m.GetParameters().Length == 4);

        if (_getCover == null)
        {
            foreach (var type in gameAssembly.GetTypes())
            {
                _getCover = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetCover" && m.GetParameters().Length == 4);
                if (_getCover != null) break;
            }
        }

        _directionType = _getCover?.GetParameters().FirstOrDefault()?.ParameterType
            ?? gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "Direction");

        if (skillTemplateType != null)
        {
            _isIgnoringCover = (MemberInfo?)skillTemplateType.GetField("IsIgnoringCover", BindingFlags.Public | BindingFlags.Instance)
                ?? skillTemplateType.GetProperty("IsIgnoringCover", BindingFlags.Public | BindingFlags.Instance);
            _isIgnoringCoverInsideForTarget = skillTemplateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "IsIgnoringCoverInsideForTarget" && m.GetParameters().Length == 1);
        }

        _skillGetTemplate = skillType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m =>
                m.Name == "GetTemplate" &&
                m.GetParameters().Length == 0 &&
                (skillTemplateType == null || skillTemplateType.IsAssignableFrom(m.ReturnType)));
        _skillGetId = skillType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetID" && m.GetParameters().Length == 0 && m.ReturnType == typeof(string));
        if (entityPropertiesType != null)
        {
            _coverEffectivenessMult = (MemberInfo?)entityPropertiesType.GetField("CoverEffectivenessMult", BindingFlags.Public | BindingFlags.Instance)
                ?? entityPropertiesType.GetProperty("CoverEffectivenessMult", BindingFlags.Public | BindingFlags.Instance);
        }
        _tileGetEntity = tileType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetEntity" && m.GetParameters().Length == 0);

        if (entityType != null)
        {
            _isInfantry = entityType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "IsInfantry" && m.GetParameters().Length == 0);
            _getElements = entityType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetElements" && m.GetParameters().Length == 0);
            _getFactionId = entityType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetFactionID" && m.GetParameters().Length == 0);
            _getContainerEntity = entityType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetContainerEntity" && m.GetParameters().Length == 0);
            _getEntityTemplate = entityType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetTemplate" && m.GetParameters().Length == 0);
            if (_getElements != null)
                _elementsCount = _getElements.ReturnType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
        }

        if (_getEntityTemplate != null)
        {
            Type templateType = _getEntityTemplate.ReturnType;
            _providesCoverInside = (MemberInfo?)templateType.GetField("ProvidesCoverInside", BindingFlags.Public | BindingFlags.Instance)
                ?? templateType.GetProperty("ProvidesCoverInside", BindingFlags.Public | BindingFlags.Instance);
            Type? insideType = GetMemberType(_providesCoverInside);
            if (insideType != null)
            {
                _insideAccuracyMult = (MemberInfo?)insideType.GetField("AccuracyMult", BindingFlags.Public | BindingFlags.Instance)
                    ?? insideType.GetProperty("AccuracyMult", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        Log($"Resolved API: GetDistanceTo={_getDistanceTo != null}, GetDirectionTo={_getDirectionTo != null}, GetCover={_getCover != null}, SkillGetTemplate={_skillGetTemplate != null}, IsIgnoringCover={_isIgnoringCover != null}, CoverEffectivenessMult={_coverEffectivenessMult != null}, SkillGetID={_skillGetId != null}, IsInfantry={_isInfantry != null}, GetElements={_getElements != null}, GetFactionID={_getFactionId != null}, InsideCover={_providesCoverInside != null && _insideAccuracyMult != null}.");

        var method = skillType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetCoverMult" && m.ReturnType == typeof(float));
        if (method == null)
        {
            Error("Skill.GetCoverMult() not found; directional cover changes are unavailable.");
            return;
        }

        try
        {
            _harmony?.Patch(method, prefix: new global::HarmonyLib.HarmonyMethod(typeof(Plugin), nameof(GetCoverMultPrefix)));
            _patchesApplied = true;
            Log("Patched Skill.GetCoverMult().");
        }
        catch (Exception e)
        {
            Error("Failed to patch Skill.GetCoverMult(): " + e);
        }
    }

    public static bool GetCoverMultPrefix(
        object __instance,
        object _from,
        object _targetTile,
        object _overrideTargetEntity,
        object _defenderProperties,
        bool _pretendToBeInsideContainer,
        ref float __result)
    {
        if (!_enabled) return true;

        try
        {
            if (SkillIgnoresCover(__instance, _overrideTargetEntity))
            {
                if (_indirectFireCover && IsIndirectFireSkill(__instance))
                {
                    __result = ResolveIndirectFireCover(
                        __instance,
                        _targetTile,
                        _overrideTargetEntity,
                        _defenderProperties,
                        _pretendToBeInsideContainer);
                    return false;
                }

                __result = 1.0f;
                return false;
            }

            if (_from == null || _targetTile == null || _getDistanceTo == null)
                return true;

            int distance = Convert.ToInt32(_getDistanceTo.Invoke(_from, new[] { _targetTile }) ?? 0);
            if (distance < _minimumCoverDistance)
            {
                __result = ApplySquadSize(1.0f, _overrideTargetEntity);
                return false;
            }

            int direction = _getDirectionTo != null
                ? Convert.ToInt32(_getDirectionTo.Invoke(_targetTile, new[] { _from }) ?? 0)
                : 0;

            int coverLevel = ResolveCoverLevel(_targetTile, direction, _overrideTargetEntity, _defenderProperties);
            coverLevel = Math.Clamp(coverLevel, 0, 3);
            float coverMultiplier = CoverMultipliers[coverLevel];

            if (_respectCoverUsage)
                coverMultiplier = ApplyCoverUsage(coverMultiplier, _defenderProperties);

            __result = Math.Clamp(ApplySquadSize(coverMultiplier, _overrideTargetEntity), 0.01f, 1.0f);
            return false;
        }
        catch (Exception e)
        {
            Error("GetCoverMult prefix failed; falling back to vanilla: " + e.Message);
            return true;
        }
    }

    private static bool IsIndirectFireSkill(object skill)
    {
        if (_skillGetId == null || skill == null) return false;

        string id;
        try { id = Convert.ToString(_skillGetId.Invoke(skill, null)) ?? string.Empty; }
        catch { return false; }

        string value = id.ToLowerInvariant();
        if (value.Contains("grenade") || value.Contains("rpg") || value.Contains("rocket") ||
            value.Contains("atgm") || value.Contains("flamethrower") || value.Contains("flame_thrower"))
            return false;

        return value.Contains("mortar") || value.Contains("howitzer") ||
               value.Contains("artillery") || value.Contains("indirect");
    }

    private static float ResolveIndirectFireCover(
        object skill,
        object targetTile,
        object targetEntity,
        object defenderProperties,
        bool pretendToBeInsideContainer)
    {
        float surrounding = 1.0f;
        if (_indirectAverageSurroundingCover && targetTile != null)
        {
            float total = 0f;
            int samples = 0;
            for (int direction = 0; direction < 8; direction++)
            {
                int level = Math.Clamp(ResolveCoverLevel(targetTile, direction, targetEntity, defenderProperties), 0, 3);
                float value = CoverMultipliers[level];
                if (_respectCoverUsage) value = ApplyCoverUsage(value, defenderProperties);
                total += value;
                samples++;
            }
            if (samples > 0) surrounding = total / samples;
        }

        float inside = 1.0f;
        if (_indirectInsideCover)
            inside = ResolveInsideCoverMultiplier(targetTile, targetEntity, pretendToBeInsideContainer);

        float final = 1.0f;
        if (_indirectAverageSurroundingCover) final = Math.Min(final, surrounding);
        if (_indirectInsideCover) final = Math.Min(final, inside);
        final = Math.Clamp(final, 0.01f, 1.0f);

        try { _lastIndirectSkillId = Convert.ToString(_skillGetId?.Invoke(skill, null)) ?? "-"; }
        catch { _lastIndirectSkillId = "-"; }
        _lastIndirectSurrounding = surrounding;
        _lastIndirectInside = inside;
        _lastIndirectFinal = final;

        // Keep hot-path combat diagnostics in the DevConsole panel only. Logging this
        // calculation for every query can generate thousands of lines per combat turn.

        // Intentionally no squad-size multiplier here: indirect fire already averages
        // all directions and can combine with dedicated inside-cover protection.
        return final;
    }

    private static float ResolveInsideCoverMultiplier(object targetTile, object targetEntity, bool pretendToBeInsideContainer)
    {
        try
        {
            object? container = targetEntity != null && _getContainerEntity != null
                ? _getContainerEntity.Invoke(targetEntity, null)
                : null;

            if (container == null && pretendToBeInsideContainer && targetTile != null && _tileGetEntity != null)
                container = _tileGetEntity.Invoke(targetTile, null);

            if (container == null || _getEntityTemplate == null || _providesCoverInside == null || _insideAccuracyMult == null)
                return 1.0f;

            object? entityTemplate = _getEntityTemplate.Invoke(container, null);
            object? insideTemplate = ReadMember(entityTemplate, _providesCoverInside);
            object? raw = ReadMember(insideTemplate, _insideAccuracyMult);
            if (raw == null) return 1.0f;
            return Math.Clamp(Convert.ToSingle(raw), 0.01f, 1.0f);
        }
        catch
        {
            return 1.0f;
        }
    }

    private static Type? GetMemberType(MemberInfo? member)
    {
        return member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => null
        };
    }

    private static object? ReadMember(object? instance, MemberInfo? member)
    {
        if (instance == null || member == null) return null;
        return member switch
        {
            FieldInfo field => field.GetValue(instance),
            PropertyInfo property when property.CanRead => property.GetValue(instance),
            _ => null
        };
    }

    private static bool SkillIgnoresCover(object skill, object targetEntity)
    {
        if (_skillGetTemplate == null || skill == null) return false;

        object? template;
        try { template = _skillGetTemplate.Invoke(skill, null); }
        catch { return false; }
        if (template == null) return false;

        // Current MENACE exposes the generic cover-ignore flag as SkillTemplate.IsIgnoringCover.
        // IsIgnoringCoverInsideForTarget() is intentionally not treated as a generic cover ignore:
        // vanilla only uses it to decide whether the special inside-container modifier applies.
        try
        {
            return ReadMember(template, _isIgnoringCover) is bool ignoresCover && ignoresCover;
        }
        catch
        {
            return false;
        }
    }

    private static int ResolveCoverLevel(object targetTile, int direction, object targetEntity, object defenderProperties)
    {
        if (_getCover == null || _directionType == null) return 0;
        object directionEnum = Enum.ToObject(_directionType, direction);
        object? result = _getCover.Invoke(targetTile, new[] { directionEnum, targetEntity, defenderProperties, (object)true });
        return result == null ? 0 : Convert.ToInt32(result);
    }

    private static float ApplyCoverUsage(float coverMultiplier, object defenderProperties)
    {
        if (defenderProperties == null || _coverEffectivenessMult == null) return coverMultiplier;

        object? raw;
        try { raw = ReadMember(defenderProperties, _coverEffectivenessMult); }
        catch { return coverMultiplier; }
        if (raw == null) return coverMultiplier;

        // Match the current vanilla GetCoverMult path: CoverEffectivenessMult is clamped
        // to 0..1, zero disables cover, otherwise the cover multiplier is divided by it.
        float effectiveness = Math.Clamp(Convert.ToSingle(raw), 0f, 1f);
        if (effectiveness <= 0f) return 1.0f;
        return Math.Min(coverMultiplier / effectiveness, 1.0f);
    }

    private static float ApplySquadSize(float multiplier, object targetEntity)
    {
        if (!_squadSizeScaling || targetEntity == null || _isInfantry == null || _getElements == null || _elementsCount == null)
            return multiplier;

        if (!Convert.ToBoolean(_isInfantry.Invoke(targetEntity, null) ?? false))
            return multiplier;

        int factionId = -1;
        if (_getFactionId != null)
        {
            try { factionId = Convert.ToInt32(_getFactionId.Invoke(targetEntity, null) ?? -1); }
            catch { factionId = -1; }
        }

        // Current game IDs observed by the v38 loader: Wildlife=7, Constructs=8.
        if ((_excludeWildlife && factionId == 7) || (_excludeConstructs && factionId == 8))
            return multiplier;

        var elements = _getElements.Invoke(targetEntity, null);
        if (elements == null) return multiplier;

        int count = Convert.ToInt32(_elementsCount.GetValue(elements) ?? 8);
        float squadMultiplier = count <= 3
            ? _smallSquadMultiplier
            : count <= 5
                ? _mediumSquadMultiplier
                : count <= 7
                    ? _largeSquadMultiplier
                    : 1.0f;

        // Do not log per-query squad scaling, even in Debug mode. GetCoverMult can be
        // evaluated many times for previews/AI/targeting and would flood MelonLoader.log.

        return multiplier * squadMultiplier;
    }

    private static void ReloadSettings()
    {
        _enabled = Setting("EnableGameplay", true);
        _respectCoverUsage = Setting("RespectCoverUsage", true);
        _squadSizeScaling = Setting("SquadSizeScaling", true);
        _excludeWildlife = Setting("ExcludeWildlife", true);
        _excludeConstructs = Setting("ExcludeConstructs", true);
        _verboseLogging = Setting("VerboseLogging", false);
        _indirectFireCover = Setting("EnableIndirectFireCover", true);
        _indirectAverageSurroundingCover = Setting("IndirectAverageSurroundingCover", true);
        _indirectInsideCover = Setting("IndirectInsideCover", true);
        _minimumCoverDistance = Math.Clamp((int)Math.Round(Setting("MinimumCoverDistance", 2f)), 1, 6);

        CoverMultipliers[1] = Percent(Setting("LightCover", 80f));
        CoverMultipliers[2] = Percent(Setting("MediumCover", 60f));
        CoverMultipliers[3] = Percent(Setting("HeavyCover", 40f));
        _bunkerMultiplier = Percent(Setting("BunkerCover", 20f));
        _forestMultiplier = Percent(Setting("ForestCover", 55f));
        _openVehicleMultiplier = Percent(Setting("OpenVehicleCover", 60f));
        _watchtowerMultiplier = Percent(Setting("WatchtowerCover", 30f));
        _buildingMultiplier = Percent(Setting("BuildingCover", 30f));
        _smallSquadMultiplier = Percent(Setting("SmallSquad", 70f));
        _mediumSquadMultiplier = Percent(Setting("MediumSquad", 80f));
        _largeSquadMultiplier = Percent(Setting("LargeSquad", 90f));
    }

    private static T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private static float Percent(float value) => Math.Clamp(value / 100f, 0.01f, 1.0f);

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (string.Equals(modName, SettingsGroup, StringComparison.Ordinal))
        {
            ReloadSettings();

            if (_inTacticalScene &&
                key is ("EnableGameplay" or "BunkerCover" or "ForestCover" or "OpenVehicleCover" or "WatchtowerCover" or "BuildingCover"))
            {
                ApplyInsideCoverTemplates(forceLog: _verboseLogging);
            }

            if (_verboseLogging)
                Log($"Setting changed: {key}={value}.");
            return;
        }

        // Garrisonable Buildings owns the base building profile. StrongerCover only
        // overrides AccuracyMult while enabled, so remember the user's latest base
        // value and re-assert our stronger value on the next frame.
        if (string.Equals(modName, GarrisonSettingsGroup, StringComparison.Ordinal) && key == "BuildingAccuracy")
        {
            try { _pendingBuildingBaseAccuracy = Percent(Convert.ToSingle(value)); }
            catch { _pendingBuildingBaseAccuracy = null; }
            _insideCoverRefreshPending = true;
        }
    }

    private static void ApplyInsideCoverTemplates(bool forceLog)
    {
        try
        {
            var templates = GameQuery.FindAll("InsideCoverTemplate");
            if (templates == null || templates.Length == 0)
            {
                if (forceLog) Warn("No InsideCoverTemplate instances are currently available.");
                return;
            }

            InsideGameValues.Clear();

            var classified = new Dictionary<string, GameObj>(StringComparer.OrdinalIgnoreCase);
            var unmatched = new List<GameObj>();

            foreach (var template in templates)
            {
                string name = template.GetName() ?? string.Empty;
                string category = ClassifyInsideCover(name);
                if (category.Length == 0) unmatched.Add(template);
                else classified[category] = template;
            }

            if (!classified.ContainsKey("forest") && unmatched.Count == 1 &&
                classified.ContainsKey("bunker") && classified.ContainsKey("vehicle") && classified.ContainsKey("watchtower"))
            {
                classified["forest"] = unmatched[0];
                if (forceLog || _verboseLogging)
                    Log($"Detected forest/vegetation template by elimination: '{unmatched[0].GetName()}'.");
                unmatched.Clear();
            }

            int changed = 0;
            changed += ApplyInside(classified, "bunker", _bunkerMultiplier) ? 1 : 0;
            changed += ApplyInside(classified, "forest", _forestMultiplier) ? 1 : 0;
            changed += ApplyInside(classified, "vehicle", _openVehicleMultiplier) ? 1 : 0;
            changed += ApplyInside(classified, "watchtower", _watchtowerMultiplier) ? 1 : 0;
            changed += ApplyInside(classified, "building", _buildingMultiplier) ? 1 : 0;
            _insideCoverSeen = changed > 0;

            if (forceLog || _verboseLogging)
            {
                Log($"Inside cover target templates processed: {changed} known profile(s). Enabled={_enabled}; Garrisonable building profile={(classified.ContainsKey("building") ? "present" : "not found")}.");
                if (classified.TryGetValue("closed_vehicle", out var closedVehicle))
                    Log($"Closed vehicle template left at vanilla value: '{closedVehicle.GetName()}'.");
                foreach (var item in unmatched)
                    Log($"Unknown InsideCoverTemplate left unchanged: '{item.GetName()}'.");
            }
        }
        catch (Exception e)
        {
            if (forceLog || _verboseLogging)
                Warn("Could not update InsideCoverTemplate values: " + e.Message);
        }
    }

    private static string ClassifyInsideCover(string name)
    {
        string value = name.ToLowerInvariant();
        if (value.Contains("bunker")) return "bunker";
        if (value.Contains("forest") || value.Contains("vegetation") || value.Contains("woods")) return "forest";
        if (value.Contains("watchtower") || value.Contains("watch_tower") || value.Contains("tower_heavy")) return "watchtower";
        if (value.Contains("cover_inside_building") || value.Contains("garrisonable_building")) return "building";
        if (value.Contains("vehicle") && value.Contains("open")) return "vehicle";
        if (value.Contains("vehicle") && value.Contains("closed")) return "closed_vehicle";
        return string.Empty;
    }

    private static bool ApplyInside(Dictionary<string, GameObj> classified, string category, float configuredValue)
    {
        if (!classified.TryGetValue(category, out var template)) return false;
        string name = template.GetName() ?? category;

        if (!OriginalInsideCover.ContainsKey(name))
        {
            try
            {
                // The live building profile may already reflect StrongerCover because
                // Garrisonable Buildings has a compatibility fallback. Preserve the
                // actual Garrisonable base setting so disable/unload restores 40% (or
                // the user's configured base), not our stronger runtime value.
                if (category == "building" && TryReadGarrisonBuildingBaseAccuracy(out float buildingBase))
                    OriginalInsideCover[name] = buildingBase;
                else
                    OriginalInsideCover[name] = template.ReadFloat("AccuracyMult");
            }
            catch { }
        }

        float value = configuredValue;
        if (!_enabled && OriginalInsideCover.TryGetValue(name, out float originalValue))
            value = originalValue;

        bool ok = template.WriteFloat("AccuracyMult", value);

        // Read the value back from the live game object. This is the value shown in the
        // diagnostic panel, so the panel reports what MENACE actually has rather than
        // merely echoing the configured ModSettings value.
        try
        {
            InsideGameValues[category] = template.ReadFloat("AccuracyMult");
        }
        catch
        {
            InsideGameValues.Remove(category);
        }

        // Successful per-template writes are visible in the DevConsole diagnostics.
        // Only log failures so Debug mode stays compact during template refreshes.
        if (_verboseLogging && !ok)
            Warn($"Inside cover '{name}' write failed (requested AccuracyMult={value:0.00}).");
        return ok;
    }

    private static bool TryReadGarrisonBuildingBaseAccuracy(out float value)
    {
        value = 0.40f;
        try
        {
            float setting = ModSettings.Get<float>(GarrisonSettingsGroup, "BuildingAccuracy");
            value = Percent(setting);
            return true;
        }
        catch
        {
            try
            {
                object raw = ModSettings.Get(GarrisonSettingsGroup, "BuildingAccuracy");
                if (raw == null) return false;
                value = Percent(Convert.ToSingle(raw));
                return true;
            }
            catch { return false; }
        }
    }

    private static void UpdateOriginalBuildingBase(float baseAccuracy)
    {
        try
        {
            var templates = GameQuery.FindAll("InsideCoverTemplate");
            foreach (var template in templates)
            {
                if (ClassifyInsideCover(template.GetName() ?? string.Empty) != "building") continue;
                string name = template.GetName() ?? "cover_inside_building";
                OriginalInsideCover[name] = Math.Clamp(baseAccuracy, 0.01f, 1.0f);
                if (_verboseLogging)
                    Log($"Updated Garrisonable Buildings base AccuracyMult snapshot: {OriginalInsideCover[name]:0.00}.");
                break;
            }
        }
        catch { }
    }

    private static void RestoreInsideCoverTemplates()
    {
        if (OriginalInsideCover.Count == 0) return;
        try
        {
            var templates = GameQuery.FindAll("InsideCoverTemplate");
            foreach (var template in templates)
            {
                string name = template.GetName() ?? string.Empty;
                if (OriginalInsideCover.TryGetValue(name, out float original))
                    template.WriteFloat("AccuracyMult", original);
            }
        }
        catch { }
    }

    private static void RefreshInsideGameValues()
    {
        if (!_inTacticalScene) return;

        try
        {
            var templates = GameQuery.FindAll("InsideCoverTemplate");
            if (templates == null || templates.Length == 0)
            {
                InsideGameValues.Clear();
                return;
            }

            var classified = new Dictionary<string, GameObj>(StringComparer.OrdinalIgnoreCase);
            var unmatched = new List<GameObj>();

            foreach (var template in templates)
            {
                string category = ClassifyInsideCover(template.GetName() ?? string.Empty);
                if (category.Length == 0) unmatched.Add(template);
                else classified[category] = template;
            }

            if (!classified.ContainsKey("forest") && unmatched.Count == 1 &&
                classified.ContainsKey("bunker") && classified.ContainsKey("vehicle") && classified.ContainsKey("watchtower"))
                classified["forest"] = unmatched[0];

            InsideGameValues.Clear();
            foreach (string category in new[] { "bunker", "forest", "vehicle", "watchtower", "building" })
            {
                if (!classified.TryGetValue(category, out var template)) continue;
                try { InsideGameValues[category] = template.ReadFloat("AccuracyMult"); }
                catch { }
            }
        }
        catch
        {
            InsideGameValues.Clear();
        }
    }

    private static void DrawDebug(Rect area)
    {
        float y = area.y;
        const float h = 22f;
        void Line(string text)
        {
            GUI.Label(new Rect(area.x, y, area.width, h), text);
            y += h;
        }

        void InsideLine(string label, string category, float configuredValue)
        {
            if (!_inTacticalScene)
            {
                Line($"{label}: not loaded (Tactical only)");
                return;
            }

            if (!InsideGameValues.TryGetValue(category, out float gameValue))
            {
                Line($"{label}: not found");
                return;
            }

            string text = $"{label}: {gameValue * 100:0}%";
            if (_enabled && Math.Abs(gameValue - configuredValue) > 0.005f)
                text += $"  [WARNING: setting {configuredValue * 100:0}%]";
            Line(text);
        }

        if (_inTacticalScene && Time.unscaledTime >= _nextInsideDiagnosticRefresh)
        {
            _nextInsideDiagnosticRefresh = Time.unscaledTime + 0.5f;
            RefreshInsideGameValues();
        }

        Line($"StrongerCover {Version}");
        Line("Enabled: " + (_enabled ? "yes" : "no"));
        Line("Harmony patch: " + (_patchesApplied ? "bound" : "not bound"));
        Line("Inside-cover templates: " + (_insideCoverSeen ? "seen" : "not seen"));
        Line($"Directional: Light {CoverMultipliers[1] * 100:0}% / Medium {CoverMultipliers[2] * 100:0}% / Heavy {CoverMultipliers[3] * 100:0}%");
        Line("Inside cover (live game values):");
        InsideLine("Bunker", "bunker", _bunkerMultiplier);
        InsideLine("Forest", "forest", _forestMultiplier);
        InsideLine("Open vehicle", "vehicle", _openVehicleMultiplier);
        InsideLine("Watchtower", "watchtower", _watchtowerMultiplier);
        InsideLine("Garrisoned building", "building", _buildingMultiplier);
        Line($"Indirect fire cover: {(_indirectFireCover ? "on" : "off")}");
        Line($"Last indirect: {_lastIndirectSkillId}");
        Line($"  surrounding {_lastIndirectSurrounding * 100:0.#}% / inside {_lastIndirectInside * 100:0.#}% / final {_lastIndirectFinal * 100:0.#}%");
        Line($"Squad scaling: {(_squadSizeScaling ? "on" : "off")} ({_smallSquadMultiplier * 100:0}% / {_mediumSquadMultiplier * 100:0}% / {_largeSquadMultiplier * 100:0}%)");
        Line($"Faction exclusions: Wildlife={(_excludeWildlife ? "yes" : "no")}, Constructs={(_excludeConstructs ? "yes" : "no")}");
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel(SettingsGroup);
        if (_inTacticalScene)
            RestoreInsideCoverTemplates();
    }

    private static void Log(string message)
    {
        if (_verboseLogging) _logger?.Msg(message);
    }

    private static void Warn(string message)
    {
        if (_verboseLogging) _logger?.Warning(message);
    }

    private static void Error(string message)
    {
        if (_verboseLogging) _logger?.Error(message);
    }
}
