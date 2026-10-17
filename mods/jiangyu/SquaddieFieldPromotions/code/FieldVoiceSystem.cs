using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Strategy;
using UnityEngine;

namespace SquaddieFieldPromotions;

/// <summary>
/// Standalone port of the original Menace Modpack Loader John/Jane tactical bark
/// pipeline. Uses the already-existing MelonLoader Harmony runtime by reflection,
/// with no reference to Menace.ModpackLoader, Menace.SDK or CustomLeaders.
/// No passive polling/logging: F9/F10 are explicit lab actions.
/// </summary>
internal sealed class FieldVoiceSystem
{
    private const int LimVoiceBankId = 1717671160;

    private sealed class BarkSpec
    {
        internal BarkSpec(string key, string sourcePath, string cloneSuffix, int[] sourceItemIds, string wavFile,
            string textFrom = null, string textTo = null, string flagFrom = null)
        {
            Key = key;
            SourcePath = sourcePath;
            CloneSuffix = cloneSuffix;
            SourceItemIds = sourceItemIds;
            WavFile = wavFile;
            TextFrom = textFrom;
            TextTo = textTo;
            FlagFrom = flagFrom;
        }
        internal string Key { get; }
        internal string SourcePath { get; }
        internal string CloneSuffix { get; }
        internal int[] SourceItemIds { get; }
        internal int SourceSoundItemId => SourceItemIds[0];
        internal string WavFile { get; }
        internal string TextFrom { get; }
        internal string TextTo { get; }
        internal string FlagFrom { get; }
    }

    private sealed class VoiceProfile
    {
        internal VoiceProfile(string key, string speakerId, string bankName, string conversationPrefix,
            string assetPrefix, string audioFolder)
        {
            Key = key;
            SpeakerId = speakerId;
            BankName = bankName;
            BankId = StableId(bankName);
            ConversationPrefix = conversationPrefix;
            AssetPrefix = assetPrefix;
            AudioFolder = audioFolder;
        }

        internal string Key { get; }
        internal string SpeakerId { get; }
        internal string BankName { get; }
        internal int BankId { get; }
        internal string ConversationPrefix { get; }
        internal string AssetPrefix { get; }
        internal string AudioFolder { get; }
        internal UnityEngine.Object BankUnityObject { get; set; }
        internal object BankProxy { get; set; }
        internal bool BankRegistered { get; set; }
        internal bool BankRuntimeRefreshed { get; set; }
        internal bool ClipsApplied { get; set; }
        internal readonly Dictionary<string, UnityEngine.Object> ConversationUnityObjects = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, object> ConversationProxies = new(StringComparer.Ordinal);
    }

    private static readonly BarkSpec[] VoiceBarks =
    {
        new("ready", "Lim/click_bark", "click_bark", new[] { 1429249920 }, "ready.wav",
            "Orders?", "Ready.", "click_bark_lim"),
        new("moving", "Lim/moving_lim", "moving", new[] { 346567201, 1422910161, 1008610263 }, "moving.wav"),
        new("move_done", "Lim/moving_finished", "moving_finished", new[] { 1548531623, 517605642, 1478094426 }, "move_done.wav"),
        new("spotted", "Lim/enemy_sighted_lim", "enemy_sighted", new[] { 531013313, 1752711602, 424333922, 1507490980 }, "spotted.wav"),
        new("taking_fire", "Lim/taking_fire", "taking_fire", new[] { 1606505927, 1801389253, 211144209, 217362430 }, "taking_fire.wav"),
        new("kill", "Lim/enemy_killed", "enemy_killed", new[] { 1220472246, 1340203303, 1689421009, 250161950, 1105767636, 1887425228 }, "kill.wav"),
        new("suppressed", "Lim/suppressed", "suppressed", new[] { 1068577831, 583494727, 1484684022 }, "suppressed.wav"),
        new("miss", "Lim/missed_open", "missed_open", new[] { 595616649, 223685125, 633842281 }, "miss.wav",
            null, null, "lim_missed_played"),
        new("ready_quiet", "Lim/click_bark_quiet", "click_bark_quiet", new[] { 189141870, 291105099, 1562730311 }, "ready_quiet.wav"),
        new("ready_shouty", "Lim/click_bark_shouty", "click_bark_shouty", new[] { 401409205, 1033008030, 308349311, 918749289, 972975437, 767067942, 101976164, 355717366, 710938811, 1800348056 }, "ready_shouty.wav"),
        new("ready_suppressed", "Lim/click_bark_suppressed", "click_bark_suppressed", new[] { 1821547188, 1887060936 }, "ready_suppressed.wav"),
        new("ready_hurt", "Lim/click_hurt", "click_hurt", new[] { 626234054, 462094842, 1618230852 }, "ready_hurt.wav"),
        new("after_kills", "Lim/click_after_killing_x_enemies", "click_after_killing", new[] { 776144351, 633223830, 76172676, 456512450, 1781182962, 1375725410, 308349311 }, "after_kills.wav"),
        new("enemy_hidden", "Lim/click_enemy_not_seen_for_x_turns", "click_enemy_hidden", new[] { 1834900971, 1391980550 }, "enemy_hidden.wav"),
        new("objective_threat", "Lim/click_enemy_threatening_obj_zone1", "click_objective_threat", new[] { 673678138 }, "objective_threat.wav"),
        new("many_enemies", "Lim/click_lot_of_enemies_visible", "click_many_enemies", new[] { 319688596 }, "many_enemies.wav"),
        new("enemy_trees", "Lim/click_bark_enemy_in_trees", "enemy_in_trees", new[] { 1645106339, 1351377185 }, "enemy_trees.wav"),
        new("last_click", "Lim/click_bark_last", "click_last", new[] { 1342617602 }, "last_click.wav"),
        new("move_quiet", "Lim/moving_lim_quietly", "moving_quiet", new[] { 836306028, 692284477 }, "move_quiet.wav"),
        new("move_shout", "Lim/moving_lim_shouty", "moving_shout", new[] { 633223830, 76172676, 1781182962, 1375725410, 710938811, 101976164, 1992384521 }, "move_shout.wav"),
        new("move_hurt", "Lim/moving_lim_weakened", "moving_hurt", new[] { 941242775, 703051116 }, "move_hurt.wav"),
        new("move_cover", "Lim/moving_into_cover_lim", "moving_cover", new[] { 130226873, 1887482926, 1625034991 }, "move_cover.wav"),
        new("move_cover_done", "Lim/moving_into_cover_finished_lim", "moving_cover_done", new[] { 87084287, 1560820273, 995624230 }, "move_cover_done.wav"),
        new("move_done_quiet", "Lim/moving_finished_quietly", "moving_finished_quiet", new[] { 2099420847, 1889224656 }, "move_done_quiet.wav"),
        new("move_done_hurt", "Lim/moving_finished_weakened", "moving_finished_hurt", new[] { 1624596341, 1769218732 }, "move_done_hurt.wav"),
        new("spotted_quiet", "Lim/enemy_sighted_lim_quietly", "enemy_sighted_quiet", new[] { 1802028948 }, "spotted_quiet.wav"),
        new("spotted_heavy", "Lim/enemy_sighted_heavy", "enemy_sighted_heavy", new[] { 1647801155 }, "spotted_heavy.wav"),
        new("spotted_sniper", "Lim/enemy_sighted_sniper", "enemy_sighted_sniper", new[] { 223872832 }, "spotted_sniper.wav"),
        new("spotted_turret", "Lim/enemy_sighted_turret", "enemy_sighted_turret", new[] { 1737981063 }, "spotted_turret.wav"),
        new("spotted_vehicle", "Lim/enemy_sighted_vehicle", "enemy_sighted_vehicle", new[] { 2124631340 }, "spotted_vehicle.wav"),
        new("spotted_weapons", "Lim/enemy_sighted_weapons_team", "enemy_sighted_weapons", new[] { 1844114502 }, "spotted_weapons.wav"),
        new("spotted_emp", "Lim/enemy_sighted_emp", "enemy_sighted_emp", new[] { 176825631 }, "spotted_emp.wav"),
        new("spotted_bug", "Lim/enemy_sighted_bugs11", "enemy_sighted_bug", new[] { 183941083, 168643344 }, "spotted_bug.wav"),
        new("spotted_hurt", "Lim/enemy_sighted_weakened", "enemy_sighted_hurt", new[] { 93170995, 802371045 }, "spotted_hurt.wav"),
        new("enemy_damaged", "Lim/enemy_damaged", "enemy_damaged", new[] { 784724246, 1659736066, 1128336699, 5071716, 57039452 }, "enemy_damaged.wav"),
        new("enemy_vehicle_damaged", "Lim/enemy_damaged_vehicle", "enemy_vehicle_damaged", new[] { 909073691, 362259811, 2068391239 }, "enemy_vehicle_damaged.wav"),
        new("enemy_blocked", "Lim/enemy_damage_blocked", "enemy_damage_blocked", new[] { 760444281, 1783722124 }, "enemy_blocked.wav"),
        new("enemy_suppressed", "Lim/enemy_suppressed", "enemy_suppressed", new[] { 908514569, 1307516728, 775777655 }, "enemy_suppressed.wav"),
        new("kill_heavy", "Lim/enemy_killed_heavy", "enemy_killed_heavy", new[] { 2040024293 }, "kill_heavy.wav"),
        new("kill_sniper", "Lim/enemy_killed_sniper", "enemy_killed_sniper", new[] { 2117001390 }, "kill_sniper.wav"),
        new("kill_turret", "Lim/enemy_killed_turret", "enemy_killed_turret", new[] { 2111355758 }, "kill_turret.wav"),
        new("kill_vehicle", "Lim/enemy_killed_vehicle", "enemy_killed_vehicle", new[] { 1215532407 }, "kill_vehicle.wav"),
        new("kill_weapons", "Lim/enemy_killed_weapons_team", "enemy_killed_weapons", new[] { 1797995275 }, "kill_weapons.wav"),
        new("kill_bugs", "Lim/enemy_killed_bugs111", "enemy_killed_bugs", new[] { 1451178184, 465459578, 355717366, 101976164 }, "kill_bugs.wav"),
        new("kill_hurt", "Lim/enemy_killed_weakened", "enemy_killed_hurt", new[] { 64231980, 1653991268 }, "kill_hurt.wav"),
        new("construct_damaged", "Lim/construct_bodypart_damaged", "construct_damaged", new[] { 434444061, 692544867 }, "construct_damaged.wav"),
        new("construct_spotted", "Lim/construct_sighted_tier1", "construct_sighted", new[] { 144780424, 715015166, 18249344, 1313643241 }, "construct_spotted.wav"),
        new("construct_boss", "Lim/construct_sighted_boss", "construct_boss", new[] { 1450030992 }, "construct_boss.wav"),
        new("construct_master", "Lim/construct_sighted_master", "construct_master", new[] { 624585361 }, "construct_master.wav"),
        new("construct_killed", "Lim/construct_killed", "construct_killed", new[] { 2052386109, 144731579, 1160053574, 782882000 }, "construct_killed.wav"),
        new("construct_killed_boss", "Lim/construct_killed_boss", "construct_killed_boss", new[] { 887154227 }, "construct_killed_boss.wav"),
        new("construct_killed_master", "Lim/construct_killed_master", "construct_killed_master", new[] { 1310622399 }, "construct_killed_master.wav"),
        new("miss_twice", "Lim/missed_open_twice_in_a_row", "missed_twice", new[] { 761591473 }, "miss_twice.wav"),
        new("miss_cover", "Lim/missed_cover", "missed_cover", new[] { 796138324, 1980195970, 41619854 }, "miss_cover.wav"),
        new("taking_fire_hurt", "Lim/taking_fire_weakened", "taking_fire_hurt", new[] { 568214604, 2037714324 }, "taking_fire_hurt.wav"),
        new("ally_taking_fire", "Lim/response_taking_fire_anyone", "ally_taking_fire", new[] { 853875640, 1429925997 }, "ally_taking_fire.wav"),
        new("ally_suppressed", "Lim/response_allied_suppressed", "ally_suppressed", new[] { 1144476626, 1688757644 }, "ally_suppressed.wav"),
        new("morale_wavering", "Lim/morale_wavering", "morale_wavering", new[] { 1020891250, 88342915 }, "morale_wavering.wav"),
        new("morale_fleeing", "Lim/morale_fleeing", "morale_fleeing", new[] { 815206503, 1695863955 }, "morale_fleeing.wav"),
        new("morale_recovery", "Lim/morale_recovery", "morale_recovery", new[] { 1793158050, 479790871 }, "morale_recovery.wav"),
        new("last_man", "Lim/last_man_standing", "last_man", new[] { 1891565608 }, "last_man.wav"),
        new("squad_casualty", "Lim/squaddie_element_lim", "squad_casualty", new[] { 870013219, 7304847, 143556171, 1453971708, 1087487326 }, "squad_casualty.wav"),
        new("ally_dead", "Lim/response_death_anyone", "ally_dead", new[] { 312897920 }, "ally_dead.wav"),
        new("rescue", "Lim/response_rescue_anyone", "rescue", new[] { 2107315943 }, "rescue.wav"),
        new("success", "Lim/success_any", "success", new[] { 1612829282, 1349596, 243450506, 460668199, 136756886 }, "success.wav"),
        new("grenade_frag", "Lim/special_grenade_frag", "grenade_frag", new[] { 2072262307 }, "grenade_frag.wav"),
        new("suppress_fire", "Lim/special_lmg_suppression", "suppress_fire", new[] { 1406596249 }, "suppress_fire.wav"),
        new("rocket", "Lim/special_rocket", "rocket", new[] { 1265789444 }, "rocket.wav"),
        new("emp_success", "Lim/emp_success", "emp_success", new[] { 740076754 }, "emp_success.wav"),
        new("emp_failed", "Lim/emp_failed", "emp_failed", new[] { 1464585855 }, "emp_failed.wav"),
        new("danger_close", "Lim/danger_close", "danger_close", new[] { 1105262917 }, "danger_close.wav"),
        new("offmap", "Lim/offmap_response", "offmap", new[] { 343033985, 75898795 }, "offmap.wav"),
        new("massive_explosion", "Lim/massive_explosion", "massive_explosion", new[] { 593504437 }, "massive_explosion.wav"),
        new("enemy_fleeing", "Lim/enemy_fleeing", "enemy_fleeing", new[] { 1124517750 }, "enemy_fleeing.wav"),
        new("idle_combat", "Lim/idle_bark_combat", "idle_combat", new[] { 725132608 }, "idle_combat.wav"),
        new("idle_combat2", "Lim/idle_bark_combat1", "idle_combat2", new[] { 1740524068 }, "idle_combat2.wav"),
        new("arrival", "Lim/arrival_lim", "arrival", new[] { 613452459, 972633271 }, "arrival.wav"),
        new("building_destroyed", "Lim/building_destroyed", "building_destroyed", new[] { 1930547573, 82481324, 820150862 }, "building_destroyed.wav"),
    };


