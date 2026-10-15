using MenaceModSuite.Shared;

namespace MenaceInfantryAssault;

// Jiangyu host binding. All gameplay logic is in the shared Plugin partial class;
// the lifecycle adaptation is in SuiteSystem (GameplaySuite/src/Shared/Jiangyu).
public sealed class InfantryAssaultSystem : SuiteSystem<Plugin>
{
    protected override string HarmonyId => "luca.infantryassault";
}
