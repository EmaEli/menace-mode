using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMenace.Strategy;

namespace SquaddieProbe;

internal sealed class SquaddieIdentitySnapshot
{
    public int Id { get; init; }
    public string Name { get; init; } = "?";
    public string Nickname { get; init; } = "";
    public int Missions { get; init; }
    public bool Alive { get; init; }
}

/// <summary>
/// Strategy-layer identity and squad-membership cache.
/// Squaddie objects themselves are never retained across scenes: only primitive managed data is.
/// </summary>
internal sealed class SquaddieIdentityCache
{
    private sealed class Entry
    {
        public int Id;
        public string Name = "?";
        public string Nickname = "";
        public int Missions;
        public bool Alive;
    }

    private readonly Action<string> _log;
    private readonly Dictionary<int, Entry> _entries = new();
    private readonly Dictionary<int, int[]> _squadMembersBySquaddie = new();
    private readonly Dictionary<int, int> _squadNumberBySquaddie = new();
    private readonly Dictionary<int, string> _squadLeaderNameByNumber = new();
    // Last real (positive squad number) roster mapping seen for this StrategyState.
    // Tactical hooks may run while MENACE temporarily exposes an incomplete roster;
    // keep the last trustworthy mapping so rank modifiers never degrade to one squaddie.
    private readonly Dictionary<int, int[]> _lastKnownSquadMembersBySquaddie = new();
    private readonly Dictionary<int, int> _lastKnownSquadNumberBySquaddie = new();

    private bool _reflectionReady;
    private Type? _strategyStateType;
    private Type? _squaddieType;
    private Type? _rosterType;
    private Type? _baseUnitLeaderType;
    private MethodInfo? _strategyStateGet;
    private MemberInfo? _strategyStateSquaddies;
    private MemberInfo? _strategyStateRoster;
    private MethodInfo? _squaddiesGetAliveCount;
    private MethodInfo? _squaddiesGetAllAlive;
    private MethodInfo? _squaddieGetId;
    private MethodInfo? _squaddieGetMissionsParticipated;
    private MemberInfo? _squaddieName;
    private MemberInfo? _squaddieNickname;
    private MethodInfo? _rosterGetHiredLeaders;
    private MethodInfo? _leaderGetSquaddieIds;

    private long _nextRefreshMs;
    private long _lastStatePointer;
    private int _lastLoggedCount = -1;
    private int _lastLoggedSquadCount = -1;
    private bool _loggedWaiting;
    private string _lastSetupStatus = "";
    private string _sceneName = "?";

    public int Count => _entries.Count;
    public bool HasSnapshot { get; private set; }
    public bool SquadMappingReady { get; private set; }
    // Increments only after a StrategyState roster refresh actually succeeded.
    // Runtime features that react to scene transitions can use this to avoid
    // treating a stale pre-transition cache as the new campaign state.
    public int RefreshGeneration { get; private set; }
    // Pointer of the StrategyState used by the most recent successful refresh.
    // This lets load-sensitive systems distinguish the pre-load campaign state
    // from the state that MENACE installs for the selected save.
    public long CurrentStatePointer => _lastStatePointer;

    public SquaddieIdentityCache(Action<string> log)
    {
        _log = log;
        TryResolveReflection();
    }

    public void SceneChanged(string sceneName)
    {
        _sceneName = sceneName;
        _nextRefreshMs = 0;
        _loggedWaiting = false;
    }

    public void Update()
    {
        long now = Environment.TickCount64;
        if (now < _nextRefreshMs) return;
        _nextRefreshMs = now + 1000;

        if (!_reflectionReady && !TryResolveReflection()) return;
        RefreshFromStrategyState();
    }

    public bool Contains(int id) => id > 0 && _entries.ContainsKey(id);

    public bool TryGet(int id, out SquaddieIdentitySnapshot identity)
    {
        if (_entries.TryGetValue(id, out var e))
        {
            identity = Snapshot(e);
            return true;
        }
        identity = new SquaddieIdentitySnapshot { Id = id };
        return false;
    }

