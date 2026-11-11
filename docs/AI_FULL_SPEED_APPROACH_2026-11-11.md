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

Windows build/install evidence will be recorded after canonical gates pass. Startup,
native hook dispatch, actual movement and gameplay acceptance remain untested for 1.13.0.
No public release or promotion to main is implied.