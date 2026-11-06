using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Mapgen;
using Il2CppMenace.Tactical.Mapgen.TerrainGen;
using LargerMaps;
using UnityEngine;
using Object = UnityEngine.Object;

int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
TerrainPoint Point(ushort id, float x, float z) => new()
{
    AssetTypeID = id, Flags = 0, Scale = 3,
    Position = new Vector3 { x = x, y = 999, z = z },
    Orientation = new Quaternion { y = 0.5f, w = 0.5f }
};
void Reset() { Object.Spawned.Clear(); Object.FailAt = -1; TacticalManager.Instance = new(); }

foreach (var kind in new[] { TerrainAssetTypeGameplay.ForestDense, TerrainAssetTypeGameplay.ForestSparse,
    TerrainAssetTypeGameplay.ForestSingle })
foreach (int size in new[] { 64, 80, 96 })
{
    Reset();
    var generator = new Generator(); generator.m_Config.PointType = kind;
    var input = new MapgenInput();
    var map = new MapBlueprint { Width = size, Height = size, TotalWorldUnits = size * 8 + 176 };
    map.GameplayTerrainPoints.Add(Point((ushort)kind, 10, 24));
    map.GameplayTerrainPoints.Add(Point(2000, 20, 30));
    map.GameplayTerrainPoints.Add(Point((ushort)kind, size * 8 - 1, size * 8 - 1));
    Check(!Plugin.Run(generator, input, map), "native inlined body must be skipped");
    Check(Object.Spawned.Count == 2 && input.Rng.Draws == 2, "one draw / spawn per matching point");
    var first = Object.Spawned[0];
    float expectedX = 98 * 512f / map.TotalWorldUnits, expectedZ = 112 * 512f / map.TotalWorldUnits;
    Check(Math.Abs(map.Samples[0].x - expectedX) < 0.001f, "border-aware scaled X");
    Check(Math.Abs(map.Samples[0].z - expectedZ) < 0.001f, "border-aware scaled Z");
    Check(Math.Abs(first.pos.y - expectedX - 2 * expectedZ) < 0.001f, "terrain Y, not old point Y");
    Check(first.pos.x == 10 && first.pos.z == 24 && first.rotation.y == 0.5f, "XZ / orientation retained");
    Check(ReferenceEquals(first.parent, TacticalManager.Instance.Root.transform), "native level parent");
    Check(ReferenceEquals(first.prefab, generator.m_Config.Prefabs[0]) &&
        ReferenceEquals(Object.Spawned[1].prefab, generator.m_Config.Prefabs[1]), "original RNG / prefab order");
    Check(map.GameplayTerrainPoints[0].Position.y == 999 && map.GameplayTerrainPoints[0].Scale == 3,
        "blueprint source and authored transforms untouched");
    Check(map.Samples.All(s => s.x >= 0 && s.z >= 0 && s.x <= 512 && s.z <= 512), "far-edge samples in bounds");
}

Reset();
var gen = new Generator(); var rngInput = new MapgenInput(); var blueprint = new MapBlueprint();
blueprint.GameplayTerrainPoints.Add(Point(1002, -10000, 10000));
Check(!Plugin.Run(gen, rngInput, blueprint) && blueprint.Samples[0] == (0f, 512f), "border clamp");
foreach (var dimensions in new[] { (42, 42), (32, 42) })
{
    Reset(); blueprint.Width = dimensions.Item1; blueprint.Height = dimensions.Item2;
    Check(Plugin.Run(gen, rngInput, blueprint) && Object.Spawned.Count == 0, "native map untouched");
}
blueprint.Width = 64; blueprint.Height = 64;
foreach (var kind in new[] { TerrainAssetTypeGameplay.GrassTall, TerrainAssetTypeGameplay.Dunes })
{
    gen.m_Config.PointType = kind;
    Check(Plugin.Run(gen, rngInput, blueprint), "non-forest prefab untouched");
}
gen.m_Config.PointType = TerrainAssetTypeGameplay.ForestSingle;
gen.m_Config.ScatterType = TerrainScatterAssetType.Feature;
Check(Plugin.Run(gen, rngInput, blueprint), "feature branch untouched");
gen.m_Config.ScatterType = TerrainScatterAssetType.Prefab;
var prefabs = gen.m_Config.Prefabs; gen.m_Config.Prefabs = Array.Empty<GameObject>();
Check(Plugin.Run(gen, rngInput, blueprint), "native missing-prefab warning retained");
gen.m_Config.Prefabs = prefabs;
Reset();
TacticalManager.Instance = null;
int before = rngInput.Rng.Draws;
Check(!Plugin.Run(gen, rngInput, blueprint) && rngInput.Rng.Draws == before, "missing parent safe before RNG");
Reset(); blueprint.TotalWorldUnits = float.NaN;
Check(!Plugin.Run(gen, rngInput, blueprint) && Object.Spawned.Count == 0, "invalid dimensions safe");
Reset(); blueprint.TotalWorldUnits = 688;
blueprint.GameplayTerrainPoints.Add(Point(1002, 20, 20));
Object.FailAt = 1; before = rngInput.Rng.Draws;
Check(!Plugin.Run(gen, rngInput, blueprint), "partial failure must not rerun native");
Check(Object.Spawned.Count == 1 && rngInput.Rng.Draws == before + 2, "no duplicate spawn / RNG replay");
Check(Plugin.Run(gen, rngInput, null) && Plugin.Run(null, rngInput, blueprint), "missing blueprint/config fall through");
Console.WriteLine($"Forest prefab production-hook checks passed: {checks}");