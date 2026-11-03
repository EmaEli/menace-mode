using System.Diagnostics;
using System.Security.Cryptography;
using HarmonyLib;
using Il2CppMenace.Tactical;
using Jiangyu.Sdk;
using Path = System.IO.Path;

namespace TacticalRoundCheckpoint;

public sealed class RoundCaptureSystem : JiangyuSystem
{
    private const string NativeHash = "871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111";
    private static RoundCaptureSystem? _owner;
    private Harmony? _harmony;
    private CheckpointStore? _store;
    private string _sessionDirectory = "";
    private string _battleId = "";
    private IntPtr _manager;
    private int _mainThread;
    private bool _disabled;

    public override void OnInit()
    {
        _owner = this;
        _mainThread = Environment.CurrentManagedThreadId;
        try
        {
            string game = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
            using var binary = File.OpenRead(Path.Combine(game, "GameAssembly.dll"));
            using var sha256 = SHA256.Create();
            string hash = Convert.ToHexString(sha256.ComputeHash(binary)).ToLowerInvariant();
            if (hash != NativeHash) throw new InvalidOperationException("Unsupported native game build.");
            _sessionDirectory = Path.Combine(game, "_MENACE_Diagnostics", "TacticalRoundCheckpoint",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            var method = AccessTools.Method(typeof(TacticalManager), "NextRound", Type.EmptyTypes)
                ?? throw new MissingMethodException("TacticalManager.NextRound()");
            if (method.ReturnType != typeof(void)) throw new MissingMethodException("NextRound return type");
            _harmony = new Harmony("luca.tacticalroundcheckpoint");
            _harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(RoundCaptureSystem), nameof(BeforeRound)),
                postfix: new HarmonyMethod(typeof(RoundCaptureSystem), nameof(AfterRound)) { priority = Priority.Last });
            Context.Log.Warn("CAPTURE ONLY, NOT AUTOSAVE: no battle loading. Round diagnostics: " + _sessionDirectory);
        }
        catch (Exception e) { Disable(e); }
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Never retain native wrappers or conflate separate missions, even if pointers are reused.
        _store = null;
        _manager = IntPtr.Zero;
    }

    public override void OnUnload()
    {
        _owner = null;
        _store = null;
        _harmony?.UnpatchSelf();
    }

    public static void BeforeRound(TacticalManager __instance, out int __state)
    {
        __state = -1;
        var owner = _owner;
        if (owner == null || owner._disabled) return;
        try
        {
            owner.RequireMainThread();
            __state = __instance.GetRound();
        }
        catch (Exception e) { owner.Disable(e); }
    }

    public static void AfterRound(TacticalManager __instance, int __state)
    {
        var owner = _owner;
        if (owner == null || owner._disabled || __state < 0) return;
        try
        {
            owner.RequireMainThread();
            int round = __instance.GetRound();
            if (!RoundBoundary.Advanced(__state, round)) return;
            owner.Capture(__instance, round, __state);
        }
        catch (Exception e) { owner.Disable(e); }
    }

    private void Capture(TacticalManager manager, int round, int previousRound)
    {
        if (_store == null || _manager != manager.Pointer || previousRound == 0)
        {
            _battleId = Guid.NewGuid().ToString("N");
            _manager = manager.Pointer;
            _store = new CheckpointStore(Path.Combine(_sessionDirectory, _battleId));
        }
        var mission = manager.GetMission() ?? throw new InvalidOperationException("No mission at round boundary.");
        var actors = new List<ActorObservation>();
        var seen = new HashSet<IntPtr>();
        var factions = manager.GetFactions() ?? throw new InvalidOperationException("No factions.");
        foreach (var faction in factions)
        {
            if (faction == null) continue;
            var roster = faction.GetActors();
            for (int i = 0; i < roster.Count; i++)
            {
                var actor = roster[i];
                if (actor == null) throw new InvalidOperationException("Null roster entry.");
                if (!seen.Add(actor.Pointer)) continue;
                // Do not wait and take a misleading mid-round snapshot. No mutation/pause/retry.
                if (actor.IsMoving() || actor.GetSkills().IsBusy())
                    throw new InvalidOperationException("Round boundary still has moving/busy actors.");
                var tile = actor.GetTile();
                actors.Add(new ActorObservation(actor.GetFactionID(), i, actor.GetTemplate().GetID(),
                    tile == null ? null : tile.GetX(), tile == null ? null : tile.GetZ(),
                    actor.GetHitpoints(), actor.GetActionPoints(), actor.GetMorale(), actor.GetSuppression(),
                    actor.IsTurnDone(), actor.IsContainedWithinAnotherEntity()));
            }
        }
        if (actors.Count == 0) throw new InvalidOperationException("Empty round roster.");
        _store.Write(new RoundObservation(_battleId, round, mission.GetTemplate().GetID(),
            mission.GetSeed(), NativeHash, actors.ToArray()));
    }

    private void RequireMainThread()
    {
        if (Environment.CurrentManagedThreadId != _mainThread)
            throw new InvalidOperationException("Round hook was not on the initialization thread.");
    }

    private void Disable(Exception e)
    {
        if (_disabled) return;
        _disabled = true;
        Context.Log.Warn("Capture-only round probe disabled; no game save was made: " + e.Message);
    }
}