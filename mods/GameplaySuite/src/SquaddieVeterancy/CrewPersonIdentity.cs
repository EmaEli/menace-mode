using System;
using System.Collections.Generic;

namespace SquaddieProbe;

/// <summary>Stable identity for one person in a pilot's persistent vehicle-crew pool.</summary>
internal static class CrewPersonIdentity
{
    internal static string Create() => Guid.NewGuid().ToString("N");

    internal static bool IsValid(string? value) =>
        Guid.TryParseExact(value, "N", out Guid parsed) && parsed != Guid.Empty;

    internal static string EnsureUnique(string? value, ISet<string> usedIds)
    {
        if (usedIds == null) throw new ArgumentNullException(nameof(usedIds));
        if (IsValid(value) && usedIds.Add(value!)) return value!;

        string replacement;
        do replacement = Create();
        while (!usedIds.Add(replacement));
        return replacement;
    }
}