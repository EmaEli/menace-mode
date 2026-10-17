// Native API simulation: these tests exercise production callbacks, NOT the game engine.
namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
}
namespace Il2CppMenace.Items
{
    public class BaseItemTemplate
    {
        public string Id;
        public string GetID() => Id;
        public static string CreateGuid() => Guid.NewGuid().ToString();
    }
    public class BaseItem
    {
        public BaseItemTemplate Template;
        private readonly string _guid = Guid.NewGuid().ToString();
        public string GetGuid() => _guid;
        public BaseItemTemplate GetBaseItemTemplate() => Template;
    }
    public class DossierItemTemplate : BaseItemTemplate
    {
        public Il2CppMenace.Tools.LocalizedLine Title, ShortName;
        public Il2CppMenace.Tools.LocalizedMultiLine Description;
        public object[] m_UnlockedLeaders = Array.Empty<object>();
        public int Price = 1;
        public int VanillaRedeems;
        public void Redeem() => VanillaRedeems++;
        public int GetTradeValue() => Price;
        public BaseItem CreateItem(string guid) => new() { Template = this };
    }
}
namespace Il2CppMenace.Strategy
{
    public class BlackMarket
    {
        public readonly List<Il2CppMenace.Items.BaseItem> Items = new();
        public int LastTimeout { get; private set; } = -1;
        public void Restock(bool decrement, bool reset) { }
        public bool ContainsItem(Il2CppMenace.Items.BaseItemTemplate template) => Items.Any(i => i.Template == template);
        public void AddItem(Il2CppMenace.Items.BaseItem item, int timeout)
        { LastTimeout = timeout; Items.Add(item); }
        public bool RemoveItem(Il2CppMenace.Items.BaseItem item) => Items.Remove(item);
        public void GetInstances(Il2CppSystem.Collections.Generic.List<Il2CppMenace.Items.BaseItem> result, bool special)
        { if (!special) result.AddRange(Items); }
    }
}
namespace Il2CppMenace.States
{
    public class StrategyState
    {
        public static StrategyState Current;
        public object Squaddies = new();
        public Il2CppMenace.Strategy.BlackMarket BlackMarket = new();
        public bool Ironman;
        public bool IsIronman() => Ironman;
        public static StrategyState Get() => Current;
    }
}
namespace Il2CppMenace.UI
{
    public interface IOpenScreenParams { }
    public class UIManager
    {
        public static UIManager Current = new();
        public SimpleDialog Last;
        public object Active;
        public static UIManager Get() => Current;
        public SimpleDialog AddSimpleDialog() => Last = new();
        public Screen GetActiveScreen() => new() { Value = Active };
    }
    public class Screen { public object Value; public T TryCast<T>() where T : class => Value as T; }
    public class SimpleDialog
    {
        public string Text;
        public Label Label = new();
        public void Init(string name, string title, string text, string yes, object yesAction, string no, object noAction, bool translate) => Text = text;
        public Label GetTextLabel() => Label;
    }
    public class Label { public bool enableRichText = true; }
}
namespace Il2CppMenace.UI.Strategy
{
    public class BlackMarketUIScreen
    {
        public int Updates;
        public void OnOpened(Il2CppMenace.UI.IOpenScreenParams parameters) { }
        public void UpdateWindow() => Updates++;
        // Native FilterItems removes merchant items priced below 1 before rendering.
        public Il2CppSystem.Collections.Generic.List<Il2CppMenace.Items.BaseItem> m_TempFilteredRegularOffers
        {
            get
            {
                var visible = new Il2CppSystem.Collections.Generic.List<Il2CppMenace.Items.BaseItem>();
                visible.AddRange(Il2CppMenace.States.StrategyState.Current.BlackMarket.Items
                    .Where(i => ((Il2CppMenace.Items.DossierItemTemplate)i.Template).GetTradeValue() >= 1));
                return visible;
            }
        }
    }
}
namespace Il2CppMenace.Tools
{
    public enum LocaCategory { Items = 7 }
    public class BaseLocalizedString
    {
        public readonly LocaCategory Category;
        public readonly string Field;
        public string Id;
        private string _text;
        protected BaseLocalizedString(LocaCategory category, string field, bool allowPlaceholders)
        { Category = category; Field = field; }
        public void SetId(string id) => Id = id;
        public void SetDefaultTranslation(string text) => _text = text;
        public string GetRawDefaultTranslation() => _text;
    }
    public class LocalizedLine : BaseLocalizedString
    {
        public LocalizedLine(LocaCategory category, string field, bool allowPlaceholders)
            : base(category, field, allowPlaceholders) { }
    }
    public class LocalizedMultiLine : BaseLocalizedString
    {
        public LocalizedMultiLine(LocaCategory category, string field, bool allowPlaceholders)
            : base(category, field, allowPlaceholders) { }
    }
    public static class DataTemplateLoader
    {
        public static object Template;
        public static T Get<T>(string id, bool warn) where T : class => Template as T;
    }
}
namespace Jiangyu.Sdk
{
    public class JiangyuSystem
    {
        public ModContext Context = new();
        public virtual void OnInit() { }
        public virtual void OnTemplatesApplied() { }
        public virtual void OnUnload() { }
    }
    public class ModContext { public FakePatches Patches = new(); }
    public class FakePatches
    {
        public Dictionary<string, Action<PatchInfo>> Prefixes = new(), Postfixes = new();
        public void Prefix(string type, string method, int count, Action<PatchInfo> callback) => Prefixes[method] = callback;
        public void Postfix(string type, string method, int count, Action<PatchInfo> callback) => Postfixes[method] = callback;
    }
    public class PatchInfo { public object Instance; public object Result; public bool Skip; }
    public static class Log
    {
        public static List<string> Warnings = new(), InfoLines = new();
        public static void Warn(string text) => Warnings.Add(text);
        public static void Info(string text) => InfoLines.Add(text);
    }
}
namespace MenaceModSettings
{
    public class SettingsBuilder
    {
        public void AddHeader(string text) { }
        public void AddToggle(string key, string label, bool value) => ModSettings.Values[key] = value;
    }
    public static class ModSettings
    {
        public static Dictionary<string, bool> Values = new();
        public static event Action<string, string, object> Changed;
        public static void Register(string group, Action<SettingsBuilder> build) => build(new());
        public static T Get<T>(string group, string key) => (T)(object)Values[key];
        public static void Set(string key, bool value)
        { Values[key] = value; Changed?.Invoke("Squaddie Field Promotions", key, value); }
    }
}
namespace SquaddieFieldPromotions
{
    internal static class NativeCandidates
    {
        internal static bool Fail;
        internal static Selection Value;
        internal static Selection Read(Il2CppMenace.States.StrategyState state) => Fail ? throw new IOException("simulated read error") : Value;
    }
}