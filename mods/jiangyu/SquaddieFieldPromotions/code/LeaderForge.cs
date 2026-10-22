using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMenace.Conversations;
using Il2CppMenace.States;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;

namespace SquaddieFieldPromotions;

/// <summary>
/// Builds the native UnitLeaderTemplate/SpeakerTemplate pair described by a PromotionRecord
/// (verified 0.6.1 lab path: Jiangyu clone bridge, bundled portraits, lettered badge, John/Jane
/// voices) and registers it in DataTemplateLoader. Install additionally offers the leader in
/// Hire; Rehydrate only recreates templates so a loading roster can resolve them.
/// </summary>
internal sealed class LeaderForge
{
    private const string SourceLeaderId = "squad_leader.sachin";
    private readonly string _modFolder;
    private readonly List<UnityEngine.Object> _pinned = new();
    private StandalonePortraitAssets _maleArt;
    private StandalonePortraitAssets _femaleArt;
    private readonly Dictionary<string, StandalonePortraitAssets> _portraitProfileArt =
        new(StringComparer.Ordinal);
    private FieldVoiceSystem _voices;
    // Leaders created before John/Jane audio was ready keep the native donor voice until bound.
    private readonly Dictionary<string, (UnitLeaderTemplate Leader, SpeakerTemplate Speaker, bool Female, int PitchPreset, string VoiceSetId)> _pendingVoice =
        new(StringComparer.Ordinal);
    private PromotionVoiceCatalog _voiceCatalog;
    private bool _voiceCatalogLoaded;

    internal LeaderForge(string modFolder) => _modFolder = modFolder;

    internal bool VoicesReady => _voices != null && _voices.Ready;
    internal bool HasPendingVoices => _pendingVoice.Count > 0;
    internal bool AssetsPresent =>
        StandalonePortraitAssets.ArePresent(_modFolder, "male") && StandalonePortraitAssets.ArePresent(_modFolder, "female") &&
        StandalonePortraitAssets.ArePortraitProfilesPresent(_modFolder);

    internal void Unload() => _voices?.UnpatchOnUnload();

    internal static PromotionRecord CreateRecord(StrategyState state, Selection selection, int badgeIndex,
        SquaddieServiceRecord service, string sourceLeaderTemplateId = "")
    {
        var soldier = selection.Soldier;
        string token = Guid.NewGuid().ToString("N");
        var record = new PromotionRecord
        {
            LeaderId = PromotionRecord.LeaderPrefix + token,
            SpeakerId = PromotionRecord.SpeakerPrefix + token,
            NumericGuid = FindUnusedLeaderGuid(state),
            SourceSquaddieId = soldier.Id,
            Name = Clean(soldier.Name, "Squaddie"),
            Nickname = Clean(soldier.Nickname, "Field"),
            Gender = soldier.Gender,
            SkinColor = soldier.SkinColor,
            BadgeIndex = badgeIndex,
            VoicePitchPreset = VoicePitchProfiles.ForBadge(badgeIndex),
            InitialAttributes = PromotionRules.Attributes(selection.UsesXp ? soldier.Xp : null),
        };
        PromotionIdentity.Populate(record, soldier, service, sourceLeaderTemplateId);
        PerkGeneration.Generate(record, selection.UsesXp ? soldier.Xp : null, ReadMentor(state, soldier.Id, service));
        record.Validate();
        return record;
    }

    // The mentor is optional flavour: an unreadable roster falls back to the base pool.
    private static MentorSnapshot ReadMentor(StrategyState state, int soldierId, SquaddieServiceRecord service)
    {
        try { return MentorPerks.Read(state, soldierId, service); }
        catch { return null; }
    }

    private static string Clean(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var chars = new List<char>();
        foreach (char c in value.Trim())
            if (!char.IsControl(c) && chars.Count < 160) chars.Add(c);
        return chars.Count == 0 ? fallback : new string(chars.ToArray());
    }

