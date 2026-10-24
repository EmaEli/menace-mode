using System;
using System.Collections.Generic;

namespace SquaddieProbe;

internal static class CrewPersonIdentityChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string first = CrewPersonIdentity.EnsureUnique("", used);
        check(CrewPersonIdentity.IsValid(first) && used.Contains(first),
            "legacy crew record receives a valid persistent person ID");

        var oneExisting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        check(CrewPersonIdentity.EnsureUnique(first, oneExisting) == first,
            "a unique saved person ID survives normalization unchanged");

        string duplicate = CrewPersonIdentity.EnsureUnique(first, used);
        check(CrewPersonIdentity.IsValid(duplicate) && duplicate != first && used.Contains(duplicate),
            "duplicate person IDs are replaced so crew records remain distinguishable");

        string malformed = CrewPersonIdentity.EnsureUnique("not-a-guid", used);
        check(CrewPersonIdentity.IsValid(malformed) && malformed != "not-a-guid",
            "malformed saved person ID is repaired");

        string replacement = CrewPersonIdentity.Create();
        check(CrewPersonIdentity.IsValid(replacement) && replacement != first,
            "a casualty replacement receives a new individual identity");
    }
}