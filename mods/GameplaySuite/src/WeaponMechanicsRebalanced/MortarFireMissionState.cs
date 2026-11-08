namespace MenaceWeaponMechanicsRebalanced;

// Coordinates only: a fire mission must never follow a hidden actor.
internal sealed class MortarFireMissionState
{
    private readonly Dictionary<(long Actor, long Agent, long Skill), (int X, int Z, int Start, int End)> _missions = new();
    private long _map;
    private int _round = -1;

    internal void Clear()
    {
        _missions.Clear();
        _map = 0;
        _round = -1;
    }

    internal void Observe(long map, int round)
    {
        if (_map != map || round < _round) Clear();
        _map = map;
        _round = round;
        foreach (var key in _missions.Where(pair => round > pair.Value.End).Select(pair => pair.Key).ToArray())
            _missions.Remove(key);
    }

    internal bool TryGet((long Actor, long Agent, long Skill) key, out (int X, int Z) target)
    {
        target = default;
        if (!_missions.TryGetValue(key, out var mission)) return false;
        target = (mission.X, mission.Z);
        return true;
    }

    internal void Acquire((long Actor, long Agent, long Skill) key, int x, int z, int rounds)
    {
        // Re-evaluations and blind follow-up shots never renew or move a mission.
        if (_map == 0 || _round < 0 || _missions.ContainsKey(key)) return;
        _missions[key] = (x, z, _round, _round + Math.Clamp(rounds, 2, 4) - 1);
    }

    internal void SetDuration(int rounds)
    {
        foreach (var key in _missions.Keys.ToArray())
        {
            var mission = _missions[key];
            _missions[key] = (mission.X, mission.Z, mission.Start, mission.Start + Math.Clamp(rounds, 2, 4) - 1);
        }
        Observe(_map, _round);
    }

    internal static int Priority(int nativeScore) => (int)Math.Min(int.MaxValue, Math.Max(250L, (long)nativeScore * 6));
}