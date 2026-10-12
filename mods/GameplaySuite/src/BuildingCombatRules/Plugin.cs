using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Items;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Il2CppMenace.Tags;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using UnityEngine;

[assembly: AssemblyVersion("0.1.4.0")]
[assembly: AssemblyFileVersion("0.1.4.0")]
[assembly: AssemblyInformationalVersion("0.1.4")]

namespace MenaceBuildingCombatRules;

public sealed class Plugin : IModpackPlugin
{
    private const string SettingsGroup = "Building Combat Rules";
    private const string Version = "0.1.4";
    private const int CollapseResumeGraceFrames = 6;
    private const int CollapseEjectRetryFrames = 15;
    private const int CollapseStallReportFrames = 600;

    private static MelonLogger.Instance? _logger;
    private static global::HarmonyLib.Harmony? _harmony;
    private static bool _patchesApplied;
    private static bool _protectBuildings = true;
    private static bool _safeCollapse = true;
    private static bool _debug;

    private static MethodInfo? _getEjectionTile;
    private static readonly Dictionary<IntPtr, PendingCollapse> PendingCollapses = new();
    private static readonly HashSet<IntPtr> ResumingCollapses = new();

    // These are deliberately weapon-family identifiers, not actor tags. The rule applies
    // only to direct-fire small arms. Explosive, incendiary, crew-served and anti-vehicle
    // weapons are left completely untouched and may still damage/destroy structures.
    private static readonly string[] SmallArmsTokens =
    {
        "pistol", "revolver", "smg", "pdw", "carbine", "rifle", "assault_rifle", "battle_rifle",
        "sniper_rifle", "designated_marksman", "marksman_rifle", "shotgun", "light_machinegun",
        "light_machine_gun", "light_mg", "plasma_rifle", "laser_rifle", "emp_rifle"
    };

    private static readonly string[] HeavyOrStructuralTokens =
    {
        "grenade", "rocket", "rpg", "missile", "mortar", "launcher", "howitzer", "artillery",
        "tripod", "anti_tank", "anti-tank", "antitank", "anti_materiel", "anti-materiel", "atgm",
        "autocannon", "auto_cannon", "cannon", "chaingun", "chain_gun", "minigun", "recoilless",
        "railgun", "heavy_machinegun", "heavy_machine_gun", "heavy_mg", "medium_machinegun",
        "medium_machine_gun", "medium_mg", "flamethrower", "flame_thrower", "heavy_flamer",
        "demolition", "breach", "charge", "mine", "laser_lance"
    };

