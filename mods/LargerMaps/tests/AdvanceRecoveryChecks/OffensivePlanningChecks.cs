using AIPerformance;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.AI;
using Il2CppMenace.Tactical.AI.Behaviors;
using Il2CppMenace.Tactical.AI.Data;
using Il2CppMenace.Tactical.Objectives;
using Il2CppMenace.Tags;

internal static class OffensivePlanningChecks
{
    private sealed class Fixture
    {
        internal readonly Actor Actor = new();
        internal readonly Actor Enemy = new() { Tile = new(8, 0), Detected = true, Pointer = (IntPtr)901 };
        internal readonly InflictDamage Damage = new();
        internal readonly TileScore Front, Back, Side;
        internal Agent Agent => Actor.Agent;
        internal Fixture()
        {
            Plugin.BeginFixture(); Plugin.BeginOffensiveFixture();
            TacticalManager.Instance = new(); AIConfig.WEIGHTS = new();
            Agent.Faction.Kind = 9;
            Agent.Tiles[Actor.Tile] = new() { Tile = Actor.Tile };
            TileScore Add(int x, int z)
            {
                var tile = new Tile(x, z); var score = new TileScore { Tile = tile };
                Agent.Tiles.Add(tile, score); return score;
            }
            Front = Add(4, 0); Back = Add(-4, 0); Side = Add(4, 4);
            Front.SafetyScore = 156; Back.SafetyScore = 12; // Raw safety cost: higher is worse before native normalization.
            Damage.Owner = Agent; Damage.Value = (_, from) => from.GetX() >= 4 ? 10 : 0;
            Agent.Behaviors.Add(Damage); Enemy.Tile.Entity = Enemy;
            Agent.Faction.Opponents.Add(new() { Actor = Enemy });
        }
        internal void Plan() => Plugin.OffensiveScoresFixture(Agent);
    }

