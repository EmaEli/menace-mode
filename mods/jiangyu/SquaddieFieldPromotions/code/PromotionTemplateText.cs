using System;
using Il2CppMenace.Conversations;
using Il2CppMenace.Strategy;
using Il2CppMenace.Tools;

namespace SquaddieFieldPromotions;

internal static class PromotionTemplateText
{
    internal static void Apply(PromotionRecord record, UnitLeaderTemplate leader,
        UnitLeaderTemplate source, UnitLeaderTemplate voiceDonor)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        record.Validate();
        if (leader?.SpeakerTemplate == null)
            throw new InvalidOperationException("Promoted leader has no speaker template.");

        var titleSource = leader.UnitTitle ?? source?.UnitTitle ?? voiceDonor?.UnitTitle;
        var descriptionSource = leader.UnitDescription ?? source?.UnitDescription ?? voiceDonor?.UnitDescription;
        var speakerDescriptionSource = leader.SpeakerTemplate.Description ?? voiceDonor?.SpeakerTemplate?.Description;
        if (titleSource == null || descriptionSource == null || speakerDescriptionSource == null)
            throw new InvalidOperationException("Native localized text donors are missing.");

        leader.UnitTitle = NewLine(titleSource, record.LeaderId, "UnitTitle", record.UnitTitle);
        leader.UnitDescription = NewMultiLine(descriptionSource, record.LeaderId,
            "UnitDescription", record.UnitDescription);
        leader.SpeakerTemplate.Description = NewMultiLine(speakerDescriptionSource, record.SpeakerId,
            "Description", record.UnitDescription);
    }

    internal static void VerifyDisplay(PromotionRecord record, UnitLeaderTemplate leader)
    {
        // Read through the same native getters as the UI, not just the written fields.
        if (leader.GetUnitTitle()?.ToString() != record.UnitTitle ||
            leader.GetUnitDescription()?.ToString() != record.UnitDescription ||
            leader.SpeakerTemplate.Description?.ToString() != record.UnitDescription)
            throw new InvalidOperationException("Native displayed promotion text does not match the saved identity.");
    }

    private static LocalizedLine NewLine(LocalizedLine source, string id, string field, string text)
    {
        var line = new LocalizedLine(source.GetCategory(), field, false);
        line.SetId(id);
        line.SetDefaultTranslation(text);
        return line;
    }

    private static LocalizedMultiLine NewMultiLine(LocalizedMultiLine source, string id, string field, string text)
    {
        var line = new LocalizedMultiLine(source.GetCategory(), field, false);
        line.SetId(id);
        line.SetDefaultTranslation(text);
        return line;
    }
}