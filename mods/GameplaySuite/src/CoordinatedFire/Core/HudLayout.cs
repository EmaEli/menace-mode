using System;

namespace CoordinatedFire.Core;

public enum HudAnchor
{
    BottomCenter,
    BottomLeft,
    BottomRight,
    TopCenter,
    TopLeft,
    TopRight,
    Center
}

public readonly record struct HudRect(float X, float Y, float Width, float Height, float Scale);

public static class HudLayout
{
    public const float MinScale = 0.65f;
    public const float MaxScale = 1.50f;

    public static HudRect Compute(
        int screenWidth,
        int screenHeight,
        float requestedScale,
        bool compact,
        HudAnchor anchor,
        float offsetX,
        float offsetY)
    {
        if (screenWidth <= 0 || screenHeight <= 0)
            return new HudRect(4, 4, 1, 1, MinScale);

        float baseWidth = compact ? 310f : 390f;
        float baseHeight = compact ? 42f : 78f;
        float scale = Math.Clamp(requestedScale, MinScale, MaxScale);

        // Never let an oversized user scale push the entire control off-screen.
        float fitX = Math.Max(0.10f, (screenWidth - 8f) / baseWidth);
        float fitY = Math.Max(0.10f, (screenHeight - 8f) / baseHeight);
        scale = Math.Min(scale, Math.Min(fitX, fitY));

        float width = baseWidth * scale;
        float height = baseHeight * scale;
        float side = 18f;
        float top = 90f * scale;
        float bottomReserved = 165f * scale;
        float x;
        float y;

        switch (anchor)
        {
            case HudAnchor.BottomLeft:
                x = side;
                y = screenHeight - height - bottomReserved;
                break;
            case HudAnchor.BottomRight:
                x = screenWidth - width - side;
                y = screenHeight - height - bottomReserved;
                break;
            case HudAnchor.TopCenter:
                x = (screenWidth - width) / 2f;
                y = top;
                break;
            case HudAnchor.TopLeft:
                x = side;
                y = top;
                break;
            case HudAnchor.TopRight:
                x = screenWidth - width - side;
                y = top;
                break;
            case HudAnchor.Center:
                x = (screenWidth - width) / 2f;
                y = (screenHeight - height) / 2f;
                break;
            default:
                x = (screenWidth - width) / 2f;
                y = screenHeight - height - bottomReserved;
                break;
        }

        // Offsets are screen pixels, deliberately independent from HUD scale.
        x += offsetX;
        y += offsetY;

        float maxX = Math.Max(4f, screenWidth - width - 4f);
        float maxY = Math.Max(4f, screenHeight - height - 4f);
        x = Math.Clamp(x, 4f, maxX);
        y = Math.Clamp(y, 4f, maxY);

        return new HudRect(x, y, width, height, scale);
    }
}
