using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace AIPerformance;

public sealed partial class Plugin
{
    // Preserve the real worker lifetime, not vanilla's detached 12-second wrapper.
    // This never cancels, waits on the game thread, skips actors or changes scoring.
    private static void PatchAiTaskLifetime(Harmony harmony)
    {
        Type faction = FindType("Il2CppMenace.Tactical.AI.AIFaction") ??
                       FindType("Menace.Tactical.AI.AIFaction");
        MethodInfo method = faction?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(m => m.Name == "RunWithTimeout" && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(Il2CppSystem.Action)
                && m.GetParameters()[1].ParameterType == typeof(Il2CppMenace.Tactical.Actor)
                && m.ReturnType == typeof(Il2CppSystem.Threading.Tasks.Task));
        if (method == null)
            throw new NotSupportedException("AI task safety: expected AIFaction.RunWithTimeout(Action, Actor) signature missing.");
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(Plugin).GetMethod(
            nameof(AIFactionRunWithTimeoutPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
    }

    private static bool AIFactionRunWithTimeoutPrefix(
        Il2CppSystem.Action __0, ref Il2CppSystem.Threading.Tasks.Task __result)
    {
        if (__0 == null) return true;
        __result = Il2CppSystem.Threading.Tasks.Task.Run(__0);
        return false;
    }
}