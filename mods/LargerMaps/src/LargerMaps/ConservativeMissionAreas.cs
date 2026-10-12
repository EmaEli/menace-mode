using System;

namespace LargerMaps;

public sealed partial class Plugin
{
    // Extended-map compatibility strategy based on the fast 1.3.11 64x64
    // test. This runs only during the original, narrowly scoped mission area
    // generation calls; it never hooks AI evaluation or objective Init methods.
    // Do not convert all objective/AI control zones into enlarged search areas.
    private static void ConservativeLargeMissionAreaAddToListPostfix(
        object __instance, object[] __args, int __state)
    {
        if (!Enabled() || TargetSize <= ZoneReferenceSize ||
            __state < 0 || __args == null || __args.Length < 2) return;

        bool enemyScope = _enemySpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool deploymentScope = _playerDeploymentAreaScopeDepth > 0;
        bool targetScope = _objectiveTargetSpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool evacuationScope = _evacuationAreaScopeDepth > 0;
        bool recoverScope = IsRecoverDropoffAreaConfig(__instance);
        if (!enemyScope && !deploymentScope && !targetScope &&
            !evacuationScope && !recoverScope) return;

        object list = __args[0];
        object preview = __args[1];
        int count = GetCollectionCount(list);
        if (count <= __state) return;

        int mapW = ToInt(GetMember(preview, "SizeX"), TargetSize);
        int mapH = ToInt(GetMember(preview, "SizeZ"), TargetSize);
        if (mapW <= ZoneReferenceSize || mapH <= ZoneReferenceSize) return;

        int areaType = ToInt(GetMember(__instance, "AreaType"), -1);
        for (int i = __state; i < count; i++)
        {
            object rect = GetIndexedValue(list, i);
            if (rect == null) continue;
            object replacement = null;
            string before = string.Empty;
            string after = string.Empty;
            bool changed = false;

            if (areaType == 0 || areaType == 1)
            {
                // PrimaryChunks and Chunk areas are already resolved to the
                // real setpiece's coordinates by native map generation. Moving
                // or scaling them again can disconnect spawns from objectives.
                // Only retain the 1.3.11 evacuation minimum and prevent
                // malformed actor-spawn ranges on extended maps.
                if (evacuationScope)
                    changed = TryEnsureMinimumAreaRect(rect, mapW, mapH,
                        MinimumEvacuationSize, out replacement, out before, out after);
                else if (enemyScope || deploymentScope || targetScope || recoverScope)
                    changed = TryNormalizeSpawnRect(rect, mapW, mapH, 1,
                        out replacement, out before, out after);
            }
            else if (areaType >= 2 && areaType <= 10 && areaType != 6)
            {
                // MissionTemplate.GetBounds native patch has already extended
                // the north/south/east/west span to the selected map size.
                // Only adjust its short border thickness, never that span.
                changed = TryScaleReferenceBorderRect(rect, areaType, mapW,
                    mapH, false, out replacement, out before, out after);
            }
            else if (areaType == 6)
            {
                // Explicitly-authored Rect zones are still in the vanilla
                // 42x42 coordinate system, as with the old 1.3.11 handler.
                // Keep its positive-size convention; unlike 1.4.10, do not
                // convert signed objective/AI-zone deltas into giant areas.
                changed = Reference64TryScaleMissionAreaRect(rect, areaType,
                    mapW, mapH, evacuationScope ? MinimumEvacuationSize : 1,
                    out replacement, out before, out after);
                if (!changed && !evacuationScope)
                    changed = TryNormalizeSpawnRect(rect, mapW, mapH, 1,
                        out replacement, out before, out after);
            }
            if (!changed || replacement == null) continue;
            if (!TrySetIndexedValue(list, i, replacement))
            {
                if (Debug())
                    _log.Warning($"{ModName}: conservative area replacement failed: {before}.");
                continue;
            }

            if (evacuationScope) _evacuationRectsScaled++;
            else if (recoverScope) _recoverDropoffRectsScaled++;
            else if (deploymentScope) _playerDeploymentRectsScaled++;
            else if (targetScope) _objectiveTargetSpawnRectsScaled++;
            else _enemySpawnRectsScaled++;

            if (Debug())
                _log.Msg($"{ModName}: conservative area type={areaType}: {before} -> {after}.");
        }
    }
}
