using System.Text;

namespace Altinn.FileAnalyzers.MimeType;

/// <summary>
/// Recognizes plain text, which has no magic bytes for signature based detection to find.
/// </summary>
internal static class PlainText
{
    private static readonly Dictionary<string, string> _mimeTypeByExtension = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
    };

    /// <summary>
    /// Gets the MIME type for a plain text file extension such as ".txt", or null for other extensions.
    /// </summary>
    public static string? MimeTypeForExtension(string extension) =>
        _mimeTypeByExtension.GetValueOrDefault(extension);

    /// <summary>
    /// Whether the content is UTF-8, UTF-16 with a byte order mark or Windows-1252 text without
    /// control characters other than tab, line feed, form feed and carriage return.
    /// </summary>
    public static bool IsPlainText(ReadOnlySpan<byte> content)
    {
        if (content is [0xFF, 0xFE, ..])
            return IsUtf16Text(content[2..], bigEndian: false);
        if (content is [0xFE, 0xFF, ..])
            return IsUtf16Text(content[2..], bigEndian: true);
        if (content is [0xEF, 0xBB, 0xBF, ..])
            return IsUtf8Text(content[3..]);

        return IsUtf8Text(content) || IsWindows1252Text(content);
    }

    private static bool IsUtf8Text(ReadOnlySpan<byte> content)
    {
        while (!content.IsEmpty)
        {
            if (
                Rune.DecodeFromUtf8(content, out Rune rune, out int consumed)
                    != System.Buffers.OperationStatus.Done
                || IsDisallowedControl(rune.Value)
            )
                return false;
            content = content[consumed..];
        }
        return true;
    }

    private static bool IsUtf16Text(ReadOnlySpan<byte> content, bool bigEndian)
    {
        if (content.Length % 2 != 0)
            return false;

        bool expectLowSurrogate = false;
        for (int i = 0; i < content.Length; i += 2)
        {
            char c = bigEndian
                ? (char)(content[i] << 8 | content[i + 1])
                : (char)(content[i + 1] << 8 | content[i]);

            if (expectLowSurrogate != char.IsLowSurrogate(c))
                return false;
            expectLowSurrogate = char.IsHighSurrogate(c);
            if (IsDisallowedControl(c))
                return false;
        }
        return !expectLowSurrogate;
    }

    private static bool IsWindows1252Text(ReadOnlySpan<byte> content)
    {
        foreach (byte b in content)
        {
            // 0x80-0x9F are printable in Windows-1252, except these five unassigned bytes.
            if (b is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D || (b < 0x80 && IsDisallowedControl(b)))
                return false;
        }
        return true;
    }

    private static bool IsDisallowedControl(int codePoint) =>
        codePoint switch
        {
            '\t' or '\n' or '\f' or '\r' => false,
            < 0x20 or 0x7F => true,
            >= 0x80 and <= 0x9F => true,
            _ => false,
        };
}
