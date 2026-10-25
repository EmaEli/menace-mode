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
            var pilotSlot = Image();
            var voicePortrait = Image();
            var badge = Image();
            var speaker = new SpeakerTemplate
            {
                StandLookRightImage = voicePortrait.texture, StandLookLeftImage = voicePortrait.texture,
                StandLookRightInactiveImage = voicePortrait.texture, OperationSelectImage = voicePortrait.texture,
                BarkImage = voicePortrait,
            };
            var leader = new UnitLeaderTemplate
            { Slot = pilotSlot, SlotInactive = pilotSlot, Badge = badge, SpeakerTemplate = speaker };
            for (int pass = 0; pass < 2; pass++)
            {
                art.ApplyTo(leader, speaker);
                check(leader.Slot == sprites["slot.png"] && leader.SlotInactive == sprites["slot_inactive.png"],
                    gender + " infantry slots use the bundled art on initial assignment and repeated reconstruction");
                check(speaker.StandLookRightImage == sprites["stand_right.png"].texture &&
                    speaker.StandLookLeftImage == sprites["stand_left.png"].texture &&
                    speaker.StandLookRightInactiveImage == sprites["stand_right_inactive.png"].texture &&
                    speaker.OperationSelectImage == sprites["slot.png"].texture &&
                    speaker.BarkImage == sprites["bark.png"],
                    gender + " large, inactive, operation and bark images all come from the same bundled family");
                check(leader.Badge == badge, gender + " portrait repair leaves the leader's dynamic badge unchanged");
            }
        }
    }
}