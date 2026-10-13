using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using Il2CppMenace.Tactical.Mapgen;
using Il2CppMenace.Tactical.Mapgen.TerrainGen;
using UnityEngine;

namespace LargerMaps;

public sealed partial class Plugin : IModpackPlugin
{
    private const string ModName = "Larger Maps";
    private const string Version = "1.9";
    private const int VanillaSize = 42;
    // The active map size is fixed once at startup, after the native backend is verified.
    // If fingerprint/instruction validation fails we deliberately retain safe 64x64.
    private static int _activeMapSize = 64;
    private static int TargetSize => _activeMapSize;
    private const int MinimumEvacuationSize = 5;
    private const int MinimumCaptureAreaSize = 5;
    // 64x64 is the established Larger Maps reference geometry. The native
    // MissionAreaConfig result is the baseline for larger anchored/control
    // areas: never enlarge a zone to the dimensions of a whole compound.
    private const int ZoneReferenceSize = 64;
    // Spawn areas are RectInt half-open ranges. 0 / negative width or height
    // produce no candidate cells even when the anchor itself is on the map.
    private const int MinimumAiSpawnAreaSize = 5;
    private const int ElevationPixelsPerTile = 32;

    private static MelonLogger.Instance _log;
    private static Type _mapType;
    private static Type _missionPreviewType;
    private static Type _missionType;
    private static Type _terrainGeneratorType;
    private static Type _blueprintType;
    private static Type _baseTileHighlighterType;
    private static Type _mapElevationTextureType;
    private static Type _tacticalConfigType;
    private static Type _captureAreaType;
    private static Type _objectiveType;
    private static Type _tileHighlighterType;
    private static Type _tacticalCameraType;
    private static Type _missionTemplateType;
    private static Type _genericMissionTemplateType;
    private static Type _missionAreaConfigType;
    private static Type _reinforcementGroupType;
    private static Type _killUnitType;
    private static Type _rescueUnitType;
    private static Type _evacuateToAreaType;
    private static Type _recoverObjectType;
    private static Type _chunkMetaGeneratorType;
    private static Type _aiFactionType;
    private static Type _agentType;
    private static Type _moveBehaviorType;
    private static Type _skillType;
    private static Type _lineOfSightType;
    private static Type _pathfindingProcessType;
    private static Type _battlePlanType;
    private static Type _missionPrepUiScreenType;
    private static Type _tacticalManagerType;
    private static Type _unitActorType;
    private static Type _tacticalStateType;
    private static Type _terrainUtilitiesType;
    private static Type _pointPropGeneratorType;
    private static Type _groundCoverScatterType;
    private static Type _terrainPointSpawnerType;
    private static Type _mapGenerationPassType;
    private static List<Type> _mapPassConcreteTypes;

    private static object _missionPreviewInstance;
    private static object _mapInstance;

    private static int _terrainMoveNextCalls;
    private static bool _blueprintReplacementAttempted;
    private static bool _blueprintReplaced;
    private static int _tileHighlighterInitCalls;
    private static int _tileHighlighterInitPatches;
    private static bool _tileHighlighterSurfaceDumped;
    private static MethodInfo _baseTileHighlighterInitMethod;
    private static bool _tileHighlighterReinitInProgress;
    private static int _extendedZoomPatches;

    private static int _enemySpawnAreaScopeDepth;
    private static int _enemySpawnRectsScaled;
    private static int _playerDeploymentAreaScopeDepth;
    private static int _playerDeploymentRectsScaled;
    private static int _objectiveTargetSpawnAreaScopeDepth;
    private static int _objectiveTargetSpawnRectsScaled;
    private static int _initialAiSpawnRectsFixed;
    private static int _tacticalAiSpawnRectsFixed;
    private static int _spawnFixFailures;
    private static int _spawnFixNoticeCount;
    private static int _evacuationAreaScopeDepth;
    private static int _evacuationRectsScaled;
    private static int _recoverDropoffAreaScopeDepth;
    private static int _recoverDropoffRectsScaled;
    private static List<object> _recoverDropoffAreaConfigs;

    private sealed class EvacSetpieceOffsetRestore
    {
        public object Placement;
        public int X;
        public int Y;
        public int VarianceX;
        public int VarianceY;
        public bool RestoreOffset;
        public bool RestoreVariance;
    }

    private sealed class EvacSetpiecePatchState
    {
        public readonly List<EvacSetpieceOffsetRestore> Restores = new();
        public string MissionId = "<unknown>";
    }

    private sealed class AiDiagState
    {
        public string LastState = "<none>";
        public long LastStateChangeMs;
        public long LastHeartbeatMs;
    }

    private sealed class SkillTargetDiagState
    {
        public long StartMs;
        public int Serial;
        public object Skill;
        public object Origin;
        public object Target;
        public object Into;
        public string PreList;
        public SkillTargetDiagState PreviousAoeContext;
        public bool AoeContextInstalled;
        public bool AoeContextRestored;
    }

    private static readonly Dictionary<int, AiDiagState> _aiDiagStates = new();
    private static int _aiPickCalls;
    private static int _pathCalls;
    private static int _routeAgentLines;
    private static int _routeMoveLines;
    private static int _skillTargetDiagSerial;
    private static int _aoeNullCompareHits;
    private static int _aoeRandomNullHits;
    private static int _isUsableExceptionHits;
    private static int _lineOfSightExceptionHits;
    [ThreadStatic] private static SkillTargetDiagState _activeAoeDiagState;
    private static int _deployEntitiesCalls;
    private static int _unitActorCreateCalls;
    private static int _tacticalManagerOnInitCalls;
    private static int _tacticalManagerStartCalls;
    private static int _tacticalStateOnMapReadyCalls;
    private static int _mapGenerateMoveNextCalls;
    private static int _mapGenerateStateBeforeCall = int.MinValue;
    private static int _mapGenerateLastState = int.MinValue;
    private static string _mapGenerateLastCurrent = "<none>";
    private static long _mapGenerateLastStepMs;
    private static long _mapGenerateMoveStartMs;
    private static bool _mapGenerateCompleted;
    private static int _spawnMapMoveNextCalls;
    private static int _spawnMapStateBeforeCall = int.MinValue;
    private static int _spawnMapLastState = int.MinValue;
    private static string _spawnMapLastCurrent = "<none>";
    private static long _spawnMapLastStepMs;
    private static long _spawnMapMoveStartMs;
    private static bool _spawnMapCompleted;
    private static int _worldHeightClampCorrections;
    private static int _terrainPointResolveCalls;
    private static int _terrainPointManagedHandled;
    private static int _terrainPointManagedAccepted;
    private static int _terrainPointRejectedSlope;
    private static int _terrainPointRejectedInfill;
    private static int _terrainPointDecorativeHandled;
    private static int _terrainPointGameplayHandled;
    private static int _terrainPointGroundHandled;
    private static int _terrainPointResolverFailures;
    private static int _terrainPointFixLogs;
    private static int _missionRoundConfigsScaled;
    private static readonly Dictionary<long, int> _originalRoundLimits = new();
    private static int _missionPrepPixelExtentCorrections;
    private static int _elevationRenderTextureCorrections;
    private static bool _terrainHoleFailureLogged;
    private static int _captureAreaCorrections;
    private static int _objectiveAiZoneCorrections;
    private static readonly HashSet<long> _scaledObjectiveAiZoneConfigs = new();
    [ThreadStatic] private static object _objectiveAreaContext;
    private static int _objectiveZonesResized;
    private static int _referenceScaledAnchoredAreas;
    private static int _referenceScaledBorderAreas;
    private static int _referenceScaledAuthoredAreas;
    private static int _areaGeometryDiagnosticLines;
    private const int MaxAreaGeometryDiagnosticLines = 160;

    private enum ObjectiveAreaRole { Unknown, ControlOrExtraction, Trigger, Spawn, AI }

    // 0.28 foliage-pipeline diagnostics. These counters intentionally do not alter
    // generation behavior; they only reveal where Ground/Gameplay TerrainPoints
    // are created and where they stop reaching the resolver.
    private static bool _generationPassListDumped;
    private static int _groundCoverScatterCalls;
    private static int _terrainPointSpawnerInitCalls;
    private static int _terrainPointProcessCalls;
    private static string _spawnMapPassBefore = "<none>";
    private static string _spawnMapTerrainBefore = "<none>";
    private static string _spawnMapPointsBefore = "blueprint=<null>";
    private static string _terrainGeneratePassBefore = "<none>";
    private static string _terrainGeneratePointsBefore = "blueprint=<null>";
    private static readonly Dictionary<ushort, int> _resolverAssetTypeCounts = new();
    private static readonly Dictionary<ushort, int> _resolverFlagCounts = new();

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;

        ModSettings.Register(ModName, settings =>
        {
            settings.AddHeader("Experimental maps (restart required)");
            settings.AddSlider("TacticalMapSize", "Tactical map size: 64 / 72 / 80 / 88 / 96 (restart required)", 64f, 96f, 80f);
            settings.AddHeader("Terrain landmarks (experimental, restart required)");
            settings.AddToggle("IncreaseTerrainStampDensity", "Increase fixed terrain stamp counts on maps above 64x64 (OFF by default)", false);
            settings.AddSlider("TerrainStampDensityMultiplier", "Terrain stamp attempt multiplier at 96x96 (1x-5x, restart required)", 1f, 5f, 1.5f);
            settings.AddToggle("DiagnoseTerrainStampDensity", "Log capped terrain-stamp generation counts (OFF by default)", false);
            settings.AddHeader("Mission area investigation (preview only, optional)");
            settings.AddToggle("DiagnoseMissionAreas", "Log mission area coordinates before/after transformations (restart required)", false);
            settings.AddHeader("Simplified combat AI (large maps only; restart required)");
            settings.AddToggle("SimplifyCombatAIPlanning", "Stop attack AI searching future firing positions; keep attacks available (restart required)", true);
            settings.AddToggle("PreserveBasicInfantryFirePositioning", "MEN-002 experiment: keep move-to-fire planning for five basic infantry guns (OFF; restart required)", false);
            settings.AddHeader("Local Tactical Routing (large maps only; restart required)");
            settings.AddToggle("UseLocalTacticalRouting", "Limit AI destination search on maps above 64x64 (restart required)", false);
            settings.AddSlider("LocalNearTileLimit", "Maximum nearby destinations inspected for AI movement", 12f, 64f, 20f);
            settings.AddSlider("LocalCullTileDistance", "Cull distant tactical positions (tiles)", 10f, 20f, 12f);
            settings.AddHeader("AI route investigation (optional)");
            settings.AddToggle("DiagnoseAIRoutes", "Diagnose AI operative zones and chosen movement targets (restart required)", false);
            settings.AddHeader("AI performance (diagnostic only, restart required)");
            settings.AddToggle("DiagnoseAIPerformance", "Aggregate AI task / unit timings; OFF by default", false);
            settings.AddToggle("DiagnoseAIPathfinding", "Extra AI operation and path timings (requires DiagnoseAIPerformance; OFF by default)", false);
            settings.AddHeader("Mission pacing");
            settings.AddSlider("MissionRoundMultiplier", "Multiply round limits for timed attack/defense objectives on enlarged maps", 1f, 3f, 2f);

            settings.AddHeader("Camera");
            settings.AddToggle("ExtendedZoomOut", "Allow much farther tactical camera zoom-out on enlarged maps", true);
            settings.AddSlider("ZoomOutMultiplier", "Maximum tactical zoom-out multiplier", 1f, 4f, 2f);
        });

        // Install before the game constructs any PathfindingProcess. All native sites
        // must match the exact supplied GameAssembly; failed verification is 64x64-only.
        int requested = ReadRequestedMapSize();
        if (requested > 64)
        {
            if (ExtendedPathfinding.TryInstall(requested, out string reason))
            {
                _activeMapSize = requested;
                _log.Msg($"{ModName}: experimental {requested}x{requested} backend active (128x128 pathfinder, native mission bounds and verified instructions).");
            }
            else
            {
                _activeMapSize = 64;
                _log.Warning($"{ModName}: extended pathfinding unavailable; using safe 64x64 fallback. {reason}");
            }
        }
        else _log.Msg($"{ModName}: native 64x64 pathfinding selected (no native byte patches).");

        ResolveTypes();
        InstallHooks(harmony);
        _log.Msg($"{ModName} {Version}: {TargetSize}x{TargetSize} tactical map; AI task safety and map/pathfinder backend unchanged; " +
            (TargetSize <= ZoneReferenceSize ? "64x64 reference mission areas." : "conservative mission areas (no broad objective expansion)."));
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
        {
            ApplySimplifiedCombatPlanning();
            ApplyLocalTacticalRouting();
        }
        else if (string.Equals(sceneName, "Strategy", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sceneName, "Title", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sceneName, "MissionPreparation", StringComparison.OrdinalIgnoreCase))
        {
            RestoreSimplifiedCombatPlanning();
            RestoreLocalTacticalRouting();
        }

        if (!Enabled() || !Debug()) return;

        if (string.Equals(sceneName, "MissionPreparation", StringComparison.OrdinalIgnoreCase))
        {
            GameState.RunDelayed(30, () => ProbeMissionPreview("MissionPrep+30"));
            GameState.RunDelayed(120, () => ProbeMissionPreview("MissionPrep+120"));
        }

