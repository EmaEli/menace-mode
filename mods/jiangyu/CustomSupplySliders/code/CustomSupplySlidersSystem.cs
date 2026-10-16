using MenaceModSuite.Shared;

namespace MenaceCustomSupplySliders;

// Jiangyu host binding. All gameplay logic is in the shared Plugin partial class;
// the lifecycle adaptation is in SuiteSystem (GameplaySuite/src/Shared/Jiangyu).
public sealed class CustomSupplySlidersSystem : SuiteSystem<Plugin>
{
    protected override string HarmonyId => "luca.customsupplysliders";
}
