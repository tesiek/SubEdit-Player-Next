namespace SubEdit.Player.WinForms;

internal sealed class UnsortedSubtitleWarningTracker
{
    private readonly List<SubtitleCue> _warnedCues = [];
    private long _lastPlaybackPosition = -1;

    public void Reset()
    {
        _warnedCues.Clear();
        _lastPlaybackPosition = -1;
    }

    public void Synchronize(SubtitleDocument document, long position)
    {
        var currentPosition = Math.Max(0, position);
        _warnedCues.Clear();
        _warnedCues.AddRange(document.GetUnsortedCuesReadyAt(currentPosition));
        _lastPlaybackPosition = currentPosition;
    }

    public SubtitleCue? GetNextWarning(SubtitleDocument document, long position)
    {
        var currentPosition = Math.Max(0, position);
        if (_lastPlaybackPosition >= 0 && currentPosition + 250 < _lastPlaybackPosition)
        {
            _warnedCues.Clear();
        }

        _lastPlaybackPosition = currentPosition;
        var cue = document
            .GetUnsortedCuesReadyAt(currentPosition)
            .FirstOrDefault(candidate => !_warnedCues.Any(warnedCue => ReferenceEquals(warnedCue, candidate)));
        if (cue is not null)
        {
            _warnedCues.Add(cue);
        }

        return cue;
    }
}
