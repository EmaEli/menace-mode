using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Tactical.Mapgen;
using Il2CppMenace.Tactical.Mapgen.TerrainGen;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace LargerMaps;

/// <summary>
/// MEN-008: ScatterStamps writes each stamp texture 1:1 into the fixed 513x513
/// heightmap / 1024x1024 alphamaps, so a landform footprint grows with the world
/// size of the map while the stamp prefab (water plane, rocks) keeps its authored
/// size. Shrink the stamp textures by the world-unit ratio for the duration of
/// SpawnStampsFromList so the basin matches the prefab again.
/// </summary>
public sealed partial class Plugin
{
    private const float MinimumStampFootprintScale = 0.25f;
    private const int MinimumScaledStampPixels = 8;

    private sealed class ScaledStampTextures
    {
        public float Scale;
        public Texture2D HeightMap;
        public Texture2D BlendMask;
        public Il2CppReferenceArray<Texture2D> AlphaMaps;
    }

    private sealed class SwappedStamp
    {
        public StampTemplate Template;
        public Texture2D HeightMap;
        public Texture2D BlendMask;
        public Il2CppReferenceArray<Texture2D> AlphaMaps;
    }

    private static bool _preserveStampFootprint;
    private static bool _stampFootprintLogged;
    private static bool _stampFootprintWarned;
    private static readonly Dictionary<IntPtr, ScaledStampTextures> _scaledStamps = new();
    private static readonly List<SwappedStamp> _swappedStamps = new();

    private static void PatchTerrainStampFootprint(HarmonyLib.Harmony harmony)
    {
        _preserveStampFootprint = TargetSize > VanillaSize &&
                                  ReadBool("PreserveTerrainStampFootprint", true);
        if (!_preserveStampFootprint) return;

        Type scatterStamps = FindType("Il2CppMenace.Tactical.Mapgen.TerrainGen.ScatterStamps") ??
                             FindType("Menace.Tactical.Mapgen.TerrainGen.ScatterStamps");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo method = scatterStamps?.GetMethods(flags).FirstOrDefault(m =>
        {
            if (m.Name != "SpawnStampsFromList" || m.ReturnType != typeof(void)) return false;
            ParameterInfo[] parameters = m.GetParameters();
            return parameters.Length == 2 && parameters[1].ParameterType == typeof(int);
        });
        if (method == null)
        {
            _preserveStampFootprint = false;
            _log.Warning($"{ModName}: stamp footprint fix unavailable (ScatterStamps.SpawnStampsFromList/2 not found); vanilla generation retained.");
            return;
        }

        try
        {
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(StampFootprintPrefix),
                    BindingFlags.Static | BindingFlags.NonPublic)),
                postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(StampFootprintPostfix),
                    BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            _preserveStampFootprint = false;
            _log.Warning($"{ModName}: stamp footprint fix skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void StampFootprintPrefix(object __instance,
        Il2CppSystem.Collections.Generic.List<StampCollectionItem> __0)
    {
        if (!_preserveStampFootprint) return;
        // A failed native call would skip the postfix; never let a swap outlive one placement pass.
        RestoreStampTextures();
        if (__instance == null || __0 == null) return;

        try
        {
            float scale = ComputeStampFootprintScale(GetMember(__instance, "m_Blueprint") as MapBlueprint);
            if (scale >= 0.999f) return;

            int count = __0.Count;
            for (int i = 0; i < count; i++)
            {
                StampTemplate template = __0[i]?.Stamp;
                if (template == null || _swappedStamps.Any(s => s.Template.Pointer == template.Pointer)) continue;

                ScaledStampTextures scaled = GetScaledStampTextures(template, scale);
                if (scaled == null) continue;

                _swappedStamps.Add(new SwappedStamp
                {
                    Template = template,
                    HeightMap = template.HeightMap,
                    BlendMask = template.BlendMask,
                    AlphaMaps = template.AlphaMaps
                });
                if (scaled.HeightMap != null) template.HeightMap = scaled.HeightMap;
                if (scaled.BlendMask != null) template.BlendMask = scaled.BlendMask;
                if (scaled.AlphaMaps != null) template.AlphaMaps = scaled.AlphaMaps;
            }

            if (!_stampFootprintLogged && _swappedStamps.Count > 0)
            {
                _stampFootprintLogged = true;
                _log.Msg($"{ModName}: terrain stamp footprint kept at vanilla world size (texture scale {scale:0.###}, {_swappedStamps.Count} stamp templates).");
            }
        }
        catch (Exception ex)
        {
            RestoreStampTextures();
            WarnStampFootprintOnce(ex);
        }
    }

    private static void StampFootprintPostfix() => RestoreStampTextures();

    private static void RestoreStampTextures()
    {
        if (_swappedStamps.Count == 0) return;
        foreach (SwappedStamp swapped in _swappedStamps)
        {
            try
            {
                swapped.Template.HeightMap = swapped.HeightMap;
                swapped.Template.BlendMask = swapped.BlendMask;
                swapped.Template.AlphaMaps = swapped.AlphaMaps;
            }
            catch (Exception ex)
            {
                WarnStampFootprintOnce(ex);
            }
        }
        _swappedStamps.Clear();
    }

    private static void WarnStampFootprintOnce(Exception ex)
    {
        if (_stampFootprintWarned) return;
        _stampFootprintWarned = true;
        _log.Warning($"{ModName}: stamp footprint fix could not process a stamp: {ex.GetType().Name}: {ex.Message}");
    }

    private static float ComputeStampFootprintScale(MapBlueprint blueprint)
    {
        if (blueprint == null) return 1f;
        return ComputeStampFootprintScale(blueprint.Width, blueprint.TotalWorldUnits, blueprint.BorderWorldUnits);
    }

