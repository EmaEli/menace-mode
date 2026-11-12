# Squaddie Field Promotions: native perk audit, 12 November 2026

Related: [#101](https://github.com/EmaEli/menace-mode/issues/101) (readback failure),
[#79](https://github.com/EmaEli/menace-mode/issues/79) (missing Payback),
[#67](https://github.com/EmaEli/menace-mode/issues/67) (generated progression).

## Data and scope

- SFP source/version: **0.22.6**. No gameplay source changes in this investigation.
- Fresh extraction from the installed PC game, **v0.7.18+22828**, using Jiangyu CLI
  1.4.10, not the stale September/October runtime JSON exports.
- GameAssembly SHA-256:
  `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
- Extracted at **2026-11-12T07:31:47Z**: 9,630 template instances, no skipped values;
  **145 native perks**, **21 native perk trees**.
- Compared **129 source assignments**: infantry and vehicle base/advanced pools,
  mentor tier tables and the legacy generic infantry tree. Including initial/clone
  sources, these request **61 distinct native IDs**.
- Verified native tree skill references by asset identity, name and template class.
  All native tree references resolve; all native initial/clone sources exist.
- Compared tiers separately for infantry/vehicle trees, using the game's `_vehicle`
  tree naming convention. This is a comparison with shipped choices, not a claim
  that the game enforces a universal tier for each perk.

## Missing native IDs

| SFP pool | Requested ID | Current catalog |
| --- | --- | --- |
| Infantry advanced tier 1 | `perk.payback` | Absent; `perk.payback_kubra` exists |
| Infantry advanced tier 2 | `perk.standby` | Absent; `perk.standby_unused` exists |

The 0.22.1+ infantry generator filters candidate IDs using a native lookup before
sampling, so these obsolete entries are **not selected for new promotions when the
lookup returns null**. Both still appear in source. Mentor candidates use the same
filter. Saved snapshots are not rerolled.

Do not blindly alias these IDs: a Kubra-specific perk and an explicitly unused
template need an eligibility/effect review before adding them to a generic pool.
The vehicle generator has no equivalent availability callback, although every
vehicle pool/mentor ID exists in this installed catalog.

## Current generated-tree tier differences

| Perk | SFP assignment | Same-role shipped tree tiers |
| --- | --- | --- |
| `perk.hit_the_deck` | Infantry base and mentor: **1** | **2, 3** |
| `perk.sharpshooter` | Infantry advanced: **4** | **2**; vehicle trees use 4 |
| `perk.grind_down` | Vehicle base and mentor: **3** | **2** |
| `perk.to_shreds_vehicle` | Vehicle advanced and mentor: **3** | **2**; an infantry tree uses 3 |

These are **seven assignment entries across four perk IDs**. The comment describing
the pools as native perks observed at those tiers is not consistently true for the
current same-role trees. This alone does not establish a gameplay bug: custom
progression can intentionally rebalance choices.

## Legacy generic-tree tier differences

| Perk | SFP legacy tier | Shipped infantry tree tiers |
| --- | --- | --- |
| `perk.hit_the_deck` | 1 | 2, 3 |
| `perk.solid_grouping` | 2 | 1 |
| `perk.firing_line` | 2 | 4 |
| `perk.zig_zag` | 3 | 1, 2 |
| `perk.command_rally` | 4 | 3 |

Do not silently rewrite this tree or existing saved snapshots: these support older
leaders and may already have purchased perks.

## What this proves about #101

The audit confirms two stale IDs and twelve tier-difference entries overall. It
does **not** identify the cause of the November 10 `Dynamic perk tree readback
mismatch` warning.

- Missing native lookups throw a different exception, before tree assignment.
- The two missing infantry IDs are already availability-filtered.
- `DynamicPerkTrees.Matches` compares the assigned native ID/tier with the saved
  snapshot, not with the game's predefined leader trees. A deliberately different
  tier is therefore not, by itself, a reason for this readback check to fail.
- Offline serialized assets do not expose live cached `GetID()` values or prove
  what the failed generated leader actually read back.

Next diagnosis should compare requested ID with resolved `GetID()` before mutation,
then report the exact initial-perk/tree/option-index/skill-ID/tier mismatch rather
than one combined boolean. Preserve rollback and the existing readback gate.

## Evidence and validation

- Read-only audit script: `.tmp/promotion-logs-tIpvCI/audit-perk-catalog.py`.
- JSON results: `.tmp/promotion-logs-tIpvCI/perk-audit.json`.
- Run with `python3 .tmp/promotion-logs-tIpvCI/audit-perk-catalog.py /tmp/menace-promotion-logs.tIpvCI`.
- Fresh PC cache: `E:\MENACE-Work\Builds\promotion-logs-tIpvCI\perk-audit-cache`.
- Downloaded all three index/manifest/value JSON files and matched remote/local
  SHA-256 hashes. Index hash:
  `072f81247bcd9a10d98a0280764021d1cf23b5e74fdf619ee8783650c3ddd540`;
  values hash:
  `584a8e063883b9c3fbe5c34bd6b38da1e10290a9be0929dae7a30873542e36d1`.
- Audit completed successfully against the fresh export. No game launch, gameplay
  source edits, build/install, save/settings edits or live runtime verification.
  Only isolated analysis scripts/config/cache were created on the PC.