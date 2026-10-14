using System.Globalization;
using Jiangyu.Game.Ui;
using Jiangyu.Game.Ui.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace MenaceModSettings;

/// <summary>
/// Builds one row per <see cref="SettingDefinition"/>. Every control is driven by button clicks
/// (ON/OFF, minus/plus, previous/next) so the window relies only on the SDK's native
/// <see cref="TextButton"/> and never on IL2CPP generic value-change callbacks.
/// </summary>
internal static class SettingRows
{
    public static VisualElement Build(string group, SettingDefinition def)
    {
        if (def.Kind == SettingKind.Header)
            return Header(def.Label);

        var row = new VisualElement();
        row.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        row.style.alignItems = new StyleEnum<Align>(Align.Center);
        row.style.minHeight = new StyleLength(30f);
        row.style.marginBottom = new StyleLength(4f);

        var label = new Label(def.Label ?? def.Key);
        label.style.flexGrow = new StyleFloat(1f);
        label.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
        label.style.fontSize = new StyleLength(14f);
        row.Add(label);

        var controls = new VisualElement();
        controls.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        controls.style.alignItems = new StyleEnum<Align>(Align.Center);
        controls.style.flexShrink = new StyleFloat(0f);
        row.Add(controls);

        switch (def.Kind)
        {
            case SettingKind.Toggle: Toggle(controls, group, def); break;
            case SettingKind.Slider:
            case SettingKind.Number: Stepper(controls, group, def); break;
            case SettingKind.Dropdown: Cycle(controls, group, def); break;
        }
        return row;
    }

    private static VisualElement Header(string title)
    {
        var header = new Label((title ?? string.Empty).ToUpperInvariant());
        header.style.fontSize = new StyleLength(13f);
        header.style.color = new StyleColor(new Color(0.78f, 0.70f, 0.50f));
        header.style.marginTop = new StyleLength(12f);
        header.style.marginBottom = new StyleLength(6f);
        header.style.borderBottomWidth = new StyleFloat(1f);
        header.style.borderBottomColor = new StyleColor(new Color(0.78f, 0.70f, 0.50f, 0.4f));
        return header;
    }

    private static void Toggle(VisualElement host, string group, SettingDefinition def)
    {
        var button = Button(string.Empty, 90f);
        void Paint()
        {
            var on = ModSettings.TryGet<bool>(group, def.Key, out var v) && v;
            button.Label.text = on ? "ON" : "OFF";
            button.Root.style.opacity = new StyleFloat(on ? 1f : 0.6f);
        }
        button.OnClick(() =>
        {
            var on = ModSettings.TryGet<bool>(group, def.Key, out var v) && v;
            ModSettings.Set(group, def.Key, !on);
            Paint();
        });
        Paint();
        host.Add(button.Root);
    }

    private static void Stepper(VisualElement host, string group, SettingDefinition def)
    {
        var isInt = def.Kind == SettingKind.Number;
        var format = isInt || def.Step >= 1f ? "0" : def.Step >= 0.1f ? "0.0" : "0.00";
        var value = ValueLabel(90f);
        void Paint()
        {
            ModSettings.TryGet<float>(group, def.Key, out var v);
            value.text = v.ToString(format, CultureInfo.InvariantCulture);
        }
        void Nudge(float direction)
        {
            ModSettings.TryGet<float>(group, def.Key, out var v);
            var next = (float)Math.Round(v + direction * def.Step, 4);
            ModSettings.Set(group, def.Key, isInt ? (object)(int)Math.Round(next) : next);
            Paint();
        }
        host.Add(Button("-", 36f).OnClick(() => Nudge(-1f)).Root);
        host.Add(value);
        host.Add(Button("+", 36f).OnClick(() => Nudge(1f)).Root);
        Paint();
    }

    private static void Cycle(VisualElement host, string group, SettingDefinition def)
    {
        var value = ValueLabel(170f);
        void Paint()
        {
            ModSettings.TryGet<string>(group, def.Key, out var v);
            value.text = v ?? string.Empty;
        }
        void Step(int direction)
        {
            ModSettings.TryGet<string>(group, def.Key, out var v);
            var i = Math.Max(0, Array.IndexOf(def.Options, v));
            var n = def.Options.Length;
            ModSettings.Set(group, def.Key, def.Options[((i + direction) % n + n) % n]);
            Paint();
        }
        host.Add(Button("<", 36f).OnClick(() => Step(-1)).Root);
        host.Add(value);
        host.Add(Button(">", 36f).OnClick(() => Step(1)).Root);
        Paint();
    }

    private static TextButton Button(string text, float width)
    {
        var button = new TextButton(text);
        button.Root.style.width = new StyleLength(width);
        button.Root.style.minWidth = new StyleLength(width);
        button.Root.style.height = new StyleLength(26f);
        button.Root.style.marginLeft = new StyleLength(2f);
        button.Root.style.marginRight = new StyleLength(2f);
        return button;
    }

    private static Label ValueLabel(float width)
    {
        var label = new Label();
        label.style.width = new StyleLength(width);
        label.style.fontSize = new StyleLength(14f);
        label.CenterText();
        return label;
    }
}
