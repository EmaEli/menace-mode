# SFP 0.22.1 — failure recovery test installation

Related: #79, #27, #67. Source commit: `1f991ba` on `dev`.
This is scoped build/install evidence, **not gameplay acceptance**.

## Diagnosis and behavior

- The retrieved SFP 0.22.0 log reported at 08:10:12.098: missing native perk template
  `perk.payback`. This ID exists in historical extracted data, but the current native lookup
  returned null while constructing the generated perk tree. It is not evidence of source loss.
- New infantry snapshots now filter base, advanced and mentor choices against native availability.
  Existing saved perk snapshots are not regenerated. Too few available options still fail safely.
- Uncommitted dossier redemptions grant one native `commodity.high_value_salvage` with base trade
  value 40, checking the returned item and inventory count. It is fixed compensation, not the exact
  configured/native-modified purchase price, and does not undo the native dossier purchase counter.
- Successful promotions grant no compensation. Uncertain source rollback requires reload; an
  unverified compensation insertion is not automatically repeated. No recursive dossier grant.
- Earlier failed purchases cannot be inferred safely from a save; reload a pre-purchase save
  to undo those. Installation does not rewrite saves or add retrospective compensation.

## Local and bundled-source validation

- Real-reference Release build: zero warnings/errors.
- 29 authoring checks, 21 package Python tests, and 11,702 offline/package behavior checks passed.
  Native/Unity calls are simulated by the offline executable, not exercised in MENACE.
- Broader shared Python run: 165/166 passed. The unrelated work-in-progress Overwatch source was
  0.33.3.0 while its migration inventory row still said 0.33.2.0. No Overwatch source was changed
  or deployed by this SFP task. This does not affect the selected SFP installer build.
- BuildKit: 311 inventoried files; archive verification passed.
- Candidate: `SFP-0.22.1-failure-refund-20261025-B.zip`.
- ZIP SHA256: `50dd73ce3968b05dcfd9008eac8044195450ae43197767548d47c5de368d7364`.

## Windows verification

- Confirmed MENACE closed; GameAssembly/proxy hashes matched local pinned 0.7.17 references.
- Canonical Standalone BuildOnly and SFP-only installation passed using private SDK 10.0.113.
  Both builds had zero warnings/errors, 11,702 behavior checks and six installed-settings checks.
- Installed manifest/assembly/file: **0.22.1 / 0.22.1.0 / 0.22.1.0**.
- Installed DLL SHA256: `b2056c9430c9e7f5737842ab6fb0efd09b1ac2f3025780d22328743fd161127b`.
- Installer journal and previous 0.22.0 package retained outside Mods at
  `_MENACE_Backups/20261025-082028-cd0185ce98e94ee5954fc2b8f700b883`.
- Only SFP targets were replaced. All 174 runtime assets match the inventoried kit.
  All 1,140 protected unrelated Mods/UserData files have unchanged hashes. Settings and
  Jiangyu.Loader were reused; no legacy flat SFP DLL remains. No save writes or game launch.
- Local evidence: `.tmp/sfp0221-windows-buildonly.log`, `.tmp/sfp0221-windows-install.log`,
  `.tmp/sfp0221-windows-verify.log`, `.tmp/sfp0221-canonical-install.log` (not published).

## User test

On a disposable non-Ironman campaign, retry promotion from a pre-purchase save. Expect a
new leader in Hire at zero credits, source soldier removed once, and no compensation on success.
If a new attempt fails with a verified rollback, expect exactly one High-Value Salvage item
(trade value 40); reopen Black Market to refresh the inventory. Follow any reload warning.
Report the result; retrieve the fresh Latest.log then. Keep #79 open pending gameplay evidence.