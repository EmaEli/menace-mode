using SmokeOverhaul;

internal static class SmokeNativeAccuracyChecks
{
    public static void Run(Action<bool, string> check)
    {
        var patch = new SmokeNativeStatModifiers();
        var empty = Array.Empty<Stat>();
        // Concealment, aim, defense, damage, suppression AND vision: no whitelist.
        Stat[] original = { new(2, 5, 0), new(9, 0, .3f), new(10, 0, 1.8f),
            new(11, 0, .3f), new(12, 0, .3f), new(0, -5, 1) };
        var modern = new Modern { Properties = original };
        object marker = modern.CloudAndLifecycle;
        check(patch.TryApply(new[] { modern }, empty, out int count) && count == 6,
            "0.7.18 array neutralizes all six native smoke properties");
        check(modern.Properties!.Length == 0 && original.Length == 6,
            "stat entries disabled without mutating original value-type array");
        check(ReferenceEquals(marker, modern.CloudAndLifecycle), "cloud/lifecycle untouched");
        check(!patch.TryApply(new[] { modern }, empty, out _), "repeated apply cannot overwrite original snapshot");
        check(patch.Restore() && ReferenceEquals(modern.Properties, original), "exact original native array restored");
        check(patch.Restore(), "restoration is idempotent");
        modern.Properties = null;
        check(patch.TryApply(new[] { modern }, empty, out count) && count == 0 && !patch.HasSnapshots,
            "null property array already has no stat modifiers");

        var legacy = original.Select(s => new Legacy(s.Type, s.Amount, s.Mult)).ToArray();
        object provider = legacy[1].ValueProvider!;
        check(patch.TryApply(legacy, empty, out count) && count == 6, "legacy scalar layout supported");
        check(legacy.All(s => s.Amount == 0 && s.AmountMult == 1 && s.ValueProvider == null),
            "legacy positive/negative modifiers and value providers are all neutralized");
        check(legacy.Select(s => s.PropertyType).SequenceEqual(original.Select(s => s.Type)),
            "property identities retained");
        check(patch.Restore() && legacy[0].Amount == 5 && legacy[1].AmountMult == .3f &&
            ReferenceEquals(legacy[1].ValueProvider, provider) && legacy[5].Amount == -5,
            "legacy stats and providers restored exactly");

        modern.Properties = original;
        var broken = new Rejecting { Properties = original, RejectNeutral = true };
        check(!patch.TryApply(new object[] { modern, broken }, empty, out _), "setter failure rejects whole transaction");
        check(ReferenceEquals(modern.Properties, original) && ReferenceEquals(broken.Properties, original) &&
            !patch.HasSnapshots, "partial native writes roll back including a setter that writes then throws");

        broken.RejectRestore = true;
        check(!patch.TryApply(new object[] { modern, broken }, empty, out _) && patch.HasSnapshots,
            "failed rollback retains recovery snapshot");
        broken.RejectRestore = false;
        check(patch.Restore() && ReferenceEquals(broken.Properties, original), "failed restoration can be retried");

        check(!patch.TryApply(new object[] { modern, new object() }, empty, out _) &&
            ReferenceEquals(modern.Properties, original), "unknown layout fails before any native writes");
        var ignored = new Ignoring { Properties = original };
        check(!patch.TryApply(new object[] { modern, ignored }, empty, out _) &&
            ReferenceEquals(modern.Properties, original), "ignored setter fails readback and rolls back");

        var wrapped = new Rewrapping();
        check(patch.TryApply(new[] { wrapped }, new NativeArray(new IntPtr(200), 0), out count) && count == 6,
            "fresh managed array wrappers are verified by native pointer, not reference equality");
        check(patch.Restore() && wrapped.Properties.Pointer == new IntPtr(100), "native pointer restoration verified");
    }

    private record struct Stat(int Type, int Amount, float Mult);
    private sealed class Modern
    {
        public Stat[]? Properties { get; set; }
        public object CloudAndLifecycle { get; } = new();
    }
    private sealed class Legacy
    {
        public int PropertyType, Amount;
        public float AmountMult;
        public object? ValueProvider;
        public Legacy(int type, int amount, float mult)
        { PropertyType = type; Amount = amount; AmountMult = mult; ValueProvider = new object(); }
    }
    private sealed class Rejecting
    {
        private Stat[]? _properties;
        public bool RejectNeutral, RejectRestore;
        public Stat[]? Properties
        {
            get => _properties;
            set
            {
                if (RejectRestore && value?.Length > 0) throw new InvalidOperationException();
                _properties = value;
                if (RejectNeutral && value?.Length == 0) throw new InvalidOperationException();
            }
        }
    }
    private sealed class Ignoring
    {
        private Stat[]? _properties;
        public Stat[]? Properties { get => _properties; set { if (_properties == null) _properties = value; } }
    }
    private sealed class NativeArray
    {
        public NativeArray(IntPtr pointer, int length) { Pointer = pointer; Length = length; }
        public IntPtr Pointer { get; }
        public int Length { get; }
    }
    private sealed class Rewrapping
    {
        private NativeArray _properties = new(new IntPtr(100), 6);
        public NativeArray Properties
        {
            get => new(_properties.Pointer, _properties.Length);
            set => _properties = value;
        }
    }
}