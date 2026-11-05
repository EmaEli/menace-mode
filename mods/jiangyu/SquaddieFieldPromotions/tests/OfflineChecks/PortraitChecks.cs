using System.Reflection;
using Il2CppMenace.Conversations;
using Il2CppMenace.Strategy;
using SquaddieFieldPromotions;
using UnityEngine;

// Exercises production portrait assignment, not Unity rendering or native template cloning.
internal static class PortraitChecks
{
    internal static StandalonePortraitAssets Assets(Dictionary<string, Sprite> sprites) =>
        (StandalonePortraitAssets)Activator.CreateInstance(typeof(StandalonePortraitAssets),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { sprites }, null);

    internal static void Run(Action<bool, string> check)
    {
        Sprite Image() => Sprite.Create(new Texture2D(4, 4, TextureFormat.RGBA32, false),
            new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        var malePilotSprites = StandalonePortraitAssets.PortraitFileNames.ToDictionary(name => name, _ => Image());
        var femalePilotSprites = StandalonePortraitAssets.PortraitFileNames.ToDictionary(name => name, _ => Image());
        var malePilotArt = Assets(malePilotSprites);
        var femalePilotArt = Assets(femalePilotSprites);
        foreach (string gender in new[] { "male", "female" })
        {
            bool female = gender == "female";
            var pilotSprites = female ? femalePilotSprites : malePilotSprites;
            var pilotArt = female ? femalePilotArt : malePilotArt;
            var genderSprites = StandalonePortraitAssets.PortraitFileNames.ToDictionary(name => name, _ => Image());
            var genderArt = Assets(genderSprites);
            var namedPilotSlot = Image();
            var namedPilotPortrait = Image();
            var badge = Image();
            var speaker = new SpeakerTemplate
            {
                StandLookRightImage = namedPilotPortrait.texture, StandLookLeftImage = namedPilotPortrait.texture,
                StandLookRightInactiveImage = namedPilotPortrait.texture, OperationSelectImage = namedPilotPortrait.texture,
                BarkImage = namedPilotPortrait,
            };
            var leader = new UnitLeaderTemplate
            { Slot = namedPilotSlot, SlotInactive = namedPilotSlot, Badge = badge, SpeakerTemplate = speaker };
            for (int pass = 0; pass < 2; pass++)
            {
                genderArt.ApplyTo(leader, speaker);
                pilotArt.ApplyTo(leader, speaker);
                check(leader.Slot == pilotSprites["slot.png"] && leader.SlotInactive == pilotSprites["slot_inactive.png"] &&
                    leader.Slot != namedPilotSlot,
                    gender + " vehicle commander uses gender-matched generic-pilot slot art on assignment and reconstruction");
                check(speaker.StandLookRightImage == pilotSprites["stand_right.png"].texture &&
                    speaker.StandLookLeftImage == pilotSprites["stand_left.png"].texture &&
                    speaker.StandLookRightInactiveImage == pilotSprites["stand_right_inactive.png"].texture &&
                    speaker.OperationSelectImage == pilotSprites["slot.png"].texture &&
                    speaker.BarkImage == pilotSprites["bark.png"] &&
                    speaker.StandLookRightImage != namedPilotPortrait.texture,
                    gender + " vehicle commander uses gender-matched generic-pilot art in all speaker views");
                check(leader.Badge == badge,
                    gender + " portrait replacement leaves the badge for independent dynamic letter assignment");
            }
        }
    }
}