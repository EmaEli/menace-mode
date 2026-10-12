namespace SquaddieProbe;

internal readonly struct NpcVeterancyProfile
{
    public readonly string Id;
    public readonly int GreenPct;
    public readonly int CombatTestedPct;
    public readonly int HardenedPct;
    public readonly int RegularPct;
    public readonly int VeteranPct;
    public readonly int CrackPct;
    public readonly int ElitePct;

    public NpcVeterancyProfile(
        string id,
        int greenPct,
        int combatTestedPct,
        int hardenedPct,
        int regularPct,
        int veteranPct,
        int crackPct,
        int elitePct)
    {
        Id = id;
        GreenPct = greenPct;
        CombatTestedPct = combatTestedPct;
        HardenedPct = hardenedPct;
        RegularPct = regularPct;
        VeteranPct = veteranPct;
        CrackPct = crackPct;
        ElitePct = elitePct;
    }

    public string Label => $"{Id}:{GreenPct}/{CombatTestedPct}/{HardenedPct}/{RegularPct}/{VeteranPct}/{CrackPct}/{ElitePct}";

    public VeterancyRank RankForRoll(int roll)
    {
        int greenEnd = GreenPct;
        int combatEnd = greenEnd + CombatTestedPct;
        int hardenedEnd = combatEnd + HardenedPct;
        int regularEnd = hardenedEnd + RegularPct;
        int veteranEnd = regularEnd + VeteranPct;
        int crackEnd = veteranEnd + CrackPct;

        if (roll <= greenEnd) return VeterancyRank.Green;
        if (roll <= combatEnd) return VeterancyRank.CombatTested;
        if (roll <= hardenedEnd) return VeterancyRank.Hardened;
        if (roll <= regularEnd) return VeterancyRank.Regular;
        if (roll <= veteranEnd) return VeterancyRank.Veteran;
        if (roll <= crackEnd) return VeterancyRank.Crack;
        return VeterancyRank.Elite;
    }
}