    // Garrisonable Buildings changes these vanilla multi-tile structures from
    // ContainerType.None to ContainerType.Building and assigns cover_inside_building.
    // MENACE's native collapse path was reproduced hard-crashing when one of these
    // structures dies, including a 1x2 Civilian Shelter with no live occupant.
    // During Structure.Die only, restore the two template fields changed by Garrisonable
    // to their vanilla values. If the structure is occupied, native death is deferred until
    // the occupant has fully finished its ejection/leave movement; destroying the structure
    // in the same call stack as EjectEntity was reproduced as a native hard crash.
    // The shared template is restored in the postfix, so other live structures remain garrisonable.
    private static readonly HashSet<string> MultiTileGarrisonableTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "building_civilian_shelter_1x2_01",
        "building_civilian_water_storage_plant_1x2",
        "building_pirate_makeshift_storage_01_1x2",
        "building_civilian_water_storage_plant_1x3",
        "building_civilian_water_storage_plant_2x2",
        "building_civilian_water_storage_plant_2x3",
        "building_civilian_greenhouse_01",
        "building_civilian_storage_01"
    };

    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("ProtectBuildingsFromSmallArms", "Small arms cannot damage buildings", true);
            s.AddToggle("SafeOccupiedCollapse", "Safely eject occupants before building collapse", true);
            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLogging", "Debug logging", false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel(SettingsGroup, DrawDebug);
        BindAndPatch();
        DebugLog($"{Version}: initialized. Small-arms structure immunity and occupied-building collapse safety enabled.");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Deferred collapse references are valid only inside the current tactical scene.
        PendingCollapses.Clear();
        ResumingCollapses.Clear();
    }

    public void OnUpdate()
    {
        ReloadSettings();
        ProcessPendingCollapses();
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel(SettingsGroup);
        PendingCollapses.Clear();
        ResumingCollapses.Clear();
    }

    private static void BindAndPatch()
    {
        if (_patchesApplied || _harmony == null) return;

        try
        {
            MethodInfo? damage = typeof(Structure).GetMethod(
                nameof(Structure.OnDamageReceived),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Entity), typeof(Skill), typeof(DamageInfo) },
                modifiers: null);

            MethodInfo? die = typeof(Structure).GetMethod(
                nameof(Structure.Die),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);

            _getEjectionTile = typeof(Entity).GetMethod(
                "GetEjectionTile",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            MethodInfo? transientGetCurrentProperties = typeof(TransientActor).GetMethod(
                nameof(TransientActor.GetCurrentProperties),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            MethodInfo? unitGetCurrentProperties = typeof(UnitActor).GetMethod(
                nameof(UnitActor.GetCurrentProperties),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (damage == null || die == null)
            {
                Error($"Required Structure methods not found. Damage={damage != null}, Die={die != null}.");
                return;
            }

            MethodInfo? tileHit = typeof(AttackHandler).GetMethod(
                nameof(AttackHandler.OnTileHit),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Skill), typeof(Tile), typeof(Tile), typeof(Vector3) },
                modifiers: null);

            _harmony.Patch(damage, prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.StructureDamagePrefix)));
            _harmony.Patch(die,
                prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.StructureDiePrefix)),
                postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.StructureDiePostfix)));

            var actorPropertiesFinalizer = new HarmonyMethod(typeof(Hooks), nameof(Hooks.ActorGetCurrentPropertiesFinalizer));
            if (transientGetCurrentProperties != null)
                _harmony.Patch(transientGetCurrentProperties, finalizer: actorPropertiesFinalizer);
            if (unitGetCurrentProperties != null)
                _harmony.Patch(unitGetCurrentProperties, finalizer: actorPropertiesFinalizer);

            if (tileHit != null)
            {
                _harmony.Patch(tileHit,
                    prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.AttackTileHitPrefix)),
                    postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.AttackTileHitPostfix)));
            }
            _patchesApplied = true;
            DebugLog($"Patched Structure.OnDamageReceived, Structure.Die, Garrisonable invalid-actor guard=" +
                     $"{transientGetCurrentProperties != null && unitGetCurrentProperties != null}, AttackHandler.OnTileHit={tileHit != null}. " +
                     $"Native ejection helper={_getEjectionTile != null}.");
        }
        catch (Exception e)
        {
            Error("Failed to install building combat hooks: " + e);
        }
    }

    private static void OnStructureDamage(Structure structure, Skill? skill, DamageInfo? damageInfo)
    {
        if (!_protectBuildings || structure == null || skill == null || damageInfo == null) return;
        if (!SafeIsBuilding(structure) || !IsSmallArmsSkill(skill)) return;

        // DamageInfo is a reference type. Mutating it here preserves the attack against any
        // contained infantry but removes the collateral structural component before the
        // native Structure.OnDamageReceived path gets a chance to consume building HP.
        int oldDamage = damageInfo.Damage;
        int oldArmorDamage = damageInfo.ArmorDamage;
        if (oldDamage <= 0 && oldArmorDamage <= 0) return;

        damageInfo.Damage = 0;
        damageInfo.ArmorDamage = 0;
        damageInfo.ArmorPenetration = 0;
        damageInfo.IsDamageInflicted = false;
        damageInfo.IsElementDestroyed = false;
        damageInfo.IsTargetDestroyed = false;
        damageInfo.IsContainerDestroyed = false;

        DebugLog($"Blocked small-arms structural damage: building={EntityId(structure)}, skill={SkillId(skill)}, damage={oldDamage}, armorDamage={oldArmorDamage}.");
    }


    private readonly struct CoverDestructionState
    {
        public readonly Attack? Template;
        public readonly HalfCoverClass Original;
        public readonly bool Changed;

        public CoverDestructionState(Attack template, HalfCoverClass original)
        {
            Template = template;
            Original = original;
            Changed = true;
        }
    }

    private static CoverDestructionState BeforeAttackTileHit(AttackHandler handler, Skill? skill, Tile? tile)
    {
        if (!_protectBuildings || handler == null || skill == null || !Valid(tile) || !IsSmallArmsSkill(skill))
            return default;

        try
        {
            Entity? target = tile!.GetEntity();
            if (!Valid(target) || !target!.IsBuilding()) return default;

            Attack? template = handler.GetTemplate();
            if (!Valid(template) || template!.DestroyHalfCover == HalfCoverClass.None) return default;

            HalfCoverClass original = template.DestroyHalfCover;
            template.DestroyHalfCover = HalfCoverClass.None;
            DebugLog($"Suppressed small-arms cover-destruction effect on building tile: building={EntityId(target)}, skill={SkillId(skill)}, destroyHalfCover={original}->None.");
            return new CoverDestructionState(template, original);
        }
        catch (Exception e)
        {
            DebugLog("Cover-destruction guard failed: " + Short(e));
            return default;
        }
    }

    private static void AfterAttackTileHit(CoverDestructionState state)
    {
        if (!state.Changed || !Valid(state.Template)) return;
        try { state.Template!.DestroyHalfCover = state.Original; }
        catch (Exception e) { Error("Failed to restore Attack.DestroyHalfCover after building guard: " + Short(e)); }
    }

    private sealed class PendingCollapse
    {
        public Structure Structure { get; init; } = null!;
        public Entity Occupant { get; init; } = null!;
        public bool Quiet { get; init; }
        public int Frames { get; set; }
        public int SettledFrames { get; set; }
        public int RetryCountdown { get; set; }
        public bool EjectionRequested { get; set; }
        public bool StallReported { get; set; }
    }

    private readonly struct CollapseTemplateState
    {
        public readonly EntityTemplate? Template;
        public readonly EntityContainerType OriginalContainerType;
        public readonly InsideCoverTemplate? OriginalInsideCover;
        public readonly bool Changed;

        public CollapseTemplateState(EntityTemplate template)
        {
            Template = template;
            OriginalContainerType = template.ContainerType;
            OriginalInsideCover = template.ProvidesCoverInside;
            Changed = true;
        }
    }

    private static CollapseTemplateState BeforeStructureDie(Structure structure, bool forceSafeCollapse = false)
    {
        if ((!_safeCollapse && !forceSafeCollapse) || structure == null || !SafeIsBuilding(structure)) return default;

        // Multi-tile civilian structures are vanilla non-containers. Garrisonable Buildings
        // changes only ContainerType and ProvidesCoverInside to make them enterable. Restore
        // those fields for the synchronous native death path, then put them back in the postfix.
        // This is intentionally limited to the exact multi-tile Garrisonable whitelist.
        try
        {
            EntityTemplate? template = structure.GetTemplate();
            if (template == null) return default;

            string id = template.GetID() ?? string.Empty;
            if (!MultiTileGarrisonableTemplates.Contains(id)) return default;
            if (template.ContainerType != EntityContainerType.Building) return default;

            var state = new CollapseTemplateState(template);
            template.ContainerType = EntityContainerType.None;
            template.ProvidesCoverInside = null;
            DebugLog($"Normalized multi-tile garrison template for native collapse: building={id}, ContainerType=Building->None, insideCover=cleared.");
            return state;
        }
        catch (Exception e)
        {
            Error("Multi-tile collapse template normalization failed: " + Short(e));
            return default;
        }
    }

    private static bool TryConsumeResumingCollapse(Structure structure)
    {
        if (structure == null) return false;
        try
        {
            IntPtr key = structure.Pointer;
            return key != IntPtr.Zero && ResumingCollapses.Remove(key);
        }
        catch { return false; }
    }

    private static bool ShouldDeferStructureDie(Structure structure, bool quiet)
    {
        if (!_safeCollapse || structure == null || !SafeIsBuilding(structure)) return false;

        IntPtr key;
        try { key = structure.Pointer; }
        catch { return false; }
        if (key == IntPtr.Zero) return false;

        // Repeated lethal hits while an occupant is already leaving must not restart the native
        // death path or request another ejection.
        if (PendingCollapses.ContainsKey(key)) return true;

        if (!IsManagedMultiTileGarrison(structure)) return false;

        Entity? occupant;
        try { occupant = structure.GetContainedEntity(); }
        catch { return false; }
        if (!Valid(occupant)) return false;

        var pending = new PendingCollapse
        {
            Structure = structure,
            Occupant = occupant!,
            Quiet = quiet,
            Frames = 0,
            SettledFrames = 0,
            RetryCountdown = 0,
            EjectionRequested = false,
            StallReported = false
        };
        PendingCollapses[key] = pending;

        // If MENACE has already started an exit (for example a morale/fleeing reaction), do not
        // issue a second EjectEntity call. Just let that movement finish before resuming collapse.
        if (!IsActorMoving(occupant!))
            pending.EjectionRequested = TryRequestCollapseEjection(structure, occupant!);

        DebugLog($"Deferred occupied multi-tile collapse until ejection movement finishes: building={EntityId(structure)}, occupant={EntityId(occupant!)}.");
        return true;
    }

    private static void ProcessPendingCollapses()
    {
        if (PendingCollapses.Count == 0) return;

        foreach (IntPtr key in PendingCollapses.Keys.ToArray())
        {
            if (!PendingCollapses.TryGetValue(key, out PendingCollapse? pending)) continue;
            pending.Frames++;

            if (!Valid(pending.Structure))
            {
                PendingCollapses.Remove(key);
                continue;
            }

            bool occupantValid = Valid(pending.Occupant);
            bool occupantMoving = occupantValid && IsActorMoving(pending.Occupant);
            bool stillContained = occupantValid && IsStillContainedBy(pending.Structure, pending.Occupant);

            if (stillContained)
            {
                pending.SettledFrames = 0;
                if (occupantMoving) continue;

                if (pending.RetryCountdown > 0)
                {
                    pending.RetryCountdown--;
                    continue;
                }

                pending.EjectionRequested = TryRequestCollapseEjection(pending.Structure, pending.Occupant);
                pending.RetryCountdown = CollapseEjectRetryFrames;

                if (!pending.EjectionRequested && pending.Frames >= CollapseStallReportFrames && !pending.StallReported)
                {
                    pending.StallReported = true;
                    Error($"Occupied multi-tile collapse remains deferred because MENACE could not eject the occupant: building={EntityId(pending.Structure)}, occupant={EntityId(pending.Occupant)}.");
                }
                continue;
            }

            // EjectEntity clears containment before its visual/path movement is necessarily done.
            // Wait for Actor.IsMoving() to become false, then count a fresh grace window after
            // movement has actually settled so native movement-finished callbacks can unwind.
            if (occupantMoving)
            {
                pending.SettledFrames = 0;
                continue;
            }

            pending.SettledFrames++;
            if (pending.SettledFrames < CollapseResumeGraceFrames) continue;

            PendingCollapses.Remove(key);
            ResumingCollapses.Add(key);

            try
            {
                DebugLog($"Resuming deferred multi-tile collapse after occupant exit completed: building={EntityId(pending.Structure)}, occupant={EntityId(pending.Occupant)}.");
                pending.Structure.Die(pending.Quiet);
            }
            catch (Exception e)
            {
                ResumingCollapses.Remove(key);
                Error("Deferred occupied-building collapse failed: " + Short(e));
            }
        }
    }

    private static bool IsManagedMultiTileGarrison(Structure structure)
    {
        try
        {
            EntityTemplate? template = structure.GetTemplate();
            if (template == null) return false;
            string id = template.GetID() ?? string.Empty;
            return MultiTileGarrisonableTemplates.Contains(id) && template.ContainerType == EntityContainerType.Building;
        }
        catch { return false; }
    }

    private static bool IsStillContainedBy(Structure structure, Entity occupant)
    {
        try
        {
            Entity? current = occupant.GetContainerEntity();
            if (Valid(current) && current!.Pointer == structure.Pointer) return true;
        }
        catch { }

        try
        {
            Entity? current = structure.GetContainedEntity();
            return Valid(current) && current!.Pointer == occupant.Pointer;
        }
        catch { return false; }
    }

    private static bool IsActorMoving(Entity entity)
    {
        try
        {
            Actor? actor = entity.ToActor();
            return Valid(actor) && actor!.IsMoving();
        }
        catch { return false; }
    }

    private static void AfterStructureDie(CollapseTemplateState state)
    {
        if (!state.Changed || state.Template == null) return;

        try
        {
            state.Template.ContainerType = state.OriginalContainerType;
            state.Template.ProvidesCoverInside = state.OriginalInsideCover;
            DebugLog($"Restored garrison template after native collapse: building={state.Template.GetID() ?? "-"}.");
        }
        catch (Exception e)
        {
            Error("Failed to restore multi-tile garrison template after collapse: " + Short(e));
        }
    }

    private static bool TryRequestCollapseEjection(Structure structure, Entity occupant)
    {
        if (!Valid(structure) || !Valid(occupant)) return false;

        try
        {
            Tile? ejectTile = null;
            bool nativeHelperAvailable = _getEjectionTile != null;
            if (nativeHelperAvailable)
            {
                try { ejectTile = _getEjectionTile!.Invoke(structure, null) as Tile; }
                catch (Exception e) { DebugLog("Native ejection-tile lookup failed: " + Short(e)); }
            }

            // Fall back to the structure anchor only when the private native helper itself is
            // unavailable on this game build. If the helper exists but returns no tile, respect
            // that result and retry on a later frame rather than forcing an unsafe ejection.
            if (!nativeHelperAvailable && !Valid(ejectTile))
            {
                try { ejectTile = structure.GetTile(); }
                catch { ejectTile = null; }
            }

            bool ejected = Valid(ejectTile) && structure.EjectEntity(ejectTile!);
            if (ejected)
            {
                DebugLog($"Requested pre-collapse occupant ejection: building={EntityId(structure)}, occupant={EntityId(occupant)}.");
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            DebugLog("Occupied-building ejection request failed: " + Short(e));
            return false;
        }
    }

    private static Exception? HandleActorPropertiesException(Exception? exception, ref EntityProperties? result)
    {
        if (exception == null) return null;

        // Garrisonable Buildings probes actor properties through reflection while evaluating
        // movement/container state. MENACE can invalidate a transient/unit actor between the
        // probe and MethodInfo.Invoke, which turns the native failure into a noisy
        // TargetInvocationException in that mod. Only suppress failures whose live managed
        // call stack clearly originates from Garrisonable Buildings; every other caller keeps
        // vanilla exception behavior. Returning null is the same safe "actor unavailable"
        // result that Garrisonable already handles in its discovery paths.
        string stack;
        try { stack = Environment.StackTrace ?? string.Empty; }
        catch { return exception; }

        bool fromGarrisonable =
            stack.IndexOf("GarrisonableBuildings", StringComparison.OrdinalIgnoreCase) >= 0 ||
            stack.IndexOf("Garrisonable_Buildings", StringComparison.OrdinalIgnoreCase) >= 0 ||
            stack.IndexOf("Garrisonable Buildings", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!fromGarrisonable) return exception;

        result = null;
        DebugLog("Suppressed invalid Actor.GetCurrentProperties failure from Garrisonable Buildings: " + Short(exception));
        return null;
    }

    private static bool IsSmallArmsSkill(Skill skill)
    {
        string skillId = SkillId(skill).ToLowerInvariant();
        string itemId = "";
        Item? item = null;
        try
        {
            item = skill.GetItem();
            itemId = item?.GetID()?.ToLowerInvariant() ?? "";
        }
        catch { }

        // Prefer MENACE's native weapon tags when an item-backed skill provides them.
        // Explicit structural/explosive tags always win over the name fallback.
        try
        {
            BaseItemTemplate? template = item?.GetBaseItemTemplate();
            if (template != null)
            {
                if (template.HasTag(TagType.ANTI_STRUCTURE) ||
                    template.HasTag(TagType.VEHICLE_WEAPON) ||
                    template.HasTag(TagType.INDIRECT_FIRE) ||
                    template.HasTag(TagType.GRENADE) ||
                    template.HasTag(TagType.ROCKET) ||
                    template.HasTag(TagType.MINE))
                    return false;

                if (template.HasTag(TagType.SMG) ||
                    template.HasTag(TagType.ASSAULT_RIFLE) ||
                    template.HasTag(TagType.BATTLE_RIFLE) ||
                    template.HasTag(TagType.SHOTGUN) ||
                    template.HasTag(TagType.SNIPER))
                    return true;
            }
        }
        catch { }

        string id = skillId + " " + itemId;
        if (HeavyOrStructuralTokens.Any(id.Contains)) return false;
        return SmallArmsTokens.Any(id.Contains);
    }

    private static string SkillId(Skill skill)
    {
        try { return skill.GetID() ?? "-"; }
        catch { return "-"; }
    }

    private static string EntityId(Entity entity)
    {
        try { return entity.GetTemplate()?.GetID() ?? entity.ToString() ?? "-"; }
        catch
        {
            try { return entity.ToString() ?? "-"; }
            catch { return "-"; }
        }
    }

    private static bool SafeIsBuilding(Structure structure)
    {
        try { return structure.IsBuilding(); }
        catch { return false; }
    }

    private static bool Valid(Il2CppObjectBase? obj)
    {
        if (obj == null) return false;
        try { return obj.Pointer != IntPtr.Zero; }
        catch { return false; }
    }

    private static string Short(Exception e)
    {
        Exception x = e;
        while (x is TargetInvocationException tie && tie.InnerException != null) x = tie.InnerException;
        return $"{x.GetType().Name}: {x.Message}";
    }

    private static void ReloadSettings()
    {
        _protectBuildings = Setting("ProtectBuildingsFromSmallArms", true);
        _safeCollapse = Setting("SafeOccupiedCollapse", true);
        _debug = Setting("DebugLogging", false);
    }

    private static void OnSettingChanged(string group, string key, object value)
    {
        if (string.Equals(group, SettingsGroup, StringComparison.OrdinalIgnoreCase)) ReloadSettings();
    }

    private static T Setting<T>(string key, T fallback)
    {
        try { return ModSettings.Get<T>(SettingsGroup, key); }
        catch { return fallback; }
    }

    private static void DrawDebug(Rect area)
    {
        GUI.Label(new Rect(area.x + 8, area.y + 8, Math.Max(120, area.width - 16), 22),
            $"Building Combat Rules {Version} | small-arms immunity={_protectBuildings} | safe collapse={_safeCollapse}");
    }

    private static void DebugLog(string message)
    {
        if (_debug) _logger?.Msg($"[BuildingCombatRules] {message}");
    }

    private static void Error(string message) => _logger?.Error($"[BuildingCombatRules] {message}");

    private static class Hooks
    {
        public static void StructureDamagePrefix(Structure __instance, Entity __0, Skill __1, DamageInfo __2)
        {
            try { OnStructureDamage(__instance, __1, __2); }
            catch (Exception e) { Error("Structure damage hook failed: " + Short(e)); }
        }

        public static bool StructureDiePrefix(Structure __instance, bool __0, out CollapseTemplateState __state)
        {
            __state = default;
            try
            {
                // A deferred collapse is allowed through exactly once. Force the existing
                // multi-tile template normalization even if the user changed the setting while
                // the occupant was leaving; otherwise the resumed native death could regress.
                bool resuming = TryConsumeResumingCollapse(__instance);
                if (!resuming && ShouldDeferStructureDie(__instance, __0)) return false;
                __state = BeforeStructureDie(__instance, forceSafeCollapse: resuming);
            }
            catch (Exception e)
            {
                Error("Structure death hook failed: " + Short(e));
            }
            return true;
        }

        public static void StructureDiePostfix(CollapseTemplateState __state)
        {
            try { AfterStructureDie(__state); }
            catch (Exception e) { Error("Structure death restore hook failed: " + Short(e)); }
        }

        public static Exception? ActorGetCurrentPropertiesFinalizer(Exception? __exception, ref EntityProperties? __result)
        {
            try { return HandleActorPropertiesException(__exception, ref __result); }
            catch { return __exception; }
        }

        public static void AttackTileHitPrefix(AttackHandler __instance, Skill __0, Tile __1, Tile __2, Vector3 __3, ref CoverDestructionState __state)
        {
            try { __state = BeforeAttackTileHit(__instance, __0, __1); }
            catch (Exception e) { Error("Attack tile-hit prefix failed: " + Short(e)); }
        }

        public static void AttackTileHitPostfix(CoverDestructionState __state)
        {
            try { AfterAttackTileHit(__state); }
            catch (Exception e) { Error("Attack tile-hit postfix failed: " + Short(e)); }
        }
    }
}
