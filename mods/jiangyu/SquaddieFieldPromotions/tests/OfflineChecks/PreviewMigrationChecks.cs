using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.UI.Strategy;
using Jiangyu.Sdk;
using SquaddieFieldPromotions;

internal static class PreviewMigrationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var system = new PromotionSystem();
        var state = StrategyState.Current = new StrategyState();
        system.OnInit();
        var selection = PromotionRules.Choose(new[] { new Candidate(77, "Legacy", "Soldier", 0, 1, 1, 0, false) });
        var record = LeaderForge.CreateRecord(state, selection, 0);
        var soldier = new Squaddie { Id = 77, Name = "Legacy", Nickname = "Soldier", SkinColor = 1 };
        var ledger = system.Context.State.Get<PromotionLedger>();
        ledger.Records.Add(record);
        state.Squaddies.m_AliveSquaddies.Add(soldier);
        void Open() => system.Context.Patches.Postfixes["OnOpened"](new PatchInfo { Instance = new BlackMarketUIScreen() });
        Open();
        check(state.Squaddies.IsAlive(77) && !record.SourceRemoved, "orphaned legacy record never removes a soldier");
        state.Roster.AddHirableLeader(new UnitLeaderTemplate { Id = record.LeaderId });
        system.Context.State.FailSave = true;
        Open();
        check(state.Squaddies.IsAlive(77) && !record.SourceRemoved, "legacy migration failure restores source and flag");
        system.Context.State.FailSave = false;
        Open();
        check(!state.Squaddies.IsAlive(77) && record.SourceRemoved && state.Roster.Hirable.Count == 1,
            "legacy matching promotion consumes its source, not its leader");
        int saves = system.Context.State.Saves;
        Open();
        check(system.Context.State.Saves == saves, "legacy migration is idempotent");
        // Loading an older save supplies its own native pool and ledger, not a global ID blacklist.
        record.SourceRemoved = false;
        state.Squaddies.m_AliveSquaddies.Add(soldier);
        Open();
        check(record.SourceRemoved && !state.Squaddies.IsAlive(77), "rolling back to an older preview save reconciles again");
        system.OnUnload();
        record.SourceRemoved = false;
        state.Squaddies.m_AliveSquaddies.Add(soldier);
        Open();
        check(state.Squaddies.IsAlive(77) && !record.SourceRemoved, "unloaded callbacks cannot migrate source soldiers");
    }
}