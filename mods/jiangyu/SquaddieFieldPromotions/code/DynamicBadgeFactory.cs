using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Strategy;
using UnityEngine;

namespace SquaddieFieldPromotions;

/// <summary>
/// Experimental on-demand badge lettering for the self-contained generic art families.
/// Replaces the red A/D source glyph pixels on private readable Unity sprite copies.
/// No original Generic Leaders mod, external templates, or disk mutation.
/// Requires in-game verification with MENACE 0.7.15 / Jiangyu 1.4.7.
/// </summary>
internal static class DynamicBadgeFactory
{
    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['B'] = new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
        ['C'] = new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
        ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['G'] = new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
        ['J'] = new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" },
        ['K'] = new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
        ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
        ['M'] = new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
        ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
        ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['Q'] = new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['U'] = new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
        ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
        ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
        ['X'] = new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
        ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
        ['Z'] = new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" }
    };

    // Excel-style bijective base 26: A..Z, AA..AZ, BA..BZ, ...
    // The current MENACE cap is far below the point where these labels are illegible.
    internal static string LabelForIndex(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        long n = (long)index + 1;
        var chars = new List<char>();
        while (n > 0)
        {
            n--;
            chars.Add((char)('A' + (n % 26)));
            n /= 26;
        }
        chars.Reverse();
        return new string(chars.ToArray());
    }

    internal static string Apply(UnitLeaderTemplate target, StandalonePortraitAssets art,
        string label, List<UnityEngine.Object> pinned)
    {
        int attempted = 0, created = 0;
        // Preserve the specialised placement/drag sprite, which contains the
        // donor soldier's portrait ABOVE the black/red letter shield.
        target.Badge = Generate(art.Get("badge.png"), label, pinned, ref attempted, ref created) ?? art.Get("badge.png");
        target.BadgeMini = Generate(art.Get("badge_mini.png"), label, pinned, ref attempted, ref created) ?? art.Get("badge_mini.png");
        target.BadgeDragged = Generate(art.Get("badge_dragged.png"), label, pinned, ref attempted, ref created) ?? art.Get("badge_dragged.png");
        target.BadgeUnitWindow = Generate(art.Get("badge_unit.png"), label, pinned, ref attempted, ref created) ?? art.Get("badge_unit.png");
        target.SlotBadge = Generate(art.Get("slot_badge.png"), label, pinned, ref attempted, ref created) ?? art.Get("slot_badge.png");
        target.BigBadge = Generate(art.Get("big_badge.png"), label, pinned, ref attempted, ref created) ?? art.Get("big_badge.png");
        // Preserve the target native white-silhouette state. There is no
        // red-letter white silhouette in the embedded generic portrait source.
        return created + "/" + attempted;
    }

    private static Sprite Generate(Sprite source, string label,
        List<UnityEngine.Object> pinned, ref int attempted, ref int created)
    {
        if (source == null) return null;
        attempted++;
        try
        {
            Texture2D srcTex = source.texture;
            if (srcTex == null || !srcTex.isReadable) return null;
            Rect region = source.rect;
            int w = Mathf.RoundToInt(region.width);
            int h = Mathf.RoundToInt(region.height);
            int rx = Mathf.RoundToInt(region.x);
            int ry = Mathf.RoundToInt(region.y);
            if (w < 20 || h < 20 || rx < 0 || ry < 0 ||
                rx + w > srcTex.width || ry + h > srcTex.height)
                return null;
            var all = srcTex.GetPixels32();
            if (all == null || all.Length < srcTex.width * srcTex.height)
                return null;
            var output = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    output[y * w + x] = all[(y + ry) * srcTex.width + x + rx];

            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 p = output[y * w + x];
                // Custom Leaders Alpha/Delta use a distinctive red letter.
                if (p.a < 100 || p.r < 115 || p.r < p.g * 1.5f ||
                    p.r < p.b * 1.5f || p.g > 150) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            // If the source has no replaceable red letter, it is safer to keep
            // its existing sprite than erase an unrelated bitmap area.
            if (maxX < minX || maxY < minY) return null;
            // A tiny anti-aliased fringe beyond the red bounding box is covered.
            int x0 = System.Math.Max(0, minX - 2);
            int x1 = System.Math.Min(w - 1, maxX + 2);
            int y0 = System.Math.Max(0, minY - 2);
            int y1 = System.Math.Min(h - 1, maxY + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    output[y * w + x] = new Color32(0, 0, 0, 255);
            DrawLabel(output, w, h, label, x0, y0, x1, y1);

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Il2CppStructArray<Color32>(output));
            texture.Apply(false, true);
            Vector2 originalPivot = source.pivot;
            var pivot = new Vector2(originalPivot.x / w, originalPivot.y / h);
            Sprite result = Sprite.Create(texture, new Rect(0, 0, w, h), pivot);
            if (result == null) return null;
            pinned.Add(texture);
            pinned.Add(result);
            created++;
            return result;
        }
        catch
        {
            // F9 reports the aggregate x/y count; no per-sprite spam.
            return null;
        }
    }

    private static void DrawLabel(Color32[] pixels, int width, int height,
        string label, int x0, int y0, int x1, int y1)
    {
        int glyphWidth = label.Length * 6 - 1;
        int boxW = x1 - x0 + 1, boxH = y1 - y0 + 1;
        int scale = System.Math.Min(boxW / glyphWidth, boxH / 7);
        if (scale < 1) scale = 1;
        int inkW = glyphWidth * scale;
        int inkH = 7 * scale;
        int startX = x0 + (boxW - inkW) / 2;
        int startY = y0 + (boxH - inkH) / 2;
        var red = new Color32(240, 26, 32, 255);
        for (int i = 0; i < label.Length; i++)
        {
            if (!Font.TryGetValue(label[i], out string[] glyph)) continue;
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                {
                    if (glyph[row][col] != '1') continue;
                    int xx = startX + (i * 6 + col) * scale;
                    int yy = startY + (6 - row) * scale;
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int px = xx + dx, py = yy + dy;
                            if (px >= 0 && px < width && py >= 0 && py < height)
                                pixels[py * width + px] = red;
                        }
                }
        }
    }
}
