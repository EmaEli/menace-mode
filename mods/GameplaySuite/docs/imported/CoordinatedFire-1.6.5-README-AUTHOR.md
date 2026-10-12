# Coordinated Fire 1.6.5 - authoring package

This is the **developer/author package**, not the file to upload directly to Nexus.

The public release is a standard Menace Modpack Loader v2 archive. End users will not compile anything and will not run an installer.

## Fresh dump compatibility

- MENACE now exposes `Entity.IsPlayer()` and `Entity.IsPlayerOrPlayerAI()`; the removed `IsPlayerControlled(bool)` path is legacy fallback only.
- No gameplay changes in 1.6.5.

## Build the Nexus archive

1. Install/run MENACE with MelonLoader and Menace Modpack Loader v38.0.5+ at least once.
2. Close MENACE.
3. Run `Build-Nexus-Package.cmd`.
4. Select the MENACE directory if prompted.

Output:

```text
artifacts/CoordinatedFire-1.6.5-Nexus.zip
```

That ZIP is the file intended for Nexus.

## Fresh dump compatibility

- MENACE now exposes `Entity.IsPlayer()` and `Entity.IsPlayerOrPlayerAI()`; the removed `IsPlayerControlled(bool)` path is legacy fallback only.
- No gameplay changes in 1.6.5.

## Build and deploy for local testing

Run:

```text
Build-and-Deploy-Dev.cmd
```

This builds the exact same release archive and also deploys the modpack to:

```text
MENACE/Mods/CoordinatedFire/
```

If the old experimental standalone `MENACE/Mods/CoordinatedFire.dll` exists, the dev deploy script moves it to `UserData/CoordinatedFire/backups/` so both versions cannot load at once.

## Release layout

```text
CoordinatedFire/
  modpack.json
  dlls/
    CoordinatedFire.dll
  source/
    ...exact C# source used for the build...
  README.md
  CHANGELOG.md
  LICENSE
  BUILD-INFO.json
  SHA256SUMS.txt
```

The final archive includes source for transparency and keeps the acknowledgement section from the experimental builds.

## beta.1.6.1 synchronized-drain hotfix

Fixes the safety-drain regression caught by the local core suite: when the synchronized second attack becomes illegal after the first native attack has already started, the transaction now waits for explicit first-attack completion before releasing AP/UI state. A bounded timeout prevents a missing completion event from hanging forever.

## beta.1.3 configurable HUD

Adds screen-relative HUD anchors (including Bottom Right and Top Center), X/Y offsets, scaling, compact mode, reset-to-defaults, and on-screen clamping. This is intended to avoid overlap across different resolutions/aspect ratios and other UI layouts without hard-coding per-resolution coordinates. Combat behavior is unchanged from beta.1.2.

## beta.1.2 English-only pass

All user-facing HUD, Mod Settings, status/diagnostic messages, documentation and build-script text has been reviewed to be English-only. The build scripts also suppress Windows' localized `pause` prompt and print an explicit English prompt instead. Gameplay is unchanged from beta.1.1.

## beta.1.1 hotfix

The first beta.1 authoring package could pass the core and interop smoke tests but fail when compiling the actual Modpack Loader plugin because `Harmony` was resolved as a namespace. beta.1.1 uses the fully-qualified `global::HarmonyLib.Harmony` type in the plugin lifecycle signature and field.


### AI turn-safety note

AI attacks that would spend the acting unit's final AP are intentionally left vanilla so MENACE retains its native AI turn hand-off.

## beta.1.4 AI turn-stall regression

Live testing showed a stall when an AI Coordinated Fire transaction consumed the actor's final AP (for example 40 -> 0). The release now leaves that terminal AI attack completely vanilla. AI Coordinated Fire still applies when AP remain after the combined fee.


## beta.1.5 optional vehicle mode

Adds opt-in vehicle combined fire. The setting is OFF by default so Alpha Strike can remain installed as the vehicle combined-fire provider without Coordinated Fire intercepting vehicle attacks. When enabled, Coordinated Fire pairs two different vehicle weapon templates; identical pairs remain under vanilla twin-link handling. The build also probes `Actor.IsVehicle()` and includes vehicle rule regression tests.


## beta.1.6 synchronized player volley

A new Mod Settings toggle, **Synchronized player volley (0.10s stagger, experimental)**, is OFF by default. When enabled for a manual player Coordinated Fire action, the controller waits for native start evidence from the first child and then makes one normal second `Skill.Use` attempt after 0.10 seconds. It does **not** use `IgnoreUsabilityCheck` or otherwise bypass MENACE's native busy/usability rules.

If the early second call is cleanly rejected, the transaction automatically returns to the established sequential follow-up after the first native action finishes. AI never uses the synchronized path. This lets us test the safest possible overlap behavior without making the stable sequential path depend on it.
