using System.Reflection;

namespace HarmonyLib
{
    public class Harmony { public void Patch(MethodInfo method, HarmonyMethod prefix) { } }
    public class HarmonyMethod { public HarmonyMethod(MethodInfo method) { } }
}
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public struct Quaternion { public float x, y, z, w; }
    public class Transform { }
    public class GameObject { public Transform transform = new(); }
    public class Object
    {
        public static readonly List<(GameObject prefab, Vector3 pos, Quaternion rotation, Transform parent)> Spawned = new();
        public static int FailAt = -1;
        public static GameObject Instantiate(GameObject prefab, Vector3 pos, Quaternion rotation, Transform parent)
        {
            if (Spawned.Count == FailAt) throw new InvalidOperationException("fixture spawn failure");
            Spawned.Add((prefab, pos, rotation, parent));
            return prefab;
        }
    }
}
namespace Il2CppMenace.Tactical
{
    public class TacticalManager
    {
        public static TacticalManager Instance = new();
        public UnityEngine.GameObject Root = new();
        public static TacticalManager Get() => Instance;
        public UnityEngine.GameObject GetLevelRoot() => Root;
    }
}
namespace Il2CppMenace.Tactical.Mapgen.TerrainGen
{
    public struct TerrainPoint
    {
        public UnityEngine.Vector3 Position;
        public UnityEngine.Quaternion Orientation;
        public float Scale;
        public ushort AssetTypeID, Flags;
    }
}
namespace Il2CppMenace.Tactical.Mapgen
{
    public enum TerrainScatterAssetType { EntityTemplate, Feature, Prefab }
    public enum TerrainAssetTypeGameplay { ForestDense = 1000, ForestSparse, ForestSingle, GrassTall, Dunes }
    public class TerrainPointPropConfig
    {
        public TerrainScatterAssetType ScatterType = TerrainScatterAssetType.Prefab;
        public TerrainAssetTypeGameplay PointType = TerrainAssetTypeGameplay.ForestSingle;
        public UnityEngine.GameObject[] Prefabs = { new(), new() };
    }
    public class Generator { public TerrainPointPropConfig m_Config = new(); }
    public class Rng
    {
        public int Draws;
        public T NextEntry<T>(T[] array) => array[Draws++ % array.Length];
    }
    public class MapgenInput { public Rng Rng = new(); }
    public class MapBlueprint
    {
        public int Width = 64, Height = 64, HeightMapRes = 513;
        public float TotalWorldUnits = 688, BorderWorldUnits = 88;
        public List<TerrainGen.TerrainPoint> GameplayTerrainPoints = new();
        public readonly List<(float x, float z)> Samples = new();
        public float GetWorldHeightBilinear(float x, float z) { Samples.Add((x, z)); return x + 2 * z; }
    }
}
namespace LargerMaps
{
    public sealed partial class Plugin
    {
        private const int VanillaSize = 42;
        private const string ModName = "fixture";
        private static readonly Type _pointPropGeneratorType = typeof(Il2CppMenace.Tactical.Mapgen.Generator);
        private static readonly Logger _log = new();
        private static bool Enabled() => true;
        private static object GetMember(object target, string name) => target?.GetType().GetField(name)?.GetValue(target);
        private class Logger { public void Msg(string text) { } public void Warning(string text) { } }
        public static bool Run(object generator, Il2CppMenace.Tactical.Mapgen.MapgenInput input,
            Il2CppMenace.Tactical.Mapgen.MapBlueprint blueprint) => ForestPrefabReprojectionPrefix(generator, input, blueprint);
    }
}