    // One subtitle per MENACE Sound route. Every AudioClip variation attached to the
    // route is an alternate recording of this exact sentence, so audio and text cannot diverge.
    private static readonly Dictionary<string, string> RouteSubtitles = new(StringComparer.Ordinal)
    {
        ["john|ready|0"] = "Yes, sir.",
        ["john|moving|0"] = "Alright, let's move!",
        ["john|moving|1"] = "Alright, let's move.",
        ["john|moving|2"] = "Let's move!",
        ["john|move_done|0"] = "Right.",
        ["john|move_done|1"] = "Stay sharp.",
        ["john|move_done|2"] = "I've got this.",
        ["john|spotted|0"] = "We got contact!",
        ["john|spotted|1"] = "We got company!",
        ["john|spotted|2"] = "Heads up!",
        ["john|spotted|3"] = "We got contact!",
        ["john|taking_fire|0"] = "I'm taking fire.",
        ["john|taking_fire|1"] = "Taking heavy fire!",
        ["john|taking_fire|2"] = "Taking heavy fire.",
        ["john|taking_fire|3"] = "Take cover!",
        ["john|kill|0"] = "Got'em.",
        ["john|kill|1"] = "I got him.",
        ["john|kill|2"] = "Stay dead.",
        ["john|kill|3"] = "Too slow.",
        ["john|kill|4"] = "Who's next?",
        ["john|kill|5"] = "Consider that practice.",
        ["john|suppressed|0"] = "Might need a little backup.",
        ["john|suppressed|1"] = "If you're up for it, I could use some help.",
        ["john|suppressed|2"] = "Taking heavy fire!",
        ["john|miss|0"] = "Negative. I'll retarget.",
        ["john|miss|1"] = "Dammit!",
        ["john|miss|2"] = "Dammit.",
        ["john|ready_quiet|0"] = "Roger that.",
        ["john|ready_quiet|1"] = "Stay sharp.",
        ["john|ready_quiet|2"] = "Weapons ready.",
        ["john|ready_shouty|0"] = "Expect trouble.",
        ["john|ready_shouty|1"] = "Let's do this.",
        ["john|ready_shouty|2"] = "I've got this!",
        ["john|ready_shouty|3"] = "I'll handle it.",
        ["john|ready_shouty|4"] = "I'm game. Let's finish this.",
        ["john|ready_shouty|5"] = "Let's make this quick!",
        ["john|ready_shouty|6"] = "Weapons ready.",
        ["john|ready_shouty|7"] = "Stay sharp.",
        ["john|ready_shouty|8"] = "On it!",
        ["john|ready_shouty|9"] = "Let's do this.",
        ["john|ready_suppressed|0"] = "I'm taking fire.",
        ["john|ready_suppressed|1"] = "Taking heavy fire!",
        ["john|ready_hurt|0"] = "I'm hit.",
        ["john|ready_hurt|1"] = "Ugh!",
        ["john|ready_hurt|2"] = "Oof!",
        ["john|after_kills|0"] = "Don't let up!",
        ["john|after_kills|1"] = "Finish them off!",
        ["john|after_kills|2"] = "Keep the pressure up!",
        ["john|after_kills|3"] = "Keep up the pressure!",
        ["john|after_kills|4"] = "Stay focused. This isn't over yet.",
        ["john|after_kills|5"] = "I'm just getting started.",
        ["john|after_kills|6"] = "Who's next?",
        ["john|enemy_hidden|0"] = "Everyone hear that?",
        ["john|enemy_hidden|1"] = "I don't like the sound of that.",
        ["john|objective_threat|0"] = "Nobody gets past us, clear?",
        ["john|many_enemies|0"] = "Heads up! We've got more incoming!",
        ["john|enemy_trees|0"] = "We got contact!",
        ["john|enemy_trees|1"] = "We got company!",
        ["john|last_click|0"] = "Again?",
        ["john|move_quiet|0"] = "On my way.",
        ["john|move_quiet|1"] = "I'm on my way.",
        ["john|move_shout|0"] = "GO! GO! GO!",
        ["john|move_shout|1"] = "Go, go, go!",
        ["john|move_shout|2"] = "Move, move, move!",
        ["john|move_shout|3"] = "Double time, people!",
        ["john|move_shout|4"] = "Okay, double time!",
        ["john|move_shout|5"] = "I'm going!",
        ["john|move_shout|6"] = "Push! Push forward!",
        ["john|move_hurt|0"] = "I just gotta keep moving.",
        ["john|move_hurt|1"] = "I'm hit.",
        ["john|move_cover|0"] = "Get to cover!",
        ["john|move_cover|1"] = "Take cover!",
        ["john|move_cover|2"] = "Find cover!",
        ["john|move_cover_done|0"] = "Stay sharp.",
        ["john|move_cover_done|1"] = "Got it.",
        ["john|move_cover_done|2"] = "I've got this.",
        ["john|move_done_quiet|0"] = "Stay sharp.",
        ["john|move_done_quiet|1"] = "Weapons ready.",
        ["john|move_done_hurt|0"] = "I'm fine.",
        ["john|move_done_hurt|1"] = "I'm okay.",
        ["john|spotted_quiet|0"] = "We got contact!",
        ["john|spotted_heavy|0"] = "Heads up!",
        ["john|spotted_sniper|0"] = "We got contact!",
        ["john|spotted_turret|0"] = "We got contact!",
        ["john|spotted_vehicle|0"] = "We got company!",
        ["john|spotted_weapons|0"] = "We got contact!",
        ["john|spotted_emp|0"] = "Heads up!",
        ["john|spotted_bug|0"] = "We got company!",
        ["john|spotted_bug|1"] = "We got contact!",
        ["john|spotted_hurt|0"] = "We got contact!",
        ["john|spotted_hurt|1"] = "Heads up!",
        ["john|enemy_damaged|0"] = "That had to hurt.",
        ["john|enemy_damaged|1"] = "That should slow them down.",
        ["john|enemy_damaged|2"] = "That pain you feel is me kicking your ass.",
        ["john|enemy_damaged|3"] = "That had to hurt.",
        ["john|enemy_damaged|4"] = "That should slow them down.",
        ["john|enemy_vehicle_damaged|0"] = "That should slow them down.",
        ["john|enemy_vehicle_damaged|1"] = "That had to hurt.",
        ["john|enemy_vehicle_damaged|2"] = "That should slow them down.",
        ["john|enemy_blocked|0"] = "Negative. I'll retarget.",
        ["john|enemy_blocked|1"] = "Dammit.",
        ["john|enemy_suppressed|0"] = "That should slow them down.",
        ["john|enemy_suppressed|1"] = "Stay in cover. I'll finish this thing off.",
        ["john|enemy_suppressed|2"] = "That should slow them down.",
        ["john|kill_heavy|0"] = "Too slow.",
        ["john|kill_sniper|0"] = "I got him.",
        ["john|kill_turret|0"] = "Too slow.",
        ["john|kill_vehicle|0"] = "There we go.",
        ["john|kill_weapons|0"] = "Who's next?",
        ["john|kill_bugs|0"] = "Stay dead.",
        ["john|kill_bugs|1"] = "Got'em.",
        ["john|kill_bugs|2"] = "Too slow.",
        ["john|kill_bugs|3"] = "Stay dead.",
        ["john|kill_hurt|0"] = "I'm just getting started.",
        ["john|kill_hurt|1"] = "Stay dead.",
        ["john|construct_damaged|0"] = "That should slow them down.",
        ["john|construct_damaged|1"] = "That had to hurt.",
        ["john|construct_spotted|0"] = "We got contact!",
        ["john|construct_spotted|1"] = "We got company!",
        ["john|construct_spotted|2"] = "Heads up!",
        ["john|construct_spotted|3"] = "Here they come!",
        ["john|construct_boss|0"] = "Heads up!",
        ["john|construct_master|0"] = "Heads up!",
        ["john|construct_killed|0"] = "Stay dead.",
        ["john|construct_killed|1"] = "There we go.",
        ["john|construct_killed|2"] = "Too slow.",
        ["john|construct_killed|3"] = "Who's next?",
        ["john|construct_killed_boss|0"] = "There we go.",
        ["john|construct_killed_master|0"] = "There we go.",
        ["john|miss_twice|0"] = "Dammit!",
        ["john|miss_cover|0"] = "Negative. I'll retarget.",
        ["john|miss_cover|1"] = "Can't get a lock.",
        ["john|miss_cover|2"] = "Dammit.",
        ["john|taking_fire_hurt|0"] = "I'm hit.",
        ["john|taking_fire_hurt|1"] = "I'm taking fire.",
        ["john|ally_taking_fire|0"] = "Take cover!",
        ["john|ally_taking_fire|1"] = "Find cover!",
        ["john|ally_suppressed|0"] = "Go, I'll cover you.",
        ["john|ally_suppressed|1"] = "Give us some cover fire!",
        ["john|morale_wavering|0"] = "Not good.",
        ["john|morale_wavering|1"] = "Ah, hell.",
        ["john|morale_fleeing|0"] = "I need an evac right now!",
        ["john|morale_fleeing|1"] = "I need a medical evac right now!",
        ["john|morale_recovery|0"] = "I'm fine.",
        ["john|morale_recovery|1"] = "I'm okay.",
        ["john|last_man|0"] = "I've got this.",
        ["john|squad_casualty|0"] = "No!",
        ["john|squad_casualty|1"] = "Dammit!",
        ["john|squad_casualty|2"] = "Damn.",
        ["john|squad_casualty|3"] = "Unacceptable.",
        ["john|squad_casualty|4"] = "No!",
        ["john|ally_dead|0"] = "No!",
        ["john|rescue|0"] = "You're clear! Go!",
        ["john|success|0"] = "That's more like it.",
        ["john|success|1"] = "There we go.",
        ["john|success|2"] = "That's more like it.",
        ["john|success|3"] = "There we go.",
        ["john|success|4"] = "That's more like it.",
        ["john|grenade_frag|0"] = "Fire in the hole!",
        ["john|suppress_fire|0"] = "Firing!",
        ["john|rocket|0"] = "Right, let's blow it up.",
        ["john|emp_success|0"] = "There we go.",
        ["john|emp_failed|0"] = "Can't get a lock.",
        ["john|danger_close|0"] = "Stay clear, I've got this!",
        ["john|offmap|0"] = "Acknowledged.",
        ["john|offmap|1"] = "Copy that.",
        ["john|massive_explosion|0"] = "Woah!",
        ["john|enemy_fleeing|0"] = "Finish them off!",
        ["john|idle_combat|0"] = "Stay sharp.",
        ["john|idle_combat2|0"] = "Let's do this.",
        ["john|arrival|0"] = "Let's do this.",
        ["john|arrival|1"] = "Okay everyone, weapons at the ready.",
        ["john|building_destroyed|0"] = "There we go.",
        ["john|building_destroyed|1"] = "That's more like it.",
        ["john|building_destroyed|2"] = "Woah!",
        ["jane|ready|0"] = "Yes, sir.",
        ["jane|moving|0"] = "Alright, let's move!",
        ["jane|moving|1"] = "Alright, let's move.",
        ["jane|moving|2"] = "Let's move!",
        ["jane|move_done|0"] = "Right.",
        ["jane|move_done|1"] = "Stay sharp.",
        ["jane|move_done|2"] = "I've got this.",
        ["jane|spotted|0"] = "We got contact!",
        ["jane|spotted|1"] = "We got company!",
        ["jane|spotted|2"] = "Heads up!",
        ["jane|spotted|3"] = "We got contact!",
        ["jane|taking_fire|0"] = "I'm taking fire.",
        ["jane|taking_fire|1"] = "Taking heavy fire!",
        ["jane|taking_fire|2"] = "Taking heavy fire.",
        ["jane|taking_fire|3"] = "Take cover!",
        ["jane|kill|0"] = "Got him.",
        ["jane|kill|1"] = "I got him.",
        ["jane|kill|2"] = "Stay dead.",
        ["jane|kill|3"] = "Too slow.",
        ["jane|kill|4"] = "Who's next?",
        ["jane|kill|5"] = "Consider that practice.",
        ["jane|suppressed|0"] = "Might need a little backup.",
        ["jane|suppressed|1"] = "Taking heavy fire!",
        ["jane|suppressed|2"] = "I'm taking fire.",
        ["jane|miss|0"] = "Negative. I'll retarget.",
        ["jane|miss|1"] = "Dammit!",
        ["jane|miss|2"] = "Dammit.",
        ["jane|ready_quiet|0"] = "Roger that.",
        ["jane|ready_quiet|1"] = "Stay sharp.",
        ["jane|ready_quiet|2"] = "Weapons ready.",
        ["jane|ready_shouty|0"] = "Expect trouble.",
        ["jane|ready_shouty|1"] = "Let's do this.",
        ["jane|ready_shouty|2"] = "I've got this!",
        ["jane|ready_shouty|3"] = "I'll handle it.",
        ["jane|ready_shouty|4"] = "I'm game. Let's finish this.",
        ["jane|ready_shouty|5"] = "Let's make this quick!",
        ["jane|ready_shouty|6"] = "Weapons ready.",
        ["jane|ready_shouty|7"] = "Stay sharp.",
        ["jane|ready_shouty|8"] = "On it.",
        ["jane|ready_shouty|9"] = "Let's do this.",
        ["jane|ready_suppressed|0"] = "I'm taking fire.",
        ["jane|ready_suppressed|1"] = "Taking heavy fire!",
        ["jane|ready_hurt|0"] = "I'm hit.",
        ["jane|ready_hurt|1"] = "Ugh!",
        ["jane|ready_hurt|2"] = "Argh!",
        ["jane|after_kills|0"] = "Don't let up!",
        ["jane|after_kills|1"] = "Finish them off!",
        ["jane|after_kills|2"] = "Keep the pressure up!",
        ["jane|after_kills|3"] = "Keep up the pressure!",
        ["jane|after_kills|4"] = "Stay focused. This isn't over yet.",
        ["jane|after_kills|5"] = "I'm just getting started.",
        ["jane|after_kills|6"] = "Who's next?",
        ["jane|enemy_hidden|0"] = "Everyone hear that?",
        ["jane|enemy_hidden|1"] = "I don't like the sound of that.",
        ["jane|objective_threat|0"] = "Hold position.",
        ["jane|many_enemies|0"] = "Heads up! We got more incoming!",
        ["jane|enemy_trees|0"] = "We got contact!",
        ["jane|enemy_trees|1"] = "We got company!",
        ["jane|last_click|0"] = "Again?",
        ["jane|move_quiet|0"] = "On my way.",
        ["jane|move_quiet|1"] = "I'm on my way.",
        ["jane|move_shout|0"] = "GO! GO! GO!",
        ["jane|move_shout|1"] = "Go, go, go!",
        ["jane|move_shout|2"] = "Move, move, move!",
        ["jane|move_shout|3"] = "Double time, people!",
        ["jane|move_shout|4"] = "Okay, double time!",
        ["jane|move_shout|5"] = "I'm going!",
        ["jane|move_shout|6"] = "Push! Push forward!",
        ["jane|move_hurt|0"] = "I'm hit.",
        ["jane|move_hurt|1"] = "Ugh!",
        ["jane|move_cover|0"] = "Get to cover!",
        ["jane|move_cover|1"] = "Take cover!",
        ["jane|move_cover|2"] = "Find cover!",
        ["jane|move_cover_done|0"] = "Stay sharp.",
        ["jane|move_cover_done|1"] = "Got it.",
        ["jane|move_cover_done|2"] = "I've got this.",
        ["jane|move_done_quiet|0"] = "Stay sharp.",
        ["jane|move_done_quiet|1"] = "Weapons ready.",
        ["jane|move_done_hurt|0"] = "I'm fine.",
        ["jane|move_done_hurt|1"] = "I'm okay.",
        ["jane|spotted_quiet|0"] = "We got contact!",
        ["jane|spotted_heavy|0"] = "Heads up!",
        ["jane|spotted_sniper|0"] = "We got contact!",
        ["jane|spotted_turret|0"] = "We got contact!",
        ["jane|spotted_vehicle|0"] = "We got company!",
        ["jane|spotted_weapons|0"] = "We got contact!",
        ["jane|spotted_emp|0"] = "Heads up!",
        ["jane|spotted_bug|0"] = "We got company!",
        ["jane|spotted_bug|1"] = "We got contact!",
        ["jane|spotted_hurt|0"] = "We got contact!",
        ["jane|spotted_hurt|1"] = "Heads up!",
        ["jane|enemy_damaged|0"] = "That pain you feel is me kicking your ass.",
        ["jane|enemy_damaged|1"] = "That pain you feel is me kicking your ass.",
        ["jane|enemy_damaged|2"] = "That pain you feel is me kicking your ass.",
        ["jane|enemy_damaged|3"] = "That pain you feel is me kicking your ass.",
        ["jane|enemy_damaged|4"] = "That pain you feel is me kicking your ass.",
        ["jane|enemy_vehicle_damaged|0"] = "I should slow them down.",
        ["jane|enemy_vehicle_damaged|1"] = "I should slow them down.",
        ["jane|enemy_vehicle_damaged|2"] = "I should slow them down.",
        ["jane|enemy_blocked|0"] = "Negative. I'll retarget.",
        ["jane|enemy_blocked|1"] = "Dammit.",
        ["jane|enemy_suppressed|0"] = "I should slow them down.",
        ["jane|enemy_suppressed|1"] = "Stay in cover. I'll finish this thing off.",
        ["jane|enemy_suppressed|2"] = "I should slow them down.",
        ["jane|kill_heavy|0"] = "Got him.",
        ["jane|kill_sniper|0"] = "Got him.",
        ["jane|kill_turret|0"] = "Too slow.",
        ["jane|kill_vehicle|0"] = "There we go.",
        ["jane|kill_weapons|0"] = "Got him.",
        ["jane|kill_bugs|0"] = "Stay dead.",
        ["jane|kill_bugs|1"] = "Got him.",
        ["jane|kill_bugs|2"] = "Too slow.",
        ["jane|kill_bugs|3"] = "Stay dead.",
        ["jane|kill_hurt|0"] = "Got him.",
        ["jane|kill_hurt|1"] = "I'm just getting started.",
        ["jane|construct_damaged|0"] = "I should slow them down.",
        ["jane|construct_damaged|1"] = "I should slow them down.",
        ["jane|construct_spotted|0"] = "We got contact!",
        ["jane|construct_spotted|1"] = "We got company!",
        ["jane|construct_spotted|2"] = "Heads up!",
        ["jane|construct_spotted|3"] = "Here they come!",
        ["jane|construct_boss|0"] = "Heads up!",
        ["jane|construct_master|0"] = "Heads up!",
        ["jane|construct_killed|0"] = "Stay dead.",
        ["jane|construct_killed|1"] = "There we go.",
        ["jane|construct_killed|2"] = "Too slow.",
        ["jane|construct_killed|3"] = "Who's next?",
        ["jane|construct_killed_boss|0"] = "There we go.",
        ["jane|construct_killed_master|0"] = "There we go.",
        ["jane|miss_twice|0"] = "Dammit!",
        ["jane|miss_cover|0"] = "Negative. I'll retarget.",
        ["jane|miss_cover|1"] = "Can't get a lock.",
        ["jane|miss_cover|2"] = "Dammit.",
        ["jane|taking_fire_hurt|0"] = "I'm hit.",
        ["jane|taking_fire_hurt|1"] = "I'm taking fire.",
        ["jane|ally_taking_fire|0"] = "Take cover!",
        ["jane|ally_taking_fire|1"] = "Find cover!",
        ["jane|ally_suppressed|0"] = "Go, I'll cover you.",
        ["jane|ally_suppressed|1"] = "Give us some cover fire!",
        ["jane|morale_wavering|0"] = "Not good.",
        ["jane|morale_wavering|1"] = "Ah, hell.",
        ["jane|morale_fleeing|0"] = "I need an evac right now!",
        ["jane|morale_fleeing|1"] = "I need a medical evac right now!",
        ["jane|morale_recovery|0"] = "I'm fine.",
        ["jane|morale_recovery|1"] = "I'm okay.",
        ["jane|last_man|0"] = "I've got this.",
        ["jane|squad_casualty|0"] = "No!",
        ["jane|squad_casualty|1"] = "Dammit!",
        ["jane|squad_casualty|2"] = "Damn.",
        ["jane|squad_casualty|3"] = "Unacceptable.",
        ["jane|squad_casualty|4"] = "No!",
        ["jane|ally_dead|0"] = "No!",
        ["jane|rescue|0"] = "You're clear! Go!",
        ["jane|success|0"] = "That's more like it.",
        ["jane|success|1"] = "There we go.",
        ["jane|success|2"] = "That's more like it.",
        ["jane|success|3"] = "There we go.",
        ["jane|success|4"] = "That's more like it.",
        ["jane|grenade_frag|0"] = "Fire in the hole!",
        ["jane|suppress_fire|0"] = "Firing!",
        ["jane|rocket|0"] = "Right, let's blow it up.",
        ["jane|emp_success|0"] = "There we go.",
        ["jane|emp_failed|0"] = "Can't get a lock.",
        ["jane|danger_close|0"] = "Stay clear, I've got this!",
        ["jane|offmap|0"] = "Acknowledged.",
        ["jane|offmap|1"] = "Copy that.",
        ["jane|massive_explosion|0"] = "Whoa!",
        ["jane|enemy_fleeing|0"] = "Finish them off!",
        ["jane|idle_combat|0"] = "Stay sharp.",
        ["jane|idle_combat2|0"] = "Let's do this.",
        ["jane|arrival|0"] = "Let's do this.",
        ["jane|arrival|1"] = "Okay everyone, weapons at the ready.",
        ["jane|building_destroyed|0"] = "There we go.",
        ["jane|building_destroyed|1"] = "That's more like it.",
        ["jane|building_destroyed|2"] = "Whoa!",
    };

