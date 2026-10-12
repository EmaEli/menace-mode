using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LargerMaps;

/// <summary>
/// Version-locked, fail-closed native pathfinding adaptation for MENACE v0.7.15+21883.
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
/// Different game versions or earlier patch/detour attempts force a 64x64 fallback.
/// This is EXPERIMENTAL and still requires in-game A*/AI validation.
/// </summary>
internal static class ExtendedPathfinding
{
    internal const int InternalStride = 128;
    private const string ExpectedGameAssemblySha256 = "877622dd02ed79f6a3a37580b5cd9be12e29012bbd6f15a9c80cf917af1373db";
    private const uint PageExecuteReadWrite = 0x40;

    private sealed class PatchSite
    {
        internal readonly int Rva;
        internal readonly int Offset;
        internal readonly byte[] Original;
        internal readonly byte Value80;
        internal readonly byte Value96;
        internal readonly string Purpose;

        internal PatchSite(int rva, int offset, string originalHex, byte value80, byte value96, string purpose)
        {
            Rva = rva;
            Offset = offset;
            Original = Convert.FromHexString(originalHex);
            Value80 = value80;
            Value96 = value96;
            Purpose = purpose;
            if (Offset < 0 || Offset >= Original.Length)
                throw new ArgumentException("Invalid native byte patch descriptor for " + purpose);
        }

        internal byte ForMap(int size) => (Purpose.EndsWith("-loop", StringComparison.Ordinal) ||
            Purpose.StartsWith("mission-bound-", StringComparison.Ordinal))
            ? checked((byte)size)
            : Value80;
    }

    // RVA values are offsets from the loaded GameAssembly.dll module base, not file offsets.
    // These original instructions were extracted and checked against the supplied binary.
    private static readonly PatchSite[] Sites =
    {
        // MissionTemplate.GetBounds returns (0,0,42,42) with two literal
        // immediates. MissionAreaConfig.AddToList clamps all area types to
        // this 42x42 rectangle EVEN AFTER the blueprint has grown to 96.
        // The two checked bytes must change atomically with pathfinding.
        // This preserves 64x64 as-is (TryInstall is only called for >64).
        new(0x5F61C2, 3, "C741082A000000", 0x50, 0x60, "mission-bound-width"),
        new(0x5F61CF, 3, "C7410C2A000000", 0x50, 0x60, "mission-bound-height"),
        new(0x69EF4E, 2, "BA00080000", 0x40, 0x40, "queue-capacity"),
        new(0x69EFE3, 2, "B900100000", 0x40, 0x40, "closed-buffer-allocation"),
        new(0x69F003, 5, "48C744242040000000", 0x80, 0x80, "node-grid-dim-x"),
        new(0x69F00C, 5, "48C744242840000000", 0x80, 0x80, "node-grid-dim-z"),
        new(0x69F074, 2, "83FB40", 0x50, 0x60, "ctor-inner-loop"),
        new(0x69F07B, 2, "83FD40", 0x50, 0x60, "ctor-outer-loop"),
        new(0x69E2DA, 2, "83FB40", 0x50, 0x60, "process-init-inner-loop"),
        new(0x69E2E5, 2, "83FF40", 0x50, 0x60, "process-init-outer-loop"),
        new(0x69BF2A, 2, "83FB40", 0x50, 0x60, "manager-init-inner-loop"),
        new(0x69BF35, 2, "83FE40", 0x50, 0x60, "manager-init-outer-loop"),
        new(0x69C341, 2, "BA00100000", 0x40, 0x40, "closed-buffer-clear"),
        new(0x69C7AD, 2, "BA00100000", 0x40, 0x40, "findpath-buffer-clear"),
        new(0x69C9AE, 2, "C1E106", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69CA35, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69CC4E, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69D1DF, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69D618, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69D7A3, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69DBD7, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69DC5E, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69DCE7, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69DD6D, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69DDF6, 2, "C1E006", 0x07, 0x07, "stride-disasm_findpath"),
        new(0x69EEA5, 2, "C1E006", 0x07, 0x07, "stride-disasm_processnode"),
    };

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
            if (!string.Equals(sha, ExpectedGameAssemblySha256, StringComparison.Ordinal))
            {
                reason = "GameAssembly.dll SHA-256 mismatch (game update or different build)";
                return false;
            }

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
                IntPtr at = IntPtr.Add(module, site.Rva);
                for (int i = 0; i < site.Original.Length; ++i)
                {
                    if (Marshal.ReadByte(at, i) != site.Original[i])
                    {
                        reason = "native instruction conflict at " + site.Purpose
                            + " (RVA 0x" + site.Rva.ToString("X") + ")";
                        return false;
                    }
                }
            }

            int lowestRva = Sites.Min(s => s.Rva);
            int exclusiveEndRva = Sites.Max(s => s.Rva + s.Original.Length);
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
                    Marshal.WriteByte(IntPtr.Add(module, site.Rva), site.Offset, site.ForMap(mapSize));

                writeOk = Sites.All(s =>
                    Marshal.ReadByte(IntPtr.Add(module, s.Rva), s.Offset) == s.ForMap(mapSize));
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
                        try { Marshal.WriteByte(IntPtr.Add(module, site.Rva), site.Offset, site.Original[site.Offset]); }
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
                    IntPtr at = IntPtr.Add(module, site.Rva);
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

            reason = "installed " + Sites.Length + " verified native byte edits (including MissionTemplate.GetBounds NxN)";
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
