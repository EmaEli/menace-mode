using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Items;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tags;
using Il2CppTactical;

namespace MenaceOverwatch;

internal enum WeaponRole
{
    Unknown,
    Squad,
    Leader
}

internal static class Native
{
    // MENACE 0.7.15 replaced Entity.IsPlayerControlled(bool) with two explicit methods:
    // IsPlayer() and IsPlayerOrPlayerAI(). Resolve by name so this source can still be
    // compiled against an older generated interop assembly while using the current API
    // once MelonLoader regenerates Assembly-CSharp.dll for the updated game.
    private static readonly MethodInfo? IsPlayerMethod = typeof(Actor).GetMethod(
        "IsPlayer", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
    private static readonly MethodInfo? IsPlayerOrPlayerAiMethod = typeof(Actor).GetMethod(
        "IsPlayerOrPlayerAI", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
    private static readonly MethodInfo? LegacyIsPlayerControlledMethod = typeof(Actor).GetMethod(
        "IsPlayerControlled", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);

    private static readonly Func<Actor, bool>? IsPlayerDelegate = CreateBoolDelegate(IsPlayerMethod);
    private static readonly Func<Actor, bool>? IsPlayerOrPlayerAiDelegate = CreateBoolDelegate(IsPlayerOrPlayerAiMethod);
    private static readonly Func<Actor, bool, bool>? LegacyIsPlayerControlledDelegate = CreateLegacyPlayerControlledDelegate();

    private static Func<Actor, bool>? CreateBoolDelegate(MethodInfo? method)
    {
        try { return method?.CreateDelegate(typeof(Func<Actor, bool>)) as Func<Actor, bool>; }
        catch { return null; }
    }

    private static Func<Actor, bool, bool>? CreateLegacyPlayerControlledDelegate()
    {
        try { return LegacyIsPlayerControlledMethod?.CreateDelegate(typeof(Func<Actor, bool, bool>)) as Func<Actor, bool, bool>; }
        catch { return null; }
    }

    public static bool IsPlayerControlled(Actor actor, bool includePlayerAi = false)
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

            // Legacy fallback for pre-update interop assemblies.
            if (LegacyIsPlayerControlledDelegate != null)
                return LegacyIsPlayerControlledDelegate(actor, includePlayerAi);
            if (LegacyIsPlayerControlledMethod != null)
                return Convert.ToBoolean(LegacyIsPlayerControlledMethod.Invoke(actor, new object[] { includePlayerAi }) ?? false);
        }
        catch { }
        return false;
    }
    // MENACE also removed the legacy HasLineOfSightTo(Entity, bool, Tile, Tile) overload.
    // Resolve the current 3-argument form first while retaining the old form as fallback.
    private static readonly MethodInfo? ActorLineOfSightMethod = typeof(Actor).GetMethod(
        "HasLineOfSightTo", BindingFlags.Public | BindingFlags.Instance, null,
        new[] { typeof(Entity), typeof(Tile), typeof(Tile) }, null);
    private static readonly MethodInfo? LegacyActorLineOfSightMethod = typeof(Actor).GetMethod(
        "HasLineOfSightTo", BindingFlags.Public | BindingFlags.Instance, null,
        new[] { typeof(Entity), typeof(bool), typeof(Tile), typeof(Tile) }, null);

    private static bool HasActorLineOfSight(Actor shooter, Entity target, Tile fromTile, Tile atTile)
    {
        try
        {
            if (ActorLineOfSightMethod != null)
                return Convert.ToBoolean(ActorLineOfSightMethod.Invoke(shooter, new object[] { target, fromTile, atTile }) ?? false);
            if (LegacyActorLineOfSightMethod != null)
                return Convert.ToBoolean(LegacyActorLineOfSightMethod.Invoke(shooter, new object[] { target, false, fromTile, atTile }) ?? false);
        }
        catch { }
        return false;
    }

    public static long Key(Il2CppObjectBase? value) => value?.Pointer.ToInt64() ?? 0;
    public static bool Valid(Il2CppObjectBase? value) => Key(value) != 0;
    public static bool Same(Il2CppObjectBase? a, Il2CppObjectBase? b) => Key(a) != 0 && Key(a) == Key(b);

    public static int FactionId(Actor actor)
    {
        if (!Valid(actor)) return -1;

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
        catch { return -1; }
    }

    public static bool IsPlayerAlliedTarget(Actor actor, out int factionId)
    {
        factionId = FactionId(actor);
        return factionId >= 1 && factionId <= 4;
    }

