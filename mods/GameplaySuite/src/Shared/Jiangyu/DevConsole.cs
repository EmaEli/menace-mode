#if JIANGYU
using System;
using UnityEngine;

namespace MenaceModSuite.Shared;

// Stand-in for the Modpack Loader's Menace.SDK.DevConsole. Jiangyu has no IMGUI developer
// console, so the modules' diagnostic panels are accepted and dropped; gameplay and the
// MenaceModSettings options are unaffected. Not compiled into the legacy modpack DLLs.
internal static class DevConsole
{
    public static void RegisterPanel(string name, Action<Rect> draw) { }

    public static void RemovePanel(string name) { }
}
#endif
