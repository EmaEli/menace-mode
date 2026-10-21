// Managed stand-ins only. No native IL2CPP, UI, game saves or installed mods touched.
using Il2CppInterop.Runtime.InteropTypes;
namespace Il2CppInterop.Runtime.InteropTypes
{
    public class Il2CppObjectBase
    {
        public IntPtr Pointer = (IntPtr)1;
        public T TryCast<T>() where T : class => this as T;
    }
}
namespace Il2CppSystem.Collections
{
    public interface IEnumerable { IEnumerator GetEnumerator(); }
    public interface IEnumerator { bool MoveNext(); Il2CppObjectBase Current { get; } }
    public class NativeCollection<T> : Il2CppObjectBase, IEnumerable where T : Il2CppObjectBase
    {
        public List<T> Items = new();
        public IEnumerator GetEnumerator() => new Iterator(Items);
        private sealed class Iterator : IEnumerator
        {
            private readonly List<T> items; private int index = -1;
            public Iterator(List<T> values) { items = values; }
            public bool MoveNext() => ++index < items.Count;
            public Il2CppObjectBase Current => items[index];
        }
    }
}
namespace Il2CppMenace.Tools
{
    public enum ContentVisibilityType { Regular, FutureContent, Dev }
    public class DataTemplate : Il2CppObjectBase
    {
        public string Id; public ContentVisibilityType ContentVisibility;
        public string GetID() => Id;
    }
    public static class DataTemplateLoader
    {
        public static Dictionary<Type, Il2CppObjectBase> Catalog = new();
        public static bool Throw;
        public static Il2CppObjectBase GetAll<T>() where T : Il2CppObjectBase
            => Throw ? throw new Exception("catalog failure") : Catalog.GetValueOrDefault(typeof(T))
                ?? new Il2CppSystem.Collections.NativeCollection<T>();
        public static void Set<T>(params T[] values) where T : Il2CppObjectBase
            => Catalog[typeof(T)] = new Il2CppSystem.Collections.NativeCollection<T> { Items = values.ToList() };
    }
}
namespace Il2CppMenace.Items
{
    public interface IRedeemable { }
    public class BaseItemTemplate : Il2CppMenace.Tools.DataTemplate { public int BlackMarketMaxQuantity; }
    public class CommodityTemplate : BaseItemTemplate { }
    public class Redeemable : BaseItemTemplate, IRedeemable { }
    public class BlueprintTemplate : BaseItemTemplate
    {
        public BaseItemTemplate Result;
        public List<BlueprintMaterial> Materials = new();
        public BaseItemTemplate GetCraftingResult() => Result;
        public List<BlueprintMaterial> GetCraftingMaterials() => Materials;
    }
    public struct BlueprintMaterial { public BaseItemTemplate Template; }
    public class BaseItem : Il2CppObjectBase
    {
        public bool Equipped, Temporary;
        public object GetContainer() => Equipped ? this : null;
        public bool IsTemporary() => Temporary;
    }
}
namespace Il2CppMenace.Strategy
{
    using Il2CppMenace.Items;
    public class RewardTableTemplate : Il2CppMenace.Tools.DataTemplate { public List<BaseItemTemplate> Items = new(); }
    public class GlobalDifficultyTemplate : Il2CppMenace.Tools.DataTemplate { public BaseItemTemplate[] InitialAdditionalUnlockedItems; }
    public class OwnedItems
    {
        public Dictionary<string, Il2CppSystem.Collections.NativeCollection<BaseItem>> Stock = new();
        public HashSet<string> Fail = new(), Throw = new();
        public int Calls;
        public Il2CppSystem.Collections.NativeCollection<BaseItem> GetInstances(BaseItemTemplate t)
        {
            if (!Stock.ContainsKey(t.Id)) Stock[t.Id] = new();
            return Stock[t.Id];
        }
        public BaseItem AddItem(BaseItemTemplate t, bool dialog, bool slot)
        {
            Calls++;
            if (t is IRedeemable || dialog || slot) throw new Exception("Unsafe native call");
            if (Throw.Contains(t.Id)) throw new Exception("add failure");
            if (Fail.Contains(t.Id)) return null;
            var item = new BaseItem(); GetInstances(t).Items.Add(item); return item;
        }
    }
}
namespace Il2CppMenace.States
{
    public class StrategyState : Il2CppObjectBase
    {
        public static StrategyState Current;
        public Il2CppMenace.Strategy.OwnedItems OwnedItems = new();
        public int CheatMarks;
        public static StrategyState Get() => Current;
        public void SetHasCheatBeenUsed() => CheatMarks++;
    }
}
namespace Il2CppMenace.UI
{
    public class UIManager
    {
        public static UIManager Current = new(); public Il2CppObjectBase Screen;
        public static UIManager Get() => Current;
        public Il2CppObjectBase GetActiveScreen() => Screen;
    }
}
namespace Il2CppMenace.UI.Strategy { public class StrategyUIScreen : Il2CppObjectBase { } }