    public static bool IsVisibleToPlayer(Actor actor)
    {
        if (!Valid(actor)) return false;
        try
        {
            if (IsPlayerControlled(actor)) return true;
        }
        catch { }

        // Prefer the actor/model visibility state so an Overwatch marker never reveals a
        // concealed enemy merely because the underlying tile itself is visible.
        try { return actor.Is3DModelVisibleToPlayer(); }
        catch { }

        try
        {
            var tile = actor.GetTile();
            return Valid(tile) && tile.IsVisibleToPlayer();
        }
        catch { return false; }
    }

    public static bool IsCombatReady(Actor actor) => Valid(actor) && actor.IsAlive() && !actor.IsDying()
        && !actor.IsLeavingMap() && !actor.IsStunned() && actor.GetMoraleState() != MoraleState.Fleeing;

    public static bool IsInfantryActor(Actor actor)
    {
        try { return Valid(actor) && actor.IsInfantry(); }
        catch { return false; }
    }

    public static bool IsVehicleActor(Actor actor)
    {
        try { return Valid(actor) && actor.IsVehicle(); }
        catch { return false; }
    }

    public static WeaponRole Role(Skill skill)
    {
        if (!Valid(skill) || !Valid(skill.GetItem())) return WeaponRole.Unknown;
        var itemTemplate = skill.GetItem().GetTemplate();
        if (!Valid(itemTemplate)) return WeaponRole.Unknown;
        return itemTemplate.SlotType switch
        {
            ItemSlot.InfantryWeapon => WeaponRole.Squad,
            ItemSlot.InfantrySpecial => WeaponRole.Leader,
            _ => WeaponRole.Unknown
        };
    }

    public static bool IsAlphaStrikeSkill(Skill skill)
    {
        if (!Valid(skill)) return false;
        string id = "";
        string title = "";
        try { id = skill.GetID() ?? ""; } catch { }
        try { title = skill.GetTitle() ?? ""; } catch { }

        return id.Contains("alpha_strike", StringComparison.OrdinalIgnoreCase)
            || id.Contains("alphastrike", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Alpha Strike", StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequirementsSatisfied(Skill skill, Actor actor)
    {
        if (!Valid(skill) || !Valid(actor)) return false;
        var t = skill.GetTemplate();
        if (!Valid(t)) return false;
        if (t.IsDeploymentRequired && !t.IsDeploymentRequirementSatisfied(actor)) return false;
        if (t.IsWeaponSetupRequired && !t.IsSetupRequirementSatisfied(actor)) return false;
        return true;
    }

    public static bool IsIndirectFire(Skill skill)
    {
        if (!Valid(skill)) return true;
        try
        {
            // Classify the executed attack, not sibling attacks granted by its item.
            // Recursive item tags misclassify the K-PAC rifle when its launcher is indirect.
            var attackTemplate = skill.GetTemplate();
            if (!Valid(attackTemplate)) return true;
            var tags = attackTemplate.Tags;
            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    var tag = tags[i];
                    if (Valid(tag) && tag.TagType == TagType.INDIRECT_FIRE) return true;
                }
            }

            var item = skill.GetItem();
            // Native built-in vehicle/turret attacks may have no conventional item.
            if (!Valid(item)) return false;
            var itemTemplate = item.GetTemplate();
            if (!Valid(itemTemplate)) return true;
            return itemTemplate.HasTag(TagType.INDIRECT_FIRE, false);
        }
        catch { return true; } // Metadata errors must not admit an unknown attack.
    }

    private static bool SupportedBase(Skill skill)
    {
        if (!Valid(skill) || skill.IsGarbage() || IsIndirectFire(skill)) return false;
        var t = skill.GetTemplate();
        return Valid(t) && skill.IsAttack() && skill.IsActive() && skill.IsTargeted()
            && !skill.IsMovementSkill() && !skill.IsBackgroundOnly() && skill.GetMaxRange() > 0;
    }

    public static bool SupportedInfantry(Skill skill)
        => SupportedBase(skill) && Valid(skill.GetItem()) && Role(skill) != WeaponRole.Unknown;

    public static bool SupportedVehicle(Skill skill)
    {
        // Vehicle/turret attacks are not guaranteed to expose a conventional item. Player UI
        // still hides hidden skills, while AI discovery may intentionally include hidden native
        // attacks so hostile vehicles can reserve their built-in guns for Overwatch.
        return SupportedBase(skill);
    }

    public static bool SupportedForActor(Actor actor, Skill skill)
    {
        if (IsInfantryActor(actor)) return SupportedInfantry(skill);
        if (IsVehicleActor(actor)) return SupportedVehicle(skill);
        return false;
    }

    public static List<Skill> Attacks(Actor actor, WeaponRole role)
    {
        var result = new List<Skill>();
        if (!Valid(actor) || !IsInfantryActor(actor)) return result;
        var attacks = actor.GetAllAttacks();
        if (attacks == null) return result;
        for (int i = 0; i < attacks.Count; i++)
        {
            var s = attacks[i];
            if (SupportedInfantry(s) && Role(s) == role && !s.IsHidden() && !result.Any(x => Same(x, s))) result.Add(s);
        }
        return result;
    }

