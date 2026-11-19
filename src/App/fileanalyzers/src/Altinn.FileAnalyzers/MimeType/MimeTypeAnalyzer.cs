using Altinn.App.Core.Features.FileAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using MimeDetective;

namespace Altinn.FileAnalyzers.MimeType;

internal sealed class MimeTypeAnalyzer(
    IHttpContextAccessor _httpContextAccessor,
    IContentInspector _inspector
) : IFileAnalyzer
{
    /// <inheritDoc/>
    public string Id { get; private set; } = "mimeTypeAnalyser";

    /// <inheritDoc/>
    public Task<FileAnalysisResult> Analyze(Stream stream, string? filename = null)
    {
        // Allow synchronous IO access for the usage of MimeDetective
        // which does not have async methods. This on a per request basis.
        var syncIOFeature =
            _httpContextAccessor.HttpContext?.Features.Get<IHttpBodyControlFeature>();
        if (syncIOFeature is not null)
            syncIOFeature.AllowSynchronousIO = true;

        // Plain text has no signature for the inspector to match, so its content is checked
        // separately, which needs a second read of the stream.
        string extension = Path.GetExtension(filename) ?? "";
        string? plainTextMimeType = PlainText.MimeTypeForExtension(extension);
        using var buffer =
            plainTextMimeType is not null && !stream.CanSeek ? new MemoryStream() : null;
        if (buffer is not null)
        {
            stream.CopyTo(buffer);
            buffer.Position = 0;
            stream = buffer;
        }
        long start = stream.CanSeek ? stream.Position : 0;

        var results = _inspector.Inspect(stream);

        // We only care for the 100% match anything else is considered unsafe.
        var match = results
            .OrderByDescending(match => match.Points)
            .FirstOrDefault(match => match.Percentage == 1);

        var fileAnalysisResult = new FileAnalysisResult(Id);
        if (
            plainTextMimeType is not null
            && (match is null || match.Definition.File.MimeType == PlainTextMimeType)
            && IsPlainText(stream, start)
        )
        {
            fileAnalysisResult.Extensions = [extension[1..].ToLowerInvariant()];
            fileAnalysisResult.MimeType = plainTextMimeType;
            fileAnalysisResult.Filename = filename;
        }
        else if (match is not null)
        {
            fileAnalysisResult.Extensions = match.Definition.File.Extensions.ToList();
            fileAnalysisResult.MimeType = match.Definition.File.MimeType;
            fileAnalysisResult.Filename = filename;
        }

        return Task.FromResult(fileAnalysisResult);
    }

    private const string PlainTextMimeType = "text/plain";

    private static bool IsPlainText(Stream stream, long start)
    {
        stream.Position = start;
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return PlainText.IsPlainText(content.GetBuffer().AsSpan(0, (int)content.Length));
    }
}
