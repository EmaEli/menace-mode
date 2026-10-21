namespace MenaceModSettings;

// No Unity/native dependencies: this is also executed by the offline checks.
internal static class InventoryDebugStock
{
    internal const int Target = 10;

    internal sealed class Result
    {
        internal int Types, Added, Failed;
    }

    internal static Result TopUp<T>(IEnumerable<T> templates, Func<T, string> id,
        Func<T, int> reserveCount, Func<T, bool> add, Action beforeFirstAdd)
    {
        var result = new Result();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool started = false;
        foreach (var template in templates)
        {
            string key = id(template);
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key)) continue;
            result.Types++;
            try
            {
                int count = reserveCount(template);
                if (count < 0) throw new InvalidOperationException("Invalid reserve count");
                for (int n = count; n < Target; n++)
                {
                    if (!started) { beforeFirstAdd(); started = true; }
                    if (!add(template)) { result.Failed++; break; }
                    result.Added++;
                }
            }
            catch { result.Failed++; }
        }
        return result;
    }
}