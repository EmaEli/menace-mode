using System.Text.Json;
using UnityEngine;

namespace SquaddieProbe;

/// <summary>
/// Save-aware persistence for squaddie veterancy.
///
/// XP lives in memory while the game state is unsaved. Whenever MENACE writes a
/// real .save file we write a matching snapshot under UserData/SquaddieProbe/Saves.
/// When MENACE loads an older save, the matching snapshot is loaded before Squaddies are
/// deserialized, so veterancy rolls back with the game state instead of leaking
/// future XP into the past.
/// </summary>
internal sealed class ExperienceStore : IDisposable
{
    public sealed class PersistedFile
    {
        public int Version { get; set; } = 8;
        public string SaveFile { get; set; } = "";
        public string TimestampUtc { get; set; } = "";
        public List<Record> Squaddies { get; set; } = new();
        public List<PilotRecord> Pilots { get; set; } = new();
    }

    // alpha.7/alpha.8 global format. Kept only for one-time migration.
    private sealed class LegacyPersistedFile
    {
        public int Version { get; set; } = 1;
        public List<Record> Squaddies { get; set; } = new();
    }

    public sealed class Record
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Nickname { get; set; } = "";
        public int Xp { get; set; }
    }

    public sealed class PilotRecord
    {
        public string TemplateId { get; set; } = "";
        public int TemplateGuid { get; set; }
        public string Name { get; set; } = "";
        public string LastVehicleGuid { get; set; } = "";
        public string LastVehicleName { get; set; } = "";

        // Schema v3 stored one XP value directly on the Pilot. Schema v4 used
        // four fixed anonymous crew slots. Schema v5/v6 made weapon roles themselves
        // the persistent crew identity. Schema v8 fixes that model: XP/status belong
        // to persistent PEOPLE in the Pilot's crew pool, while Light/Medium/Heavy
        // roles are only temporary assignments derived from the current loadout.
        public int Xp { get; set; }
        public List<CrewMemberRecord> Crew { get; set; } = new();
        public List<string> ActiveCrewKeys { get; set; } = new();
        public int CrewLayoutVersion { get; set; }
    }

    public sealed class CrewMemberRecord
    {
        // Slot remains as a stable persistence/display ordinal for compatibility.
        public int Slot { get; set; }
        // Stable personnel-pool identity. It must not change when the loadout changes.
        public string Key { get; set; } = "";
        // Current role assignment. This can change freely without affecting XP/status.
        public string AssignedRoleKey { get; set; } = "";
        public string Role { get; set; } = "Crewman";
        public int Xp { get; set; }
        public string Status { get; set; } = "Healthy";
    }

    public sealed class CrewMutationResult
    {
        public int Slot { get; init; }
        public string Key { get; init; } = "";
        public string Role { get; init; } = "Crewman";
        public int XpBefore { get; init; }
        public int XpAfter { get; init; }
        public string StatusBefore { get; init; } = "Healthy";
        public string StatusAfter { get; init; } = "Healthy";
    }

    private readonly Action<string> _log;
    private readonly Dictionary<int, Record> _records = new();
    private readonly Dictionary<string, PilotRecord> _pilotRecords = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _watchLock = new();
    private readonly Dictionary<string, long> _pendingSaveWrites = new(StringComparer.OrdinalIgnoreCase);
    // Last save-file timestamps for which this store has established a baseline or
    // successfully written a matching sidecar. This lets shutdown recover a MENACE
    // save write even if FileSystemWatcher delivery was delayed or missed.
    private readonly Dictionary<string, long> _knownSaveWriteTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _root;
    private readonly string _legacyPath;
    private readonly string _legacyBackupPath;
    private readonly string _snapshotRoot;

    private FileSystemWatcher? _watcher;
    private string? _watchedDirectory;
    private string? _currentSavePath;
    private bool _dirty;
    private bool _legacyAvailable;
    private long _nextWatcherAttemptMs;
    private bool _dumpedSaveStateScalars;

    private sealed class SaveHeaderFingerprint
    {
        public string Path { get; init; } = "";
        public int Version { get; init; }
        public int SaveType { get; init; }
        public long TimestampTicks { get; init; }
        public string PlanetName { get; init; } = "";
        public string OperationName { get; init; } = "";
        public int CompletedMissions { get; init; }
        public int TotalMissions { get; init; }
        public string DifficultyId { get; init; } = "";
        public string StrategyConfigId { get; init; } = "";
        public double PlayTimeSeconds { get; init; }
        public string SaveGameName { get; init; } = "";
    }

    private sealed class SaveStateScalars
    {
        public HashSet<string> Strings { get; } = new(StringComparer.Ordinal);
        public HashSet<long> Integers { get; } = new();
        public List<double> Reals { get; } = new();
        public HashSet<long> DateTicks { get; } = new();
        public List<string> DebugValues { get; } = new();
    }

    public ExperienceStore(Action<string> log)
    {
        _log = log;

        string gameRoot = Environment.CurrentDirectory;
        if (!File.Exists(System.IO.Path.Combine(gameRoot, "Menace.exe")))
            gameRoot = AppContext.BaseDirectory;

        _root = System.IO.Path.Combine(gameRoot, "UserData", "SquaddieProbe");
        _legacyPath = System.IO.Path.Combine(_root, "veterancy.json");
        _legacyBackupPath = System.IO.Path.Combine(_root, "veterancy.alpha8-backup.json");
        _snapshotRoot = System.IO.Path.Combine(_root, "Saves");
        _legacyAvailable = File.Exists(_legacyPath);

        if (_legacyAvailable)
            _log($"XP LEGACY STORE detected path={_legacyPath}; it will migrate once to the first loaded save without a sidecar");
        else
            _log("XP STORE save-aware mode: waiting for a MENACE save/load before creating a sidecar");
    }

    public string PathOnDisk => _currentSavePath == null ? _snapshotRoot : SidecarPath(_currentSavePath);
    public string CurrentSaveLabel => _currentSavePath == null ? "unsaved/new campaign" : System.IO.Path.GetFileName(_currentSavePath);
    public int RecordCount => _records.Count;
    public int PilotRecordCount => _pilotRecords.Count;
    public bool HasUnsavedChanges => _dirty;
    public bool SaveWatcherReady => _watcher != null;
    private bool _snapshotWritesSuspended;

    public void SetSnapshotWritesSuspended(bool suspended)
        => _snapshotWritesSuspended = suspended;

    public void Update()
    {
        TryInitializeWatcher();
        // Never flush a sidecar while the strategy identity cache is unavailable.
        // A game patch can temporarily make the roster unreadable; writing at that
        // point would replace valid squaddie XP with an empty snapshot.
        if (_snapshotWritesSuspended) return;

        List<string>? due = null;
        long now = Environment.TickCount64;
        lock (_watchLock)
        {
            foreach (var kv in _pendingSaveWrites)
            {
                if (now < kv.Value) continue;
                due ??= new List<string>();
                due.Add(kv.Key);
            }
            if (due != null)
                foreach (string path in due) _pendingSaveWrites.Remove(path);
        }

        if (due == null) return;
        foreach (string savePath in due)
        {
            try
            {
                if (!File.Exists(savePath)) continue;
                WriteSnapshot(savePath, "game-save");
            }
            catch (Exception e)
            {
                _log($"XP SNAPSHOT ERROR save={SafeFileName(savePath)} {e.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Resolve the actual .save selected by MENACE without patching TryGetSaveState.
    /// TryGetSaveState has a by-ref/out parameter and proved unsafe to patch under
    /// IL2CPP (it can leave the Load screen empty). ExecLoad gives us a normal
    /// SaveState value, so we fingerprint its scalar header values and match them
    /// against the real save files on disk.
    /// </summary>
    public string? ResolveGameSavePath(object saveState)
    {
        if (saveState == null) return null;
        TryInitializeWatcher();

        string? saveDir = _watchedDirectory;
        if (string.IsNullOrWhiteSpace(saveDir) || !Directory.Exists(saveDir))
        {
            try
            {
                string candidate = System.IO.Path.Combine(Application.persistentDataPath, "Saves");
                if (Directory.Exists(candidate)) saveDir = candidate;
            }
            catch { }
        }
        if (string.IsNullOrWhiteSpace(saveDir) || !Directory.Exists(saveDir))
        {
            _log("SAVE FINGERPRINT unresolved: save directory is unavailable");
            return null;
        }

        SaveStateScalars scalars = ExtractSaveStateScalars(saveState);

        // If the runtime object happens to expose a path-like string, prefer it.
        foreach (string value in scalars.Strings)
        {
            try
            {
                if (!value.EndsWith(".save", StringComparison.OrdinalIgnoreCase)) continue;
                string candidate = System.IO.Path.IsPathRooted(value)
                    ? Normalize(value)
                    : Normalize(System.IO.Path.Combine(saveDir, value));
                if (File.Exists(candidate))
                {
                    _log($"SAVE FINGERPRINT direct path save={SafeFileName(candidate)}");
                    return candidate;
                }
            }
            catch { }
        }

        var headers = new List<SaveHeaderFingerprint>();
        try
        {
            foreach (string file in Directory.GetFiles(saveDir, "*.save", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(System.IO.Path.GetFileName(file), "temp.save", StringComparison.OrdinalIgnoreCase)) continue;
                if (TryReadSaveHeader(file, out SaveHeaderFingerprint? header) && header != null)
                    headers.Add(header);
            }
        }
        catch (Exception e)
        {
            _log($"SAVE FINGERPRINT scan error {e.GetType().Name}: {e.Message}");
            return null;
        }

        if (headers.Count == 0)
        {
            _log("SAVE FINGERPRINT unresolved: no readable .save headers found");
            return null;
        }

        var scored = headers
            .Select(h => (Header: h, Score: ScoreHeader(h, scalars)))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => File.GetLastWriteTimeUtc(x.Header.Path))
            .ToList();

        var best = scored[0];
        int second = scored.Count > 1 ? scored[1].Score : -1;
        if (best.Score >= 35 && (best.Score >= 100 || best.Score - second >= 5))
        {
            _log($"SAVE FINGERPRINT matched save={SafeFileName(best.Header.Path)} score={best.Score} second={second} candidates={headers.Count}");
            return Normalize(best.Header.Path);
        }

        _log($"SAVE FINGERPRINT unresolved best={SafeFileName(best.Header.Path)} score={best.Score} second={second} candidates={headers.Count}");
        DumpSaveStateScalarsOnce(saveState, scalars);
        return null;
    }

    private static int ScoreHeader(SaveHeaderFingerprint h, SaveStateScalars s)
    {
        int score = 0;

        if (h.TimestampTicks != 0 && (s.DateTicks.Contains(h.TimestampTicks) || s.Integers.Contains(h.TimestampTicks))) score += 140;
        if (!string.IsNullOrWhiteSpace(h.SaveGameName) && s.Strings.Contains(h.SaveGameName)) score += 70;

        string stem = System.IO.Path.GetFileNameWithoutExtension(h.Path);
        if (!string.IsNullOrWhiteSpace(stem) && s.Strings.Contains(stem)) score += 65;
        if (!string.IsNullOrWhiteSpace(System.IO.Path.GetFileName(h.Path)) && s.Strings.Contains(System.IO.Path.GetFileName(h.Path))) score += 70;

        if (!string.IsNullOrWhiteSpace(h.OperationName) && s.Strings.Contains(h.OperationName)) score += 25;
        if (!string.IsNullOrWhiteSpace(h.PlanetName) && s.Strings.Contains(h.PlanetName)) score += 15;
        if (!string.IsNullOrWhiteSpace(h.DifficultyId) && s.Strings.Contains(h.DifficultyId)) score += 15;
        if (!string.IsNullOrWhiteSpace(h.StrategyConfigId) && s.Strings.Contains(h.StrategyConfigId)) score += 15;

        if (h.PlayTimeSeconds > 0 && s.Reals.Any(v => Math.Abs(v - h.PlayTimeSeconds) < 0.02)) score += 45;
        if (s.Integers.Contains(h.CompletedMissions)) score += h.CompletedMissions == 0 ? 1 : 7;
        if (s.Integers.Contains(h.TotalMissions)) score += h.TotalMissions == 0 ? 1 : 7;
        if (s.Integers.Contains(h.SaveType)) score += 3;
        if (s.Integers.Contains(h.Version)) score += 2;
        return score;
    }

    private static bool TryReadSaveHeader(string path, out SaveHeaderFingerprint? header)
    {
        header = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

            int version = reader.ReadInt32();
            if (version < 1 || version > 10000) return false;
            int saveType = reader.ReadInt32();
            long ticks = reader.ReadInt64();
            string planet = reader.ReadString();
            string operation = reader.ReadString();
            int completed = reader.ReadInt32();
            int total = reader.ReadInt32();
            string difficulty = reader.ReadString();
            string strategyConfig = version > 27 ? reader.ReadString() : "";
            double playTime = reader.ReadDouble();
            string saveName = reader.ReadString();

            header = new SaveHeaderFingerprint
            {
                Path = Normalize(path),
                Version = version,
                SaveType = saveType,
                TimestampTicks = ticks,
                PlanetName = planet ?? "",
                OperationName = operation ?? "",
                CompletedMissions = completed,
                TotalMissions = total,
                DifficultyId = difficulty ?? "",
                StrategyConfigId = strategyConfig ?? "",
                PlayTimeSeconds = playTime,
                SaveGameName = saveName ?? ""
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SaveStateScalars ExtractSaveStateScalars(object saveState)
    {
        var result = new SaveStateScalars();
        Type type = saveState.GetType();

        foreach (PropertyInfo p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
            try
            {
                object? value = p.GetValue(saveState);
                AddScalar(result, p.Name, value);
            }
            catch { }
        }

        foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsStatic) continue;
            try
            {
                object? value = f.GetValue(saveState);
                AddScalar(result, f.Name, value);
            }
            catch { }
        }

        return result;
    }

    private static void AddScalar(SaveStateScalars result, string name, object? value)
    {
        if (value == null) return;
        try
        {
            switch (value)
            {
                case string text:
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Strings.Add(text);
                        if (result.DebugValues.Count < 48) result.DebugValues.Add($"{name}='{SanitizeDebug(text)}'");
                    }
                    return;
                case DateTime dt:
                    result.DateTicks.Add(dt.Ticks);
                    if (result.DebugValues.Count < 48) result.DebugValues.Add($"{name}=DateTime({dt.Ticks})");
                    return;
                case byte b: result.Integers.Add(b); break;
                case sbyte b: result.Integers.Add(b); break;
                case short v: result.Integers.Add(v); break;
                case ushort v: result.Integers.Add(v); break;
                case int v: result.Integers.Add(v); break;
                case uint v: result.Integers.Add(v); break;
                case long v: result.Integers.Add(v); break;
                case ulong v when v <= long.MaxValue: result.Integers.Add((long)v); break;
                case float v when !float.IsNaN(v) && !float.IsInfinity(v): result.Reals.Add(v); break;
                case double v when !double.IsNaN(v) && !double.IsInfinity(v): result.Reals.Add(v); break;
                case Enum e: result.Integers.Add(Convert.ToInt64(e)); break;
                default:
                    // IL2CPP DateTime proxies are not System.DateTime. If a scalar-looking
                    // object exposes a Ticks property, capture it without depending on its type.
                    Type t = value.GetType();
                    if (t.Name.Contains("DateTime", StringComparison.OrdinalIgnoreCase))
                    {
                        PropertyInfo? ticksProp = t.GetProperty("Ticks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (ticksProp?.GetValue(value) is object ticksObj)
                        {
                            long ticks = Convert.ToInt64(ticksObj);
                            result.DateTicks.Add(ticks);
                            if (result.DebugValues.Count < 48) result.DebugValues.Add($"{name}=DateTimeLike({ticks})");
                            return;
                        }
                    }
                    return;
            }

            if (result.DebugValues.Count < 48) result.DebugValues.Add($"{name}={value}");
        }
        catch { }
    }

    private void DumpSaveStateScalarsOnce(object saveState, SaveStateScalars scalars)
    {
        if (_dumpedSaveStateScalars) return;
        _dumpedSaveStateScalars = true;
        string values = scalars.DebugValues.Count == 0 ? "<none>" : string.Join("; ", scalars.DebugValues);
        _log($"SAVE STATE SCALARS type={saveState.GetType().FullName} values={values}");
    }

    private static string SanitizeDebug(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
        return value.Length <= 120 ? value : value[..120] + "...";
    }

    public void SyncRoster(IEnumerable<SquaddieIdentitySnapshot> roster)
    {
        bool changed = false;
        foreach (var s in roster)
        {
            if (s.Id <= 0) continue;
            if (!_records.TryGetValue(s.Id, out var r))
            {
                _records[s.Id] = new Record { Id = s.Id, Name = s.Name, Nickname = s.Nickname, Xp = 0 };
                changed = true;
                continue;
            }

            // Squaddie IDs are stable inside one campaign/save timeline. A different
            // generated name on the same ID is a strong sign that this is not the
            // same individual; never carry XP through that mismatch.
            if (!string.IsNullOrWhiteSpace(r.Name)
                && !string.IsNullOrWhiteSpace(s.Name)
                && !string.Equals(r.Name, s.Name, StringComparison.Ordinal))
            {
                _log($"XP identity changed for id={s.Id}: '{Sanitize(r.Name)}' -> '{Sanitize(s.Name)}'; resetting stale XP {r.Xp}->0");
                r.Xp = 0;
                changed = true;
            }

            if (!string.Equals(r.Name, s.Name, StringComparison.Ordinal)) { r.Name = s.Name; changed = true; }
            if (!string.Equals(r.Nickname, s.Nickname, StringComparison.Ordinal)) { r.Nickname = s.Nickname; changed = true; }
        }

        if (changed) _dirty = true;
    }

    /// <summary>
    /// Checks whether the currently visible MENACE roster looks like the roster
    /// represented by the just-loaded XP sidecar. During ExecLoad MENACE can leave
    /// the previous campaign roster alive for several frames/seconds; reconciling
    /// identities during that window would falsely wipe XP for every reused ID.
    /// </summary>
    public bool IsRosterCompatibleWithLoadedRecords(
        IEnumerable<SquaddieIdentitySnapshot> roster,
        out int matching,
        out int mismatching,
        out int comparable)
    {
        matching = 0;
        mismatching = 0;
        comparable = 0;

        foreach (var s in roster)
        {
            if (s.Id <= 0 || !_records.TryGetValue(s.Id, out var r)) continue;
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(s.Name)) continue;

            comparable++;
            if (string.Equals(r.Name, s.Name, StringComparison.Ordinal)) matching++;
            else mismatching++;
        }

        return comparable > 0 && mismatching == 0 && matching > 0;
    }

    public void SyncPilots(IEnumerable<PilotIdentitySnapshot> pilots)
    {
        bool changed = false;
        foreach (var p in pilots)
        {
            if (string.IsNullOrWhiteSpace(p.TemplateId)) continue;
            if (!_pilotRecords.TryGetValue(p.TemplateId, out var r))
            {
                r = new PilotRecord
                {
                    TemplateId = p.TemplateId,
                    TemplateGuid = p.TemplateGuid,
                    Name = p.Name,
                    LastVehicleGuid = p.VehicleGuid,
                    LastVehicleName = p.VehicleName,
                    Xp = 0,
                    CrewLayoutVersion = 4
                };
                _pilotRecords[p.TemplateId] = r;
                changed = true;
            }

            if (EnsureCrewLayout(r, p.ActiveCrewRoles, p.LegacyMountCrewRoles)) changed = true;
            if (r.TemplateGuid != p.TemplateGuid) { r.TemplateGuid = p.TemplateGuid; changed = true; }
            if (!string.Equals(r.Name, p.Name, StringComparison.Ordinal)) { r.Name = p.Name; changed = true; }
            if (!string.Equals(r.LastVehicleGuid, p.VehicleGuid, StringComparison.Ordinal)) { r.LastVehicleGuid = p.VehicleGuid; changed = true; }
            if (!string.Equals(r.LastVehicleName, p.VehicleName, StringComparison.Ordinal)) { r.LastVehicleName = p.VehicleName; changed = true; }
        }
        if (changed) _dirty = true;
    }

    public int GetPilotXp(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId) || !_pilotRecords.TryGetValue(templateId, out var r)) return 0;
        NormalizeCrewRecord(r);
        return CrewAverageXp(r);
    }

    public IReadOnlyList<CrewMemberRecord> GetPilotCrewMembers(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId) || !_pilotRecords.TryGetValue(templateId, out var r))
            return Array.Empty<CrewMemberRecord>();
        NormalizeCrewRecord(r);
        return r.Crew.OrderBy(x => x.Slot).Select(CloneCrewMember).ToList();
    }

    public IReadOnlyList<CrewMemberRecord> GetActivePilotCrewMembers(string templateId, bool includeReplacements = true)
    {
        if (string.IsNullOrWhiteSpace(templateId) || !_pilotRecords.TryGetValue(templateId, out var r))
            return Array.Empty<CrewMemberRecord>();
        NormalizeCrewRecord(r);
        var active = new HashSet<string>(r.ActiveCrewKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (active.Count == 0) return Array.Empty<CrewMemberRecord>();
        return r.ActiveCrewKeys
            .Select(key => r.Crew.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x != null && (includeReplacements || !string.Equals(x.Status, "Replacement", StringComparison.OrdinalIgnoreCase)))
            .Select(x => CloneCrewMember(x!))
            .ToList();
    }

    public int GetReservePilotCrewCount(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId) || !_pilotRecords.TryGetValue(templateId, out var r)) return 0;
        NormalizeCrewRecord(r);
        var active = new HashSet<string>(r.ActiveCrewKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return r.Crew.Count(x => !active.Contains(x.Key));
    }

    public int AddPilotXp(PilotIdentitySnapshot pilot, int amount)
    {
        if (string.IsNullOrWhiteSpace(pilot.TemplateId) || amount <= 0) return GetPilotXp(pilot.TemplateId);
        SyncPilots(new[] { pilot });
        var r = _pilotRecords[pilot.TemplateId];
        NormalizeCrewRecord(r);
        var active = new HashSet<string>(r.ActiveCrewKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var member in r.Crew)
        {
            if (!active.Contains(member.Key)) continue;
            // A replacement created by a death did not participate in the current
            // mission. It becomes active at the next mission start, not when
            // post-destruction objective/mission XP is awarded.
            if (string.Equals(member.Status, "Replacement", StringComparison.OrdinalIgnoreCase)) continue;
            member.Xp = Math.Max(0, member.Xp + amount);
        }
        r.TemplateGuid = pilot.TemplateGuid;
        r.Name = pilot.Name;
        r.LastVehicleGuid = pilot.VehicleGuid;
        r.LastVehicleName = pilot.VehicleName;
        r.Xp = 0;
        _dirty = true;
        return CrewAverageXp(r);
    }

    public void ActivateCrewReplacements(PilotIdentitySnapshot pilot)
    {
        if (string.IsNullOrWhiteSpace(pilot.TemplateId)) return;
        SyncPilots(new[] { pilot });
        var r = _pilotRecords[pilot.TemplateId];
        NormalizeCrewRecord(r);
        bool changed = false;
        foreach (var member in r.Crew)
        {
            if (!string.Equals(member.Status, "Replacement", StringComparison.OrdinalIgnoreCase)) continue;
            member.Status = "Healthy";
            changed = true;
        }
        if (changed) _dirty = true;
    }

    public CrewMutationResult WoundCrewMember(PilotIdentitySnapshot pilot, string crewKey, int xpLossPct)
    {
        SyncPilots(new[] { pilot });
        var r = _pilotRecords[pilot.TemplateId];
        NormalizeCrewRecord(r);
        var member = r.Crew.FirstOrDefault(x => string.Equals(x.Key, crewKey, StringComparison.OrdinalIgnoreCase));
        if (member == null) return new CrewMutationResult { Key = crewKey };
        int before = Math.Max(0, member.Xp);
        string statusBefore = member.Status ?? "Healthy";
        int pct = Math.Max(0, Math.Min(100, xpLossPct));
        int lost = (int)Math.Round(before * (pct / 100d), MidpointRounding.AwayFromZero);
        member.Xp = Math.Max(0, before - Math.Min(before, lost));
        member.Status = "Wounded";
        _dirty = true;
        return new CrewMutationResult
        {
            Slot = member.Slot, Key = member.Key, Role = member.Role,
            XpBefore = before, XpAfter = member.Xp,
            StatusBefore = statusBefore, StatusAfter = member.Status
        };
    }

    public CrewMutationResult KillAndReplaceCrewMember(PilotIdentitySnapshot pilot, string crewKey)
    {
        SyncPilots(new[] { pilot });
        var r = _pilotRecords[pilot.TemplateId];
        NormalizeCrewRecord(r);
        var member = r.Crew.FirstOrDefault(x => string.Equals(x.Key, crewKey, StringComparison.OrdinalIgnoreCase));
        if (member == null) return new CrewMutationResult { Key = crewKey };
        int before = Math.Max(0, member.Xp);
        string statusBefore = member.Status ?? "Healthy";
        member.Xp = 0;
        member.Status = "Replacement";
        _dirty = true;
        return new CrewMutationResult
        {
            Slot = member.Slot, Key = member.Key, Role = member.Role,
            XpBefore = before, XpAfter = 0,
            StatusBefore = statusBefore, StatusAfter = member.Status
        };
    }

    public int GetXp(int id) => id > 0 && _records.TryGetValue(id, out var r) ? Math.Max(0, r.Xp) : 0;

    public int AddXp(SquaddieIdentitySnapshot identity, int amount)
    {
        if (identity.Id <= 0 || amount <= 0) return GetXp(identity.Id);
        SyncRoster(new[] { identity });
        var r = _records[identity.Id];
        r.Xp = Math.Max(0, r.Xp + amount);
        r.Name = identity.Name;
        r.Nickname = identity.Nickname;
        _dirty = true;
        return r.Xp;
    }

    public int LoseXpPercentage(SquaddieIdentitySnapshot identity, int percentage, out int lost)
    {
        lost = 0;
        if (identity.Id <= 0) return 0;

        int pct = Math.Max(0, Math.Min(100, percentage));
        SyncRoster(new[] { identity });
        var r = _records[identity.Id];
        int before = Math.Max(0, r.Xp);
        if (before <= 0 || pct <= 0) return before;

        // XP is integer-valued. Round to the nearest XP so the configured value
        // behaves as a true percentage rather than always rounding upward.
        lost = Math.Max(0, Math.Min(before,
            (int)Math.Round(before * (pct / 100.0), MidpointRounding.AwayFromZero)));
        if (lost <= 0) return before;

        r.Xp = before - lost;
        r.Name = identity.Name;
        r.Nickname = identity.Nickname;
        _dirty = true;
        return r.Xp;
    }

    /// <summary>
    /// Called from the Squaddies.ProcessSaveState load hook before MENACE replaces
    /// the campaign roster. This is the rollback point for mod-owned veterancy.
    /// </summary>
    public void LoadForGameSave(string? savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
        {
            _log("XP LOAD REQUEST ignored: SaveState.FilePath was empty");
            return;
        }

        savePath = Normalize(savePath);
        _log($"XP LOAD REQUEST save={SafeFileName(savePath)}");
        EnsureWatcherForDirectory(System.IO.Path.GetDirectoryName(savePath));

        string sidecar = SidecarPath(savePath);
        if (!File.Exists(sidecar))
            TryMigrateAdjacentSidecar(savePath, sidecar);
        if (File.Exists(sidecar))
        {
            LoadSidecar(sidecar, savePath);
            return;
        }

        // One-time transition from alpha.8. We deliberately migrate the old global
        // file to only the first save that is loaded without a sidecar. Importing it
        // into every old save would recreate the exact reload/future-XP bug that this
        // version is designed to eliminate.
        if (_legacyAvailable && File.Exists(_legacyPath))
        {
            if (TryMigrateLegacy(savePath)) return;
        }

        _records.Clear();
        _pilotRecords.Clear();
        _currentSavePath = savePath;
        _dirty = false;
        _log($"XP SNAPSHOT MISSING save={SafeFileName(savePath)} expectedSidecar={sidecar} -> fresh veterancy state");
    }

    public void ResetAll()
    {
        foreach (var r in _records.Values) r.Xp = 0;
        foreach (var r in _pilotRecords.Values)
        {
            NormalizeCrewRecord(r);
            r.Xp = 0;
            foreach (var member in r.Crew)
            {
                member.Xp = 0;
                member.Status = "Healthy";
            }
        }
        _dirty = true;
        _log("XP RESET current timeline set to zero; it will become persistent on the next MENACE save");
    }

    /// <summary>
    /// Debug-only manual snapshot. Normal gameplay persistence should come from the
    /// save watcher so the sidecar timestamp corresponds to a real MENACE save.
    /// </summary>
    public bool SaveCurrentSnapshotNow()
    {
        if (string.IsNullOrWhiteSpace(_currentSavePath))
        {
            _log("XP SNAPSHOT manual write skipped: no current MENACE save is known yet");
            return false;
        }

        WriteSnapshot(_currentSavePath, "manual-debug");
        return true;
    }

    private bool TryMigrateLegacy(string savePath)
    {
        try
        {
            string json = File.ReadAllText(_legacyPath);
            var file = JsonSerializer.Deserialize<LegacyPersistedFile>(json);
            _records.Clear();
            _pilotRecords.Clear();
            if (file?.Squaddies != null)
            {
                foreach (var r in file.Squaddies)
                {
                    if (r.Id <= 0) continue;
                    r.Xp = Math.Max(0, r.Xp);
                    _records[r.Id] = r;
                }
            }

            _currentSavePath = savePath;
            _dirty = true;
            WriteSnapshot(savePath, "alpha8-migration");

            Directory.CreateDirectory(_root);
            File.Copy(_legacyPath, _legacyBackupPath, true);
            File.Delete(_legacyPath);
            _legacyAvailable = false;
            _log($"XP LEGACY MIGRATED records={_records.Count} -> save={SafeFileName(savePath)}; backup={_legacyBackupPath}");
            return true;
        }
        catch (Exception e)
        {
            _log($"XP LEGACY MIGRATION ERROR {e.GetType().Name}: {e.Message}; old file left untouched");
            _records.Clear();
            _pilotRecords.Clear();
            _currentSavePath = savePath;
            _dirty = false;
            return false;
        }
    }

    private void LoadSidecar(string sidecar, string savePath)
    {
        try
        {
            string json = File.ReadAllText(sidecar);
            var file = JsonSerializer.Deserialize<PersistedFile>(json);
            _records.Clear();
            _pilotRecords.Clear();
            if (file?.Squaddies != null)
            {
                foreach (var r in file.Squaddies)
                {
                    if (r.Id <= 0) continue;
                    r.Xp = Math.Max(0, r.Xp);
                    _records[r.Id] = r;
                }
            }
            if (file?.Pilots != null)
            {
                foreach (var r in file.Pilots)
                {
                    if (string.IsNullOrWhiteSpace(r.TemplateId)) continue;
                    r.Xp = Math.Max(0, r.Xp);
                    NormalizeCrewRecord(r);
                    _pilotRecords[r.TemplateId] = r;
                }
            }
            _currentSavePath = savePath;
            _dirty = false;
            _log($"XP SNAPSHOT LOADED save={SafeFileName(savePath)} squaddies={_records.Count} pilots={_pilotRecords.Count} sidecar={sidecar}");
        }
        catch (Exception e)
        {
            _records.Clear();
            _pilotRecords.Clear();
            _currentSavePath = savePath;
            _dirty = false;
            _log($"XP SNAPSHOT LOAD ERROR save={SafeFileName(savePath)} {e.GetType().Name}: {e.Message}; starting fresh for this save");
        }
    }

    private void WriteSnapshot(string savePath, string reason)
    {
        savePath = Normalize(savePath);
        if (string.Equals(System.IO.Path.GetFileName(savePath), "temp.save", StringComparison.OrdinalIgnoreCase))
            return;

        string sidecar = SidecarPath(savePath);
        Directory.CreateDirectory(_snapshotRoot);
        var file = new PersistedFile
        {
            Version = 7,
            SaveFile = System.IO.Path.GetFileName(savePath),
            TimestampUtc = DateTime.UtcNow.ToString("o"),
            Squaddies = _records.Values.OrderBy(x => x.Id).Select(Clone).ToList(),
            Pilots = _pilotRecords.Values.OrderBy(x => x.TemplateId, StringComparer.OrdinalIgnoreCase).Select(ClonePilot).ToList()
        };

        string json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        string temp = sidecar + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, sidecar, true);
        _currentSavePath = savePath;
        _dirty = false;
        RememberSaveWriteTime(savePath);
        _log($"XP SNAPSHOT WRITTEN save={SafeFileName(savePath)} squaddies={_records.Count} pilots={_pilotRecords.Count} reason={reason} sidecar={sidecar}");
    }

    private static Record Clone(Record r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Nickname = r.Nickname,
        Xp = Math.Max(0, r.Xp)
    };

    private static PilotRecord ClonePilot(PilotRecord r)
    {
        NormalizeCrewRecord(r);
        return new PilotRecord
        {
            TemplateId = r.TemplateId,
            TemplateGuid = r.TemplateGuid,
            Name = r.Name,
            LastVehicleGuid = r.LastVehicleGuid,
            LastVehicleName = r.LastVehicleName,
            Xp = Math.Max(0, r.Xp),
            Crew = r.Crew.OrderBy(x => x.Slot).Select(CloneCrewMember).ToList(),
            ActiveCrewKeys = (r.ActiveCrewKeys ?? new List<string>()).ToList(),
            CrewLayoutVersion = r.CrewLayoutVersion
        };
    }

    private static CrewMemberRecord CloneCrewMember(CrewMemberRecord m) => new()
    {
        Slot = m.Slot,
        Key = m.Key ?? "",
        AssignedRoleKey = m.AssignedRoleKey ?? "",
        Role = string.IsNullOrWhiteSpace(m.Role) ? "Crewman" : m.Role,
        Xp = Math.Max(0, m.Xp),
        Status = string.IsNullOrWhiteSpace(m.Status) ? "Healthy" : m.Status
    };

    private static bool NormalizeCrewRecord(PilotRecord r)
    {
        bool changed = false;
        r.Crew ??= new List<CrewMemberRecord>();
        r.ActiveCrewKeys ??= new List<string>();
        var seenSlots = new HashSet<int>();
        int nextSlot = 1;
        foreach (var member in r.Crew.Where(x => x != null).OrderBy(x => x.Slot))
        {
            if (member.Slot <= 0 || !seenSlots.Add(member.Slot))
            {
                while (seenSlots.Contains(nextSlot)) nextSlot++;
                member.Slot = nextSlot;
                seenSlots.Add(nextSlot);
                changed = true;
            }
            nextSlot = Math.Max(nextSlot, member.Slot + 1);
            if (member.Xp < 0) { member.Xp = 0; changed = true; }
            if (member.Key == null) { member.Key = ""; changed = true; }
            if (member.AssignedRoleKey == null) { member.AssignedRoleKey = ""; changed = true; }
            if (string.IsNullOrWhiteSpace(member.Role)) { member.Role = "Crewman"; changed = true; }
            if (string.IsNullOrWhiteSpace(member.Status)) { member.Status = "Healthy"; changed = true; }
        }
        return changed;
    }

    private bool EnsureCrewLayout(PilotRecord r, IReadOnlyList<CrewRoleSpec> activeRoles, IReadOnlyList<CrewRoleSpec> legacyMountRoles)
    {
        bool changed = NormalizeCrewRecord(r);
        activeRoles ??= Array.Empty<CrewRoleSpec>();
        var roles = activeRoles
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        // 1.5.7 model: XP belongs to persistent crew PEOPLE, not to weapon roles.
        // Role is only the crew member's current assignment for the active loadout.
        // A loadout change therefore reassigns the same people first, then pulls
        // experienced personnel from reserve, and creates a Green replacement only
        // if the Pilot's persistent personnel pool is still too small.
        //
        // Migrate every older role-keyed record into stable personnel IDs without
        // discarding XP/status. This intentionally supersedes the 1.5.6 role cleanup:
        // Heavy -> Light+Medium with the same crew size keeps the same four people.
        if (r.CrewLayoutVersion < 4)
        {
            var oldActiveMembers = (r.ActiveCrewKeys ?? new List<string>())
                .Select(key => r.Crew.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x != null)
                .Cast<CrewMemberRecord>()
                .Distinct()
                .ToList();

            // Older fixed-slot files can have blank keys. Preserve every historical
            // member in the personnel pool rather than throwing away progression.
            foreach (var member in r.Crew.OrderBy(x => x.Slot))
            {
                string oldKey = member.Key ?? "";
                if (string.IsNullOrWhiteSpace(member.AssignedRoleKey))
                    member.AssignedRoleKey = oldKey;
                string personKey = $"crew:{member.Slot}";
                if (!string.Equals(member.Key, personKey, StringComparison.OrdinalIgnoreCase))
                {
                    member.Key = personKey;
                    changed = true;
                }
            }

            var migratedActive = oldActiveMembers
                .Select(x => x.Key)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (migratedActive.Count == 0 && r.Crew.Count > 0)
            {
                int desired = roles.Count > 0 ? Math.Min(roles.Count, r.Crew.Count) : 0;
                migratedActive = r.Crew.OrderBy(x => x.Slot).Take(desired).Select(x => x.Key).ToList();
            }
            r.ActiveCrewKeys = migratedActive;
            r.CrewLayoutVersion = 4;
            // Preserve legacy Pilot-level XP until the no-crew seeding block below
            // has had a chance to distribute it. Once real crew exists, Pilot XP is
            // obsolete and can be cleared.
            if (r.Crew.Count > 0) r.Xp = 0;
            changed = true;
            _log($"VEHICLE CREW V8 MIGRATION pilot={r.Name} template={r.TemplateId} personnel={r.Crew.Count} active={r.ActiveCrewKeys.Count} progression=preserved");
        }

        // Old schema v3 could store Pilot-level XP before individual crew existed.
        // If there is no personnel pool yet, seed only the required active people.
        if (r.Crew.Count == 0 && roles.Count > 0)
        {
            int migratedXp = Math.Max(0, r.Xp);
            for (int i = 0; i < roles.Count; i++)
            {
                r.Crew.Add(new CrewMemberRecord
                {
                    Slot = i + 1,
                    Key = $"crew:{i + 1}",
                    AssignedRoleKey = roles[i].Key,
                    Role = roles[i].Label,
                    Xp = migratedXp,
                    Status = "Healthy"
                });
            }
            r.ActiveCrewKeys = r.Crew.OrderBy(x => x.Slot).Select(x => x.Key).ToList();
            r.Xp = 0;
            r.CrewLayoutVersion = 4;
            changed = true;
            return changed;
        }

        if (roles.Count == 0)
        {
            if (r.ActiveCrewKeys.Count > 0)
            {
                r.ActiveCrewKeys.Clear();
                changed = true;
            }
            if (r.Xp != 0) { r.Xp = 0; changed = true; }
            return changed;
        }

        // Resolve the currently active PEOPLE in their existing order. Keeping this
        // order means Driver/Crewman stays with the Pilot when possible, while the
        // other personnel can be reassigned to different weapon jobs without XP loss.
        var activeMembers = r.ActiveCrewKeys
            .Select(key => r.Crew.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x != null)
            .Cast<CrewMemberRecord>()
            .Distinct()
            .ToList();

        int required = roles.Count;
        var selected = activeMembers.Take(required).ToList();

        // If more people are required, reuse the Pilot's reserve personnel before
        // recruiting anyone new. Stable slot order makes this deterministic.
        if (selected.Count < required)
        {
            var selectedSet = new HashSet<CrewMemberRecord>(selected);
            foreach (var reserve in r.Crew.OrderBy(x => x.Slot))
            {
                if (selectedSet.Contains(reserve)) continue;
                selected.Add(reserve);
                selectedSet.Add(reserve);
                if (selected.Count >= required) break;
            }
        }

        // Still short: create genuinely new Green crew members.
        while (selected.Count < required)
        {
            int nextSlot = r.Crew.Count == 0 ? 1 : r.Crew.Max(x => x.Slot) + 1;
            var member = new CrewMemberRecord
            {
                Slot = nextSlot,
                Key = $"crew:{nextSlot}",
                AssignedRoleKey = "",
                Role = "Crewman",
                Xp = 0,
                Status = "Healthy"
            };
            r.Crew.Add(member);
            selected.Add(member);
            changed = true;
        }

        // Assign jobs to the selected people. Roles are NOT identities.
        for (int i = 0; i < required; i++)
        {
            var member = selected[i];
            var role = roles[i];
            if (!string.Equals(member.AssignedRoleKey, role.Key, StringComparison.OrdinalIgnoreCase))
            {
                member.AssignedRoleKey = role.Key;
                changed = true;
            }
            if (!string.Equals(member.Role, role.Label, StringComparison.Ordinal))
            {
                member.Role = role.Label;
                changed = true;
            }
        }

        // Reserve members remain in the pool with their XP/status untouched. Clear
        // only their current assignment so the UI/log does not imply they are still
        // manning an old weapon that is no longer fitted.
        var selectedKeys = new HashSet<string>(selected.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
        foreach (var member in r.Crew)
        {
            if (selectedKeys.Contains(member.Key)) continue;
            if (!string.IsNullOrWhiteSpace(member.AssignedRoleKey))
            {
                member.AssignedRoleKey = "";
                changed = true;
            }
            if (!string.Equals(member.Role, "Reserve Crew", StringComparison.Ordinal))
            {
                member.Role = "Reserve Crew";
                changed = true;
            }
        }

        var newActive = selected.Select(x => x.Key).ToList();
        if (!(r.ActiveCrewKeys ?? new List<string>()).SequenceEqual(newActive, StringComparer.OrdinalIgnoreCase))
        {
            r.ActiveCrewKeys = newActive;
            changed = true;
        }

        if (r.Xp != 0) { r.Xp = 0; changed = true; }
        if (r.CrewLayoutVersion != 4) { r.CrewLayoutVersion = 4; changed = true; }
        return changed;
    }

    private static int CrewAverageXp(PilotRecord r)
    {
        NormalizeCrewRecord(r);
        if (r.ActiveCrewKeys == null || r.ActiveCrewKeys.Count == 0) return 0;
        var active = new HashSet<string>(r.ActiveCrewKeys, StringComparer.OrdinalIgnoreCase);
        var members = r.Crew.Where(x => active.Contains(x.Key)).ToList();
        if (members.Count == 0) return 0;
        return (int)Math.Round(members.Average(x => Math.Max(0, x.Xp)), MidpointRounding.AwayFromZero);
    }

    private void TryInitializeWatcher()
    {
        if (_watcher != null) return;
        long now = Environment.TickCount64;
        if (now < _nextWatcherAttemptMs) return;
        _nextWatcherAttemptMs = now + 2000;

        try
        {
            string candidate = System.IO.Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(candidate))
            {
                EnsureWatcherForDirectory(candidate);
                return;
            }

            // Fallback for installs where saves are redirected under the game folder.
            string gameCandidate = System.IO.Path.Combine(Environment.CurrentDirectory, "UserData", "Saves");
            if (Directory.Exists(gameCandidate)) EnsureWatcherForDirectory(gameCandidate);
        }
        catch (Exception e)
        {
            if (_nextWatcherAttemptMs - now <= 2100)
                _log($"SAVE WATCH probe failed {e.GetType().Name}: {e.Message}");
        }
    }

    private void EnsureWatcherForDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        directory = Normalize(directory);
        if (_watcher != null && string.Equals(_watchedDirectory, directory, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            _watcher?.Dispose();
            var watcher = new FileSystemWatcher(directory)
            {
                Filter = "*.save",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };
            watcher.Created += OnGameSaveChanged;
            watcher.Changed += OnGameSaveChanged;
            watcher.Renamed += OnGameSaveRenamed;
            SnapshotExistingSaveWriteTimes(directory);
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
            _watchedDirectory = directory;
            _log($"SAVE WATCH ready path={directory} baselineSaves={_knownSaveWriteTicks.Count}");
            MigrateAdjacentSidecars(directory);
        }
        catch (Exception e)
        {
            _log($"SAVE WATCH ERROR path={directory} {e.GetType().Name}: {e.Message}");
        }
    }

    private void OnGameSaveChanged(object sender, FileSystemEventArgs e) => QueueGameSave(e.FullPath);
    private void OnGameSaveRenamed(object sender, RenamedEventArgs e) => QueueGameSave(e.FullPath);

    private void QueueGameSave(string path)
    {
        try
        {
            if (!path.EndsWith(".save", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(System.IO.Path.GetFileName(path), "temp.save", StringComparison.OrdinalIgnoreCase)) return;
            string normalized = Normalize(path);
            lock (_watchLock)
            {
                // Debounce the game's copy/write sequence. The callback itself does
                // not touch the XP dictionary because FileSystemWatcher is off-thread.
                _pendingSaveWrites[normalized] = Environment.TickCount64 + 650;
            }
        }
        catch { }
    }

    private string SidecarPath(string savePath)
        => System.IO.Path.Combine(_snapshotRoot, SafeFileName(savePath) + ".squaddieveterancy.json");

    private static string AdjacentSidecarPath(string savePath)
        => savePath + ".squaddieveterancy.json";

    private void TryMigrateAdjacentSidecar(string savePath, string destination)
    {
        try
        {
            string source = AdjacentSidecarPath(savePath);
            if (!File.Exists(source)) return;
            Directory.CreateDirectory(_snapshotRoot);
            File.Copy(source, destination, true);
            File.Delete(source);
            _log($"XP SIDECAR MOVED save={SafeFileName(savePath)} -> {destination}");
        }
        catch (Exception e)
        {
            _log($"XP SIDECAR MOVE ERROR save={SafeFileName(savePath)} {e.GetType().Name}: {e.Message}");
        }
    }

    private void MigrateAdjacentSidecars(string saveDirectory)
    {
        try
        {
            if (!Directory.Exists(saveDirectory)) return;
            Directory.CreateDirectory(_snapshotRoot);
            foreach (string source in Directory.GetFiles(saveDirectory, "*.save.squaddieveterancy.json", SearchOption.TopDirectoryOnly))
            {
                string destination = System.IO.Path.Combine(_snapshotRoot, System.IO.Path.GetFileName(source));
                try
                {
                    bool replace = !File.Exists(destination)
                        || File.GetLastWriteTimeUtc(source) >= File.GetLastWriteTimeUtc(destination);
                    if (replace) File.Copy(source, destination, true);
                    File.Delete(source);
                    _log($"XP SIDECAR MOVED legacy={System.IO.Path.GetFileName(source)} -> {destination}");
                }
                catch (Exception e)
                {
                    _log($"XP SIDECAR MOVE ERROR file={System.IO.Path.GetFileName(source)} {e.GetType().Name}: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            _log($"XP SIDECAR SCAN ERROR {e.GetType().Name}: {e.Message}");
        }
    }
    private static string Normalize(string path) { try { return System.IO.Path.GetFullPath(path); } catch { return path; } }
    private static string SafeFileName(string path) { try { return System.IO.Path.GetFileName(path); } catch { return path; } }
    private static string Sanitize(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");

    private void SnapshotExistingSaveWriteTimes(string directory)
    {
        try
        {
            foreach (string path in Directory.GetFiles(directory, "*.save", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(System.IO.Path.GetFileName(path), "temp.save", StringComparison.OrdinalIgnoreCase)) continue;
                RememberSaveWriteTime(path);
            }
        }
        catch (Exception e)
        {
            _log($"SAVE WATCH baseline scan failed {e.GetType().Name}: {e.Message}");
        }
    }

    private void RememberSaveWriteTime(string path)
    {
        try
        {
            path = Normalize(path);
            if (!File.Exists(path)) return;
            _knownSaveWriteTicks[path] = File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch { }
    }

    /// <summary>
    /// Flush only snapshots backed by a real MENACE save write. We intentionally do
    /// NOT write merely-dirty XP to the currently loaded save during shutdown: doing
    /// that would allow XP earned after the last game save to leak backwards into an
    /// older timeline. Pending watcher events and save files whose LastWriteTime
    /// changed since our baseline are safe because MENACE actually wrote them.
    /// </summary>
    private void FlushPendingSnapshotsOnShutdown()
    {
        var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        lock (_watchLock)
        {
            foreach (string path in _pendingSaveWrites.Keys)
                candidates[path] = "shutdown-pending";
            _pendingSaveWrites.Clear();
        }

        int changedOnDisk = 0;
        string? directory = _watchedDirectory;
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            try
            {
                foreach (string rawPath in Directory.GetFiles(directory, "*.save", SearchOption.TopDirectoryOnly))
                {
                    if (string.Equals(System.IO.Path.GetFileName(rawPath), "temp.save", StringComparison.OrdinalIgnoreCase)) continue;
                    string path = Normalize(rawPath);
                    long ticks;
                    try { ticks = File.GetLastWriteTimeUtc(path).Ticks; }
                    catch { continue; }

                    if (!_knownSaveWriteTicks.TryGetValue(path, out long knownTicks) || ticks != knownTicks)
                    {
                        changedOnDisk++;
                        if (!candidates.ContainsKey(path)) candidates[path] = "shutdown-disk-change";
                    }
                }
            }
            catch (Exception e)
            {
                _log($"XP SHUTDOWN SCAN ERROR {e.GetType().Name}: {e.Message}");
            }
        }

        _log($"XP SHUTDOWN FLUSH candidates={candidates.Count} changedOnDisk={changedOnDisk} records={_records.Count} dirty={_dirty} current={CurrentSaveLabel}");

        int written = 0;
        foreach (var kv in candidates)
        {
            try
            {
                if (!File.Exists(kv.Key))
                {
                    _log($"XP SHUTDOWN FLUSH skipped missing save={SafeFileName(kv.Key)} reason={kv.Value}");
                    continue;
                }
                WriteSnapshot(kv.Key, kv.Value);
                written++;
            }
            catch (Exception e)
            {
                _log($"XP SHUTDOWN FLUSH ERROR save={SafeFileName(kv.Key)} reason={kv.Value} {e.GetType().Name}: {e.Message}");
            }
        }

        if (written == 0 && _dirty)
            _log("XP SHUTDOWN NOTE: unsaved XP remains dirty, but no MENACE save write was detected; not attaching future XP to an older save.");
        else
            _log($"XP SHUTDOWN FLUSH complete written={written}");
    }

    public void Dispose()
    {
        // Stop new watcher callbacks first, then synchronously commit any game-save
        // events that were still inside the debounce window.
        try { if (_watcher != null) _watcher.EnableRaisingEvents = false; } catch { }
        try { FlushPendingSnapshotsOnShutdown(); }
        catch (Exception e) { _log($"XP SHUTDOWN FLUSH FATAL {e.GetType().Name}: {e.Message}"); }
        try { _watcher?.Dispose(); } catch { }
        _watcher = null;
    }
}
