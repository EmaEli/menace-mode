using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace SquaddieFieldPromotions;

internal static class PrivateMember
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static bool Set(object obj, string name, object value)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo field = t.GetField(name, Flags);
                if (field != null && !field.IsInitOnly)
                {
                    field.SetValue(obj, value);
                    return true;
                }
                PropertyInfo property = t.GetProperty(name, Flags);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(obj, value);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    // Proven 0.7.15 compatibility pattern from the Squaddie Veterancy
    // identity cache. IReadOnlyList<T> proxies do not surface inherited Count
    // in .NET reflection: rewrap the same IL2CPP object pointer as an
    // IReadOnlyCollection<T> proxy before reading Count.
    internal static IEnumerable<object> Enumerate(object collection)
    {
        if (collection == null) yield break;
        if (TryGetCollectionCount(collection, out int count))
        {
            // Do not silently report an empty collection if the native
            // indexer cannot actually be read. That previously caused F9 to
            // think a campaign had no available squaddies.
            for (int i = 0; i < count; i++)
            {
                if (!TryGetCollectionItem(collection, i, out object value))
                    throw new InvalidOperationException(
                        $"Native collection {collection.GetType().Name}: cannot read item {i} of {count}.");
                if (value != null) yield return value;
            }
            yield break;
        }

        // Never swallow a broken native IReadOnlyList<T> bridge by falling
        // back to an enumerator that might silently yield zero entries.
        if (IsNativeReadOnlyList(collection.GetType()))
            throw new InvalidOperationException(
                "Cannot read IL2CPP IReadOnlyList<T> Count: " + collection.GetType().FullName);

        // Last-resort path for genuinely managed collections.
        if (collection is IEnumerable managed)
        {
            foreach (object value in managed)
                if (value != null) yield return value;
            yield break;
        }
        throw new InvalidOperationException(
            "Unsupported collection type for native list reader: " + collection.GetType().FullName);
    }

    private static bool IsNativeReadOnlyList(Type type)
        => type.IsGenericType &&
            (type.Name.StartsWith("IReadOnlyList`", StringComparison.Ordinal) ||
             (type.FullName ?? "").Contains("Il2CppSystem.Collections.Generic.IReadOnlyList`1", StringComparison.Ordinal));

    private static IEnumerable<Type> ReflectionSurfaces(Type type)
    {
        yield return type;
        foreach (Type iface in type.GetInterfaces())
            yield return iface;
    }

    private static bool TryGetCollectionCount(object collection, out int count)
    {
        count = 0;
        Type listType = collection.GetType();
        foreach (Type surface in ReflectionSurfaces(listType))
            if (TryReadCount(collection, surface, out count)) return true;

        // The class hierarchy in Il2CppInterop does not expose the
        // inherited IReadOnlyCollection<T>.Count on IReadOnlyList<T>.
        // Rewrap the SAME native pointer; no native allocations or writes.
        try
        {
            Type[] args = listType.IsGenericType ? listType.GetGenericArguments() : Type.EmptyTypes;
            if (args.Length == 1 && IsNativeReadOnlyList(listType))
            {
                Type openCollection = listType.Assembly.GetType(
                    "Il2CppSystem.Collections.Generic.IReadOnlyCollection`1", false);
                if (openCollection != null)
                {
                    Type sibling = openCollection.MakeGenericType(args);
                    object proxy = CreateTypedProxy(collection, sibling);
                    if (TryReadCount(proxy, sibling, out count)) return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool TryReadCount(object collection, Type surface, out int count)
    {
        count = 0;
        try
        {
            PropertyInfo prop = surface.GetProperty("Count", Flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                count = Convert.ToInt32(prop.GetValue(collection) ?? 0);
                return true;
            }
        }
        catch { }
        try
        {
            MethodInfo method = surface.GetMethod("get_Count", Flags,
                null, Type.EmptyTypes, null);
            if (method != null)
            {
                count = Convert.ToInt32(method.Invoke(collection, null) ?? 0);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TryGetCollectionItem(object collection, int index, out object item)
    {
        item = null;
        Type listType = collection.GetType();
        foreach (Type surface in ReflectionSurfaces(listType))
        {
            try
            {
                foreach (PropertyInfo prop in surface.GetProperties(Flags))
                {
                    ParameterInfo[] parameters = prop.GetIndexParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(int)) continue;
                    item = CreateTypedElement(prop.GetValue(collection, new object[] { index }), listType);
                    return true;
                }
            }
            catch { }
            try
            {
                MethodInfo getter = surface.GetMethod("get_Item", Flags,
                    null, new[] { typeof(int) }, null);
                if (getter == null) continue;
                item = CreateTypedElement(getter.Invoke(collection, new object[] { index }), listType);
                return true;
            }
            catch { }
        }
        return false;
    }

    private static object CreateTypedElement(object raw, Type collectionType)
    {
        if (raw == null || !collectionType.IsGenericType) return raw;
        Type[] args = collectionType.GetGenericArguments();
        if (args.Length != 1 || args[0].IsInstanceOfType(raw)) return raw;
        try { return CreateTypedProxy(raw, args[0]); }
        catch { return raw; }
    }

    private static object CreateTypedProxy(object value, Type targetType)
    {
        if (targetType.IsInstanceOfType(value)) return value;
        if (value is Il2CppObjectBase native && native.Pointer != IntPtr.Zero)
        {
            object typed = Activator.CreateInstance(targetType, new object[] { native.Pointer });
            if (typed != null) return typed;
        }
        return value;
    }
}
