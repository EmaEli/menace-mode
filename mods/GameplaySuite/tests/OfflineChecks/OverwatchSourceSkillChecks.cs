using System;
using MenaceOverwatch;

internal static class OverwatchSourceSkillChecks
{
    private sealed class Node(long pointer, bool hasItem, bool unsafeMetadata)
    {
        public long Pointer { get; } = pointer;
        public bool HasItem { get; } = hasItem;
        public bool UnsafeMetadata { get; } = unsafeMetadata;
        public Node? Source { get; set; }
    }

    internal static void Run(Action<bool, string> check)
    {
        var direct = new Node(1, true, false);
        bool found = SkillSourceChain.TryFindItemOwner(direct, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out var owner, out bool unsafeChain);
        check(found && ReferenceEquals(owner, direct) && !unsafeChain, "direct weapon item remains preferred");

        var origin = new Node(3, true, false);
        var derived = new Node(2, false, false) { Source = origin };
        found = SkillSourceChain.TryFindItemOwner(derived, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out owner, out unsafeChain);
        check(found && ReferenceEquals(owner, origin) && !unsafeChain, "derived skill resolves its source weapon");

        var indirectOrigin = new Node(5, true, true);
        derived = new Node(4, false, false) { Source = indirectOrigin };
        found = SkillSourceChain.TryFindItemOwner(derived, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out owner, out unsafeChain);
        check(found && ReferenceEquals(owner, indirectOrigin) && unsafeChain, "indirect source restrictions propagate");

        var empty = new Node(6, false, false);
        found = SkillSourceChain.TryFindItemOwner(empty, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out owner, out unsafeChain);
        check(found && owner == null && !unsafeChain, "itemless native attacks remain distinguishable from bad chains");

        var first = new Node(7, false, false);
        var second = new Node(8, false, false);
        first.Source = second;
        second.Source = first;
        found = SkillSourceChain.TryFindItemOwner(first, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out owner, out unsafeChain);
        check(!found && unsafeChain, "cyclic source chains fail closed");

        var chainStart = new Node(100, false, false);
        var cursor = chainStart;
        for (int depth = 1; depth < SkillSourceChain.MaximumDepth; depth++)
        {
            cursor.Source = new Node(100 + depth, depth == SkillSourceChain.MaximumDepth - 1, false);
            cursor = cursor.Source;
        }
        found = SkillSourceChain.TryFindItemOwner(chainStart, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out owner, out unsafeChain);
        check(found && ReferenceEquals(owner, cursor) && !unsafeChain, "bounded source chain accepts its final allowed node");

        var overlong = new Node(200, false, false);
        cursor = overlong;
        for (int depth = 1; depth <= SkillSourceChain.MaximumDepth; depth++)
        {
            cursor.Source = new Node(200 + depth, depth == SkillSourceChain.MaximumDepth, false);
            cursor = cursor.Source;
        }
        found = SkillSourceChain.TryFindItemOwner(overlong, node => node.Source,
            node => node.HasItem, node => node.UnsafeMetadata, node => node.Pointer,
            out owner, out unsafeChain);
        check(!found && owner == null && unsafeChain, "source chains beyond the bound fail closed");
    }
}