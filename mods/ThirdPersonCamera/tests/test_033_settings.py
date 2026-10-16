"""Validate the 0.3.3 ModpackLoader companion without pretending it is a full .NET build."""
from pathlib import Path
import json
import re
root=Path(__file__).resolve().parents[1]
cam=(root/'src/ThirdPersonCameraMod.cs').read_text()
ui=(root/'src/ThirdPersonCameraSettings/Plugin.cs').read_text()
p=(root/'src/ThirdPersonCameraSettings/ThirdPersonCameraSettings.csproj').read_text()
manifest=json.loads((root/'modpack/ThirdPersonCamera/modpack.json').read_text())
script=(root/'tools/build-and-deploy.ps1').read_text()
keys=['AutomaticMode','MovementChance','FiringChance','FollowVisibleEnemyMovement','VehicleDistance','VehicleHeight','OrbitSensitivity']
assert all('"'+k+'"' in ui for k in keys)
assert all('"'+k+'"' in cam for k in keys)
assert ui.count('settings.AddSlider(')==5
assert ui.count('settings.AddToggle(')==2
assert 'ModSettings.Register(MenuName, settings =>' in ui
# Working Larger Maps uses 'using Menace.SDK;' for ModSettings, even though
# its plugin interface itself comes from Menace.ModpackLoader.
assert 'using Menace.SDK;' in ui
assert 'using Menace.ModpackLoader;' in ui
# The standalone camera reflection reader must look in the *same* namespace.
assert '"Menace.SDK.ModSettings"' in cam
assert 'api = assembly.GetType(typeName, false)' in cam
assert cam.index('"MenaceModSettings.ModSettings"') < cam.index('"Menace.SDK.ModSettings"')
assert 'private object ReadMenuValue(string key)' in cam
assert 'Menace.SDK.ModSettings' in script
assert r'using\s+Menace\.SDK' in script

# Interface contract verified against the working Larger Maps plugin and
# the compiler's CS0535 diagnostics, rather than inferred from old snapshots.
assert 'public sealed class Plugin : IModpackPlugin' in ui
assert 'public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)' in ui
assert 'public void OnSceneLoaded(int buildIndex, string sceneName)' in ui
assert not re.search(r'OnInitialize\s*\(\s*MelonLogger\.Instance\s+\w+\s*,\s*Harmony\s+\w+', ui)

assert not re.search(r'\bModSettings\.Register\s*\(', re.sub(r'//[^\n]*', '', cam))
assert 'TryAttachInGameSettings();' in cam
assert '_menuRegistered = true' in cam and '_menuGet = candidate' in cam
assert 'private object ReadMenuValue(string key)' in cam
assert manifest['name']=='Cinematic Action Camera' and manifest['version']=='1.0.0'
assert manifest['code']['prebuiltDlls']==['ThirdPersonCameraSettings.dll']
assert 'Menace.ModpackLoader' in manifest['code']['references']
assert 'Menace.ModpackLoader.dll' in p
assert 'src\\ThirdPersonCameraSettings\\ThirdPersonCameraSettings.csproj' in script
assert 'ThirdPersonCameraSettings.dll' in script
assert 'Mods\\Menace.ModpackLoader.dll' in script
assert 'modpack.json' in script
assert "OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)" in script
assert "OnSceneLoaded(int buildIndex, string sceneName)" in script
assert 'InstallEventHooks();' in cam
assert 'UITactical.SetUIVisibility(false)' in cam
assert 'UITactical.SetUIVisibility(true)' in cam
assert 'new HarmonyLib.Harmony(' in cam
for name, s in [('camera',cam),('settings',ui)]:
    src=re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"','',s,flags=re.S)
    for start, end in [('(',')'),('[',']'),('{','}')]:
        count=0
        for c in src:
            if c==start:count+=1
            if c==end:count-=1
            assert count>=0,(name,start,end)
        assert count==0,(name,start,end)
print('PASS: v0.3.3 settings bridge, both sliders, ModpackLoader manifest/build integration, fallback & static C# syntax')
