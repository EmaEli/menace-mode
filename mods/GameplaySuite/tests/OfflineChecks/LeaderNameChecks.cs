using SquaddieProbe;

internal static class LeaderNameChecks
{
    public static void Run(Action<bool, string> check)
    {
        const string id = "mod.squaddiefieldpromotions.leader.0123456789abcdef0123456789abcdef";
        var leader = new Leader { Name = id, Nickname = new LocalizedLine("Girly Girl") };
        check(LeaderDisplayName.Resolve(leader) == "Girly Girl", "native localized nickname wins over internal Name");
        leader.Nickname = new LocalizedLine(" ");
        leader.Speaker = new Speaker { Nickname = new LocalizedLine("Tsunami"), FullName = "Tobias Irvin" };
        check(LeaderDisplayName.Resolve(leader) == "Tsunami", "speaker nickname preferred to full name");
        leader.Speaker.Nickname = null;
        check(LeaderDisplayName.Resolve(leader) == "Tobias Irvin", "speaker full name fallback");
        leader.Speaker.FullName = "";
        leader.Speaker.Forename = new LocalizedLine("Chloé");
        leader.Speaker.Surname = new LocalizedLine("O'Neil Jr.");
        check(LeaderDisplayName.Resolve(leader) == "Chloé O'Neil Jr.", "localized name parts preserve punctuation");
        leader.Speaker = null;
        check(LeaderDisplayName.Resolve(leader) == LeaderDisplayName.Unknown, "internal ID never shown");

        foreach (string invalid in new[] { "0123456789abcdef0123456789abcdef", "01234567-89ab-cdef-0123-456789abcdef",
            id, "Il2CppMenace.Strategy.BaseUnitLeader", "UnitLeaderTemplate", "SpeakerTemplate" })
        {
            leader.Nickname = new LocalizedLine(invalid);
            leader.DisplayName = "Dr. de Vries";
            check(LeaderDisplayName.Resolve(leader) == "Dr. de Vries", "reject internal text and continue to later source");
        }
        leader.DisplayName = "";
        leader.Nickname = null;
        var template = new LeaderTemplate { SpeakerTemplate = new Speaker { Nickname = new LocalizedLine("Pike") } };
        var templateOnly = new TemplateLeader { LeaderTemplate = template };
        check(LeaderDisplayName.Resolve(templateOnly) == "Pike", "template speaker fallback");
        template.SpeakerTemplate = null;
        template.UnitTitle = new LocalizedLine("Weapons Team");
        check(LeaderDisplayName.Resolve(templateOnly) == "Weapons Team", "localized unit title fallback");
        template.UnitTitle = null;
        check(LeaderDisplayName.Resolve(templateOnly) == LeaderDisplayName.Unknown, "never call template GetID");
        check(template.IdReads == 0, "identity getter untouched");
        check(LeaderDisplayName.Resolve(new ThrowingLeader()) == "Hey Hey", "throwing primary getter falls back");
        check(LeaderDisplayName.Resolve(new { Nickname = new object() }) == LeaderDisplayName.Unknown, "never display object/proxy ToString");
        leader.Nickname = new LocalizedLine("  xX_Raven.Jr_Xx  ");
        check(LeaderDisplayName.Resolve(leader) == "xX_Raven.Jr_Xx", "preserve user nickname casing and punctuation");
    }

    public sealed class LocalizedLine
    {
        private readonly string _text;
        public LocalizedLine(string text) => _text = text;
        public override string ToString() => _text;
    }

    public sealed class Speaker
    {
        public LocalizedLine? Nickname { get; set; }
        public string FullName { get; set; } = "";
        public LocalizedLine? Forename { get; set; }
        public LocalizedLine? Surname { get; set; }
    }

    public sealed class Leader
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public LocalizedLine? Nickname { get; set; }
        public Speaker? Speaker { get; set; }
        public LocalizedLine? GetNickname() => Nickname;
        public Speaker? GetSpeakerTemplate() => Speaker;
    }

    public sealed class LeaderTemplate
    {
        public Speaker? SpeakerTemplate;
        public LocalizedLine? UnitTitle;
        public int IdReads;
        public string GetID() { IdReads++; return "must not be called"; }
    }

    public sealed class TemplateLeader
    {
        public LeaderTemplate? LeaderTemplate;
    }

    public sealed class ThrowingLeader
    {
        public LocalizedLine GetNickname() => throw new InvalidOperationException("unavailable");
        public string Nickname => "Hey Hey";
    }
}