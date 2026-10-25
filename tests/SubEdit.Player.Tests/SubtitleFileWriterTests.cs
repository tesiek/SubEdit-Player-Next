using System.Security.Cryptography;
using System.Text;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public sealed class SubtitleFileWriterTests : IDisposable
{
    private readonly string _testDirectory;

    public SubtitleFileWriterTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _testDirectory = Path.Combine(Path.GetTempPath(), "subedit_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public void Write_Cp1250WithUnsupportedCharacter_ThrowsEncoderFallbackExceptionAndKeepsExistingFileBitIdentical()
    {
        // BUG-DOC-01 regression test
        var cp1250 = Encoding.GetEncoding(1250, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
        var targetFile = Path.Combine(_testDirectory, "existing_cp1250.txt");
        const string originalContent = "Pierwsza linia\r\nZażółć gęślą jaźń";

        File.WriteAllText(targetFile, originalContent, cp1250);
        var originalBytes = File.ReadAllBytes(targetFile);
        var originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
        Assert.True(originalBytes.Length > 0);

        const string invalidContent = originalContent + "\r\nNiedozwolony znak: \u0416 (Cyrillic)";

        var ex = Assert.Throws<EncoderFallbackException>(() =>
            SubtitleFileWriter.Write(targetFile, invalidContent, cp1250));

        Assert.Equal('\u0416', ex.CharUnknown);

        var afterBytes = File.ReadAllBytes(targetFile);
        var afterHash = Convert.ToHexString(SHA256.HashData(afterBytes));

        Assert.Equal(originalBytes.Length, afterBytes.Length);
        Assert.Equal(originalHash, afterHash);
        Assert.Equal(originalBytes, afterBytes);

        var tempFiles = Directory.GetFiles(_testDirectory, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void Write_ExistingDestination_ReplacesContentAndDeletesTempFile()
    {
        var targetFile = Path.Combine(_testDirectory, "existing_utf8.txt");
        File.WriteAllText(targetFile, "Stara tresc", Encoding.UTF8);

        const string newContent = "Nowa tresc z polskimi znakami: ąćęłńóśźż";
        SubtitleFileWriter.Write(targetFile, newContent, Encoding.UTF8);

        var readBack = File.ReadAllText(targetFile, Encoding.UTF8);
        Assert.Equal(newContent, readBack);

        var tempFiles = Directory.GetFiles(_testDirectory, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void Write_NewDestination_CreatesFileAndDeletesTempFile()
    {
        var targetFile = Path.Combine(_testDirectory, "brand_new_subtitles.txt");
        Assert.False(File.Exists(targetFile));

        const string content = "{100}{200}Pierwszy napis\r\n{210}{300}Drugi napis";
        SubtitleFileWriter.Write(targetFile, content, Encoding.UTF8);

        Assert.True(File.Exists(targetFile));
        Assert.Equal(content, File.ReadAllText(targetFile, Encoding.UTF8));

        var tempFiles = Directory.GetFiles(_testDirectory, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Theory]
    [InlineData("utf8_bom.txt", "Utf8Bom")]
    [InlineData("utf8_nobom.txt", "Utf8")]
    [InlineData("utf16_le.txt", "Utf16LittleEndianBom")]
    [InlineData("utf16_be.txt", "Utf16BigEndianBom")]
    [InlineData("cp1250.txt", "Ansi")]
    public void Write_PreservesEncodingPreambleAndBytes(string fileName, string encodingKindName)
    {
        var encodingKind = Enum.Parse<SubtitleEncodingKind>(encodingKindName);
        var targetFile = Path.Combine(_testDirectory, fileName);
        var encoding = SubtitleFileReader.CreateEncoding(encodingKind);
        const string text = "Wersja testowa kodowania: Zażółć gęślą jaźń";

        SubtitleFileWriter.Write(targetFile, text, encoding);

        var fileContent = SubtitleFileReader.Read(targetFile);
        Assert.Equal(text, fileContent.Text);
        Assert.Equal(encodingKind, SubtitleFileReader.GetEncodingKind(fileContent.Encoding));

        var preamble = encoding.GetPreamble();
        var rawBytes = File.ReadAllBytes(targetFile);
        if (preamble.Length > 0)
        {
            Assert.True(rawBytes.Length >= preamble.Length);
            Assert.Equal(preamble, rawBytes[..preamble.Length]);
        }
        else
        {
            // For UTF-8 without BOM, verify it doesn't accidentally start with EF BB BF
            if (encodingKind == SubtitleEncodingKind.Utf8)
            {
                Assert.False(rawBytes.Length >= 3 && rawBytes[0] == 0xef && rawBytes[1] == 0xbb && rawBytes[2] == 0xbf);
            }
        }
    }

    [Fact]
    public void Write_ReadOnlyDestination_FailsSafelyWithoutModifyingTargetAndCleansUpTemp()
    {
        var targetFile = Path.Combine(_testDirectory, "readonly_target.txt");
        const string originalContent = "Oryginalna tresc tylko do odczytu";
        File.WriteAllText(targetFile, originalContent, Encoding.UTF8);
        File.SetAttributes(targetFile, FileAttributes.ReadOnly);

        try
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                SubtitleFileWriter.Write(targetFile, "Proba modyfikacji", Encoding.UTF8));

            File.SetAttributes(targetFile, FileAttributes.Normal);
            Assert.Equal(originalContent, File.ReadAllText(targetFile, Encoding.UTF8));

            var tempFiles = Directory.GetFiles(_testDirectory, "*.tmp");
            Assert.Empty(tempFiles);
        }
        finally
        {
            if (File.Exists(targetFile))
            {
                File.SetAttributes(targetFile, FileAttributes.Normal);
            }
        }
    }

    [Fact]
    public void Write_BeforeReplaceCallback_ExecutedBeforeTargetIsReplaced()
    {
        var targetFile = Path.Combine(_testDirectory, "backup_test.txt");
        const string originalContent = "Zawartosc wersji 1";
        File.WriteAllText(targetFile, originalContent, Encoding.UTF8);

        var backupPath = targetFile + ".bak";
        var backupExecuted = false;

        SubtitleFileWriter.Write(
            targetFile,
            "Zawartosc wersji 2",
            Encoding.UTF8,
            path =>
            {
                backupExecuted = true;
                Assert.Equal(targetFile, path);
                // When backup callback is executed, targetFile still contains originalContent!
                Assert.Equal(originalContent, File.ReadAllText(path, Encoding.UTF8));
                File.Copy(path, backupPath, true);
            });

        Assert.True(backupExecuted);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(originalContent, File.ReadAllText(backupPath, Encoding.UTF8));
        Assert.Equal("Zawartosc wersji 2", File.ReadAllText(targetFile, Encoding.UTF8));
    }

    [Fact]
    public void Write_BeforeReplaceThrowsException_KeepsDestinationIntactAndCleansUpTemp()
    {
        var targetFile = Path.Combine(_testDirectory, "fail_backup_test.txt");
        const string originalContent = "Nienaruszona zawartosc";
        File.WriteAllText(targetFile, originalContent, Encoding.UTF8);

        Assert.Throws<InvalidOperationException>(() =>
            SubtitleFileWriter.Write(
                targetFile,
                "Nowa zawartosc",
                Encoding.UTF8,
                _ => throw new InvalidOperationException("Symulowany blad backupu")));

        Assert.Equal(originalContent, File.ReadAllText(targetFile, Encoding.UTF8));
        var tempFiles = Directory.GetFiles(_testDirectory, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void AnsiEncodingSaveErrorMessage_MatchesExpectedPolishText()
    {
        const string expectedMessage =
            "Tekst zawiera znaki, których nie można zapisać w kodowaniu ANSI (Windows-1250).\r\n\r\nUsuń lub zamień te znaki albo zmień kodowanie napisów na UTF-8.";

        Assert.Equal(expectedMessage, Form1.AnsiEncodingSaveErrorMessage);
    }
}
