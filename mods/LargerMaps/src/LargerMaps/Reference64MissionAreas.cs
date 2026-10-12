using System;
using System.Linq;
using System.Reflection;

namespace LargerMaps;

// Regression test only: 1.3.11 mission-area handlers copied without changes
// to their decisions or math. The sole changes are the three method names.
// On 64x64, the 1.4.10 area handlers are not installed. On >64, these are
// never installed and 1.4.10 code runs unmodified.
public sealed partial class Plugin
{
    private static void Reference64MissionAreaAddToListPrefix(object __instance, object[] __args, out int __state)
    {
        __state = -1;
        if (!Enabled() || __args == null || __args.Length == 0) return;

        bool enemyScope = _enemySpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool playerDeploymentScope = _playerDeploymentAreaScopeDepth > 0;
        bool objectiveTargetScope = _objectiveTargetSpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool evacuationScope = _evacuationAreaScopeDepth > 0;
        bool recoverDropoffScope = IsRecoverDropoffAreaConfig(__instance);
        if (!enemyScope && !playerDeploymentScope && !objectiveTargetScope && !evacuationScope && !recoverDropoffScope) return;

        __state = GetCollectionCount(__args[0]);
    }

    private static void Reference64MissionAreaAddToListPostfix(object __instance, object[] __args, int __state)
    {
        if (!Enabled() || __state < 0 || __args == null || __args.Length < 2) return;

        bool enemyScope = _enemySpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool playerDeploymentScope = _playerDeploymentAreaScopeDepth > 0;
        bool objectiveTargetScope = _objectiveTargetSpawnAreaScopeDepth > 0 && ExpandEnemySpawnAreas();
        bool evacuationScope = _evacuationAreaScopeDepth > 0;
        bool recoverDropoffScope = IsRecoverDropoffAreaConfig(__instance);
        if (!enemyScope && !playerDeploymentScope && !objectiveTargetScope && !evacuationScope && !recoverDropoffScope) return;

        object list = __args[0];
        object preview = __args[1];
        int count = GetCollectionCount(list);
        if (count <= __state) return;

        int mapWidth = ToInt(GetMember(preview, "SizeX"), TargetSize);
        int mapHeight = ToInt(GetMember(preview, "SizeZ"), TargetSize);
        if (mapWidth <= VanillaSize || mapHeight <= VanillaSize) return;

        string kind = evacuationScope ? "EVACUATION" : (recoverDropoffScope ? "RECOVER DROPOFF" : (playerDeploymentScope ? "PLAYER DEPLOYMENT" : (objectiveTargetScope ? "OBJECTIVE TARGET SPAWN" : "ENEMY SPAWN")));
        for (int i = __state; i < count; i++)
        {
            object rect = GetIndexedValue(list, i);
            if (rect == null) continue;

            int areaType = ToInt(GetMember(__instance, "AreaType"), -1);

            // PrimaryChunks / Chunk are not authored coordinates. AddToList has already
            // resolved them against the concrete generated setpiece/chunk position on the
            // 64x64 blueprint. Scaling their x/y from the map origin moves a deployment
            // zone away from the building it belongs to (e.g. Secure Repair Depot).
            // Keep those generated rectangles anchored in place. Evacuation is the one
            // exception: keep the resolved anchor, but guarantee a practical minimum 5x5
            // usable objective footprint around it.
            if (areaType == 0 || areaType == 1)
            {
                if (evacuationScope)
                {
                    if (!TryEnsureMinimumAreaRect(rect, mapWidth, mapHeight, MinimumEvacuationSize, out object expanded, out string evacBefore, out string evacAfter))
                    {
                        if (Debug())
                        {
                            int px = ToInt(GetMember(rect, "xMin") ?? GetMember(rect, "x"), 0);
                            int py = ToInt(GetMember(rect, "yMin") ?? GetMember(rect, "y"), 0);
                            int pw = ToInt(GetMember(rect, "width"), 0);
                            int ph = ToInt(GetMember(rect, "height"), 0);
                            _log.Msg($"{ModName}: EVACUATION AREA preserved anchor-aware type={areaType} ({px},{py},{pw},{ph}); already at least {MinimumEvacuationSize}x{MinimumEvacuationSize}.");
                        }
                        continue;
                    }

                    if (!TrySetIndexedValue(list, i, expanded))
                    {
                        if (Debug()) _log.Warning($"{ModName}: EVACUATION AREA could not replace anchor-aware rect at index {i}: {evacBefore}.");
                        continue;
                    }

                    _evacuationRectsScaled++;
                    if (_evacuationRectsScaled <= 12 || Debug())
                        _log.Msg($"{ModName}: EVACUATION AREA minimum footprint {evacBefore} -> {evacAfter}; anchor preserved, minimum={MinimumEvacuationSize}x{MinimumEvacuationSize}.");
                    continue;
                }

                if (Debug())
                {
                    int px = ToInt(GetMember(rect, "xMin") ?? GetMember(rect, "x"), 0);
                    int py = ToInt(GetMember(rect, "yMin") ?? GetMember(rect, "y"), 0);
                    int pw = ToInt(GetMember(rect, "width"), 0);
                    int ph = ToInt(GetMember(rect, "height"), 0);
                    _log.Msg($"{ModName}: {kind} AREA preserved anchor-aware type={areaType} ({px},{py},{pw},{ph}); chunk/setpiece position is already in enlarged-map coordinates.");
                }
                continue;
            }

            if (!Reference64TryScaleMissionAreaRect(rect, areaType, mapWidth, mapHeight, evacuationScope ? MinimumEvacuationSize : 1, out object scaled, out string before, out string after))
                continue;
            if (!TrySetIndexedValue(list, i, scaled))
            {
                if (Debug()) _log.Warning($"{ModName}: {kind} AREA could not replace generated rect at index {i}: {before}.");
                continue;
            }

            int scaledCount;
            if (evacuationScope)
            {
                _evacuationRectsScaled++;
                scaledCount = _evacuationRectsScaled;
            }
            else if (recoverDropoffScope)
            {
                _recoverDropoffRectsScaled++;
                scaledCount = _recoverDropoffRectsScaled;
            }
            else if (playerDeploymentScope)
            {
                _playerDeploymentRectsScaled++;
                scaledCount = _playerDeploymentRectsScaled;
            }
            else if (objectiveTargetScope)
            {
                _objectiveTargetSpawnRectsScaled++;
                scaledCount = _objectiveTargetSpawnRectsScaled;
            }
            else
            {
                _enemySpawnRectsScaled++;
                scaledCount = _enemySpawnRectsScaled;
            }

            if ((evacuationScope && scaledCount <= 12) || (Debug() && scaledCount <= 40))
                _log.Msg($"{ModName}: {kind} AREA scaled {before} -> {after} within {mapWidth}x{mapHeight} map.");
        }
    }

