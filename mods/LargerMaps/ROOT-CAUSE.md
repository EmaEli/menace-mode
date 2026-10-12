# Enemy turn freeze — native task lifetime analysis

**Evidence used:** supplied `GameAssembly.dll`, `dump.cs`, `Player(20261008-065649).log`, `Latest(20261008-065638).log`, Larger Maps 1.3.10 and MENACE Gameplay Suite 0.31.4 authoring source.

## Verified native control flow

- `Menace.Tactical.AI.AIFaction.RunWithTimeout(Action, Actor)` lives at **RVA `0x7995F0`** in the supplied dump/binary; its delegate body `<RunWithTimeout>b__0` lives at **RVA `0x7AD1D0`**.
- `RunWithTimeout` creates a wrapper delegate and invokes native `System.Threading.Tasks.Task.Run(Action)` (RVA `0x1B0F4F0`) to execute that wrapper.
- The wrapper builds a **separate** `Task` with the original AI action (`Task..ctor(Action)`, RVA `0x1B11940`), calls `Task.Start()` (RVA `0x1B10250`), then `Task.Wait(12000)` (RVA `0x1B10E20`). Native operand `0x2EE0` equals **12000 ms**.
- On timeout the wrapper logs `AI task timed out for actor...` and returns, **without awaiting the running inner action task to completion**. The outer task is therefore no longer a reliable completion signal for the actual AI calculation.
- In the provided Player.log, many such timeouts occur **before** `Agent.Evaluate()` dictionary-enumeration exceptions and concurrent dictionary corruption. This supports detached work causing overlapping evaluations. It does not prove that there are no other AI performance or geometry bugs.

## Patch scope

Only the native `RunWithTimeout(Action, Actor)` entry point is Harmony-patched, returning native `Task.Run(action)` **itself**, rather than a wrapper that detaches the inner action after 12 seconds. The game still gets a genuine native Task, still schedules AI evaluations asynchronously, still sees any natural task faults, and can wait until the work really completes.

The 64x64 map, all gameplay options, weapon ranges, AI scoring and rest of Larger Maps are unchanged. Existing MENACE Gameplay Suite 0.31.4 remains unchanged. This is a narrow **concurrency safety** change, not a proven AI speed optimization.

## Limits and testing

A slow task may now hold an enemy phase **longer than 12 seconds** instead of being abandoned. That is intentional data-integrity protection and may reveal other hotspots that need optimization. Absence of the old timeout log is NOT a speed measurement, since this patch bypasses that wrapper. Test whether enemy turns **finish**, note wall-clock durations, and check for remaining `Collection was modified` / concurrent-dictionary exceptions. A separate tile/LoS bug has not been ruled out.

**Build status:** Source-only authoring ZIP. Not compiled or tested in a running MENACE installation in this environment; build with the provided scripts and your existing .NET 6 + Menace Modpack Loader development setup.