    // Pure: world size of one heightmap pixel on a vanilla-width map divided by the
    // same on the enlarged map (same 513 resolution, fixed border, tile size unchanged).
    private static float ComputeStampFootprintScale(int width, float totalWorldUnits, float borderWorldUnits)
    {
        if (width <= VanillaSize || totalWorldUnits <= 0.001f || borderWorldUnits < 0f) return 1f;
        float playable = totalWorldUnits - 2f * borderWorldUnits;
        if (playable <= 0.001f) return 1f;
        float vanillaTotal = 2f * borderWorldUnits + playable * VanillaSize / width;
        return Math.Clamp(vanillaTotal / totalWorldUnits, MinimumStampFootprintScale, 1f);
    }

    // Scaled copies are cached per template for the session (map size is fixed at startup).
    // All-or-nothing: the native pass indexes mask/alphamaps relative to the heightmap size.
    private static ScaledStampTextures GetScaledStampTextures(StampTemplate template, float scale)
    {
        IntPtr key = template.Pointer;
        if (_scaledStamps.TryGetValue(key, out ScaledStampTextures cached))
        {
            if (Math.Abs(cached.Scale - scale) < 0.001f) return cached;
            DestroyScaledStampTextures(cached);
            _scaledStamps.Remove(key);
        }

        var result = new ScaledStampTextures { Scale = scale };
        bool ok = TryDownscaleStampTexture(template.HeightMap, scale, out result.HeightMap) &&
                  TryDownscaleStampTexture(template.BlendMask, scale, out result.BlendMask);

        Il2CppReferenceArray<Texture2D> alphaMaps = ok ? template.AlphaMaps : null;
        if (ok && alphaMaps != null && alphaMaps.Length > 0)
        {
            result.AlphaMaps = new Il2CppReferenceArray<Texture2D>(alphaMaps.Length);
            for (int i = 0; i < alphaMaps.Length && ok; i++)
            {
                ok = TryDownscaleStampTexture(alphaMaps[i], scale, out Texture2D scaledAlpha);
                result.AlphaMaps[i] = scaledAlpha;
            }
        }

        if (!ok || (result.HeightMap == null && result.BlendMask == null && result.AlphaMaps == null))
        {
            DestroyScaledStampTextures(result);
            return null;
        }
        _scaledStamps[key] = result;
        return result;
    }

    private static void DestroyScaledStampTextures(ScaledStampTextures set)
    {
        if (set == null) return;
        if (set.HeightMap != null) UnityEngine.Object.Destroy(set.HeightMap);
        if (set.BlendMask != null) UnityEngine.Object.Destroy(set.BlendMask);
        if (set.AlphaMaps == null) return;
        for (int i = 0; i < set.AlphaMaps.Length; i++)
            if (set.AlphaMaps[i] != null) UnityEngine.Object.Destroy(set.AlphaMaps[i]);
    }

    // Null result with true => keep the original texture (missing or already at the
    // minimum size). Non-null source that cannot be scaled => false.
    private static bool TryDownscaleStampTexture(Texture2D source, float scale, out Texture2D scaled)
    {
        scaled = null;
        if (source == null) return true;
        try
        {
            int width = Math.Max(MinimumScaledStampPixels, (int)Math.Round(source.width * scale));
            int height = Math.Max(MinimumScaledStampPixels, (int)Math.Round(source.height * scale));
            if (width >= source.width || height >= source.height) return true;

            // Float formats keep the full heightmap range; reads stay format-agnostic
            // for GetPixel / GetPixelBilinear / GetPixels on the native side.
            uint components = GraphicsFormatUtility.GetComponentCount(source.graphicsFormat);
            TextureFormat format = components <= 1 ? TextureFormat.RFloat
                : components == 2 ? TextureFormat.RGFloat : TextureFormat.RGBAFloat;
            scaled = new Texture2D(width, height, format, false, true)
            {
                name = source.name + " (Larger Maps footprint)",
                filterMode = source.filterMode,
                wrapMode = source.wrapMode,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (source.isReadable) BoxDownsample(source, scaled);
            else BlitDownsample(source, scaled);
            scaled.Apply(false, false);
            return true;
        }
        catch (Exception ex)
        {
            if (scaled != null) UnityEngine.Object.Destroy(scaled);
            scaled = null;
            WarnStampFootprintOnce(ex);
            return false;
        }
    }

    // CPU area average: every destination texel averages the source block it covers,
    // which avoids the aliasing a 4-tap bilinear blit would leave on basin rims.
    private static void BoxDownsample(Texture2D source, Texture2D target)
    {
        Il2CppStructArray<Color> src = source.GetPixels();
        int sw = source.width, sh = source.height, tw = target.width, th = target.height;
        var dst = new Color[tw * th];
        for (int ty = 0; ty < th; ty++)
        {
            int y0 = ty * sh / th, y1 = Math.Max(y0 + 1, (ty + 1) * sh / th);
            for (int tx = 0; tx < tw; tx++)
            {
                int x0 = tx * sw / tw, x1 = Math.Max(x0 + 1, (tx + 1) * sw / tw);
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        Color c = src[y * sw + x];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                    }
                float inv = 1f / ((y1 - y0) * (x1 - x0));
                dst[ty * tw + tx] = new Color(r * inv, g * inv, b * inv, a * inv);
            }
        }
        target.SetPixels(dst);
    }

    private static void BlitDownsample(Texture2D source, Texture2D target)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temp = RenderTexture.GetTemporary(target.width, target.height, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        try
        {
            Graphics.Blit(source, temp);
            RenderTexture.active = temp;
            target.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);
        }
    }
}
