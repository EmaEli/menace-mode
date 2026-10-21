"""Execute production camera geometry with managed stand-ins (not Unity rendering)."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
SOURCE = (ROOT / "mods/ThirdPersonCamera/src/ThirdPersonCameraMod.cs").read_text()
DOTNET = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")


def part(start, end):
    return start + SOURCE.split(start, 1)[1].split(end, 1)[0]


HARNESS = r'''
using System;
struct Vector3 {
    public float x,y,z; public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
    public static Vector3 up => new(0,1,0); public static Vector3 forward => new(0,0,1);
    public float sqrMagnitude => x*x+y*y+z*z;
    public Vector3 normalized => this*(1/MathF.Sqrt(sqrMagnitude));
    public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
    public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
    public static Vector3 operator *(Vector3 a,float n)=>new(a.x*n,a.y*n,a.z*n);
    public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Math.Clamp(t,0,1);
    // Bypass Unity's easing, so tests inspect the requested pose, not simulated rendering.
    public static Vector3 Slerp(Vector3 a,Vector3 b,float t)=>b;
}
struct Quaternion {
    public static Vector3 LastLook; float yaw;
    public static Quaternion AngleAxis(float angle,Vector3 axis)=>new(){yaw=angle*MathF.PI/180};
    public static Vector3 operator *(Quaternion q,Vector3 v)=>new(v.x*MathF.Cos(q.yaw)+v.z*MathF.Sin(q.yaw),v.y,-v.x*MathF.Sin(q.yaw)+v.z*MathF.Cos(q.yaw));
    public static Quaternion LookRotation(Vector3 direction,Vector3 up) { LastLook=direction; return new(); }
    public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>b;
}
static class Mathf {
    public const float Deg2Rad=MathF.PI/180;
    public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);
    public static float Clamp01(float v)=>Math.Clamp(v,0,1);
    public static float Abs(float v)=>MathF.Abs(v); public static float Max(float a,float b)=>Math.Max(a,b);
    public static float Sin(float v)=>MathF.Sin(v); public static float Cos(float v)=>MathF.Cos(v);
    public static float Exp(float v)=>MathF.Exp(v);
    public static float DeltaAngle(float a,float b)=>((b-a+540)%360)-180;
}
static class Time { public static float unscaledDeltaTime=0.1f; }
class Transform { public Vector3 position; public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;} }
class Camera { public Transform transform=new(); public float fieldOfView,nearClipPlane,farClipPlane; }
class Element { public Transform transform=new(); }
class Actor {
    public Vector3 Position,Forward; public bool Moving,Vehicle; public string Id="tank";
    public Vector3 GetPos()=>Position; public Vector3 GetForward()=>Forward;
    public bool IsMoving()=>Moving; public bool IsVehicle()=>Vehicle;
    public Actor GetTemplate()=>this; public string GetID()=>Id;
}
class Setting { public float Value; public Setting(float value){Value=value;} }
partial class CameraChecks {
    enum EventKind {Movement,Firing}
    const float TransitionSpeed=8f;
    Actor _trackedActor=new(); Element _trackedElement=new(); Camera _unityCamera=new();
    bool _manual; EventKind _autoKind; Vector3? _autoAttackTarget;
    Vector3 _heading,_lastElementPosition,_smoothedPosition; Quaternion _smoothedRotation;
    float _orbitYaw,_orbitPitch,_savedFar=100,_cachedVehicleDistance=8,_cachedVehicleHeight=4;
    float _cachedDistance=3.2f,_cachedHeight=1.75f,_cachedWalkerDistance=13,_cachedWalkerHeight=7;
    Setting _shoulder=new(0.55f),_fieldOfView=new(66);
    static int checks;
    static void Check(bool ok){checks++;if(!ok)throw new Exception("Camera geometry check "+checks+" failed");}
    static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<0.00001f;
    public static void Main(){
        foreach(int type in new[]{0,1,2})
        foreach(int dx in new[]{-1,0,1}) foreach(int dz in new[]{-1,0,1}) {
            if(dx==0&&dz==0)continue;
            bool vehicle=type!=0,walker=type==2;
            var c=new CameraChecks(); c._autoKind=EventKind.Firing;
            c._trackedActor.Position=new(10,2,10); c._trackedActor.Vehicle=vehicle;
            c._trackedActor.Id=walker?"vehicle.WALKER":"tank";
            c._trackedActor.Forward=new(-dx,0,-dz); c._trackedActor.Moving=true;
            c._trackedElement.transform.position=new(30,2,30);
            Vector3 target=new(10+dx*20,5,10+dz*20);
            Vector3 bearing=new Vector3(dx,0,dz).normalized;
            c.SetAutoAttackTarget(EventKind.Firing,target);
            Check(Near(c._heading,bearing));
            c.UpdateThirdPersonCamera();
            float distance=walker?13:vehicle?8:3.2f, height=walker?7:vehicle?4:1.75f;
            Vector3 desired=c._trackedActor.Position+Vector3.up*height-bearing*distance;
            if(!vehicle)desired+=Vector3.Cross(Vector3.up,bearing)*0.55f;
            Check(Near(c._smoothedPosition,desired*Blend(TransitionSpeed,0.1f)));
            float lookHeight=walker?3.85f:vehicle?2.2f:1.38f;
            Check(Near(Quaternion.LastLook,target+Vector3.up*lookHeight-desired));
            Check(Near(c._lastElementPosition,c._trackedElement.transform.position));
            c.SetAutoAttackTarget(EventKind.Firing,new Vector3(10-dx*20,2,10-dz*20));
            Check(Near(c._heading,bearing*(-1)));
            c.SetAutoAttackTarget(EventKind.Movement,target);
            Check(!c._autoAttackTarget.HasValue);
        }
        var manual=new CameraChecks(); manual._manual=true; manual._autoKind=EventKind.Firing;
        manual._trackedActor.Forward=Vector3.forward; manual._autoAttackTarget=new(100,0,-100);
        manual._trackedElement.transform.position=new(2,0,2); manual.UpdateThirdPersonCamera();
        Check(Near(manual._heading,Vector3.forward));
        var missing=new CameraChecks(); missing.SetAutoAttackTarget(EventKind.Firing,null);
        Check(!missing._autoAttackTarget.HasValue);
        Check(Near(FlatDirection(new Vector3(0,10,0),Vector3.forward),Vector3.forward));
        Console.WriteLine($"Camera geometry C# checks: {checks} passed");
    }
}
'''


class CameraGeometryTests(unittest.TestCase):
    @unittest.skipUnless(Path(DOTNET).is_file(), ".NET SDK not available")
    def test_actual_camera_geometry(self):
        methods = [
            part("        private void SetAutoAttackTarget", "        private bool RollChance"),
            part("        private void UpdateThirdPersonCamera", "        private void HideTacticalUi"),
        ]
        with tempfile.TemporaryDirectory(prefix="menace-camera-geometry-") as folder:
            root = Path(folder)
            (root / "Checks.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (root / "Program.cs").write_text(HARNESS + "\npartial class CameraChecks {\n" + "\n".join(methods) + "\n}")
            result = subprocess.run([DOTNET, "run", "--project", str(root / "Checks.csproj"), "-c", "Release", "--nologo", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("Camera geometry C# checks: 147 passed", result.stdout)


if __name__ == "__main__":
    unittest.main()