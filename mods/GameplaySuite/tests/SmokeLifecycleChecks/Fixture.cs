namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public class GameObject { public bool activeInHierarchy = true; }
}
namespace Il2CppMenace.Tactical.TileEffects
{
    public class TileEffectTemplate
    {
        public bool Smoke = true;
        public int RemoveAfterRounds = 4;
        public bool HasTimeLimit = true;
        public UnityEngine.GameObject? SpawnObjectOrEffect = new();
        public int AmountOfPrefabsSpawned = 1;
    }
    public class Tile
    {
        public readonly List<TileEffectHandler> Effects = new();
        public List<TileEffectHandler> GetEffects() => Effects;
    }
    public class TileEffectHandler
    {
        public IntPtr Pointer;
        public TileEffectTemplate Template = new();
        public Tile? Tile = new();
        public UnityEngine.GameObject? m_WorldObject;
        public float Delay;
        public int Lifetime;
        public bool Throw;
        public TileEffectTemplate GetTemplate() => Throw ? throw new Exception("fixture") : Template;
        public Tile? GetTile() => Throw ? throw new Exception("fixture") : Tile;
        public float GetDelay() => Delay;
        public int GetLifetime() => Lifetime;
    }
}
namespace SmokeOverhaul
{
    public static class RuntimeSettings
    {
        public static bool Enabled = true;
        public static bool DebugLogging = true;
    }
    public sealed partial class Plugin
    {
        internal static bool AiSmokeOnMainThread = true;
        internal static readonly List<string> Messages = new();
        private static bool IsSmoke(Il2CppMenace.Tactical.TileEffects.TileEffectTemplate template) => template.Smoke;
        private static string GetTemplateId(Il2CppMenace.Tactical.TileEffects.TileEffectTemplate template) => "tile_effect.smoke_grenade";
        internal static void DebugMsg(string message) => Messages.Add(message);
        internal static void Tick(float now) => UpdateSmokeLifecycleDiagnostics(now);
        internal static void Reset() { ResetSmokeLifecycleDiagnostics(); Messages.Clear(); }
        internal static int Pending => SmokeObservations.Count;
    }
    internal static partial class Patches
    {
        internal static int Errors;
        internal static void LogTemplateErrorOnce(Exception ex) => Errors++;
    }
}