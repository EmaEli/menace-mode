"""Static regression checks for the MENACE camera authoring package.

These do NOT replace a real Windows dotnet build or tactical mission smoke test.
Run: python tests/test_03_static.py
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
src = (root/'src/ThirdPersonCameraMod.cs').read_text(encoding='utf-8')
project = (root/'ThirdPersonCamera.csproj').read_text(encoding='utf-8')
builder = (root/'tools/build-and-deploy.ps1').read_text(encoding='utf-8')
container_filter = re.search(
    r'private static bool ShouldSkipFiringCameraFromEnclosedBuilding\(.*?\n        \}', src, re.S)

checks = {
    'release identity 1.2.2': '"Cinematic Action Camera", "1.2.2"' in src and '<Version>1.2.2</Version>' in project,
    'MEN-006 compatibility contract': 'public static bool IsCinematicViewActive' in src,
    'native tactical UI visibility used': 'UITactical.SetUIVisibility(false)' in src and 'UITactical.SetUIVisibility(true)' in src,
    'UI restored by exit even when camera fails': re.search(r'private void ExitThirdPerson\(.*?finally\s*\{.*?RestoreTacticalUi\(\);', src, re.S) is not None,
    'depth of field is temporarily disabled and restored': all(x in src for x in (
        'private void DisableDepthOfField()', 'settings.DepthOfFieldQuality = 0;',
        '_savedDepthOfFieldQuality = settings.DepthOfFieldQuality;',
        'settings.DepthOfFieldQuality = _savedDepthOfFieldQuality;',
        'private void RestoreDepthOfField()', 'settings.ApplyToAllCameras();')) and
        re.search(r'private void ExitThirdPerson\(.*?finally\s*\{[^}]*?RestoreDepthOfField\(\);', src, re.S) is not None and
        'PersistentPlayerSettings.Save()' not in src,
    'UI restored on scene exit': 'override void OnSceneWasUnloaded' in src and 'ExitThirdPerson(false)' in src,
    'manual camera permits vehicles': 'if (manual ? !IsPlayerUnit(actor)' in src and '(actor.IsInfantry() || actor.IsVehicle() || actor.HasTag(TagType.JETPACK))' in src,
    'jetpack-tagged infantry is a supported follow target': 'actor.HasTag(TagType.JETPACK)' in src and 'using Il2CppMenace.Tags;' in src,
    'separate vehicle positioning': 'VehicleDistance' in src and 'VehicleHeight' in src and '_trackedActor.IsVehicle()' in src,
    'visible enemy movement eligible': 'FollowVisibleEnemyMovement' in src and '!actor.IsHiddenToPlayer()' in src and 'actor.Is3DModelVisibleToPlayer()' in src,
    'current vision / not only discovered': 'actor.IsDiscoveredByPlayer()' in src and '!actor.IsHiddenToPlayer()' in src,
    'per-type movement and firing probabilities': all(key in src for key in ('_cachedVehicleMovementChance', '_cachedVehicleFiringChance', '_cachedWalkerMovementChance', '_cachedWalkerFiringChance', 'RollChance(actor, kind)')),
    'enemy movement chances are independently configurable by category': all(key in src for key in ('EnemyInfantryMovementChance', 'EnemyVehicleMovementChance', 'EnemyWalkerMovementChance', '_cachedEnemyWalkerMovementChance')),
    'minimum move filter uses native event endpoints': 'MovementPostfix(Actor _actor, Tile _from, Tile _to)' in src and 'from.GetDistanceTo(to) < _cachedMinimumMovementTiles' in src,
    'jetpack jumps observe the concrete native effect before movement': 'PatchEvent("OnApply", 6, nameof(JetpackApplyPrefix),' in src and 'typeof(JetPackHandler), true)' in src and 'JetpackApplyPrefix(Actor __0, Tile __1, Tile __2)' in src,
    'jetpack effect is not rejected by generic movement classification': 'if (!actor.HasTag(TagType.JETPACK) || !skill.IsMovementSkill())' not in src and 'PatchEvent("InvokeOnSkillUse"' not in src,
    'invalid jetpack destinations are ignored just like the native effect': '!targetTile.IsValidMovementDestination()' in src,
    'jetpack skill destinations honor minimum movement distance': 'from.GetDistanceTo(targetTile)' in src and 'distance < _cachedMinimumMovementTiles' in src,
    'poll fallback cannot bypass minimum move filter': 'moving && !_seenMovement && _cachedMinimumMovementTiles == 0' in src,
    'attacked friendly never triggers auto camera': all(x not in src for x in ('InvokeOnDamageReceived','InvokeOnAttackMissed','InvokeOnSuppressionApplied','EventKind.UnderFire','UnderFireChance')),
    'fire follows friendly shooter': 'if (!IsPlayerUnit(attacker)' in src,
    'enclosed building and bunker shooters skip automatic firing': 'ShouldSkipFiringCameraFromEnclosedBuilding(attacker)' in src and 'GetContainerEntity()' in src,
    'only Building container type suppresses firing cameras': container_filter is not None and '"ContainerType"' in container_filter.group(0) and '"Building"' in container_filter.group(0) and '"Tower"' not in container_filter.group(0),
    'no repeated per-frame debug': 'DebugLog("Auto camera "' in src and 'if (_debug != null && _debug.Value)' in src,
    'same deployment workflow': 'dotnet build' in builder and 'MenaceThirdPersonCamera.dll' in builder,
}

# Lightweight structural check after ignoring strings and comments.
clean = re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"', '', src, flags=re.S)
for left,right in [('(',')'),('[',']'),('{','}')]:
    count = 0
    for c in clean:
        if c == left:
            count += 1
        if c == right:
            count -= 1
        assert count >= 0, 'Malformed C# delimiters'
    checks[f'balanced {left}{right}'] = count == 0

for name, success in checks.items():
    print(('PASS' if success else 'FAIL') + '  ' + name)
assert all(checks.values()), 'One or more static checks failed'
print(f'{len(checks)} static regression checks passed')