internal static class NpcVeterancyProfiles
{
    private static readonly HashSet<string> AlliedCombatTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "allied.local_forces_basic_infantry",
        "allied.local_forces_medium_machinegun_weapon_team",
        "allied.local_forces_mortar_weapon_team",
        "allied.pirate_scavengers",
        "allied.player_local_forces_conscripts",
        "allied.player_local_forces_regular_infantry",
        "allied_local_forces.hunters",
        "allied_local_forces.militia"
    };

    public static bool IsAlliedCombatTemplate(string? templateId)
        => !string.IsNullOrWhiteSpace(templateId) && AlliedCombatTemplates.Contains(templateId.Trim());

    // Constructs and Wildlife stay outside the veterancy model. They are hostile for
    // combat/XP purposes, but are not human crews and therefore never receive ranks.
    public static bool IsHostileVeterancyFaction(int factionId)
    {
        return factionId == (int)Menace.SDK.FactionType.EnemyLocalForces
            || factionId == (int)Menace.SDK.FactionType.Pirates
            || factionId == (int)Menace.SDK.FactionType.RogueArmy;
    }

    public static bool IsHostileVehicleTemplate(int factionId, string? templateId)
    {
        if (!IsHostileVeterancyFaction(factionId) || string.IsNullOrWhiteSpace(templateId)) return false;
        string id = templateId.Trim();

        if (id.StartsWith("enemy.vehicle_rogue_army_", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.StartsWith("enemy.pirate_vehicle.", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.Equals("enemy.rogue_army_heavy_infantry_in_light_carrier", StringComparison.OrdinalIgnoreCase)) return true;
        if (id.StartsWith("enemy.pirate_", StringComparison.OrdinalIgnoreCase)
            && (id.Contains("_on_light_truck", StringComparison.OrdinalIgnoreCase)
                || id.Contains("_on_heavy_truck", StringComparison.OrdinalIgnoreCase))) return true;

        return false;
    }

    public static NpcVeterancyProfile Resolve(int factionId, string? templateId, bool detailed)
    {
        string id = templateId?.Trim() ?? "";
        if (!detailed)
        {
            if (IsAlliedCombatTemplate(id)) return P("legacy.allied_npc", 30, 30, 20, 15, 5, 0, 0);
            return LegacyFactionProfile(factionId);
        }

        // Pirates: most bodies are irregular. Better-equipped specialists climb into
        // Regular/Veteran territory, while command/boarding formations are the only
        // pirate infantry with meaningful Crack/Elite representation.
        switch (id)
        {
            case "enemy.pirate_scavengers_tutorial_weak":
                return P("pirate.tutorial_weak", 95, 5, 0, 0, 0, 0, 0);
            case "enemy.pirate_outcasts":
            case "enemy.pirate_outcasts_with_shotguns":
                return P("pirate.outcasts", 75, 20, 5, 0, 0, 0, 0);
            case "enemy.pirate_scavengers":
                return P("pirate.scavengers", 55, 30, 12, 3, 0, 0, 0);
            case "enemy.pirate_chaingun_team":
            case "enemy.pirate_emp_team":
            case "enemy.pirate_flamethrower_team":
                return P("pirate.weapon_team", 35, 35, 20, 8, 2, 0, 0);
            case "enemy.pirate_emp_team_engineer":
            case "enemy.pirate_bombmaker":
                return P("pirate.specialist", 20, 30, 25, 15, 8, 2, 0);
            case "enemy.pirate_saboteur_chaingun_team":
            case "enemy.pirate_saboteurs_scavenger_ringleader":
            case "enemy.pirate_saboteurs_scavengers":
                return P("pirate.saboteur", 5, 15, 25, 25, 20, 8, 2);
            case "enemy.pirate_veteran_scavengers_t2":
                return P("pirate.veteran_scavenger", 2, 8, 20, 30, 25, 12, 3);
            case "enemy.pirate_boarding_commandos":
            case "enemy.pirate_boarding_commandos_bodyguard":
            case "enemy.pirate_boarding_commandos_first_mate":
                return P("pirate.boarding_elite", 0, 3, 7, 20, 35, 25, 10);
            case "enemy.pirate_captain":
                return P("pirate.command", 0, 0, 5, 15, 35, 30, 15);
            case "enemy.pirate_bullet_sponge":
                return P("pirate.dev_heavy", 10, 20, 25, 25, 15, 5, 0);
        }

        // Rogue Army: trained line formations start around Hardened/Regular; advanced
        // teams, officers and storm troops progressively shift toward Veteran/Crack.
        switch (id)
        {
            case "enemy.rogue_army_conscripts":
                return P("rogue.conscripts", 40, 35, 20, 5, 0, 0, 0);
            case "enemy.rogue_army_infantry":
            case "enemy.rogue_army_infantry_with_battle_rifles":
                return P("rogue.line_infantry", 10, 25, 30, 25, 8, 2, 0);
            case "enemy.rogue_army_medium_machinegun_weapon_team":
            case "enemy.rogue_army_mortar_weapon_team":
                return P("rogue.weapon_team", 5, 20, 30, 30, 12, 3, 0);
            case "enemy.rogue_army_autocannon_weapon_team":
            case "enemy.rogue_army_atgm_weapon_team":
                return P("rogue.advanced_weapon_team", 2, 10, 25, 35, 20, 7, 1);
            case "enemy.rogue_army_heavy_infantry":
                return P("rogue.heavy_infantry", 2, 8, 20, 35, 25, 8, 2);
            case "enemy.rogue_army_officer_squad":
                return P("rogue.officer", 0, 2, 8, 20, 35, 25, 10);
            case "enemy.rogue_army_sniper_team_jaeger":
                return P("rogue.jaeger", 0, 2, 8, 20, 30, 30, 10);
            case "enemy.rogue_army_storm_troopers":
                return P("rogue.storm_troopers", 0, 0, 5, 15, 30, 35, 15);
            case "enemy.rogue_army_ammo_runner":
                return P("rogue.support", 15, 30, 30, 20, 5, 0, 0);
        }

        // Temporary allied combatants use the same ladder, but elite outcomes stay
        // rare because these forces are mission-local auxiliaries rather than the
        // player's persistent veteran roster.
        switch (id)
        {
            case "allied_local_forces.militia":
                return P("ally.civilian_militia", 60, 25, 10, 5, 0, 0, 0);
            case "allied_local_forces.hunters":
                return P("ally.civilian_hunters", 30, 35, 20, 10, 5, 0, 0);
            case "allied.player_local_forces_conscripts":
                return P("ally.local_conscripts", 50, 30, 15, 5, 0, 0, 0);
            case "allied.local_forces_basic_infantry":
            case "allied.player_local_forces_regular_infantry":
                return P("ally.local_infantry", 15, 30, 30, 20, 5, 0, 0);
            case "allied.local_forces_medium_machinegun_weapon_team":
            case "allied.local_forces_mortar_weapon_team":
                return P("ally.local_weapon_team", 10, 25, 30, 25, 8, 2, 0);
            case "allied.pirate_scavengers":
                return P("ally.pirate_scavengers", 55, 30, 12, 3, 0, 0, 0);
        }

        if (id.StartsWith("enemy.pirate_", StringComparison.OrdinalIgnoreCase))
            return P("pirate.fallback", 55, 30, 12, 3, 0, 0, 0);
        if (id.StartsWith("enemy.rogue_army_", StringComparison.OrdinalIgnoreCase))
            return P("rogue.fallback", 10, 20, 30, 25, 10, 4, 1);
        if (id.StartsWith("allied.pirate_", StringComparison.OrdinalIgnoreCase))
            return P("ally.pirate_fallback", 55, 30, 12, 3, 0, 0, 0);
        if (id.StartsWith("allied.local_forces_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("allied.player_local_forces_", StringComparison.OrdinalIgnoreCase))
            return P("ally.local_forces_fallback", 20, 30, 30, 15, 5, 0, 0);

        return FactionProfile(factionId);
    }

    public static NpcVeterancyProfile ResolveVehicle(int factionId, string? templateId, bool detailed)
    {
        string id = templateId?.Trim() ?? "";
        if (!detailed) return VehicleFactionProfile(factionId);

        // Pirate motorized squads: crew quality broadly follows the formation riding
        // in the truck. Dedicated gun trucks improve with specialization, but Crack
        // and Elite crews remain uncommon outside veteran/boarding formations.
        switch (id)
        {
            case "enemy.pirate_outcasts_on_heavy_truck":
            case "enemy.pirate_outcasts_shotgun_on_light_truck":
                return P("pirate.vehicle.outcasts", 45, 30, 20, 5, 0, 0, 0);
            case "enemy.pirate_scavengers_on_heavy_truck":
                return P("pirate.vehicle.scavengers", 35, 35, 20, 8, 2, 0, 0);
            case "enemy.pirate_chaingun_team_on_light_truck":
            case "enemy.pirate_emp_team_on_light_truck":
            case "enemy.pirate_flamethrower_team_on_light_truck":
                return P("pirate.vehicle.weapon_team", 25, 35, 22, 13, 5, 0, 0);
            case "enemy.pirate_boarding_commando_on_light_truck":
                return P("pirate.vehicle.boarding", 0, 5, 10, 25, 35, 20, 5);
            case "enemy.pirate_veteran_scavengers_tier_2_on_heavy_truck":
                return P("pirate.vehicle.veteran_scavengers", 2, 8, 20, 30, 25, 12, 3);
            case "enemy.pirate_vehicle.chaingun_guntruck":
                return P("pirate.vehicle.chaingun", 20, 35, 25, 15, 5, 0, 0);
            case "enemy.pirate_vehicle.laser_guntruck":
            case "enemy.pirate_vehicle.rocket_guntruck":
                return P("pirate.vehicle.specialist_guntruck", 10, 25, 30, 25, 8, 2, 0);
            case "enemy.pirate_vehicle.flamethrower_heavy_truck":
                return P("pirate.vehicle.flamethrower_heavy", 10, 25, 30, 25, 8, 2, 0);
            case "enemy.pirate_vehicle.heavy_mg_heavy_truck":
                return P("pirate.vehicle.hmg_heavy", 5, 20, 30, 30, 12, 3, 0);
        }

        // Rogue Army vehicles represent trained crews. Walkers start around Hardened,
        // while tanks and mercenary walkers have a meaningful Veteran/Crack tail.
        switch (id)
        {
            case "enemy.vehicle_rogue_army_auto_laser_walker":
                return P("rogue.vehicle.laser_walker", 5, 20, 35, 30, 8, 2, 0);
            case "enemy.vehicle_rogue_army_auto_autocannon_walker":
                return P("rogue.vehicle.autocannon_walker", 2, 15, 30, 35, 15, 3, 0);
            case "enemy.rogue_army_heavy_infantry_in_light_carrier":
                return P("rogue.vehicle.light_carrier", 2, 12, 28, 35, 18, 5, 0);
            case "enemy.vehicle_rogue_army_battle_tank":
                return P("rogue.vehicle.battle_tank", 0, 5, 20, 35, 25, 12, 3);
            case "enemy.vehicle_rogue_army_heavy_tank":
                return P("rogue.vehicle.heavy_tank", 0, 3, 15, 32, 30, 15, 5);
            case "enemy.vehicle_rogue_army_mercenary_medium_walker":
                return P("rogue.vehicle.mercenary_walker", 0, 2, 10, 28, 35, 20, 5);
        }

        return VehicleFactionProfile(factionId);
    }

    private static NpcVeterancyProfile FactionProfile(int factionId)
    {
        if (factionId == (int)Menace.SDK.FactionType.RogueArmy)
            return P("faction.rogue_army", 10, 20, 30, 25, 10, 4, 1);
        if (factionId == (int)Menace.SDK.FactionType.Pirates)
            return P("faction.pirates", 55, 30, 12, 3, 0, 0, 0);
        if (factionId == (int)Menace.SDK.FactionType.EnemyLocalForces)
            return P("faction.enemy_local_forces", 25, 30, 25, 15, 5, 0, 0);

        return P("faction.generic_human_hostile", 55, 30, 12, 3, 0, 0, 0);
    }

    private static NpcVeterancyProfile VehicleFactionProfile(int factionId)
    {
        if (factionId == (int)Menace.SDK.FactionType.RogueArmy)
            return P("vehicle_faction.rogue_army", 2, 13, 30, 35, 15, 5, 0);
        if (factionId == (int)Menace.SDK.FactionType.Pirates)
            return P("vehicle_faction.pirates", 20, 35, 25, 15, 5, 0, 0);
        if (factionId == (int)Menace.SDK.FactionType.EnemyLocalForces)
            return P("vehicle_faction.enemy_local_forces", 20, 30, 30, 15, 5, 0, 0);
        return P("vehicle_faction.generic_human_hostile", 20, 35, 25, 15, 5, 0, 0);
    }

    private static NpcVeterancyProfile LegacyFactionProfile(int factionId)
    {
        if (factionId == (int)Menace.SDK.FactionType.RogueArmy)
            return P("legacy.rogue_army", 10, 20, 30, 25, 10, 4, 1);
        return P("legacy.generic_human_hostile", 55, 30, 12, 3, 0, 0, 0);
    }

    private static NpcVeterancyProfile P(string id, int green, int combatTested, int hardened, int regular, int veteran, int crack, int elite)
        => new(id, green, combatTested, hardened, regular, veteran, crack, elite);
}
