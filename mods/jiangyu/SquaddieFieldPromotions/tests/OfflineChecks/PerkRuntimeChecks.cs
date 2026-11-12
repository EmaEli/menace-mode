using System.Reflection;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using SquaddieFieldPromotions;

// Exercise production generation/materialization with managed native API simulation.
internal static class PerkRuntimeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var poolIds = new HashSet<string>();
        foreach (var type in new[] { typeof(PerkGeneration), typeof(VehiclePerkGeneration) })
            foreach (string name in new[] { "Base", "Advanced" })
                foreach (var row in (string[][])type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null))
                    foreach (string id in row) poolIds.Add("perk." + id);
        check(!poolIds.Contains("perk.payback") && !poolIds.Contains("perk.standby"),
            "new pools exclude obsolete IDs instead of substituting signature/unused perks");
        foreach (string id in poolIds.Concat(new[] { PromotionProgression.InitialPerkId,
                     PromotionProgression.VehicleInitialPerkId, VehiclePerkGeneration.LegacyInitialPerkId }))
            DataTemplateLoader.AdditionalTemplates[id] = new PerkTemplate { Id = id };

        PromotionRecord Record(int index, bool vehicle = false, int xp = 8000) => new()
        {
            LeaderId = PromotionRecord.LeaderPrefix + index.ToString("x32"),
            IsVehicleCrewPromotion = vehicle, SourceCrewXp = vehicle ? xp : 0,
        };
        for (int index = 0; index < 100; index++)
            foreach (int xp in new[] { 0, 250, 500, 1000, 2000, 4000, 8000 })
            {
                var infantry = Record(index);
                PerkGeneration.Generate(infantry, xp, new MentorSnapshot("mentor", new[] { "perk.hit_the_deck" }),
                    DynamicPerkTrees.IsAvailable);
                check(infantry.GeneratedPerks.All(p =>
                        (p.Id != "perk.hit_the_deck" && p.Id != "perk.sharpshooter") || p.Tier == 2),
                    "new infantry choices use observed native tiers, including mentors");
                var vehicle = Record(index, true, xp);
                VehiclePerkGeneration.Generate(vehicle, new MentorSnapshot("mentor", new[] { "perk.grind_down", "perk.to_shreds_vehicle" }),
                    id => id != "perk.lancer" && id != "perk.fervor" && id != "perk.to_shreds_vehicle" && DynamicPerkTrees.IsAvailable(id));
                check(vehicle.GeneratedPerks.Count == 15 && vehicle.GeneratedPerks.All(p =>
                        p.Id != "perk.lancer" && p.Id != "perk.fervor" && p.Id != "perk.to_shreds_vehicle" &&
                        (p.Id != "perk.grind_down" || p.Tier == 2)),
                    "vehicle base/advanced/mentor choices filter unavailable IDs without losing slots");
            }

        void Failure(Action action, string expected)
        {
            try { action(); }
            catch (InvalidOperationException ex)
            {
                check(ex.Message.Contains(expected), "precise perk failure: " + expected);
                return;
            }
            throw new Exception("Expected perk failure: " + expected);
        }
        var record = Record(1000);
        PerkGeneration.Generate(record, 8000, null);
        var pinned = new List<UnityEngine.Object>();
        var leader = new UnitLeaderTemplate();
        DynamicPerkTrees.Apply(record, leader, pinned);
        check(DynamicPerkTrees.Matches(record, leader), "native-shaped tree roundtrip matches saved snapshot");
        var originalTrees = leader.PerkTrees;
        var originalInitial = leader.InitialPerk;
        DynamicPerkTrees.VerifyOrApply(record, leader, pinned);
        check(ReferenceEquals(originalTrees, leader.PerkTrees), "matching saved trees are not replaced");

        string saved = JsonSerializer.Serialize(record);
        string idToBreak = record.GeneratedPerks[0].Id;
        var correct = DataTemplateLoader.AdditionalTemplates[idToBreak];
        DataTemplateLoader.AdditionalTemplates[idToBreak] = new PerkTemplate { Id = "perk.wrong_id" };
        check(!DynamicPerkTrees.IsAvailable(idToBreak) && !DynamicPerkTrees.IsAvailable("perk.missing"),
            "native map aliases and missing templates are unavailable");
        Failure(() => DynamicPerkTrees.Apply(record, leader, pinned),
            $"requested '{idToBreak}', actual 'perk.wrong_id'");
        DataTemplateLoader.AdditionalTemplates.Remove(idToBreak);
        Failure(() => DynamicPerkTrees.Apply(record, leader, pinned), "Missing native perk template: " + idToBreak);
        DataTemplateLoader.AdditionalTemplates[idToBreak] = correct;
        check(ReferenceEquals(originalTrees, leader.PerkTrees) && ReferenceEquals(originalInitial, leader.InitialPerk) &&
            JsonSerializer.Serialize(record) == saved, "lookup failures do not mutate native leader or saved snapshot");

        foreach (var test in new (string Expected, Action<UnitLeaderTemplate> Mutate)[]
        {
            ("initial perk expected", l => l.InitialPerk = null),
            ("tree count expected 1, actual 0", l => l.PerkTrees = new Il2CppReferenceArray<PerkTreeTemplate>(0)),
            ("tree[0] is null", l => l.PerkTrees[0] = null),
            ("option count expected 16, actual 0", l => l.PerkTrees[0].Perks = new Il2CppReferenceArray<Perk>(0)),
            ("option[0] skill expected", l => l.PerkTrees[0].Perks[0] = null),
            ("actual 'perk.wrong_id'", l => l.PerkTrees[0].Perks[0].Skill = new PerkTemplate { Id = "perk.wrong_id" }),
            ("tier expected", l => l.PerkTrees[0].Perks[0].Tier = 99),
        })
        {
            var changed = new UnitLeaderTemplate();
            DynamicPerkTrees.Apply(record, changed, pinned);
            test.Mutate(changed);
            check(DynamicPerkTrees.ReadbackMismatch(record, changed).Contains(test.Expected),
                "readback identifies exact field/index: " + test.Expected);
        }
        leader.AfterPerkTreesAssigned = l =>
        {
            l.AfterPerkTreesAssigned = null;
            l.PerkTrees[0].Perks[0].Tier = 99;
        };
        Failure(() => DynamicPerkTrees.Apply(record, leader, pinned), "option[0]");
        check(ReferenceEquals(originalTrees, leader.PerkTrees) && ReferenceEquals(originalInitial, leader.InitialPerk) &&
            DynamicPerkTrees.Matches(record, leader), "failed readback restores original native tree and initial perk");

        var old = Record(1001);
        PerkGeneration.Generate(old, 8000, null);
        var oldTierOne = old.GeneratedPerks.First(p => p.Tier == 1);
        var deck = old.GeneratedPerks.FirstOrDefault(p => p.Id == "perk.hit_the_deck");
        if (deck != null) deck.Id = oldTierOne.Id;
        oldTierOne.Id = "perk.hit_the_deck";
        string oldSaved = JsonSerializer.Serialize(old);
        var restored = new UnitLeaderTemplate();
        DynamicPerkTrees.VerifyOrApply(old, restored, pinned);
        check(DynamicPerkTrees.Matches(old, restored) && oldTierOne.Tier == 1 &&
            JsonSerializer.Serialize(old) == oldSaved, "old saved tier-1 Hit the Deck restores without rerolling/rebalancing");

        var unavailableVehicle = Record(1002, true);
        VehiclePerkGeneration.Generate(unavailableVehicle, null);
        string previous = JsonSerializer.Serialize(unavailableVehicle);
        Failure(() => VehiclePerkGeneration.Generate(unavailableVehicle, null, _ => false),
            "Insufficient curated vehicle perk options");
        check(JsonSerializer.Serialize(unavailableVehicle) == previous, "failed vehicle generation preserves previous snapshot");
    }
}