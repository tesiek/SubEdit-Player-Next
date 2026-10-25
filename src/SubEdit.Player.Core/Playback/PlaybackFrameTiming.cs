namespace SubEdit.Player.Core.Playback;

public static class PlaybackFrameTiming
{
    public static long GetStepOffsetMilliseconds(int frameCount, double framesPerSecond)
    {
        var frameDuration = GetFrameDurationMilliseconds(framesPerSecond, roundUp: false);
        return frameCount * frameDuration;
    }

    public static long GetLastFramePositionMilliseconds(long durationMilliseconds, double framesPerSecond)
    {
        var frameDuration = GetFrameDurationMilliseconds(framesPerSecond, roundUp: true);
        return Math.Max(0, durationMilliseconds - frameDuration);
    }

    private static long GetFrameDurationMilliseconds(double framesPerSecond, bool roundUp)
    {
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }

        var milliseconds = 1000d / framesPerSecond;
        return Math.Max(1L, (long)(roundUp ? Math.Ceiling(milliseconds) : Math.Round(milliseconds)));
    }
}
