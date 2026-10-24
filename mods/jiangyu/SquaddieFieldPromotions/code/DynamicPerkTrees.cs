using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;

namespace SquaddieFieldPromotions;

internal static class DynamicPerkTrees
{
    internal static void Apply(PromotionRecord record, UnitLeaderTemplate leader,
        List<UnityEngine.Object> pinned)
    {
        PerkGeneration.Validate(record);
        if (record.PerkSchemaVersion == 0) return;
        var initial = Lookup(record.InitialPerkId);
        // Build the full replacement tree *before* changing the cloned leader.
        var entries = new Perk[record.GeneratedPerks.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            var source = record.GeneratedPerks[i];
            entries[i] = new Perk { Skill = Lookup(source.Id), Tier = source.Tier };
        }
        var tree = UnityEngine.ScriptableObject.CreateInstance<PerkTreeTemplate>();
        tree.name = "SFP.Perks." + record.LeaderId;
        tree.Perks = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Perk>(entries);
        pinned.Add(tree);
        leader.InitialPerk = initial;
        leader.PerkTrees = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PerkTreeTemplate>(
            new[] { tree });
        Verify(record, leader);
    }

    internal static void VerifyOrApply(PromotionRecord record, UnitLeaderTemplate leader,
        List<UnityEngine.Object> pinned)
    {
        if (record.PerkSchemaVersion == 0) return;
        // Rehydrate never regenerates; existing native objects in this process may
        // still be linked from the saved roster. Do not mutate a purchased career.
        Verify(record, leader);
    }

    private static PerkTemplate Lookup(string id) =>
        DataTemplateLoader.Get<PerkTemplate>(id, false)
        ?? throw new InvalidOperationException("Missing native perk template: " + id);

    private static void Verify(PromotionRecord record, UnitLeaderTemplate leader)
    {
        if (leader.InitialPerk?.GetID() != record.InitialPerkId ||
            leader.PerkTrees == null || leader.PerkTrees.Length != 1 ||
            leader.PerkTrees[0] == null ||
            leader.PerkTrees[0].Perks == null ||
            leader.PerkTrees[0].Perks.Length != record.GeneratedPerks.Count)
            throw new InvalidOperationException("Dynamic perk tree installation failed.");
        var native = leader.PerkTrees[0].Perks;
        for (int i = 0; i < native.Length; i++)
        {
            if (native[i]?.Skill?.GetID() != record.GeneratedPerks[i].Id ||
                native[i].Tier != record.GeneratedPerks[i].Tier)
                throw new InvalidOperationException("Generated perk readback mismatch.");
        }
    }
}
