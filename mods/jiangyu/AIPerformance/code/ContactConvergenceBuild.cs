using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace AIPerformance;

internal static class ContactConvergenceBuild
{
    internal static bool IsCurrentSupported()
    {
        string exe = Process.GetCurrentProcess().MainModule?.FileName;
        string directory = string.IsNullOrWhiteSpace(exe) ? AppContext.BaseDirectory : Path.GetDirectoryName(exe);
        using var stream = File.OpenRead(Path.Combine(directory, "GameAssembly.dll"));
        using var hash = SHA256.Create();
        return string.Equals(Convert.ToHexString(hash.ComputeHash(stream)),
            "494c9e212d411d898b3abafb4ad233f7a39a22648d713a86b9641d698b2d12e9", StringComparison.OrdinalIgnoreCase);
    }
}