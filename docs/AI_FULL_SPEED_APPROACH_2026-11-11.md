# Contact approach: short-step scoring correction (#17)

## Rejected gameplay and cause

The player rejected 1.12.1 convergence in the same mission on 64x64, then requested
full available movement toward legitimate contact until enemies enter the unit's sight.
The captured log shows many round-2 one-tile moves leaving 73–82 AP. Alert/assignment
counts alone did not establish useful convergence.

The reviewed 0.7.18 native `Move.GetHighestTileScoreScaled` compares
`TileScore.GetScaledScore`: scaled safety + scaled utility minus
`(DistanceToCurrentTile + DistanceScore) * DistancePickScale`.
The AP-derived travel-to-current penalty outweighs the modest contact progress reward.
The native selection can therefore prefer a cheap one-square destination.

## 1.13.0 correction

- A postfix on that existing native candidate selector adds back only travel-to-current
  when comparing affordable movement-only candidates toward a legitimate contact.
- No tile score, path, AP, ammunition, attack value or native target field is rewritten.
  Native movement continues with a member of its own existing candidate list.
- Native safety and mission-distance terms remain. Dangerous or blocked approaches are
  not forced; real movement and post-movement shot/setup costs remain authoritative.
- As soon as any detected opponent passes the actor's own native vision/LOS/concealment
  check, the correction stops. Faction radio detection is not treated as personal sight.
  Undetected actors are never passed to that check or read for live coordinates.
- Native useful actions, defense, deployment, morale/suppression and completed/delayed
  movement guards remain. Contact lifecycle reset and the #100 startup fix are retained.
- Existing default-OFF diagnostics report native/selected coordinates and actual path AP.

## Validation

- Failed-first linked regression reproduced the one-square choice and failed the
  full-progress expectation with a pass-through selection hook.
- Production-linked managed checks now cover a 90-AP forward route beating an 18-AP
  one-square step, native own sight/other opponents, hidden reads, danger, mission distance,
  affordability/reserves, native actions, settings, workers and unchanged native fields.
- Exact native-byte checks pin the ranking formula/call site and own-sight checks to the
  reviewed game SHA256 `494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9`.
- Real proxy metadata validates the new selector, score and actor-sight signatures.

## Verified test installation

- Source `83b117cac246370dfa30eb2b8a190e6a4f6a0242` committed/pushed to dev;
  unrelated dirty work was not staged or shipped. All 85 tracked source payloads in the
  isolated canonical kit match that commit; three compiled payloads are inventoried.
- Local checks: 19,780 linked checks, 303 behavior checks, 232 real proxy/production IL
  contracts, nine exact native-byte tests, 158 LargerMaps Python tests (eight expected
  skips), and 210 GameplaySuite Python tests pass. Production Release: zero warnings/errors.
- Canonical AI-only BuildKit: 90 files, one package, SHA256
  `a488927b88337348a15b5682a68496b234b80bd1e900824690e4f5db31d34af2`.
  Inventory, clean extracted-source Release build and 18,011 scoped shared/artifact
  checks pass. Existing nullable warnings are confined to the shared offline harness.
- Hash-matched SFTP upload/download and remote script/archive checks passed before use.
  Current MENACE 0.7.18+22828 native hash and all 177 proxy/runtime references match.
- Canonical Windows BuildOnly, then Standalone installation passed with private SDK
  10.0.113, zero production warnings/errors and the same 232/303/19,780 checks.
- Installed manifest/assembly/file: `1.13.0 / 1.13.0.0 / 1.13.0.0`; DLL SHA256
  `C3E89D6CD5D8EE0396DC2B6EBC87671857D7DCC1292A0E0986AC80F8016BE057`.
  Independent verification matches the installed files to the Windows stage/journal.
- Affected-only backup and rollback journal under the game's `_MENACE_Backups`:
  `20261111-202902-831f2dc8043b4ed5ba2c61ccd3bb84e9`. All three original 1.12.1
  files match the preflight hashes. No loose duplicate was present or added.
- All 1,464 unrelated Mods/UserData files and 177 references are unchanged. Settings
  companion/loader reused; existing support/contact diagnostics remain ON, unedited.
  Game closed before/after; no saves or unrelated mods were modified.
- Retained stage: `E:\MENACE-Work\Builds\AI1130-approach-20261111`; canonical log:
  `E:\MENACE-Work\Temp\MENACE-install.log`. Local gate logs use `.tmp/ai1130-*`.

Startup, native hook dispatch, actual movement and gameplay acceptance remain untested
for 1.13.0. Try the same 64x64 mission for a few enemy turns: observe longer approach
moves while enemies are out of personal sight, followed by normal tactical positioning.
Retrieve fresh Latest.log after the player's result. #17 remains open; no public release
or promotion to main is implied.

Tracking: [#17 test-install evidence](https://github.com/EmaEli/menace-mode/issues/17#issuecomment-6805338737).

## First gameplay capture

- Closed-game `Latest.log` captured after the user's test: 11,012,060 bytes / 32,268
  lines; SHA256 `9db0a12fbf2d9c4270c0321e5eb2f6b6e352db1f20bd871168a22d05ca20197d`.
  Transfer round-trip passed. Remote manifest is 1.13.0 and DLL hash matches the
  verified install `C3E89D6CD5D8EE0396DC2B6EBC87671857D7DCC1292A0E0986AC80F8016BE057`.
- Log confirms 1.13.0 initialized and the enlarged map is 64×64. Round 1 had zero
  faction-6 contacts; subsequent refreshes record 3, 4, 8 and 8 contacts in rounds
  2–5. In round 2, 44/45 units were assigned; 43 were awakened.
- The opt-in `march-choice` trace recorded 108 changed movement choices: 40 in round 2,
  38 in round 3, 28 in round 4 and two in round 5. Native-cost median was 18 AP;
  selected-path-cost median was 90 AP in rounds 2–4 (round 5: 82.5 AP, only two
  records). Thus the implemented selector ran and preferred longer existing routes.
- This trace records the selected native candidate and its estimated path AP cost,
  **not proof that each move executed or its destination was reached**. It is strong
  evidence the short-step choice was overridden, but not by itself gameplay acceptance.
  Player's visual/behavioral verdict and any further turn evidence remain pending.
- No startup crash observed; the game exited and the log snapshot is closed-game.
  Capture path on PC: `E:\MENACE-Work\Builds\AI1130-approach-20261111\Latest-after-test.log`.
- Tracking: [#17 gameplay capture](https://github.com/EmaEli/menace-mode/issues/17#issuecomment-6807278208).