using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SquaddieProbe;

// Display-only reflection adapter. Never reads GetID/m_ID, mutates a template, or caches
// native objects. Kept independent of IL2CPP so tests execute this exact resolver offline.
internal static class LeaderDisplayName
{
    internal const string Unknown = "Unknown Squad Leader";
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Regex GuidToken = new(@"[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    public static string Resolve(object leader)
    {
        string? nickname = Text(leader, "GetNickname", "Nickname", "m_Nickname");
        if (nickname != null) return nickname;

        object? speaker = First(leader, "GetSpeakerTemplate", "SpeakerTemplate", "m_SpeakerTemplate");
        string? speakerName = SpeakerName(speaker);
        if (speakerName != null) return speakerName;

        string? direct = Text(leader, "GetDisplayName", "DisplayName", "m_DisplayName", "GetName", "Name", "m_Name");
        if (direct != null) return direct;

        object? template = First(leader, "LeaderTemplate", "UnitLeaderTemplate", "m_LeaderTemplate",
            "m_UnitLeaderTemplate", "Template", "m_Template", "GetLeaderTemplate", "GetUnitLeaderTemplate", "GetTemplate");
        if (template == null) return Unknown;

        return SpeakerName(First(template, "SpeakerTemplate", "GetSpeakerTemplate"))
            ?? Text(template, "Nickname", "GetNickname", "DisplayName", "GetDisplayName", "UnitTitle", "GetUnitTitle")
            ?? Unknown;
    }

    private static string? SpeakerName(object? speaker)
    {
        if (speaker == null) return null;
        string? text = Text(speaker, "Nickname", "GetNickname", "FullName");
        if (text != null) return text;
        string fullName = ((Text(speaker, "Forename") ?? "") + " " + (Text(speaker, "Surname") ?? "")).Trim();
        return fullName.Length > 0 ? fullName : Text(speaker, "Title");
    }

    private static string? Text(object target, params string[] names)
    {
        foreach (object value in Values(target, names))
        {
            try
            {
                // Only native localized strings have a meaningful non-string ToString.
                // Unity objects/proxy type names must never be used as a player's name.
                bool localized = false;
                for (Type? type = value.GetType(); type != null; type = type.BaseType)
                    if (type.Name == "BaseLocalizedString" || type.Name == "LocalizedLine") localized = true;
                if (value is not string && !localized) continue;
                string? text = PlayerText(value.ToString());
                if (text != null) return text;
            }
            catch { /* A broken localized field must not prevent the next fallback. */ }
        }
        return null;
    }

    private static string? PlayerText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string text = value!.Trim();
        if (GuidToken.IsMatch(text) || text.StartsWith("mod.", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("BaseUnitLeader", StringComparison.OrdinalIgnoreCase)
            || text.Contains("UnitLeaderTemplate", StringComparison.OrdinalIgnoreCase)
            || text.Contains("SpeakerTemplate", StringComparison.OrdinalIgnoreCase)) return null;
        // Preserve the actual localized casing, punctuation, accents and spacing.
        return text;
    }

    private static object? First(object target, params string[] names)
    {
        foreach (object value in Values(target, names)) return value;
        return null;
    }

    private static IEnumerable<object> Values(object target, string[] names)
    {
        Type type = target.GetType();
        foreach (string name in names)
        {
            object? value = null;
            try
            {
                MethodInfo? method = type.GetMethod(name, Members, null, Type.EmptyTypes, null);
                PropertyInfo? property = type.GetProperty(name, Members);
                if (method != null) value = method.Invoke(target, null);
                else if (property != null && property.GetIndexParameters().Length == 0) value = property.GetValue(target);
                else value = type.GetField(name, Members)?.GetValue(target);
            }
            catch { /* Missing/throwing native members are optional display sources. */ }
            if (value != null) yield return value;
        }
    }
}