    private static bool Reference64TryScaleMissionAreaRect(object rect, int areaType, int mapWidth, int mapHeight, int minimumSize, out object scaled, out string before, out string after)
    {
        scaled = null;
        before = "<unknown>";
        after = "<unknown>";
        try
        {
            int x = ToInt(GetMember(rect, "xMin") ?? GetMember(rect, "x"), 0);
            int y = ToInt(GetMember(rect, "yMin") ?? GetMember(rect, "y"), 0);
            int w = ToInt(GetMember(rect, "width"), 0);
            int h = ToInt(GetMember(rect, "height"), 0);
            if (w <= 0 || h <= 0) return false;

            before = $"type={areaType} ({x},{y},{w},{h})";

            float scaleX = mapWidth / (float)VanillaSize;
            float scaleY = mapHeight / (float)VanillaSize;

            // MissionAreaConfig rectangles are authored in the vanilla 42x42 coordinate space.
            // Scale both their position and footprint into the actual enlarged map. This also
            // preserves edge margins correctly; e.g. a north-edge band keeps the same relative
            // one-tile vanilla inset instead of being shoved to an arbitrary hard edge.
            int newX = (int)Math.Round(x * scaleX);
            int newY = (int)Math.Round(y * scaleY);
            int scaledW = Math.Max(1, (int)Math.Round(w * scaleX));
            int scaledH = Math.Max(1, (int)Math.Round(h * scaleY));
            int newW = Math.Max(Math.Max(1, minimumSize), scaledW);
            int newH = Math.Max(Math.Max(1, minimumSize), scaledH);

            // If the evacuation minimum makes the rectangle larger than pure proportional
            // scaling would, grow it around the same approximate center instead of only
            // extending to the right/bottom.
            if (newW > scaledW) newX -= (newW - scaledW) / 2;
            if (newH > scaledH) newY -= (newH - scaledH) / 2;

            newW = Math.Min(mapWidth, newW);
            newH = Math.Min(mapHeight, newH);
            newX = Math.Max(0, Math.Min(newX, mapWidth - newW));
            newY = Math.Max(0, Math.Min(newY, mapHeight - newH));

            Type rectType = rect.GetType();
            ConstructorInfo ctor = rectType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 4 && p.All(v => v.ParameterType == typeof(int));
                });
            if (ctor == null) return false;

            scaled = ctor.Invoke(new object[] { newX, newY, newW, newH });
            after = $"({newX},{newY},{newW},{newH}) scale={scaleX:0.###}x{scaleY:0.###}";
            return true;
        }
        catch (Exception ex)
        {
            if (Debug()) _log.Warning($"{ModName}: generated-area scaling failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

}
