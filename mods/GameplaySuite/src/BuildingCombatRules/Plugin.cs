using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Items;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Il2CppMenace.Tags;
using MenaceModSettings;
using MenaceModSuite.Shared;
using UnityEngine;

[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
[assembly: AssemblyInformationalVersion("0.3.0")]

namespace MenaceBuildingCombatRules;

// Host-agnostic core; the Jiangyu host binding lives in mods/jiangyu/BuildingCombatRules/code.
public sealed partial class Plugin : IHostedPlugin
{
    private const string SettingsGroup = "Building Combat Rules";
    private const string Version = "0.3.0";

    private static ModLog? _logger;
    private static global::HarmonyLib.Harmony? _harmony;
    private static bool _patchesApplied;
    private static bool _protectBuildings = true;
    private static bool _protectVegetation = true;
    private static bool _debug;

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

    // Safe collapse of occupied multi-tile garrisons lived here in 0.1.2-0.2.0; it is part of
    // Garrisonable Buildings since 1.1.0 (GitHub #50).

    public void Initialize(ModLog logger, global::HarmonyLib.Harmony harmony)
    {
        _logger = logger;
        _harmony = harmony;

        ModSettings.Register(SettingsGroup, s =>
        {
            s.AddHeader("Gameplay");
            s.AddToggle("ProtectBuildingsFromSmallArms", "Small arms cannot damage buildings", true);
            s.AddToggle("ProtectVegetationFromSmallArms", "Small arms cannot damage trees and forest", true);
            s.AddHeader("Diagnostics");
            s.AddToggle("DebugLogging", "Debug logging", false);
        });

        ReloadSettings();
        ModSettings.OnSettingChanged += OnSettingChanged;
        DevConsole.RegisterPanel(SettingsGroup, DrawDebug);
        BindAndPatch();
        DebugLog($"{Version}: initialized. Small-arms structure immunity enabled.");
    }

    public void OnUpdate()
    {
        ReloadSettings();
    }

    public void OnUnload()
    {
        ModSettings.OnSettingChanged -= OnSettingChanged;
        DevConsole.RemovePanel(SettingsGroup);
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

            if (damage == null)
            {
                Error("Structure.OnDamageReceived not found.");
                return;
            }

            MethodInfo? tileHit = typeof(AttackHandler).GetMethod(
                nameof(AttackHandler.OnTileHit),
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(Skill), typeof(Tile), typeof(Tile), typeof(Vector3) },
                modifiers: null);

            _harmony.Patch(damage, prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.StructureDamagePrefix)));

            if (tileHit != null)
            {
                _harmony.Patch(tileHit,
                    prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.AttackTileHitPrefix)),
                    postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.AttackTileHitPostfix)));
            }
            _patchesApplied = true;
            DebugLog($"Patched Structure.OnDamageReceived, AttackHandler.OnTileHit={tileHit != null}.");
        }
        catch (Exception e)
        {
            Error("Failed to install building combat hooks: " + e);
        }
    }

    private static void OnStructureDamage(Structure structure, Skill? skill, DamageInfo? damageInfo)
    {
        if (structure == null || skill == null || damageInfo == null) return;
        if (!IsProtectedStructure(structure) || !IsSmallArmsSkill(skill)) return;

        // DamageInfo is a reference type. Mutating it here preserves the attack against any
        // contained infantry but removes the collateral structural component before the
        // native Structure.OnDamageReceived path gets a chance to consume structure HP.
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

        DebugLog($"Blocked small-arms structural damage: structure={EntityId(structure)}, skill={SkillId(skill)}, damage={oldDamage}, armorDamage={oldArmorDamage}.");
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
        if ((!_protectBuildings && !_protectVegetation) || handler == null || skill == null || !Valid(tile) || !IsSmallArmsSkill(skill))
            return default;

        try
        {
            Entity? target = tile!.GetEntity();
            if (!IsProtectedStructure(target)) return default;

            Attack? template = handler.GetTemplate();
            if (!Valid(template) || template!.DestroyHalfCover == HalfCoverClass.None) return default;

            HalfCoverClass original = template.DestroyHalfCover;
            template.DestroyHalfCover = HalfCoverClass.None;
            DebugLog($"Suppressed small-arms cover-destruction effect on protected tile: structure={EntityId(target!)}, skill={SkillId(skill)}, destroyHalfCover={original}->None.");
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

        // A rifle-backed grenade/launcher mode is not a personal-weapon bullet.
        // Reject its explicit heavy/explosive ID before accepting the item's rifle tag.
        string id = skillId + " " + itemId;
        if (HeavyOrStructuralTokens.Any(id.Contains)) return false;

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

    private static bool IsProtectedStructure(Entity? entity)
    {
        if (!Valid(entity)) return false;
        try
        {
            return entity!.IsStructure() &&
                ((_protectBuildings && entity.IsBuilding()) || (_protectVegetation && entity.IsVegetation()));
        }
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
        _protectVegetation = Setting("ProtectVegetationFromSmallArms", true);
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
            $"Building Combat Rules {Version} | buildings={_protectBuildings} | trees={_protectVegetation}");
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
