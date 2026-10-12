namespace SquaddieProbe;

internal enum VeterancyRank
{
    Green = 0,
    CombatTested = 1,
    Hardened = 2,
    Regular = 3,
    Veteran = 4,
    Crack = 5,
    Elite = 6
}

internal static class VeterancyRules
{
    public const int CombatTestedXp = 250;
    public const int HardenedXp = 500;
    public const int RegularXp = 1000;
    public const int VeteranXp = 2000;
    public const int CrackXp = 4000;
    public const int EliteXp = 8000;

    public static VeterancyRank RankFromXp(double xp)
    {
        if (xp >= EliteXp) return VeterancyRank.Elite;
        if (xp >= CrackXp) return VeterancyRank.Crack;
        if (xp >= VeteranXp) return VeterancyRank.Veteran;
        if (xp >= RegularXp) return VeterancyRank.Regular;
        if (xp >= HardenedXp) return VeterancyRank.Hardened;
        if (xp >= CombatTestedXp) return VeterancyRank.CombatTested;
        return VeterancyRank.Green;
    }

    public static string DisplayName(VeterancyRank rank) => rank switch
    {
        VeterancyRank.CombatTested => "Combat Tested",
        VeterancyRank.Hardened => "Hardened",
        VeterancyRank.Regular => "Regular",
        VeterancyRank.Veteran => "Veteran",
        VeterancyRank.Crack => "Crack",
        VeterancyRank.Elite => "Elite",
        _ => "Green"
    };

    public static int NextThreshold(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => CombatTestedXp,
        VeterancyRank.CombatTested => HardenedXp,
        VeterancyRank.Hardened => RegularXp,
        VeterancyRank.Regular => VeteranXp,
        VeterancyRank.Veteran => CrackXp,
        VeterancyRank.Crack => EliteXp,
        _ => EliteXp
    };

    public static string NextRankName(VeterancyRank rank) => rank switch
    {
        VeterancyRank.Green => DisplayName(VeterancyRank.CombatTested),
        VeterancyRank.CombatTested => DisplayName(VeterancyRank.Hardened),
        VeterancyRank.Hardened => DisplayName(VeterancyRank.Regular),
        VeterancyRank.Regular => DisplayName(VeterancyRank.Veteran),
        VeterancyRank.Veteran => DisplayName(VeterancyRank.Crack),
        VeterancyRank.Crack => DisplayName(VeterancyRank.Elite),
        _ => "Max rank"
    };

    public static double Progress01(double xp, VeterancyRank rank)
    {
        if (rank == VeterancyRank.Elite) return 1d;
        double floor = rank switch
        {
            VeterancyRank.CombatTested => CombatTestedXp,
            VeterancyRank.Hardened => HardenedXp,
            VeterancyRank.Regular => RegularXp,
            VeterancyRank.Veteran => VeteranXp,
            VeterancyRank.Crack => CrackXp,
            _ => 0d
        };
        double ceiling = NextThreshold(rank);
        if (ceiling <= floor) return 1d;
        return Math.Max(0d, Math.Min(1d, (xp - floor) / (ceiling - floor)));
    }
}

internal sealed class VeterancyRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "?";
    public string Nickname { get; init; } = "";
    public bool Alive { get; init; }
    public int Xp { get; init; }
    public VeterancyRank Rank { get; init; }
    public int SquadNumber { get; init; }
    public double SquadAverageXp { get; init; }
    public VeterancyRank SquadRank { get; init; }
    public int SquadHpBonus { get; init; }
    public int SquadAccuracyPct { get; init; }
    public int SquadDefensePct { get; init; }
    public int SquadSuppressionResistancePct { get; init; }
    public int SquadConcealment { get; init; }
}


internal sealed class VeterancySquadView
{
    public int SquadNumber { get; init; }
    public string LeaderName { get; init; } = "Unknown";
    public double AverageXp { get; init; }
    public VeterancyRank Rank { get; init; }
    public bool Deployed { get; init; }
    public IReadOnlyList<VeterancyRow> Members { get; init; } = Array.Empty<VeterancyRow>();
}

internal sealed class CrewRoleSpec
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "Crewman";
}

internal sealed class PilotIdentitySnapshot
{
    public string TemplateId { get; init; } = "";
    public int TemplateGuid { get; init; }
    public string Name { get; init; } = "Unknown pilot";
    public string VehicleGuid { get; init; } = "";
    public string VehicleName { get; init; } = "Unassigned";
    public bool Alive { get; init; }
    public long LeaderPtr { get; init; }
    // The MENACE Pilot/leader is deliberately not part of veterancy XP. These
    // roles describe only the abstract crew working under that native leader.
    public IReadOnlyList<CrewRoleSpec> ActiveCrewRoles { get; init; } = Array.Empty<CrewRoleSpec>();
    // Legacy diagnostic/migration snapshot of the old physical mount-derived
    // layout. 1.5.7 no longer treats roles as personnel identities: XP/status live
    // on the persistent crew pool and the current loadout only assigns jobs.
    public IReadOnlyList<CrewRoleSpec> LegacyMountCrewRoles { get; init; } = Array.Empty<CrewRoleSpec>();
    public int LightWeaponsMounted { get; init; }
    public int MediumWeaponsMounted { get; init; }
    public int HeavyWeaponsMounted { get; init; }
}

internal sealed class CrewMemberVeterancyView
{
    public int Slot { get; init; }
    public string Key { get; init; } = "";
    public string Role { get; init; } = "Crewman";
    public int Xp { get; init; }
    public string Status { get; init; } = "Healthy";
}

internal sealed class PilotVeterancyView
{
    public string TemplateId { get; init; } = "";
    public string PilotName { get; init; } = "Unknown pilot";
    public string VehicleName { get; init; } = "Unassigned";
    public string VehicleGuid { get; init; } = "";
    // Xp is the average of currently active abstract crew members. The native
    // MENACE Pilot/leader is never included in this XP or rank calculation.
    public int Xp { get; init; }
    public VeterancyRank Rank { get; init; }
    public IReadOnlyList<CrewMemberVeterancyView> Crew { get; init; } = Array.Empty<CrewMemberVeterancyView>();
    public int ReserveCrewCount { get; init; }
    public bool Alive { get; init; }
    public bool Deployed { get; init; }
}

