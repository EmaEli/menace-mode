# Garrison exit crash — #62 / #44

## Confirmed evidence

- User feedback: MENACE crashed while exiting a building with GarrisonableBuildings
  **1.3.1**. Its prior **1.3.0** exit deadlock is a separate failed iteration.
- Game **0.7.18+22828**, .NET runtime **6.0.36**, CoordinatedFire **1.6.13** already loaded.
- Fresh Latest.log: garrison hooks installed; native `Default->Deployed` at
  **09:29:19.528**. No managed fatal exception or successful exit is logged.
- WER Application Error **09:29:21.726 +01:00**, PID **2352**:
  **coreclr.dll**, access violation **c0000005**, fault offset **0x3c129**.
- Matching private minidump **Menace.exe.2352.dmp**, 63,372,195 bytes;
  exception OS thread **0x3700**. Game confirmed stopped after the crash.
- `dotnet-dump clrstack -f -r`: native compilation via
  `IL_STUB_PInvoke -> MonoMod...CompileMethodHook -> IL_STUB_ReversePInvoke`.
  CompileMethodHook's **RDI=0xcac00fef40** contains CORINFO_METHOD_INFO:
  method **0x7ffc5556f900**, scope **0x7ffc5556d5b8**, IL **0x259f65d3f04**, size **284**.
- The dump maps IL into **GarrisonableBuildings.dll**, base **0x259f65d0000**,
  IL RVA **0x3f04**. Exact installed DLL SHA256:
  **61d13607aa480c933a789719878640abada56c6bb2d8a730e87a7e6702f65e7b**.
- Actual DLL metadata maps that RVA to **Plugin.BeginGarrisonMovement**,
  token **0x06000066**, body RVA **0x3ef8**, 12-byte header, 284 IL bytes,
  one exception region, implementation flags **0** (no NoOptimization/NoInlining).

This attributes the failure to compilation of the new helper and matches the known
#44 optimizer signature. It does **not** demonstrate an invalid native exit pointer,
property-update recursion, AP error, or failure in another mod. Do not rewrite the
movement transaction based only on those hypotheses.

## Narrow 1.3.2 safeguard

- Add **NoInlining | NoOptimization** only to the crash-attributed helper, following
  the existing CoordinatedFire/Overwatch mitigation. Retain shared main-thread PrepareJit.
- Do not change deployment, preview restoration, exit, status checks, AP or logging logic.
- Add a regression assertion for the emitted implementation flags and production-helper
  tests preserving virtual current-property dispatch during preview and actual movement.
- The new assertion fails against unguarded 1.3.1. Managed tests do not reproduce the
  Windows IL2CPP/MonoMod optimizer; precompilation alone had not prevented this crash.

## Validation status

- Production-hook harness: **77 assertions passed**, including the safeguard and virtual
  dispatch. Full GameplaySuite Python suite: **183 tests passed**. Selected-package
  offline C# runner: **17,905 checks passed**, including real 1.3.2.0 DLL metadata.
- Real Release net6 DLL: helper flags **72 / 0x48**, same token/RVAs, 284 IL bytes and
  one exception region. Helper IL SHA256 is identical before/after:
  **ebdfcd2be7fc32b56c77512f39b4a959976f827cc63dca567c479eb05eee2cfe**.

Native crash reproduction and 1.3.2 gameplay acceptance remain unperformed. Keep #62 and
#44 open. Retained private artifacts live under `.tmp/` with crash/131/20261105 names;
raw dumps and logs must not be published. Build/install results are recorded in the issues.