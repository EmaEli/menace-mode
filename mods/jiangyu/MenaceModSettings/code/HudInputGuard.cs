using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppMenace.States;
using Il2CppMenace.Tactical;
using Jiangyu.Sdk;
using UnityEngine;

namespace MenaceModSettings;

/// <summary>
/// One input guard for all IMGUI panels. Lives in the shared companion assembly: compiling
/// a static registry into each consumer would create independent registries and patches.
/// Rect providers return GUI coordinates, or null when hidden, and must not draw anything.
/// </summary>
public static class HudInputGuard
{
    private static readonly Dictionary<object, Func<Rect?>> Panels = new();
    private static readonly HudPointerCapture Capture = new();
    private static global::HarmonyLib.Harmony _harmony;
    private static int _mainThread;
    private static bool _errorLogged;

    public static void Register(object owner, Func<Rect?> rectangle) => Panels[owner] = rectangle;
    public static void Unregister(object owner) => Panels.Remove(owner);

    internal static void Install()
    {
        if (_harmony != null) return;
        _mainThread = Environment.CurrentManagedThreadId;
        var harmony = new global::HarmonyLib.Harmony("menacemodsettings.hudinput");
        try
        {
            var prefix = AccessTools.Method(typeof(HudInputGuard), nameof(AllowMouseInput));
            RuntimeHelpers.PrepareMethod(prefix.MethodHandle);
            var hook = new HarmonyMethod(prefix) { priority = Priority.First };
            harmony.Patch(Require(typeof(TacticalState), "HandleMouseInput", Type.EmptyTypes), prefix: hook);
            harmony.Patch(Require(typeof(TacticalCamera), "HandleMouseInput", new[] { typeof(bool) }), prefix: hook);
            harmony.Patch(Require(typeof(NoneAction), "HandleLeftClickOnTile", new[] { typeof(Tile), typeof(Actor) }), prefix: hook);
            _harmony = harmony;
        }
        catch (Exception e)
        {
            harmony.UnpatchSelf();
            Log.Error("HUD input guard could not bind; tactical HUD clicks are not protected: " + e);
        }
    }

    private static MethodInfo Require(Type type, string name, Type[] args)
        => AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.FullName, name);

    // Invoked only on mouse paths, never on HandleKeyInput or movement execution.
    private static bool AllowMouseInput()
    {
        // Do not touch Unity or IL2CPP from worker threads (see the previous JIT/thread fix).
        if (Environment.CurrentManagedThreadId != _mainThread) return true;
        try
        {
            if (!Application.isFocused || !TacticalManager.IsMissionRunning())
            {
                Capture.Reset();
                return true;
            }
            var mouse = Input.mousePosition;
            var point = new Vector2(mouse.x, Screen.height - mouse.y);
            bool inside = false;
            foreach (var provider in Panels.Values)
            {
                var rect = provider();
                if (rect.HasValue && rect.Value.Contains(point)) inside = true;
            }
            int held = 0, pressed = 0;
            for (int button = 0; button < 3; button++)
            {
                if (Input.GetMouseButton(button)) held |= 1 << button;
                if (Input.GetMouseButtonDown(button)) pressed |= 1 << button;
            }
            return !Capture.Sample(Time.frameCount, inside, held, pressed);
        }
        catch (Exception e)
        {
            Capture.Reset();
            if (!_errorLogged)
            {
                _errorLogged = true;
                Log.Error("HUD input guard failed; native input remains enabled: " + e);
            }
            return true;
        }
    }

    internal static void SceneChanged()
    {
        Capture.Reset();
        _errorLogged = false;
    }

    internal static void Unload()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        Panels.Clear();
        SceneChanged();
    }
}