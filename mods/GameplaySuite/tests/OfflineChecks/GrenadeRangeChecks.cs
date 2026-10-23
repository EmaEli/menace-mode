using MenaceWeaponRangesRebalanced;

internal static class GrenadeRangeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (string skill in new[]
        {
            "active.throw_anti_tank_grenade_tier1_grenade_bundle",
            "active.throw_anti_tank_grenade_tier2",
            "active.throw_emp_grenade",
            "active.throw_frag_grenade_tier2",
            "active.throw_frag_grenade_tier3",
            "active.throw_incendiary_grenade",
            "active.throw_nano_thermite_grenade",
            "active.throw_smoke_grenade",
            "active.throw_smoke_grenade_limited",
            "active.throw_corrosive_grenade",
            "active.throw_nova_grenade",
            "active.throw_plasma_grenade",
            "active.throw_scrap_bomb_frag_tier1",
            "active.throw_flashbang",
            "ACTIVE.THROW_SCRAP_BOMB_FRAG_TIER1",
            "ACTIVE.THROW_FLASHBANG",
        })
            check(ThrownGrenadeRangeRules.IsThrownGrenadeSkill(skill), "recognize thrown-grenade skill " + skill);

        foreach (string skill in new[]
        {
            "active.launch_underbarrel_frag_grenade",
            "active.fire_grenade_launcher",
            "specialweapon.grenade_launcher_gr331",
            "active.throw_knife",
            "active.launch_scrap_bomb_frag_tier1",
            "active.launch_flashbang",
            "active.throw_scrap_metal",
            "active.throw_flashbang_launcher",
            "",
            " ",
        })
            check(!ThrownGrenadeRangeRules.IsThrownGrenadeSkill(skill), "exclude non-thrown-grenade attack " + skill);

        foreach (string skill in new[] { "active.throw_scrap_bomb_frag_tier1", "active.throw_flashbang" })
        {
            bool recognized = ThrownGrenadeRangeRules.IsThrownGrenadeSkill(skill);
            var applied = ThrownGrenadeRangeRules.Evaluate(3, 3, recognized, false, 2);
            check(applied.ShouldOwn && applied.DesiredRange == 2, "cap non-grenade-named throwable " + skill);
            var restored = ThrownGrenadeRangeRules.Evaluate(3, applied.DesiredRange, false, applied.ShouldOwn, 2);
            check(!restored.ShouldOwn && restored.DesiredRange == 3, "restore non-grenade-named throwable " + skill);
        }

        var capped = ThrownGrenadeRangeRules.Evaluate(3, 3, true, false, 2);
        check(capped.ShouldOwn && capped.DesiredRange == 2, "default cap reduces vanilla 1-3 to 1-2");

        var shortRange = ThrownGrenadeRangeRules.Evaluate(1, 1, true, false, 2);
        check(!shortRange.ShouldOwn && shortRange.DesiredRange == 1, "do not widen grenade ranges below the cap");

        var increasedCap = ThrownGrenadeRangeRules.Evaluate(3, 2, true, true, 4);
        check(!increasedCap.ShouldOwn && increasedCap.DesiredRange == 3, "raising the cap restores the original range");

        var disabled = ThrownGrenadeRangeRules.Evaluate(3, 2, false, true, 2);
        check(!disabled.ShouldOwn && disabled.DesiredRange == 3, "disabling the cap restores the original range");

        var externalIncrease = ThrownGrenadeRangeRules.Evaluate(3, 4, true, false, 2);
        check(externalIncrease.ShouldOwn && externalIncrease.DesiredRange == 2, "cap a later external range increase");

        var customCap = ThrownGrenadeRangeRules.Evaluate(5, 5, true, false, 4);
        check(customCap.ShouldOwn && customCap.DesiredRange == 4, "apply the configured maximum");

        var alreadyShorter = ThrownGrenadeRangeRules.Evaluate(2, 2, true, false, 4);
        check(!alreadyShorter.ShouldOwn && alreadyShorter.DesiredRange == 2, "leave ranges under the configured maximum unchanged");
    }
}