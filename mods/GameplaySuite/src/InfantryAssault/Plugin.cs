using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("0.5.2.0")]
[assembly: AssemblyFileVersion("0.5.2.0")]

namespace MenaceInfantryAssault;

public sealed class Plugin : IModpackPlugin
{
    private const string SettingsGroup = "Infantry Assault";
    private const string Version = "0.5.2";
    private const int AssaultApCost = 40;
    private const string AssaultSkillId = "mod.infantry_assault";

    private static MelonLogger.Instance? _logger;
    private static global::HarmonyLib.Harmony? _harmony;
    private static Assembly? _game;
    private static bool _inTacticalScene;
    private static bool _patchesApplied;
    private static bool _templatePrepared;
    private static bool _assaultBusy;
    private static int? _blockedPlayerAssaultActionKey;
    private static bool _verbose;
    private static bool _enabled = true;
    private static bool _enableAi = true;
    private static float _assaultCoverPercent = 50f;
    private static float _cqbAccuracyBonusPercent = 20f;
    private static float _aiAggressiveness = 100f;

    private static Type? _skillType;
    private static Type? _actorType;
    private static Type? _entityType;
    private static Type? _tileType;
    private static Type? _skillTemplateType;
    private static Type? _skillContainerType;
    private static Type? _dataTemplateLoaderType;
    private static Type? _agentType;
    private static Type? _tacticalManagerType;
    private static Type? _tacticalStateType;
    private static Type? _movementActionType;
    private static Type? _movementFlagsType;
    private static Type? _usageParameterType;
    private static Type? _skillActionType;

    private static MethodInfo? _skillGetId;
    private static MethodInfo? _skillGetActor;
    private static MethodInfo? _skillGetApConsumer;
    private static MethodInfo? _skillGetItem;
    private static MethodInfo? _skillGetTemplate;
    private static MethodInfo? _skillIsAttack;
    private static MethodInfo? _skillIsOutOfUses;
    private static MethodInfo? _skillGetActionPointCost;
    private static MethodInfo? _skillGetMinRange;
    private static MethodInfo? _skillGetMaxRange;
    private static MethodInfo? _skillUse;
    private static MethodInfo? _skillGetCoverMult;
    private static MethodInfo? _skillOnVerifyTarget;

    private static MethodInfo? _entityGetTile;
    private static MethodInfo? _entityGetContainedEntity;
    private static MethodInfo? _entityGetContainerEntity;
    private static MethodInfo? _entityGetFactionId;
    private static MethodInfo? _entityIsInfantry;
    private static MethodInfo? _entityIsAlive;
    private static MethodInfo? _entityIsAlliedWithEntity;
    private static MethodInfo? _entityGetElements;
    private static MethodInfo? _entityGetHitpoints;
    private static MethodInfo? _entityGetArmorDurability;
    private static MethodInfo? _entityGetCurrentProperties;
    private static MethodInfo? _entityGetTemplate;

    private static MethodInfo? _actorIsInfantry;
    private static MethodInfo? _actorGetActionPoints;
    private static MethodInfo? _actorGetSuppression;
    private static MethodInfo? _actorGetMorale;
    private static MethodInfo? _actorSetActionPointsUnsafe;
    private static MethodInfo? _actorIsDying;
    private static MethodInfo? _actorIsStunned;
    private static MethodInfo? _actorIsMoving;
    private static MethodInfo? _actorIsDeployed;
    private static MethodInfo? _actorGetStance;
    private static MethodInfo? _actorGetSuppressionState;
    private static MethodInfo? _actorMoveTo;
    private static FieldInfo? _actorHasActedField;

    private static MethodInfo? _tileGetEntity;
    private static MethodInfo? _tileGetDistanceTo;
    private static MethodInfo? _tileGetSurfaceType;


    private static MethodInfo? _tacticalGet;
    private static MethodInfo? _tacticalIsSkillBusy;
    private static MethodInfo? _tacticalGetFactions;
    private static MethodInfo? _tacticalInvokeOnEntitySpawned;
    private static MethodInfo? _tacticalStateGet;
    private static MethodInfo? _tacticalStateClearCurrentAction;
    private static MethodInfo? _tacticalStateCancelCurrentAction;
    private static MethodInfo? _tacticalStateGetCurrentAction;

    private static MethodInfo? _agentGetActor;
    private static MethodInfo? _agentExecute;

    private static MethodInfo? _skillActionGetSkill;
    private static MethodInfo? _skillActionCancelWithoutCancelAim;

    private static object? _assaultTemplate;

    // The IL2CPP lists are declared as List<BaseSkill>, and enumeration can create
    // a new managed wrapper for the same native Skill every time. Cache native
    // identity only; RuntimeHelpers.GetHashCode(wrapper) is not stable across casts.
    private static readonly HashSet<IntPtr> AssaultSkillPointers = new();
    private static readonly Dictionary<int, object> LastTargetBySkill = new();

    private enum AssaultPhase
    {
        None,
        DefensiveFire,
        AssaultFire
    }

    private static AssaultPhase _phase;
    private static float _phaseCqbBonusScale = 1f;
    private static int _phaseWeaponKey;
    private static int _activeActorKey;
    private static string _lastStatus = "Idle";
    private static int _assaultsStarted;
    private static int _aiAssaultsStarted;

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("EnableGameplay", "Enable Infantry Assault", true);
            s.AddSlider("AssaultCover", "Target cover effectiveness during CQB (%)", 0f, 100f, 50f);
            s.AddSlider("CQBAccuracyBonus", "CQB hit chance bonus (percentage points)", 0f, 50f, 20f);

            s.AddHeader("AI");
            s.AddToggle("EnableAI", "AI can use infantry assaults", true);
            s.AddSlider("AIAggressiveness", "AI assault aggressiveness (%)", 50f, 200f, 100f);

