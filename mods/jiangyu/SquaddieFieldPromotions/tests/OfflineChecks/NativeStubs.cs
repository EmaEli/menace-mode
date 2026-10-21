// Native API simulation: these tests exercise production callbacks, NOT the game engine.
namespace Il2CppSystem
{
    public class NativeObject
    {
        private static int _next;
        public IntPtr Pointer { get; set; } = (IntPtr)(++_next);
    }
}
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
        public int Price = 100;
        public int VanillaRedeems;
        public void Redeem() => VanillaRedeems++;
        public int GetTradeValue() => Price;
        public BaseItem CreateItem(string guid) => new() { Template = this };
    }
}
namespace Il2CppMenace.Conversations
{
    public class SpeakerTemplate
    {
        public Il2CppMenace.Tools.LocalizedLine Nickname;
        public Il2CppMenace.Tools.LocalizedMultiLine Description;
        public string FullName;
    }
}
namespace Il2CppMenace.Strategy
{
    public class MissionTemplate
    {
        public string Id;
        public string GetID() => Id;
    }
    public class Mission
    {
        public MissionTemplate Template;
        public int Index, Seed;
        public MissionTemplate GetTemplate() => Template;
        public int GetIdx() => Index;
        public int GetSeed() => Seed;
    }
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
    public class UnitLeaderTemplate
    {
        public Il2CppMenace.Conversations.SpeakerTemplate SpeakerTemplate;
        public Il2CppMenace.Tools.LocalizedLine UnitTitle;
        public Il2CppMenace.Tools.LocalizedMultiLine UnitDescription;
        public bool IsInfantry = true;
        public UnitLeaderTemplate InfantryUnitTemplate;
        public UnityEngine.Sprite Badge, BadgeWhite, BadgeMini, BadgeDragged, BadgeUnitWindow, SlotBadge, BigBadge;
        public string Id;
        public string GetID() => Id;
        // Native v0.7.17 getters select the entity's text for infantry (ActorType == 0).
        public Il2CppMenace.Tools.LocalizedLine GetUnitTitle() => DisplayText("GetUnitTitle",
            IsInfantry ? InfantryUnitTemplate?.UnitTitle : UnitTitle) as Il2CppMenace.Tools.LocalizedLine;
        public Il2CppMenace.Tools.LocalizedMultiLine GetUnitDescription() => DisplayText("GetUnitDescription",
            IsInfantry ? InfantryUnitTemplate?.UnitDescription : UnitDescription) as Il2CppMenace.Tools.LocalizedMultiLine;
        public UnityEngine.Sprite GetBadge() => DisplayText("GetBadge",
            IsInfantry ? InfantryUnitTemplate?.Badge : Badge) as UnityEngine.Sprite;
        public UnityEngine.Sprite GetBadgeWhite() => DisplayText("GetBadgeWhite",
            IsInfantry ? InfantryUnitTemplate?.BadgeWhite : BadgeWhite) as UnityEngine.Sprite;
        private object DisplayText(string method, object nativeResult)
        {
            var info = new Jiangyu.Sdk.PatchInfo { Instance = this, Result = nativeResult };
            if (Jiangyu.Sdk.FakePatches.Current?.Postfixes.TryGetValue(method, out var callback) == true)
                callback(info);
            return info.Result;
        }
    }
    public class UnitStatistics
    {
        public readonly List<int> WoundedIds = new(), DownedIds = new();
        public IReadOnlyList<int> GetLastMissionWoundedSquaddieIds() => WoundedIds;
        public IReadOnlyList<int> GetLastMissionDownedSquaddieIds() => DownedIds;
    }
    public class BaseUnitLeader
    {
        public Il2CppSystem.Collections.Generic.List<int> m_SquaddieIds = new();
        public UnitLeaderTemplate LeaderTemplate;
        public UnitStatistics Statistics = new();
        public Il2CppSystem.Collections.Generic.List<int> GetSquaddieIds() => m_SquaddieIds;
        public UnitStatistics GetStatistics() => Statistics;
        public bool FailDetach;
        public bool TryRemoveSquaddie(int id) => !FailDetach && m_SquaddieIds.Remove(id);
        public bool HasSquaddie(int id) => m_SquaddieIds.Contains(id);
    }
    public class Squaddie
    {
        public int Id, Gender, SkinColor;
        public string Name, Nickname;
        public BaseUnitLeader Owner;
        public int GetId() => Id;
        public int GetGender() => Gender;
        public int GetSkinColor() => SkinColor;
        public BaseUnitLeader GetCurrentLeader() => Owner != null && Owner.HasSquaddie(Id) ? Owner : null;
    }
    public class Squaddies
    {
        public Il2CppSystem.Collections.Generic.List<Squaddie> m_AliveSquaddies = new(), m_WoundedSquaddies = new(), m_KilledSquaddies = new();
        public Action OnSquaddiesChanged;
        public bool FailReadback;
        public bool IsAlive(int id) => FailReadback || m_AliveSquaddies.Any(s => s?.Id == id);
        public static void OnMissionEnd(Mission mission, Il2CppSystem.Collections.Generic.List<BaseUnitLeader> leaders) { }
    }
    public class Roster : Il2CppSystem.NativeObject
    {
        public readonly Il2CppSystem.Collections.Generic.List<UnitLeaderTemplate> m_HirableLeaders = new();
        public readonly Il2CppSystem.Collections.Generic.List<BaseUnitLeader> m_HiredLeaders = new();
        public readonly Il2CppSystem.Collections.Generic.List<BaseUnitLeader> m_DismissedLeaders = new();
        public readonly Il2CppSystem.Collections.Generic.List<BaseUnitLeader> m_UnburiedLeaders = new();
        public readonly Il2CppSystem.Collections.Generic.List<BaseUnitLeader> m_BuriedLeaders = new();
        public Il2CppSystem.Collections.Generic.List<UnitLeaderTemplate> Hirable => m_HirableLeaders;
        public void AddHirableLeader(UnitLeaderTemplate leader) => m_HirableLeaders.Add(leader);
        public void ProcessSaveState(SaveState save) { }
    }
    public class DeployedEntity
    {
        public BaseUnitLeader Leader;
        public int Tile;
        public BaseUnitLeader GetUnitLeader() => Leader;
    }
    public class BattlePlan
    {
        public readonly Il2CppSystem.Collections.Generic.List<DeployedEntity> m_EntitiesToDeploy = new();
    }
    public class SaveState : Il2CppSystem.NativeObject
    {
        public string FilePath;
        public bool Loading = true;
        public string GetFilePath() => FilePath;
        public bool IsLoading() => Loading;
        public void Close() { }
    }
    public static class SaveSystem
    {
        public static string LastSavedPath;
        public static int SaveCalls;
        public static bool FailSave;
        private static void ExecLoad(SaveState state) { }
        public static void Save(SaveStateType type, string path, string saveGameName)
        {
            SaveCalls++;
            if (FailSave) throw new IOException("simulated native save failure");
            LastSavedPath = path;
        }
    }
    public enum SaveStateType { None, Manual, Quick, Auto, Ironman }
}
namespace Il2CppMenace.States
{
    public class StrategyState
    {
        public static StrategyState Current;
        public Il2CppMenace.Strategy.Squaddies Squaddies = new();
        public Il2CppMenace.Strategy.Roster Roster = new();
        public Il2CppMenace.Strategy.BattlePlan BattlePlan = new();
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
        public LocaCategory GetCategory() => Category;
        public string GetRawDefaultTranslation() => _text;
        public override string ToString() => _text;
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
        public virtual void OnSceneLoaded(int buildIndex, string sceneName) { }
        public virtual void OnUnload() { }
    }
    public class ModContext
    {
        public FakePatches Patches = new();
        public FakeState State = new();
        public FakeCoroutines Coroutines = new();
        public string ModFolder = Path.GetTempPath();
    }
    public class FakePatches
    {
        public static FakePatches Current;
        public FakePatches() { Current = this; }
        public Dictionary<string, Action<PatchInfo>> Prefixes = new(), Postfixes = new();
        public void Prefix(string type, string method, int count, Action<PatchInfo> callback) => Prefixes[method] = callback;
        public void Postfix(string type, string method, int count, Action<PatchInfo> callback) => Postfixes[method] = callback;
    }
    public class FakeState
    {
        public readonly Dictionary<Type, object> Blobs = new();
        public int Saves;
        public bool FailSave;
        public T Get<T>() where T : class, new()
        {
            if (!Blobs.TryGetValue(typeof(T), out var blob)) Blobs[typeof(T)] = blob = new T();
            return (T)blob;
        }
        public void Save() { if (FailSave) throw new IOException("simulated state failure"); Saves++; }
    }
    public class FakeCoroutines
    {
        public readonly List<System.Collections.IEnumerator> Started = new();
        public object Start(System.Collections.IEnumerator routine) { Started.Add(routine); return routine; }
        public void Stop(object handle) { if (handle is System.Collections.IEnumerator routine) Started.Remove(routine); }
        public void RunAll() { foreach (var routine in Started) while (routine.MoveNext()) { } Started.Clear(); }
    }
    public class PatchInfo
    {
        public object Instance; public object Result; public bool Skip;
        public IReadOnlyList<object> Args = Array.Empty<object>();
    }
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
        public void AddNumber(string key, string label, int min, int max, int value, int step)
        { ModSettings.Values[key] = value; ModSettings.Numbers[key] = (min, max, value, step); }
    }
    public static class ModSettings
    {
        public static Dictionary<string, object> Values = new();
        public static Dictionary<string, (int Min, int Max, int Default, int Step)> Numbers = new();
        public static event Action<string, string, object> Changed;
        public static void Register(string group, Action<SettingsBuilder> build) => build(new());
        public static T Get<T>(string group, string key) => (T)(object)Values[key];
        public static void Set(string key, object value) => Set("Squaddie Field Promotions", key, value);
        public static void Set(string group, string key, object value)
        { Values[key] = value; Changed?.Invoke(group, key, value); }
    }
}
namespace SquaddieFieldPromotions
{
    internal static class PrivateMember
    {
        internal static IEnumerable<object> Enumerate(object collection)
        {
            if (collection is System.Collections.IEnumerable values)
                foreach (object value in values) if (value != null) yield return value;
        }
    }
    internal static class NativeCandidates
    {
        internal static bool Fail;
        internal static Selection Value;
        internal static ISet<int> LastExcluded;
        internal static Selection Read(Il2CppMenace.States.StrategyState state, ISet<int> excluded)
        {
            LastExcluded = excluded;
            if (Fail) throw new IOException("simulated read error");
            return Value != null && excluded.Contains(Value.Soldier.Id) ? null : Value;
        }
    }
    // Simulates the native template forge: records every install/rehydrate, never touches the engine.
    internal sealed class LeaderForge
    {
        internal static bool FailInstall;
        internal static int NextGuid = 1000;
        internal readonly List<string> Installed = new(), Rehydrated = new();
        internal readonly List<string> Refreshed = new();
        internal int VoicePrepareCalls;
        internal bool VoiceSucceeds = true;
        internal bool Unloaded;
        internal readonly string ModFolder;
        internal static LeaderForge Last;
        internal LeaderForge(string modFolder) { ModFolder = modFolder; Last = this; }
        internal bool AssetsPresent = true;
        internal bool HasPendingVoices => Installed.Count + Rehydrated.Count > 0 && VoicePrepareCalls == 0;
        internal void Unload() => Unloaded = true;
        internal static PromotionRecord CreateRecord(Il2CppMenace.States.StrategyState state, Selection selection,
            int badgeIndex, SquaddieServiceRecord service = null, string sourceLeaderTemplateId = "")
        {
            string token = Guid.NewGuid().ToString("N");
            var record = new PromotionRecord
            {
                LeaderId = PromotionRecord.LeaderPrefix + token, SpeakerId = PromotionRecord.SpeakerPrefix + token,
                NumericGuid = NextGuid++, SourceSquaddieId = selection.Soldier.Id, Name = selection.Soldier.Name,
                Nickname = selection.Soldier.Nickname, Gender = selection.Soldier.Gender, SkinColor = selection.Soldier.SkinColor,
                BadgeIndex = badgeIndex, InitialAttributes = PromotionRules.Attributes(selection.UsesXp ? selection.Soldier.Xp : null),
            };
            PromotionIdentity.Populate(record, selection.Soldier, service, sourceLeaderTemplateId);
            record.Validate();
            return record;
        }
        internal string Install(Il2CppMenace.States.StrategyState state, PromotionRecord record, bool forceGenericHead)
        {
            record.Validate();
            if (FailInstall) throw new InvalidOperationException("simulated native clone failure");
            HeadModes.Add(forceGenericHead);
            state.Roster.AddHirableLeader(new Il2CppMenace.Strategy.UnitLeaderTemplate { Id = record.LeaderId });
            Installed.Add(record.LeaderId);
            return "fake-install";
        }
        internal void RollbackInstall(Il2CppMenace.States.StrategyState state, PromotionRecord record)
        {
            state.Roster.Hirable.RemoveAll(t => t.Id == record.LeaderId);
            Installed.Remove(record.LeaderId);
        }
        internal static bool RosterContainsLeader(Il2CppMenace.States.StrategyState state, string id) =>
            state.Roster.Hirable.Any(t => t.Id == id);
        internal string Rehydrate(PromotionRecord record, bool forceGenericHead)
        {
            record.Validate();
            if (record.Name == "Broken") throw new InvalidOperationException("simulated rehydrate failure");
            HeadModes.Add(forceGenericHead);
            Rehydrated.Add(record.LeaderId);
            return "fake-rehydrate";
        }
        internal bool RefreshIdentity(PromotionRecord record)
        {
            if (record.Name == "Broken") return false;
            Refreshed.Add(record.LeaderId);
            return true;
        }
        internal readonly List<bool> HeadModes = new();
        internal bool SetHeadMode(PromotionRecord record, bool forceGenericHead)
        { HeadModes.Add(forceGenericHead); return true; }
        internal bool TryPrepareVoices(out string status)
        {
            VoicePrepareCalls++;
            status = VoiceSucceeds ? "ok" : "bundle missing";
            return VoiceSucceeds;
        }
    }
}