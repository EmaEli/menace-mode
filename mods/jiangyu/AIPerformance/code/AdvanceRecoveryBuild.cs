using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace AIPerformance;

internal static class AdvanceRecoveryBuild
{
    internal static bool IsSupported(string sha256) =>
        string.Equals(sha256, "871b4c3468c68dae3d75e7b5e29fcc9b0a8a253fcebf64045af348f0aec75111", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sha256, "494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9", StringComparison.OrdinalIgnoreCase);

    // Independent of LargerMaps and whether its native pathfinder was installed.
    internal static bool IsCurrentSupported()
    {
        string exe = Process.GetCurrentProcess().MainModule?.FileName;
        string directory = string.IsNullOrWhiteSpace(exe) ? AppContext.BaseDirectory : Path.GetDirectoryName(exe);
        using var stream = File.OpenRead(Path.Combine(directory, "GameAssembly.dll"));
        using var hash = SHA256.Create();
        return IsSupported(Convert.ToHexString(hash.ComputeHash(stream)));
    }
}