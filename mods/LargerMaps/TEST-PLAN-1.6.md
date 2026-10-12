# Larger Maps 1.6 — targeted combat-planning experiment

## Purpose

Check whether disabling MENACE's speculative *attack and buff positioning* calculations on >64 maps makes AI turns faster **while units still move and shoot**. This is not a new pathfinder and not a proven performance fix.

## Setup

1. Close MENACE, back up the save and the current LargerMaps DLL/manifest, then run `build-and-deploy.bat` from this archive.
2. Set `TacticalMapSize=80`, `SimplifyCombatAIPlanning=ON`, `UseLocalTacticalRouting=OFF`. **If 1.5 saved Local Tactical Routing=ON, explicitly turn it OFF** and restart MENACE. Keep other mods unchanged.
3. Start the existing relief-outpost mission, seed `5320037`, with the same eight player units as the 1.4.13/1.5 comparisons.
4. In `MelonLoader/Latest.log`, check for `simplified large-map combat planning: N attack / M buff skills` where N is positive. If N=0 or a warning says the feature was not installed, stop and send the startup log instead of wasting time on gameplay.
5. First verify that Lim and a walker can move normally. Then end one player turn and observe the AI for **up to 3 minutes**. Do not wait for round 6 if AI remains unusably slow.
6. Record whether enemies advance toward objectives, actually shoot when in range, and complete their turns. Send `Latest.log` and `Player.log` if the new AI still stalls or behaves badly.

## Quick rollback without reinstalling

Set `SimplifyCombatAIPlanning=OFF` and restart MENACE. No code-level pathfinding hook is installed by this feature; changing settings while an active tactical scene is running is not supported. If unexpected regressions persist, restore the previous LargerMaps DLL and `modpack.json` together.

## What is and is not demonstrated

We verified `Attack.OnCollect` (RVA `0x7B2342`) and `Assist.OnCollect` (RVA `0x7AEA69`) native branches on `SkillTemplate.AIConfig.InfluencesPositioning` at offset `0x14` in the supplied `GameAssembly.dll`. This is strong evidence the flag will reduce *positioning-collection work* if changed before actors are initialized. It does **not** prove overall AI turn speed or tactical behavior in a live mission. Existing objective/mission-area changes are unchanged from 1.5.
