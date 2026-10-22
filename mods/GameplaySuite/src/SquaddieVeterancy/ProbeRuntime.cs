using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Strategy;
using Il2CppMenace.Items;
using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Il2CppMenace.UI.Tactical;
using UnityEngine;
using UnityEngine.UIElements;
using Menace.SDK;

namespace SquaddieProbe;

internal sealed class ProbeRuntime : IDisposable
{
    private sealed class LiveElementState
    {
        public Element? Element;
        public int SquaddieId;
        public int ElementIndex = -1;
        public long ParentEntityPtr;
        public int BaseMaxHp;
        public int AppliedBonus;
        public bool InitialHpSettled;
        public bool PlayerSquaddie;
    }

    private sealed class ElementCreateHpState
    {
        public bool PlayerSquaddie;
        public int SquaddieId;
        public int BaseHp;
        public int AppliedBonus;
        public long OwnerPtr;
    }

    private sealed class HitchanceMutationState
    {
        public object? AccuracyTarget;
        public MemberInfo? AccuracyMember;
        public float AccuracyOriginal;
        public object? DefenseTarget;
        public MemberInfo? DefenseMember;
        public float DefenseOriginal;
        public int AttackerSid;
        public int DefenderSid;
        public int AccuracyPct;
        public int DefensePct;
    }

    private sealed class VeterancyPropertiesOwnerState
    {
        public int SquaddieId;
        public string PilotTemplateId = "";
        public bool IsTemporaryNpc;
        public bool NpcVehicle;
        public bool NpcEnemy;
        public VeterancyRank NpcRank;
        public long OwnerPtr;
        public long LastSeenAtMs;
    }

    private sealed class IncomingTargetHint
    {
        public int DefenderSid;
        public string DefenderPilotTemplateId = "";
        public bool PressureVehicle;
        public long TargetOwnerPtr;
        public long SkillPtr;
        public long AttackerPtr;
        public long TargetTilePtr;
        public long SeenAtMs;
    }

    private sealed class VehiclePressureState
    {
        public long OwnerPtr;
        public Actor? Actor;
        public VeterancyRank Rank;
        public string Identity = "";
        public float Pressure;
        public Vector3 LastThreatPos;
        public bool HasThreat;
        public bool BreakContactPending;
        public bool RetreatInProgress;
        public long RetreatStartedAtMs;
        public bool BreakHandledThisActivation;
        // Keep the vehicle visibly in Break Contact until its forced-retreat activation
        // has actually ended.  Dropping to Buttoned Up while AP is still forced to zero
        // made a Buttoned vehicle look/behave like it was panicking.
        public bool PostBreakPressureResetPending;
        public bool ApPenaltyAppliedThisActivation;
        public long ActivationSerial;
        public float LastHudValue = -1f;
        public bool HudReadyLogged;
        public int HudStatusState;
        public long HudStatusMessageUntilMs;
    }

    private enum ObjectiveKind
    {
        Unknown = 0,
        Primary = 1,
        Secondary = 2
    }

    private readonly global::HarmonyLib.Harmony _harmony;
    private readonly Action<string> _log;
    private readonly SquaddieIdentityCache _identities;
    private readonly ExperienceStore _xp;
    private readonly Dictionary<long, LiveElementState> _liveElements = new();
    private readonly Dictionary<string, long> _xpDedup = new();
    private readonly Dictionary<string, IncomingTargetHint> _incomingTargetHints = new();
    private readonly Dictionary<string, long> _malfunctionDedup = new();
    private readonly HashSet<MethodBase> _patched = new();
    private readonly Dictionary<long, ReliabilityContext> _reliabilityContexts = new();
    private readonly Dictionary<long, long> _skillOwnerBySkillPtr = new();
    private readonly HashSet<long> _skillOwnerResolveLogged = new();
    private readonly Dictionary<long, FailureNotice> _failureNotices = new();
    private readonly Dictionary<long, NpcRankState> _npcRanks = new();
    private readonly Dictionary<long, NpcRankState> _npcVehicleRanks = new();
    private readonly Dictionary<long, string> _ownerTemplateIds = new();
    private readonly HashSet<long> _ownerTemplateDiagnosticsLogged = new();
    private readonly HashSet<long> _enemyOwnersLogged = new();
    private readonly HashSet<long> _enemyVehicleOwnersLogged = new();
    private readonly HashSet<long> _alliedNpcOwnersLogged = new();
    private readonly HashSet<long> _unclassifiedOwnersLogged = new();
    private readonly HashSet<string> _combatModifierLogs = new();
    private readonly Dictionary<long, VeterancyHudMarker> _veterancyHudMarkers = new();
    private readonly Dictionary<VeterancyRank, Sprite> _veterancySprites = new();
    private readonly Dictionary<long, VeterancyPropertiesOwnerState> _veterancyPropertiesOwners = new();
    private readonly Dictionary<int, int> _deployedSquadRepresentatives = new();
    private readonly HashSet<long> _enemyElementKillsAwarded = new();
    private readonly Dictionary<long, ObjectiveKind> _objectiveKinds = new();
    private readonly Dictionary<long, ObjectiveKind> _completedObjectives = new();
    private readonly HashSet<string> _objectiveKindDiagnostics = new();
    private readonly HashSet<int> _downedSquaddiesThisMission = new();
    private readonly Dictionary<string, PilotIdentitySnapshot> _pilotRoster = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PilotIdentitySnapshot> _deployedPilots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<long> _vehicleElementKillsAwarded = new();
    private readonly HashSet<long> _playerVehicleDestructionsResolved = new();

    private sealed class ReliabilityDecision
    {
        public long DecisionKey;
        public int Sid;
        public bool IsEnemy;
        public bool IsAlliedNpc;
        public int FactionId = -1;
        public long OwnerPtr;
        public VeterancyRank Rank;
        public int ElementIndex;
        public long ElementPtr;
        public Element? Element;
        public int ChancePct;
        public int Roll;
        public bool Skip;
        public bool Candidate;
    }

    private sealed class ReliabilityContext
    {
        public long Serial;
        public long SkillPtr;
        public long OwnerPtr;
        public long CreatedAtMs;
        public long LastTouchedAtMs;
        public bool Committed;
        public bool Consumed;
        // Some AI attacks commit and finish Skill.Use before MENACE asks for the
        // squad execution list. A late execution context inherits that already-real
        // commit so filtering, HUD feedback and XP semantics still belong to the
        // attack rather than being mistaken for UI preflight.
        public bool LateCommitted;
        public bool FailureNoticeQueued;
        public int UseDepth;
        public readonly Dictionary<long, ReliabilityDecision> Decisions = new();
        public readonly HashSet<string> ListLogs = new();
        public readonly HashSet<string> CountLogs = new();
    }

    private sealed class NpcRankState
    {
        public VeterancyRank Rank;
        public int FactionId;
        public string TemplateId = "";
        public long OwnerPtr;
        public int ElementIndex;
    }

    private sealed class FailureNotice
    {
        public Actor Actor = null!;
        public int Count;
        public float CreatedAt;
        public float ExpiresAt;
        public Label? Label;
    }

    private sealed class VeterancyHudMarker
    {
        public VisualElement Element = null!;
        public long HudKey;
        public VeterancyRank Rank;
        public bool Enemy;
    }

    private static ProbeRuntime? Current;

    internal static int GetSquaddieXpForCompatibility(int squaddieId)
    {
        var current = Current;
        if (current == null || squaddieId <= 0) return -1;
        return current._xp.GetXp(squaddieId);
    }
    private static bool _hpMutationGuard;
    private int _suppressionImpactPropertyValue = int.MinValue;

    private bool _enabled = true;
    private bool _awardXp = true;
    private bool _teamHpBuffs = true;
    private bool _verbose;
    private bool _malfunctionDiagnostics = false;
    private bool _individualReliability = true;
    private bool _enemyReliability = true;
    private bool _alliedNpcReliability = true;
    private bool _detailedNpcProfiles = true;
    private bool _showFailureNotice = true;
    private bool _showVeterancyMarkers = true;
    private bool _teamAccuracyMods = true;
    private bool _teamDefenseMods = true;
    private bool _teamSuppressionResistanceMods = true;
    private bool _teamConcealmentMods = true;
    private bool _rescueXpLoss = true;
    private int _rescueXpLossPct = 25;
    private bool _rescueResolutionPending;
    private int _rescueResolutionBaselineGeneration;
    private long _rescueResolutionDeadlineMs;

    // ExecLoad runs before MENACE has necessarily replaced the current StrategyState.
    // While this guard is active, never reconcile sidecar identities against the
    // pre-load roster (for example: New Campaign -> Load older campaign).
    private bool _loadRosterSyncPending;
    private int _loadRosterSyncBaselineGeneration;
    private long _loadRosterSyncBaselineStatePtr;
    private long _loadRosterSyncStartedAtMs;
    private long _nextLoadRosterWaitLogAtMs;

    private bool _vehicleCrewVeterancy = true;
    private int _vehicleXpPerAttack = 1;
    private int _vehicleXpPerEnemyElementKill = 5;
    private int _vehicleXpPerIncomingAttack = 2;
    private int _vehicleXpPerMission = 10;
    private int _vehicleXpPerMissionSurvived = 10;
    private int _vehicleXpPerPrimaryObjective = 25;
    private int _vehicleXpPerSecondaryObjective = 10;
    private int _vehicleDestroyedCrewSurvivalPct = 50;
    private int _vehicleCrewWoundedXpLossPct = 25;
    private int _vehicleMinorDefectInjuryPct = 25;
    private int _vehicleMajorDefectDeathPct = 50;
    private bool _vehicleCrewPressure = true;
    private int _vehiclePressureReceivedPct = 50;
    private int _vehicleDamagePressureBump = 40;
    private int _vehicleLightDefectPressureBump = 10;
    private int _vehicleMediumDefectPressureBump = 20;
    private int _vehicleHeavyDefectPressureBump = 35;
    private int _vehicleButtonedThreshold = 50;
    private int _vehicleBreakContactThreshold = 100;
    private int _vehicleButtonedApPenalty = 20;
    private int _vehicleButtonedAccuracyPenaltyPct = 20;
    private int _vehiclePressureRecoveryPerTurn = 20;
    private int _vehiclePressureAfterBreakContact = 60;
    private int _vehicleBreakContactMaxTiles = 8;
    private int _vehicleRallyPressureReduction = 80;
    private bool _vehicleCrewPressureRankMods = true;
    private int _vehicleGreenPressureResistancePct = -6;
    private int _vehicleCombatTestedPressureResistancePct = -4;
    private int _vehicleHardenedPressureResistancePct = -2;
    private int _vehicleRegularPressureResistancePct;
    private int _vehicleVeteranPressureResistancePct = 2;
    private int _vehicleCrackPressureResistancePct = 4;
    private int _vehicleElitePressureResistancePct = 6;
    private readonly Dictionary<long, VehiclePressureState> _vehiclePressureStates = new();
    private long _vehiclePressureActiveActorPtr;
    private long _vehiclePressureActivationSerial;
    private long _nextPilotRosterRefreshMs;
    private long _lastPilotStatePtr;
    private int _lastLoggedPilotCount = -1;
    private bool _missionVehicleXpAwarded;

    private int _greenNoFirePct = 10;
    private int _combatTestedNoFirePct = 5;
    private int _hardenedNoFirePct = 3;
    private int _regularNoFirePct = 2;
    private int _suppressedNoFirePct = 5;
    private int _xpPerAttack = 1;
    private int _xpPerEnemyElementKill = 5;
    private int _xpPerEnemyVehicleKill = 50;
    private int _xpPerIncomingAttack = 10;
    private int _xpPerPrimaryObjective = 25;
    private int _xpPerSecondaryObjective = 10;
    private int _combatTestedHp = 1;
    private int _hardenedHp = 1;
    private int _regularHp = 2;
    private int _veteranHp = 3;
    private int _greenAccuracyPct = -6;
    private int _combatTestedAccuracyPct = -4;
    private int _hardenedAccuracyPct = -2;
    private int _regularAccuracyPct;
    private int _veteranAccuracyPct = 2;
    private int _crackAccuracyPct = 4;
    private int _eliteAccuracyPct = 6;
    private int _greenDefensePct = -6;
    private int _combatTestedDefensePct = -4;
    private int _hardenedDefensePct = -2;
    private int _regularDefensePct;
    private int _veteranDefensePct = 2;
    private int _crackDefensePct = 4;
    private int _eliteDefensePct = 6;
    private int _greenSuppressionResistancePct = -6;
    private int _combatTestedSuppressionResistancePct = -4;
    private int _hardenedSuppressionResistancePct = -2;
    private int _regularSuppressionResistancePct;
    private int _veteranSuppressionResistancePct = 2;
    private int _crackSuppressionResistancePct = 4;
    private int _eliteSuppressionResistancePct = 6;
    private int _greenConcealment = -2;
    private int _combatTestedConcealment = -1;
    private int _hardenedConcealment = -1;
    private int _regularConcealment;
    private int _veteranConcealment = 1;
    private long _selectedKey;
    private int _xpAwards;
    private int _malfunctionElementEvents;
    private int _jamChecks;
    private int _nativeMalfunctions;
    private long _recentMalfunctionAtMs;
    private long _recentMalfunctionElementPtr;
    private long _recentMalfunctionSkillPtr;
    private int _recentMalfunctionSid;
    private string _sceneName = "?";
    private long _reliabilitySerial;
    private int _reliabilityRolls;
    private int _reliabilityNoFires;
    private int _reliabilityFilteredLists;
    private int _reliabilityAdjustedCounts;
    private int _enemyReliabilityRolls;
    private int _enemyReliabilityNoFires;
    private int _alliedNpcReliabilityRolls;
    private int _alliedNpcReliabilityNoFires;
    private float _nextVeterancyHudRefreshAt;
    private bool _missionObjectiveXpAwarded;

    public string Status { get; private set; } = "starting";
    public string SelectedSummary { get; private set; } = "-";
    public int PatchedMethodCount => _patched.Count;
    public int CachedSquaddieCount => _identities.Count;
    public int CachedPilotCount => _pilotRoster.Count;
    public int PersistedRecordCount => _xp.RecordCount;
    public int PersistedPilotRecordCount => _xp.PilotRecordCount;
    public int XpAwards => _xpAwards;
    public string PersistencePath => _xp.PathOnDisk;
    public string CurrentSaveLabel => _xp.CurrentSaveLabel;
    public bool HasUnsavedXp => _xp.HasUnsavedChanges;
    public bool SaveWatcherReady => _xp.SaveWatcherReady;
    public int MalfunctionElementEvents => _malfunctionElementEvents;
    public int JamChecks => _jamChecks;
    public int NativeMalfunctions => _nativeMalfunctions;
    public bool SquadMappingReady => _identities.SquadMappingReady;
    public int ReliabilityRolls => _reliabilityRolls;
    public int ReliabilityNoFires => _reliabilityNoFires;
    public int ReliabilityFilteredLists => _reliabilityFilteredLists;
    public int ReliabilityAdjustedCounts => _reliabilityAdjustedCounts;
    public int NpcRankAssignments => _npcRanks.Count + _npcVehicleRanks.Count;
    public int EnemyReliabilityRolls => _enemyReliabilityRolls;
    public int EnemyReliabilityNoFires => _enemyReliabilityNoFires;
    public int AlliedNpcReliabilityRolls => _alliedNpcReliabilityRolls;
    public int AlliedNpcReliabilityNoFires => _alliedNpcReliabilityNoFires;
    public string SceneName => _sceneName;

    public ProbeRuntime(global::HarmonyLib.Harmony harmony, Action<string> log)
    {
        _harmony = harmony;
        _log = log;
        _identities = new SquaddieIdentityCache(_log);
        _xp = new ExperienceStore(_log);
        Current = this;
        try
        {
            TacticalEventHooks.OnElementMalfunction += OnNativeElementMalfunction;
            TacticalEventHooks.OnSkillUsed += OnNativeSkillUsedForXp;
            TacticalEventHooks.OnObjectiveStateChanged += OnNativeObjectiveStateChangedForXp;
            _log("MALFUNCTION EVENT attached TacticalEventHooks.OnElementMalfunction");
            _log("XP EVENTS attached TacticalEventHooks.OnSkillUsed + OnObjectiveStateChanged");
        }
        catch (Exception e)
        {
            _log($"MALFUNCTION EVENT attach failed {e.GetType().Name}: {e.Message}");
        }
        InstallHooks();
        Status = "ready";
    }

    public void Dispose()
    {
        try
        {
            TacticalEventHooks.OnElementMalfunction -= OnNativeElementMalfunction;
            TacticalEventHooks.OnSkillUsed -= OnNativeSkillUsedForXp;
            TacticalEventHooks.OnObjectiveStateChanged -= OnNativeObjectiveStateChangedForXp;
        }
        catch (Exception e)
        {
            _log($"EVENT detach during shutdown failed {e.GetType().Name}: {e.Message}");
        }

        try { _xp.Dispose(); }
        catch (Exception e) { _log($"XP store shutdown failed {e.GetType().Name}: {e.Message}"); }

        if (ReferenceEquals(Current, this)) Current = null;
        Status = "disposed";
    }

    public void Configure(bool enabled, bool awardXp, int xpPerAttack,
        int xpPerEnemyElementKill, int xpPerEnemyVehicleKill, int xpPerIncomingAttack, int xpPerPrimaryObjective, int xpPerSecondaryObjective,
        bool rescueXpLoss, int rescueXpLossPct,
        bool vehicleCrewVeterancy, int vehicleXpPerAttack, int vehicleXpPerEnemyElementKill, int vehicleXpPerIncomingAttack,
        int vehicleXpPerMission, int vehicleXpPerMissionSurvived, int vehicleXpPerPrimaryObjective, int vehicleXpPerSecondaryObjective,
        int vehicleDestroyedCrewSurvivalPct, int vehicleCrewWoundedXpLossPct, int vehicleMinorDefectInjuryPct, int vehicleMajorDefectDeathPct,
        bool vehicleCrewPressure, int vehiclePressureReceivedPct, int vehicleDamagePressureBump,
        int vehicleLightDefectPressureBump, int vehicleMediumDefectPressureBump, int vehicleHeavyDefectPressureBump,
        int vehicleButtonedThreshold, int vehicleBreakContactThreshold,
        int vehicleButtonedApPenalty, int vehicleButtonedAccuracyPenaltyPct, int vehiclePressureRecoveryPerTurn,
        int vehiclePressureAfterBreakContact, int vehicleBreakContactMaxTiles, int vehicleRallyPressureReduction, bool vehicleCrewPressureRankMods,
        int vehicleGreenPressureResistancePct, int vehicleCombatTestedPressureResistancePct, int vehicleHardenedPressureResistancePct,
        int vehicleRegularPressureResistancePct, int vehicleVeteranPressureResistancePct, int vehicleCrackPressureResistancePct,
        int vehicleElitePressureResistancePct,
        bool teamHpBuffs,
        int combatTestedHp, int hardenedHp, int regularHp, int veteranHp,
        bool teamAccuracyMods, int greenAccuracyPct, int combatTestedAccuracyPct, int hardenedAccuracyPct, int regularAccuracyPct, int veteranAccuracyPct, int crackAccuracyPct, int eliteAccuracyPct,
        bool teamDefenseMods, int greenDefensePct, int combatTestedDefensePct, int hardenedDefensePct, int regularDefensePct, int veteranDefensePct, int crackDefensePct, int eliteDefensePct,
        bool teamSuppressionResistanceMods, int greenSuppressionResistancePct, int combatTestedSuppressionResistancePct, int hardenedSuppressionResistancePct, int regularSuppressionResistancePct, int veteranSuppressionResistancePct, int crackSuppressionResistancePct, int eliteSuppressionResistancePct,
        bool teamConcealmentMods, int greenConcealment, int combatTestedConcealment, int hardenedConcealment, int regularConcealment, int veteranConcealment,
        bool individualReliability, bool enemyReliability, bool alliedNpcReliability, bool detailedNpcProfiles,
        int greenNoFirePct, int combatTestedNoFirePct, int hardenedNoFirePct, int regularNoFirePct, int suppressedNoFirePct,
        bool showFailureNotice, bool showVeterancyMarkers, bool malfunctionDiagnostics, bool verbose)
    {
        int nextCombat = Math.Max(0, combatTestedHp);
        int nextHardened = Math.Max(0, hardenedHp);
        int nextRegular = Math.Max(0, regularHp);
        int nextVeteran = Math.Max(0, veteranHp);
        bool hpConfigChanged = _enabled != enabled
            || _teamHpBuffs != teamHpBuffs
            || _combatTestedHp != nextCombat
            || _hardenedHp != nextHardened
            || _regularHp != nextRegular
            || _veteranHp != nextVeteran;
        bool npcReliabilityChanged = _enemyReliability != enemyReliability
            || _alliedNpcReliability != alliedNpcReliability
            || _detailedNpcProfiles != detailedNpcProfiles;

        _enabled = enabled;
        _awardXp = awardXp;
        _xpPerAttack = Math.Max(0, xpPerAttack);
        _xpPerEnemyElementKill = Math.Max(0, xpPerEnemyElementKill);
        _xpPerEnemyVehicleKill = Math.Max(0, xpPerEnemyVehicleKill);
        _xpPerIncomingAttack = Math.Max(0, xpPerIncomingAttack);
        _xpPerPrimaryObjective = Math.Max(0, xpPerPrimaryObjective);
        _xpPerSecondaryObjective = Math.Max(0, xpPerSecondaryObjective);
        _rescueXpLoss = rescueXpLoss;
        _rescueXpLossPct = Math.Max(0, Math.Min(100, rescueXpLossPct));
        _vehicleCrewVeterancy = vehicleCrewVeterancy;
        _vehicleXpPerAttack = Math.Max(0, vehicleXpPerAttack);
        _vehicleXpPerEnemyElementKill = Math.Max(0, vehicleXpPerEnemyElementKill);
        _vehicleXpPerIncomingAttack = Math.Max(0, vehicleXpPerIncomingAttack);
        _vehicleXpPerMission = Math.Max(0, vehicleXpPerMission);
        _vehicleXpPerMissionSurvived = Math.Max(0, vehicleXpPerMissionSurvived);
        _vehicleXpPerPrimaryObjective = Math.Max(0, vehicleXpPerPrimaryObjective);
        _vehicleXpPerSecondaryObjective = Math.Max(0, vehicleXpPerSecondaryObjective);
        _vehicleDestroyedCrewSurvivalPct = Math.Max(0, Math.Min(100, vehicleDestroyedCrewSurvivalPct));
        _vehicleCrewWoundedXpLossPct = Math.Max(0, Math.Min(100, vehicleCrewWoundedXpLossPct));
        _vehicleMinorDefectInjuryPct = Math.Max(0, Math.Min(100, vehicleMinorDefectInjuryPct));
        _vehicleMajorDefectDeathPct = Math.Max(0, Math.Min(100, vehicleMajorDefectDeathPct));
        bool vehiclePressureWasEnabled = _vehicleCrewPressure;
        _vehicleCrewPressure = vehicleCrewPressure;
        _vehiclePressureReceivedPct = Math.Max(10, Math.Min(100, vehiclePressureReceivedPct));
        _vehicleDamagePressureBump = Math.Max(0, Math.Min(100, vehicleDamagePressureBump));
        _vehicleLightDefectPressureBump = Math.Max(0, Math.Min(75, vehicleLightDefectPressureBump));
        _vehicleMediumDefectPressureBump = Math.Max(0, Math.Min(75, vehicleMediumDefectPressureBump));
        _vehicleHeavyDefectPressureBump = Math.Max(0, Math.Min(100, vehicleHeavyDefectPressureBump));
        _vehicleButtonedThreshold = Math.Max(10, Math.Min(100, vehicleButtonedThreshold));
        _vehicleBreakContactThreshold = Math.Max(_vehicleButtonedThreshold + 1, Math.Min(150, vehicleBreakContactThreshold));
        _vehicleButtonedApPenalty = Math.Max(0, Math.Min(60, vehicleButtonedApPenalty));
        _vehicleButtonedAccuracyPenaltyPct = Math.Max(0, Math.Min(60, vehicleButtonedAccuracyPenaltyPct));
        _vehiclePressureRecoveryPerTurn = Math.Max(0, Math.Min(100, vehiclePressureRecoveryPerTurn));
        _vehiclePressureAfterBreakContact = Math.Max(0, Math.Min(_vehicleBreakContactThreshold - 1, vehiclePressureAfterBreakContact));
        _vehicleBreakContactMaxTiles = Math.Max(2, Math.Min(12, vehicleBreakContactMaxTiles));
        _vehicleRallyPressureReduction = Math.Max(0, Math.Min(150, vehicleRallyPressureReduction));
        _vehicleCrewPressureRankMods = vehicleCrewPressureRankMods;
        _vehicleGreenPressureResistancePct = Math.Max(-50, Math.Min(75, vehicleGreenPressureResistancePct));
        _vehicleCombatTestedPressureResistancePct = Math.Max(-50, Math.Min(75, vehicleCombatTestedPressureResistancePct));
        _vehicleHardenedPressureResistancePct = Math.Max(-50, Math.Min(75, vehicleHardenedPressureResistancePct));
        _vehicleRegularPressureResistancePct = Math.Max(-50, Math.Min(75, vehicleRegularPressureResistancePct));
        _vehicleVeteranPressureResistancePct = Math.Max(-50, Math.Min(75, vehicleVeteranPressureResistancePct));
        _vehicleCrackPressureResistancePct = Math.Max(-50, Math.Min(75, vehicleCrackPressureResistancePct));
        _vehicleElitePressureResistancePct = Math.Max(-50, Math.Min(75, vehicleElitePressureResistancePct));
        if (vehiclePressureWasEnabled && !_vehicleCrewPressure)
        {
            HideVehiclePressureBars();
            _vehiclePressureStates.Clear();
            _vehiclePressureActiveActorPtr = 0;
        }
        _teamHpBuffs = teamHpBuffs;
        _combatTestedHp = nextCombat;
        _hardenedHp = nextHardened;
        _regularHp = nextRegular;
        _veteranHp = nextVeteran;
        _teamAccuracyMods = teamAccuracyMods;
        _greenAccuracyPct = Math.Max(-50, Math.Min(50, greenAccuracyPct));
        _combatTestedAccuracyPct = Math.Max(-50, Math.Min(50, combatTestedAccuracyPct));
        _hardenedAccuracyPct = Math.Max(-50, Math.Min(50, hardenedAccuracyPct));
        _regularAccuracyPct = Math.Max(-50, Math.Min(50, regularAccuracyPct));
        _veteranAccuracyPct = Math.Max(-50, Math.Min(50, veteranAccuracyPct));
        _crackAccuracyPct = Math.Max(-50, Math.Min(50, crackAccuracyPct));
        _eliteAccuracyPct = Math.Max(-50, Math.Min(50, eliteAccuracyPct));
        _teamDefenseMods = teamDefenseMods;
        _greenDefensePct = Math.Max(-50, Math.Min(50, greenDefensePct));
        _combatTestedDefensePct = Math.Max(-50, Math.Min(50, combatTestedDefensePct));
        _hardenedDefensePct = Math.Max(-50, Math.Min(50, hardenedDefensePct));
        _regularDefensePct = Math.Max(-50, Math.Min(50, regularDefensePct));
        _veteranDefensePct = Math.Max(-50, Math.Min(50, veteranDefensePct));
        _crackDefensePct = Math.Max(-50, Math.Min(50, crackDefensePct));
        _eliteDefensePct = Math.Max(-50, Math.Min(50, eliteDefensePct));
        _teamSuppressionResistanceMods = teamSuppressionResistanceMods;
        _greenSuppressionResistancePct = Math.Max(-50, Math.Min(50, greenSuppressionResistancePct));
        _combatTestedSuppressionResistancePct = Math.Max(-50, Math.Min(50, combatTestedSuppressionResistancePct));
        _hardenedSuppressionResistancePct = Math.Max(-50, Math.Min(50, hardenedSuppressionResistancePct));
        _regularSuppressionResistancePct = Math.Max(-50, Math.Min(50, regularSuppressionResistancePct));
        _veteranSuppressionResistancePct = Math.Max(-50, Math.Min(50, veteranSuppressionResistancePct));
        _crackSuppressionResistancePct = Math.Max(-50, Math.Min(50, crackSuppressionResistancePct));
        _eliteSuppressionResistancePct = Math.Max(-50, Math.Min(50, eliteSuppressionResistancePct));
        _teamConcealmentMods = teamConcealmentMods;
        _greenConcealment = Math.Max(-20, Math.Min(20, greenConcealment));
        _combatTestedConcealment = Math.Max(-20, Math.Min(20, combatTestedConcealment));
        _hardenedConcealment = Math.Max(-20, Math.Min(20, hardenedConcealment));
        _regularConcealment = Math.Max(-20, Math.Min(20, regularConcealment));
        _veteranConcealment = Math.Max(-20, Math.Min(20, veteranConcealment));
        _individualReliability = individualReliability;
        _enemyReliability = enemyReliability;
        _alliedNpcReliability = alliedNpcReliability;
        _detailedNpcProfiles = detailedNpcProfiles;
        if (npcReliabilityChanged)
        {
            // Do not let a pending NPC roll survive a runtime option/profile change.
            ResetReliabilityContext();
            _npcRanks.Clear();
            _npcVehicleRanks.Clear();
            _enemyOwnersLogged.Clear();
            _enemyVehicleOwnersLogged.Clear();
            _alliedNpcOwnersLogged.Clear();
            _unclassifiedOwnersLogged.Clear();
        }
        _greenNoFirePct = Math.Max(0, Math.Min(100, greenNoFirePct));
        _combatTestedNoFirePct = Math.Max(0, Math.Min(100, combatTestedNoFirePct));
        _hardenedNoFirePct = Math.Max(0, Math.Min(100, hardenedNoFirePct));
        _regularNoFirePct = Math.Max(0, Math.Min(100, regularNoFirePct));
        _suppressedNoFirePct = Math.Max(0, Math.Min(100, suppressedNoFirePct));
        _showFailureNotice = showFailureNotice;
        if (!_showFailureNotice) ClearFailureNotices();
        _showVeterancyMarkers = showVeterancyMarkers;
        if (!_showVeterancyMarkers) ClearVeterancyHudMarkers();
        _malfunctionDiagnostics = malfunctionDiagnostics;
        _verbose = verbose;

        if (hpConfigChanged)
        {
            foreach (var state in _liveElements.Values.ToList())
                ReapplyHpBonus(state);
        }
    }

    public void SceneChanged(string sceneName)
    {
        _vehiclePressureStates.Clear();
        _vehiclePressureActiveActorPtr = 0;
        _vehiclePressureActivationSerial = 0;
        string previousScene = _sceneName;
        bool enteringTactical = sceneName.Equals("Tactical", StringComparison.OrdinalIgnoreCase);
        bool postMissionRosterScene = IsPostMissionRosterScene(sceneName);
        bool returningFromTactical = previousScene.Equals("Tactical", StringComparison.OrdinalIgnoreCase)
            && postMissionRosterScene;

        if (enteringTactical)
        {
            ResetRescueTracking("new-tactical");
        }
        else if (returningFromTactical && _downedSquaddiesThisMission.Count > 0)
        {
            _rescueResolutionPending = true;
            _rescueResolutionBaselineGeneration = _identities.RefreshGeneration;
            _rescueResolutionDeadlineMs = Environment.TickCount64 + 10000;
            _log($"RESCUE XP pending scene={sceneName} casualties={_downedSquaddiesThisMission.Count} baselineGeneration={_rescueResolutionBaselineGeneration}");
        }
        else if (!postMissionRosterScene
            && !enteringTactical
            && _downedSquaddiesThisMission.Count > 0)
        {
            ResetRescueTracking($"scene-{sceneName}");
        }

        _sceneName = sceneName;
        _selectedKey = 0;
        _liveElements.Clear();
        _xpDedup.Clear();
        _incomingTargetHints.Clear();
        _malfunctionDedup.Clear();
        _recentMalfunctionAtMs = 0;
        _recentMalfunctionElementPtr = 0;
        _recentMalfunctionSkillPtr = 0;
        _recentMalfunctionSid = 0;
        _skillOwnerBySkillPtr.Clear();
        _skillOwnerResolveLogged.Clear();
        _npcRanks.Clear();
        _npcVehicleRanks.Clear();
        _ownerTemplateIds.Clear();
        _ownerTemplateDiagnosticsLogged.Clear();
        _enemyOwnersLogged.Clear();
        _enemyVehicleOwnersLogged.Clear();
        _alliedNpcOwnersLogged.Clear();
        _unclassifiedOwnersLogged.Clear();
        _combatModifierLogs.Clear();
        _veterancyPropertiesOwners.Clear();
        _deployedSquadRepresentatives.Clear();
        _deployedPilots.Clear();
        _vehicleElementKillsAwarded.Clear();
        _playerVehicleDestructionsResolved.Clear();
        _enemyElementKillsAwarded.Clear();
        _pilotRoster.Clear();
        _nextPilotRosterRefreshMs = 0;
        _lastPilotStatePtr = 0;
        _lastLoggedPilotCount = -1;
        _missionVehicleXpAwarded = false;
        _objectiveKinds.Clear();
        _completedObjectives.Clear();
        _objectiveKindDiagnostics.Clear();
        _missionObjectiveXpAwarded = false;
        ClearFailureNotices();
        ClearVeterancyHudMarkers();
        _nextVeterancyHudRefreshAt = 0f;
        ResetReliabilityContext();
        _identities.SceneChanged(sceneName);
        // Do NOT write XP here. Scene transitions are not save points; writing here
        // would make a later game-save reload keep future XP.
        _log($"SCENE name={sceneName}");
    }

    public void Update()
    {
        // Keep the identity cache alive even when gameplay effects are toggled off.
        // Snapshot writes are held while no authoritative roster is available so a
        // game/API patch cannot overwrite valid squaddie XP with an empty sidecar.
        _identities.Update();
        _xp.SetSnapshotWritesSuspended(!_identities.HasSnapshot);
        _xp.Update();
        UpdateFailureNotices();
        if (!_enabled)
        {
            ClearVeterancyHudMarkers();
            return;
        }

        var rosterSnapshot = _identities.GetRosterSnapshot();
        bool rosterSafe = TryReleaseLoadRosterSyncGuard(rosterSnapshot);
        if (rosterSafe)
        {
            _xp.SyncRoster(rosterSnapshot);
            if (_vehicleCrewVeterancy) RefreshPilotRoster(syncStore: true);
        }
        ResolveRescuedSquaddieXpLoss();
        PruneXpDedup();
        PruneReliabilityContexts();

        TacticalManager manager;
        try { manager = TacticalManager.Get(); }
        catch { Status = $"{_sceneName}: waiting for TacticalManager"; return; }
        if (!Valid(manager)) { Status = $"{_sceneName}: strategy/non-tactical"; return; }
        Status = "tactical manager active";

        FinalizePendingInitialHp();
        UpdateVeterancyHudMarkers();
        UpdateVehiclePressureSystem(manager);

        Actor? actor = null;
        try { actor = manager.GetActiveActor(); } catch { }
        if (!Valid(actor)) { SelectedSummary = "none"; return; }

        long key = Key(actor);
        if (key == _selectedKey) return;
        _selectedKey = key;
        string name = SafeCall(actor!, "GetDisplayName") ?? SafeCall(actor!, "GetName") ?? "?";
        SelectedSummary = $"{name} key=0x{key:X}";
    }

