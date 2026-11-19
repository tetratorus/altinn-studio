using System.Text;
using Altinn.App.Core.Features.FileAnalysis;
using Altinn.App.Core.Models.Validation;
using Altinn.FileAnalyzers.MimeType;
using Altinn.Platform.Storage.Interface.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MimeDetective;
using Moq;

namespace Altinn.FileAnalyzers.Tests.MimeType;

public class MimeTypePlainTextTests
{
    private static readonly byte[] _utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly MimeTypeAnalyzer _analyzer;

    public MimeTypePlainTextTests()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddMimeTypeValidation();
        var serviceProvider = services.BuildServiceProvider();
        _analyzer = new MimeTypeAnalyzer(
            new Mock<IHttpContextAccessor>().Object,
            serviceProvider.GetRequiredService<IContentInspector>()
        );
    }

    private Task<FileAnalysisResult> Analyze(byte[] content, string filename) =>
        _analyzer.Analyze(new MemoryStream(content), filename);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public async Task Analyze_Utf8TextWithoutBom_ReturnsTextPlain()
    {
        var result = await Analyze(Utf8("Hei på deg, blåbærsyltetøy\r\nLinje 2\n"), "notes.txt");

        Assert.Equal("text/plain", result.MimeType);
        Assert.Equal(["txt"], result.Extensions);
    }

    [Fact]
    public async Task Analyze_CsvWithoutBom_ReturnsTextCsv()
    {
        var result = await Analyze(Utf8("name;age\nKari;42\nOla;37\n"), "people.csv");

        Assert.Equal("text/csv", result.MimeType);
        Assert.Equal(["csv"], result.Extensions);
    }

    [Fact]
    public async Task Analyze_CsvWithUtf8Bom_ReturnsTextCsv()
    {
        var result = await Analyze([.. _utf8Bom, .. Utf8("name;age\nKari;42\n")], "Export.CSV");

        Assert.Equal("text/csv", result.MimeType);
    }

    [Fact]
    public async Task Analyze_Utf16LeWithBom_ReturnsTextPlain()
    {
        byte[] content = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("Æ, ø og å\r\n\tinnrykk\f")];

        var result = await Analyze(content, "notes.txt");

        Assert.Equal("text/plain", result.MimeType);
    }

    [Fact]
    public async Task Analyze_Utf16BeWithBom_ReturnsTextPlain()
    {
        byte[] content = [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("Æ, ø og å\n")];

        var result = await Analyze(content, "notes.txt");

        Assert.Equal("text/plain", result.MimeType);
    }

    [Fact]
    public async Task Analyze_Windows1252Text_ReturnsTextPlain()
    {
        // "Blåbær – 5 €" in Windows-1252, which is not valid UTF-8.
        byte[] content =
        [
            0x42,
            0x6C,
            0xE5,
            0x62,
            0xE6,
            0x72,
            0x20,
            0x96,
            0x20,
            0x35,
            0x20,
            0x80,
            0x0D,
            0x0A,
        ];

        var result = await Analyze(content, "notes.txt");

        Assert.Equal("text/plain", result.MimeType);
    }

    [Fact]
    public async Task Analyze_TextWithNullByte_ReturnsNoMimeType()
    {
        var result = await Analyze(Utf8("abc\0def\n"), "notes.txt");

        Assert.Null(result.MimeType);
    }

    [Fact]
    public async Task Analyze_TextWithOtherControlCharacter_ReturnsNoMimeType()
    {
        var result = await Analyze(Utf8("abc\u001Bdef\n"), "notes.txt");

        Assert.Null(result.MimeType);
    }

    [Fact]
    public async Task Analyze_PdfRenamedToTxt_IsNotTextPlain()
    {
        var stream = EmbeddedResource.LoadDataAsStream(
            "Altinn.FileAnalyzers.Tests.MimeType.example.pdf"
        );

        var result = await _analyzer.Analyze(stream, "example.txt");

        Assert.Equal("application/pdf", result.MimeType);
    }

    [Fact]
    public async Task Analyze_PlainTextWithOtherExtension_ReturnsNoMimeType()
    {
        var result = await Analyze(Utf8("<html><body>hi</body></html>\n"), "page.html");

        Assert.Null(result.MimeType);
    }

    [Fact]
    public async Task Analyze_PlainTextWithoutFilename_ReturnsNoMimeType()
    {
        var result = await _analyzer.Analyze(new MemoryStream(Utf8("hello\n")));

        Assert.Null(result.MimeType);
    }

    [Fact]
    public async Task Analyze_NonSeekableStream_ReturnsTextPlain()
    {
        var stream = new NonSeekableStream(Utf8("hello\n"));

        var result = await _analyzer.Analyze(stream, "notes.txt");

        Assert.Equal("text/plain", result.MimeType);
    }

    [Theory]
    [InlineData("notes.txt", "hello\n", true)]
    [InlineData("people.csv", "a;b\n1;2\n", true)]
    [InlineData("people.csv", "a\0b\n", false)]
    public async Task AnalyzeThenValidate_TextAndCsvAllowed(
        string filename,
        string content,
        bool expectedSuccess
    )
    {
        var dataType = new DataType()
        {
            Id = "attachment",
            EnabledFileAnalysers = ["mimeTypeAnalyser"],
            EnabledFileValidators = ["mimeTypeValidator"],
            AllowedContentTypes = ["text/plain", "text/csv"],
        };
        var analysis = await Analyze(Utf8(content), filename);

        (bool success, IEnumerable<ValidationIssue> errors) =
            await new MimeTypeValidator().Validate(dataType, [analysis]);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedSuccess, !errors.Any());
    }

    [Fact]
    public async Task AnalyzeThenValidate_CsvWithBom_AllowedWhenOnlyTextCsvAllowed()
    {
        var dataType = new DataType() { Id = "attachment", AllowedContentTypes = ["text/csv"] };
        var analysis = await Analyze([.. _utf8Bom, .. Utf8("a;b\n")], "export.csv");

        (bool success, _) = await new MimeTypeValidator().Validate(dataType, [analysis]);

        Assert.True(success);
    }

    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();

        public override long Position
        {
            get => base.Position;
            set => throw new NotSupportedException();
        }
    }
}