        if (string.Equals(sceneName, "Tactical", StringComparison.OrdinalIgnoreCase))
        {
            GameState.RunDelayed(1, () => { ProbeMap("Tactical+1"); ProbeTileHighlighter("Tactical+1"); ProbeTacticalStartup("Tactical+1"); });
            GameState.RunDelayed(30, () => { ProbeMap("Tactical+30"); ProbeTileHighlighter("Tactical+30"); ProbeTacticalStartup("Tactical+30"); });
            GameState.RunDelayed(120, () => ProbeTacticalStartup("Tactical+120"));
            GameState.RunDelayed(300, () => ProbeTacticalStartup("Tactical+300"));
            GameState.RunDelayed(600, () => ProbeTacticalStartup("Tactical+600"));
        }
    }

    public void OnUpdate()
    {
        if (_aiPerfEnabled)
        {
            try { PollAiPerformance(); }
            catch (Exception ex)
            {
                if (System.Threading.Interlocked.Increment(ref _aiPerfPollFailures) <= 3)
                    _log.Warning($"{ModName}: AI PERF poll skipped: {ex.GetType().Name}.");
            }
        }
    }
    public void OnGUI() { }
    public void OnUnload() { RestoreSimplifiedCombatPlanning(); RestoreLocalTacticalRouting(); }

    private static void ResolveTypes()
    {
        _mapType = FindType("Il2CppMenace.Tactical.Map") ?? FindType("Menace.Tactical.Map");
        _missionPreviewType = FindType("Il2CppMenace.Strategy.MissionPreview") ?? FindType("Menace.Strategy.MissionPreview");
        _missionType = FindType("Il2CppMenace.Strategy.Mission") ?? FindType("Menace.Strategy.Mission");
        _terrainGeneratorType = FindType("Il2CppMenace.Tactical.Mapgen.TerrainGen.TerrainMapGenerator") ?? FindType("Menace.Tactical.Mapgen.TerrainGen.TerrainMapGenerator");
        _blueprintType = FindType("Il2CppMenace.Tactical.Mapgen.MapBlueprint") ?? FindType("Menace.Tactical.Mapgen.MapBlueprint");
        _baseTileHighlighterType = FindType("Il2CppMenace.Tactical.BaseTileHighlighter") ?? FindType("Menace.Tactical.BaseTileHighlighter");
        _mapElevationTextureType = FindType("Il2CppMenace.Tactical.MapElevationTexture") ?? FindType("Menace.Tactical.MapElevationTexture");
        _tacticalConfigType = FindType("Il2CppMenace.Tactical.TacticalConfig") ?? FindType("Menace.Tactical.TacticalConfig");
        _captureAreaType = FindType("Il2CppMenace.Tactical.Objectives.CaptureArea") ?? FindType("Menace.Tactical.Objectives.CaptureArea");
        _objectiveType = FindType("Il2CppMenace.Tactical.Objectives.Objective") ?? FindType("Menace.Tactical.Objectives.Objective");
        _tileHighlighterType = FindType("Il2CppMenace.Tactical.TileHighlighter") ?? FindType("Menace.Tactical.TileHighlighter");
        _tacticalCameraType = FindType("Il2CppMenace.Tactical.TacticalCamera") ?? FindType("Menace.Tactical.TacticalCamera");
        _missionTemplateType = FindType("Il2CppMenace.Strategy.Missions.MissionTemplate") ?? FindType("Menace.Strategy.Missions.MissionTemplate");
        _genericMissionTemplateType = FindType("Il2CppMenace.Strategy.Missions.GenericMissionTemplate") ?? FindType("Menace.Strategy.Missions.GenericMissionTemplate");
        _missionAreaConfigType = FindType("Il2CppMenace.Strategy.Missions.MissionAreaConfig") ?? FindType("Menace.Strategy.Missions.MissionAreaConfig");
        _reinforcementGroupType = FindType("Il2CppMenace.Strategy.Missions.ReinforcementGroup") ?? FindType("Menace.Strategy.Missions.ReinforcementGroup");
        _killUnitType = FindType("Il2CppMenace.Tactical.Objectives.KillUnit") ?? FindType("Menace.Tactical.Objectives.KillUnit");
        _rescueUnitType = FindType("Il2CppMenace.Tactical.Objectives.RescueUnit") ?? FindType("Menace.Tactical.Objectives.RescueUnit");
        _evacuateToAreaType = FindType("Il2CppMenace.Tactical.Objectives.EvacuateToArea") ?? FindType("Menace.Tactical.Objectives.EvacuateToArea");
        _recoverObjectType = FindType("Il2CppMenace.Tactical.Objectives.RecoverObject") ?? FindType("Menace.Tactical.Objectives.RecoverObject");
        _chunkMetaGeneratorType = FindType("Il2CppMenace.Tactical.Mapgen.ChunkMetaGenerator") ?? FindType("Menace.Tactical.Mapgen.ChunkMetaGenerator");
        _aiFactionType = FindType("Il2CppMenace.Tactical.AI.AIFaction") ?? FindType("Menace.Tactical.AI.AIFaction");
        _agentType = FindType("Il2CppMenace.Tactical.AI.Agent") ?? FindType("Menace.Tactical.AI.Agent");
        _moveBehaviorType = FindType("Il2CppMenace.Tactical.AI.Behaviors.Move") ?? FindType("Menace.Tactical.AI.Behaviors.Move");
        _skillType = FindType("Il2CppMenace.Tactical.Skills.Skill") ?? FindType("Menace.Tactical.Skills.Skill");
        _lineOfSightType = FindType("Il2CppTactical.LineOfSight") ?? FindType("Tactical.LineOfSight");
        _pathfindingProcessType = FindType("Il2CppMenace.Tactical.PathfindingProcess") ?? FindType("Menace.Tactical.PathfindingProcess");
        _battlePlanType = FindType("Il2CppMenace.Strategy.BattlePlan") ?? FindType("Menace.Strategy.BattlePlan");
        _missionPrepUiScreenType = FindType("Il2CppMenace.UI.Strategy.MissionPrepUIScreen") ?? FindType("Menace.UI.Strategy.MissionPrepUIScreen");
        _tacticalManagerType = FindType("Il2CppMenace.Tactical.TacticalManager") ?? FindType("Menace.Tactical.TacticalManager");
        _unitActorType = FindType("Il2CppMenace.Tactical.UnitActor") ?? FindType("Menace.Tactical.UnitActor");
        _tacticalStateType = FindType("Il2CppMenace.States.TacticalState") ?? FindType("Menace.States.TacticalState");
        _terrainUtilitiesType = FindType("Il2CppMenace.Tactical.Mapgen.TerrainGen.TerrainUtilities") ?? FindType("Menace.Tactical.Mapgen.TerrainGen.TerrainUtilities");
        _pointPropGeneratorType = FindType("Il2CppMenace.Tactical.Mapgen.PointPropGenerator") ?? FindType("Menace.Tactical.Mapgen.PointPropGenerator");
        _groundCoverScatterType = FindType("Il2CppMenace.Tactical.Mapgen.TerrainGen.GroundCoverScatter") ?? FindType("Menace.Tactical.Mapgen.TerrainGen.GroundCoverScatter");
        _terrainPointSpawnerType = FindType("Il2CppMenace.Tactical.Mapgen.TerrainPointSpawner") ?? FindType("Menace.Tactical.Mapgen.TerrainPointSpawner");
        _mapGenerationPassType = FindType("Il2CppMenace.Tactical.Mapgen.IMapGenerationPass") ?? FindType("Menace.Tactical.Mapgen.IMapGenerationPass");
        BuildMapPassTypeCache();
    }

    private static void InstallHooks(HarmonyLib.Harmony harmony)
    {
        try
        {
            PatchMethod(harmony, _missionPreviewType, "Generate", 1, nameof(MissionPreviewGeneratePrefix));
            PatchMissionPreviewPixelExtent(harmony);
            PatchMethod(harmony, _mapType, "Generate", 2, nameof(MapGeneratePrefix));
            PatchTerrainHoleIndexing(harmony);
            PatchTerrainGenerateStateMachine(harmony);
            PatchTerrainStampDensity(harmony);
            PatchWorldHeightBilinearSafety(harmony);
            PatchGameplayPointReprojection(harmony);
            PatchTerrainPointSpawnerHeightFix(harmony);
            PatchMapElevationTextureResize(harmony);
            PatchBaseTileHighlighterInit(harmony);
            PatchCaptureAreaMinimum(harmony);
            PatchObjectiveAiZoneScaling(harmony);
            PatchTacticalCameraZoom(harmony);
            PatchMissionAreaScaling(harmony);
            // Do not install the broad Objective.Init area-scoping hooks at any
            // map size. The 64x64 A/B test narrowed a severe AI slowdown to the
            // newer mission-area handling, even with native 64x64 pathfinding.
            // Preserve only narrowly targeted, invalid-only actor spawn safety
            // on extended maps; it cannot rewrite already-valid spawn areas.
            if (TargetSize > ZoneReferenceSize)
                PatchFinalSpawnAreaSafety(harmony);
            PatchEvacSetpiecePlacement(harmony);
            PatchMissionRoundLimits(harmony);
            PatchAiTaskLifetime(harmony);
            PatchAiPerformance(harmony);
            PatchAiDiagnostics(harmony);
            PatchAIRouteInvestigation(harmony);

            // Diagnostic-only hooks stay completely out of gameplay hot paths in normal builds.
            // Debug() is source-controlled and OFF by default.
            if (Debug())
            {
                PatchMapGenerateStateMachine(harmony);
                PatchTerrainSpawnMapStateMachine(harmony);
                PatchMapTileLifecycleDiagnostics(harmony);
                PatchSkillTargetingDiagnostics(harmony);
                PatchTacticalStartupDiagnostics(harmony);
                PatchFoliagePipelineDiagnostics(harmony);
            }
        }
        catch (Exception ex)
        {
            _log.Error($"{ModName}: hook installation failed: {ex}");
        }
    }

    private static void PatchMethod(HarmonyLib.Harmony harmony, Type type, string name, int parameterCount, string prefixName)
    {
        if (type == null) return;
        MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: could not find {type.FullName}.{name}/{parameterCount}.");
            return;
        }

        MethodInfo prefix = typeof(Plugin).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        _log.Msg($"{ModName}: hooked {FormatMethod(method)}.");
    }



    private static void PatchTacticalStartupDiagnostics(HarmonyLib.Harmony harmony)
    {
        PatchDiagnosticMethod(harmony, _missionPrepUiScreenType, "StartMission", 2,
            nameof(MissionPrepLaunchPrefix), nameof(MissionPrepLaunchPostfix), "Mission Prep launch");
        PatchDiagnosticMethod(harmony, _battlePlanType, "DeployEntities", 2,
            nameof(BattlePlanDeployEntitiesPrefix), nameof(BattlePlanDeployEntitiesPostfix), "BattlePlan deployment");
        PatchDiagnosticMethod(harmony, _unitActorType, "Create", 3,
            nameof(UnitActorCreatePrefix), nameof(UnitActorCreatePostfix), "UnitActor creation");
        PatchDiagnosticMethod(harmony, _tacticalManagerType, "OnInit", 1,
            nameof(TacticalManagerOnInitPrefix), nameof(TacticalManagerOnInitPostfix), "TacticalManager initialization");
        PatchDiagnosticMethod(harmony, _tacticalManagerType, "Start", 0,
            nameof(TacticalManagerStartPrefix), nameof(TacticalManagerStartPostfix), "TacticalManager start");
        PatchDiagnosticMethod(harmony, _tacticalStateType, "OnMapReady", 1,
            nameof(TacticalStateOnMapReadyPrefix), nameof(TacticalStateOnMapReadyPostfix), "TacticalState map-ready callback");
    }

    private static void PatchDiagnosticMethod(HarmonyLib.Harmony harmony, Type type, string name, int parameterCount,
        string prefixName, string postfixName, string label)
    {
        if (type == null)
        {
            if (Debug()) _log.Warning($"{ModName}: type missing for {label} diagnostics ({name}).");
            return;
        }

        MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: could not find {type.FullName}.{name}/{parameterCount} for {label} diagnostics.");
            return;
        }

        HarmonyMethod prefix = null;
        HarmonyMethod postfix = null;
        if (!string.IsNullOrEmpty(prefixName))
            prefix = new HarmonyMethod(typeof(Plugin).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic));
        if (!string.IsNullOrEmpty(postfixName))
            postfix = new HarmonyMethod(typeof(Plugin).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic));

        harmony.Patch(method, prefix: prefix, postfix: postfix);
        _log.Msg($"{ModName}: hooked {FormatMethod(method)} for {label} diagnostics.");
    }

    private static void MissionPrepLaunchPrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        try
        {
            object mission = InvokeNoArg(__instance, "GetMission") ?? GetMember(__instance, "m_Mission");
            object preview = InvokeNoArg(__instance, "GetMissionPreview") ?? GetMember(__instance, "m_MissionPreview");
            object selector = GetMember(__instance, "m_UnitSelector");
            object deployedUi = selector == null ? null : InvokeNoArg(selector, "GetDeployedEntities");
            _log.Msg($"{ModName}: STARTUP MissionPrep.StartMission ENTER mission={Safe(mission)} preview={Safe(preview)} uiDeployed={CollectionCount(deployedUi)} args={DescribeArgs(__args)}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: STARTUP MissionPrep.StartMission probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MissionPrepLaunchPostfix()
    {
        if (!Enabled() || !Debug()) return;
        _log.Msg($"{ModName}: STARTUP MissionPrep.StartMission RETURN.");
    }

    private static void BattlePlanDeployEntitiesPrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_deployEntitiesCalls;
        try
        {
            object entities = InvokeNoArg(__instance, "GetEntitiesToDeploy") ?? GetMember(__instance, "m_EntitiesToDeploy");
            List<object> snapshot = SnapshotCollection(entities, 128);
            _log.Msg($"{ModName}: STARTUP BattlePlan.DeployEntities ENTER call={call} entities={snapshot.Count} args={DescribeArgs(__args)}.");
            for (int i = 0; i < snapshot.Count; i++)
            {
                string desc = DescribeDeployedEntity(snapshot[i]);
                _log.Msg($"{ModName}: STARTUP DEPLOY[{i}] {desc}");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: STARTUP BattlePlan.DeployEntities prefix probe failed call={call}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void BattlePlanDeployEntitiesPostfix(object __instance)
    {
        if (!Enabled() || !Debug()) return;
        object entities = InvokeNoArg(__instance, "GetEntitiesToDeploy") ?? GetMember(__instance, "m_EntitiesToDeploy");
        _log.Msg($"{ModName}: STARTUP BattlePlan.DeployEntities RETURN call={_deployEntitiesCalls} entities={CollectionCount(entities)} unitCreateCalls={_unitActorCreateCalls}.");
    }

    private static void UnitActorCreatePrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_unitActorCreateCalls;
        object leader = (__args != null && __args.Length > 0) ? __args[0] : null;
        object tile = (__args != null && __args.Length > 1) ? __args[1] : null;
        object faction = (__args != null && __args.Length > 2) ? __args[2] : null;
        _log.Msg($"{ModName}: STARTUP UnitActor.Create ENTER call={call} leader={DescribeLeader(leader)} tile={DescribeTile(tile)} faction={Safe(faction)}.");
    }

    private static void UnitActorCreatePostfix()
    {
        if (!Enabled() || !Debug()) return;
        _log.Msg($"{ModName}: STARTUP UnitActor.Create RETURN call={_unitActorCreateCalls}.");
    }

    private static void TacticalManagerOnInitPrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_tacticalManagerOnInitCalls;
        object mission = (__args != null && __args.Length > 0) ? __args[0] : null;
        _log.Msg($"{ModName}: STARTUP TacticalManager.OnInit ENTER call={call} mission={Safe(mission)} map={DescribeCurrentMapCompact()}.");
    }

    private static void TacticalManagerOnInitPostfix(object __instance)
    {
        if (!Enabled() || !Debug()) return;
        _log.Msg($"{ModName}: STARTUP TacticalManager.OnInit RETURN call={_tacticalManagerOnInitCalls} actors={TryGetTacticalActorCount(__instance)} unitCreateCalls={_unitActorCreateCalls}.");
    }

    private static void TacticalManagerStartPrefix(object __instance)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_tacticalManagerStartCalls;
        _log.Msg($"{ModName}: STARTUP TacticalManager.Start ENTER call={call} actors={TryGetTacticalActorCount(__instance)} unitCreateCalls={_unitActorCreateCalls}.");
    }

    private static void TacticalManagerStartPostfix(object __instance)
    {
        if (!Enabled() || !Debug()) return;
        _log.Msg($"{ModName}: STARTUP TacticalManager.Start RETURN call={_tacticalManagerStartCalls} actors={TryGetTacticalActorCount(__instance)} unitCreateCalls={_unitActorCreateCalls}.");
    }

    private static void TacticalStateOnMapReadyPrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_tacticalStateOnMapReadyCalls;
        object preview = (__args != null && __args.Length > 0) ? __args[0] : null;
        _log.Msg($"{ModName}: STARTUP TacticalState.OnMapReady ENTER call={call} preview={DescribePreviewCompact(preview)} map={DescribeCurrentMapCompact()} mapGenCalls={_mapGenerateMoveNextCalls} mapGenState={FormatState(_mapGenerateLastState)} current={_mapGenerateLastCurrent} completed={_mapGenerateCompleted}.");
    }

    private static void TacticalStateOnMapReadyPostfix()
    {
        if (!Enabled() || !Debug()) return;
        _log.Msg($"{ModName}: STARTUP TacticalState.OnMapReady RETURN call={_tacticalStateOnMapReadyCalls} deployCalls={_deployEntitiesCalls} unitCreateCalls={_unitActorCreateCalls} managerStartCalls={_tacticalManagerStartCalls}.");
    }

    private static void ProbeTacticalStartup(string reason)
    {
        if (!Enabled() || !Debug()) return;
        try
        {
            object manager = null;
            if (_tacticalManagerType != null)
            {
                MethodInfo get = _tacticalManagerType.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (get != null) manager = get.Invoke(null, null);
            }
            long mapGenAge = _mapGenerateLastStepMs <= 0 ? -1 : Math.Max(0, Environment.TickCount64 - _mapGenerateLastStepMs);
            long spawnMapAge = _spawnMapLastStepMs <= 0 ? -1 : Math.Max(0, Environment.TickCount64 - _spawnMapLastStepMs);
            _log.Msg($"{ModName}: STARTUP PROBE [{reason}] manager={(manager == null ? "<null>" : "present")} actors={TryGetTacticalActorCount(manager)} deployCalls={_deployEntitiesCalls} unitCreateCalls={_unitActorCreateCalls} onInitCalls={_tacticalManagerOnInitCalls} startCalls={_tacticalManagerStartCalls} onMapReadyCalls={_tacticalStateOnMapReadyCalls} mapGenCalls={_mapGenerateMoveNextCalls} mapGenState={FormatState(_mapGenerateLastState)} mapGenCurrent={_mapGenerateLastCurrent} mapGenAge={mapGenAge}ms mapGenCompleted={_mapGenerateCompleted} spawnMapCalls={_spawnMapMoveNextCalls} spawnMapState={FormatState(_spawnMapLastState)} spawnMapCurrent={_spawnMapLastCurrent} spawnMapAge={spawnMapAge}ms spawnMapCompleted={_spawnMapCompleted}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: STARTUP PROBE [{reason}] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static int TryGetTacticalActorCount(object manager)
    {
        if (manager == null) return -1;
        try
        {
            MethodInfo method = manager.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "GetActorCount" && m.GetParameters().Length == 5);
            if (method == null) return -1;
            object result = method.Invoke(manager, new object[] { true, true, true, true, null });
            return ToInt(result, -1);
        }
        catch { return -1; }
    }

    private static string DescribeCurrentMapCompact()
    {
        if (_mapInstance == null) return "<null>";
        return $"{ToInt(GetMember(_mapInstance, "Width"), -1)}x{ToInt(GetMember(_mapInstance, "Height"), -1)}";
    }

    private static string DescribeDeployedEntity(object entity)
    {
        if (entity == null) return "<null>";
        try
        {
            object pos = InvokeNoArg(entity, "GetTilePosition") ?? GetMember(entity, "m_TilePosition");
            int x = ToInt(GetMember(pos, "x") ?? GetMember(pos, "X"), int.MinValue);
            int z = ToInt(GetMember(pos, "y") ?? GetMember(pos, "Y") ?? GetMember(pos, "z") ?? GetMember(pos, "Z"), int.MinValue);
            bool inBounds = x >= 0 && z >= 0 && x < TargetSize && z < TargetSize;
            object valid = GetMember(entity, "m_ValidTilePositionForCurrentMission");
            object set = InvokeNoArg(entity, "HasTilePositionBeenSetForCurrentMission");
            object type = InvokeNoArg(entity, "GetEntityType") ?? GetMember(entity, "m_EntityType");
            object leader = InvokeNoArg(entity, "GetUnitLeader") ?? GetMember(entity, "m_UnitLeader");
            object scale = GetMember(entity, "DeploymentZoneScale");
            return $"type={Safe(type)} pos={Safe(pos)} xy=({x},{z}) in64={inBounds} validFlag={Safe(valid)} positionSet={Safe(set)} zoneScale={Safe(scale)} leader={DescribeLeader(leader)}";
        }
        catch (Exception ex)
        {
            return $"<describe failed {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static string DescribeLeader(object leader)
    {
        if (leader == null) return "<null>";
        try
        {
            object template = InvokeNoArg(leader, "GetTemplate") ?? GetMember(leader, "m_Template");
            object id = template == null ? null : (GetMember(template, "m_ID") ?? GetMember(template, "ID"));
            object name = GetMember(leader, "Name") ?? GetMember(leader, "m_Name") ?? InvokeNoArg(leader, "GetName");
            return $"{leader.GetType().Name}(name={Safe(name)},template={Safe(id)})";
        }
        catch { return leader.GetType().Name; }
    }

    private static List<object> SnapshotCollection(object collection, int maxItems)
    {
        List<object> result = new();
        if (collection == null || maxItems <= 0) return result;

        try
        {
            if (collection is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    result.Add(item);
                    if (result.Count >= maxItems) break;
                }
                return result;
            }
        }
        catch { }

        int count = CollectionCount(collection);
        if (count <= 0) return result;
        MethodInfo getter = collection.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => (m.Name == "get_Item" || m.Name == "get_Item") && m.GetParameters().Length == 1);
        if (getter == null) return result;
        for (int i = 0; i < count && i < maxItems; i++)
        {
            try { result.Add(getter.Invoke(collection, new object[] { i })); }
            catch { break; }
        }
        return result;
    }

    private static int CollectionCount(object collection)
    {
        if (collection == null) return -1;
        if (collection is ICollection c) return c.Count;
        return ToInt(GetMember(collection, "Count") ?? GetMember(collection, "Length"), -1);
    }

    private static string DescribeArgs(object[] args)
    {
        if (args == null) return "<null>";
        try { return "[" + string.Join(", ", args.Select((a, i) => $"{i}:{Safe(a)}")) + "]"; }
        catch { return $"count={args.Length}"; }
    }

    private static void PatchMissionRoundLimits(HarmonyLib.Harmony harmony)
    {
        string[] configTypes =
        {
            "BlitzConfig",
            "BlitzPrimaryObjectivesConfig",
            "DefendAreaConfig",
            "StoryMission1Config"
        };

        int hooked = 0;
        foreach (string shortName in configTypes)
        {
            Type type = FindType($"Il2CppMenace.Tactical.Objectives.{shortName}")
                ?? FindType($"Menace.Tactical.Objectives.{shortName}");
            if (type == null)
            {
                if (Debug()) _log.Warning($"{ModName}: timed objective config {shortName} not found.");
                continue;
            }

            MethodInfo createObjective = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "CreateObjective" && m.GetParameters().Length == 0);
            if (createObjective == null)
            {
                if (Debug()) _log.Warning($"{ModName}: {type.FullName}.CreateObjective/0 not found for mission-round scaling.");
                continue;
            }

            harmony.Patch(
                createObjective,
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MissionRoundLimitConfigPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
            hooked++;
            _log.Msg($"{ModName}: hooked {FormatMethod(createObjective)} for timed-mission round scaling.");
        }

        if (Debug())
            _log.Msg($"{ModName}: timed-mission round scaling hooks installed={hooked}/{configTypes.Length}, multiplier={MissionRoundMultiplier():0.##}x.");
    }

    private static void MissionRoundLimitConfigPrefix(object __instance)
    {
        if (__instance == null) return;

        try
        {
            int current = ToInt(GetMember(__instance, "RoundLimit"), 0);
            if (current <= 0) return;

            long identity = GetStableObjectIdentity(__instance);
            if (!_originalRoundLimits.TryGetValue(identity, out int original))
            {
                original = current;
                _originalRoundLimits[identity] = original;
            }

            float multiplier = Enabled() ? MissionRoundMultiplier() : 1f;
            int target = Math.Max(1, (int)Math.Round(original * multiplier, MidpointRounding.AwayFromZero));
            if (current == target) return;

            if (!TrySetMember(__instance, "RoundLimit", target))
            {
                if (Debug()) _log.Warning($"{ModName}: could not set {__instance.GetType().Name}.RoundLimit {current}->{target}.");
                return;
            }

            _missionRoundConfigsScaled++;
            if (Debug() && _missionRoundConfigsScaled <= 40)
                _log.Msg($"{ModName}: MISSION ROUND LIMIT {__instance.GetType().Name}: {original} -> {target} ({multiplier:0.##}x).");
        }
        catch (Exception ex)
        {
            if (Debug())
                _log.Warning($"{ModName}: mission-round scaling failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static long GetStableObjectIdentity(object obj)
    {
        if (obj == null) return 0;

        try
        {
            object pointer = GetMember(obj, "Pointer");
            if (pointer is IntPtr p && p != IntPtr.Zero)
                return p.ToInt64();
            if (pointer != null)
            {
                long raw = Convert.ToInt64(pointer);
                if (raw != 0) return raw;
            }
        }
        catch { }

        unchecked
        {
            long hash = RuntimeHelpers.GetHashCode(obj);
            hash = (hash << 32) ^ (uint)(obj.GetType().FullName?.GetHashCode() ?? 0);
            return hash;
        }
    }

    private static void PatchMapTileLifecycleDiagnostics(HarmonyLib.Harmony harmony)
    {
        if (_mapType == null) return;
        MethodInfo updateBounds = _mapType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "UpdateBounds" && m.GetParameters().Length == 1);
        if (updateBounds == null)
        {
            _log.Warning($"{ModName}: Map.UpdateBounds/1 not found; tile lifecycle diagnostic unavailable.");
            return;
        }

        harmony.Patch(
            updateBounds,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MapUpdateBoundsDiagPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MapUpdateBoundsDiagPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(updateBounds)} for one-shot enlarged-map tile lifecycle diagnostics.");
    }

    private static void MapUpdateBoundsDiagPrefix(object __instance, object[] __args)
    {
        if (!Enabled()) return;
        object blueprint = Arg(__args, 0);
        int bw = ToInt(GetMember(blueprint, "Width"), -1);
        int bh = ToInt(GetMember(blueprint, "Height"), -1);
        int mw = ToInt(GetMember(__instance, "Width"), -1);
        int mh = ToInt(GetMember(__instance, "Height"), -1);
        long tiles = CountMember(__instance, "m_Tiles");
        _log.Msg($"{ModName}: TILE LIFECYCLE UpdateBounds ENTER map={mw}x{mh} tiles={tiles} inputBlueprint={bw}x{bh} mapPtr={FormatNativePointer(__instance)}.");
    }

    private static void MapUpdateBoundsDiagPostfix(object __instance, object[] __args)
    {
        if (!Enabled()) return;
        object blueprint = Arg(__args, 0);
        int bw = ToInt(GetMember(blueprint, "Width"), -1);
        int bh = ToInt(GetMember(blueprint, "Height"), -1);
        int mw = ToInt(GetMember(__instance, "Width"), -1);
        int mh = ToInt(GetMember(__instance, "Height"), -1);
        long tiles = CountMember(__instance, "m_Tiles");
        _log.Msg($"{ModName}: TILE LIFECYCLE UpdateBounds EXIT map={mw}x{mh} tiles={tiles} inputBlueprint={bw}x{bh} mapPtr={FormatNativePointer(__instance)}.");
    }

    private static void PatchSkillTargetingDiagnostics(HarmonyLib.Harmony harmony)
    {
        if (_skillType == null)
        {
            _log.Warning($"{ModName}: Skill type not found; targeting/AOE diagnostics unavailable.");
            return;
        }

        MethodInfo query = _skillType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "QueryTargetTiles" && m.GetParameters().Length == 4);
        MethodInfo aoe = _skillType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "GetAoeTiles" && m.GetParameters().Length == 4);
        MethodInfo isUsableOn = _skillType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "IsUsableOn" && m.GetParameters().Length == 3);
        MethodInfo randomAoe = _skillType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "GetRandomAoeTile" && m.GetParameters().Length == 2);

        if (query != null)
        {
            harmony.Patch(
                query,
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(QueryTargetTilesDiagPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(QueryTargetTilesDiagPostfix), BindingFlags.Static | BindingFlags.NonPublic)),
                finalizer: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(QueryTargetTilesDiagFinalizer), BindingFlags.Static | BindingFlags.NonPublic)));
            _log.Msg($"{ModName}: hooked {FormatMethod(query)} for focused target-tile failure diagnostics.");
        }
        else
        {
            _log.Warning($"{ModName}: Skill.QueryTargetTiles/4 not found; target-tile diagnostics incomplete.");
        }

        if (aoe != null)
        {
            harmony.Patch(
                aoe,
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GetAoeTilesDiagPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GetAoeTilesDiagPostfix), BindingFlags.Static | BindingFlags.NonPublic)),
                finalizer: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GetAoeTilesDiagFinalizer), BindingFlags.Static | BindingFlags.NonPublic)));
            _log.Msg($"{ModName}: hooked {FormatMethod(aoe)} for focused AOE-list failure diagnostics.");
        }
        else
        {
            _log.Warning($"{ModName}: Skill.GetAoeTiles/4 not found; AOE diagnostics incomplete.");
        }

        if (isUsableOn != null)
        {
            harmony.Patch(
                isUsableOn,
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(IsUsableOnDiagPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(IsUsableOnDiagPostfix), BindingFlags.Static | BindingFlags.NonPublic)),
                finalizer: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(IsUsableOnDiagFinalizer), BindingFlags.Static | BindingFlags.NonPublic)));
            _log.Msg($"{ModName}: hooked {FormatMethod(isUsableOn)} for focused skill usability/LoS failure diagnostics.");
        }
        else
        {
            _log.Warning($"{ModName}: Skill.IsUsableOn/3 not found; LoS caller diagnostics incomplete.");
        }

        if (randomAoe != null)
        {
            harmony.Patch(
                randomAoe,
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GetRandomAoeTileDiagPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            _log.Msg($"{ModName}: hooked {FormatMethod(randomAoe)} to identify null random-AOE tile production.");
        }
        else
        {
            _log.Warning($"{ModName}: Skill.GetRandomAoeTile/2 not found; random-AOE null-producer diagnostics unavailable.");
        }

        try
        {
            MethodInfo comparer = null;
            Type[] nestedTypes = _skillType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < nestedTypes.Length && comparer == null; i++)
            {
                MethodInfo[] methods = nestedTypes[i].GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int j = 0; j < methods.Length; j++)
                {
                    MethodInfo candidate = methods[j];
                    if (candidate.Name.Contains("GetAoeTiles", StringComparison.Ordinal) &&
                        candidate.GetParameters().Length == 2 &&
                        candidate.ReturnType == typeof(int))
                    {
                        comparer = candidate;
                        break;
                    }
                }
            }

            if (comparer != null)
            {
                harmony.Patch(
                    comparer,
                    prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GetAoeTilesComparerDiagPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                _log.Msg($"{ModName}: hooked {FormatMethod(comparer)} to identify null operands entering the native AOE sort comparer.");
            }
            else
            {
                _log.Warning($"{ModName}: GetAoeTiles sort comparer closure not found; finalizer diagnostics remain active.");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: AOE comparer diagnostic hook failed safely: {ex.GetType().Name}: {ex.Message}");
        }

        if (_lineOfSightType != null)
        {
            MethodInfo los = _lineOfSightType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "HasLineOfSight" && m.GetParameters().Length == 3);
            if (los != null)
            {
                harmony.Patch(
                    los,
                    prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(LineOfSightDiagPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                    postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(LineOfSightDiagPostfix), BindingFlags.Static | BindingFlags.NonPublic)),
                    finalizer: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(LineOfSightDiagFinalizer), BindingFlags.Static | BindingFlags.NonPublic)));
                _log.Msg($"{ModName}: hooked {FormatMethod(los)} for exception-only/slow LineOfSight diagnostics.");
            }
            else
            {
                _log.Warning($"{ModName}: Tactical.LineOfSight.HasLineOfSight/3 not found; direct LoS diagnostics unavailable.");
            }
        }
        else
        {
            _log.Warning($"{ModName}: Tactical.LineOfSight type not found; direct LoS diagnostics unavailable.");
        }
    }

    private static void QueryTargetTilesDiagPrefix(out SkillTargetDiagState __state)
    {
        __state = NewSkillDiagState();
    }

    private static void QueryTargetTilesDiagPostfix(object __instance, object[] __args, SkillTargetDiagState __state)
    {
        if (!Enabled() || __state == null) return;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state.StartMs);
        if (elapsed < 1000) return;

        object origin = Arg(__args, 0);
        object target = Arg(__args, 1);
        object into = Arg(__args, 2);
        object mode = Arg(__args, 3);
        _log.Warning($"{ModName}: TARGET TILE SLOW serial={__state.Serial} elapsed={elapsed}ms {DescribeSkillTargeting(__instance, origin, target, mode)} into={DescribeTileCollection(into)}.");
    }

    private static Exception QueryTargetTilesDiagFinalizer(object __instance, object[] __args, SkillTargetDiagState __state, Exception __exception)
    {
        if (!Enabled() || __exception == null) return __exception;
        try
        {
            long elapsed = __state == null ? -1 : Math.Max(0, Environment.TickCount64 - __state.StartMs);
            int serial = __state?.Serial ?? -1;
            object origin = Arg(__args, 0);
            object target = Arg(__args, 1);
            object into = Arg(__args, 2);
            object mode = Arg(__args, 3);
            _log.Error($"{ModName}: TARGET TILE EXCEPTION serial={serial} elapsed={elapsed}ms exception={__exception.GetType().FullName}: {__exception.Message} {DescribeSkillTargeting(__instance, origin, target, mode)} into={DescribeTileCollection(into)}.");
        }
        catch { }
        return __exception;
    }

    private static void GetAoeTilesDiagPrefix(object __instance, object[] __args, out SkillTargetDiagState __state)
    {
        __state = NewSkillDiagState();
        __state.Skill = __instance;
        __state.Origin = Arg(__args, 0);
        __state.Target = Arg(__args, 1);
        __state.Into = Arg(__args, 2);
        __state.PreList = DescribeTileCollection(__state.Into);
        __state.PreviousAoeContext = _activeAoeDiagState;
        __state.AoeContextInstalled = true;
        _activeAoeDiagState = __state;
    }

    private static void GetAoeTilesDiagPostfix(object __instance, object[] __args, SkillTargetDiagState __state)
    {
        try
        {
            if (!Enabled() || __state == null) return;
            long elapsed = Math.Max(0, Environment.TickCount64 - __state.StartMs);
            if (elapsed >= 1000)
            {
                object origin = Arg(__args, 0);
                object target = Arg(__args, 1);
                object into = Arg(__args, 2);
                object skip = Arg(__args, 3);
                _log.Warning($"{ModName}: AOE TILE SLOW serial={__state.Serial} elapsed={elapsed}ms {DescribeSkillTargeting(__instance, origin, target, $"skipEmpty={Safe(skip)}")} pre=[{__state.PreList}] post=[{DescribeTileCollection(into)}].");
            }
        }
        finally
        {
            RestoreAoeDiagContext(__state);
        }
    }

    private static Exception GetAoeTilesDiagFinalizer(object __instance, object[] __args, SkillTargetDiagState __state, Exception __exception)
    {
        try
        {
            if (Enabled() && __exception != null)
            {
                long elapsed = __state == null ? -1 : Math.Max(0, Environment.TickCount64 - __state.StartMs);
                int serial = __state?.Serial ?? -1;
                object origin = Arg(__args, 0);
                object target = Arg(__args, 1);
                object into = Arg(__args, 2);
                object skip = Arg(__args, 3);
                _log.Error($"{ModName}: AOE TILE EXCEPTION serial={serial} elapsed={elapsed}ms exception={__exception.GetType().FullName}: {__exception.Message} {DescribeSkillTargeting(__instance, origin, target, $"skipEmpty={Safe(skip)}")} pre=[{__state?.PreList ?? "<unknown>"}] post=[{DescribeTileCollection(into)}].");
            }
        }
        catch { }
        finally
        {
            RestoreAoeDiagContext(__state);
        }
        return __exception;
    }

    private static void RestoreAoeDiagContext(SkillTargetDiagState state)
    {
        if (state == null || !state.AoeContextInstalled || state.AoeContextRestored) return;
        state.AoeContextRestored = true;
        if (ReferenceEquals(_activeAoeDiagState, state))
            _activeAoeDiagState = state.PreviousAoeContext;
    }

    private static void GetAoeTilesComparerDiagPrefix(object __instance, object[] __args)
    {
        if (!Enabled()) return;
        object a = Arg(__args, 0);
        object b = Arg(__args, 1);
        if (!IsNullOrDeadNativeObject(a) && !IsNullOrDeadNativeObject(b)) return;

        int hit = System.Threading.Interlocked.Increment(ref _aoeNullCompareHits);
        if (!ShouldLogDiagnosticHit(hit)) return;

        object target = GetMember(__instance, "_target");
        SkillTargetDiagState ctx = _activeAoeDiagState;
        string skill = ctx == null ? "<no-active-GetAoeTiles-context>" : DescribeSkillTargeting(ctx.Skill, ctx.Origin, ctx.Target, "GetAoeTiles");
        string pre = ctx?.PreList ?? "<unknown>";
        string current = ctx == null ? "<unknown>" : DescribeTileCollection(ctx.Into);
        _log.Error($"{ModName}: AOE SORT NULL hit={hit} a={DescribeTileDetailed(a)} b={DescribeTileDetailed(b)} capturedTarget={DescribeTileDetailed(target)} capturedTargetRefetch={DescribeTileRefetch(target)} context={skill} pre=[{pre}] current=[{current}] binding={DescribeTacticalBinding()}.");
    }

    private static void GetRandomAoeTileDiagPostfix(object __instance, object[] __args, object __result)
    {
        if (!Enabled() || !IsNullOrDeadNativeObject(__result)) return;
        int hit = System.Threading.Interlocked.Increment(ref _aoeRandomNullHits);
        if (!ShouldLogDiagnosticHit(hit)) return;

        object origin = Arg(__args, 0);
        object target = Arg(__args, 1);
        _log.Warning($"{ModName}: AOE RANDOM NULL hit={hit} {DescribeSkillTargeting(__instance, origin, target, "GetRandomAoeTile")} originRefetch={DescribeTileRefetch(origin)} targetRefetch={DescribeTileRefetch(target)} binding={DescribeTacticalBinding()}.");
    }

    private static void IsUsableOnDiagPrefix(out long __state)
    {
        __state = Environment.TickCount64;
    }

    private static void IsUsableOnDiagPostfix(object __instance, object[] __args, long __state)
    {
        if (!Enabled()) return;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state);
        if (elapsed < 1000) return;

        int serial = System.Threading.Interlocked.Increment(ref _skillTargetDiagSerial);
        object target = Arg(__args, 0);
        object user = Arg(__args, 1);
        object usage = Arg(__args, 2);
        _log.Warning($"{ModName}: ISUSABLE SLOW serial={serial} elapsed={elapsed}ms {DescribeSkillTargeting(__instance, user, target, $"params={Safe(usage)}")} userRefetch={DescribeTileRefetch(user)} targetRefetch={DescribeTileRefetch(target)} binding={DescribeTacticalBinding()}.");
    }

    private static Exception IsUsableOnDiagFinalizer(object __instance, object[] __args, long __state, Exception __exception)
    {
        if (!Enabled() || __exception == null) return __exception;
        try
        {
            int hit = System.Threading.Interlocked.Increment(ref _isUsableExceptionHits);
            if (ShouldLogDiagnosticHit(hit))
            {
                long elapsed = Math.Max(0, Environment.TickCount64 - __state);
                object target = Arg(__args, 0);
                object user = Arg(__args, 1);
                object usage = Arg(__args, 2);
                _log.Error($"{ModName}: ISUSABLE EXCEPTION hit={hit} elapsed={elapsed}ms exception={__exception.GetType().FullName}: {__exception.Message} {DescribeSkillTargeting(__instance, user, target, $"params={Safe(usage)}")} userNull={IsNullOrDeadNativeObject(user)} targetNull={IsNullOrDeadNativeObject(target)} userRefetch={DescribeTileRefetch(user)} targetRefetch={DescribeTileRefetch(target)} binding={DescribeTacticalBinding()}.");
            }
        }
        catch { }
        return __exception;
    }

    private static void LineOfSightDiagPrefix(out long __state)
    {
        __state = Environment.TickCount64;
    }

    private static void LineOfSightDiagPostfix(object[] __args, long __state)
    {
        if (!Enabled()) return;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state);
        if (elapsed < 1000) return;

        int serial = System.Threading.Interlocked.Increment(ref _skillTargetDiagSerial);
        object origin = Arg(__args, 0);
        object dest = Arg(__args, 1);
        object flags = Arg(__args, 2);
        _log.Warning($"{ModName}: LOS SLOW serial={serial} elapsed={elapsed}ms {DescribeLineOfSightContext(origin, dest, flags)}.");
    }

    private static Exception LineOfSightDiagFinalizer(object[] __args, long __state, Exception __exception)
    {
        if (!Enabled() || __exception == null) return __exception;
        try
        {
            int hit = System.Threading.Interlocked.Increment(ref _lineOfSightExceptionHits);
            if (ShouldLogDiagnosticHit(hit))
            {
                long elapsed = Math.Max(0, Environment.TickCount64 - __state);
                object origin = Arg(__args, 0);
                object dest = Arg(__args, 1);
                object flags = Arg(__args, 2);
                _log.Error($"{ModName}: LOS EXCEPTION hit={hit} elapsed={elapsed}ms exception={__exception.GetType().FullName}: {__exception.Message} {DescribeLineOfSightContext(origin, dest, flags)}.");
            }
        }
        catch { }
        return __exception;
    }

    private static SkillTargetDiagState NewSkillDiagState()
    {
        return new SkillTargetDiagState
        {
            StartMs = Environment.TickCount64,
            Serial = System.Threading.Interlocked.Increment(ref _skillTargetDiagSerial)
        };
    }

    private static object Arg(object[] args, int index)
    {
        return args != null && index >= 0 && index < args.Length ? args[index] : null;
    }

    private static bool ShouldLogDiagnosticHit(int hit)
    {
        return hit <= 32 || hit % 100 == 0;
    }

    private static string DescribeSkillTargeting(object skill, object origin, object target, object mode)
    {
        object actor = InvokeNoArg(skill, "GetActor");
        object id = InvokeNoArg(skill, "GetID");
        object title = InvokeNoArg(skill, "GetTitle");
        return $"skill={Safe(id)} title={Safe(title)} {DescribeSkillTemplate(skill)} {DescribeActor(actor)} origin={DescribeTileDetailed(origin)} target={DescribeTileDetailed(target)} mode={Safe(mode)} map={DescribeMapDimensions()}";
    }

    private static string DescribeSkillTemplate(object skill)
    {
        if (skill == null) return "template=<null-skill>";
        try
        {
            object template = InvokeNoArg(skill, "GetTemplate") ?? GetMember(skill, "m_Template");
            if (template == null) return "template=<null>";
            object id = GetMember(template, "m_ID") ?? GetMember(template, "ID");
            object lof = GetMember(template, "IsLineOfFireNeeded");
            object aoeType = GetMember(template, "AoEType");
            object custom = GetMember(template, "UseCustomAoEShape");
            return $"template={Safe(id)} LoF={Safe(lof)} AoEType={Safe(aoeType)} customAoE={Safe(custom)}";
        }
        catch
        {
            return "template=<unreadable>";
        }
    }

    private static string DescribeLineOfSightContext(object origin, object dest, object flags)
    {
        object manager = ResolveTacticalManagerForDiagnostics();
        object activeActor = manager == null ? null : (InvokeNoArg(manager, "GetActiveActor") ?? GetMember(manager, "m_ActiveActor"));
        return $"originNull={IsNullOrDeadNativeObject(origin)} destNull={IsNullOrDeadNativeObject(dest)} origin={DescribeTileDetailed(origin)} dest={DescribeTileDetailed(dest)} originRefetch={DescribeTileRefetch(origin)} destRefetch={DescribeTileRefetch(dest)} flags={Safe(flags)} active={DescribeActor(activeActor)} map={DescribeMapDimensions()} binding={DescribeTacticalBinding()}";
    }

    private static string DescribeTileDetailed(object tile)
    {
        if (tile == null) return "<null>";
        bool hasPointer = TryGetNativePointer(tile, out long ptr);
        int x = ToInt(InvokeNoArg(tile, "GetX") ?? GetMember(tile, "m_X"), -1);
        int z = ToInt(InvokeNoArg(tile, "GetZ") ?? GetMember(tile, "m_Z"), -1);
        string ptrText = hasPointer ? $"0x{ptr:X}" : "?";
        string ptrState = hasPointer && ptr == 0 ? ",DEADPTR" : string.Empty;
        string range = x < 0 || z < 0 || x >= TargetSize || z >= TargetSize ? "OUTSIDE_MAP" : (x >= VanillaSize || z >= VanillaSize ? "BEYOND42" : "vanilla42");
        return $"({x},{z},ptr={ptrText},{range}{ptrState})";
    }

    private static string DescribeTileCollection(object collection)
    {
        if (collection == null) return "<null-list>";
        int declaredCount = CollectionCount(collection);
        List<object> items = SnapshotCollection(collection, 4096);
        List<string> nulls = new();
        List<string> deadPtrs = new();
        List<string> beyond42 = new();
        List<string> outsideMap = new();
        List<string> nullNeighbors = new();

        for (int i = 0; i < items.Count; i++)
        {
            object tile = items[i];
            if (tile == null)
            {
                nulls.Add(i.ToString());
                AppendTileNeighbors(items, i, nullNeighbors);
                continue;
            }

            if (TryGetNativePointer(tile, out long ptr) && ptr == 0)
            {
                deadPtrs.Add(i.ToString());
                AppendTileNeighbors(items, i, nullNeighbors);
            }

            int x = ToInt(InvokeNoArg(tile, "GetX") ?? GetMember(tile, "m_X"), -1);
            int z = ToInt(InvokeNoArg(tile, "GetZ") ?? GetMember(tile, "m_Z"), -1);
            if (x < 0 || z < 0 || x >= TargetSize || z >= TargetSize)
                outsideMap.Add($"{i}:{DescribeTileDetailed(tile)}");
            else if (x >= VanillaSize || z >= VanillaSize)
                beyond42.Add($"{i}:{DescribeTileDetailed(tile)}");
        }

        string nullText = nulls.Count == 0 ? "none" : string.Join(",", nulls.Take(128));
        string deadText = deadPtrs.Count == 0 ? "none" : string.Join(",", deadPtrs.Take(128));
        string neighborText = nullNeighbors.Count == 0 ? "none" : string.Join(";", nullNeighbors.Take(64));
        string beyondText = beyond42.Count == 0 ? "none" : string.Join(",", beyond42.Take(128));
        string outsideText = outsideMap.Count == 0 ? "none" : string.Join(",", outsideMap.Take(128));
        return $"count={declaredCount} snap={items.Count} nullCount={nulls.Count} deadPtrCount={deadPtrs.Count} nullIdx=[{nullText}] deadPtrIdx=[{deadText}] nullNeighbors=[{neighborText}] beyond42=[{beyondText}] outsideMap=[{outsideText}]";
    }

    private static void AppendTileNeighbors(List<object> items, int center, List<string> output)
    {
        if (items == null || output == null || output.Count >= 64) return;
        int from = Math.Max(0, center - 2);
        int to = Math.Min(items.Count - 1, center + 2);
        List<string> one = new();
        for (int i = from; i <= to; i++)
            one.Add($"{i}:{DescribeTileDetailed(items[i])}");
        output.Add($"around[{center}]={{" + string.Join(",", one) + "}");
    }

    private static string DescribeMapDimensions()
    {
        object manager = ResolveTacticalManagerForDiagnostics();
        object map = ResolveRuntimeMapForDiagnostics(manager);
        if (map == null) return "map=<null>";
        int width = ToInt(GetMember(map, "Width"), -1);
        int height = ToInt(GetMember(map, "Height"), -1);
        object bp = GetMember(map, "Blueprint") ?? InvokeNoArg(map, "get_Blueprint");
        int bw = ToInt(GetMember(bp, "Width"), -1);
        int bh = ToInt(GetMember(bp, "Height"), -1);
        return $"Map={width}x{height},Blueprint={bw}x{bh},mapPtr={FormatNativePointer(map)}";
    }

    private static string DescribeTileRefetch(object tile)
    {
        if (tile == null) return "<null-tile>";
        try
        {
            bool hasPtr = TryGetNativePointer(tile, out long originalPtr);
            int x = ToInt(InvokeNoArg(tile, "GetX") ?? GetMember(tile, "m_X"), int.MinValue);
            int z = ToInt(InvokeNoArg(tile, "GetZ") ?? GetMember(tile, "m_Z"), int.MinValue);
            object manager = ResolveTacticalManagerForDiagnostics();
            object map = ResolveRuntimeMapForDiagnostics(manager);
            if (map == null) return $"coords=({x},{z}) map=<null> originalPtr={(hasPtr ? $"0x{originalPtr:X}" : "?")}";
            int width = ToInt(GetMember(map, "Width"), -1);
            int height = ToInt(GetMember(map, "Height"), -1);
            if (x == int.MinValue || z == int.MinValue) return $"coords=<unreadable> map={width}x{height} originalPtr={(hasPtr ? $"0x{originalPtr:X}" : "?")}";
            if (x < 0 || z < 0 || x >= width || z >= height) return $"coords=({x},{z}) map={width}x{height} OUTSIDE_RUNTIME_MAP originalPtr={(hasPtr ? $"0x{originalPtr:X}" : "?")}";

            MethodInfo getter = map.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "GetTile" && m.GetParameters().Length == 2);
            if (getter == null) return $"coords=({x},{z}) GetTile/2=<missing> map={width}x{height}";
            object fetched = getter.Invoke(map, new object[] { x, z });
            bool fetchedHasPtr = TryGetNativePointer(fetched, out long fetchedPtr);
            bool samePtr = hasPtr && fetchedHasPtr && originalPtr != 0 && originalPtr == fetchedPtr;
            bool sameWrapper = ReferenceEquals(tile, fetched);
            return $"coords=({x},{z}) originalPtr={(hasPtr ? $"0x{originalPtr:X}" : "?")} fetched={DescribeTileDetailed(fetched)} samePtr={samePtr} sameWrapper={sameWrapper}";
        }
        catch (Exception ex)
        {
            return $"refetch-error={ex.GetType().Name}:{ex.Message}";
        }
    }

    private static object ResolveTacticalManagerForDiagnostics()
    {
        if (_tacticalManagerType == null) return null;
        return InvokeStaticNoArg(_tacticalManagerType, "Get") ?? GetStaticMember(_tacticalManagerType, "s_Singleton");
    }

    private static object ResolveRuntimeMapForDiagnostics(object manager)
    {
        return InvokeNoArg(manager, "GetMap") ?? GetMember(manager, "m_Map") ?? _mapInstance;
    }

    private static string DescribeTacticalBinding()
    {
        try
        {
            object manager = ResolveTacticalManagerForDiagnostics();
            object map = ResolveRuntimeMapForDiagnostics(manager);
            object active = manager == null ? null : (InvokeNoArg(manager, "GetActiveActor") ?? GetMember(manager, "m_ActiveActor"));
            bool sameTrackedMap = map != null && _mapInstance != null && NativePointersEqual(map, _mapInstance);
            return $"managerPtr={FormatNativePointer(manager)} managerMapPtr={FormatNativePointer(map)} trackedMapPtr={FormatNativePointer(_mapInstance)} sameTrackedMap={sameTrackedMap} activeActorPtr={FormatNativePointer(active)}";
        }
        catch (Exception ex)
        {
            return $"binding-error={ex.GetType().Name}:{ex.Message}";
        }
    }

    private static bool NativePointersEqual(object a, object b)
    {
        if (a == null || b == null) return false;
        if (TryGetNativePointer(a, out long pa) && TryGetNativePointer(b, out long pb) && pa != 0 && pb != 0)
            return pa == pb;
        return ReferenceEquals(a, b);
    }

    private static bool IsNullOrDeadNativeObject(object obj)
    {
        if (obj == null) return true;
        return TryGetNativePointer(obj, out long ptr) && ptr == 0;
    }

    private static string FormatNativePointer(object obj)
    {
        if (obj == null) return "<null>";
        return TryGetNativePointer(obj, out long ptr) ? $"0x{ptr:X}" : "?";
    }

    private static bool TryGetNativePointer(object obj, out long pointer)
    {
        pointer = 0;
        if (obj == null) return false;
        try
        {
            object raw = GetMember(obj, "Pointer");
            if (raw is IntPtr ip)
            {
                pointer = ip.ToInt64();
                return true;
            }
            if (raw != null)
            {
                pointer = Convert.ToInt64(raw);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static object InvokeStaticNoArg(Type type, string methodName)
    {
        if (type == null || string.IsNullOrEmpty(methodName)) return null;
        try
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
            return method?.Invoke(null, null);
        }
        catch { return null; }
    }

    private static object GetStaticMember(Type type, string name)
    {
        if (type == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            PropertyInfo p = type.GetProperty(name, flags);
            if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                return p.GetValue(null);
        }
        catch { }
        try
        {
            FieldInfo f = type.GetField(name, flags);
            if (f != null) return f.GetValue(null);
        }
        catch { }
        return null;
    }

    // Vanilla AIFaction.RunWithTimeout launches a Task.Run wrapper. That wrapper starts
    // a second Task for the actual AI action, calls Wait(12000), and RETURNS after the
    // timeout even while the inner task is still running. Subsequent AI passes can then
    // mutate the same Agent tile-score dictionaries concurrently. The native body and
    // its 12,000 ms constant were verified against the supplied game binary and dump.
    //
    // Return the *actual* worker task to vanilla instead. This retains the native
    // asynchronous AI scheduling/completion logic, prevents detached work after the
    // 12-second wrapper finishes, and removes one unnecessary scheduled wrapper task.
    // This does NOT introduce arbitrary timeouts or alter AI search/behavior scoring.
    private static void PatchAiTaskLifetime(HarmonyLib.Harmony harmony)
    {
        if (_aiFactionType == null)
        {
            _log.Error($"{ModName}: AI task safety could not be installed: AIFaction type missing.");
            return;
        }

        MethodInfo method = _aiFactionType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "RunWithTimeout"
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(Il2CppSystem.Action)
                && m.ReturnType == typeof(Il2CppSystem.Threading.Tasks.Task));

        if (method == null)
        {
            _log.Error($"{ModName}: AI task safety could not be installed: expected AIFaction.RunWithTimeout(Action, Actor) signature missing. The native timeout behavior is unchanged.");
            return;
        }

        MethodInfo prefix = typeof(Plugin).GetMethod(
            nameof(AIFactionRunWithTimeoutPrefix), BindingFlags.Static | BindingFlags.NonPublic);
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        _log.Msg($"{ModName}: AI task lifetime safety active (no detached work after vanilla's 12s timeout).");
    }

    private static bool AIFactionRunWithTimeoutPrefix(
        Il2CppSystem.Action __0, ref Il2CppSystem.Threading.Tasks.Task __result)
    {
        if (!Enabled() || __0 == null) return true;

        // Returning Task.Run(action) itself means the caller only observes completion
        // once the actual AI evaluation has finished. Do not detach another inner task,
        // skip actors, suppress exceptions, or spin/wait on the main game thread.
        __result = Il2CppSystem.Threading.Tasks.Task.Run(__0);
        return false;
    }

    private static void PatchAiDiagnostics(HarmonyLib.Harmony harmony)
    {
        // Keep the old broad AI/pathfinding probes completely out of the hot path unless
        // explicitly enabled in source. The current investigation is focused on Tile/LoS/targeting.
        if (!AIDiagnostics()) return;

        if (_aiFactionType == null)
        {
            _log.Warning($"{ModName}: AIFaction type not found; AI stall diagnostics unavailable.");
            return;
        }

        PatchAiMethod(harmony, _aiFactionType, "Process", 0, null, nameof(AIFactionProcessPostfix), "AI state heartbeat");
        PatchAiMethod(harmony, _aiFactionType, "Pick", 1, nameof(AIFactionPickPrefix), nameof(AIFactionPickPostfix), "AI actor pick");
        PatchAiMethod(harmony, _aiFactionType, "Finish", 0, nameof(AIFactionFinishPrefix), null, "AI faction finish");
        PatchAiMethod(harmony, _aiFactionType, "Think", 0, nameof(AIFactionThinkPrefix), nameof(AIFactionThinkPostfix), "AI think");
        PatchAiMethod(harmony, _aiFactionType, "Execute", 0, nameof(AIFactionExecutePrefix), nameof(AIFactionExecutePostfix), "AI execute");

        if (_pathfindingProcessType == null)
        {
            _log.Warning($"{ModName}: PathfindingProcess type not found; pathfinding diagnostics unavailable.");
            return;
        }

        PatchPathMethod(harmony, "FindPath");
        PatchPathMethod(harmony, "FindPathForwardOrBackward");
    }

    // AI route investigation is OFF by default and is installed only when the user
    // enables its dedicated setting before launching the game. No AI behavior is
    // modified: we observe the native Agent's existing operational zones and the
    // Move behavior's already-selected target. No pathfinding calculation is added.
    private static void PatchAIRouteInvestigation(HarmonyLib.Harmony harmony)
    {
        if (!ReadBool("DiagnoseAIRoutes", false)) return;

        TryInstallRouteProbe(harmony, _agentType, "OnTurnStart", 0,
            nameof(AIRouteAgentTurnPostfix), false, "agent turn");
        TryInstallRouteProbe(harmony, _moveBehaviorType, "OnExecute", 1,
            nameof(AIRouteMoveExecutionPrefix), true, "movement decision");
        _log.Msg($"{ModName}: AI route diagnostics enabled for this session (limited to 120 agent and 200 movement entries).");
    }

    private static void TryInstallRouteProbe(HarmonyLib.Harmony harmony, Type type,
        string methodName, int paramCount, string callbackName, bool prefix, string label)
    {
        try
        {
            MethodInfo method = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == paramCount);
            MethodInfo callback = typeof(Plugin).GetMethod(callbackName, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null || callback == null)
            {
                _log.Warning($"{ModName}: AI route diagnostic unavailable for {label}: method not found.");
                return;
            }
            harmony.Patch(method, prefix: prefix ? new HarmonyMethod(callback) : null,
                postfix: prefix ? null : new HarmonyMethod(callback));
        }
        catch (Exception e)
        {
            _log.Warning($"{ModName}: AI route diagnostic unavailable for {label}: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void AIRouteAgentTurnPostfix(object __instance)
    {
        if (!ReadBool("DiagnoseAIRoutes", false) || __instance == null) return;
        if (System.Threading.Interlocked.Increment(ref _routeAgentLines) > 120) return;
        try
        {
            object actor = InvokeNoArg(__instance, "GetActor") ?? GetMember(__instance, "m_Actor");
            object role = InvokeNoArg(__instance, "GetRole");
            object zones = InvokeNoArg(__instance, "GetZones");
            int count = GetCollectionCount(zones);
            var snippets = new List<string>();
            foreach (object zone in SnapshotCollection(zones, 4))
            {
                object area = GetMember(zone, "Area");
                int x = ToInt(GetMember(area, "m_XMin") ?? GetMember(area, "xMin"), -1);
                int z = ToInt(GetMember(area, "m_YMin") ?? GetMember(area, "yMin"), -1);
                int w = ToInt(GetMember(area, "m_Width") ?? GetMember(area, "width"), -1);
                int h = ToInt(GetMember(area, "m_Height") ?? GetMember(area, "height"), -1);
                object order = GetMember(zone, "Order");
                object priority = GetMember(zone, "Priority");
                snippets.Add($"{Safe(order)}:({x},{z},{w},{h}) p={Safe(priority)}");
            }
            object tile = InvokeNoArg(actor, "GetTile");
            object template = InvokeNoArg(actor, "GetTemplate");
            object templateId = GetMember(template, "m_ID") ?? GetMember(template, "ID");
            _log.Msg($"{ModName}: AI ROUTE AGENT id={GetStableObjectKey(actor):X} template={Safe(templateId)} tile={DescribeTile(tile)} " +
                $"sleeping={Safe(InvokeNoArg(__instance, "IsSleeping"))} zones={count} [{string.Join(";", snippets)}] " +
                $"zoneWeight={Safe(GetMember(role, "ZoneImportanceWeight"))} distanceWeight={Safe(GetMember(role, "DistanceScale"))}.");
        }
        catch (Exception ex)
        {
            if (_routeAgentLines < 6) _log.Warning($"{ModName}: AI route agent diagnostic failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AIRouteMoveExecutionPrefix(object __instance, object[] __args)
    {
        if (!ReadBool("DiagnoseAIRoutes", false) || __instance == null) return;
        if (System.Threading.Interlocked.Increment(ref _routeMoveLines) > 200) return;
        try
        {
            object actor = __args != null && __args.Length > 0 ? __args[0] : null;
            object chosen = GetMember(__instance, "m_TargetTile");
            object destination = GetMember(chosen, "Tile");
            object ultimate = GetMember(chosen, "UltimateTile");
            object path = GetMember(__instance, "m_Path");
            object candidates = GetMember(__instance, "m_Destinations");
            _log.Msg($"{ModName}: AI ROUTE MOVE id={GetStableObjectKey(actor):X} " +
                $"from={DescribeTile(InvokeNoArg(actor, "GetTile"))} target={DescribeTile(destination)} " +
                $"ultimate={DescribeTile(ultimate)} pathNodes={GetCollectionCount(path)} candidates={GetCollectionCount(candidates)}.");
        }
        catch (Exception ex)
        {
            if (_routeMoveLines < 6) _log.Warning($"{ModName}: AI route movement diagnostic failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PatchAiMethod(HarmonyLib.Harmony harmony, Type type, string name, int parameterCount, string prefixName, string postfixName, string label)
    {
        MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: could not find {type.FullName}.{name}/{parameterCount} for {label} diagnostics.");
            return;
        }

        HarmonyMethod prefix = null;
        HarmonyMethod postfix = null;
        if (!string.IsNullOrEmpty(prefixName))
            prefix = new HarmonyMethod(typeof(Plugin).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic));
        if (!string.IsNullOrEmpty(postfixName))
            postfix = new HarmonyMethod(typeof(Plugin).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic));

        harmony.Patch(method, prefix: prefix, postfix: postfix);
        _log.Msg($"{ModName}: hooked {FormatMethod(method)} for {label} diagnostics.");
    }

    private static void PatchPathMethod(HarmonyLib.Harmony harmony, string name)
    {
        MethodInfo method = _pathfindingProcessType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: PathfindingProcess.{name} not found.");
            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(PathfindingPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(PathfindingPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(method)} for slow/boundary pathfinding diagnostics.");
    }

    private static void AIFactionProcessPostfix(object __instance)
    {
        if (!Enabled() || !AIDiagnostics() || __instance == null) return;
        try
        {
            int key = RuntimeHelpers.GetHashCode(__instance);
            long now = Environment.TickCount64;
            string state = Safe(GetMember(__instance, "m_CurrentState"));
            int pickable = ToInt(GetMember(__instance, "m_PickableActorsCount"), -1);
            float aiTime = ToFloat(GetMember(__instance, "m_Time"), -1f);
            float beginPick = ToFloat(GetMember(__instance, "m_BeginPickTime"), -1f);

            if (!_aiDiagStates.TryGetValue(key, out AiDiagState diag))
            {
                diag = new AiDiagState { LastState = state, LastStateChangeMs = now, LastHeartbeatMs = now };
                _aiDiagStates[key] = diag;
                _log.Msg($"{ModName}: AI STATE faction=0x{key:X8} state={state} pickable={pickable} time={aiTime:0.###} beginPick={beginPick:0.###}.");
                return;
            }

            if (!string.Equals(diag.LastState, state, StringComparison.Ordinal))
            {
                long held = Math.Max(0, now - diag.LastStateChangeMs);
                _log.Msg($"{ModName}: AI STATE faction=0x{key:X8} {diag.LastState} -> {state} after={held}ms pickable={pickable} time={aiTime:0.###} beginPick={beginPick:0.###}.");
                diag.LastState = state;
                diag.LastStateChangeMs = now;
                diag.LastHeartbeatMs = now;
            }
            else if (now - diag.LastHeartbeatMs >= 2000)
            {
                long held = Math.Max(0, now - diag.LastStateChangeMs);
                _log.Msg($"{ModName}: AI HEARTBEAT faction=0x{key:X8} state={state} held={held}ms pickable={pickable} time={aiTime:0.###} beginPick={beginPick:0.###}.");
                diag.LastHeartbeatMs = now;
            }
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: AI Process diagnostic failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AIFactionPickPrefix(object[] __args, out long __state)
    {
        __state = Environment.TickCount64;
        if (!Enabled() || !AIDiagnostics()) return;
        object actor = __args != null && __args.Length > 0 ? __args[0] : null;
        int call = ++_aiPickCalls;
        _log.Msg($"{ModName}: AI PICK START #{call} {DescribeActor(actor)}.");
    }

    private static void AIFactionPickPostfix(object[] __args, long __state)
    {
        if (!Enabled() || !AIDiagnostics()) return;
        object actor = __args != null && __args.Length > 0 ? __args[0] : null;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state);
        _log.Msg($"{ModName}: AI PICK RETURN elapsed={elapsed}ms {DescribeActor(actor)}.");
    }

    private static void AIFactionFinishPrefix(object __instance)
    {
        if (!Enabled() || !AIDiagnostics()) return;
        int key = __instance == null ? 0 : RuntimeHelpers.GetHashCode(__instance);
        _log.Msg($"{ModName}: AI FINISH called faction=0x{key:X8} state={Safe(GetMember(__instance, "m_CurrentState"))} pickable={ToInt(GetMember(__instance, "m_PickableActorsCount"), -1)}.");
    }

    private static void AIFactionThinkPrefix(object __instance, out long __state)
    {
        __state = Environment.TickCount64;
        if (!Enabled() || !AIDiagnostics()) return;
        int key = __instance == null ? 0 : RuntimeHelpers.GetHashCode(__instance);
        _log.Msg($"{ModName}: AI THINK START faction=0x{key:X8} state={Safe(GetMember(__instance, "m_CurrentState"))}.");
    }

    private static void AIFactionThinkPostfix(object __instance, long __state)
    {
        if (!Enabled() || !AIDiagnostics()) return;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state);
        int key = __instance == null ? 0 : RuntimeHelpers.GetHashCode(__instance);
        _log.Msg($"{ModName}: AI THINK RETURN faction=0x{key:X8} elapsed={elapsed}ms state={Safe(GetMember(__instance, "m_CurrentState"))}.");
    }

    private static void AIFactionExecutePrefix(object __instance, out long __state)
    {
        __state = Environment.TickCount64;
        if (!Enabled() || !AIDiagnostics()) return;
        int key = __instance == null ? 0 : RuntimeHelpers.GetHashCode(__instance);
        _log.Msg($"{ModName}: AI EXECUTE START faction=0x{key:X8} state={Safe(GetMember(__instance, "m_CurrentState"))}.");
    }

    private static void AIFactionExecutePostfix(object __instance, long __state)
    {
        if (!Enabled() || !AIDiagnostics()) return;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state);
        int key = __instance == null ? 0 : RuntimeHelpers.GetHashCode(__instance);
        _log.Msg($"{ModName}: AI EXECUTE RETURN faction=0x{key:X8} elapsed={elapsed}ms state={Safe(GetMember(__instance, "m_CurrentState"))}.");
    }

    private static void PathfindingPrefix(MethodBase __originalMethod, object[] __args, out long __state)
    {
        __state = Environment.TickCount64;
        if (!Enabled() || !AIDiagnostics()) return;

        int call = ++_pathCalls;
        object start = __args != null && __args.Length > 0 ? __args[0] : null;
        object dest = __args != null && __args.Length > 1 ? __args[1] : null;
        object actor = __args != null && __args.Length > 2 ? __args[2] : null;
        bool boundary = IsBoundaryTile(start) || IsBoundaryTile(dest);

        if (boundary)
            _log.Msg($"{ModName}: PATH START #{call} method={__originalMethod?.Name} boundary=true start={DescribeTile(start)} dest={DescribeTile(dest)} {DescribeActor(actor)}.");
    }

    private static void PathfindingPostfix(MethodBase __originalMethod, object[] __args, long __state)
    {
        if (!Enabled() || !AIDiagnostics()) return;
        long elapsed = Math.Max(0, Environment.TickCount64 - __state);
        object start = __args != null && __args.Length > 0 ? __args[0] : null;
        object dest = __args != null && __args.Length > 1 ? __args[1] : null;
        object actor = __args != null && __args.Length > 2 ? __args[2] : null;
        bool boundary = IsBoundaryTile(start) || IsBoundaryTile(dest);

        if (boundary || elapsed >= 250)
            _log.Msg($"{ModName}: PATH RETURN method={__originalMethod?.Name} elapsed={elapsed}ms boundary={boundary} start={DescribeTile(start)} dest={DescribeTile(dest)} {DescribeActor(actor)}.");
    }

    private static bool IsBoundaryTile(object tile)
    {
        if (tile == null) return false;
        int x = ToInt(InvokeNoArg(tile, "GetX") ?? GetMember(tile, "m_X"), -1);
        int z = ToInt(InvokeNoArg(tile, "GetZ") ?? GetMember(tile, "m_Z"), -1);
        return x < 0 || z < 0 || x >= TargetSize - 2 || z >= TargetSize - 2;
    }

    private static string DescribeTile(object tile)
    {
        if (tile == null) return "<null>";
        int x = ToInt(InvokeNoArg(tile, "GetX") ?? GetMember(tile, "m_X"), -1);
        int z = ToInt(InvokeNoArg(tile, "GetZ") ?? GetMember(tile, "m_Z"), -1);
        return $"({x},{z})";
    }

    private static string DescribeActor(object actor)
    {
        if (actor == null) return "actor=<null>";
        try
        {
            object id = GetMember(actor, "ID") ?? InvokeNoArg(actor, "get_ID");
            object name = GetMember(actor, "DebugName") ?? InvokeNoArg(actor, "get_DebugName") ?? InvokeNoArg(actor, "GetName");
            object tile = InvokeNoArg(actor, "GetTile");
            object ap = InvokeNoArg(actor, "GetActionPoints");
            object faction = InvokeNoArg(actor, "GetFactionID");
            object entityType = InvokeNoArg(actor, "GetEntityType") ?? GetMember(actor, "m_EntityType");
            object template = InvokeNoArg(actor, "GetTemplate") ?? GetMember(actor, "m_Template");
            object templateId = template == null ? null : (GetMember(template, "m_ID") ?? GetMember(template, "ID"));
            return $"actorPtr={FormatNativePointer(actor)} actor={Safe(id)} name={Safe(name)} faction={Safe(faction)} type={Safe(entityType)} template={Safe(templateId)} tile={DescribeTileDetailed(tile)} AP={Safe(ap)}";
        }
        catch
        {
            return $"actorPtr={FormatNativePointer(actor)} actorType={actor.GetType().FullName}";
        }
    }

    private static object InvokeNoArg(object obj, string methodName)
    {
        if (obj == null || string.IsNullOrEmpty(methodName)) return null;
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = obj.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
            return method?.Invoke(obj, null);
        }
        catch { return null; }
    }

    private static void PatchTacticalCameraZoom(HarmonyLib.Harmony harmony)
    {
        if (_tacticalCameraType == null)
        {
            _log.Warning($"{ModName}: TacticalCamera type not found; extended zoom-out unavailable.");
            return;
        }

        MethodInfo start = _tacticalCameraType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "Start" && m.GetParameters().Length == 0);
        if (start == null)
        {
            _log.Warning($"{ModName}: TacticalCamera.Start() not found; extended zoom-out unavailable.");
            return;
        }

        harmony.Patch(
            start,
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TacticalCameraStartPostfix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked {FormatMethod(start)} for optional extended tactical zoom-out.");
    }

    private static void TacticalCameraStartPostfix(object __instance)
    {
        if (!Enabled() || !ExtendedZoomOut() || __instance == null) return;

        try
        {
            object zoomLimit = GetMember(__instance, "m_ZoomLimit");
            if (zoomLimit == null)
            {
                _log.Warning($"{ModName}: TacticalCamera.m_ZoomLimit was not readable; leaving vanilla zoom limit.");
                return;
            }

            float minZoom = ToFloat(GetMember(zoomLimit, "x") ?? GetMember(zoomLimit, "X"), float.NaN);
            float maxZoom = ToFloat(GetMember(zoomLimit, "y") ?? GetMember(zoomLimit, "Y"), float.NaN);
            float multiplier = Math.Clamp(ReadFloat("ZoomOutMultiplier", 2f), 1f, 4f);

            if (float.IsNaN(minZoom) || float.IsNaN(maxZoom) || maxZoom <= 0f)
            {
                _log.Warning($"{ModName}: TacticalCamera zoom limits were invalid ({Safe(zoomLimit)}); leaving vanilla zoom limit.");
                return;
            }

            float extendedMax = maxZoom * multiplier;
            if (multiplier <= 1.001f || extendedMax <= maxZoom + 0.01f) return;

            // m_ZoomLimit is a UnityEngine.Vector2 value type. Reflection returns a boxed copy;
            // update its Y value and assign the boxed struct back to the camera field.
            bool changedY = TrySetMember(zoomLimit, "y", extendedMax) || TrySetMember(zoomLimit, "Y", extendedMax);
            if (!changedY || !TrySetMember(__instance, "m_ZoomLimit", zoomLimit))
            {
                _log.Warning($"{ModName}: could not update TacticalCamera.m_ZoomLimit; leaving vanilla zoom limit.");
                return;
            }

            _extendedZoomPatches++;
            _log.Msg($"{ModName}: EXTENDED ZOOM enabled: TacticalCamera zoom limit {minZoom:0.##}-{maxZoom:0.##} -> {minZoom:0.##}-{extendedMax:0.##} ({multiplier:0.##}x max, patch #{_extendedZoomPatches}).");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: extended zoom-out patch failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PatchMissionAreaScaling(HarmonyLib.Harmony harmony)
    {
        if (_missionAreaConfigType == null)
        {
            _log.Warning($"{ModName}: MissionAreaConfig type not found; enemy spawn-area scaling unavailable.");
            return;
        }

        MethodInfo addToList = _missionAreaConfigType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "AddToList" && m.GetParameters().Length == 5);
        if (addToList == null)
        {
            _log.Warning($"{ModName}: MissionAreaConfig.AddToList/5 not found; enemy spawn-area scaling unavailable.");
            return;
        }

        // 64x64 uses the exact 1.3.11 handlers confirmed fast by the user.
        // Extended maps use the same cheap, scoped prefix and a conservative
        // large-map postfix. Neither version performs the 1.4.x broad
        // Objective.Init role-classification and area-expansion pipeline.
        bool useReference64Areas = TargetSize <= ZoneReferenceSize;
        string areaPrefix = nameof(Reference64MissionAreaAddToListPrefix);
        string areaPostfix = useReference64Areas ? nameof(Reference64MissionAreaAddToListPostfix) : nameof(ConservativeLargeMissionAreaAddToListPostfix);
        harmony.Patch(
            addToList,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(areaPrefix, BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(areaPostfix, BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(addToList)} for scoped proportional enemy spawn-area expansion " +
            (useReference64Areas ? "(original 1.3.11 area handlers)." : "(conservative large-map area handlers)."));

        // Keep the proven 1.3.11 area-scope coverage for all map sizes.
        // Invalid scripted actor rectangles at N>64 are covered separately by
        // the invalid-only Mission.TryCreateInitialAiUnit hook above.
        PatchScopedMethod(harmony, _genericMissionTemplateType, "OnCreateInitialAiUnits", 4, nameof(EnemySpawnScopePrefix), nameof(EnemySpawnScopePostfix), "enemy initial spawn");
        PatchScopedMethod(harmony, _reinforcementGroupType, "Init", 2, nameof(EnemySpawnScopePrefix), nameof(EnemySpawnScopePostfix), "enemy reinforcement spawn");
        PatchScopedMethod(harmony, _missionTemplateType, "CreateDeploymentZones", 3, nameof(PlayerDeploymentScopePrefix), nameof(PlayerDeploymentScopePostfix), "player deployment zone");
        // KillUnit objectives (for example the Pirate First Mate) own a separate target-spawn
        // area pipeline, so they bypass GenericMissionTemplate.OnCreateInitialAiUnits. Scope
        // both phases: different objective implementations build their MissionAreaConfig list
        // either during Init or immediately before the target actor is created.
        PatchScopedMethod(harmony, _killUnitType, "Init", 4, nameof(ObjectiveTargetSpawnScopePrefix), nameof(ObjectiveTargetSpawnScopePostfix), "kill-unit target spawn");
        PatchScopedMethod(harmony, _killUnitType, "OnCreateInitialAiUnits", 4, nameof(ObjectiveTargetSpawnScopePrefix), nameof(ObjectiveTargetSpawnScopePostfix), "kill-unit target creation");
        // RescueUnit uses the native objective area, as in 1.3.11. Malformed
        // actor spawn rectangles on large maps are normalized at the callsite.

        // Evacuation objectives are another intentional exception to the general
        // "leave objective areas vanilla" rule. Authored evacuation setpiece offsets
        // and randomization are scaled separately in ChunkMetaGenerator, while
        // EvacuateToArea rectangles are mapped into the enlarged coordinate space
        // when they are true map-space areas. Arbitrary POI/structure objectives
        // remain untouched.
        PatchScopedMethod(harmony, _evacuateToAreaType, "Init", 4, nameof(EvacuationAreaScopePrefix), nameof(EvacuationAreaScopePostfix), "evacuation objective area");

        // RecoverObject has a dedicated player drop-off area list. On vanilla maps those
        // drop-off zones are authored to line up with extraction/evacuation, but leaving
        // map-space drop-off rectangles in 42x42 coordinates while evac is moved into
        // 64x64 space separates the two markers. Scope RecoverObject.Init and scale only
        // the MissionAreaConfig instances referenced by PlayerDropOffAreas; object spawn
        // areas and unrelated objective areas remain untouched.
        PatchScopedMethod(harmony, _recoverObjectType, "Init", 4, nameof(RecoverDropoffAreaScopePrefix), nameof(RecoverDropoffAreaScopePostfix), "recover-object drop-off area");
    }


    // All authored mission objectives (including future objective classes) use
    // Objective.Init and, for their rectangles, MissionAreaConfig.AddToList.
    // Hook the concrete overrides instead of listing a few mission templates.
    // The resulting RectInt list is used by both the preview and tactical logic.
    private static void PatchAllObjectiveAreaScopes(HarmonyLib.Harmony harmony)
    {
        if (_objectiveType == null) return;
        Type[] types;
        try { types = _objectiveType.Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: could not discover objective area types: {ex.GetType().Name}.");
            return;
        }

        int patched = 0;
        int failures = 0;
        MethodInfo prefix = typeof(Plugin).GetMethod(nameof(ObjectiveAreaScopePrefix), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo postfix = typeof(Plugin).GetMethod(nameof(ObjectiveAreaScopePostfix), BindingFlags.Static | BindingFlags.NonPublic);
        foreach (Type type in types)
        {
            if (type == null || !_objectiveType.IsAssignableFrom(type) ||
                (type != _objectiveType && type.IsAbstract) ||
                type.Namespace != _objectiveType.Namespace) continue;
            MethodInfo init = type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == "Init" && m.GetParameters().Length == 4);
            if (init == null) continue;
            try
            {
                harmony.Patch(init, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                patched++;
            }
            catch (Exception ex)
            {
                failures++;
                if (Debug()) _log.Warning($"{ModName}: objective area scope not installed for {type.Name}: {ex.Message}");
            }
        }
        if (patched < 2 || failures > 0)
            _log.Warning($"{ModName}: objective-area coverage: {patched} Init hooks, {failures} failures; check map-zone behavior.");
        else
            _log.Msg($"{ModName}: objective-area scaling installed for {patched} objective Init methods (mission types discovered automatically).");
    }

    private static void ObjectiveAreaScopePrefix(object __instance, out object __state)
    {
        __state = _objectiveAreaContext;
        if (Enabled()) _objectiveAreaContext = __instance;
    }

    private static void ObjectiveAreaScopePostfix(object __state)
    {
        _objectiveAreaContext = __state;
    }

    // Identify the role from the ACTUAL ObjectiveConfig field pointing at this
    // MissionAreaConfig, not from a mission name. A mission may contain scoring,
    // extraction, trigger, AI and spawn zones in the same Objective.Init call.
    private static ObjectiveAreaRole GetObjectiveAreaRole(object areaConfig)
    {
        object objective = _objectiveAreaContext;
        if (objective == null || areaConfig == null) return ObjectiveAreaRole.Unknown;
        object config = GetMember(objective, "m_Config") ?? InvokeNoArg(objective, "GetConfig");
        if (config == null) return ObjectiveAreaRole.Unknown;

        try
        {
            object aiZones = GetMember(config, "AIZones");
            int count = Math.Min(256, Math.Max(0, GetCollectionCount(aiZones)));
            for (int i = 0; i < count; i++)
            {
                object zone = GetIndexedValue(aiZones, i);
                if (NativePointersEqual(GetMember(zone, "Area"), areaConfig))
                    return ObjectiveAreaRole.AI;
            }

            for (Type t = config.GetType(); t != null; t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    // IL2CPP uses Il2CppReferenceArray<MissionAreaConfig> rather
                    // than MissionAreaConfig[] in several generated assemblies.
                    // Inspect the constructed type too, not just its short name.
                    string fieldTypeName = field.FieldType.FullName ?? field.FieldType.Name;
                    if (!fieldTypeName.Contains("MissionAreaConfig", StringComparison.Ordinal) &&
                        !field.Name.Contains("Area", StringComparison.OrdinalIgnoreCase)) continue;
                    object value;
                    try { value = field.GetValue(config); }
                    catch { value = GetMember(config, field.Name); }
                    if (value == null) continue;
                    bool matches = NativePointersEqual(value, areaConfig);
                    if (!matches)
                    {
                        int n = Math.Min(256, Math.Max(0, GetCollectionCount(value)));
                        for (int i = 0; i < n; i++)
                            if (NativePointersEqual(GetIndexedValue(value, i), areaConfig)) { matches = true; break; }
                    }
                    if (!matches) continue;

                    if (field.Name.Contains("Spawn", StringComparison.OrdinalIgnoreCase))
                        return ObjectiveAreaRole.Spawn;
                    if (field.Name.Contains("Trigger", StringComparison.OrdinalIgnoreCase))
                        return ObjectiveAreaRole.Trigger;
                    return ObjectiveAreaRole.ControlOrExtraction;
                }
            }
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: objective area role detection failed: {ex.GetType().Name}: {ex.Message}");
        }

        // Narrow fallback for a few historic wrapper variants that expose only
        // ObjectiveConfig (not the concrete derived configuration type).
        string typeName = objective.GetType().Name;
        if (typeName.Contains("CustomBarks", StringComparison.Ordinal)) return ObjectiveAreaRole.Trigger;
        if (typeName.Contains("KillUnit", StringComparison.Ordinal)) return ObjectiveAreaRole.Spawn;
        if (typeName.Contains("DefendArea", StringComparison.Ordinal) ||
            typeName.Contains("CaptureArea", StringComparison.Ordinal) ||
            typeName.Contains("InvestigateArea", StringComparison.Ordinal) ||
            typeName.Contains("SabotageArea", StringComparison.Ordinal) ||
            typeName.Contains("EvacuateToArea", StringComparison.Ordinal) ||
            typeName.Contains("StoryMission1", StringComparison.Ordinal) ||
            typeName.Contains("CombatTutorial", StringComparison.Ordinal))
            return ObjectiveAreaRole.ControlOrExtraction;
        if (IsRecoverDropoffAreaConfig(areaConfig)) return ObjectiveAreaRole.ControlOrExtraction;
        return ObjectiveAreaRole.Unknown;
    }

    // A placed setpiece already has an absolute position on the generated map.
    // Scale the native 64x64 footprint by N/64, not the whole compound's bounds.
    // For primary-complex objectives we also scale the original *offset* from
    // the compound center, preserving intentional asymmetry/deployment insets.
    private static bool TryFindNearestPrimaryCompound(object preview,
        double objectiveCenterX, double objectiveCenterY,
        int mapWidth, int mapHeight,
        out int left, out int top, out int width, out int height)
    {
        left = top = width = height = 0;
        object chunks = GetMember(GetMember(preview, "Blueprint"), "Chunks");
        int count = Math.Min(512, GetCollectionCount(chunks));
        if (count <= 0) return false;

        double nearestDistance = double.MaxValue;
        double nearestCenter = double.MaxValue;
        for (int j = 0; j < count; j++)
        {
            object chunk = GetIndexedValue(chunks, j);
            if (chunk == null || ToInt(GetMember(chunk, "Primary"), 0) == 0) continue;
            object bounds = InvokeNoArg(chunk, "GetBounds");
            if (bounds == null) continue;
            int x = ToInt(GetMember(bounds, "xMin") ?? GetMember(bounds, "x"), -1);
            int y = ToInt(GetMember(bounds, "yMin") ?? GetMember(bounds, "y"), -1);
            int w = ToInt(GetMember(bounds, "width"), 0);
            int h = ToInt(GetMember(bounds, "height"), 0);
            if (w < 2 || h < 2 || x < 0 || y < 0 || x + (long)w > mapWidth || y + (long)h > mapHeight)
                continue;
            double dx = Math.Max(0, Math.Max(x - objectiveCenterX, objectiveCenterX - (x + w - 1)));
            double dy = Math.Max(0, Math.Max(y - objectiveCenterY, objectiveCenterY - (y + h - 1)));
            double dist = dx * dx + dy * dy;
            double cx = x + (w - 1) * 0.5;
            double cy = y + (h - 1) * 0.5;
            double centerDist = (cx - objectiveCenterX) * (cx - objectiveCenterX) +
                (cy - objectiveCenterY) * (cy - objectiveCenterY);
            if (dist < nearestDistance || (dist == nearestDistance && centerDist < nearestCenter))
            {
                nearestDistance = dist;
                nearestCenter = centerDist;
                left = x; top = y; width = w; height = h;
            }
        }
        if (width == 0) return false;
        double maxOffset = Math.Max(6.0, Math.Max(width, height) * 0.75);
        return nearestDistance <= maxOffset * maxOffset;
    }

    // RectInt can be half-open (spawns/deployment) or treated with inclusive
    // endpoints by some Objective routines. The signed width/height is preserved.
    // "Original" means the unmodified RectInt produced by native AddToList on
    // this mission, not the setpiece size or a guessed 5x5 minimum.
    private static bool TryScaleResolvedAnchoredRect(object rect, object preview,
        int mapWidth, int mapHeight, bool inclusiveEndpoints, bool trackPrimaryOffset,
        int originalReferenceMinimumSpan,
        out object scaled, out string before, out string after)
    {
        scaled = null;
        before = after = "<unavailable>";
        if (rect == null || mapWidth <= ZoneReferenceSize || mapHeight <= ZoneReferenceSize)
            return false;
        try
        {
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), int.MinValue);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), int.MinValue);
            int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), int.MinValue);
            int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), int.MinValue);
            if (x == int.MinValue || y == int.MinValue || w == int.MinValue || h == int.MinValue)
                return false;
            before = $"({x},{y},{w},{h})";
            if ((w == 0 || h == 0) && originalReferenceMinimumSpan <= 0)
                return false; // invalid spawn handled by a separate safety net
            int extra = inclusiveEndpoints ? 1 : 0;
            int originalW = (int)Math.Min((long)mapWidth,
                Math.Max(originalReferenceMinimumSpan, Math.Abs((long)w) + extra));
            int originalH = (int)Math.Min((long)mapHeight,
                Math.Max(originalReferenceMinimumSpan, Math.Abs((long)h) + extra));
            double sx = mapWidth / (double)ZoneReferenceSize;
            double sy = mapHeight / (double)ZoneReferenceSize;
            int newW = Math.Clamp((int)Math.Round(originalW * sx, MidpointRounding.AwayFromZero), 1, mapWidth);
            int newH = Math.Clamp((int)Math.Round(originalH * sy, MidpointRounding.AwayFromZero), 1, mapHeight);
            double desiredCx = x + w * 0.5;
            double desiredCy = y + h * 0.5;
            if (trackPrimaryOffset && TryFindNearestPrimaryCompound(preview, desiredCx, desiredCy,
                mapWidth, mapHeight, out int baseX, out int baseY, out int baseW, out int baseH))
            {
                double anchorX = baseX + (baseW - 1) * 0.5;
                double anchorY = baseY + (baseH - 1) * 0.5;
                desiredCx = anchorX + (desiredCx - anchorX) * sx;
                desiredCy = anchorY + (desiredCy - anchorY) * sy;
            }
            // Center calculation uses the source sign as originally authored.
            // The low edge of a negative-delta area is x+w (inclusive) or x+w
            // (half-open); the signed result is reconstructed after clamping.
            int lowX = Math.Clamp((int)Math.Round(desiredCx - (newW - extra) * 0.5), 0, mapWidth - newW);
            int lowY = Math.Clamp((int)Math.Round(desiredCy - (newH - extra) * 0.5), 0, mapHeight - newH);
            int nx = w < 0 ? lowX + newW - extra : lowX;
            int ny = h < 0 ? lowY + newH - extra : lowY;
            int nw = w < 0 ? extra - newW : newW - extra;
            int nh = h < 0 ? extra - newH : newH - extra;
            if (nx == x && ny == y && nw == w && nh == h) return false;
            if (!TryNewRectInt(rect, nx, ny, nw, nh, out scaled)) return false;
            after = $"({nx},{ny},{nw},{nh}) footprint={newW}x{newH} factor={sx:0.###}/{sy:0.###}";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: reference anchored-area scaling failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // Authored Rect areas do not refer to placed chunks. At 64x64 their
    // AddToList output is the authoritative reference, so positions and deltas
    // both use N/64 (NOT N/42, which would also change the 64 baseline).
    private static bool TryScaleReferenceAuthoredRect(object rect, int mapWidth, int mapHeight,
        out object scaled, out string before, out string after)
    {
        scaled = null;
        before = after = "<unavailable>";
        if (rect == null || mapWidth <= ZoneReferenceSize || mapHeight <= ZoneReferenceSize)
            return false;
        try
        {
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), int.MinValue);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), int.MinValue);
            int dx = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), int.MinValue);
            int dy = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), int.MinValue);
            if (x == int.MinValue || y == int.MinValue || dx == int.MinValue || dy == int.MinValue)
                return false;
            before = $"({x},{y},{dx},{dy})";
            double sx = mapWidth / (double)ZoneReferenceSize;
            double sy = mapHeight / (double)ZoneReferenceSize;
            int ax = (int)Math.Round(x * sx), ay = (int)Math.Round(y * sy);
            int bx = (int)Math.Round(((long)x + dx) * sx);
            int by = (int)Math.Round(((long)y + dy) * sy);
            // Independently clip endpoints so signed directions stay intact.
            int nx = Math.Clamp(ax, 0, mapWidth - 1);
            int ny = Math.Clamp(ay, 0, mapHeight - 1);
            int ex = Math.Clamp(bx, 0, mapWidth - 1);
            int ey = Math.Clamp(by, 0, mapHeight - 1);
            int nw = ex - nx, nh = ey - ny;
            if (dx != 0 && nw == 0)
            {
                if (dx > 0 && nx < mapWidth - 1) nw = 1;
                else if (dx < 0 && nx > 0) nw = -1;
            }
            if (dy != 0 && nh == 0)
            {
                if (dy > 0 && ny < mapHeight - 1) nh = 1;
                else if (dy < 0 && ny > 0) nh = -1;
            }
            if (nx == x && ny == y && nw == dx && nh == dy) return false;
            if (!TryNewRectInt(rect, nx, ny, nw, nh, out scaled)) return false;
            after = $"({nx},{ny},{nw},{nh}) factor={sx:0.###}/{sy:0.###}";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: reference authored-area scaling failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // MissionTemplate.GetBounds now yields the expanded map width on N>64, so
    // AddToList already positions border strips along the ACTUAL map edges.
    // Its short axis, however, is still the template's vanilla BorderWidth
    // (3 or 4 tiles). Match the 64x64 reference: first convert that short
    // thickness from 42->64, then from 64->N. Never scale the long axis again.
    // The 64x64 reference is produced from 42x42 native template widths.
    // Retaining that intermediate integer rounding reproduces the actual
    // reference thickness rather than inventing a new width at 96x96.
    private static int ScaleBorderThickness(int nativeThickness, int currentSize)
    {
        int at64 = Math.Max(1, (int)Math.Round(nativeThickness * (double)ZoneReferenceSize / VanillaSize));
        return Math.Clamp((int)Math.Round(at64 * (double)currentSize / ZoneReferenceSize), 1, currentSize);
    }

    private static bool TryScaleReferenceBorderRect(object rect, int areaType,
        int mapWidth, int mapHeight, bool inclusiveEndpoints,
        out object scaled, out string before, out string after)
    {
        scaled = null;
        before = after = "<unavailable>";
        if (rect == null || areaType < 2 || areaType > 10 || areaType == 6 ||
            mapWidth <= ZoneReferenceSize || mapHeight <= ZoneReferenceSize) return false;
        try
        {
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), int.MinValue);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), int.MinValue);
            int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), int.MinValue);
            int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), int.MinValue);
            if (x == int.MinValue || y == int.MinValue || w == int.MinValue || h == int.MinValue ||
                w == 0 || h == 0) return false;
            before = $"({x},{y},{w},{h})";
            int extra = inclusiveEndpoints ? 1 : 0;
            int spanW = (int)Math.Min((long)mapWidth, Math.Abs((long)w) + extra);
            int spanH = (int)Math.Min((long)mapHeight, Math.Abs((long)h) + extra);
            bool thinX = areaType == 3 || areaType == 4 || areaType >= 7;
            bool thinY = areaType == 2 || areaType == 5 || areaType >= 7;
            int targetW = thinX ? ScaleBorderThickness(spanW, mapWidth) : spanW;
            int targetH = thinY ? ScaleBorderThickness(spanH, mapHeight) : spanH;
            int lowX = Math.Min(x, x + w);
            int lowY = Math.Min(y, y + h);
            // Native start/inset and long-axis length already use current N.
            // Keep every authored inset, only grow the band's short axis
            // inward from its existing edge (never extend outside the map).
            bool east = areaType == 3 || areaType == 7 || areaType == 8;
            bool north = areaType == 5 || areaType == 7 || areaType == 10;
            if (east) lowX = Math.Max(0, Math.Min(x, x + w) + spanW - targetW);
            if (north) lowY = Math.Max(0, Math.Min(y, y + h) + spanH - targetH);
            lowX = Math.Clamp(lowX, 0, mapWidth - targetW);
            lowY = Math.Clamp(lowY, 0, mapHeight - targetH);
            int nx = w < 0 ? lowX + targetW - extra : lowX;
            int ny = h < 0 ? lowY + targetH - extra : lowY;
            int nw = w < 0 ? extra - targetW : targetW - extra;
            int nh = h < 0 ? extra - targetH : targetH - extra;
            if (nx == x && ny == y && nw == w && nh == h) return false;
            if (!TryNewRectInt(rect, nx, ny, nw, nh, out scaled)) return false;
            after = $"({nx},{ny},{nw},{nh}) footprint={targetW}x{targetH}, native long axis retained";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: reference border-area scaling failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool TryNewRectInt(object original, int x, int y, int w, int h, out object result)
    {
        result = null;
        ConstructorInfo ctor = original?.GetType().GetConstructors(BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(c =>
            c.GetParameters().Length == 4 && c.GetParameters().All(p => p.ParameterType == typeof(int)));
        if (ctor == null) return false;
        result = ctor.Invoke(new object[] { x, y, w, h });
        return result != null;
    }

    // Final safety net: not all mission actors use MissionAreaConfig.AddToList.
    // Generic mission previews can supply an already-resolved 0x1 strip through
    // MissionTemplate.CreateInitialAiUnits, while RescueUnit owns a separate path.
    // Always adapt the concrete spawn rect, never the mission template globally.
    private static void PatchFinalSpawnAreaSafety(HarmonyLib.Harmony harmony)
    {
        try
        {
            MethodInfo initial = _missionType?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "TryCreateInitialAiUnit" && m.GetParameters().Length == 9);
            if (initial == null)
                _log.Warning($"{ModName}: Mission.TryCreateInitialAiUnit/9 missing; initial AI spawn safety unavailable.");
            else
            {
                MethodInfo prefix = typeof(Plugin).GetMethod(nameof(MissionInitialSpawnPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(initial, prefix: new HarmonyMethod(prefix));
                _log.Msg($"{ModName}: initial AI spawn area safety active (civilians, rescue targets and enemies).");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: initial AI spawn safety hook unavailable: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            MethodInfo prefix = typeof(Plugin).GetMethod(nameof(TacticalSpawnAreaPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            int hooked = 0;
            if (_tacticalManagerType != null)
                foreach (MethodInfo method in _tacticalManagerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    ParameterInfo[] args = method.GetParameters();
                    if (method.Name != "TrySpawnUnit" || (args.Length != 5 && args.Length != 6) ||
                        args[2].ParameterType.Name.IndexOf("RectInt", StringComparison.Ordinal) < 0) continue;
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                    hooked++;
                }
            if (hooked == 0) _log.Warning($"{ModName}: tactical reinforcement RectInt spawn hook missing.");
            else if (Debug()) _log.Msg($"{ModName}: tactical RectInt spawn safety installed for {hooked} overload(s).");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: tactical spawn safety hook unavailable: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MissionInitialSpawnPrefix(object[] __args)
    {
        if (!Enabled() || TargetSize <= ZoneReferenceSize || __args == null || __args.Length < 5) return;
        // IList<RectInt> is argument #2; preview is argument #4.
        object preview = __args[4];
        int width = ToInt(GetMember(preview, "SizeX"), TargetSize);
        int height = ToInt(GetMember(preview, "SizeZ"), TargetSize);
        NormalizeSpawnList(__args[2], width, height);
    }

    private static void TacticalSpawnAreaPrefix(object[] __args)
    {
        if (!Enabled() || TargetSize <= ZoneReferenceSize || __args == null || __args.Length < 3) return;
        object rect = __args[2];
        if (!TryNormalizeSpawnRect(rect, TargetSize, TargetSize, MinimumAiSpawnAreaSize,
            out object newRect, out string before, out string after)) return;
        __args[2] = newRect;
        _tacticalAiSpawnRectsFixed++;
        NoteSpawnRectFix("reinforcement", before, after);
    }

    private static void NormalizeSpawnList(object list, int width, int height)
    {
        int count = GetCollectionCount(list);
        if (count <= 0 || width < VanillaSize || height < VanillaSize) return;
        for (int i = 0; i < count; i++)
        {
            object rect = GetIndexedValue(list, i);
            if (!TryNormalizeSpawnRect(rect, width, height, MinimumAiSpawnAreaSize,
                out object newRect, out string before, out string after)) continue;
            if (TrySetIndexedValue(list, i, newRect))
            {
                _initialAiSpawnRectsFixed++;
                NoteSpawnRectFix("initial", before, after);
            }
            else
            {
                _spawnFixFailures++;
                if (_spawnFixFailures == 1)
                    _log.Warning($"{ModName}: unable to write an initial spawn rect ({before}); further identical warnings suppressed.");
            }
        }
    }

    private static void NoteSpawnRectFix(string kind, string before, string after)
    {
        if (++_spawnFixNoticeCount <= 1 || Debug())
            _log.Msg($"{ModName}: {kind} AI spawn area corrected {before} -> {after}; subsequent corrections silent (Debug OFF).");
    }

    // Normalize HALF-OPEN Unity RectInt ranges to valid positive widths and
    // heights. Signed or zero deltas are not usable as actor search ranges.
    // Preserve the area center/nearby setpiece position and clamp to the chosen
    // map size, rather than scaling an already-resolved coordinate a second time.
    private static bool TryNormalizeSpawnRect(object rect, int mapWidth, int mapHeight, int minimumSpan,
        out object normalized, out string before, out string after)
    {
        normalized = null;
        before = "<unknown>";
        after = "<unknown>";
        if (rect == null || mapWidth <= 0 || mapHeight <= 0) return false;
        try
        {
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), int.MinValue);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), int.MinValue);
            int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), int.MinValue);
            int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), int.MinValue);
            if (x == int.MinValue || y == int.MinValue || w == int.MinValue || h == int.MinValue) return false;
            before = $"({x},{y},{w},{h})";
            // Valid small ranges are intentionally small; only fix unusable or
            // out-of-map rectangles (the 1.4.2 civilian/actor bug).
            if (w > 0 && h > 0 && x >= 0 && y >= 0 &&
                (long)x + w <= mapWidth && (long)y + h <= mapHeight) return false;

            long left = Math.Min((long)x, (long)x + w);
            long top = Math.Min((long)y, (long)y + h);
            long oldSpanX = Math.Max(1L, Math.Abs((long)w));
            long oldSpanY = Math.Max(1L, Math.Abs((long)h));
            int newW = (int)Math.Min((long)mapWidth, Math.Max((long)minimumSpan, oldSpanX));
            int newH = (int)Math.Min((long)mapHeight, Math.Max((long)minimumSpan, oldSpanY));
            long centerX = left + (oldSpanX - 1) / 2;
            long centerY = top + (oldSpanY - 1) / 2;
            int newX = (int)Math.Clamp(centerX - (newW - 1) / 2, 0L, (long)mapWidth - newW);
            int newY = (int)Math.Clamp(centerY - (newH - 1) / 2, 0L, (long)mapHeight - newH);

            ConstructorInfo ctor = rect.GetType().GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(int) });
            if (ctor == null) return false;
            normalized = ctor.Invoke(new object[] { newX, newY, newW, newH });
            after = $"({newX},{newY},{newW},{newH})";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: spawn area normalization failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void PatchEvacSetpiecePlacement(HarmonyLib.Harmony harmony)
    {
        if (_chunkMetaGeneratorType == null)
        {
            if (Debug()) _log.Warning($"{ModName}: ChunkMetaGenerator type not found; evacuation setpiece placement scaling unavailable.");
            return;
        }

        MethodInfo method = _chunkMetaGeneratorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "AddSetpiecesFromList" && m.GetParameters().Length == 4);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: ChunkMetaGenerator.AddSetpiecesFromList/4 not found; evacuation setpiece placement scaling unavailable.");
            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(EvacSetpiecesPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(EvacSetpiecesPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(method)} for proportional evacuation setpiece placement.");
    }

    private static void EvacSetpiecesPrefix(object[] __args, out EvacSetpiecePatchState __state)
    {
        __state = null;
        if (!Enabled() || __args == null || __args.Length < 4) return;

        object input = __args[0];
        object sourceList = __args[1];
        string sourceListName = Safe(__args[2]);
        bool primary = false;
        try { primary = Convert.ToBoolean(__args[3]); } catch { }

        string missionId = GetMissionTemplateIdFromMapgenInput(input);
        int groupCount = GetCollectionCount(sourceList);
        if (groupCount <= 0) return;

        float scale = TargetSize / (float)VanillaSize;
        EvacSetpiecePatchState state = new() { MissionId = missionId };

        for (int g = 0; g < groupCount; g++)
        {
            object group = GetIndexedValue(sourceList, g);
            object templates = GetMember(group, "Templates");
            int templateCount = GetCollectionCount(templates);
            for (int i = 0; i < templateCount; i++)
            {
                object placement = GetIndexedValue(templates, i);
                if (placement == null) continue;

                object setpiece = GetMember(placement, "Setpiece");
                string setpieceId = GetDataTemplateId(setpiece);
                if (!IsEvacSetpiece(setpieceId)) continue;

                object offset = GetMember(placement, "Offset");
                if (offset == null) continue;

                int x = ReadVectorIntComponent(offset, "x", "X", "m_X");
                int y = ReadVectorIntComponent(offset, "y", "Y", "m_Y");
                int newX = (int)Math.Round(x * scale);
                int newY = (int)Math.Round(y * scale);
                bool offsetChanged = newX != x || newY != y;

                object variance = GetMember(placement, "Variance");
                int varianceX = variance == null ? 0 : ReadVectorIntComponent(variance, "x", "X", "m_X");
                int varianceY = variance == null ? 0 : ReadVectorIntComponent(variance, "y", "Y", "m_Y");
                int newVarianceX = (int)Math.Round(varianceX * scale);
                int newVarianceY = (int)Math.Round(varianceY * scale);
                bool varianceChanged = variance != null && (newVarianceX != varianceX || newVarianceY != varianceY);

                if (!offsetChanged && !varianceChanged) continue;

                bool offsetApplied = false;
                bool varianceApplied = false;

                if (offsetChanged)
                {
                    object changedOffset = GetMember(placement, "Offset");
                    if (changedOffset == null || !WriteVectorInt(changedOffset, newX, newY) || !TrySetMember(placement, "Offset", changedOffset))
                        continue;
                    offsetApplied = true;
                }

                if (varianceChanged)
                {
                    object changedVariance = GetMember(placement, "Variance");
                    if (changedVariance == null || !WriteVectorInt(changedVariance, newVarianceX, newVarianceY) || !TrySetMember(placement, "Variance", changedVariance))
                    {
                        if (offsetApplied)
                        {
                            object originalOffset = GetMember(placement, "Offset");
                            if (originalOffset != null && WriteVectorInt(originalOffset, x, y))
                                TrySetMember(placement, "Offset", originalOffset);
                        }
                        continue;
                    }
                    varianceApplied = true;
                }

                state.Restores.Add(new EvacSetpieceOffsetRestore
                {
                    Placement = placement,
                    X = x,
                    Y = y,
                    VarianceX = varianceX,
                    VarianceY = varianceY,
                    RestoreOffset = offsetApplied,
                    RestoreVariance = varianceApplied
                });

                _log.Msg($"{ModName}: EVAC SETPIECE {missionId} {setpieceId} [{(primary ? "primary" : "secondary")}/{sourceListName}] offset ({x},{y}) -> ({newX},{newY}), variance ({varianceX},{varianceY}) -> ({newVarianceX},{newVarianceY}) scale={scale:0.###}.");
            }
        }

        if (state.Restores.Count > 0)
        {
            __state = state;
            _log.Msg($"{ModName}: EVAC SETPIECE placement adjusted {state.Restores.Count} authored placement(s) for {missionId}.");
        }
    }

    private static void EvacSetpiecesPostfix(EvacSetpiecePatchState __state)
    {
        if (__state == null) return;
        foreach (EvacSetpieceOffsetRestore restore in __state.Restores)
        {
            try
            {
                if (restore.RestoreOffset)
                {
                    object offset = GetMember(restore.Placement, "Offset");
                    if (offset != null && WriteVectorInt(offset, restore.X, restore.Y))
                        TrySetMember(restore.Placement, "Offset", offset);
                }

                if (restore.RestoreVariance)
                {
                    object variance = GetMember(restore.Placement, "Variance");
                    if (variance != null && WriteVectorInt(variance, restore.VarianceX, restore.VarianceY))
                        TrySetMember(restore.Placement, "Variance", variance);
                }
            }
            catch { }
        }
    }

    private static string GetMissionTemplateIdFromMapgenInput(object input)
    {
        object mission = GetMember(input, "Mission") ?? InvokeNoArg(input, "get_Mission");
        object template = InvokeNoArg(mission, "GetTemplate") ?? GetMember(mission, "m_Template");
        return GetDataTemplateId(template);
    }

    private static string GetDataTemplateId(object template)
    {
        if (template == null) return "<null>";
        object id = GetMember(template, "m_ID") ?? GetMember(template, "ID") ?? InvokeNoArg(template, "get_ID");
        return id == null ? "<unknown>" : Safe(id);
    }

    private static bool IsEvacSetpiece(string setpieceId)
    {
        if (string.IsNullOrEmpty(setpieceId)) return false;

        // Current game data uses these five authored extraction/evacuation setpieces.
        // Keep this explicit instead of matching any arbitrary name containing "evac",
        // because some future decorative setpiece could use that word without being a
        // mission extraction destination.
        return string.Equals(setpieceId, "setpiece_civilian_evac_small_radar_01", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setpieceId, "setpiece_civilian_evac_zone_01", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setpieceId, "setpiece_civilian_evac_3x3_micro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setpieceId, "setpiece_civilian_evac_7x6_mini", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setpieceId, "setpiece_break_off_raid_evac_zone", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadVectorIntComponent(object vector, params string[] names)
    {
        foreach (string name in names)
        {
            object value = GetMember(vector, name);
            if (value != null) return ToInt(value, 0);
        }
        return 0;
    }

    private static bool WriteVectorInt(object vector, int x, int y)
    {
        bool xOk = TrySetMember(vector, "x", x) || TrySetMember(vector, "X", x) || TrySetMember(vector, "m_X", x);
        bool yOk = TrySetMember(vector, "y", y) || TrySetMember(vector, "Y", y) || TrySetMember(vector, "m_Y", y);
        return xOk && yOk;
    }

    private static void PatchScopedMethod(HarmonyLib.Harmony harmony, Type type, string name, int parameterCount, string prefixName, string postfixName, string label)
    {
        if (type == null)
        {
            if (Debug()) _log.Warning($"{ModName}: {label} scope type not found.");
            return;
        }

        MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: could not find {type.FullName}.{name}/{parameterCount} for {label} scope.");
            return;
        }

        MethodInfo prefix = typeof(Plugin).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo postfix = typeof(Plugin).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic);
        harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
        _log.Msg($"{ModName}: hooked {FormatMethod(method)} as {label} scaling scope.");
    }

    private static void EnemySpawnScopePrefix()
    {
        if (!Enabled() || !ExpandEnemySpawnAreas()) return;
        _enemySpawnAreaScopeDepth++;
    }

    private static void EnemySpawnScopePostfix()
    {
        if (_enemySpawnAreaScopeDepth > 0) _enemySpawnAreaScopeDepth--;
    }

    private static void PlayerDeploymentScopePrefix()
    {
        if (!Enabled()) return;
        _playerDeploymentAreaScopeDepth++;
    }

    private static void PlayerDeploymentScopePostfix()
    {
        if (_playerDeploymentAreaScopeDepth > 0) _playerDeploymentAreaScopeDepth--;
    }

    private static void ObjectiveTargetSpawnScopePrefix()
    {
        if (!Enabled() || !ExpandEnemySpawnAreas()) return;
        _objectiveTargetSpawnAreaScopeDepth++;
    }

    private static void ObjectiveTargetSpawnScopePostfix()
    {
        if (_objectiveTargetSpawnAreaScopeDepth > 0) _objectiveTargetSpawnAreaScopeDepth--;
    }

    private static void RecoverDropoffAreaScopePrefix(object __instance)
    {
        if (!Enabled()) return;

        _recoverDropoffAreaScopeDepth++;
        if (_recoverDropoffAreaScopeDepth != 1) return;

        _recoverDropoffAreaConfigs = new List<object>();
        object config = GetMember(__instance, "m_Config");
        object areas = GetMember(config, "PlayerDropOffAreas");
        int count = GetCollectionCount(areas);
        for (int i = 0; i < count; i++)
        {
            object area = GetIndexedValue(areas, i);
            if (area != null) _recoverDropoffAreaConfigs.Add(area);
        }
    }

    private static void RecoverDropoffAreaScopePostfix()
    {
        if (_recoverDropoffAreaScopeDepth > 0) _recoverDropoffAreaScopeDepth--;
        if (_recoverDropoffAreaScopeDepth == 0) _recoverDropoffAreaConfigs = null;
    }

    private static bool IsRecoverDropoffAreaConfig(object missionAreaConfig)
    {
        if (_recoverDropoffAreaScopeDepth <= 0 || missionAreaConfig == null || _recoverDropoffAreaConfigs == null)
            return false;

        foreach (object configuredArea in _recoverDropoffAreaConfigs)
        {
            if (NativePointersEqual(configuredArea, missionAreaConfig)) return true;
        }
        return false;
    }

    private static void EvacuationAreaScopePrefix()
    {
        if (!Enabled()) return;
        _evacuationAreaScopeDepth++;
    }

    private static void EvacuationAreaScopePostfix(object __instance)
    {
        try
        {
            // MissionAreaConfig.AddToList is useful for map-space scaling, but some
            // evacuation objectives can reach their finalized runtime m_Areas without
            // passing through a resizeable enlarged-map rectangle in that scoped hook.
            // Enforce the 5x5 safety minimum directly on the finalized EvacuateToArea
            // runtime list after vanilla Init so the objective can never remain 2x2/3x3.
            EnsureRuntimeEvacuationAreaMinimum(__instance);
        }
        finally
        {
            if (_evacuationAreaScopeDepth > 0) _evacuationAreaScopeDepth--;
        }
    }

    private static void EnsureRuntimeEvacuationAreaMinimum(object objective)
    {
        if (!Enabled() || objective == null) return;

        object areas = GetMember(objective, "m_Areas");
        int count = GetCollectionCount(areas);
        if (count <= 0)
        {
            _log.Warning($"{ModName}: EVAC RUNTIME AREA list was empty after EvacuateToArea.Init; minimum footprint could not be enforced.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            object rect = GetIndexedValue(areas, i);
            if (rect == null)
            {
                _log.Warning($"{ModName}: EVAC RUNTIME AREA index {i} was null; minimum footprint could not be enforced.");
                continue;
            }

            // MENACE deliberately uses signed RectInt deltas for some mission areas.
            // For example raw width=-1,height=-1 represents two inclusive tile
            // coordinates on each axis (a 2x2 objective area), not an invalid rect.
            // Read the raw backing values and measure the gameplay span as |delta|+1.
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), 0);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), 0);
            int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), 0);
            int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), 0);

            int spanW = (int)Math.Min((long)TargetSize, Math.Abs((long)w) + 1L);
            int spanH = (int)Math.Min((long)TargetSize, Math.Abs((long)h) + 1L);
            string before = $"raw=({x},{y},{w},{h}) span={spanW}x{spanH}";

            if (!TryCalculateMinimumInclusiveRect(x, y, w, h, TargetSize, TargetSize, MinimumEvacuationSize,
                    out int newX, out int newY, out int newW, out int newH, out int newSpanW, out int newSpanH))
            {
                if (Debug())
                    _log.Msg($"{ModName}: EVAC RUNTIME AREA already satisfies minimum: {before}.");
                continue;
            }

            string after = $"raw=({newX},{newY},{newW},{newH}) span={newSpanW}x{newSpanH}";

            if (!TryRewriteRectIntBox(rect, newX, newY, newW, newH))
            {
                _log.Warning($"{ModName}: EVAC RUNTIME AREA could not rewrite RectInt at index {i}: {before}.");
                continue;
            }

            if (!TrySetIndexedValue(areas, i, rect))
            {
                _log.Warning($"{ModName}: EVAC RUNTIME AREA could not replace RectInt at index {i}: {before}.");
                continue;
            }

            _evacuationRectsScaled++;
            _log.Msg($"{ModName}: EVAC RUNTIME AREA minimum footprint {before} -> {after}; finalized EvacuateToArea.m_Areas, minimum={MinimumEvacuationSize}x{MinimumEvacuationSize}.");
        }
    }

    private static bool TryCalculateMinimumInclusiveRect(
        int x, int y, int widthDelta, int heightDelta,
        int mapWidth, int mapHeight, int minimumSize,
        out int newX, out int newY, out int newWidthDelta, out int newHeightDelta,
        out int newSpanW, out int newSpanH)
    {
        newX = x;
        newY = y;
        newWidthDelta = widthDelta;
        newHeightDelta = heightDelta;
        newSpanW = (int)Math.Min((long)Math.Max(1, mapWidth), Math.Abs((long)widthDelta) + 1L);
        newSpanH = (int)Math.Min((long)Math.Max(1, mapHeight), Math.Abs((long)heightDelta) + 1L);

        if (mapWidth <= 0 || mapHeight <= 0) return false;

        int targetSpanW = Math.Min(mapWidth, Math.Max(newSpanW, minimumSize));
        int targetSpanH = Math.Min(mapHeight, Math.Max(newSpanH, minimumSize));
        if (targetSpanW == newSpanW && targetSpanH == newSpanH) return false;

        int x2 = x + widthDelta;
        int y2 = y + heightDelta;
        int minX = Math.Min(x, x2);
        int maxX = Math.Max(x, x2);
        int minY = Math.Min(y, y2);
        int maxY = Math.Max(y, y2);

        // Preserve the current area's approximate center, then clamp the enlarged
        // inclusive endpoints to the real map. A 5-tile span therefore uses a
        // raw RectInt delta of +/-4, matching MENACE's inclusive IsInside helper.
        int centerX = minX + (maxX - minX) / 2;
        int centerY = minY + (maxY - minY) / 2;
        int outMinX = centerX - (targetSpanW - 1) / 2;
        int outMinY = centerY - (targetSpanH - 1) / 2;
        int outMaxX = outMinX + targetSpanW - 1;
        int outMaxY = outMinY + targetSpanH - 1;

        if (outMinX < 0) { outMaxX -= outMinX; outMinX = 0; }
        if (outMinY < 0) { outMaxY -= outMinY; outMinY = 0; }
        if (outMaxX >= mapWidth)
        {
            int shift = outMaxX - (mapWidth - 1);
            outMinX -= shift;
            outMaxX -= shift;
        }
        if (outMaxY >= mapHeight)
        {
            int shift = outMaxY - (mapHeight - 1);
            outMinY -= shift;
            outMaxY -= shift;
        }
        outMinX = Math.Max(0, outMinX);
        outMinY = Math.Max(0, outMinY);
        outMaxX = Math.Min(mapWidth - 1, outMaxX);
        outMaxY = Math.Min(mapHeight - 1, outMaxY);

        bool reverseX = widthDelta < 0;
        bool reverseY = heightDelta < 0;
        newX = reverseX ? outMaxX : outMinX;
        newY = reverseY ? outMaxY : outMinY;
        newWidthDelta = reverseX ? outMinX - outMaxX : outMaxX - outMinX;
        newHeightDelta = reverseY ? outMinY - outMaxY : outMaxY - outMinY;
        newSpanW = Math.Abs(newWidthDelta) + 1;
        newSpanH = Math.Abs(newHeightDelta) + 1;
        return true;
    }

    private static bool TryRewriteRectIntBox(object rect, int x, int y, int width, int height)
    {
        if (rect == null) return false;

        // UnityEngine.RectInt stores these four values directly. Prefer the backing
        // fields so changing x/y does not trigger property side effects on width/height;
        // fall back to the public properties for interop variants that hide the fields.
        bool setX = TrySetMember(rect, "m_XMin", x) || TrySetMember(rect, "x", x);
        bool setY = TrySetMember(rect, "m_YMin", y) || TrySetMember(rect, "y", y);
        bool setW = TrySetMember(rect, "m_Width", width) || TrySetMember(rect, "width", width);
        bool setH = TrySetMember(rect, "m_Height", height) || TrySetMember(rect, "height", height);
        return setX && setY && setW && setH;
    }

    private static void MissionAreaAddToListPrefix(object __instance, object[] __args, out int __state)
    {
        __state = -1;
        if (!Enabled() || __args == null || __args.Length == 0) return;

        bool enemyScope = _enemySpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool playerDeploymentScope = _playerDeploymentAreaScopeDepth > 0;
        bool objectiveTargetScope = _objectiveTargetSpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool evacuationScope = _evacuationAreaScopeDepth > 0;
        bool recoverDropoffScope = IsRecoverDropoffAreaConfig(__instance);
        if (AreaDiagnostics() && _areaGeometryDiagnosticLines < MaxAreaGeometryDiagnosticLines)
        {
            int currentCount = GetCollectionCount(__args[0]);
            object preview = __args.Length > 1 ? __args[1] : null;
            _log.Msg($"{ModName}: AREA DIAG entered type={ToInt(GetMember(__instance, "AreaType"), -1)} " +
                $"scopes=enemy:{enemyScope},deploy:{playerDeploymentScope},target:{objectiveTargetScope},evac:{evacuationScope},recover:{recoverDropoffScope},objective:{_objectiveAreaContext?.GetType().Name ?? "none"} " +
                $"oldCount={currentCount} preview={ToInt(GetMember(preview, "SizeX"), -1)}x{ToInt(GetMember(preview, "SizeZ"), -1)}");
            _areaGeometryDiagnosticLines++;
        }
        if (!enemyScope && !playerDeploymentScope && !objectiveTargetScope && !evacuationScope &&
            !recoverDropoffScope && _objectiveAreaContext == null) return;

        __state = GetCollectionCount(__args[0]);
    }

    private static void MissionAreaAddToListPostfix(object __instance, object[] __args, int __state)
    {
        if (!Enabled() || __state < 0 || __args == null || __args.Length < 2) return;
        bool enemyScope = _enemySpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool deploymentScope = _playerDeploymentAreaScopeDepth > 0;
        bool targetScope = _objectiveTargetSpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool evacuationScope = _evacuationAreaScopeDepth > 0;
        bool recoverScope = IsRecoverDropoffAreaConfig(__instance);
        ObjectiveAreaRole role = GetObjectiveAreaRole(__instance);
        if (role == ObjectiveAreaRole.Spawn) targetScope = true;
        bool inSpawnOrDeployment = enemyScope || deploymentScope || targetScope || evacuationScope || recoverScope;
        if (!inSpawnOrDeployment && role == ObjectiveAreaRole.Unknown)
        {
            if (AreaDiagnostics()) LogUnmodifiedAreaResult(__args[0], __state, -1, -1,
                ToInt(GetMember(__instance, "AreaType"), -1), role, "unresolved objective role or unscoped area");
            return;
        }

        object list = __args[0];
        object preview = __args[1];
        int count = GetCollectionCount(list);
        int mapW = ToInt(GetMember(preview, "SizeX"), TargetSize);
        int mapH = ToInt(GetMember(preview, "SizeZ"), TargetSize);
        int areaType = ToInt(GetMember(__instance, "AreaType"), -1);
        if (count <= __state || mapW <= VanillaSize || mapH <= VanillaSize)
        {
            if (AreaDiagnostics()) LogUnmodifiedAreaResult(list, __state, mapW, mapH, areaType, role,
                count <= __state ? "no rectangles added" : "preview dimensions at vanilla size");
            return;
        }
        bool enlarged = mapW > ZoneReferenceSize && mapH > ZoneReferenceSize;
        // 64x64 must retain the 1.3.12 reference: objective areas and valid
        // PrimaryChunks/Chunk deployment rectangles are never rewritten here.
        // Historical 42->64 map-space SPAWN transformations and evacuation
        // safety are retained separately below.
        if (!enlarged && !inSpawnOrDeployment)
        {
            if (AreaDiagnostics()) LogUnmodifiedAreaResult(list, __state, mapW, mapH, areaType, role,
                "unmodified 64-reference objective area");
            return;
        }
        for (int i = __state; i < count; i++)
        {
            object rect = GetIndexedValue(list, i);
            if (rect == null) continue;
            bool traceArea = AreaDiagnostics() && _areaGeometryDiagnosticLines < MaxAreaGeometryDiagnosticLines;
            string originalArea = traceArea ? DescribeAreaRect(rect) : null;
            try
            {

            // AIZones of type Rect are already transformed exactly once in
            // ObjectiveInitAiZonesPrefix; do not multiply their coordinates again.
            if (role == ObjectiveAreaRole.AI && areaType == 6) continue;

            bool objectiveArea = role == ObjectiveAreaRole.ControlOrExtraction ||
                                 role == ObjectiveAreaRole.Trigger || role == ObjectiveAreaRole.AI;
            bool halfOpen = !objectiveArea;
            bool primaryOffset = areaType == 0 &&
                ((objectiveArea && role == ObjectiveAreaRole.ControlOrExtraction) || deploymentScope);
            string objectiveName = _objectiveAreaContext?.GetType().Name ?? string.Empty;
            // Capture and evacuation already had a 5-tile safety minimum in
            // 1.3.12. Treat that finalized 64x64 size as their baseline.
            int oldReferenceMinimum = (objectiveName.EndsWith("CaptureArea", StringComparison.Ordinal) ||
                objectiveName.EndsWith("EvacuateToArea", StringComparison.Ordinal) || evacuationScope)
                ? MinimumCaptureAreaSize : 0;
            object replacement = null;
            string before = "", after = "";
            bool changed = false;

            if (enlarged && objectiveArea)
            {
                // The same unmodified MissionAreaConfig output drives previews
                // and tactical objective checks. Scale each semantic area once.
                if (areaType == 0 || areaType == 1)
                    changed = TryScaleResolvedAnchoredRect(rect, preview, mapW, mapH,
                        true, primaryOffset, oldReferenceMinimum, out replacement, out before, out after);
                else if (areaType == 6)
                    changed = TryScaleReferenceAuthoredRect(rect, mapW, mapH,
                        out replacement, out before, out after);
                else if (areaType >= 2 && areaType <= 10)
                    changed = TryScaleReferenceBorderRect(rect, areaType, mapW, mapH,
                        true, out replacement, out before, out after);
                if (changed && TrySetIndexedValue(list, i, replacement))
                {
                    _objectiveZonesResized++;
                    if (_objectiveZonesResized == 1 || Debug())
                        _log.Msg($"{ModName}: 64-reference objective areas active ({mapW}x{mapH}): type={areaType} {before} -> {after}.");
                }
                else if (changed && Debug())
                    _log.Warning($"{ModName}: failed to replace objective area {before}.");
                continue;
            }
            if (!inSpawnOrDeployment) continue;

            if (areaType == 0 || areaType == 1)
            {
                if (enlarged)
                {
                    // Scale the NATIVE resolved deployment/spawn/drop-off
                    // footprint, not the entire primary compound. The center
                    // stays on the building/zone that the mission selected.
                    changed = TryScaleResolvedAnchoredRect(rect, preview, mapW, mapH,
                        false, primaryOffset, oldReferenceMinimum, out replacement, out before, out after);
                    if (changed && TrySetIndexedValue(list, i, replacement))
                    {
                        _referenceScaledAnchoredAreas++;
                        if (Debug()) _log.Msg($"{ModName}: anchored area type={areaType} {before} -> {after}.");
                        continue;
                    }
                }
                if (evacuationScope)
                {
                    // Existing 1.3.12 emergency fix: degenerate EVAC rects.
                    if (TryEnsureMinimumAreaRect(rect, mapW, mapH, MinimumEvacuationSize,
                        out object expanded, out before, out after) &&
                        TrySetIndexedValue(list, i, expanded))
                        _evacuationRectsScaled++;
                }
                else if (enlarged &&
                    TryNormalizeSpawnRect(rect, mapW, mapH,
                        recoverScope ? 1 : MinimumAiSpawnAreaSize,
                        out object fixedRect, out before, out after))
                    TrySetIndexedValue(list, i, fixedRect);
                continue;
            }

            if (enlarged && areaType >= 2 && areaType <= 10 && areaType != 6)
            {
                // MissionTemplate.GetBounds was patched together with the
                // pathfinder: native long dimension is now N, but BorderWidth
                // remains a vanilla-template thickness. Enlarge ONLY that
                // narrow dimension to match its 64x64 reference proportion.
                if (TryScaleReferenceBorderRect(rect, areaType, mapW, mapH,
                    false, out replacement, out before, out after) &&
                    TrySetIndexedValue(list, i, replacement))
                {
                    _referenceScaledBorderAreas++;
                    if (Debug()) _log.Msg($"{ModName}: native border thickness {before} -> {after}.");
                }
                continue;
            }
            if (!enlarged && areaType != 6 && (areaType < 2 || areaType > 10)) continue;
            if (enlarged && areaType != 6) continue;

            // Authored Rect spawn/deployment/drop-off zones were already scaled
            // from vanilla 42x42 to 64x64 in 1.3.12. Preserve that 64 baseline,
            // and apply the equivalent 42->N conversion at every larger size.
            int minimum = evacuationScope ? MinimumEvacuationSize :
                (recoverScope || !enlarged ? 1 : MinimumAiSpawnAreaSize);
            if (!TryScaleMissionAreaRect(rect, areaType, mapW, mapH, minimum,
                out replacement, out before, out after)) continue;
            if (!TrySetIndexedValue(list, i, replacement)) continue;
            if (evacuationScope) _evacuationRectsScaled++;
            else if (recoverScope) _recoverDropoffRectsScaled++;
            else if (deploymentScope) _playerDeploymentRectsScaled++;
            else if (targetScope) _objectiveTargetSpawnRectsScaled++;
            else _enemySpawnRectsScaled++;
            if (Debug()) _log.Msg($"{ModName}: authored spawn/drop-off rect {before} -> {after}.");
            }
            finally
            {
                if (traceArea && _areaGeometryDiagnosticLines < MaxAreaGeometryDiagnosticLines)
                {
                    object outcome = GetIndexedValue(list, i);
                    _log.Msg($"{ModName}: AREA DIAG result type={areaType}, role={role}, " +
                        $"scopes=enemy:{enemyScope},deploy:{deploymentScope},target:{targetScope},evac:{evacuationScope},recover:{recoverScope}, " +
                        $"before={originalArea}, after={DescribeAreaRect(outcome)}");
                    _areaGeometryDiagnosticLines++;
                }
            }
        }
    }

    private static void LogUnmodifiedAreaResult(object list, int start, int mapW, int mapH,
        int areaType, ObjectiveAreaRole role, string why)
    {
        if (!AreaDiagnostics() || _areaGeometryDiagnosticLines >= MaxAreaGeometryDiagnosticLines) return;
        int count = GetCollectionCount(list);
        if (count <= start)
        {
            _log.Msg($"{ModName}: AREA DIAG native-only type={areaType}, role={role}, preview={mapW}x{mapH}, " +
                $"reason={why}, count={count}, oldCount={start}, no new rectangles");
            _areaGeometryDiagnosticLines++;
            return;
        }
        for (int i = Math.Max(0, start); i < count && _areaGeometryDiagnosticLines < MaxAreaGeometryDiagnosticLines; i++)
        {
            _log.Msg($"{ModName}: AREA DIAG native-only type={areaType}, role={role}, preview={mapW}x{mapH}, " +
                $"reason={why}, rect={DescribeAreaRect(GetIndexedValue(list, i))}");
            _areaGeometryDiagnosticLines++;
        }
    }

    private static string DescribeAreaRect(object rect)
    {
        if (rect == null) return "<null>";
        try
        {
            return $"({ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), -999)}," +
                $"{ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), -999)}," +
                $"{ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), -999)}," +
                $"{ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), -999)})";
        }
        catch { return "<unreadable>"; }
    }

    private static bool TryScaleMissionAreaRect(object rect, int areaType, int mapWidth, int mapHeight, int minimumSize, out object scaled, out string before, out string after)
    {
        scaled = null;
        before = "<unknown>";
        after = "<unknown>";
        try
        {
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), 0);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), 0);
            int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), 0);
            int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), 0);
            // On certain native generated mission-area paths width/height are
            // zero or signed negative. These are not usable RectInt spawn ranges.
            // Recover their geometric origin first, then yield positive extents.
            long left = Math.Min((long)x, (long)x + w);
            long top = Math.Min((long)y, (long)y + h);
            long spanW = Math.Max(1L, Math.Abs((long)w));
            long spanH = Math.Max(1L, Math.Abs((long)h));
            before = $"type={areaType} ({x},{y},{w},{h})";

            float scaleX = mapWidth / (float)VanillaSize;
            float scaleY = mapHeight / (float)VanillaSize;

            // MissionAreaConfig rectangles are authored in the vanilla 42x42 coordinate space.
            // Scale both their position and footprint into the actual enlarged map. This also
            // preserves edge margins correctly; e.g. a north-edge band keeps the same relative
            // one-tile vanilla inset instead of being shoved to an arbitrary hard edge.
            int newX = (int)Math.Round(left * scaleX);
            int newY = (int)Math.Round(top * scaleY);
            int scaledW = Math.Max(1, (int)Math.Round(spanW * scaleX));
            int scaledH = Math.Max(1, (int)Math.Round(spanH * scaleY));
            int newW = Math.Max(Math.Max(1, minimumSize), scaledW);
            int newH = Math.Max(Math.Max(1, minimumSize), scaledH);

            // If the evacuation minimum makes the rectangle larger than pure proportional
            // scaling would, grow it around the same approximate center instead of only
            // extending to the right/bottom.
            if (newW > scaledW) newX -= (newW - scaledW) / 2;
            if (newH > scaledH) newY -= (newH - scaledH) / 2;

            newW = Math.Min(mapWidth, newW);
            newH = Math.Min(mapHeight, newH);
            newX = Math.Max(0, Math.Min(newX, mapWidth - newW));
            newY = Math.Max(0, Math.Min(newY, mapHeight - newH));

            Type rectType = rect.GetType();
            ConstructorInfo ctor = rectType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 4 && p.All(v => v.ParameterType == typeof(int));
                });
            if (ctor == null) return false;

            scaled = ctor.Invoke(new object[] { newX, newY, newW, newH });
            after = $"({newX},{newY},{newW},{newH}) scale={scaleX:0.###}x{scaleY:0.###}";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: generated-area scaling failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool TryEnsureMinimumAreaRect(object rect, int mapWidth, int mapHeight, int minimumSize, out object expanded, out string before, out string after)
    {
        expanded = null;
        before = "<unknown>";
        after = "<unknown>";
        try
        {
            int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), 0);
            int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), 0);
            int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), 0);
            int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), 0);
            int spanW = (int)Math.Min((long)Math.Max(1, mapWidth), Math.Abs((long)w) + 1L);
            int spanH = (int)Math.Min((long)Math.Max(1, mapHeight), Math.Abs((long)h) + 1L);
            before = $"raw=({x},{y},{w},{h}) span={spanW}x{spanH}";

            if (!TryCalculateMinimumInclusiveRect(x, y, w, h, mapWidth, mapHeight, minimumSize,
                    out int newX, out int newY, out int newW, out int newH, out int newSpanW, out int newSpanH))
                return false;

            Type rectType = rect.GetType();
            ConstructorInfo ctor = rectType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 4 && p.All(v => v.ParameterType == typeof(int));
                });
            if (ctor == null) return false;

            expanded = ctor.Invoke(new object[] { newX, newY, newW, newH });
            after = $"raw=({newX},{newY},{newW},{newH}) span={newSpanW}x{newSpanH}";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: minimum-area expansion failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static int GetCollectionCount(object list)
    {
        if (list == null) return -1;
        if (list is Array managedArray) return managedArray.Length;
        object count = GetMember(list, "Count") ?? GetMember(list, "Length");
        return ToInt(count, -1);
    }

    private static object GetIndexedValue(object list, int index)
    {
        if (list == null) return null;
        try
        {
            if (list is Array array) return array.GetValue(index);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo item = list.GetType().GetProperty("Item", flags);
            if (item != null && item.CanRead) return item.GetValue(list, new object[] { index });

            MethodInfo getter = list.GetType().GetMethod("get_Item", flags, null, new[] { typeof(int) }, null);
            return getter?.Invoke(list, new object[] { index });
        }
        catch { return null; }
    }

    private static bool TrySetIndexedValue(object list, int index, object value)
    {
        if (list == null || value == null) return false;
        try
        {
            if (list is Array array) { array.SetValue(value, index); return true; }
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo item = list.GetType().GetProperty("Item", flags);
            if (item != null && item.CanWrite)
            {
                item.SetValue(list, value, new object[] { index });
                return true;
            }

            MethodInfo setter = list.GetType().GetMethods(flags)
                .FirstOrDefault(m => m.Name == "set_Item" && m.GetParameters().Length == 2);
            if (setter != null)
            {
                setter.Invoke(list, new object[] { index, value });
                return true;
            }
        }
        catch { }
        return false;
    }

    // Unity TerrainData.GetHoles() returns [height,width] (indexed [z,x]).
    // MENACE v0.7.15's native Map.AddHole incorrectly iterates [x,z] and
    // therefore throws IndexOutOfRangeException for non-square regions.
    // Preserve the game's exact sampling/clipping and only fix the 2D indexing.
    private static void PatchTerrainHoleIndexing(HarmonyLib.Harmony harmony)
    {
        if (_mapType == null)
        {
            _log.Warning($"{ModName}: Map type missing; terrain hole indexing fix unavailable.");
            return;
        }

        MethodInfo addHole = _mapType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "AddHole" && m.GetParameters().Length == 2);
        if (addHole == null)
        {
            _log.Warning($"{ModName}: Map.AddHole(Vector3,float) missing; terrain hole indexing fix unavailable.");
            return;
        }

        harmony.Patch(addHole,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MapAddHolePrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: terrain-hole array indexing safety active ([z,x] for Unity GetHoles/SetHoles).");
    }

    private static bool MapAddHolePrefix(Il2CppMenace.Tactical.Map __instance, Vector3 __0, float __1)
    {
        if (!Enabled() || __instance == null) return true;
        if (__1 <= 0f) return false; // Native Map.AddHole also has no area to modify.

        try
        {
            Terrain terrain = __instance.GetTerrain();
            TerrainData data = terrain == null ? null : terrain.terrainData;
            if (data == null) return true;

            int last = data.holesResolution - 1;
            float worldWidth = __instance.TerrainBounds.width;
            if (last < 1 || worldWidth <= 0f || !float.IsFinite(worldWidth) || !float.IsFinite(__1) ||
                !float.IsFinite(__0.x) || !float.IsFinite(__0.z))
                return true;

            // These are the same (inclusive-clamped, exclusive-end) coordinates
            // computed by the original native Map.AddHole implementation
            // (Unity Mathf.RoundToInt, not FloorToInt).
            float scale = last / worldWidth;
            int startX = Math.Clamp(Mathf.RoundToInt((__0.x - __1) * scale), 0, last);
            int startZ = Math.Clamp(Mathf.RoundToInt((__0.z - __1) * scale), 0, last);
            int endX = Math.Clamp(Mathf.RoundToInt((__0.x + __1) * scale), 0, last);
            int endZ = Math.Clamp(Mathf.RoundToInt((__0.z + __1) * scale), 0, last);
            int width = endX - startX;
            int height = endZ - startZ;
            if (width <= 0 || height <= 0) return false;

            // Il2CppInterop exposes Unity's bool[,] as Il2CppObjectBase, not
            // as a CLR 2D array. Read its validated x64 IL2CPP array layout:
            // object header [0..15], bounds pointer [+16], length [+24], data [+32].
            // We confirmed these offsets from this game's own native AddHole body.
            var holes = data.GetHoles(startX, startZ, width, height);
            if (holes == null || holes.Pointer == IntPtr.Zero) return true;
            IntPtr arrayPointer = holes.Pointer;
            IntPtr boundsPointer = Marshal.ReadIntPtr(arrayPointer, 0x10);
            if (boundsPointer == IntPtr.Zero) return true;
            long total = Marshal.ReadInt64(arrayPointer, 0x18);
            long nativeRows = Marshal.ReadInt64(boundsPointer, 0);
            long nativeColumns = Marshal.ReadInt64(boundsPointer, 0x10);
            if (total != (long)width * height || nativeRows != height || nativeColumns != width ||
                total <= 0 || total > 1_000_000)
            {
                WarnTerrainHoleFailure($"unexpected IL2CPP bool[,] shape: array={nativeRows}x{nativeColumns}, expected={height}x{width}, elements={total}");
                return true;
            }

            byte[] values = new byte[(int)total];
            IntPtr bytesPointer = IntPtr.Add(arrayPointer, 0x20);
            Marshal.Copy(bytesPointer, values, 0, values.Length);
            float radiusSquared = __1 * __1;
            for (int x = 0; x < width; x++)
            {
                float deltaX = (startX + x) / scale - __0.x;
                float dxSquared = deltaX * deltaX;
                for (int z = 0; z < height; z++)
                {
                    float deltaZ = (startZ + z) / scale - __0.z;
                    if (dxSquared + deltaZ * deltaZ < radiusSquared)
                        values[z * width + x] = 0; // [z,x], not native's incorrect [x,z]
                }
            }

            Marshal.Copy(values, 0, bytesPointer, values.Length);
            data.SetHoles(startX, startZ, holes);
            if (Debug())
                _log.Msg($"{ModName}: applied terrain hole at ({__0.x:0.##},{__0.z:0.##}), radius={__1:0.##}, area={width}x{height} ({startX},{startZ}).");
            return false; // Correctly applied: do not run buggy native Map.AddHole.
        }
        catch (Exception ex)
        {
            WarnTerrainHoleFailure($"managed Map.AddHole replacement failed: {ex.GetType().Name}: {ex.Message}");
            return true; // Keep vanilla behavior if this game's Terrain API changes.
        }
    }

    private static void WarnTerrainHoleFailure(string message)
    {
        if (_terrainHoleFailureLogged && !Debug()) return;
        _terrainHoleFailureLogged = true;
        _log.Warning($"{ModName}: {message}. Original Map.AddHole will run as fallback.");
    }

    private static void PatchMapElevationTextureResize(HarmonyLib.Harmony harmony)
    {
        if (_mapElevationTextureType == null || _tacticalConfigType == null)
        {
            _log.Warning($"{ModName}: MapElevationTexture/TacticalConfig type not found; elevation render-texture size correction unavailable.");
            return;
        }

        MethodInfo resize = _mapElevationTextureType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
            {
                if (m.Name != "Resize") return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int);
            });
        if (resize == null)
        {
            _log.Warning($"{ModName}: MapElevationTexture.Resize(int,int,MapBlueprint) not found; elevation render-texture size correction unavailable.");
            return;
        }

        harmony.Patch(
            resize,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MapElevationTextureResizePrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(resize)} so TacticalConfig.MapElevationTexture follows the actual map dimensions before vanilla elevation setup.");
    }

    private static void MapElevationTextureResizePrefix(object[] __args)
    {
        if (!Enabled() || __args == null || __args.Length < 2) return;

        try
        {
            int sizeX = ToInt(__args[0], -1);
            int sizeZ = ToInt(__args[1], -1);
            if (sizeX <= 0 || sizeZ <= 0) return;

            int desiredWidth = checked(sizeX * ElevationPixelsPerTile);
            int desiredHeight = checked(sizeZ * ElevationPixelsPerTile);

            MethodInfo getConfig = _tacticalConfigType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "Get" && m.GetParameters().Length == 0);
            object config = getConfig?.Invoke(null, null);
            object renderTexture = GetMember(config, "MapElevationTexture");
            if (renderTexture == null)
            {
                if (Debug()) _log.Warning($"{ModName}: TacticalConfig.MapElevationTexture was null before MapElevationTexture.Resize({sizeX},{sizeZ}).");
                return;
            }

            int beforeWidth = ToInt(GetMember(renderTexture, "width") ?? GetMember(renderTexture, "Width"), -1);
            int beforeHeight = ToInt(GetMember(renderTexture, "height") ?? GetMember(renderTexture, "Height"), -1);
            if (beforeWidth == desiredWidth && beforeHeight == desiredHeight) return;

            // MENACE ships this shared RenderTexture at 42*32 = 1344 pixels. Vanilla
            // MapElevationTexture.Resize correctly computes 64*32 = 2048 for an enlarged
            // blueprint, but only validates the shared TacticalConfig asset and does not
            // resize it. That leaves a 2048 Texture2D paired with a 1344 RenderTexture.
            // Resize the same runtime object in place so all existing references remain valid.
            InvokeNoArg(renderTexture, "Release");
            bool setWidth = TrySetMember(renderTexture, "width", desiredWidth) || TrySetMember(renderTexture, "Width", desiredWidth);
            bool setHeight = TrySetMember(renderTexture, "height", desiredHeight) || TrySetMember(renderTexture, "Height", desiredHeight);
            if (!setWidth || !setHeight)
            {
                // Do not leave the shared asset released or half-resized if this game build
                // changes the RenderTexture wrapper shape. Best-effort restore the dimensions
                // that were present when we entered the hook, then recreate it.
                if (beforeWidth > 0)
                    _ = TrySetMember(renderTexture, "width", beforeWidth) || TrySetMember(renderTexture, "Width", beforeWidth);
                if (beforeHeight > 0)
                    _ = TrySetMember(renderTexture, "height", beforeHeight) || TrySetMember(renderTexture, "Height", beforeHeight);
                InvokeNoArg(renderTexture, "Create");
                _log.Warning($"{ModName}: could not resize TacticalConfig.MapElevationTexture {beforeWidth}x{beforeHeight} -> {desiredWidth}x{desiredHeight}; restored the previous target as far as possible and left vanilla behavior intact.");
                return;
            }

            InvokeNoArg(renderTexture, "Create");
            int afterWidth = ToInt(GetMember(renderTexture, "width") ?? GetMember(renderTexture, "Width"), -1);
            int afterHeight = ToInt(GetMember(renderTexture, "height") ?? GetMember(renderTexture, "Height"), -1);
            if (afterWidth != desiredWidth || afterHeight != desiredHeight)
            {
                InvokeNoArg(renderTexture, "Release");
                if (beforeWidth > 0)
                    _ = TrySetMember(renderTexture, "width", beforeWidth) || TrySetMember(renderTexture, "Width", beforeWidth);
                if (beforeHeight > 0)
                    _ = TrySetMember(renderTexture, "height", beforeHeight) || TrySetMember(renderTexture, "Height", beforeHeight);
                InvokeNoArg(renderTexture, "Create");
                _log.Warning($"{ModName}: elevation render-texture resize did not stick: requested {desiredWidth}x{desiredHeight}, read back {afterWidth}x{afterHeight}; restored the previous target as far as possible.");
                return;
            }

            _elevationRenderTextureCorrections++;
            if (_elevationRenderTextureCorrections <= 4 || Debug())
                _log.Msg($"{ModName}: ELEVATION RENDER TEXTURE resized {beforeWidth}x{beforeHeight} -> {afterWidth}x{afterHeight} for {sizeX}x{sizeZ} map.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: elevation render-texture correction failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PatchCaptureAreaMinimum(HarmonyLib.Harmony harmony)
    {
        if (_captureAreaType == null)
        {
            if (Debug()) _log.Warning($"{ModName}: CaptureArea type not found; 5x5 capture-area minimum unavailable.");
            return;
        }

        MethodInfo init = _captureAreaType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "Init" && m.GetParameters().Length == 4);
        if (init == null)
        {
            if (Debug()) _log.Warning($"{ModName}: CaptureArea.Init/4 not found; 5x5 capture-area minimum unavailable.");
            return;
        }

        harmony.Patch(
            init,
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(CaptureAreaInitPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(init)} for minimum 5x5 finalized capture areas on enlarged maps.");
    }

    private static void CaptureAreaInitPostfix(object __instance, object[] __args)
    {
        if (!Enabled() || __instance == null || __args == null || __args.Length == 0) return;

        try
        {
            object preview = __args[0];
            int mapWidth = ToInt(GetMember(preview, "SizeX"), TargetSize);
            int mapHeight = ToInt(GetMember(preview, "SizeZ"), TargetSize);
            if (mapWidth <= VanillaSize || mapHeight <= VanillaSize) return;

            object areas = GetMember(__instance, "m_CaptureAreaRects");
            int count = GetCollectionCount(areas);
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                object rect = GetIndexedValue(areas, i);
                if (rect == null) continue;

                if (!TryEnsureMinimumAreaRect(rect, mapWidth, mapHeight, MinimumCaptureAreaSize,
                        out object expanded, out string before, out string after))
                    continue;

                if (!TrySetIndexedValue(areas, i, expanded))
                {
                    if (Debug()) _log.Warning($"{ModName}: CAPTURE AREA could not replace RectInt at index {i}: {before}.");
                    continue;
                }

                _captureAreaCorrections++;
                if (Debug())
                    _log.Msg($"{ModName}: CAPTURE AREA minimum footprint {before} -> {after}; minimum={MinimumCaptureAreaSize}x{MinimumCaptureAreaSize}.");
            }
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: capture-area minimum fix failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PatchObjectiveAiZoneScaling(HarmonyLib.Harmony harmony)
    {
        if (_objectiveType == null)
        {
            if (Debug()) _log.Warning($"{ModName}: Objective type not found; authored AI-zone scaling unavailable.");
            return;
        }

        MethodInfo init = _objectiveType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "Init" && m.GetParameters().Length == 4);
        if (init == null)
        {
            if (Debug()) _log.Warning($"{ModName}: Objective.Init/4 not found; authored AI-zone scaling unavailable.");
            return;
        }

        harmony.Patch(
            init,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(ObjectiveInitAiZonesPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: hooked {FormatMethod(init)} for proportional map-space ObjectiveConfig.AIZones scaling.");
    }

    private static void ObjectiveInitAiZonesPrefix(object __instance, object[] __args)
    {
        if (!Enabled() || __instance == null || __args == null || __args.Length == 0) return;

        try
        {
            object preview = __args[0];
            int mapWidth = ToInt(GetMember(preview, "SizeX"), TargetSize);
            int mapHeight = ToInt(GetMember(preview, "SizeZ"), TargetSize);
            if (mapWidth <= VanillaSize || mapHeight <= VanillaSize) return;

            object config = InvokeNoArg(__instance, "GetConfig") ?? GetMember(__instance, "m_Config");
            if (config == null) return;

            long key = GetStableObjectKey(config);
            if (key != 0 && !_scaledObjectiveAiZoneConfigs.Add(key)) return;

            object zones = GetMember(config, "AIZones");
            int zoneCount = GetCollectionCount(zones);
            if (zoneCount <= 0) return;

            float scaleX = mapWidth / (float)VanillaSize;
            float scaleY = mapHeight / (float)VanillaSize;

            for (int i = 0; i < zoneCount; i++)
            {
                object zone = GetIndexedValue(zones, i);
                object area = GetMember(zone, "Area");
                if (area == null) continue;

                int areaType = ToInt(GetMember(area, "AreaType"), -1);
                // PrimaryChunks/Chunk are resolved against generated setpieces and border
                // area types already use the live map dimensions. Only authored Rect zones
                // need 42x42 -> enlarged-map coordinate conversion.
                if (areaType != 6) continue;

                object rect = GetMember(area, "Rect");
                if (rect == null) continue;

                int x = ToInt(GetMember(rect, "m_XMin") ?? GetMember(rect, "x"), 0);
                int y = ToInt(GetMember(rect, "m_YMin") ?? GetMember(rect, "y"), 0);
                int w = ToInt(GetMember(rect, "m_Width") ?? GetMember(rect, "width"), 0);
                int h = ToInt(GetMember(rect, "m_Height") ?? GetMember(rect, "height"), 0);
                if (w == 0 || h == 0) continue;

                int newX = (int)Math.Round(x * scaleX);
                int newY = (int)Math.Round(y * scaleY);
                int newW = (int)Math.Round(w * scaleX);
                int newH = (int)Math.Round(h * scaleY);
                if (newX == x && newY == y && newW == w && newH == h) continue;

                if (!TryRewriteRectIntBox(rect, newX, newY, newW, newH) || !TrySetMember(area, "Rect", rect))
                {
                    if (Debug()) _log.Warning($"{ModName}: OBJECTIVE AI ZONE could not scale rect #{i} raw=({x},{y},{w},{h}).");
                    continue;
                }

                _objectiveAiZoneCorrections++;
                if (Debug())
                    _log.Msg($"{ModName}: OBJECTIVE AI ZONE scaled raw=({x},{y},{w},{h}) -> ({newX},{newY},{newW},{newH}) within {mapWidth}x{mapHeight}; rect #{i}.");
            }
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: objective AI-zone scaling failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static long GetStableObjectKey(object obj)
    {
        if (obj == null) return 0;
        if (TryGetNativePointer(obj, out long pointer) && pointer != 0) return pointer;
        return unchecked((long)0x8000000000000000UL | (uint)RuntimeHelpers.GetHashCode(obj));
    }

    private static void PatchBaseTileHighlighterInit(HarmonyLib.Harmony harmony)
    {
        if (_baseTileHighlighterType == null)
        {
            _log.Warning($"{ModName}: BaseTileHighlighter type not found; movement/range highlight fix unavailable.");
            return;
        }

        MethodInfo init = _baseTileHighlighterType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
            {
                if (m.Name != "Init") return false;
                ParameterInfo[] p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int);
            });

        if (init == null)
        {
            _log.Warning($"{ModName}: BaseTileHighlighter.Init(int,int) not found; movement/range highlight fix unavailable.");
            return;
        }

        _baseTileHighlighterInitMethod = init;

        harmony.Patch(
            init,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(BaseTileHighlighterInitPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(BaseTileHighlighterInitPostfix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked {FormatMethod(init)}; vanilla Init is allowed to complete, then Larger Maps safely reruns native Init at the actual enlarged map size when needed.");
    }

    private static void PatchTerrainGenerateStateMachine(HarmonyLib.Harmony harmony)
    {
        if (_terrainGeneratorType == null)
        {
            _log.Error($"{ModName}: TerrainMapGenerator type not found.");
            return;
        }

        Type nested = _terrainGeneratorType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.IndexOf("Generate", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 t.Name.IndexOf("d__", StringComparison.OrdinalIgnoreCase) >= 0);
        if (nested == null)
        {
            _log.Error($"{ModName}: TerrainMapGenerator.Generate state machine not found.");
            return;
        }

        MethodInfo moveNext = nested.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "MoveNext" && m.GetParameters().Length == 0);
        if (moveNext == null)
        {
            _log.Error($"{ModName}: TerrainMapGenerator.Generate MoveNext not found.");
            return;
        }

        harmony.Patch(
            moveNext,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainGenerateMoveNextPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainGenerateMoveNextPostfix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked targeted generator coroutine {nested.FullName}.MoveNext only.");
    }

    private static void PatchMapGenerateStateMachine(HarmonyLib.Harmony harmony)
    {
        if (_mapType == null)
        {
            _log.Warning($"{ModName}: Map type not found; GenerateMap coroutine diagnostics unavailable.");
            return;
        }

        Type nested = _mapType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.IndexOf("GenerateMap", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 t.Name.IndexOf("d__", StringComparison.OrdinalIgnoreCase) >= 0);
        if (nested == null)
        {
            _log.Warning($"{ModName}: Map.GenerateMap state machine not found; map-generation completion diagnostics unavailable.");
            return;
        }

        MethodInfo moveNext = nested.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "MoveNext" && m.GetParameters().Length == 0 && m.ReturnType == typeof(bool));
        if (moveNext == null)
        {
            _log.Warning($"{ModName}: Map.GenerateMap MoveNext not found; map-generation completion diagnostics unavailable.");
            return;
        }

        harmony.Patch(
            moveNext,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MapGenerateMoveNextPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MapGenerateMoveNextPostfix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked targeted tactical map coroutine {nested.FullName}.MoveNext for state/callback diagnostics.");
    }

    private static void MapGenerateMoveNextPrefix(object __instance)
    {
        if (!Enabled() || !Debug() || __instance == null) return;
        _mapGenerateMoveNextCalls++;
        _mapGenerateStateBeforeCall = ReadCoroutineState(__instance);
        _mapGenerateMoveStartMs = Environment.TickCount64;
    }

    private static void MapGenerateMoveNextPostfix(object __instance, bool __result)
    {
        if (!Enabled() || !Debug() || __instance == null) return;
        try
        {
            int after = ReadCoroutineState(__instance);
            object current = GetMember(__instance, "__2__current");
            if (current == null) current = GetMember(__instance, "<>2__current");
            string currentType = DescribeYieldObject(current);
            object callback = GetMember(__instance, "_onReadyCallback");
            object preview = GetMember(__instance, "_previewResult");
            long elapsed = Math.Max(0, Environment.TickCount64 - _mapGenerateMoveStartMs);

            bool changed = _mapGenerateStateBeforeCall != after || after != _mapGenerateLastState;
            _mapGenerateLastState = after;
            _mapGenerateLastCurrent = currentType;
            _mapGenerateLastStepMs = Environment.TickCount64;
            if (!__result) _mapGenerateCompleted = true;

            if (changed || _mapGenerateMoveNextCalls <= 2 || !__result || elapsed >= 250)
            {
                _log.Msg($"{ModName}: MAPGEN STEP call={_mapGenerateMoveNextCalls} state={FormatState(_mapGenerateStateBeforeCall)}->{FormatState(after)} result={__result} elapsed={elapsed}ms current={currentType} callback={(callback == null ? "<null>" : "present")} preview={DescribePreviewCompact(preview)}.");
            }

            if (!__result)
                _log.Msg($"{ModName}: MAPGEN COMPLETE call={_mapGenerateMoveNextCalls} finalState={FormatState(after)} callback={(callback == null ? "<null>" : "present")} onMapReadyCalls={_tacticalStateOnMapReadyCalls} deployCalls={_deployEntitiesCalls} startCalls={_tacticalManagerStartCalls}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: MAPGEN MoveNext diagnostic failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PatchTerrainPointSpawnerHeightFix(HarmonyLib.Harmony harmony)
    {
        if (_terrainPointSpawnerType == null)
        {
            _log.Warning($"{ModName}: TerrainPointSpawner type not found; decorative/ground terrain height correction unavailable.");
            return;
        }

        MethodInfo process = _terrainPointSpawnerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "ProcessTerrainPoints" && m.GetParameters().Length == 0);
        if (process == null)
        {
            _log.Warning($"{ModName}: TerrainPointSpawner.ProcessTerrainPoints/0 not found; decorative/ground terrain height correction unavailable.");
            return;
        }

        harmony.Patch(
            process,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainPointSpawnerProcessPrefix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked {FormatMethod(process)} with enlarged-map decorative/ground TerrainPoint height reprojection.");
    }

    private static void PatchFoliagePipelineDiagnostics(HarmonyLib.Harmony harmony)
    {
        try
        {
            if (_groundCoverScatterType != null)
            {
                MethodInfo modify = _groundCoverScatterType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "ModifyBlueprint" && m.GetParameters().Length == 2 && m.ReturnType == typeof(bool));
                if (modify != null)
                {
                    harmony.Patch(
                        modify,
                        prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GroundCoverScatterModifyBlueprintPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                        postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GroundCoverScatterModifyBlueprintPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                    _log.Msg($"{ModName}: hooked {FormatMethod(modify)} for 0.28 Ground-cover creation diagnostics.");
                }
                else if (Debug())
                {
                    _log.Warning($"{ModName}: GroundCoverScatter.ModifyBlueprint/2 not found; Ground point creation diagnostics unavailable.");
                }
            }
            else if (Debug())
            {
                _log.Warning($"{ModName}: GroundCoverScatter type not found; Ground point creation diagnostics unavailable.");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: 0.28 GroundCoverScatter diagnostic hook failed safely: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            if (_terrainPointSpawnerType != null)
            {
                MethodInfo initialize = _terrainPointSpawnerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 4);
                if (initialize != null)
                {
                    harmony.Patch(
                        initialize,
                        prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainPointSpawnerInitializePrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                        postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainPointSpawnerInitializePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                    _log.Msg($"{ModName}: hooked {FormatMethod(initialize)} for 0.28 TerrainPointSpawner diagnostics.");
                }
                else if (Debug())
                {
                    _log.Warning($"{ModName}: TerrainPointSpawner.Initialize/4 not found; point-spawner diagnostics incomplete.");
                }

                MethodInfo process = _terrainPointSpawnerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "ProcessTerrainPoints" && m.GetParameters().Length == 0);
                if (process != null)
                {
                    // The functional prefix is installed unconditionally by
                    // PatchTerrainPointSpawnerHeightFix. Debug mode only adds the
                    // diagnostic postfix here, avoiding a duplicate gameplay prefix.
                    harmony.Patch(
                        process,
                        postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainPointSpawnerProcessPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                    _log.Msg($"{ModName}: added {FormatMethod(process)} TerrainPoint debug postfix.");
                }
                else if (Debug())
                {
                    _log.Warning($"{ModName}: TerrainPointSpawner.ProcessTerrainPoints/0 not found; point-routing diagnostics incomplete.");
                }
            }
            else if (Debug())
            {
                _log.Warning($"{ModName}: TerrainPointSpawner type not found; point-spawner diagnostics unavailable.");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: 0.28 TerrainPointSpawner diagnostic hooks failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void GroundCoverScatterModifyBlueprintPrefix(object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_groundCoverScatterCalls;
        try
        {
            MapBlueprint blueprint = (__args != null && __args.Length > 1) ? __args[1] as MapBlueprint : null;
            _log.Msg($"{ModName}: FOLIAGE GroundCoverScatter ENTER call={call} {SummarizeBlueprintTerrainPoints(blueprint)}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: FOLIAGE GroundCoverScatter ENTER probe failed call={call}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void GroundCoverScatterModifyBlueprintPostfix(object[] __args, bool __result)
    {
        if (!Enabled() || !Debug()) return;
        try
        {
            MapBlueprint blueprint = (__args != null && __args.Length > 1) ? __args[1] as MapBlueprint : null;
            _log.Msg($"{ModName}: FOLIAGE GroundCoverScatter EXIT call={_groundCoverScatterCalls} result={__result} {SummarizeBlueprintTerrainPoints(blueprint)}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: FOLIAGE GroundCoverScatter EXIT probe failed call={_groundCoverScatterCalls}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TerrainPointSpawnerInitializePrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !Debug()) return;
        int call = ++_terrainPointSpawnerInitCalls;
        try
        {
            MapBlueprint blueprint = (__args != null && __args.Length > 0) ? __args[0] as MapBlueprint : null;
            TerrainData terrainData = (__args != null && __args.Length > 2) ? __args[2] as TerrainData : null;
            _log.Msg($"{ModName}: FOLIAGE TerrainPointSpawner.Initialize ENTER call={call} {SummarizeBlueprintTerrainPoints(blueprint)} terrainData={DescribeTerrainData(terrainData)}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: FOLIAGE TerrainPointSpawner.Initialize ENTER probe failed call={call}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TerrainPointSpawnerInitializePostfix(object __instance)
    {
        if (!Enabled() || !Debug()) return;
        try
        {
            MapBlueprint blueprint = GetMember(__instance, "m_Blueprint") as MapBlueprint;
            TerrainData terrainData = GetMember(__instance, "m_TerrainData") as TerrainData;
            long gpuPoints = CountMember(__instance, "m_GPUTerrainPoints");
            long rendererKeys = CountMember(__instance, "m_RendererKeys");
            _log.Msg($"{ModName}: FOLIAGE TerrainPointSpawner.Initialize EXIT call={_terrainPointSpawnerInitCalls} gpuPoints={gpuPoints} rendererKeys={rendererKeys} {SummarizeBlueprintTerrainPoints(blueprint)} terrainData={DescribeTerrainData(terrainData)}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: FOLIAGE TerrainPointSpawner.Initialize EXIT probe failed call={_terrainPointSpawnerInitCalls}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TerrainPointSpawnerProcessPrefix(object __instance)
    {
        if (!Enabled()) return true;

        int call = ++_terrainPointProcessCalls;
        MapBlueprint blueprint = null;
        TerrainData terrainData = null;

        try
        {
            blueprint = GetMember(__instance, "m_Blueprint") as MapBlueprint;
            terrainData = GetMember(__instance, "m_TerrainData") as TerrainData;

            if (Debug())
                _log.Msg($"{ModName}: FOLIAGE TerrainPointSpawner.Process ENTER call={call} {SummarizeBlueprintTerrainPoints(blueprint)} terrainData={DescribeTerrainData(terrainData)}.");

            if (blueprint == null || terrainData == null ||
                (blueprint.Width <= VanillaSize && blueprint.Height <= VanillaSize))
                return true;

            // 0.27 proved that Harmony's ref-TerrainPoint bridge for the native
            // TerrainUtilities.TryResolveTerrainPoint call exposes a zeroed struct,
            // even though the same points are correct in MapBlueprint.TerrainPoints.
            // Replace the caller instead: read the real list values, reproduce the
            // vanilla resolver logic in managed code, and feed the resolved points
            // directly into the GPU list before Initialize renders it.
            var gpuPoints = GetMember(__instance, "m_GPUTerrainPoints") as Il2CppSystem.Collections.Generic.List<TerrainPoint>;
            if (gpuPoints == null)
                throw new InvalidOperationException("m_GPUTerrainPoints was not available as List<TerrainPoint>");

            var terrainPoints = blueprint.TerrainPoints;
            if (terrainPoints == null)
                throw new InvalidOperationException("MapBlueprint.TerrainPoints is null");

            gpuPoints.Clear();

            int sourceCount = terrainPoints.Count;
            int added = 0;
            int skippedGroup = 0;

            for (int i = 0; i < sourceCount; i++)
            {
                TerrainPoint source = terrainPoints[i];
                if (!TryResolveTerrainPointManaged(source, blueprint, terrainData, 45f, out TerrainPoint resolved))
                    continue;

                int group = resolved.AssetTypeID / 1000;
                if (group == 0 || group == 2)
                {
                    gpuPoints.Add(resolved);
                    added++;
                }
                else
                {
                    skippedGroup++;
                }
            }

            if (Debug())
                _log.Msg($"{ModName}: TERRAIN SPAWNER MANAGED call={call} source={sourceCount} gpuAdded={added} skippedGroup={skippedGroup} slopeReject={_terrainPointRejectedSlope} infillReject={_terrainPointRejectedInfill} failures={_terrainPointResolverFailures}.");

            return false; // skip native ProcessTerrainPoints and its unsafe 1:1 height sampling
        }
        catch (Exception ex)
        {
            _terrainPointResolverFailures++;
            _log.Error($"{ModName}: managed TerrainPointSpawner.Process replacement failed: {ex.GetType().Name}: {ex.Message}");
            // Do not fall back to native on enlarged maps: that path can index past
            // the fixed 513x513 cached heightmap. Leave the GPU list as-is instead.
            return blueprint == null || (blueprint.Width <= VanillaSize && blueprint.Height <= VanillaSize);
        }
    }

    private static bool TryResolveTerrainPointManaged(
        TerrainPoint source,
        MapBlueprint blueprint,
        TerrainData terrainData,
        float maxSlope,
        out TerrainPoint resolved)
    {
        resolved = source;
        _terrainPointResolveCalls++;

        try
        {
            Vector3 position = source.Position;
            float x = position.x;
            float y = position.y;
            float z = position.z;
            ushort assetTypeId = source.AssetTypeID;
            int group = assetTypeId / 1000;
            ushort rawFlags = (ushort)source.Flags;

            IncrementCounter(_resolverAssetTypeCounts, assetTypeId);
            IncrementCounter(_resolverFlagCounts, rawFlags);

            bool canBeReprojected = (source.Flags & TerrainPointFlags.CanBeReprojected) != 0;
            bool isInfill = (source.Flags & TerrainPointFlags.IsInfill) != 0;

            _terrainPointManagedHandled++;
            if (group == 0) _terrainPointDecorativeHandled++;
            else if (group == 1) _terrainPointGameplayHandled++;
            else if (group == 2) _terrainPointGroundHandled++;

            float border = blueprint.BorderWorldUnits;
            float totalWorld = blueprint.TotalWorldUnits;
            int heightMapRes = blueprint.HeightMapRes;
            if (totalWorld <= 0.001f || heightMapRes < 2)
                throw new InvalidOperationException($"invalid terrain dimensions totalWorld={totalWorld}, heightMapRes={heightMapRes}");

            // Exact native rule: non-infill Decorative and Ground points are
            // slope-filtered in normalized Unity terrain coordinates.
            if (!isInfill && (group == 0 || group == 2))
            {
                float nx = (x + border) / totalWorld;
                float nz = (z + border) / totalWorld;
                if (terrainData.GetSteepness(nx, nz) > maxSlope)
                {
                    _terrainPointRejectedSlope++;
                    return false;
                }
            }

            // Exact native rule for infill points.
            if (isInfill)
            {
                if (!blueprint.TryWorldToTilePos(new Vector2(x, z), out Vector2Int tilePos) ||
                    !blueprint.IsBlocked(tilePos.x, tilePos.y))
                {
                    _terrainPointRejectedInfill++;
                    return false;
                }
            }

            if (canBeReprojected || isInfill)
            {
                float heightScale = (heightMapRes - 1f) / totalWorld;
                float sampleX = Math.Clamp((x + border) * heightScale, 0f, heightMapRes - 1f);
                float sampleZ = Math.Clamp((z + border) * heightScale, 0f, heightMapRes - 1f);
                float newY = blueprint.GetWorldHeightBilinear(sampleX, sampleZ);

                resolved = new TerrainPoint(
                    new Vector3(x, newY, z),
                    source.Orientation,
                    source.Scale,
                    source.AssetTypeID,
                    source.Flags);

                if (Debug() && _terrainPointFixLogs < 24)
                {
                    _terrainPointFixLogs++;
                    string groupName = group == 2 ? "Ground" : (group == 1 ? "Gameplay" : "Decorative");
                    _log.Msg($"{ModName}: TERRAIN MANAGED FIX #{_terrainPointFixLogs} group={groupName} asset={assetTypeId} world=({x:0.###},{z:0.###}) sample=({sampleX:0.###},{sampleZ:0.###}) y={y:0.###}->{newY:0.###} totalWorld={totalWorld:0.###} res={heightMapRes}.");
                }
            }

            _terrainPointManagedAccepted++;
            return true;
        }
        catch (Exception ex)
        {
            _terrainPointResolverFailures++;
            if (Debug() && _terrainPointResolverFailures <= 8)
                _log.Warning($"{ModName}: managed TerrainPoint resolve failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void TerrainPointSpawnerProcessPostfix(object __instance)
    {
        if (!Enabled() || !Debug()) return;
        try
        {
            MapBlueprint blueprint = GetMember(__instance, "m_Blueprint") as MapBlueprint;
            TerrainData terrainData = GetMember(__instance, "m_TerrainData") as TerrainData;
            long gpuPoints = CountMember(__instance, "m_GPUTerrainPoints");
            long rendererKeys = CountMember(__instance, "m_RendererKeys");
            _log.Msg($"{ModName}: FOLIAGE TerrainPointSpawner.Process EXIT call={_terrainPointProcessCalls} gpuPoints={gpuPoints} rendererKeys={rendererKeys} {SummarizeBlueprintTerrainPoints(blueprint)} terrainData={DescribeTerrainData(terrainData)}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: FOLIAGE TerrainPointSpawner.Process EXIT probe failed call={_terrainPointProcessCalls}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string SummarizeBlueprintTerrainPoints(MapBlueprint blueprint)
    {
        if (blueprint == null) return "blueprint=<null>";

        try
        {
            var terrainPoints = blueprint.TerrainPoints;
            var gameplayPoints = blueprint.GameplayTerrainPoints;
            int terrainCount = terrainPoints == null ? -1 : terrainPoints.Count;
            int gameplayCount = gameplayPoints == null ? -1 : gameplayPoints.Count;

            string terrain = SummarizeTerrainPointSequence(terrainCount, i => terrainPoints[i]);
            string gameplay = SummarizeTerrainPointSequence(gameplayCount, i => gameplayPoints[i]);
            return $"blueprint={blueprint.Width}x{blueprint.Height} TerrainPoints={terrain} GameplayTerrainPoints={gameplay}";
        }
        catch (Exception ex)
        {
            return $"blueprint={Safe(GetMember(blueprint, "Width"))}x{Safe(GetMember(blueprint, "Height"))} pointSummary=<failed {ex.GetType().Name}>";
        }
    }

    private static string SummarizeTerrainPointSequence(int count, Func<int, TerrainPoint> getter)
    {
        if (count < 0 || getter == null) return "<unavailable>";
        if (count == 0) return "count=0";

        int decorative = 0;
        int gameplay = 0;
        int ground = 0;
        int other = 0;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        Dictionary<ushort, int> assetIds = new();
        Dictionary<ushort, int> flags = new();

        for (int i = 0; i < count; i++)
        {
            TerrainPoint point = getter(i);
            ushort id = point.AssetTypeID;
            int group = id / 1000;
            if (group == 0) decorative++;
            else if (group == 1) gameplay++;
            else if (group == 2) ground++;
            else other++;

            IncrementCounter(assetIds, id);
            IncrementCounter(flags, (ushort)point.Flags);

            Vector3 pos = point.Position;
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;
        }

        return $"count={count} groups[d={decorative},g={gameplay},ground={ground},other={other}] bounds[x={minX:0.#}..{maxX:0.#},z={minZ:0.#}..{maxZ:0.#}] ids={FormatHistogram(assetIds, 12)} flags={FormatHistogram(flags, 8)}";
    }

    private static string DescribeTerrainData(TerrainData terrainData)
    {
        if (terrainData == null) return "<null>";
        try
        {
            int heightmap = ToInt(GetMember(terrainData, "heightmapResolution") ?? GetMember(terrainData, "HeightmapResolution"), -1);
            int alphamap = ToInt(GetMember(terrainData, "alphamapResolution") ?? GetMember(terrainData, "AlphamapResolution"), -1);
            int detail = ToInt(GetMember(terrainData, "detailResolution") ?? GetMember(terrainData, "DetailResolution"), -1);
            int trees = ToInt(GetMember(terrainData, "treeInstanceCount") ?? GetMember(terrainData, "TreeInstanceCount"), -1);
            if (trees < 0) trees = (int)CountMember(terrainData, "treeInstances");
            long treePrototypes = CountMember(terrainData, "treePrototypes");
            long detailPrototypes = CountMember(terrainData, "detailPrototypes");
            long terrainLayers = CountMember(terrainData, "terrainLayers");
            object size = GetMember(terrainData, "size") ?? GetMember(terrainData, "Size");
            return $"h={heightmap} alpha={alphamap} detailRes={detail} detailProto={detailPrototypes} treeProto={treePrototypes} trees={trees} layers={terrainLayers} size={Safe(size)}";
        }
        catch (Exception ex)
        {
            return $"<failed {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static void IncrementCounter(Dictionary<ushort, int> counters, ushort key)
    {
        if (counters == null) return;
        counters.TryGetValue(key, out int current);
        counters[key] = current + 1;
    }

    private static string FormatHistogram(Dictionary<ushort, int> counters, int maxEntries)
    {
        if (counters == null || counters.Count == 0) return "{}";
        try
        {
            IEnumerable<string> parts = counters
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Take(Math.Max(1, maxEntries))
                .Select(kv => $"{kv.Key}:{kv.Value}");
            string suffix = counters.Count > maxEntries ? $",+{counters.Count - maxEntries} more" : string.Empty;
            return "{" + string.Join(",", parts) + suffix + "}";
        }
        catch
        {
            return $"{{count={counters.Count}}}";
        }
    }

    private static void BuildMapPassTypeCache()
    {
        _mapPassConcreteTypes = new List<Type>();
        if (_mapGenerationPassType == null) return;

        try
        {
            Type[] types;
            try { types = _mapGenerationPassType.Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            foreach (Type type in types)
            {
                if (type == null || type.IsInterface || type.IsAbstract) continue;
                try
                {
                    if (_mapGenerationPassType.IsAssignableFrom(type))
                        _mapPassConcreteTypes.Add(type);
                }
                catch { }
            }

            _mapPassConcreteTypes = _mapPassConcreteTypes
                .Distinct()
                .OrderBy(t => t.FullName ?? t.Name)
                .ToList();
        }
        catch
        {
            _mapPassConcreteTypes.Clear();
        }
    }

    private static string DescribeRuntimeType(object value)
    {
        if (value == null) return "<null>";

        string managed;
        try { managed = value.GetType().FullName ?? value.GetType().Name; }
        catch { managed = "<?>"; }

        try
        {
            string textValue = value.ToString();
            if (!string.IsNullOrWhiteSpace(textValue) &&
                !string.Equals(textValue, managed, StringComparison.Ordinal) &&
                textValue.IndexOf("IMapGenerationPass", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return $"{textValue} [wrapper={managed}]";
            }
        }
        catch { }

        string concrete = TryResolveMapPassConcreteType(value);
        return string.IsNullOrEmpty(concrete) ? managed : $"{concrete} [wrapper={managed}]";
    }

    private static string TryResolveMapPassConcreteType(object value)
    {
        if (value == null || _mapPassConcreteTypes == null || _mapPassConcreteTypes.Count == 0) return null;

        try
        {
            MethodInfo castMethod = null;
            for (Type t = value.GetType(); t != null && castMethod == null; t = t.BaseType)
            {
                MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                castMethod = methods.FirstOrDefault(m => m.Name == "TryCast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                    ?? methods.FirstOrDefault(m => m.Name == "Cast" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            }
            if (castMethod == null) return null;

            foreach (Type candidate in _mapPassConcreteTypes)
            {
                try
                {
                    object cast = castMethod.MakeGenericMethod(candidate).Invoke(value, null);
                    if (cast != null) return candidate.FullName ?? candidate.Name;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    private static void DumpGenerationPassList(object generator)
    {
        if (generator == null || _generationPassListDumped) return;
        try
        {
            object passCollection = GetMember(generator, "m_Passes");
            List<object> passes = SnapshotCollection(passCollection, 64);
            int count = CollectionCount(passCollection);
            if (count <= 0 && passes.Count == 0) return;

            _generationPassListDumped = true;
            _log.Msg($"{ModName}: MAP PASS LIST count={(count >= 0 ? count : passes.Count)} concreteCandidates={_mapPassConcreteTypes?.Count ?? 0}.");
            for (int i = 0; i < passes.Count; i++)
                _log.Msg($"{ModName}: MAP PASS [{i + 1}/{passes.Count}] {DescribeRuntimeType(passes[i])}.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: MAP PASS LIST probe failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DescribeGenerateCurrentPass(object stateMachine)
    {
        if (stateMachine == null) return "<none>";
        object enumerator = GetMember(stateMachine, "__7__wrap5");
        if (enumerator == null) enumerator = GetMember(stateMachine, "<>7__wrap5");
        return DescribeEnumeratorCurrent(enumerator);
    }

    private static MapBlueprint GetGenerateBlueprint(object stateMachine)
    {
        if (stateMachine == null) return null;
        object generator = GetMember(stateMachine, "__4__this");
        if (generator == null) generator = GetMember(stateMachine, "<>4__this");
        return GetMember(generator, "m_Blueprint") as MapBlueprint;
    }

    private static string DescribeSpawnMapCurrentPass(object stateMachine)
    {
        if (stateMachine == null) return "<none>";
        object enumerator = GetMember(stateMachine, "__7__wrap7");
        if (enumerator == null) enumerator = GetMember(stateMachine, "<>7__wrap7");
        return DescribeEnumeratorCurrent(enumerator);
    }

    private static MapBlueprint GetSpawnMapBlueprint(object stateMachine)
    {
        if (stateMachine == null) return null;
        object blueprint = GetMember(stateMachine, "_blueprint");
        if (blueprint == null) blueprint = GetMember(stateMachine, "_blueprint_5__0");
        if (blueprint == null) blueprint = GetMember(stateMachine, "<blueprint>5__0");
        return blueprint as MapBlueprint;
    }

    private static TerrainData GetSpawnMapTerrainData(object stateMachine)
    {
        if (stateMachine == null) return null;
        object terrainData = GetMember(stateMachine, "_terrainData_5__4");
        if (terrainData == null) terrainData = GetMember(stateMachine, "<terrainData>5__4");
        return terrainData as TerrainData;
    }

    private static int _gameplayPointReprojectionCalls;
    private static int _gameplayPointReprojectionLogs;

    private static void PatchGameplayPointReprojection(HarmonyLib.Harmony harmony)
    {
        if (_pointPropGeneratorType == null)
        {
            if (Debug()) _log.Warning($"{ModName}: PointPropGenerator type not found; gameplay-point reprojection fix unavailable.");
            return;
        }

        MethodInfo method = _pointPropGeneratorType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "ReprojectGameplayPoint" && m.GetParameters().Length == 2);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: PointPropGenerator.ReprojectGameplayPoint/2 not found; gameplay-point reprojection fix unavailable.");
            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(GameplayPointReprojectionPrefix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked {FormatMethod(method)} with direct enlarged-map gameplay-point world-to-heightmap reprojection.");
    }

    private static bool GameplayPointReprojectionPrefix(TerrainPoint __0, MapBlueprint __1, ref float __result)
    {
        if (!Enabled() || __1 == null || (__1.Width <= VanillaSize && __1.Height <= VanillaSize))
            return true;

        _gameplayPointReprojectionCalls++;

        try
        {
            Vector3 position = __0.Position;
            float totalWorld = __1.TotalWorldUnits;
            float border = __1.BorderWorldUnits;
            int heightMapRes = __1.HeightMapRes;
            if (totalWorld <= 0.001f || heightMapRes < 2)
                throw new InvalidOperationException($"invalid terrain dimensions totalWorld={totalWorld}, heightMapRes={heightMapRes}");

            float heightScale = (heightMapRes - 1f) / totalWorld;
            float sampleX = Math.Clamp((position.x + border) * heightScale, 0f, heightMapRes - 1f);
            float sampleZ = Math.Clamp((position.z + border) * heightScale, 0f, heightMapRes - 1f);
            __result = __1.GetWorldHeightBilinear(sampleX, sampleZ);

            if (Debug() && _gameplayPointReprojectionLogs < 16)
            {
                _gameplayPointReprojectionLogs++;
                _log.Msg($"{ModName}: GAMEPLAY POINT FIX #{_gameplayPointReprojectionLogs} asset={__0.AssetTypeID} world=({position.x:0.###},{position.z:0.###}) sample=({sampleX:0.###},{sampleZ:0.###}) y={position.y:0.###}->{__result:0.###}.");
            }

            return false;
        }
        catch (Exception ex)
        {
            if (Debug())
                _log.Warning($"{ModName}: gameplay-point reprojection failed safely: {ex.GetType().Name}: {ex.Message}");
            __result = __0.Position.y;
            return false;
        }
    }

    private static void PatchTerrainPointResolver(HarmonyLib.Harmony harmony)
    {
        if (_terrainUtilitiesType == null)
        {
            if (Debug()) _log.Warning($"{ModName}: TerrainUtilities type not found; enlarged-map TerrainPoint resolver unavailable.");
            return;
        }

        MethodInfo method = _terrainUtilitiesType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "TryResolveTerrainPoint" && m.GetParameters().Length == 4);
        if (method == null)
        {
            if (Debug()) _log.Warning($"{ModName}: TerrainUtilities.TryResolveTerrainPoint/4 not found; enlarged-map TerrainPoint resolver unavailable.");
            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainPointResolverPrefix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked {FormatMethod(method)} with an enlarged-map managed replacement. This fixes the native 1-world-unit=1-heightmap-pixel assumption before grass/trees are spawned.");
    }

    private static bool TerrainPointResolverPrefix(
        ref TerrainPoint __0,
        MapBlueprint __1,
        TerrainData __2,
        float __3,
        ref bool __result)
    {
        if (!Enabled() || __1 == null || __2 == null) return true;
        if (__1.Width <= VanillaSize && __1.Height <= VanillaSize) return true;

        _terrainPointResolveCalls++;

        try
        {
            Vector3 position = __0.Position;
            float x = position.x;
            float y = position.y;
            float z = position.z;
            ushort assetTypeId = __0.AssetTypeID;
            int group = assetTypeId / 1000;
            ushort rawFlags = (ushort)__0.Flags;
            IncrementCounter(_resolverAssetTypeCounts, assetTypeId);
            IncrementCounter(_resolverFlagCounts, rawFlags);
            bool canBeReprojected = (__0.Flags & TerrainPointFlags.CanBeReprojected) != 0;
            bool isInfill = (__0.Flags & TerrainPointFlags.IsInfill) != 0;

            _terrainPointManagedHandled++;
            if (group == 0) _terrainPointDecorativeHandled++;
            else if (group == 1) _terrainPointGameplayHandled++;
            else if (group == 2) _terrainPointGroundHandled++;

            float border = __1.BorderWorldUnits;
            float totalWorld = __1.TotalWorldUnits;
            int heightMapRes = __1.HeightMapRes;
            if (totalWorld <= 0.001f || heightMapRes < 2)
                throw new InvalidOperationException($"invalid terrain dimensions totalWorld={totalWorld}, heightMapRes={heightMapRes}");

            // This mirrors the native resolver: non-infill Decorative and Ground
            // points are rejected when the Unity terrain slope is above maxSlope.
            if (!isInfill && (group == 0 || group == 2))
            {
                float nx = (x + border) / totalWorld;
                float nz = (z + border) / totalWorld;
                if (__2.GetSteepness(nx, nz) > __3)
                {
                    _terrainPointRejectedSlope++;
                    __result = false;
                    return false;
                }
            }

            // Native infill points must resolve to an in-bounds blocked tile.
            if (isInfill)
            {
                if (!__1.TryWorldToTilePos(new Vector2(x, z), out Vector2Int tilePos) ||
                    !__1.IsBlocked(tilePos.x, tilePos.y))
                {
                    _terrainPointRejectedInfill++;
                    __result = false;
                    return false;
                }
            }

            // Vanilla total terrain width is 512 world units and its cached heightmap
            // contains 513 samples (512 intervals), so the native resolver's direct
            // world-coordinate sampling happens to be 1:1. A 64x64 map expands the
            // full terrain to 688 world units while the cache remains 513x513.
            // Convert only this native resolver path back into the cached-heightmap
            // coordinate space before sampling Y.
            if (canBeReprojected || isInfill)
            {
                float heightScale = (heightMapRes - 1f) / totalWorld;
                float sampleX = Math.Clamp((x + border) * heightScale, 0f, heightMapRes - 1f);
                float sampleZ = Math.Clamp((z + border) * heightScale, 0f, heightMapRes - 1f);
                float newY = __1.GetWorldHeightBilinear(sampleX, sampleZ);

                __0 = new TerrainPoint(
                    new Vector3(x, newY, z),
                    __0.Orientation,
                    __0.Scale,
                    __0.AssetTypeID,
                    __0.Flags);

                if (Debug() && _terrainPointFixLogs < 24 && (group == 0 || group == 1 || group == 2))
                {
                    _terrainPointFixLogs++;
                    string groupName = group == 2 ? "Ground" : (group == 1 ? "Gameplay" : "Decorative");
                    _log.Msg($"{ModName}: TERRAIN RESOLVE FIX #{_terrainPointFixLogs} group={groupName} asset={assetTypeId} world=({x:0.###},{z:0.###}) sample=({sampleX:0.###},{sampleZ:0.###}) y={y:0.###}->{newY:0.###} totalWorld={totalWorld:0.###} res={heightMapRes}.");
                }
            }

            _terrainPointManagedAccepted++;
            __result = true;
            return false; // skip the enlarged-map native body with its 1:1 sampling assumption
        }
        catch (Exception ex)
        {
            _terrainPointResolverFailures++;
            if (Debug() && _terrainPointResolverFailures <= 8)
                _log.Warning($"{ModName}: managed TerrainPoint resolver failed; rejecting this point instead of re-entering the unsafe enlarged-map native resolver: {ex.GetType().Name}: {ex.Message}");
            __result = false;
            return false;
        }
    }

    private static void PatchWorldHeightBilinearSafety(HarmonyLib.Harmony harmony)
    {
        if (_blueprintType == null)
        {
            _log.Warning($"{ModName}: MapBlueprint type not found; terrain-height boundary safety unavailable.");
            return;
        }

        MethodInfo method = _blueprintType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "GetWorldHeightBilinear" && m.GetParameters().Length == 2);
        if (method == null)
        {
            _log.Warning($"{ModName}: MapBlueprint.GetWorldHeightBilinear(float,float) not found; terrain-height boundary safety unavailable.");
            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(WorldHeightBilinearPrefix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked {FormatMethod(method)} as final managed-call heightmap boundary safety; enlarged TerrainPoint mapping is handled by the managed TerrainPoint reprojection fixes.");
    }

    private static void WorldHeightBilinearPrefix(object __instance, ref float __0, ref float __1)
    {
        if (!Enabled() || __instance == null) return;

        try
        {
            int resolution = (int)ToFloat(GetMember(__instance, "HeightMapRes"), 513f);
            if (resolution < 2) resolution = 513;
            float max = resolution - 1f;

            // 0.26 performs enlarged-map world->heightmap conversion in the managed
            // TerrainUtilities.TryResolveTerrainPoint replacement itself. This hook is
            // intentionally only a final boundary guard for managed/wrapper callers.

            float beforeClampX = __0;
            float beforeClampZ = __1;

            if (float.IsNaN(__0) || float.IsNegativeInfinity(__0)) __0 = 0f;
            else if (float.IsPositiveInfinity(__0)) __0 = max;
            else __0 = Math.Clamp(__0, 0f, max);

            if (float.IsNaN(__1) || float.IsNegativeInfinity(__1)) __1 = 0f;
            else if (float.IsPositiveInfinity(__1)) __1 = max;
            else __1 = Math.Clamp(__1, 0f, max);

            if (beforeClampX != __0 || beforeClampZ != __1)
            {
                _worldHeightClampCorrections++;
                if (Debug() && _worldHeightClampCorrections <= 24)
                    _log.Msg($"{ModName}: TERRAIN HEIGHT SAFETY clamp #{_worldHeightClampCorrections}: ({beforeClampX:0.###},{beforeClampZ:0.###}) -> ({__0:0.###},{__1:0.###}) for heightmapRes={resolution}.");
            }
        }
        catch (Exception ex)
        {
            if (Debug())
                _log.Warning($"{ModName}: terrain-point heightmap mapping failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void PatchTerrainSpawnMapStateMachine(HarmonyLib.Harmony harmony)
    {
        if (_terrainGeneratorType == null)
        {
            _log.Warning($"{ModName}: TerrainMapGenerator type not found; SpawnMap diagnostics unavailable.");
            return;
        }

        Type nested = _terrainGeneratorType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.IndexOf("SpawnMap", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 t.Name.IndexOf("d__", StringComparison.OrdinalIgnoreCase) >= 0);
        if (nested == null)
        {
            _log.Warning($"{ModName}: TerrainMapGenerator.SpawnMap state machine not found; SpawnMap diagnostics unavailable.");
            return;
        }

        MethodInfo moveNext = nested.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "MoveNext" && m.GetParameters().Length == 0 && m.ReturnType == typeof(bool));
        if (moveNext == null)
        {
            _log.Warning($"{ModName}: TerrainMapGenerator.SpawnMap MoveNext not found; SpawnMap diagnostics unavailable.");
            return;
        }

        harmony.Patch(
            moveNext,
            prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainSpawnMapMoveNextPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(TerrainSpawnMapMoveNextPostfix), BindingFlags.Static | BindingFlags.NonPublic)));

        _log.Msg($"{ModName}: hooked targeted terrain spawn coroutine {nested.FullName}.MoveNext for pass-level loading diagnostics.");
    }

    private static void TerrainSpawnMapMoveNextPrefix(object __instance)
    {
        if (!Enabled() || !Debug() || __instance == null) return;
        _spawnMapMoveNextCalls++;
        _spawnMapStateBeforeCall = ReadCoroutineState(__instance);
        _spawnMapMoveStartMs = Environment.TickCount64;
        _spawnMapPassBefore = DescribeSpawnMapCurrentPass(__instance);
        _spawnMapTerrainBefore = DescribeTerrainData(GetSpawnMapTerrainData(__instance));
        _spawnMapPointsBefore = SummarizeBlueprintTerrainPoints(GetSpawnMapBlueprint(__instance));
    }

    private static void TerrainSpawnMapMoveNextPostfix(object __instance, bool __result)
    {
        if (!Enabled() || !Debug() || __instance == null) return;
        try
        {
            int after = ReadCoroutineState(__instance);
            object current = GetMember(__instance, "__2__current");
            if (current == null) current = GetMember(__instance, "<>2__current");
            string currentType = DescribeYieldObject(current);
            long elapsed = Math.Max(0, Environment.TickCount64 - _spawnMapMoveStartMs);
            string detail = DescribeSpawnMapState(__instance);
            string passAfter = DescribeSpawnMapCurrentPass(__instance);
            string terrainAfter = DescribeTerrainData(GetSpawnMapTerrainData(__instance));
            string pointsAfter = SummarizeBlueprintTerrainPoints(GetSpawnMapBlueprint(__instance));

            bool changed = _spawnMapStateBeforeCall != after || after != _spawnMapLastState;
            _spawnMapLastState = after;
            _spawnMapLastCurrent = currentType;
            _spawnMapLastStepMs = Environment.TickCount64;
            if (!__result) _spawnMapCompleted = true;

            if (changed || _spawnMapMoveNextCalls <= 12 || !__result || elapsed >= 100)
                _log.Msg($"{ModName}: SPAWNMAP STEP call={_spawnMapMoveNextCalls} state={FormatState(_spawnMapStateBeforeCall)}->{FormatState(after)} result={__result} elapsed={elapsed}ms current={currentType} {detail}.");

            if (_spawnMapMoveNextCalls <= 20 &&
                (!string.Equals(_spawnMapPassBefore, "<none>", StringComparison.Ordinal) ||
                 !string.Equals(passAfter, "<none>", StringComparison.Ordinal)))
            {
                _log.Msg($"{ModName}: FOLIAGE SPAWN STEP call={_spawnMapMoveNextCalls} state={FormatState(_spawnMapStateBeforeCall)}->{FormatState(after)} passBefore={_spawnMapPassBefore} passAfter={passAfter} terrainBefore=[{_spawnMapTerrainBefore}] terrainAfter=[{terrainAfter}] pointsBefore=[{_spawnMapPointsBefore}] pointsAfter=[{pointsAfter}].");
            }

            if (!__result)
            {
                _log.Msg($"{ModName}: SPAWNMAP COMPLETE call={_spawnMapMoveNextCalls} finalState={FormatState(after)} parentMapGenCalls={_mapGenerateMoveNextCalls} onMapReadyCalls={_tacticalStateOnMapReadyCalls}.");
                _log.Msg($"{ModName}: FOLIAGE FINAL {pointsAfter} terrainData={terrainAfter} groundCoverCalls={_groundCoverScatterCalls} spawnerInitCalls={_terrainPointSpawnerInitCalls} spawnerProcessCalls={_terrainPointProcessCalls}.");
                _log.Msg($"{ModName}: TERRAIN MANAGED SUMMARY calls={_terrainPointResolveCalls} managed={_terrainPointManagedHandled} accepted={_terrainPointManagedAccepted} slopeReject={_terrainPointRejectedSlope} infillReject={_terrainPointRejectedInfill} groups=decorative:{_terrainPointDecorativeHandled},gameplay:{_terrainPointGameplayHandled},ground:{_terrainPointGroundHandled} failures={_terrainPointResolverFailures} gameplayPointFixCalls={_gameplayPointReprojectionCalls} safetyClamps={_worldHeightClampCorrections} assetIds={FormatHistogram(_resolverAssetTypeCounts, 16)} flags={FormatHistogram(_resolverFlagCounts, 8)}.");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: SPAWNMAP MoveNext diagnostic failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DescribeSpawnMapState(object stateMachine)
    {
        try
        {
            object generator = GetMember(stateMachine, "__4__this");
            if (generator == null) generator = GetMember(stateMachine, "<>4__this");
            long passCount = CountMember(generator, "m_Passes");

            object biome = GetMember(stateMachine, "_biome_5__3");
            if (biome == null) biome = GetMember(stateMachine, "<biome>5__3");
            object terrain = GetMember(stateMachine, "_terrain_5__6");
            if (terrain == null) terrain = GetMember(stateMachine, "<terrain>5__6");

            object enumerator = GetMember(stateMachine, "__7__wrap7");
            if (enumerator == null) enumerator = GetMember(stateMachine, "<>7__wrap7");
            string pass = DescribeEnumeratorCurrent(enumerator);

            return $"passes={passCount} pass={pass} biome={DescribeSimpleType(biome)} terrain={DescribeSimpleType(terrain)}";
        }
        catch
        {
            return "passes=? pass=?";
        }
    }

    private static string DescribeEnumeratorCurrent(object enumerator)
    {
        if (enumerator == null) return "<none>";
        try
        {
            object current = GetMember(enumerator, "Current");
            if (current == null) current = GetMember(enumerator, "current");
            if (current == null) current = GetMember(enumerator, "m_Current");
            if (current == null) current = GetMember(enumerator, "_current");
            return DescribeRuntimeType(current);
        }
        catch { return "<?>"; }
    }

    private static string DescribeSimpleType(object value)
    {
        if (value == null) return "<null>";
        try { return value.GetType().FullName ?? value.GetType().Name; }
        catch { return "<?>"; }
    }

    private static int ReadCoroutineState(object stateMachine)
    {
        object value = GetMember(stateMachine, "__1__state");
        if (value == null) value = GetMember(stateMachine, "<>1__state");
        return ToInt(value, int.MinValue);
    }

    private static string FormatState(int state)
    {
        return state == int.MinValue ? "?" : state.ToString();
    }

    private static string DescribeYieldObject(object current)
    {
        if (current == null) return "<null>";
        try
        {
            Type t = current.GetType();
            return t.FullName ?? t.Name;
        }
        catch { return "<unreadable>"; }
    }

    private static string DescribePreviewCompact(object preview)
    {
        if (preview == null) return "<null>";
        try
        {
            object blueprint = GetMember(preview, "Blueprint") ?? GetMember(preview, "m_Blueprint");
            int sx = ToInt(GetMember(preview, "SizeX"), -1);
            int sz = ToInt(GetMember(preview, "SizeZ"), -1);
            int bw = ToInt(GetMember(blueprint, "Width"), -1);
            int bh = ToInt(GetMember(blueprint, "Height"), -1);
            long tiles = CountMember(preview, "Tiles");
            return $"size={sx}x{sz},blueprint={bw}x{bh},tiles={tiles}";
        }
        catch (Exception ex)
        {
            return $"<preview {ex.GetType().Name}>";
        }
    }

    private static void BaseTileHighlighterInitPrefix(object __instance, object[] __args)
    {
        if (!Enabled() || !FixTileHighlighter()) return;
        if (_tileHighlighterReinitInProgress) return;

        _tileHighlighterInitCalls++;
        int requestedX = (__args != null && __args.Length > 0) ? ToInt(__args[0], -1) : -1;
        int requestedZ = (__args != null && __args.Length > 1) ? ToInt(__args[1], -1) : -1;
        int desiredX = GetDesiredMapWidth();
        int desiredZ = GetDesiredMapHeight();

        if (Debug())
            _log.Msg($"{ModName}: TILE HIGHLIGHTER INIT natural call #{_tileHighlighterInitCalls}: requested={requestedX}x{requestedZ}, desiredMap={desiredX}x{desiredZ}. Vanilla Init will complete before any correction.");
    }

    private static void BaseTileHighlighterInitPostfix(object __instance)
    {
        if (!Enabled() || !FixTileHighlighter() || __instance == null) return;

        if (_tileHighlighterReinitInProgress)
        {
            if (Debug()) ProbeTileHighlighterInstance(__instance, "native-reinit-postfix");
            return;
        }

        if (Debug())
        {
            ProbeTileHighlighterInstance(__instance, "vanilla-Init-postfix");
            DumpTileHighlighterSurface(__instance.GetType());
        }

        int currentX = ToInt(GetMember(__instance, "m_MapTilesX"), -1);
        object resolution = GetMember(__instance, "m_MapResolution");
        int desiredX = GetDesiredMapWidth();
        int desiredZ = GetDesiredMapHeight();

        if (desiredX <= VanillaSize || desiredZ <= VanillaSize) return;
        if (currentX == desiredX && ResolutionMatches(resolution, desiredX, desiredZ)) return;
        if (_baseTileHighlighterInitMethod == null)
        {
            _log.Warning($"{ModName}: TILE HIGHLIGHTER correction skipped because Init MethodInfo was not retained.");
            return;
        }

        _tileHighlighterReinitInProgress = true;
        try
        {
            _log.Msg($"{ModName}: TILE HIGHLIGHTER native reinit {currentX} -> {desiredX}x{desiredZ}; calling BaseTileHighlighter.Init({desiredX}, {desiredZ}) after vanilla initialization.");
            _baseTileHighlighterInitMethod.Invoke(__instance, new object[] { desiredX, desiredZ });
            _tileHighlighterInitPatches++;

            int afterX = ToInt(GetMember(__instance, "m_MapTilesX"), -1);
            object afterResolution = GetMember(__instance, "m_MapResolution");
            object afterMapSize = GetMember(__instance, "m_MapSize");
            bool ok = afterX == desiredX && ResolutionMatches(afterResolution, desiredX, desiredZ);

            if (ok)
                _log.Msg($"{ModName}: TILE HIGHLIGHTER native reinit SUCCESS: mapTilesX={afterX}, resolution={Safe(afterResolution)}, mapSize={Safe(afterMapSize)}, corrections={_tileHighlighterInitPatches}.");
            else
                _log.Warning($"{ModName}: TILE HIGHLIGHTER native reinit DID NOT STICK: mapTilesX={afterX}, resolution={Safe(afterResolution)}, mapSize={Safe(afterMapSize)}.");
        }
        catch (TargetInvocationException ex)
        {
            Exception inner = ex.InnerException ?? ex;
            _log.Error($"{ModName}: TILE HIGHLIGHTER native reinit failed: {inner.GetType().Name}: {inner.Message}");
        }
        catch (Exception ex)
        {
            _log.Error($"{ModName}: TILE HIGHLIGHTER native reinit failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _tileHighlighterReinitInProgress = false;
        }
    }

    private static int GetDesiredMapWidth()
    {
        int value = ToInt(GetMember(_mapInstance, "Width"), -1);
        if (value <= VanillaSize && _blueprintReplaced) value = TargetSize;
        return value;
    }

    private static int GetDesiredMapHeight()
    {
        int value = ToInt(GetMember(_mapInstance, "Height"), -1);
        if (value <= VanillaSize && _blueprintReplaced) value = TargetSize;
        return value;
    }

    private static bool ResolutionMatches(object resolution, int width, int height)
    {
        if (resolution == null) return false;
        try
        {
            float x = Convert.ToSingle(GetMember(resolution, "x") ?? GetMember(resolution, "X"));
            float y = Convert.ToSingle(GetMember(resolution, "y") ?? GetMember(resolution, "Y"));
            return Math.Abs(x - width) < 0.01f && Math.Abs(y - height) < 0.01f;
        }
        catch
        {
            string text = Safe(resolution);
            return text.Contains(width.ToString()) && text.Contains(height.ToString());
        }
    }

    private static void DumpTileHighlighterSurface(Type type)
    {
        if (_tileHighlighterSurfaceDumped || type == null) return;
        _tileHighlighterSurfaceDumped = true;
        try
        {
            _log.Msg($"{ModName}: TILE HIGHLIGHTER SURFACE BEGIN type={type.FullName}");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo f in type.GetFields(flags)
                .Where(f => IsHighlighterRelevantName(f.Name))
                .OrderBy(f => f.Name)
                .Take(80))
            {
                _log.Msg($"{ModName}: HIGHLIGHT FIELD {f.FieldType?.FullName ?? "?"} {f.Name}");
            }
            foreach (MethodInfo m in type.GetMethods(flags)
                .Where(m => IsHighlighterRelevantName(m.Name))
                .OrderBy(m => m.Name)
                .ThenBy(m => m.GetParameters().Length)
                .Take(120))
            {
                string ps = string.Join(", ", m.GetParameters().Select(p => (p.ParameterType?.Name ?? "?") + " " + p.Name));
                _log.Msg($"{ModName}: HIGHLIGHT METHOD {(m.ReturnType?.Name ?? "?")} {m.Name}({ps})");
            }
            _log.Msg($"{ModName}: TILE HIGHLIGHTER SURFACE END");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: tile-highlighter surface dump failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsHighlighterRelevantName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("init") || n.Contains("texture") || n.Contains("pixel") || n.Contains("map") ||
               n.Contains("size") || n.Contains("resolution") || n.Contains("highlight") ||
               n.Contains("movement") || n.Contains("skill") || n.Contains("range") || n.Contains("aoe") ||
               n.Contains("dirty") || n.Contains("raw");
    }

    private static void PatchMissionPreviewPixelExtent(HarmonyLib.Harmony harmony)
    {
        if (_missionPreviewType == null) return;

        // MENACE aliases GetPixelWidth() and GetPixelHeight() to the exact same IL2CPP
        // native body (RVA 0x58F7C0 in v0.7.14). Patching both MethodInfo objects
        // therefore detours the same tiny native function twice and is unsafe. Hook the
        // shared body once through GetPixelWidth; calls through either alias reach it.
        MethodInfo method = _missionPreviewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "GetPixelWidth" && m.GetParameters().Length == 0 && m.ReturnType == typeof(int));
        if (method == null)
        {
            _log.Warning($"{ModName}: could not find {_missionPreviewType.FullName}.GetPixelWidth() for shared Mission Prep map extent correction.");
            return;
        }

        MethodInfo postfix = typeof(Plugin).GetMethod(nameof(MissionPreviewPixelExtentPostfix), BindingFlags.Static | BindingFlags.NonPublic);
        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        _log.Msg($"{ModName}: hooked shared MissionPreview pixel-extent native body once via {FormatMethod(method)}; GetPixelWidth/GetPixelHeight share this body.");
    }

    private static void MissionPreviewPixelExtentPostfix(object __instance, ref int __result, MethodBase __originalMethod)
    {
        if (!Enabled() || !ReplaceBlueprint() || __instance == null) return;

        try
        {
            int resolution = ToInt(GetMember(__instance, "m_Resolution"), -1);
            if (resolution <= 0)
                resolution = ToInt(InvokeNoArg(__instance, "GetPixelsPerTile"), -1);
            if (resolution <= 0)
            {
                if (Debug()) _log.Warning($"{ModName}: Mission Prep map extent correction could not read MissionPreview resolution; leaving {__originalMethod?.Name}={__result}.");
                return;
            }

            int desired = checked(resolution * TargetSize);
            if (__result == desired) return;

            int vanilla = __result;
            __result = desired;
            int correction = ++_missionPrepPixelExtentCorrections;
            if (Debug() && correction <= 4)
                _log.Msg($"{ModName}: MISSION PREP MAP {__originalMethod?.Name} corrected {vanilla} -> {desired} pixels ({resolution}px/tile, {VanillaSize} -> {TargetSize} tiles, sample #{correction}). Further identical calls are suppressed.");
        }
        catch (Exception ex)
        {
            _log.Warning($"{ModName}: Mission Prep map extent correction failed safely in {__originalMethod?.Name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void MissionPreviewGeneratePrefix(object __instance)
    {
        if (!Enabled()) return;
        _missionPreviewInstance = __instance;
        _blueprintReplacementAttempted = false;
        _blueprintReplaced = false;
        _terrainMoveNextCalls = 0;
        _initialAiSpawnRectsFixed = 0;
        _tacticalAiSpawnRectsFixed = 0;
        _spawnFixFailures = 0;
        _spawnFixNoticeCount = 0;
        _areaGeometryDiagnosticLines = 0;
        if (AreaDiagnostics()) _log.Msg($"{ModName}: AREA GEOMETRY DIAG enabled, first {MaxAreaGeometryDiagnosticLines} records for this mission preview (no per-turn logs).");
        if (Debug()) _log.Msg($"{ModName}: MissionPreview.Generate started; waiting for TerrainMapGenerator blueprint creation.");
    }

    private static void MapGeneratePrefix(object __instance)
    {
        if (!Enabled()) return;
        _mapInstance = __instance;
        _mapGenerateMoveNextCalls = 0;
        _mapGenerateStateBeforeCall = int.MinValue;
        _mapGenerateLastState = int.MinValue;
        _mapGenerateLastCurrent = "<none>";
        _mapGenerateLastStepMs = Environment.TickCount64;
        _mapGenerateMoveStartMs = 0;
        _mapGenerateCompleted = false;
        _spawnMapMoveNextCalls = 0;
        _spawnMapStateBeforeCall = int.MinValue;
        _spawnMapLastState = int.MinValue;
        _spawnMapLastCurrent = "<none>";
        _spawnMapLastStepMs = 0;
        _spawnMapMoveStartMs = 0;
        _spawnMapCompleted = false;
        _terrainPointResolveCalls = 0;
        _terrainPointManagedHandled = 0;
        _terrainPointManagedAccepted = 0;
        _terrainPointRejectedSlope = 0;
        _terrainPointRejectedInfill = 0;
        _terrainPointDecorativeHandled = 0;
        _terrainPointGameplayHandled = 0;
        _terrainPointGroundHandled = 0;
        _terrainPointResolverFailures = 0;
        _terrainPointFixLogs = 0;
        _gameplayPointReprojectionCalls = 0;
        _gameplayPointReprojectionLogs = 0;
        _worldHeightClampCorrections = 0;
        _generationPassListDumped = false;
        _groundCoverScatterCalls = 0;
        _terrainPointSpawnerInitCalls = 0;
        _terrainPointProcessCalls = 0;
        _spawnMapPassBefore = "<none>";
        _spawnMapTerrainBefore = "<none>";
        _spawnMapPointsBefore = "blueprint=<null>";
        _terrainGeneratePassBefore = "<none>";
        _terrainGeneratePointsBefore = "blueprint=<null>";
        _resolverAssetTypeCounts.Clear();
        _resolverFlagCounts.Clear();
        _tacticalStateOnMapReadyCalls = 0;
        _deployEntitiesCalls = 0;
        _unitActorCreateCalls = 0;
        _tacticalManagerStartCalls = 0;
        if (Debug()) _log.Msg($"{ModName}: Tactical Map.Generate entered; preview blueprint replacement={_blueprintReplaced}. Map.GenerateMap state tracking reset.");
    }

    private static void TerrainGenerateMoveNextPrefix(object __instance)
    {
        if (!Enabled() || __instance == null) return;
        _terrainMoveNextCalls++;
        if (Debug())
        {
            _terrainGeneratePassBefore = DescribeGenerateCurrentPass(__instance);
            _terrainGeneratePointsBefore = SummarizeBlueprintTerrainPoints(GetGenerateBlueprint(__instance));
        }
        if (Debug() && _terrainMoveNextCalls <= 12)
            ProbeTerrainState(__instance, "prefix", false);
    }

    private static void TerrainGenerateMoveNextPostfix(object __instance)
    {
        if (!Enabled() || __instance == null) return;
        ProbeTerrainState(__instance, "postfix", true);

        if (Debug() && _terrainMoveNextCalls <= 20)
        {
            try
            {
                string passAfter = DescribeGenerateCurrentPass(__instance);
                string pointsAfter = SummarizeBlueprintTerrainPoints(GetGenerateBlueprint(__instance));
                if (!string.Equals(_terrainGeneratePassBefore, "<none>", StringComparison.Ordinal) ||
                    !string.Equals(passAfter, "<none>", StringComparison.Ordinal) ||
                    !string.Equals(_terrainGeneratePointsBefore, pointsAfter, StringComparison.Ordinal))
                {
                    _log.Msg($"{ModName}: FOLIAGE GENERATE STEP call={_terrainMoveNextCalls} state={FormatState(ReadCoroutineState(__instance))} passBefore={_terrainGeneratePassBefore} passAfter={passAfter} before=[{_terrainGeneratePointsBefore}] after=[{pointsAfter}].");
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"{ModName}: FOLIAGE GENERATE STEP probe failed safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void ProbeTerrainState(object stateMachine, string phase, bool mayReplace)
    {
        try
        {
            object state = GetMember(stateMachine, "__1__state");
            object generator = GetMember(stateMachine, "__4__this");
            object layerValue = GetMember(stateMachine, "_terrainLayerCount_5__5");
            int layers = ToInt(layerValue, -1);

            if (generator == null)
            {
                if (Debug() && _terrainMoveNextCalls <= 4)
                    _log.Msg($"{ModName}: TERRAIN GEN {phase} call={_terrainMoveNextCalls} state={Safe(state)} generator=<null> layers={layers}.");
                return;
            }

            if (Debug() && !_generationPassListDumped)
                DumpGenerationPassList(generator);

            object blueprint = GetMember(generator, "m_Blueprint");
            if (blueprint == null)
            {
                if (Debug() && _terrainMoveNextCalls <= 8)
                    _log.Msg($"{ModName}: TERRAIN GEN {phase} call={_terrainMoveNextCalls} state={Safe(state)} blueprint=<null> layers={layers}.");
                return;
            }

            int width = ToInt(GetMember(blueprint, "Width"), -1);
            int height = ToInt(GetMember(blueprint, "Height"), -1);
            long tiles = CountMember(blueprint, "Tiles");
            long chunks = CountMember(blueprint, "Chunks");
            long areas = CountMember(blueprint, "Areas");
            long terrainPoints = CountMember(blueprint, "TerrainPoints");
            long gameplayPoints = CountMember(blueprint, "GameplayTerrainPoints");

            if (Debug() && _terrainMoveNextCalls <= 12)
            {
                _log.Msg($"{ModName}: TERRAIN GEN {phase} call={_terrainMoveNextCalls} state={Safe(state)} Blueprint={width}x{height} layers={layers} tiles={tiles} chunks={chunks} areas={areas} terrainPts={terrainPoints}/{gameplayPoints}.");
            }

            if (!mayReplace || _blueprintReplacementAttempted || !ReplaceBlueprint()) return;
            if (width != VanillaSize || height != VanillaSize) return;

            // Replace only while the blueprint is still structurally empty. The constructor
            // is expected to allocate the vanilla tile array, so Tiles=1764 is fine here.
            if (chunks > 0 || areas > 0 || terrainPoints > 0 || gameplayPoints > 0)
            {
                _blueprintReplacementAttempted = true;
                _log.Warning($"{ModName}: blueprint appeared too late to replace safely (chunks={chunks}, areas={areas}, terrainPts={terrainPoints}/{gameplayPoints}); leaving vanilla 42x42.");
                return;
            }

            if (layers <= 0)
            {
                layers = ToInt(GetMember(blueprint, "AlphamapLayerCount"), -1);
            }
            if (layers <= 0)
            {
                _blueprintReplacementAttempted = true;
                _log.Warning($"{ModName}: could not determine terrain layer count; leaving vanilla 42x42.");
                return;
            }

            _blueprintReplacementAttempted = true;
            object replacement = CreateBlueprint(TargetSize, TargetSize, layers);
            if (replacement == null)
            {
                _log.Error($"{ModName}: failed to construct replacement MapBlueprint({TargetSize},{TargetSize},{layers}); leaving vanilla map.");
                return;
            }

            CopyIfWritable(blueprint, replacement, "OwningFaction");
            CopyIfWritable(blueprint, replacement, "RoadConfig");
            CopyIfWritable(blueprint, replacement, "RaiseTerrainEdges");
            CopyIfWritable(blueprint, replacement, "RaiseInMeters");

            if (!TrySetMember(generator, "m_Blueprint", replacement))
            {
                _log.Error($"{ModName}: created enlarged blueprint but could not assign TerrainMapGenerator.m_Blueprint; leaving vanilla map.");
                return;
            }

            object verify = GetMember(generator, "m_Blueprint");
            int vw = ToInt(GetMember(verify, "Width"), -1);
            int vh = ToInt(GetMember(verify, "Height"), -1);
            long vt = CountMember(verify, "Tiles");

            if (vw == TargetSize && vh == TargetSize)
            {
                _blueprintReplaced = true;
                _log.Msg($"{ModName}: BLUEPRINT REPLACED {VanillaSize}x{VanillaSize} -> {TargetSize}x{TargetSize} inside TerrainMapGenerator.Generate; layers={layers}, tiles={vt}, state={Safe(state)}.");
            }
            else
            {
                _log.Error($"{ModName}: blueprint assignment did not stick (readback {vw}x{vh}); leaving test unsuccessful.");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"{ModName}: terrain generator probe/replacement failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static object CreateBlueprint(int width, int height, int layers)
    {
        if (_blueprintType == null) return null;
        try
        {
            ConstructorInfo ctor = _blueprintType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 3 && p.All(x => x.ParameterType == typeof(int));
                });
            return ctor?.Invoke(new object[] { width, height, layers });
        }
        catch (Exception ex)
        {
            _log.Error($"{ModName}: replacement blueprint constructor failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void ProbeMissionPreview(string reason)
    {
        if (!Enabled() || _missionPreviewInstance == null) return;
        try
        {
            MethodInfo getPreview = _missionPreviewInstance.GetType().GetMethod("GetPreview", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            object result = getPreview?.Invoke(_missionPreviewInstance, null);
            ProbePreviewResult(result, reason);
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: preview probe [{reason}] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ProbePreviewResult(object result, string reason)
    {
        if (!Enabled() || result == null)
        {
            if (Debug()) _log.Msg($"{ModName}: PREVIEW RESULT [{reason}] <null> replacement={_blueprintReplaced}.");
            return;
        }

        try
        {
            object bp = GetMember(result, "Blueprint");
            int sx = ToInt(GetMember(result, "SizeX"), -1);
            int sz = ToInt(GetMember(result, "SizeZ"), -1);
            int bw = ToInt(GetMember(bp, "Width"), -1);
            int bh = ToInt(GetMember(bp, "Height"), -1);
            long tiles = CountMember(bp, "Tiles");
            long chunks = CountMember(bp, "Chunks");
            _log.Msg($"{ModName}: PREVIEW RESULT [{reason}] Size={sx}x{sz}, Blueprint={bw}x{bh}, Tiles={tiles}, Chunks={chunks}, replacement={_blueprintReplaced}.");
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: PREVIEW RESULT [{reason}] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ProbeMap(string reason)
    {
        if (!Enabled() || _mapInstance == null)
        {
            if (Debug()) _log.Msg($"{ModName}: MAP RESULT [{reason}] no captured Map instance.");
            return;
        }

        try
        {
            object bp = GetMember(_mapInstance, "Blueprint");
            int width = ToInt(GetMember(_mapInstance, "Width"), -1);
            int height = ToInt(GetMember(_mapInstance, "Height"), -1);
            int bw = ToInt(GetMember(bp, "Width"), -1);
            int bh = ToInt(GetMember(bp, "Height"), -1);
            long tiles = CountMember(bp, "Tiles");
            string staticX = TryReadStaticInt(_mapType, "TILES_X", out int sx) ? sx.ToString() : "?";
            string staticZ = TryReadStaticInt(_mapType, "TILES_Z", out int sz) ? sz.ToString() : "?";
            object playableBounds = GetMember(_mapInstance, "PlayableAreaBounds");
            object terrainBounds = GetMember(_mapInstance, "TerrainBounds");
            _log.Msg($"{ModName}: MAP RESULT [{reason}] Map={width}x{height}, Blueprint={bw}x{bh}, Tiles={tiles}, VanillaConstants={staticX}x{staticZ}, PlayableBounds={Safe(playableBounds)}, TerrainBounds={Safe(terrainBounds)}, replacement={_blueprintReplaced}.");
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: MAP RESULT [{reason}] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ProbeTileHighlighter(string reason)
    {
        if (!Enabled() || !Debug() || _tileHighlighterType == null) return;
        try
        {
            MethodInfo instanceMethod = _tileHighlighterType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "Instance" && m.GetParameters().Length == 0);
            object instance = instanceMethod?.Invoke(null, null);
            if (instance == null)
            {
                if (Debug()) _log.Msg($"{ModName}: TILE HIGHLIGHTER [{reason}] instance=<null>.");
                return;
            }
            ProbeTileHighlighterInstance(instance, reason);
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: TILE HIGHLIGHTER [{reason}] probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ProbeTileHighlighterInstance(object instance, string reason)
    {
        try
        {
            object mapTilesX = GetMember(instance, "m_MapTilesX");
            object mapResolution = GetMember(instance, "m_MapResolution");
            object mapSize = GetMember(instance, "m_MapSize");
            long textures = CountMember(instance, "m_Textures");
            long rawTextures = CountMember(instance, "m_RawTextures");
            _log.Msg($"{ModName}: TILE HIGHLIGHTER [{reason}] mapTilesX={Safe(mapTilesX)}, resolution={Safe(mapResolution)}, mapSize={Safe(mapSize)}, textures={textures}, rawTextures={rawTextures}, initCalls={_tileHighlighterInitCalls}, patched={_tileHighlighterInitPatches}.");
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: TILE HIGHLIGHTER [{reason}] state read failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void CopyIfWritable(object source, object destination, string name)
    {
        try
        {
            object value = GetMember(source, name);
            if (value != null) TrySetMember(destination, name, value);
        }
        catch { }
    }

    private static object GetMember(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                PropertyInfo p = t.GetProperty(name, flags);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                    return p.GetValue(obj);
            }
            catch { }

            try
            {
                FieldInfo f = t.GetField(name, flags);
                if (f != null) return f.GetValue(obj);
            }
            catch { }
        }

        return null;
    }

    private static bool TrySetMember(object obj, string name, object value)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return false;
        Type t = obj.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        try
        {
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null && p.CanWrite)
            {
                p.SetValue(obj, value);
                return true;
            }
        }
        catch { }

        try
        {
            FieldInfo f = t.GetField(name, flags);
            if (f != null && !f.IsInitOnly)
            {
                f.SetValue(obj, value);
                return true;
            }
        }
        catch { }

        return false;
    }

    private static long CountMember(object obj, string member)
    {
        object value = GetMember(obj, member);
        if (value == null) return -1;
        try
        {
            if (value is ICollection collection) return collection.Count;
        }
        catch { }

        foreach (string name in new[] { "Count", "Length", "LongLength" })
        {
            object count = GetMember(value, name);
            if (count == null) continue;
            try { return Convert.ToInt64(count); } catch { }
        }
        return -1;
    }

    private static int ToInt(object value, int fallback)
    {
        if (value == null) return fallback;
        try { return Convert.ToInt32(value); } catch { return fallback; }
    }

    private static float ToFloat(object value, float fallback)
    {
        if (value == null) return fallback;
        try { return Convert.ToSingle(value); } catch { return fallback; }
    }

    private static bool TryReadStaticInt(Type type, string name, out int value)
    {
        value = 0;
        if (type == null) return false;
        try
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo p = type.GetProperty(name, flags);
            if (p != null && p.CanRead)
            {
                value = Convert.ToInt32(p.GetValue(null));
                return true;
            }

            MethodInfo getter = type.GetMethod("get_" + name, flags, null, Type.EmptyTypes, null);
            if (getter != null)
            {
                value = Convert.ToInt32(getter.Invoke(null, null));
                return true;
            }
        }
        catch { }
        return false;
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type t = a.GetType(fullName, false, false);
                if (t != null) return t;
            }
            catch { }
        }
        return null;
    }

    private static string Safe(object value)
    {
        if (value == null) return "<null>";
        try { return value.ToString() ?? "<null>"; } catch { return "<unprintable>"; }
    }

    private static string FormatMethod(MethodBase m)
    {
        if (m == null) return "<null>";
        try { return $"{m.DeclaringType?.FullName}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"; }
        catch { return m.Name; }
    }

    // Core map-generation and compatibility behavior is mandatory in the stable
    // release. Keeping these as constants also prevents stale pre-1.0 config values
    // from accidentally disabling required fixes after an upgrade.
    // Restart-only setting. The native function bodies and memory allocation sizes
    // cannot safely change in the middle of a battle.
    private static int ReadRequestedMapSize()
    {
        float requested = ReadFloat("TacticalMapSize", 80f);
        if (float.IsNaN(requested) || float.IsInfinity(requested)) return 64;
        // All five values fit the verified imm8 native loop sites; never request
        // a size >96 or an untested non-8-aligned gameplay grid.
        int snapped = (int)(Math.Floor(Math.Clamp(requested, 64f, 96f) / 8f + 0.5f) * 8);
        return Math.Clamp(snapped, 64, 96);
    }

    private static bool Enabled() => true;
    private static bool Debug() => false;
    private static bool AreaDiagnostics() => ReadBool("DiagnoseMissionAreas", false);
    private static bool ReplaceBlueprint() => true;
    private static bool FixTileHighlighter() => true;
    private static bool ExpandEnemySpawnAreas() => true;
    private static bool ExtendedZoomOut() => ReadBool("ExtendedZoomOut", true);
    private static bool AIDiagnostics() => false;
    private static float MissionRoundMultiplier() => Math.Clamp(ReadFloat("MissionRoundMultiplier", 2f), 1f, 3f);

    private static float ReadFloat(string key, float fallback)
    {
        try
        {
            object value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToSingle(value);
        }
        catch { return fallback; }
    }

    private static bool ReadBool(string key, bool fallback)
    {
        try
        {
            object value = ModSettings.Get(ModName, key);
            return value == null ? fallback : Convert.ToBoolean(value);
        }
        catch { return fallback; }
    }
}
