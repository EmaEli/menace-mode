using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tactical.Skills;
using MenaceInfantryAssault;

int checks = 0;
void Check(bool value, string label)
{
    checks++;
    if (!value) throw new InvalidOperationException(label);
}

var assaultTemplate = new Il2CppObjectBase((IntPtr)100);
var otherTemplate = new Il2CppObjectBase((IntPtr)101);
Plugin.SetTemplate(assaultTemplate);
Skill.NativeTemplates[(IntPtr)1] = assaultTemplate;
Skill.NativeTemplates[(IntPtr)2] = otherTemplate;
var assault = new Skill((IntPtr)1);
Check(Plugin.Classify(assault), "live Assault is recognized");
Check(Plugin.Classify(new Il2CppObjectBase((IntPtr)1)), "BaseSkill wrapper casts to its real native Skill");
Check(Plugin.Classify(new Skill((IntPtr)1)), "new wrapper retains native identity");
Check(!Plugin.Classify(new Skill((IntPtr)2)), "ordinary weapon passes through");

// Model the real folded getter: the Harmony wrapper says Skill, but native
// memory at this address is a SkillContainer. GetTemplate/GetID would be unsafe.
var foldedReceiver = new Skill((IntPtr)3);
int reads = Skill.TemplateReads;
Check(!Plugin.Classify(foldedReceiver), "miswrapped SkillContainer passes through");
Check(Skill.TemplateReads == reads, "non-Skill receiver is never dereferenced as Skill");
Check(!Plugin.Classify(new Skill(IntPtr.Zero)), "zero native pointer is rejected");
Check(!Plugin.Classify(new object()), "managed non-native object is rejected");
Check(!Plugin.Classify(null), "null receiver is rejected");

Plugin.WorkerThread = true;
reads = Skill.TemplateReads;
Check(!Plugin.Classify(assault), "worker hook keeps vanilla behavior");
Check(Skill.TemplateReads == reads, "worker hook performs no native reads");
Plugin.WorkerThread = false;
Plugin.SetTemplate(null);
reads = Skill.TemplateReads;
Check(!Plugin.Classify(assault), "unresolved Assault template fails closed");
Check(Skill.TemplateReads == reads, "unresolved template performs no native reads");
Plugin.SetTemplate(new Il2CppObjectBase(IntPtr.Zero));
Check(!Plugin.Classify(assault), "zero template pointer fails closed");
Plugin.SetTemplate(new Il2CppObjectBase((IntPtr)100));
Check(Plugin.Classify(assault), "template wrapper identity is native, not managed");

Skill.NativeTemplates[(IntPtr)1] = otherTemplate;
Check(!Plugin.Classify(assault), "reused Skill address does not retain cached Assault status");
Skill.NativeTemplates.Remove((IntPtr)1);
Check(!Plugin.Classify(assault), "address reused by a non-Skill is rejected");
Skill.NativeTemplates[(IntPtr)4] = null;
Check(!Plugin.Classify(new Skill((IntPtr)4)), "missing live template fails closed");
Skill.ThrowOnTemplate = true;
Check(!Plugin.Classify(new Skill((IntPtr)2)), "managed interop failure fails closed");
Skill.ThrowOnTemplate = false;
Check(Skill.IdReads == 0, "classification never converts a native ID string");
Console.WriteLine($"Assault identity checks passed: {checks}");

namespace MenaceInfantryAssault
{
    public sealed partial class Plugin
    {
        private static object? _assaultTemplate;
        public static bool WorkerThread;
        private static bool OffMainThread => WorkerThread;
        public static bool Classify(object? skill) => IsAssaultSkill(skill);
        public static void SetTemplate(object? template) => _assaultTemplate = template;
    }
}

namespace Il2CppInterop.Runtime.InteropTypes
{
    public class Il2CppObjectBase
    {
        public IntPtr Pointer { get; }
        public Il2CppObjectBase(IntPtr pointer) => Pointer = pointer;
        public T? TryCast<T>() where T : Il2CppObjectBase
        {
            if (typeof(T) == typeof(Skill) && Skill.NativeTemplates.ContainsKey(Pointer))
                return (T)(object)new Skill(Pointer);
            return null;
        }
    }
}

namespace Il2CppMenace.Tactical.Skills
{
    public class Skill : Il2CppObjectBase
    {
        public static readonly Dictionary<IntPtr, Il2CppObjectBase?> NativeTemplates = new();
        public static int TemplateReads, IdReads;
        public static bool ThrowOnTemplate;
        public Skill(IntPtr pointer) : base(pointer) { }
        public Il2CppObjectBase? GetTemplate()
        {
            TemplateReads++;
            if (ThrowOnTemplate) throw new InvalidOperationException("Unavailable native template");
            return NativeTemplates[Pointer];
        }
        public string GetID()
        {
            IdReads++;
            throw new InvalidOperationException("Native string conversion must not run");
        }
    }
}