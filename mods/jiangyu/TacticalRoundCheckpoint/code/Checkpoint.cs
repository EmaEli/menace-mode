using System.Text.Json;

namespace TacticalRoundCheckpoint;

// A deliberately incomplete observation, never a loadable save or a list of setters.
public sealed record ActorObservation(int Faction, int RosterIndex, string Template,
    int? X, int? Z, int Hitpoints, int ActionPoints, float Morale, float Suppression,
    bool TurnDone, bool Contained);

public sealed record RoundObservation(string BattleId, int Round, string Mission,
    int MissionSeed, string GameAssemblySha256, ActorObservation[] Actors)
{
    public int FormatVersion => 1;
    public string Kind => "diagnostic-round-observation";
    public bool CanResume => false;
    public string[] MissingState => new[]
    {
        "campaign-baseline-and-stable-entity-identities", "dead-and-spawned-entities",
        "weapons-ammunition-inventory-and-squaddie-elements", "skill-effects-and-cooldowns",
        "map-destruction-smoke-fire-and-fog", "objectives-reinforcements-and-delayed-abilities",
        "rng-ai-and-initiative", "mod-owned-tactical-state"
    };
}

public static class RoundBoundary
{
    public static bool Advanced(int before, int after) => before >= 0 && after > before;
}

public sealed class CheckpointStore
{
    private readonly string _directory;
    private string? _battleId;
    private int _lastRound;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public CheckpointStore(string directory)
    {
        _directory = Path.GetFullPath(directory);
        RejectLinks(_directory);
        Directory.CreateDirectory(_directory);
        RejectLinks(_directory);
    }

    public bool Write(RoundObservation snapshot)
    {
        if (snapshot.Round < 1 || string.IsNullOrWhiteSpace(snapshot.BattleId))
            throw new ArgumentException("A positive round and battle identity are required.");
        if (_battleId != null && _battleId != snapshot.BattleId)
            throw new InvalidOperationException("Do not mix battles in one checkpoint directory.");
        if (snapshot.Round <= _lastRound) return false;

        // Serialize first: invalid data (including NaN) must not replace a good observation.
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, Json);
        int slot = (snapshot.Round - 1) % 3;
        string target = Path.Combine(_directory, $"slot-{slot}.json");
        string pending = target + ".pending";
        RejectLinks(_directory);
        RejectLinks(target);
        RejectLinks(pending);
        // A stale pending file fails closed, rather than truncating an unknown file.
        using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(bytes);
            file.Flush(flushToDisk: true);
        }
        File.Move(pending, target, overwrite: true);
        _battleId = snapshot.BattleId;
        _lastRound = snapshot.Round;
        return true;
    }

    private static void RejectLinks(string path)
    {
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Diagnostic output must not follow links.");
        }
    }
}