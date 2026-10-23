using Il2CppMenace.Tactical;
using Il2CppMenace.Tactical.Skills;
using MenaceWeaponRangesRebalanced;

int checks = 0;
void Check(bool value, string label)
{
    if (!value) throw new Exception(label);
    checks++;
}

Plugin.Install();
Check(HarmonyLib.Harmony.Hooks.Count == 5, "bind exact getter, range overloads, setter and container hooks");
var player = new Skill("active.throw_frag_grenade_tier2", 3, 1);
var pirate = new Skill("active.throw_scrap_bomb_frag_tier1", 3, 2);
var launcher = new Skill("active.fire_grenade_launcher", 20, 3);
var playerSkills = new SkillContainer(player, launcher);
var pirateSkills = new SkillContainer(pirate);
TacticalManager.Current = new(new BaseFaction(new Actor(playerSkills)), new BaseFaction(new Actor(pirateSkills)));

// Regression: a template-only write says 2 but the native runtime cache still says 3.
player.TemplateMaxRange = 2;
Check(player.m_MaxRange == 3, "fixture reproduces template/cache disagreement");
Check(player.GetMaxRange() == 2 && player.m_MaxRange == 2, "getter hook changes the cache, not just its return value");
Plugin.Configure(true, true);
Check(pirate.m_MaxRange == 2, "refresh already-loaded AI skills too");
Check(!player.IsInRange(3, 0) && player.IsInRange(2, 0), "distance targeting rejects 3 and permits 2");
Check(!pirate.IsInRange(new Tile(3), new Tile(0)), "tile targeting rejects 3");
Check(pirate.IsInRange(new Tile(2), new Tile(0)), "tile targeting permits 2");
Check(launcher.m_MaxRange == 20 && launcher.GetMaxRange() == 20, "launchers stay untouched");
Check(pirate.TemplateMaxRange == 3, "runtime enforcement does not mutate templates");

Plugin.Configure(true, true, 1);
Check(player.m_MaxRange == 1 && pirate.m_MaxRange == 1, "lower cap on loaded skills");
Plugin.Configure(true, true, 4);
Check(player.m_MaxRange == 3 && pirate.m_MaxRange == 3, "raising cap restores uncapped range, without widening");
Plugin.Configure(true, true);
Plugin.Configure(true, false);
Check(player.m_MaxRange == 3 && pirate.m_MaxRange == 3, "feature toggle restores owned caches");
Plugin.Configure(true, true);
Plugin.Configure(false, true);
Check(player.m_MaxRange == 3 && pirate.m_MaxRange == 3, "module disable restores owned caches");

Plugin.Configure(true, true);
pirate.SetRanges(1, 5, 1);
Check(pirate.m_MaxRange == 2, "cap fresh native SetRanges result");
Plugin.Configure(false, true);
Check(pirate.m_MaxRange == 5, "restore new native calculation rather than stale original");
Plugin.Configure(true, true);
pirate.SetRanges(1, 2, 1);
Plugin.Configure(false, true);
Check(pirate.m_MaxRange == 2, "fresh native value equal to cap must not restore an obsolete longer range");
Plugin.Configure(true, true);
pirate.m_MaxRange = 1;
pirateSkills.Update();
Check(pirate.m_MaxRange == 1, "native shorter range is preserved");
Plugin.Configure(false, true);
Check(pirate.m_MaxRange == 1, "disable does not overwrite external shorter range");

Plugin.Configure(true, true);
pirate.m_MaxRange = 4;
pirateSkills.Update();
Check(pirate.m_MaxRange == 2, "cap native container recalculation");
Plugin.Configure(false, true);
Check(pirate.m_MaxRange == 4, "restore latest container recalculation");
Plugin.Configure(true, true);
Plugin.SceneChanged();
Check(player.m_MaxRange == 3 && pirate.m_MaxRange == 4, "restore surviving actors before clearing scene state");
Plugin.Configure(true, true);
Plugin.Configure(false, true);
Check(player.m_MaxRange == 3 && pirate.m_MaxRange == 4, "scene refresh retains correct restoration values");

Plugin.Configure(true, true);
var late = new Skill("active.throw_flashbang", 3, 20);
Check(!late.IsInRange(3, 0) && late.m_MaxRange == 2, "distance overload repairs a late/stale cache before native comparison");
var lateTile = new Skill("active.throw_flashbang", 3, 21);
Check(!lateTile.IsInRange(new Tile(3), new Tile(0)) && lateTile.m_MaxRange == 2,
    "tile overload repairs a late/stale cache before native comparison");
Check(Plugin.Logs.Any(s => s.Contains("cachedMax=3 nativeMax=2")), "diagnostics distinguish cached and applied native range");
Console.WriteLine($"Grenade runtime checks passed: {checks}. Managed stand-ins; native detours/gameplay not tested.");