    public IReadOnlyList<SquaddieIdentitySnapshot> GetRosterSnapshot()
        => _entries.Values.OrderBy(x => x.Id).Select(Snapshot).ToList();

    public IReadOnlyList<int> GetSquadMembers(int squaddieId)
    {
        if (_squadMembersBySquaddie.TryGetValue(squaddieId, out var ids) && ids.Length > 0)
            return ids;
        return squaddieId > 0 ? new[] { squaddieId } : Array.Empty<int>();
    }

    public int GetSquadNumber(int squaddieId)
        => _squadNumberBySquaddie.TryGetValue(squaddieId, out int n) ? n : 0;

    public bool TryGetReliableSquad(int squaddieId, out int squadNumber, out IReadOnlyList<int> members, out bool recovered)
    {
        squadNumber = 0;
        members = Array.Empty<int>();
        recovered = false;
        if (squaddieId <= 0) return false;

        if (_squadNumberBySquaddie.TryGetValue(squaddieId, out int currentSquad)
            && currentSquad > 0
            && _squadMembersBySquaddie.TryGetValue(squaddieId, out var currentMembers)
            && currentMembers.Length > 0)
        {
            squadNumber = currentSquad;
            members = currentMembers;
            return true;
        }

        // Never use an old mapping while the player is actively managing squads in
        // Strategy. It is only a bridge for transient Tactical/MissionPreparation
        // gaps where the current roster API momentarily returns squad=0.
        bool canRecover = _sceneName.Equals("Tactical", StringComparison.OrdinalIgnoreCase)
            || _sceneName.Equals("MissionPreparation", StringComparison.OrdinalIgnoreCase);
        if (!canRecover) return false;

        if (_lastKnownSquadNumberBySquaddie.TryGetValue(squaddieId, out int cachedSquad)
            && cachedSquad > 0
            && _lastKnownSquadMembersBySquaddie.TryGetValue(squaddieId, out var cachedMembers)
            && cachedMembers.Length > 0)
        {
            squadNumber = cachedSquad;
            members = cachedMembers;
            recovered = true;
            return true;
        }

        return false;
    }

    public string GetSquadLeaderName(int squadNumber)
        => squadNumber > 0 && _squadLeaderNameByNumber.TryGetValue(squadNumber, out string? name)
            ? name
            : "Unknown";

    public string Label(int id)
    {
        if (id <= 0) return "none";
        if (!_entries.TryGetValue(id, out var e))
            return HasSnapshot ? $"id={id}:not-in-cache" : $"id={id}:cache-not-ready";

        return $"id={id}:name={Sanitize(e.Name)}:nick={Sanitize(e.Nickname)}:missions={e.Missions}:alive={e.Alive}:squad={GetSquadNumber(id)}";
    }

    public void MarkKilled(int id)
    {
        if (id <= 0) return;
        if (_entries.TryGetValue(id, out var e)) e.Alive = false;
    }

