using System.Collections.Generic;
using Jiangyu.Sdk;
using UnityEngine;

namespace MenaceModSettings;

/// <summary>
/// Shared IMGUI frame hook. Jiangyu systems have no <c>OnGUI</c> callback, so mods that draw
/// an IMGUI HUD (GUI/GUILayout) register a draw callback here; a single injected
/// <see cref="ModGuiHost"/> behaviour on a persistent hidden GameObject fans its Unity
/// <c>OnGUI</c> message out to them. A callback that throws is logged once and disabled.
/// </summary>
public static class ModGui
{
    private static readonly List<Action> Draws = new();
    private static readonly HashSet<Action> Failed = new();
    private static Action[] _snapshot = Array.Empty<Action>();
    private static GameObject _host;

    public static void Add(Action draw)
    {
        if (draw == null || Draws.Contains(draw))
            return;
        Draws.Add(draw);
        _snapshot = Draws.ToArray();
        EnsureHost();
    }

    public static void Remove(Action draw)
    {
        if (draw == null || !Draws.Remove(draw))
            return;
        Failed.Remove(draw);
        _snapshot = Draws.ToArray();
    }

    internal static void Dispatch()
    {
        var draws = _snapshot;
        for (var i = 0; i < draws.Length; i++)
        {
            var draw = draws[i];
            if (Failed.Contains(draw))
                continue;
            try { draw(); }
            catch (Exception e)
            {
                Failed.Add(draw);
                Log.Error($"OnGUI callback from {draw.Method.DeclaringType?.FullName} disabled after exception: {e}");
            }
        }
    }

    private static void EnsureHost()
    {
        if (_host != null)
            return;
        try
        {
            _host = new GameObject("MenaceModSettings.ModGuiHost") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ModGuiHost>();
        }
        catch (Exception e)
        {
            Log.Error("ModGui host creation failed; IMGUI callbacks will not run: " + e);
        }
    }
}

/// <summary>Injected behaviour whose Unity OnGUI message drives <see cref="ModGui"/>.</summary>
[JiangyuType]
public sealed class ModGuiHost : MonoBehaviour
{
    public ModGuiHost(IntPtr ptr) : base(ptr) { }

    public void OnGUI() => ModGui.Dispatch();
}
