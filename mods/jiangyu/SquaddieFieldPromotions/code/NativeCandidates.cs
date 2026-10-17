using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppMenace.States;

namespace SquaddieFieldPromotions;

internal static class NativeCandidates
{
    internal static Selection Read(StrategyState state, ISet<int> excluded)
    {
        var list = state.Squaddies.GetAllAlive()
            ?? throw new InvalidOperationException("Living squaddie collection unavailable.");
        // IReadOnlyList<T>'s proxy does NOT expose inherited Count. Rewrap the SAME
        // pointer as its sibling interface, as verified in Veterancy / the 0.6.1 lab.
        var collection = new Il2CppSystem.Collections.Generic.IReadOnlyCollection<Il2CppMenace.Strategy.Squaddie>(list.Pointer);
        int count = collection.Count;
        var snapshots = new List<Candidate>();
        MethodInfo xpApi = FindXpApi();
        for (int i = 0; i < count; i++)
        {
            var soldier = list[i] ?? throw new InvalidOperationException("Null living squaddie.");
            int id = soldier.GetId();
            if (id == Il2CppMenace.Strategy.Squaddies.INVALID_SQUADDIE_ID)
                throw new InvalidOperationException("Invalid native squaddie identity.");
            if (!state.Squaddies.IsAlive(id))
                throw new InvalidOperationException("Living squaddie collection changed during review.");
            snapshots.Add(new Candidate(id, soldier.Name, soldier.Nickname,
                (int)soldier.GetGender(), (int)soldier.GetSkinColor(), soldier.GetMissionsParticipated(),
                ReadXp(xpApi, id), soldier.GetCurrentLeader() != null));
        }
        if (count != collection.Count)
            throw new InvalidOperationException("Living squaddie count changed during review.");
        return PromotionRules.Choose(snapshots, excluded);
    }

    private static MethodInfo FindXpApi()
    {
        try
        {
            var methods = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("SquaddieProbe.CompatibilityApi", false))
                .Where(t => t != null)
                .Select(t => t!.GetMethod("GetSquaddieXp", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(int) }, null))
                .Where(m => m?.ReturnType == typeof(int)).ToArray();
            // Ambiguous providers are NOT chosen by assembly enumeration order.
            return methods.Length == 1 ? methods[0] : null;
        }
        catch { return null; } // Discovery of an OPTIONAL provider must not block native review.
    }

    private static int? ReadXp(MethodInfo api, int id)
    {
        if (api == null) return null;
        try { return api.Invoke(null, new object[] { id }) is int xp && xp >= 0 ? xp : null; }
        catch { return null; } // Optional provider failure never blocks native-missions mode.
    }
}