    internal static void Run(Action<bool, string> check)
    {
        var f = new Fixture(); var path = f.Front.Path;
        f.Plan();
        check(f.Front.UtilityByAttacksScore == 50 && f.Front.UtilityScore == 50,
            "bounded native damage restores a firing position, not just cover");
        check(f.Back.UtilityByAttacksScore == 0 && f.Back.UtilityScore == 0, "cover-only retreat gains no attack value");
        check(f.Front.MinimumUtilityAPCost == f.Damage.Skill.Cost, "shot cost retained for native movement reserve");
        check(f.Front.SafetyScore == 156 && f.Back.SafetyScore == 12 && ReferenceEquals(f.Front.Path, path) &&
            f.Front.APCost == 0, "planning leaves cover, threats, path and AP ownership native");
        f.Plan();
        check(f.Front.UtilityScore == 50, "repeated observations cannot inflate projected utility");
        check(Plugin.OffensiveEnabled, "native projection hook remains enabled after successful scoring");

        f = new(); f.Damage.Score = 13; f.Damage.m_Candidates.Add(new());
        var move = f.Agent.Behaviors[1]; move.Score = 200;
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Agent, move), f.Damage),
            "already legal positive native shot beats higher-scoring cover-only Move");
        var better = new InflictDamage { Owner = f.Agent, Score = 18 }; better.m_Candidates.Add(new());
        f.Agent.Behaviors.Add(better);
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Agent, move), better), "best existing damage action selected");
        var smoke = new Behavior { ID = "Smoke", Score = 5 };
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Agent, smoke), smoke), "non-movement emergency/support actions preserved");
        f.Damage.m_Candidates.Clear(); better.m_Candidates.Clear();
        check(ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Agent, move), move), "score alone cannot invent an attack candidate");

        foreach (string reason in new[] { "allied", "alien", "sleeping", "deployment", "contained", "dying", "moving",
            "AP", "suppression", "morale", "avoid", "machine", "xeno", "drone", "nan" })
        {
            f = new(); f.Damage.Score = 10; f.Damage.m_Candidates.Add(new());
            switch (reason)
            {
                case "allied": f.Agent.Faction.Allied = true; break;
                case "alien": f.Agent.Faction.Kind = 7; break;
                case "sleeping": f.Agent.Sleeping = true; break;
                case "deployment": f.Agent.Deployment = true; break;
                case "contained": f.Actor.Contained = true; break;
                case "dying": f.Actor.Dying = true; break;
                case "moving": f.Actor.Moving = true; break;
                case "AP": f.Actor.AP = 0; break;
                case "suppression": f.Actor.Suppression = .9f; break;
                case "morale": f.Actor.Morale = .2f; break;
                case "avoid": f.Agent.Role.AvoidOpponents = true; break;
                case "machine": f.Actor.Tags.Add(TagType.MACHINE); break;
                case "xeno": f.Actor.Tags.Add(TagType.XENO); break;
                case "drone": f.Actor.Tags.Add(TagType.DRONE); break;
                case "nan": f.Agent.Role.Move = float.NaN; break;
            }
            f.Plan(); move = f.Agent.Behaviors[1];
            check(f.Damage.ValuesRead == 0 && f.Front.UtilityScore == 0 &&
                ReferenceEquals(Plugin.OffensiveChoiceFixture(f.Agent, move), move), reason + " retains native behavior");
        }

        foreach (string reason in new[] { "hidden", "allied-target", "empty", "limited", "AP", "range/LoF", "applicability", "teleport",
            "AoE", "indirect", "deployment", "setup", "zero", "negative", "nan" })
        {
            f = new();
            switch (reason)
            {
                case "hidden": f.Enemy.Detected = false; f.Enemy.RejectHiddenReads = true; break;
                case "allied-target": f.Enemy.FactionKind = 3; f.Agent.Faction.AllyCivilian = true; break;
                case "empty": f.Damage.Skill.Empty = true; break;
                case "limited": f.Damage.Skill.Limited = true; break;
                case "AP": f.Damage.Skill.Cost = 101; break;
                case "range/LoF": f.Damage.Skill.Usable = (_, _) => false; break;
                case "applicability": f.Damage.Skill.Applicable = false; break;
                case "teleport": f.Damage.Skill.Teleport = true; break;
                case "AoE": f.Damage.Skill.Template.AoEType = 1; break;
                case "indirect": f.Damage.Skill.Template.IsLineOfFireNeeded = false; break;
                case "deployment": f.Damage.Skill.Template.IsDeploymentRequired = true; break;
                case "setup": f.Damage.Skill.Template.IsWeaponSetupRequired = true; break;
                case "zero": f.Damage.Value = (_, _) => 0; break;
                case "negative": f.Damage.Value = (_, _) => -1; break;
                case "nan": f.Damage.Value = (_, _) => float.NaN; break;
            }
            f.Plan();
            check(f.Front.UtilityByAttacksScore == 0 && Plugin.OffensiveEnabled, reason + " cannot fabricate a useful shot");
            if (reason == "hidden") check(f.Enemy.PositionReads == 0, "no stale contact position polling");
        }

        f = new(); f.Enemy.Detected = false; f.Plan();
        check(f.Front.UtilityScore > AIConfig.WEIGHTS.UtilityThreshold && f.Back.UtilityScore == 0,
            "no-shot advance is directional immediately, not an Idle-only rescue");
        check(f.Front.UtilityByAttacksScore == 0 && f.Front.SafetyScore == 156, "mission progress is not a fake shot or edited safety");
        f = new(); f.Enemy.Detected = false; f.Agent.Behaviors.Add(new Behavior { ID = "Buff", Score = 3 }); f.Plan();
        check(f.Front.UtilityScore == 0, "positive native support action blocks blind advance even without tile utility");
        foreach (string context in new[] { "hold", "mixed", "inside", "no-order", "ignored-zone", "existing-support" })
        {
            f = new(); f.Enemy.Detected = false;
            switch (context)
            {
                case "hold": f.Agent.Zones[0].Order = 2; break;
                case "mixed": f.Agent.Zones.Add(new() { Order = 2 }); break;
                case "inside": f.Agent.Zones[0].Area = new(-1, -1, 4, 4); break;
                case "no-order": f.Agent.Zones.Clear(); break;
                case "ignored-zone": f.Agent.Role.ZoneImportanceWeight = 0; break;
                case "existing-support": f.Back.UtilityScore = 1; break;
            }
            f.Plan(); check(f.Front.UtilityScore == 0, context + " prevents blind objective advance");
        }
        f = new(); f.Enemy.Detected = false;
        f.Agent.Zones.Add(new() { Area = new(-20, 0, 4, 4) }); f.Actor.ID = 11; f.Plan();
        check(f.Back.UtilityScore > 0 && f.Front.UtilityScore == 0, "multiple orders use native ID allocation, not all-unit blob");

        f = new(); f.Agent.Faction.Actors.Add(new() { Pointer = (IntPtr)912, Tile = new(2, 1) });
        f.Agent.Faction.Actors.Add(new() { Pointer = (IntPtr)913, Tile = new(2, -1) }); f.Plan();
        check(f.Front.UtilityByAttacksScore == 75, "two nearby supporters increase offensive utility by bounded 50 percent");
        f = new(); f.Agent.Faction.Actors.Add(new() { Pointer = (IntPtr)914, Tile = new(60, 60) }); f.Plan();
        check(f.Front.UtilityByAttacksScore == 50, "distant roster superiority does not grant local support");

        foreach (bool visible in new[] { false, true })
        {
            f = new(); f.Agent.Faction.Opponents.Clear();
            f.Enemy.FactionKind = 3; f.Enemy.Detected = visible; f.Enemy.RejectHiddenReads = true;
            TacticalManager.Instance.Mission.Objectives.Objectives.Add(new ProtectUnit { Target = f.Enemy });
            f.Plan();
            check(f.Front.UtilityByAttacksScore == (visible ? 100 : 0), "mission civilian projected priority requires native detection");
            if (!visible) check(f.Enemy.PositionReads == 0, "mission identity never reveals a hidden civilian position");
        }

        f = new(); f.Damage.Skill.Usable = (_, _) => throw new Exception("native scoring failure"); f.Plan();
        check(!Plugin.OffensiveEnabled && f.Front.UtilityScore == 0, "native failure disables bounded planning without partial edits");
        f = new(); TileScore.UtilityWritesBeforeThrow = 1; f.Plan();
        check(!Plugin.OffensiveEnabled && f.Front.UtilityScore == 0 && f.Side.UtilityScore == 0 &&
            f.Front.UtilityByAttacksScore == 0 && f.Front.MinimumUtilityAPCost == 1024,
            "partial native write rolls back every utility and reserve field");

        var budget = new OffensivePlanningBudget();
        int accepted = 0;
        Parallel.For(0, 100, _ => { if (budget.Spend(1, 1, 10, 20)) Interlocked.Increment(ref accepted); });
        check(accepted == OffensivePlanningBudget.PerRound, "concurrent/repeated evaluations cannot exceed per-unit round budget");
        check(!budget.Spend(1, 0, 10, 20) && budget.Spend(1, 2, 10, 20) && !budget.Spend(1, 1, 10, 20),
            "round budget renews on real round progression and rejects rewind");
        budget.Reset(); check(budget.Spend(2, 1, 10, 20), "mission reset clears managed-only probe state");
        check(!budget.Spend(1, 1, 10, 20), "late old-map task cannot reset or replenish current mission budget");
        f = new();
        better = new() { Owner = f.Agent }; f.Agent.Behaviors.Add(better); f.Plan();
        check(better.ValuesRead > 0 && f.Damage.ValuesRead > 0, "both eligible weapons receive bounded native probes");
        f = new(); f.Agent.Zones[0].Order = 2; f.Plan();
        check(f.Front.UtilityByAttacksScore == 0, "firing-position search cannot take defenders outside their ordered area");
        f = new(); var adjacent = new Tile(1, 0); f.Agent.Tiles.Add(adjacent, new() { Tile = adjacent });
        f.Damage.Value = (_, from) => from.GetX() == 1 ? 10 : 0; f.Plan();
        check(f.Agent.Tiles[adjacent].UtilityByAttacksScore > 0, "current origin never crowds out adjacent firing positions");
        f = new();
        for (int x = -6; x <= 6; x++)
        for (int z = -6; z <= 6; z++)
        {
            var tile = new Tile(x, z); f.Agent.Tiles.Add(tile, new() { Tile = tile });
        }
        for (int i = 0; i < 100; i++)
        {
            var enemy = new Actor { Detected = true, Tile = new(10 + i, 5), Pointer = (IntPtr)(1000 + i) };
            enemy.Tile.Entity = enemy; f.Agent.Faction.Opponents.Add(new() { Actor = enemy });
        }
        var visited = new HashSet<IntPtr>();
        f.Damage.Value = (target, _) => { visited.Add(target.Pointer); return 10; };
        f.Plan();
        check(f.Damage.Skill.Probes > 0 && f.Damage.Skill.Probes <= OffensivePlanningBudget.PerEvaluation &&
            visited.Count <= OffensivePlanningRuntime.MaxTargets, "dense map/contact set stays within evaluation and target caps");
        for (int i = 0; i < 20; i++) f.Plan();
        check(f.Damage.Skill.Probes <= OffensivePlanningBudget.PerRound, "same-unit repeated AI passes cannot replenish round probes");
    }
}