    private bool TryReleaseLoadRosterSyncGuard(IReadOnlyList<SquaddieIdentitySnapshot> roster)
    {
        if (!_loadRosterSyncPending) return true;

        long now = Environment.TickCount64;
        int generation = _identities.RefreshGeneration;
        long statePtr = _identities.CurrentStatePointer;
        bool refreshedAfterLoad = generation > _loadRosterSyncBaselineGeneration;
        bool stateChanged = statePtr != 0
            && _loadRosterSyncBaselineStatePtr != 0
            && statePtr != _loadRosterSyncBaselineStatePtr;

        // A fresh timeline with no sidecar records has nothing that identity
        // reconciliation could destroy, so it is safe to resume immediately once
        // a post-load roster refresh exists.
        if (_xp.RecordCount == 0 && refreshedAfterLoad)
        {
            _loadRosterSyncPending = false;
            _log($"XP LOAD ROSTER GUARD released reason=fresh-timeline generation={generation} state=0x{statePtr:X}");
            return true;
        }

        int matching = 0, mismatching = 0, comparable = 0;
        bool compatible = roster.Count > 0
            && _xp.IsRosterCompatibleWithLoadedRecords(roster, out matching, out mismatching, out comparable);

        // Normal case: MENACE has either installed a new StrategyState for the
        // selected save, or (for same-state loads) the visible roster now matches
        // the identities in the selected save's sidecar.
        if (refreshedAfterLoad && roster.Count > 0 && (stateChanged || compatible))
        {
            _loadRosterSyncPending = false;
            string reason = stateChanged ? "strategy-state-changed" : "roster-matches-sidecar";
            _log($"XP LOAD ROSTER GUARD released reason={reason} generation={generation} state=0x{statePtr:X} roster={roster.Count} comparable={comparable} matching={matching} mismatching={mismatching}");
            return true;
        }

        // If a save's sidecar is genuinely stale/corrupt, do not keep the guard
        // forever. After several successful refreshes and a generous settle period,
        // allow the normal identity safety check to reset mismatched IDs. This path
        // must never trigger during the ordinary New Campaign -> Load transition.
        long elapsed = now - _loadRosterSyncStartedAtMs;
        if (elapsed >= 15000
            && generation >= _loadRosterSyncBaselineGeneration + 3
            && roster.Count > 0)
        {
            _loadRosterSyncPending = false;
            _log($"XP LOAD ROSTER GUARD timeout after={elapsed}ms generation={generation} state=0x{statePtr:X} roster={roster.Count} comparable={comparable} matching={matching} mismatching={mismatching}; allowing normal identity reconciliation");
            return true;
        }

        if (now >= _nextLoadRosterWaitLogAtMs)
        {
            _nextLoadRosterWaitLogAtMs = now + 2000;
            _log($"XP LOAD ROSTER GUARD waiting generation={generation}/{_loadRosterSyncBaselineGeneration} state=0x{statePtr:X}/0x{_loadRosterSyncBaselineStatePtr:X} roster={roster.Count} comparable={comparable} matching={matching} mismatching={mismatching}");
        }
        return false;
    }

    public IReadOnlyList<VeterancyRow> GetRosterRows()
    {
        var roster = _identities.GetRosterSnapshot();
        var rows = new List<VeterancyRow>(roster.Count);
        foreach (var s in roster.OrderBy(x => _identities.GetSquadNumber(x.Id)).ThenBy(x => x.Id))
        {
            int xp = _xp.GetXp(s.Id);
            double avg = GetSquadAverageXp(s.Id);
            var squadRank = VeterancyRules.RankFromXp(avg);
            rows.Add(new VeterancyRow
            {
                Id = s.Id,
                Name = s.Name,
                Nickname = s.Nickname,
                Alive = s.Alive,
                Xp = xp,
                Rank = VeterancyRules.RankFromXp(xp),
                SquadNumber = _identities.GetSquadNumber(s.Id),
                SquadAverageXp = avg,
                SquadRank = squadRank,
                SquadHpBonus = HpBonusForRank(squadRank),
                SquadAccuracyPct = AccuracyPctForRank(squadRank),
                SquadDefensePct = DefensePctForRank(squadRank),
                SquadSuppressionResistancePct = SuppressionResistancePctForRank(squadRank),
                SquadConcealment = ConcealmentForRank(squadRank)
            });
        }
        return rows;
    }

    public IReadOnlyList<VeterancySquadView> GetRosterSquads(bool deployedOnly)
    {
        var rows = GetRosterRows().Where(x => x.Alive).ToList();
        var deployedKeys = new HashSet<int>(_deployedSquadRepresentatives.Keys);

        if (deployedOnly)
        {
            var deployedSquaddieIds = new HashSet<int>(_liveElements.Values
                .Where(x => x.PlayerSquaddie && x.SquaddieId > 0)
                .Select(x => x.SquaddieId));
            rows = rows.Where(x => deployedSquaddieIds.Contains(x.Id)).ToList();
        }
        else
        {
            // Mission Planning is a squad roster, not the full pool of unassigned
            // replacement squaddies. Fallback squad number 0 entries are deliberately
            // omitted so they do not appear as dozens of one-man pseudo-squads.
            rows = rows.Where(x => x.SquadNumber > 0).ToList();
        }

        var squads = new List<VeterancySquadView>();
        foreach (var group in rows
            .GroupBy(x => x.SquadNumber > 0 ? x.SquadNumber : -x.Id)
            .OrderBy(x => x.Key > 0 ? x.Key : int.MaxValue)
            .ThenBy(x => x.Key))
        {
            var members = group.OrderBy(x => x.Id).ToList();
            if (members.Count == 0) continue;
            var first = members[0];
            squads.Add(new VeterancySquadView
            {
                SquadNumber = first.SquadNumber,
                LeaderName = _identities.GetSquadLeaderName(first.SquadNumber),
                AverageXp = first.SquadAverageXp,
                Rank = first.SquadRank,
                Deployed = deployedKeys.Contains(group.Key),
                Members = members
            });
        }

        return squads;
    }

