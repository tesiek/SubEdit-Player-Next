using System;

namespace FlyleafLib.MediaPlayer;

internal sealed class SeekAccurateAttempt
{
    public long SeekStartMs { get; set; }
    public long? FirstFramePtsTicks { get; set; }
    public long? FinalPtsTicks { get; set; }
    public long HalfFrameTicks { get; set; }
    public string Result { get; set; } = "FAILURE";
    internal Func<bool> Cancel = null!;
    internal bool RejectOvershoot;

    internal bool IsCancelled()
    {
        if (!Cancel()) return false;
        Result = "SUPERSEDED";
        return true;
    }

    internal bool ObserveFrame(long pts, long target, long halfFrame)
    {
        if (IsCancelled()) return false;
        HalfFrameTicks = halfFrame;
        if (FirstFramePtsTicks == null)
        {
            if (IsCancelled()) return false;
            FirstFramePtsTicks = pts;
            if (RejectOvershoot && pts > target + halfFrame)
            {
                Result = "OVERSHOOT";
                return false;
            }
        }
        return true;
    }

    internal void Complete(long pts)
    {
        FinalPtsTicks = pts;
        Result = "SUCCESS";
    }
}

public partial class Player
{
    /// <summary>
    /// Gets or sets the default SeekAccurate policy for this Player instance.
    /// Default is <see cref="SeekAccurateMode.Legacy"/>.
    /// </summary>
    public SeekAccurateMode SeekAccurateMode { get; set; } = SeekAccurateMode.Legacy;

    /// <summary>
    /// Seeks at the exact timestamp using the specified mode.
    /// </summary>
    /// <param name="ms">Target timestamp in milliseconds.</param>
    /// <param name="mode">The accurate seek mode (Conditional or Legacy).</param>
    public void SeekAccurate(int ms, SeekAccurateMode mode)
        => Seek(ms, false, !IsLive, mode);

    internal int ExecuteAccurateSeek(SeekData request)
    {
        bool isLegacy = request.accurateMode == SeekAccurateMode.Legacy;
        bool Cancelled() => seeks.IsSuperseded(request.intentId) || !CanPlay || decoder.Interrupt;

        SeekAccurateAttempt Attempt(int preroll, bool rejectOvershoot)
        {
            var probe = new SeekAccurateAttempt
            {
                SeekStartMs = Math.Max(0, request.ms - preroll),
                Cancel = Cancelled,
                RejectOvershoot = rejectOvershoot
            };

            if (!probe.IsCancelled())
            {
                decoder.PauseDecoders();
                int ret = decoder.Seek(probe.SeekStartMs, false, false);
                if (ret >= 0 && !probe.IsCancelled())
                    decoder.GetVideoFrameAccurate(request.ms * 10000L, probe);
            }
            return probe;
        }

        var first = Attempt(isLegacy ? 3000 : 0, !isLegacy);
        var final = first;
        if (!isLegacy && final.Result is "FAILURE" or "OVERSHOOT")
        {
            if (!Cancelled())
                final = Attempt(3000, false);
            else
                final.Result = "SUPERSEDED";
        }

        if (Cancelled()) final.Result = "SUPERSEDED";
        if (final.Result != "SUCCESS") decoder.Flush();

        return final.Result == "SUCCESS" ? 0 : final.Result == "SUPERSEDED" ? -2 : -1;
    }
}
