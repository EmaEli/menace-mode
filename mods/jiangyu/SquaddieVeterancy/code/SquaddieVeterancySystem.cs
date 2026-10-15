using MenaceModSuite.Shared;

namespace SquaddieProbe;

// Jiangyu host binding. All gameplay logic is in the shared Plugin partial class;
// the lifecycle adaptation is in SuiteSystem (GameplaySuite/src/Shared/Jiangyu).
public sealed class SquaddieVeterancySystem : SuiteSystem<Plugin>
{
    protected override string HarmonyId => "luca.squaddieprobe";
}
