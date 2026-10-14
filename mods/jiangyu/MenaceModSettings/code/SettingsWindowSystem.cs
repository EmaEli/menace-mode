using Il2CppInterop.Runtime;
using Jiangyu.Game.Input;
using Jiangyu.Game.Ui;
using Jiangyu.Game.Ui.Components;
using Jiangyu.Sdk;
using UnityEngine;
using UnityEngine.UIElements;

namespace MenaceModSettings;

/// <summary>
/// The settings window: a full-screen overlay holding a native window frame, one tab per
/// registered group and one row per setting. Toggled with a configurable key (F9 by default),
/// closed by the CLOSE button or a click on the dimmed backdrop. Injected once; the loader
/// re-applies it per screen.
/// </summary>
public sealed class SettingsWindowSystem : JiangyuSystem
{
    public const string OwnGroup = "MenaceModSettings";
    public const string ToggleKeySetting = "ToggleKey";
    private static readonly string[] ToggleKeyOptions = { "F8", "F9", "F10", "F11", "F12", "Insert", "Home", "End" };

    private UiInjection _injection;
    private IDisposable _hotkey;
    private KeyCode _toggleKey;
    private VisualElement _overlay;
    private VisualElement _window;
    private VisualElement _tabs;
    private VisualElement _rows;
    private Label _hint;
    private EventCallback<PointerDownEvent> _onBackdropPress;
    private EventCallback<GeometryChangedEvent> _onWindowResized;
    private bool _open;
    private string _selected;

    public override void OnInit()
    {
        ModSettings.Register(OwnGroup, s =>
        {
            s.AddHeader("Window");
            s.AddDropdown(ToggleKeySetting, "Key that opens and closes this window", ToggleKeyOptions, "F9");
        });
        ModSettings.Changed += OnSettingChanged;
        BindHotkey();
        _injection = UI.Inject(UiTarget.Overlay(), Build, Bind);
        Log.Debug($"settings window on {_toggleKey}");
    }

    public override void OnUnload()
    {
        ModSettings.Changed -= OnSettingChanged;
        _hotkey?.Dispose();
        _injection?.Remove();
    }

    private void OnSettingChanged(string group, string key, object value)
    {
        if (group != OwnGroup || key != ToggleKeySetting)
            return;
        BindHotkey();
        PaintHint();
    }

    private void BindHotkey()
    {
        var name = ModSettings.TryGet<string>(OwnGroup, ToggleKeySetting, out var v) ? v : null;
        _toggleKey = Enum.TryParse<KeyCode>(name, out var key) ? key : KeyCode.F9;
        _hotkey?.Dispose();
        _hotkey = Hotkeys.OnKeyDown(_toggleKey, Toggle);
    }

    public void Toggle() => SetOpen(!_open);

    private void SetOpen(bool open)
    {
        _open = open;
        if (_overlay == null)
            return;
        if (open)
            Render();
        _overlay.SetVisible(open);
    }

    private VisualElement Build()
    {
        var overlay = new VisualElement { name = "MenaceModSettingsOverlay" };
        overlay.style.position = new StyleEnum<Position>(Position.Absolute);
        overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = new StyleLength(0f);
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
        overlay.style.justifyContent = new StyleEnum<Justify>(Justify.Center);
        overlay.style.alignItems = new StyleEnum<Align>(Align.Center);
        overlay.pickingMode = PickingMode.Position;

        var window = new Flyout();
        window.Root.style.width = new StyleLength(820f);
        window.Root.style.maxHeight = new StyleLength(Length.Percent(85f));
        window.Root.style.overflow = new StyleEnum<Overflow>(Overflow.Hidden);
        window.Content.style.paddingLeft = window.Content.style.paddingRight = new StyleLength(18f);
        window.Content.style.paddingTop = window.Content.style.paddingBottom = new StyleLength(14f);
        window.Content.style.flexGrow = new StyleFloat(1f);
        window.Content.style.flexShrink = new StyleFloat(1f);
        window.Content.style.minHeight = new StyleLength(0f);
        overlay.Add(window.Root);
        _window = window.Root;
        WatchWindowHeight(overlay, window.Root);

        var header = Row();
        var title = SettingRows.Text(Locale.Text("MenaceModSettings::ui/title", "MOD SETTINGS"), 20f);
        title.style.flexGrow = new StyleFloat(1f);
        header.Add(title);
        header.Add(new TextButton(Locale.Text("MenaceModSettings::ui/reset_group", "RESET GROUP")).OnClick(ResetSelected).Root);
        header.Add(new TextButton(Locale.Text("MenaceModSettings::ui/close", "CLOSE")).OnClick(() => SetOpen(false)).Root);
        window.Content.Add(header);

        var body = Row();
        body.style.flexGrow = new StyleFloat(1f);
        body.style.flexShrink = new StyleFloat(1f);
        body.style.minHeight = new StyleLength(0f);
        body.style.marginTop = new StyleLength(10f);
        body.style.alignItems = new StyleEnum<Align>(Align.Stretch);
        _tabs = new VisualElement();
        _tabs.style.width = new StyleLength(200f);
        _tabs.style.marginRight = new StyleLength(14f);
        body.Add(_tabs);
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = new StyleFloat(1f);
        scroll.style.flexShrink = new StyleFloat(1f);
        scroll.style.minHeight = new StyleLength(0f);
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        scroll.contentContainer.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);
        scroll.contentContainer.style.flexGrow = new StyleFloat(1f);
        StyleScroller(scroll.verticalScroller);
        _rows = scroll;
        body.Add(scroll);
        window.Content.Add(body);

