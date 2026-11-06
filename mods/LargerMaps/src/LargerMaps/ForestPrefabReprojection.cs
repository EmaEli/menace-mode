using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Mapgen;
using Il2CppMenace.Tactical.Mapgen.TerrainGen;
using UnityEngine;

namespace LargerMaps;

public sealed partial class Plugin
{
    private static bool _forestPrefabFailureLogged;

    private static void PatchForestPrefabReprojection(HarmonyLib.Harmony harmony)
    {
        MethodInfo method = _pointPropGeneratorType?.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "SpawnPrefabs" && m.ReturnType == typeof(void) &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[] { typeof(MapgenInput), typeof(MapBlueprint) }));
        if (method == null)
        {
            _log.Warning($"{ModName}: forest prefab height correction unavailable (PointPropGenerator.SpawnPrefabs not found).");
            return;
        }
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(Plugin).GetMethod(
            nameof(ForestPrefabReprojectionPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        _log.Msg($"{ModName}: forest prefab height correction active (includes inlined native reprojection).");
    }

    private static bool ForestPrefabReprojectionPrefix(object __instance, MapgenInput __0, MapBlueprint __1)
    {
        if (!Enabled() || __1 == null || (__1.Width <= VanillaSize && __1.Height <= VanillaSize))
            return true;

        TerrainPointPropConfig config = GetMember(__instance, "m_Config") as TerrainPointPropConfig;
        if (config == null || config.ScatterType != TerrainScatterAssetType.Prefab ||
            (config.PointType != TerrainAssetTypeGameplay.ForestDense &&
             config.PointType != TerrainAssetTypeGameplay.ForestSparse &&
             config.PointType != TerrainAssetTypeGameplay.ForestSingle))
            return true; // Never reproject lakes, structures, dunes or other prefab types.

        var prefabs = config.Prefabs;
        if (prefabs == null || prefabs.Length == 0) return true; // Retain native warning.

        try
        {
            var points = __1.GameplayTerrainPoints;
            var rng = __0?.Rng;
            var parent = TacticalManager.Get()?.GetLevelRoot()?.transform;
            float totalWorld = __1.TotalWorldUnits;
            int resolution = __1.HeightMapRes;
            if (points == null || rng == null || parent == null ||
                !float.IsFinite(totalWorld) || totalWorld <= 0.001f || resolution < 2)
                throw new InvalidOperationException("forest prefab inputs unavailable or invalid");

            // The 0.7.18 native SpawnPrefabs body inlines ReprojectGameplayPoint:
            // patching the helper never intercepts its unscaled (world + border) samples.
            // Mirror ONLY its forest-prefab loop; do not rescale shared height queries.
            float scale = (resolution - 1f) / totalWorld;
            float border = __1.BorderWorldUnits;
            int count = points.Count;
            for (int i = 0; i < count; i++)
            {
                TerrainPoint point = points[i];
                if (point.AssetTypeID != (ushort)config.PointType) continue;
                Vector3 position = point.Position;
                float sampleX = Math.Clamp((position.x + border) * scale, 0f, resolution - 1f);
                float sampleZ = Math.Clamp((position.z + border) * scale, 0f, resolution - 1f);
                position.y = __1.GetWorldHeightBilinear(sampleX, sampleZ);
                // Native consumes exactly one NextEntry per matching point and uses
                // the authored prefab scale, point orientation and LevelRoot parent.
                GameObject prefab = rng.NextEntry<GameObject>(prefabs);
                UnityEngine.Object.Instantiate(prefab, position, point.Orientation, parent);
            }
        }
        catch (Exception ex)
        {
            if (!_forestPrefabFailureLogged)
            {
                _forestPrefabFailureLogged = true;
                _log.Warning($"{ModName}: forest prefab placement stopped safely: {ex.GetType().Name}: {ex.Message}");
            }
            // Never rerun vanilla after a partial spawn (duplicates / extra RNG draws).
            // Its unscaled native sampling is unsafe on enlarged maps in any case.
        }
        return false;
    }
}