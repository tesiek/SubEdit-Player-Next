using System.Globalization;
using SubEdit.Player.Core.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

public sealed class MediaInspectorTests
{
    [Fact]
    public void GetFourCc_WithValidUInt32_DecodesCorrectAsciiString()
    {
        Assert.Equal("H264", MediaInspector.GetFourCc(0x34363248u));
        Assert.Equal("XVID", MediaInspector.GetFourCc(0x44495658u));
    }

    [Fact]
    public void GetFourCc_WithZeroOrNonPrintableChars_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, MediaInspector.GetFourCc(0u));
        Assert.Equal(string.Empty, MediaInspector.GetFourCc(0x3436320Au));
        Assert.Equal(string.Empty, MediaInspector.GetFourCc(0x44495680u));
        Assert.Equal(string.Empty, MediaInspector.GetFourCc(0x20202020u));
    }

    [Fact]
    public void NormalizeVideoCodecName_StandardizesKnownAliasesToCanonicalNames()
    {
        foreach (var alias in new[] { "h264", "h.264", "avc", "avc1", "V_MPEG4/ISO/AVC" })
        {
            Assert.Equal("H.264", MediaInspector.NormalizeVideoCodecName(alias));
        }

        foreach (var alias in new[] { "h265", "h.265", "hevc", "hvc1", "V_MPEGH/ISO/HEVC" })
        {
            Assert.Equal("H.265", MediaInspector.NormalizeVideoCodecName(alias));
        }

        foreach (var alias in new[] { "mpeg2video", "mpeg2", "mpeg-2", "V_MPEG2" })
        {
            Assert.Equal("MPEG2", MediaInspector.NormalizeVideoCodecName(alias));
        }

        Assert.Equal("H.264", MediaInspector.NormalizeVideoCodecName("  AvC1  "));
        Assert.Equal("H.265", MediaInspector.NormalizeVideoCodecName("  HeVc  "));
        Assert.Equal("MPEG2", MediaInspector.NormalizeVideoCodecName("  MpEg2ViDeO  "));
        Assert.Equal("CustomCodec", MediaInspector.NormalizeVideoCodecName("  CustomCodec  "));
        Assert.Equal(string.Empty, MediaInspector.NormalizeVideoCodecName(null));
        Assert.Equal(string.Empty, MediaInspector.NormalizeVideoCodecName(string.Empty));
        Assert.Equal(string.Empty, MediaInspector.NormalizeVideoCodecName(" \t "));
    }

    [Fact]
    public void GetCodecName_ExtractsAndNormalizesCodecFromDescriptionOrFourCc()
    {
        Assert.Equal("H.264", MediaInspector.GetCodecName(null, "hvc1", "h264 - MPEG-4 AVC (part 10)"));
        Assert.Equal("H.265", MediaInspector.GetCodecName(null, "avc1", "hevc(H.265)"));
        Assert.Equal("MPEG2", MediaInspector.GetCodecName(null, null, "mpeg2video-MPEG-2 video"));
        Assert.Equal("H.264", MediaInspector.GetCodecName(null, "avc1", null));
        Assert.Equal("H.265", MediaInspector.GetCodecName(null, "hvc1", " \t "));
        Assert.Equal(string.Empty, MediaInspector.GetCodecName(null, null, null));

        // An explicit AVI FourCC takes precedence without reading a file or normalizing its alias.
        Assert.Equal("H264", MediaInspector.GetCodecName("video.AVI", "h264", "hevc (H.265)"));
    }

    [Fact]
    public void TryGetFileSize_ReturnsFormattedSizeForExistingFile_AndEmptyForMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "subedit_media_inspector_" + Guid.NewGuid().ToString("N") + ".bin");
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                stream.SetLength(5L * 1024 * 1024);
            }

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("5.0 MB", MediaInspector.TryGetFileSize(path));
            Assert.Equal(string.Empty, MediaInspector.TryGetFileSize(path + ".missing"));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal("5,0 MB", MediaInspector.TryGetFileSize(path));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup must not mask the test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup must not mask the test result.
            }
        }
    }
}