        _hint = SettingRows.Text(string.Empty, 11f);
        _hint.style.opacity = new StyleFloat(0.7f);
        _hint.style.marginTop = new StyleLength(10f);
        _hint.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
        PaintHint();
        window.Content.Add(_hint);
        return overlay;
    }

    // A percent max-height is not enough to make the ScrollView scroll when a group has
    // many rows: the frame keeps growing with its content and runs off screen. Measure the
    // frame after layout and pin its height to the limit only when it overflows, so short
    // groups keep an auto-sized window.
    private void WatchWindowHeight(VisualElement overlay, VisualElement frame)
    {
        try
        {
            _onWindowResized = DelegateSupport.ConvertDelegate<EventCallback<GeometryChangedEvent>>(
                (Action<GeometryChangedEvent>)(_ => ClampWindowHeight(overlay, frame)));
            frame.RegisterCallback<GeometryChangedEvent>(_onWindowResized);
        }
        catch (Exception ex) { Log.Warn($"window height clamp not available: {ex.Message}"); }
    }

    private static void ClampWindowHeight(VisualElement overlay, VisualElement frame)
    {
        float limit = overlay.resolvedStyle.height * 0.85f;
        if (limit <= 0f || float.IsNaN(limit))
            return;
        if (frame.resolvedStyle.height > limit + 1f)
            frame.style.height = new StyleLength(limit);
    }

    // The game's panel has no default Unity theme, so an unstyled scroller shows as bright blocks.
    private void StyleScroller(Scroller scroller)
    {
        try
        {
            scroller.style.width = new StyleLength(8f);
            scroller.lowButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            scroller.highButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            scroller.slider.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            var dragger = scroller.slider.Q("unity-dragger", (string)null);
            if (dragger != null)
                dragger.style.backgroundColor = new StyleColor(new Color(0.78f, 0.70f, 0.50f, 0.6f));
        }
        catch (Exception ex) { Log.Debug($"scroller styling skipped: {ex.Message}"); }
    }

    private void PaintHint()
    {
        if (_hint != null)
            _hint.text = Locale.Format("MenaceModSettings::ui/hint",
                "Changes apply immediately and are saved to UserData/MenaceModSettings/settings.json. {0} toggles this window.", _toggleKey);
    }

    private void Bind(VisualElement element)
    {
        _overlay = element;
        element.SetVisible(_open);
        if (_open)
            Render();
        try
        {
            _onBackdropPress = DelegateSupport.ConvertDelegate<EventCallback<PointerDownEvent>>(
                (Action<PointerDownEvent>)(evt =>
                {
                    var target = evt.target != null ? evt.target.TryCast<VisualElement>() : null;
                    if (target != null && target.Pointer == element.Pointer)
                        SetOpen(false);
                }));
            element.RegisterCallback<PointerDownEvent>(_onBackdropPress);
        }
        catch (Exception ex) { Log.Warn($"backdrop close not available: {ex.Message}"); }
    }

    private void Render()
    {
        if (_tabs == null || _rows == null)
            return;
        var groups = ModSettings.AllGroups;
        if (groups.Count > 0 && (_selected == null || groups.All(g => g.Name != _selected)))
            _selected = groups[0].Name;

        _tabs.Clear();
        foreach (var group in groups)
        {
            var name = group.Name;
            var tab = new TextButton(name).WithStyle("old-text-button").OnClick(() => { _selected = name; Render(); });
            tab.Root.style.marginBottom = new StyleLength(4f);
            tab.Root.style.opacity = new StyleFloat(name == _selected ? 1f : 0.6f);
            _tabs.Add(tab.Root);
        }

        _rows.Clear();
        if (_window != null)
            _window.style.height = new StyleLength(StyleKeyword.Auto);
        var current = groups.FirstOrDefault(g => g.Name == _selected);
        if (current == null)
        {
            _rows.Add(SettingRows.Text(Locale.Text("MenaceModSettings::ui/empty", "No mod has registered settings."), 14f));
            return;
        }
        foreach (var def in current.Definitions)
            _rows.Add(SettingRows.Build(current.Name, def));
    }

    private void ResetSelected()
    {
        if (_selected == null)
            return;
        ModSettings.ResetGroup(_selected);
        Render();
    }

    private static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        row.style.alignItems = new StyleEnum<Align>(Align.Center);
        return row;
    }
}
