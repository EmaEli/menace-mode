using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jiangyu.Sdk;
using Jiangyu.Game.Input;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Conversations;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;
using UnityEngine;

namespace SquaddieFieldPromotions;

/// <summary>
/// Jiangyu 0.1 research prototype. NO vanilla save loading support, no dossier,
/// no permanent promotions, no pool mutations. Testing is opt-in and disposable.
/// This uses Jiangyu's proven clone implementation via a strictly checked,
/// experimental reflection bridge until Jiangyu exposes a public runtime API.
/// </summary>
public sealed class PromotionLabSystem : JiangyuSystem
{
    private const string SafeBaselineId = "mod.squaddiefieldpromotions.control";
    private const string SourceLeaderId = "squad_leader.sachin";
    private const string Prefix = "mod.squaddiefieldpromotions.";
    private const string UnsafeMarker = "ENABLE_UNSAFE_LAB_TEST.txt";
    private readonly List<UnityEngine.Object> _pinned = new();
    private int _createdCount;

    // Vanilla order: Agility, WeaponSkill, Valour, Toughness, Vitality, Precision, Positioning.
    // Competent entry-level squad leader, not a faceless recruit with dreadful stats.
    internal static readonly byte[] DefaultAttributes = { 65, 65, 62, 50, 50, 5, 45 };

    public override void OnInit()
    {
        // Jiangyu owns keyboard polling. No per-frame OnUpdate, no passive log spam.
        Hotkeys.OnKeyDown(KeyCode.F8, Inspect);
        Hotkeys.OnKeyDown(KeyCode.F9, GenerateTrial);
    }

    public override void OnUnload()
    {
        // Never destroy clones while the game might hold their pointers.
        // Jiangyu terminates the hotkey registrations on unload.
    }

    private static StrategyState? CurrentState()
    {
        try { return StrategyState.Get(); }
        catch { return null; }
    }