    private bool TryResolveReflection()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => ReferenceEquals(a, typeof(Squaddie).Assembly)
                         || (a.GetName().Name ?? "").Contains("Menace", StringComparison.OrdinalIgnoreCase)
                         || (a.GetName().Name ?? "").Contains("Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();

            var types = new List<Type>();
            foreach (var assembly in assemblies)
            {
                try { types.AddRange(assembly.GetTypes()); }
                catch (ReflectionTypeLoadException e) { types.AddRange(e.Types.Where(t => t != null).Cast<Type>()); }
                catch { }
            }

            _strategyStateType = types.FirstOrDefault(t => t.Name == "StrategyState" && (t.FullName ?? "").Contains("Menace.Strategy", StringComparison.OrdinalIgnoreCase))
                ?? types.FirstOrDefault(t => t.Name == "StrategyState");
            _squaddieType = typeof(Squaddie);
            Type squaddiesType = typeof(Squaddies);
            _rosterType = types.FirstOrDefault(t => t.Name == "Roster" && (t.FullName ?? "").Contains("Menace.Strategy", StringComparison.OrdinalIgnoreCase))
                ?? types.FirstOrDefault(t => t.Name == "Roster");
            _baseUnitLeaderType = types.FirstOrDefault(t => t.Name == "BaseUnitLeader" && (t.FullName ?? "").Contains("Menace.Strategy", StringComparison.OrdinalIgnoreCase))
                ?? types.FirstOrDefault(t => t.Name == "BaseUnitLeader");

            if (_strategyStateType == null || _squaddieType == null)
            {
                LogSetupStatus("SQUADDIE CACHE setup waiting: StrategyState/Squaddie type missing");
                return false;
            }

            _strategyStateGet = _strategyStateType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            _strategyStateSquaddies = FindReadableMember(_strategyStateType, "Squaddies");
            _strategyStateRoster = FindReadableMember(_strategyStateType, "Roster");
            _squaddiesGetAliveCount = squaddiesType.GetMethod("GetAliveCount", BindingFlags.Public | BindingFlags.Instance);
            _squaddiesGetAllAlive = squaddiesType.GetMethod("GetAllAlive", BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetId = _squaddieType.GetMethod("GetId", BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetMissionsParticipated = _squaddieType.GetMethod("GetMissionsParticipated", BindingFlags.Public | BindingFlags.Instance);
            _squaddieName = FindReadableMember(_squaddieType, "Name");
            _squaddieNickname = FindReadableMember(_squaddieType, "Nickname");

            if (_rosterType != null)
                _rosterGetHiredLeaders = _rosterType.GetMethod("GetHiredLeaders", BindingFlags.Public | BindingFlags.Instance);
            if (_baseUnitLeaderType != null)
                _leaderGetSquaddieIds = _baseUnitLeaderType.GetMethod("GetSquaddieIds", BindingFlags.Public | BindingFlags.Instance);

            _reflectionReady = _strategyStateGet != null
                && _strategyStateSquaddies != null
                && _squaddiesGetAllAlive != null
                && _squaddieGetId != null
                && _squaddieName != null
                && _squaddieNickname != null;

            if (_reflectionReady)
            {
                bool squads = _strategyStateRoster != null && _rosterGetHiredLeaders != null && _leaderGetSquaddieIds != null && _baseUnitLeaderType != null;
                LogSetupStatus($"SQUADDIE CACHE setup ready: identities=yes squadMembership={(squads ? "yes" : "no")}");
            }
            else
                LogSetupStatus("SQUADDIE CACHE setup incomplete: one or more required identity members were not resolved");
        }
        catch (Exception e)
        {
            LogSetupStatus($"SQUADDIE CACHE setup error {e.GetType().Name}: {e.Message}");
            _reflectionReady = false;
        }

        return _reflectionReady;
    }

    private void RefreshFromStrategyState()
    {
        if (!_reflectionReady || _strategyStateGet == null || _strategyStateSquaddies == null
            || _squaddiesGetAllAlive == null
            || _squaddieType == null || _squaddieGetId == null || _squaddieName == null || _squaddieNickname == null)
            return;

        try
        {
            object? state = _strategyStateGet.Invoke(null, null);
            if (state == null)
            {
                if (!_loggedWaiting && _sceneName.Equals("Strategy", StringComparison.OrdinalIgnoreCase))
                {
                    _log("SQUADDIE CACHE waiting: StrategyState.Get() returned null in Strategy scene; will retry");
                    _loggedWaiting = true;
                }
                return;
            }

            long statePtr = PointerOf(state);
            if (statePtr != 0 && _lastStatePointer != 0 && statePtr != _lastStatePointer)
            {
                _entries.Clear();
                _squadMembersBySquaddie.Clear();
                _squadNumberBySquaddie.Clear();
                _squadLeaderNameByNumber.Clear();
                _lastKnownSquadMembersBySquaddie.Clear();
                _lastKnownSquadNumberBySquaddie.Clear();
                HasSnapshot = false;
                SquadMappingReady = false;
                _lastLoggedCount = -1;
                _lastLoggedSquadCount = -1;
                _log($"SQUADDIE CACHE campaign/state changed old=0x{_lastStatePointer:X} new=0x{statePtr:X}; managed cache reset");
            }
            if (statePtr != 0) _lastStatePointer = statePtr;

            object? manager = GetMemberValue(_strategyStateSquaddies, state);
            if (manager == null) return;

            object? allAlive = _squaddiesGetAllAlive.Invoke(manager, null);
            if (allAlive == null) return;

            int aliveCount;
            if (!TryGetCollectionCount(allAlive, out aliveCount))
            {
                if (_squaddiesGetAliveCount != null)
                    aliveCount = Convert.ToInt32(_squaddiesGetAliveCount.Invoke(manager, null) ?? 0); // pre-0.7.15 fallback
                else
                {
                    _log($"SQUADDIE CACHE error: GetAllAlive() type {allAlive.GetType().FullName} exposes neither Count/get_Count nor legacy GetAliveCount()");
                    return;
                }
            }

            MarkAllNotAlive();
            int read = 0;
            for (int i = 0; i < aliveCount; i++)
            {
                if (!TryGetCollectionItem(allAlive, i, out object? raw) || raw == null)
                    continue;

                object proxy = CreateTypedProxy(raw, _squaddieType);
                int id = SafeInt(_squaddieGetId, proxy, -1);
                if (id <= 0) continue;

                string name = SafeString(_squaddieName, proxy, "?");
                string nick = SafeString(_squaddieNickname, proxy, "");
                int missions = SafeInt(_squaddieGetMissionsParticipated, proxy, 0);

                if (!_entries.TryGetValue(id, out var e))
                {
                    e = new Entry { Id = id };
                    _entries[id] = e;
                }
                e.Name = name;
                e.Nickname = nick;
                e.Missions = missions;
                e.Alive = true;
                read++;
            }

            HasSnapshot = true;
            _loggedWaiting = false;
            RefreshSquadMembership(state);
            RefreshGeneration++;

            if (read != _lastLoggedCount)
            {
                _lastLoggedCount = read;
                _log($"SQUADDIE CACHE READY count={read} squads={_squadMembersBySquaddie.Values.Select(x => string.Join(",", x)).Distinct().Count()} state=0x{statePtr:X}");
                foreach (var e in _entries.Values.Where(x => x.Alive).OrderBy(x => x.Id).Take(16))
                    _log($"SQUADDIE CACHE id={e.Id} name={Sanitize(e.Name)} nick={Sanitize(e.Nickname)} missions={e.Missions} squad={GetSquadNumber(e.Id)}");
                if (read > 16) _log($"SQUADDIE CACHE list truncated: {read - 16} additional alive squaddies");
            }
        }
        catch (TargetInvocationException e)
        {
            string msg = e.InnerException?.Message ?? e.Message;
            _log($"SQUADDIE CACHE refresh invocation error: {msg}");
        }
        catch (Exception e)
        {
            _log($"SQUADDIE CACHE refresh error {e.GetType().Name}: {e.Message}");
        }
    }

    private void RefreshSquadMembership(object state)
    {
        if (_strategyStateRoster == null || _rosterGetHiredLeaders == null || _leaderGetSquaddieIds == null || _baseUnitLeaderType == null)
        {
            SquadMappingReady = false;
            EnsureFallbackSquads();
            return;
        }

        try
        {
            object? roster = GetMemberValue(_strategyStateRoster, state);
            object? hired = roster == null ? null : _rosterGetHiredLeaders.Invoke(roster, null);
            if (hired == null)
            {
                SquadMappingReady = false;
                EnsureFallbackSquads();
                return;
            }

            if (!TryGetCollectionCount(hired, out int leaderCount))
            {
                SquadMappingReady = false;
                EnsureFallbackSquads();
                _log($"SQUAD MEMBERSHIP fallback: GetHiredLeaders() type {hired.GetType().FullName} exposes no Count/get_Count");
                return;
            }

            _squadMembersBySquaddie.Clear();
            _squadNumberBySquaddie.Clear();
            _squadLeaderNameByNumber.Clear();
            int mappedSquads = 0;

            for (int i = 0; i < leaderCount; i++)
            {
                if (!TryGetCollectionItem(hired, i, out object? rawLeader) || rawLeader == null)
                    continue;
                object leader = CreateTypedProxy(rawLeader, _baseUnitLeaderType);
                string leaderName = ResolveLeaderDisplayName(leader);
                object? ids = _leaderGetSquaddieIds.Invoke(leader, null);
                if (ids == null) continue;

                if (!TryGetCollectionCount(ids, out int count))
                    continue;

                var members = new List<int>();
                for (int j = 0; j < count; j++)
                {
                    if (!TryGetCollectionItem(ids, j, out object? rawId) || rawId == null)
                        continue;
                    int id = Convert.ToInt32(rawId);
                    if (id > 0) members.Add(id);
                }
                if (members.Count == 0) continue;

                int[] frozen = members.Distinct().ToArray();
                int squadNumber = i + 1;
                _squadLeaderNameByNumber[squadNumber] = string.IsNullOrWhiteSpace(leaderName) ? "Unknown" : leaderName;
                foreach (int id in frozen)
                {
                    _squadMembersBySquaddie[id] = frozen;
                    _squadNumberBySquaddie[id] = squadNumber;
                    _lastKnownSquadMembersBySquaddie[id] = frozen;
                    _lastKnownSquadNumberBySquaddie[id] = squadNumber;
                }
                mappedSquads++;
            }

            EnsureFallbackSquads();
            SquadMappingReady = mappedSquads > 0;
            if (mappedSquads != _lastLoggedSquadCount)
            {
                _lastLoggedSquadCount = mappedSquads;
                _log($"SQUAD MEMBERSHIP READY squads={mappedSquads} mappedSquaddies={_squadMembersBySquaddie.Count}");
                foreach (var pair in _squadLeaderNameByNumber.OrderBy(x => x.Key))
                    _log($"SQUAD LEADER squad={pair.Key} name={Sanitize(pair.Value)}");
            }
        }
        catch (Exception e)
        {
            SquadMappingReady = false;
            EnsureFallbackSquads();
            _log($"SQUAD MEMBERSHIP refresh error {e.GetType().Name}: {e.Message}");
        }
    }


    private static string ResolveLeaderDisplayName(object leader)
    {
        string? direct = TryReadTextMember(
            leader,
            new[] { "DisplayName", "Name", "Nickname", "m_DisplayName", "m_Name", "m_Nickname" },
            new[] { "GetDisplayName", "GetName", "GetNickname" });
        if (IsUsefulLeaderText(direct)) return HumanizeLeaderText(direct!);

        object? template = TryReadObjectMember(
            leader,
            new[] { "Template", "m_Template", "UnitLeaderTemplate", "m_UnitLeaderTemplate", "LeaderTemplate", "m_LeaderTemplate" },
            new[] { "GetTemplate", "GetUnitLeaderTemplate", "GetLeaderTemplate" });
        if (template != null)
        {
            string? templateText = TryReadTextMember(
                template,
                new[] { "DisplayName", "Name", "Nickname", "m_ID", "ID", "SpeakerTemplate" },
                new[] { "GetDisplayName", "GetName", "GetID" });
            if (IsUsefulLeaderText(templateText)) return HumanizeLeaderText(templateText!);
        }

        return "Unknown";
    }

    private static string? TryReadTextMember(object target, string[] propertyOrFieldNames, string[] methodNames)
    {
        Type type = target.GetType();
        foreach (string name in propertyOrFieldNames)
        {
            try
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    string? value = property.GetValue(target)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            catch { }

            try
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    string? value = field.GetValue(target)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            catch { }
        }

        foreach (string name in methodNames)
        {
            try
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method == null) continue;
                string? value = method.Invoke(target, null)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }
        }