    /// <summary>Creates (or validates an existing) template pair and offers it in Hire.</summary>
    internal string Install(StrategyState state, PromotionRecord record, bool forceGenericHead)
    {
        record.Validate();
        if (state == null || state.Roster == null) throw new InvalidOperationException("Roster unavailable.");
        if (RosterContainsLeader(state, record.LeaderId) || RosterContainsGuid(state, record.NumericGuid))
            throw new InvalidOperationException("Generated leader identity already appears in the roster.");
        string result = Materialize(record, forceGenericHead);
        var leader = DataTemplateLoader.Get<UnitLeaderTemplate>(record.LeaderId, false)
            ?? throw new InvalidOperationException("Registered leader template lookup failed.");
        PromotionTemplateText.VerifyDisplay(record, leader);
        state.Roster.AddHirableLeader(leader);
        if (!RosterContainsLeader(state, record.LeaderId))
            throw new InvalidOperationException("Hire list readback failed after AddHirableLeader.");
        return result;
    }

    // Registered templates remain pinned, but a failed promotion must not leave an extra Hire entry.
    internal void RollbackInstall(StrategyState state, PromotionRecord record)
    {
        var offers = state.Roster.m_HirableLeaders
            ?? throw new InvalidOperationException("Hire list unavailable for rollback.");
        for (int i = offers.Count - 1; i >= 0; i--)
            if (offers[i]?.GetID() == record.LeaderId) offers.RemoveAt(i);
        _pendingVoice.Remove(record.LeaderId);
    }

    /// <summary>Recreates templates for a saved record before the native roster deserializes.</summary>
    internal string Rehydrate(PromotionRecord record, bool forceGenericHead)
    {
        record.Validate();
        return Materialize(record, forceGenericHead);
    }

    internal bool TryAssignVoiceSet(PromotionLedger ledger, PromotionRecord record) =>
        PromotionVoiceAssignment.TryAssignRotatingProfile(ledger, record, GetVoiceCatalog());

    internal int AssignMissingVoiceSets(PromotionLedger ledger) =>
        PromotionVoiceAssignment.AssignMissingProfiles(ledger, GetVoiceCatalog());

    private PromotionVoiceCatalog GetVoiceCatalog()
    {
        if (!_voiceCatalogLoaded)
        {
            _voiceCatalog = PromotionVoiceCatalog.Load(_modFolder);
            _voiceCatalogLoaded = true;
        }
        return _voiceCatalog;
    }

    internal bool SetHeadMode(PromotionRecord record, bool forceGenericHead)
    {
        var leader = DataTemplateLoader.Get<UnitLeaderTemplate>(record.LeaderId, false);
        if (leader == null) return false;
        if (forceGenericHead)
        {
            leader.CustomHead = null;
            return true;
        }

        var nativeDonor = DataTemplateLoader.Get<UnitLeaderTemplate>(
            record.Gender == Convert.ToInt32(Il2CppMenace.Tactical.Gender.Female) ? "squad_leader.sy" : "squad_leader.lim", false);
        if (nativeDonor == null) return false;
        var carda = DataTemplateLoader.Get<UnitLeaderTemplate>("squad_leader.carda", false);
        leader.CustomHead = record.Gender == Convert.ToInt32(Il2CppMenace.Tactical.Gender.Female) &&
            carda != null && carda.Gender == leader.Gender && carda.CustomHead != null
                ? carda.CustomHead
                : nativeDonor.CustomHead;
        return true;
    }

    /// <summary>
    /// One-shot John/Jane preparation. Binds every leader created while audio was not ready.
    /// Returns false with a reason when the native donor voice stays in use.
    /// </summary>
    internal bool TryPrepareVoices(out string status)
    {
        _voices ??= new FieldVoiceSystem(_modFolder);
        if (!_voices.TryPrepare(_pendingVoice.Values.Select(pending => pending.VoiceSetId), out status)) return false;
        foreach (var pending in _pendingVoice.Values)
            BindVoice(pending.Leader, pending.Speaker, pending.Female, pending.PitchPreset, pending.VoiceSetId);
        _pendingVoice.Clear();
        return true;
    }