    private static readonly Dictionary<string, string> BarkSubtitles = new(StringComparer.Ordinal)
    {
        ["ready"] = "Ready.",
        ["moving"] = "Moving.",
        ["move_done"] = "In position.",
        ["spotted"] = "Enemy spotted.",
        ["taking_fire"] = "Taking fire!",
        ["kill"] = "Target down.",
        ["suppressed"] = "Pinned down!",
        ["miss"] = "Missed.",
        ["ready_quiet"] = "Ready.",
        ["ready_shouty"] = "Ready!",
        ["ready_suppressed"] = "Pinned down!",
        ["ready_hurt"] = "I'm hurt.",
        ["after_kills"] = "Keep the pressure on!",
        ["enemy_hidden"] = "Something's out there.",
        ["objective_threat"] = "Watch the objective!",
        ["many_enemies"] = "More enemies incoming!",
        ["enemy_trees"] = "Enemy in cover.",
        ["last_click"] = "Yeah?",
        ["move_quiet"] = "Moving quietly.",
        ["move_shout"] = "Moving!",
        ["move_hurt"] = "Moving, wounded.",
        ["move_cover"] = "Moving to cover.",
        ["move_cover_done"] = "In cover.",
        ["move_done_quiet"] = "In position.",
        ["move_done_hurt"] = "In position, wounded.",
        ["spotted_quiet"] = "Enemy spotted.",
        ["spotted_heavy"] = "Heavy enemy spotted.",
        ["spotted_sniper"] = "Sniper spotted.",
        ["spotted_turret"] = "Turret spotted.",
        ["spotted_vehicle"] = "Vehicle spotted.",
        ["spotted_weapons"] = "Weapons team spotted.",
        ["spotted_emp"] = "EMP threat spotted.",
        ["spotted_bug"] = "Hostile creature spotted.",
        ["spotted_hurt"] = "Enemy spotted.",
        ["enemy_damaged"] = "Target hit.",
        ["enemy_vehicle_damaged"] = "Vehicle hit.",
        ["enemy_blocked"] = "Attack ineffective.",
        ["enemy_suppressed"] = "Enemy suppressed.",
        ["kill_heavy"] = "Heavy target down.",
        ["kill_sniper"] = "Sniper down.",
        ["kill_turret"] = "Turret destroyed.",
        ["kill_vehicle"] = "Vehicle destroyed.",
        ["kill_weapons"] = "Weapons team down.",
        ["kill_bugs"] = "Hostile creature down.",
        ["kill_hurt"] = "Target down.",
        ["construct_damaged"] = "Construct damaged.",
        ["construct_spotted"] = "Construct spotted.",
        ["construct_boss"] = "Major construct spotted.",
        ["construct_master"] = "Major construct spotted.",
        ["construct_killed"] = "Construct destroyed.",
        ["construct_killed_boss"] = "Major construct destroyed.",
        ["construct_killed_master"] = "Major construct destroyed.",
        ["miss_twice"] = "Missed again.",
        ["miss_cover"] = "Target still in cover.",
        ["taking_fire_hurt"] = "Taking fire, wounded!",
        ["ally_taking_fire"] = "Ally under fire!",
        ["ally_suppressed"] = "Ally suppressed!",
        ["morale_wavering"] = "Losing nerve.",
        ["morale_fleeing"] = "Panicking!",
        ["morale_recovery"] = "Regaining composure.",
        ["last_man"] = "Last one standing.",
        ["squad_casualty"] = "Casualty!",
        ["ally_dead"] = "Ally down.",
        ["rescue"] = "Get them out!",
        ["success"] = "Objective complete.",
        ["grenade_frag"] = "Frag out!",
        ["suppress_fire"] = "Suppressing fire!",
        ["rocket"] = "Rocket away!",
        ["emp_success"] = "EMP successful.",
        ["emp_failed"] = "EMP failed.",
        ["danger_close"] = "Danger close!",
        ["offmap"] = "Fire support acknowledged.",
        ["massive_explosion"] = "Explosion!",
        ["enemy_fleeing"] = "Enemy retreating.",
        ["idle_combat"] = "Combat ready.",
        ["idle_combat2"] = "Combat ready.",
        ["arrival"] = "Weapons ready.",
        ["building_destroyed"] = "Structure destroyed.",
    };


