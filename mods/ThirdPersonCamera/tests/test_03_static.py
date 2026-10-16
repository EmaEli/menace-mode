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

checks = {
    'release identity 1.0.0': '"Cinematic Action Camera", "1.0.0"' in src and '<Version>1.0.0</Version>' in project,
    'MEN-006 compatibility contract': 'public static bool IsCinematicViewActive' in src,
    'native tactical UI visibility used': 'UITactical.SetUIVisibility(false)' in src and 'UITactical.SetUIVisibility(true)' in src,
    'UI restored by exit even when camera fails': re.search(r'private void ExitThirdPerson\(.*?finally\s*\{.*?RestoreTacticalUi\(\);', src, re.S) is not None,
    'UI restored on scene exit': 'override void OnSceneWasUnloaded' in src and 'ExitThirdPerson(false)' in src,
    'manual camera permits vehicles': 'if (manual ? !IsPlayerUnit(actor)' in src and '(actor.IsInfantry() || actor.IsVehicle())' in src,
    'separate vehicle positioning': 'VehicleDistance' in src and 'VehicleHeight' in src and '_trackedActor.IsVehicle()' in src,
    'visible enemy movement eligible': 'FollowVisibleEnemyMovement' in src and '!actor.IsHiddenToPlayer()' in src and 'actor.Is3DModelVisibleToPlayer()' in src,
    'current vision / not only discovered': 'actor.IsDiscoveredByPlayer()' in src and '!actor.IsHiddenToPlayer()' in src,
    'enemy movement uses movement probability': 'float chance = kind == EventKind.Movement ? _cachedMovementChance : _cachedFiringChance;' in src,
    'attacked friendly never triggers auto camera': all(x not in src for x in ('InvokeOnDamageReceived','InvokeOnAttackMissed','InvokeOnSuppressionApplied','EventKind.UnderFire','UnderFireChance')),
    'fire follows friendly shooter': 'if (!IsPlayerUnit(attacker)' in src,
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