    private string BindVoice(UnitLeaderTemplate leader, SpeakerTemplate speaker, bool female, int pitchPreset,
        string voiceSetId)
    {
        string tactical = _voices.BindSpeaker(speaker, female, pitchPreset, voiceSetId);
        string hiring;
        try
        {
            hiring = string.IsNullOrEmpty(voiceSetId)
                ? _voices.BindHiringBarks(leader, female, pitchPreset)
                : _voices.BindHiringBarks(leader, female, pitchPreset, voiceSetId);
        }
        catch (Exception error) { hiring = "native (" + error.GetType().Name + ")"; }
        string promotion;
        try
        {
            promotion = string.IsNullOrEmpty(voiceSetId)
                ? _voices.BindPromotionBarks(leader, female, pitchPreset)
                : _voices.BindPromotionBarks(leader, female, pitchPreset, voiceSetId);
        }
        catch (Exception error) { promotion = "native donor promotion bark (" + error.GetType().Name + ")"; }
        return tactical + "/" + hiring + "/" + promotion;
    }

    private static Il2CppReferenceArray<PromotionBarkSound> ClonePromotionBarkSounds(
        Il2CppReferenceArray<PromotionBarkSound> source)
    {
        if (source == null || source.Length == 0)
            throw new InvalidOperationException("Native gendered promotion bark sounds are missing.");
        var clone = new Il2CppReferenceArray<PromotionBarkSound>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            var bark = source[i];
            if (bark == null || bark.PerkTier < 1 || bark.PerkTier > 4)
                throw new InvalidOperationException("Native promotion bark has an invalid perk tier.");
            clone[i] = new PromotionBarkSound { PerkTier = bark.PerkTier, Sound = bark.Sound };
        }
        return clone;
    }

    private string Materialize(PromotionRecord record, bool forceGenericHead)
    {
        PromotionVoiceAssignment.ValidateAssignment(record, GetVoiceCatalog());
        var existing = DataTemplateLoader.Get<UnitLeaderTemplate>(record.LeaderId, false);
        if (existing != null)
        {
            // Same session (promote, save, load): the registered pair must be exactly this record.
            if (existing.GetGuid() != record.NumericGuid ||
                existing.SpeakerTemplate == null || existing.SpeakerTemplate.GetID() != record.SpeakerId ||
                Convert.ToInt32(existing.Gender) != record.Gender ||
                Convert.ToInt32(existing.SkinColor) != record.SkinColor)
                throw new InvalidOperationException("Native leader template ID collision / identity mismatch.");
            ApplyIdentityText(record, existing);
            ApplyProgression(record, existing);
            SetHeadMode(record, forceGenericHead);
            return "reuse=registered, badge=" + DynamicBadgeFactory.LabelForIndex(record.BadgeIndex);
        }
        if (DataTemplateLoader.Get<SpeakerTemplate>(record.SpeakerId, false) != null)
            throw new InvalidOperationException("Speaker ID already registered without a matching leader.");
        if (TemplateGuidConflicts(record.LeaderId, record.NumericGuid))
            throw new InvalidOperationException("Numeric leader GUID collides with a loaded leader template.");

        UnitLeaderTemplate leader = null;
        SpeakerTemplate speaker = null;
        try
        {
            var source = DataTemplateLoader.Get<UnitLeaderTemplate>(SourceLeaderId, false);
            var lim = DataTemplateLoader.Get<UnitLeaderTemplate>("squad_leader.lim", false);
            var sy = DataTemplateLoader.Get<UnitLeaderTemplate>("squad_leader.sy", false);
            if (source == null || source.SpeakerTemplate == null || lim == null || lim.SpeakerTemplate == null ||
                sy == null || sy.SpeakerTemplate == null || lim.Gender == sy.Gender)
                throw new InvalidOperationException("Native donor templates are missing.");
            if (record.Gender != Convert.ToInt32(lim.Gender) && record.Gender != Convert.ToInt32(sy.Gender))
                throw new InvalidOperationException("Snapshot gender is not a supported native value.");
            bool female = record.Gender == Convert.ToInt32(sy.Gender);
            var voiceDonor = female ? sy : lim;
            string gender = female ? "female" : "male";
            var art = female ? _femaleArt : _maleArt;
            if (art != null && !art.IsAlive) art = null;
            if (art == null)
            {
                art = StandalonePortraitAssets.Load(_modFolder, gender, _pinned);
                if (female) _femaleArt = art; else _maleArt = art;
            }
            DataTemplateLoader.GetAll<UnitLeaderTemplate>();
            DataTemplateLoader.GetAll<SpeakerTemplate>();
            var bridge = new JiangyuCloneBridge();
            speaker = bridge.Clone<SpeakerTemplate>(voiceDonor.SpeakerTemplate, record.SpeakerId);
            leader = bridge.Clone<UnitLeaderTemplate>(source, record.LeaderId);
            _pinned.Add(speaker);
            _pinned.Add(leader);
            speaker.Forename = NewLine(voiceDonor.SpeakerTemplate.Forename, record.SpeakerId, "Forename", record.Name);
            speaker.Surname = NewLine(voiceDonor.SpeakerTemplate.Surname, record.SpeakerId, "Surname", string.Empty);
            speaker.Nickname = NewLine(voiceDonor.SpeakerTemplate.Nickname, record.SpeakerId, "Nickname", record.Nickname);
            leader.SpeakerTemplate = speaker;
            // The cloned leader comes from Sachin, whose direct perk-promotion sounds
            // bypass SpeakerTemplate tags. Start from the matching gender donor instead.
            leader.PromotionBarkSounds = ClonePromotionBarkSounds(voiceDonor.PromotionBarkSounds);
            leader.HiringSelectBarkSound = voiceDonor.HiringSelectBarkSound;
            leader.HiredBarkSound = voiceDonor.HiredBarkSound;
            string voice;
            if (VoicesReady) voice = BindVoice(leader, speaker, female, record.VoicePitchPreset, record.VoiceSetId);
            else
            {
                _pendingVoice[record.LeaderId] = (leader, speaker, female, record.VoicePitchPreset, record.VoiceSetId);
                voice = "native-" + gender + " (pending John/Jane)";
            }
            ApplyIdentityText(record, leader, source, voiceDonor);
            leader.Gender = (Il2CppMenace.Tactical.Gender)record.Gender;
            leader.SkinColor = (Il2CppMenace.Tactical.SkinColor)record.SkinColor;
            string head = ApplyStandaloneVisuals(leader, speaker, voiceDonor, art, female, forceGenericHead);
            string visual = "embedded-" + gender;
            if (record.PortraitProfile.Length != 0)
            {
                if (!_portraitProfileArt.TryGetValue(record.PortraitProfile, out var portrait) ||
                    portrait == null || !portrait.IsAlive)
                {
                    portrait = StandalonePortraitAssets.LoadPortraitProfile(
                        _modFolder, record.PortraitProfile, _pinned);
                    _portraitProfileArt[record.PortraitProfile] = portrait;
                }
                // Only the six portrait fields change; default gender art still supplies dynamic badges.
                portrait.ApplyTo(leader, speaker);
                visual = "embedded-" + record.PortraitProfile;
            }
            string badgeLabel = DynamicBadgeFactory.LabelForIndex(record.BadgeIndex);
            string badge = DynamicBadgeFactory.Apply(leader, art, badgeLabel, _pinned);
            ApplyProgression(record, leader);
            leader.QualityLevel = 1;
            leader.HiringCosts = 0;
            leader.InitialAttributes = new Il2CppStructArray<byte>(record.InitialAttributes);
            if (leader.InitialAttributes == null || leader.InitialAttributes.Length != record.InitialAttributes.Length)
                throw new InvalidOperationException("InitialAttributes replacement failed.");
            for (int i = 0; i < record.InitialAttributes.Length; i++)
                if (leader.InitialAttributes[i] != record.InitialAttributes[i])
                    throw new InvalidOperationException("InitialAttributes readback mismatch.");
            if (!PrivateMember.Set(leader, "m_Guid", record.NumericGuid) || leader.GetGuid() != record.NumericGuid)
                throw new InvalidOperationException("Numeric leader GUID readback mismatch.");
            if (leader.GetID() != record.LeaderId || speaker.GetID() != record.SpeakerId)
                throw new InvalidOperationException("Cloned IDs mismatch.");
            if (DataTemplateLoader.Get<UnitLeaderTemplate>(record.LeaderId, false) != null ||
                DataTemplateLoader.Get<SpeakerTemplate>(record.SpeakerId, false) != null)
                throw new InvalidOperationException("Template registered during clone before validation.");
            bridge.Register<SpeakerTemplate>(speaker, record.SpeakerId);
            bridge.Register<UnitLeaderTemplate>(leader, record.LeaderId);
            if (DataTemplateLoader.Get<UnitLeaderTemplate>(record.LeaderId, false) == null ||
                DataTemplateLoader.Get<SpeakerTemplate>(record.SpeakerId, false) == null)
                throw new InvalidOperationException("Dynamic template registration failed.");
            return $"visual={visual}, head={head}, voice={voice}, badge={badgeLabel}/{badge}, " +
                   $"leaderId={record.LeaderId}, guid={record.NumericGuid}";
        }
        catch
        {
            // Jiangyu's native template loader may retain objects after a partial
            // registration. Never free any native object which could be referenced.
            if (speaker != null && !_pinned.Contains(speaker)) _pinned.Add(speaker);
            if (leader != null && !_pinned.Contains(leader)) _pinned.Add(leader);
            _pendingVoice.Remove(record.LeaderId);
            throw;
        }
    }

    private static string ApplyStandaloneVisuals(UnitLeaderTemplate leader, SpeakerTemplate speaker,
        UnitLeaderTemplate nativeDonor, StandalonePortraitAssets art, bool female, bool forceGenericHead)
    {
        art.ApplyTo(leader, speaker);
        if (forceGenericHead)
        {
            leader.CustomHead = null;
            return "generic-head";
        }
        // Squaddie holds no persistent appearance/head pointer in strategy state.
        // Prefer Carda's helmeted head for women, and Lim for men.
        var carda = DataTemplateLoader.Get<UnitLeaderTemplate>("squad_leader.carda", false);
        if (female && carda != null && carda.Gender == leader.Gender && carda.CustomHead != null)
        {
            leader.CustomHead = carda.CustomHead;
            return "carda-helmet-head";
        }
        if (nativeDonor.CustomHead != null)
        {
            leader.CustomHead = nativeDonor.CustomHead;
            return "native-gender-head";
        }
        return "native-gender-skin-head";
    }

    private void ApplyProgression(PromotionRecord record, UnitLeaderTemplate leader)
    {
        if (record.PerkSchemaVersion == 0) ApplyGenericProgression(leader);
        else DynamicPerkTrees.VerifyOrApply(record, leader, _pinned);
    }

    private static void ApplyGenericProgression(UnitLeaderTemplate leader)
    {
        var source = DataTemplateLoader.Get<UnitLeaderTemplate>(PromotionProgression.TemplateId, false);
        var initialPerk = DataTemplateLoader.Get<PerkTemplate>(PromotionProgression.InitialPerkId, false);
        if (source?.PerkTrees == null || source.PerkTrees.Length != 1 || source.PerkTrees[0] == null ||
            initialPerk == null)
            throw new InvalidOperationException("Generic infantry promotion templates are missing.");

        var tree = source.PerkTrees[0];
        var perks = tree.Perks;
        var expected = PromotionProgression.GenericInfantryPerks;
        if (perks == null || perks.Length != expected.Length)
            throw new InvalidOperationException("Generic infantry perk tree has an unexpected entry count.");
        for (int i = 0; i < expected.Length; i++)
            if (perks[i] == null || perks[i].Skill == null ||
                perks[i].Skill.GetID() != expected[i].SkillId || perks[i].Tier != expected[i].Tier)
                throw new InvalidOperationException("Generic infantry perk tree failed validation.");

        var trees = new Il2CppReferenceArray<PerkTreeTemplate>(1);
        trees[0] = tree;
        leader.InitialPerk = initialPerk;
        leader.PerkTrees = trees;
        if (leader.InitialPerk == null || leader.InitialPerk.GetID() != PromotionProgression.InitialPerkId ||
            leader.PerkTrees == null || leader.PerkTrees.Length != 1 || leader.PerkTrees[0] != tree)
            throw new InvalidOperationException("Generic infantry progression assignment failed.");
    }

    internal bool RefreshIdentity(PromotionRecord record)
    {
        var leader = DataTemplateLoader.Get<UnitLeaderTemplate>(record.LeaderId, false);
        if (leader == null) return false;
        ApplyIdentityText(record, leader);
        // Do not make pre-load template reconstruction depend on a display check.
        PromotionTemplateText.VerifyDisplay(record, leader);
        return true;
    }

    private static void ApplyIdentityText(PromotionRecord record, UnitLeaderTemplate leader)
    {
        var source = DataTemplateLoader.Get<UnitLeaderTemplate>(SourceLeaderId, false);
        string voiceDonorId = record.Gender == Convert.ToInt32(Il2CppMenace.Tactical.Gender.Female)
            ? "squad_leader.sy" : "squad_leader.lim";
        var voiceDonor = DataTemplateLoader.Get<UnitLeaderTemplate>(voiceDonorId, false);
        ApplyIdentityText(record, leader, source, voiceDonor);
    }

    private static void ApplyIdentityText(PromotionRecord record, UnitLeaderTemplate leader,
        UnitLeaderTemplate source, UnitLeaderTemplate voiceDonor)
    {
        PromotionTemplateText.Apply(record, leader, source, voiceDonor);
        PrivateMember.Set(leader.SpeakerTemplate, "m_LocalizedStrings", null);
        PrivateMember.Set(leader, "m_LocalizedStrings", null);
    }

    internal static bool RosterContainsLeader(StrategyState state, string leaderId)
    {
        foreach (object candidate in PrivateMember.Enumerate(state.Roster.GetHirableLeaders()))
            if (candidate is UnitLeaderTemplate item && item.GetID() == leaderId) return true;
        foreach (object candidate in PrivateMember.Enumerate(state.Roster.GetHiredLeaders()))
            if (candidate is BaseUnitLeader item && item.LeaderTemplate != null &&
                item.LeaderTemplate.GetID() == leaderId) return true;
        return false;
    }

    private static bool RosterContainsGuid(StrategyState state, int guid)
    {
        foreach (object candidate in PrivateMember.Enumerate(state.Roster.GetHirableLeaders()))
            if (candidate is UnitLeaderTemplate item && item.GetGuid() == guid) return true;
        foreach (object candidate in PrivateMember.Enumerate(state.Roster.GetHiredLeaders()))
            if (candidate is BaseUnitLeader item && item.LeaderTemplate != null &&
                item.LeaderTemplate.GetGuid() == guid) return true;
        return false;
    }

    private static bool TemplateGuidConflicts(string expectedId, int numericGuid)
    {
        foreach (object item in PrivateMember.Enumerate(DataTemplateLoader.GetAll<UnitLeaderTemplate>()))
            if (item is UnitLeaderTemplate template && template.GetGuid() == numericGuid &&
                template.GetID() != expectedId) return true;
        return false;
    }

    private static int FindUnusedLeaderGuid(StrategyState state)
    {
        var used = new HashSet<int>();
        try
        {
            foreach (object item in PrivateMember.Enumerate(DataTemplateLoader.GetAll<UnitLeaderTemplate>()))
                if (item is UnitLeaderTemplate t) used.Add(t.GetGuid());
        }
        catch { /* checked again against active candidate and hired lists below */ }
        foreach (object entry in PrivateMember.Enumerate(state.Roster.GetHirableLeaders()))
            if (entry is UnitLeaderTemplate template) used.Add(template.GetGuid());
        foreach (object entry in PrivateMember.Enumerate(state.Roster.GetHiredLeaders()))
            if (entry is BaseUnitLeader leader && leader.LeaderTemplate != null)
                used.Add(leader.LeaderTemplate.GetGuid());
        for (int i = 0; i < 32; i++)
        {
            // Positive signed int within game GUID type; random GUID is not an index.
            int candidate = (int)((uint)Guid.NewGuid().GetHashCode() & 0x7fffffff);
            if (candidate > 0 && !used.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not allocate a unique numeric leader GUID.");
    }

    private static LocalizedLine NewLine(LocalizedLine original, string id, string field, string value)
    {
        if (original == null) throw new InvalidOperationException("Missing localized source: " + field);
        var line = new LocalizedLine(original.GetCategory(), field, false);
        line.SetId(id);
        line.SetDefaultTranslation(value);
        return line;
    }

}
