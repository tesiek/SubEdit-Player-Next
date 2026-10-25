using FlyleafLib.MediaPlayer;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class SeekAccuratePolicySelectorTests
{
    [Theory]
    [InlineData("matroska")]
    [InlineData("matroska,webm")]
    [InlineData("webm")]
    [InlineData("mov,mp4,m4a,3gp,3g2,mj2")]
    [InlineData("mp4")]
    [InlineData("mov")]
    [InlineData("m4a")]
    [InlineData("3gp")]
    [InlineData("3g2")]
    [InlineData("mj2")]
    [InlineData("avi")]
    public void Select_KnownConditionalFormats_ReturnsConditional(string formatName)
    {
        var result = SeekAccuratePolicySelector.Select(formatName);
        Assert.Equal(SeekAccurateMode.Conditional, result);
    }

    [Theory]
    [InlineData("mpegts")]
    [InlineData("mpegtsraw")] // unrecognized variant falls back to Legacy
    public void Select_KnownLegacyFormats_ReturnsLegacy(string formatName)
    {
        var result = SeekAccuratePolicySelector.Select(formatName);
        Assert.Equal(SeekAccurateMode.Legacy, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData(",,,")]
    public void Select_NullOrEmptyOrWhitespace_ReturnsLegacy(string? formatName)
    {
        var result = SeekAccuratePolicySelector.Select(formatName);
        Assert.Equal(SeekAccurateMode.Legacy, result);
    }

    [Theory]
    [InlineData("unknown_demuxer")]
    [InlineData("flv")]
    [InlineData("asf")]
    [InlineData("ogg")]
    [InlineData("rm")]
    [InlineData("wav")]
    public void Select_UnknownOrUnverifiedFormats_FallsBackToLegacy(string formatName)
    {
        var result = SeekAccuratePolicySelector.Select(formatName);
        Assert.Equal(SeekAccurateMode.Legacy, result);
    }

    [Theory]
    [InlineData("MATROSKA")]
    [InlineData("Matroska,WebM")]
    [InlineData("MOV,MP4,M4A,3GP,3G2,MJ2")]
    [InlineData("AVI")]
    [InlineData("MPEGTS")]
    public void Select_CaseInsensitive_HandlesVariousCasings(string formatName)
    {
        var result = SeekAccuratePolicySelector.Select(formatName);
        if (formatName.Equals("MPEGTS", System.StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(SeekAccurateMode.Legacy, result);
        }
        else
        {
            Assert.Equal(SeekAccurateMode.Conditional, result);
        }
    }

    [Theory]
    [InlineData("  matroska ,  webm  ", SeekAccurateMode.Conditional)]
    [InlineData("  mov , mp4  ", SeekAccurateMode.Conditional)]
    [InlineData("  avi  ", SeekAccurateMode.Conditional)]
    [InlineData("  mpegts  ", SeekAccurateMode.Legacy)]
    [InlineData(" , , avi , , ", SeekAccurateMode.Conditional)]
    [InlineData(" , , mpegts , , ", SeekAccurateMode.Legacy)]
    public void Select_WhitespaceAndExtraDelimiters_HandlesCleanly(string formatName, SeekAccurateMode expected)
    {
        var result = SeekAccuratePolicySelector.Select(formatName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Select_MixedFormatContainingKnownLegacy_PrioritizesLegacy()
    {
        // If a transport stream demuxer somehow reports both mpegts and an mp4 token,
        // legacy must be prioritized for safety.
        var result = SeekAccuratePolicySelector.Select("mp4,mpegts");
        Assert.Equal(SeekAccurateMode.Legacy, result);
    }

    [Fact]
    public void SeekAccurateMode_DefaultValue_IsLegacy()
    {
        Assert.Equal(SeekAccurateMode.Legacy, default(SeekAccurateMode));
    }

    [Fact]
    public void SeekAccurateMode_ExplicitEnumValues_MatchSafetyContract()
    {
        Assert.Equal(0, (int)SeekAccurateMode.Legacy);
        Assert.Equal(1, (int)SeekAccurateMode.Conditional);
    }
}


