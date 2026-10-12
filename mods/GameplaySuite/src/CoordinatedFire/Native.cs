using CoordinatedFire.Core;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Items;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;

namespace CoordinatedFire;

/// <summary>Compatibility checked against the current MENACE dump; player ownership follows Entity.IsPlayer()/IsPlayerOrPlayerAI().</summary>
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
    public static long Key(Il2CppObjectBase? value) => value?.Pointer.ToInt64() ?? 0;
    public static long WeaponTemplateKey(Skill skill)
        => Valid(skill) && Valid(skill.GetItem()) ? Key(skill.GetItem().GetTemplate()) : 0;
    public static bool Same(Il2CppObjectBase? a, Il2CppObjectBase? b) => Key(a) != 0 && Key(a) == Key(b);
    public static bool Valid(Il2CppObjectBase? value) => Key(value) != 0;

    public static ActorSnapshot ReadActor(Actor actor) => new(
        Key(actor), IsPlayerControlled(actor), actor.IsInfantry(), actor.IsVehicle(),
        actor.IsAlive() && !actor.IsDying() && !actor.IsLeavingMap() && !actor.IsTurnDone(),
        actor.IsStunned(), actor.GetMoraleState() == MoraleState.Fleeing,
        actor.IsMoving(), actor.GetSkills().IsBusy() || TacticalManager.IsSkillBusy(),
        actor.GetActionPoints());

    public static WeaponRole Role(Skill skill)
    {
        var item = skill.GetItem();
        if (!Valid(item)) return WeaponRole.Unknown;
        return item.GetTemplate().SlotType switch
        {
            ItemSlot.InfantryWeapon => WeaponRole.Squad,
            ItemSlot.InfantrySpecial => WeaponRole.Leader,
            _ => WeaponRole.Unknown
        };
    }

    public static bool Supported(Skill skill)
    {
        if (!Valid(skill) || skill.IsGarbage() || !Valid(skill.GetItem())) return false;
        var template = skill.GetTemplate();
        // GetAllAttacks() is the game's authoritative attack list. Native target
        // checks below remain authoritative for line-of-fire/range/faction rules.
        return Valid(template) && skill.IsAttack() && skill.IsActive() && skill.IsTargeted()
            && !skill.IsMovementSkill() && !skill.IsBackgroundOnly();
    }

    public static bool RequirementState(Skill skill, Actor actor,
        out bool requiresDeployment, out bool deploymentSatisfied,
        out bool requiresSetup, out bool setupSatisfied)
    {
        requiresDeployment = deploymentSatisfied = requiresSetup = setupSatisfied = false;
        if (!Valid(skill) || !Valid(actor)) return false;
        var template = skill.GetTemplate();
        if (!Valid(template)) return false;
        requiresDeployment = template.IsDeploymentRequired;
        requiresSetup = template.IsWeaponSetupRequired;
        deploymentSatisfied = !requiresDeployment || template.IsDeploymentRequirementSatisfied(actor);
        setupSatisfied = !requiresSetup || template.IsSetupRequirementSatisfied(actor);
        return true;
    }

    public static bool RequirementsSatisfied(Skill skill, Actor actor)
    {
        return RequirementState(skill, actor, out _, out var deploymentSatisfied,
            out _, out var setupSatisfied) && deploymentSatisfied && setupSatisfied;
    }

    public static bool Ready(Skill skill, Actor actor) => Supported(skill) && RequirementsSatisfied(skill, actor)
        && skill.IsEnabled() && !skill.IsHidden() && skill.HasExecutingElements()
        && skill.IsUsable(TargetUsageParams.Player) && skill.IsAffordable()
        && (!skill.HasLimitedUses() || (!skill.IsOutOfUses() && skill.GetUses() > 0));

    public static bool CanTarget(Skill skill, Actor actor, Tile target)
    {
        if (!Valid(target) || !Valid(actor.GetTile())) return false;
        // Target FIRST, origin SECOND. Player includes deployment, uses and faction checks.
        return Ready(skill, actor) && skill.IsUsableOn(target, actor.GetTile(), TargetUsageParams.Player)
            && skill.IsApplicableTo(target, null);
    }

    public static AttackSnapshot ReadAttack(Actor actor, Skill skill)
    {
        RequirementState(skill, actor, out var requiresDeployment, out var deploymentSatisfied,
            out var requiresSetup, out var setupSatisfied);
        return new(
            Key(skill), WeaponTemplateKey(skill), Key(actor), skill.GetID(), Role(skill),
            skill.GetActionPointCost(), skill.GetMinRange(), skill.GetMaxRange(),
            Supported(skill) && skill.IsEnabled() && !skill.IsHidden() && skill.HasExecutingElements()
                && skill.IsUsable(TargetUsageParams.Player),
            skill.IsAffordable() && !skill.IsOutOfUses(),
            skill.HasLimitedUses(), skill.GetUses(),
            true,
            skill.IsAttack(),
            requiresDeployment, deploymentSatisfied, requiresSetup, setupSatisfied);
    }

    public static List<Skill> Attacks(Actor actor, WeaponRole role)
    {
        var answer = new List<Skill>();
        var attacks = actor.GetAllAttacks();
        if (attacks == null) return answer;
        for (int i = 0; i < attacks.Count; i++)
        {
            var s = attacks[i];
            if (Supported(s) && Role(s) == role
                && !s.IsHidden() && !answer.Any(x => Same(x, s))) answer.Add(s);
        }
        return answer;
    }


    public static List<Skill> VehicleAttacks(Actor actor)
    {
        var answer = new List<Skill>();
        var attacks = actor.GetAllAttacks();
        if (attacks == null) return answer;
        for (int i = 0; i < attacks.Count; i++)
        {
            var s = attacks[i];
            if (Supported(s) && !s.IsHidden() && Valid(s.GetItem())
                && !answer.Any(x => Same(x, s))) answer.Add(s);
        }
        return answer;
    }

    public static List<(Skill First, Skill Second)> VehiclePairs(Actor actor)
    {
        var answer = new List<(Skill First, Skill Second)>();
        var attacks = VehicleAttacks(actor);
        for (int i = 0; i < attacks.Count; i++)
        {
            long firstItem = WeaponTemplateKey(attacks[i]);
            if (firstItem == 0) continue;
            for (int j = i + 1; j < attacks.Count; j++)
            {
                long secondItem = WeaponTemplateKey(attacks[j]);
                if (secondItem == 0 || secondItem == firstItem) continue;
                answer.Add((attacks[i], attacks[j]));
            }
        }
        return answer;
    }

    public static bool HasAttack(Actor actor, Skill skill)
    {
        if (!Valid(actor) || !Valid(skill)) return false;
        var attacks = actor.GetAllAttacks();
        if (attacks == null) return false;
        for (int i = 0; i < attacks.Count; i++)
            if (Same(attacks[i], skill)) return true;
        return false;
    }

    public static Actor? ResolveActorForSkill(Skill skill)
    {
        if (!Valid(skill)) return null;
        var actor = skill.GetActor();
        if (Valid(actor) && HasAttack(actor, skill)) return actor;
        actor = skill.GetAPConsumer();
        if (Valid(actor) && HasAttack(actor, skill)) return actor;
        if (TacticalManager.IsMissionRunning())
        {
            actor = TacticalManager.Get().GetActiveActor();
            if (Valid(actor) && HasAttack(actor, skill)) return actor;
        }
        return null;
    }

    public static string Title(Skill? skill, Actor? actor = null)
    {
        if (!Valid(skill)) return "-";
        string title = skill!.GetTitle();
        if (!Valid(actor)) return title;
        if (!RequirementState(skill, actor!, out var requiresDeployment, out var deploymentSatisfied,
            out var requiresSetup, out var setupSatisfied)) return title;
        if (requiresDeployment)
            title += deploymentSatisfied ? " [DEPLOYED]" : " [NEEDS DEPLOY]";
        if (requiresSetup)
            title += setupSatisfied ? " [SET UP]" : " [NEEDS SETUP]";
        return title;
    }
}
