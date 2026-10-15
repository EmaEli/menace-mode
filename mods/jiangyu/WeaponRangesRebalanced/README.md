# Weapon Ranges Rebalanced 2.9.2

Weapon Ranges Rebalanced (WRR) now has a deliberately narrow scope: **weapon ranges plus the verified long-range indirect-fire AI turn-performance fix**.

## What WRR does

- discovers current `WeaponTemplate` and `ModularVehicleWeaponTemplate` objects at runtime;
- applies the realism-oriented configurable `MaxRange` table;
- keeps `MinRange` and `IdealRange` native;
- scales explicit range-overriding secondary attack skills proportionally with their parent weapon;
- auto-classifies recognizable future/mod-added ranged weapons while leaving unknown weapons unchanged;
- supports per-family strength sliders and exact `weapon-overrides.json` range overrides;
- restores captured pre-WRR ranges when disabled;
- keeps the verified long-range indirect-fire large-map AI fix.

At default 100% WRR/Artillery strength the current mortar-family maximum ranges are:

- light mortar: **34**;
- medium 80 mm mortar: **42**;
- heavy 120 mm mortar: **48**.

Secondary explicit-range skills such as battle-rifle alternate fire, SMG/PDW tap fire, underbarrel grenade attacks and range-overriding vehicle mortar attacks continue to scale with their owning weapon rather than being forced to the primary value.

## Long-range indirect-fire AI turn fix

WRR sets `AIConfig.InfluencesPositioning = false` on current long-range no-Line-of-Fire attack skills in the mortar/artillery/rocket-organ/indirect-fire families. In 2.8.1 this covers light/medium/120 mm mortars, pirate/modular rocket organs, Local Forces/Rogue Army artillery and ATGM indirect-fire modes. Direct-fire artillery such as the 15 cm assault howitzer is intentionally excluded.

MENACE defines this flag as whether the AI should actively move into a position specifically to use that behavior. Disabling it keeps the mortar attack usable from valid positions but prevents the expensive attack-specific move-to-fire search that could produce extremely long/timeout turns on enlarged maps and long ranges.

The write is performed through the runtime IL2CPP wrapper and read back for verification when Debug logging is enabled.

## What WRR no longer does

Starting with 2.8, these mechanics were moved to **Weapon Mechanics Rebalanced 0.1**:

- mortar AP changes;
- mortar ammunition/use changes;
- mortar HP/armor-durability damage scaling;
- mortar scatter/suppression rebalance;
- cumulative mortar ranging-fire memory;
- Assault Rifle Sustained Fire AP/ammo economy;
- Battle Rifle Spray AP/ammo economy;
- Aimed Fire AP ceiling.

This means WRR can be installed by users who want only realistic ranges and the indirect-fire AI fix without changing weapon action economy or damage behavior.

## Mod Settings

Open MENACE Mod Settings and select **Weapon Ranges Rebalanced**.

- **Enable Weapon Ranges Rebalanced**
- **Overall rebalance strength (%)**
- **Auto-balance new / unlisted weapons**
- **Apply infantry / special / turret weapons**
- **Apply modular vehicle weapons**
- family-specific range strength sliders;
- **Enable weapon-overrides.json**;
- diagnostics/debug options.

Exact range overrides are read from `weapon-overrides.json`: in the Jiangyu build from `UserData/WeaponRangesRebalanced/`, in the legacy modpack build from the active modpack folder. Values are integer maximum ranges from 1 to 60 and reload automatically while the game is running.

## Logging

Normal gameplay is intentionally silent. **Debug logging** enables concise diagnostics, including the medium-mortar positioning-policy verification. **Log full weapon table (debug)** prints the full range table only when explicitly requested.
