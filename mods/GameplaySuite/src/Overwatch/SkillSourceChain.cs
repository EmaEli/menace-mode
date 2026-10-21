using System;

namespace MenaceOverwatch;

internal static class SkillSourceChain
{
    internal const int MaximumDepth = 8;

    internal static bool TryFindItemOwner<T>(
        T? start,
        Func<T, T?> source,
        Func<T, bool> hasItem,
        Func<T, bool> hasUnsafeMetadata,
        Func<T, long> key,
        out T? itemOwner,
        out bool unsafeChain)
        where T : class
    {
        itemOwner = null;
        unsafeChain = false;
        Span<long> visited = stackalloc long[MaximumDepth];
        T? current = start;

        for (int depth = 0; depth < MaximumDepth; depth++)
        {
            if (current == null) return true;

            try
            {
                long pointer = key(current);
                if (pointer == 0)
                {
                    unsafeChain = true;
                    return false;
                }
                for (int i = 0; i < depth; i++)
                {
                    if (visited[i] != pointer) continue;
                    unsafeChain = true;
                    return false;
                }
                visited[depth] = pointer;
                unsafeChain |= hasUnsafeMetadata(current);
                if (hasItem(current))
                {
                    itemOwner = current;
                    return true;
                }
                current = source(current);
            }
            catch
            {
                unsafeChain = true;
                return false;
            }
        }

        if (current == null) return true;
        unsafeChain = true;
        return false;
    }
}