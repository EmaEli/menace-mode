using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LargerMaps;

/// <summary>
/// Version-locked, fail-closed native pathfinding adaptation for the MENACE builds listed in
/// <see cref="SupportedBuilds"/> (v0.7.15+21883, v0.7.16+22323 and v0.7.17+22362).
///
/// Only instruction IMMEDIATES are modified, in memory, before any tactical map is loaded:
/// - MissionTemplate.GetBounds's fixed 42x42 clipping rectangle becomes active NxN
/// - 128x128 reference array (striding remains a native multidimensional array operation)
/// - 16384-byte closed bitmap (native alloc/free pair is NOT altered)
/// - 16384-entry native FastPriorityQueue capacity
/// - selectable 72/80/88/96 active-node initialization limits (all fit the original imm8 encoding)
/// - 12 x*64+z bitmap indexing expressions updated to x*128+z
/// - both 4096-byte bitmap memset lengths updated to 16384
///
/// Never patch game files on disk. All sites must match the exact original assembly.
/// Unknown game builds or earlier patch/detour attempts force a 64x64 fallback.
/// This is EXPERIMENTAL and still requires in-game A*/AI validation.
///
/// Each patched instruction is addressed as (native method, byte offset inside that method).
/// A game update moves the methods but, so far, has not changed their bodies, so adding a build
/// means adding one <see cref="GameBuild"/> row with the method RVAs from its IL2CPP dump after
/// checking that every expected instruction is still at the same offset (see
/// tests/test_extended_pathfinding.py with MENACE_GAMEASSEMBLY set).
/// </summary>
internal static class ExtendedPathfinding
{
    internal const int InternalStride = 128;
    private const uint PageExecuteReadWrite = 0x40;

    private enum NativeMethod
    {
        MissionTemplateGetBounds,
        PathfindingProcessCtor,
        PathfindingProcessInit,
        PathfindingManagerInit,
        PathfindingProcessFindPath,
        PathfindingProcessProcessNode,
        PathfindingProcessClear,
    }

    /// <summary>One supported GameAssembly.dll: its on-disk SHA-256 and the RVA of each patched method.</summary>
    private sealed class GameBuild
    {
        internal readonly string Version;
        internal readonly string Sha256;
        private readonly int[] _methodRvas;

        internal GameBuild(string version, string sha256,
            int missionTemplateGetBounds, int pathfindingProcessCtor, int pathfindingProcessInit,
            int pathfindingManagerInit, int pathfindingProcessFindPath, int pathfindingProcessProcessNode,
            int pathfindingProcessClear)
        {
            Version = version;
            Sha256 = sha256;
            _methodRvas = new[]
            {
                missionTemplateGetBounds, pathfindingProcessCtor, pathfindingProcessInit,
                pathfindingManagerInit, pathfindingProcessFindPath, pathfindingProcessProcessNode,
                pathfindingProcessClear,
            };
        }

        internal int Rva(NativeMethod method) => _methodRvas[(int)method];
    }

    private sealed class PatchSite
    {
        internal readonly NativeMethod Method;
        internal readonly int Delta;
        internal readonly int Offset;
        internal readonly byte[] Original;
        internal readonly byte Value80;
        internal readonly byte Value96;
        internal readonly string Purpose;

        internal PatchSite(NativeMethod method, int delta, int offset, string originalHex, byte value80, byte value96, string purpose)
        {
            Method = method;
            Delta = delta;
            Offset = offset;
            Original = Convert.FromHexString(originalHex);
            Value80 = value80;
            Value96 = value96;
            Purpose = purpose;
            if (Delta < 0 || Offset < 0 || Offset >= Original.Length)
                throw new ArgumentException("Invalid native byte patch descriptor for " + purpose);
        }

        internal int Rva(GameBuild build) => build.Rva(Method) + Delta;

        internal byte ForMap(int size) => (Purpose.EndsWith("-loop", StringComparison.Ordinal) ||
            Purpose.StartsWith("mission-bound-", StringComparison.Ordinal))
            ? checked((byte)size)
            : Value80;
    }

    // Method RVAs come from the IL2CPP dump of each build (offsets from the loaded
    // GameAssembly.dll module base, not file offsets). Order follows NativeMethod.
    private static readonly GameBuild[] SupportedBuilds =
    {
        new("v0.7.15+21883", "877622dd02ed79f6a3a37580b5cd9be12e29012bbd6f15a9c80cf917af1373db",
            0x5F61C0, 0x69EED0, 0x69E210, 0x69BE30, 0x69C6B0, 0x69E840, 0x69C300),
        new("v0.7.16+22323", "bd2266957504db63dbf6fbef63e2e0f53085e9f0cc17e2d2dd3352d04e960b32",
            0x60F630, 0x6B1B00, 0x6B0E40, 0x6AEA60, 0x6AF2E0, 0x6B1470, 0x6AEF30),
        new("v0.7.17+22362", "871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111",
            0x60F630, 0x6B1EA0, 0x6B11E0, 0x6AEE00, 0x6AF680, 0x6B1810, 0x6AF2D0),
    };

