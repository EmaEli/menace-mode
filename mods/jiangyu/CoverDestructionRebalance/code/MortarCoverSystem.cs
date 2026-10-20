using System.Reflection;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using Il2CppMenace.Tactical.Skills.Effects;
using Jiangyu.Sdk;

namespace CoverDestructionRebalance;

// The KDL pass alone cannot protect handlers replaced after template application.
// Reassert the two infantry mortar exceptions on the actual handler at each impact.
public sealed class MortarCoverSystem : JiangyuSystem
{
    private bool _failureLogged;

    public override void OnInit()
    {
        _failureLogged = false;
        // Native callbacks can run on IL2CPP worker threads (#44). Compile our
        // non-generic callback path on the main thread before registering it.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var method in typeof(MortarCoverSystem).GetMethods(flags))
            RuntimeHelpers.PrepareMethod(method.MethodHandle);

        Context.Patches.Prefix("Il2CppMenace.Tactical.Skills.Effects.AttackHandler",
            "OnTileHit", 4, BeforeTileHit);
    }

    private void BeforeTileHit(PatchInfo info)
    {
        try
        {
            if (info.Args.Count == 0) return;
            var handler = info.Instance as AttackHandler
                ?? (info.Instance as Il2CppObjectBase)?.TryCast<AttackHandler>();
            var skill = info.Args[0] as Skill
                ?? (info.Args[0] as Il2CppObjectBase)?.TryCast<Skill>();
            ProtectMortarCover(handler, skill);
        }
        catch (Exception e)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Context.Log.Warn("Mortar half-cover guard failed (" + e.GetType().Name + ").");
        }
    }

    private static void ProtectMortarCover(AttackHandler handler, Skill skill)
    {
        if (handler == null || handler.Pointer == IntPtr.Zero
            || skill == null || skill.Pointer == IntPtr.Zero) return;
        string id = skill.GetID();
        if (!string.Equals(id, "active.launch_light_mortar_frag", StringComparison.Ordinal)
            && !string.Equals(id, "active.launch_medium_mortar_frag", StringComparison.Ordinal)) return;

        var attack = handler.GetTemplate();
        if (attack != null && attack.Pointer != IntPtr.Zero
            && attack.DestroyHalfCover != HalfCoverClass.None)
            attack.DestroyHalfCover = HalfCoverClass.None;
        // Do not skip OnTileHit: unit damage, suppression and native effects stay intact.
        // Do not restore the stale value after impact: this is a permanent skill rule.
    }
}