# Coordinated Fire

**Version 1.6.7** for MENACE.

Version 1.6.7 additionally recovers the verified asynchronous AP-zero side effect from long-running/setup child attacks while Coordinated Fire exclusively owns the action, preventing a valid salvo from permanently disabling the feature. Version 1.6.6 fixed the AP-accounting shutdown reported with Lim's **Run & Gun / Full Send**. Coordinated Fire now lets MENACE execute each child attack without a native AP debit, while keeping normal ammo/limited-use consumption and settling the single combined AP fee once after the volley. The existing synchronized-volley cleanup hardening remains unchanged.

Coordinated Fire lets an infantry squad fire its normal squad weapon and the squad leader's special weapon at the same target as one coordinated action. An **optional vehicle mode** can apply the same rule to two different vehicle weapon types.

- AP cost = the **higher** of the two normal attack costs, paid once.
- Both attacks use MENACE's native damage, accuracy, cover, suppression, animations and ammo handling.
- Both weapons consume their own ammunition when their native attack executes.
- Deployment / tripod / setup requirements must already be satisfied.
- AI can use Coordinated Fire too when MENACE has chosen a target that both weapons can legally attack.
- Optional vehicle support is **OFF by default** for compatibility with Alpha Strike and other vehicle combined-fire mods.
- Optional **Synchronized player volley** mode makes a guarded 0.10-second early follow-up attempt for a more simultaneous-looking volley. It never bypasses MENACE usability/busy checks and cleanly falls back to sequential fire if the game rejects the overlap.
- Vehicle mode combines two different weapon templates; identical weapon pairs are left to MENACE's native twin-link behavior.

## Requirements

- MENACE
- MelonLoader
- **Menace Modpack Loader v38.0.5 or newer**

The easiest route is the Menace Mod Manager, which can install the required loaders and manage modpacks.

## Installation

### Menace Mod Manager

Download the Nexus archive and add it to the Menace Mod Manager. No compilation or installer script is required.

### Manual

Extract the archive so the game has this structure:

```text
MENACE/
  Mods/
    CoordinatedFire/
      modpack.json
      dlls/
        CoordinatedFire.dll
```

If you tested an old pre-0.3 standalone build, remove this obsolete file first:

```text
MENACE/Mods/CoordinatedFire.dll
```

Do **not** remove `Menace.ModpackLoader.dll`.

## Interface

The old large debug panel is gone.

During the player's tactical turn, a compact Coordinated Fire control appears above the lower HUD:

- the two small mode buttons show the squad and leader attacks; click either one to cycle if a weapon exposes multiple supported fire modes;
- the main button starts combined targeting;
- the status line explains why the action is unavailable, including **NEEDS DEPLOY** / **NEEDS SETUP** requirements;
- diagnostics are moved out of the tactical HUD into the Modpack Loader developer console panel.

Interface position, scale and visibility can be changed in **Mod Settings -> Coordinated Fire**. The HUD is anchored relative to the current screen size, supports user offsets, and is clamped so it cannot be moved fully off-screen.

## Settings

The Modpack Loader persists these settings in its normal `UserData/ModSettings.json` file:

- **Enable Coordinated Fire** - master switch.
- **AI uses Coordinated Fire** - enabled by default.
- **Enable vehicle Coordinated Fire** - **disabled by default**. Turn this on only when you want Coordinated Fire to provide mixed-weapon vehicle volleys. Leave it off when using Alpha Strike for vehicle combined fire.
- **Synchronized player volley (0.10s stagger, experimental)** - **disabled by default**. For manual player Coordinated Fire only, the second native attack is attempted 0.10 seconds after the first begins. MENACE native checks are not bypassed; a clean rejection automatically falls back to the normal sequential follow-up. AI remains on the proven sequential path.
- **Show tactical button** - show/hide the compact control.
- **Show HUD when unavailable** - useful for seeing setup/deployment requirements.
- **HUD position** - Bottom Center, Bottom Left, Bottom Right, Top Center, Top Left, Top Right, or Center (Custom Offset).
- **Horizontal offset (px)** - fine-tune the HUD left/right after anchoring.
- **Vertical offset (px)** - fine-tune the HUD up/down after anchoring.
- **HUD scale** - 0.65x to 1.5x; automatically reduced further only if needed to fit an unusually small screen.
- **Compact HUD** - shows only the main Coordinated Fire button.
- **Reset HUD layout to defaults** - restores Bottom Center, zero offsets, 1.0x scale and full HUD mode.
- **Debug logging** - disabled by default. Enables detailed runtime diagnostics only when troubleshooting; normal gameplay stays quiet.

## Vehicle mode and Alpha Strike compatibility

Vehicle support is deliberately opt-in. With **Enable vehicle Coordinated Fire = Off**, this mod does not convert vehicle attacks, so Alpha Strike can remain the vehicle combined-fire provider.

If vehicle Coordinated Fire is enabled while an Alpha Strike assembly is detected, the compatibility warning is available when **Debug logging** is enabled. Running both vehicle combined-fire systems at the same time is not recommended.

For vehicles, the mod discovers supported native attacks from different equipped weapon templates and uses the same max-AP / native-ammo / shared-target rules as infantry. If a vehicle exposes several compatible fire-mode combinations, clicking the two small weapon labels cycles backward/forward through the available pairs.

## Firing timing / current limitation

The default and AI paths remain fully sequential through MENACE native actions. With **Synchronized player volley** enabled, manual player attacks make one guarded early follow-up attempt after a 0.10-second stagger. This does **not** bypass MENACE native usability/busy checks: if MENACE refuses the overlap, Coordinated Fire automatically returns to the proven sequential path.

The second attack is always revalidated before launch. If the target/weapon state becomes illegal, the second shot is cancelled rather than forced.

## AI-assisted development disclosure

OpenAI / ChatGPT was used extensively as a development assistant for research, code generation, reverse-engineering analysis, debugging, test design and documentation. The mod was iteratively tested in MENACE and corrected based on actual gameplay behavior and logs. Promotional artwork may also use AI-generated imagery.

Existing MENACE mods published on Nexus were used as references and inspiration where credited below; Coordinated Fire is an independent implementation and does not redistribute their code or assets.

## Credits / acknowledgements

**JackAttackMenace - Alpha Strike and Death From Above**  
Thanks for the gameplay inspiration of combining multiple weapon attacks while paying the higher AP cost. Coordinated Fire is an independent implementation; no code, DLLs, icons or assets from Alpha Strike are included.

**p0ss, antistrategie and Menace Modkit / Modpack Loader contributors**  
Thanks for MENACE modding documentation, runtime loader work and reverse-engineering references.

**LavaGang / MelonLoader / Il2CppInterop contributors**  
Thanks for the loading and IL2CPP interoperability infrastructure.

**Andreas Pardeike / Harmony contributors**  
Thanks for Harmony runtime patching.

None of the projects or authors above has tested or endorsed Coordinated Fire.

## Uninstall

Close MENACE and remove:

```text
MENACE/Mods/CoordinatedFire/
```

Settings and diagnostic files in `UserData` can be kept or removed separately.


### AI turn-safety note

AI attacks that would spend the acting unit's final AP are intentionally left vanilla so MENACE retains its native AI turn hand-off.