    // Each site is (method, byte offset inside the method). The original instructions were
    // extracted from the v0.7.15 binary and re-verified byte for byte in v0.7.16/0.7.17.
    private static readonly PatchSite[] Sites =
    {
        // MissionTemplate.GetBounds returns (0,0,42,42) with two literal
        // immediates. MissionAreaConfig.AddToList clamps all area types to
        // this 42x42 rectangle EVEN AFTER the blueprint has grown to 96.
        // The two checked bytes must change atomically with pathfinding.
        // This preserves 64x64 as-is (TryInstall is only called for >64).
        new(NativeMethod.MissionTemplateGetBounds, 0x002, 3, "C741082A000000", 0x50, 0x60, "mission-bound-width"),
        new(NativeMethod.MissionTemplateGetBounds, 0x00F, 3, "C7410C2A000000", 0x50, 0x60, "mission-bound-height"),
        new(NativeMethod.PathfindingProcessCtor, 0x07E, 2, "BA00080000", 0x40, 0x40, "queue-capacity"),
        new(NativeMethod.PathfindingProcessCtor, 0x113, 2, "B900100000", 0x40, 0x40, "closed-buffer-allocation"),
        new(NativeMethod.PathfindingProcessCtor, 0x133, 5, "48C744242040000000", 0x80, 0x80, "node-grid-dim-x"),
        new(NativeMethod.PathfindingProcessCtor, 0x13C, 5, "48C744242840000000", 0x80, 0x80, "node-grid-dim-z"),
        new(NativeMethod.PathfindingProcessCtor, 0x1A4, 2, "83FB40", 0x50, 0x60, "ctor-inner-loop"),
        new(NativeMethod.PathfindingProcessCtor, 0x1AB, 2, "83FD40", 0x50, 0x60, "ctor-outer-loop"),
        new(NativeMethod.PathfindingProcessInit, 0x0CA, 2, "83FB40", 0x50, 0x60, "process-init-inner-loop"),
        new(NativeMethod.PathfindingProcessInit, 0x0D5, 2, "83FF40", 0x50, 0x60, "process-init-outer-loop"),
        new(NativeMethod.PathfindingManagerInit, 0x0FA, 2, "83FB40", 0x50, 0x60, "manager-init-inner-loop"),
        new(NativeMethod.PathfindingManagerInit, 0x105, 2, "83FE40", 0x50, 0x60, "manager-init-outer-loop"),
        new(NativeMethod.PathfindingProcessClear, 0x041, 2, "BA00100000", 0x40, 0x40, "closed-buffer-clear"),
        new(NativeMethod.PathfindingProcessFindPath, 0x0FD, 2, "BA00100000", 0x40, 0x40, "findpath-buffer-clear"),
        new(NativeMethod.PathfindingProcessFindPath, 0x2FE, 2, "C1E106", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x385, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x59E, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0xB2F, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0xF68, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x10F3, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x1527, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x15AE, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x1637, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x16BD, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessFindPath, 0x1746, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(NativeMethod.PathfindingProcessProcessNode, 0x665, 2, "C1E006", 0x07, 0x07, "stride-disasm_processnode"),
    };

    /// <summary>Game build matched by the current install, for log/diagnostic output. Null until
    /// <see cref="TryInstall"/> has identified the on-disk GameAssembly.dll.</summary>
    internal static string MatchedGameVersion { get; private set; }

    // Do not install more than once in the process, or change native dimensions mid-session.
    private static bool _installAttempted;

    internal static bool TryInstall(int mapSize, out string reason)
    {
        if (_installAttempted)
        {
            reason = "native pathfinder installation already attempted; restart MENACE";
            return false;
        }
        _installAttempted = true;
        reason = "";
        if (mapSize < 72 || mapSize > 96 || (mapSize % 8) != 0)
        {
            reason = "unsupported experimental map size " + mapSize;
            return false;
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IntPtr.Size != 8)
        {
            reason = "native backend requires Windows x64";
            return false;
        }

        try
        {
            // Validate the entire on-disk native build, not merely the game UI version.
            // SHA-256 is calculated over the file bytes, before touching executable memory.
            // Process.MainModule points to Menace.exe even when the CLR host/base
            // directory differs from the game installation folder.
            string exe = Process.GetCurrentProcess().MainModule?.FileName;
            string baseDir = !string.IsNullOrWhiteSpace(exe)
                ? Path.GetDirectoryName(exe)
                : AppContext.BaseDirectory;
            string gameAssemblyPath = Path.Combine(baseDir, "GameAssembly.dll");
            if (!File.Exists(gameAssemblyPath))
            {
                reason = "GameAssembly.dll not found at " + gameAssemblyPath;
                return false;
            }
            string sha;
            using (var stream = File.OpenRead(gameAssemblyPath))
            using (var hasher = SHA256.Create())
                sha = Convert.ToHexString(hasher.ComputeHash(stream)).ToLowerInvariant();
            GameBuild build = SupportedBuilds.FirstOrDefault(b => string.Equals(sha, b.Sha256, StringComparison.Ordinal));
            if (build == null)
            {
                reason = "GameAssembly.dll SHA-256 " + sha.Substring(0, 12) + " is not a supported build (game update or different build); supported: "
                    + string.Join(", ", SupportedBuilds.Select(b => b.Version));
                return false;
            }
            MatchedGameVersion = build.Version;

            IntPtr module = GetModuleHandleW("GameAssembly.dll");
            if (module == IntPtr.Zero)
            {
                reason = "GameAssembly.dll is not loaded in this process";
                return false;
            }

            // Check every full native instruction before any write. A concurrent mod that
            // already hooked or patched these bytes causes a safe fallback instead.
            foreach (PatchSite site in Sites)
            {
                IntPtr at = IntPtr.Add(module, site.Rva(build));
                for (int i = 0; i < site.Original.Length; ++i)
                {
                    if (Marshal.ReadByte(at, i) != site.Original[i])
                    {
                        reason = "native instruction conflict at " + site.Purpose
                            + " (RVA 0x" + site.Rva(build).ToString("X") + ", " + build.Version + ")";
                        return false;
                    }
                }
            }

            int lowestRva = Sites.Min(s => s.Rva(build));
            int exclusiveEndRva = Sites.Max(s => s.Rva(build) + s.Original.Length);
            IntPtr span = IntPtr.Add(module, lowestRva);
            uint sizeBytes = checked((uint)(exclusiveEndRva - lowestRva));
            if (!VirtualProtect(span, new UIntPtr(sizeBytes), PageExecuteReadWrite, out uint originalProtection))
            {
                reason = "VirtualProtect failed before writing the native patch (Win32 "
                    + Marshal.GetLastWin32Error() + ")";
                return false;
            }

            bool writeOk = false;
            bool restored = false;
            Exception writeError = null;
            try
            {
                foreach (PatchSite site in Sites)
                    Marshal.WriteByte(IntPtr.Add(module, site.Rva(build)), site.Offset, site.ForMap(mapSize));

                writeOk = Sites.All(s =>
                    Marshal.ReadByte(IntPtr.Add(module, s.Rva(build)), s.Offset) == s.ForMap(mapSize));
            }
            catch (Exception ex)
            {
                writeError = ex;
                writeOk = false;
            }
            finally
            {
                // Recover the exact original code in the same writable span when
                // any single write fails. Never claim to have fallen back to 64x64
                // if the rollback did not restore EVERY original instruction byte.
                if (!writeOk)
                {
                    foreach (PatchSite site in Sites)
                    {
                        try { Marshal.WriteByte(IntPtr.Add(module, site.Rva(build)), site.Offset, site.Original[site.Offset]); }
                        catch { /* full rollback verification is immediately below */ }
                    }
                }
                FlushInstructionCache(GetCurrentProcess(), span, new UIntPtr(sizeBytes));
                restored = VirtualProtect(span, new UIntPtr(sizeBytes), originalProtection, out _);
            }

            if (!restored)
                Environment.FailFast("Larger Maps 1.4: native code-page protection could not be restored");

            if (!writeOk)
            {
                bool fullyRolledBack = Sites.All(site =>
                {
                    IntPtr at = IntPtr.Add(module, site.Rva(build));
                    for (int i = 0; i < site.Original.Length; ++i)
                        if (Marshal.ReadByte(at, i) != site.Original[i]) return false;
                    return true;
                });
                if (!fullyRolledBack)
                    Environment.FailFast("Larger Maps 1.4: failed to roll back partial native patch");
                reason = "native byte verification failed; fully rolled back"
                    + (writeError == null ? "" : " (" + writeError.GetType().Name + ")");
                return false;
            }

            reason = "installed " + Sites.Length + " verified native byte edits for " + build.Version + " (including MissionTemplate.GetBounds NxN)";
            return true;
        }
        catch (Exception ex)
        {
            reason = "native pathfinding installation failed: " + ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);
}
