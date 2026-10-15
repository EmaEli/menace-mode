using System;
using UnityEngine;

namespace MenaceModSuite.Shared;

// No-op developer console. Jiangyu has no IMGUI developer console, so the modules'
// diagnostic panels are accepted and dropped; gameplay and the MenaceModSettings
// options are unaffected.
internal static class DevConsole
{
    public static void RegisterPanel(string name, Action<Rect> draw) { }

    public static void RemovePanel(string name) { }
}