            s.AddHeader("Diagnostics");
            s.AddToggle("VerboseLogging", "Debug logging", false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel(SettingsGroup, DrawDebug);
        Log($"{Version}: initialized. Adjacent infantry assaults, garrison assaults, defensive fire, and AI use enabled.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _inTacticalScene = string.Equals(sceneName, "Tactical", StringComparison.Ordinal);
        _assaultBusy = false;
        _blockedPlayerAssaultActionKey = null;
        _phase = AssaultPhase.None;
        _phaseCqbBonusScale = 1f;
        _phaseWeaponKey = 0;
        _activeActorKey = 0;
        LastTargetBySkill.Clear();
        AssaultSkillPointers.Clear();

        if (!_inTacticalScene) return;
        MelonCoroutines.Start(DelayedTacticalSetup());
    }

    public void OnUpdate()
    {
        ReloadSettings();
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel(SettingsGroup);
        AssaultSkillPointers.Clear();
        LastTargetBySkill.Clear();
        _assaultBusy = false;
        _blockedPlayerAssaultActionKey = null;
        _phase = AssaultPhase.None;
        _phaseCqbBonusScale = 1f;
        _phaseWeaponKey = 0;
    }

    private static IEnumerator DelayedTacticalSetup()
    {
        for (int i = 0; i < 8; i++) yield return null;
        BindAndPatch();
        if (_patchesApplied)
        {
            PrepareAssaultTemplate();
            ScanAssaultSkills();

            // Skills are granted by EntityTemplate before actor spawn. Run a second diagnostic
            // pass after tactical actors have settled so the log confirms both player and AI
            // infantry actually received the manifest-defined action.
            for (int i = 0; i < 60 && _inTacticalScene; i++) yield return null;
            if (_inTacticalScene)
            {
                EnsureAssaultSkillsOnAllActors();
                ScanAssaultSkills();
            }
        }
    }

    private static void BindAndPatch()
    {
        if (_patchesApplied) return;

        _game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (_game == null)
        {
            Error("Assembly-CSharp not found.");
            return;
        }

        _skillType = FindType("Menace.Tactical.Skills.Skill", "Skill");
        _actorType = FindType("Menace.Tactical.Actor", "Actor");
        _entityType = FindType("Menace.Tactical.Entity", "Entity");
        _tileType = FindType("Menace.Tactical.Tile", "Tile");
        _skillTemplateType = FindType("Menace.Tactical.Skills.SkillTemplate", "SkillTemplate");
        _skillContainerType = FindType("Menace.Tactical.Skills.SkillContainer", "SkillContainer");
        _dataTemplateLoaderType = FindType("DataTemplateLoader", "DataTemplateLoader");
        _agentType = FindType("Menace.Tactical.AI.Agent", "Agent");
        _tacticalManagerType = FindType("Menace.Tactical.TacticalManager", "TacticalManager");
        _tacticalStateType = FindType("Menace.States.TacticalState", "TacticalState");
        _movementActionType = FindType("Menace.Tactical.MovementAction", "MovementAction");
        _movementFlagsType = FindType("Menace.Tactical.MovementFlags", "MovementFlags");
        _usageParameterType = FindType("Menace.Tactical.Skills.UsageParameter", "UsageParameter");
        _skillActionType = FindType("Menace.States.SkillAction", "SkillAction");

        if (_skillType == null || _actorType == null || _entityType == null || _tileType == null ||
            _skillTemplateType == null || _skillContainerType == null || _agentType == null || _tacticalManagerType == null)
        {
            Error("Required MENACE tactical types could not be resolved.");
            return;
        }

        _skillGetId = Method(_skillType, "GetID", 0);
        _skillGetActor = Method(_skillType, "GetActor", 0);
        _skillGetApConsumer = Method(_skillType, "GetAPConsumer", 0);
        _skillGetItem = Method(_skillType, "GetItem", 0);
        _skillGetTemplate = Method(_skillType, "GetTemplate", 0);
        _skillIsAttack = Method(_skillType, "IsAttack", 0);
        _skillIsOutOfUses = Method(_skillType, "IsOutOfUses", 0);
        _skillGetActionPointCost = Method(_skillType, "GetActionPointCost", 0);
        _skillGetMinRange = Method(_skillType, "GetMinRange", 0);
        _skillGetMaxRange = Method(_skillType, "GetMaxRange", 0);
        _skillUse = Method(_skillType, "Use", 2);
        _skillGetCoverMult = _skillType.GetMethods(AllInstance).FirstOrDefault(m => m.Name == "GetCoverMult" && m.ReturnType == typeof(float));
        _skillOnVerifyTarget = Method(_skillType, "OnVerifyTarget", 3);

        _entityGetTile = Method(_entityType, "GetTile", 0);
        _entityGetContainedEntity = Method(_entityType, "GetContainedEntity", 0);
        _entityGetContainerEntity = Method(_entityType, "GetContainerEntity", 0);
        _entityGetFactionId = Method(_entityType, "GetFactionID", 0);
        _entityIsInfantry = Method(_entityType, "IsInfantry", 0);
        _entityIsAlive = Method(_entityType, "IsAlive", 0);
        _entityIsAlliedWithEntity = _entityType.GetMethods(AllInstance)
            .FirstOrDefault(m => m.Name == "IsAlliedWith" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == _entityType);
        _entityGetElements = Method(_entityType, "GetElements", 0);
        _entityGetHitpoints = Method(_entityType, "GetHitpoints", 0);
        _entityGetArmorDurability = Method(_entityType, "GetArmorDurability", 0);
        _entityGetCurrentProperties = Method(_entityType, "GetCurrentProperties", 0);
        _entityGetTemplate = Method(_entityType, "GetTemplate", 0);

        _actorIsInfantry = Method(_actorType, "IsInfantry", 0);
        _actorGetActionPoints = Method(_actorType, "GetActionPoints", 0);
        _actorGetSuppression = Method(_actorType, "GetSuppression", 0);
        _actorGetMorale = Method(_actorType, "GetMorale", 0);
        _actorSetActionPointsUnsafe = Method(_actorType, "SetActionPointsUnsafe", 1);
        _actorIsDying = Method(_actorType, "IsDying", 0);
        _actorIsStunned = Method(_actorType, "IsStunned", 0);
        _actorIsMoving = Method(_actorType, "IsMoving", 0);
        _actorIsDeployed = Method(_actorType, "IsDeployed", 0);
        _actorGetStance = Method(_actorType, "GetStance", 0);
        _actorGetSuppressionState = Method(_actorType, "GetSuppressionState", 0);
        _actorMoveTo = _actorType.GetMethods(AllInstance).FirstOrDefault(m => m.Name == "MoveTo" && m.GetParameters().Length == 3);
        _actorHasActedField = _actorType.GetField("m_HasActed", BindingFlags.NonPublic | BindingFlags.Instance);

        _tileGetEntity = Method(_tileType, "GetEntity", 0);
        _tileGetDistanceTo = Method(_tileType, "GetDistanceTo", 1);
        _tileGetSurfaceType = Method(_tileType, "GetSurfaceType", 0);


        _tacticalGet = Method(_tacticalManagerType, "Get", 0, isStatic: true);
        _tacticalIsSkillBusy = Method(_tacticalManagerType, "IsSkillBusy", 0, isStatic: true);
        _tacticalGetFactions = Method(_tacticalManagerType, "GetFactions", 0);
        _tacticalInvokeOnEntitySpawned = Method(_tacticalManagerType, "InvokeOnEntitySpawned", 1);

        if (_tacticalStateType != null)
        {
            _tacticalStateGet = Method(_tacticalStateType, "Get", 0, isStatic: true);
            _tacticalStateClearCurrentAction = Method(_tacticalStateType, "ClearCurrentAction", 0);
            _tacticalStateCancelCurrentAction = Method(_tacticalStateType, "CancelCurrentAction", 0);
            _tacticalStateGetCurrentAction = Method(_tacticalStateType, "GetCurrentAction", 0);
        }

        _agentGetActor = Method(_agentType, "GetActor", 0);
        _agentExecute = Method(_agentType, "Execute", 0);

        if (_skillActionType != null)
        {
            _skillActionGetSkill = Method(_skillActionType, "GetSkill", 0);
            _skillActionCancelWithoutCancelAim = Method(_skillActionType, "CancelWithoutCancelAim", 0);
        }

        try
        {
            Patch(Method(_skillType, "GetTitle", 0), postfix: nameof(SkillGetTitlePostfix));
            Patch(Method(_skillType, "GetDescription", 0), postfix: nameof(SkillGetDescriptionPostfix));
            Patch(Method(_skillType, "IsVisible", 0), prefix: nameof(SkillIsVisiblePrefix));
            Patch(Method(_skillType, "IsHidden", 0), prefix: nameof(SkillIsHiddenPrefix));
            Patch(_skillGetActionPointCost, prefix: nameof(SkillGetActionPointCostPrefix));
            Patch(_skillGetMinRange, prefix: nameof(SkillGetMinRangePrefix));
            Patch(_skillGetMaxRange, prefix: nameof(SkillGetMaxRangePrefix));
            Patch(Method(_skillType, "GetIdealRange", 0), prefix: nameof(SkillGetIdealRangePrefix));

            // The Assault template is cloned from the native Charge skill only for its UI shell.
            // Do not let Charge's native target/movement applicability reject an occupied enemy tile:
            // an assault deliberately targets an occupied adjacent tile and only moves into it after victory.
            foreach (var applicable in _skillType.GetMethods(AllInstance).Where(m => m.Name == "IsApplicableTo" && m.GetParameters().Length == 2))
                Patch(applicable, prefix: nameof(SkillIsApplicableToPrefix));

            foreach (var inRange in _skillType.GetMethods(AllInstance).Where(m => m.Name == "IsInRange" && (m.GetParameters().Length == 2 || m.GetParameters().Length == 3)))
                Patch(inRange, prefix: nameof(SkillIsInRangePrefix));

            Patch(Method(_skillType, "IsInRangeShape", 1), prefix: nameof(SkillIsInRangeShapePrefix));

            foreach (var usable in _skillType.GetMethods(AllInstance).Where(m => m.Name == "IsUsableOn" && (m.GetParameters().Length == 2 || m.GetParameters().Length == 3)))
                Patch(usable, prefix: nameof(SkillIsUsableOnPrefix));

            // Newer MENACE builds route player target confirmation through Skill.OnVerifyTarget
            // and SkillAction.HandleLeftClickOnTile. Assault owns both checks so the native
            // Charge movement handler can never veto an occupied enemy tile.
            Patch(_skillOnVerifyTarget, prefix: nameof(SkillOnVerifyTargetPrefix));
            if (_skillActionType != null)
            {
                Patch(Method(_skillActionType, "HandleLeftClickOnTile", 2), prefix: nameof(SkillActionHandleLeftClickPrefix));
                Patch(Method(_skillActionType, "HandleMouseMoveOnTile", 4), prefix: nameof(SkillActionHandleMouseMovePrefix));
            }

            Patch(_skillUse, prefix: nameof(SkillUsePrefix));
            Patch(_skillGetCoverMult, postfix: nameof(SkillGetCoverMultPostfix), priority: global::HarmonyLib.Priority.Last);
            // Assault is granted through EntityTemplate.Skills by the modpack manifest, following
            // the same proven pattern as Field Logistics. No post-spawn SkillContainer mutation.
            Patch(_tacticalInvokeOnEntitySpawned, postfix: nameof(TacticalInvokeOnEntitySpawnedPostfix));
            Patch(_agentExecute, prefix: nameof(AgentExecutePrefix));

            _patchesApplied = true;
            Log("Harmony patches applied.");
        }
        catch (Exception e)
        {
            Error("Failed to apply patches: " + e);
        }

        Log($"Resolved API: Skill={_skillType != null}, Actor={_actorType != null}, Tile={_tileType != null}, SkillContainer={_skillContainerType != null}, Agent={_agentType != null}, TacticalManager={_tacticalManagerType != null}, SkillAction={_skillActionType != null}, VerifyTarget={_skillOnVerifyTarget != null}.");
    }

    private static void PrepareAssaultTemplate()
    {
        if (_templatePrepared || _dataTemplateLoaderType == null || _skillTemplateType == null) return;

        try
        {
            MethodInfo? genericGet = _dataTemplateLoaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
            if (genericGet == null)
            {
                Error("DataTemplateLoader.Get<T>() not found.");
                return;
            }

            _assaultTemplate = genericGet.MakeGenericMethod(_skillTemplateType)
                .Invoke(null, new object?[] { AssaultSkillId, true });
            if (_assaultTemplate == null)
            {
                Error($"Manifest-defined Assault SkillTemplate '{AssaultSkillId}' was not found. Template cloning/patching did not apply.");
                return;
            }

            object? handlers = GetMember(_assaultTemplate, "EventHandlers");
            int handlerCount = CollectionCount(handlers);
            if (handlerCount > 0)
            {
                Error($"Refusing to enable Assault: manifest clone still has {handlerCount} native Charge event handler(s).");
                _assaultTemplate = null;
                return;
            }

            _templatePrepared = true;
            Log($"Resolved manifest-defined Assault SkillTemplate '{AssaultSkillId}' with native Charge damage handlers removed.");
        }
        catch (Exception e)
        {
            Error("Could not resolve manifest-defined Assault template: " + e);
        }
    }

    private static object? GetActorSkills(object actor)
    {
        try
        {
            // Use the runtime proxy type, matching the proven Field Logistics reflection pattern.
            // Invoking a MethodInfo cached from a different IL2CPP proxy type caused the 0.1.4
            // "Object does not match target type" failures.
            MethodInfo? runtimeGetSkills = Method(actor.GetType(), "GetSkills", 0);
            if (runtimeGetSkills != null) return runtimeGetSkills.Invoke(actor, null);

            object? direct = GetMember(actor, "SkillContainer") ?? GetMember(actor, "Skills");
            return direct;
        }
        catch (Exception e)
        {
            if (_verbose) Warn("GetActorSkills failed: " + (e.InnerException?.Message ?? e.Message));
            return null;
        }
    }

    private static object? GetAllSkills(object skillContainer)
    {
        try
        {
            MethodInfo? getAll = Method(skillContainer.GetType(), "GetAllSkills", 0);
            return getAll?.Invoke(skillContainer, null)
                ?? GetMember(skillContainer, "Skills")
                ?? GetMember(skillContainer, "m_Skills");
        }
        catch (Exception e)
        {
            if (_verbose) Warn("GetAllSkills failed: " + (e.InnerException?.Message ?? e.Message));
            return null;
        }
    }

    private static object? FindAssaultSkillInContainer(object container)
    {
        try
        {
            // MENACE can queue inherent skills before committing them to the live list.
            // Check both collections so loaded-save synchronization does not create duplicates.
            foreach (string getterName in new[] { "GetAllSkills", "GetSkillsInAddQueue" })
            {
                MethodInfo? getter = Method(container.GetType(), getterName, 0);
                object? skills = getter?.Invoke(container, null);
                if (skills == null) continue;

                foreach (object? skill in EnumerateObjects(skills))
                {
                    if (skill == null) continue;
                    // The list is List<BaseSkill>, not List<Skill>. Use the
                    // runtime wrapper's virtual GetID() rather than a MethodInfo
                    // obtained from the concrete Skill class.
                    if (IsAssaultSkill(skill)) return skill;
                }
            }
        }
        catch { }
        return null;
    }

    public static void SkillGetTitlePostfix(object __instance, ref string __result)
    {
        if (IsAssaultSkill(__instance)) __result = "Assault";
    }

    public static void SkillGetDescriptionPostfix(object __instance, ref string __result)
    {
        if (!IsAssaultSkill(__instance)) return;
        __result = "Assault adjacent enemy infantry. The defender fires first if able. Surviving assaulters then fire at close range with reduced enemy cover. If the defender is destroyed, the assaulters occupy the target position.";
    }

    public static bool SkillGetMinRangePrefix(object __instance, ref int __result)
    {
        if (!IsAssaultSkill(__instance)) return true;
        __result = 1;
        return false;
    }

    public static bool SkillGetIdealRangePrefix(object __instance, ref int __result)
    {
        if (!IsAssaultSkill(__instance)) return true;
        __result = 1;
        return false;
    }

    public static bool SkillGetMaxRangePrefix(object __instance, ref int __result)
    {
        if (!IsAssaultSkill(__instance)) return true;
        __result = 1;
        return false;
    }

    public static bool SkillIsVisiblePrefix(object __instance, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;
        __result = true;
        return false;
    }

    public static bool SkillIsHiddenPrefix(object __instance, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;
        __result = false;
        return false;
    }

    public static bool SkillGetActionPointCostPrefix(object __instance, ref int __result)
    {
        if (!IsAssaultSkill(__instance)) return true;
        __result = AssaultApCost;
        return false;
    }

    public static bool SkillIsApplicableToPrefix(object __instance, object[] __args, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;

        try
        {
            object? targetTile = __args.Length > 0 ? __args[0] : null;
            object? overrideTarget = __args.Length > 1 ? __args[1] : null;
            object? attacker = GetSkillActor(__instance);
            object? userTile = attacker != null ? _entityGetTile?.Invoke(attacker, null) : null;
            if (targetTile != null) LastTargetBySkill[Key(__instance)] = targetTile;
            __result = ValidateAssaultTarget(attacker, targetTile, userTile, overrideTarget, out _);
        }
        catch
        {
            __result = false;
        }
        return false;
    }

    public static bool SkillIsInRangePrefix(object __instance, object[] __args, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;

        try
        {
            object? attacker = GetSkillActor(__instance);
            if (attacker == null || !CanInitiateAssault(attacker))
            {
                __result = false;
                return false;
            }

            // IsInRange(int distance, int additionalRange)
            if (__args.Length >= 1 && __args[0] is int distance)
            {
                __result = distance == 1;
                return false;
            }

            // IsInRange(Tile targetTile, Tile userTile, bool includeAngle = true)
            object? targetTile = __args.Length > 0 ? __args[0] : null;
            object? userTile = __args.Length > 1 ? __args[1] : null;
            if (targetTile != null) LastTargetBySkill[Key(__instance)] = targetTile;
            __result = targetTile != null && userTile != null && Distance(userTile, targetTile) == 1;
        }
        catch
        {
            __result = false;
        }
        return false;
    }

    public static bool SkillIsInRangeShapePrefix(object __instance, object[] __args, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;

        try
        {
            object? targetTile = __args.Length > 0 ? __args[0] : null;
            object? attacker = GetSkillActor(__instance);
            if (attacker == null || !CanInitiateAssault(attacker))
            {
                __result = false;
                return false;
            }
            object? userTile = _entityGetTile?.Invoke(attacker, null);
            if (targetTile != null) LastTargetBySkill[Key(__instance)] = targetTile;
            __result = targetTile != null && userTile != null && Distance(userTile, targetTile) == 1;
        }
        catch
        {
            __result = false;
        }
        return false;
    }

    public static bool SkillIsUsableOnPrefix(object __instance, object[] __args, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;

        try
        {
            object? targetTile = __args.Length > 0 ? __args[0] : null;
            object? userTile = __args.Length > 1 ? __args[1] : null;
            object? attacker = GetSkillActor(__instance);
            if (targetTile != null) LastTargetBySkill[Key(__instance)] = targetTile;
            __result = ValidateAssault(attacker, targetTile, userTile, out _, out _, out _, out _);
        }
        catch
        {
            __result = false;
        }
        return false;
    }

    public static bool SkillOnVerifyTargetPrefix(object __instance, object[] __args, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;

        try
        {
            object? originTile = __args.Length > 0 ? __args[0] : null;
            object? targetTile = __args.Length > 1 ? __args[1] : null;
            object? attacker = GetSkillActor(__instance);
            if (targetTile != null) LastTargetBySkill[Key(__instance)] = targetTile;
            __result = ValidateAssaultTarget(attacker, targetTile, originTile, null, out _);
        }
        catch
        {
            __result = false;
        }
        return false;
    }

    public static bool SkillActionHandleMouseMovePrefix(object __instance)
    {
        // Assault confirmation is fully handled by our click prefix and custom coroutine.
        // If MENACE later invokes mouse-move on that already-committed SkillAction, the
        // native path can call Actor.AimAt with an element list captured before CQB
        // casualties changed the squad size, causing ArgumentOutOfRangeException spam.
        // Block only the exact SkillAction instance that already launched an Assault;
        // fresh Assault targeting actions and every non-Assault skill remain native.
        if (!_blockedPlayerAssaultActionKey.HasValue || Key(__instance) != _blockedPlayerAssaultActionKey.Value)
            return true;

        object? skill = null;
        try { skill = _skillActionGetSkill?.Invoke(__instance, null); } catch { }
        return !IsAssaultSkill(skill);
    }

    public static bool SkillActionHandleLeftClickPrefix(object __instance, object[] __args)
    {
        object? skill = null;
        try { skill = _skillActionGetSkill?.Invoke(__instance, null); } catch { }
        if (!IsAssaultSkill(skill)) return true;

        object? targetTile = __args.Length > 0 ? __args[0] : null;
        object? activeActor = __args.Length > 1 ? __args[1] : null;
        object? attacker = activeActor ?? (skill != null ? GetSkillActor(skill) : null);

        if (!_enabled || _assaultBusy || !ValidateAssault(attacker, targetTile, null, out object? defender, out object? weapon, out int moveCost, out int totalCost))
        {
            if (_verbose) Log($"Assault click rejected: attacker={NameOf(attacker)}, target={NameOf(targetTile)}, AP={(attacker != null ? GetActionPoints(attacker) : -1)}.");
            return false;
        }

        if (targetTile != null && skill != null) LastTargetBySkill[Key(skill)] = targetTile;

        // Remember this exact targeting action before the custom Assault coroutine starts.
        // Even if TacticalState cleanup is delayed/re-entered by MENACE, subsequent mouse
        // movement on this stale action is suppressed by SkillActionHandleMouseMovePrefix.
        _blockedPlayerAssaultActionKey = Key(__instance);
        StartAssault(attacker!, defender!, targetTile!, weapon!, moveCost, totalCost, fromAi: false);

        // We intercept SkillAction.HandleLeftClickOnTile, so the native method never gets
        // the chance to finish its normal TacticalState cleanup. Canceling only the local
        // SkillAction aim leaves TacticalState.m_CurrentAction pointing at a half-cancelled
        // SkillAction; the next mouse-move then dereferences cleared state every frame.
        FinishPlayerAssaultTargeting(__instance);
        MelonCoroutines.Start(FinishPlayerAssaultTargetingDeferred());
        return false;
    }

    private static IEnumerator FinishPlayerAssaultTargetingDeferred()
    {
        // The click prefix runs inside MENACE input handling. Re-check on the next frame,
        // after that input stack has unwound, and cancel only a still-stale Assault action.
        // This prevents SkillAction.HandleMouseMoveOnTile from later aiming with an invalid
        // executing-element list after CQB casualties changed the squad composition.
        yield return null;
        try
        {
            object? state = _tacticalStateGet?.Invoke(null, null);
            if (state != null)
            {
                object? current = _tacticalStateGetCurrentAction?.Invoke(state, null);
                if (current != null && _skillActionType != null && _skillActionType.IsInstanceOfType(current))
                {
                    object? skill = _skillActionGetSkill?.Invoke(current, null);
                    if (IsAssaultSkill(skill))
                    {
                        _tacticalStateCancelCurrentAction?.Invoke(state, null);
                        _tacticalStateClearCurrentAction?.Invoke(state, null);
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Deferred Assault targeting cleanup failed: " + (e.InnerException?.Message ?? e.Message));
        }
    }

    private static void FinishPlayerAssaultTargeting(object skillAction)
    {
        try
        {
            _skillActionCancelWithoutCancelAim?.Invoke(skillAction, null);
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Assault targeting cleanup (aim) failed: " + (e.InnerException?.Message ?? e.Message));
        }

        try
        {
            object? state = _tacticalStateGet?.Invoke(null, null);
            if (state == null || _tacticalStateClearCurrentAction == null) return;

            // StartAssault only schedules a coroutine and does not install a replacement
            // TacticalAction, so this is still the action whose prefix we are executing.
            _tacticalStateClearCurrentAction.Invoke(state, null);
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Assault targeting cleanup (TacticalState) failed: " + (e.InnerException?.Message ?? e.Message));
        }
    }

    public static bool SkillUsePrefix(object __instance, object[] __args, ref bool __result)
    {
        if (!IsAssaultSkill(__instance)) return true;

        object? targetTile = __args.Length > 0 ? __args[0] : null;
        object? attacker = null;
        try { attacker = GetSkillActor(__instance); } catch { }

        if (!_enabled || _assaultBusy || !ValidateAssault(attacker, targetTile, null, out object? defender, out object? weapon, out int moveCost, out int totalCost))
        {
            __result = false;
            return false;
        }

        StartAssault(attacker!, defender!, targetTile!, weapon!, moveCost, totalCost, fromAi: false);
        __result = true;
        return false;
    }

    public static void SkillGetCoverMultPostfix(object __instance, object _targetTile, object _overrideTargetEntity, ref float __result)
    {
        if (!_enabled || (_phase != AssaultPhase.DefensiveFire && _phase != AssaultPhase.AssaultFire) || Key(__instance) != _phaseWeaponKey) return;

        // At point-blank range StrongerCover can intentionally suppress directional cover.
        // An occupied bunker/building/tower must still contribute its live inside-cover value
        // before the assault reduction is applied, otherwise a garrison could become completely
        // unprotected merely because the attacker is adjacent.
        float normal = Math.Clamp(__result, 0.01f, 1f);
        float inside = ResolveInsideCoverMultiplier(_overrideTargetEntity, _targetTile);
        if (inside < 1f) normal = Math.Min(normal, inside);

        float baseKeepProtection = Math.Clamp(_assaultCoverPercent / 100f, 0f, 1f);
        float bonusScale = Math.Clamp(_phaseCqbBonusScale, 0f, 1f);
        // Scaling the CQB cover bonus means scaling how much protection is removed.
        // Example: AssaultCover=50% normally removes half of cover protection; a Pinned Down
        // defender uses a 0.5 bonus scale, so only one quarter is removed (75% protection kept).
        float keepProtection = 1f - ((1f - baseKeepProtection) * bonusScale);
        float protection = 1f - normal;
        __result = Math.Clamp(1f - (protection * keepProtection), 0.01f, 1f);

        if (_verbose)
            Log($"CQB cover ({_phase}): normal hit mult={normal:0.###}, inside={inside:0.###}, cqb hit mult={__result:0.###}, protection kept={keepProtection:P0}, bonusScale={bonusScale:0.##}.");
    }

    public static void TacticalInvokeOnEntitySpawnedPostfix(object __0)
    {
        if (!_inTacticalScene || !_enabled || __0 == null) return;
        try
        {
            if (IsInfantry(__0) && ShouldHaveAssaultSkill(__0)) EnsureAssaultSkill(__0);
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Assault spawn synchronization failed: " + (e.InnerException?.Message ?? e.Message));
        }
    }

    public static bool AgentExecutePrefix(object __instance, ref bool __result)
    {
        if (!_inTacticalScene || !_enabled || !_enableAi) return true;

        try
        {
            object? actor = _agentGetActor?.Invoke(__instance, null);
            if (actor == null || !IsInfantry(actor) || !IsAlive(actor) || IsDying(actor)) return true;

            int actorKey = Key(actor);
            if (_assaultBusy)
            {
                if (_activeActorKey == actorKey)
                {
                    __result = true;
                    return false;
                }
                return true;
            }

            if (!TryChooseAiAssault(actor, out object? defender, out object? targetTile, out object? weapon, out int moveCost, out int totalCost))
                return true;

            StartAssault(actor, defender!, targetTile!, weapon!, moveCost, totalCost, fromAi: true);
            __result = true;
            return false;
        }
        catch (Exception e)
        {
            if (_verbose) Warn("AI assault evaluation failed: " + e.Message);
            return true;
        }
    }

    private static bool TryChooseAiAssault(object attacker, out object? bestDefender, out object? bestTile, out object? bestWeapon, out int bestMoveCost, out int bestTotalCost)
    {
        bestDefender = null;
        bestTile = null;
        bestWeapon = null;
        bestMoveCost = 0;
        bestTotalCost = 0;
        float bestScore = float.MinValue;

        object? manager = _tacticalGet?.Invoke(null, null);
        object? factions = manager != null ? _tacticalGetFactions?.Invoke(manager, null) : null;
        if (manager == null || factions == null) return false;

        object? attackerTile = _entityGetTile?.Invoke(attacker, null);
        if (attackerTile == null) return false;
        int attackerFaction = GetFaction(attacker);
        int attackerElements = ElementCount(attacker);
        int currentAp = GetActionPoints(attacker);
        if (currentAp <= 0) return false;

        foreach (object? faction in EnumerateObjects(factions))
        {
            if (faction == null) continue;
            MethodInfo? getActors = Method(faction.GetType(), "GetActors", 0);
            object? actors = getActors?.Invoke(faction, null);
            if (actors == null) continue;

            foreach (object? defender in EnumerateObjects(actors))
            {
                if (defender == null || ReferenceEquals(defender, attacker)) continue;
                if (!IsInfantry(defender) || !IsAlive(defender) || IsDying(defender) || AreAllied(attacker, defender)) continue;

                object? defenderTile = _entityGetTile?.Invoke(defender, null);
                if (defenderTile == null || Distance(attackerTile, defenderTile) != 1) continue;

                // AI assaults are a finishing/exploitation action, not a generic adjacent attack.
                // Only consider enemies that MENACE itself currently classifies as Suppressed
                // or PinnedDown (SuppressionState 1/2). Player-controlled Assault is unaffected.
                if (!IsSuppressedOrPinned(defender)) continue;

                MethodInfo? detected = Method(defender.GetType(), "IsDetectedByFaction", 1);
                if (detected != null)
                {
                    try
                    {
                        if (!Convert.ToBoolean(detected.Invoke(defender, new object[] { attackerFaction }) ?? false)) continue;
                    }
                    catch { }
                }

                object? weapon = SelectSmallArms(attacker, defender);
                if (weapon == null) continue;
                int moveCost = GetAdjacentMoveCost(attacker, defenderTile, defender);
                int totalCost = AssaultApCost;
                if (currentAp < AssaultApCost) continue;

                int defenderElements = ElementCount(defender);
                bool contained = GetContainer(defender) != null;
                bool defenderCanFire = CanDefensiveFire(defender, attacker, out _);
                float apFraction = Math.Clamp((float)totalCost / Math.Max(1, currentAp), 0f, 1.5f);

                float score = 100f;
                score += (attackerElements - defenderElements) * 12f;
                if (contained) score += 35f;
                if (!defenderCanFire) score += 30f;
                score -= apFraction * 35f;
                score *= Math.Clamp(_aiAggressiveness / 100f, 0.5f, 2f);

                if (score < 80f || score <= bestScore) continue;
                bestScore = score;
                bestDefender = defender;
                bestTile = defenderTile;
                bestWeapon = weapon;
                bestMoveCost = moveCost;
                bestTotalCost = totalCost;
            }
        }

        if (bestDefender != null && _verbose)
            Log($"AI assault candidate: {NameOf(attacker)} -> {NameOf(bestDefender)}, score={bestScore:0.#}, cost={bestTotalCost}.");
        return bestDefender != null;
    }

    private static int GetSuppressionStateValue(object actor)
    {
        try
        {
            object? state = _actorGetSuppressionState?.Invoke(actor, null);
            if (state == null)
            {
                MethodInfo? runtimeGetter = Method(actor.GetType(), "GetSuppressionState", 0);
                state = runtimeGetter?.Invoke(actor, null);
            }
            return state != null ? Convert.ToInt32(state) : 0;
        }
        catch { return 0; }
    }

    private static int GetStanceValue(object actor)
    {
        try
        {
            object? stance = _actorGetStance?.Invoke(actor, null);
            if (stance == null)
            {
                MethodInfo? runtimeGetter = Method(actor.GetType(), "GetStance", 0);
                stance = runtimeGetter?.Invoke(actor, null);
            }
            return stance != null ? Convert.ToInt32(stance) : 0;
        }
        catch { return 0; }
    }

    private static bool IsSuppressed(object actor) => GetSuppressionStateValue(actor) == 1;
    private static bool IsPinnedDown(object actor) => GetSuppressionStateValue(actor) == 2 || GetStanceValue(actor) == 2;
    private static bool IsSuppressedOrPinned(object actor)
    {
        int suppression = GetSuppressionStateValue(actor);
        return suppression == 1 || suppression == 2 || GetStanceValue(actor) == 2;
    }

    private static bool IsDeployedOrKneeling(object actor)
    {
        try
        {
            // MENACE exposes the kneeling/deployed heavy-weapon posture as ActorStance.Deployed.
            // Check both the convenience predicate and the stance enum so the Assault gate
            // survives wrapper/API differences between game builds.
            object? deployed = _actorIsDeployed?.Invoke(actor, null);
            if (deployed != null && Convert.ToBoolean(deployed)) return true;
            return GetStanceValue(actor) == 1; // ActorStance.Deployed
        }
        catch { return false; }
    }

    private static bool CanInitiateAssault(object actor)
    {
        // Assault requires mobile, combat-capable infantry. Deployed/kneeling crews and
        // infantry currently Suppressed or Pinned Down must recover/change stance first.
        if (IsDeployedOrKneeling(actor)) return false;
        return !IsSuppressedOrPinned(actor);
    }

    private static bool ValidateAssaultTarget(object? attacker, object? targetTile, object? suppliedUserTile, object? overrideTarget, out object? defender)
    {
        defender = null;
        if (!_enabled || attacker == null || targetTile == null || !IsInfantry(attacker) || !IsAlive(attacker) || IsDying(attacker)) return false;
        if (!CanInitiateAssault(attacker)) return false;

        object? userTile = suppliedUserTile ?? _entityGetTile?.Invoke(attacker, null);
        if (userTile == null || Distance(userTile, targetTile) != 1) return false;

        // Resolve the occupant ourselves. Native Charge applicability is intentionally not used:
        // the defender occupies the destination tile until the assault has been won.
        defender = ResolveTarget(targetTile, attacker);
        if (defender == null && overrideTarget != null && IsInfantry(overrideTarget) && !AreAllied(attacker, overrideTarget))
            defender = overrideTarget;

        return defender != null && IsInfantry(defender) && IsAlive(defender) && !IsDying(defender) && !AreAllied(attacker, defender);
    }

    private static bool ValidateAssault(object? attacker, object? targetTile, object? suppliedUserTile, out object? defender, out object? weapon, out int moveCost, out int totalCost)
    {
        defender = null;
        weapon = null;
        moveCost = 0;
        totalCost = 0;

        if (!ValidateAssaultTarget(attacker, targetTile, suppliedUserTile, null, out defender)) return false;

        weapon = SelectSmallArms(attacker!, defender);
        if (weapon == null) return false;

        // Assault has a flat AP cost. Movement/entry cost is only used internally to
        // grant temporary AP for the post-victory MoveTo, then restored; it is not charged.
        moveCost = GetAdjacentMoveCost(attacker!, targetTile!, defender);
        totalCost = AssaultApCost;
        return GetActionPoints(attacker!) >= AssaultApCost;
    }

    private static void StartAssault(object attacker, object defender, object targetTile, object attackerWeapon, int moveCost, int totalCost, bool fromAi)
    {
        if (_assaultBusy) return;

        _assaultBusy = true;
        _activeActorKey = Key(attacker);
        _assaultsStarted++;
        if (fromAi) _aiAssaultsStarted++;
        _lastStatus = $"Starting: {NameOf(attacker)} -> {NameOf(defender)}";

        MelonCoroutines.Start(AssaultRoutine(attacker, defender, targetTile, attackerWeapon, moveCost, totalCost, fromAi));
    }

    private static IEnumerator AssaultRoutine(object attacker, object defender, object targetTile, object attackerWeapon, int moveCost, int totalCost, bool fromAi)
    {
        object? defenderContainer = GetContainer(defender);
        int residualAp = Math.Max(0, GetActionPoints(attacker) - totalCost);
        SetActionPoints(attacker, residualAp);

        if (_verbose)
            Log($"Assault started{(fromAi ? " [AI]" : string.Empty)}: {NameOf(attacker)} -> {NameOf(defender)}, fixedCost={totalCost}, moveGrant={moveCost}, weaponAP={GetActionPointCostRaw(attackerWeapon)}, AP after={residualAp}.");

        object? cqbAccuracyTarget = null;
        object? cqbAccuracyOriginal = null;
        bool cqbAccuracyApplied = false;

        try
        {
            if (CanDefensiveFire(defender, attacker, out object? defenderWeapon) && defenderWeapon != null)
            {
                _lastStatus = "Defensive fire";
                _phase = AssaultPhase.DefensiveFire;
                float defenderCqbBonusScale = IsPinnedDown(defender) ? 0.5f : 1f;
                _phaseCqbBonusScale = defenderCqbBonusScale;
                _phaseWeaponKey = Key(defenderWeapon);

                bool oldHasActed = GetHasActed(defender);
                int defenderAp = GetActionPoints(defender);
                int defenderFireCost = GetActionPointCostRaw(defenderWeapon);
                object? oldApplyInside = TemporarilyEnableApplyInside(defenderWeapon);
                object? defenderTile = _entityGetTile?.Invoke(defender, null);
                object? attackerTileBefore = _entityGetTile?.Invoke(attacker, null);
                object? tileEntityBefore = GetTileEntity(attackerTileBefore);
                object? resolvedTargetBefore = attackerTileBefore != null ? ResolveTarget(attackerTileBefore, defender) : null;
                int attackerHpBefore = GetHitpoints(attacker);
                int attackerArmorBefore = GetArmorDurability(attacker);
                float attackerSuppressionBefore = GetSuppression(attacker);
                float attackerMoraleBefore = GetMorale(attacker);

                if (_verbose)
                {
                    Log($"[ASSAULT_DEBUG] defensive-pre defender={NameOf(defender)} defenderTile={DescribeTile(defenderTile)} attacker={NameOf(attacker)} attackerTile={DescribeTile(attackerTileBefore)} targetTile={DescribeTile(attackerTileBefore)} tileEntity={DescribeEntity(tileEntityBefore)} resolvedTarget={DescribeEntity(resolvedTargetBefore)} intendedMatch={SameEntity(attacker, resolvedTargetBefore)} weapon={GetSkillId(defenderWeapon)} hp={attackerHpBefore} armor={attackerArmorBefore} suppression={attackerSuppressionBefore:0.##} morale={attackerMoraleBefore:0.##}.");
                }

                cqbAccuracyApplied = TryApplyCqbAccuracyBonus(defender, defenderCqbBonusScale, out cqbAccuracyTarget, out cqbAccuracyOriginal);
                bool used = UseWeaponReaction(defenderWeapon, attackerTileBefore);
                if (_verbose) Log($"Defensive fire: weapon={GetSkillId(defenderWeapon)}, used={used}, cqbBonus={_cqbAccuracyBonusPercent * defenderCqbBonusScale:0.#}pp, pinned={defenderCqbBonusScale < 1f}.");

                if (used)
                {
                    yield return null;
                    while (IsSkillBusy()) yield return null;
                    for (int i = 0; i < 2; i++) yield return null;
                }

                if (_verbose)
                {
                    object? attackerTileAfter = _entityGetTile?.Invoke(attacker, null);
                    object? tileEntityAfter = GetTileEntity(attackerTileBefore);
                    object? resolvedTargetAfter = attackerTileBefore != null ? ResolveTarget(attackerTileBefore, defender) : null;
                    int attackerHpAfter = GetHitpoints(attacker);
                    int attackerArmorAfter = GetArmorDurability(attacker);
                    float attackerSuppressionAfter = GetSuppression(attacker);
                    float attackerMoraleAfter = GetMorale(attacker);
                    Log($"[ASSAULT_DEBUG] defensive-post used={used} attacker={NameOf(attacker)} attackerTileNow={DescribeTile(attackerTileAfter)} targetTile={DescribeTile(attackerTileBefore)} tileEntity={DescribeEntity(tileEntityAfter)} resolvedTarget={DescribeEntity(resolvedTargetAfter)} intendedMatch={SameEntity(attacker, resolvedTargetAfter)} hp={attackerHpBefore}->{attackerHpAfter} armor={attackerArmorBefore}->{attackerArmorAfter} suppression={attackerSuppressionBefore:0.##}->{attackerSuppressionAfter:0.##} morale={attackerMoraleBefore:0.##}->{attackerMoraleAfter:0.##} alive={IsAlive(attacker)} dying={IsDying(attacker)}.");
                }

                if (cqbAccuracyApplied)
                {
                    RestoreCqbAccuracyBonus(cqbAccuracyTarget, cqbAccuracyOriginal);
                    cqbAccuracyApplied = false;
                }
                RestoreApplyInside(defenderWeapon, oldApplyInside);
                SetActionPoints(defender, defenderAp);
                SetHasActed(defender, oldHasActed);
                _phase = AssaultPhase.None;
                _phaseCqbBonusScale = 1f;
                _phaseWeaponKey = 0;

                if (!IsAlive(attacker) || IsDying(attacker))
                {
                    _lastStatus = "Assault stopped by defensive fire";
                    if (_verbose) Log("Assault ended: attacker destroyed by defensive fire.");
                    yield break;
                }
            }

            if (!IsAlive(defender) || IsDying(defender))
            {
                _lastStatus = "Defender already destroyed; advancing";
                yield return AdvanceAfterVictory(attacker, targetTile, defenderContainer, moveCost, residualAp);
                yield break;
            }

            _lastStatus = "Assault fire";
            _phase = AssaultPhase.AssaultFire;
            _phaseCqbBonusScale = 1f;
            _phaseWeaponKey = Key(attackerWeapon);
            int attackerFireCost = GetActionPointCostRaw(attackerWeapon);
            object? oldInside = TemporarilyEnableApplyInside(attackerWeapon);
            cqbAccuracyApplied = TryApplyCqbAccuracyBonus(attacker, 1f, out cqbAccuracyTarget, out cqbAccuracyOriginal);
            bool attackUsed = UseWeaponReaction(attackerWeapon, targetTile);
            if (_verbose) Log($"Assault fire: weapon={GetSkillId(attackerWeapon)}, used={attackUsed}, cqbBonus={_cqbAccuracyBonusPercent:0.#}pp.");

            if (attackUsed)
            {
                yield return null;
                while (IsSkillBusy()) yield return null;
                for (int i = 0; i < 2; i++) yield return null;
            }

            if (cqbAccuracyApplied)
            {
                RestoreCqbAccuracyBonus(cqbAccuracyTarget, cqbAccuracyOriginal);
                cqbAccuracyApplied = false;
            }
            SetActionPoints(attacker, residualAp);
            RestoreApplyInside(attackerWeapon, oldInside);
            _phase = AssaultPhase.None;
            _phaseCqbBonusScale = 1f;
            _phaseWeaponKey = 0;

            if (!IsAlive(defender) || IsDying(defender))
            {
                _lastStatus = "Assault won; advancing";
                yield return AdvanceAfterVictory(attacker, targetTile, defenderContainer, moveCost, residualAp);
            }
            else
            {
                _lastStatus = "Assault repulsed";
                if (_verbose) Log("Assault repulsed: defender survived; attacker remains on origin tile.");
            }
        }
        finally
        {
            if (cqbAccuracyApplied) RestoreCqbAccuracyBonus(cqbAccuracyTarget, cqbAccuracyOriginal);
            _phase = AssaultPhase.None;
            _phaseCqbBonusScale = 1f;
            _phaseWeaponKey = 0;
            _activeActorKey = 0;
            _assaultBusy = false;
            if (!fromAi) MelonCoroutines.Start(FinishPlayerAssaultTargetingDeferred());
        }
    }

    private static IEnumerator AdvanceAfterVictory(object attacker, object targetTile, object? targetContainer, int moveCost, int residualAp)
    {
        if (!IsAlive(attacker) || IsDying(attacker) || _actorMoveTo == null || _movementActionType == null || _movementFlagsType == null)
            yield break;

        int temporaryAp = residualAp + Math.Max(0, moveCost);
        SetActionPoints(attacker, temporaryAp);

        bool advanceStarted = true;
        try
        {
            int actionValue = 0;
            if (GetContainer(attacker) != null) actionValue |= 2; // MovementAction.Leave
            if (targetContainer != null) actionValue |= 1;       // MovementAction.Enter
            object action = Enum.ToObject(_movementActionType, actionValue);
            object flags = Enum.ToObject(_movementFlagsType, 0);
            object?[] args = { targetTile, action, flags };
            _actorMoveTo.Invoke(attacker, args);
        }
        catch (Exception e)
        {
            advanceStarted = false;
            SetActionPoints(attacker, residualAp);
            Warn("Could not start advance after successful assault: " + e.Message);
        }

        if (!advanceStarted)
            yield break;

        // C# iterator methods cannot yield from a try block that has a catch clause.
        // Wait for the real MoveTo coroutine outside the guarded invocation.
        yield return null;
        int safety = 0;
        while (IsMoving(attacker) && safety++ < 600) yield return null;
        SetActionPoints(attacker, residualAp);

        if (_verbose) Log($"Assault victory advance complete. Container={(targetContainer != null ? "yes" : "no")}, AP={residualAp}.");
    }

    private static object? ResolveTarget(object targetTile, object? attacker)
    {
        try
        {
            object? entity = _tileGetEntity?.Invoke(targetTile, null);
            if (entity == null) return null;

            if (IsInfantry(entity) && (attacker == null || !AreAllied(attacker, entity))) return entity;

            object? contained = _entityGetContainedEntity?.Invoke(entity, null);
            if (contained != null && IsInfantry(contained) && (attacker == null || !AreAllied(attacker, contained))) return contained;
        }
        catch { }
        return null;
    }

    private static float ResolveInsideCoverMultiplier(object? targetEntity, object? targetTile)
    {
        try
        {
            object? container = targetEntity != null ? GetContainer(targetEntity) : null;
            if (container == null && targetTile != null)
            {
                object? tileEntity = _tileGetEntity?.Invoke(targetTile, null);
                if (tileEntity != null && !IsInfantry(tileEntity)) container = tileEntity;
            }
            if (container == null) return 1f;

            object? template = _entityGetTemplate?.Invoke(container, null);
            if (template == null) return 1f;
            object? insideTemplate = GetMember(template, "ProvidesCoverInside");
            if (insideTemplate == null) return 1f;
            object? raw = GetMember(insideTemplate, "AccuracyMult");
            if (raw == null) return 1f;
            return Math.Clamp(Convert.ToSingle(raw), 0.01f, 1f);
        }
        catch { return 1f; }
    }

    private static object? SelectSmallArms(object actor, object? target)
    {
        try
        {
            // Prefer Actor.GetAllAttacks(), but transient/dev-spawned actors may expose
            // legitimate attack skills only through their SkillContainer. Merge both sources
            // and deduplicate by native object identity instead of requiring an item-backed skill.
            var candidates = new List<(object Skill, bool FromAttackList)>();
            var seen = new HashSet<int>();

            MethodInfo? getAllAttacks = Method(actor.GetType(), "GetAllAttacks", 0);
            object? attacks = getAllAttacks?.Invoke(actor, null);
            if (attacks != null)
            {
                foreach (object? skill in EnumerateObjects(attacks))
                {
                    if (skill == null) continue;
                    int key = Key(skill);
                    if (seen.Add(key)) candidates.Add((skill, true));
                }
            }

            object? skillContainer = GetActorSkills(actor);
            object? allSkills = skillContainer != null ? GetAllSkills(skillContainer) : null;
            if (allSkills != null)
            {
                foreach (object? skill in EnumerateObjects(allSkills))
                {
                    if (skill == null) continue;
                    int key = Key(skill);
                    if (seen.Add(key)) candidates.Add((skill, false));
                }
            }

            object? best = null;
            float bestScore = float.MinValue;

            foreach ((object skill, bool fromAttackList) in candidates)
            {
                if (IsAssaultSkill(skill)) continue;

                MethodInfo? outOfUses = Method(skill.GetType(), "IsOutOfUses", 0);
                if (outOfUses != null && Bool(outOfUses.Invoke(skill, null))) continue;

                MethodInfo? isAttack = Method(skill.GetType(), "IsAttack", 0);
                bool confirmedAttack = fromAttackList || (isAttack != null && Bool(isAttack.Invoke(skill, null)));
                if (!confirmedAttack) continue;

                MethodInfo? getMin = Method(skill.GetType(), "GetMinRange", 0);
                MethodInfo? getMax = Method(skill.GetType(), "GetMaxRange", 0);
                int min = Int(getMin?.Invoke(skill, null), 0);
                int max = Int(getMax?.Invoke(skill, null), 0);
                if (min > 1 || max < 1) continue;

                string id = GetSkillIdRuntime(skill).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(id) || IsExcludedAssaultWeapon(id)) continue;

                MethodInfo? getItem = Method(skill.GetType(), "GetItem", 0);
                object? item = getItem?.Invoke(skill, null);

                int ap = GetActionPointCostRaw(skill);
                float score = 100f + Math.Min(max, 30) - ap * 0.05f;
                if (item != null) score += 25f; // prefer conventional equipped small arms
                if (id.Contains("attack") || id.Contains("shoot") || id.Contains("fire")) score += 20f;
                if (id.Contains("overwatch") || id.Contains("suppress")) score -= 40f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = skill;
                }
            }

            return best;
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Small-arms selection failed: " + (e.InnerException?.Message ?? e.Message));
            return null;
        }
    }


    private static bool TryApplyCqbAccuracyBonus(object attacker, float bonusScale, out object? properties, out object? originalValue)
    {
        properties = null;
        originalValue = null;
        bonusScale = Math.Clamp(bonusScale, 0f, 1f);
        if (_cqbAccuracyBonusPercent <= 0f || bonusScale <= 0f) return false;

        try
        {
            properties = _entityGetCurrentProperties?.Invoke(attacker, null);
            if (properties == null) return false;

            originalValue = GetMember(properties, "AccuracyMult");
            if (originalValue == null) return false;

            float current = Convert.ToSingle(originalValue);
            float boosted = Math.Max(0f, current + ((_cqbAccuracyBonusPercent * bonusScale) / 100f));
            if (!SetMember(properties, "AccuracyMult", boosted)) return false;
            return true;
        }
        catch
        {
            properties = null;
            originalValue = null;
            return false;
        }
    }

    private static void RestoreCqbAccuracyBonus(object? properties, object? originalValue)
    {
        if (properties == null || originalValue == null) return;
        try { SetMember(properties, "AccuracyMult", originalValue); }
        catch { }
    }

    private static string GetSkillIdRuntime(object skill)
    {
        // GetAllSkills() and GetSkillsInAddQueue() expose BaseSkill wrappers.
        // Invoking a Skill-derived MethodInfo on one of those wrappers throws
        // "Object does not match target type" even when the native Skill exists.
        try
        {
            MethodInfo? getId = _skillGetId;
            if (getId?.DeclaringType?.IsInstanceOfType(skill) != true)
                getId = Method(skill.GetType(), "GetID", 0);

            string? id = Convert.ToString(getId?.Invoke(skill, null));
            if (!string.IsNullOrEmpty(id)) return id;
        }
        catch { }

        // If a native virtual GetID invocation is unavailable on this interop
        // wrapper, reading BaseSkill.GetTemplate() is an independent fallback.
        try
        {
            MethodInfo? getTemplate = Method(skill.GetType(), "GetTemplate", 0);
            object? template = getTemplate?.Invoke(skill, null);
            return template != null ? GetObjectId(template) : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static bool IsExcludedAssaultWeapon(string id)
    {
        string[] excluded =
        {
            // Infantry Assault is deliberately limited to direct-fire small arms.
            // Weapon-team / crew-served / indirect / anti-vehicle attacks are not valid CQB weapons.
            "grenade", "rocket", "missile", "mortar", "launcher", "howitzer", "artillery",
            "tripod", "anti_tank", "anti-tank", "antitank", "atgm", "autocannon", "auto_cannon",
            "cannon", "chaingun", "chain_gun", "minigun", "recoilless", "railgun",
            "heavy_machinegun", "heavy_machine_gun", "medium_machinegun", "medium_machine_gun",
            "suppressive", "sustained_fire",
            "flamethrower", "flame_thrower", "charge", "melee", "bayonet", "knife", "mine"
        };
        return excluded.Any(id.Contains);
    }

    private static bool CanDefensiveFire(object defender, object attacker, out object? weapon)
    {
        weapon = null;
        // Suppressed infantry cannot return defensive fire. Pinned Down infantry may still
        // fire, but their CQB accuracy and cover-reduction bonuses are halved by AssaultRoutine.
        // Some MENACE builds can also surface PinnedDown through IsStunned(); explicitly allow
        // that pinned state while keeping every other stun incapable of reaction fire.
        if (!IsAlive(defender) || IsDying(defender) || IsSuppressed(defender)) return false;
        bool pinned = IsPinnedDown(defender);
        if (IsStunned(defender) && !pinned) return false;
        weapon = SelectSmallArms(defender, attacker);
        return weapon != null;
    }

    private static bool UseWeaponReaction(object weapon, object? targetTile)
    {
        if (_skillUse == null || _usageParameterType == null || targetTile == null) return false;
        try
        {
            // MENACE changed UsageParameter values in the current patch. Resolve flags by
            // enum name instead of hard-coded integers. Ignore the normal turn/usability gate
            // and suppress AP consumption, but deliberately DO NOT suppress uses/ammo.
            object usage = MakeUsageFlags("IgnoreUsabilityCheck", "NoApConsumed");
            MethodInfo? use = Method(weapon.GetType(), "Use", 2);
            if (use == null) return false;
            return Convert.ToBoolean(use.Invoke(weapon, new[] { targetTile, usage }) ?? false);
        }
        catch (Exception e)
        {
            Warn("Weapon use failed: " + (e.InnerException?.Message ?? e.Message));
            return false;
        }
    }

    private static object? TemporarilyEnableApplyInside(object weapon)
    {
        try
        {
            object? template = Method(weapon.GetType(), "GetTemplate", 0)?.Invoke(weapon, null);
            if (template == null) return null;
            object? old = GetMember(template, "ApplyToEntityInsideStructure");
            SetMember(template, "ApplyToEntityInsideStructure", true);
            return old;
        }
        catch { return null; }
    }

    private static void RestoreApplyInside(object weapon, object? oldValue)
    {
        if (oldValue == null) return;
        try
        {
            object? template = Method(weapon.GetType(), "GetTemplate", 0)?.Invoke(weapon, null);
            if (template != null) SetMember(template, "ApplyToEntityInsideStructure", oldValue);
        }
        catch { }
    }

    private static int GetAdjacentMoveCost(object actor, object targetTile, object? defender)
    {
        int cost = GetFallbackMoveCost(actor, targetTile);

        try
        {
            object? props = _entityGetCurrentProperties?.Invoke(actor, null);
            if (props != null)
            {
                if (GetContainer(actor) != null)
                {
                    MethodInfo? leave = Method(props.GetType(), "GetAPLeaveCost", 0);
                    cost += Int(leave?.Invoke(props, null), 0);
                }

                if (defender != null && GetContainer(defender) != null)
                {
                    MethodInfo? enter = Method(props.GetType(), "GetAPEnterCost", 0);
                    cost += Int(enter?.Invoke(props, null), 0);
                }
            }
        }
        catch { }

        return Math.Max(1, cost);
    }

    private static int GetFallbackMoveCost(object? actor, object? targetTile = null)
    {
        if (actor == null) return 1;
        try
        {
            object? template = _entityGetTemplate?.Invoke(actor, null);
            object? movementType = template != null ? GetMember(template, "MovementType") : null;
            if (movementType != null)
            {
                if (targetTile != null && _tileGetSurfaceType != null)
                {
                    int surface = Int(_tileGetSurfaceType.Invoke(targetTile, null), 0);
                    MethodInfo? perTile = Method(movementType.GetType(), "GetMovementCostForTileType", 2);
                    if (perTile != null)
                        return Math.Max(1, Int(perTile.Invoke(movementType, new object?[] { surface, actor }), 1));
                }

                MethodInfo? lowest = Method(movementType.GetType(), "GetLowestMovementCost", 0);
                if (lowest != null) return Math.Max(1, Int(lowest.Invoke(movementType, null), 1));
            }
        }
        catch { }
        return 1;
    }

    private static void EnsureAssaultSkillsOnAllActors()
    {
        try
        {
            object? manager = _tacticalGet?.Invoke(null, null);
            object? factions = manager != null ? _tacticalGetFactions?.Invoke(manager, null) : null;
            if (manager == null || factions == null) return;

            foreach (object? faction in EnumerateObjects(factions))
            {
                if (faction == null) continue;
                MethodInfo? getActors = Method(faction.GetType(), "GetActors", 0);
                object? actors = getActors?.Invoke(faction, null);
                if (actors == null) continue;
                foreach (object? actor in EnumerateObjects(actors))
                {
                    if (actor != null && IsInfantry(actor) && ShouldHaveAssaultSkill(actor)) EnsureAssaultSkill(actor);
                }
            }
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Assault skill synchronization pass failed: " + (e.InnerException?.Message ?? e.Message));
        }
    }

    private static bool EnsureAssaultSkill(object actor)
    {
        if (_assaultTemplate == null || actor == null || !IsInfantry(actor)) return false;

        try
        {
            object? container = GetActorSkills(actor);
            if (container == null) return false;

            object? existing = FindAssaultSkillInContainer(container);
            if (existing != null) return true;

            // Existing-save actors may have been materialized before the suite's template
            // patches were applied. Ask MENACE to add the template's inherent skills using
            // its native path first; this avoids constructing a Skill with a synthetic origin.
            object? template = _entityGetTemplate?.Invoke(actor, null);
            MethodInfo? addInherent = Method(container.GetType(), "AddInherentSkills", 1);
            if (template != null && addInherent != null)
            {
                try
                {
                    addInherent.Invoke(container, new[] { template });
                    Method(container.GetType(), "ScheduleUpdate", 0)?.Invoke(container, null);
                    existing = FindAssaultSkillInContainer(container);
                    if (existing != null)
                    {
                        if (_verbose) Log($"Runtime synchronized Assault skill to {NameOf(actor)} through native inherent-skill refresh.");
                        return true;
                    }
                }
                catch (Exception e)
                {
                    // A native partial success or a failed ScheduleUpdate may
                    // still have queued Assault. Never follow that by creating
                    // a second instance if it is now visible in the container.
                    if (FindAssaultSkillInContainer(container) != null) return true;
                    if (_verbose) Log("Native inherent-skill refresh skipped: " + (e.InnerException?.Message ?? e.Message));
                }
            }

            if (FindAssaultSkillInContainer(container) != null) return true;

            MethodInfo? createSkill = Method(_assaultTemplate.GetType(), "CreateSkill", 1);
            MethodInfo? add = Method(container.GetType(), "Add", 1);
            if (createSkill == null || add == null) return false;

            // The origin parameter is Nullable<Origin>; null is a valid origin for this
            // synthetic inherent action. SkillContainer.Add wires owner/container state.
            object? skill = createSkill.Invoke(_assaultTemplate, new object?[] { null });
            if (skill == null) return false;

            bool added = Bool(add.Invoke(container, new[] { skill }));
            if (!added)
            {
                // Add can reject a duplicate queued by another path; re-check before failing.
                return FindAssaultSkillInContainer(container) != null;
            }

            // The skill is now owned by the container. Treat synchronization as successful here.
            // UI/enable refresh is best-effort: on loaded-save actors MENACE can throw while
            // refreshing transient UI state even though SkillContainer.Add already succeeded.
            RememberAssaultSkill(skill);

            try { Method(skill.GetType(), "SetEnabled", 1)?.Invoke(skill, new object[] { true }); }
            catch (Exception e) { if (_verbose) Log("Assault skill enable refresh skipped: " + (e.InnerException?.Message ?? e.Message)); }

            try
            {
                SetMember(container, "UiNeedsUpdate", true);
                Method(container.GetType(), "ScheduleUpdate", 0)?.Invoke(container, null);
            }
            catch (Exception e)
            {
                if (_verbose) Log("Assault skill UI refresh skipped: " + (e.InnerException?.Message ?? e.Message));
            }

            if (_verbose) Log($"Runtime synchronized Assault skill to {NameOf(actor)}.");
            return true;
        }
        catch (Exception e)
        {
            if (_verbose) Warn("Assault runtime synchronization failed: " + (e.InnerException?.Message ?? e.Message));
            return false;
        }
    }


    private static bool ShouldHaveAssaultSkill(object actor)
    {
        try
        {
            object? template = _entityGetTemplate?.Invoke(actor, null);
            if (template == null) return false;

            object? skills = GetMember(template, "Skills") ?? GetMember(template, "m_Skills");
            if (skills == null) return false;

            foreach (object? skillTemplate in EnumerateObjects(skills))
            {
                if (skillTemplate == null) continue;
                string id = GetObjectId(skillTemplate);
                if (string.Equals(id, AssaultSkillId, StringComparison.Ordinal)) return true;
            }
        }
        catch { }
        return false;
    }

    private static string GetObjectId(object value)
    {
        try
        {
            MethodInfo? getId = Method(value.GetType(), "GetID", 0);
            string? id = Convert.ToString(getId?.Invoke(value, null));
            if (!string.IsNullOrWhiteSpace(id)) return id;

            foreach (string member in new[] { "m_ID", "ID", "Id", "name" })
            {
                string? candidate = Convert.ToString(GetMember(value, member));
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
            }
        }
        catch { }
        return string.Empty;
    }

    private static void ScanAssaultSkills()
    {
        try
        {
            object? manager = _tacticalGet?.Invoke(null, null);
            object? factions = manager != null ? _tacticalGetFactions?.Invoke(manager, null) : null;
            if (manager == null || factions == null) return;

            int infantry = 0;
            int present = 0;
            int missing = 0;
            foreach (object? faction in EnumerateObjects(factions))
            {
                if (faction == null) continue;
                MethodInfo? getActors = Method(faction.GetType(), "GetActors", 0);
                object? actors = getActors?.Invoke(faction, null);
                if (actors == null) continue;
                foreach (object? actor in EnumerateObjects(actors))
                {
                    if (actor == null || !IsInfantry(actor) || !ShouldHaveAssaultSkill(actor)) continue;
                    infantry++;
                    object? container = GetActorSkills(actor);
                    object? assault = container != null ? FindAssaultSkillInContainer(container) : null;
                    if (assault != null)
                    {
                        present++;
                    }
                    else
                    {
                        missing++;
                        if (_verbose) Warn($"Manifest Assault skill missing on infantry template actor {NameOf(actor)} ({actor.GetType().Name}).");
                    }
                }
            }
            Log($"Tactical Assault skill scan: eligibleInfantry={infantry}, present={present}, missing={missing}. Runtime fallback is restricted to templates that declare Assault.");
        }
        catch (Exception e)
        {
            Warn("Assault skill diagnostic scan failed: " + (e.InnerException?.Message ?? e.Message));
        }
    }

    private static bool IsAssaultSkill(object? skill)
    {
        if (skill == null) return false;
        IntPtr ptr = skill is Il2CppObjectBase native ? native.Pointer : IntPtr.Zero;
        if (ptr != IntPtr.Zero && AssaultSkillPointers.Contains(ptr)) return true;

        // Never trust managed wrapper hash codes: List<BaseSkill> and a Harmony
        // Skill hook may represent the same IL2CPP object with different wrappers.
        if (!string.Equals(GetSkillIdRuntime(skill), AssaultSkillId, StringComparison.Ordinal))
            return false;

        RememberAssaultSkill(skill);
        return true;
    }

    private static void RememberAssaultSkill(object skill)
    {
        if (skill is Il2CppObjectBase native && native.Pointer != IntPtr.Zero)
            AssaultSkillPointers.Add(native.Pointer);
    }

    private static string GetSkillId(object skill) => GetSkillIdRuntime(skill);

    private static int GetActionPointCostRaw(object skill)
    {
        try
        {
            if (IsAssaultSkill(skill)) return AssaultApCost;
            MethodInfo? getCost = Method(skill.GetType(), "GetActionPointCost", 0);
            return Math.Max(0, Int(getCost?.Invoke(skill, null), 0));
        }
        catch { return 0; }
    }

    private static object? GetSkillActor(object skill)
    {
        try
        {
            object? actor = _skillGetActor?.Invoke(skill, null);
            if (actor != null) return actor;
        }
        catch { }

        try
        {
            object? actor = _skillGetApConsumer?.Invoke(skill, null);
            if (actor != null) return actor;
        }
        catch { }

        return null;
    }

    private static object MakeUsageFlags(params string[] names)
    {
        if (_usageParameterType == null) return 0;
        int value = 0;
        foreach (string name in names)
        {
            try { value |= Convert.ToInt32(Enum.Parse(_usageParameterType, name, ignoreCase: false)); }
            catch { }
        }
        return Enum.ToObject(_usageParameterType, value);
    }

    private static object? GetContainer(object entity)
    {
        try { return _entityGetContainerEntity?.Invoke(entity, null); }
        catch { return null; }
    }

    private static object? GetTileEntity(object? tile)
    {
        if (tile == null) return null;
        try { return _tileGetEntity?.Invoke(tile, null); }
        catch { return null; }
    }

    private static int GetHitpoints(object entity)
    {
        try
        {
            MethodInfo? get = _entityGetHitpoints;
            if (get == null || !get.DeclaringType!.IsInstanceOfType(entity))
                get = Method(entity.GetType(), "GetHitpoints", 0);
            return Int(get?.Invoke(entity, null), -1);
        }
        catch { return -1; }
    }

    private static int GetArmorDurability(object entity)
    {
        try
        {
            MethodInfo? get = _entityGetArmorDurability;
            if (get == null || !get.DeclaringType!.IsInstanceOfType(entity))
                get = Method(entity.GetType(), "GetArmorDurability", 0);
            return Int(get?.Invoke(entity, null), -1);
        }
        catch { return -1; }
    }

    private static float GetSuppression(object actor)
    {
        try
        {
            MethodInfo? get = _actorGetSuppression;
            if (get == null || !get.DeclaringType!.IsInstanceOfType(actor))
                get = Method(actor.GetType(), "GetSuppression", 0);
            object? value = get?.Invoke(actor, null);
            return value == null ? float.NaN : Convert.ToSingle(value);
        }
        catch { return float.NaN; }
    }

    private static float GetMorale(object actor)
    {
        try
        {
            MethodInfo? get = _actorGetMorale;
            if (get == null || !get.DeclaringType!.IsInstanceOfType(actor))
                get = Method(actor.GetType(), "GetMorale", 0);
            object? value = get?.Invoke(actor, null);
            return value == null ? float.NaN : Convert.ToSingle(value);
        }
        catch { return float.NaN; }
    }

    private static string DescribeTile(object? tile)
    {
        if (tile == null) return "(null)";
        try { return tile.ToString() ?? tile.GetType().Name; }
        catch { return tile.GetType().Name; }
    }

    private static string DescribeEntity(object? entity)
    {
        if (entity == null) return "(null)";
        return $"{NameOf(entity)}#{Key(entity)}";
    }

    private static bool SameEntity(object? a, object? b)
    {
        return a != null && b != null && (ReferenceEquals(a, b) || Key(a) == Key(b));
    }

    private static int GetActionPoints(object actor)
    {
        try { return Int(_actorGetActionPoints?.Invoke(actor, null), 0); }
        catch { return 0; }
    }

    private static void SetActionPoints(object actor, int value)
    {
        try { _actorSetActionPointsUnsafe?.Invoke(actor, new object[] { Math.Max(0, value) }); }
        catch { }
    }

    private static bool IsAlive(object entity)
    {
        try { return Bool(_entityIsAlive?.Invoke(entity, null), true); }
        catch { return true; }
    }

    private static bool IsInfantry(object entity)
    {
        try
        {
            if (_actorType != null && _actorType.IsInstanceOfType(entity) && _actorIsInfantry != null)
                return Bool(_actorIsInfantry.Invoke(entity, null));
            return Bool(_entityIsInfantry?.Invoke(entity, null));
        }
        catch { return false; }
    }

    private static bool IsDying(object actor)
    {
        try { return Bool(_actorIsDying?.Invoke(actor, null)); }
        catch { return false; }
    }

    private static bool IsStunned(object actor)
    {
        try { return Bool(_actorIsStunned?.Invoke(actor, null)); }
        catch { return false; }
    }

    private static bool IsMoving(object actor)
    {
        try { return Bool(_actorIsMoving?.Invoke(actor, null)); }
        catch { return false; }
    }

    private static bool AreAllied(object a, object b)
    {
        try { return Bool(_entityIsAlliedWithEntity?.Invoke(a, new[] { b })); }
        catch { return GetFaction(a) == GetFaction(b); }
    }

    private static int GetFaction(object entity)
    {
        try { return Int(_entityGetFactionId?.Invoke(entity, null), -1); }
        catch { return -1; }
    }

    private static int Distance(object a, object b)
    {
        try { return Int(_tileGetDistanceTo?.Invoke(a, new[] { b }), 999); }
        catch { return 999; }
    }

    private static int ElementCount(object entity)
    {
        try
        {
            object? elements = _entityGetElements?.Invoke(entity, null);
            if (elements == null) return 1;
            object? count = GetMember(elements, "Count");
            if (count != null) return Math.Max(0, Int(count, 1));
            int result = 0;
            foreach (object? _ in EnumerateObjects(elements)) result++;
            return Math.Max(1, result);
        }
        catch { return 1; }
    }

    private static bool GetHasActed(object actor)
    {
        try { return Bool(_actorHasActedField?.GetValue(actor)); }
        catch { return false; }
    }

    private static void SetHasActed(object actor, bool value)
    {
        try { _actorHasActedField?.SetValue(actor, value); }
        catch { }
    }

    private static bool IsSkillBusy()
    {
        try { return Bool(_tacticalIsSkillBusy?.Invoke(null, null)); }
        catch { return false; }
    }

    private static string NameOf(object? entity)
    {
        if (entity == null) return "(null)";
        try
        {
            PropertyInfo? p = entity.GetType().GetProperty("DebugName", AllInstance);
            string? name = Convert.ToString(p?.GetValue(entity));
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        catch { }
        return entity.GetType().Name;
    }

    private static void ReloadSettings()
    {
        _enabled = Setting("EnableGameplay", true);
        _enableAi = Setting("EnableAI", true);
        _assaultCoverPercent = Math.Clamp(Setting("AssaultCover", 50f), 0f, 100f);
        _cqbAccuracyBonusPercent = Math.Clamp(Setting("CQBAccuracyBonus", 20f), 0f, 50f);
        _aiAggressiveness = Math.Clamp(Setting("AIAggressiveness", 100f), 50f, 200f);
        _verbose = Setting("VerboseLogging", false);
    }

    private static T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (!string.Equals(modName, SettingsGroup, StringComparison.Ordinal)) return;
        ReloadSettings();
        if (_verbose) Log($"Setting changed: {key}={value}.");
    }

    private static void DrawDebug(Rect area)
    {
        GUILayout.BeginArea(area);
        GUILayout.Label($"Infantry Assault {Version}");
        GUILayout.Label($"Enabled: {_enabled}");
        GUILayout.Label($"AI enabled: {_enableAi} | aggressiveness: {_aiAggressiveness:0}%");
        GUILayout.Label($"CQB target cover effectiveness: {_assaultCoverPercent:0}% | CQB hit bonus: +{_cqbAccuracyBonusPercent:0.#}pp");
        GUILayout.Label($"Template prepared: {_templatePrepared} | patches: {_patchesApplied}");
        GUILayout.Label($"Assault busy: {_assaultBusy} | phase: {_phase}");
        GUILayout.Label($"Assaults started: {_assaultsStarted} | AI: {_aiAssaultsStarted}");
        GUILayout.Label($"Last status: {_lastStatus}");
        GUILayout.EndArea();
    }

    private static Type? FindType(string fullName, string simpleName)
    {
        Type? type = _game?.GetType(fullName, false);
        if (type != null) return type;
        try { return _game?.GetTypes().FirstOrDefault(t => t.Name == simpleName); }
        catch { return null; }
    }

    private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static MethodInfo? Method(Type type, string name, int parameterCount, bool isStatic = false)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        return type.GetMethods(flags).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
    }

    private static void Patch(MethodInfo? original, string? prefix = null, string? postfix = null, int? priority = null)
    {
        if (original == null) throw new MissingMethodException("Harmony target method was not resolved.");
        global::HarmonyLib.HarmonyMethod? pre = null;
        global::HarmonyLib.HarmonyMethod? post = null;
        if (prefix != null) pre = new global::HarmonyLib.HarmonyMethod(typeof(Plugin), prefix);
        if (postfix != null) post = new global::HarmonyLib.HarmonyMethod(typeof(Plugin), postfix);
        if (priority.HasValue)
        {
            if (pre != null) pre.priority = priority.Value;
            if (post != null) post.priority = priority.Value;
        }
        _harmony?.Patch(original, prefix: pre, postfix: post);
    }

    private static int Key(object value) => RuntimeHelpers.GetHashCode(value);
    private static bool Bool(object? value, bool fallback = false) { try { return value == null ? fallback : Convert.ToBoolean(value); } catch { return fallback; } }
    private static int Int(object? value, int fallback = 0) { try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; } }

    private static IEnumerable<object?> EnumerateObjects(object collection)
    {
        if (collection is IEnumerable enumerable)
        {
            foreach (object? item in enumerable) yield return item;
            yield break;
        }

        int count = Int(GetMember(collection, "Count"), -1);
        MethodInfo? getItem = collection.GetType().GetMethods(AllInstance)
            .FirstOrDefault(m => m.Name == "get_Item" && m.GetParameters().Length == 1);
        if (count < 0 || getItem == null) yield break;
        for (int i = 0; i < count; i++)
        {
            object? item = null;
            try { item = getItem.Invoke(collection, new object[] { i }); }
            catch { }
            yield return item;
        }
    }

    private static int CollectionCount(object? collection)
    {
        if (collection == null) return 0;
        object? count = GetMember(collection, "Count");
        if (count != null) return Math.Max(0, Int(count, 0));
        int result = 0;
        foreach (object? _ in EnumerateObjects(collection)) result++;
        return result;
    }

    private static void ClearCollection(object? collection)
    {
        if (collection == null) return;
        try
        {
            if (collection is IList list)
            {
                list.Clear();
                return;
            }
            Method(collection.GetType(), "Clear", 0)?.Invoke(collection, null);
        }
        catch { }
    }

    private static object? GetMember(object instance, string name)
    {
        Type? type = instance.GetType();
        while (type != null)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            PropertyInfo? prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanRead) return prop.GetValue(instance);
            type = type.BaseType;
        }
        return null;
    }

    private static bool SetMember(object instance, string name, object? value)
    {
        Type? type = instance.GetType();
        while (type != null)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return true;
            }
            PropertyInfo? prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(instance, value);
                return true;
            }
            type = type.BaseType;
        }
        return false;
    }

    private static bool SetEnumMember(object instance, string name, int value)
    {
        Type? type = instance.GetType();
        while (type != null)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType.IsEnum)
            {
                field.SetValue(instance, Enum.ToObject(field.FieldType, value));
                return true;
            }
            PropertyInfo? prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && prop.PropertyType.IsEnum)
            {
                prop.SetValue(instance, Enum.ToObject(prop.PropertyType, value));
                return true;
            }
            type = type.BaseType;
        }
        return false;
    }

    private static bool SetMemberDeep(object instance, string name, object? value) => SetMember(instance, name, value);

    private static void Log(string message)
    {
        if (_verbose) _logger?.Msg(message);
    }

    private static void Warn(string message)
    {
        if (_verbose) _logger?.Warning(message);
    }

    private static void Error(string message) => _logger?.Error(message);
}
