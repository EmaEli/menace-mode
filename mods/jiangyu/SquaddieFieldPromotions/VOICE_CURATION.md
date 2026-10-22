# XCOM voice curation — 0.16.0 (#73)

First pruning pass: only the strongest currently imported profiles can receive a
new assignment, including old records that have no voice ID. Selection remains
gender-matched and least-used-first. Existing assignments are never rerolled.

## Minimum gate

- At least 42 mapped route slots.
- At least 12 distinct asset names; repeating one clip across many slots is not variety.
- All these routes must exist: ready, moving, spotted, kill, taking_fire, miss,
  grenade_frag, success, emp_success, emp_failed, morale_recovery, ready_quiet (index 0).
- Sparse profiles and their assets remain available only for saved-assignment compatibility.

This leaves **31 eligible profiles: 22 male and 9 female**. Each currently has 42/180
mapped slots. The original 30 profiles have 12 distinct recordings each; Trepang 2
BlackOps has 42. The remaining **138/180 slots are silent**. This is not a complete
voice bank or a listening/semantic verification claim. The source packs may contain
more usable recordings; this gate describes the current import only.

## Retained profiles

Male: Anthony Carmine, Augustus Cole, Baird, Bautista, COG Vermelo, Clayton Carmine,
Delmont Walker, Dizzy Wallin, Fahz Chutani, Gabriel Diaz, Garron Paduk, Griffin, JD,
Jeremiah Keegan, Marcus, Michael Barrick, Minh Young Kim, Prescott G5, Sid Redburn,
Tai Kaliso, UIR Male, Trepang 2 BlackOps.

Female: Bernadette Mataki, COG Casan, Commando Akagi, Kait, Lahni Kaliso, Mikayla Dorn,
Reyna Torres, Sam Byrne, UIR Female.

## Trepang 2 BlackOps private test

The 42-slot mapping uses 42 distinct source hashes selected from 162 primary and
96 focused automated transcripts. Five clips have user-reported wording checks;
the other 37 have not been human-audited. Four action-adjacent mappings remain
provisional: ready acknowledgement, quiet readiness, EMP success and success.
Audio/catalog artifacts remain private; no redistribution permission is assumed.
Saved assignments are not rerolled, and unmapped events remain captionless silence.

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