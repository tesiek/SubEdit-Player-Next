namespace SubEdit.Player.WinForms.Playback;

/// <summary>
/// Normalizes and aggregates mouse wheel Delta into 120-unit notches and converts them to seek offsets.
/// </summary>
public sealed class WheelSeekDeltaAccumulator
{
    public const int WheelDeltaPerStep = 120;
    public const int MillisecondsPerStep = 10_000;

    public int Remainder { get; private set; }

    /// <summary>
    /// Processes a raw mouse wheel Delta, updates the signed remainder, and returns the accumulated offset in milliseconds.
    /// </summary>
    /// <param name="wheelDelta">The raw wheel Delta from MouseEventArgs or WM_MOUSEWHEEL.</param>
    /// <returns>The calculated seek offset in milliseconds (steps * 10,000), or 0 if no full steps were completed.</returns>
    public long ProcessDelta(int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return 0;
        }

        var total = Remainder + wheelDelta;
        var steps = total / WheelDeltaPerStep;
        Remainder = total % WheelDeltaPerStep;

        return (long)steps * MillisecondsPerStep;
    }

    /// <summary>
    /// Resets the accumulated remainder to zero.
    /// </summary>
    public void Reset()
    {
        Remainder = 0;
    }
}
