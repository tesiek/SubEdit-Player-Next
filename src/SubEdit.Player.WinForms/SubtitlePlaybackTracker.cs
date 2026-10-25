namespace SubEdit.Player.WinForms;

internal sealed class SubtitlePlaybackTracker
{
    private SubtitleDocument? _document;
    private int _nextCueIndex;
    private SubtitleCue? _visibleCue;
    private long _lastPlaybackPosition = -1;

    public void Synchronize(SubtitleDocument document, long position)
    {
        var currentPosition = Math.Max(0, position);
        _document = document;
        _nextCueIndex = 0;
        _visibleCue = null;

        while (_nextCueIndex < document.Cues.Count &&
               document.Cues[_nextCueIndex].StartMilliseconds <= currentPosition)
        {
            var cue = document.Cues[_nextCueIndex++];
            if (cue.EndMilliseconds >= cue.StartMilliseconds &&
                currentPosition <= cue.EndMilliseconds)
            {
                _visibleCue = cue;
            }
        }

        _lastPlaybackPosition = currentPosition;
    }

    public SubtitleCue? GetCueAt(SubtitleDocument document, long position)
    {
        var currentPosition = Math.Max(0, position);
        if (!ReferenceEquals(_document, document) ||
            (_lastPlaybackPosition >= 0 && currentPosition + 250 < _lastPlaybackPosition))
        {
            Synchronize(document, currentPosition);
        }

        _lastPlaybackPosition = currentPosition;
        var activeCue = _visibleCue is { } visibleCue && currentPosition <= visibleCue.EndMilliseconds
            ? visibleCue
            : null;

        // Preserve the source order. A future malformed cue blocks later lines until its
        // start is reached; once reached, consume every already-due line and retain the
        // last one still active at the current playback position.
        while (_nextCueIndex < document.Cues.Count &&
               document.Cues[_nextCueIndex].StartMilliseconds <= currentPosition)
        {
            var cue = document.Cues[_nextCueIndex++];
            if (currentPosition <= cue.EndMilliseconds)
            {
                activeCue = cue;
            }
        }

        _visibleCue = activeCue;
        return _visibleCue;
    }
}
