#if !JIANGYU
using MelonLoader;
using Menace.ModpackLoader;
using MenaceModSuite.Shared;

namespace CoordinatedFire;

// Legacy Menace.ModpackLoader host binding. Not compiled into the Jiangyu package.
public sealed partial class Plugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, global::HarmonyLib.Harmony harmony)
    {
        var log = new ModLog(m => logger.Msg(m), m => logger.Warning(m), m => logger.Error(m));
        Initialize(log, harmony);
    }
}
#endif
