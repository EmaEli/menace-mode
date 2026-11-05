using SmokeOverhaul;
using Il2CppMenace.Tactical.TileEffects;

int checks = 0;
void Check(bool condition, string name)
{ if (!condition) throw new Exception(name); checks++; }
TileEffectHandler Make(int id = 1)
{
    var handler = new TileEffectHandler { Pointer = new IntPtr(id) };
    handler.Tile!.Effects.Add(handler);
    return handler;
}
Plugin.Reset();
var smoke = Make();
RuntimeSettings.DebugLogging = false;
Plugin.ObserveSmokeAssigned(smoke);
Check(Plugin.Pending == 0 && Plugin.Messages.Count == 0, "debug off is inert");
RuntimeSettings.DebugLogging = true;
RuntimeSettings.Enabled = false;
Plugin.ObserveSmokeAssigned(smoke);
Check(Plugin.Pending == 0, "disabled gameplay is inert");
RuntimeSettings.Enabled = true;
Plugin.AiSmokeOnMainThread = false;
Plugin.ObserveSmokeAssigned(smoke);
Check(Plugin.Pending == 0, "worker callbacks are ignored");
Plugin.AiSmokeOnMainThread = true;
smoke.Template.Smoke = false;
Plugin.ObserveSmokeAssigned(smoke);
Check(Plugin.Pending == 0, "non-smoke ignored");
smoke.Template.Smoke = true;
smoke.Delay = 3;
Plugin.ObserveSmokeAssigned(smoke);
Plugin.ObserveSmokeAssigned(Make());
Check(Plugin.Pending == 1 && Plugin.Messages.Count == 1, "deduplicate native pointers");
Plugin.Tick(4.9f);
Check(Plugin.Messages.Count == 1, "wait for native visual delay");
Plugin.Tick(5);
Check(Plugin.Messages.Last().Contains("attached=True") && Plugin.Messages.Last().Contains("worldPresent=False"), "distinguish live tile with missing visual");
smoke.m_WorldObject = new();
smoke.Lifetime = 1;
Plugin.Tick(11);
Check(Plugin.Pending == 0 && Plugin.Messages.Last().Contains("worldActive=True"), "second sample releases reference");
Check(smoke.Template.RemoveAfterRounds == 4 && smoke.Tile!.Effects.Count == 1 && smoke.Lifetime == 1, "observations never mutate lifecycle");

Plugin.Reset();
smoke = Make();
Plugin.ObserveSmokeAssigned(smoke);
Plugin.ObserveSmokeRemoved(smoke);
Check(Plugin.Pending == 0 && Plugin.Messages.Last().Contains("native-remove"), "early removal recorded before native cleanup");
Check(smoke.Tile!.Effects.Count == 1, "removal observer does not remove effect");
Plugin.Tick(20);
Check(Plugin.Messages.Count == 2, "removed handlers never sampled again");

Plugin.Reset();
for (int i = 0; i < 30; i++) Plugin.ObserveSmokeAssigned(Make(i));
Check(Plugin.Pending == 12, "scene budget limits retained references");
Plugin.Tick(2); Plugin.Tick(8);
Plugin.ObserveSmokeAssigned(Make(99));
Check(Plugin.Pending == 0 && Plugin.Messages.Count == 36, "finished observations do not reset budget");
Plugin.Reset();
Plugin.ObserveSmokeAssigned(Make());
Check(Plugin.Pending == 1, "scene reset renews budget");
RuntimeSettings.DebugLogging = false;
Plugin.Tick(3);
Check(Plugin.Pending == 0 && Plugin.Messages.Count == 1, "debug disable drops references silently");
RuntimeSettings.DebugLogging = true;
Plugin.Reset();
smoke = Make();
Plugin.ObserveSmokeAssigned(smoke);
smoke.Throw = true;
Plugin.Tick(2); Plugin.Tick(8);
Check(Plugin.Pending == 0 && Patches.Errors == 2, "failed native reads contained and released");
Plugin.Reset();
smoke = Make();
smoke.Tile!.Effects.Clear();
smoke.m_WorldObject = new() { activeInHierarchy = false };
Plugin.ObserveSmokeAssigned(smoke);
Plugin.Tick(2);
Check(Plugin.Messages.Last().Contains("attached=False") && Plugin.Messages.Last().Contains("worldActive=False"), "detached/inactive states distinguished");
Plugin.Reset();
Check(Plugin.Pending == 0, "unload/scene reset clears references");
Console.WriteLine($"PASS: {checks} production smoke lifecycle diagnostic checks; native rendering not executed.");