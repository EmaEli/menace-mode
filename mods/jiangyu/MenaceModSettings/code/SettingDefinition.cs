using System.Collections.Generic;
using System.Globalization;

namespace MenaceModSettings;

public enum SettingKind
{
    Header,
    Toggle,
    Slider,
    Number,
    Dropdown,
}

/// <summary>One entry declared by a mod through <see cref="SettingsBuilder"/>.</summary>
public sealed class SettingDefinition
{
    public SettingKind Kind { get; init; }
    public string Key { get; init; }
    public string Label { get; init; }
    public object Default { get; init; }
    public float Min { get; init; }
    public float Max { get; init; }
    public float Step { get; init; }
    public string[] Options { get; init; }

    /// <summary>Clamp, snap and type-coerce a raw value (user input or JSON) to this definition.</summary>
    public object Coerce(object raw)
    {
        switch (Kind)
        {
            case SettingKind.Toggle:
                return raw switch
                {
                    bool b => b,
                    string s when bool.TryParse(s, out var b) => b,
                    _ => ToDouble(raw, (bool)Default ? 1 : 0) != 0,
                };
            case SettingKind.Slider:
            {
                var v = (float)ToDouble(raw, (float)Default);
                return Math.Clamp(v, Min, Max);
            }
            case SettingKind.Number:
            {
                var v = (int)Math.Round(ToDouble(raw, (int)Default));
                return Math.Clamp(v, (int)Min, (int)Max);
            }
            case SettingKind.Dropdown:
            {
                var s = raw?.ToString();
                return s != null && Array.IndexOf(Options, s) >= 0 ? s : (string)Default;
            }
            default:
                return raw;
        }
    }

    private static double ToDouble(object raw, double fallback)
    {
        switch (raw)
        {
            case null: return fallback;
            case double d: return d;
            case float f: return f;
            case int i: return i;
            case long l: return l;
            case bool b: return b ? 1 : 0;
            case string s:
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : fallback;
            default:
                try { return Convert.ToDouble(raw, CultureInfo.InvariantCulture); }
                catch { return fallback; }
        }
    }
}

/// <summary>Collects the definitions of one settings group, in declaration order.</summary>
public sealed class SettingsBuilder
{
    internal readonly List<SettingDefinition> Items = new();

    public SettingsBuilder AddHeader(string title)
    {
        Items.Add(new SettingDefinition { Kind = SettingKind.Header, Label = title });
        return this;
    }

    public SettingsBuilder AddToggle(string key, string label, bool defaultValue)
    {
        Items.Add(new SettingDefinition { Kind = SettingKind.Toggle, Key = key, Label = label, Default = defaultValue });
        return this;
    }

    public SettingsBuilder AddSlider(string key, string label, float min, float max, float defaultValue, float step = 0f)
    {
        Items.Add(new SettingDefinition
        {
            Kind = SettingKind.Slider, Key = key, Label = label,
            Min = min, Max = max, Default = Math.Clamp(defaultValue, min, max),
            Step = step > 0f ? step : NiceStep(max - min),
        });
        return this;
    }

    public SettingsBuilder AddNumber(string key, string label, int min, int max, int defaultValue)
    {
        Items.Add(new SettingDefinition
        {
            Kind = SettingKind.Number, Key = key, Label = label,
            Min = min, Max = max, Default = Math.Clamp(defaultValue, min, max), Step = 1f,
        });
        return this;
    }

    public SettingsBuilder AddDropdown(string key, string label, string[] options, string defaultValue)
    {
        var opts = options is { Length: > 0 } ? options : new[] { defaultValue ?? string.Empty };
        Items.Add(new SettingDefinition
        {
            Kind = SettingKind.Dropdown, Key = key, Label = label, Options = opts,
            Default = Array.IndexOf(opts, defaultValue) >= 0 ? defaultValue : opts[0],
        });
        return this;
    }

    // A step that divides the range into roughly 20 to 50 clicks and reads as a round number.
    private static float NiceStep(float range)
    {
        if (range <= 0f) return 1f;
        var raw = range / 40.0;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var norm = raw / mag;
        var nice = norm < 1.5 ? 1 : norm < 3.5 ? 2 : norm < 7.5 ? 5 : 10;
        return (float)(nice * mag);
    }
}
