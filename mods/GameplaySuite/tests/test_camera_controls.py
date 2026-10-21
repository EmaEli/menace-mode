"""Execute production camera selection, distance filtering and upgrade migration offline."""
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
SOURCE = (ROOT / "mods/ThirdPersonCamera/src/ThirdPersonCameraMod.cs").read_text()
COMPANION = (ROOT / "mods/jiangyu/CinematicActionCamera/code/CameraSettingsSystem.cs").read_text()
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


def between(source, start, end):
    return start + source.split(start, 1)[1].split(end, 1)[0]


HARNESS = r'''
using System; using System.Collections; using System.Collections.Generic; using System.Reflection;
class MelonPreferences_Entry<T> { public T Value; public MelonPreferences_Entry(T v){Value=v;} }
class Category { public MelonPreferences_Entry<T> CreateEntry<T>(string key,T v)=>new(v); }
static class Time { public static float realtimeSinceStartup; }
static class Mathf { public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b); }
enum TagType { JETPACK }
class Actor { public int ID=1; public bool Vehicle,Infantry=true,Player=true,Alive=true,Discovered=true,Hidden,ModelVisible=true,Jetpack; public string Id="infantry"; public Tile CurrentTile;
    public bool IsVehicle()=>Vehicle; public bool IsInfantry()=>Infantry; public bool IsAlive()=>Alive;
    public bool IsPlayer()=>Player; public bool IsPlayerOrPlayerAI()=>Player;
    public bool IsDiscoveredByPlayer()=>Discovered; public bool IsHiddenToPlayer()=>Hidden;
    public bool Is3DModelVisibleToPlayer()=>ModelVisible; public bool HasTag(TagType tag)=>tag==TagType.JETPACK&&Jetpack;
    public Actor GetTemplate()=>this; public string GetID()=>Id; public Tile GetTile()=>CurrentTile; }
class Tile { public int X,Y; public bool Valid=true; public Tile(int x,int y=0){X=x;Y=y;}
    public bool IsValidMovementDestination()=>Valid;
    public int GetDistanceTo(Tile other)=>Math.Max(Math.Abs(X-other.X),Math.Abs(Y-other.Y)); }
static class ModSettings {
    public static Dictionary<string,object> Values=new();
    public static object Get(string group,string key)=>Values[key];
    public static void ImportMissing(string group,IDictionary<string,object> values){
        foreach(var pair in values)Values.TryAdd(pair.Key,pair.Value);
    }
}
partial class Companion {
    const string Group="Cinematic Action Camera";
    public void Migrate(){var e=ImportPreferences();if(e.MoveNext())throw new Exception("Unexpected migration wait");}
}
namespace MenaceThirdPersonCamera {
partial class ThirdPersonCameraMod {
    enum EventKind { Movement,Firing }
    static ThirdPersonCameraMod s_instance;
    readonly Random _cameraRandom=new(123);
    int shots; static int checks;
    void ConsiderAutoCamera(Actor a,EventKind k,float duration){shots++;}
    object ReadMenuValue(string key)=>ModSettings.Values.TryGetValue(key,out var v)?v:null;
    bool MenuBoolean(string key,bool fallback)=>ReadMenuValue(key) is object v?Convert.ToBoolean(v):fallback;
    float MenuNumber(string key,float fallback)=>ReadMenuValue(key) is object v?Convert.ToSingle(v):fallback;
    static void Check(bool ok){checks++;if(!ok)throw new Exception("Camera control check "+checks+" failed");}
    static bool Eq(string key,float n)=>Convert.ToSingle(ModSettings.Values[key])==n;
    public static void Main(){
        var c=new ThirdPersonCameraMod(); s_instance=c; c.Init();
        var infantry=new Actor{Id="infantry.walker"};
        var vehicle=new Actor{Vehicle=true,Id="tank"};
        var walker=new Actor{Vehicle=true,Id="vehicle.WALKER"};
        Check(!IsWalkerVehicle(infantry)&&!IsWalkerVehicle(vehicle)&&IsWalkerVehicle(walker));
        Check(!IsWalkerVehicle(new Actor{Vehicle=true,Id=null}));
        var jetpack=new Actor{Player=false,Infantry=false,Jetpack=true};
        var enemyTypes=new[]{new Actor{Player=false},new Actor{Player=false,Vehicle=true,Id="tank"},new Actor{Player=false,Vehicle=true,Id="vehicle.walker"}};
        Check(IsSupportedUnit(infantry)&&IsSupportedUnit(vehicle)&&IsSupportedUnit(jetpack));
        Check(!IsSupportedUnit(new Actor{Infantry=false}));
        Check(IsCurrentlyVisibleEnemy(jetpack)&&c.IsEligibleAutoActor(jetpack,EventKind.Movement));
        Check(IsCurrentlyVisibleEnemy(enemyTypes[1])&&c.IsEligibleAutoActor(enemyTypes[1],EventKind.Movement));
        Check(!c.IsEligibleAutoActor(jetpack,EventKind.Firing));
        for(int enabled=0;enabled<3;enabled++){
            c._cachedEnemyInfantryMovementChance=c._cachedEnemyVehicleMovementChance=c._cachedEnemyWalkerMovementChance=0;
            if(enabled==0)c._cachedEnemyInfantryMovementChance=100;
            if(enabled==1)c._cachedEnemyVehicleMovementChance=100;
            if(enabled==2)c._cachedEnemyWalkerMovementChance=100;
            for(int t=0;t<3;t++)Check(c.RollChance(enemyTypes[t],EventKind.Movement)==(t==enabled));
        }
        var types=new[]{infantry,vehicle,walker};
        for(int enabled=0;enabled<3;enabled++)for(int firing=0;firing<2;firing++){
            c._cachedMovementChance=c._cachedFiringChance=0;
            c._cachedVehicleMovementChance=c._cachedVehicleFiringChance=0;
            c._cachedWalkerMovementChance=c._cachedWalkerFiringChance=0;
            if(enabled==0){if(firing==0)c._cachedMovementChance=100;else c._cachedFiringChance=100;}
            if(enabled==1){if(firing==0)c._cachedVehicleMovementChance=100;else c._cachedVehicleFiringChance=100;}
            if(enabled==2){if(firing==0)c._cachedWalkerMovementChance=100;else c._cachedWalkerFiringChance=100;}
            for(int t=0;t<3;t++)for(int k=0;k<2;k++)Check(c.RollChance(types[t],(EventKind)k)==(t==enabled&&k==firing));
        }
        c._cachedMinimumMovementTiles=3;
        foreach(var actor in types)foreach(int length in new[]{0,1,2,3,4}){
            int before=c.shots;c.HandleMovement(actor,new Tile(0),new Tile(length,length));
            Check(c.shots-before==(length>=3?1:0));
        }
        int count=c.shots;c.HandleMovement(infantry,null,new Tile(10));Check(c.shots==count);
        c._cachedMinimumMovementTiles=0;c.HandleMovement(infantry,null,null);Check(c.shots==count+1);
        var alliedJetpack=new Actor{Infantry=false,Jetpack=true,CurrentTile=new Tile(0)};
        var jumpOrigin=new Tile(0);
        int beforeSkill=c.shots;
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,new Tile(5){Valid=false});Check(c.shots==beforeSkill);
        c.HandleJetpackJump(new Actor{Alive=false},jumpOrigin,new Tile(5));Check(c.shots==beforeSkill);
        c._cachedMinimumMovementTiles=3;
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,new Tile(2));Check(c.shots==beforeSkill);
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,new Tile(3));Check(c.shots==beforeSkill+1);
        c.HandleJetpackJump(alliedJetpack,null,new Tile(5));Check(c.shots==beforeSkill+1);
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,null);Check(c.shots==beforeSkill+1);
        c._cachedMinimumMovementTiles=0;
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,new Tile(1));Check(c.shots==beforeSkill+2);
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,new Tile(0));Check(c.shots==beforeSkill+2);
        alliedJetpack.CurrentTile=new Tile(5);
        c.HandleJetpackJump(alliedJetpack,jumpOrigin,new Tile(5));Check(c.shots==beforeSkill+3);
        c.HandleJetpackJump(infantry,jumpOrigin,new Tile(5));Check(c.shots==beforeSkill+4);
        c._movementChance.Value=17;c._firingChance.Value=23;c._vehicleDistance.Value=9;
        c._vehicleHeight.Value=4;c._distance.Value=5;c._height.Value=2;
        c.RefreshSettings();
        Check(c._cachedVehicleMovementChance==17&&c._cachedWalkerFiringChance==23);
        Check(c._cachedWalkerDistance==9&&c._cachedWalkerHeight==4&&c._cachedDistance==5&&c._cachedHeight==2);
        Check(!GetLegacySettingsValues().ContainsKey("WalkerMovementChance"));
        c._enemyWalkerMovementChance.Value=0;
        Check(Convert.ToInt32(GetLegacySettingsValues()["EnemyWalkerMovementChance"])==0);
        c._enemyWalkerMovementChance.Value=-1;
        ModSettings.Values=new(){{"MovementChance",31f},{"FiringChance",42f},{"VehicleDistance",11f},{"VehicleHeight",6f},{"WalkerFiringChance",0f}};
        c._vehicleMovementChance.Value=0;
        new Companion().Migrate();
        Check(Eq("VehicleMovementChance",0)&&Eq("WalkerMovementChance",31));
        Check(Eq("VehicleFiringChance",42)&&Eq("WalkerFiringChance",0));
        Check(Eq("WalkerDistance",11)&&Eq("WalkerHeight",6));
        Check(Eq("EnemyInfantryMovementChance",31)&&Eq("EnemyVehicleMovementChance",0));
        Check(Eq("EnemyWalkerMovementChance",31));
        Check(Eq("Distance",5)&&Eq("Height",2));
        ModSettings.Values["EnemyWalkerMovementChance"]=0f;new Companion().Migrate();Check(Eq("EnemyWalkerMovementChance",0));
        ModSettings.Values["WalkerDistance"]=15f;new Companion().Migrate();Check(Eq("WalkerDistance",15));
        Time.realtimeSinceStartup=1;c.RefreshSettings();
        Check(c._cachedVehicleMovementChance==0&&c._cachedWalkerMovementChance==31&&c._cachedWalkerFiringChance==0);
        Check(c._cachedEnemyInfantryMovementChance==31&&c._cachedEnemyVehicleMovementChance==0&&c._cachedEnemyWalkerMovementChance==0);
        Check(c._cachedWalkerDistance==15&&c._cachedDistance==5);
        ModSettings.Values["MinimumMovementTiles"]=2.9f;Time.realtimeSinceStartup=2;c.RefreshSettings();Check(c._cachedMinimumMovementTiles==3);
        Console.WriteLine($"Camera control C# checks: {checks} passed");
    }
}}
'''


class CameraControlTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_production_controls_and_migration(self):
        methods = [
            between(SOURCE, "        public static Dictionary<string, object> GetLegacySettingsValues", "        private HarmonyLib.Harmony _harmony"),
            between(SOURCE, "        private MelonPreferences_Entry<string> _toggleKey", "        // The optional companion"),
            between(SOURCE, "        private float _nextSettingsRefresh", "        private TacticalCamera"),
            between(SOURCE, "        private void HandleMovement(", "        private void HandleMovementFinished"),
            between(SOURCE, "        private static bool IsWalkerVehicle", "        private static bool TacticalUiIsFrontmost"),
            between(SOURCE, "        private void RefreshSettings", "        // The 0.3 reflection-based"),
        ]
        entries = re.findall(r'            _\w+ = category.CreateEntry\([^;]+;', SOURCE)
        methods.append("void Init(){var category=new Category();" + "\n".join(entries) + "}")
        companion = between(COMPANION, "    private IEnumerator ImportPreferences", "    public override void OnUnload")
        with tempfile.TemporaryDirectory(prefix="menace-camera-controls-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (root / "Program.cs").write_text(HARNESS + "\nnamespace MenaceThirdPersonCamera { partial class ThirdPersonCameraMod {\nvoid DebugLog(string message){}\n" + "\n".join(methods) + "\n}}\npartial class Companion {\n" + companion + "\n}")
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Camera control C# checks: 95 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()