using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private Controls.TransportDropdownController? _transportDropdowns;
    private void ShowSubtitleTracksMenu()
    {
        var menu = new ContextMenuStrip();
        var embeddedTracks = _playback.EmbeddedSubtitleTracks;
        var textSubtitleFiles = GetSubtitleFilesForMenu();

        var disableItem = new ToolStripMenuItem("S: Wyłącz napisy")
        {
            Checked = !_externalSubtitleOverlayEnabled && _playback.ActiveEmbeddedSubtitleTrackId is null
        };
        disableItem.Click += (_, _) => DisableSubtitlesFromMenu();
        menu.Items.Add(disableItem);

        foreach (var track in embeddedTracks)
        {
            var item = new ToolStripMenuItem(FormatSubtitleTrackDescription(track))
            {
                Checked = !_externalSubtitleOverlayEnabled && track.Id == _playback.ActiveEmbeddedSubtitleTrackId
            };
            var trackId = track.Id;
            item.Click += (_, _) => SelectEmbeddedSubtitle(trackId);
            menu.Items.Add(item);
        }

        if (textSubtitleFiles.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            foreach (var subtitlePath in textSubtitleFiles)
            {
                var item = new ToolStripMenuItem($"S: {Path.GetFileName(subtitlePath)}")
                {
                    Checked = _externalSubtitleOverlayEnabled &&
                        string.Equals(subtitlePath, _currentSubtitlePath, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (_, _) => SelectTextSubtitle(subtitlePath);
                menu.Items.Add(item);
            }
        }

        ShowTransportMenu(menu, _subtitleTracksButton);
    }

    private IReadOnlyList<string> GetSubtitleFilesForMenu()
    {
        var directory = _currentVideoPath is null ? null : Path.GetDirectoryName(_currentVideoPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            var videoFileName = Path.GetFileNameWithoutExtension(_currentVideoPath);
            return Directory.EnumerateFiles(directory)
                .Where(path => SubtitleMenuExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    videoFileName,
                    StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(Path.GetFileName, NaturalFileNameComparer.Instance)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void SelectEmbeddedSubtitle(int trackId)
    {
        if (_playback.SelectEmbeddedSubtitleTrack(trackId))
        {
            _externalSubtitleOverlayEnabled = false;
            UpdateSubtitleOverlay(_playback.PositionMilliseconds);
            RememberSubtitleTrackSelection(trackId);
        }
    }

    private void SelectTextSubtitle(string subtitlePath)
    {
        if (!ConfirmSubtitleChanges())
        {
            return;
        }

        if (LoadSubtitleFile(subtitlePath, showErrors: true, showSrtConversionQuestion: true))
        {
            _settings.Playback.LastSubtitleTrackId = null;
        }
    }

    private void DisableSubtitlesFromMenu()
    {
        _playback.SelectEmbeddedSubtitleTrack(null);
        _externalSubtitleOverlayEnabled = false;
        UpdateSubtitleOverlay(_playback.PositionMilliseconds);
        RememberSubtitleTrackSelection(null);
    }

    private sealed class NaturalFileNameComparer : IComparer<string?>
    {
        public static readonly NaturalFileNameComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return StrCmpLogicalW(left, right);
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string left, string right);
    }

    private void ShowAudioTracksMenu()
    {
        ShowTracksMenu(
            _audioTracksButton,
            _playback.AudioTracks,
            _playback.ActiveAudioTrackId,
            track => track.Id,
            FormatAudioTrackDescription,
            _playback.SelectAudioTrack,
            RememberAudioTrackSelection,
            "Brak ścieżek audio");
    }

    private void ShowVideoTracksMenu()
    {
        ShowTracksMenu(
            _videoTracksButton,
            _playback.VideoTracks,
            _playback.ActiveVideoTrackId,
            track => track.Id,
            FormatVideoTrackDescription,
            _playback.SelectVideoTrack,
            rememberSelection: null,
            "Brak ścieżek wideo");
    }

    private void ShowChaptersMenu()
    {
        var menu = new ContextMenuStrip();
        var chapters = _playback.Chapters;
        if (chapters.Count == 0)
        {
            menu.Items.Add("Brak rozdziałów").Enabled = false;
        }
        else
        {
            var currentChapter = _playback.ActiveChapterIndex;
            foreach (var chapter in chapters)
            {
                var name = string.IsNullOrWhiteSpace(chapter.Name)
                    ? $"Rozdział {chapter.Index + 1}"
                    : chapter.Name;
                var item = new ToolStripMenuItem($"{FormatTime(chapter.PositionMilliseconds)}  {name}")
                {
                    Checked = chapter.Index == currentChapter
                };
                item.Click += (_, _) => SeekToChapter(chapter.Index, chapter.PositionMilliseconds);
                menu.Items.Add(item);
            }
        }

        ShowTransportMenu(menu, _chaptersButton);
    }

    private void ShowTracksMenu<TTrack>(
        ToolStripButton button,
        IReadOnlyList<TTrack> tracks,
        int? selectedTrackId,
        Func<TTrack, int> getTrackId,
        Func<TTrack, string> getTrackTitle,
        Func<int, bool> selectTrack,
        Action<int>? rememberSelection,
        string emptyMessage)
    {
        var menu = new ContextMenuStrip();
        if (tracks.Count == 0)
        {
            menu.Items.Add(emptyMessage).Enabled = false;
        }
        else
        {
            foreach (var track in tracks)
            {
                var trackId = getTrackId(track);
                var item = new ToolStripMenuItem(getTrackTitle(track))
                {
                    Checked = trackId == selectedTrackId
                };
                item.Click += (_, _) =>
                {
                    if (selectTrack(trackId))
                    {
                        rememberSelection?.Invoke(trackId);
                    }
                };
                menu.Items.Add(item);
            }
        }

        ShowTransportMenu(menu, button);
    }

    private void ShowTransportMenu(ContextMenuStrip menu, ToolStripButton button)
    {
        _transportDropdowns ??= new Controls.TransportDropdownController(_transportToolbar);
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_fullScreenActiveMenu, menu)) _fullScreenActiveMenu = null;
        };
        _transportDropdowns.Toggle(menu, button);
        _fullScreenActiveMenu = _isFullScreen ? _transportDropdowns.ActiveMenu : null;
    }

    private void RememberAudioTrackSelection(int trackId)
    {
        _settings.Playback.LastAudioTrackId = trackId;
        RefreshAudioTrackStatus();
    }

    private void RememberSubtitleTrackSelection(int? trackId) =>
        _settings.Playback.LastSubtitleTrackId = trackId ?? -1;

    private void ApplyInitialTrackSelection()
    {
        if (!_playback.IsMediaOpen)
        {
            return;
        }

        if (!_automaticTrackSelectionPending || !_playback.AreTracksReady)
        {
            return;
        }

        _automaticTrackSelectionPending = false;
        var audioTracks = GetAudioTrackCandidates();
        // LoadVideoFile clears the manual preference before opening a different media path.
        var preferredAudioTrackId = _settings.Playback.LastAudioTrackId;
        if (preferredAudioTrackId is int rememberedAudioTrackId &&
            (!audioTracks.Any(track => track.Id == rememberedAudioTrackId) ||
             !_playback.SelectAudioTrack(rememberedAudioTrackId)))
        {
            _settings.Playback.LastAudioTrackId = null;
            preferredAudioTrackId = null;
        }

        if (_externalSubtitleOverlayEnabled)
        {
            DisableEmbeddedSubtitleTrack();
            _settings.Playback.LastSubtitleTrackId = null;
            return;
        }

        if (preferredAudioTrackId is null)
        {
            preferredAudioTrackId = AutomaticSubtitleSelector.SelectPreferredAudio(audioTracks);
            if (preferredAudioTrackId is int selectedAudioTrackId)
            {
                _playback.SelectAudioTrack(selectedAudioTrackId);
            }
        }

        var selectedAudio = preferredAudioTrackId is int audioTrackId
            ? audioTracks.FirstOrDefault(track => track.Id == audioTrackId)
            : null;

        if (selectedAudio is not null && AutomaticSubtitleSelector.IsPolishAudio(selectedAudio))
        {
            DisableSubtitlesForPolishAudio();
            return;
        }

        var subtitleTrackId = AutomaticSubtitleSelector.SelectPreferredSubtitle(
            GetEmbeddedSubtitleTrackCandidates());
        if (subtitleTrackId is int selectedSubtitleTrackId &&
            _playback.SelectEmbeddedSubtitleTrack(selectedSubtitleTrackId))
        {
            _externalSubtitleOverlayEnabled = false;
            RememberSubtitleTrackSelection(selectedSubtitleTrackId);
            UpdateSubtitleOverlay(_playback.PositionMilliseconds);
            return;
        }

        DisableEmbeddedSubtitleTrack();
        RememberSubtitleTrackSelection(null);
    }

    private IReadOnlyList<EmbeddedTrackCandidate> GetAudioTrackCandidates()
    {
        return _playback.AudioTracks
            .Select(track => new EmbeddedTrackCandidate(
                track.Id,
                track.Language,
                track.Description,
                track.Name))
            .ToArray();
    }

    private IReadOnlyList<EmbeddedTrackCandidate> GetEmbeddedSubtitleTrackCandidates()
    {
        return _playback.EmbeddedSubtitleTracks
            .Select(track =>
                new EmbeddedTrackCandidate(
                    track.Id,
                    track.Language,
                    track.Description,
                    track.Name))
            .ToArray();
    }

    private void DisableEmbeddedSubtitleTrack()
    {
        _playback.SelectEmbeddedSubtitleTrack(null);
    }

    private void EnableExternalSubtitleOverlay()
    {
        _externalSubtitleOverlayEnabled = true;
        DisableEmbeddedSubtitleTrack();
        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                if (!IsDisposed && _externalSubtitleOverlayEnabled)
                {
                    DisableEmbeddedSubtitleTrack();
                }
            });
        }
    }

    private void DisableSubtitlesForPolishAudio()
    {
        DisableEmbeddedSubtitleTrack();
        _externalSubtitleOverlayEnabled = false;
        RememberSubtitleTrackSelection(null);
        UpdateSubtitleOverlay(_playback.PositionMilliseconds);
    }

    private string FormatAudioTrackDescription(PlaybackAudioTrack track)
    {
        if (!track.HasDetails)
        {
            return track.Name ?? string.Empty;
        }

        var title = GetTrackTitle(track.Name, track.Description, track.Language);
        var languageCode = GetLanguageCode(track.Language) ?? GetLanguageCode(title);
        var languageSuffix = string.IsNullOrWhiteSpace(languageCode) ? string.Empty : $" [{languageCode}]";
        var codec = SimplifyCodecName(track.CodecDescription);
        var bitrate = track.Bitrate > 0 ? $", {track.Bitrate / 1000} kb/s" : string.Empty;
        var channels = track.Channels switch
        {
            1 => "mono",
            2 => "stereo",
            _ => $"{track.Channels} kanałów"
        };
        var rate = track.SampleRate > 0 ? $", {track.SampleRate} Hz" : string.Empty;
        return $"A: {title}{languageSuffix} ({codec}{rate}, {channels}{bitrate})";
    }

    private string FormatVideoTrackDescription(PlaybackVideoTrack track)
    {
        if (!track.HasDetails)
        {
            return track.Name ?? string.Empty;
        }

        var title = GetTrackTitle(track.Name, track.Description, track.Language);
        var languageCode = GetLanguageCode(track.Language) ?? GetLanguageCode(title);
        var languageSuffix = string.IsNullOrWhiteSpace(languageCode) ? string.Empty : $" [{languageCode}]";
        var codec = SimplifyCodecName(track.CodecDescription);
        var bitrate = track.Bitrate > 0 ? $", {track.Bitrate / 1000} kb/s" : string.Empty;
        var resolution = track.Width > 0 && track.Height > 0 ? $", {track.Width}x{track.Height}" : string.Empty;
        var fps = track.FramesPerSecond is > 0d
            ? $", {FormatFrameRate(track.FramesPerSecond.Value)} FPS"
            : string.Empty;
        return $"V: {title}{languageSuffix} ({codec}{resolution}{fps}{bitrate})";
    }

    private string FormatSubtitleTrackDescription(PlaybackSubtitleTrack track)
    {
        if (!track.HasDetails)
        {
            return track.Name ?? string.Empty;
        }

        var title = GetTrackTitle(track.Name, track.Description, track.Language);
        var languageCode = GetLanguageCode(track.Language) ?? GetLanguageCode(title);
        var languageSuffix = string.IsNullOrWhiteSpace(languageCode) ? string.Empty : $" [{languageCode}]";
        return $"S: {title}{languageSuffix}";
    }

    private static string GetTrackTitle(string? trackName, string? description, string? language)
    {
        var title = CleanTrackTitle(description);
        if (string.IsNullOrWhiteSpace(title) || IsGenericTrackTitle(title))
        {
            title = CleanTrackTitle(trackName);
        }

        return string.IsNullOrWhiteSpace(title) || IsGenericTrackTitle(title)
            ? GetLanguageName(language)
            : title;
    }

    private static string CleanTrackTitle(string? value)
    {
        var title = value?.Trim() ?? string.Empty;
        if (title.StartsWith("Track ", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = title.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                title = title[(separatorIndex + 3)..].Trim();
            }
        }

        while (title.Length >= 2 && title[0] == '[' && title[^1] == ']')
        {
            title = title[1..^1].Trim();
        }

        return title;
    }

    private static bool IsGenericTrackTitle(string title) =>
        title.StartsWith("Track ", StringComparison.OrdinalIgnoreCase) &&
        title.Length > 6 &&
        title[6..].All(char.IsDigit);

    private static string SimplifyCodecName(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return string.Empty;
        }

        const string akaMarker = "(aka ";
        var akaIndex = codec.IndexOf(akaMarker, StringComparison.OrdinalIgnoreCase);
        if (akaIndex < 0)
        {
            return codec;
        }

        var endIndex = codec.IndexOf(')', akaIndex + akaMarker.Length);
        return endIndex < 0
            ? codec[(akaIndex + akaMarker.Length)..].Trim()
            : codec[(akaIndex + akaMarker.Length)..endIndex].Trim();
    }

    private static string? GetLanguageCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalized = language.Trim().Trim('[', ']');
        var parenthesisIndex = normalized.IndexOf('(');
        if (parenthesisIndex > 0)
        {
            normalized = normalized[..parenthesisIndex].Trim();
        }
        var culture = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(item =>
                string.Equals(item.ThreeLetterISOLanguageName, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.TwoLetterISOLanguageName, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.EnglishName, normalized, StringComparison.OrdinalIgnoreCase));
        return culture?.ThreeLetterISOLanguageName ??
            (normalized.Length is 2 or 3 ? normalized.ToLowerInvariant() : null);
    }

    private static string GetLanguageName(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "Język nieznany";
        }

        return CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(culture => string.Equals(
                culture.ThreeLetterISOLanguageName,
                languageCode,
                StringComparison.OrdinalIgnoreCase))?.EnglishName
            ?? languageCode;
    }
}