    public static List<Skill> VehicleAttacks(Actor actor, bool alphaStrikeOnly, bool includeHidden = false)
    {
        var result = new List<Skill>();
        if (!Valid(actor) || !IsVehicleActor(actor)) return result;
        var attacks = actor.GetAllAttacks();
        if (attacks == null) return result;

        for (int i = 0; i < attacks.Count; i++)
        {
            var s = attacks[i];
            if (!SupportedVehicle(s) || (!includeHidden && s.IsHidden())) continue;
            bool alpha = IsAlphaStrikeSkill(s);
            if (alpha != alphaStrikeOnly) continue;
            if (!result.Any(x => Same(x, s))) result.Add(s);
        }

        return result
            .OrderBy(s => s.GetActionPointCost())
            .ThenByDescending(s => s.GetMaxRange())
            .ThenBy(s => Title(s), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool CanArm(Actor actor, Skill skill, out string reason)
    {
        reason = "Ready.";
        if (!IsCombatReady(actor)) { reason = "Unit cannot enter Overwatch in its current state."; return false; }
        if (IsIndirectFire(skill)) { reason = "Indirect-fire weapons cannot use Overwatch."; return false; }
        if (!SupportedForActor(actor, skill)) { reason = "Select a supported ranged attack."; return false; }
        if (!RequirementsSatisfied(skill, actor)) { reason = "Weapon deployment/setup requirements are not satisfied."; return false; }
        if (!skill.IsEnabled() || skill.IsHidden() || !skill.HasExecutingElements()) { reason = "Attack is unavailable."; return false; }
        if (!skill.IsUsable(TargetUsageParams.Player)) { reason = "Attack is not currently usable."; return false; }
        if (skill.HasLimitedUses() && (skill.IsOutOfUses() || skill.GetUses() <= 0)) { reason = "No ammunition/uses remaining."; return false; }
        int cost = skill.GetActionPointCost();
        if (cost <= 0) { reason = "Zero-cost attacks are not used for Overwatch."; return false; }
        if (actor.GetActionPoints() < cost) { reason = $"Requires {cost} AP."; return false; }
        return true;
    }

    // AI Overwatch is reserved at the end of an AI activation, when the actor may already be
    // marked turn-done or no longer be the active actor. In that state Skill.IsUsable(Player)
    // can legitimately return false even though the weapon, ammo and leftover AP are valid for
    // a later reaction shot. Keep all physical/weapon checks, but do not require active-turn
    // usability for this end-of-activation reservation.
    public static bool CanReserveAI(Actor actor, Skill skill, int availableAp, out string reason)
    {
        reason = "Ready.";
        if (!IsCombatReady(actor)) { reason = "unit is not combat-ready"; return false; }
        if (IsIndirectFire(skill)) { reason = "indirect-fire weapons cannot use Overwatch"; return false; }
        if (!SupportedForActor(actor, skill)) { reason = "not a supported ranged attack"; return false; }
        if (!RequirementsSatisfied(skill, actor)) { reason = "deployment/setup requirement is not satisfied"; return false; }
        // Hidden is a UI property for several native enemy/vehicle attacks. AI reservation may
        // use such a skill as long as the underlying attack is enabled and executable.
        if (!skill.IsEnabled() || !skill.HasExecutingElements()) { reason = "attack is disabled/unexecutable"; return false; }
        if (skill.HasLimitedUses() && (skill.IsOutOfUses() || skill.GetUses() <= 0)) { reason = "no ammunition/uses remaining"; return false; }
        int cost = skill.GetActionPointCost();
        if (cost <= 0) { reason = "zero-cost attacks are not used for Overwatch"; return false; }
        if (availableAp < cost) { reason = $"only {availableAp} AP remain; attack costs {cost}"; return false; }
        return true;
    }

    public static int AmmoBound(Skill skill)
    {
        if (!Valid(skill)) return 0;
        if (!skill.HasLimitedUses()) return int.MaxValue;
        return Math.Max(0, skill.GetUses());
    }

    public static bool CanReact(Actor shooter, Skill skill, Actor target, Tile tile)
        => CanReact(shooter, skill, target, tile, out _);

    public static bool CanReact(Actor shooter, Skill skill, Actor target, Tile tile, out string reason)
    {
        reason = "Ready.";
        if (!IsCombatReady(shooter)) { reason = "shooter is not combat-ready"; return false; }
        if (!IsCombatReady(target)) { reason = "target is not combat-ready"; return false; }
        Tile source = shooter.GetTile();
        if (!Valid(tile) || !Valid(source)) { reason = "missing source/target tile"; return false; }
        if (IsIndirectFire(skill)) { reason = "indirect-fire weapons cannot use Overwatch"; return false; }
        if (!SupportedForActor(shooter, skill)) { reason = "skill is not a supported ranged attack for this actor"; return false; }
        if (!RequirementsSatisfied(skill, shooter)) { reason = "deployment/setup requirement is no longer satisfied"; return false; }
        // Enemy vehicle weapons can be hidden from player-facing UI while remaining valid native
        // attacks. Permit hidden skills for non-player shooters; player reactions stay strict.
        if (!skill.IsEnabled() || (IsPlayerControlled(shooter) && skill.IsHidden()) || !skill.HasExecutingElements()) { reason = "skill is disabled/hidden"; return false; }
        if (skill.HasLimitedUses() && (skill.IsOutOfUses() || skill.GetUses() <= 0)) { reason = "weapon has no uses/ammo"; return false; }

        int distance = shooter.GetDistanceTo(tile);
        int minRange = Math.Max(0, skill.GetMinRange());
        int maxRange = Math.Max(minRange, skill.GetMaxRange());
        if (distance < minRange || distance > maxRange)
        { reason = $"out of range (distance={distance}, allowed={minRange}-{maxRange})"; return false; }

        if (IsVehicleActor(shooter))
        {
            try
            {
                // v0.7.16 IsApplicableTo only queries handlers; it does NOT perform this
                // native range/cone check. Use the game's actual facing and ConeAngle,
                // not a guessed hull/turret angle or a free pre-shot rotation.
                // CanReact runs both when queueing and immediately before Skill.Use.
                if (!skill.IsInRange(tile, source, true))
                { reason = "outside native vehicle weapon range/firing sector"; return false; }
            }
            catch (Exception e)
            {
                reason = "native vehicle firing-sector check unavailable: " + e.GetType().Name;
                return false;
            }
        }

        if (!source.HasLineOfSightTo(tile, LineOfSightFlags.Default))
        { reason = "no tile line of sight"; return false; }

        bool actorLos = HasActorLineOfSight(shooter, target, source, tile);
        if (!actorLos)
        { reason = "target is not visible/detectable to the shooter"; return false; }

        if (!skill.IsApplicableTo(tile, target))
        { reason = "skill is not applicable to the target entity/tile"; return false; }

        return true;
    }

    private static EntityProperties? CurrentProperties(Actor actor, out string concreteType)
    {
        concreteType = "unknown";
        if (!Valid(actor)) return null;

        try
        {
            var unit = actor.TryCast<UnitActor>();
            if (unit != null)
            {
                concreteType = "UnitActor";
                return unit.GetCurrentProperties();
            }
        }
        catch { }

        try
        {
            var transient = actor.TryCast<TransientActor>();
            if (transient != null)
            {
                concreteType = "TransientActor";
                return transient.GetCurrentProperties();
            }
        }
        catch { }

        return null;
    }

    public static bool TryHitChance(Actor shooter, Skill skill, Actor target, Tile tile, int aimPenalty,
        out int nativeChance, out int effectiveChance, out string detail)
    {
        nativeChance = effectiveChance = -1;
        detail = "not evaluated";
        try
        {
            var shooterProperties = CurrentProperties(shooter, out string shooterType);
            var defenderProperties = CurrentProperties(target, out string targetType);
            if (!Valid(shooterProperties) || !Valid(defenderProperties))
            {
                detail = $"could not resolve concrete EntityProperties (shooter={shooterType}, target={targetType})";
                return false;
            }

            var hit = skill.GetHitchance(
                shooter.GetTile(),
                tile,
                shooterProperties!,
                defenderProperties!,
                true,
                target,
                true);

            float raw = hit.FinalValue;
            detail = $"raw={raw:0.###} acc={hit.Accuracy:0.###} cover={hit.CoverMult:0.###} defense={hit.DefenseMult:0.###} dropoff={hit.AccuracyDropoff:0.###} alwaysHits={hit.AlwaysHits} shooterType={shooterType} targetType={targetType}";
            if (float.IsNaN(raw) || float.IsInfinity(raw))
            {
                detail = "invalid FinalValue; " + detail;
                return false;
            }

            nativeChance = Math.Clamp((int)Math.Round(raw), 0, 100);
            effectiveChance = Math.Clamp(nativeChance - Math.Max(0, aimPenalty), 0, 100);
            detail += $" overwatchPenalty={Math.Max(0, aimPenalty)} effective={effectiveChance}";
            return true;
        }
        catch (Exception e)
        {
            detail = $"{e.GetType().Name}: {e.Message}";
            return false;
        }
    }

    public static string Title(Skill? skill) => Valid(skill) ? skill!.GetTitle() : "-";
}
