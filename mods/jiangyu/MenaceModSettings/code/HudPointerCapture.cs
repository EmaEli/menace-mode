namespace MenaceModSettings;

// Pure input policy, also compiled by the offline regression runner. No Unity/game access.
internal sealed class HudPointerCapture
{
    private int _captured;
    private int _blockedFrame = -1;

    public bool Sample(int frame, bool inside, int heldButtons, int pressedButtons)
    {
        // A press belongs to the HUD only if it STARTED inside, not merely dragged in.
        // Keep the release frame blocked for every native handler, even after capture ends.
        if (_captured != 0 || inside) _blockedFrame = frame;
        _captured = (_captured & heldButtons) | (inside ? pressedButtons : 0);
        return _blockedFrame == frame;
    }

    public void Reset()
    {
        _captured = 0;
        _blockedFrame = -1;
    }
}