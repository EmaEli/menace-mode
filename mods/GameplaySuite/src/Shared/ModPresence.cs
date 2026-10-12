namespace MenaceModSuite.Shared;

internal static class ModPresence
{
    public static bool IsAssemblyLoaded(string assemblyNameToken)
    {
        if (string.IsNullOrWhiteSpace(assemblyNameToken)) return false;

        try
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a =>
            {
                string name = a.GetName().Name ?? string.Empty;
                return name.IndexOf(assemblyNameToken, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }
        catch
        {
            return false;
        }
    }

    public static bool IsModDirectoryPresent(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return false;

        try
        {
            string mods = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
            return Directory.Exists(Path.Combine(mods, folderName));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsLoadedOrInstalled(string assemblyNameToken, string folderName)
        => IsAssemblyLoaded(assemblyNameToken) || IsModDirectoryPresent(folderName);
}
