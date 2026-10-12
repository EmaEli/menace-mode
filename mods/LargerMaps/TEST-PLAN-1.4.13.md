# Larger Maps 1.4.13 - Conservative area candidate

## Basis

The reported 64x64 A/B test was slow with v1.4.10 and fast with v1.3.11 and the v1.4.12 legacy-area variant. v1.4.10's objective Init area-role expansion is the *leading suspect*, not a demonstrated root cause. We deliberately skip a redundant 80x80 run of 1.4.12 and assume its unchanged v1.4.10 large-map area handling would be slow.

## Windows build and single end-to-end check

1. Preserve the known-fast v1.3.11 DLL, modpack.json, and save. Keep MENACE closed while building/deploying.
2. Run build-and-deploy.bat, then verify in Latest.log: `Larger Maps 1.4.13`, and with 80x80 selected: `conservative large-map area handlers` and `experimental 80x80 backend active`.
3. Leave other mods and settings unchanged and test the same mission seed 5320037, including contact around turns 5-7. This is the first runtime validation of conservative large-map geometry. There is no guarantee the AI will be faster.
4. Check enemy border spawns, setpiece/objective positioning, blue deployment regions, defense/capture scoring, and reinforcement entry on more than one mission type before any release.
5. If AI is still slow or mission objectives are malformed, restore v1.3.11/1.4.12 and supply Latest.log and Player.log; do not reintroduce vanilla's detached AI timeout.

## Caveats

Static tests validate the source and modeled geometry only. This environment cannot build against the user's Windows MENACE install or execute Unity IL2CPP. The removed broad area role pipeline may have masked genuine mission-bounds bugs; those need selective corrections rather than blanket enlargement.