    public IReadOnlyList<PilotVeterancyView> GetPilotRoster(bool deployedOnly)
    {
        if (!_vehicleCrewVeterancy) return Array.Empty<PilotVeterancyView>();
        if (!_loadRosterSyncPending) RefreshPilotRoster(syncStore: true, force: true);

        IEnumerable<PilotIdentitySnapshot> source = deployedOnly
            ? _deployedPilots.Values
            : _pilotRoster.Values.Where(x => x.Alive);

        return source
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.TemplateId, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                int xp = _xp.GetPilotXp(x.TemplateId);
                var crew = _xp.GetActivePilotCrewMembers(x.TemplateId)
                    .Select(m => new CrewMemberVeterancyView
                    {
                        Slot = m.Slot,
                        Key = m.Key,
                        Role = m.Role,
                        Xp = m.Xp,
                        Status = m.Status
                    })
                    .ToList();
                return new PilotVeterancyView
                {
                    TemplateId = x.TemplateId,
                    PilotName = x.Name,
                    VehicleName = x.VehicleName,
                    VehicleGuid = x.VehicleGuid,
                    Xp = xp,
                    Rank = VeterancyRules.RankFromXp(xp),
                    Crew = crew,
                    ReserveCrewCount = _xp.GetReservePilotCrewCount(x.TemplateId),
                    Alive = x.Alive,
                    Deployed = _deployedPilots.ContainsKey(x.TemplateId)
                };
            })
            .ToList();
    }

    public void SaveXpNow() => _xp.SaveCurrentSnapshotNow();

    public void ResetXp()
    {
        _xp.ResetAll();
        foreach (var state in _liveElements.Values.ToList())
            ReapplyHpBonus(state);
    }

    private void InstallHooks()
    {
        _log("VETERANCY HOOKS BEGIN");
        InstallSaveSystemHooks();
        PatchPrefix(typeof(Skill), "Use", nameof(SkillUsePrefix), new[] { typeof(Tile), typeof(UsageParameter) });
        PatchPostfix(typeof(Skill), "Use", nameof(SkillUsePostfix), new[] { typeof(Tile), typeof(UsageParameter) });
        PatchPrefix(typeof(SkillContainer), "OnSkillUsed", nameof(SkillContainerOnSkillUsedCommitPrefix), new[] { typeof(Skill), typeof(Tile) });
        PatchPostfix(typeof(Skill), "GetExecutingElements", nameof(SkillGetExecutingElementsPostfix), new[] { typeof(ExecutingElementType) });
        PatchPostfix(typeof(Skill), "GetExecutingElementsAmount", nameof(SkillGetExecutingElementsAmountPostfix), new[] { typeof(ExecutingElementType) });
        PatchPrefix(typeof(Skill), "OnAnySkillAppliedByElement", nameof(SkillOnAnySkillAppliedByElementPrefix));
        // The SDK's OnElementDeath event intentionally exposes only the dead element.
        // Patch MENACE's native dispatcher as well so XP can attribute the killing Entity.
        PatchPostfix(typeof(TacticalManager), "InvokeOnElementDeath", nameof(TacticalManagerInvokeOnElementDeathPostfix));
        PatchPostfix(typeof(TacticalManager), "InvokeOnDamageReceived", nameof(TacticalManagerInvokeOnDamageReceivedPostfix), new[] { typeof(Entity), typeof(Entity), typeof(Skill), typeof(DamageInfo) });
        PatchPostfix(typeof(SkillContainer), "OnDefectCaused", nameof(SkillContainerOnDefectCausedPostfix), new[] { typeof(Actor), typeof(DefectTemplate), typeof(Skill) });
        PatchPostfix(typeof(UnitActor), "OnMissionStarted", nameof(UnitActorOnMissionStartedPostfix));
        PatchPostfix(typeof(TacticalManager), "Finish", nameof(TacticalManagerFinishPostfix));
        InstallCreateElementHooks();
        PatchPostfix(typeof(Element), "SetSquaddieId", nameof(ElementSetSquaddieIdPostfix), new[] { typeof(int) });
        InstallHitChanceHooks();
        InstallSquadPropertyHooks();
        // Crew Pressure is the authoritative combat-stress model for every tracked
        // vehicle. Native morale may still change internally after damage, but it must
        // not trigger MENACE's generic panic/flee control path. Buttoned Up therefore
        // means only the configured AP and accuracy penalties; Break Contact remains
        // the separate controlled-retreat state at the higher pressure threshold.
        PatchPostfix(typeof(Actor), "GetMoraleState", nameof(ActorGetMoraleStatePostfix));
        PatchPrefix(typeof(ChangeMalfunctionChanceHandler), "OnBeforeAnySkillUsed", nameof(MalfunctionBeforeAnySkillUsedPrefix));
        PatchPrefix(typeof(ChangeMalfunctionChanceHandler), "OnAnySkillAppliedByElement", nameof(MalfunctionOnAnySkillAppliedByElementPrefix));
        PatchPostfix(typeof(JammingHandler), "IsJammed", nameof(JammingIsJammedPostfix));
        _log($"VETERANCY HOOKS END patched={_patched.Count}");
    }

    private void InstallSaveSystemHooks()
    {
        try
        {
            Type? saveSystemType = typeof(SaveState).Assembly.GetType("Il2CppMenace.Strategy.SaveSystem");
            if (saveSystemType == null)
            {
                saveSystemType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => string.Equals(t.FullName, "Il2CppMenace.Strategy.SaveSystem", StringComparison.Ordinal));
            }

            if (saveSystemType == null)
            {
                _log("SAVE PATH HOOKS unavailable: Il2CppMenace.Strategy.SaveSystem not found");
                return;
            }

            // Do NOT patch TryGetSaveState. It has a by-ref/out SaveState parameter and
            // patching it under IL2CPP can interfere with the game's load-list population.
            // Resolve the selected save only at ExecLoad, where the SaveState is a normal
            // value parameter and the UI has already finished building its list.
            PatchPrefix(saveSystemType, "ExecLoad", nameof(SaveSystemExecLoadPrefix));
        }
        catch (Exception e)
        {
            _log($"SAVE PATH HOOKS ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private void InstallCreateElementHooks()
    {
        try
        {
            MethodInfo? target = global::HarmonyLib.AccessTools.DeclaredMethod(
                typeof(Entity),
                "CreateElement",
                new[] { typeof(UnitLeaderTemplate), typeof(int), typeof(int), typeof(Vector2) });

            // Pre-0.7.15 compatibility: the first parameter used to be GameObject.
            target ??= global::HarmonyLib.AccessTools.DeclaredMethod(
                typeof(Entity),
                "CreateElement",
                new[] { typeof(GameObject), typeof(int), typeof(int), typeof(Vector2) });

            target ??= global::HarmonyLib.AccessTools.GetDeclaredMethods(typeof(Entity))
                .FirstOrDefault(m => string.Equals(m.Name, "CreateElement", StringComparison.Ordinal)
                    && m.GetParameters().Length == 4
                    && m.GetParameters()[1].ParameterType == typeof(int)
                    && m.GetParameters()[2].ParameterType == typeof(int));

            if (target == null)
            {
                _log("HOOK NOT FOUND Il2CppMenace.Tactical.Entity.CreateElement(*,int,int,Vector2)");
                return;
            }

            var prefix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                nameof(EntityCreateElementPrefix), BindingFlags.Static | BindingFlags.NonPublic)!);
            var postfix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                nameof(EntityCreateElementPostfix), BindingFlags.Static | BindingFlags.NonPublic)!);
            _harmony.Patch(target, prefix: prefix, postfix: postfix);
            _patched.Add(target);
            _log("HOOKED " + Signature(target));
        }
        catch (Exception e)
        {
            _log($"HOOK FAILED Il2CppMenace.Tactical.Entity.CreateElement: {e.GetType().Name}: {e.Message}");
        }
    }

    private void InstallHitChanceHooks()
    {
        try
        {
            MethodInfo? target = global::HarmonyLib.AccessTools.GetDeclaredMethods(typeof(Skill))
                .FirstOrDefault(m => (string.Equals(m.Name, "GetHitchance", StringComparison.Ordinal)
                                   || string.Equals(m.Name, "GetHitChance", StringComparison.Ordinal))
                    && m.GetParameters().Length == 7);

            if (target == null)
            {
                _log("HOOK NOT FOUND Il2CppMenace.Tactical.Skills.Skill.GetHitchance(7 args)");
                return;
            }

            var prefix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                nameof(SkillGetHitchancePrefix), BindingFlags.Static | BindingFlags.NonPublic)!);
            var postfix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                nameof(SkillGetHitchancePostfix), BindingFlags.Static | BindingFlags.NonPublic)!);
            _harmony.Patch(target, prefix: prefix, postfix: postfix);
            _patched.Add(target);
            _log("HOOKED " + Signature(target));
        }
        catch (Exception e)
        {
            _log($"HOOK FAILED Il2CppMenace.Tactical.Skills.Skill.GetHitchance: {e.GetType().Name}: {e.Message}");
        }
    }

    private void InstallSquadPropertyHooks()
    {
        try
        {
            // Map the current EntityProperties object back to its persistent player
            // squad. We do not mutate the returned object here: this hook is only
            // identity plumbing for the stat getter postfixes below.
            foreach (Type type in new[] { typeof(UnitActor), typeof(Actor), typeof(Entity) })
            {
                foreach (MethodInfo target in global::HarmonyLib.AccessTools.GetDeclaredMethods(type)
                    .Where(m => (string.Equals(m.Name, "GetCurrentProperties", StringComparison.Ordinal)
                              || string.Equals(m.Name, "GetProperties", StringComparison.Ordinal))
                             && m.GetParameters().Length == 0
                             && typeof(EntityProperties).IsAssignableFrom(m.ReturnType)))
                {
                    if (_patched.Contains(target)) continue;
                    var postfix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                        nameof(VeterancyPropertiesOwnerPostfix), BindingFlags.Static | BindingFlags.NonPublic)!);
                    postfix.priority = global::HarmonyLib.Priority.Last;
                    _harmony.Patch(target, postfix: postfix);
                    _patched.Add(target);
                    _log("HOOKED " + Signature(target) + " [veterancy property owner map]");
                }
            }

            MethodInfo? concealment = global::HarmonyLib.AccessTools.DeclaredMethod(typeof(EntityProperties), "GetConcealment");
            if (concealment != null)
            {
                var postfix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                    nameof(EntityPropertiesGetConcealmentPostfix), BindingFlags.Static | BindingFlags.NonPublic)!);
                // Squad Size Modifiers also patches concealment. Running last means
                // our veterancy delta is added to the value SSM already produced.
                postfix.priority = global::HarmonyLib.Priority.Last;
                _harmony.Patch(concealment, postfix: postfix);
                _patched.Add(concealment);
                _log("HOOKED " + Signature(concealment) + " [veterancy concealment last]");
            }
            else
            {
                _log("HOOK NOT FOUND Il2CppMenace.Tactical.EntityProperties.GetConcealment");
            }

            MethodInfo? propertyValue = global::HarmonyLib.AccessTools.GetDeclaredMethods(typeof(EntityProperties))
                .FirstOrDefault(m => string.Equals(m.Name, "GetPropertyValue", StringComparison.Ordinal)
                    && m.GetParameters().Length == 1
                    && m.ReturnType == typeof(float));
            if (propertyValue != null)
            {
                Type propertyType = propertyValue.GetParameters()[0].ParameterType;
                try
                {
                    string? suppressionName = Enum.GetNames(propertyType)
                        .FirstOrDefault(n => string.Equals(n, "SuppressionImpactMult", StringComparison.OrdinalIgnoreCase));
                    if (suppressionName != null)
                        _suppressionImpactPropertyValue = Convert.ToInt32(Enum.Parse(propertyType, suppressionName));
                }
                catch { _suppressionImpactPropertyValue = int.MinValue; }

                var postfix = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(
                    nameof(EntityPropertiesGetPropertyValuePostfix), BindingFlags.Static | BindingFlags.NonPublic)!);
                // Original Squad Size Modifiers 1.3.0 patches this exact method for
                // suppression resistance. Priority.Last composes our rank modifier
                // on top of its already-adjusted impact multiplier instead of
                // replacing it or depending on SSM at compile/runtime.
                postfix.priority = global::HarmonyLib.Priority.Last;
                _harmony.Patch(propertyValue, postfix: postfix);
                _patched.Add(propertyValue);
                _log("HOOKED " + Signature(propertyValue) + $" [veterancy suppression last property={_suppressionImpactPropertyValue}]");
            }
            else
            {
                _log("HOOK NOT FOUND Il2CppMenace.Tactical.EntityProperties.GetPropertyValue(enum)->float");
            }
        }
        catch (Exception e)
        {
            _log($"SQUAD PROPERTY HOOKS ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void VeterancyPropertiesOwnerPostfix(object __instance, EntityProperties __result)
    {
        var c = Current;
        if (c == null || !c._enabled || __result == null || !Valid(__result)) return;
        try
        {
            long ownerPtr = Key(__instance);
            if (ownerPtr == 0) return;

            bool actorObject = false;
            try
            {
                if (__instance is Actor) actorObject = true;
                else if (__instance is Il2CppObjectBase io)
                {
                    var entity = new Entity(io.Pointer);
                    actorObject = entity.TryCast<Actor>() != null;
                }
            }
            catch { }
            if (!actorObject) return;

            int sid = c.ResolvePlayerSquaddieIdForOwner(ownerPtr);
            string pilotTemplateId = "";
            bool isNpc = false;
            bool npcVehicle = false;
            bool npcEnemy = false;
            VeterancyRank npcRank = VeterancyRank.Green;

            if (sid <= 0 || !c._identities.Contains(sid))
            {
                sid = 0;
                if ((c._vehicleCrewVeterancy || c._vehicleCrewPressure)
                    && c.TryResolvePlayerPilotForOwner(ownerPtr, out var pilot))
                {
                    pilotTemplateId = pilot.TemplateId;
                }
                else
                {
                    // Vehicle suppression is independent from the infantry no-fire toggle.
                    // A human-crewed hostile vehicle must still get its crew rank/bar even
                    // when Enemy Reliability is disabled.
                    int factionId = c.GetOwnerFactionId(ownerPtr);
                    string templateId = c.ResolveOwnerTemplateId(ownerPtr);
                    if (c._vehicleCrewPressure
                        && NpcVeterancyProfiles.IsHostileVehicleTemplate(factionId, templateId))
                    {
                        isNpc = true;
                        npcVehicle = true;
                        npcEnemy = true;
                        npcRank = c.GetOrAssignNpcVehicleRank(ownerPtr, factionId, templateId, out _);
                    }
                    else if (c.TryGetTemporaryActorRank(ownerPtr, out var resolvedRank, out bool resolvedVehicle, out bool resolvedEnemy))
                    {
                        isNpc = true;
                        npcRank = resolvedRank;
                        npcVehicle = resolvedVehicle;
                        npcEnemy = resolvedEnemy;
                    }
                }
            }
            if (sid <= 0 && string.IsNullOrWhiteSpace(pilotTemplateId) && !isNpc) return;

            long propsPtr = Key(__result);
            if (propsPtr == 0) return;
            var state = new VeterancyPropertiesOwnerState
            {
                SquaddieId = sid,
                PilotTemplateId = pilotTemplateId,
                IsTemporaryNpc = isNpc,
                NpcVehicle = npcVehicle,
                NpcEnemy = npcEnemy,
                NpcRank = npcRank,
                OwnerPtr = ownerPtr,
                LastSeenAtMs = Environment.TickCount64
            };
            c._veterancyPropertiesOwners[propsPtr] = state;
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"VETERANCY PROPERTY MAP ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void EntityPropertiesGetConcealmentPostfix(EntityProperties __instance, ref int __result)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._teamConcealmentMods || __instance == null) return;
        try
        {
            if (!c.TryGetVeterancyPropertiesOwner(__instance, out var owner) || owner.SquaddieId <= 0) return;
            int delta = c.GetTeamConcealment(owner.SquaddieId);
            int before = __result;
            int after = Math.Max(-99, Math.Min(99, before + delta));
            __result = after;
            c.LogSquadPropertyModifierOnce("concealment", owner, delta, before, after, isPercent: false);
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"CONCEALMENT VETERANCY ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void EntityPropertiesGetPropertyValuePostfix(EntityProperties __instance, object __0, ref float __result)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._teamSuppressionResistanceMods || __instance == null) return;
        try
        {
            if (c._suppressionImpactPropertyValue == int.MinValue) return;
            int propertyValue;
            try { propertyValue = Convert.ToInt32(__0); }
            catch { return; }
            if (propertyValue != c._suppressionImpactPropertyValue) return;
            if (!c.TryGetVeterancyPropertiesOwner(__instance, out var owner)) return;

            bool vehicleOwner = owner.SquaddieId <= 0
                && (!string.IsNullOrWhiteSpace(owner.PilotTemplateId) || (owner.IsTemporaryNpc && owner.NpcVehicle));
            if (vehicleOwner) return; // vehicle pressure is custom in 1.9; leave native suppression untouched

            int resistPct = 0;
            float before = __result;
            float baseline = before;

            if (owner.IsTemporaryNpc)
            {
                if (!c._teamSuppressionResistanceMods) return;
                resistPct = c.SuppressionResistancePctForRank(owner.NpcRank);
            }
            else
            {
                if (!c._teamSuppressionResistanceMods || owner.SquaddieId <= 0) return;
                resistPct = c.GetTeamSuppressionResistancePct(owner.SquaddieId);
            }

            float after = Math.Max(0f, Math.Min(10f, baseline * SuppressionResistanceMultiplier(resistPct)));
            __result = after;
            if (owner.IsTemporaryNpc)
                c.LogTemporaryNpcModifierOnce("suppression-resistance", owner.OwnerPtr, owner.NpcRank, owner.NpcVehicle, resistPct, before, after);
            else
                c.LogSquadPropertyModifierOnce("suppression-resistance", owner, resistPct, before, after, isPercent: true);
            if (c._verbose && (resistPct != 0 || Math.Abs(baseline - before) > 0.0001f))
                c._log(owner.IsTemporaryNpc
                    ? $"NPC SUPPRESSION RESIST APPLY owner=0x{owner.OwnerPtr:X} vehicle={(owner.NpcVehicle ? 1 : 0)} rank={VeterancyRules.DisplayName(owner.NpcRank)} pct={resistPct:+#;-#;0}% impact={before:0.###}->{after:0.###}"
                    : $"SUPPRESSION RESIST VETERANCY APPLY sid={owner.SquaddieId} pct={resistPct:+#;-#;0}% impact={before:0.###}->{after:0.###}");
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"SUPPRESSION RESIST VETERANCY ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static float SuppressionResistanceMultiplier(int resistPct)
        => Math.Max(0f, Math.Min(2f, 1f - (resistPct / 100f)));

    private void PatchPrefix(Type type, string methodName, string patchName, Type[]? args = null)
    {
        try
        {
            MethodInfo? target = args == null
                ? global::HarmonyLib.AccessTools.DeclaredMethod(type, methodName)
                : global::HarmonyLib.AccessTools.DeclaredMethod(type, methodName, args);
            if (target == null) { _log($"HOOK NOT FOUND {type.FullName}.{methodName}"); return; }
            var patch = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(patchName, BindingFlags.Static | BindingFlags.NonPublic)!);
            _harmony.Patch(target, prefix: patch);
            _patched.Add(target);
            _log("HOOKED " + Signature(target));
        }
        catch (Exception e) { _log($"HOOK FAILED {type.FullName}.{methodName}: {e.GetType().Name}: {e.Message}"); }
    }

    private void PatchPostfix(Type type, string methodName, string patchName, Type[]? args = null)
    {
        try
        {
            MethodInfo? target = args == null
                ? global::HarmonyLib.AccessTools.DeclaredMethod(type, methodName)
                : global::HarmonyLib.AccessTools.DeclaredMethod(type, methodName, args);
            if (target == null) { _log($"HOOK NOT FOUND {type.FullName}.{methodName}"); return; }
            if (methodName == "GetExecutingElements")
                _log("HOOK TARGET " + Signature(target));
            var patch = new global::HarmonyLib.HarmonyMethod(typeof(ProbeRuntime).GetMethod(patchName, BindingFlags.Static | BindingFlags.NonPublic)!);
            _harmony.Patch(target, postfix: patch);
            _patched.Add(target);
            _log("HOOKED " + Signature(target));
        }
        catch (Exception e) { _log($"HOOK FAILED {type.FullName}.{methodName}: {e.GetType().Name}: {e.Message}"); }
    }

    private static void SaveSystemExecLoadPrefix(SaveState __0)
    {
        var c = Current;
        if (c == null) return;
        try
        {
            if (__0 == null)
            {
                c._log("XP LOAD REQUEST unresolved: ExecLoad SaveState was null");
                return;
            }

            string? path = c._xp.ResolveGameSavePath(__0);
            if (string.IsNullOrWhiteSpace(path))
            {
                c._log($"XP LOAD REQUEST unresolved: could not fingerprint selected save from SaveState ptr=0x{Key(__0):X}");
                return;
            }

            c._log($"XP LOAD RESOLVED save={SafeFileName(path)} ptr=0x{Key(__0):X}");
            c._loadRosterSyncPending = true;
            c._loadRosterSyncBaselineGeneration = c._identities.RefreshGeneration;
            c._loadRosterSyncBaselineStatePtr = c._identities.CurrentStatePointer;
            c._loadRosterSyncStartedAtMs = Environment.TickCount64;
            c._nextLoadRosterWaitLogAtMs = 0;
            c._log($"XP LOAD ROSTER GUARD armed save={SafeFileName(path)} baselineGeneration={c._loadRosterSyncBaselineGeneration} baselineState=0x{c._loadRosterSyncBaselineStatePtr:X}");
            c._xp.LoadForGameSave(path);
            c._liveElements.Clear();
            c._xpDedup.Clear();
            c._incomingTargetHints.Clear();
            c._malfunctionDedup.Clear();
            c._recentMalfunctionAtMs = 0;
            c._recentMalfunctionElementPtr = 0;
            c._recentMalfunctionSkillPtr = 0;
            c._recentMalfunctionSid = 0;
            c._npcRanks.Clear();
            c._ownerTemplateIds.Clear();
            c._ownerTemplateDiagnosticsLogged.Clear();
            c._enemyOwnersLogged.Clear();
            c._alliedNpcOwnersLogged.Clear();
            c._unclassifiedOwnersLogged.Clear();
            c._combatModifierLogs.Clear();
            c._vehiclePressureStates.Clear();
            c._vehiclePressureActiveActorPtr = 0;
            c._vehiclePressureActivationSerial = 0;
            c._veterancyPropertiesOwners.Clear();
            c._deployedSquadRepresentatives.Clear();
            c._deployedPilots.Clear();
            c._vehicleElementKillsAwarded.Clear();
            c._pilotRoster.Clear();
            c._nextPilotRosterRefreshMs = 0;
            c._lastPilotStatePtr = 0;
            c._lastLoggedPilotCount = -1;
            c._missionVehicleXpAwarded = false;
            c._enemyElementKillsAwarded.Clear();
            c._objectiveKinds.Clear();
            c._completedObjectives.Clear();
            c._objectiveKindDiagnostics.Clear();
            c._missionObjectiveXpAwarded = false;
            c.ResetRescueTracking("save-load");
            c.ResetReliabilityContext();
        }
        catch (Exception e)
        {
            c._log($"XP LOAD HOOK ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillUsePrefix(Skill __instance, Tile __0, UsageParameter __1)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._individualReliability || !Valid(__instance)) return;
        try
        {
            if (!c.IsRealAttack(__instance)) return;

            // alpha.10.6 proved that arming here is too late for normal squad fire:
            // MENACE has already queried GetExecutingElements by the time Skill.Use
            // begins. Only consume a context that was created by that real preflight.
            // If none exists, do nothing rather than inventing rolls that cannot
            // possibly affect this attack (important for leader/special child skills).
            if (!c.TryGetReliabilityContext(__instance, includeConsumed: false, out var ctx))
            {
                if (c._verbose)
                    c._log($"RELIABILITY USE NO_PREFLIGHT skill={SkillId(__instance)}");
                return;
            }

            ctx.UseDepth++;
            ctx.LastTouchedAtMs = Environment.TickCount64;
            if (ctx.UseDepth == 1 && (c._verbose || ctx.Decisions.Values.Any(x => x.Skip && x.Candidate)))
                c._log($"RELIABILITY USE BEGIN serial={ctx.Serial} skill={SkillId(__instance)} candidates={ctx.Decisions.Values.Count(x => x.Candidate)} noFire={ctx.Decisions.Values.Count(x => x.Skip && x.Candidate)}");
        }
        catch (Exception e)
        {
            c._log($"RELIABILITY USE BEGIN ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillUsePostfix(Skill __instance, Tile __0, UsageParameter __1, bool __result)
    {
        var c = Current;
        if (c == null || !Valid(__instance)) return;
        try
        {
            if (!c.TryGetReliabilityContext(__instance, includeConsumed: true, out var ctx)) return;
            if (ctx.UseDepth > 0) ctx.UseDepth--;
            ctx.LastTouchedAtMs = Environment.TickCount64;

            // Coordinated Fire can re-enter Skill.Use for the same native skill.
            // Only consume the pending roll set when the outermost call returns.
            if (ctx.UseDepth > 0) return;

            if (!__result)
            {
                ctx.Consumed = true;
                c._log($"RELIABILITY CANCEL serial={ctx.Serial} skill={SkillId(__instance)} use rejected");
                return;
            }

            ctx.Consumed = true;
            if (c._verbose)
                c._log($"RELIABILITY USE END serial={ctx.Serial} skill={SkillId(__instance)} committed={ctx.Committed} candidates={ctx.Decisions.Values.Count(x => x.Candidate)} noFire={ctx.Decisions.Values.Count(x => x.Skip && x.Candidate)}");
        }
        catch (Exception e)
        {
            c._log($"RELIABILITY USE POST ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillContainerOnSkillUsedCommitPrefix(SkillContainer __instance, Skill _skill, Tile _targetTile)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._individualReliability || !Valid(_skill)) return;
        try
        {
            // Learn the stable skill -> owner mapping from the authoritative
            // SkillContainer callback. This is a safe fallback for wrappers where
            // Skill.GetEntity/GetActor/Container are stripped or unavailable.
            long skillPtr = Key(_skill);
            long ownerPtr = c.ResolveSkillContainerOwnerPtr(__instance);
            if (skillPtr != 0 && ownerPtr != 0)
                c._skillOwnerBySkillPtr[skillPtr] = ownerPtr;

            if (!c.TryGetReliabilityContext(_skill, includeConsumed: false, out var ctx))
            {
                // Forced/native direct uses (notably Overwatch reactions) can enter
                // Skill.Use without MENACE first asking for GetExecutingElements.
                // Create an EMPTY committed context here. Do not roll or mark any
                // squaddie as a candidate yet: if this is a leader/special weapon,
                // no normal execution list will ever populate the context. If MENACE
                // subsequently asks for AllButSpecial during this same use, the
                // GetExecutingElements postfix will seed, roll, filter and apply reliability
                // against this already-committed context.
                if (!c.IsRealAttack(_skill) || ownerPtr == 0) return;
                if (!c.TryGetReliabilityOwnerElements(ownerPtr, out var eligible) || eligible.Count == 0) return;

                ctx = c.GetOrCreateReliabilityContext(
                    _skill, ownerPtr, Array.Empty<ReliabilityElement>(), "commit-fallback");
                c._log($"RELIABILITY COMMIT FALLBACK serial={ctx.Serial} skill={SkillId(_skill)} eligible={eligible.Count}");
            }

            ctx.Committed = true;
            ctx.LastTouchedAtMs = Environment.TickCount64;
            int candidates = ctx.Decisions.Values.Count(x => x.Candidate);
            int noFire = ctx.Decisions.Values.Count(x => x.Skip && x.Candidate);
            c._log($"RELIABILITY COMMIT serial={ctx.Serial} skill={SkillId(_skill)} candidates={candidates} noFire={noFire}");
            c.ApplyCommittedReliabilityConsequences(ctx, _skill);
        }
        catch (Exception e)
        {
            c._log($"RELIABILITY COMMIT ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    // MENACE 0.7.14 returns a List<int> here, not List<Element>. Crucially, the
    // normal squad execution list is commonly queried BEFORE Skill.Use. alpha.10.8
    // therefore creates a stable pending roll set here and keeps it until the next
    // real use of this exact Skill instance. Repeated UI/preflight queries reuse the
    // same rolls, so hovering or rechecking a target cannot reroll reliability.
    private static void SkillGetExecutingElementsPostfix(
        Skill __instance,
        ExecutingElementType _type,
        ref Il2CppSystem.Collections.Generic.List<int> __result)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._individualReliability || !Valid(__instance) || __result == null) return;
        try
        {
            if (!c.IsRealAttack(__instance)) return;
            if (_type == ExecutingElementType.SpecialOnly) return;

            long ownerPtr = c.ResolveSkillOwnerEntityPtr(__instance);
            if (ownerPtr == 0) return;
            if (!c.TryGetReliabilityOwnerElements(ownerPtr, out var ownerElements) || ownerElements.Count == 0) return;
            if (!c.TryResolveExecutingIndices(__instance, ownerPtr, ownerElements, __result, out var items, out var rawValues)) return;
            if (items.Count == 0) return; // e.g. SpecialOnly [0]: leader/special weapon path

            var ctx = c.GetOrCreateReliabilityContext(__instance, ownerPtr, items, "executing-list");
            c.EnsureReliabilityDecisions(ctx, __instance, items);

            if (c._verbose)
            {
                string listKey = $"raw:{_type}:{string.Join(",", rawValues)}";
                if (ctx.ListLogs.Add(listKey))
                    c._log($"RELIABILITY INDEX LIST serial={ctx.Serial} skill={SkillId(__instance)} owner=0x{ownerPtr:X} raw=[{string.Join(",", rawValues)}] mapped={items.Count}/{rawValues.Count}");
            }

            var removeValues = new HashSet<int>();
            foreach (var item in items)
            {
                if (item.DecisionKey == 0) continue;
                if (!ctx.Decisions.TryGetValue(item.DecisionKey, out var decision)) continue;
                decision.Candidate = true;
                decision.Element = item.Element;
                decision.ElementIndex = item.ElementIndex;
                decision.ElementPtr = item.ElementPtr;
                if (decision.Skip)
                    removeValues.Add(item.ElementIndex);
            }

            int nativeCount = __result.Count;

            // Never hand MENACE an empty execution list. A squad can be unreliable,
            // but at least one otherwise-valid shooter is always kept.
            if (removeValues.Count > 0 && nativeCount - removeValues.Count <= 0)
            {
                var kept = items.FirstOrDefault(x => removeValues.Contains(x.ElementIndex));
                if (kept != null && kept.DecisionKey != 0)
                {
                    removeValues.Remove(kept.ElementIndex);
                    if (ctx.Decisions.TryGetValue(kept.DecisionKey, out var d)) d.Skip = false;
                    if (c._reliabilityNoFires > 0) c._reliabilityNoFires--;
                    if (kept.IsEnemy && c._enemyReliabilityNoFires > 0) c._enemyReliabilityNoFires--;
                    if (kept.IsAlliedNpc && c._alliedNpcReliabilityNoFires > 0) c._alliedNpcReliabilityNoFires--;
                    string who = IsTemporaryNpc(kept)
                        ? $"npc={(kept.IsEnemy ? "hostile" : "allied")} faction={FactionLabel(kept.FactionId)}"
                        : $"sid={kept.Sid}";
                    c._log($"RELIABILITY SAFETY serial={ctx.Serial} {who} idx={kept.ElementIndex} kept as last available shooter");
                }
            }

            if (removeValues.Count <= 0)
            {
                // A late-committed context represents an attack that already returned
                // from Skill.Use. Nothing else should reuse it as a fresh preflight.
                if (ctx.LateCommitted)
                {
                    ctx.Consumed = true;
                    ctx.LastTouchedAtMs = Environment.TickCount64;
                }
                return;
            }

            var filtered = new Il2CppSystem.Collections.Generic.List<int>();
            for (int i = 0; i < nativeCount; i++)
            {
                int elementIndex = __result[i];
                if (removeValues.Contains(elementIndex)) continue;
                filtered.Add(elementIndex);
            }
            __result = filtered;

            c._reliabilityFilteredLists++;
            string key = $"filter:{_type}:{nativeCount}->{filtered.Count}:{string.Join(",", removeValues.OrderBy(x => x))}";
            if (ctx.ListLogs.Add(key))
                c._log($"RELIABILITY FILTER serial={ctx.Serial} skill={SkillId(__instance)} type={_type} before={nativeCount} removed={removeValues.Count} after={filtered.Count}");

            // Usually filtering happens before OnSkillUsed. If MENACE asks for the
            // list later as well, preserve one-XP-per-participant semantics.
            if (ctx.Committed)
                c.ApplyCommittedReliabilityConsequences(ctx, __instance);

            if (ctx.LateCommitted)
            {
                // The real Skill.Use already ended before this execution-list query.
                // Mark the context consumed now; the amount hook has an explicit
                // late-commit fallback so MENACE can still receive the filtered count.
                ctx.Consumed = true;
                ctx.LastTouchedAtMs = Environment.TickCount64;
            }
        }
        catch (Exception e)
        {
            c._log($"RELIABILITY FILTER ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillGetExecutingElementsAmountPostfix(Skill __instance, ExecutingElementType _type, ref int __result)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._individualReliability || !Valid(__instance) || __result <= 0) return;
        try
        {
            if (_type == ExecutingElementType.SpecialOnly) return;
            if (!c.TryGetReliabilityContext(__instance, includeConsumed: false, out var ctx))
            {
                // Late AI execution lists are deliberately marked consumed immediately
                // after filtering because Skill.Use already finished. Permit only that
                // special case to adjust MENACE's subsequent executing-element count.
                if (!c.TryGetReliabilityContext(__instance, includeConsumed: true, out ctx) || !ctx.LateCommitted)
                    return;
            }

            int skip = ctx.Decisions.Values.Count(x => x.Skip && x.Candidate);
            if (skip <= 0) return;

            int before = __result;
            int target = Math.Max(1, before - skip);
            if (target >= before) return;
            __result = target;
            c._reliabilityAdjustedCounts++;
            string key = $"{_type}:{before}->{target}";
            if (ctx.CountLogs.Add(key))
                c._log($"RELIABILITY COUNT serial={ctx.Serial} skill={SkillId(__instance)} type={_type} native={before} skipped={skip} result={target}");
        }
        catch (Exception e)
        {
            c._log($"RELIABILITY COUNT ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillOnAnySkillAppliedByElementPrefix(Skill __instance, Skill _skill, Element _element, SkillApplyEvent _event)
    {
        var c = Current;
        if (c == null || !c._enabled) return;
        try
        {
            // MENACE broadcasts this event to every skill on the actor. Process only
            // the applied skill's own instance; this collapses the broadcast to one
            // callback per participating tactical element.
            if (!Valid(__instance) || !Valid(_skill) || !Valid(_element)) return;
            if (Key(__instance) != Key(_skill)) return;
            if (!c.IsRealAttack(_skill)) return;

            int sid = 0;
            int idx = -1;
            try { sid = _element.GetSquaddieId(); } catch { }
            try { idx = _element.GetElementIndex(); } catch { }

            // Check battle-local NPC identity first so hostile/allied temporary elements
            // can never be mistaken for persistent player squaddies by raw SquaddieId.
            if (c.TryGetReliabilityDecisionByElement(_skill, _element, out var elementContext, out var elementDecision)
                && IsTemporaryNpc(elementDecision))
            {
                if (elementDecision.Skip && elementDecision.Candidate)
                {
                    string relation = elementDecision.IsEnemy ? "hostile" : "allied";
                    c._log($"RELIABILITY LEAK serial={elementContext.Serial} npc={relation} faction={FactionLabel(elementDecision.FactionId)} elem=0x{elementDecision.ElementPtr:X} idx={idx} skill={SkillId(_skill)} reached element callback after being filtered");
                }
                return;
            }

            if (sid > 0 && c._identities.TryGet(sid, out var identity) && identity.Alive)
            {
                if (c.TryGetReliabilityNoFireDecision(_skill, sid, out var reliabilityContext, out _))
                {
                    // A candidate that was actually removed from GetExecutingElements
                    // must not reach the element callback. If it does, the filter did
                    // not control the real execution path.
                    c._log($"RELIABILITY LEAK serial={reliabilityContext.Serial} sid={sid} idx={idx} skill={SkillId(_skill)} reached element callback after being filtered");
                }

                // XP is awarded once per committed squad attack through the native
                // TacticalEventHooks.OnSkillUsed event, not once per firing element.
                // Keep this callback only as a reliability leak validator.
                return;
            }
        }
        catch (Exception e)
        {
            c._log($"XP EVENT ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void EntityCreateElementPrefix(Entity __instance, int __1, ref int __2, out ElementCreateHpState __state)
    {
        __state = new ElementCreateHpState();
        var c = Current;
        if (c == null || !Valid(__instance)) return;

        try
        {
            int sid = __1;
            int baseHp = __2;
            if (sid <= 0 || baseHp <= 0 || baseHp > 1000) return;

            bool isPlayerUnit = false;
            try { isPlayerUnit = __instance.TryCast<UnitActor>() != null; } catch { }
            if (!isPlayerUnit || !c._identities.Contains(sid)) return;

            int bonus = c.GetTeamHpBonus(sid);
            __state.PlayerSquaddie = true;
            __state.SquaddieId = sid;
            __state.BaseHp = baseHp;
            __state.AppliedBonus = bonus;
            __state.OwnerPtr = Key(__instance);

            if (bonus != 0)
            {
                int desired = Math.Max(1, Math.Min(1000, baseHp + bonus));
                __2 = desired;
                if (c.TryGetReliableSquadAverageXp(sid, out double avg, out int squadNumber, out _))
                    c._log($"HP SPAWN APPLY sid={sid} squad={squadNumber} avgXp={avg:0.00} rank={VeterancyRules.DisplayName(VeterancyRules.RankFromXp(avg))} base={baseHp} bonus=+{bonus} createHp={desired}");
            }
            else if (c._verbose)
            {
                c._log($"HP SPAWN BASE sid={sid} base={baseHp} bonus=0");
            }
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"HP SPAWN PREFIX ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void EntityCreateElementPostfix(Entity __instance, Element __result, ElementCreateHpState __state)
    {
        var c = Current;
        if (c == null || __state == null || !__state.PlayerSquaddie || !Valid(__result)) return;

        try
        {
            long ptr = Key(__result);
            if (!c._liveElements.TryGetValue(ptr, out var state))
            {
                state = new LiveElementState();
                c._liveElements[ptr] = state;
            }

            state.Element = __result;
            state.SquaddieId = __state.SquaddieId;
            state.ParentEntityPtr = __state.OwnerPtr != 0 ? __state.OwnerPtr : Key(__instance);
            state.BaseMaxHp = __state.BaseHp;
            state.AppliedBonus = __state.AppliedBonus;
            state.InitialHpSettled = false;
            state.PlayerSquaddie = true;
            try { state.ElementIndex = __result.GetElementIndex(); } catch { }

            int actualMax = 0;
            int actualHp = 0;
            try { actualMax = __result.GetHitpointsMax(); } catch { }
            try { actualHp = __result.GetHitpoints(); } catch { }
            int expected = Math.Max(1, __state.BaseHp + __state.AppliedBonus);

            // MENACE's CreateElement _hp argument initializes CURRENT HP, but max HP
            // still comes from EntityProperties. alpha.10.13.1 proved this with e.g.
            // actual=9/8. We now finish the spawn atomically here, where both the
            // real Element and its SquaddieId are available.
            if (__state.AppliedBonus != 0 && actualMax == __state.BaseHp && expected != actualMax)
            {
                ProbeRuntime._hpMutationGuard = true;
                try
                {
                    __result.SetHitpointsMax(expected);
                    // Usually CreateElement already consumed the modified _hp and
                    // current HP is == expected. If this game path did not, top up
                    // only a full-health vanilla spawn; never heal an injured unit.
                    if (actualHp == __state.BaseHp) __result.SetHitpoints(expected);
                    else if (actualHp > expected) __result.SetHitpoints(expected);
                }
                finally { ProbeRuntime._hpMutationGuard = false; }

                try { actualMax = __result.GetHitpointsMax(); } catch { }
                try { actualHp = __result.GetHitpoints(); } catch { }
                c._log($"HP SPAWN MAX FIX sid={state.SquaddieId} expected={expected} actual={actualHp}/{actualMax}");
            }

            state.InitialHpSettled = actualMax == expected;
            c.TrackDeployedSquad(state.SquaddieId);

            if (__state.AppliedBonus != 0 || c._verbose)
                c._log($"HP SPAWN RESULT sid={state.SquaddieId} elem=0x{ptr:X} idx={state.ElementIndex} base={state.BaseMaxHp} bonus=+{state.AppliedBonus} expectedMax={expected} actual={actualHp}/{actualMax} settled={(state.InitialHpSettled ? 1 : 0)}");
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"HP SPAWN POSTFIX ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void ElementSetSquaddieIdPostfix(Element __instance, int _id)
    {
        var c = Current;
        if (c == null || !Valid(__instance) || _id <= 0) return;
        try
        {
            long ptr = Key(__instance);
            if (!c._liveElements.TryGetValue(ptr, out var state))
            {
                state = new LiveElementState();
                c._liveElements[ptr] = state;
            }
            state.Element = __instance;
            state.SquaddieId = _id;
            try { state.ElementIndex = __instance.GetElementIndex(); } catch { }
            state.ParentEntityPtr = c.ResolveElementParentEntityPtr(__instance);
            if (c._verbose)
                c._log($"ELEMENT MAP ptr=0x{ptr:X} sid={_id} identity={c._identities.Label(_id)} playerSpawn={(state.PlayerSquaddie ? 1 : 0)}");
        }
        catch (Exception e) { c._log($"ELEMENT MAP ERROR {e.GetType().Name}: {e.Message}"); }
    }

    private static void SkillGetHitchancePrefix(
        Skill __instance,
        Tile __1,
        EntityProperties __2,
        EntityProperties __3,
        Entity __5,
        out HitchanceMutationState? __state)
    {
        __state = null;
        var c = Current;
        if (c == null || !c._enabled || !Valid(__instance)) return;

        try
        {
            long attackerPtr = c.ResolveSkillOwnerEntityPtr(__instance);
            int attackerSid = c.ResolvePlayerSquaddieIdForOwner(attackerPtr);
            bool attackerNpc = false;
            bool attackerNpcVehicle = false;
            VeterancyRank attackerNpcRank = VeterancyRank.Green;
            int accuracyPct = attackerSid > 0 ? c.GetTeamAccuracyPct(attackerSid) : 0;
            if (attackerSid <= 0 && c.TryGetTemporaryActorRank(attackerPtr, out var resolvedAttackerRank, out bool resolvedAttackerVehicle, out _))
            {
                attackerNpc = true;
                attackerNpcVehicle = resolvedAttackerVehicle;
                attackerNpcRank = resolvedAttackerRank;
                accuracyPct = c._teamAccuracyMods ? c.AccuracyPctForRank(attackerNpcRank) : 0;
            }

            int buttonedAccuracyPenalty = c.GetButtonedAccuracyPenalty(attackerPtr);
            if (buttonedAccuracyPenalty > 0) accuracyPct -= buttonedAccuracyPenalty;

            long targetPtr = Key(__5);
            int defenderSid = c.ResolvePlayerSquaddieIdForOwner(targetPtr);
            PilotIdentitySnapshot? defenderPilot = null;
            if (defenderSid <= 0 && targetPtr != 0 && c._vehicleCrewVeterancy
                && c.TryResolvePlayerPilotForOwner(targetPtr, out var resolvedPilot))
                defenderPilot = resolvedPilot;

            bool defenderNpc = false;
            bool defenderNpcVehicle = false;
            VeterancyRank defenderNpcRank = VeterancyRank.Green;
            int defensePct = defenderSid > 0 ? c.GetTeamDefensePct(defenderSid) : 0;
            if (defenderSid <= 0 && defenderPilot == null && targetPtr != 0
                && c.TryGetTemporaryActorRank(targetPtr, out var resolvedDefenderRank, out bool resolvedDefenderVehicle, out _))
            {
                defenderNpc = true;
                defenderNpcVehicle = resolvedDefenderVehicle;
                defenderNpcRank = resolvedDefenderRank;
                defensePct = c._teamDefenseMods ? c.DefensePctForRank(defenderNpcRank) : 0;
            }

            // MENACE can target an Entity through _overrideTargetEntity while the
            // target Tile itself has no Entity attached. Remember the exact hostile
            // attacker + target-tile pair here, but do NOT award XP during hitchance
            // evaluation: AI can query several candidates before committing. The hint
            // is consumed only when TacticalEventHooks.OnSkillUsed reports the real
            // attack action.
            bool pressureVehicleTarget = false;
            if (c._vehicleCrewPressure && targetPtr != 0)
            {
                try { pressureVehicleTarget = c.TryResolveVehiclePressureTarget(targetPtr, out _, out _); }
                catch { }
            }

            if (((attackerSid <= 0 && (defenderSid > 0 || defenderPilot != null)) || pressureVehicleTarget) && targetPtr != 0)
            {
                int attackerFaction = c.GetOwnerFactionId(attackerPtr);
                long targetTilePtr = Key(__1);
                bool playerAttacker = attackerSid > 0 || c.TryResolvePlayerPilotForOwner(attackerPtr, out _);
                if ((IsHostileFaction(attackerFaction) || playerAttacker) && targetTilePtr != 0)
                {
                    c.RememberIncomingTargetHint(
                        attackerPtr, targetTilePtr, Key(__instance), targetPtr, defenderSid, defenderPilot?.TemplateId ?? "", pressureVehicleTarget);
                }
            }

            if (accuracyPct == 0 && defensePct == 0)
            {
                if (attackerSid > 0) c.LogCombatModifierOnce("accuracy", attackerPtr, attackerSid, accuracyPct);
                else if (attackerNpc) c.LogTemporaryNpcModifierOnce("accuracy", attackerPtr, attackerNpcRank, attackerNpcVehicle, accuracyPct);
                if (defenderSid > 0) c.LogCombatModifierOnce("defense", targetPtr, defenderSid, defensePct);
                else if (defenderNpc) c.LogTemporaryNpcModifierOnce("defense", targetPtr, defenderNpcRank, defenderNpcVehicle, defensePct);
                return;
            }

            var state = new HitchanceMutationState
            {
                AttackerSid = attackerSid,
                DefenderSid = defenderSid,
                AccuracyPct = accuracyPct,
                DefensePct = defensePct
            };

            if (accuracyPct != 0 && __2 != null && TryAdjustFloatMember(__2, "AccuracyMult", accuracyPct / 100f, out var accMember, out float accOld, out float accNew))
            {
                state.AccuracyTarget = __2;
                state.AccuracyMember = accMember;
                state.AccuracyOriginal = accOld;
            }
            else if (accuracyPct != 0 && c._verbose)
            {
                c._log(attackerNpc
                    ? $"HITCHANCE NPC ACCURACY SKIP owner=0x{attackerPtr:X} rank={VeterancyRules.DisplayName(attackerNpcRank)} pct={accuracyPct:+#;-#;0}% props={( __2 == null ? "null" : __2.GetType().FullName)}"
                    : $"HITCHANCE ACCURACY SKIP sid={attackerSid} pct={accuracyPct:+#;-#;0}% props={( __2 == null ? "null" : __2.GetType().FullName)}");
            }

            if (defensePct != 0 && __3 != null && TryAdjustFloatMember(__3, "DefenseMult", defensePct / 100f, out var defMember, out float defOld, out float defNew))
            {
                state.DefenseTarget = __3;
                state.DefenseMember = defMember;
                state.DefenseOriginal = defOld;
            }
            else if (defensePct != 0 && c._verbose)
            {
                c._log(defenderNpc
                    ? $"HITCHANCE NPC DEFENSE SKIP owner=0x{targetPtr:X} rank={VeterancyRules.DisplayName(defenderNpcRank)} pct={defensePct:+#;-#;0}% props={( __3 == null ? "null" : __3.GetType().FullName)}"
                    : $"HITCHANCE DEFENSE SKIP sid={defenderSid} pct={defensePct:+#;-#;0}% props={( __3 == null ? "null" : __3.GetType().FullName)}");
            }

            if (state.AccuracyMember != null || state.DefenseMember != null)
            {
                __state = state;
                if (attackerSid > 0) c.LogCombatModifierOnce("accuracy", attackerPtr, attackerSid, accuracyPct);
                else if (attackerNpc) c.LogTemporaryNpcModifierOnce("accuracy", attackerPtr, attackerNpcRank, attackerNpcVehicle, accuracyPct);
                if (defenderSid > 0) c.LogCombatModifierOnce("defense", targetPtr, defenderSid, defensePct);
                else if (defenderNpc) c.LogTemporaryNpcModifierOnce("defense", targetPtr, defenderNpcRank, defenderNpcVehicle, defensePct);
            }
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"HITCHANCE VETERANCY PREFIX ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillGetHitchancePostfix(HitchanceMutationState? __state)
    {
        if (__state == null) return;
        var c = Current;
        try
        {
            if (__state.AccuracyTarget != null && __state.AccuracyMember != null)
                TrySetFloatMember(__state.AccuracyTarget, __state.AccuracyMember, __state.AccuracyOriginal);
            if (__state.DefenseTarget != null && __state.DefenseMember != null)
                TrySetFloatMember(__state.DefenseTarget, __state.DefenseMember, __state.DefenseOriginal);
        }
        catch (Exception e)
        {
            if (c?._verbose == true) c._log($"HITCHANCE VETERANCY RESTORE ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static bool TryAdjustFloatMember(object target, string logicalName, float delta, out MemberInfo? member, out float original, out float updated)
    {
        member = null;
        original = 0f;
        updated = 0f;
        if (target == null) return false;
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = target.GetType();
            string[] candidates =
            {
                logicalName,
                "m_" + logicalName,
                "_" + logicalName,
                "<" + logicalName + ">k__BackingField"
            };

            foreach (string name in candidates)
            {
                PropertyInfo? prop = type.GetProperty(name, flags);
                if (prop != null && prop.PropertyType == typeof(float) && prop.CanRead && prop.CanWrite && prop.GetIndexParameters().Length == 0)
                {
                    original = Convert.ToSingle(prop.GetValue(target));
                    updated = Math.Max(0f, original + delta);
                    prop.SetValue(target, updated);
                    member = prop;
                    return true;
                }

                FieldInfo? field = type.GetField(name, flags);
                if (field != null && field.FieldType == typeof(float) && !field.IsInitOnly)
                {
                    original = Convert.ToSingle(field.GetValue(target));
                    updated = Math.Max(0f, original + delta);
                    field.SetValue(target, updated);
                    member = field;
                    return true;
                }
            }

            string normalized = logicalName.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
            foreach (PropertyInfo prop in type.GetProperties(flags))
            {
                string n = prop.Name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
                if (!n.EndsWith(normalized, StringComparison.Ordinal) || prop.PropertyType != typeof(float) || !prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length != 0) continue;
                original = Convert.ToSingle(prop.GetValue(target));
                updated = Math.Max(0f, original + delta);
                prop.SetValue(target, updated);
                member = prop;
                return true;
            }
            foreach (FieldInfo field in type.GetFields(flags))
            {
                string n = field.Name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
                if (!n.EndsWith(normalized, StringComparison.Ordinal) || field.FieldType != typeof(float) || field.IsInitOnly) continue;
                original = Convert.ToSingle(field.GetValue(target));
                updated = Math.Max(0f, original + delta);
                field.SetValue(target, updated);
                member = field;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TrySetFloatMember(object target, MemberInfo member, float value)
    {
        try
        {
            if (member is PropertyInfo prop && prop.CanWrite)
            {
                prop.SetValue(target, value);
                return true;
            }
            if (member is FieldInfo field && !field.IsInitOnly)
            {
                field.SetValue(target, value);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static void MalfunctionBeforeAnySkillUsedPrefix(ChangeMalfunctionChanceHandler __instance, Skill _skill, Tile _fromTile, Tile _targetTile, EntityProperties _properties, Entity _overrideTargetEntity)
    {
        var c = Current;
        if (c == null || !c._malfunctionDiagnostics || !c._verbose) return;
        try
        {
            c._log($"MALFUNCTION BEFORE USE skill={SkillId(_skill)} handler=0x{Key(__instance):X}");
        }
        catch (Exception e) { c._log($"MALFUNCTION BEFORE USE ERROR {e.GetType().Name}: {e.Message}"); }
    }

    private static void MalfunctionOnAnySkillAppliedByElementPrefix(ChangeMalfunctionChanceHandler __instance, Skill _skill, Element _element, SkillApplyEvent _event)
    {
        var c = Current;
        if (c == null || !c._malfunctionDiagnostics || !Valid(_element)) return;
        try
        {
            int sid = 0;
            int idx = -1;
            try { sid = _element.GetSquaddieId(); } catch { }
            try { idx = _element.GetElementIndex(); } catch { }
            if (sid <= 0 || !c._identities.TryGet(sid, out var identity)) return;

            long now = Environment.TickCount64;
            long elementPtr = Key(_element);
            long skillPtr = Key(_skill);
            string dedup = $"{Key(__instance):X}:{skillPtr:X}:{elementPtr:X}:{(int)_event}";
            if (c._malfunctionDedup.TryGetValue(dedup, out long last) && now - last < 350)
            {
                c._recentMalfunctionAtMs = now;
                c._recentMalfunctionElementPtr = elementPtr;
                c._recentMalfunctionSkillPtr = skillPtr;
                c._recentMalfunctionSid = sid;
                return;
            }
            c._malfunctionDedup[dedup] = now;
            c._recentMalfunctionAtMs = now;
            c._recentMalfunctionElementPtr = elementPtr;
            c._recentMalfunctionSkillPtr = skillPtr;
            c._recentMalfunctionSid = sid;
            c._malfunctionElementEvents++;

            int xp = c._xp.GetXp(sid);
            c._log($"MALFUNCTION CONTEXT sid={sid} idx={idx} name={Sanitize(identity.Name)} rank={VeterancyRules.DisplayName(VeterancyRules.RankFromXp(xp))} xp={xp} skill={SkillId(_skill)} event={_event} handler=0x{Key(__instance):X}");
        }
        catch (Exception e) { c._log($"MALFUNCTION CONTEXT ERROR {e.GetType().Name}: {e.Message}"); }
    }

    private static void JammingIsJammedPostfix(JammingHandler __instance, bool __result)
    {
        var c = Current;
        if (c == null || !c._malfunctionDiagnostics) return;
        try
        {
            long now = Environment.TickCount64;
            bool correlated = c._recentMalfunctionSid > 0 && now - c._recentMalfunctionAtMs <= 1500;
            if (!correlated && !__result) return;

            if (correlated) c._jamChecks++;
            int sid = correlated ? c._recentMalfunctionSid : 0;
            int xp = sid > 0 ? c._xp.GetXp(sid) : 0;
            string rank = sid > 0 ? VeterancyRules.DisplayName(VeterancyRules.RankFromXp(xp)) : "?";
            c._log($"JAM CHECK result={__result} correlated={correlated} sid={sid} rank={rank} xp={xp} recentElement=0x{c._recentMalfunctionElementPtr:X} recentSkill=0x{c._recentMalfunctionSkillPtr:X} handler=0x{Key(__instance):X}");
        }
        catch (Exception e) { c._log($"JAM CHECK ERROR {e.GetType().Name}: {e.Message}"); }
    }

    private void OnNativeElementMalfunction(IntPtr elementPtr)
    {
        if (!_malfunctionDiagnostics) return;
        try
        {
            _nativeMalfunctions++;
            long ptr = elementPtr.ToInt64();
            int sid = 0;
            if (_liveElements.TryGetValue(ptr, out var state)) sid = state.SquaddieId;
            if (sid <= 0 && _recentMalfunctionElementPtr == ptr) sid = _recentMalfunctionSid;

            if (sid > 0 && _identities.TryGet(sid, out var identity))
            {
                int xp = _xp.GetXp(sid);
                _log($"ELEMENT MALFUNCTION native ptr=0x{ptr:X} sid={sid} name={Sanitize(identity.Name)} rank={VeterancyRules.DisplayName(VeterancyRules.RankFromXp(xp))} xp={xp}");
            }
            else
            {
                _log($"ELEMENT MALFUNCTION native ptr=0x{ptr:X} sid={sid} identity=unresolved");
            }
        }
        catch (Exception e) { _log($"ELEMENT MALFUNCTION EVENT ERROR {e.GetType().Name}: {e.Message}"); }
    }

    private sealed class ReliabilityElement
    {
        public int Index;
        public Element Element = null!;
        public long DecisionKey;
        public int Sid;
        public bool IsEnemy;
        public bool IsAlliedNpc;
        public int FactionId = -1;
        public string TemplateId = "";
        public long OwnerPtr;
        public int ElementIndex;
        public long ElementPtr;
    }

    private void PruneReliabilityContexts()
    {
        if (_reliabilityContexts.Count == 0) return;
        long now = Environment.TickCount64;
        foreach (var pair in _reliabilityContexts.ToList())
        {
            var ctx = pair.Value;
            long age = now - ctx.LastTouchedAtMs;
            long maxAge = ctx.Consumed ? 5000 : 120000;
            if (age < 0 || age > maxAge)
                _reliabilityContexts.Remove(pair.Key);
        }
    }

    private void ResetReliabilityContext()
    {
        _reliabilityContexts.Clear();
    }

    private bool TryGetReliabilityContext(Skill skill, bool includeConsumed, out ReliabilityContext context)
    {
        context = null!;
        if (!_enabled || !_individualReliability || !Valid(skill)) return false;
        long skillPtr = Key(skill);
        if (skillPtr == 0 || !_reliabilityContexts.TryGetValue(skillPtr, out var ctx)) return false;

        long now = Environment.TickCount64;
        long age = now - ctx.LastTouchedAtMs;
        long maxAge = ctx.Consumed ? 5000 : 120000;
        if (age < 0 || age > maxAge)
        {
            _reliabilityContexts.Remove(skillPtr);
            return false;
        }
        if (ctx.Consumed && !includeConsumed) return false;
        context = ctx;
        return true;
    }

    private ReliabilityContext GetOrCreateReliabilityContext(
        Skill skill,
        long ownerPtr,
        IReadOnlyList<ReliabilityElement> seedItems,
        string source)
    {
        long skillPtr = Key(skill);
        long now = Environment.TickCount64;

        bool inheritLateCommit = false;
        long inheritedFromSerial = 0;
        if (skillPtr != 0 && _reliabilityContexts.TryGetValue(skillPtr, out var existing))
        {
            long age = now - existing.LastTouchedAtMs;
            if (!existing.Consumed && existing.OwnerPtr == ownerPtr && age >= 0 && age <= 120000)
            {
                existing.LastTouchedAtMs = now;
                EnsureReliabilityDecisions(existing, skill, seedItems);
                return existing;
            }

            // AI/native attack ordering can be: OnSkillUsed(commit) -> Skill.Use end ->
            // GetExecutingElements. The commit-fallback context is intentionally empty
            // because no squad list existed yet. When the real execution list arrives
            // shortly afterwards, carry only that proven commit into the new context.
            // Requiring an empty previous decision set prevents a later attack from
            // inheriting state from an ordinary completed shot.
            inheritLateCommit = source == "executing-list"
                && existing.Consumed
                && existing.Committed
                && existing.OwnerPtr == ownerPtr
                && existing.Decisions.Count == 0
                && age >= 0
                && age <= 5000;
            if (inheritLateCommit) inheritedFromSerial = existing.Serial;
        }

        var ctx = new ReliabilityContext
        {
            Serial = ++_reliabilitySerial,
            SkillPtr = skillPtr,
            OwnerPtr = ownerPtr,
            CreatedAtMs = now,
            LastTouchedAtMs = now,
            Committed = inheritLateCommit,
            LateCommitted = inheritLateCommit
        };
        _reliabilityContexts[skillPtr] = ctx;
        EnsureReliabilityDecisions(ctx, skill, seedItems);
        _log($"RELIABILITY ARM serial={ctx.Serial} source={source} skill={SkillId(skill)} ptr=0x{skillPtr:X} owner=0x{ownerPtr:X} seeded={seedItems.Count}");
        if (inheritLateCommit)
            _log($"RELIABILITY LATE COMMIT serial={ctx.Serial} inheritedFrom={inheritedFromSerial} skill={SkillId(skill)} owner=0x{ownerPtr:X}");
        return ctx;
    }

    private bool TryGetReliabilityNoFireDecision(
        Skill skill,
        int sid,
        out ReliabilityContext context,
        out ReliabilityDecision decision)
    {
        context = null!;
        decision = null!;
        if (sid <= 0 || !TryGetReliabilityContext(skill, includeConsumed: true, out var ctx)) return false;
        if (!ctx.Decisions.TryGetValue(sid, out var d)) return false;
        if (!d.Skip || !d.Candidate) return false;
        context = ctx;
        decision = d;
        return true;
    }

    private bool TryGetReliabilityDecisionByElement(
        Skill skill,
        Element element,
        out ReliabilityContext context,
        out ReliabilityDecision decision)
    {
        context = null!;
        decision = null!;
        if (!Valid(element) || !TryGetReliabilityContext(skill, includeConsumed: true, out var ctx)) return false;
        long elementPtr = Key(element);
        if (elementPtr == 0 || !ctx.Decisions.TryGetValue(elementPtr, out var d)) return false;
        context = ctx;
        decision = d;
        return true;
    }

    private int NoFireChance(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => _greenNoFirePct,
        VeterancyRank.CombatTested => _combatTestedNoFirePct,
        VeterancyRank.Hardened => _hardenedNoFirePct,
        VeterancyRank.Regular => _regularNoFirePct,
        _ => 0
    };

    private bool IsSkillOwnerSuppressed(Skill skill)
    {
        if (_suppressedNoFirePct <= 0 || !Valid(skill)) return false;
        try
        {
            object? actor = TryInvokeManagedNoArg(skill, "GetActor");
            if (actor == null || Key(actor) == 0) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo? method = actor.GetType().GetMethods(flags)
                .FirstOrDefault(m => string.Equals(m.Name, "GetSuppressionState", StringComparison.Ordinal));
            if (method == null) return false;

            object? value;
            var pars = method.GetParameters();
            if (pars.Length == 0) value = method.Invoke(actor, null);
            else if (pars.Length == 1) value = method.Invoke(actor, new object[] { 0f });
            else return false;
            if (value == null) return false;

            string label = value.ToString() ?? "";
            if (string.Equals(label, "Suppressed", StringComparison.OrdinalIgnoreCase)) return true;
            if (label.Contains("Pinned", StringComparison.OrdinalIgnoreCase)) return false;

            try { return Convert.ToInt32(value) == 1; }
            catch { return false; }
        }
        catch { return false; }
    }

    private void EnsureReliabilityDecisions(
        ReliabilityContext context,
        Skill skill,
        IReadOnlyList<ReliabilityElement> items)
    {
        bool suppressed = IsSkillOwnerSuppressed(skill);
        int suppressionPenalty = suppressed ? _suppressedNoFirePct : 0;
        if (suppressed && suppressionPenalty > 0)
        {
            string suppressionKey = $"suppression:{context.OwnerPtr:X}:{suppressionPenalty}";
            if (_combatModifierLogs.Add(suppressionKey))
                _log($"RELIABILITY SUPPRESSION MOD owner=0x{context.OwnerPtr:X} state=Suppressed penalty=+{suppressionPenalty}%");
        }

        foreach (var item in items)
        {
            if (item.DecisionKey == 0 || context.Decisions.ContainsKey(item.DecisionKey)) continue;

            VeterancyRank rank;
            int xp = 0;
            SquaddieIdentitySnapshot? identity = null;

            if (item.Sid > 0)
            {
                if (!_identities.TryGet(item.Sid, out var resolvedIdentity) || !resolvedIdentity.Alive) continue;
                identity = resolvedIdentity;
                xp = _xp.GetXp(item.Sid);
                rank = VeterancyRules.RankFromXp(xp);
            }
            else if (IsTemporaryNpc(item) && IsTemporaryNpcReliabilityEnabled(item))
            {
                rank = GetOrAssignNpcRank(item, out _);
            }
            else
            {
                continue;
            }

            int baseChance = NoFireChance(rank);
            int chance = Math.Max(0, Math.Min(100, baseChance + suppressionPenalty));
            int roll = chance <= 0 ? 100 : System.Random.Shared.Next(1, 101);
            bool skip = chance > 0 && roll <= chance;
            var decision = new ReliabilityDecision
            {
                DecisionKey = item.DecisionKey,
                Sid = item.Sid,
                IsEnemy = item.IsEnemy,
                IsAlliedNpc = item.IsAlliedNpc,
                FactionId = item.FactionId,
                OwnerPtr = item.OwnerPtr,
                Rank = rank,
                ElementIndex = item.ElementIndex,
                ElementPtr = item.ElementPtr,
                Element = item.Element,
                ChancePct = chance,
                Roll = roll,
                Skip = skip
            };
            context.Decisions[item.DecisionKey] = decision;
            _reliabilityRolls++;
            if (item.IsEnemy) _enemyReliabilityRolls++;
            if (item.IsAlliedNpc) _alliedNpcReliabilityRolls++;
            if (skip)
            {
                _reliabilityNoFires++;
                if (item.IsEnemy) _enemyReliabilityNoFires++;
                if (item.IsAlliedNpc) _alliedNpcReliabilityNoFires++;
            }

            if (skip || _verbose)
            {
                if (IsTemporaryNpc(item))
                {
                    string relation = item.IsEnemy ? "hostile" : "allied";
                    _log($"RELIABILITY ROLL serial={context.Serial} npc={relation} faction={FactionLabel(item.FactionId)} template={TemplateLabel(item.TemplateId)} elem=0x{item.ElementPtr:X} idx={item.ElementIndex} rank={VeterancyRules.DisplayName(rank)} baseChance={baseChance}% suppressed={(suppressed ? 1 : 0)} suppressionPenalty=+{suppressionPenalty}% chance={chance}% roll={roll} result={(skip ? "NO_FIRE" : "fire")}");
                }
                else if (identity != null)
                {
                    _log($"RELIABILITY ROLL serial={context.Serial} sid={item.Sid} idx={item.ElementIndex} name={Sanitize(identity.Name)} rank={VeterancyRules.DisplayName(rank)} xp={xp} baseChance={baseChance}% suppressed={(suppressed ? 1 : 0)} suppressionPenalty=+{suppressionPenalty}% chance={chance}% roll={roll} result={(skip ? "NO_FIRE" : "fire")}");
                }
            }
        }
        context.LastTouchedAtMs = Environment.TickCount64;
    }

    private void ApplyCommittedReliabilityConsequences(ReliabilityContext context, Skill skill)
    {
        if (!context.Committed || !Valid(skill)) return;

        int noFire = context.Decisions.Values.Count(x => x.Skip && x.Candidate);
        if (noFire > 0 && !context.FailureNoticeQueued)
        {
            context.FailureNoticeQueued = true;
            QueueFailureNotice(skill, noFire);
        }

        // No separate XP for individual no-fire elements. The squad has already
        // earned its single attack-action XP when MENACE committed the attack.
    }

    private void QueueFailureNotice(Skill skill, int count)
    {
        if (!_showFailureNotice || count <= 0 || !Valid(skill)) return;
        try
        {
            object? actorObj = TryInvokeManagedNoArg(skill, "GetActor");
            if (actorObj is not Actor actor || !Valid(actor)) return;

            long actorKey = Key(actor);
            if (actorKey == 0) return;
            RemoveFailureNotice(actorKey);
            _failureNotices[actorKey] = new FailureNotice
            {
                Actor = actor,
                Count = count,
                CreatedAt = Time.realtimeSinceStartup,
                ExpiresAt = 0f
            };
            _log($"FAILURE NOTICE queued actor=0x{actorKey:X} count={count}");
        }
        catch (Exception e)
        {
            if (_verbose) _log($"FAILURE NOTICE queue error {e.GetType().Name}: {e.Message}");
        }
    }

    private void UpdateFailureNotices()
    {
        if (_failureNotices.Count == 0) return;
        if (!_showFailureNotice)
        {
            ClearFailureNotices();
            return;
        }

        float now = Time.realtimeSinceStartup;
        foreach (long key in _failureNotices.Keys.ToList())
        {
            if (!_failureNotices.TryGetValue(key, out var notice)) continue;
            if (!Valid(notice.Actor))
            {
                RemoveFailureNotice(key);
                continue;
            }

            if (notice.Label == null)
            {
                // UnitHUDs are pooled and can bind a frame after the attack callback.
                // Retry briefly rather than falling back to approximate world/screen math.
                if (now - notice.CreatedAt > 0.75f)
                {
                    RemoveFailureNotice(key);
                    continue;
                }

                if (TryAttachFailureNotice(notice))
                {
                    notice.ExpiresAt = now + 1.0f;
                    _log($"FAILURE NOTICE shown actor=0x{key:X} count={notice.Count}");
                }
                continue;
            }

            if (now >= notice.ExpiresAt)
                RemoveFailureNotice(key);
        }
    }

    private bool TryAttachFailureNotice(FailureNotice notice)
    {
        UnitHUD? actorHud = FindActorUnitHud(notice.Actor);
        if (!Valid(actorHud)) return false;
        try
        {
            var hitpointsBar = actorHud!.m_HitpointsBar;
            var host = hitpointsBar?.parent;
            if (host == null) return false;

            var label = new Label();
            label.name = "SquaddieProbeFailureNotice";
            label.text = notice.Count == 1 ? "1 unit failed to fire" : $"{notice.Count} units failed to fire";
            label.pickingMode = PickingMode.Ignore;
            label.style.position = new StyleEnum<Position>(Position.Absolute);
            label.style.left = new StyleLength(-55f);
            label.style.right = new StyleLength(-55f);
            label.style.bottom = new StyleLength(18f);
            label.style.height = new StyleLength(20f);
            label.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            label.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
            label.style.fontSize = new StyleLength(12f);
            label.style.color = new StyleColor(Color.white);
            label.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.72f));
            label.style.paddingLeft = new StyleLength(4f);
            label.style.paddingRight = new StyleLength(4f);
            label.style.flexShrink = 0f;
            host.Add(label);
            notice.Label = label;
            return true;
        }
        catch (Exception e)
        {
            if (_verbose) _log($"FAILURE NOTICE attach error {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    private void UpdateVeterancyHudMarkers()
    {
        if (!_showVeterancyMarkers && !_vehicleCrewPressure)
        {
            ClearVeterancyHudMarkers();
            return;
        }
        if (!_showVeterancyMarkers) ClearVeterancyHudMarkers();

        float now = Time.realtimeSinceStartup;
        if (now < _nextVeterancyHudRefreshAt) return;
        _nextVeterancyHudRefreshAt = now + 0.20f;

        var wanted = new HashSet<long>();
        try
        {
            var hud = TacticalState.Get()?.GetUI()?.GetHUD();
            var hudList = hud?.m_HUDList;
            int count = hudList?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var unitHud = hudList![i]?.TryCast<UnitHUD>();
                if (unitHud == null) continue;
                Actor actor;
                try { actor = unitHud.GetActor(); }
                catch { continue; }
                if (!Valid(actor)) continue;

                long actorKey = Key(actor);
                if (actorKey == 0) continue;

                // MENACE does not accumulate native suppression on vehicles even when its
                // immunity flags are cleared. 1.9 therefore owns a separate crew-pressure
                // state and 1.9.5.8 renders it through a dedicated mod-owned meter rather
                // than MENACE's unreliable native vehicle suppression ProgressBar.
                if (_vehicleCrewPressure && _vehiclePressureStates.ContainsKey(actorKey))
                {
                    bool pressureEnemy = IsHostileFaction(GetOwnerFactionId(actorKey));
                    if (!pressureEnemy || IsActorVisibleToPlayer(actor))
                    {
                        RefreshVehiclePressureHud(actorKey, unitHud, actor);
                    }
                    else
                    {
                        // Pooled hostile HUDs may exist before the actor is revealed. Never
                        // let the custom pressure display expose a concealed enemy vehicle.
                        HideVehiclePressureHud(unitHud);
                    }
                }
                else
                {
                    // UnitHUD instances are pooled. If a HUD that previously belonged to a
                    // pressured vehicle is rebound to something else, remove our custom UI.
                    RemoveVehiclePressureHudDecorations(unitHud);
                }

                if (!_showVeterancyMarkers)
                {
                    RemoveVeterancyHudMarker(actorKey);
                    continue;
                }

                if (!TryResolveHudVeterancyRank(actor, out var rank, out bool enemy))
                {
                    RemoveVeterancyHudMarker(actorKey);
                    continue;
                }

                // Hostile UnitHUD instances are pooled and can exist while the actor is still
                // concealed. MENACE may rebuild/rebind that HUD when the enemy is revealed.
                // Attaching before reveal leaves us holding a perfectly valid-looking marker
                // that is no longer part of the visible hierarchy. Match Overwatch semantics:
                // never expose hidden enemy veterancy, and attach only once the actor itself is
                // visible to the player.
                if (enemy && !IsActorVisibleToPlayer(actor))
                {
                    RemoveVeterancyHudMarker(actorKey);
                    continue;
                }

                wanted.Add(actorKey);
                if (!TryRefreshVeterancyHudMarker(actorKey, unitHud, rank, enemy))
                    RemoveVeterancyHudMarker(actorKey);
            }
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VETERANCY HUD refresh error {e.GetType().Name}: {e.Message}");
        }

        foreach (long key in _veterancyHudMarkers.Keys.ToList())
            if (!wanted.Contains(key)) RemoveVeterancyHudMarker(key);
    }

    private void HideVehiclePressureBars()
    {
        if (_vehiclePressureStates.Count == 0) return;
        try
        {
            var hud = TacticalState.Get()?.GetUI()?.GetHUD();
            var hudList = hud?.m_HUDList;
            int count = hudList?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var unitHud = hudList![i]?.TryCast<UnitHUD>();
                if (unitHud == null) continue;
                Actor actor;
                try { actor = unitHud.GetActor(); } catch { continue; }
                long key = Key(actor);
                if (key == 0 || !_vehiclePressureStates.ContainsKey(key)) continue;
                HideVehiclePressureHud(unitHud);
            }
        }
        catch { }
    }

    private void RefreshVehiclePressureHud(long actorKey, UnitHUD unitHud, Actor actor)
    {
        try
        {
            if (!Valid(unitHud) || !Valid(actor)) return;
            if (!_vehiclePressureStates.TryGetValue(actorKey, out var state)) return;
            state.Actor = actor;

            float maxPressure = Math.Max(1f, _vehicleBreakContactThreshold);
            float normalized = Math.Max(0f, Math.Min(1f, state.Pressure / maxPressure));
            // Do not reuse MENACE's native suppression ProgressBar for Crew Pressure.
            // On 0.7.15 that bar can display a native/stale value (observed around 50%)
            // even while our authoritative VehiclePressureState is exactly zero. Keep the
            // native vehicle bar hidden and render an independent meter owned only by this mod.
            if (unitHud.m_SuppressionBar != null)
                unitHud.m_SuppressionBar.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            bool changed = Math.Abs(state.LastHudValue - normalized) >= 0.001f;
            RefreshVehiclePressureMeter(unitHud, state, normalized);

            if (!state.HudReadyLogged)
            {
                state.HudReadyLogged = true;
                _log($"VEHICLE PRESSURE HUD READY owner=0x{actorKey:X} pressure={state.Pressure:0.##}/{_vehicleBreakContactThreshold} normalized={normalized:0.###} state={VehiclePressureLabel(state.Pressure)} surface=custom");
            }
            else if (changed)
            {
                _log($"VEHICLE PRESSURE HUD owner=0x{actorKey:X} pressure={state.Pressure:0.##}/{_vehicleBreakContactThreshold} normalized={normalized:0.###} state={VehiclePressureLabel(state.Pressure)} surface=custom");
            }
            state.LastHudValue = normalized;

            RefreshVehiclePressureStatusLabel(unitHud, state);
            HideSelectedVehicleNativeSuppressionBar(actorKey);
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VEHICLE PRESSURE HUD refresh error owner=0x{actorKey:X} {e.GetType().Name}: {e.Message}");
        }
    }

    private void HideVehiclePressureHud(UnitHUD unitHud)
    {
        try
        {
            if (unitHud.m_SuppressionBar != null)
                unitHud.m_SuppressionBar.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            RemoveVehiclePressureHudDecorations(unitHud);
        }
        catch { }
    }

    private static Label? FindVehiclePressureStatusLabel(UnitHUD unitHud)
    {
        try
        {
            var host = unitHud.m_HitpointsBar?.parent;
            if (host == null) return null;
            for (int i = 0; i < host.childCount; i++)
            {
                var child = host.ElementAt(i);
                if (child != null && child.name == "SquaddieProbeVehiclePressureStatus")
                    return child.TryCast<Label>();
            }
        }
        catch { }
        return null;
    }

    private static Label? FindVehiclePressureBadge(UnitHUD unitHud)
    {
        try
        {
            var host = unitHud.m_HitpointsBar?.parent;
            if (host == null) return null;
            for (int i = 0; i < host.childCount; i++)
            {
                var child = host.ElementAt(i);
                if (child != null && child.name == "SquaddieProbeVehiclePressureBadge")
                    return child.TryCast<Label>();
            }
        }
        catch { }
        return null;
    }

    private static void RemoveVehiclePressureStatusLabel(UnitHUD unitHud)
    {
        try { FindVehiclePressureStatusLabel(unitHud)?.RemoveFromHierarchy(); } catch { }
    }

    private static void RemoveVehiclePressureBadge(UnitHUD unitHud)
    {
        try { FindVehiclePressureBadge(unitHud)?.RemoveFromHierarchy(); } catch { }
    }

    private static VisualElement? FindVehiclePressureMeter(UnitHUD unitHud)
    {
        try
        {
            var host = unitHud.m_HitpointsBar?.parent;
            if (host == null) return null;
            for (int i = 0; i < host.childCount; i++)
            {
                var child = host.ElementAt(i);
                if (child != null && child.name == "SquaddieProbeVehiclePressureMeter")
                    return child;
            }
        }
        catch { }
        return null;
    }

    private static VisualElement? FindVehiclePressureMeterFill(VisualElement meter)
    {
        try
        {
            for (int i = 0; i < meter.childCount; i++)
            {
                var child = meter.ElementAt(i);
                if (child != null && child.name == "SquaddieProbeVehiclePressureMeterFill")
                    return child;
            }
        }
        catch { }
        return null;
    }

    private static void RemoveVehiclePressureMeter(UnitHUD unitHud)
    {
        try { FindVehiclePressureMeter(unitHud)?.RemoveFromHierarchy(); } catch { }
    }

    private void RefreshVehiclePressureMeter(UnitHUD unitHud, VehiclePressureState state, float normalized)
    {
        try
        {
            // At zero pressure show nothing at all. This makes mission-start state
            // unambiguous and prevents any native suppression value being mistaken
            // for Crew Pressure.
            if (normalized <= 0.001f)
            {
                RemoveVehiclePressureMeter(unitHud);
                return;
            }

            var host = unitHud.m_HitpointsBar?.parent;
            if (host == null) return;

            var meter = FindVehiclePressureMeter(unitHud);
            if (meter == null)
            {
                meter = new VisualElement();
                meter.name = "SquaddieProbeVehiclePressureMeter";
                meter.pickingMode = PickingMode.Ignore;
                meter.style.position = new StyleEnum<Position>(Position.Absolute);
                meter.style.left = new StyleLength(-14f);
                meter.style.right = new StyleLength(-14f);
                meter.style.bottom = new StyleLength(29f);
                meter.style.height = new StyleLength(6f);
                meter.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.88f));
                meter.style.flexShrink = 0f;
                host.Add(meter);

                var fill = new VisualElement();
                fill.name = "SquaddieProbeVehiclePressureMeterFill";
                fill.pickingMode = PickingMode.Ignore;
                fill.style.position = new StyleEnum<Position>(Position.Absolute);
                fill.style.left = new StyleLength(0f);
                fill.style.top = new StyleLength(0f);
                fill.style.bottom = new StyleLength(0f);
                meter.Add(fill);
            }

            var meterFill = FindVehiclePressureMeterFill(meter);
            if (meterFill == null) return;

            float pct = Math.Max(0f, Math.Min(100f, normalized * 100f));
            meterFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            if (state.Pressure >= _vehicleBreakContactThreshold)
                meterFill.style.backgroundColor = new StyleColor(new Color(1f, 0.25f, 0.20f, 1f));
            else if (state.Pressure >= _vehicleButtonedThreshold)
                meterFill.style.backgroundColor = new StyleColor(new Color(1f, 0.78f, 0.20f, 1f));
            else
                meterFill.style.backgroundColor = new StyleColor(new Color(0.92f, 0.92f, 0.92f, 1f));
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VEHICLE PRESSURE custom-meter error owner=0x{state.OwnerPtr:X} {e.GetType().Name}: {e.Message}");
        }
    }

    private static void RemoveVehiclePressureHudDecorations(UnitHUD unitHud)
    {
        RemoveVehiclePressureStatusLabel(unitHud);
        RemoveVehiclePressureBadge(unitHud);
        RemoveVehiclePressureMeter(unitHud);
    }

    private static int VehiclePressureHudState(float pressure, float buttonedThreshold, float breakThreshold)
    {
        if (pressure >= breakThreshold) return 2;
        if (pressure >= buttonedThreshold) return 1;
        return 0;
    }

    private void RefreshVehiclePressureStatusLabel(UnitHUD unitHud, VehiclePressureState state)
    {
        try
        {
            int currentStatus = VehiclePressureHudState(state.Pressure, _vehicleButtonedThreshold, _vehicleBreakContactThreshold);
            long now = Environment.TickCount64;

            if (currentStatus != state.HudStatusState)
            {
                state.HudStatusState = currentStatus;
                state.HudStatusMessageUntilMs = currentStatus > 0 ? now + 5000 : 0;
            }

            if (currentStatus <= 0)
            {
                RemoveVehiclePressureHudDecorations(unitHud);
                return;
            }

            var host = unitHud.m_HitpointsBar?.parent;
            if (host == null) return;

            // Persistent compact badge. The large status callout below is intentionally
            // temporary so the battlefield does not stay covered in text.
            var badge = FindVehiclePressureBadge(unitHud);
            if (badge == null)
            {
                badge = new Label();
                badge.name = "SquaddieProbeVehiclePressureBadge";
                badge.pickingMode = PickingMode.Ignore;
                badge.style.position = new StyleEnum<Position>(Position.Absolute);
                badge.style.right = new StyleLength(-15f);
                badge.style.bottom = new StyleLength(38f);
                badge.style.width = new StyleLength(24f);
                badge.style.height = new StyleLength(24f);
                badge.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                badge.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
                badge.style.fontSize = new StyleLength(15f);
                badge.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.88f));
                badge.style.paddingLeft = new StyleLength(2f);
                badge.style.paddingRight = new StyleLength(2f);
                badge.style.paddingTop = new StyleLength(1f);
                badge.style.paddingBottom = new StyleLength(1f);
                badge.style.flexShrink = 0f;
                host.Add(badge);
            }

            if (currentStatus >= 2)
            {
                badge.text = "←";
                badge.style.color = new StyleColor(new Color(1f, 0.25f, 0.20f, 1f));
            }
            else
            {
                badge.text = "B";
                badge.style.color = new StyleColor(new Color(1f, 0.78f, 0.20f, 1f));
            }

            // The full callout is shown only for five seconds after entering/changing state.
            if (now >= state.HudStatusMessageUntilMs)
            {
                RemoveVehiclePressureStatusLabel(unitHud);
                return;
            }

            var label = FindVehiclePressureStatusLabel(unitHud);
            if (label == null)
            {
                label = new Label();
                label.name = "SquaddieProbeVehiclePressureStatus";
                label.pickingMode = PickingMode.Ignore;
                label.style.position = new StyleEnum<Position>(Position.Absolute);
                label.style.left = new StyleLength(-58f);
                label.style.right = new StyleLength(-58f);
                label.style.bottom = new StyleLength(66f);
                label.style.minHeight = new StyleLength(31f);
                label.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
                label.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
                label.style.fontSize = new StyleLength(12f);
                label.style.color = new StyleColor(Color.white);
                label.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.88f));
                label.style.paddingLeft = new StyleLength(5f);
                label.style.paddingRight = new StyleLength(5f);
                label.style.paddingTop = new StyleLength(2f);
                label.style.paddingBottom = new StyleLength(2f);
                label.style.flexShrink = 0f;
                host.Add(label);
            }

            string pressureText = $"CREW PRESSURE {state.Pressure:0}/{_vehicleBreakContactThreshold}";
            if (currentStatus >= 2)
            {
                label.text = pressureText + "\nBREAK CONTACT  |  AUTO-RETREAT";
                label.style.color = new StyleColor(new Color(1f, 0.35f, 0.28f, 1f));
            }
            else
            {
                label.text = pressureText + $"\nBUTTONED UP  |  -{_vehicleButtonedApPenalty} AP  |  -{_vehicleButtonedAccuracyPenaltyPct}% ACC";
                label.style.color = new StyleColor(new Color(1f, 0.78f, 0.20f, 1f));
            }
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VEHICLE PRESSURE status-label error owner=0x{state.OwnerPtr:X} {e.GetType().Name}: {e.Message}");
        }
    }

    private void HideSelectedVehicleNativeSuppressionBar(long actorKey)
    {
        try
        {
            object? ui = TacticalState.Get()?.GetUI();
            if (ui == null) return;

            object? panel = TryReadManagedMember(ui, "m_SelectedUnitPanel");
            if (panel == null) return;
            object? selectedActor = TryReadManagedMember(panel, "m_Actor");
            if (Key(selectedActor) != actorKey) return;

            if (TryReadManagedMember(panel, "m_SuppressionBar") is ProgressBar bar)
                bar.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VEHICLE PRESSURE selected-panel hide error owner=0x{actorKey:X} {e.GetType().Name}: {e.Message}");
        }
    }

    private bool TryResolveHudVeterancyRank(Actor actor, out VeterancyRank rank, out bool enemy)
    {
        rank = VeterancyRank.Green;
        enemy = false;
        long ownerPtr = Key(actor);
        if (ownerPtr == 0) return false;

        int factionId = GetOwnerFactionId(ownerPtr);
        enemy = IsHostileFaction(factionId);

        // Player vehicles use their persistent abstract crew pool. Enemy human-crewed
        // vehicles instead receive a battle-local rank from the same temporary NPC
        // system as hostile infantry. Constructs/Wildlife remain deliberately excluded.
        if (!enemy && _vehicleCrewVeterancy && TryResolvePlayerPilotForOwner(ownerPtr, out var vehiclePilot))
        {
            int vehicleXp = _xp.GetPilotXp(vehiclePilot.TemplateId);
            rank = VeterancyRules.RankFromXp(vehicleXp);
            return true;
        }

        if (TryGetTemporaryActorRank(ownerPtr, out var npcRank, out _, out bool npcEnemy))
        {
            rank = npcRank;
            enemy = npcEnemy;
            return true;
        }

        if (!TryGetReliabilityOwnerElements(ownerPtr, out var items) || items.Count == 0) return false;
        int sid = items.FirstOrDefault(x => !IsTemporaryNpc(x) && x.Sid > 0)?.Sid ?? 0;
        if (sid <= 0) return false;
        if (!TryGetReliableSquadAverageXp(sid, out double squadAvg, out _, out _)) return false;
        rank = VeterancyRules.RankFromXp(squadAvg);
        return true;
    }

    private bool TryRefreshVeterancyHudMarker(long actorKey, UnitHUD actorHud, VeterancyRank rank, bool enemy)
    {
        try
        {
            var hitpointsBar = actorHud.m_HitpointsBar;
            var host = hitpointsBar?.parent;
            if (host == null) return false;

            long hudKey = Key(actorHud);
            foreach (long staleKey in _veterancyHudMarkers
                .Where(kv => kv.Key != actorKey && kv.Value.HudKey == hudKey)
                .Select(kv => kv.Key)
                .ToList())
            {
                RemoveVeterancyHudMarker(staleKey);
            }

            VeterancyHudMarker? marker = null;
            bool markerNeedsVisualRefresh = false;
            if (_veterancyHudMarkers.TryGetValue(actorKey, out var existing) && existing.HudKey == hudKey)
            {
                // Enemy HUDs are pooled/rebuilt aggressively on reveal. The UnitHUD pointer may
                // stay the same while its hitpoint host hierarchy is replaced, so HudKey alone
                // is not enough. Recreate the marker if it has become detached or belongs to an
                // old host.
                var currentParent = existing.Element?.parent;
                bool stillAttached = currentParent != null && Key(currentParent) == Key(host);
                if (stillAttached) marker = existing;
                else
                {
                    if (enemy && _verbose) _log($"VETERANCY HUD reattach actor=0x{actorKey:X} reason=host-rebuilt");
                    RemoveVeterancyHudMarker(actorKey);
                }
            }

            if (marker == null)
            {
                RemoveVeterancyHudMarker(actorKey);

                // Always replace an older marker rather than trying to reuse it. 1.9.5.8 used a
                // Label in normal flex layout; even with a transparent Label background that
                // enlarged MENACE's HP-bar host and exposed its dark panel as a large black box.
                // The marker must therefore be an ABSOLUTE overlay which never participates in
                // HUD layout sizing.
                for (int i = host.childCount - 1; i >= 0; i--)
                {
                    var child = host.ElementAt(i);
                    if (child != null && child.name == "SquaddieProbeVeterancy")
                    {
                        try { child.RemoveFromHierarchy(); } catch { }
                    }
                }

                var element = new VisualElement();
                element.name = "SquaddieProbeVeterancy";
                element.pickingMode = PickingMode.Ignore;
                element.style.position = new StyleEnum<Position>(Position.Absolute);
                // Keep the marker out of flex layout and place it immediately to the RIGHT of
                // MENACE's HP/suppression block. For vehicles, use our custom Crew Pressure meter
                // when present. worldBound is in panel coordinates, so convert the calculated
                // position back into the HP host's local coordinates. If layout has not resolved
                // yet, use a conservative fallback and correct it on the next refresh.
                float markerLeft = 44f;
                float markerTop = 0f;
                TryGetVeterancyHudMarkerPosition(actorHud, host, hitpointsBar, out markerLeft, out markerTop);
                element.style.left = new StyleLength(markerLeft);
                element.style.top = new StyleLength(markerTop);
                element.style.width = new StyleLength(18f);
                element.style.height = new StyleLength(18f);
                element.style.flexShrink = 0f;
                element.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                host.Add(element);

                marker = new VeterancyHudMarker
                {
                    Element = element,
                    HudKey = hudKey,
                    Rank = VeterancyRank.Green,
                    Enemy = enemy
                };
                _veterancyHudMarkers[actorKey] = marker;
                markerNeedsVisualRefresh = true;
            }

            // HUD geometry can settle a frame after creation or change when pooled. Recompute the
            // absolute marker position every refresh so it stays immediately to the right of the
            // native HP/suppression block (or the custom vehicle Crew Pressure meter).
            if (TryGetVeterancyHudMarkerPosition(actorHud, host, hitpointsBar, out float markerLeftNow, out float markerTopNow))
            {
                marker.Element.style.left = new StyleLength(markerLeftNow);
                marker.Element.style.top = new StyleLength(markerTopNow);
            }

            if (markerNeedsVisualRefresh || marker.Rank != rank || marker.Enemy != enemy)
            {
                var sprite = GetVeterancySprite(rank);
                if (sprite == null) return false;
                marker.Element.style.backgroundImage = new StyleBackground(sprite);
                marker.Element.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                marker.Rank = rank;
                marker.Enemy = enemy;
                if (_verbose)
                {
                    string glyph = rank switch
                    {
                        VeterancyRank.Green => "bar",
                        VeterancyRank.Regular => "double",
                        VeterancyRank.Crack => "double",
                        VeterancyRank.Elite => "triple",
                        _ => "single"
                    };
                    _log($"VETERANCY HUD marker actor=0x{actorKey:X} side={(enemy ? "enemy" : "player")} rank={VeterancyRules.DisplayName(rank)} glyph={glyph}");
                }
            }
            return true;
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VETERANCY HUD attach error {e.GetType().Name}: {e.Message}");
            return false;
        }
    }


    private static bool TryGetVeterancyHudMarkerPosition(UnitHUD actorHud, VisualElement host, VisualElement hitpointsBar, out float left, out float top)
    {
        const float MarkerSize = 18f;
        const float Gap = 4f;
        left = 44f;
        top = 0f;

        try
        {
            Rect hp = hitpointsBar.worldBound;
            if (hp.width <= 0.5f || hp.height <= 0.5f) return false;

            float right = hp.xMax;
            float topY = hp.yMin;
            float bottomY = hp.yMax;

            // Infantry: MENACE native suppression bar. A display:none element normally resolves
            // to an empty worldBound, so the geometry check naturally excludes hidden bars.
            try
            {
                var suppression = actorHud.m_SuppressionBar;
                if (suppression != null)
                {
                    Rect sr = suppression.worldBound;
                    if (sr.width > 0.5f && sr.height > 0.5f)
                    {
                        right = Math.Max(right, sr.xMax);
                        topY = Math.Min(topY, sr.yMin);
                        bottomY = Math.Max(bottomY, sr.yMax);
                    }
                }
            }
            catch { }

            // Vehicles: Squaddie Veterancy deliberately hides MENACE's stale suppression bar and
            // renders a mod-owned Crew Pressure meter instead. Include that meter when visible.
            try
            {
                var pressure = FindVehiclePressureMeter(actorHud);
                if (pressure != null)
                {
                    Rect pr = pressure.worldBound;
                    if (pr.width > 0.5f && pr.height > 0.5f)
                    {
                        right = Math.Max(right, pr.xMax);
                        topY = Math.Min(topY, pr.yMin);
                        bottomY = Math.Max(bottomY, pr.yMax);
                    }
                }
            }
            catch { }

            Rect hostRect = host.worldBound;
            float localLeft = right - hostRect.xMin + Gap;
            float localTop = ((topY + bottomY) * 0.5f) - hostRect.yMin - (MarkerSize * 0.5f);

            if (float.IsNaN(localLeft) || float.IsInfinity(localLeft)
                || float.IsNaN(localTop) || float.IsInfinity(localTop))
                return false;

            // The marker is an absolute overlay and may legitimately sit just outside the native
            // bar's own rect. Keep only a broad sanity bound to protect against unresolved layout.
            if (localLeft < -64f || localLeft > 512f || localTop < -64f || localTop > 256f)
                return false;

            left = localLeft;
            top = localTop;
            return true;
        }
        catch { return false; }
    }

    private Sprite? GetVeterancySprite(VeterancyRank rank)
    {
        if (_veterancySprites.TryGetValue(rank, out var cached) && cached != null) return cached;

        try
        {
            const int texSize = 40;
            var texture = new Texture2D(texSize, texSize);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            ClearHudTexture(texture);
            Color accent = rank switch
            {
                VeterancyRank.Green => new Color(0.365f, 0.396f, 0.196f, 1f),
                VeterancyRank.CombatTested => new Color(0.96f, 0.62f, 0.20f, 1f),
                VeterancyRank.Hardened => new Color(0.94f, 0.95f, 0.97f, 1f),
                VeterancyRank.Regular => new Color(0.94f, 0.95f, 0.97f, 1f),
                VeterancyRank.Veteran => new Color(1.00f, 0.78f, 0.08f, 1f),
                VeterancyRank.Crack => new Color(1.00f, 0.78f, 0.08f, 1f),
                VeterancyRank.Elite => new Color(1.00f, 0.78f, 0.08f, 1f),
                _ => Color.white
            };
            Color outline = new Color(0.015f, 0.012f, 0.010f, 1f);

            if (rank == VeterancyRank.Green)
            {
                DrawHudLine(texture, 8, 20, 32, 20, outline, 7);
                DrawHudLine(texture, 9, 20, 31, 20, accent, 3);
            }
            else if (rank == VeterancyRank.Regular || rank == VeterancyRank.Crack)
            {
                DrawChevron(texture, 20, 31, 13, outline, 7);
                DrawChevron(texture, 20, 31, 13, accent, 3);
                DrawChevron(texture, 20, 18, 13, outline, 7);
                DrawChevron(texture, 20, 18, 13, accent, 3);
            }
            else if (rank == VeterancyRank.Elite)
            {
                DrawChevron(texture, 20, 36, 12, outline, 7);
                DrawChevron(texture, 20, 36, 12, accent, 3);
                DrawChevron(texture, 20, 24, 12, outline, 7);
                DrawChevron(texture, 20, 24, 12, accent, 3);
                DrawChevron(texture, 20, 12, 12, outline, 7);
                DrawChevron(texture, 20, 12, 12, accent, 3);
            }
            else
            {
                // Combat Tested, Hardened and Veteran use one chevron; color distinguishes them.
                DrawChevron(texture, 20, 27, 14, outline, 7);
                DrawChevron(texture, 20, 27, 14, accent, 3);
            }

            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texSize, texSize), new Vector2(0.5f, 0.5f));
            _veterancySprites[rank] = sprite;
            return sprite;
        }
        catch (Exception e)
        {
            if (_verbose) _log("VETERANCY HUD sprite creation failed: " + e.Message);
            return null;
        }
    }

    private static void ClearHudTexture(Texture2D texture)
    {
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
    }

    private static void DrawChevron(Texture2D texture, int cx, int apexY, int halfWidth, Color color, int thickness)
    {
        int baseY = apexY - 12;
        DrawHudLine(texture, cx - halfWidth, baseY, cx, apexY, color, thickness);
        DrawHudLine(texture, cx, apexY, cx + halfWidth, baseY, color, thickness);
    }

    private static void DrawHudLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps <= 0) steps = 1;
        int radius = Math.Max(0, thickness / 2);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = (int)Math.Round(x0 + dx * t);
            int y = (int)Math.Round(y0 + dy * t);
            for (int oy = -radius; oy <= radius; oy++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int px = x + ox, py = y + oy;
                    if (px < 0 || py < 0 || px >= texture.width || py >= texture.height) continue;
                    if (ox * ox + oy * oy <= radius * radius + 1) texture.SetPixel(px, py, color);
                }
            }
        }
    }

    private void RemoveVeterancyHudMarker(long actorKey)
    {
        if (!_veterancyHudMarkers.TryGetValue(actorKey, out var marker)) return;
        try { marker.Element.RemoveFromHierarchy(); } catch { }
        _veterancyHudMarkers.Remove(actorKey);
    }

    private void ClearVeterancyHudMarkers()
    {
        foreach (long key in _veterancyHudMarkers.Keys.ToList()) RemoveVeterancyHudMarker(key);
        _veterancyHudMarkers.Clear();
    }

    // Current MENACE exposes Entity.IsPlayer() and Entity.IsPlayerOrPlayerAI().
    // Keep the old IsPlayerControlled(bool) path only as a backwards-compatible fallback.
    private static readonly MethodInfo? IsPlayerMethod = typeof(Actor).GetMethod(
        "IsPlayer", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
    private static readonly MethodInfo? IsPlayerOrPlayerAiMethod = typeof(Actor).GetMethod(
        "IsPlayerOrPlayerAI", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
    private static readonly MethodInfo? LegacyIsPlayerControlledMethod = typeof(Actor).GetMethod(
        "IsPlayerControlled", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);

    private static readonly Func<Actor, bool>? IsPlayerDelegate = CreateActorBoolDelegate(IsPlayerMethod);
    private static readonly Func<Actor, bool>? IsPlayerOrPlayerAiDelegate = CreateActorBoolDelegate(IsPlayerOrPlayerAiMethod);
    private static readonly Func<Actor, bool, bool>? LegacyIsPlayerControlledDelegate = CreateLegacyPlayerControlledDelegate();

    private static Func<Actor, bool>? CreateActorBoolDelegate(MethodInfo? method)
    {
        try { return method?.CreateDelegate(typeof(Func<Actor, bool>)) as Func<Actor, bool>; }
        catch { return null; }
    }

    private static Func<Actor, bool, bool>? CreateLegacyPlayerControlledDelegate()
    {
        try { return LegacyIsPlayerControlledMethod?.CreateDelegate(typeof(Func<Actor, bool, bool>)) as Func<Actor, bool, bool>; }
        catch { return null; }
    }

    private static bool IsPlayerControlledCompat(Actor actor, bool includePlayerAi = false)
    {
        if (!Valid(actor)) return false;
        try
        {
            if (includePlayerAi)
            {
                if (IsPlayerOrPlayerAiDelegate != null) return IsPlayerOrPlayerAiDelegate(actor);
                if (IsPlayerOrPlayerAiMethod != null)
                    return Convert.ToBoolean(IsPlayerOrPlayerAiMethod.Invoke(actor, null) ?? false);
            }
            else
            {
                if (IsPlayerDelegate != null) return IsPlayerDelegate(actor);
                if (IsPlayerMethod != null)
                    return Convert.ToBoolean(IsPlayerMethod.Invoke(actor, null) ?? false);
            }

            if (LegacyIsPlayerControlledDelegate != null)
                return LegacyIsPlayerControlledDelegate(actor, includePlayerAi);
            if (LegacyIsPlayerControlledMethod != null)
                return Convert.ToBoolean(LegacyIsPlayerControlledMethod.Invoke(actor, new object[] { includePlayerAi }) ?? false);
        }
        catch { }
        return false;
    }

    private static bool IsActorVisibleToPlayer(Actor actor)
    {
        if (!Valid(actor)) return false;
        try
        {
            if (IsPlayerControlledCompat(actor)) return true;
        }
        catch { }

        // Prefer model visibility: a visible tile alone must never reveal a concealed enemy rank.
        try { return actor.Is3DModelVisibleToPlayer(); }
        catch { }

        try
        {
            var tile = actor.GetTile();
            return Valid(tile) && tile.IsVisibleToPlayer();
        }
        catch { return false; }
    }

    private static UnitHUD? FindActorUnitHud(Actor actor)
    {
        if (!Valid(actor)) return null;
        try
        {
            var hud = TacticalState.Get()?.GetUI()?.GetHUD();
            var hudList = hud?.m_HUDList;
            int count = hudList?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var unitHud = hudList![i]?.TryCast<UnitHUD>();
                if (unitHud == null) continue;
                var owner = unitHud.GetActor();
                if (Key(owner) == Key(actor)) return unitHud;
            }
        }
        catch { }
        return null;
    }

    private void RemoveFailureNotice(long actorKey)
    {
        if (!_failureNotices.TryGetValue(actorKey, out var notice)) return;
        try { notice.Label?.RemoveFromHierarchy(); } catch { }
        _failureNotices.Remove(actorKey);
    }

    private void ClearFailureNotices()
    {
        foreach (long key in _failureNotices.Keys.ToList())
            RemoveFailureNotice(key);
    }

    private long ResolveElementParentEntityPtr(Element element)
    {
        try
        {
            long ptr = Key(element);
            if (ptr == 0) return 0;

            // Prefer the native Element.GetEntity() wrapper over raw field reads.
            try
            {
                long managedPtr = Key(element.GetEntity());
                if (managedPtr != 0) return managedPtr;
            }
            catch { }

            // Fallback: Element.m_Entity is at +0x30 (verified MENACE 0.7.14-0.7.17).
            var obj = new GameObj(new IntPtr(ptr));
            return obj.ReadPtr("m_Entity", 0x30).ToInt64();
        }
        catch { return 0; }
    }

    private static object? TryReadManagedMember(object target, string name)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = target.GetType();

            PropertyInfo? prop = type.GetProperty(name, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
                return prop.GetValue(target);

            FieldInfo? field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(target);
        }
        catch { }
        return null;
    }

    private static object? TryInvokeManagedNoArg(object target, string methodName)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo? method = target.GetType().GetMethod(
                methodName,
                flags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            return method?.Invoke(target, null);
        }
        catch { return null; }
    }

    private long ResolveSkillContainerOwnerPtr(SkillContainer container)
    {
        try
        {
            if (container == null) return 0;

            // Prefer managed IL2CPP wrapper access. It avoids following raw field
            // pointers from a Skill object, which caused AccessViolationException
            // in alpha.10.5 on MENACE 0.7.14.
            object? owner = TryReadManagedMember(container, "Owner")
                ?? TryInvokeManagedNoArg(container, "GetOwner");
            long managedPtr = Key(owner);
            if (managedPtr != 0) return managedPtr;

            // This direct read is retained only for the SkillContainer instance
            // handed to us by MENACE itself in OnSkillUsed. alpha.10.4 proved this
            // exact container +0x10 path stable; unlike skill->container chasing,
            // it never dereferences an untrusted intermediate pointer.
            long ptr = Key(container);
            if (ptr == 0) return 0;
            return new GameObj(new IntPtr(ptr)).ReadPtr(0x10).ToInt64();
        }
        catch { return 0; }
    }

    private long ResolveSkillOwnerEntityPtr(Skill skill)
    {
        long skillPtr = Key(skill);
        if (skillPtr == 0) return 0;

        if (_skillOwnerBySkillPtr.TryGetValue(skillPtr, out long cached) && cached != 0)
            return cached;

        try
        {
            // Prefer native wrapper methods. Reference-code for MENACE 0.7.14
            // exposes Skill.GetEntity()/GetActor(); invoking those wrappers is much
            // safer than manually following Skill.Container at a guessed offset.
            object? owner = TryInvokeManagedNoArg(skill, "GetEntity");
            long ownerPtr = Key(owner);
            string source = "GetEntity";

            if (ownerPtr == 0)
            {
                owner = TryInvokeManagedNoArg(skill, "GetActor");
                ownerPtr = Key(owner);
                source = "GetActor";
            }

            if (ownerPtr == 0)
            {
                object? containerObj = TryReadManagedMember(skill, "Container");
                if (containerObj is SkillContainer container)
                {
                    ownerPtr = ResolveSkillContainerOwnerPtr(container);
                    source = "Container";
                }
            }

            if (ownerPtr != 0)
            {
                _skillOwnerBySkillPtr[skillPtr] = ownerPtr;
                if (_verbose && _skillOwnerResolveLogged.Add(skillPtr))
                    _log($"RELIABILITY OWNER RESOLVED skill={SkillId(skill)} source={source} owner=0x{ownerPtr:X}");
                return ownerPtr;
            }
        }
        catch { }

        if (_verbose && _skillOwnerResolveLogged.Add(skillPtr))
            _log($"RELIABILITY OWNER UNRESOLVED skill={SkillId(skill)} ptr=0x{skillPtr:X}; waiting for SkillContainer.OnSkillUsed cache");
        return 0;
    }

    private bool TryGetReliabilityOwnerElements(long ownerPtr, out List<ReliabilityElement> items)
    {
        items = new List<ReliabilityElement>();
        if (ownerPtr == 0) return false;

        try
        {
            // Verified MENACE 0.7.14 layout: Entity.Elements is List<Element> at
            // +0x20. Player squaddies are recognized by persistent SquaddieId.
            // Hostile elements receive a battle-local rank and are never serialized.
            IntPtr listPtr = new GameObj(new IntPtr(ownerPtr)).ReadPtr(0x20);
            if (listPtr == IntPtr.Zero) return false;

            var listType = typeof(Il2CppSystem.Collections.Generic.List<Element>);
            var ctor = listType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(IntPtr) },
                modifiers: null);
            if (ctor == null) return false;
            if (ctor.Invoke(new object[] { listPtr }) is not Il2CppSystem.Collections.Generic.List<Element> elements) return false;

            int count = elements.Count;
            if (count < 0 || count > 128) return false;

            var rawElements = new List<(int ListIndex, Element Element, int Sid, int ElementIndex, long ElementPtr)>();
            for (int i = 0; i < count; i++)
            {
                Element element = elements[i];
                if (!Valid(element)) continue;

                int sid = 0;
                int idx = -1;
                try { sid = element.GetSquaddieId(); } catch { }
                try { idx = element.GetElementIndex(); } catch { }
                if (idx < 0) continue;

                long elementPtr = Key(element);
                if (elementPtr == 0) continue;
                rawElements.Add((i, element, sid, idx, elementPtr));
            }

            // Resolve the runtime EntityTemplate once per tactical owner. MENACE's
            // IEntityProperties contract exposes GetTemplate(); reflection keeps this
            // diagnostic resilient to generated IL2CPP wrapper naming differences.
            string templateId = ResolveOwnerTemplateId(ownerPtr);

            // Faction remains authoritative for hostiles. Temporary allied combatants
            // are recognized by exact EntityTemplate ID because MENACE may expose them
            // through Player/Civilian/local-force factions depending on the mission.
            int factionId = GetOwnerFactionId(ownerPtr);
            bool hostileNpc = NpcVeterancyProfiles.IsHostileVeterancyFaction(factionId);
            bool hostileVehicle = hostileNpc && NpcVeterancyProfiles.IsHostileVehicleTemplate(factionId, templateId);
            bool alliedNpc = !hostileNpc && NpcVeterancyProfiles.IsAlliedCombatTemplate(templateId);
            // Human-crewed enemy vehicles are ranked at the actor/crew level, not as
            // reliability elements. Their single element therefore never enters the
            // infantry no-fire pipeline.
            if (hostileVehicle) return false;
            if (hostileNpc || alliedNpc)
            {
                if (hostileNpc && !_enemyReliability) return false;
                if (alliedNpc && !_alliedNpcReliability) return false;

                NpcVeterancyProfile profile = GetNpcVeterancyProfile(factionId, templateId);
                if (hostileNpc)
                {
                    if (_enemyOwnersLogged.Add(ownerPtr))
                        _log($"ENEMY OWNER DETECTED faction={FactionLabel(factionId)} template={TemplateLabel(templateId)} owner=0x{ownerPtr:X} elements={rawElements.Count} temporaryProfile={profile.Label}");
                }
                else if (_alliedNpcOwnersLogged.Add(ownerPtr))
                {
                    _log($"ALLIED NPC OWNER DETECTED faction={FactionLabel(factionId)} template={TemplateLabel(templateId)} owner=0x{ownerPtr:X} elements={rawElements.Count} temporaryProfile={profile.Label}");
                }

                foreach (var raw in rawElements)
                {
                    // Element 0 is the leader/special element. Reliability/veterancy is
                    // assigned to ordinary squad elements only, matching player semantics.
                    if (raw.ElementIndex <= 0) continue;

                    items.Add(new ReliabilityElement
                    {
                        Index = raw.ListIndex,
                        Element = raw.Element,
                        DecisionKey = raw.ElementPtr,
                        Sid = 0,
                        IsEnemy = hostileNpc,
                        IsAlliedNpc = alliedNpc,
                        FactionId = factionId,
                        TemplateId = templateId,
                        OwnerPtr = ownerPtr,
                        ElementIndex = raw.ElementIndex,
                        ElementPtr = raw.ElementPtr
                    });
                }
                return items.Count > 0;
            }

            // Owners outside the NPC veterancy path only participate when their elements map
            // to the player's persistent strategy roster. Constructs, Wildlife, civilians
            // and non-combat mission NPCs therefore receive no temporary veterancy.
            foreach (var raw in rawElements)
            {
                if (raw.Sid <= 0 || !_identities.TryGet(raw.Sid, out var identity) || !identity.Alive) continue;

                items.Add(new ReliabilityElement
                {
                    Index = raw.ListIndex,
                    Element = raw.Element,
                    DecisionKey = raw.Sid,
                    Sid = raw.Sid,
                    IsEnemy = false,
                    IsAlliedNpc = false,
                    FactionId = factionId >= 0 ? factionId : (int)Menace.SDK.FactionType.Player,
                    TemplateId = templateId,
                    OwnerPtr = ownerPtr,
                    ElementIndex = raw.ElementIndex,
                    ElementPtr = raw.ElementPtr
                });

                // Refresh the general live map from the authoritative owner list.
                // This also helps the HP experiment without mutating any HP here.
                if (!_liveElements.TryGetValue(raw.ElementPtr, out var state))
                {
                    state = new LiveElementState();
                    _liveElements[raw.ElementPtr] = state;
                }
                state.Element = raw.Element;
                state.SquaddieId = raw.Sid;
                state.ElementIndex = raw.ElementIndex;
                state.ParentEntityPtr = ownerPtr;
            }

            if (items.Count == 0 && (_enemyReliability || _alliedNpcReliability) && rawElements.Count > 0
                && _unclassifiedOwnersLogged.Add(ownerPtr))
            {
                string typeName = "?";
                try { typeName = new GameObj(new IntPtr(ownerPtr)).GetTypeName(); } catch { }
                _log($"RELIABILITY OWNER UNCLASSIFIED owner=0x{ownerPtr:X} faction={FactionLabel(factionId)} template={TemplateLabel(templateId)} type={typeName} elements={rawElements.Count}");
            }

            return items.Count > 0;
        }
        catch (Exception e)
        {
            if (_verbose) _log($"RELIABILITY OWNER ELEMENTS ERROR owner=0x{ownerPtr:X} {e.GetType().Name}: {e.Message}");
            items.Clear();
            return false;
        }
    }

    private string ResolveOwnerTemplateId(long ownerPtr)
    {
        if (ownerPtr == 0) return "";
        if (_ownerTemplateIds.TryGetValue(ownerPtr, out string? cached)) return cached;

        string templateId = "";
        string source = "unresolved";
        string templateType = "?";
        try
        {
            var entity = new Entity(new IntPtr(ownerPtr));

            // Primary path documented by the MENACE IEntityProperties contract.
            object? template = TryInvokeManagedNoArg(entity, "GetTemplate");
            if (template == null)
            {
                // Explicit interface implementations can be emitted with a qualified
                // method name. Search the wrapper without binding to EntityTemplate.
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo? templateMethod = entity.GetType().GetMethods(flags)
                    .FirstOrDefault(m => m.GetParameters().Length == 0
                        && (string.Equals(m.Name, "GetTemplate", StringComparison.Ordinal)
                            || m.Name.EndsWith(".GetTemplate", StringComparison.Ordinal)));
                if (templateMethod != null)
                {
                    try
                    {
                        template = templateMethod.Invoke(entity, null);
                        if (template != null) source = "Entity.interface.GetTemplate";
                    }
                    catch { }
                }
            }
            if (template == null)
            {
                // Generated wrappers sometimes surface a property instead of the
                // reconstructed method; keep a non-invasive fallback for that case.
                template = TryReadManagedMember(entity, "Template")
                    ?? TryReadManagedMember(entity, "m_Template");
                if (template != null) source = "Entity.Template";
            }
            else if (source == "unresolved") source = "Entity.GetTemplate";

            if (template != null)
            {
                templateType = template.GetType().FullName ?? template.GetType().Name;

                // ExtractedData uses DataTemplate.m_ID as the canonical stable key.
                foreach (string member in new[] { "m_ID", "ID", "Id", "id", "name" })
                {
                    object? value = TryReadManagedMember(template, member);
                    string? text = value?.ToString();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    templateId = text.Trim();
                    source += "." + member;
                    break;
                }

                if (string.IsNullOrWhiteSpace(templateId))
                {
                    foreach (string method in new[] { "GetID", "GetId", "GetName" })
                    {
                        object? value = TryInvokeManagedNoArg(template, method);
                        string? text = value?.ToString();
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        templateId = text.Trim();
                        source += "." + method;
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            source = $"error:{e.GetType().Name}";
        }

        templateId ??= "";
        _ownerTemplateIds[ownerPtr] = templateId;
        if (_ownerTemplateDiagnosticsLogged.Add(ownerPtr))
        {
            int factionId = GetOwnerFactionId(ownerPtr);
            string typeName = "?";
            try { typeName = new GameObj(new IntPtr(ownerPtr)).GetTypeName(); } catch { }
            _log($"ENTITY TEMPLATE RESOLVE owner=0x{ownerPtr:X} faction={FactionLabel(factionId)} entityType={Sanitize(typeName)} templateType={Sanitize(templateType)} template={TemplateLabel(templateId)} source={Sanitize(source)}");
        }

        return templateId;
    }

    private static string TemplateLabel(string? templateId)
        => string.IsNullOrWhiteSpace(templateId) ? "?" : Sanitize(templateId.Trim());

    private static bool IsHostileFaction(int factionId)
    {
        return factionId == (int)Menace.SDK.FactionType.EnemyLocalForces
            || factionId == (int)Menace.SDK.FactionType.Pirates
            || factionId == (int)Menace.SDK.FactionType.Wildlife
            || factionId == (int)Menace.SDK.FactionType.Constructs
            || factionId == (int)Menace.SDK.FactionType.RogueArmy;
    }

    private int GetOwnerFactionId(long ownerPtr)
    {
        if (ownerPtr == 0) return -1;

        // Use MENACE's native Actor.GetFaction() path first. The alpha.10.10
        // raw FactionId read did not classify live pirate actors even though the
        // same actor pointers were valid everywhere else. Overwatch independently
        // proved GetFaction() on UnitActor / TransientActor / Actor is stable on
        // MENACE 0.7.14, and this keeps SquaddieProbe independent of Overwatch.
        try
        {
            var actor = new Actor(new IntPtr(ownerPtr));

            try
            {
                var unit = actor.TryCast<UnitActor>();
                if (unit != null) return (int)unit.GetFaction();
            }
            catch { }

            try
            {
                var transient = actor.TryCast<TransientActor>();
                if (transient != null) return (int)transient.GetFaction();
            }
            catch { }

            try { return (int)actor.GetFaction(); }
            catch { }
        }
        catch { }

        // Last-resort compatibility fallback only. Do not make this the primary
        // classification path: runtime field layout/name resolution proved too
        // brittle for hostile actors in alpha.10.10.
        try { return new GameObj(new IntPtr(ownerPtr)).ReadInt("FactionId", 0xBC); }
        catch { return -1; }
    }

    private static string FactionLabel(int factionId)
    {
        if (factionId >= (int)Menace.SDK.FactionType.Neutral && factionId <= (int)Menace.SDK.FactionType.RogueArmy)
            return TacticalController.GetFactionName((Menace.SDK.FactionType)factionId);
        return $"Unknown({factionId})";
    }

    private NpcVeterancyProfile GetNpcVeterancyProfile(int factionId, string? templateId)
    {
        return NpcVeterancyProfiles.Resolve(factionId, templateId, _detailedNpcProfiles);
    }

    private NpcVeterancyProfile GetNpcVehicleVeterancyProfile(int factionId, string? templateId)
    {
        return NpcVeterancyProfiles.ResolveVehicle(factionId, templateId, _detailedNpcProfiles);
    }

    private VeterancyRank GetOrAssignNpcVehicleRank(long ownerPtr, int factionId, string templateId, out int profileRoll)
    {
        profileRoll = 0;
        if (ownerPtr != 0 && _npcVehicleRanks.TryGetValue(ownerPtr, out var existing))
        {
            EnsureNpcVehiclePressureState(ownerPtr, existing.Rank, "npc-rank-existing");
            return existing.Rank;
        }

        NpcVeterancyProfile profile = GetNpcVehicleVeterancyProfile(factionId, templateId);
        profileRoll = System.Random.Shared.Next(1, 101);
        VeterancyRank rank = profile.RankForRoll(profileRoll);

        if (ownerPtr != 0)
        {
            _npcVehicleRanks[ownerPtr] = new NpcRankState
            {
                Rank = rank,
                FactionId = factionId,
                TemplateId = templateId,
                OwnerPtr = ownerPtr,
                ElementIndex = -1
            };
        }

        if (_enemyVehicleOwnersLogged.Add(ownerPtr))
            _log($"NPC VEHICLE RANK ASSIGN relation=hostile faction={FactionLabel(factionId)} template={TemplateLabel(templateId)} owner=0x{ownerPtr:X} profileRoll={profileRoll} rank={VeterancyRules.DisplayName(rank)} profile={profile.Label}");
        EnsureNpcVehiclePressureState(ownerPtr, rank, "npc-rank-assign");
        return rank;
    }

    private bool TryGetTemporaryActorRank(long ownerPtr, out VeterancyRank rank, out bool vehicle, out bool enemy)
    {
        rank = VeterancyRank.Green;
        vehicle = false;
        enemy = false;
        if (ownerPtr == 0) return false;

        int factionId = GetOwnerFactionId(ownerPtr);
        string templateId = ResolveOwnerTemplateId(ownerPtr);
        bool hostileHuman = NpcVeterancyProfiles.IsHostileVeterancyFaction(factionId);

        if (hostileHuman && _enemyReliability
            && NpcVeterancyProfiles.IsHostileVehicleTemplate(factionId, templateId))
        {
            enemy = true;
            vehicle = true;
            rank = GetOrAssignNpcVehicleRank(ownerPtr, factionId, templateId, out _);
            return true;
        }

        if (!TryGetReliabilityOwnerElements(ownerPtr, out var items) || items.Count == 0) return false;
        var npcItems = items.Where(IsTemporaryNpc).ToList();
        if (npcItems.Count == 0) return false;

        enemy = npcItems.Any(x => x.IsEnemy);
        bool allied = npcItems.Any(x => x.IsAlliedNpc);
        if (enemy && !_enemyReliability) return false;
        if (allied && !_alliedNpcReliability) return false;

        double ordinalTotal = 0d;
        int assigned = 0;
        foreach (var item in npcItems)
        {
            if (!IsTemporaryNpcReliabilityEnabled(item)) continue;
            ordinalTotal += (int)GetOrAssignNpcRank(item, out _);
            assigned++;
        }
        if (assigned == 0) return false;

        int rounded = (int)Math.Floor((ordinalTotal / assigned) + 0.5d);
        rounded = Math.Max((int)VeterancyRank.Green, Math.Min((int)VeterancyRank.Elite, rounded));
        rank = (VeterancyRank)rounded;
        return true;
    }

    private static bool IsTemporaryNpc(ReliabilityElement item)
        => item.IsEnemy || item.IsAlliedNpc;

    private static bool IsTemporaryNpc(ReliabilityDecision decision)
        => decision.IsEnemy || decision.IsAlliedNpc;

    private bool IsTemporaryNpcReliabilityEnabled(ReliabilityElement item)
        => item.IsEnemy ? _enemyReliability : item.IsAlliedNpc && _alliedNpcReliability;

    private VeterancyRank GetOrAssignNpcRank(ReliabilityElement item, out int profileRoll)
    {
        profileRoll = 0;
        if (item.ElementPtr != 0 && _npcRanks.TryGetValue(item.ElementPtr, out var existing))
            return existing.Rank;

        NpcVeterancyProfile profile = GetNpcVeterancyProfile(item.FactionId, item.TemplateId);
        profileRoll = System.Random.Shared.Next(1, 101);
        VeterancyRank rank = profile.RankForRoll(profileRoll);

        if (item.ElementPtr != 0)
        {
            _npcRanks[item.ElementPtr] = new NpcRankState
            {
                Rank = rank,
                FactionId = item.FactionId,
                TemplateId = item.TemplateId,
                OwnerPtr = item.OwnerPtr,
                ElementIndex = item.ElementIndex
            };
        }

        string relation = item.IsEnemy ? "hostile" : item.IsAlliedNpc ? "allied" : "npc";
        _log($"NPC RANK ASSIGN relation={relation} faction={FactionLabel(item.FactionId)} template={TemplateLabel(item.TemplateId)} owner=0x{item.OwnerPtr:X} elem=0x{item.ElementPtr:X} idx={item.ElementIndex} profileRoll={profileRoll} rank={VeterancyRules.DisplayName(rank)} profile={profile.Label}");
        return rank;
    }

    private LiveElementState? FindLiveElement(long ownerPtr, int elementIndex)
    {
        foreach (var state in _liveElements.Values)
        {
            if (state.Element == null || !Valid(state.Element)) continue;

            if (state.ElementIndex < 0)
            {
                try { state.ElementIndex = state.Element.GetElementIndex(); } catch { }
            }
            if (state.ElementIndex != elementIndex) continue;

            if (state.ParentEntityPtr == 0)
                state.ParentEntityPtr = ResolveElementParentEntityPtr(state.Element);
            if (ownerPtr != 0 && state.ParentEntityPtr != ownerPtr) continue;

            return state;
        }
        return null;
    }

    private bool TryResolveExecutingIndices(
        Skill skill,
        long ownerPtr,
        IReadOnlyList<ReliabilityElement> ownerElements,
        Il2CppSystem.Collections.Generic.List<int> list,
        out List<ReliabilityElement> items,
        out List<int> rawValues)
    {
        items = new List<ReliabilityElement>();
        rawValues = new List<int>();
        try
        {
            int count = list.Count;
            if (count < 0 || count > 128) return false;
            if (ownerPtr == 0 || ownerElements.Count == 0) return false;

            var byIndex = ownerElements
                .GroupBy(x => x.ElementIndex)
                .ToDictionary(g => g.Key, g => g.First());

            rawValues = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int elementIndex = list[i];
                rawValues.Add(elementIndex);
                if (!byIndex.TryGetValue(elementIndex, out var ownerElement)) continue;

                items.Add(new ReliabilityElement
                {
                    Index = i,
                    Element = ownerElement.Element,
                    DecisionKey = ownerElement.DecisionKey,
                    Sid = ownerElement.Sid,
                    IsEnemy = ownerElement.IsEnemy,
                    IsAlliedNpc = ownerElement.IsAlliedNpc,
                    FactionId = ownerElement.FactionId,
                    TemplateId = ownerElement.TemplateId,
                    OwnerPtr = ownerElement.OwnerPtr,
                    ElementIndex = elementIndex,
                    ElementPtr = ownerElement.ElementPtr
                });
            }

            return true;
        }
        catch (Exception e)
        {
            _log($"RELIABILITY INDEX READ ERROR {e.GetType().Name}: {e.Message}");
            items.Clear();
            rawValues.Clear();
            return false;
        }
    }

    private void OnNativeSkillUsedForXp(IntPtr userPtr, IntPtr skillPtr, IntPtr targetTilePtr)
    {
        if (!_enabled || (!_awardXp && !_vehicleCrewVeterancy && !_vehicleCrewPressure) || userPtr == IntPtr.Zero || skillPtr == IntPtr.Zero) return;

        try
        {
            var skill = new Skill(skillPtr);
            if (!Valid(skill)) return;

            long user = userPtr.ToInt64();
            long targetKey = targetTilePtr.ToInt64();

            // 1.9.4 integrates MENACE's native Command: Rally with custom Vehicle
            // Crew Pressure. TacticalEventHooks.OnSkillUsed already reports Rally,
            // so handle it before the attack-only XP filter without adding a new
            // Harmony hook. Native Rally still owns morale/suppression; we only
            // mirror its -80 suppression intent into Crew Pressure.
            if (_vehicleCrewPressure && IsVehicleRallySkill(skill))
            {
                ApplyVehicleRallyFromCommittedSkill(user, skill, targetKey);
                return;
            }

            // XP has slightly different attack semantics from reliability. Some
            // committed MENACE attacks (notably chaingun salvo skills) are excluded
            // from IsRealAttack() because reliability must ignore wrapper/child
            // firing skills. For XP they are valid combat actions; the existing
            // actor+target dedup below collapses any parent/child dispatch.
            if (!IsXpAttack(skill)) return;

            // Vehicle pressure is independent from XP. Apply it before any of the
            // player/enemy XP branches return from this already-stable commit hook.
            ApplyVehiclePressureFromCommittedAttack(user, skill, targetKey);

            // Player attack: one XP event for the squad action, regardless of how
            // many squaddies actually fire or fail their reliability roll.
            int attackerSid = ResolvePlayerSquaddieIdForOwner(user);
            if (_awardXp && attackerSid > 0)
            {
                TrackDeployedSquad(attackerSid);
                // Collapse multi-skill wrappers such as Coordinated Fire into the
                // single squad action the player committed. The short actor+target
                // window is long enough for child skill dispatch, but far shorter
                // than a normal second player command.
                string dedup = $"attack-action:{user:X}:{targetKey:X}";
                if (RememberXpEvent(dedup, 1200))
                    AwardSquadXp(attackerSid, _xpPerAttack, "attack", $"skill={SkillId(skill)}");
                return;
            }

            if (_vehicleCrewVeterancy && TryResolvePlayerPilotForOwner(user, out var pilot))
            {
                TrackDeployedPilot(pilot, "attack");
                string dedup = $"vehicle-attack-action:{user:X}:{targetKey:X}";
                if (_vehicleXpPerAttack > 0 && RememberXpEvent(dedup, 1200))
                    AwardPilotXp(pilot, _vehicleXpPerAttack, "attack", $"skill={SkillId(skill)}");
                return;
            }

            if (!_awardXp && !_vehicleCrewVeterancy && !_vehicleCrewPressure) return;

            // Enemy attack: being deliberately targeted is combat experience even
            // if the attack later misses. Award it once per enemy attack action,
            // not per projectile, hit, or damaged squaddie.
            int factionId = GetOwnerFactionId(user);
            if (!IsHostileFaction(factionId) || targetTilePtr == IntPtr.Zero) return;

            long targetOwner = 0;
            int targetSid = 0;
            PilotIdentitySnapshot? targetPilot = null;
            string targetResolution = "tile";

            // Fast path: some attacks attach the target Entity directly to the Tile.
            try
            {
                var tile = new Tile(targetTilePtr);
                var target = tile.GetEntity();
                if (Valid(target))
                {
                    targetOwner = Key(target);
                    targetSid = ResolvePlayerSquaddieIdForOwner(targetOwner);
                    if (targetSid <= 0 && _vehicleCrewVeterancy && TryResolvePlayerPilotForOwner(targetOwner, out var resolvedTargetPilot))
                        targetPilot = resolvedTargetPilot;
                }
            }
            catch { }

            // MENACE also supports attacks whose real target is carried as
            // Skill.GetHitchance(..., _overrideTargetEntity, ...). In that case
            // tile.GetEntity() is null even though the player unit is explicitly
            // under fire. Consume the hint only after the attack is committed.
            if (targetSid <= 0 && targetPilot == null && TryResolveIncomingTargetHint(user, targetKey, skillPtr.ToInt64(), out var hint))
            {
                targetOwner = hint.TargetOwnerPtr;
                targetSid = hint.DefenderSid;
                targetResolution = "hitchance-override";
                if (targetSid <= 0 && !string.IsNullOrWhiteSpace(hint.DefenderPilotTemplateId)
                    && TryResolvePlayerPilotForOwner(targetOwner, out var hintedPilot))
                    targetPilot = hintedPilot;
            }

            if (targetSid <= 0 && targetPilot == null)
            {
                if (_verbose)
                    _log($"XP TARGET UNRESOLVED attacker=0x{user:X} tile=0x{targetKey:X} skill={SkillId(skill)}");
                return;
            }

            string incomingDedup = $"targeted-action:{user:X}:{targetKey:X}:{targetOwner:X}";
            if (!RememberXpEvent(incomingDedup, 1200)) return;

            if (targetSid > 0 && _awardXp)
            {
                TrackDeployedSquad(targetSid);
                _log($"XP TARGET RESOLVED via={targetResolution} sid={targetSid} attackerFaction={FactionLabel(factionId)} skill={SkillId(skill)}");
                AwardSquadXp(targetSid, _xpPerIncomingAttack, "targeted", $"by={FactionLabel(factionId)} skill={SkillId(skill)}");
            }
            else if (targetPilot != null && _vehicleCrewVeterancy)
            {
                TrackDeployedPilot(targetPilot, "targeted");
                _log($"VEHICLE TARGET RESOLVED via={targetResolution} pilot={Sanitize(targetPilot.Name)} template={Sanitize(targetPilot.TemplateId)} attackerFaction={FactionLabel(factionId)} skill={SkillId(skill)}");
                AwardPilotXp(targetPilot, _vehicleXpPerIncomingAttack, "targeted", $"by={FactionLabel(factionId)} skill={SkillId(skill)}");
            }
        }
        catch (Exception e)
        {
            _log($"XP SKILL EVENT ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void TacticalManagerInvokeOnElementDeathPostfix(object[] __args)
    {
        var c = Current;
        if (c == null || !c._enabled) return;
        if (__args == null || __args.Length < 3) return;

        try
        {
            var victim = __args[0] as Entity;
            var element = __args[1] as Element;
            var attacker = __args[2] as Entity;
            if (!Valid(victim) || !Valid(element)) return;

            long victimPtr = Key(victim);
            long elementPtr = Key(element);
            bool victimIsVehicle = false;
            try
            {
                var template = victim!.GetTemplate();
                victimIsVehicle = Valid(template) && template.ActorType == ActorType.Vehicle;
            }
            catch { }

            // A tactical infantry element death is the casualty event. MENACE decides
            // only after the mission whether that squaddie is recovered.
            if (elementPtr != 0
                && c._liveElements.TryGetValue(elementPtr, out var live)
                && live.PlayerSquaddie
                && live.SquaddieId > 0
                && c._identities.Contains(live.SquaddieId)
                && c._downedSquaddiesThisMission.Add(live.SquaddieId))
            {
                c._log($"SQUADDIE CASUALTY sid={live.SquaddieId} {c._identities.Label(live.SquaddieId)} xp={c._xp.GetXp(live.SquaddieId)} awaiting-post-mission-recovery=1");
            }

            // Player vehicle destruction resolves the currently active abstract crew members
            // exactly once. Each member independently survives or dies. Survivors
            // are wounded and lose the configured XP percentage; deaths are replaced
            // by a fresh 0-XP crew member so the crew always remains four strong.
            if (victimIsVehicle
                && victimPtr != 0
                && c._vehicleCrewVeterancy
                && c.TryResolvePlayerPilotForOwner(victimPtr, out var destroyedPilot)
                && c._playerVehicleDestructionsResolved.Add(victimPtr))
            {
                c.TrackDeployedPilot(destroyedPilot, "vehicle-destroyed");
                c.ResolveDestroyedVehicleCrew(destroyedPilot, victimPtr);
            }

            if (!Valid(attacker)) return;

            int victimFaction = c.GetOwnerFactionId(victimPtr);
            if (!IsHostileFaction(victimFaction)) return;

            int elementIndex = -1;
            try { elementIndex = element.GetElementIndex(); } catch { }

            long attackerPtr = Key(attacker);
            if (c._vehicleCrewVeterancy
                && elementPtr != 0
                && c.TryResolvePlayerPilotForOwner(attackerPtr, out var pilot)
                && c._vehicleElementKillsAwarded.Add(elementPtr))
            {
                c.TrackDeployedPilot(pilot, victimIsVehicle ? "enemy-vehicle-destroyed" : "enemy-element-kill");
                int vehicleCrewAward = victimIsVehicle ? c._xpPerEnemyVehicleKill : c._vehicleXpPerEnemyElementKill;
                if (vehicleCrewAward > 0)
                {
                    c.AwardPilotXp(
                        pilot,
                        vehicleCrewAward,
                        victimIsVehicle ? "enemy-vehicle-destroyed" : "enemy-element-kill",
                        victimIsVehicle
                            ? $"victimFaction={FactionLabel(victimFaction)} entity=0x{victimPtr:X}"
                            : $"victimFaction={FactionLabel(victimFaction)} element=0x{elementPtr:X} idx={elementIndex}");
                }
            }

            if (!c._awardXp) return;
            int attackerSid = c.ResolvePlayerSquaddieIdForOwner(attackerPtr);
            if (attackerSid <= 0) return;
            if (elementPtr == 0 || !c._enemyElementKillsAwarded.Add(elementPtr)) return;

            c.TrackDeployedSquad(attackerSid);
            int infantryAward = victimIsVehicle ? c._xpPerEnemyVehicleKill : c._xpPerEnemyElementKill;
            if (infantryAward <= 0) return;
            c.AwardSquadXp(
                attackerSid,
                infantryAward,
                victimIsVehicle ? "enemy-vehicle-destroyed" : "enemy-element-kill",
                victimIsVehicle
                    ? $"victimFaction={FactionLabel(victimFaction)} entity=0x{victimPtr:X}"
                    : $"victimFaction={FactionLabel(victimFaction)} element=0x{elementPtr:X} idx={elementIndex}");
        }
        catch (Exception e)
        {
            c._log($"XP KILL EVENT ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void TacticalManagerInvokeOnDamageReceivedPostfix(Entity __0, Entity __1, Skill __2, DamageInfo __3)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._vehicleCrewPressure) return;
        if (!Valid(__0) || !Valid(__3)) return;

        try
        {
            long targetPtr = Key(__0);
            if (targetPtr == 0 || !c.TryResolveVehiclePressureTarget(targetPtr, out _, out _)) return;

            int damage = 0;
            int armorDamage = 0;
            bool inflicted = false;
            bool destroyed = false;
            bool crit = false;
            bool penetrated = false;
            try { damage = __3.Damage; } catch { }
            try { armorDamage = __3.ArmorDamage; } catch { }
            try { inflicted = __3.IsDamageInflicted; } catch { }
            try { destroyed = __3.IsTargetDestroyed; } catch { }
            try { crit = __3.IsCrit; } catch { }
            try { penetrated = __3.IsAbleToPenetrate; } catch { }

            // Pressure from merely being fired at is handled by the normal weapon-pressure path.
            // This event is reserved for the much larger shock of a hit that actually inflicts damage.
            if (!inflicted || damage <= 0 || destroyed || c._vehicleDamagePressureBump <= 0) return;

            long attackerPtr = Valid(__1) ? Key(__1) : 0;
            long skillPtr = Valid(__2) ? Key(__2) : 0;
            string dedup = $"vehicle-pressure-damage:{targetPtr:X}:{attackerPtr:X}:{skillPtr:X}";
            if (!c.RememberXpEvent(dedup, 1500)) return;

            bool hasThreat = false;
            Vector3 threatPos = default;
            if (Valid(__1) && attackerPtr != targetPtr)
            {
                try
                {
                    threatPos = __1.GetPos();
                    hasThreat = true;
                }
                catch { }
            }

            c.ApplyVehiclePressureShock(
                targetPtr,
                c._vehicleDamagePressureBump,
                "confirmed-damage",
                $"damage={damage} armorDamage={armorDamage} crit={(crit ? 1 : 0)} penetrated={(penetrated ? 1 : 0)} skill={(Valid(__2) ? SkillId(__2) : "-")}",
                hasThreat,
                threatPos);
        }
        catch (Exception e)
        {
            c._log($"VEHICLE DAMAGE PRESSURE ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void SkillContainerOnDefectCausedPostfix(Actor __0, DefectTemplate __1, Skill __2)
    {
        var c = Current;
        if (c == null || !c._enabled || (!c._vehicleCrewVeterancy && !c._vehicleCrewPressure)) return;
        if (!Valid(__0) || !Valid(__1)) return;

        try
        {
            long targetPtr = Key(__0);
            if (targetPtr == 0) return;

            int severityValue;
            try { severityValue = Convert.ToInt32(__1.Severity); }
            catch { severityValue = 0; }
            string severity = __1.Severity.ToString();
            string dedup = $"vehicle-defect:{targetPtr:X}:{Key(__1):X}:{Key(__2):X}:{severityValue}";
            if (!c.RememberXpEvent(dedup, 750)) return;

            if (c._vehicleCrewPressure && c.TryResolveVehiclePressureTarget(targetPtr, out _, out _))
            {
                int defectBump = __1.Severity == DefectSeverity.Heavy
                    ? c._vehicleHeavyDefectPressureBump
                    : (__1.Severity == DefectSeverity.Medium
                        ? c._vehicleMediumDefectPressureBump
                        : c._vehicleLightDefectPressureBump);
                if (defectBump > 0)
                    c.ApplyVehiclePressureShock(targetPtr, defectBump, $"defect-{severity.ToLowerInvariant()}", $"severity={severity}", false, default);
            }

            if (!c._vehicleCrewVeterancy || !c.TryResolvePlayerPilotForOwner(targetPtr, out var pilot)) return;

            c.TrackDeployedPilot(pilot, "defect");
            if (__1.Severity == DefectSeverity.Heavy)
                c.ResolveMajorVehicleDefect(pilot, severity, targetPtr);
            else
                c.ResolveMinorVehicleDefect(pilot, severity, targetPtr);
        }
        catch (Exception e)
        {
            c._log($"VEHICLE CREW DEFECT ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private void OnNativeObjectiveStateChangedForXp(IntPtr objectivePtr, int newState)
    {
        if (!_enabled || objectivePtr == IntPtr.Zero) return;
        try
        {
            long ptr = objectivePtr.ToInt64();
            var kind = ClassifyObjective(ptr, out string source);
            if (kind != ObjectiveKind.Unknown) _objectiveKinds[ptr] = kind;

            // ObjectiveState 2 is Completed and 3 is Failed in the current game.
            // Capture completion while the Objective object is still alive: by the
            // time TacticalManager.Finish() runs MENACE may already have cleared the
            // ObjectiveManager list. The pointer is used only as a mission-local key.
            if (newState == 2)
            {
                if (kind != ObjectiveKind.Unknown)
                {
                    _completedObjectives[ptr] = kind;
                    _log($"OBJECTIVE XP CAPTURE ptr=0x{ptr:X} kind={kind} source={source} completed=1");
                }
                else
                {
                    _log($"OBJECTIVE XP CAPTURE unresolved ptr=0x{ptr:X} state={newState} source={source}");
                }
            }
            else if (newState == 3)
            {
                _completedObjectives.Remove(ptr);
            }

            if (_verbose)
                _log($"OBJECTIVE XP STATE ptr=0x{ptr:X} state={newState} kind={kind} source={source}");
        }
        catch (Exception e)
        {
            if (_verbose) _log($"OBJECTIVE XP STATE ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void TacticalManagerFinishPostfix(object[] __args)
    {
        var c = Current;
        if (c == null || !c._enabled) return;

        int reason = 0;
        try
        {
            if (__args != null && __args.Length > 0 && __args[0] != null)
                reason = Convert.ToInt32(__args[0]);
        }
        catch { }

        // 3 = loading a savegame. Never award mission XP during a tactical rollback.
        if (reason == 3)
        {
            c._missionObjectiveXpAwarded = true;
            c._missionVehicleXpAwarded = true;
            c._log($"MISSION XP skipped finishReason={reason}");
            return;
        }

        if (c._vehicleCrewVeterancy && !c._missionVehicleXpAwarded)
            c.AwardVehicleMissionCompletionXp(reason);

        if ((c._awardXp || c._vehicleCrewVeterancy) && !c._missionObjectiveXpAwarded)
        {
            if (reason == 1)
            {
                c._missionObjectiveXpAwarded = true;
                c._log($"MISSION OBJECTIVE XP skipped finishReason={reason}");
            }
            else
            {
                c.AwardMissionObjectiveXp(reason);
            }
        }
    }

    private void AwardMissionObjectiveXp(int finishReason)
    {
        if (_missionObjectiveXpAwarded) return;
        _missionObjectiveXpAwarded = true;

        try
        {
            int missionStatus = Menace.SDK.Mission.GetStatus();
            if (missionStatus == Menace.SDK.Mission.STATUS_FAILED)
            {
                _log($"MISSION OBJECTIVE XP skipped: mission status Failed finishReason={finishReason}");
                return;
            }

            // Do not query Mission.GetObjectives() here. Runtime testing showed that
            // MENACE clears that list before/inside TacticalManager.Finish(). Instead
            // use the deduplicated completion records captured from
            // TacticalEventHooks.OnObjectiveStateChanged while the mission was live.
            int primaryCompleted = 0;
            int secondaryCompleted = 0;
            foreach (var pair in _completedObjectives.OrderBy(p => p.Key))
            {
                if (pair.Value == ObjectiveKind.Primary) primaryCompleted++;
                else if (pair.Value == ObjectiveKind.Secondary) secondaryCompleted++;
                _log($"OBJECTIVE XP RESULT ptr=0x{pair.Key:X} kind={pair.Value} complete=1 failed=0 source=cached-state");
            }

            if (primaryCompleted == 0 && secondaryCompleted == 0)
            {
                _log($"MISSION OBJECTIVE XP no completed objectives captured finishReason={finishReason}");
                return;
            }

            // Do not allow retreat/failure farming from a secondary objective alone.
            if (primaryCompleted <= 0)
            {
                _log($"MISSION OBJECTIVE XP skipped: no completed primary objective finishReason={finishReason} secondaryCompleted={secondaryCompleted}");
                return;
            }

            int squadTotal = primaryCompleted * _xpPerPrimaryObjective + secondaryCompleted * _xpPerSecondaryObjective;
            int vehicleTotal = primaryCompleted * _vehicleXpPerPrimaryObjective + secondaryCompleted * _vehicleXpPerSecondaryObjective;

            // Spawn tracking should already contain every deployed player squad.
            // Rebuild from live elements as a safety net for unusual spawn paths.
            foreach (var live in _liveElements.Values)
            {
                if (live.PlayerSquaddie && live.SquaddieId > 0)
                    TrackDeployedSquad(live.SquaddieId);
            }

            var reps = _deployedSquadRepresentatives.Values.Distinct().ToList();
            if (_awardXp && squadTotal > 0 && reps.Count > 0)
            {
                _log($"MISSION OBJECTIVE XP primary={primaryCompleted}x{_xpPerPrimaryObjective} secondary={secondaryCompleted}x{_xpPerSecondaryObjective} total=+{squadTotal} deployedSquads={reps.Count} finishReason={finishReason} missionStatus={missionStatus}");
                foreach (int sid in reps)
                    AwardSquadXp(sid, squadTotal, "mission-objectives", $"primary={primaryCompleted} secondary={secondaryCompleted}");
            }
            else if (_awardXp)
            {
                _log($"MISSION OBJECTIVE XP infantry skipped total={squadTotal} deployedSquads={reps.Count}");
            }

            if (_vehicleCrewVeterancy && vehicleTotal > 0 && _deployedPilots.Count > 0)
            {
                _log($"VEHICLE OBJECTIVE XP primary={primaryCompleted}x{_vehicleXpPerPrimaryObjective} secondary={secondaryCompleted}x{_vehicleXpPerSecondaryObjective} total=+{vehicleTotal} deployedPilots={_deployedPilots.Count}");
                foreach (var pilot in _deployedPilots.Values.ToList())
                    AwardPilotXp(pilot, vehicleTotal, "mission-objectives", $"primary={primaryCompleted} secondary={secondaryCompleted}");
            }
            else if (_vehicleCrewVeterancy)
            {
                _log($"VEHICLE OBJECTIVE XP skipped total={vehicleTotal} deployedPilots={_deployedPilots.Count}");
            }
        }
        catch (Exception e)
        {
            _log($"MISSION OBJECTIVE XP ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static void UnitActorOnMissionStartedPostfix(UnitActor __instance)
    {
        var c = Current;
        if (c == null || !c._enabled || (!c._vehicleCrewVeterancy && !c._vehicleCrewPressure) || !Valid(__instance)) return;
        try
        {
            if (c._vehicleCrewVeterancy)
                c.TrackPilotFromUnitActor(__instance, "mission-start");
            c.EnsurePlayerVehiclePressureState(__instance, "mission-start");
        }
        catch (Exception e) { c._log($"VEHICLE CREW mission-start error {e.GetType().Name}: {e.Message}"); }
    }

    private void RefreshPilotRoster(bool syncStore, bool force = false)
    {
        if (!_vehicleCrewVeterancy) return;
        long now = Environment.TickCount64;
        if (!force && now < _nextPilotRosterRefreshMs) return;
        _nextPilotRosterRefreshMs = now + 500;

        try
        {
            var state = StrategyState.Get();
            if (!Valid(state)) return;
            long statePtr = Key(state);
            if (_lastPilotStatePtr != 0 && statePtr != 0 && statePtr != _lastPilotStatePtr)
            {
                _pilotRoster.Clear();
                _lastLoggedPilotCount = -1;
                _log($"VEHICLE CREW campaign/state changed old=0x{_lastPilotStatePtr:X} new=0x{statePtr:X}; roster cache reset");
            }
            if (statePtr != 0) _lastPilotStatePtr = statePtr;

            var roster = state.Roster;
            if (!Valid(roster)) return;
            var leaders = GetHiredLeadersSafe(roster);
            if (leaders.Count == 0) return;

            var next = new Dictionary<string, PilotIdentitySnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var leader in leaders)
            {
                if (!Valid(leader)) continue;
                PilotIdentitySnapshot? pilot = BuildPilotSnapshot(leader);
                if (pilot == null || string.IsNullOrWhiteSpace(pilot.TemplateId)) continue;
                next[pilot.TemplateId] = pilot;
            }

            _pilotRoster.Clear();
            foreach (var pair in next) _pilotRoster[pair.Key] = pair.Value;
            if (syncStore) _xp.SyncPilots(_pilotRoster.Values);

            if (_lastLoggedPilotCount != _pilotRoster.Count)
            {
                _lastLoggedPilotCount = _pilotRoster.Count;
                _log($"VEHICLE CREW ROSTER READY pilots={_pilotRoster.Count} state=0x{statePtr:X}");
                foreach (var pilot in _pilotRoster.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    _log($"VEHICLE CREW pilot={Sanitize(pilot.Name)} template={Sanitize(pilot.TemplateId)} guid={pilot.TemplateGuid} vehicle={Sanitize(pilot.VehicleName)} itemGuid={Sanitize(pilot.VehicleGuid)} loadout=L{pilot.LightWeaponsMounted}/M{pilot.MediumWeaponsMounted}/H{pilot.HeavyWeaponsMounted} activeCrew={pilot.ActiveCrewRoles.Count} avgXp={_xp.GetPilotXp(pilot.TemplateId)} crew={FormatCrewMembers(pilot.TemplateId)} reserve={_xp.GetReservePilotCrewCount(pilot.TemplateId)} alive={(pilot.Alive ? 1 : 0)}");
            }
        }
        catch (Exception e)
        {
            _log($"VEHICLE CREW roster refresh error {e.GetType().Name}: {e.Message}");
        }
    }

    private List<BaseUnitLeader> GetHiredLeadersSafe(Il2CppMenace.Strategy.Roster roster)
    {
        var result = new List<BaseUnitLeader>();
        try
        {
            object? collection = TryReadManagedMember(roster, "m_HiredLeaders")
                ?? TryInvokeManagedNoArg(roster, "GetHiredLeaders");
            if (collection == null) return result;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = collection.GetType();

            int count = -1;
            try
            {
                object? countValue = TryReadManagedMember(collection, "Count");
                if (countValue != null) count = Convert.ToInt32(countValue);
            }
            catch { count = -1; }

            MethodInfo? getItem = null;
            PropertyInfo? itemProperty = null;
            try
            {
                itemProperty = type.GetProperty("Item", flags);
                getItem = type.GetMethod(
                    "get_Item",
                    flags,
                    binder: null,
                    types: new[] { typeof(int) },
                    modifiers: null);
            }
            catch { }

            if (count >= 0 && (itemProperty != null || getItem != null))
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        object? value = itemProperty != null
                            ? itemProperty.GetValue(collection, new object[] { i })
                            : getItem?.Invoke(collection, new object[] { i });
                        if (value is BaseUnitLeader leader) result.Add(leader);
                    }
                    catch { }
                }
                return result;
            }

            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (object? value in enumerable)
                    if (value is BaseUnitLeader leader) result.Add(leader);
            }
        }
        catch (Exception e)
        {
            _log($"VEHICLE CREW hired-roster reflection error {e.GetType().Name}: {e.Message}");
        }
        return result;
    }

    private PilotIdentitySnapshot? BuildPilotSnapshot(BaseUnitLeader leader)
    {
        try
        {
            if (!Valid(leader) || !leader.IsVehicle() || !leader.IsAlliedWithPlayer()) return null;
            var pilot = new Pilot(leader.Pointer);
            if (!Valid(pilot)) return null;

            string templateId = "";
            int templateGuid = 0;
            try
            {
                var leaderTemplate = pilot.LeaderTemplate;
                if (Valid(leaderTemplate))
                {
                    templateId = leaderTemplate.GetID() ?? "";
                    templateGuid = leaderTemplate.GetGuid();
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(templateId))
                templateId = templateGuid != 0 ? $"guid:{templateGuid}" : $"ptr:{Key(pilot):X}";

            string name = "";
            try
            {
                var speaker = pilot.GetSpeakerTemplate();
                if (Valid(speaker)) name = speaker.FullName ?? "";
            }
            catch { }
            if (string.IsNullOrWhiteSpace(name))
            {
                try { name = pilot.GetNickname()?.ToString() ?? ""; } catch { }
            }
            if (string.IsNullOrWhiteSpace(name)) name = templateId;

            string vehicleGuid = "";
            string vehicleName = "Unassigned";
            try
            {
                var vehicle = pilot.GetVehicle();
                if (Valid(vehicle))
                {
                    vehicleGuid = vehicle.GetItemGuid() ?? "";
                    var template = pilot.GetTemplate();
                    if (Valid(template))
                    {
                        try { vehicleName = template.Title?.ToString() ?? ""; } catch { }
                        if (string.IsNullOrWhiteSpace(vehicleName))
                            try { vehicleName = template.GetID() ?? ""; } catch { }
                    }
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(vehicleName)) vehicleName = "Unassigned";

            bool alive = true;
            try { alive = pilot.GetHealthStatus() != LeaderHealthStatus.PermanentlyDead; } catch { }

            int lightMounted = 0;
            int mediumMounted = 0;
            int heavyMounted = 0;
            var crewRoles = BuildActiveVehicleCrewRoles(
                pilot,
                !string.IsNullOrWhiteSpace(vehicleGuid),
                out lightMounted,
                out mediumMounted,
                out heavyMounted,
                out var legacyMountCrewRoles);

            return new PilotIdentitySnapshot
            {
                TemplateId = templateId,
                TemplateGuid = templateGuid,
                Name = name,
                VehicleGuid = vehicleGuid,
                VehicleName = vehicleName,
                Alive = alive,
                LeaderPtr = Key(pilot),
                ActiveCrewRoles = crewRoles,
                LegacyMountCrewRoles = legacyMountCrewRoles,
                LightWeaponsMounted = lightMounted,
                MediumWeaponsMounted = mediumMounted,
                HeavyWeaponsMounted = heavyMounted
            };
        }
        catch (Exception e)
        {
            _log($"VEHICLE CREW snapshot error leader=0x{Key(leader):X} {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    private IReadOnlyList<CrewRoleSpec> BuildActiveVehicleCrewRoles(
        Pilot pilot,
        bool hasVehicle,
        out int lightMounted,
        out int mediumMounted,
        out int heavyMounted,
        out IReadOnlyList<CrewRoleSpec> legacyMountCrewRoles)
    {
        lightMounted = 0;
        mediumMounted = 0;
        heavyMounted = 0;
        int legacyLightMounted = 0;
        int legacyMediumMounted = 0;
        int legacyHeavyMounted = 0;
        var roles = new List<CrewRoleSpec>();
        var legacyRoles = new List<CrewRoleSpec>();
        legacyMountCrewRoles = legacyRoles;
        if (!hasVehicle || !Valid(pilot)) return roles;

        // The native MENACE Pilot/leader is intentionally outside our XP model.
        // One abstract base crewman represents the driver/copilot working under
        // that leader. Weapon crew is derived only from occupied modular slots.
        roles.Add(new CrewRoleSpec { Key = "base:crewman", Label = "Driver / Crewman" });
        legacyRoles.Add(new CrewRoleSpec { Key = "base:crewman", Label = "Driver / Crewman" });

        try
        {
            var items = pilot.GetItems();
            if (!Valid(items)) return roles;
            var modular = items.GetModularVehicle();
            if (!Valid(modular)) return roles;
            var slots = modular.Slots;
            if (slots == null) return roles;

            // Current layout: classify crew demand by the mounted weapon's own class.
            // We still reconstruct the old physical-mount layout for diagnostics and
            // backwards compatibility, but 1.5.7 assigns persistent PEOPLE to roles,
            // so changing weapon class no longer discards or invents progression.
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!Valid(slot) || !Valid(slot.MountedWeapon)) continue;

                if (Valid(slot.Data))
                {
                    switch (slot.Data.SlotType)
                    {
                        case Il2CppMenace.Strategy.ModularVehicleSlotType.Light: legacyLightMounted++; break;
                        case Il2CppMenace.Strategy.ModularVehicleSlotType.Medium: legacyMediumMounted++; break;
                        case Il2CppMenace.Strategy.ModularVehicleSlotType.Heavy: legacyHeavyMounted++; break;
                    }
                }

                bool classified = false;
                try
                {
                    var weaponTemplate = slot.MountedWeapon.GetTemplate();
                    if (Valid(weaponTemplate))
                    {
                        switch (weaponTemplate.SlotType)
                        {
                            case Il2CppMenace.Items.ItemSlot.ModularVehicleLight:
                                lightMounted++;
                                classified = true;
                                break;
                            case Il2CppMenace.Items.ItemSlot.ModularVehicleMedium:
                                mediumMounted++;
                                classified = true;
                                break;
                            case Il2CppMenace.Items.ItemSlot.ModularVehicleHeavy:
                                heavyMounted++;
                                classified = true;
                                break;
                        }
                    }
                }
                catch { }

                // Safety fallback for an unexpected/custom weapon template that does
                // not expose a modular ItemSlot. This preserves compatibility with the
                // old mount-based behaviour rather than silently dropping its crew.
                if (!classified && Valid(slot.Data))
                {
                    switch (slot.Data.SlotType)
                    {
                        case Il2CppMenace.Strategy.ModularVehicleSlotType.Light: lightMounted++; break;
                        case Il2CppMenace.Strategy.ModularVehicleSlotType.Medium: mediumMounted++; break;
                        case Il2CppMenace.Strategy.ModularVehicleSlotType.Heavy: heavyMounted++; break;
                    }
                }
            }

            AddWeaponCrewRoles(roles, "light", "Light", lightMounted, loadersPerWeapon: 0);
            AddWeaponCrewRoles(roles, "medium", "Medium", mediumMounted, loadersPerWeapon: 1);
            AddWeaponCrewRoles(roles, "heavy", "Heavy", heavyMounted, loadersPerWeapon: 2);

            AddWeaponCrewRoles(legacyRoles, "light", "Light", legacyLightMounted, loadersPerWeapon: 0);
            AddWeaponCrewRoles(legacyRoles, "medium", "Medium", legacyMediumMounted, loadersPerWeapon: 1);
            AddWeaponCrewRoles(legacyRoles, "heavy", "Heavy", legacyHeavyMounted, loadersPerWeapon: 2);
            legacyMountCrewRoles = legacyRoles;
        }
        catch (Exception e)
        {
            _log($"VEHICLE CREW LOADOUT ERROR pilot=0x{Key(pilot):X} {e.GetType().Name}: {e.Message}; using base crew only");
        }
        return roles;
    }

    private static void AddWeaponCrewRoles(List<CrewRoleSpec> roles, string keyPrefix, string labelPrefix, int weaponCount, int loadersPerWeapon)
    {
        for (int weapon = 1; weapon <= weaponCount; weapon++)
        {
            string gunnerLabel = weaponCount > 1 ? $"{labelPrefix} Gunner {weapon}" : $"{labelPrefix} Gunner";
            roles.Add(new CrewRoleSpec
            {
                Key = $"{keyPrefix}:{weapon}:gunner",
                Label = gunnerLabel
            });
            for (int loader = 1; loader <= loadersPerWeapon; loader++)
            {
                string loaderLabel;
                if (weaponCount > 1 && loadersPerWeapon > 1) loaderLabel = $"{labelPrefix} Loader {weapon}.{loader}";
                else if (weaponCount > 1) loaderLabel = $"{labelPrefix} Loader {weapon}";
                else if (loadersPerWeapon > 1) loaderLabel = $"{labelPrefix} Loader {loader}";
                else loaderLabel = $"{labelPrefix} Loader";
                roles.Add(new CrewRoleSpec
                {
                    Key = $"{keyPrefix}:{weapon}:loader:{loader}",
                    Label = loaderLabel
                });
            }
        }
    }

    private bool TryResolvePlayerPilotForOwner(long ownerPtr, out PilotIdentitySnapshot pilot)
    {
        pilot = new PilotIdentitySnapshot();
        if (ownerPtr == 0) return false;
        try
        {
            string typeName = "";
            try { typeName = new GameObj(new IntPtr(ownerPtr)).GetTypeName() ?? ""; } catch { }
            if (!typeName.Contains("UnitActor", StringComparison.OrdinalIgnoreCase)) return false;

            var actor = new UnitActor(new IntPtr(ownerPtr));
            if (!Valid(actor)) return false;
            var leader = actor.GetLeader();
            var snapshot = BuildPilotSnapshot(leader);
            if (snapshot == null) return false;
            pilot = snapshot;
            return true;
        }
        catch { return false; }
    }

    private void TrackPilotFromUnitActor(UnitActor actor, string source)
    {
        if (!Valid(actor)) return;
        var leader = actor.GetLeader();
        var pilot = BuildPilotSnapshot(leader);
        if (pilot == null) return;
        if (string.Equals(source, "mission-start", StringComparison.OrdinalIgnoreCase) && !_loadRosterSyncPending)
            _xp.ActivateCrewReplacements(pilot);
        TrackDeployedPilot(pilot, source);
    }

    private void TrackDeployedPilot(PilotIdentitySnapshot pilot, string source)
    {
        if (string.IsNullOrWhiteSpace(pilot.TemplateId)) return;
        _pilotRoster[pilot.TemplateId] = pilot;
        // ExecLoad can briefly leave the previous campaign/tactical state alive.
        // Never add pilot records to the newly loaded sidecar until the same roster
        // guard that protects squaddie identities has released.
        if (!_loadRosterSyncPending) _xp.SyncPilots(new[] { pilot });
        if (_deployedPilots.TryAdd(pilot.TemplateId, pilot))
            _log($"VEHICLE CREW DEPLOYED pilot={Sanitize(pilot.Name)} template={Sanitize(pilot.TemplateId)} vehicle={Sanitize(pilot.VehicleName)} itemGuid={Sanitize(pilot.VehicleGuid)} loadout=L{pilot.LightWeaponsMounted}/M{pilot.MediumWeaponsMounted}/H{pilot.HeavyWeaponsMounted} activeCrew={pilot.ActiveCrewRoles.Count} avgXp={_xp.GetPilotXp(pilot.TemplateId)} crew={FormatCrewMembers(pilot.TemplateId)} reserve={_xp.GetReservePilotCrewCount(pilot.TemplateId)} source={source}");
        else
            _deployedPilots[pilot.TemplateId] = pilot;
    }

    private void AwardPilotXp(PilotIdentitySnapshot pilot, int amount, string source, string detail = "")
    {
        if (!_vehicleCrewVeterancy || amount <= 0 || string.IsNullOrWhiteSpace(pilot.TemplateId)) return;
        int before = _xp.GetPilotXp(pilot.TemplateId);
        int after = _xp.AddPilotXp(pilot, amount);
        _xpAwards++;
        string members = FormatCrewMembers(pilot.TemplateId);
        _log($"VEHICLE CREW XP AWARD pilot={Sanitize(pilot.Name)} template={Sanitize(pilot.TemplateId)} vehicle={Sanitize(pilot.VehicleName)} source={source} amount=+{amount} avgXp={before}->{after} rank={VeterancyRules.DisplayName(VeterancyRules.RankFromXp(after))} crew={members}{(string.IsNullOrWhiteSpace(detail) ? "" : " " + detail)}");
    }

    private void ResolveDestroyedVehicleCrew(PilotIdentitySnapshot pilot, long vehiclePtr)
    {
        int beforeAvg = _xp.GetPilotXp(pilot.TemplateId);
        var activeKeys = ActiveCrewKeys(pilot.TemplateId);
        foreach (string crewKey in activeKeys)
        {
            int roll = System.Random.Shared.Next(1, 101);
            if (roll <= _vehicleDestroyedCrewSurvivalPct)
            {
                var result = _xp.WoundCrewMember(pilot, crewKey, _vehicleCrewWoundedXpLossPct);
                _log($"VEHICLE CREW DESTROYED outcome=SURVIVED_WOUNDED pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} role={Sanitize(result.Role)} key={Sanitize(result.Key)} roll={roll} survivalChance={_vehicleDestroyedCrewSurvivalPct}% xp={result.XpBefore}->{result.XpAfter} lossPct={_vehicleCrewWoundedXpLossPct}%");
            }
            else
            {
                var result = _xp.KillAndReplaceCrewMember(pilot, crewKey);
                _log($"VEHICLE CREW DESTROYED outcome=KILLED_REPLACED pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} role={Sanitize(result.Role)} key={Sanitize(result.Key)} roll={roll} survivalChance={_vehicleDestroyedCrewSurvivalPct}% xp={result.XpBefore}->0");
            }
        }
        int afterAvg = _xp.GetPilotXp(pilot.TemplateId);
        _log($"VEHICLE CREW DESTRUCTION RESOLVED pilot={Sanitize(pilot.Name)} template={Sanitize(pilot.TemplateId)} activeBefore={activeKeys.Count} avgXp={beforeAvg}->{afterAvg} rank={VeterancyRules.DisplayName(VeterancyRules.RankFromXp(afterAvg))} crew={FormatCrewMembers(pilot.TemplateId)}");
    }

    private void ResolveMinorVehicleDefect(PilotIdentitySnapshot pilot, string severity, long vehiclePtr)
    {
        int roll = System.Random.Shared.Next(1, 101);
        if (roll > _vehicleMinorDefectInjuryPct)
        {
            _log($"VEHICLE CREW DEFECT severity={Sanitize(severity)} class=minor pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} injuryRoll={roll} injuryChance={_vehicleMinorDefectInjuryPct}% outcome=NO_CREW_INJURY");
            return;
        }

        var activeKeys = ActiveCrewKeys(pilot.TemplateId);
        if (activeKeys.Count == 0)
        {
            _log($"VEHICLE CREW DEFECT severity={Sanitize(severity)} class=minor pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} outcome=NO_ACTIVE_CREW");
            return;
        }
        string crewKey = activeKeys[System.Random.Shared.Next(0, activeKeys.Count)];
        var result = _xp.WoundCrewMember(pilot, crewKey, _vehicleCrewWoundedXpLossPct);
        _log($"VEHICLE CREW DEFECT severity={Sanitize(severity)} class=minor pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} injuryRoll={roll} injuryChance={_vehicleMinorDefectInjuryPct}% role={Sanitize(result.Role)} key={Sanitize(result.Key)} outcome=WOUNDED xp={result.XpBefore}->{result.XpAfter} lossPct={_vehicleCrewWoundedXpLossPct}%");
    }

    private void ResolveMajorVehicleDefect(PilotIdentitySnapshot pilot, string severity, long vehiclePtr)
    {
        var activeKeys = ActiveCrewKeys(pilot.TemplateId);
        if (activeKeys.Count == 0)
        {
            _log($"VEHICLE CREW DEFECT severity={Sanitize(severity)} class=major pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} outcome=NO_ACTIVE_CREW");
            return;
        }
        string crewKey = activeKeys[System.Random.Shared.Next(0, activeKeys.Count)];
        int deathRoll = System.Random.Shared.Next(1, 101);
        if (deathRoll <= _vehicleMajorDefectDeathPct)
        {
            var result = _xp.KillAndReplaceCrewMember(pilot, crewKey);
            _log($"VEHICLE CREW DEFECT severity={Sanitize(severity)} class=major pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} role={Sanitize(result.Role)} key={Sanitize(result.Key)} deathRoll={deathRoll} deathChance={_vehicleMajorDefectDeathPct}% outcome=KILLED_REPLACED xp={result.XpBefore}->0");
        }
        else
        {
            var result = _xp.WoundCrewMember(pilot, crewKey, _vehicleCrewWoundedXpLossPct);
            _log($"VEHICLE CREW DEFECT severity={Sanitize(severity)} class=major pilot={Sanitize(pilot.Name)} vehicle=0x{vehiclePtr:X} role={Sanitize(result.Role)} key={Sanitize(result.Key)} deathRoll={deathRoll} deathChance={_vehicleMajorDefectDeathPct}% outcome=SURVIVED_WOUNDED xp={result.XpBefore}->{result.XpAfter} lossPct={_vehicleCrewWoundedXpLossPct}%");
        }
    }

    private List<string> ActiveCrewKeys(string pilotTemplateId)
        => _xp.GetActivePilotCrewMembers(pilotTemplateId, includeReplacements: false)
            .Select(x => x.Key)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

    private string FormatCrewMembers(string pilotTemplateId)
    {
        var activeKeys = new HashSet<string>(_xp.GetActivePilotCrewMembers(pilotTemplateId).Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
        return string.Join(",", _xp.GetPilotCrewMembers(pilotTemplateId)
            .OrderBy(x => activeKeys.Contains(x.Key) ? 0 : 1)
            .ThenBy(x => x.Slot)
            .Select(x => $"{(activeKeys.Contains(x.Key) ? "A" : "R")}:{Sanitize(x.Role)}:{x.Xp}:{CrewStatusCode(x.Status)}"));
    }

    private static string CrewStatusCode(string status)
        => string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase) ? "H"
            : string.Equals(status, "Wounded", StringComparison.OrdinalIgnoreCase) ? "W" : "R";

    private void AwardVehicleMissionCompletionXp(int finishReason)
    {
        if (_missionVehicleXpAwarded) return;
        _missionVehicleXpAwarded = true;
        if (!_vehicleCrewVeterancy || _deployedPilots.Count == 0) return;

        try
        {
            int missionStatus = Menace.SDK.Mission.GetStatus();
            if (finishReason == 1 || missionStatus == Menace.SDK.Mission.STATUS_FAILED)
            {
                _log($"VEHICLE MISSION XP skipped finishReason={finishReason} missionStatus={missionStatus}");
                return;
            }

            foreach (var pilot in _deployedPilots.Values.ToList())
            {
                int amount = _vehicleXpPerMission;
                bool survived = PilotSurvivedMission(pilot);
                if (survived) amount += _vehicleXpPerMissionSurvived;
                if (amount > 0)
                    AwardPilotXp(pilot, amount, "mission-finish", $"participated={_vehicleXpPerMission} survived={(survived ? _vehicleXpPerMissionSurvived : 0)} finishReason={finishReason}");
            }
        }
        catch (Exception e)
        {
            _log($"VEHICLE MISSION XP ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private static bool PilotSurvivedMission(PilotIdentitySnapshot pilot)
    {
        if (!pilot.Alive) return false;
        if (pilot.LeaderPtr == 0) return pilot.Alive;
        try
        {
            var livePilot = new Pilot(new IntPtr(pilot.LeaderPtr));
            return Valid(livePilot) && livePilot.GetHealthStatus() != LeaderHealthStatus.PermanentlyDead;
        }
        catch { return pilot.Alive; }
    }

    private ObjectiveKind ClassifyObjective(long objectivePtr, out string source)
    {
        source = "unknown";
        if (objectivePtr == 0) return ObjectiveKind.Unknown;
        if (_objectiveKinds.TryGetValue(objectivePtr, out var cached) && cached != ObjectiveKind.Unknown)
        {
            source = "cache";
            return cached;
        }

        try
        {
            string runtimeType = "?";
            try { runtimeType = new GameObj(new IntPtr(objectivePtr)).GetTypeName() ?? "?"; } catch { }
            if (runtimeType.Contains("Secondary", StringComparison.OrdinalIgnoreCase))
            {
                source = $"type:{runtimeType}";
                return ObjectiveKind.Secondary;
            }
            if (runtimeType.Contains("Primary", StringComparison.OrdinalIgnoreCase))
            {
                source = $"type:{runtimeType}";
                return ObjectiveKind.Primary;
            }

            Type? type = typeof(TacticalManager).Assembly.GetType("Il2CppMenace.Tactical.Objectives.Objective");
            if (type == null)
            {
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => { try { return a.GetType("Il2CppMenace.Tactical.Objectives.Objective"); } catch { return null; } })
                    .FirstOrDefault(t => t != null);
            }
            if (type == null) return ObjectiveKind.Unknown;

            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: new[] { typeof(IntPtr) }, modifiers: null);
            object? proxy = ctor?.Invoke(new object[] { new IntPtr(objectivePtr) });
            if (proxy == null) return ObjectiveKind.Unknown;

            // Prefer explicit semantic bool members. This is intentionally broad so
            // the mod survives small IL2CPP naming changes without a game-version
            // dependency.
            var boolMembers = new List<(string name, bool value)>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length != 0 || prop.PropertyType != typeof(bool)) continue;
                try { boolMembers.Add((prop.Name, (bool)(prop.GetValue(proxy) ?? false))); } catch { }
            }
            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType != typeof(bool)) continue;
                try { boolMembers.Add((field.Name, (bool)(field.GetValue(proxy) ?? false))); } catch { }
            }
            foreach (var method in type.GetMethods(flags))
            {
                if (method.ReturnType != typeof(bool) || method.GetParameters().Length != 0) continue;
                string n = method.Name;
                if (!ContainsObjectiveRoleWord(n)) continue;
                try { boolMembers.Add((n, (bool)(method.Invoke(proxy, null) ?? false))); } catch { }
            }

            foreach (var member in boolMembers)
            {
                string n = member.name;
                bool v = member.value;
                if (n.Contains("Secondary", StringComparison.OrdinalIgnoreCase) || n.Contains("Optional", StringComparison.OrdinalIgnoreCase))
                {
                    source = $"bool:{n}={v}";
                    return v ? ObjectiveKind.Secondary : ObjectiveKind.Primary;
                }
                if (n.Contains("Primary", StringComparison.OrdinalIgnoreCase))
                {
                    source = $"bool:{n}={v}";
                    return v ? ObjectiveKind.Primary : ObjectiveKind.Secondary;
                }
                if (n.Contains("Required", StringComparison.OrdinalIgnoreCase) || n.Contains("Mandatory", StringComparison.OrdinalIgnoreCase))
                {
                    source = $"bool:{n}={v}";
                    return v ? ObjectiveKind.Primary : ObjectiveKind.Secondary;
                }
            }

            // Some builds encode the role as an enum/category rather than a bool.
            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length != 0 || !ContainsObjectiveCategoryWord(prop.Name)) continue;
                try
                {
                    string value = prop.GetValue(proxy)?.ToString() ?? "";
                    if (TryParseObjectiveKindText(value, out var kind))
                    {
                        source = $"property:{prop.Name}={value}";
                        return kind;
                    }
                }
                catch { }
            }
            foreach (var field in type.GetFields(flags))
            {
                if (!ContainsObjectiveCategoryWord(field.Name)) continue;
                try
                {
                    string value = field.GetValue(proxy)?.ToString() ?? "";
                    if (TryParseObjectiveKindText(value, out var kind))
                    {
                        source = $"field:{field.Name}={value}";
                        return kind;
                    }
                }
                catch { }
            }
            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetParameters().Length != 0 || method.ReturnType == typeof(void) || !ContainsObjectiveCategoryWord(method.Name)) continue;
                try
                {
                    string value = method.Invoke(proxy, null)?.ToString() ?? "";
                    if (TryParseObjectiveKindText(value, out var kind))
                    {
                        source = $"method:{method.Name}={value}";
                        return kind;
                    }
                }
                catch { }
            }

            string diagKey = type.FullName ?? type.Name;
            if (_objectiveKindDiagnostics.Add(diagKey))
            {
                string hints = string.Join(",", type.GetMembers(flags)
                    .Select(m => m.Name)
                    .Where(n => ContainsObjectiveRoleWord(n) || ContainsObjectiveCategoryWord(n))
                    .Distinct()
                    .Take(32));
                _log($"OBJECTIVE XP CLASSIFY unresolved type={runtimeType} proxyType={diagKey} hints={hints}");
            }
        }
        catch (Exception e)
        {
            if (_verbose) _log($"OBJECTIVE XP CLASSIFY ERROR ptr=0x{objectivePtr:X} {e.GetType().Name}: {e.Message}");
        }
        return ObjectiveKind.Unknown;
    }

    private static bool ContainsObjectiveRoleWord(string name)
        => name.Contains("Secondary", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Optional", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Primary", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Required", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Mandatory", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsObjectiveCategoryWord(string name)
        => name.Contains("Type", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Kind", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Category", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Role", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Priority", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseObjectiveKindText(string value, out ObjectiveKind kind)
    {
        if (value.Contains("Secondary", StringComparison.OrdinalIgnoreCase) || value.Contains("Optional", StringComparison.OrdinalIgnoreCase))
        {
            kind = ObjectiveKind.Secondary;
            return true;
        }
        if (value.Contains("Primary", StringComparison.OrdinalIgnoreCase) || value.Contains("Required", StringComparison.OrdinalIgnoreCase) || value.Contains("Mandatory", StringComparison.OrdinalIgnoreCase))
        {
            kind = ObjectiveKind.Primary;
            return true;
        }
        kind = ObjectiveKind.Unknown;
        return false;
    }

    private static string IncomingTargetHintKey(long attackerPtr, long targetTilePtr)
        => $"{attackerPtr:X}:{targetTilePtr:X}";

    private void RememberIncomingTargetHint(
        long attackerPtr,
        long targetTilePtr,
        long skillPtr,
        long targetOwnerPtr,
        int defenderSid,
        string defenderPilotTemplateId,
        bool pressureVehicle = false)
    {
        if (attackerPtr == 0 || targetTilePtr == 0 || targetOwnerPtr == 0) return;
        if (defenderSid <= 0 && string.IsNullOrWhiteSpace(defenderPilotTemplateId) && !pressureVehicle) return;
        string key = IncomingTargetHintKey(attackerPtr, targetTilePtr);
        _incomingTargetHints[key] = new IncomingTargetHint
        {
            DefenderSid = defenderSid,
            DefenderPilotTemplateId = defenderPilotTemplateId ?? "",
            PressureVehicle = pressureVehicle,
            TargetOwnerPtr = targetOwnerPtr,
            SkillPtr = skillPtr,
            AttackerPtr = attackerPtr,
            TargetTilePtr = targetTilePtr,
            SeenAtMs = Environment.TickCount64
        };
    }

    private bool TryPeekIncomingTargetHint(long attackerPtr, long targetTilePtr, long skillPtr, out IncomingTargetHint hint)
    {
        hint = null!;
        if (attackerPtr == 0 || targetTilePtr == 0) return false;
        string key = IncomingTargetHintKey(attackerPtr, targetTilePtr);
        if (!_incomingTargetHints.TryGetValue(key, out var candidate)) return false;
        long age = Environment.TickCount64 - candidate.SeenAtMs;
        if (age < 0 || age > 5000) return false;
        if (candidate.SkillPtr != 0 && skillPtr != 0 && candidate.SkillPtr != skillPtr) return false;
        hint = candidate;
        return true;
    }

    private bool TryResolveIncomingTargetHint(
        long attackerPtr,
        long targetTilePtr,
        long skillPtr,
        out IncomingTargetHint hint)
    {
        hint = null!;
        if (attackerPtr == 0 || targetTilePtr == 0) return false;
        string key = IncomingTargetHintKey(attackerPtr, targetTilePtr);
        if (!_incomingTargetHints.TryGetValue(key, out var candidate)) return false;

        // Target selection and commit are close together. Keep the window generous
        // enough for AI animation/dispatch, but short enough that an old target
        // evaluation cannot be reused by a later attack from the same tile.
        long age = Environment.TickCount64 - candidate.SeenAtMs;
        if (age < 0 || age > 5000)
        {
            _incomingTargetHints.Remove(key);
            return false;
        }

        if (candidate.SkillPtr != 0 && skillPtr != 0 && candidate.SkillPtr != skillPtr)
            return false;

        bool validSquad = candidate.DefenderSid > 0 && _identities.Contains(candidate.DefenderSid);
        bool validVehicle = !string.IsNullOrWhiteSpace(candidate.DefenderPilotTemplateId) || candidate.PressureVehicle;
        if (!validSquad && !validVehicle) return false;

        _incomingTargetHints.Remove(key);
        hint = candidate;
        return true;
    }

    private bool RememberXpEvent(string key, int windowMs)
    {
        long now = Environment.TickCount64;
        if (_xpDedup.TryGetValue(key, out long last) && now - last < windowMs) return false;
        _xpDedup[key] = now;
        return true;
    }

    private static bool IsPostMissionRosterScene(string sceneName)
    {
        // MENACE can return from Tactical either directly to Strategy or to
        // MissionPreparation after a completed mission. Both scenes expose the
        // post-mission StrategyState roster used to decide which casualties were
        // rescued and which were permanently lost.
        return sceneName.Equals("Strategy", StringComparison.OrdinalIgnoreCase)
            || sceneName.Equals("MissionPreparation", StringComparison.OrdinalIgnoreCase);
    }

    private void ResolveRescuedSquaddieXpLoss()
    {
        if (!_rescueResolutionPending || _downedSquaddiesThisMission.Count == 0) return;
        if (!IsPostMissionRosterScene(_sceneName)) return;

        // SceneChanged() can run before StrategyState is ready. Never resolve against
        // the stale tactical-era cache: wait for at least one successful fresh read.
        if (_identities.RefreshGeneration <= _rescueResolutionBaselineGeneration) return;

        long now = Environment.TickCount64;
        foreach (int sid in _downedSquaddiesThisMission.ToList())
        {
            if (!_identities.TryGet(sid, out var identity) || !identity.Alive) continue;

            int before = _xp.GetXp(sid);
            int after = before;
            int lost = 0;
            var beforeRank = VeterancyRules.RankFromXp(before);

            if (_rescueXpLoss && _rescueXpLossPct > 0)
                after = _xp.LoseXpPercentage(identity, _rescueXpLossPct, out lost);

            var afterRank = VeterancyRules.RankFromXp(after);
            _log($"SQUADDIE RESCUED sid={sid} name={Sanitize(identity.Name)} xp={before}->{after} lost={lost} lossPct={_rescueXpLossPct}% enabled={(_rescueXpLoss ? 1 : 0)} rank={VeterancyRules.DisplayName(beforeRank)}->{VeterancyRules.DisplayName(afterRank)}");
            _downedSquaddiesThisMission.Remove(sid);
        }

        if (_downedSquaddiesThisMission.Count == 0)
        {
            _rescueResolutionPending = false;
            return;
        }

        // Non-recovered casualties remain absent/dead. Keep checking briefly because
        // Strategy scene activation can precede MENACE's casualty recovery pass.
        if (now >= _rescueResolutionDeadlineMs)
        {
            foreach (int sid in _downedSquaddiesThisMission.OrderBy(x => x))
                _log($"SQUADDIE CASUALTY NOT RESCUED sid={sid} xp={_xp.GetXp(sid)}");
            _downedSquaddiesThisMission.Clear();
            _rescueResolutionPending = false;
        }
    }

    private void ResetRescueTracking(string reason)
    {
        if ((_downedSquaddiesThisMission.Count > 0 || _rescueResolutionPending) && _verbose)
            _log($"RESCUE XP tracking reset reason={reason} casualties={_downedSquaddiesThisMission.Count}");
        _downedSquaddiesThisMission.Clear();
        _rescueResolutionPending = false;
        _rescueResolutionBaselineGeneration = _identities.RefreshGeneration;
        _rescueResolutionDeadlineMs = 0;
    }

    private void TrackDeployedSquad(int squaddieId)
    {
        if (squaddieId <= 0 || !_identities.Contains(squaddieId)) return;
        if (!_identities.TryGetReliableSquad(squaddieId, out int squad, out var members, out bool recovered))
        {
            LogSquadMappingSkipOnce(squaddieId);
            return;
        }
        if (recovered) LogSquadMappingRecoveryOnce(squaddieId, squad, members.Count);
        if (!_deployedSquadRepresentatives.ContainsKey(squad))
            _deployedSquadRepresentatives[squad] = squaddieId;
    }

    private int AwardSquadXp(int representativeSid, int amount, string source, string detail = "")
    {
        if (!_enabled || !_awardXp || amount <= 0 || representativeSid <= 0) return 0;
        if (!_identities.Contains(representativeSid)) return 0;

        if (!_identities.TryGetReliableSquad(representativeSid, out int squadNumber, out var reliableMembers, out bool recovered))
        {
            LogSquadMappingSkipOnce(representativeSid);
            _log($"SQUAD XP SKIP sid={representativeSid} source={source} amount=+{amount} reason=unresolved-squad-mapping");
            return 0;
        }
        if (recovered) LogSquadMappingRecoveryOnce(representativeSid, squadNumber, reliableMembers.Count);

        var recipients = new List<SquaddieIdentitySnapshot>();
        foreach (int sid in reliableMembers.Distinct())
        {
            if (sid <= 0) continue;
            if (_identities.TryGet(sid, out var identity) && identity.Alive)
                recipients.Add(identity);
        }
        if (recipients.Count == 0) return 0;

        double squadAvgBefore = recipients.Average(x => (double)_xp.GetXp(x.Id));
        var squadRankBefore = VeterancyRules.RankFromXp(squadAvgBefore);
        foreach (var identity in recipients)
            _xp.AddXp(identity, amount);
        double squadAvgAfter = recipients.Average(x => (double)_xp.GetXp(x.Id));
        var squadRankAfter = VeterancyRules.RankFromXp(squadAvgAfter);
        _xpAwards++;

        string suffix = string.IsNullOrWhiteSpace(detail) ? "" : " " + detail;
        _log($"SQUAD XP AWARD squad={squadNumber} sid={representativeSid} source={source} amount=+{amount} recipients={recipients.Count} avg={squadAvgBefore:0.00}->{squadAvgAfter:0.00} rank={VeterancyRules.DisplayName(squadRankAfter)}{suffix}");

        if (squadRankBefore != squadRankAfter)
        {
            _log($"SQUAD RANK UP squad={squadNumber} {VeterancyRules.DisplayName(squadRankBefore)}->{VeterancyRules.DisplayName(squadRankAfter)} avgXp={squadAvgAfter:0.00} hpBonus=+{HpBonusForRank(squadRankAfter)} accuracy={AccuracyPctForRank(squadRankAfter):+#;-#;0}% defense={DefensePctForRank(squadRankAfter):+#;-#;0}% suppressionResist={SuppressionResistancePctForRank(squadRankAfter):+#;-#;0}% concealment={ConcealmentForRank(squadRankAfter):+#;-#;0}");
            ReapplyHpBonusesForSquad(representativeSid);
        }
        return recipients.Count;
    }

    private bool IsRealAttack(Skill skill)
    {
        string id = SkillId(skill);
        string lower = id.ToLowerInvariant();
        if (lower.Contains("salvo") || lower.Contains("background_") || lower.Contains("fake_attack") || lower.Contains("fakeattack"))
            return false;

        try
        {
            if (skill.IsAttack()) return true;
        }
        catch { }

        // Conservative fallback for the normal squad-fire naming used by current MENACE builds.
        return lower.StartsWith("active.fire_", StringComparison.Ordinal);
    }

    private bool IsXpAttack(Skill skill)
    {
        string id = SkillId(skill);
        string lower = id.ToLowerInvariant();

        // Keep synthetic/background attacks out of XP, but unlike reliability do
        // not reject real salvo skills. Some MENACE weapons commit their actual
        // attack as an active.*salvo skill. XP is deduplicated per actor+target
        // action, so allowing these cannot multiply rewards for child projectiles.
        if (lower.Contains("background_") || lower.Contains("fake_attack") || lower.Contains("fakeattack"))
            return false;

        try
        {
            if (skill.IsAttack()) return true;
        }
        catch { }

        return lower.StartsWith("active.fire_", StringComparison.Ordinal)
            || lower.StartsWith("active.launch_", StringComparison.Ordinal)
            || lower.Contains("salvo");
    }

    private double GetSquadAverageXp(int squaddieId)
    {
        var members = _identities.GetSquadMembers(squaddieId);
        if (members.Count == 0) return _xp.GetXp(squaddieId);
        double total = 0;
        int count = 0;
        foreach (int id in members)
        {
            if (id <= 0) continue;
            total += _xp.GetXp(id);
            count++;
        }
        return count == 0 ? 0 : total / count;
    }

    private bool TryGetReliableSquadAverageXp(int squaddieId, out double averageXp, out int squadNumber, out bool recovered)
    {
        averageXp = 0;
        squadNumber = 0;
        recovered = false;
        if (!_identities.Contains(squaddieId)) return false;
        if (!_identities.TryGetReliableSquad(squaddieId, out squadNumber, out var members, out recovered))
        {
            LogSquadMappingSkipOnce(squaddieId);
            return false;
        }

        double total = 0;
        int count = 0;
        foreach (int id in members)
        {
            if (id <= 0) continue;
            total += _xp.GetXp(id);
            count++;
        }
        if (count == 0)
        {
            LogSquadMappingSkipOnce(squaddieId);
            return false;
        }

        averageXp = total / count;
        if (recovered) LogSquadMappingRecoveryOnce(squaddieId, squadNumber, members.Count);
        return true;
    }

    private int ResolvePlayerSquaddieIdForOwner(long ownerPtr)
    {
        if (ownerPtr == 0) return 0;

        foreach (var state in _liveElements.Values)
        {
            if (!state.PlayerSquaddie || state.SquaddieId <= 0) continue;
            if (state.ParentEntityPtr == 0 && state.Element != null && Valid(state.Element))
                state.ParentEntityPtr = ResolveElementParentEntityPtr(state.Element);
            if (state.ParentEntityPtr == ownerPtr && _identities.Contains(state.SquaddieId))
                return state.SquaddieId;
        }

        // Fallback for calls that happen before the CreateElement postfix has populated
        // the live map. Reuse the already-hardened owner/element resolver, but accept
        // only persistent player squaddie IDs.
        try
        {
            if (TryGetReliabilityOwnerElements(ownerPtr, out var items))
            {
                var player = items.FirstOrDefault(x => !IsTemporaryNpc(x) && x.Sid > 0 && _identities.Contains(x.Sid));
                if (player != null) return player.Sid;
            }
        }
        catch { }

        return 0;
    }

    private int GetTeamHpBonus(int squaddieId)
    {
        if (!_enabled || !_teamHpBuffs || !_identities.Contains(squaddieId)) return 0;
        if (!TryGetReliableSquadAverageXp(squaddieId, out double avg, out _, out _)) return 0;
        return HpBonusForRank(VeterancyRules.RankFromXp(avg));
    }

    private int GetTeamAccuracyPct(int squaddieId)
    {
        if (!_enabled || !_teamAccuracyMods || !_identities.Contains(squaddieId)) return 0;
        if (!TryGetReliableSquadAverageXp(squaddieId, out double avg, out _, out _)) return 0;
        return AccuracyPctForRank(VeterancyRules.RankFromXp(avg));
    }

    private int GetTeamDefensePct(int squaddieId)
    {
        if (!_enabled || !_teamDefenseMods || !_identities.Contains(squaddieId)) return 0;
        if (!TryGetReliableSquadAverageXp(squaddieId, out double avg, out _, out _)) return 0;
        return DefensePctForRank(VeterancyRules.RankFromXp(avg));
    }

    private bool TryGetVeterancyPropertiesOwner(EntityProperties properties, out VeterancyPropertiesOwnerState owner)
    {
        owner = null!;
        long key = Key(properties);
        if (key == 0 || !_veterancyPropertiesOwners.TryGetValue(key, out var found)) return false;
        bool knownSquaddie = found.SquaddieId > 0 && _identities.Contains(found.SquaddieId);
        bool validSquad = knownSquaddie && _identities.TryGetReliableSquad(found.SquaddieId, out _, out _, out _);
        bool validCrew = !string.IsNullOrWhiteSpace(found.PilotTemplateId);
        bool validNpc = found.IsTemporaryNpc
            && ((found.NpcVehicle && (_vehicleCrewPressure || _enemyReliability))
                || (found.NpcEnemy ? _enemyReliability : _alliedNpcReliability));
        if (!validSquad && !validCrew && !validNpc)
        {
            // A known player squaddie can temporarily lose the live roster mapping
            // during Tactical transitions. Keep the owner association so it can
            // recover later, but skip this modifier call now.
            if (knownSquaddie)
            {
                LogSquadMappingSkipOnce(found.SquaddieId);
                return false;
            }
            _veterancyPropertiesOwners.Remove(key);
            return false;
        }
        found.LastSeenAtMs = Environment.TickCount64;
        owner = found;
        return true;
    }

    private void EnsurePlayerVehiclePressureState(UnitActor actor, string source)
    {
        if (!_vehicleCrewPressure || !Valid(actor)) return;
        long ownerPtr = Key(actor);
        if (ownerPtr == 0) return;
        if (!TryResolvePlayerPilotForOwner(ownerPtr, out var pilot) || string.IsNullOrWhiteSpace(pilot.TemplateId)) return;
        EnsureVehiclePressureState(actor, ownerPtr, GetVehicleCrewRank(pilot.TemplateId),
            $"pilot={Sanitize(pilot.TemplateId)}", source);
    }

    private void EnsureNpcVehiclePressureState(long ownerPtr, VeterancyRank rank, string source)
    {
        if (!_vehicleCrewPressure || ownerPtr == 0) return;
        try
        {
            var actor = new Actor(new IntPtr(ownerPtr));
            if (!Valid(actor)) return;
            EnsureVehiclePressureState(actor, ownerPtr, rank,
                $"npc=1 template={TemplateLabel(ResolveOwnerTemplateId(ownerPtr))}", source);
        }
        catch (Exception e)
        {
            if (_verbose) _log($"NPC VEHICLE PRESSURE state error {e.GetType().Name}: {e.Message}");
        }
    }

    private VehiclePressureState EnsureVehiclePressureState(Actor actor, long ownerPtr, VeterancyRank rank, string identity, string source)
    {
        if (_vehiclePressureStates.TryGetValue(ownerPtr, out var existing))
        {
            existing.Actor = actor;
            existing.Rank = rank;
            if (!string.IsNullOrWhiteSpace(identity)) existing.Identity = identity;
            return existing;
        }

        var state = new VehiclePressureState
        {
            OwnerPtr = ownerPtr,
            Actor = actor,
            Rank = rank,
            Identity = identity ?? "",
            Pressure = 0f
        };
        _vehiclePressureStates[ownerPtr] = state;
        _log($"VEHICLE PRESSURE READY owner=0x{ownerPtr:X} {identity} rank={VeterancyRules.DisplayName(rank)} source={source} buttoned={_vehicleButtonedThreshold} break={_vehicleBreakContactThreshold} received={_vehiclePressureReceivedPct}%");
        return state;
    }

    private bool TryResolveVehiclePressureTarget(long ownerPtr, out Actor actor, out VehiclePressureState state)
    {
        actor = null!;
        state = null!;
        if (!_vehicleCrewPressure || ownerPtr == 0) return false;

        try
        {
            if (_vehiclePressureStates.TryGetValue(ownerPtr, out var existing))
            {
                actor = existing.Actor ?? new Actor(new IntPtr(ownerPtr));
                if (!Valid(actor)) return false;
                existing.Actor = actor;
                if (TryResolvePlayerPilotForOwner(ownerPtr, out var existingPilot) && !string.IsNullOrWhiteSpace(existingPilot.TemplateId))
                    existing.Rank = GetVehicleCrewRank(existingPilot.TemplateId);
                state = existing;
                return true;
            }

            actor = new Actor(new IntPtr(ownerPtr));
            if (!Valid(actor)) return false;

            if (TryResolvePlayerPilotForOwner(ownerPtr, out var pilot) && !string.IsNullOrWhiteSpace(pilot.TemplateId))
            {
                state = EnsureVehiclePressureState(actor, ownerPtr, GetVehicleCrewRank(pilot.TemplateId),
                    $"pilot={Sanitize(pilot.TemplateId)}", "pressure-target");
                return true;
            }

            int factionId = GetOwnerFactionId(ownerPtr);
            string templateId = ResolveOwnerTemplateId(ownerPtr);
            if (!NpcVeterancyProfiles.IsHostileVehicleTemplate(factionId, templateId)) return false;

            var rank = GetOrAssignNpcVehicleRank(ownerPtr, factionId, templateId, out _);
            state = EnsureVehiclePressureState(actor, ownerPtr, rank,
                $"npc=1 template={TemplateLabel(templateId)}", "pressure-target");
            return true;
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VEHICLE PRESSURE target resolve error owner=0x{ownerPtr:X} {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    private void ApplyVehiclePressureFromCommittedAttack(long attackerPtr, Skill skill, long targetTilePtr)
    {
        if (!_vehicleCrewPressure || attackerPtr == 0 || targetTilePtr == 0 || !Valid(skill)) return;

        try
        {
            long targetOwnerPtr = 0;
            try
            {
                var tile = new Tile(new IntPtr(targetTilePtr));
                var entity = tile.GetEntity();
                if (!Valid(entity))
                {
                    try { entity = tile.GetActor(); } catch { }
                }
                if (Valid(entity))
                {
                    long candidate = Key(entity);
                    if (TryResolveVehiclePressureTarget(candidate, out _, out _)) targetOwnerPtr = candidate;
                }
            }
            catch { }

            if (targetOwnerPtr == 0 && TryPeekIncomingTargetHint(attackerPtr, targetTilePtr, Key(skill), out var hint) && hint.PressureVehicle)
                targetOwnerPtr = hint.TargetOwnerPtr;

            if (targetOwnerPtr == 0 || targetOwnerPtr == attackerPtr) return;
            if (!TryResolveVehiclePressureTarget(targetOwnerPtr, out var targetActor, out var state)) return;

            string dedup = $"vehicle-pressure-action:{attackerPtr:X}:{targetTilePtr:X}:{targetOwnerPtr:X}";
            if (!RememberXpEvent(dedup, 1200)) return;

            float sourcePressure = GetSkillPressurePotential(skill, out float perShot, out int repetitions, out string pressureSource);
            if (sourcePressure <= 0.001f) return;

            int resistPct = _vehicleCrewPressureRankMods ? VehiclePressureResistancePctForRank(state.Rank) : 0;
            float receivedFactor = Math.Max(0.10f, Math.Min(1.00f, _vehiclePressureReceivedPct / 100f));
            float rankFactor = SuppressionResistanceMultiplier(resistPct);
            float added = Math.Max(0f, sourcePressure * receivedFactor * rankFactor);
            if (added <= 0.001f) return;

            float before = state.Pressure;
            state.Pressure = Math.Min(_vehicleBreakContactThreshold, before + added);
            state.Actor = targetActor;

            try
            {
                var attacker = new Actor(new IntPtr(attackerPtr));
                if (Valid(attacker))
                {
                    state.LastThreatPos = attacker.GetPos();
                    state.HasThreat = true;
                }
            }
            catch { }

            bool crossedButtoned = before < _vehicleButtonedThreshold && state.Pressure >= _vehicleButtonedThreshold;
            bool crossedBreak = before < _vehicleBreakContactThreshold && state.Pressure >= _vehicleBreakContactThreshold;
            if (state.Pressure >= _vehicleBreakContactThreshold) state.BreakContactPending = true;

            _log($"VEHICLE PRESSURE APPLY target=0x{targetOwnerPtr:X} attacker=0x{attackerPtr:X} skill={SkillId(skill)} source={pressureSource} base={perShot:0.##} reps={repetitions} raw={sourcePressure:0.##} receive={_vehiclePressureReceivedPct}% rank={VeterancyRules.DisplayName(state.Rank)} rankMod={resistPct:+#;-#;0}% add=+{added:0.##} pressure={before:0.##}->{state.Pressure:0.##} state={VehiclePressureLabel(state.Pressure)} nativeMorale={SafeMoraleLabel(targetActor)}");

            if (crossedButtoned)
                _log($"VEHICLE BUTTONED UP owner=0x{targetOwnerPtr:X} pressure={state.Pressure:0.##} APpenalty={_vehicleButtonedApPenalty} accuracyPenalty={_vehicleButtonedAccuracyPenaltyPct}%");
            if (crossedBreak)
                _log($"VEHICLE BREAK CONTACT ARMED owner=0x{targetOwnerPtr:X} pressure={state.Pressure:0.##} threat=({state.LastThreatPos.x:0.0},{state.LastThreatPos.z:0.0})");

            if (_vehiclePressureActiveActorPtr == targetOwnerPtr
                && state.Pressure >= _vehicleButtonedThreshold
                && !state.RetreatInProgress)
                ApplyButtonedApPenalty(targetActor, state);
        }
        catch (Exception e)
        {
            _log($"VEHICLE PRESSURE APPLY ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private void ApplyVehiclePressureShock(long targetOwnerPtr, float baseAmount, string source, string detail, bool hasThreat, Vector3 threatPos)
    {
        if (!_vehicleCrewPressure || targetOwnerPtr == 0 || baseAmount <= 0.001f) return;
        if (!TryResolveVehiclePressureTarget(targetOwnerPtr, out var targetActor, out var state)) return;

        int resistPct = _vehicleCrewPressureRankMods ? VehiclePressureResistancePctForRank(state.Rank) : 0;
        float rankFactor = SuppressionResistanceMultiplier(resistPct);
        float added = Math.Max(0f, baseAmount * rankFactor);
        if (added <= 0.001f) return;

        float before = state.Pressure;
        state.Pressure = Math.Min(_vehicleBreakContactThreshold, before + added);
        state.Actor = targetActor;
        if (hasThreat)
        {
            state.LastThreatPos = threatPos;
            state.HasThreat = true;
        }

        bool crossedButtoned = before < _vehicleButtonedThreshold && state.Pressure >= _vehicleButtonedThreshold;
        bool crossedBreak = before < _vehicleBreakContactThreshold && state.Pressure >= _vehicleBreakContactThreshold;
        if (state.Pressure >= _vehicleBreakContactThreshold) state.BreakContactPending = true;

        _log($"VEHICLE PRESSURE SHOCK target=0x{targetOwnerPtr:X} source={source} base=+{baseAmount:0.##} rank={VeterancyRules.DisplayName(state.Rank)} rankMod={resistPct:+#;-#;0}% add=+{added:0.##} pressure={before:0.##}->{state.Pressure:0.##} state={VehiclePressureLabel(state.Pressure)} nativeMorale={SafeMoraleLabel(targetActor)} {detail}");

        if (crossedButtoned)
            _log($"VEHICLE BUTTONED UP owner=0x{targetOwnerPtr:X} pressure={state.Pressure:0.##} APpenalty={_vehicleButtonedApPenalty} accuracyPenalty={_vehicleButtonedAccuracyPenaltyPct}% source={source}");
        if (crossedBreak)
            _log($"VEHICLE BREAK CONTACT ARMED owner=0x{targetOwnerPtr:X} pressure={state.Pressure:0.##} source={source} threat={(state.HasThreat ? $"({state.LastThreatPos.x:0.0},{state.LastThreatPos.z:0.0})" : "unknown")}");

        if (_vehiclePressureActiveActorPtr == targetOwnerPtr
            && state.Pressure >= _vehicleButtonedThreshold
            && !state.RetreatInProgress)
            ApplyButtonedApPenalty(targetActor, state);
    }

    private float GetSkillPressurePotential(Skill skill, out float perShot, out int repetitions, out string source)
    {
        perShot = 0f;
        repetitions = 1;
        source = "none";
        if (!Valid(skill)) return 0f;

        try
        {
            var template = skill.GetTemplate();
            if (Valid(template)) repetitions = Math.Max(1, (int)template.Repetitions);
        }
        catch { }

        try
        {
            var item = skill.GetItem();
            if (Valid(item))
            {
                var itemTemplate = item.GetTemplate();
                var weapon = itemTemplate?.TryCast<WeaponTemplate>();
                if (Valid(weapon) && weapon.Suppression > 0.001f)
                {
                    perShot = weapon.Suppression;
                    source = "weapon";
                }
            }
        }
        catch { }

        if (perShot <= 0.001f)
        {
            try
            {
                var handlers = skill.GetSkillEventHandlers();
                int count = handlers?.Length ?? 0;
                for (int i = 0; i < count; i++)
                {
                    var handler = handlers![i];
                    if (!Valid(handler)) continue;
                    var attack = handler.TryCast<AttackHandler>();
                    if (!Valid(attack)) continue;
                    var attackTemplate = attack.GetTemplate();
                    if (!Valid(attackTemplate) || attackTemplate.Suppression <= 0.001f) continue;
                    float mult = attackTemplate.SuppressionDealtMult;
                    if (mult <= 0.001f) mult = 1f;
                    float candidate = attackTemplate.Suppression * mult;
                    if (candidate > perShot)
                    {
                        perShot = candidate;
                        source = "attack-handler";
                    }
                }
            }
            catch { }
        }

        return perShot * repetitions;
    }

    private static bool IsVehicleRallySkill(Skill skill)
    {
        if (!Valid(skill)) return false;
        return string.Equals(SkillId(skill), "perk.command_rally", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyVehicleRallyFromCommittedSkill(long userPtr, Skill skill, long targetTilePtr)
    {
        if (!_vehicleCrewPressure || _vehicleRallyPressureReduction <= 0 || targetTilePtr == 0 || !Valid(skill)) return;

        try
        {
            long targetOwnerPtr = 0;
            try
            {
                var tile = new Tile(new IntPtr(targetTilePtr));
                var entity = tile.GetEntity();
                if (!Valid(entity))
                {
                    try { entity = tile.GetActor(); } catch { }
                }
                if (Valid(entity))
                {
                    long candidate = Key(entity);
                    if (TryResolveVehiclePressureTarget(candidate, out _, out _)) targetOwnerPtr = candidate;
                }
            }
            catch { }

            if (targetOwnerPtr == 0 && TryPeekIncomingTargetHint(userPtr, targetTilePtr, Key(skill), out var hint) && hint.PressureVehicle)
                targetOwnerPtr = hint.TargetOwnerPtr;

            if (targetOwnerPtr == 0 || !TryResolveVehiclePressureTarget(targetOwnerPtr, out var targetActor, out var state))
            {
                if (_verbose) _log($"VEHICLE RALLY target unresolved user=0x{userPtr:X} tile=0x{targetTilePtr:X}");
                return;
            }

            float before = state.Pressure;
            if (before <= 0.001f) return;

            bool wasPending = state.BreakContactPending;
            bool retreating = state.RetreatInProgress;
            state.Pressure = Math.Max(0f, before - _vehicleRallyPressureReduction);
            bool cancelledBreak = false;
            if (!retreating && state.Pressure < _vehicleBreakContactThreshold && state.BreakContactPending)
            {
                state.BreakContactPending = false;
                cancelledBreak = true;
            }

            _log($"VEHICLE RALLY target=0x{targetOwnerPtr:X} reduction=-{_vehicleRallyPressureReduction} pressure={before:0.##}->{state.Pressure:0.##} state={VehiclePressureLabel(state.Pressure)} nativeMoraleAtCommit={SafeMoraleLabel(targetActor)} breakPending={(wasPending ? 1 : 0)} breakCancelled={(cancelledBreak ? 1 : 0)} retreatInProgress={(retreating ? 1 : 0)}");
        }
        catch (Exception e)
        {
            _log($"VEHICLE RALLY ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private bool IsWalkerVehicle(long ownerPtr)
    {
        if (ownerPtr == 0) return false;
        string templateId = ResolveOwnerTemplateId(ownerPtr);
        return !string.IsNullOrWhiteSpace(templateId)
            && templateId.IndexOf("walker", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SafeMoraleLabel(Actor? actor)
    {
        if (actor == null || !Valid(actor)) return "?";
        try { return actor.GetMoraleState().ToString(); }
        catch { return "?"; }
    }

    private static void ActorGetMoraleStatePostfix(Actor __instance, ref MoraleState __result)
    {
        var c = Current;
        if (c == null || !c._enabled || !c._vehicleCrewPressure || !Valid(__instance)) return;

        try
        {
            long ownerPtr = Key(__instance);
            if (ownerPtr == 0 || !c._vehiclePressureStates.TryGetValue(ownerPtr, out var state)) return;

            // Crew Pressure is the authoritative combat-stress system for tracked
            // vehicles. MENACE's native morale can independently fall to Wavering or
            // Fleeing after damage; exposing that state makes the generic AI/player
            // control path panic-move or lock a vehicle even though our intended states
            // are Normal -> Buttoned Up (-AP/-accuracy only) -> Break Contact (our own
            // controlled retreat). Keep native morale internal and report Neutral to
            // gameplay while this vehicle is managed by Crew Pressure.
            if (__result != MoraleState.Neutral)
            {
                if (c._verbose)
                    c._log($"VEHICLE PRESSURE MORALE OVERRIDE owner=0x{ownerPtr:X} pressure={state.Pressure:0.##} state={c.VehiclePressureLabel(state.Pressure)} native={__result}->Neutral");
                __result = MoraleState.Neutral;
            }
        }
        catch (Exception e)
        {
            if (c._verbose) c._log($"VEHICLE PRESSURE MORALE OVERRIDE ERROR {e.GetType().Name}: {e.Message}");
        }
    }

    private string VehiclePressureLabel(float pressure)
    {
        if (pressure >= _vehicleBreakContactThreshold) return "Break Contact";
        if (pressure >= _vehicleButtonedThreshold) return "Buttoned Up";
        return "Normal";
    }

    private int VehiclePressureResistancePctForRank(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => _vehicleGreenPressureResistancePct,
        VeterancyRank.CombatTested => _vehicleCombatTestedPressureResistancePct,
        VeterancyRank.Hardened => _vehicleHardenedPressureResistancePct,
        VeterancyRank.Regular => _vehicleRegularPressureResistancePct,
        VeterancyRank.Veteran => _vehicleVeteranPressureResistancePct,
        VeterancyRank.Crack => _vehicleCrackPressureResistancePct,
        VeterancyRank.Elite => _vehicleElitePressureResistancePct,
        _ => 0
    };

    private int GetButtonedAccuracyPenalty(long ownerPtr)
    {
        if (!_vehicleCrewPressure || _vehicleButtonedAccuracyPenaltyPct <= 0 || ownerPtr == 0) return 0;
        if (!_vehiclePressureStates.TryGetValue(ownerPtr, out var state)) return 0;
        return state.Pressure >= _vehicleButtonedThreshold ? _vehicleButtonedAccuracyPenaltyPct : 0;
    }

    private void UpdateVehiclePressureSystem(TacticalManager manager)
    {
        if (!_vehicleCrewPressure || !Valid(manager)) return;

        Actor? active = null;
        try { active = manager.GetActiveActor(); } catch { }
        long activePtr = Valid(active) ? Key(active) : 0;

        // MENACE can briefly report no active actor while animations/actions settle.
        // Do not treat that transient gap as a new activation or the Buttoned AP
        // penalty could be applied twice when the same actor reappears.
        if (activePtr == 0) return;

        if (_vehiclePressureActiveActorPtr != 0 && _vehiclePressureActiveActorPtr != activePtr
            && _vehiclePressureStates.TryGetValue(_vehiclePressureActiveActorPtr, out var previous))
        {
            EndVehiclePressureActivation(previous);
        }

        if (_vehiclePressureActiveActorPtr != activePtr)
        {
            _vehiclePressureActiveActorPtr = activePtr;
            if (_vehiclePressureStates.TryGetValue(activePtr, out var state))
            {
                state.Actor = active;
                state.ActivationSerial = ++_vehiclePressureActivationSerial;
                state.ApPenaltyAppliedThisActivation = false;
                state.BreakHandledThisActivation = false;
                state.RetreatInProgress = false;
                BeginVehiclePressureActivation(manager, active!, state);
            }
        }
        else if (_vehiclePressureStates.TryGetValue(activePtr, out var current) && current.RetreatInProgress)
        {
            try
            {
                if (Environment.TickCount64 - current.RetreatStartedAtMs >= 250 && !active!.IsMoving())
                    FinalizeBreakContact(active!, current, "movement-finished");
            }
            catch { }
        }
    }

    private void BeginVehiclePressureActivation(TacticalManager manager, Actor actor, VehiclePressureState state)
    {
        if (!Valid(actor)) return;
        state.Actor = actor;

        if (TryResolvePlayerPilotForOwner(state.OwnerPtr, out var pilot) && !string.IsNullOrWhiteSpace(pilot.TemplateId))
            state.Rank = GetVehicleCrewRank(pilot.TemplateId);

        if (state.BreakContactPending || state.Pressure >= _vehicleBreakContactThreshold)
        {
            TryStartBreakContact(manager, actor, state);
            return;
        }

        if (state.Pressure >= _vehicleButtonedThreshold)
            ApplyButtonedApPenalty(actor, state);
    }

    private void EndVehiclePressureActivation(VehiclePressureState state)
    {
        if (state == null) return;
        if (state.RetreatInProgress && state.Actor != null && Valid(state.Actor))
            FinalizeBreakContact(state.Actor, state, "activation-ended");

        // Break Contact consumes the activation.  Keep the state at Break Contact
        // until that activation is over, then transition to Buttoned Up for the NEXT
        // activation.  This guarantees that every vehicle shown as Buttoned Up is a
        // normally usable vehicle with only the configured AP/accuracy penalties.
        if (state.PostBreakPressureResetPending)
        {
            float before = state.Pressure;
            float postBreakCap = Math.Min(_vehiclePressureAfterBreakContact, _vehicleBreakContactThreshold - 1);
            state.Pressure = Math.Min(before, postBreakCap);
            state.PostBreakPressureResetPending = false;
            state.BreakContactPending = false;
            _log($"VEHICLE BREAK CONTACT RESET owner=0x{state.OwnerPtr:X} pressure={before:0.##}->{state.Pressure:0.##} state={VehiclePressureLabel(state.Pressure)} nextActivation=usable");
            return;
        }

        if (state.BreakHandledThisActivation || state.BreakContactPending) return;
        if (state.Pressure <= 0f || _vehiclePressureRecoveryPerTurn <= 0) return;

        float beforeRecovery = state.Pressure;
        state.Pressure = Math.Max(0f, beforeRecovery - _vehiclePressureRecoveryPerTurn);
        if (state.Pressure < _vehicleBreakContactThreshold) state.BreakContactPending = false;
        if (Math.Abs(beforeRecovery - state.Pressure) >= 0.01f)
            _log($"VEHICLE PRESSURE RECOVER owner=0x{state.OwnerPtr:X} amount=-{_vehiclePressureRecoveryPerTurn} pressure={beforeRecovery:0.##}->{state.Pressure:0.##} state={VehiclePressureLabel(state.Pressure)} nativeMorale={SafeMoraleLabel(state.Actor)}");
    }

    private void ApplyButtonedApPenalty(Actor actor, VehiclePressureState state)
    {
        if (state.ApPenaltyAppliedThisActivation || _vehicleButtonedApPenalty <= 0 || !Valid(actor)) return;
        try
        {
            int before = actor.GetActionPoints();
            int after = Math.Max(0, before - _vehicleButtonedApPenalty);
            if (after != before) actor.SetActionPoints(after, true);
            state.ApPenaltyAppliedThisActivation = true;
            _log($"VEHICLE BUTTONED AP owner=0x{state.OwnerPtr:X} pressure={state.Pressure:0.##} AP={before}->{after}");
        }
        catch (Exception e)
        {
            if (_verbose) _log($"VEHICLE BUTTONED AP ERROR owner=0x{state.OwnerPtr:X} {e.GetType().Name}: {e.Message}");
        }
    }

    private void TryStartBreakContact(TacticalManager manager, Actor actor, VehiclePressureState state)
    {
        if (state.BreakHandledThisActivation || !Valid(actor)) return;
        state.BreakHandledThisActivation = true;

        // Do not hand control back to MENACE's generic morale-flee behavior here.
        // Vehicles use Crew Pressure for combat stress: Buttoned Up stays usable, and
        // only the explicit Break Contact threshold triggers the controlled retreat.
        // Native morale is intentionally ignored for this decision.

        if (!state.HasThreat || !TryFindBreakContactRetreat(manager, actor, state, out var destination, out var action, out int cost, out float gain, out string locomotion))
        {
            int beforeAp = 0;
            try { beforeAp = actor.GetActionPoints(); actor.SetActionPoints(0, true); } catch { }
            float before = state.Pressure;
            state.BreakContactPending = false;
            state.PostBreakPressureResetPending = true;
            _log($"VEHICLE BREAK CONTACT FAILED owner=0x{state.OwnerPtr:X} reason=no-safe-retreat-path AP={beforeAp}->0 pressure={before:0.##} reset=after-activation nativeMorale={SafeMoraleLabel(actor)}");
            return;
        }

        try
        {
            bool started = actor.MoveTo(destination, ref action, MovementFlags.None);
            if (!started)
            {
                int beforeAp = actor.GetActionPoints();
                actor.SetActionPoints(0, true);
                float before = state.Pressure;
                state.BreakContactPending = false;
                state.PostBreakPressureResetPending = true;
                _log($"VEHICLE BREAK CONTACT FAILED owner=0x{state.OwnerPtr:X} reason=MoveTo-returned-false AP={beforeAp}->0 pressure={before:0.##} reset=after-activation");
                return;
            }

            state.RetreatInProgress = true;
            state.RetreatStartedAtMs = Environment.TickCount64;
            _log($"VEHICLE BREAK CONTACT START owner=0x{state.OwnerPtr:X} locomotion={locomotion} cost={cost} gain={gain:0.0} action={action} dest=({destination.GetX()},{destination.GetZ()}) pressure={state.Pressure:0.##} nativeMorale={SafeMoraleLabel(actor)}");
        }
        catch (Exception e)
        {
            try { actor.SetActionPoints(0, true); } catch { }
            float before = state.Pressure;
            state.BreakContactPending = false;
            state.PostBreakPressureResetPending = true;
            _log($"VEHICLE BREAK CONTACT ERROR owner=0x{state.OwnerPtr:X} {e.GetType().Name}: {e.Message} pressure={before:0.##} reset=after-activation");
        }
    }

    private bool TryFindBreakContactRetreat(TacticalManager manager, Actor actor, VehiclePressureState state,
        out Tile destination, out MovementAction action, out int cost, out float distanceGain, out string locomotion)
    {
        destination = null!;
        action = MovementAction.Default;
        cost = 0;
        distanceGain = 0f;
        locomotion = "none";
        if (!Valid(manager) || !Valid(actor) || !state.HasThreat) return false;

        Map map;
        Tile current;
        try
        {
            map = manager.GetMap();
            current = actor.GetTile();
        }
        catch { return false; }
        if (!Valid(map) || !Valid(current)) return false;

        int ap;
        try { ap = actor.GetActionPoints(); } catch { return false; }
        if (ap <= 0) return false;

        int cx = current.GetX();
        int cz = current.GetZ();
        Vector3 currentPos = actor.GetPos();
        Vector3 away = currentPos - state.LastThreatPos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) return false;

        float currentThreatDistance = Vector3.Distance(currentPos, state.LastThreatPos);
        int radius = Math.Max(2, _vehicleBreakContactMaxTiles);
        bool walker = IsWalkerVehicle(state.OwnerPtr);

        // Prefer the locomotion that looks natural for the chassis, but MENACE 0.7.15
        // can occasionally return zero explicit Backwards paths even when the actor still
        // has legal movement. Fall back to native Auto, then Forward for conventional
        // vehicles. Walkers continue to prefer Forward so their leg animation remains
        // correct. We never use Force, SkipPathfinding or teleport movement.
        Il2CppTactical.MoveBackwardsMode[] pathModes = walker
            ? new[] { Il2CppTactical.MoveBackwardsMode.Forward, Il2CppTactical.MoveBackwardsMode.Auto }
            : new[] { Il2CppTactical.MoveBackwardsMode.Backwards, Il2CppTactical.MoveBackwardsMode.Auto, Il2CppTactical.MoveBackwardsMode.Forward };
        string[] locomotionLabels = walker
            ? new[] { "walker-forward", "walker-auto-fallback" }
            : new[] { "reverse", "auto-fallback", "forward-fallback" };

        for (int pass = 0; pass < pathModes.Length; pass++)
        {
            var pathMode = pathModes[pass];
            string passLocomotion = locomotionLabels[pass];
            float bestScore = float.MinValue;
            MovementAction bestAction = MovementAction.Default;
            int bestCost = 0;
            float bestGain = 0f;
            Tile? bestTile = null;
            int destinationCandidates = 0;
            int nativePaths = 0;
            int affordablePaths = 0;
            int distancingPaths = 0;

            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    int x = cx + dx;
                    int z = cz + dz;
                    if (!map.IsInBounds(x, z)) continue;

                    Tile tile;
                    try { tile = map.GetTile(x, z); } catch { continue; }
                    if (!Valid(tile)) continue;

                    // Do not pre-filter with Tile.IsValidMovementDestination() /
                    // Tile.CanBeEnteredBy(actor). MENACE's native pathfinder is the
                    // authority on vehicle reachability.
                    Vector3 candidatePos;
                    try { candidatePos = tile.GetPos(); } catch { continue; }
                    destinationCandidates++;

                    Il2CppTactical.MovementResult result;
                    try { result = Il2CppTactical.MovementSystem.GetPath(actor, tile, pathMode, MovementFlags.None); }
                    catch { continue; }
                    if (result == null || !result.Success || result.Cost <= 0) continue;
                    try { if (result.Path == null || result.Path.Count < 2) continue; } catch { }
                    nativePaths++;
                    if (result.Cost > ap) continue;
                    affordablePaths++;

                    float candidateThreatDistance = Vector3.Distance(candidatePos, state.LastThreatPos);
                    float gain = candidateThreatDistance - currentThreatDistance;
                    if (gain <= 0.05f) continue;
                    distancingPaths++;

                    Vector3 delta = candidatePos - currentPos;
                    delta.y = 0f;
                    float alignment = 0f;
                    if (delta.sqrMagnitude > 0.001f && away.sqrMagnitude > 0.001f)
                        alignment = Vector3.Dot(delta.normalized, away.normalized);

                    float score = gain * 1000f + alignment * 40f - result.Cost;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestTile = tile;
                    if (walker)
                    {
                        // Never force Backwards on walkers: doing so makes the chassis
                        // slide while the leg animation remains stationary.
                        bestAction = result.Action & ~MovementAction.Backwards;
                    }
                    else if (pathMode == Il2CppTactical.MoveBackwardsMode.Backwards)
                    {
                        bestAction = result.Action | MovementAction.Backwards;
                    }
                    else if (pathMode == Il2CppTactical.MoveBackwardsMode.Forward)
                    {
                        bestAction = result.Action & ~MovementAction.Backwards;
                    }
                    else
                    {
                        // Auto is a compatibility fallback: trust MENACE's own action.
                        bestAction = result.Action;
                    }
                    bestCost = result.Cost;
                    bestGain = gain;
                }
            }

            if (Valid(bestTile))
            {
                _log($"VEHICLE BREAK CONTACT SEARCH owner=0x{state.OwnerPtr:X} locomotion={passLocomotion} radius={radius} AP={ap} destinations={destinationCandidates} nativePaths={nativePaths} affordable={affordablePaths} distancing={distancingPaths} result=found cost={bestCost} gain={bestGain:0.0}");
                destination = bestTile!;
                action = bestAction;
                cost = bestCost;
                distanceGain = bestGain;
                locomotion = passLocomotion;
                return true;
            }

            _log($"VEHICLE BREAK CONTACT SEARCH owner=0x{state.OwnerPtr:X} locomotion={passLocomotion} radius={radius} AP={ap} destinations={destinationCandidates} nativePaths={nativePaths} affordable={affordablePaths} distancing={distancingPaths} result=none");
        }

        return false;
    }

    private void FinalizeBreakContact(Actor actor, VehiclePressureState state, string reason)
    {
        if (!state.RetreatInProgress) return;
        int beforeAp = 0;
        try
        {
            beforeAp = actor.GetActionPoints();
            actor.SetActionPoints(0, true);
        }
        catch { }

        float before = state.Pressure;
        state.BreakContactPending = false;
        state.RetreatInProgress = false;
        state.PostBreakPressureResetPending = true;
        _log($"VEHICLE BREAK CONTACT COMPLETE owner=0x{state.OwnerPtr:X} reason={reason} AP={beforeAp}->0 pressure={before:0.##} state=Break Contact reset=after-activation nativeMorale={SafeMoraleLabel(actor)}");
    }

    private VeterancyRank GetVehicleCrewRank(string pilotTemplateId)
    {
        if (!_vehicleCrewVeterancy || string.IsNullOrWhiteSpace(pilotTemplateId)) return VeterancyRank.Regular;
        return VeterancyRules.RankFromXp(_xp.GetPilotXp(pilotTemplateId));
    }

    private int GetTeamSuppressionResistancePct(int squaddieId)
    {
        if (!_enabled || !_teamSuppressionResistanceMods || !_identities.Contains(squaddieId)) return 0;
        if (!TryGetReliableSquadAverageXp(squaddieId, out double avg, out _, out _)) return 0;
        return SuppressionResistancePctForRank(VeterancyRules.RankFromXp(avg));
    }

    private int GetTeamConcealment(int squaddieId)
    {
        if (!_enabled || !_teamConcealmentMods || !_identities.Contains(squaddieId)) return 0;
        if (!TryGetReliableSquadAverageXp(squaddieId, out double avg, out _, out _)) return 0;
        return ConcealmentForRank(VeterancyRules.RankFromXp(avg));
    }

    private int HpBonusForRank(VeterancyRank rank) => rank switch
    {
        VeterancyRank.CombatTested => _combatTestedHp,
        VeterancyRank.Hardened => _hardenedHp,
        VeterancyRank.Regular => _regularHp,
        VeterancyRank.Veteran => _veteranHp,
        VeterancyRank.Crack => _veteranHp,
        VeterancyRank.Elite => _veteranHp,
        _ => 0
    };

    private int AccuracyPctForRank(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => _greenAccuracyPct,
        VeterancyRank.CombatTested => _combatTestedAccuracyPct,
        VeterancyRank.Hardened => _hardenedAccuracyPct,
        VeterancyRank.Regular => _regularAccuracyPct,
        VeterancyRank.Veteran => _veteranAccuracyPct,
        VeterancyRank.Crack => _crackAccuracyPct,
        VeterancyRank.Elite => _eliteAccuracyPct,
        _ => 0
    };

    private int DefensePctForRank(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => _greenDefensePct,
        VeterancyRank.CombatTested => _combatTestedDefensePct,
        VeterancyRank.Hardened => _hardenedDefensePct,
        VeterancyRank.Regular => _regularDefensePct,
        VeterancyRank.Veteran => _veteranDefensePct,
        VeterancyRank.Crack => _crackDefensePct,
        VeterancyRank.Elite => _eliteDefensePct,
        _ => 0
    };

    private int SuppressionResistancePctForRank(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => _greenSuppressionResistancePct,
        VeterancyRank.CombatTested => _combatTestedSuppressionResistancePct,
        VeterancyRank.Hardened => _hardenedSuppressionResistancePct,
        VeterancyRank.Regular => _regularSuppressionResistancePct,
        VeterancyRank.Veteran => _veteranSuppressionResistancePct,
        VeterancyRank.Crack => _crackSuppressionResistancePct,
        VeterancyRank.Elite => _eliteSuppressionResistancePct,
        _ => 0
    };

    private int ConcealmentForRank(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => _greenConcealment,
        VeterancyRank.CombatTested => _combatTestedConcealment,
        VeterancyRank.Hardened => _hardenedConcealment,
        VeterancyRank.Regular => _regularConcealment,
        VeterancyRank.Veteran => _veteranConcealment,
        VeterancyRank.Crack => _veteranConcealment,
        VeterancyRank.Elite => _veteranConcealment,
        _ => 0
    };

    private void LogSquadPropertyModifierOnce(string kind, VeterancyPropertiesOwnerState owner, int delta, float before, float after, bool isPercent)
    {
        if (owner.SquaddieId <= 0 || owner.OwnerPtr == 0) return;
        if (!TryGetReliableSquadAverageXp(owner.SquaddieId, out double avg, out int squadNumber, out _)) return;
        var rank = VeterancyRules.RankFromXp(avg);
        string key = $"property:{kind}:{owner.OwnerPtr:X}:{rank}:{delta}:{before:0.###}:{after:0.###}";
        if (!_combatModifierLogs.Add(key)) return;
        string suffix = isPercent ? $"{delta:+#;-#;0}%" : $"{delta:+#;-#;0}";
        _log($"SQUAD {kind.ToUpperInvariant()} MOD owner=0x{owner.OwnerPtr:X} sid={owner.SquaddieId} squad={squadNumber} avgXp={avg:0.00} rank={VeterancyRules.DisplayName(rank)} mod={suffix} value={before:0.###}->{after:0.###}");
    }

    private void LogTemporaryNpcModifierOnce(string kind, long ownerPtr, VeterancyRank rank, bool vehicle, int delta)
    {
        if (ownerPtr == 0) return;
        string key = $"npc-property:{kind}:{ownerPtr:X}:{rank}:{vehicle}:{delta}";
        if (!_combatModifierLogs.Add(key)) return;
        _log($"NPC {kind.ToUpperInvariant()} MOD owner=0x{ownerPtr:X} vehicle={(vehicle ? 1 : 0)} rank={VeterancyRules.DisplayName(rank)} mod={delta:+#;-#;0}%");
    }

    private void LogTemporaryNpcModifierOnce(string kind, long ownerPtr, VeterancyRank rank, bool vehicle, int delta, float before, float after)
    {
        if (ownerPtr == 0) return;
        string key = $"npc-property:{kind}:{ownerPtr:X}:{rank}:{vehicle}:{delta}:{before:0.###}:{after:0.###}";
        if (!_combatModifierLogs.Add(key)) return;
        _log($"NPC {kind.ToUpperInvariant()} MOD owner=0x{ownerPtr:X} vehicle={(vehicle ? 1 : 0)} rank={VeterancyRules.DisplayName(rank)} mod={delta:+#;-#;0}% value={before:0.###}->{after:0.###}");
    }

    private void LogCombatModifierOnce(string kind, long ownerPtr, int sid, int pct)
    {
        if (sid <= 0 || ownerPtr == 0) return;
        if (!TryGetReliableSquadAverageXp(sid, out double avg, out int squadNumber, out _)) return;
        var rank = VeterancyRules.RankFromXp(avg);
        string key = $"{kind}:{ownerPtr:X}:{rank}:{pct}";
        if (!_combatModifierLogs.Add(key)) return;
        _log($"SQUAD {kind.ToUpperInvariant()} MOD owner=0x{ownerPtr:X} sid={sid} squad={squadNumber} avgXp={avg:0.00} rank={VeterancyRules.DisplayName(rank)} pct={pct:+#;-#;0}%");
    }

    private void LogSquadMappingSkipOnce(int sid)
    {
        if (sid <= 0) return;
        string key = $"squad-mapping-skip:{sid}:{_sceneName}";
        if (!_combatModifierLogs.Add(key)) return;
        _log($"SQUAD RANK SKIP sid={sid} scene={Sanitize(_sceneName)} reason=unresolved-squad-mapping; individual XP will not be used as squad XP");
    }

    private void LogSquadMappingRecoveryOnce(int sid, int squadNumber, int members)
    {
        if (sid <= 0 || squadNumber <= 0) return;
        string key = $"squad-mapping-recovered:{sid}:{squadNumber}:{_sceneName}";
        if (!_combatModifierLogs.Add(key)) return;
        _log($"SQUAD MAPPING RECOVERED sid={sid} squad={squadNumber} members={members} scene={Sanitize(_sceneName)} source=last-known-valid-roster");
    }

    private void ReapplyHpBonusesForSquad(int anySquaddieId)
    {
        if (!_identities.TryGetReliableSquad(anySquaddieId, out _, out var reliableMembers, out _))
        {
            LogSquadMappingSkipOnce(anySquaddieId);
            return;
        }
        var members = new HashSet<int>(reliableMembers);
        foreach (var state in _liveElements.Values.ToList())
        {
            if (state.PlayerSquaddie && members.Contains(state.SquaddieId)) ReapplyHpBonus(state);
        }
    }

    private void FinalizePendingInitialHp()
    {
        if (!_enabled || !_teamHpBuffs) return;

        foreach (var state in _liveElements.Values.ToList())
        {
            if (!state.PlayerSquaddie || state.InitialHpSettled || state.BaseMaxHp <= 0 || state.BaseMaxHp > 1000 || state.SquaddieId <= 0) continue;
            if (state.Element == null || !Valid(state.Element) || !_identities.Contains(state.SquaddieId)) continue;

            try
            {
                int currentMax = state.Element.GetHitpointsMax();
                int currentHp = state.Element.GetHitpoints();
                if (currentMax <= 0 || currentMax > 1000 || currentHp <= 0 || currentHp > 1000) continue;

                int desiredBonus = GetTeamHpBonus(state.SquaddieId);
                int desiredMax = Math.Max(1, state.BaseMaxHp + desiredBonus);
                if (desiredMax > 1000) continue;

                if (currentMax == desiredMax)
                {
                    state.AppliedBonus = desiredBonus;
                    state.InitialHpSettled = true;
                    continue;
                }

                // This exact equality is the safety gate: apply only after MENACE's
                // live max HP has settled to the sane vanilla base value captured during
                // CreateElement. Never infer a base from raw fields.
                if (currentMax != state.BaseMaxHp) continue;
                if (desiredBonus <= 0)
                {
                    state.AppliedBonus = 0;
                    state.InitialHpSettled = true;
                    continue;
                }

                bool wasFull = currentHp >= currentMax;
                _hpMutationGuard = true;
                try
                {
                    state.Element.SetHitpointsMax(desiredMax);
                    if (wasFull) state.Element.SetHitpoints(desiredMax);
                    else if (currentHp > desiredMax) state.Element.SetHitpoints(desiredMax);
                }
                finally { _hpMutationGuard = false; }

                state.AppliedBonus = desiredBonus;
                state.InitialHpSettled = true;
                _log($"HP INITIAL BUFF sid={state.SquaddieId} base={state.BaseMaxHp} bonus=+{desiredBonus} max={currentMax}->{desiredMax} hp={currentHp}->{(wasFull ? desiredMax : Math.Min(currentHp, desiredMax))}");
            }
            catch (Exception e)
            {
                if (_verbose) _log($"HP INITIAL BUFF ERROR sid={state.SquaddieId} {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private void ReapplyHpBonus(LiveElementState state)
    {
        if (!state.PlayerSquaddie || state.Element == null || !Valid(state.Element) || state.SquaddieId <= 0 || !_identities.Contains(state.SquaddieId)) return;
        try
        {
            // Only mutate elements for which CreateElement captured a sane vanilla
            // HP base. Never infer the base value later from mutable Element fields.
            if (state.BaseMaxHp <= 0 || state.BaseMaxHp > 1000) return;

            int desiredBonus = GetTeamHpBonus(state.SquaddieId);
            int currentMax = state.Element.GetHitpointsMax();
            int currentHp = state.Element.GetHitpoints();
            if (currentMax <= 0 || currentMax > 1000 || currentHp <= 0 || currentHp > 1000)
            {
                if (_verbose) _log($"HP BUFF skipped sid={state.SquaddieId} transient hp={currentHp}/{currentMax}");
                return;
            }

            int baseMax = state.BaseMaxHp;
            int expectedCurrent = Math.Max(1, baseMax + state.AppliedBonus);
            int desiredMax = Math.Max(1, baseMax + desiredBonus);
            if (desiredMax > 1000) return;
            if (currentMax != baseMax && currentMax != expectedCurrent)
            {
                if (_verbose) _log($"HP BUFF skipped sid={state.SquaddieId} unexpected max={currentMax} base={baseMax} appliedBonus={state.AppliedBonus}");
                return;
            }
            if (currentMax == desiredMax && state.AppliedBonus == desiredBonus)
            {
                state.BaseMaxHp = baseMax;
                return;
            }

            bool wasFull = currentHp >= currentMax;
            _hpMutationGuard = true;
            try
            {
                state.Element.SetHitpointsMax(desiredMax);
                if (wasFull) state.Element.SetHitpoints(desiredMax);
                else if (currentHp > desiredMax) state.Element.SetHitpoints(desiredMax);
            }
            finally { _hpMutationGuard = false; }

            int oldBonus = state.AppliedBonus;
            state.BaseMaxHp = baseMax;
            state.AppliedBonus = desiredBonus;
            state.InitialHpSettled = true;
            if (TryGetReliableSquadAverageXp(state.SquaddieId, out double avg, out int squadNumber, out _))
                _log($"HP BUFF sid={state.SquaddieId} squad={squadNumber} avgXp={avg:0.00} rank={VeterancyRules.DisplayName(VeterancyRules.RankFromXp(avg))} bonus={oldBonus}->{desiredBonus} maxHp={currentMax}->{desiredMax}");
        }
        catch (Exception e)
        {
            if (_verbose) _log($"HP BUFF ERROR sid={state.SquaddieId} {e.GetType().Name}: {e.Message}");
        }
    }

    private void PruneXpDedup()
    {
        try
        {
            long now = Environment.TickCount64;
            if (_incomingTargetHints.Count > 0)
            {
                foreach (string key in _incomingTargetHints
                    .Where(kv => now - kv.Value.SeenAtMs > 5000)
                    .Select(kv => kv.Key)
                    .ToList())
                    _incomingTargetHints.Remove(key);
            }
            if (_xpDedup.Count >= 256)
            {
                foreach (string key in _xpDedup.Where(kv => now - kv.Value > 5000).Select(kv => kv.Key).ToList())
                    _xpDedup.Remove(key);
            }
            if (_malfunctionDedup.Count >= 128)
            {
                foreach (string key in _malfunctionDedup.Where(kv => now - kv.Value > 5000).Select(kv => kv.Key).ToList())
                    _malfunctionDedup.Remove(key);
            }
        }
        catch (InvalidOperationException e)
        {
            // Combat callbacks can touch these short-lived dedup dictionaries while
            // Update is pruning them. Skip only this cleanup pass instead of aborting
            // the remainder of the frame's veterancy update.
            if (_verbose) _log($"XP DEDUP PRUNE deferred: {e.Message}");
        }
    }

    private static string SkillId(Skill? skill)
    {
        if (!Valid(skill)) return "?";
        return SafeCall(skill!, "GetID") ?? SafeCall(skill!, "GetId") ?? SafeCall(skill!, "GetName") ?? "?";
    }

    private static string Signature(MethodBase m)
    {
        string pars;
        try { pars = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)); }
        catch { pars = "?"; }
        string ret = m is MethodInfo mi ? mi.ReturnType.Name + " " : "";
        return $"{ret}{m.DeclaringType?.FullName}.{m.Name}({pars})";
    }

    private static bool Valid(Il2CppObjectBase? value) => value != null && value.Pointer.ToInt64() != 0;
    private static long Key(object? value) { try { return value is Il2CppObjectBase io ? io.Pointer.ToInt64() : value?.GetHashCode() ?? 0; } catch { return 0; } }
    private static string? SafeCall(object obj, string method) { try { return obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.Invoke(obj, null)?.ToString(); } catch { return null; } }
    private static string NormalizePath(string path) { try { return System.IO.Path.GetFullPath(path); } catch { return path; } }
    private static string SafeFileName(string path) { try { return System.IO.Path.GetFileName(path); } catch { return path; } }
    private static string Sanitize(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
}
