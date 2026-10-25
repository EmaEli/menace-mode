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
        foreach (string gender in new[] { "male", "female" })
        {
            var sprites = StandalonePortraitAssets.PortraitFileNames.ToDictionary(name => name, _ => Image());
            var art = Assets(sprites);
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
                art.ApplyTo(leader, speaker);
                check(leader.Slot == sprites["slot.png"] && leader.SlotInactive == sprites["slot_inactive.png"] &&
                    leader.Slot != namedPilotSlot,
                    gender + " vehicle commander slots use generic art instead of named pilot art on assignment and reconstruction");
                check(speaker.StandLookRightImage == sprites["stand_right.png"].texture &&
                    speaker.StandLookLeftImage == sprites["stand_left.png"].texture &&
                    speaker.StandLookRightInactiveImage == sprites["stand_right_inactive.png"].texture &&
                    speaker.OperationSelectImage == sprites["slot.png"].texture &&
                    speaker.BarkImage == sprites["bark.png"] &&
                    speaker.StandLookRightImage != namedPilotPortrait.texture,
                    gender + " large, inactive, operation and bark images replace named pilot art with one generic family");
                check(leader.Badge == badge, gender + " generic portrait assignment preserves native pilot badge art");
            }
        }
    }
}