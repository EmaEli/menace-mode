# Squaddie Veterancy new-game crash — #99

Tracking: https://github.com/EmaEli/menace-mode/issues/99

## Report and diagnosis

Nexus report: new-game loading crashes even with Veterancy alone. Last visible loader
message: Firing blackmarket_refresh event. Captured stack enters
IL2CPP.il2cpp_class_get_field_from_name through GameObj.FieldOffset,
ResolveElementParentEntityPtr and ElementSetSquaddieIdPostfix.
Reporter mod/game versions are unknown; current 1.10.4 source contains this path.

Two unsafe assumptions were found:

- The 0.7.18 dump maps Element.SetSquaddieId and unrelated setters to the same RVA
  `0x5F39E0` (including SetRemainingDuration). A native detour can receive a non-Element
  pointer wrapped as a managed Element. The old hook checked only a nonzero pointer.
- When GetEntity returned null or threw, the resolver tried GameObj.ReadPtr with the
  named m_Entity field and a hard-coded offset, entering the exact field-lookup path
  in the report. No parent can be legitimate for an unowned/preview element; interpreting
  another type as Element is not safe even before reaching that fallback.

These findings explain a concrete route to the reported stack; no native reproduction
has established which unrelated receiver, if any, the reporter encountered. The last
black-market message alone does not establish market-event causation.

## Fix: 1.10.5

Source commit `c61a36a` on dev:

- Check the real native type with TryCast before any Element getter or live-map insertion
  in the shared setter hook. Validate native identity in the parent resolver too.
- Remove raw parent-field access entirely. Missing parents and managed wrapper failures
  resolve to zero; existing tactical resolution retries later without a negative cache.
- No XP/save schema, settings, balance or logging changes. Retains #97 retreat safety.

## Validation — 11 November 2026

- Fourteen managed assertions execute the production hook/resolver with native-class
  stand-ins: wrong receiver, null/zero, missing/late parent, repeated ID assignment,
  managed cast/getter failures and inactive runtime/invalid ID. No raw-memory SDK stand-in
  exists, so reintroducing the fallback also fails compilation. This is not IL2CPP dispatch.
- Existing production vehicle-retreat regression tests passed.
- 121 metadata-only native contracts / 74 binary native-type references passed, including
  GetEntity, GetElementIndex and SetSquaddieId; all 121 local proxies match the PC hashes.
- 18,011 selected-package offline C# regression/artifact checks passed.
- Full Python suite: 209 tests, 206 passed; two errors in WMR distribution collection
  and one migration-table failure reflect unrelated WMR 0.7.0 source/compiled/documentation
  version mismatch during concurrent work. No unrelated module files were changed to hide it.
- 106-file single-module BuildKit inventory, ZIP integrity and clean extracted-source
  rebuild passed. Clean local and Windows module builds emitted 12 existing nullable/
  unused-field warnings, zero errors; none are on the changed hook/resolver lines.

## Canonical Windows test installation

- Game: MENACE 0.7.18+22828, GameAssembly SHA256
  `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
- Candidate ZIP SHA256:
  `c990a22775004350da6112650938bda8ac9b812cfcc79d35aa2fb9b9b5320044`.
- Canonical BuildOnly passed before installation; game was closed and only Veterancy
  selected with UseInstalledSettings. Settings companion and loader were reused/preserved.
- Manifest 1.10.5, real AssemblyVersion/FileVersion 1.10.5.0 verified after installation.
- Installed DLL SHA256:
  `48979cdece5fb7ef78cd14e54941fe24b963fb17f8038f3a69df79f326810186`.
- Backup/rollback journal under game root:
  `_MENACE_Backups/20261111-132830-7686ef758ca44bf78d06fe30e0ce94d1`.
  Prior 1.10.4 DLL hash verified against its recorded canonical install. Only the
  SquaddieVeterancy folder was moved/replaced; catalogued absent legacy aliases are inert
  journal entries. The initial verification wrapper incorrectly rejected those entries;
  a read-only catalog-derived recheck verified backup and installed inventories without
  rerunning deployment or relaxing the unrelated-file guard.
- All 1,457 protected unrelated Mods/UserData files were hash-verified unchanged.
  Saves were never written. No public Nexus package was created or published.
- Kit, retained build/install logs and verification scripts:
  `E:\MENACE-Work\Builds\SquaddieVeterancy-1.10.5-newgame-20261111`.
  Stable canonical log: `E:\MENACE-Work\Temp\MENACE-install.log`.

**Pending:** game startup, a new campaign and an existing-campaign load; then ordinary
tactical squaddie/XP tracking. Retrieve fresh Latest.log after user feedback. Build/install
success is not evidence of an in-game fix. Keep #99 open until that scope is accepted.