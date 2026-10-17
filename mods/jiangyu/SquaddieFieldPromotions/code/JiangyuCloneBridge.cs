using System;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Tools;

namespace SquaddieFieldPromotions;

/// <summary>
/// Experimental compatibility bridge into Jiangyu's proven live clone engine.
/// It does NOT duplicate IL2CPP pointer writes or native-array allocation.
/// All entrypoints are private in Jiangyu 1.x, so their exact signatures are
/// checked before we mutate any MENACE roster. If Jiangyu changes them, fail
/// closed on incompatibility. Reserved for the later, save-safe recruitment gate;
/// the 0.8 preview never invokes this bridge or registers runtime leaders.
/// </summary>
internal sealed class JiangyuCloneBridge
{
    private readonly MethodInfo _clone;
    private readonly MethodInfo _copyArrays;
    private readonly MethodInfo _copyOwned;
    private readonly MethodInfo _findMap;
    private readonly MethodInfo _register;
    private readonly MethodInfo _mirror;
    private readonly object _loaderLog;

    public JiangyuCloneBridge()
    {
        const string prefix = "Jiangyu.Loader.";
        Type? applier = Find(prefix + "Templates.TemplateCloneApplier");
        Type? logType = Find(prefix + "Logging.LoaderLog");
        if (applier == null || logType == null)
            throw new NotSupportedException("Jiangyu.Loader internal clone engine was not found in the loaded runtime.");

        _clone = GetMethod(applier, "TryCloneTemplate", 4);
        _copyArrays = GetMethod(applier, "DeepCopyCollectionContainers", 4);
        _copyOwned = GetMethod(applier, "DeepCopyOwnedReferences", 4);
        _findMap = GetMethod(applier, "TryGetTemplateMap", 2);
        _register = GetMethod(applier, "RegisterCloneIntoSlot", 6);
        _mirror = GetMethod(applier, "MirrorCloneToAncestors", 4);

        // Jiangyu.Loader.Logging.LoaderLog(MelonLogger.Instance) accepts null.
        // Its Warning / Debug methods use null-safe logging, avoiding unsolicited
        // per-clone output. The lab reports concise results via Jiangyu.Sdk.Log.
        _loaderLog = Activator.CreateInstance(logType, new object?[] { null })
            ?? throw new NotSupportedException("Could not create the Jiangyu clone-log adapter.");
    }

    private static Type? Find(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type? type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            catch { }
        }
        return null;
    }

    private static MethodInfo GetMethod(Type type, string name, int count)
    {
        MethodInfo[] matches = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == name && m.GetParameters().Length == count)
            .ToArray();
        if (matches.Length != 1)
            throw new NotSupportedException($"Jiangyu clone method '{name}' is missing or has changed signature.");
        return matches[0];
    }

    public T Clone<T>(T source, string cloneId) where T : DataTemplate
    {
        object?[] arguments = { source, cloneId, _loaderLog, null };
        bool ok = InvokeBool(_clone, arguments);
        if (!ok || arguments[3] is not DataTemplate raw)
            throw new InvalidOperationException("Jiangyu failed to clone " + typeof(T).Name + " (" + cloneId + ").");

        // The cloned object may have the base DataTemplate CLR wrapper here.
        // Casting through Il2CppInterop respects the actual native runtime type.
        T? typed = raw.TryCast<T>();
        if (typed == null)
            throw new InvalidOperationException("Jiangyu returned an incompatible clone of " + typeof(T).Name + ".");
        InvokeVoid(_copyArrays, new object?[] { typed, typeof(T), cloneId, _loaderLog });
        InvokeVoid(_copyOwned, new object?[] { typed, typeof(T), cloneId, _loaderLog });
        if (!string.Equals(typed.GetID(), cloneId, StringComparison.Ordinal))
            throw new InvalidOperationException("Jiangyu clone did not set the intended m_ID.");
        return typed;
    }

    public void Register<T>(T clone, string cloneId) where T : DataTemplate
    {
        object?[] find = { typeof(T), null };
        if (!InvokeBool(_findMap, find) || find[1] == null)
            throw new InvalidOperationException("Jiangyu could not locate the loaded " + typeof(T).Name + " lookup map.");

        // Jiangyu's own helper mirrors the template into *both* the map and
        // correctly-typed IL2CPP array. It then mirrors registered ancestors.
        InvokeVoid(_register, new[] { typeof(T), find[1], typeof(T), cloneId, clone, _loaderLog });
        InvokeVoid(_mirror, new object?[] { typeof(T), cloneId, clone, _loaderLog });
    }

    private static bool InvokeBool(MethodInfo method, object?[] args)
    {
        try { return method.Invoke(null, args) is true; }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException("Jiangyu " + method.Name + ": " +
                (ex.InnerException?.Message ?? ex.Message), ex);
        }
    }

    private static void InvokeVoid(MethodInfo method, object?[] args)
    {
        try { method.Invoke(null, args); }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException("Jiangyu " + method.Name + ": " +
                (ex.InnerException?.Message ?? ex.Message), ex);
        }
    }
}
