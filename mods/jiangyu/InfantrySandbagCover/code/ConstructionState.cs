namespace InfantrySandbagCover;

// No native wrappers: actor pointers and integer tile coordinates, as in mortar ranging.
internal sealed class ConstructionState
{
    internal const int DefaultTurns = 2;
    internal const int MaxTurns = 10;
    internal sealed class Site
    {
        internal int X, Z, Required, Completed, LastWorkedRound = -1, MovedRound = -1;
    }

    private readonly Dictionary<long, Site> _sites = new();
    internal IEnumerable<long> Actors => _sites.Keys.ToArray();
    internal Site Get(long actor) => _sites.TryGetValue(actor, out var site) ? site : null;

    internal bool CanWork(long actor, int x, int z, int round, int tilesMoved)
    {
        if (actor == 0 || round < 0 || tilesMoved > 0) return false;
        var site = Get(actor);
        return site == null || (site.LastWorkedRound != round && site.MovedRound != round
            && (site.Completed == 0 || (site.X == x && site.Z == z)));
    }

    // Called only after native Skill.Use accepts and charges the action.
    // Final work is committed separately, only if native placement actually adds cover.
    internal bool BeginWork(long actor, int x, int z, int round, int tilesMoved, int required)
    {
        if (!CanWork(actor, x, z, round, tilesMoved))
            throw new InvalidOperationException("Construction is not available for this actor/round/tile.");
        var site = Get(actor);
        if (site == null) _sites[actor] = site = new Site();
        if (site.Completed == 0)
        {
            site.X = x;
            site.Z = z;
            // Changing the setting affects new sites, not already-paid work.
            site.Required = Math.Clamp(required, 1, MaxTurns);
        }
        if (site.Completed + 1 >= site.Required) return true;
        site.Completed++;
        site.LastWorkedRound = round;
        return false;
    }

    internal void Complete(long actor, int round)
    {
        var site = Get(actor) ?? throw new InvalidOperationException("Missing construction site.");
        site.Completed = 0;
        site.LastWorkedRound = round;
    }

    internal void Moved(long actor, int round)
    {
        var site = Get(actor);
        if (site == null) _sites[actor] = site = new Site();
        site.Completed = 0;
        site.MovedRound = round;
    }

    internal void Remove(long actor) => _sites.Remove(actor);
    internal void Clear() => _sites.Clear();
}