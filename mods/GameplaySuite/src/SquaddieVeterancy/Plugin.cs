using MenaceModSettings;
using MenaceModSuite.Shared;
using UnityEngine;

[assembly: AssemblyVersion("1.10.1.0")]
[assembly: AssemblyFileVersion("1.10.1.0")]

namespace SquaddieProbe;

// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/SquaddieVeterancy/code.
public sealed partial class Plugin : IHostedPlugin
{
    private const string Version = "1.10";
    private const string SettingsGroup = "Squaddie Veterancy";

    private ModLog? _logger;
    private global::HarmonyLib.Harmony? _harmony;
    private ProbeRuntime? _runtime;
    private float _bindAt;
    private bool _attempted;
    private int _rosterPage;
    private bool _confirmReset;
    private string _sceneName = "?";
    private bool _rosterOpen;
    private bool _rosterWindowInitialized;
    private Rect _rosterWindowRect;
    private Vector2 _rosterScroll;
    private bool _rosterDragging;
    private bool _debugLogging;
    private long _debugLogWindowStartedMs;
    private int _debugLogLinesInWindow;
    private int _debugLogSuppressedInWindow;
    private const int MaxDebugLogLinesPerSecond = 12;
    private Vector2 _rosterDragOffset;

    public void Initialize(ModLog logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("Persistent squaddie veterancy");
            s.AddToggle("EnableProbe", "Enable squaddie veterancy", true);
            s.AddToggle("AwardXp", "Enable squad combat and mission XP", true);
            s.AddSlider("XpPerAttack", "XP per squad attack action", 0f, 25f, 1f);
            s.AddSlider("XpPerEnemyElementKill", "XP per enemy infantry element killed", 0f, 50f, 5f);
            s.AddSlider("XpPerEnemyVehicleKill", "XP per enemy vehicle destroyed (infantry & vehicle crews)", 0f, 150f, 50f);
            s.AddSlider("XpPerIncomingAttack", "XP when squad is targeted by an enemy attack", 0f, 50f, 10f);
            s.AddSlider("XpPerPrimaryObjective", "XP per completed primary objective", 0f, 100f, 25f);
            s.AddSlider("XpPerSecondaryObjective", "XP per completed secondary objective", 0f, 100f, 10f);

            s.AddHeader("Rescued squaddie consequences");
            s.AddToggle("RescueXpLoss", "Rescued squaddies lose veterancy XP", true);
            s.AddSlider("RescueXpLossPct", "XP lost when a squaddie is rescued (%)", 0f, 100f, 25f);

            s.AddHeader("Vehicle crew veterancy");
            s.AddToggle("VehicleCrewVeterancy", "Enable persistent loadout-derived vehicle crew veterancy", true);
            s.AddSlider("VehicleXpPerAttack", "Vehicle crew XP per attack action", 0f, 25f, 1f);
            s.AddSlider("VehicleXpPerIncomingAttack", "Vehicle crew XP when targeted by an enemy attack", 0f, 50f, 2f);
            s.AddSlider("VehicleXpPerEnemyElementKill", "Vehicle crew XP per enemy infantry element killed", 0f, 50f, 5f);
            s.AddSlider("VehicleXpPerMission", "Vehicle crew XP per completed mission", 0f, 100f, 10f);
            s.AddSlider("VehicleXpPerMissionSurvived", "Additional XP when vehicle crew survives mission", 0f, 100f, 10f);
            s.AddSlider("VehicleXpPerPrimaryObjective", "Vehicle crew XP per completed primary objective", 0f, 100f, 25f);
            s.AddSlider("VehicleXpPerSecondaryObjective", "Vehicle crew XP per completed secondary objective", 0f, 100f, 10f);

            s.AddHeader("Vehicle crew casualties");
            s.AddSlider("VehicleDestroyedCrewSurvivalPct", "Survival chance per crew member when vehicle is destroyed (%)", 0f, 100f, 50f);
            s.AddSlider("VehicleCrewWoundedXpLossPct", "XP lost by a wounded crew member (%)", 0f, 100f, 25f);
            s.AddSlider("VehicleMinorDefectInjuryPct", "Chance a Light/Medium defect wounds one crew member (%)", 0f, 100f, 25f);
            s.AddSlider("VehicleMajorDefectDeathPct", "Death chance for affected crew member on Heavy defect (%)", 0f, 100f, 50f);

            s.AddHeader("Vehicle crew pressure / break contact");
            s.AddToggle("VehicleCrewPressure", "Enable vehicle crew pressure, Buttoned Up and Break Contact", true);
            s.AddSlider("VehiclePressureReceivedPct", "Vehicle pressure received vs infantry suppression (%)", 10f, 100f, 50f);
            s.AddSlider("VehicleDamagePressureBump", "Pressure shock when a vehicle actually takes damage", 0f, 100f, 40f);
            s.AddSlider("VehicleLightDefectPressureBump", "Additional pressure from a Light defect", 0f, 75f, 10f);
            s.AddSlider("VehicleMediumDefectPressureBump", "Additional pressure from a Medium defect", 0f, 75f, 20f);
            s.AddSlider("VehicleHeavyDefectPressureBump", "Additional pressure from a Heavy defect", 0f, 100f, 35f);
            s.AddSlider("VehicleButtonedThreshold", "Buttoned Up pressure threshold", 10f, 100f, 50f);
            s.AddSlider("VehicleBreakContactThreshold", "Break Contact pressure threshold", 20f, 150f, 100f);
            s.AddSlider("VehicleButtonedApPenalty", "Buttoned Up AP penalty", 0f, 60f, 20f);
            s.AddSlider("VehicleButtonedAccuracyPenaltyPct", "Buttoned Up accuracy penalty (%)", 0f, 60f, 20f);
            s.AddSlider("VehiclePressureRecoveryPerTurn", "Pressure recovered after a normal vehicle activation", 0f, 100f, 20f);
            s.AddSlider("VehiclePressureAfterBreakContact", "Pressure after Break Contact retreat", 0f, 100f, 60f);
            s.AddSlider("VehicleBreakContactMaxTiles", "Maximum Break Contact distance (tiles)", 2f, 12f, 8f);
            s.AddSlider("VehicleRallyPressureReduction", "Crew Pressure removed by Command: Rally", 0f, 150f, 80f);
            s.AddToggle("VehicleCrewPressureRankMods", "Scale vehicle pressure resistance by crew rank", true);
            // 1.9 pressure keys intentionally avoid inheriting obsolete native-suppression settings.
            s.AddSlider("VehicleV19GreenPressureResistancePct", "Green vehicle crew pressure resistance modifier (%)", -50f, 75f, -6f);
            s.AddSlider("VehicleV19CombatTestedPressureResistancePct", "Combat Tested vehicle crew pressure resistance modifier (%)", -50f, 75f, -4f);
            s.AddSlider("VehicleV19HardenedPressureResistancePct", "Hardened vehicle crew pressure resistance modifier (%)", -50f, 75f, -2f);
            s.AddSlider("VehicleV19RegularPressureResistancePct", "Regular vehicle crew pressure resistance modifier (%)", -50f, 75f, 0f);
            s.AddSlider("VehicleV19VeteranPressureResistancePct", "Veteran vehicle crew pressure resistance modifier (%)", -50f, 75f, 2f);
            s.AddSlider("VehicleV19CrackPressureResistancePct", "Crack vehicle crew pressure resistance modifier (%)", -50f, 75f, 4f);
            s.AddSlider("VehicleV19ElitePressureResistancePct", "Elite vehicle crew pressure resistance modifier (%)", -50f, 75f, 6f);

            s.AddHeader("Squad-average veterancy");
            s.AddToggle("TeamHpBuffs", "Enable squad-average HP modifier", true);
            s.AddSlider("CombatTestedHp", "Combat Tested HP per element", 0f, 5f, 1f);
            s.AddSlider("HardenedHp", "Hardened HP per element", 0f, 5f, 1f);
            s.AddSlider("RegularHp", "Regular HP per element", 0f, 5f, 2f);
            s.AddSlider("VeteranHp", "Veteran HP per element", 0f, 6f, 3f);

            s.AddToggle("TeamAccuracyMods", "Enable squad-average accuracy modifier", true);
            s.AddSlider("GreenAccuracyPct", "Green accuracy modifier (%)", -15f, 15f, -6f);
            s.AddSlider("ExtendedCombatTestedAccuracyPct", "Combat Tested accuracy modifier (%)", -15f, 15f, -4f);
            s.AddSlider("HardenedAccuracyPct", "Hardened accuracy modifier (%)", -15f, 15f, -2f);
            s.AddSlider("RegularAccuracyPct", "Regular accuracy modifier (%)", -15f, 15f, 0f);
            // Extended-rank keys deliberately supersede older defaults so existing ModSettings.json files cannot silently override the 1.7 curve.
            s.AddSlider("ExtendedVeteranAccuracyPct", "Veteran accuracy modifier (%)", -15f, 15f, 2f);
            s.AddSlider("ExtendedCrackAccuracyPct", "Crack accuracy modifier (%)", -15f, 15f, 4f);
            s.AddSlider("ExtendedEliteAccuracyPct", "Elite accuracy modifier (%)", -15f, 15f, 6f);

            s.AddToggle("TeamDefenseMods", "Enable squad-average defense modifier", true);
            s.AddSlider("GreenDefensePct", "Green defense modifier (%)", -15f, 15f, -6f);
            s.AddSlider("ExtendedCombatTestedDefensePct", "Combat Tested defense modifier (%)", -15f, 15f, -4f);
            s.AddSlider("HardenedDefensePct", "Hardened defense modifier (%)", -15f, 15f, -2f);
            s.AddSlider("RegularDefensePct", "Regular defense modifier (%)", -15f, 15f, 0f);
            s.AddSlider("ExtendedVeteranDefensePct", "Veteran defense modifier (%)", -15f, 15f, 2f);
            s.AddSlider("ExtendedCrackDefensePct", "Crack defense modifier (%)", -15f, 15f, 4f);
            s.AddSlider("ExtendedEliteDefensePct", "Elite defense modifier (%)", -15f, 15f, 6f);

            s.AddToggle("TeamSuppressionResistanceMods", "Enable squad-average suppression resistance modifier", true);
            s.AddSlider("GreenSuppressionResistancePct", "Green suppression resistance modifier (%)", -15f, 15f, -6f);
            s.AddSlider("ExtendedCombatTestedSuppressionResistancePct", "Combat Tested suppression resistance modifier (%)", -15f, 15f, -4f);
            s.AddSlider("HardenedSuppressionResistancePct", "Hardened suppression resistance modifier (%)", -15f, 15f, -2f);
            s.AddSlider("RegularSuppressionResistancePct", "Regular suppression resistance modifier (%)", -15f, 15f, 0f);
            s.AddSlider("ExtendedVeteranSuppressionResistancePct", "Veteran suppression resistance modifier (%)", -15f, 15f, 2f);
            s.AddSlider("ExtendedCrackSuppressionResistancePct", "Crack suppression resistance modifier (%)", -15f, 15f, 4f);
            s.AddSlider("ExtendedEliteSuppressionResistancePct", "Elite suppression resistance modifier (%)", -15f, 15f, 6f);

            s.AddToggle("TeamConcealmentMods", "Enable squad-average concealment modifier", true);
            s.AddSlider("GreenConcealment", "Green concealment modifier (tiles)", -5f, 5f, -2f);
            s.AddSlider("CombatTestedConcealment", "Combat Tested concealment modifier (tiles)", -5f, 5f, -1f);
            s.AddSlider("HardenedConcealment", "Hardened concealment modifier (tiles)", -5f, 5f, -1f);
            s.AddSlider("RegularConcealment", "Regular concealment modifier (tiles)", -5f, 5f, 0f);
            s.AddSlider("VeteranConcealment", "Veteran concealment modifier (tiles)", -5f, 5f, 1f);

            s.AddHeader("Tactical veterancy feedback");
            s.AddToggle("ShowVeterancyMarkers", "Show squad veterancy markers near unit HP bar", true);

            s.AddHeader("Veterancy roster");
            s.AddToggle("ShowVeterancyRoster", "Enable Tactical / Mission Planning veterancy roster (Ctrl+V)", true);

            s.AddHeader("NPC temporary veterancy");
            s.AddToggle("DetailedNpcProfiles", "Use faction + unit veterancy profiles for human NPCs", true);
            s.AddToggle("EnemyReliability", "Enable temporary veterancy for hostile human NPC infantry and vehicles", true);
            s.AddToggle("AlliedNpcReliability", "Enable temporary veterancy for allied NPC infantry", true);

            s.AddHeader("Individual reliability");
            s.AddToggle("IndividualReliability", "Enable individual no-fire reliability", true);
            s.AddSlider("GreenNoFirePct", "Green no-fire chance (%)", 0f, 30f, 10f);
            s.AddSlider("CombatTestedNoFirePct", "Combat Tested no-fire chance (%)", 0f, 20f, 5f);
            s.AddSlider("HardenedNoFirePct", "Hardened no-fire chance (%)", 0f, 15f, 3f);
            s.AddSlider("RegularNoFirePct", "Regular no-fire chance (%)", 0f, 10f, 2f);
            s.AddSlider("SuppressedNoFirePct", "Additional no-fire chance while Suppressed (%)", 0f, 30f, 5f);
            s.AddToggle("ShowFailedToFireText", "Show short failed-to-fire text above squad", true);

            s.AddHeader("Reliability diagnostics");
            s.AddToggle("MalfunctionDiagnostics", "Trace native malfunction path (research only)", false);

            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLoggingV2", "Debug logging (limited)", false);
        });

        DevConsole.RegisterPanel("Squaddie Veterancy", DrawDebug);
        _debugLogging = Setting("DebugLoggingV2", false);
        _bindAt = Time.realtimeSinceStartup + 2f;
        Log($"{Version}: save-aware infantry + vehicle crew veterancy initialized.");
        Log("NPC PROFILE BUILD: seven-rank faction + EntityTemplate veterancy enabled for Pirates, Rogue Army, hostile Local Forces, allied NPC infantry and human-crewed enemy vehicles; Constructs/Wildlife excluded.");
        Log("Ranks: Green 0-249 XP, Combat Tested 250-499, Hardened 500-999, Regular 1000-1999, Veteran 2000-3999, Crack 4000-7999, Elite 8000+.");
        Log("VEHICLE CREW BUILD: persistent loadout-derived abstract crews assigned under native Pilots; leader XP excluded; incoming-fire XP and casualties enabled.");
        Log("VEHICLE PRESSURE BUILD: custom human-crewed vehicle pressure enabled; Buttoned Up reduces AP/accuracy, wheeled/tracked vehicles reverse on Break Contact, walkers turn and retreat forward, and Command: Rally reduces Crew Pressure. Native morale/suppression remain separate; the native suppression bar is display-only for Crew Pressure.");
    }

    public void OnUnload()
    {
        Log("shutdown requested; flushing pending veterancy snapshots");
        try { _runtime?.Dispose(); }
        catch (Exception e) { Log("Shutdown failed: " + e); }
        _runtime = null;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _sceneName = sceneName ?? "?";
        _rosterScroll = Vector2.zero;
        _rosterWindowInitialized = false;
        _rosterDragging = false;
        // The roster is always opt-in in both Mission Preparation and Tactical.
        // Do not consult the legacy OpenPlanningRosterByDefault setting: older
        // ModSettings.json files may still contain it as true.
        _rosterOpen = false;

        _runtime?.SceneChanged(_sceneName);
        if (_runtime == null)
        {
            _attempted = false;
            _bindAt = Time.realtimeSinceStartup + 2f;
        }
    }

    public void OnUpdate()
    {
        _debugLogging = Setting("DebugLoggingV2", false);
        bool enabled = Setting("EnableProbe", true);
        if (!_attempted && enabled && Time.realtimeSinceStartup >= _bindAt)
        {
            _attempted = true;
            try
            {
                if (_harmony == null) throw new InvalidOperationException("Harmony unavailable.");
                _runtime = new ProbeRuntime(_harmony, Log);
            }
            catch (Exception e) { Log("Runtime init failed: " + e); }
        }

        if (_runtime == null) return;

        _runtime.Configure(
            enabled,
            Setting("AwardXp", true),
            Math.Max(0, (int)Math.Round(Setting("XpPerAttack", 1f))),
            Math.Max(0, (int)Math.Round(Setting("XpPerEnemyElementKill", 5f))),
            Math.Max(0, (int)Math.Round(Setting("XpPerEnemyVehicleKill", 50f))),
            Math.Max(0, (int)Math.Round(Setting("XpPerIncomingAttack", 10f))),
            Math.Max(0, (int)Math.Round(Setting("XpPerPrimaryObjective", 25f))),
            Math.Max(0, (int)Math.Round(Setting("XpPerSecondaryObjective", 10f))),
            Setting("RescueXpLoss", true),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("RescueXpLossPct", 25f)))),
            Setting("VehicleCrewVeterancy", true),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerAttack", 1f))),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerEnemyElementKill", 5f))),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerIncomingAttack", 2f))),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerMission", 10f))),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerMissionSurvived", 10f))),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerPrimaryObjective", 25f))),
            Math.Max(0, (int)Math.Round(Setting("VehicleXpPerSecondaryObjective", 10f))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehicleDestroyedCrewSurvivalPct", 50f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehicleCrewWoundedXpLossPct", 25f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehicleMinorDefectInjuryPct", 25f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehicleMajorDefectDeathPct", 50f)))),
            Setting("VehicleCrewPressure", true),
            Math.Max(10, Math.Min(100, (int)Math.Round(Setting("VehiclePressureReceivedPct", 50f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehicleDamagePressureBump", 40f)))),
            Math.Max(0, Math.Min(75, (int)Math.Round(Setting("VehicleLightDefectPressureBump", 10f)))),
            Math.Max(0, Math.Min(75, (int)Math.Round(Setting("VehicleMediumDefectPressureBump", 20f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehicleHeavyDefectPressureBump", 35f)))),
            Math.Max(10, Math.Min(100, (int)Math.Round(Setting("VehicleButtonedThreshold", 50f)))),
            Math.Max(20, Math.Min(150, (int)Math.Round(Setting("VehicleBreakContactThreshold", 100f)))),
            Math.Max(0, Math.Min(60, (int)Math.Round(Setting("VehicleButtonedApPenalty", 20f)))),
            Math.Max(0, Math.Min(60, (int)Math.Round(Setting("VehicleButtonedAccuracyPenaltyPct", 20f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehiclePressureRecoveryPerTurn", 20f)))),
            Math.Max(0, Math.Min(100, (int)Math.Round(Setting("VehiclePressureAfterBreakContact", 60f)))),
            Math.Max(2, Math.Min(12, (int)Math.Round(Setting("VehicleBreakContactMaxTiles", 8f)))),
            Math.Max(0, Math.Min(150, (int)Math.Round(Setting("VehicleRallyPressureReduction", 80f)))),
            Setting("VehicleCrewPressureRankMods", true),
            (int)Math.Round(Setting("VehicleV19GreenPressureResistancePct", -6f)),
            (int)Math.Round(Setting("VehicleV19CombatTestedPressureResistancePct", -4f)),
            (int)Math.Round(Setting("VehicleV19HardenedPressureResistancePct", -2f)),
            (int)Math.Round(Setting("VehicleV19RegularPressureResistancePct", 0f)),
            (int)Math.Round(Setting("VehicleV19VeteranPressureResistancePct", 2f)),
            (int)Math.Round(Setting("VehicleV19CrackPressureResistancePct", 4f)),
            (int)Math.Round(Setting("VehicleV19ElitePressureResistancePct", 6f)),
            Setting("TeamHpBuffs", true),
            Math.Max(0, (int)Math.Round(Setting("CombatTestedHp", 1f))),
            Math.Max(0, (int)Math.Round(Setting("HardenedHp", 1f))),
            Math.Max(0, (int)Math.Round(Setting("RegularHp", 2f))),
            Math.Max(0, (int)Math.Round(Setting("VeteranHp", 3f))),
            Setting("TeamAccuracyMods", true),
            (int)Math.Round(Setting("GreenAccuracyPct", -6f)),
            (int)Math.Round(Setting("ExtendedCombatTestedAccuracyPct", -4f)),
            (int)Math.Round(Setting("HardenedAccuracyPct", -2f)),
            (int)Math.Round(Setting("RegularAccuracyPct", 0f)),
            (int)Math.Round(Setting("ExtendedVeteranAccuracyPct", 2f)),
            (int)Math.Round(Setting("ExtendedCrackAccuracyPct", 4f)),
            (int)Math.Round(Setting("ExtendedEliteAccuracyPct", 6f)),
            Setting("TeamDefenseMods", true),
            (int)Math.Round(Setting("GreenDefensePct", -6f)),
            (int)Math.Round(Setting("ExtendedCombatTestedDefensePct", -4f)),
            (int)Math.Round(Setting("HardenedDefensePct", -2f)),
            (int)Math.Round(Setting("RegularDefensePct", 0f)),
            (int)Math.Round(Setting("ExtendedVeteranDefensePct", 2f)),
            (int)Math.Round(Setting("ExtendedCrackDefensePct", 4f)),
            (int)Math.Round(Setting("ExtendedEliteDefensePct", 6f)),
            Setting("TeamSuppressionResistanceMods", true),
            (int)Math.Round(Setting("GreenSuppressionResistancePct", -6f)),
            (int)Math.Round(Setting("ExtendedCombatTestedSuppressionResistancePct", -4f)),
            (int)Math.Round(Setting("HardenedSuppressionResistancePct", -2f)),
            (int)Math.Round(Setting("RegularSuppressionResistancePct", 0f)),
            (int)Math.Round(Setting("ExtendedVeteranSuppressionResistancePct", 2f)),
            (int)Math.Round(Setting("ExtendedCrackSuppressionResistancePct", 4f)),
            (int)Math.Round(Setting("ExtendedEliteSuppressionResistancePct", 6f)),
            Setting("TeamConcealmentMods", true),
            (int)Math.Round(Setting("GreenConcealment", -2f)),
            (int)Math.Round(Setting("CombatTestedConcealment", -1f)),
            (int)Math.Round(Setting("HardenedConcealment", -1f)),
            (int)Math.Round(Setting("RegularConcealment", 0f)),
            (int)Math.Round(Setting("VeteranConcealment", 1f)),
            Setting("IndividualReliability", true),
            Setting("EnemyReliability", true),
            Setting("AlliedNpcReliability", true),
            Setting("DetailedNpcProfiles", true),
            Math.Max(0, (int)Math.Round(Setting("GreenNoFirePct", 10f))),
            Math.Max(0, (int)Math.Round(Setting("CombatTestedNoFirePct", 5f))),
            Math.Max(0, (int)Math.Round(Setting("HardenedNoFirePct", 3f))),
            Math.Max(0, (int)Math.Round(Setting("RegularNoFirePct", 2f))),
            Math.Max(0, (int)Math.Round(Setting("SuppressedNoFirePct", 5f))),
            Setting("ShowFailedToFireText", true),
            Setting("ShowVeterancyMarkers", true),
            Setting("MalfunctionDiagnostics", false),
            Setting("DebugLoggingV2", false));

        try { _runtime.Update(); }
        catch (Exception e) { Log("Update failed: " + e); }
    }

    public void OnGUI()
    {
        if (_runtime == null || !Setting("EnableProbe", true) || !Setting("ShowVeterancyRoster", true)) return;

        bool tactical = _sceneName.Equals("Tactical", StringComparison.OrdinalIgnoreCase);
        bool planning = _sceneName.Equals("MissionPreparation", StringComparison.OrdinalIgnoreCase);
        if (!tactical && !planning) return;

        Event ev = Event.current;
        if (ev != null && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.V
            && (ev.modifiers & EventModifiers.Control) != 0)
        {
            _rosterOpen = !_rosterOpen;
            if (_rosterOpen) _rosterScroll = Vector2.zero;
            ev.Use();
        }

        EnsureRosterWindowRect();

        if (!_rosterOpen)
        {
            // Keep the launcher in exactly the same place in Mission Preparation
            // and Tactical so the roster has one consistent access point.
            var buttonRect = new Rect(Math.Max(12f, Screen.width - 188f), 72f, 168f, 30f);
            if (GUI.Button(buttonRect, "Veterancy  [Ctrl+V]"))
            {
                _rosterOpen = true;
                _rosterScroll = Vector2.zero;
            }
            return;
        }

        HandleRosterDrag();
        DrawVeterancyRosterPanel(tactical, planning);
    }

    private void EnsureRosterWindowRect()
    {
        float width = Math.Min(720f, Math.Max(560f, Screen.width * 0.42f));
        float height = Math.Min(760f, Math.Max(420f, Screen.height * 0.76f));
        if (_rosterWindowInitialized
            && _rosterWindowRect.width <= Screen.width
            && _rosterWindowRect.height <= Screen.height) return;

        _rosterWindowRect = new Rect(
            Math.Max(12f, Screen.width - width - 24f),
            Math.Max(48f, (Screen.height - height) * 0.5f),
            width,
            height);
        _rosterWindowInitialized = true;
    }

    private void HandleRosterDrag()
    {
        Event ev = Event.current;
        if (ev == null) return;

        Rect dragRect = new Rect(_rosterWindowRect.x, _rosterWindowRect.y, Math.Max(0f, _rosterWindowRect.width - 44f), 30f);

        if (ev.type == EventType.MouseDown && ev.button == 0 && dragRect.Contains(ev.mousePosition))
        {
            _rosterDragging = true;
            _rosterDragOffset = new Vector2(
                ev.mousePosition.x - _rosterWindowRect.x,
                ev.mousePosition.y - _rosterWindowRect.y);
            ev.Use();
            return;
        }

        if (ev.type == EventType.MouseDrag && ev.button == 0 && _rosterDragging)
        {
            float maxX = Math.Max(12f, Screen.width - _rosterWindowRect.width - 12f);
            float maxY = Math.Max(12f, Screen.height - _rosterWindowRect.height - 12f);
            _rosterWindowRect.x = Mathf.Clamp(ev.mousePosition.x - _rosterDragOffset.x, 12f, maxX);
            _rosterWindowRect.y = Mathf.Clamp(ev.mousePosition.y - _rosterDragOffset.y, 12f, maxY);
            ev.Use();
            return;
        }

        if ((ev.type == EventType.MouseUp || ev.rawType == EventType.MouseUp) && _rosterDragging)
        {
            _rosterDragging = false;
            if (ev.type == EventType.MouseUp) ev.Use();
        }
    }

    private void DrawVeterancyRosterPanel(bool tactical, bool planning)
    {
        if (_runtime == null) return;

        Rect window = _rosterWindowRect;
        GUI.Box(window, GUIContent.none);

        GUI.Label(new Rect(window.x + 12f, window.y + 5f, window.width - 54f, 22f),
            planning ? "Veterancy - Mission Planning" : "Veterancy - Deployed Units");
        if (GUI.Button(new Rect(window.xMax - 34f, window.y + 4f, 24f, 22f), "X"))
        {
            _rosterOpen = false;
            return;
        }

        float infoY = window.y + 32f;
        GUI.Label(new Rect(window.x + 12f, infoY, window.width - 24f, 20f),
            tactical ? "Deployed infantry squads and vehicle crews - XP and rank" : "Player infantry squads and vehicle crews - XP and rank");
        GUI.Label(new Rect(window.x + 12f, infoY + 20f, window.width - 24f, 20f),
            "Ctrl+V toggles this roster. Vehicle rank uses active abstract crew only; the native Pilot/leader is not part of crew XP.");

        float footerY = window.yMax - 34f;
        Rect viewport = new Rect(window.x + 12f, infoY + 46f, window.width - 24f, Math.Max(80f, footerY - (infoY + 52f)));

        var squads = _runtime.SquadMappingReady
            ? _runtime.GetRosterSquads(deployedOnly: tactical)
            : Array.Empty<VeterancySquadView>();
        var pilots = _runtime.GetPilotRoster(deployedOnly: tactical);

        if (!_runtime.SquadMappingReady && pilots.Count == 0)
        {
            GUI.Label(new Rect(viewport.x, viewport.y + 12f, viewport.width, 22f),
                "Veterancy roster is still loading. It will populate automatically.");
        }
        else if (squads.Count == 0 && pilots.Count == 0)
        {
            GUI.Label(new Rect(viewport.x, viewport.y + 12f, viewport.width, 22f),
                tactical ? "Waiting for deployed player squads / vehicles..." : "No living player squads or vehicle crews are currently available.");
        }
        else
        {
            float totalHeight = MeasureRosterHeight(squads, pilots);
            HandleRosterScroll(viewport, totalHeight);
            DrawRosterContents(viewport, squads, pilots, _rosterScroll.y);
        }

        GUI.Label(new Rect(window.x + 12f, footerY + 4f, window.width - 180f, 22f),
            "Ranks: Green 0-249 | Combat Tested 250-499 | Hardened 500-999 | Regular 1000-1999 | Veteran 2000-3999 | Crack 4000-7999 | Elite 8000+");
        if (GUI.Button(new Rect(window.xMax - 86f, footerY + 2f, 74f, 24f), "Close"))
            _rosterOpen = false;
    }

    private void HandleRosterScroll(Rect viewport, float totalHeight)
    {
        float maxScroll = Math.Max(0f, totalHeight - viewport.height);
        _rosterScroll.y = Mathf.Clamp(_rosterScroll.y, 0f, maxScroll);

        Event ev = Event.current;
        if (ev != null && ev.type == EventType.ScrollWheel && viewport.Contains(ev.mousePosition))
        {
            _rosterScroll.y = Mathf.Clamp(_rosterScroll.y + ev.delta.y * 28f, 0f, maxScroll);
            ev.Use();
        }

        if (maxScroll <= 0f) return;

        float bx = viewport.xMax - 66f;
        if (GUI.Button(new Rect(bx, viewport.y + 2f, 62f, 22f), "Up"))
            _rosterScroll.y = Mathf.Clamp(_rosterScroll.y - Math.Max(80f, viewport.height * 0.65f), 0f, maxScroll);
        if (GUI.Button(new Rect(bx, viewport.yMax - 24f, 62f, 22f), "Down"))
            _rosterScroll.y = Mathf.Clamp(_rosterScroll.y + Math.Max(80f, viewport.height * 0.65f), 0f, maxScroll);

        string scrollText = $"{(int)_rosterScroll.y}/{(int)maxScroll}";
        GUI.Label(new Rect(bx - 8f, viewport.y + 26f, 70f, 20f), scrollText);
    }

    private static float MeasureRosterHeight(IReadOnlyList<VeterancySquadView> squads, IReadOnlyList<PilotVeterancyView> pilots)
    {
        float h = 0f;
        foreach (var squad in squads)
            h += 104f + squad.Members.Count * 22f + 8f;
        if (pilots.Count > 0)
        {
            h += 34f;
            foreach (var pilot in pilots) h += MeasurePilotBlockHeight(pilot) + 8f;
        }
        return h;
    }

    private static float MeasurePilotBlockHeight(PilotVeterancyView pilot)
    {
        int rows = Math.Max(1, (pilot.Crew.Count + 1) / 2);
        return 66f + rows * 20f;
    }

    private void DrawRosterContents(Rect viewport, IReadOnlyList<VeterancySquadView> squads, IReadOnlyList<PilotVeterancyView> pilots, float scrollY)
    {
        float y = viewport.y - scrollY;
        float contentWidth = viewport.width - 74f;

        foreach (var squad in squads)
        {
            float blockHeight = 104f + squad.Members.Count * 22f;
            Rect block = new Rect(viewport.x, y, contentWidth, blockHeight);
            DrawSquadBlockRaw(viewport, block, squad);
            y += blockHeight + 8f;
        }

        if (pilots.Count > 0)
        {
            DrawVisibleLabel(viewport, new Rect(viewport.x + 4f, y + 5f, contentWidth - 8f, 22f), "VEHICLE CREWS");
            y += 34f;
            foreach (var pilot in pilots)
            {
                float blockHeight = MeasurePilotBlockHeight(pilot);
                Rect block = new Rect(viewport.x, y, contentWidth, blockHeight);
                DrawPilotBlockRaw(viewport, block, pilot);
                y += blockHeight + 8f;
            }
        }
    }

    private void DrawPilotBlockRaw(Rect viewport, Rect block, PilotVeterancyView pilot)
    {
        if (block.yMax < viewport.y || block.y > viewport.yMax) return;

        Rect clippedBox = IntersectRect(block, viewport);
        if (clippedBox.width > 0f && clippedBox.height > 0f)
            GUI.Box(clippedBox, GUIContent.none);

        float x = block.x + 8f;
        float y = block.y + 6f;
        float w = block.width - 16f;
        int next = XpToNext(pilot.Xp, pilot.Rank);
        string progress = pilot.Rank == VeterancyRank.Elite
            ? "Max rank"
            : $"{next} XP to {VeterancyRules.NextRankName(pilot.Rank)}";

        DrawVisibleLabel(viewport, new Rect(x, y, 200f, 20f), $"Leader: {pilot.PilotName}");
        DrawVisibleLabel(viewport, new Rect(x + 202f, y, 122f, 20f), VeterancyRules.DisplayName(pilot.Rank));
        DrawVisibleLabel(viewport, new Rect(x + 326f, y, 104f, 20f), $"avg {pilot.Xp} XP");
        DrawVisibleLabel(viewport, new Rect(x + 432f, y, Math.Max(40f, w - 432f), 20f), progress);
        string reserve = pilot.ReserveCrewCount > 0 ? $" | Reserve: {pilot.ReserveCrewCount}" : "";
        DrawVisibleLabel(viewport, new Rect(x, y + 22f, w, 20f), $"Vehicle: {pilot.VehicleName} | Active crew: {pilot.Crew.Count}{reserve}");

        float rowY = y + 43f;
        float half = w * 0.5f;
        for (int i = 0; i < pilot.Crew.Count; i++)
        {
            var m = pilot.Crew[i];
            string status = string.Equals(m.Status, "Healthy", StringComparison.OrdinalIgnoreCase) ? "H"
                : string.Equals(m.Status, "Wounded", StringComparison.OrdinalIgnoreCase) ? "W" : "R";
            string text = $"{m.Role}: {m.Xp} XP [{status}]";
            int col = i % 2;
            int row = i / 2;
            DrawVisibleLabel(viewport, new Rect(x + col * half, rowY + row * 20f, half, 20f), text);
        }
        if (pilot.Crew.Count == 0)
            DrawVisibleLabel(viewport, new Rect(x, rowY, w, 20f), "No active abstract crew (no vehicle assigned)." );

        int rows = Math.Max(1, (pilot.Crew.Count + 1) / 2);
        float barY = rowY + rows * 20f + 2f;
        DrawProgressBarRaw(viewport, new Rect(x, barY, w, 8f), pilot.Xp, pilot.Rank, RankColor(pilot.Rank));
    }

    private void DrawSquadBlockRaw(Rect viewport, Rect block, VeterancySquadView squad)
    {
        if (block.yMax < viewport.y || block.y > viewport.yMax) return;

        Rect clippedBox = IntersectRect(block, viewport);
        if (clippedBox.width > 0f && clippedBox.height > 0f)
            GUI.Box(clippedBox, GUIContent.none);

        Color oldColor = GUI.color;
        Color rankColor = RankColor(squad.Rank);
        string squadName = squad.SquadNumber > 0 ? $"Squad {squad.SquadNumber}" : "Unassigned squad";
        int next = XpToNext(squad.AverageXp, squad.Rank);
        string progress = squad.Rank == VeterancyRank.Elite
            ? "Max rank"
            : $"{next} XP to {VeterancyRules.NextRankName(squad.Rank)}";

        float x = block.x + 8f;
        float y = block.y + 6f;
        float w = block.width - 16f;

        DrawVisibleLabel(viewport, new Rect(x, y, 110f, 20f), squadName);
        DrawVisibleLabel(viewport, new Rect(x + 112f, y, 122f, 20f), VeterancyRules.DisplayName(squad.Rank));
        DrawVisibleLabel(viewport, new Rect(x + 236f, y, 104f, 20f), $"Avg {squad.AverageXp:0} XP");
        DrawVisibleLabel(viewport, new Rect(x + 342f, y, Math.Max(40f, w - 342f), 20f), progress);

        DrawVisibleLabel(viewport, new Rect(x, y + 22f, w, 20f), $"Squad Leader: {squad.LeaderName}");
        DrawProgressBarRaw(viewport, new Rect(x, y + 45f, w, 8f), squad.AverageXp, squad.Rank, rankColor);

        float rowY = y + 58f;
        DrawVisibleLabel(viewport, new Rect(x, rowY, 260f, 20f), "Squaddie");
        DrawVisibleLabel(viewport, new Rect(x + 262f, rowY, 70f, 20f), "XP");
        DrawVisibleLabel(viewport, new Rect(x + 334f, rowY, 120f, 20f), "Rank");
        DrawVisibleLabel(viewport, new Rect(x + 456f, rowY, Math.Max(40f, w - 456f), 20f), "Progress");
        rowY += 22f;

        foreach (var member in squad.Members)
        {
            string name = string.IsNullOrWhiteSpace(member.Nickname)
                ? member.Name
                : $"{member.Nickname} ({member.Name})";
            int memberNext = XpToNext(member.Xp, member.Rank);
            string memberProgress = member.Rank == VeterancyRank.Elite
                ? "Max rank"
                : $"{memberNext} to {VeterancyRules.NextRankName(member.Rank)}";

            DrawVisibleLabel(viewport, new Rect(x, rowY, 260f, 20f), name);
            DrawVisibleLabel(viewport, new Rect(x + 262f, rowY, 70f, 20f), member.Xp.ToString());
            DrawVisibleLabel(viewport, new Rect(x + 334f, rowY, 120f, 20f), VeterancyRules.DisplayName(member.Rank));
            DrawVisibleLabel(viewport, new Rect(x + 456f, rowY, Math.Max(40f, w - 456f), 20f), memberProgress);
            rowY += 22f;
        }

        GUI.color = oldColor;
    }

    private static void DrawProgressBarRaw(Rect viewport, Rect outer, double xp, VeterancyRank rank, Color rankColor)
    {
        Rect clippedOuter = IntersectRect(outer, viewport);
        if (clippedOuter.width <= 0f || clippedOuter.height <= 0f) return;

        GUI.Box(clippedOuter, GUIContent.none);
        float fraction = (float)VeterancyRules.Progress01(xp, rank);
        Rect inner = new Rect(outer.x + 1f, outer.y + 1f, Math.Max(0f, (outer.width - 2f) * fraction), Math.Max(1f, outer.height - 2f));
        inner = IntersectRect(inner, viewport);
        if (inner.width <= 0f || inner.height <= 0f) return;

        Color old = GUI.color;
        GUI.color = rankColor;
        GUI.Box(inner, GUIContent.none);
        GUI.color = old;
    }

    private static void DrawVisibleLabel(Rect viewport, Rect rect, string text)
    {
        if (rect.yMax < viewport.y || rect.y > viewport.yMax) return;
        GUI.Label(rect, text);
    }

    private static Rect IntersectRect(Rect a, Rect b)
    {
        float xMin = Math.Max(a.xMin, b.xMin);
        float yMin = Math.Max(a.yMin, b.yMin);
        float xMax = Math.Min(a.xMax, b.xMax);
        float yMax = Math.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin) return new Rect(0f, 0f, 0f, 0f);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static int XpToNext(double xp, VeterancyRank rank)
    {
        if (rank == VeterancyRank.Elite) return 0;
        return Math.Max(0, (int)Math.Ceiling(VeterancyRules.NextThreshold(rank) - xp));
    }

    private static Color RankColor(VeterancyRank rank) => rank switch
    {
        VeterancyRank.CombatTested => new Color(0.68f, 0.52f, 0.25f, 1f),
        VeterancyRank.Hardened => new Color(0.78f, 0.80f, 0.84f, 1f),
        VeterancyRank.Regular => new Color(0.78f, 0.80f, 0.84f, 1f),
        VeterancyRank.Veteran => new Color(0.91f, 0.71f, 0.20f, 1f),
        VeterancyRank.Crack => new Color(0.91f, 0.71f, 0.20f, 1f),
        VeterancyRank.Elite => new Color(0.91f, 0.71f, 0.20f, 1f),
        _ => new Color(0.36f, 0.40f, 0.20f, 1f)
    };

    private T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private void DrawDebug(Rect area)
    {
        float x = area.x;
        float y = area.y;
        float w = area.width;
        const float line = 20f;

        void Label(string text)
        {
            GUI.Label(new Rect(x, y, w, line), text);
            y += line;
        }

        Label($"Squaddie Veterancy {Version} - infantry + vehicle crew veterancy");
        Label("Ranks: Green 0-249 | Combat Tested 250-499 | Hardened 500-999 | Regular 1000-1999 | Veteran 2000-3999 | Crack 4000-7999 | Elite 8000+");
        Label("Infantry XP: attack +1 | kill +5 | targeted +10 | primary +25 | secondary +10.");
        Label("Vehicle crew XP: attack +1 | targeted +2 | infantry kill +5 | enemy vehicle +50 | mission +10 | survived +10 | objectives +25/+10.");
        Label("Vehicle crews: persistent loadout-derived personnel pool. Destroyed vehicle rolls survival per active member; survivors are wounded, deaths become 0-XP replacements.");
        Label("Default Accuracy / Defense / Suppression modifiers: Green -6%, Combat Tested -4%, Hardened -2%, Regular 0%, Veteran +2%, Crack +4%, Elite +6%. HP and concealment still cap at Veteran. Reliability remains individual.");
        Label("Suppressed adds +5% no-fire by default; Pinned is intentionally left to MENACE native behavior.");
        Label("HUD veterancy: Green bar | Combat Tested brass ^ | Hardened silver ^ | Regular silver ^^ | Veteran gold ^ | Crack gold ^^ | Elite gold ^^^.");
        Label("NPC ranks are battle-local for human infantry and human-crewed enemy vehicles. NPC rank also affects accuracy, defense and suppression resistance; vehicle crews do not use infantry no-fire rolls. Constructs and Wildlife stay excluded.");

        if (_runtime == null)
        {
            Label("State: not bound yet");
            return;
        }

        Label($"State: {_runtime.Status} | hooks={_runtime.PatchedMethodCount} | squaddies={_runtime.CachedSquaddieCount} | pilots={_runtime.CachedPilotCount} | squads={(_runtime.SquadMappingReady ? "mapped" : "fallback")}");
        Label($"XP awards this session: {_runtime.XpAwards} | squaddie records={_runtime.PersistedRecordCount} | crew records={_runtime.PersistedPilotRecordCount} | unsaved XP={(_runtime.HasUnsavedXp ? "YES" : "no")}");
        Label($"Save: {_runtime.CurrentSaveLabel} | watcher={(_runtime.SaveWatcherReady ? "ready" : "waiting")}");
        Label($"Reliability: rolls={_runtime.ReliabilityRolls} | no-fire={_runtime.ReliabilityNoFires} | filteredLists={_runtime.ReliabilityFilteredLists} | adjustedCounts={_runtime.ReliabilityAdjustedCounts} | npcRanks={_runtime.NpcRankAssignments} | enemyRolls={_runtime.EnemyReliabilityRolls} | enemyNoFire={_runtime.EnemyReliabilityNoFires} | alliedRolls={_runtime.AlliedNpcReliabilityRolls} | alliedNoFire={_runtime.AlliedNpcReliabilityNoFires}");
        Label($"Native malfunction diagnostics: elementContexts={_runtime.MalfunctionElementEvents} | jamChecks={_runtime.JamChecks} | nativeEvents={_runtime.NativeMalfunctions}");
        Label("XP snapshot: " + _runtime.PersistencePath);

        if (GUI.Button(new Rect(x, y, 130f, 22f), "Snapshot sidecar"))
            _runtime.SaveXpNow();

        if (!_confirmReset)
        {
            if (GUI.Button(new Rect(x + 140f, y, 120f, 22f), "Reset XP...")) _confirmReset = true;
        }
        else
        {
            if (GUI.Button(new Rect(x + 140f, y, 120f, 22f), "Confirm reset"))
            {
                _runtime.ResetXp();
                _confirmReset = false;
            }
            if (GUI.Button(new Rect(x + 268f, y, 70f, 22f), "Cancel")) _confirmReset = false;
        }
        y += 28f;

        var rows = _runtime.GetRosterRows();
        int rowsPerPage = Math.Max(5, (int)Math.Floor((area.yMax - y - 48f) / line) - 1);
        int pages = Math.Max(1, (rows.Count + rowsPerPage - 1) / rowsPerPage);
        _rosterPage = Math.Max(0, Math.Min(_rosterPage, pages - 1));

        Label("ID | Squaddie | XP | Rank | Squad avg/rank | HP | Acc | Def | Sup | Con");
        int start = _rosterPage * rowsPerPage;
        int end = Math.Min(rows.Count, start + rowsPerPage);
        for (int i = start; i < end; i++)
        {
            var r = rows[i];
            string squad = r.SquadNumber > 0 ? r.SquadNumber.ToString() : "?";
            string nick = string.IsNullOrWhiteSpace(r.Nickname) ? r.Name : $"{r.Nickname} ({r.Name})";
            string life = r.Alive ? "" : " [DEAD]";
            Label($"#{r.Id}{life} | {nick} | {r.Xp} XP | {VeterancyRules.DisplayName(r.Rank)} | S{squad} {r.SquadAverageXp:0.0} {VeterancyRules.DisplayName(r.SquadRank)} | HP +{r.SquadHpBonus} | Acc {r.SquadAccuracyPct:+#;-#;0}% | Def {r.SquadDefensePct:+#;-#;0}% | Sup {r.SquadSuppressionResistancePct:+#;-#;0}% | Con {r.SquadConcealment:+#;-#;0}");
        }

        y = Math.Min(y + 4f, area.yMax - 24f);
        if (GUI.Button(new Rect(x, y, 80f, 22f), "Previous") && _rosterPage > 0) _rosterPage--;
        GUI.Label(new Rect(x + 90f, y, 120f, 22f), $"Page {_rosterPage + 1}/{pages}");
        if (GUI.Button(new Rect(x + 200f, y, 80f, 22f), "Next") && _rosterPage + 1 < pages) _rosterPage++;
    }

    private void Log(string text)
    {
        if (!_debugLogging || _logger == null) return;

        // Debug logging is intentionally capped. Several veterancy hooks sit on very hot
        // query paths (hitchance, concealment, reliability), and an unrestricted trace can
        // generate hundreds of thousands of lines in a single mission.
        long now = Environment.TickCount64;
        if (_debugLogWindowStartedMs == 0 || now - _debugLogWindowStartedMs >= 1000)
        {
            if (_debugLogSuppressedInWindow > 0)
                _logger.Msg($"[SquaddieProbe] DEBUG RATE LIMIT: suppressed {_debugLogSuppressedInWindow} trace line(s) in the previous second.");
            _debugLogWindowStartedMs = now;
            _debugLogLinesInWindow = 0;
            _debugLogSuppressedInWindow = 0;
        }

        if (_debugLogLinesInWindow >= MaxDebugLogLinesPerSecond)
        {
            _debugLogSuppressedInWindow++;
            return;
        }

        _debugLogLinesInWindow++;
        _logger.Msg("[SquaddieProbe] " + text);
    }
}
