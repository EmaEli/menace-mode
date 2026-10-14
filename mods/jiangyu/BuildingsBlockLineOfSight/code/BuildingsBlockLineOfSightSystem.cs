using MenaceModSuite.Shared;

namespace BuildingsBlockLineOfSight;

// Jiangyu host binding. All gameplay logic is in the shared Plugin partial class;
// the lifecycle adaptation is in SuiteSystem (GameplaySuite/src/Shared/Jiangyu).
public sealed class BuildingsBlockLineOfSightSystem : SuiteSystem<Plugin>
{
    protected override string HarmonyId => "luca.buildingsblocklos";
}
