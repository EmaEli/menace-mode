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
/// registered group and one row per setting. Toggled with F10, closed by the CLOSE button
/// or a click on the dimmed backdrop. Injected once; the loader re-applies it per screen.
/// </summary>
public sealed class SettingsWindowSystem : JiangyuSystem
{
    public const KeyCode ToggleKey = KeyCode.F10;

    private UiInjection _injection;
    private IDisposable _hotkey;
    private VisualElement _overlay;
    private VisualElement _tabs;
    private VisualElement _rows;
    private EventCallback<PointerDownEvent> _onBackdropPress;
    private bool _open;
    private string _selected;

    public override void OnInit()
    {
        _injection = UI.Inject(UiTarget.Overlay(), Build, Bind);
        _hotkey = Hotkeys.OnKeyDown(ToggleKey, Toggle);
        Log.Debug($"settings window on {ToggleKey}");
    }

    public override void OnUnload()
    {
        _hotkey?.Dispose();
        _injection?.Remove();
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
        window.Content.style.paddingLeft = window.Content.style.paddingRight = new StyleLength(18f);
        window.Content.style.paddingTop = window.Content.style.paddingBottom = new StyleLength(14f);
        window.Content.style.flexGrow = new StyleFloat(1f);
        overlay.Add(window.Root);

        var header = Row();
        var title = new Label(Locale.Text("MenaceModSettings::ui/title", "MOD SETTINGS"));
        title.style.fontSize = new StyleLength(20f);
        title.style.flexGrow = new StyleFloat(1f);
        header.Add(title);
        header.Add(new TextButton(Locale.Text("MenaceModSettings::ui/reset_group", "RESET GROUP")).OnClick(ResetSelected).Root);
        header.Add(new TextButton(Locale.Text("MenaceModSettings::ui/close", "CLOSE")).OnClick(() => SetOpen(false)).Root);
        window.Content.Add(header);

        var body = Row();
        body.style.flexGrow = new StyleFloat(1f);
        body.style.marginTop = new StyleLength(10f);
        body.style.alignItems = new StyleEnum<Align>(Align.Stretch);
        _tabs = new VisualElement();
        _tabs.style.width = new StyleLength(200f);
        _tabs.style.marginRight = new StyleLength(14f);
        body.Add(_tabs);
        var scroll = new ScrollView();
        scroll.style.flexGrow = new StyleFloat(1f);
        _rows = scroll;
        body.Add(scroll);
        window.Content.Add(body);

        var hint = new Label(Locale.Format("MenaceModSettings::ui/hint",
            "Changes apply immediately and are saved to UserData/MenaceModSettings/settings.json. {0} toggles this window.", ToggleKey));
        hint.style.fontSize = new StyleLength(11f);
        hint.style.opacity = new StyleFloat(0.7f);
        hint.style.marginTop = new StyleLength(10f);
        hint.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
        window.Content.Add(hint);
        return overlay;
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
        var current = groups.FirstOrDefault(g => g.Name == _selected);
        if (current == null)
        {
            _rows.Add(new Label(Locale.Text("MenaceModSettings::ui/empty", "No mod has registered settings.")));
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
