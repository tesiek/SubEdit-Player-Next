using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleTimingFormatterTests
{
    [Theory]
    [InlineData(0, (int)SubtitleTimingFormat.TmPlayerLong, "00:00:00:")]
    [InlineData(0, (int)SubtitleTimingFormat.TmPlayerShort, "0:00:00:")]
    [InlineData(57, (int)SubtitleTimingFormat.TmPlayerLong, "00:00:57:")]
    [InlineData(3661, (int)SubtitleTimingFormat.TmPlayerLong, "01:01:01:")]
    [InlineData(3661, (int)SubtitleTimingFormat.TmPlayerShort, "1:01:01:")]
    [InlineData(-24, (int)SubtitleTimingFormat.TmPlayerLong, "00:00:-24:")]
    [InlineData(-24, (int)SubtitleTimingFormat.TmPlayerShort, "00:00:-24:")]
    [InlineData(-3599, (int)SubtitleTimingFormat.TmPlayerLong, "00:-59:-59:")]
    [InlineData(-3604, (int)SubtitleTimingFormat.TmPlayerLong, "-1:00:-4:")]
    [InlineData(-3604, (int)SubtitleTimingFormat.TmPlayerShort, "-1:00:-4:")]
    [InlineData(-6640, (int)SubtitleTimingFormat.TmPlayerLong, "-1:-50:-40:")]
    public void FormatTmPlayerSeconds_ProducesExpectedHistoricalSignedString(long totalSeconds, int formatValue, string expected)
    {
        var formatted = SubtitleTimingFormatter.FormatTmPlayerSeconds(totalSeconds, (SubtitleTimingFormat)formatValue);
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("00", "00", "00", 0)]
    [InlineData("0", "00", "57", 57)]
    [InlineData("01", "01", "01", 3661)]
    [InlineData("00", "00", "-24", -24)]
    [InlineData("00", "-59", "-59", -3599)]
    [InlineData("-1", "00", "-4", -3604)]
    [InlineData("-1", "-50", "-40", -6640)]
    [InlineData("-01", "-50", "-40", -6640)]
    public void TryParseTmPlayerTime_ParsesSignedComponentsCorrectly(string hours, string minutes, string seconds, long expectedTotalSeconds)
    {
        var success = SubtitleTimingFormatter.TryParseTmPlayerTime(hours, minutes, seconds, out var totalSeconds);
        Assert.True(success);
        Assert.Equal(expectedTotalSeconds, totalSeconds);
    }

    [Theory]
    [InlineData(0, "00:00:00,000")]
    [InlineData(1234, "00:00:01,234")]
    [InlineData(3661050, "01:01:01,050")]
    [InlineData(-24000, "-00:00:24,000")]
    [InlineData(-3604005, "-01:00:04,005")]
    public void FormatSrtTimestamp_FormatsWorkingSignedTimestamps(long milliseconds, string expected)
    {
        var formatted = SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, ',');
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("00:00:00,000", 0)]
    [InlineData("00:00:01,234", 1234)]
    [InlineData("01:01:01.050", 3661050)]
    [InlineData("-00:00:24,000", -24000)]
    [InlineData("-01:00:04,005", -3604005)]
    [InlineData("-1:00:04,005", -3604005)]
    public void TryParseSrtTimestamp_ParsesSignedTimestampsCorrectly(string text, long expectedMilliseconds)
    {
        var success = SubtitleTimingFormatter.TryParseSrtTimestamp(text, out var milliseconds);
        Assert.True(success);
        Assert.Equal(expectedMilliseconds, milliseconds);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(-0.5, -1)]
    [InlineData(1.5, 2)]
    [InlineData(-1.5, -2)]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -3)]
    [InlineData(10.4, 10)]
    [InlineData(-10.4, -10)]
    [InlineData(10.6, 11)]
    [InlineData(-10.6, -11)]
    public void RoundAwayFromZero_IsStrictlySymmetric(double value, long expected)
    {
        var result = SubtitleTimingFormatter.RoundAwayFromZero(value);
        Assert.Equal(expected, result);

        var negResult = SubtitleTimingFormatter.RoundAwayFromZero(-value);
        Assert.Equal(-result, negResult);
    }

    [Fact]
    public void TryUpdateSrtTimingLine_Start_UpdatesStartTimeOnly()
    {
        var line = "00:00:01,000 --> 00:00:03,000";
        var success = SubtitleTimingFormatter.TryUpdateSrtTimingLine(
            line,
            positionMilliseconds: 2500,
            insertStart: true,
            preserveDuration: false,
            out var updatedLine);

        Assert.True(success);
        Assert.Equal("00:00:02,500 --> 00:00:03,000", updatedLine);
        Assert.DoesNotContain("{", updatedLine);
        Assert.DoesNotContain("}", updatedLine);
        Assert.DoesNotContain("[", updatedLine);
        Assert.DoesNotContain("]", updatedLine);
    }

    [Fact]
    public void TryUpdateSrtTimingLine_End_UpdatesEndTimeOnly()
    {
        var line = "00:00:01,000 --> 00:00:03,000";
        var success = SubtitleTimingFormatter.TryUpdateSrtTimingLine(
            line,
            positionMilliseconds: 4000,
            insertStart: false,
            preserveDuration: false,
            out var updatedLine);

        Assert.True(success);
        Assert.Equal("00:00:01,000 --> 00:00:04,000", updatedLine);
        Assert.DoesNotContain("{", updatedLine);
        Assert.DoesNotContain("}", updatedLine);
        Assert.DoesNotContain("[", updatedLine);
        Assert.DoesNotContain("]", updatedLine);
    }

    [Fact]
    public void TryUpdateSrtTimingLine_PreservesDotSeparatorAndFormatting()
    {
        var line = "  00:00:01.000 --> 00:00:03.000  X1:100\r\n";
        var success = SubtitleTimingFormatter.TryUpdateSrtTimingLine(
            line,
            positionMilliseconds: 2500,
            insertStart: true,
            preserveDuration: false,
            out var updatedLine);

        Assert.True(success);
        Assert.Equal("  00:00:02.500 --> 00:00:03.000  X1:100\r\n", updatedLine);
    }

    [Fact]
    public void FindSrtTimingLineIndex_ResolvesCorrectTimingLine_FromAnyLineInCue()
    {
        var lines = new[]
        {
            "12",
            "00:00:01,000 --> 00:00:03,000",
            "Tekst napisu linia 1",
            "Tekst napisu linia 2",
            "",
            "13",
            "00:00:05,000 --> 00:00:07,000",
            "Kolejny napis"
        };

        // Caret on cue number line (12)
        Assert.Equal(1, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 0));
        // Caret on timing line
        Assert.Equal(1, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 1));
        // Caret on first text line
        Assert.Equal(1, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 2));
        // Caret on second text line
        Assert.Equal(1, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 3));
        // Caret on blank line between cues
        Assert.Equal(-1, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 4));
        // Caret on second cue number line (13)
        Assert.Equal(6, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 5));
        // Caret on second cue timing line
        Assert.Equal(6, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 6));
        // Caret on second cue text line
        Assert.Equal(6, SubtitleTimingFormatter.FindSrtTimingLineIndex(lines, 7));
    }

    [Fact]
    public void SubtitleTimingFormatter_Insert_OnSrtTimingLine_NeverProducesInlineBrackets()
    {
        var line = "00:00:01,000 --> 00:00:03,000";
        var result = SubtitleTimingFormatter.Insert(
            line,
            positionMilliseconds: 2500,
            framesPerSecond: 25.0,
            defaultFormat: SubtitleTimingFormat.MicroDvd,
            insertStart: true,
            out var prefixLength);

        Assert.Equal("00:00:02,500 --> 00:00:03,000", result);
        Assert.Equal(0, prefixLength);
        Assert.DoesNotContain("{", result);
        Assert.DoesNotContain("}", result);
        Assert.DoesNotContain("[", result);
        Assert.DoesNotContain("]", result);
    }

    [Fact]
    public void SubtitleTimingFormatter_Insert_InlineFormats_NoRegression()
    {
        // MicroDVD start update
        var microDvd = SubtitleTimingFormatter.Insert(
            "{100}{200}Kwestia",
            positionMilliseconds: 5000,
            framesPerSecond: 25.0,
            defaultFormat: SubtitleTimingFormat.MicroDvd,
            insertStart: true,
            out var prefixLen1);
        Assert.Equal("{125}{200}Kwestia", microDvd);
        Assert.Equal(10, prefixLen1);

        // MPL2 end update
        var mpl2 = SubtitleTimingFormatter.Insert(
            "[100][200]Kwestia",
            positionMilliseconds: 5000,
            framesPerSecond: 25.0,
            defaultFormat: SubtitleTimingFormat.Mpl2,
            insertStart: false,
            out var prefixLen2);
        Assert.Equal("[100][50]Kwestia", mpl2);
        Assert.Equal(9, prefixLen2);

        // TMPlayer start update
        var tmPlayer = SubtitleTimingFormatter.Insert(
            "00:00:10:Kwestia",
            positionMilliseconds: 15000,
            framesPerSecond: 25.0,
            defaultFormat: SubtitleTimingFormat.TmPlayerLong,
            insertStart: true,
            out var prefixLen3);
        Assert.Equal("00:00:15:Kwestia", tmPlayer);
        Assert.Equal(9, prefixLen3);
    }
}
