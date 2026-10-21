// Managed Unity simulation for the production badge algorithm, not rendering evidence.
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppStructArray<T>
    {
        public readonly T[] Values;
        public Il2CppStructArray(T[] values) { Values = values; }
    }
}

namespace UnityEngine
{
    public enum HideFlags { DontUnloadUnusedAsset }
    public enum TextureFormat { RGBA32 }
    public class Object { public HideFlags hideFlags; }
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte red, byte green, byte blue, byte alpha)
        { r = red; g = green; b = blue; a = alpha; }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float xx, float yy) { x = xx; y = yy; }
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float xx, float yy, float w, float h) { x = xx; y = yy; width = w; height = h; }
    }
    public static class Mathf { public static int RoundToInt(float value) => (int)Math.Round(value); }
    public class Texture2D : Object
    {
        public int width, height;
        public bool isReadable = true;
        private Color32[] _pixels;
        public Texture2D(int w, int h, TextureFormat format, bool mipmaps)
        { width = w; height = h; _pixels = new Color32[w * h]; }
        public Color32[] GetPixels32() => (Color32[])_pixels.Clone();
        public void SetPixels32(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32> pixels)
        { _pixels = (Color32[])pixels.Values.Clone(); }
        public void Apply(bool mipmaps, bool noLongerReadable) { isReadable = !noLongerReadable; }
    }
    public class Sprite : Object
    {
        public Texture2D texture;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit = 100) => new()
        {
            texture = texture, rect = rect,
            pivot = new Vector2(pivot.x * rect.width, pivot.y * rect.height), pixelsPerUnit = pixelsPerUnit,
        };
    }
}

namespace SquaddieFieldPromotions
{
    internal sealed class StandalonePortraitAssets
    {
        internal readonly Dictionary<string, UnityEngine.Sprite> Sprites = new();
        internal UnityEngine.Sprite Get(string name) => Sprites[name];
    }
}