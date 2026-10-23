# XCOM voice curation (#73)

First pruning pass: only the strongest currently imported profiles can receive a
new assignment, including old records that have no voice ID. Selection remains
gender-matched and least-used-first. Existing assignments are never rerolled.

## Minimum gate

- At least 42 mapped route slots.
- At least 12 distinct asset names; repeating one clip across many slots is not variety.
- All these routes must exist: ready, moving, spotted, kill, taking_fire, miss,
  grenade_frag, success, emp_success, emp_failed, morale_recovery, ready_quiet (index 0).
- Sparse profiles and their assets remain available only for saved-assignment compatibility.

This leaves **31 eligible profiles: 22 male and 9 female**. The original 30 profiles
still have 42/180 mapped slots and 12 imported asset names each, with 138 silent slots.
Trepang 2 BlackOps now has 180/180 mapped slots and 211 selected recordings (0.17.2/0.17.3 reuse
existing recordings as generic damage taunts and building-collapse warnings). The minimum is an assignment gate, NOT a cap or delivery target.
These are curated test candidates, not human listening acceptance. All other 55
profiles and their 607 imported assets are preserved unchanged. The 23 October
source-byte audit finds 603 distinct source contents behind those 607 names;
asset count is not necessarily recording variety.

## Retained profiles

Male: Anthony Carmine, Augustus Cole, Baird, Bautista, COG Vermelo, Clayton Carmine,
Delmont Walker, Dizzy Wallin, Fahz Chutani, Gabriel Diaz, Garron Paduk, Griffin, JD,
Jeremiah Keegan, Marcus, Michael Barrick, Minh Young Kim, Prescott G5, Sid Redburn,
Tai Kaliso, UIR Male, Trepang 2 BlackOps.

Female: Bernadette Mataki, COG Casan, Commando Akagi, Kait, Lahni Kaliso, Mikayla Dorn,
Reyna Torres, Sam Byrne, UIR Female.

## Trepang 2 BlackOps full-inventory private candidate

Curation starts from all 1,525 content records and 2,460 original cue aliases, not
the old 42-recording test. Every content has an explicit private decision:
211 use / 1,190 review / 124 reject. Same-wording alternatives are retained for
possible intonation review, not counted as new sentences. Uncertain or unestablished
contexts are not deployed. Only five user wording checks have human evidence.

The initial 0.17.0 candidate mapped 167 native slots; 146 routes had differently worded variants.
Each recording has its own caption. Tactical playback selects an immutable
audio/caption/native-Sound-ID pair, avoiding immediate repeats within that route.
An owned SAY-node copy preserves native speaker selection and timing; shared
templates and donor banks are not edited during playback. Legacy same-wording
variation routes retain their existing behavior. Direct hiring/promotion fields
use the route's first recording, as does a safe preparation-error fallback.

Specialized sightings use truthful generic sightings rather than invented enemy
types. EMP success uses neutral acknowledgment. Versions 0.17.2/0.17.3 map the
remaining damage/destruction slots with existing general taunts and hazard warnings,
not literal hit/destruction reports: 180 slots and 156 captioned variant routes.
Quiet/shout/hurt delivery, wording beyond the five checks, and the audio mix still
require listening feedback.

Audio, transcripts and per-clip mappings remain private; no redistribution
permission is assumed. Saved assignments are not rerolled. #73 remains open for
listening/gameplay acceptance and review candidates; this is not a finished bank.

## Structured proposals for the other 55 voices — NOT installed

The full 34,144-row/58-pack CSV already exists. Its 1,378 ASR-review flags are not
the usable inventory. All 34,144 retained source files were found and hashed on
23 October (32,195 distinct byte contents). Three robot packs remain excluded.

The first additive, text/context-reviewed proposal uses 26 exact-utterance groups,
not keyword matches, with separate conservative ASR rejection gates. It proposes
1,619 genuinely additional source contents and 1,155 previously silent native
slots across all 55 human profiles. Existing source hashes are reused rather than
renamed/counting the same audio twice. Same-wording alternatives are not added
merely because their bytes differ. Missing support is not filled to meet a quota.

These are **proposals**, not new shipped coverage or complete semantic/listening
curation. Generic confirmations in specialized kill slots do not identify enemy
types. Quiet/shouting/hurt delivery is not inferred. Baseline mappings and fallback
order are retained, not retrospectively approved: 1,761 baseline route/asset
references still require review under this pass. The existing assignment gate
would admit 36 profiles instead of 31; that change has not been activated.

Private outputs include per-source decisions, per-voice and per-slot comparisons,
a baseline audit, an audio-listening queue and unapproved follow-up candidates.
The actual proposed catalog passes production parser/variant checks without
changing the runtime catalog, bundle, version, BlackOps or saved assignments.
Audio authoring/PCM verification, Windows build/install and listening remain open.

The reusable, data-free authoring tool is
[`tools/curate_voice_proposals.py`](tools/curate_voice_proposals.py); see
[`tools/README.md`](tools/README.md). Keep all inputs, approvals, transcripts and
per-clip outputs private. #73 tracks the private checkpoint and next build decision.

## Retired from new assignments (25)

| Catalog ID | Mapped slots | Distinct recordings |
| --- | ---: | ---: |
| adamfenixvp | 21 | 7 |
| aliciavp | 39 | 11 |
| anyavp | 39 | 11 |
| benjamincarminevp | 38 | 10 |
| bishopvp | 36 | 11 |
| commandosanguinovp | 39 | 11 |
| domvp | 39 | 11 |
| fem_bishop_vp | 31 | 9 |
| garycarminevp | 41 | 11 |
| hanacolevp | 39 | 11 |
| hoffmanvp | 40 | 10 |
| jasitakvp | 41 | 11 |
| lesliemacallistervp | 39 | 11 |
| lizziecarminevp | 39 | 11 |
| odst_buck_voice | 36 | 11 |
| odst_dare_voice | 33 | 9 |
| odst_dutch_voice | 31 | 8 |
| odst_fm2_voice | 30 | 9 |
| odst_mickey_voice | 31 | 8 |
| odst_romeo_voice | 33 | 9 |
| onyxguardfemalevp | 35 | 9 |
| onyxguardmalevp | 34 | 9 |
| oscardiazvp | 39 | 11 |
| presscottvp | 29 | 9 |
| sofiahendrikvp | 32 | 9 |

No audio, profiles or saved characters are deleted. Missing events still resolve to
captionless silence, never another speaker. Existing characters with a retired profile
keep it; this update alone does not improve their coverage or change their voice.