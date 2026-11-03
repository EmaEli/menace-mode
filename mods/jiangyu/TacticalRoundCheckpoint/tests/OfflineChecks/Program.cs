using System.Text.Json;
using TacticalRoundCheckpoint;

int checks = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) throw new Exception(name);
}
void Throws<T>(Action action, string name) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, name); return; }
    throw new Exception("Expected " + typeof(T).Name + ": " + name);
}
RoundObservation Snapshot(int round) => new("battle-a", round, "mission.test", 123, "native-hash",
    new[] { new ActorObservation(1, 0, "entity.test", 4, 7, 60, 100, 50, 0, false, false) });

Check(RoundBoundary.Advanced(0, 1), "first full round captured");
Check(RoundBoundary.Advanced(1, 2), "next full round captured");
Check(!RoundBoundary.Advanced(2, 2), "unit activation is not a full round");
Check(!RoundBoundary.Advanced(2, 1), "round rollback is not a new checkpoint");
Check(!RoundBoundary.Advanced(-1, 0), "deployment round excluded");
Check(!RoundBoundary.Advanced(-1, 1), "failed before-hook excluded");

string root = Path.Combine(Path.GetFullPath(args[0]), "checkpoint-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string campaign = Path.Combine(root, "campaign.save");
File.WriteAllText(campaign, "untouched campaign sentinel");
string output = Path.Combine(root, "diagnostics", "battle-a");
var store = new CheckpointStore(output);
for (int round = 1; round <= 7; round++) Check(store.Write(Snapshot(round)), "write round " + round);
var files = Directory.GetFiles(output, "*.json");
Check(files.Length == 3, "bounded three-slot ring");
var rounds = new List<int>();
foreach (string file in files)
{
    using var json = JsonDocument.Parse(File.ReadAllBytes(file));
    var saved = json.RootElement;
    rounds.Add(saved.GetProperty("Round").GetInt32());
    Check(!saved.GetProperty("CanResume").GetBoolean(), "cannot advertise a diagnostic as loadable");
    Check(saved.GetProperty("MissingState").GetArrayLength() == 8, "incomplete state declared");
    Check(saved.GetProperty("Actors")[0].GetProperty("ActionPoints").GetInt32() == 100, "AP observation retained");
    Check(saved.GetProperty("MissionSeed").GetInt32() == 123, "seed retained");
}
Check(rounds.Order().SequenceEqual(new[] { 5, 6, 7 }), "latest three full rounds retained");
Check(!store.Write(Snapshot(7)), "duplicate round ignored");
Check(!store.Write(Snapshot(6)), "older round ignored");
Throws<InvalidOperationException>(() => store.Write(Snapshot(8) with { BattleId = "battle-b" }), "battle isolation");
Throws<ArgumentException>(() => store.Write(Snapshot(0)), "round zero rejected");

string slot = Path.Combine(output, "slot-1.json");
byte[] good = File.ReadAllBytes(slot);
var invalid = Snapshot(8) with
{
    Actors = new[] { Snapshot(8).Actors[0] with { Morale = float.NaN } }
};
Throws<ArgumentException>(() => store.Write(invalid), "nonfinite observation rejected before opening file");
Check(File.ReadAllBytes(slot).SequenceEqual(good), "serialization failure preserves prior checkpoint");
Check(!File.Exists(slot + ".pending"), "serialization failure creates no pending file");

File.WriteAllText(slot + ".pending", "interrupted earlier observation");
Throws<IOException>(() => store.Write(Snapshot(8)), "stale pending file fails closed");
Check(File.ReadAllBytes(slot).SequenceEqual(good), "write failure preserves prior checkpoint");
Check(File.ReadAllText(slot + ".pending") == "interrupted earlier observation", "pending file not truncated");
Check(File.ReadAllText(campaign) == "untouched campaign sentinel", "campaign save never changed");

if (!OperatingSystem.IsWindows())
{
    string link = Path.Combine(root, "linked-diagnostics");
    Directory.CreateSymbolicLink(link, output);
    Throws<IOException>(() => new CheckpointStore(link), "redirected diagnostic directory rejected");
}
Console.WriteLine($"{checks} round checkpoint behavior checks passed. Test artifacts: {root}");