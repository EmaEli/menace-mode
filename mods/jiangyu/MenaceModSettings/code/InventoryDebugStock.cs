namespace MenaceModSettings;

// No Unity/native dependencies: this is also executed by the offline checks.
internal static class InventoryDebugStock
{
    internal const int Target = 10;

    internal sealed class Result
    {
        internal int Types, Added, Failed;
        internal readonly List<Failure> Failures = new();
    }

    internal sealed class Failure
    {
        internal string ItemId, Stage, Details;
    }

    internal static Result TopUp<T>(IEnumerable<T> templates, Func<T, string> id,
        Func<T, int> reserveCount, Func<T, bool> add, Action beforeFirstAdd)
    {
        var result = new Result();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool started = false;
        foreach (var template in templates)
        {
            string key;
            try { key = id(template); }
            catch (Exception error)
            {
                RecordFailure(result, "<unknown>", "identify item", Describe(error));
                continue;
            }
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key)) continue;
            result.Types++;
            string stage = "read current stock";
            try
            {
                int count = reserveCount(template);
                if (count < 0) throw new InvalidOperationException("Invalid reserve count.");
                for (int n = count; n < Target; n++)
                {
                    if (!started)
                    {
                        stage = "mark campaign cheated";
                        beforeFirstAdd();
                        started = true;
                    }
                    stage = "add item";
                    if (!add(template))
                    {
                        RecordFailure(result, key, stage, "AddItem returned no item.");
                        break;
                    }
                    result.Added++;
                }
            }
            catch (Exception error) { RecordFailure(result, key, stage, Describe(error)); }
        }
        return result;
    }

    private static void RecordFailure(Result result, string itemId, string stage, string details)
    {
        result.Failed++;
        result.Failures.Add(new Failure { ItemId = itemId, Stage = stage, Details = details });
    }

    private static string Describe(Exception error)
        => $"{error.GetType().Name}: {error.Message}".Replace('\r', ' ').Replace('\n', ' ').Trim();
}