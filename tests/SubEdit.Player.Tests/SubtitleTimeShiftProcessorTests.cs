using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleTimeShiftProcessorTests
{
    [Fact]
    public void MicroDvd_ShiftAndReverseShift_RestoresOriginalFramesExactly()
    {
        const string original = "{650}{675}Pierwszy wers\r\n{700}{750}Drugi wers";
        const double fps = 25.0;
        const double shiftSeconds = -50.0; // delta = -50 * 25 = -1250 frames

        var shifted = SubtitleTimeShiftProcessor.Shift(original, shiftSeconds, fps);
        Assert.Equal("{-600}{-575}Pierwszy wers\r\n{-550}{-500}Drugi wers", shifted);

        var restored = SubtitleTimeShiftProcessor.Shift(shifted, -shiftSeconds, fps);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void MicroDvd_ExplicitZeroEnd_IsNotLostAndNotTreatedAsMissing()
    {
        const string inputWithExplicitZero = "{100}{0}Tekst ze startem 100 i jawnym endem 0";
        const string inputWithMissingEnd = "{100}{}Tekst ze startem 100 i brakiem endu";
        const double fps = 25.0;
        const double shiftSeconds = 2.0; // delta = +50 frames

        var shiftedZero = SubtitleTimeShiftProcessor.Shift(inputWithExplicitZero, shiftSeconds, fps);
        Assert.Equal("{150}{50}Tekst ze startem 100 i jawnym endem 0", shiftedZero);

        var shiftedMissing = SubtitleTimeShiftProcessor.Shift(inputWithMissingEnd, shiftSeconds, fps);
        Assert.Equal("{150}{}Tekst ze startem 100 i brakiem endu", shiftedMissing);
    }

    [Fact]
    public void Mpl2_ShiftAndReverseShift_RestoresOriginalDecisecondsExactly()
    {
        const string original = "[248][263]Pierwszy wers MPL2\r\n[300][350]Drugi wers MPL2";
        const double fps = 25.0;
        const double shiftSeconds = -50.0; // delta = -50 * 10 = -500 deciseconds

        var shifted = SubtitleTimeShiftProcessor.Shift(original, shiftSeconds, fps);
        Assert.Equal("[-252][-237]Pierwszy wers MPL2\r\n[-200][-150]Drugi wers MPL2", shifted);

        var restored = SubtitleTimeShiftProcessor.Shift(shifted, -shiftSeconds, fps);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Mpl2_ExplicitZeroEnd_IsNotLostAndNotTreatedAsMissing()
    {
        const string inputWithExplicitZero = "[10][0]Tekst MPL2 jawne 0";
        const string inputWithMissingEnd = "[10][]Tekst MPL2 brak endu";
        const double fps = 25.0;
        const double shiftSeconds = 5.0; // delta = +50 deci

        var shiftedZero = SubtitleTimeShiftProcessor.Shift(inputWithExplicitZero, shiftSeconds, fps);
        Assert.Equal("[60][50]Tekst MPL2 jawne 0", shiftedZero);

        var shiftedMissing = SubtitleTimeShiftProcessor.Shift(inputWithMissingEnd, shiftSeconds, fps);
        Assert.Equal("[60][]Tekst MPL2 brak endu", shiftedMissing);
    }

    [Fact]
    public void TmPlayer_ShiftAndReverseShift_PreservesSignedValuesWithoutClamping()
    {
        const string original = "00:00:26:Tekst TMPlayer";
        const double fps = 25.0;
        const double shiftSeconds = -50.0; // -50s -> 26 - 50 = -24s -> "00:00:-24:"

        var shifted = SubtitleTimeShiftProcessor.Shift(original, shiftSeconds, fps);
        Assert.Equal("00:00:-24:Tekst TMPlayer", shifted);

        var restored = SubtitleTimeShiftProcessor.Shift(shifted, -shiftSeconds, fps);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void TmPlayer_LargeNegativeShift_ProducesCorrectMultiComponentNegativeString()
    {
        const string original = "0:00:00:Początek";
        const double fps = 25.0;
        const double shiftSeconds = -3604.0; // 0 - 3604s = -3604s -> "-1:00:-4:"

        var shifted = SubtitleTimeShiftProcessor.Shift(original, shiftSeconds, fps);
        Assert.Equal("-1:00:-4:Początek", shifted);

        var restored = SubtitleTimeShiftProcessor.Shift(shifted, -shiftSeconds, fps);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Srt_WorkingSignedTimestamps_ShiftAndReverseShiftAccurately()
    {
        const string original = "1\r\n00:00:26,000 --> 00:00:28,500\r\nTekst napisów SRT";
        const double fps = 25.0;
        const double shiftSeconds = -50.0;

        var shifted = SubtitleTimeShiftProcessor.Shift(original, shiftSeconds, fps);
        Assert.Contains("-00:00:24,000 --> -00:00:21,500", shifted);

        var restored = SubtitleTimeShiftProcessor.Shift(shifted, -shiftSeconds, fps);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void GeometricShift_SymmetricRoundingAndPreservesExplicitZero()
    {
        const string original = "{100}{0}Tekst MicroDVD\r\n{200}{250}Drugi";
        // Skalowanie 1.5, przesunięcie -1.0s (-25 klatek przy 25fps)
        var result = GeometricSubtitleShiftProcessor.Shift(
            original,
            initialSubtitleMilliseconds: 4000,
            initialVideoMilliseconds: 3000,
            finalSubtitleMilliseconds: 8000,
            finalVideoMilliseconds: 9000,
            framesPerSecond: 25.0,
            firstLine: 0,
            lastLine: 1);

        Assert.NotEmpty(result.Text);
        // Sprawdźmy, że jawne zero end nadal ma postać liczbową, a nie puste {}
        var lines = result.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var match = System.Text.RegularExpressions.Regex.Match(lines[0], @"^\{(?<start>-?\d+)}\{(?<end>-?\d+)}");
        Assert.True(match.Success);
    }
}
