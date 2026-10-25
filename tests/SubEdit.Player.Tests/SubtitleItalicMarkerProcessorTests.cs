using System.Collections.Generic;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleItalicMarkerProcessorTests
{
    private static readonly HashSet<char> SlashMarker = ['/'];
    private static readonly HashSet<char> AsteriskMarker = ['*'];
    private static readonly HashSet<char> HashMarker = ['#'];

    [Fact]
    public void Convert_SingleColorCodeBeforeSlashItalics_ConvertsItalicsAndPreservesColorPrefix()
    {
        const string input = "{100}{200}{c:$0000FF}/Tekst pochyły/";

        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}{c:$0000FF}<i>Tekst pochyły</i>", result.Text);
        Assert.Equal(1, result.ChangedLineCount);
    }

    [Fact]
    public void Convert_MultipleControlCodesBeforeSlashItalics_ConvertsItalicsAndPreservesAllCodes()
    {
        const string input = "{100}{200}{c:$0000FF}{f:Times}{s:18}/Tekst pochyły/";

        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}{c:$0000FF}{f:Times}{s:18}<i>Tekst pochyły</i>", result.Text);
        Assert.Equal(1, result.ChangedLineCount);
    }

    [Fact]
    public void Convert_MultipleControlCodes_PreservesExactOriginalOrderOfPrefixCodes()
    {
        const string input = "{100}{200}{s:18}{c:$0000FF}{f:Times}/Tekst/";

        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}{s:18}{c:$0000FF}{f:Times}<i>Tekst</i>", result.Text);
        Assert.Equal(1, result.ChangedLineCount);
    }

    [Fact]
    public void Convert_NoControlCodes_ConvertsSlashItalicsNormally()
    {
        const string input = "{100}{200}/Tekst pochyły/";

        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}<i>Tekst pochyły</i>", result.Text);
        Assert.Equal(1, result.ChangedLineCount);
    }

    [Theory]
    [InlineData("{100}{200}{c:$0000FF}/Tekst z samym początkowym", "{100}{200}{c:$0000FF}<i>Tekst z samym początkowym</i>", 1)]
    [InlineData("{100}{200}{c:$0000FF}Tekst z samym końcowym/", "{100}{200}{c:$0000FF}<i>Tekst z samym końcowym</i>", 1)]
    [InlineData("{100}{200}{c:$0000FF}Zwykły tekst bez markerów", "{100}{200}{c:$0000FF}Zwykły tekst bez markerów", 0)]
    [InlineData("{100}{200}{c:$0000FF}/", "{100}{200}{c:$0000FF}/", 0)]
    [InlineData("{100}{200}{c:$0000FF}//", "{100}{200}{c:$0000FF}//", 0)]
    public void Convert_IncompleteSlashMarkers_PreservesExistingSemantics(string input, string expected, int expectedChangedLines)
    {
        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal(expected, result.Text);
        Assert.Equal(expectedChangedLines, result.ChangedLineCount);
    }

    [Fact]
    public void Convert_MultiSegmentSeparatedByPipe_HandlesControlCodesPerSegment()
    {
        const string input = "{100}{200}{c:$0000FF}/Linia pierwsza/|{c:$00FF00}/Linia druga/";

        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}{c:$0000FF}<i>Linia pierwsza</i>|{c:$00FF00}<i>Linia druga</i>", result.Text);
        Assert.Equal(1, result.ChangedLineCount);
    }

    [Fact]
    public void Convert_MultiSegmentSeparatedByPipe_MixedItalicAndPlainSegments()
    {
        const string input = "{100}{200}{c:$0000FF}/Linia pierwsza/|{c:$00FF00}Linia druga";

        var result = SubtitleItalicMarkerProcessor.Convert(input, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}{c:$0000FF}<i>Linia pierwsza</i>|{c:$00FF00}Linia druga", result.Text);
        Assert.Equal(1, result.ChangedLineCount);
    }

    [Fact]
    public void Convert_TmPlayerAndMpl2Formats_PreservesLeadingCodes()
    {
        const string tmPlayer = "00:00:10:{c:$0000FF}/Tekst pochyły/";
        const string mpl2 = "[100][200]{c:$0000FF}/Tekst pochyły/";

        var tmResult = SubtitleItalicMarkerProcessor.Convert(tmPlayer, SlashMarker, framesPerSecond: 25.0);
        var mplResult = SubtitleItalicMarkerProcessor.Convert(mpl2, SlashMarker, framesPerSecond: 25.0);

        Assert.Equal("00:00:10:{c:$0000FF}<i>Tekst pochyły</i>", tmResult.Text);
        Assert.Equal("[100][200]{c:$0000FF}<i>Tekst pochyły</i>", mplResult.Text);
    }

    [Fact]
    public void Convert_AlternativeItalicMarkers_AsteriskAndHashSupported()
    {
        const string asteriskInput = "{100}{200}{c:$0000FF}*Tekst gwiazdkowy*";
        const string hashInput = "{100}{200}{c:$0000FF}#Tekst z hashem#";

        var asteriskResult = SubtitleItalicMarkerProcessor.Convert(asteriskInput, AsteriskMarker, framesPerSecond: 25.0);
        var hashResult = SubtitleItalicMarkerProcessor.Convert(hashInput, HashMarker, framesPerSecond: 25.0);

        Assert.Equal("{100}{200}{c:$0000FF}<i>Tekst gwiazdkowy</i>", asteriskResult.Text);
        Assert.Equal("{100}{200}{c:$0000FF}<i>Tekst z hashem</i>", hashResult.Text);
    }
}
