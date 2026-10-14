using MenaceModSuite.Shared;

namespace MenaceOverwatch;

// Jiangyu host binding. All gameplay logic is in the shared Plugin partial class;
// the lifecycle adaptation is in SuiteSystem (GameplaySuite/src/Shared/Jiangyu).
public sealed class OverwatchSystem : SuiteSystem<Plugin>
{
    protected override string HarmonyId => "luca.overwatch";
}
