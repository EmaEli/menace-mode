# Starting Options 0.1 - Windows MENACE test plan

This change is authored but has not yet been compiled and exercised in MENACE.

1. Back up saves and install a built **MenaceGameplaySuite 0.32**; remove older suite / standalone copies of the same modules.
2. In Mod Options > Starting Options, set **Maximum selectable starting leaders** to 12 and **Starting Spare Parts** to 1000, and leave Debug OFF.
3. Open **New Game**, enable **Customize Difficulty**, confirm the **Initial Leaders** slider supports up to 12 (or, if the game has fewer than 12 pickable leaders, is restricted to the available number).
4. Select the highest available Initial Leaders setting, start the new campaign, complete the leader picker and verify the requested number of leaders exists.
5. With **Black Market Headstart** checked, verify 1000 Spare Parts exist at campaign start (trade value 5 each). Other rewards should match vanilla.
6. Start a second new campaign with Black Market Headstart **unchecked**: no additional Spare Parts should be granted.
7. Return the mod's Spare Parts count to 36 and confirm the default unchanged reward for a fresh campaign.
8. Verify changing leader cap requires reopening the New Game window. Verify old saved campaigns remain unaffected.
9. Check MelonLoader logs for *errors only*; standard operation should print no Starting Options messages. Turn on Debug only to troubleshoot.

The patched methods, `NewGameWindow.AddCustomDifficultySlider` and `NewGameWindow.CreateGlobalDifficultyObject`, were matched against the user's IL2CPP dump and GameAssembly and exist as named methods in the October 2026 interop Assembly-CSharp DLL. Compatibility still depends on the specific installed MENACE version and actual runtime patches.
