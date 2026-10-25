namespace FlyleafLib.MediaPlayer;

/// <summary>
/// Specifies the accurate seeking algorithm.
/// </summary>
public enum SeekAccurateMode
{
    /// <summary>
    /// Legacy Flyleaf SeekAccurate: always seeks 3000 ms backwards before decoding to target.
    /// Safe default matching upstream Flyleaf behavior.
    /// </summary>
    Legacy = 0,

    /// <summary>
    /// Conditional SeekAccurate: attempts 0 ms preroll first with overshoot detection.
    /// If failure or overshoot occurs, automatically falls back to 3000 ms preroll.
    /// </summary>
    Conditional = 1
}
