using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Conversations;
using Il2CppMenace.Strategy;
using UnityEngine;

namespace SquaddieFieldPromotions;

/// <summary>
/// Standalone portrait and badge sources, extracted from the author's Generic Low Level
/// Squad Leaders 4.0.2. The balaclava male/female families remain the default; three optional
/// profiles can override the six portrait fields for the first matching record in each save.
/// All artwork ships with THIS Jiangyu mod.
///
/// Non-Jiangyu raw artwork is copied AFTER CLI deploy by Build-and-Deploy.cmd.
/// No Custom Leaders loader, original generic mod, or Menace Modpack Loader is needed.
/// Images are decoded once on F9 and held for the strategy session.
/// </summary>
internal sealed class StandalonePortraitAssets
{
    internal static readonly string[] PortraitFileNames =
    {
        "slot.png", "slot_inactive.png", "bark.png", "stand_right.png", "stand_left.png",
        "stand_right_inactive.png"
    };
    private static readonly string[] BadgeFileNames =
    {
        "badge.png", "badge_mini.png", "badge_dragged.png", "badge_unit.png", "slot_badge.png", "big_badge.png"
    };
    internal static readonly string[] FileNames = PortraitFileNames.Concat(BadgeFileNames).ToArray();

    private readonly Dictionary<string, Sprite> _sprites;

    private StandalonePortraitAssets(Dictionary<string, Sprite> sprites) => _sprites = sprites;

    internal static bool ArePresent(string modFolder, string gender)
    {
        string root = Path.Combine(modFolder, "runtime_assets", gender);
        return FileNames.All(name => File.Exists(Path.Combine(root, name)));
    }

    internal static bool ArePortraitProfilePresent(string modFolder, string profile)
    {
        if (!PromotionPortraitAssignment.IsKnownProfile(profile)) return false;
        string root = Path.Combine(modFolder, "runtime_assets", profile);
        return PortraitFileNames.All(name => File.Exists(Path.Combine(root, name)));
    }

    internal static bool ArePortraitProfilesPresent(string modFolder) =>
        PromotionPortraitAssignment.AllProfiles.All(profile => ArePortraitProfilePresent(modFolder, profile));

    internal static StandalonePortraitAssets Load(string modFolder, string gender,
        List<UnityEngine.Object> pinned)
    {
        if (gender != "male" && gender != "female")
            throw new ArgumentException("Unknown portrait family", nameof(gender));
        if (!ArePresent(modFolder, gender))
            throw new InvalidOperationException("Standalone " + gender +
                " portrait files missing from deployed mod. Use Build-and-Deploy.cmd; " +
                "the CLI deploy step alone does not preserve raw runtime_assets.");

        string folder = Path.Combine(modFolder, "runtime_assets", gender);
        return LoadFiles(folder, gender, FileNames, pinned);
    }

    internal static StandalonePortraitAssets LoadPortraitProfile(string modFolder, string profile,
        List<UnityEngine.Object> pinned)
    {
        if (!PromotionPortraitAssignment.IsKnownProfile(profile))
            throw new ArgumentException("Unknown portrait profile", nameof(profile));
        if (!ArePortraitProfilePresent(modFolder, profile))
            throw new InvalidOperationException("Standalone " + profile +
                " portrait files missing from deployed mod. Use Build-and-Deploy.cmd; " +
                "the CLI deploy step alone does not preserve raw runtime_assets.");

        string folder = Path.Combine(modFolder, "runtime_assets", profile);
        return LoadFiles(folder, profile, PortraitFileNames, pinned);
    }

    private static StandalonePortraitAssets LoadFiles(string folder, string assetFamily,
        IEnumerable<string> fileNames, List<UnityEngine.Object> pinned)
    {
        var loaded = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (string name in fileNames)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            byte[] png = File.ReadAllBytes(Path.Combine(folder, name));
            if (png.Length < 8 || !DecodePng(texture, png))
                throw new InvalidOperationException("PNG could not be decoded by Unity IL2CPP: " + assetFamily + "/" + name);
            texture.name = "sfp.embedded." + assetFamily + "." + name;
            texture.filterMode = FilterMode.Bilinear;
            // Runtime-created assets are otherwise destroyed by the scene-change
            // Resources.UnloadUnusedAssets (rehydration runs before Strategy loads).
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Sprite sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            if (sprite == null)
                throw new InvalidOperationException("Sprite.Create failed: " + assetFamily + "/" + name);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            pinned.Add(texture);
            pinned.Add(sprite);
            loaded[name] = sprite;
        }
        return new StandalonePortraitAssets(loaded);
    }

    // ImageConversion.LoadImage differs between managed Unity and IL2CPP proxy
    // assemblies. Resolve its available 2/3-argument native bridge at runtime
    // instead of assuming a managed byte[] overload works on MENACE 0.7.15.
    private static bool DecodePng(Texture2D texture, byte[] png)
    {
        Type imageConversion = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.ImageConversion", false))
            .FirstOrDefault(t => t != null);
        if (imageConversion == null)
            imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
        if (imageConversion == null) return false;
        var nativeBytes = new Il2CppStructArray<byte>(png);
        foreach (MethodInfo method in imageConversion.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "LoadImage") continue;
            var ps = method.GetParameters();
            if (ps.Length < 2 || ps.Length > 3 || !ps[0].ParameterType.IsInstanceOfType(texture))
                continue;
            object bytesArg = ps[1].ParameterType.IsInstanceOfType(nativeBytes) ? nativeBytes :
                ps[1].ParameterType.IsInstanceOfType(png) ? png : null;
            if (bytesArg == null) continue;
            try
            {
                object[] args = ps.Length == 3 ? new object[] { texture, bytesArg, false } :
                                                    new object[] { texture, bytesArg };
                object result = method.Invoke(null, args);
                if ((result is bool ok && ok) || (result == null && texture.width > 2))
                    return texture.width > 2 && texture.height > 2;
            }
            catch { /* Try the alternate Unity proxy overload, if present. */ }
        }
        return false;
    }

    internal bool IsAlive
    {
        get
        {
            try
            {
                foreach (Sprite sprite in _sprites.Values)
                    if (sprite == null || sprite.texture == null) return false;
                return true;
            }
            catch { return false; }
        }
    }

    internal void ApplyTo(UnitLeaderTemplate leader, SpeakerTemplate speaker)
    {
        leader.Slot = _sprites["slot.png"];
        leader.SlotInactive = _sprites["slot_inactive.png"];
        speaker.StandLookRightImage = _sprites["stand_right.png"].texture;
        speaker.StandLookLeftImage = _sprites["stand_left.png"].texture;
        speaker.StandLookRightInactiveImage = _sprites["stand_right_inactive.png"].texture;
        speaker.OperationSelectImage = _sprites["slot.png"].texture;
        speaker.BarkImage = _sprites["bark.png"];
    }

    internal Sprite Get(string fileName) => _sprites[fileName];

    internal void ApplyBadges(UnitLeaderTemplate template)
    {
        template.Badge = _sprites["badge.png"];
        template.BadgeMini = _sprites["badge_mini.png"];
        template.BadgeDragged = _sprites["badge_dragged.png"];
        template.BadgeUnitWindow = _sprites["badge_unit.png"];
        template.SlotBadge = _sprites["slot_badge.png"];
        template.BigBadge = _sprites["big_badge.png"];
    }
}
