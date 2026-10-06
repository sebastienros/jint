using System;
using System.Text;

namespace Jint.HtmlParser.Css.Syntax;

/// <summary>
/// CSS Syntax §3.2 (Editor's Draft, 2026-09) "determine the fallback encoding" of a style sheet's bytes.
/// </summary>
/// <remarks>
/// The parser owns the byte-level encoding declaration; the host owns the Encoding Standard's label table
/// and decoders, supplied as a "get an encoding" delegate (: a canonical name, or
/// <see langword="null"/> for failure). The caller then runs the Encoding Standard "decode" with the
/// result, which lets a byte order mark override it.
/// </remarks>
internal static class CssStyleSheetEncoding
{
    private const int DeclarationWindow = 1024;
    private static ReadOnlySpan<byte> Prefix => "@charset \""u8;

    internal static string DetermineFallback(ReadOnlySpan<byte> bytes, string? protocolLabel, string? environmentEncoding,
        Func<string, string?> getEncoding)
    {
        ArgumentNullException.ThrowIfNull(getEncoding);
        if (protocolLabel is not null && getEncoding(protocolLabel) is { } fromProtocol) return fromProtocol;
        if (TryReadDeclarationLabel(bytes, out var label) && getEncoding(label) is { } declared)
        {
            return declared.Equals("utf-16be", StringComparison.OrdinalIgnoreCase)
                || declared.Equals("utf-16le", StringComparison.OrdinalIgnoreCase) ? "utf-8" : declared;
        }
        return environmentEncoding ?? "utf-8";
    }

    /// <summary>
    /// The label of a leading <c>@charset "…";</c> declaration, matched byte for byte within the first
    /// 1024 bytes: <c>40 63 68 61 72 73 65 74 20 22 XX* 22 3B</c> with each XX in 0x00–0x21 or 0x23–0x7F.
    /// </summary>
    internal static bool TryReadDeclarationLabel(ReadOnlySpan<byte> bytes, out string label)
    {
        label = string.Empty;
        if (bytes.Length > DeclarationWindow) bytes = bytes[..DeclarationWindow];
        if (!bytes.StartsWith(Prefix)) return false;
        var rest = bytes[Prefix.Length..];
        var end = rest.IndexOf((byte) '"');
        if (end < 0 || end + 1 >= rest.Length || rest[end + 1] != (byte) ';') return false;
        var value = rest[..end];
        if (value.IndexOfAnyInRange((byte) 0x80, (byte) 0xFF) >= 0) return false;
        label = Encoding.ASCII.GetString(value);
        return true;
    }
}
