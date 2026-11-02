# Occupied 1x2 shelter crash — issue #50

## Evidence

- MENACE 0.7.17+22362; game and Assembly-CSharp hashes match the local 0.7.17 references.
- Installed at the crash: GarrisonableBuildings 1.1.0, BuildingCombatRules 0.3.0,
  InfantryAssault 0.5.10, MenaceModSettings 0.8.2.
- Latest.log SHA256: `46cbdf822ff15c485bc717bc8f6077c94f80d582aa7aba9abe5c14c6d623b312`.
- At 16:48:40.223 the garrison leave surcharge runs. At 16:48:40.226,
  GarrisonableBuildings normalizes then restores `building_civilian_shelter_1x2_01`.
  There is no deferred-occupant collapse record; no managed exception is logged.
- Windows Application Error: 16:48:41, `coreclr.dll`, access violation `c0000005`.
- Crash-thread managed frames from `Menace.exe.13556.dmp`:
  `Buffer.Memmove` → `Il2CppStringToManaged` → `Skill.GetID` →
  `InfantryAssault.GetSkillIdRuntime` → `IsAssaultSkill` → generated detour.
- The native dump confirms `Skill.IsVisible` and `SkillContainer.IsAIDryrun`
  share RVA `0x6C2500`. A shared-body hook's managed Skill wrapper is not proof
  that the receiver has the native Skill class. This provides a concrete unsafe
  classification path; the generated frames do not independently name the hook.

## Candidate fix

InfantryAssault 0.5.11 validates the native class through `TryCast<Skill>()` before
accessing Skill members, then compares the native template pointer to the loaded
Assault template. Classification no longer invokes GetID or converts a native
string. The old cache of skill addresses is removed to avoid stale identity after
address reuse. Unknown/non-Skill receivers keep vanilla behavior.

No GarrisonableBuildings collapse-timing changes are included. A recently detached
occupant can still bypass that mod's pending-collapse branch; test the confirmed
Infantry Assault crash path first rather than changing two mechanisms at once.
Existing working-tree pinned-target cover changes (#80), already in the installed
0.5.10 baseline, remain in the test BuildKit snapshot; unrelated work is preserved.

## Checks and deployment

- 20 production identity-guard assertions against managed native-class stand-ins.
- 171 GameplaySuite Python tests passed; 17,874 offline C# checks passed.
- Local real-proxy Release build: zero warnings/errors.
- Verified 126-file standalone BuildKit SHA256:
  `7f1638e72ce2c03a4bd8cc4282aef07038a206688cac62b03471dc1e6af0e06a`.
- Canonical Windows PowerShell 5.1 BuildOnly and installation passed while the
  game was closed. InfantryAssault built without warnings/errors; the settings
  companion retained its existing JIA005 warning.
- Installed InfantryAssault manifest 0.5.11, assembly/file 0.5.11.0; DLL SHA256:
  `28e1edf8d8204264338ca0dc3fee4477ff09dca019a60e98fa267b6ee130956d`.
- GarrisonableBuildings remains 1.1.0. MenaceModSettings remains 0.8.2.
- Backup outside Mods: `_MENACE_Backups/20261102-165905-6e3d76b5a563416da73e42bf503d1b84`.
  Originals contain only InfantryAssault, MenaceModSettings and Jiangyu.Loader.dll.
- Independent post-install audit: all 1,341 protected unrelated Mods/UserData files
  retain the same paths and hashes. The first audit wrapper nested the JSON array
  on PowerShell 5.1 and falsely reported differences; fixing deserialization and
  rerunning the read-only audit returned zero differences. No reinstall was needed.

## Acceptance pending

Reload the mission before the crash and destroy an occupied 1x2 Civilian Shelter.
Also check that the Assault action remains available. Retrieve fresh Latest.log
after the user's result. Keep #50 open until the user confirms this reproduction.
Managed stand-ins and successful installation are not proof of an in-game fix.