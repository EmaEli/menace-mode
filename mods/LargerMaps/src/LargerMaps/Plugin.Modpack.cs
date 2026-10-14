#if !JIANGYU
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

namespace LargerMaps;

// Legacy Menace.ModpackLoader host binding. Not compiled into the Jiangyu package.
public sealed partial class Plugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        var log = new ModLog(m => logger.Msg(m), m => logger.Warning(m), m => logger.Error(m));
        Initialize(log, harmony, GameState.RunDelayed, Version);
    }
}
#endif
