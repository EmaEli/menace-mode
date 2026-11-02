using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical.Skills;

namespace MenaceInfantryAssault;

public sealed partial class Plugin
{
    private static bool IsAssaultSkill(object? skill)
    {
        if (OffMainThread || skill is not Il2CppObjectBase native) return false;

        try
        {
            if (native.Pointer == IntPtr.Zero ||
                _assaultTemplate is not Il2CppObjectBase template || template.Pointer == IntPtr.Zero)
                return false;

            // IL2CPP folds unrelated getters into one native body: in 0.7.17,
            // Skill.IsVisible and SkillContainer.IsAIDryrun share RVA 0x6C2500.
            // A detour can therefore wrap a SkillContainer pointer as a Skill.
            // TryCast checks the *native* class; managed 'is Skill' does not.
            Skill? actual = native.TryCast<Skill>();
            if (actual == null) return false;

            // Compare the stable template identity, without invoking GetID or
            // converting any native string during collapse/skill teardown.
            // Do not cache skill addresses, which can be reused after removal.
            return actual.GetTemplate()?.Pointer == template.Pointer;
        }
        catch { return false; }
    }
}