    private readonly string _modpackRoot;
    private static FieldVoiceSystem _instance;
    private string _voiceStage = "not initialized";
    private string _failure = "not prepared";
    private bool _voicesReady;
    private bool _prepareAttempted;
    private bool _patchInstalled;
    private object _harmony;
    private RuntimeVoiceBundle _voiceBundleAudio;
    private IReadOnlyDictionary<string, AudioClip> _voiceClips;
    private readonly Dictionary<string, string[]> _voiceRouteAssets = new(StringComparer.Ordinal);
    private Type _soundBankType;
    private Type _soundManagerType;
    private Type _conversationTemplateType;
    private object _loadedConversationAssets;
    private readonly HashSet<IntPtr> _voiceInjectedManagers = new();
    private readonly List<VoiceProfile> _voiceProfiles = CreateVoiceProfiles();
    private static readonly HashSet<Type> NativeRecordAllocationTypes = new();
    private readonly HashSet<string> _loggedErrors = new(StringComparer.Ordinal);

    internal bool Ready => _voicesReady && _patchInstalled;
    internal string Stage => Ready ? "John+Jane ready" : _failure;
    internal static int ConversationsPerGender => VoiceBarks.Length;
    internal static int SoundRoutesPerGender => TotalVoiceSounds;
    internal static int SourceWavCount => 429;

    internal FieldVoiceSystem(string modFolder)
    {
        _modpackRoot = modFolder;
        _instance = this;
    }

    internal void UnpatchOnUnload()
    {
        if (_harmony != null)
        {
            try { _harmony.GetType().GetMethod("UnpatchSelf", Type.EmptyTypes)?.Invoke(_harmony, null); }
            catch { /* game is closing or other mods still hold the runtime */ }
        }
        _patchInstalled = false;
        if (ReferenceEquals(_instance, this)) _instance = null;
    }

    internal bool IsBundlePresent()
    {
        string dir = Path.Combine(_modpackRoot, "runtime_assets", "voice");
        return File.Exists(Path.Combine(dir, RuntimeVoiceBundle.BundleFile)) &&
               File.Exists(Path.Combine(dir, RuntimeVoiceBundle.ReceiptFile)) &&
               File.Exists(Path.Combine(dir, "voice_routes.json"));
    }

    internal bool TryPrepare(out string status)
    {
        if (Ready) { status = "john+jane:ready"; return true; }
        if (_prepareAttempted) { status = _failure; return false; }
        if (!IsBundlePresent())
        {
            status = "bundle missing; run Build-Voice-Bundle.cmd first";
            _failure = status;
            return false;
        }
        _prepareAttempted = true;
        try
        {
            if (!TrySetupVoices())
            {
                status = _failure;
                return false;
            }
            _voiceStage = "install tactical conversation patch";
            InstallConversationManagerPatch();
            _voicesReady = true;
            status = "john+jane:ready";
            _failure = status;
            return true;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            status = _failure = _voiceStage + ": " + cause.GetType().Name + ": " + cause.Message;
            return false;
        }
    }

    internal string BindSpeaker(object speaker, bool female)
    {
        if (!Ready || speaker == null) return "native-fallback";
        var tag = female ? "sfp_jane" : "sfp_john";
        // Never mutate the donors! Each SFP leader has a unique, owned SpeakerTemplate.
        if (!SetMemberValue(speaker, "Tags", tag + " marine " + (female ? "female" : "male") + " infantry"))
            throw new InvalidOperationException("Cannot set cloned speaker.Tags for John/Jane.");
        var current = GetMemberValue(speaker, "Tags")?.ToString() ?? "";
        if (!current.StartsWith(tag + " ", StringComparison.Ordinal))
            throw new InvalidOperationException("Cloned speaker.Tags read-back failed.");
        // The source speaker carries a stale split-tag cache from its vanilla parent.
        // Setting the native string[] cache is required by HasTag/MatchesTags.
        var tags = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(
            new[] { tag, "marine", female ? "female" : "male", "infantry" });
        if (!SetMemberValue(speaker, "m_SplitTags", tags))
            throw new InvalidOperationException("Cannot update cloned speaker.m_SplitTags.");
        return female ? "Jane" : "John";
    }

    /// <summary>
    /// MENACE's Hire screen does not use the tactical conversation tags. It
    /// plays two direct Stem.ID values stored on UnitLeaderTemplate. The
    /// experimental leader was cloned from Sachin, so without this override
    /// BOTH genders kept Sachin's selection/hired speech despite correct
    /// John/Jane tactical barks. Reuse the already registered private banks.
    /// </summary>
    internal string BindHiringBarks(UnitLeaderTemplate leader, bool female)
    {
        if (leader == null) throw new ArgumentNullException(nameof(leader));
        if (!Ready) throw new InvalidOperationException("John/Jane voices are not prepared.");
        VoiceProfile profile = _voiceProfiles.First(p => p.Key == (female ? "jane" : "john"));
        if (!profile.BankRegistered || !profile.ClipsApplied || !profile.BankRuntimeRefreshed)
            throw new InvalidOperationException("The requested John/Jane SoundBank is not fully ready.");
        BarkSpec selectedBark = VoiceBarks.First(b => b.Key == "ready");
        BarkSpec hiredBark = VoiceBarks.First(b => b.Key == "arrival");
        int selectedId = GetCustomItemId(profile, selectedBark, 0);
        int hiredId = GetCustomItemId(profile, hiredBark, 0);

        // Native fields are Il2CppStem.ID (bank + item), not a speaker tag.
        leader.HiringSelectBarkSound = new Il2CppStem.ID(profile.BankId, selectedId);
        leader.HiredBarkSound = new Il2CppStem.ID(profile.BankId, hiredId);
        if (leader.HiringSelectBarkSound.BankId != profile.BankId ||
            leader.HiringSelectBarkSound.ItemId != selectedId ||
            leader.HiredBarkSound.BankId != profile.BankId ||
            leader.HiredBarkSound.ItemId != hiredId)
            throw new InvalidOperationException("John/Jane Hire sound ID readback mismatch.");
        return female ? "Jane (ready/arrival)" : "John (ready/arrival)";
    }

    // No automatic repeated diagnostics. The outer Jiangyu system logs only
    // a single summary when the user explicitly requests F9 or F10.
    private void DebugLog(string message) { }
    private void DebugWarning(string message) { }
    private void LogWaitingOnce(string key, string message) { _failure = message; }
    private void LogErrorOnce(string key, string message)
    {
        if (_loggedErrors.Add(key)) _failure = message;
    }

    private static List<VoiceProfile> CreateVoiceProfiles()
    {
        // The two standalone voice banks are shared by arbitrarily many dynamic
        // leaders. They are NOT tied to any of the original fourteen fixed IDs.
        return new List<VoiceProfile>
        {
            new("john", "sfp_john", "sfp_tactical_barks_john", "SFPJohn", "cl2_john", "john"),
            new("jane", "sfp_jane", "sfp_tactical_barks_jane", "SFPJane", "cl2_jane", "jane")
        };
    }

