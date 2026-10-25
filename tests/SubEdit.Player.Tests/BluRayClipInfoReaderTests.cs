using System.Buffers.Binary;
using System.Text;
using SubEdit.Player.Core.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

public sealed class BluRayClipInfoReaderTests
{
    [Fact]
    public void ParseClipInfoData_WithValidSyntheticHeader_ExtractsStreamPidCodingTypeAndLanguage()
    {
        foreach (var magic in new[] { "HDMV", "CLPI" })
        {
            var data = CreateSyntheticClipInfo();
            Encoding.ASCII.GetBytes(magic).CopyTo(data, 0);

            var result = BluRayClipInfoReader.ParseClipInfoData(data);

            var entry = Assert.Single(result);
            Assert.Equal(0x1100, entry.Key);
            Assert.Equal(0x1100, entry.Value.Pid);
            Assert.Equal((byte)0x81, entry.Value.CodingType);
            Assert.Equal("pol", entry.Value.LanguageCode);
        }

        var mixedCaseLanguage = CreateSyntheticClipInfo();
        Encoding.ASCII.GetBytes("PoL").CopyTo(mixedCaseLanguage, 59);
        Assert.Equal("pol", Assert.Single(BluRayClipInfoReader.ParseClipInfoData(mixedCaseLanguage)).Value.LanguageCode);

        var undefinedLanguage = CreateSyntheticClipInfo();
        Encoding.ASCII.GetBytes("und").CopyTo(undefinedLanguage, 59);
        Assert.Null(Assert.Single(BluRayClipInfoReader.ParseClipInfoData(undefinedLanguage)).Value.LanguageCode);
    }

    [Fact]
    public void ParseClipInfoData_WhenHeaderHasInvalidMagic_ReturnsEmptyDictionary()
    {
        var data = CreateSyntheticClipInfo();
        Encoding.ASCII.GetBytes("TEST").CopyTo(data, 0);

        Assert.Empty(BluRayClipInfoReader.ParseClipInfoData(data));
    }

    [Fact]
    public void ParseClipInfoData_WhenDataIsTruncatedOrCorrupt_ReturnsSafelyWithoutThrowing()
    {
        Assert.Empty(BluRayClipInfoReader.ParseClipInfoData([]));
        Assert.Empty(BluRayClipInfoReader.ParseClipInfoData(new byte[8]));

        var invalidOffset = CreateSyntheticClipInfo();
        BinaryPrimitives.WriteUInt32BigEndian(invalidOffset.AsSpan(12, 4), 255);
        Assert.Empty(BluRayClipInfoReader.ParseClipInfoData(invalidOffset));

        var data = CreateSyntheticClipInfo();
        Assert.Empty(BluRayClipInfoReader.ParseClipInfoData(data[..45]));
        Assert.Empty(BluRayClipInfoReader.ParseClipInfoData(data[..56]));

        // A truncated language leaves the already readable PID and coding type available.
        var partialResult = BluRayClipInfoReader.ParseClipInfoData(data[..61]);
        var entry = Assert.Single(partialResult);
        Assert.Equal(0x1100, entry.Key);
        Assert.Equal(0x1100, entry.Value.Pid);
        Assert.Equal((byte)0x81, entry.Value.CodingType);
        Assert.Null(entry.Value.LanguageCode);
    }

    [Fact]
    public void FindMatchingClpiPath_WhenStandardBdmvStructure_ResolvesClpiCandidatePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "subedit_clpi_tests_" + Guid.NewGuid().ToString("N"));

        try
        {
            var streamDirectory = Path.Combine(directory, "BDMV", "STREAM");
            var clipInfoDirectory = Path.Combine(directory, "BDMV", "CLIPINF");
            Directory.CreateDirectory(streamDirectory);
            Directory.CreateDirectory(clipInfoDirectory);
            var mediaPath = Path.Combine(streamDirectory, "00001.m2ts");
            var clpiPath = Path.Combine(clipInfoDirectory, "00001.clpi");
            File.WriteAllBytes(mediaPath, []);

            Assert.Null(BluRayClipInfoReader.FindMatchingClpiPath(mediaPath));

            File.WriteAllBytes(clpiPath, []);

            Assert.Equal(clpiPath, BluRayClipInfoReader.FindMatchingClpiPath(mediaPath));
            Assert.Equal(clpiPath, BluRayClipInfoReader.FindMatchingClpiPath(Path.ChangeExtension(mediaPath, ".mts")));
            Assert.Null(BluRayClipInfoReader.FindMatchingClpiPath(Path.ChangeExtension(mediaPath, ".mp4")));
            Assert.Null(BluRayClipInfoReader.FindMatchingClpiPath(Path.Combine(directory, "00001.m2ts")));
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
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

    private static byte[] CreateSyntheticClipInfo()
    {
        var data = new byte[62];
        Encoding.ASCII.GetBytes("HDMV0100").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12, 4), 40); // ProgramInfo offset.
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(40, 4), 18); // Payload length after this field.
        data[45] = 1; // One program sequence, after reserved byte 44.
        // Bytes 46-49: SPN = 0; bytes 50-51: program map PID = 0x1000.
        data[50] = 0x10;
        data[52] = 1; // One stream, followed by reserved byte 53.
        data[54] = 0x11; // Stream PID = 0x1100, big-endian.
        data[56] = 5; // Coding info length: type + audio format + three language bytes.
        data[57] = 0x81; // AC-3 coding type.
        data[58] = 0x31; // Audio format / sampling frequency; not exposed by this parser.
        Encoding.ASCII.GetBytes("pol").CopyTo(data, 59);
        return data;
    }
}
