# Squaddie Field Promotions 0.1 - Jiangyu authoring project

**Status: EXPERIMENTAL SOURCE. Not compiled or tested in-game.**

This is a **real Jiangyu code mod**, not an `IModpackPlugin` for Menace Modpack Loader.
The old `SquaddieFieldPromotions-0.1-authoring.zip` is obsolete; do not install both.

## Scope of version 0.1

- Jiangyu template cloning through `templates/control.kdl` with a harmless, **unrecruited** control template.
- F8: inspect whether the Jiangyu control clone and the runtime dynamic-clone methods exist. Read-only.
- F9: explicitly authorised test. Create **one unique dynamic leader and speaker template**, assign that leader a selected existing squaddie's name, nickname, gender and skin, and add to the native **Hire** candidate list.
- When **Squaddie Veterancy** is installed, read XP via the existing optional `SquaddieProbe.CompatibilityApi.GetSquaddieXp(int)` API. When unavailable, use competent low-level leader attributes.
- No automatic changes; no per-turn/per-frame logging. Explicit F8/F9 output only.

**Not included:** promotion dossier in the Black Market; removing the squaddie from the pool; permanent promotions; save/load support; silent voice/neutral portraits; new training or perk trees; unlimited recruitment limit. The newly created candidate has a unique identity, but **saving it is unsafe in 0.1**. Static control clone is diagnostic ONLY and is **not** a cap on later dynamic generation.

This lab invokes Jiangyu.Loader's **internal** template clone methods by reflection, with runtime signature checks. They are not public SDK APIs. It is designed to fail closed and report an incompatibility if a Jiangyu update changes them. Source is not guaranteed to compile or run until verified with your exact game/Jiangyu versions.

## Requirements

- MENACE Windows with MelonLoader, and Jiangyu.Loader installed.
- Jiangyu CLI configured (the `jiangyu` command works at a terminal).
- .NET SDK and Jiangyu SDK/game references configured through Jiangyu CLI.
- A disposable **non-Ironman** campaign and a backup of any save folder before testing.

## Build and deploy (Windows)

1. Extract the **authoring ZIP**, not directly inside MENACE/Mods.
2. Double-click `Build-and-Deploy.cmd` from the extracted project directory. It calls:

   ```powershell
   jiangyu code sync
   jiangyu compile
   jiangyu deploy
   jiangyu package --output artifacts
   ```

3. If unsuccessful, share `artifacts/build-and-deploy.log` from this project.
4. On success, Jiangyu deploys the compiled output to `MENACE/Mods/SquaddieFieldPromotions/` (game path comes from Jiangyu's global config).

You do **not** install the files under `code/` or `templates/` as a standalone compiled mod. Jiangyu builds a `compiled/` directory and deploys **that**.

## In-game lab procedure

1. Enter a fresh **throwaway** non-Ironman campaign. Do not use an important campaign.
2. Press **F8** while in the strategic layer. The output should include `[SFP 0.1] PRECHECK OK` in `MelonLoader/Latest.log`.
3. If F8 reports an error, **stop** and send the log. Do not attempt F9.
4. Manually create an empty file named `ENABLE_UNSAFE_LAB_TEST.txt` in the **deployed** `MENACE/Mods/SquaddieFieldPromotions/` folder. Be careful: Windows Explorer may hide `.txt` file extensions.
5. Press **F9**. It tries to register a new identity and add it to Hire. Check `MelonLoader/Latest.log` for `[SFP 0.1] TRIAL CREATED` or `[SFP 0.1] TRIAL FAILED`.
6. Close and reopen the normal **Hire** screen. Inspect whether the candidate is present; a borrowed vanilla portrait/voice is expected during this lab.
7. **QUIT WITHOUT SAVING.** The generated runtime template will NOT be reconstructed on restart yet. Disable the marker by deleting it.

**Warning:** automatic saves can still happen in some game modes. Run this only on a disposable campaign you will never rely on; exclude Ironman. Reopening any save containing a trial leader may fail.

## Debug and fallback stat rules

Fallback (with no Squaddie Veterancy XP) in game attribute order:

| Agility | Weapon Skill | Valour | Toughness | Vitality | Precision | Positioning |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 65 | 65 | 62 | 50 | 50 | 5 | 45 |

With XP available, add the following conservative bonus to each non-Precision attribute; Precision receives half the bonus, and all values are capped at 85:

| XP | Bonus |
| ---: | ---: |
| 0-24 | 0 |
| 25-49 | +2 |
| 50-99 | +4 |
| 100-149 | +6 |
| 150-249 | +8 |
| 250-499 | +10 |
| 500+ | +12 |

These are **provisional design values**, not a proven conversion between Squaddie Veterancy XP and MENACE leader attributes.

## Troubleshooting and next iteration

- Missing `jiangyu` command: install/configure the Jiangyu CLI first.
- F8 baseline clone missing: inspect `MelonLoader/Latest.log` and Jiangyu's compile/deploy output.
- F8 bridge missing: Jiangyu internals differ; send the log and Jiangyu version, don't patch blind.
- F9 succeeded but Hire missing: inspect the normal Hire UI state and provide `Latest.log`.
- No save/load tests in 0.1. Next iteration must establish **pre-roster** rehydration, because Jiangyu's `Context.State` load hook is after `SaveSystem.ExecLoad`, and may be too late to register the IDs saved in vanilla leader fields.

Related issue: https://github.com/EmaEli/menace-mode/issues/27

Research references:
- https://github.com/antistrategie/jiangyu/blob/main/site/sdk/index.md
- https://github.com/antistrategie/jiangyu/blob/main/site/sdk/hotkeys.md
- https://github.com/antistrategie/jiangyu/blob/main/docs/research/verified/template-cloning.md