    private void Inspect()
    {
        StrategyState? state = CurrentState();
        if (state == null || state.Roster == null)
        {
            Log.Warn("[SFP 0.1] Open a disposable campaign in the strategy layer first.");
            return;
        }

        try
        {
            UnitLeaderTemplate? baseTemplate = DataTemplateLoader.Get<UnitLeaderTemplate>(SourceLeaderId, false);
            UnitLeaderTemplate? control = DataTemplateLoader.Get<UnitLeaderTemplate>(SafeBaselineId, false);
            if (baseTemplate == null || control == null)
            {
                Log.Warn("[SFP 0.1] Jiangyu control clone not found. Compile/deploy the templates/*.kdl file.");
                return;
            }

            var bridge = new JiangyuCloneBridge();
            Log.Info($"[SFP 0.1] PRECHECK OK: vanilla={baseTemplate.GetID()}, " +
                     $"JiangyuControl={control.GetID()}, dynamicBridge=ready, " +
                     $"hirable={state.Roster.GetHirableLeaders().Count}. " +
                     "F9 requires ENABLE_UNSAFE_LAB_TEST.txt in the mod folder.");
        }
        catch (Exception ex)
        {
            Log.Warn($"[SFP 0.1] PRECHECK BLOCKED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void GenerateTrial()
    {
        if (!File.Exists(Path.Combine(Context.ModFolder, UnsafeMarker)))
        {
            Log.Warn("[SFP 0.1] F9 disabled. Create ENABLE_UNSAFE_LAB_TEST.txt in the deployed mod folder, then retry on a disposable campaign.");
            return;
        }

        StrategyState? state = CurrentState();
        if (state == null || state.Roster == null || state.Squaddies == null)
        {
            Log.Warn("[SFP 0.1] No current strategy roster. F9 does nothing.");
            return;
        }

        UnitLeaderTemplate? leader = null;
        SpeakerTemplate? speaker = null;
        try
        {
            UnitLeaderTemplate? source = DataTemplateLoader.Get<UnitLeaderTemplate>(SourceLeaderId, false);
            if (source == null || source.SpeakerTemplate == null)
                throw new InvalidOperationException("Required vanilla Sachin leader/speaker unavailable.");
            // Force maps/arrays to exist BEFORE inserting a dynamic clone.
            DataTemplateLoader.GetAll<UnitLeaderTemplate>();
            DataTemplateLoader.GetAll<SpeakerTemplate>();
            Squaddie? soldier = FindUnusedSquaddie(state);
            if (soldier == null)
                throw new InvalidOperationException("No living, currently unassigned squaddie available.");

            // These GUID-shaped IDs are generated only on demand; no preallocated slots.
            string token = Guid.NewGuid().ToString("N");
            string leaderId = Prefix + "leader." + token;
            string speakerId = Prefix + "speaker." + token;
            int numericGuid = FindUnusedLeaderGuid(state);

            var bridge = new JiangyuCloneBridge();
            // Jiangyu's internal clone helper sets the non-serialized m_ID using
            // native IL2CPP reflection and updates BOTH m_TemplateMaps and
            // m_TemplateArrays, unlike the previous map-only research prototype.
            speaker = bridge.Clone<SpeakerTemplate>(source.SpeakerTemplate, speakerId);
            leader = bridge.Clone<UnitLeaderTemplate>(source, leaderId);
            _pinned.Add(speaker);
            _pinned.Add(leader);

            string name = string.IsNullOrWhiteSpace(soldier.Name) ? "Squaddie" : soldier.Name.Trim();
            string nickname = string.IsNullOrWhiteSpace(soldier.Nickname) ? "Field" : soldier.Nickname.Trim();
            int xp = ReadOptionalVeterancyXp(soldier.GetId());
            byte[] attrs = BuildInitialAttributes(xp);

            speaker.Forename = NewLine(source.SpeakerTemplate.Forename, speakerId, "Forename", name);
            speaker.Surname = NewLine(source.SpeakerTemplate.Surname, speakerId, "Surname", string.Empty);
            speaker.Nickname = NewLine(source.SpeakerTemplate.Nickname, speakerId, "Nickname", nickname);
            leader.SpeakerTemplate = speaker;
            leader.UnitTitle = NewLine(source.UnitTitle, leaderId, "UnitTitle", "Independent Squad (test)");
            leader.Gender = soldier.GetGender();
            leader.SkinColor = soldier.GetSkinColor();
            leader.QualityLevel = 1;
            leader.HiringCosts = 0;

            // Replace the array, do not modify an array possibly shared with vanilla.
            leader.InitialAttributes = new Il2CppStructArray<byte>(attrs);
            if (leader.InitialAttributes == null || leader.InitialAttributes.Length != attrs.Length)
                throw new InvalidOperationException("InitialAttributes replacement failed.");
            for (int i = 0; i < attrs.Length; i++)
                if (leader.InitialAttributes[i] != attrs[i])
                    throw new InvalidOperationException("InitialAttributes readback mismatch.");

            if (!PrivateMember.Set(leader, "m_Guid", numericGuid) || leader.GetGuid() != numericGuid)
                throw new InvalidOperationException("Unable to assign distinct m_Guid.");
            // Prevent cached vanilla string collections from shadowing fresh identity.
            PrivateMember.Set(speaker, "m_LocalizedStrings", null);
            PrivateMember.Set(leader, "m_LocalizedStrings", null);

            // Validate identity BEFORE touching the loader's template caches.
            if (!string.Equals(leader.GetID(), leaderId, StringComparison.Ordinal) ||
                !string.Equals(speaker.GetID(), speakerId, StringComparison.Ordinal))
                throw new InvalidOperationException("Cloned template ID mismatch.");
            if (DataTemplateLoader.Get<UnitLeaderTemplate>(leaderId, false) != null ||
                DataTemplateLoader.Get<SpeakerTemplate>(speakerId, false) != null)
                throw new InvalidOperationException("Generated ID collides with an existing template.");

            bridge.Register<SpeakerTemplate>(speaker, speakerId);
            bridge.Register<UnitLeaderTemplate>(leader, leaderId);
            if (DataTemplateLoader.Get<SpeakerTemplate>(speakerId, false) == null ||
                DataTemplateLoader.Get<UnitLeaderTemplate>(leaderId, false) == null)
                throw new InvalidOperationException("Dynamic registration did not resolve by ID.");

            // ADD TO HIRE only after the two native template registrations pass.
            // The source squaddie is deliberately untouched for this test.
            state.Roster.AddHirableLeader(leader);
            _createdCount++;
            Log.Info($"[SFP 0.1] TRIAL CREATED #{_createdCount}: {name} '{nickname}', " +
                     $"squaddie={soldier.GetId()}, XP={(xp >= 0 ? xp.ToString() : "unavailable; fallback stats")}, " +
                     $"leaderId={leaderId}, guid={numericGuid}. Open Hire. DO NOT SAVE.");
        }
        catch (Exception ex)
        {
            // Partial native registrations cannot be safely removed mid-session.
            // Pinned clones must outlive all potential game references.
            if (speaker != null && !_pinned.Contains(speaker)) _pinned.Add(speaker);
            if (leader != null && !_pinned.Contains(leader)) _pinned.Add(leader);
            Log.Warn($"[SFP 0.1] TRIAL FAILED (no Hire change intended): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Squaddie? FindUnusedSquaddie(StrategyState state)
    {
        var alive = state.Squaddies.GetAllAlive();
        if (alive == null) return null;
        for (int i = 0; i < alive.Count; i++)
        {
            Squaddie? s = alive[i];
            if (s == null || s.GetId() <= 0 || s.GetCurrentLeader() != null) continue;
            return s;
        }
        return null;
    }

    private static int ReadOptionalVeterancyXp(int squaddieId)
    {
        try
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? api = assembly.GetType("SquaddieProbe.CompatibilityApi", false);
                if (api == null) continue;
                MethodInfo? method = api.GetMethod("GetSquaddieXp", BindingFlags.Public | BindingFlags.Static);
                if (method == null || method.GetParameters().Length != 1) continue;
                object? result = method.Invoke(null, new object[] { squaddieId });
                return result == null ? -1 : Convert.ToInt32(result);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[SFP 0.1] Optional Veterancy API failed: " + ex.GetType().Name);
        }
        return -1;
    }

    internal static byte[] BuildInitialAttributes(int xp)
    {
        byte[] attrs = (byte[])DefaultAttributes.Clone();
        if (xp < 0) return attrs;
        // Conservative conversion until balance testing with XP tiers is complete.
        int bonus = xp >= 500 ? 12 : xp >= 250 ? 10 : xp >= 150 ? 8 :
                    xp >= 100 ? 6 : xp >= 50 ? 4 : xp >= 25 ? 2 : 0;
        for (int i = 0; i < attrs.Length; i++)
            attrs[i] = (byte)Math.Min(85, attrs[i] + (i == 5 ? bonus / 2 : bonus));
        return attrs;
    }

    private static LocalizedLine NewLine(LocalizedLine original, string id, string field, string value)
    {
        if (original == null) throw new InvalidOperationException("Missing localized source: " + field);
        var line = new LocalizedLine(original.GetCategory(), field, false);
        line.SetId(id);
        line.SetDefaultTranslation(value);
        return line;
    }

    private static int FindUnusedLeaderGuid(StrategyState state)
    {
        var used = new HashSet<int>();
        // Fully enumerate current vanilla and mod leader templates where possible.
        try
        {
            object all = DataTemplateLoader.GetAll<UnitLeaderTemplate>();
            foreach (object item in PrivateMember.Enumerate(all))
                if (item is UnitLeaderTemplate t) used.Add(t.GetGuid());
        }
        catch { /* checked again against active candidate and hired lists below */ }
        var available = state.Roster.GetHirableLeaders();
        for (int i = 0; i < available.Count; i++)
            if (available[i] != null) used.Add(available[i].GetGuid());
        var hired = state.Roster.GetHiredLeaders();
        for (int i = 0; i < hired.Count; i++)
            if (hired[i]?.LeaderTemplate != null) used.Add(hired[i].LeaderTemplate.GetGuid());
        for (int i = 0; i < 32; i++)
        {
            // Positive signed int within game GUID type; random GUID is not an index.
            int candidate = (int)((uint)Guid.NewGuid().GetHashCode() & 0x7fffffff);
            if (candidate > 0 && !used.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not allocate a unique numeric leader GUID.");
    }
}

internal static class PrivateMember
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static bool Set(object obj, string name, object? value)
    {
        for (Type? t = obj.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo? field = t.GetField(name, Flags);
                if (field != null && !field.IsInitOnly)
                {
                    field.SetValue(obj, value);
                    return true;
                }
                PropertyInfo? property = t.GetProperty(name, Flags);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(obj, value);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    internal static IEnumerable<object> Enumerate(object collection)
    {
        if (collection is IEnumerable native)
        {
            foreach (object? entry in native)
                if (entry != null) yield return entry;
            yield break;
        }
        MethodInfo? get = collection.GetType().GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);
        object? enumerator = get?.Invoke(collection, null);
        if (enumerator == null) yield break;
        MethodInfo? next = enumerator.GetType().GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo? current = enumerator.GetType().GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
        if (next == null || current == null) yield break;
        while (Convert.ToBoolean(next.Invoke(enumerator, null)))
        {
            object? value = current.GetValue(enumerator);
            if (value != null) yield return value;
        }
    }
}
