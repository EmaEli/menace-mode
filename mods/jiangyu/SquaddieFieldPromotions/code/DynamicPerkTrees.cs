using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using UnityEngine;

namespace SquaddieFieldPromotions;

/// <summary>
/// Binds a promoted leader's saved perk snapshot to a fresh per-leader native tree.
/// Never edits shared vanilla or generic SFP trees and never regenerates from current XP.
/// </summary>
internal static class DynamicPerkTrees
{
    internal static void Apply(PromotionRecord record, UnitLeaderTemplate leader, List<UnityEngine.Object> pinned)
    {
        ValidateSnapshot(record);
        if (leader == null) throw new ArgumentNullException(nameof(leader));
        if (record.PerkSchemaVersion == 0)
            throw new InvalidOperationException("Legacy promotion records use the shared generic tree.");
        var initial = Lookup(record.InitialPerkId);
        // Resolve every template before the cloned leader is changed.
        var skills = new PerkTemplate[record.GeneratedPerks.Count];
        for (int i = 0; i < skills.Length; i++) skills[i] = Lookup(record.GeneratedPerks[i].Id);

        var perks = new Il2CppReferenceArray<Perk>(skills.Length);
        for (int i = 0; i < skills.Length; i++)
            perks[i] = new Perk { Skill = skills[i], Tier = record.GeneratedPerks[i].Tier };
        var tree = ScriptableObject.CreateInstance<PerkTreeTemplate>();
        tree.name = "SFP.Perks." + record.LeaderId;
        tree.hideFlags = HideFlags.DontUnloadUnusedAsset;
        tree.Perks = perks;
        pinned.Add(tree);

        var trees = new Il2CppReferenceArray<PerkTreeTemplate>(1);
        trees[0] = tree;
        var previousInitial = leader.InitialPerk;
        var previousTrees = leader.PerkTrees;
        try
        {
            leader.InitialPerk = initial;
            leader.PerkTrees = trees;
            string mismatch = ReadbackMismatch(record, leader);
            if (mismatch != null)
                throw new InvalidOperationException("Dynamic perk tree readback mismatch: " + mismatch);
        }
        catch
        {
            // VerifyOrApply also handles existing leaders: leave their old tree intact on failure.
            leader.InitialPerk = previousInitial;
            leader.PerkTrees = previousTrees;
            throw;
        }
    }

    /// <summary>Keeps an already bound identical tree; otherwise binds the saved snapshot.</summary>
    internal static void VerifyOrApply(PromotionRecord record, UnitLeaderTemplate leader, List<UnityEngine.Object> pinned)
    {
        ValidateSnapshot(record);
        if (Matches(record, leader)) return;
        Apply(record, leader, pinned);
    }

    private static void ValidateSnapshot(PromotionRecord record)
    {
        if (record.IsVehicleCrewPromotion) VehiclePerkGeneration.Validate(record);
        else PerkGeneration.Validate(record);
    }

    internal static bool Matches(PromotionRecord record, UnitLeaderTemplate leader) =>
        ReadbackMismatch(record, leader) == null;

    internal static string ReadbackMismatch(PromotionRecord record, UnitLeaderTemplate leader)
    {
        if (leader == null) return "leader is null";
        string actualInitial = leader.InitialPerk?.GetID();
        if (!string.Equals(actualInitial, record.InitialPerkId, StringComparison.Ordinal))
            return $"initial perk expected '{record.InitialPerkId}', actual '{actualInitial ?? "<null>"}'";
        var trees = leader.PerkTrees;
        if (trees == null || trees.Length != 1)
            return $"tree count expected 1, actual {trees?.Length.ToString() ?? "<null>"}";
        if (trees[0] == null) return "tree[0] is null";
        var native = trees[0].Perks;
        if (native == null || native.Length != record.GeneratedPerks.Count)
            return $"option count expected {record.GeneratedPerks.Count}, actual {native?.Length.ToString() ?? "<null>"}";
        for (int i = 0; i < native.Length; i++)
        {
            var option = native[i];
            var expected = record.GeneratedPerks[i];
            string actualId = option?.Skill?.GetID();
            if (!string.Equals(actualId, expected.Id, StringComparison.Ordinal))
                return $"option[{i}] skill expected '{expected.Id}', actual '{actualId ?? "<null>"}'";
            if (option.Tier != expected.Tier)
                return $"option[{i}] '{expected.Id}' tier expected {expected.Tier}, actual {option.Tier}";
        }
        return null;
    }

    // A map entry is not enough: reject aliases/corrupted IDs before creating a new snapshot.
    internal static bool IsAvailable(string id)
    {
        var perk = DataTemplateLoader.Get<PerkTemplate>(id, false);
        return perk != null && string.Equals(perk.GetID(), id, StringComparison.Ordinal);
    }

    private static PerkTemplate Lookup(string id)
    {
        var perk = DataTemplateLoader.Get<PerkTemplate>(id, false)
            ?? throw new InvalidOperationException("Missing native perk template: " + id);
        string actual = perk.GetID();
        if (!string.Equals(actual, id, StringComparison.Ordinal))
            throw new InvalidOperationException($"Native perk lookup ID mismatch: requested '{id}', actual '{actual ?? "<null>"}'.");
        return perk;
    }
}
