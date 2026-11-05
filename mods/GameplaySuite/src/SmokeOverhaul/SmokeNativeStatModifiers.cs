using System;
using System.Collections.Generic;
using System.Reflection;

namespace SmokeOverhaul;

// Receives concrete native ChangeProperty wrappers, never tile effects or skills.
// Reflection keeps the legacy scalar and 0.7.18 Properties-array layouts compatible.
internal sealed class SmokeNativeStatModifiers
{
    private readonly List<Snapshot> _snapshots = new();
    internal bool HasSnapshots => _snapshots.Count != 0;

    internal bool TryApply(IEnumerable<object> handlers, object emptyProperties, out int propertyCount)
    {
        propertyCount = 0;
        if (HasSnapshots) return false;
        var pending = new List<Snapshot>();
        try
        {
            foreach (object handler in handlers)
            {
                MemberInfo? properties = FindMember(handler, "Properties");
                if (properties != null)
                {
                    object? original = Read(handler, properties);
                    if (original == null) continue;
                    propertyCount += Convert.ToInt32(Read(original, FindMember(original, "Length")
                        ?? throw new InvalidOperationException("Smoke property array has no Length")));
                    // Keep the original array intact for exact restoration. Removing stat
                    // entries does not remove EventHandlers, tile effects or their VFX.
                    pending.Add(new Snapshot(handler, properties, original, emptyProperties));
                }
                else
                {
                    propertyCount++;
                    Add(handler, "Amount", 0, pending);
                    Add(handler, "AmountMult", 1f, pending);
                    // A provider can otherwise reintroduce a modifier at evaluation time.
                    Add(handler, "ValueProvider", null, pending);
                }
            }

            foreach (Snapshot snapshot in pending)
            {
                _snapshots.Add(snapshot); // Journal before a potentially partial setter.
                Write(snapshot.Owner, snapshot.Member, snapshot.Neutral);
                if (!SameValue(Read(snapshot.Owner, snapshot.Member), snapshot.Neutral))
                    throw new InvalidOperationException("Smoke stat modifier readback failed");
            }
            return true;
        }
        catch
        {
            Restore();
            return false;
        }
    }

    internal bool Restore()
    {
        for (int i = _snapshots.Count - 1; i >= 0; i--)
        {
            Snapshot snapshot = _snapshots[i];
            try
            {
                Write(snapshot.Owner, snapshot.Member, snapshot.Original);
                if (SameValue(Read(snapshot.Owner, snapshot.Member), snapshot.Original))
                    _snapshots.RemoveAt(i);
            }
            catch { } // Retain failed entries so disable/unload can retry restoration.
        }
        return !HasSnapshots;
    }

    private static void Add(object owner, string name, object? neutral, List<Snapshot> pending)
    {
        MemberInfo member = FindMember(owner, name)
            ?? throw new InvalidOperationException($"Missing smoke stat member: {name}");
        pending.Add(new Snapshot(owner, member, Read(owner, member), neutral));
    }

    private static MemberInfo? FindMember(object owner, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        return (MemberInfo?)owner.GetType().GetProperty(name, flags)
            ?? owner.GetType().GetField(name, flags);
    }

    private static object? Read(object owner, MemberInfo member)
        => member is PropertyInfo property ? property.GetValue(owner) : ((FieldInfo)member).GetValue(owner);

    private static void Write(object owner, MemberInfo member, object? value)
    {
        if (member is PropertyInfo property) property.SetValue(owner, value);
        else ((FieldInfo)member).SetValue(owner, value);
    }

    private static bool SameValue(object? actual, object? expected)
    {
        if (ReferenceEquals(actual, expected)) return true;
        if (actual == null || expected == null) return false;
        // A native field getter can create a fresh managed wrapper for the same
        // native array/provider. Managed reference equality would reject it.
        MemberInfo? pointer = FindMember(actual, "Pointer");
        MemberInfo? expectedPointer = FindMember(expected, "Pointer");
        if (pointer != null && expectedPointer != null && Read(actual, pointer) is IntPtr address)
            return address != IntPtr.Zero && Equals(address, Read(expected, expectedPointer));
        return Equals(actual, expected);
    }

    private sealed class Snapshot
    {
        internal Snapshot(object owner, MemberInfo member, object? original, object? neutral)
        { Owner = owner; Member = member; Original = original; Neutral = neutral; }
        internal object Owner { get; }
        internal MemberInfo Member { get; }
        internal object? Original { get; }
        internal object? Neutral { get; }
    }
}