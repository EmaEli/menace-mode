using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppMenace.Strategy;

namespace SquaddieFieldPromotions;

public sealed class SquaddieServiceRecord
{
    public int SquaddieId { get; set; }
    public List<ServiceUnitRecord> Units { get; set; } = new();
}

public sealed class ServiceUnitRecord
{
    public string LeaderTemplateId { get; set; } = "";
    public string UnitTitle { get; set; } = "";
    public string CommanderName { get; set; } = "";
    public int MissionsServed { get; set; }
    public int WoundedMissions { get; set; }
}

internal sealed class MissionServiceGroup
{
    internal string LeaderTemplateId { get; }
    internal string UnitTitle { get; }
    internal string CommanderName { get; }
    internal int[] SquaddieIds { get; }
    internal int[] WoundedSquaddieIds { get; }

    internal MissionServiceGroup(string leaderTemplateId, string unitTitle, string commanderName,
        IEnumerable<int> squaddieIds, IEnumerable<int> woundedSquaddieIds)
    {
        LeaderTemplateId = leaderTemplateId;
        UnitTitle = unitTitle;
        CommanderName = commanderName;
        SquaddieIds = squaddieIds.Distinct().OrderBy(id => id).ToArray();
        WoundedSquaddieIds = woundedSquaddieIds.Distinct().OrderBy(id => id).ToArray();
    }
}

internal sealed class MissionServiceSnapshot
{
    internal string MissionKey { get; }
    internal IReadOnlyList<MissionServiceGroup> Groups { get; }

    internal MissionServiceSnapshot(string missionKey, IEnumerable<MissionServiceGroup> groups)
    {
        MissionKey = missionKey;
        Groups = groups.ToArray();
    }
}

internal static class SquaddieServiceHistory
{
    internal static MissionServiceSnapshot Capture(Mission mission,
        Il2CppSystem.Collections.Generic.List<BaseUnitLeader> deployedLeaders)
    {
        if (mission == null || deployedLeaders == null)
            throw new InvalidOperationException("Mission or deployed leader list is unavailable.");

        var groups = new List<MissionServiceGroup>();
        foreach (object value in PrivateMember.Enumerate(deployedLeaders))
        {
            if (value is not BaseUnitLeader leader || leader.LeaderTemplate == null)
                throw new InvalidOperationException("A deployed leader has no native unit template.");

            var participants = new HashSet<int>(ReadIds(leader.GetSquaddieIds()));
            var wounded = new HashSet<int>();
            var statistics = leader.GetStatistics();
            if (statistics != null)
            {
                foreach (int id in ReadIds(statistics.GetLastMissionWoundedSquaddieIds()))
                {
                    participants.Add(id);
                    wounded.Add(id);
                }
                foreach (int id in ReadIds(statistics.GetLastMissionDownedSquaddieIds()))
                    participants.Add(id);
            }

            var template = leader.LeaderTemplate;
            string unitTitle = template.UnitTitle?.GetRawDefaultTranslation();
            string commander = PromotionIdentity.CommanderName(template.SpeakerTemplate);
            groups.Add(new MissionServiceGroup(template.GetID(), unitTitle, commander, participants, wounded));
        }

        return new MissionServiceSnapshot(KeyFor(mission), groups);
    }

    internal static string KeyFor(Mission mission)
    {
        if (mission == null || mission.GetTemplate() == null ||
            string.IsNullOrWhiteSpace(mission.GetTemplate().GetID()))
            throw new InvalidOperationException("Native mission identity is unavailable.");
        return mission.GetTemplate().GetID() + ":" + mission.GetIdx() + ":" + mission.GetSeed();
    }

    internal static bool Record(PromotionLedger ledger, MissionServiceSnapshot snapshot)
    {
        if (ledger == null || snapshot == null) throw new ArgumentNullException();
        ledger.Validated();
        ValidateSnapshot(snapshot);
        if (ledger.ProcessedMissionKeys.Contains(snapshot.MissionKey, StringComparer.Ordinal)) return false;

        // Copy-on-write lets the caller revert the two collections if sidecar persistence fails.
        var nextRecords = ledger.ServiceRecords.Select(Clone).ToList();
        var nextKeys = new List<string>(ledger.ProcessedMissionKeys) { snapshot.MissionKey };
        foreach (var group in snapshot.Groups)
        {
            var wounded = new HashSet<int>(group.WoundedSquaddieIds);
            foreach (int id in group.SquaddieIds)
            {
                var squaddie = nextRecords.FirstOrDefault(record => record.SquaddieId == id);
                if (squaddie == null)
                {
                    squaddie = new SquaddieServiceRecord { SquaddieId = id };
                    nextRecords.Add(squaddie);
                }

                var unit = squaddie.Units.FirstOrDefault(item => item.LeaderTemplateId == group.LeaderTemplateId);
                if (unit == null)
                {
                    unit = new ServiceUnitRecord
                    {
                        LeaderTemplateId = group.LeaderTemplateId,
                        UnitTitle = PromotionIdentity.Clean(group.UnitTitle, group.LeaderTemplateId, 120),
                        CommanderName = PromotionIdentity.Clean(group.CommanderName, "", 100),
                    };
                    squaddie.Units.Add(unit);
                }
                checked
                {
                    unit.MissionsServed++;
                    if (wounded.Contains(id)) unit.WoundedMissions++;
                }
            }
        }

        ledger.ServiceRecords = nextRecords;
        ledger.ProcessedMissionKeys = nextKeys;
        return true;
    }

    private static SquaddieServiceRecord Clone(SquaddieServiceRecord source) => new()
    {
        SquaddieId = source.SquaddieId,
        Units = source.Units.Select(unit => new ServiceUnitRecord
        {
            LeaderTemplateId = unit.LeaderTemplateId,
            UnitTitle = unit.UnitTitle,
            CommanderName = unit.CommanderName,
            MissionsServed = unit.MissionsServed,
            WoundedMissions = unit.WoundedMissions,
        }).ToList(),
    };

    private static int[] ReadIds(object list)
    {
        var ids = new List<int>();
        foreach (object value in PrivateMember.Enumerate(list))
        {
            int id = Convert.ToInt32(value);
            if (id <= 0) throw new InvalidOperationException("Native mission contains an invalid squaddie ID.");
            ids.Add(id);
        }
        return ids.ToArray();
    }

    private static void ValidateSnapshot(MissionServiceSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.MissionKey) || snapshot.MissionKey.Length > 256 || snapshot.Groups == null)
            throw new InvalidDataException("Mission service snapshot has an invalid identity.");
        var seenSquaddies = new HashSet<int>();
        foreach (var group in snapshot.Groups)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.LeaderTemplateId) || group.LeaderTemplateId.Length > 256 ||
                group.SquaddieIds == null || group.WoundedSquaddieIds == null)
                throw new InvalidDataException("Mission service snapshot contains an invalid unit.");
            var participants = new HashSet<int>();
            foreach (int id in group.SquaddieIds)
                if (id <= 0 || !participants.Add(id) || !seenSquaddies.Add(id))
                    throw new InvalidDataException("Mission service snapshot contains an ambiguous squaddie assignment.");
            foreach (int id in group.WoundedSquaddieIds)
                if (!participants.Contains(id))
                    throw new InvalidDataException("Wounded squaddie is not attributed to a deployed unit.");
        }
    }
}