    private static int StableId(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in value)
            {
                hash ^= (byte)ch;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    private static int GetCustomItemId(VoiceProfile profile, BarkSpec bark, int sourceIndex) =>
        StableId($"sfp_{profile.Key}_{bark.Key}_{sourceIndex + 1}");

    private static string GetSoundName(VoiceProfile profile, BarkSpec bark, int sourceIndex) =>
        $"sfp_{profile.Key}_{bark.Key}_{sourceIndex + 1}";

    private static string GetRouteKey(VoiceProfile profile, BarkSpec bark, int sourceIndex) =>
        $"{profile.Key}|{bark.Key}|{sourceIndex}";

    private static string GetVariationAssetName(VoiceProfile profile, string wavPath) =>
        $"{profile.AssetPrefix}_{Path.GetFileNameWithoutExtension(wavPath)}";

    private static int TotalVoiceSounds => VoiceBarks.Sum(b => b.SourceItemIds.Length);

    private static string GetClonePath(VoiceProfile profile, BarkSpec bark) =>
        $"{profile.ConversationPrefix}/{bark.CloneSuffix}";

    private bool TrySetupVoices()
    {
        try
        {
            _voiceStage = "mount John/Jane bundle";
            if (!EnsureVoiceClipsLoaded()) return false;
            foreach (var profile in _voiceProfiles)
            {
                _voiceStage = $"clone {profile.Key} conversations";
                if (!EnsureConversationClones(profile)) return false;
                _voiceStage = $"register {profile.Key} native SoundBank";
                if (!EnsureSoundBank(profile)) return false;
                _voiceStage = $"restore {profile.Key} conversation deserialization";
                foreach (var conversation in profile.ConversationProxies.Values)
                    RefreshConversationSerializedState(conversation);
                if (!profile.BankRegistered || !profile.ClipsApplied ||
                    profile.ConversationProxies.Count != VoiceBarks.Length)
                    throw new InvalidOperationException($"{profile.Key} profile not fully prepared.");
            }
            return true;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            _failure = $"{_voiceStage}: {cause.GetType().Name}: {cause.Message}";
            return false;
        }
    }

    // Jiangyu is hosted by MelonLoader. Resolve Harmony at runtime rather than
    // introducing a second mod framework or a compile-time Harmony dependency.
    // This is executed ONLY after both isolated banks and cloned conversations exist.
    private void InstallConversationManagerPatch()
    {
        var manager = FindLoadedType("BaseConversationManager", "Menace.Conversations")
            ?? throw new MissingMemberException("BaseConversationManager proxy is unavailable.");
        var target = manager.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "GetAvailableConversationTemplates")
            ?? throw new MissingMethodException(manager.FullName, "GetAvailableConversationTemplates");
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetType("HarmonyLib.Harmony", false) != null)
            ?? throw new InvalidOperationException("Harmony runtime is not loaded by MelonLoader.");
        var harmonyType = asm.GetType("HarmonyLib.Harmony", true);
        var harmonyMethodType = asm.GetType("HarmonyLib.HarmonyMethod", true);
        var prefixMethod = typeof(FieldVoiceSystem).GetMethod(nameof(ConversationManagerPrefix),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(ConversationManagerPrefix));
        var method = Activator.CreateInstance(harmonyMethodType, new object[] { prefixMethod })
            ?? throw new InvalidOperationException("Cannot construct HarmonyMethod.");
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "menace.sfp.voice.0.4" })
            ?? throw new InvalidOperationException("Cannot initialize voice Harmony instance.");
        var patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "Patch" && m.GetParameters().Length >= 2 &&
                m.GetParameters()[0].ParameterType == typeof(MethodBase))
            ?? throw new MissingMethodException("Harmony.Patch(MethodBase, HarmonyMethod,...)");
        var parms = patch.GetParameters();
        var args = new object[parms.Length];
        args[0] = target;
        args[1] = method;
        patch.Invoke(harmony, args);
        _harmony = harmony;
        _patchInstalled = true;
    }

    public static void ConversationManagerPrefix(Il2CppObjectBase __instance)
    {
        var instance = _instance;
        if (instance == null || __instance == null)
            return;

        try
        {
            instance.TryInjectConversationsIntoManager(__instance);
        }
        catch (Exception ex)
        {
            instance.LogErrorOnce("voice.manager.inject", $"[SFP Voice] Conversation manager injection failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Explicit loops avoid Roslyn 4.3 CS0656 (NullableAttribute..ctor) in
    // the LINQ SelectMany projection over the profile dictionaries.
    // Snapshot collection order and contents are unchanged.
    private IEnumerable<object> AllConversationProxies()
    {
        var conversations = new List<object>();
        foreach (var profile in _voiceProfiles)
        {
            foreach (var conversation in profile.ConversationProxies.Values)
                conversations.Add(conversation);
        }
        return conversations;
    }

    private void TryInjectConversationsIntoManager(Il2CppObjectBase manager)
    {
        if (!Ready || manager == null) return;
        var pointer = manager.Pointer;
        if (pointer == IntPtr.Zero || _voiceInjectedManagers.Contains(pointer))
            return;
        var all = AllConversationProxies().ToArray();
        if (all.Length == 0) return;
        var managerTypeName = manager.GetType().Name;

        var master = GetMemberValue(manager, "m_ConversationTemplates");
        var sample = master == null || GetCollectionCount(master) == 0 ? null : GetCollectionItem(master, 0);
        var managerConversationType = sample == null ? null : GetMemberValue(sample, "ConversationType");
        var cloneConversationType = GetMemberValue(all[0], "ConversationType");
        if (managerConversationType != null && cloneConversationType != null)
        {
            if (Convert.ToInt32(managerConversationType) != Convert.ToInt32(cloneConversationType))
                return;
        }
        else if (managerTypeName.IndexOf("TacticalBarksManager", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        var buckets = GetMemberValue(manager, "m_AvailableTemplatesByTriggerType");
        if (buckets == null || master == null)
            return;

        var bucketType = buckets.GetType();
        var genericArgs = bucketType.GetGenericArguments();
        if (genericArgs.Length != 2)
            return;
        var triggerType = genericArgs[0];
        var listType = genericArgs[1];
        var containsKey = bucketType.GetMethod("ContainsKey", new[] { triggerType });
        var getItem = bucketType.GetMethod("get_Item", new[] { triggerType });
        var setItem = bucketType.GetMethod("set_Item", new[] { triggerType, listType });
        var add = listType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);
        if (containsKey == null || getItem == null || setItem == null || add == null)
            return;

        var appended = 0;
        var verified = 0;
        foreach (var conversation in all)
        {
            var triggers = GetMemberValue(conversation, "Triggers");
            if (triggers == null) continue;
            var triggerCount = GetCollectionCount(triggers);
            var present = 0;
            for (var i = 0; i < triggerCount; i++)
            {
                var rawTrigger = GetCollectionItem(triggers, i);
                if (rawTrigger == null) continue;
                var key = Enum.ToObject(triggerType, Convert.ToInt32(rawTrigger));
                var hasKey = (bool)(containsKey.Invoke(buckets, new[] { key }) ?? false);
                if (!hasKey)
                {
                    var freshList = Activator.CreateInstance(listType)
                        ?? throw new InvalidOperationException("Cannot create conversation trigger list.");
                    setItem.Invoke(buckets, new[] { key, freshList });
                }
                var list = getItem.Invoke(buckets, new[] { key });
                if (list == null) continue;
                if (!CollectionContainsSameNativeObject(list, conversation))
                {
                    add.Invoke(list, new[] { conversation });
                    if (!CollectionContainsSameNativeObject(list, conversation))
                        throw new InvalidOperationException("Conversation bucket append failed read-back.");
                    appended++;
                }
                present++;
            }
            if (triggerCount > 0 && present == triggerCount) verified++;
        }

        var missing = all.Where(c => !CollectionContainsSameNativeObject(master, c)).ToArray();
        if (missing.Length > 0)
        {
            var count = GetCollectionCount(master);
            var expanded = CreateArray(master.GetType(), count + missing.Length, out var error)
                ?? throw new InvalidOperationException(error);
            for (var i = 0; i < count; i++)
                if (!SetCollectionItem(expanded, i, GetCollectionItem(master, i)!))
                    throw new InvalidOperationException("Cannot preserve existing conversation snapshot entry.");
            for (var i = 0; i < missing.Length; i++)
                if (!SetCollectionItem(expanded, count + i, missing[i]))
                    throw new InvalidOperationException("Cannot append custom conversation to master snapshot.");
            RequireSet(manager, "m_ConversationTemplates", expanded);
        }

        if (verified == all.Length)
        {
            _voiceInjectedManagers.Add(pointer);
            DebugLog($"[SFP Voice] Injected {verified}/{all.Length} John/Jane conversations into {managerTypeName}; {appended} new trigger bucket entries; master snapshot preserved.");
        }
    }

    private bool EnsureConversationClones(VoiceProfile profile)
    {
        _conversationTemplateType ??= FindLoadedType("ConversationTemplate", "Menace.Conversations");
        if (_conversationTemplateType == null)
        {
            LogWaitingOnce($"voice.{profile.Key}.conversation", "[SFP Voice] waiting for ConversationTemplate managed proxy type");
            return false;
        }

        foreach (var bark in VoiceBarks)
        {
            if (profile.ConversationProxies.ContainsKey(bark.Key)) continue;
            var clonePath = GetClonePath(profile, bark);
            var existing = FindConversationByPath(clonePath);
            if (existing.proxy != null && existing.unityObject != null)
            {
                RefreshConversationSerializedState(existing.proxy);
                profile.ConversationProxies[bark.Key] = existing.proxy;
                profile.ConversationUnityObjects[bark.Key] = existing.unityObject;
                continue;
            }

            var source = FindConversationByPath(bark.SourcePath);
            if (source.proxy == null || source.unityObject == null)
            {
                LogWaitingOnce($"voice.{profile.Key}.conversation", $"[SFP Voice] waiting for source ConversationTemplate Path '{bark.SourcePath}'");
                return false;
            }

            var cloneUnity = UnityEngine.Object.Instantiate(source.unityObject);
            if (cloneUnity == null) throw new InvalidOperationException($"Object.Instantiate returned null for {bark.SourcePath}.");
            cloneUnity.name = clonePath.Replace('/', '_');
            cloneUnity.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var cloneProxy = CreateTypedProxy(cloneUnity, _conversationTemplateType);
            if (cloneProxy == null)
            {
                UnityEngine.Object.Destroy(cloneUnity);
                throw new InvalidOperationException($"Failed to create typed proxy for {bark.SourcePath}.");
            }
            try
            {
                DetachConversationData(cloneProxy);
                if (!SetMemberValue(cloneProxy, "Path", clonePath) || !PatchConversationContent(cloneProxy, profile, bark))
                    throw new InvalidOperationException($"Failed to patch conversation clone {clonePath}.");
                RefreshConversationSerializedState(cloneProxy);
            }
            catch
            {
                UnityEngine.Object.Destroy(cloneUnity);
                throw;
            }
            profile.ConversationProxies[bark.Key] = cloneProxy;
            profile.ConversationUnityObjects[bark.Key] = cloneUnity;
            DebugLog($"[SFP Voice] Cloned ConversationTemplate by Path: {bark.SourcePath} -> {clonePath}.");
        }


        return profile.ConversationProxies.Count == VoiceBarks.Length;
    }

    private (object proxy, UnityEngine.Object unityObject) FindConversationByPath(string path)
    {
        if (_conversationTemplateType == null)
            return (null, null);

        try
        {
            var il2cppType = Il2CppType.From(_conversationTemplateType);
            if (_loadedConversationAssets == null)
            {
                _loadedConversationAssets = Resources.LoadAll("Data/Conversations", il2cppType);
                DebugLog("[SFP Voice] Loaded conversation Resources folder using IL2CPP types.");
            }
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);
            if (objects == null)
                return (null, null);

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                var proxy = CreateTypedProxy(obj, _conversationTemplateType);
                if (proxy == null)
                    continue;

                var candidatePath = GetMemberValue(proxy, "Path")?.ToString();
                if (string.Equals(candidatePath, path, StringComparison.Ordinal))
                    return (proxy, obj);
            }
        }
        catch (Exception ex)
        {
            DebugWarning($"[SFP Voice] Conversation Path scan failed: {ex.GetType().Name}: {ex.Message}");
        }

        return (null, null);
    }

    private bool PatchConversationContent(object conversation, VoiceProfile profile, BarkSpec bark)
    {
        var roles = GetMemberValue(conversation, "Roles");
        if (roles == null) return false;

        var rolePatched = false;
        for (var i = 0; i < GetCollectionCount(roles); i++)
        {
            var role = GetCollectionItem(roles, i);
            if (role == null) continue;
            var requirements = GetMemberValue(role, "m_SerializedRequirements");
            if (requirements == null) continue;
            for (var j = 0; j < GetCollectionCount(requirements); j++)
            {
                var raw = GetCollectionItem(requirements, j)?.ToString();
                if (string.IsNullOrEmpty(raw) || !raw.StartsWith("HasOneTag|", StringComparison.Ordinal)) continue;
                if (raw.IndexOf("\\\"lim\\\"", StringComparison.Ordinal) < 0 && raw.IndexOf("\"lim\"", StringComparison.Ordinal) < 0) continue;
                var patched = raw.Replace("\\\"lim\\\"", $"\\\"{profile.SpeakerId}\\\"")
                                 .Replace("\"lim\"", $"\"{profile.SpeakerId}\"");
                if (SetCollectionItem(requirements, j, patched)) rolePatched = true;
            }
        }

        var nodes = GetMemberValue(conversation, "Nodes");
        var serializedNodes = nodes == null ? null : GetMemberValue(nodes, "m_SerializedNodes");
        if (serializedNodes == null) return false;

        var nodePatched = false;
        for (var i = 0; i < GetCollectionCount(serializedNodes); i++)
        {
            var raw = GetCollectionItem(serializedNodes, i)?.ToString();
            if (raw == null) continue;
            var patched = raw.Replace(LimVoiceBankId.ToString(), profile.BankId.ToString());
            for (var sourceIndex = 0; sourceIndex < bark.SourceItemIds.Length; sourceIndex++)
            {
                var sourceId = bark.SourceItemIds[sourceIndex];
                var customId = GetCustomItemId(profile, bark, sourceIndex);
                patched = patched.Replace(sourceId.ToString(), customId.ToString());
                var subtitleKey = $"{profile.Key}|{bark.Key}|{sourceIndex}";
                if (!RouteSubtitles.TryGetValue(subtitleKey, out var exactSubtitle))
                    throw new InvalidOperationException($"Missing exact subtitle for voice route {subtitleKey}.");
                patched = ReplaceSerializedTextForSoundItem(patched, customId, exactSubtitle);
            }
            if (bark.FlagFrom != null)
                patched = patched.Replace(bark.FlagFrom, $"sfp_{profile.Key}_{bark.Key}_played");
            if (!string.Equals(raw, patched, StringComparison.Ordinal) && SetCollectionItem(serializedNodes, i, patched))
                nodePatched = true;
        }

        SetMemberValue(conversation, "Active", true);
        SetMemberValue(conversation, "PlayChance", 100);
        return rolePatched && nodePatched;
    }


    private static string ReplaceSerializedTextForSoundItem(string raw, int itemId, string subtitle)
    {
        // SAY nodes store Sound.itemId and Text together. Patch only the Text belonging
        // to this exact custom Sound route, never every SAY in the conversation.
        var markers = new[]
        {
            (Item: $"\\\"itemId\\\":{itemId}", Text: "\\\"Text\\\":\\\"", End: "\\\""),
            (Item: $"\"itemId\":{itemId}", Text: "\"Text\":\"", End: "\"")
        };

        foreach (var marker in markers)
        {
            var search = 0;
            while (true)
            {
                var item = raw.IndexOf(marker.Item, search, StringComparison.Ordinal);
                if (item < 0) break;
                var nextSay = raw.IndexOf("SAY|", item + marker.Item.Length, StringComparison.Ordinal);
                var text = raw.IndexOf(marker.Text, item + marker.Item.Length, StringComparison.Ordinal);
                if (text < 0 || (nextSay >= 0 && text > nextSay))
                {
                    search = item + marker.Item.Length;
                    continue;
                }
                var valueStart = text + marker.Text.Length;
                var valueEnd = raw.IndexOf(marker.End, valueStart, StringComparison.Ordinal);
                if (valueEnd < 0) break;
                raw = raw.Substring(0, valueStart) + subtitle + raw.Substring(valueEnd);
                search = valueStart + subtitle.Length + marker.End.Length;
            }
        }
        return raw;
    }

    private static string ReplaceSerializedTextValues(string raw, string subtitle)
    {
        // Runtime/data-extractor representations can expose the serialized SAY JSON either
        // directly ("Text":"...") or with one escaped JSON layer (\"Text\":\"...\").
        // Support both forms and modify only the displayed Text value; sound routing is untouched.
        raw = ReplaceSerializedStringField(raw, "\"Text\":\"", "\"", subtitle);
        raw = ReplaceSerializedStringField(raw, "\\\"Text\\\":\\\"", "\\\"", subtitle);
        return raw;
    }

    private static string ReplaceSerializedStringField(string raw, string marker, string terminator, string replacement)
    {
        var search = 0;
        while (true)
        {
            var key = raw.IndexOf(marker, search, StringComparison.Ordinal);
            if (key < 0) break;
            var valueStart = key + marker.Length;
            var valueEnd = raw.IndexOf(terminator, valueStart, StringComparison.Ordinal);
            if (valueEnd < 0) break;
            raw = raw.Substring(0, valueStart) + replacement + raw.Substring(valueEnd);
            search = valueStart + replacement.Length + terminator.Length;
        }
        return raw;
    }

    // Copy only owned data records/containers, never the AudioClip assets themselves.
    // Object.Instantiate alone is not a guarantee that mutable lists are isolated.
    private static void DetachConversationData(object clone)
    {
        var roles = GetMemberValue(clone, "Roles")
            ?? throw new InvalidOperationException("Cannot isolate null conversation Roles.");
        var ownedRoles = CopyCollection(roles, role =>
        {
            var copy = CopyDataRecord(role);
            CopyCollectionMember(copy, "m_SerializedRequirements", required: true);
            CopyCollectionMember(copy, "Requirements", required: false);
            return copy;
        });
        RequireSet(clone, "Roles", ownedRoles);

        var sourceNodes = GetMemberValue(clone, "Nodes")
            ?? throw new InvalidOperationException("Cannot isolate null conversation Nodes.");
        var nodes = CopyDataRecord(sourceNodes);
        CopyCollectionMember(nodes, "m_SerializedNodes", required: true);
        CopyCollectionMember(nodes, "m_Nodes", required: false);
        RequireSet(clone, "Nodes", nodes);
    }

    private static void DetachSoundBankData(object bank)
    {
        var structure = GetMemberValue(bank, "m_Structure");
        var structureMember = "m_Structure";
        if (structure == null)
        {
            structure = GetMemberValue(bank, "Structure");
            structureMember = "Structure";
        }
        var owner = structure == null ? bank : CopyDataRecord(structure);
        var sounds = GetMemberValue(owner, "sounds")
            ?? throw new InvalidOperationException("Cannot isolate SoundBank.sounds.");
        var ownedSounds = CopyCollection(sounds, sound =>
        {
            var copy = CopyDataRecord(sound);
            var variations = GetMemberValue(copy, "variations")
                ?? throw new InvalidOperationException("Cannot isolate null Sound.variations.");
            RequireSet(copy, "variations", CopyCollection(variations, CopyDataRecord));
            return copy;
        });
        RequireSet(owner, "sounds", ownedSounds);
        CopyCollectionMember(owner, "busIndices", required: false);
        if (structure != null)
            RequireSet(bank, structureMember, owner);
    }

    private static void CopyCollectionMember(object owner, string name, bool required)
    {
        var source = GetMemberValue(owner, name);
        if (source == null)
        {
            if (required)
                throw new InvalidOperationException($"Missing owned collection {name}.");
            return;
        }
        RequireSet(owner, name, CopyCollection(source, value => value));
    }

    private static object CopyCollection(object source, Func<object, object> copyElement)
    {
        var type = source.GetType();
        var count = GetCollectionCount(source);
        object copy;
        if (type.IsArray)
            copy = Array.CreateInstance(type.GetElementType()!, count);
        else if (type.Name.IndexOf("Array", StringComparison.Ordinal) >= 0)
            copy = CreateArray(type, count, out var error)
                ?? throw new InvalidOperationException(error);
        else
            copy = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot construct collection {type.FullName}.");

        var add = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);
        for (var i = 0; i < count; i++)
        {
            var value = source is Array managed ? managed.GetValue(i) : GetCollectionItem(source, i);
            var cloned = value == null ? null : copyElement(value);
            if (copy is Array managedCopy)
                managedCopy.SetValue(cloned, i);
            else if (add != null)
                add.Invoke(copy, new[] { cloned });
            else if (!SetCollectionItem(copy, i, cloned!))
                throw new InvalidOperationException($"Cannot copy element {i} in {type.FullName}.");
        }
        return copy;
    }

    private static object CopyDataRecord(object source)
    {
        var type = source.GetType();
        if (source is UnityEngine.Object)
            throw new InvalidOperationException($"Refusing to copy a Unity asset as a data record: {type.FullName}.");
        var copy = AllocateOwnedDataRecord(source);
        var copied = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current != null && current != typeof(object) &&
             current != typeof(Il2CppObjectBase) &&
             !(current.Namespace ?? "").StartsWith("Il2CppSystem", StringComparison.Ordinal); current = current.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var property in current.GetProperties(flags))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0 ||
                    !names.Add(property.Name))
                    continue;
                property.SetValue(copy, property.GetValue(source));
                copied++;
            }
            foreach (var field in current.GetFields(flags))
            {
                if (field.IsInitOnly || !names.Add(field.Name))
                    continue;
                field.SetValue(copy, field.GetValue(source));
                copied++;
            }
        }
        if (copied == 0)
            throw new InvalidOperationException($"No data members copied on {type.FullName}.");
        if (!type.IsValueType && SameNativeObject(copy, source))
            throw new InvalidOperationException($"Owned data copy still aliases its source: {type.FullName}.");
        return copy;
    }

    // Generated IL2CPP wrappers do not necessarily expose a managed empty
    // constructor. Stem.Sound is such a record in the user's v0.7.15 build.
    // As in Jiangyu's Il2CppInstanceAllocator, allocate the native object and
    // wrap the NEW pointer. CopyDataRecord then copies the source data members,
    // and DetachSoundBankData separately copies sounds/variations/containers.
    // This is not a general constructor: it is only for copying an existing
    // non-Unity data record whose fields are about to be populated from source.
    private static object AllocateOwnedDataRecord(object source)
    {
        var type = source.GetType();
        if (source is UnityEngine.Object)
            throw new InvalidOperationException($"Native record allocation is forbidden for Unity assets: {type.FullName}.");

        // Keep the already-working constructor path for roles, node containers
        // and other records. Do not catch a constructor failure and silently
        // turn it into uninitialised allocation.
        if (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null)
            return Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot construct owned data record {type.FullName}.");

        if (source is not Il2CppObjectBase nativeSource)
            throw new MissingMethodException($"Managed data record {type.FullName} has no parameterless constructor; no native fallback is allowed.");

        var wrapperCtor = type.GetConstructor(new[] { typeof(IntPtr) })
            ?? throw new MissingMethodException(type.FullName, ".ctor(IntPtr)");
        var sourcePointer = nativeSource.Pointer;
        if (sourcePointer == IntPtr.Zero)
            throw new InvalidOperationException($"Cannot copy a null native record: {type.FullName}.");

        // Resolve from the live source rather than hardcoding an assembly name,
        // an address, a field offset or a constructor argument for Stem.Sound.
        var classPointer = IL2CPP.il2cpp_object_get_class(sourcePointer);
        if (classPointer == IntPtr.Zero)
            throw new InvalidOperationException($"Cannot resolve the native class for {type.FullName}.");
        var copyPointer = IL2CPP.il2cpp_object_new(classPointer);
        if (copyPointer == IntPtr.Zero || copyPointer == sourcePointer)
            throw new InvalidOperationException($"Native allocation did not return a distinct {type.FullName} object.");

        // Reflection can allocate managed objects before the wrapper's own GC
        // handle is established. Hold a temporary native root across that gap.
        var temporaryHandle = IL2CPP.il2cpp_gchandle_new(copyPointer, false);
        if (temporaryHandle == 0)
            throw new InvalidOperationException($"Cannot root the new native {type.FullName} record.");
        try
        {
            var copy = wrapperCtor.Invoke(new object[] { copyPointer });
            if (copy is not Il2CppObjectBase wrappedCopy || wrappedCopy.Pointer != copyPointer)
                throw new InvalidOperationException($"The {type.FullName} wrapper did not retain the fresh native allocation.");
            if (IL2CPP.il2cpp_object_get_class(wrappedCopy.Pointer) != classPointer)
                throw new InvalidOperationException($"Native record class mismatch for {type.FullName}.");

            // Do not run il2cpp_runtime_object_init here: this path is reached
            // precisely when an empty constructor is unavailable. It is a copy,
            // not blank authoring: the source's data fields are written next.
            if (NativeRecordAllocationTypes.Add(type))
                _instance?.DebugLog($"[SFP Voice] DATA RECORD ALLOCATOR: {type.FullName} -> fresh IL2CPP allocation + IntPtr wrapper (no empty constructor).");
            return copy;
        }
        finally
        {
            // The successfully constructed wrapper owns its own strong handle.
            // On failure the unregistered temporary record may be collected.
            IL2CPP.il2cpp_gchandle_free(temporaryHandle);
            GC.KeepAlive(source);
        }
    }

    // Verify the whole writable chain before replacing a single AudioClip.
    // Merely having a different top-level SoundBank is not sufficient.
    private void VerifySoundBankIsolation(object sourceBank, object cloneBank)
    {
        var sourceOwner = GetMemberValue(sourceBank, "m_Structure")
            ?? GetMemberValue(sourceBank, "Structure") ?? sourceBank;
        var cloneOwner = GetMemberValue(cloneBank, "m_Structure")
            ?? GetMemberValue(cloneBank, "Structure") ?? cloneBank;
        RequireDistinctStorage(sourceOwner, cloneOwner, "SoundBank structure");
        var sourceSounds = GetMemberValue(sourceOwner, "sounds")
            ?? throw new InvalidOperationException("Source SoundBank.sounds is missing.");
        var cloneSounds = GetMemberValue(cloneOwner, "sounds")
            ?? throw new InvalidOperationException("Cloned SoundBank.sounds is missing.");
        RequireDistinctStorage(sourceSounds, cloneSounds, "SoundBank sounds collection");
        var soundCount = GetCollectionCount(sourceSounds);
        if (soundCount != GetCollectionCount(cloneSounds))
            throw new InvalidOperationException("SoundBank copy changed the number of Sound records.");

        var variationCount = 0;
        for (var i = 0; i < soundCount; i++)
        {
            var original = GetCollectionItem(sourceSounds, i);
            var copy = GetCollectionItem(cloneSounds, i);
            if (original == null && copy == null)
                continue;
            if (original == null || copy == null)
                throw new InvalidOperationException($"SoundBank copy lost sounds[{i}].");
            RequireDistinctStorage(original, copy, $"sounds[{i}]");
            var originalVariations = GetMemberValue(original, "variations")
                ?? throw new InvalidOperationException($"Source sounds[{i}].variations is missing.");
            var copyVariations = GetMemberValue(copy, "variations")
                ?? throw new InvalidOperationException($"Cloned sounds[{i}].variations is missing.");
            RequireDistinctStorage(originalVariations, copyVariations, $"sounds[{i}].variations");
            var count = GetCollectionCount(originalVariations);
            if (count != GetCollectionCount(copyVariations))
                throw new InvalidOperationException($"SoundBank copy changed sounds[{i}].variations length.");
            for (var j = 0; j < count; j++)
            {
                var originalVariation = GetCollectionItem(originalVariations, j);
                var copyVariation = GetCollectionItem(copyVariations, j);
                if (originalVariation == null && copyVariation == null)
                    continue;
                if (originalVariation == null || copyVariation == null)
                    throw new InvalidOperationException($"SoundBank copy lost sounds[{i}].variations[{j}].");
                RequireDistinctStorage(originalVariation, copyVariation, $"sounds[{i}].variations[{j}]");
                variationCount++;
            }
        }
        DebugLog($"[SFP Voice] SOUNDBANK ISOLATION OK: {soundCount} sounds, {variationCount} variations; mutable records and containers are separate from Lim.");
    }

    private static void RequireDistinctStorage(object source, object copy, string label)
    {
        if (source.GetType().IsValueType)
            return; // Boxed value records are copied by value; children are checked separately.
        if (SameNativeObject(source, copy))
            throw new InvalidOperationException($"Refusing to modify {label}: the copy still shares its source's storage.");
    }

    private static void RequireSet(object owner, string name, object value)
    {
        if (!SetMemberValue(owner, name, value))
            throw new InvalidOperationException($"Cannot set owned member {owner.GetType().Name}.{name}.");
    }

    private static bool SameNativeObject(object a, object b)
    {
        if (ReferenceEquals(a, b))
            return true;
        return TryGetNativePointer(a, out var left) && TryGetNativePointer(b, out var right) && left == right;
    }

    private static bool CollectionContainsSameNativeObject(object collection, object candidate)
    {
        if (!TryGetNativePointer(candidate, out var wanted))
            return false;

        var count = GetCollectionCount(collection);
        for (var i = 0; i < count; i++)
        {
            var item = GetCollectionItem(collection, i);
            if (item != null && TryGetNativePointer(item, out var pointer) && pointer == wanted)
                return true;
        }

        return false;
    }

    private static bool TryGetNativePointer(object target, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        if (target == null)
            return false;

        try
        {
            var prop = target.GetType().GetProperty(
                "Pointer",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var value = prop?.GetValue(target);
            if (value is IntPtr p && p != IntPtr.Zero)
            {
                pointer = p;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private bool EnsureVoiceClipsLoaded()
    {
        if (_voiceClips != null) return true;
        var routesPath = Path.Combine(_modpackRoot, "runtime_assets", "voice", "voice_routes.json");
        if (!File.Exists(routesPath))
        {
            _failure = "voice_routes.json missing from deployed SFP mod";
            return false;
        }
        var spec = JsonSerializer.Deserialize<VoiceRoutesFile>(File.ReadAllText(routesPath));
        if (spec == null || spec.schemaVersion != 1 || spec.routes == null)
            throw new InvalidDataException("Invalid voice route manifest.");
        _voiceRouteAssets.Clear();
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in _voiceProfiles)
        foreach (var bark in VoiceBarks)
        for (var sourceIndex = 0; sourceIndex < bark.SourceItemIds.Length; sourceIndex++)
        {
            var key = GetRouteKey(profile, bark, sourceIndex);
            if (!spec.routes.TryGetValue(key, out var assets) || assets == null || assets.Length == 0)
                throw new InvalidDataException("Missing semantic voice route " + key);
            foreach (var asset in assets)
            {
                if (!asset.StartsWith("cl2_" + profile.Key + "_", StringComparison.Ordinal))
                    throw new InvalidDataException("Wrong gender in voice route " + key);
                required.Add(asset);
            }
            _voiceRouteAssets[key] = assets;
        }
        if (required.Count != 429)
            throw new InvalidDataException($"Expected 429 John/Jane voice assets, found {required.Count}.");
        _voiceStage = "validate and load Unity voice bundle";
        _voiceBundleAudio = RuntimeVoiceBundle.Load(_modpackRoot, required);
        _voiceClips = _voiceBundleAudio.Clips;
        return true;
    }

    private sealed class VoiceRoutesFile
    {
        public int schemaVersion { get; set; }
        public Dictionary<string, string[]> routes { get; set; }
    }

    private bool EnsureSoundBank(VoiceProfile profile)
    {
        if (profile.BankProxy != null && profile.BankUnityObject != null)
            return FinishSoundBankSetup(profile);

        _soundBankType ??= FindLoadedType("SoundBank", "Stem");
        if (_soundBankType == null)
        {
            LogWaitingOnce($"voice.{profile.Key}.bank", "[SFP Voice] waiting for Stem.SoundBank managed proxy type");
            return false;
        }

        var existing = FindSoundBankById(profile.BankId);
        if (existing.proxy != null && existing.unityObject != null)
        {
            profile.BankProxy = existing.proxy;
            profile.BankUnityObject = existing.unityObject;
            return FinishSoundBankSetup(profile);
        }

        var source = FindSoundBankById(LimVoiceBankId);
        if (source.proxy == null || source.unityObject == null)
        {
            LogWaitingOnce($"voice.{profile.Key}.bank", $"[SFP Voice] waiting for Lim SoundBank id={LimVoiceBankId}");
            return false;
        }

        var cloneUnity = UnityEngine.Object.Instantiate(source.unityObject);
        if (cloneUnity == null) return false;
        cloneUnity.name = profile.BankName;
        cloneUnity.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var cloneProxy = CreateTypedProxy(cloneUnity, _soundBankType);
        if (cloneProxy == null)
        {
            UnityEngine.Object.Destroy(cloneUnity);
            return false;
        }

        try
        {
            DetachSoundBankData(cloneProxy);
            VerifySoundBankIsolation(source.proxy, cloneProxy);
            ConfigureSounds(cloneProxy, profile);
            if (!TrySetBankId(cloneProxy, profile.BankId))
                throw new InvalidOperationException("failed to assign unique SoundBank ID");
        }
        catch
        {
            UnityEngine.Object.Destroy(cloneUnity);
            throw;
        }

        profile.BankUnityObject = cloneUnity;
        profile.BankProxy = cloneProxy;
        if (!FinishSoundBankSetup(profile)) return false;
        DebugLog($"[SFP Voice] Cloned Lim SoundBank {LimVoiceBankId} -> {profile.BankId} ({profile.BankName}).");
        return true;
    }

    private bool FinishSoundBankSetup(VoiceProfile profile)
    {
        if (profile.BankProxy == null) return false;
        if (!TryReadBankId(profile.BankProxy, out var id) || id != profile.BankId)
            throw new InvalidOperationException($"{profile.Key} bank ID read-back failed: {id}.");

        if (!profile.ClipsApplied)
        {
            if (_voiceClips == null) return false;
            var total = 0;
            var semanticAssets = 0;
            foreach (var bark in VoiceBarks)
            for (var sourceIndex = 0; sourceIndex < bark.SourceItemIds.Length; sourceIndex++)
            {
                var routeKey = GetRouteKey(profile, bark, sourceIndex);
                if (!_voiceRouteAssets.TryGetValue(routeKey, out var assetNames) || assetNames.Length == 0)
                    throw new InvalidOperationException($"Semantic voice route has no assets: {routeKey}.");
                var clips = new List<AudioClip>(assetNames.Length);
                foreach (var assetName in assetNames)
                {
                    if (!_voiceClips.TryGetValue(assetName, out var clip))
                        throw new InvalidOperationException($"Loaded voice bundle is missing {assetName}.");
                    clips.Add(clip);
                }
                var itemId = GetCustomItemId(profile, bark, sourceIndex);
                var replaced = ReplaceSoundVariationClips(profile.BankProxy, itemId, clips);
                if (replaced <= 0)
                    throw new InvalidOperationException($"No SoundVariation.clip fields replaced for {profile.Key}/{bark.Key}[{sourceIndex}] itemId={itemId}.");
                if (replaced != clips.Count)
                    DebugWarning($"[SFP Voice] VARIATION COUNT DRIFT: {profile.Key}/{bark.Key}[{sourceIndex + 1}] MENACE variations={replaced}, bundled semantic clips={clips.Count}; clips will cycle safely.");
                total += replaced;
                semanticAssets += clips.Count;
                DebugLog($"[SFP Voice] BARK AUDIO OK: {profile.Key}/{bark.Key}[{sourceIndex + 1}/{bark.SourceItemIds.Length}] -> {clips.Count} semantic clip(s); {replaced} MENACE variation(s).");
            }
            profile.ClipsApplied = true;
            DebugLog($"[SFP Voice] {profile.Key} tactical voice set applied: {TotalVoiceSounds} sound routes across {VoiceBarks.Length} conversations, {semanticAssets} transcription-mapped clip slots -> {total} SoundBank variation(s), volume scale 0.60.");
        }

        if (!profile.BankRuntimeRefreshed)
            profile.BankRuntimeRefreshed = RefreshSoundBankRuntimeState(profile.BankProxy, profile);
        if (!profile.BankRuntimeRefreshed) return false;

        if (!profile.BankRegistered)
            profile.BankRegistered = RegisterSoundBank(profile.BankProxy, profile);
        return profile.BankRegistered;
    }

    private (object proxy, UnityEngine.Object unityObject) FindSoundBankById(int wantedBankId)
    {
        if (_soundBankType == null) return (null, null);
        try
        {
            var objects = Resources.FindObjectsOfTypeAll(Il2CppType.From(_soundBankType));
            if (objects == null) return (null, null);
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                var proxy = CreateTypedProxy(obj, _soundBankType);
                if (proxy != null && TryReadBankId(proxy, out var bankId) && bankId == wantedBankId)
                    return (proxy, obj);
            }
        }
        catch (Exception ex)
        {
            DebugWarning($"[SFP Voice] SoundBank scan failed: {ex.GetType().Name}: {ex.Message}");
        }
        return (null, null);
    }

    private bool RefreshSoundBankRuntimeState(object bankProxy, VoiceProfile profile)
    {
        try
        {
            var method = bankProxy.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "OnAfterDeserialize" && m.GetParameters().Length == 0);
            if (method == null) return false;
            method.Invoke(bankProxy, null);
            if (!TryReadBankId(bankProxy, out var bankId) || bankId != profile.BankId)
                throw new InvalidOperationException($"SoundBank refresh changed bankId: {bankId}.");
            object owner = GetMemberValue(bankProxy, "m_Structure") ?? GetMemberValue(bankProxy, "Structure") ?? bankProxy;
            var sounds = GetMemberValue(owner, "sounds") ?? GetMemberValue(owner, "Sounds")
                ?? throw new InvalidOperationException("SoundBank refresh lost sounds[].");
            if (GetCollectionCount(sounds) != TotalVoiceSounds)
                throw new InvalidOperationException($"SoundBank refresh expected {TotalVoiceSounds} Sounds but found {GetCollectionCount(sounds)}.");
            var ids = new HashSet<int>();
            for (var i = 0; i < GetCollectionCount(sounds); i++)
            {
                var sound = GetCollectionItem(sounds, i) ?? throw new InvalidOperationException($"sounds[{i}] null");
                var soundId = GetMemberValue(sound, "id") ?? GetMemberValue(sound, "Id");
                if (soundId == null) throw new InvalidOperationException($"sounds[{i}] missing id");
                ids.Add(Convert.ToInt32(soundId));
            }
            if (ids.Count != TotalVoiceSounds || VoiceBarks.Any(b => b.SourceItemIds.Select((_, i) => GetCustomItemId(profile, b, i)).Any(id => !ids.Contains(id))))
                throw new InvalidOperationException($"SoundBank refresh lost one or more {profile.Key} itemIds.");
            DebugLog($"[SFP Voice] SOUNDBANK RUNTIME REFRESH OK: profile={profile.Key}, bankId={bankId}, sounds={TotalVoiceSounds}.");
            return true;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            LogWaitingOnce($"voice.{profile.Key}.bank", $"[SFP Voice] SoundBank runtime refresh failed: {cause.GetType().Name}: {cause.Message}");
            return false;
        }
    }

    private bool RegisterSoundBank(object bankProxy, VoiceProfile profile)
    {
        _soundManagerType ??= FindLoadedType("SoundManager", "Stem");
        if (_soundManagerType == null) return false;
        try
        {
            var candidates = _soundManagerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "RegisterBank" && m.GetParameters().Length == 1).ToArray();
            foreach (var method in candidates)
            {
                var parameterType = method.GetParameters()[0].ParameterType;
                if (!parameterType.IsInstanceOfType(bankProxy) && !parameterType.IsAssignableFrom(bankProxy.GetType())) continue;
                method.Invoke(null, new[] { bankProxy });
                DebugLog($"[SFP Voice] Registered '{profile.BankName}' with Stem.SoundManager.");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            LogWaitingOnce($"voice.{profile.Key}.bank", $"[SFP Voice] RegisterBank failed: {cause.GetType().Name}: {cause.Message}");
            return false;
        }
    }

    private void ConfigureSounds(object bankProxy, VoiceProfile profile)
    {
        object structure = GetMemberValue(bankProxy, "m_Structure") ?? GetMemberValue(bankProxy, "Structure");
        string structureMember = GetMemberValue(bankProxy, "m_Structure") != null ? "m_Structure" : (GetMemberValue(bankProxy, "Structure") != null ? "Structure" : null);
        object owner = structure ?? bankProxy;
        var sounds = GetMemberValue(owner, "sounds") ?? GetMemberValue(owner, "Sounds")
            ?? throw new InvalidOperationException("SoundBank sounds collection is missing.");
        var buses = GetMemberValue(owner, "busIndices") ?? GetMemberValue(owner, "BusIndices");
        var count = GetCollectionCount(sounds);
        if (buses != null && GetCollectionCount(buses) != count)
            throw new InvalidOperationException($"SoundBank busIndices/sounds mismatch: sounds={count}, buses={GetCollectionCount(buses)}.");

        var selectedSounds = new List<object>(TotalVoiceSounds);
        var selectedBuses = new List<object>(TotalVoiceSounds);
        foreach (var bark in VoiceBarks)
        for (var sourceIndex = 0; sourceIndex < bark.SourceItemIds.Length; sourceIndex++)
        {
            var sourceItemId = bark.SourceItemIds[sourceIndex];
            object sourceSound = null;
            var selectedIndex = -1;
            for (var i = 0; i < count; i++)
            {
                var candidate = GetCollectionItem(sounds, i);
                if (candidate == null) continue;
                var id = GetMemberValue(candidate, "id") ?? GetMemberValue(candidate, "Id");
                if (id != null && Convert.ToInt32(id) == sourceItemId)
                { sourceSound = candidate; selectedIndex = i; break; }
            }
            if (sourceSound == null || selectedIndex < 0)
                throw new InvalidOperationException($"Source Sound id {sourceItemId} for bark '{bark.Key}' was not found.");

            var selected = CopyDataRecord(sourceSound);
            var sourceVariations = GetMemberValue(sourceSound, "variations") ?? GetMemberValue(sourceSound, "Variations")
                ?? throw new InvalidOperationException($"Source Sound {sourceItemId} has no variations.");
            RequireSet(selected, "variations", CopyCollection(sourceVariations, CopyDataRecord));

            var customId = GetCustomItemId(profile, bark, sourceIndex);
            if (!SetMemberValue(selected, "id", customId) && !SetMemberValue(selected, "Id", customId))
                throw new InvalidOperationException($"Could not assign Sound.id for {profile.Key}/{bark.Key}[{sourceIndex}].");
            SetMemberValue(selected, "name", GetSoundName(profile, bark, sourceIndex));
            SetMemberValue(selected, "Name", GetSoundName(profile, bark, sourceIndex));

            var rawVolume = GetMemberValue(selected, "fixedVolume") ?? GetMemberValue(selected, "FixedVolume");
            var scaledVolume = rawVolume == null ? 0.60f : Convert.ToSingle(rawVolume) * 0.60f;
            if (!SetMemberValue(selected, "fixedVolume", scaledVolume))
                SetMemberValue(selected, "FixedVolume", scaledVolume);

            selectedSounds.Add(selected);
            if (buses != null)
                selectedBuses.Add(GetCollectionItem(buses, selectedIndex) ?? throw new InvalidOperationException($"busIndices[{selectedIndex}] is null."));
            DebugLog($"[SFP Voice] SOUND ROUTE: {profile.Key}/{bark.Key}[{sourceIndex + 1}/{bark.SourceItemIds.Length}] sourceIndex={selectedIndex} sourceItem={sourceItemId} -> itemId={customId}, volume=60%.");
        }

        var reducedSounds = CreateCollectionLike(sounds, selectedSounds);
        if (!SetMemberValue(owner, "sounds", reducedSounds) && !SetMemberValue(owner, "Sounds", reducedSounds))
            throw new InvalidOperationException("Could not replace SoundBank sounds with tactical subset.");
        if (buses != null)
        {
            var reducedBuses = CreateCollectionLike(buses, selectedBuses);
            if (!SetMemberValue(owner, "busIndices", reducedBuses) && !SetMemberValue(owner, "BusIndices", reducedBuses))
                throw new InvalidOperationException("Could not replace SoundBank busIndices with tactical subset.");
        }
        if (structure != null && structure.GetType().IsValueType && structureMember != null)
            RequireSet(bankProxy, structureMember, owner);

        var verifySounds = GetMemberValue(owner, "sounds") ?? GetMemberValue(owner, "Sounds")
            ?? throw new InvalidOperationException("SoundBank sounds disappeared after rewrite.");
        if (GetCollectionCount(verifySounds) != TotalVoiceSounds)
            throw new InvalidOperationException($"SoundBank rewrite produced {GetCollectionCount(verifySounds)} sounds, expected {TotalVoiceSounds}.");
        var ids = new HashSet<int>();
        for (var i = 0; i < GetCollectionCount(verifySounds); i++)
        {
            var sound = GetCollectionItem(verifySounds, i) ?? throw new InvalidOperationException("tactical Sound is null");
            ids.Add(Convert.ToInt32(GetMemberValue(sound, "id") ?? GetMemberValue(sound, "Id")!));
        }
        if (ids.Count != TotalVoiceSounds || VoiceBarks.Any(b => b.SourceItemIds.Select((_, i) => GetCustomItemId(profile, b, i)).Any(id => !ids.Contains(id))))
            throw new InvalidOperationException($"{profile.Key} SoundBank itemId verification failed.");
        DebugLog($"[SFP Voice] UNIQUE SOUNDS OK: profile={profile.Key}, bankId={profile.BankId}, sounds={TotalVoiceSounds}, buses={(buses == null ? 0 : selectedBuses.Count)}, volumeScale=0.60.");
    }

    private static object CreateCollectionLike(object source, IReadOnlyList<object> elements)
    {
        var type = source.GetType();
        object result;
        if (type.IsArray)
            result = Array.CreateInstance(type.GetElementType()!, elements.Count);
        else if (type.Name.IndexOf("Array", StringComparison.Ordinal) >= 0)
            result = CreateArray(type, elements.Count, out var error)
                ?? throw new InvalidOperationException(error);
        else
            result = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Cannot construct collection {type.FullName}.");

        var add = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);
        for (var i = 0; i < elements.Count; i++)
        {
            var value = elements[i];
            if (result is Array arr)
                arr.SetValue(value, i);
            else if (add != null)
                add.Invoke(result, new[] { value });
            else if (!SetCollectionItem(result, i, value))
                throw new InvalidOperationException($"Cannot set element {i} in {type.FullName}.");
        }
        return result;
    }

    private static int ReplaceSoundVariationClips(object bankProxy, int itemId, IReadOnlyList<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return 0;
        object sounds = GetMemberValue(bankProxy, "sounds");
        object structure = null;
        if (sounds == null)
        {
            structure = GetMemberValue(bankProxy, "m_Structure") ?? GetMemberValue(bankProxy, "Structure");
            if (structure != null) sounds = GetMemberValue(structure, "sounds") ?? GetMemberValue(structure, "Sounds");
        }
        if (sounds == null) return 0;

        for (var i = 0; i < GetCollectionCount(sounds); i++)
        {
            var sound = GetCollectionItem(sounds, i);
            if (sound == null) continue;
            var rawId = GetMemberValue(sound, "id") ?? GetMemberValue(sound, "Id");
            if (rawId == null || Convert.ToInt32(rawId) != itemId) continue;
            var variations = GetMemberValue(sound, "variations") ?? GetMemberValue(sound, "Variations");
            if (variations == null) return 0;
            var replaced = 0;
            for (var j = 0; j < GetCollectionCount(variations); j++)
            {
                var variation = GetCollectionItem(variations, j);
                if (variation == null) continue;
                var clip = clips[j % clips.Count];
                if (!SetMemberValue(variation, "clip", clip) && !SetMemberValue(variation, "Clip", clip)) continue;
                SetCollectionItem(variations, j, variation);
                replaced++;
            }
            SetCollectionItem(sounds, i, sound);
            if (structure != null && structure.GetType().IsValueType)
                SetMemberValue(bankProxy, "m_Structure", structure);
            return replaced;
        }
        return 0;
    }

    private static bool TryReadBankId(object bankProxy, out int bankId)
    {
        bankId = 0;

        object value = GetMemberValue(bankProxy, "bankId") ?? GetMemberValue(bankProxy, "BankId");
        if (value == null)
        {
            var structure = GetMemberValue(bankProxy, "m_Structure") ?? GetMemberValue(bankProxy, "Structure");
            if (structure != null)
                value = GetMemberValue(structure, "bankId") ?? GetMemberValue(structure, "BankId");
        }

        if (value == null)
            return false;

        try
        {
            bankId = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetBankId(object bankProxy, int bankId)
    {
        if (SetMemberValue(bankProxy, "bankId", bankId) || SetMemberValue(bankProxy, "BankId", bankId))
            return true;

        var structure = GetMemberValue(bankProxy, "m_Structure") ?? GetMemberValue(bankProxy, "Structure");
        if (structure == null)
            return false;

        var changed = SetMemberValue(structure, "bankId", bankId) || SetMemberValue(structure, "BankId", bankId);
        if (!changed)
            return false;

        if (structure.GetType().IsValueType)
        {
            if (!SetMemberValue(bankProxy, "m_Structure", structure) &&
                !SetMemberValue(bankProxy, "Structure", structure))
                return false;
        }

        return true;
    }

    private void RefreshConversationSerializedState(object conversation)
    {
        var roles = GetMemberValue(conversation, "Roles")
            ?? throw new InvalidOperationException("Conversation.Roles is null.");
        for (var i = 0; i < GetCollectionCount(roles); i++)
        {
            var role = GetCollectionItem(roles, i)
                ?? throw new InvalidOperationException($"Conversation role {i} is null.");
            InvokeRequiredCallback(role, "OnAfterDeserialize", conversation);
        }

        var nodes = GetMemberValue(conversation, "Nodes")
            ?? throw new InvalidOperationException("Conversation.Nodes is null.");
        InvokeRequiredCallback(nodes, "OnAfterDeserialize", conversation);

        var path = GetMemberValue(conversation, "Path")?.ToString() ?? "<unknown conversation>";
        DebugLog($"[SFP Voice] Refreshed serialized role/node state for {path}.");
    }

    private static void InvokeRequiredCallback(object target, string name, object argument)
    {
        var method = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsInstanceOfType(argument));
        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, name + "(ConversationTemplate)");
        method.Invoke(target, new[] { argument });
    }

    private static Type FindLoadedType(string typeName, string namespaceContains)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (!string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    continue;

                var ns = type.Namespace ?? string.Empty;
                if (ns.IndexOf(namespaceContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return type;
            }
        }

        return null;
    }

    private static object CreateTypedProxy(UnityEngine.Object unityObject, Type managedType)
    {
        if (unityObject == null || unityObject.Pointer == IntPtr.Zero)
            return null;
        var ctor = managedType.GetConstructor(new[] { typeof(IntPtr) });
        return ctor?.Invoke(new object[] { unityObject.Pointer });
    }

    // Compile-time ABI guard: a build against UnityDependencies must fail here,
    // rather than producing a DLL that fails only after entering the game.
    private static Il2CppObjectBase RequireGeneratedUnityObject(UnityEngine.Object value) => value;

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null)
            return null;

        var type = target.GetType();
        var property = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase) && p.CanRead);
        if (property != null)
        {
            try { return property.GetValue(target); }
            catch { }
        }

        var field = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => string.Equals(f.Name, memberName, StringComparison.OrdinalIgnoreCase));
        if (field != null)
        {
            try { return field.GetValue(target); }
            catch { }
        }

        return null;
    }

    private static bool SetMemberValue(object target, string memberName, object value)
    {
        if (target == null)
            return false;

        var type = target.GetType();
        var property = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase) && p.CanWrite);
        if (property != null)
        {
            try
            {
                property.SetValue(target, ConvertForTarget(value, property.PropertyType));
                return true;
            }
            catch
            {
                // Try field fallback.
            }
        }

        var field = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => string.Equals(f.Name, memberName, StringComparison.OrdinalIgnoreCase));
        if (field != null)
        {
            try
            {
                field.SetValue(target, ConvertForTarget(value, field.FieldType));
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static object ConvertForTarget(object value, Type targetType)
    {
        if (value == null)
            return null;
        if (targetType.IsInstanceOfType(value))
            return value;
        if (targetType.IsEnum)
            return Enum.ToObject(targetType, Convert.ToInt32(value));
        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(uint)) return Convert.ToUInt32(value);
        if (targetType == typeof(long)) return Convert.ToInt64(value);
        if (targetType == typeof(ulong)) return Convert.ToUInt64(value);
        if (targetType == typeof(short)) return Convert.ToInt16(value);
        if (targetType == typeof(ushort)) return Convert.ToUInt16(value);
        if (targetType == typeof(byte)) return Convert.ToByte(value);
        if (targetType == typeof(sbyte)) return Convert.ToSByte(value);
        if (targetType == typeof(float)) return Convert.ToSingle(value);
        if (targetType == typeof(double)) return Convert.ToDouble(value);
        if (targetType == typeof(bool)) return Convert.ToBoolean(value);
        if (targetType == typeof(string)) return value.ToString();
        return value;
    }

    private static int GetCollectionCount(object collection)
    {
        var type = collection.GetType();
        var count = type.GetProperty("Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (count != null)
            return Convert.ToInt32(count.GetValue(collection));

        var length = type.GetProperty("Length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (length != null)
            return Convert.ToInt32(length.GetValue(collection));

        return 0;
    }

    private static object GetCollectionItem(object collection, int index)
    {
        var type = collection.GetType();
        var getter = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "get_Item" && m.GetParameters().Length == 1);
        if (getter != null)
        {
            try
            {
                var parameterType = getter.GetParameters()[0].ParameterType;
                return getter.Invoke(collection, new[] { ConvertNumeric(index, parameterType) });
            }
            catch { }
        }

        var item = type.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (item != null)
        {
            try { return item.GetValue(collection, new object[] { index }); }
            catch { }
        }

        return null;
    }

    private static bool SetCollectionItem(object collection, int index, object value)
    {
        var type = collection.GetType();
        var setter = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "set_Item" && m.GetParameters().Length == 2);
        if (setter != null)
        {
            try
            {
                var parameters = setter.GetParameters();
                setter.Invoke(collection, new[]
                {
                    ConvertNumeric(index, parameters[0].ParameterType),
                    ConvertForTarget(value, parameters[1].ParameterType)
                });
                return true;
            }
            catch { }
        }

        var item = type.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (item != null && item.CanWrite)
        {
            try
            {
                item.SetValue(collection, value, new object[] { index });
                return true;
            }
            catch { }
        }

        return false;
    }

    private static object CreateArray(Type arrayType, int count, out string error)
    {
        foreach (var ctor in arrayType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var p = ctor.GetParameters();
            if (p.Length != 1)
                continue;

            try
            {
                var arg = ConvertNumeric(count, p[0].ParameterType);
                error = string.Empty;
                return ctor.Invoke(new[] { arg });
            }
            catch
            {
                // Try the next one-argument constructor.
            }
        }

        error = $"no usable one-argument length constructor on {arrayType.FullName}";
        return null;
    }

    private static object ConvertNumeric(int value, Type targetType)
    {
        if (targetType == typeof(int)) return value;
        if (targetType == typeof(uint)) return (uint)value;
        if (targetType == typeof(long)) return (long)value;
        if (targetType == typeof(ulong)) return (ulong)value;
        if (targetType == typeof(short)) return (short)value;
        if (targetType == typeof(ushort)) return (ushort)value;
        if (targetType == typeof(byte)) return (byte)value;
        if (targetType == typeof(sbyte)) return (sbyte)value;
        return Convert.ChangeType(value, targetType);
    }

}
