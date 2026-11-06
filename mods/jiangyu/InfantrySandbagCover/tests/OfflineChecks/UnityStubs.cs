namespace UnityEngine;

public class Object
{
    public bool Destroyed;
    public static void Destroy(Object value)
    {
        value.Destroyed = true;
        if (value is GameObject go) foreach (var c in go.Children) Destroy(c);
    }
}
public enum HideFlags { HideAndDontSave }
public class Component : Object { public T Cast<T>() => (T)(object)this; }
public sealed class Transform(GameObject owner)
{
    public void SetParent(Transform parent, bool world) => parent.Owner.Children.Add(owner);
    public GameObject Owner => owner;
}
public sealed class GameObject : Object
{
    public static List<GameObject> All = new();
    public static bool FailComponent;
    public string Name;
    public HideFlags hideFlags;
    public Transform transform;
    public List<GameObject> Children = new();
    public LineRenderer Renderer;
    public GameObject(string name) { Name = name; transform = new(this); All.Add(this); }
    public Component AddComponent(Type type)
    {
        if (FailComponent) throw new ArgumentException("fixture native component failure");
        if (Renderer != null) throw new InvalidOperationException("Two renderers on the same object");
        if (type != typeof(LineRenderer)) throw new InvalidOperationException();
        return Renderer = new LineRenderer();
    }
}
public sealed class LineRenderer : Component
{
    public bool useWorldSpace, loop;
    public int positionCount, numCornerVertices, sortingOrder;
    public float startWidth, endWidth;
    public Color startColor, endColor;
    public Material sharedMaterial;
    public Vector3[] Positions = new Vector3[4];
    public void SetPosition(int i, Vector3 p) => Positions[i] = p;
}
public sealed class Shader : Object { public static Shader Find(string name) => new(); }
public sealed class Material : Object
{
    public Material(Shader shader) { }
    public HideFlags hideFlags;
    public int renderQueue;
    public bool HasProperty(string key) => true;
    public void SetInt(string key, int value) { }
    public void SetColor(string key, Color value) { }
    public void SetTexture(string key, Texture2D texture) { }
}
public sealed class Texture2D { public static Texture2D whiteTexture = new(); }
public record struct Vector3(float x, float y, float z)
{
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
}
public record struct Color(float r, float g, float b, float a)
{
    public static Color white = new(1, 1, 1, 1), black = new(0, 0, 0, 1);
}
public record struct Matrix4x4(int Value) { public static Matrix4x4 identity = new(1); }
public record struct Rect(float x, float y, float width, float height);
public enum TextAnchor { MiddleCenter }
public enum FontStyle { Bold }
public enum EventType { Repaint }
public sealed class Event { public static Event current = new(); public EventType type; }
public sealed class GUIStyleState { public Color textColor; }
public sealed class GUIStyle
{
    public GUIStyle() { }
    public GUIStyle(GUIStyle other) { }
    public TextAnchor alignment;
    public int fontSize;
    public FontStyle fontStyle;
    public GUIStyleState normal = new();
}
public sealed class GUISkin { public GUIStyle label = new(); }
public static class GUI
{
    public static Color color, contentColor, backgroundColor;
    public static Matrix4x4 matrix;
    public static int depth;
    public static GUISkin skin = new();
    public static List<string> Labels = new();
    public static void Label(Rect rect, string text, GUIStyle style) => Labels.Add(text);
}
public static class Screen { public static int height = 1000; }
public sealed class Camera { public static Camera main = new(); public Vector3 WorldToScreenPoint(Vector3 p) => new(p.x, p.y, 10); }