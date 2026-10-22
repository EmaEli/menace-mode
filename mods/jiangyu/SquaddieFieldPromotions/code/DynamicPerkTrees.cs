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
        PerkGeneration.Validate(record);
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
        leader.InitialPerk = initial;
        leader.PerkTrees = trees;
        if (!Matches(record, leader))
            throw new InvalidOperationException("Dynamic perk tree readback mismatch.");
    }

    /// <summary>Keeps an already bound identical tree; otherwise binds the saved snapshot.</summary>
    internal static void VerifyOrApply(PromotionRecord record, UnitLeaderTemplate leader, List<UnityEngine.Object> pinned)
    {
        PerkGeneration.Validate(record);
        if (Matches(record, leader)) return;
        Apply(record, leader, pinned);
    }

    internal static bool Matches(PromotionRecord record, UnitLeaderTemplate leader)
    {
        if (leader?.InitialPerk == null || leader.InitialPerk.GetID() != record.InitialPerkId ||
            leader.PerkTrees == null || leader.PerkTrees.Length != 1 || leader.PerkTrees[0] == null)
            return false;
        var native = leader.PerkTrees[0].Perks;
        if (native == null || native.Length != record.GeneratedPerks.Count) return false;
        for (int i = 0; i < native.Length; i++)
            if (native[i]?.Skill == null || native[i].Skill.GetID() != record.GeneratedPerks[i].Id ||
                native[i].Tier != record.GeneratedPerks[i].Tier)
                return false;
        return true;
    }

    private static PerkTemplate Lookup(string id) =>
        DataTemplateLoader.Get<PerkTemplate>(id, false)
        ?? throw new InvalidOperationException("Missing native perk template: " + id);
}
