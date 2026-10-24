namespace SquaddieProbe;

/// <summary>
/// Stable compatibility surface for optional integrations with other mods.
/// A negative XP value or an empty rank means the Squaddie Veterancy runtime is not available.
/// </summary>
public static class CompatibilityApi
{
    public const int ApiVersion = 1;
    public const int VehicleCrewApiVersion = 2;

    public static int GetSquaddieXp(int squaddieId)
        => ProbeRuntime.GetSquaddieXpForCompatibility(squaddieId);

    public static string GetSquaddieRank(int squaddieId)
    {
        int xp = GetSquaddieXp(squaddieId);
        return xp < 0
            ? string.Empty
            : VeterancyRules.DisplayName(VeterancyRules.RankFromXp(xp));
    }

    /// <summary>Returns copied crew-person records for living, hired vehicle commanders.</summary>
    public static VehicleCrewMemberSnapshot[] GetVehicleCrewMembers()
        => ProbeRuntime.GetVehicleCrewForCompatibility();

    /// <summary>Consumes one healthy crew person, leaving a zero-XP replacement in the pool.</summary>
    public static VehicleCrewMutationResult PromoteVehicleCrewMember(string pilotTemplateId,
        string sourcePersonId, string replacementPersonId, int expectedXp)
        => ProbeRuntime.PromoteVehicleCrewForCompatibility(pilotTemplateId, sourcePersonId,
            replacementPersonId, expectedXp);

    /// <summary>Restores the source identity only if its exact promotion replacement is still present.</summary>
    public static VehicleCrewMutationResult RestorePromotedVehicleCrewMember(string pilotTemplateId,
        string sourcePersonId, string replacementPersonId, int sourceXp)
        => ProbeRuntime.RestoreVehicleCrewForCompatibility(pilotTemplateId, sourcePersonId,
            replacementPersonId, sourceXp);

    /// <summary>Idempotently completes a saved promotion whose promoted leader is already in the roster.</summary>
    public static VehicleCrewMutationResult ReconcilePromotedVehicleCrewMember(string pilotTemplateId,
        string sourcePersonId, string replacementPersonId, int sourceXp)
        => ProbeRuntime.ReconcileVehicleCrewForCompatibility(pilotTemplateId, sourcePersonId,
            replacementPersonId, sourceXp);
}

/// <summary>Value-only snapshot; slot/role assignments are not person identity.</summary>
public sealed class VehicleCrewMemberSnapshot
{
    public string PilotTemplateId { get; set; } = "";
    public string PilotName { get; set; } = "";
    public string VehicleGuid { get; set; } = "";
    public string VehicleName { get; set; } = "";
    public string PersonId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string Rank { get; set; } = "";
    public int Xp { get; set; }
    public bool IsActive { get; set; }
    public bool IsPilotDeployed { get; set; }
}

/// <summary>Result of an idempotent crew promotion or exact rollback request.</summary>
public sealed class VehicleCrewMutationResult
{
    public bool Success { get; set; }
    public bool Changed { get; set; }
    public string Error { get; set; } = "";

    internal static VehicleCrewMutationResult Succeeded(bool changed) => new()
    {
        Success = true,
        Changed = changed
    };

    internal static VehicleCrewMutationResult Failed(string error) => new()
    {
        Success = false,
        Error = error ?? "Vehicle crew mutation failed."
    };
}
