# Private voice proposals

`curate_voice_proposals.py` prepares an additive draft, **not a deployable mod**.
It uses Python's standard library and writes only to a fresh output directory.
Keep input data, exact-utterance approvals and every output in ignored private
storage; none belongs in a public commit or issue comment.

## Inputs and decisions

Run `python3 mods/jiangyu/SquaddieFieldPromotions/tools/curate_voice_proposals.py --help`
for the required paths. Inputs are:

- The immutable full review CSV, with source path/pack/event, original ASR text,
  metrics, status and triage flags. The earlier review flags are not all usable audio.
- A complete current source index (`relative_path`, `source_sha256`, `bytes`,
  `state`), with one entry per CSV row. Never substitute guessed audio hashes.
- The baseline runtime catalog, its private source build manifest (`clips` with
  `assetName`, `profileId`, `sourceRelative`), and the actual native routes JSON.
- A private schema-1 approvals JSON. Its `inputs` object pins SHA-256 digests under
  `csv`, `source_index`, `baseline`, `baseline_sources`, `native`. Each `groups`
  entry has `id`, `sourceEvents`, `nativeFamilies`, `texts`, and `reason`.

Each approval covers a complete normalized utterance AND its source-event
context. Normalization folds case/punctuation only; it does not remove words,
negation or digits. Preserve the original caption, not a correction guessed from
the filename. Unknown target families, ambiguous approvals, source/pack mismatch,
missing provenance, unsafe paths and duplicate index entries fail validation.

ASR `ok`/P4 is necessary but insufficient. Selection also requires finite metrics,
average log probability >= -0.5, no-speech probability <= 0.2, and 0.25–8 seconds.
These are conservative rejection thresholds, not estimates of transcription
accuracy. Identical source bytes with conflicting transcripts are quarantined.

Within a voice/family, retain one candidate per normalized wording, ranking by
ASR metrics with deterministic path ties. Different bytes do not establish useful
intonation variety. Reuse compatible retained assets by source hash. A baseline
caption conflict remains review rather than creating the same audio under a new
name. No minimum-coverage target or arbitrary recording cap drives selection.

## Outputs and remaining gates

Reports account for every row, voice and native slot. Unlisted utterances remain
review, not automatic rejection. New routes get exact per-recording captions;
old recordings/caption order remain unchanged. The proposal is deliberately named
`proposed_voice_profiles.json` and `deploymentReady` is always false.

`newRecordings` counts new per-profile audio assets after source-byte deduplication;
`newSourceContents` counts distinct new hashes across profiles. Per-voice reports
separate asset names from source-byte contents. Byte deduplication does not prove
decoded-PCM equality or human-perceived variety. Current hashes cannot prove that
unhashed historical ASR inputs were identical.

Validate a private catalog with the existing SFP OfflineChecks runner using
`--voice-proposal <private/proposed_voice_profiles.json>`. This exercises the
production catalog parser, paired caption choices, silent missing routes, stable
speaker metadata and retained fallback order. It reports eligibility changes;
passing is not approval to expand the assignment pool or proof of Unity playback.

Before installation, separately review proposed semantics/eligibility, build the
audio bundle with source hashes and pinned Unity, reopen and compare retained PCM,
then run canonical mod build/installer gates. Never copy this draft directly into
the live mod. Follow `docs/MOD_BUILD_DEPLOY_WORKFLOW.md` and the canonical issue #73.

Synthetic regression tests:
`python3 -m unittest discover -s mods/jiangyu/SquaddieFieldPromotions/tests -p test_voice_proposals.py -v`.