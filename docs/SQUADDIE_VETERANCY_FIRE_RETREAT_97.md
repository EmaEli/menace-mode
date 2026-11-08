# Burning vehicles and delayed retreat — #97

## Evidence

- Previous-session Unity Player log: NullReferenceException in UnityEngine.Behaviour.set_enabled, called by PathMover.StartMovement through the delayed StartPathInternal callback and Element.DelayImplElement.
- The native 0.7.18 StartMovement implementation enables the mover without checking whether its Unity component survived the delay. This supports the reported destruction-during-retreat race; the stack alone does not identify a specific fire effect.
- Current MelonLoader snapshot had no matching exception. The relevant previous Unity log SHA256 is 32b0da292b7ee1eac42cd38d4352c60ad5ce5951857b193eabd9ad7761e293e1; raw logs remain private and excluded from Git.

## Implementation

Source commit: c80f851. SquaddieVeterancy 1.10.4:

- Guard the actual delayed native StartMovement entrypoint, not just the original MoveTo request. Reject destroyed Unity movers/elements and dead/dying owners.
- Wait for native skills and turn-start work to settle before starting Break Contact.
- Attempt the native special.vehicle_extinguish_fire skill once per retreat activation when a small, medium or big vehicle-fire effect is present. Preserve native availability, targeting and AP checks; retreat uses remaining AP after the skill finishes.
- Cancel dead retreat state without calling MoveTo or writing action points on the destroyed actor.
- Retain the existing native pathfinding and no-path fallback for living actors.

## Validation and candidate

- Local production DLL built against the current PC's 119 hash-matching proxy assemblies.
- Focused vehicle regression and distribution tests passed; native metadata gate passed 115 checks including 74 production binary type references.
- Selected-package offline checks passed (18,009 assertions when the candidate was built).
- Broader Python suite: 205 tests, one failure in the unrelated ammo-diagnostics package-version alignment test while weapon-module edits were also in progress. No unrelated weapon code was changed for this fix.
- Canonical Windows installer with Standalone / SquaddieVeterancy / UseInstalledSettings / BuildOnly passed. The Windows build reports 12 existing warnings and zero errors; all 115 native checks passed there too.
- Candidate ZIP SHA256: f19de886d2e6440b59d72ea1b65f4dc173793a54d2c8861072b36a910b8db6af. It is an inventoried working-tree snapshot made before the source commit, not a published release rebuild.
- Verified candidate directory on menace-pc: E:\MENACE-Work\Builds\SV-1.10.4-fire-flee-20261108.

## Deployment status

The final deployment check found MENACE still running with SquaddieVeterancy 1.10.3. Installation was deferred; no live mod, save or settings file changed. The prepared install-if-closed script uses the canonical installer and rejects unexpected installed versions.

After the game closes, install the candidate through the canonical installer, then test lethal fire damage during retreat and a surviving burning vehicle in Break Contact. Retrieve fresh MelonLoader and Unity logs after the test. In-game acceptance is pending.