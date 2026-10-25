using SubEdit.Player.Core.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

public class AutomaticSubtitleSelectorTests
{
    [Fact]
    public void SelectExternalSubtitle_WhenExactMatchExists_SelectsMatchingSubtitleRegardlessOfOrder()
    {
        const string video = @"C:\Movies\movie.mkv";
        const string exact = @"C:\Movies\movie.srt";
        const string other = @"C:\Movies\other.srt";
        const string fuzzy = @"C:\Movies\movie.720p.bluray.srt";

        Assert.Equal(exact, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [other, fuzzy, exact]));
        Assert.Equal(exact, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [exact, fuzzy, other]));
    }

    [Fact]
    public void SelectExternalSubtitle_WhenMultipleExtensionsMatch_PrefersHighestRankedExtension()
    {
        const string video = @"C:\Movies\movie.mkv";
        const string txt = @"C:\Movies\movie.txt";
        const string srt = @"C:\Movies\movie.srt";
        const string sub = @"C:\Movies\movie.sub";
        const string mpl = @"C:\Movies\movie.mpl";

        Assert.Equal(txt, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [mpl, sub, srt, txt]));
        Assert.Equal(srt, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [mpl, sub, srt]));
        Assert.Equal(sub, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [mpl, sub]));
        Assert.Equal(mpl, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [mpl]));

        // Extension priority is evaluated before filename similarity, including an exact match.
        const string unrelatedTxt = @"C:\Movies\different.txt";
        Assert.Equal(unrelatedTxt, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [srt, unrelatedTxt]));
    }

    [Fact]
    public void SelectExternalSubtitle_WhenExactMatchMissing_SelectsClosestFuzzyMatch()
    {
        const string video = @"C:\Movies\movie.mkv";
        const string closest = @"C:\Movies\movie.720p.bluray.srt";
        const string unrelated = @"C:\Movies\different.srt";

        Assert.Equal(closest, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [unrelated, closest]));
        Assert.Equal(closest, AutomaticSubtitleSelector.SelectExternalSubtitle(video, [closest, unrelated]));
    }

    [Fact]
    public void SelectExternalSubtitle_WhenNoSupportedSubtitleExists_ReturnsNull()
    {
        Assert.Null(AutomaticSubtitleSelector.SelectExternalSubtitle(
            @"C:\Movies\movie.mkv", [@"C:\Movies\movie.doc", @"C:\Movies\movie.exe"]));
    }

    [Fact]
    public void SelectPreferredAudio_PrefersPolishOverEnglishAndEnglishOverOthers()
    {
        var german = new EmbeddedTrackCandidate(0, "ger", null, null);
        var english = new EmbeddedTrackCandidate(1, "eng", null, null);
        var polish = new EmbeddedTrackCandidate(4, "pol", null, null);
        var lowerIdPolish = new EmbeddedTrackCandidate(2, "pol", null, null);

        Assert.Equal(lowerIdPolish.Id, AutomaticSubtitleSelector.SelectPreferredAudio(
            [german, english, polish, lowerIdPolish]));
        Assert.Equal(lowerIdPolish.Id, AutomaticSubtitleSelector.SelectPreferredAudio(
            [lowerIdPolish, polish, english, german]));
        Assert.Equal(english.Id, AutomaticSubtitleSelector.SelectPreferredAudio([german, english]));
        Assert.Null(AutomaticSubtitleSelector.SelectPreferredAudio([german]));
        Assert.True(AutomaticSubtitleSelector.IsPolishAudio(polish));
        Assert.False(AutomaticSubtitleSelector.IsPolishAudio(english));
    }

    [Fact]
    public void SelectPreferredSubtitle_PrefersStandardPolishOverForcedOrEnglish()
    {
        var forcedPolish = new EmbeddedTrackCandidate(0, "pol", null, "forced");
        var standardEnglish = new EmbeddedTrackCandidate(1, "eng", "Main", null);
        var sdhPolish = new EmbeddedTrackCandidate(2, "pol", "SDH", null);
        var standardPolish = new EmbeddedTrackCandidate(4, "pol", "Main", null);
        var higherIdStandardPolish = new EmbeddedTrackCandidate(5, "pol", "Main", null);
        var ccEnglish = new EmbeddedTrackCandidate(0, "eng", null, "English CC");
        var german = new EmbeddedTrackCandidate(0, "ger", null, null);

        Assert.Equal(standardPolish.Id, AutomaticSubtitleSelector.SelectPreferredSubtitle(
            [forcedPolish, standardEnglish, sdhPolish, higherIdStandardPolish, standardPolish]));
        Assert.Equal(standardPolish.Id, AutomaticSubtitleSelector.SelectPreferredSubtitle(
            [standardPolish, higherIdStandardPolish, sdhPolish, standardEnglish, forcedPolish]));
        Assert.Equal(forcedPolish.Id, AutomaticSubtitleSelector.SelectPreferredSubtitle([standardEnglish, forcedPolish]));
        Assert.Equal(standardEnglish.Id, AutomaticSubtitleSelector.SelectPreferredSubtitle([ccEnglish, standardEnglish]));
        Assert.Equal(ccEnglish.Id, AutomaticSubtitleSelector.SelectPreferredSubtitle([german, ccEnglish]));
        Assert.Null(AutomaticSubtitleSelector.SelectPreferredSubtitle([german]));
    }
}
