using MenaceModSettings;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

var capture = new HudPointerCapture();
Check(!capture.Sample(0, false, 0, 0), "empty map must remain usable");
Check(capture.Sample(1, true, 0, 0), "hover over a panel blocks map input");
Check(!capture.Sample(2, false, 0, 0), "leaving an idle panel unblocks immediately");

// Each physical button is captured independently. Multiple native queries in the same
// frame must not lose the release guard, including when the panel disappears meanwhile.
for (int button = 0; button < 3; button++)
{
    int mask = 1 << button;
    capture.Reset();
    Check(capture.Sample(10, true, mask, mask), "HUD press");
    Check(capture.Sample(11, false, mask, 0), "drag out of panel");
    Check(capture.Sample(12, false, 0, 0), "release outside must be consumed");
    Check(capture.Sample(12, false, 0, 0), "second handler on release frame");
    Check(!capture.Sample(13, false, 0, 0), "capture must not stick after release");
    Check(!capture.Sample(14, false, mask, mask), "map press must pass");
    Check(capture.Sample(15, true, mask, 0), "map drag hovering a panel");
    Check(!capture.Sample(16, false, mask, 0), "map-origin drag must not become captured");
    Check(!capture.Sample(17, false, 0, 0), "map-origin release must pass");
}

capture.Reset();
Check(capture.Sample(20, true, 3, 3), "two held HUD buttons");
Check(capture.Sample(21, false, 2, 0), "first button released, second still held");
Check(capture.Sample(22, false, 2, 0), "second button remains captured");
Check(capture.Sample(23, false, 0, 0), "last release consumed");
Check(!capture.Sample(24, false, 0, 0), "both released");

for (int frame = 30; frame < 40; frame += 2)
{
    Check(capture.Sample(frame, true, 1, 1), "repeated HUD click down");
    Check(capture.Sample(frame + 1, true, 0, 0), "repeated HUD click up");
}
capture.Sample(40, true, 1, 1);
capture.Reset();
Check(!capture.Sample(41, false, 0, 0), "scene/focus reset clears capture");
LeaderNameChecks.Run(Check);
SettingsChecks.Run(Check);
CoverSettingsChecks.Run(Check);
CoordinatedFireChecks.Run(Check);
SmokeAiChecks.Run(Check);
AssaultDefenseChecks.Run(Check);
LargerMaps.Plugin.RunPlanningChecks(Check);
if (args.Length > 0) ArtifactChecks.Run(Check, args[0]);
Console.WriteLine($"Offline C# regression checks: {checks} passed.");