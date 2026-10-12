# Infantry Assault 0.5.2: native skill recognition

## Evidence

Game: MENACE v0.7.15+21883, modded by MENACE Gameplay Suite 0.31.4.

- `Latest(20261008-082000).log`: after tactical initialization, the verbose Assault scan reported `eligibleInfantry=13, present=2, missing=11`, including six named player squads, with eleven `Assault runtime synchronization failed: Object reference not set...` warnings.
- `Player(20261008-082004).log`: six deployed player units (Pike, Darby, Tech, Carda, Corlain and Lim) **already have** `mod.infantry_assault` in their skill lists. Therefore at least those six scan misses are false positives.
- `dump.cs`, `Menace.Tactical.Skills.SkillContainer`: `GetAllSkills()` and `GetSkillsInAddQueue()` return `List<BaseSkill>`.
- Original 0.5.1 `GetSkillId` invokes a cached `MethodInfo` obtained from `Skill.GetID`, even when the current managed wrapper is `BaseSkill`. Runtime reflection requires the MethodInfo declaring type to match the managed wrapper; failure is swallowed and treated as missing.
- The 0.5.1 recognized-skill cache uses `RuntimeHelpers.GetHashCode()` on managed wrappers. A cast/re-wrapper may have another managed identity for the same native object.
- The subsequent `EnsureAssaultSkill` pass can try `AddInherentSkills` and `CreateSkill` for skills which are already present, causing unnecessary native mutations and likely contributing to the observed errors. The exact native `NullReferenceException` site cannot be established from the abbreviated warnings alone.

## Fix

- Use `GetID` on the actual runtime proxy; invoke the cached `Skill.GetID` reflection method only for compatible wrappers.
- Fall back to `BaseSkill.GetTemplate` ID if the runtime wrapper cannot provide `GetID`.
- Cache recognized **native** IL2CPP pointers, not managed proxy hash codes.
- Always inspect both committed and queued skills; repeat the check after a partially failed native inherent refresh before creating a fallback skill.
- Leave `ShouldHaveAssaultSkill` manifest eligibility gate intact. No broad skill grants to unexpected infantry, constructs, wildlife, or vehicles.
- All unrelated module code and the CQB implementation are unchanged. No default verbose logging.

## Runtime test needed

No .NET SDK/MENACE runtime is available in this authoring environment. The modified source and modpack manifests passed offline regression checks; compilation and gameplay verification must happen on the user's MENACE installation.

Expected with `Infantry Assault > Debug logging = ON` temporarily: pre-spawn scan may show zero eligible; after actor creation, eligible troops already owning Assault should no longer appear in the missing list, and the old 11 error burst should disappear. If a few genuinely missing skills remain, keep the logs so we can identify the exact failure actor and stage.
