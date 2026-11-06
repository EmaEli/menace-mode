using Il2CppInterop.Runtime;
using Il2CppMenace.Tactical;
using Jiangyu.Sdk;
using UnityEngine;

namespace InfantrySandbagCover;

// Same independent, dual-LineRenderer technique as WMR mortar ranging. Never use
// TileHighlighter's shared delayed-AoE channels (Supply Drop and other native markers).
internal sealed class ConstructionOverlay
{
    private sealed class Marker
    {
        internal GameObject Root;
        internal Vector3 Center;
    }
    private readonly Dictionary<long, Marker> _markers = new();
    private Material _material;
    private GUIStyle _style;
    private bool _failed;
    private string _stage = "initialization";

    private Material Material()
    {
        if (_material != null) return _material;
        foreach (string name in new[] { "Hidden/Internal-Colored", "Sprites/Default", "UI/Default", "Unlit/Color" })
        {
            var shader = Shader.Find(name);
            if (shader == null) continue;
            _stage = "material creation (" + name + ")";
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 5000 };
            _stage = "material properties";
            foreach (var p in new[] { ("_ZWrite", 0), ("_ZTest", 8), ("_Cull", 0), ("_SrcBlend", 5), ("_DstBlend", 10) })
                if (_material.HasProperty(p.Item1)) _material.SetInt(p.Item1, p.Item2);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", Color.white);
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", Texture2D.whiteTexture);
            return _material;
        }
        throw new InvalidOperationException("No compatible tile-border shader.");
    }

    private LineRenderer Line(GameObject root, Material material, bool outer)
    {
        // Only one Renderer per GameObject; generic AddComponent<LineRenderer> is stripped.
        var child = new GameObject(outer ? "Sandbag Border Outline" : "Sandbag Border Progress")
            { hideFlags = HideFlags.HideAndDontSave };
        _stage = "renderer parenting";
        child.transform.SetParent(root.transform, false);
        _stage = "LineRenderer component creation";
        var line = child.AddComponent(Il2CppType.Of<LineRenderer>()).Cast<LineRenderer>();
        _stage = "LineRenderer properties";
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.sharedMaterial = material;
        line.numCornerVertices = 4;
        line.sortingOrder = outer ? 32760 : 32761;
        line.startWidth = line.endWidth = outer ? 0.68f : 0.38f;
        return line;
    }

    internal void Refresh(ConstructionState work, Dictionary<long, Actor> actors)
    {
        try
        {
            var desired = new HashSet<long>();
            foreach (long key in work.Actors)
            {
                var site = work.Get(key);
                if (site.Completed <= 0 || !actors.TryGetValue(key, out var actor)) continue;
                var tile = actor.GetTile();
                if (tile == null) continue;
                desired.Add(key);
                if (_markers.ContainsKey(key)) continue;
                // Keep the screen-space progress label independent of world-renderer
                // availability. A Unity component failure must not hide all progress.
                var marker = new Marker { Center = tile.GetPos() };
                _markers[key] = marker;
                if (_failed) continue;
                _stage = "root creation";
                var root = new GameObject("Infantry Sandbag Construction") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var center = marker.Center;
                    var material = Material();
                    var outer = Line(root, material, true);
                    var inner = Line(root, material, false);
                    _stage = "border positions and colors";
                    Position(outer, center);
                    Position(inner, center);
                    outer.startColor = outer.endColor = new Color(0.015f, 0.015f, 0.015f, 0.96f);
                    inner.startColor = inner.endColor = new Color(1f, 0.78f, 0.12f, 1f);
                    marker.Root = root;
                }
                catch { UnityEngine.Object.Destroy(root); throw; }
            }
            foreach (long key in _markers.Keys.ToArray())
                if (!desired.Contains(key))
                {
                    if (_markers[key].Root != null) UnityEngine.Object.Destroy(_markers[key].Root);
                    _markers.Remove(key);
                }
        }
        catch (Exception e)
        {
            bool firstFailure = !_failed;
            _failed = true;
            foreach (var marker in _markers.Values)
            {
                if (marker.Root != null) UnityEngine.Object.Destroy(marker.Root);
                marker.Root = null;
            }
            if (_material != null) UnityEngine.Object.Destroy(_material);
            _material = null;
            if (firstFailure) Log.Warn("Sandbag tile border unavailable at " + _stage + ": " + e.GetType().Name
                + ": " + e.Message + ". Progress labels remain enabled.");
        }
    }

    private static void Position(LineRenderer line, Vector3 center)
    {
        const float half = 3.86f;
        float y = center.y + 0.55f;
        line.SetPosition(0, new Vector3(center.x - half, y, center.z - half));
        line.SetPosition(1, new Vector3(center.x + half, y, center.z - half));
        line.SetPosition(2, new Vector3(center.x + half, y, center.z + half));
        line.SetPosition(3, new Vector3(center.x - half, y, center.z + half));
    }

    internal void Draw(ConstructionState work)
    {
        if (_markers.Count == 0 || Event.current == null || Event.current.type != EventType.Repaint) return;
        var camera = Camera.main;
        if (camera == null) return;
        _style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
        var color = GUI.color;
        var content = GUI.contentColor;
        var background = GUI.backgroundColor;
        var matrix = GUI.matrix;
        int depth = GUI.depth;
        try
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = GUI.contentColor = Color.white;
            GUI.depth = -1000;
            foreach (var pair in _markers)
            {
                var site = work.Get(pair.Key);
                if (site == null || site.Completed <= 0) continue;
                var p = camera.WorldToScreenPoint(pair.Value.Center + new Vector3(0f, 0.8f, 0f));
                if (p.z <= 0f) continue;
                string text = "Sandbags " + site.Completed + "/" + site.Required;
                _style.normal.textColor = Color.black;
                GUI.Label(new Rect(p.x - 79, Screen.height - p.y - 10, 160, 24), text, _style);
                _style.normal.textColor = new Color(1f, 0.78f, 0.12f, 1f);
                GUI.Label(new Rect(p.x - 80, Screen.height - p.y - 11, 160, 24), text, _style);
            }
        }
        finally { GUI.color = color; GUI.contentColor = content; GUI.backgroundColor = background; GUI.matrix = matrix; GUI.depth = depth; }
    }

    private void ClearObjects()
    {
        foreach (var marker in _markers.Values) if (marker.Root != null) UnityEngine.Object.Destroy(marker.Root);
        _markers.Clear();
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _material = null;
    }

    internal void Clear() { ClearObjects(); _style = null; _failed = false; }
}