        return null;
    }

    private static object? TryReadObjectMember(object target, string[] propertyOrFieldNames, string[] methodNames)
    {
        Type type = target.GetType();
        foreach (string name in propertyOrFieldNames)
        {
            try
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    object? value = property.GetValue(target);
                    if (value != null) return value;
                }
            }
            catch { }

            try
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object? value = field.GetValue(target);
                    if (value != null) return value;
                }
            }
            catch { }
        }

        foreach (string name in methodNames)
        {
            try
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method == null) continue;
                object? value = method.Invoke(target, null);
                if (value != null) return value;
            }
            catch { }
        }

        return null;
    }

    private static bool IsUsefulLeaderText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string v = value.Trim();
        if (v.Contains("BaseUnitLeader", StringComparison.OrdinalIgnoreCase)) return false;
        if (v.Contains("UnitLeaderTemplate", StringComparison.OrdinalIgnoreCase)) return false;
        if (v.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string HumanizeLeaderText(string value)
    {
        string v = value.Trim();
        int slash = Math.Max(v.LastIndexOf('/'), v.LastIndexOf('\\'));
        if (slash >= 0 && slash + 1 < v.Length) v = v[(slash + 1)..];
        int dot = v.LastIndexOf('.');
        if (dot >= 0 && dot + 1 < v.Length) v = v[(dot + 1)..];
        v = v.Replace('_', ' ').Trim();
        if (v.Length == 0) return "Unknown";

        string[] parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0) continue;
            if (part.All(c => !char.IsLetter(c) || char.IsLower(c)))
                parts[i] = char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part[1..] : "");
        }
        return string.Join(" ", parts);
    }

    private void EnsureFallbackSquads()
    {
        foreach (var e in _entries.Values.Where(x => x.Alive))
        {
            if (!_squadMembersBySquaddie.ContainsKey(e.Id)) _squadMembersBySquaddie[e.Id] = new[] { e.Id };
            if (!_squadNumberBySquaddie.ContainsKey(e.Id)) _squadNumberBySquaddie[e.Id] = 0;
        }
    }

    private void MarkAllNotAlive()
    {
        foreach (var e in _entries.Values) e.Alive = false;
    }

    private static SquaddieIdentitySnapshot Snapshot(Entry e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Nickname = e.Nickname,
        Missions = e.Missions,
        Alive = e.Alive
    };

    private static IEnumerable<Type> ReflectionSurfaces(Type type)
    {
        yield return type;
        foreach (Type iface in type.GetInterfaces())
            yield return iface;
    }

    // MENACE 0.7.15 returns IL2CPP IReadOnlyList<T> interface proxies for several
    // strategy collections. Il2CppInterop represents IReadOnlyList<T> and its
    // parent IReadOnlyCollection<T> as separate CLR proxy classes, so normal
    // Reflection/GetInterfaces() cannot see the inherited Count member. Re-wrap
    // the SAME native pointer as IReadOnlyCollection<T> and read Count there.
    // This is generic and therefore also covers Roster.GetHiredLeaders() and
    // BaseUnitLeader.GetSquaddieIds().
    private static bool TryGetCollectionCount(object collection, out int count)
    {
        count = 0;

        // Ordinary CLR/concrete IL2CPP collections first (List<T>, arrays, etc.).
        if (TryReadCount(collection, collection.GetType(), out count))
            return true;

        foreach (Type surface in ReflectionSurfaces(collection.GetType()))
        {
            if (surface == collection.GetType()) continue;
            if (TryReadCount(collection, surface, out count))
                return true;
        }

        // IL2CPP IReadOnlyList<T> bridge: construct the sibling
        // IReadOnlyCollection<T> proxy over the same native object pointer.
        try
        {
            Type listType = collection.GetType();
            Type[] args = listType.IsGenericType ? listType.GetGenericArguments() : Type.EmptyTypes;
            if (args.Length == 1 && (listType.Name.StartsWith("IReadOnlyList`", StringComparison.Ordinal)
                                  || (listType.FullName ?? "").Contains("IReadOnlyList`1", StringComparison.Ordinal)))
            {
                Type? openCollection = listType.Assembly.GetType("Il2CppSystem.Collections.Generic.IReadOnlyCollection`1", throwOnError: false);
                if (openCollection != null)
                {
                    Type countType = openCollection.MakeGenericType(args);
                    object countProxy = CreateTypedProxy(collection, countType);
                    if (TryReadCount(countProxy, countType, out count))
                        return true;
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
            var property = surface.GetProperty("Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                count = Convert.ToInt32(property.GetValue(collection) ?? 0);
                return true;
            }
        }
        catch { }

        try
        {
            var getter = surface.GetMethod("get_Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (getter != null)
            {
                count = Convert.ToInt32(getter.Invoke(collection, null) ?? 0);
                return true;
            }
        }
        catch { }

        return false;
    }

    private static PropertyInfo? FindIndexer(Type type)
    {
        foreach (Type surface in ReflectionSurfaces(type))
        {
            try
            {
                var indexer = surface.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(p => p.GetIndexParameters().Length == 1
                        && p.GetIndexParameters()[0].ParameterType == typeof(int));
                if (indexer != null) return indexer;
            }
            catch { }
        }
        return null;
    }

    private static bool TryGetCollectionItem(object collection, int index, out object? item)
    {
        item = null;
        PropertyInfo? indexer = FindIndexer(collection.GetType());
        if (indexer != null)
        {
            try
            {
                item = indexer.GetValue(collection, new object[] { index });
                return true;
            }
            catch { }
        }

        foreach (Type surface in ReflectionSurfaces(collection.GetType()))
        {
            try
            {
                var getter = surface.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (getter == null) continue;
                item = getter.Invoke(collection, new object[] { index });
                return true;
            }
            catch { }
        }
        return false;
    }

    private static object CreateTypedProxy(object item, Type targetType)
    {
        if (item.GetType() == targetType) return item;
        if (item is Il2CppObjectBase il2cppItem)
        {
            IntPtr ptr = il2cppItem.Pointer;
            if (ptr != IntPtr.Zero)
            {
                object? typed = Activator.CreateInstance(targetType, new object[] { ptr });
                if (typed != null) return typed;
            }
        }
        return item;
    }

    private static int SafeInt(MethodInfo? method, object target, int fallback)
    {
        if (method == null) return fallback;
        try { return Convert.ToInt32(method.Invoke(target, null)); }
        catch { return fallback; }
    }


    private void LogSetupStatus(string message)
    {
        if (string.Equals(_lastSetupStatus, message, StringComparison.Ordinal)) return;
        _lastSetupStatus = message;
        _log(message);
    }

    private static MemberInfo? FindReadableMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return (MemberInfo?)type.GetProperty(name, flags) ?? type.GetField(name, flags);
    }

    private static object? GetMemberValue(MemberInfo? member, object target)
    {
        try
        {
            return member switch
            {
                PropertyInfo property => property.GetValue(target),
                FieldInfo field => field.GetValue(target),
                _ => null
            };
        }
        catch { return null; }
    }

    private static string SafeString(MemberInfo member, object target, string fallback)
    {
        try { return GetMemberValue(member, target)?.ToString() ?? fallback; }
        catch { return fallback; }
    }

    private static long PointerOf(object? value)
    {
        try { return value is Il2CppObjectBase io ? io.Pointer.ToInt64() : 0; }
        catch { return 0; }
    }

    private static string Sanitize(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
}
