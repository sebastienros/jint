#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

/// <summary>Test-only strict XML byte boundary. Production receives the decoded string.</summary>
internal static partial class XmlByteDecoder
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly Encoding Utf16Be = new UnicodeEncoding(true, false, true);
    private static readonly Encoding Utf16Le = new UnicodeEncoding(false, false, true);

    internal static (string? Text, XmlDecodingDecision Decision) Decode(ReadOnlySpan<byte> bytes)
    {
        Encoding encoding;
        string decision;
        if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            encoding = Utf8;
            decision = "utf-8-bom";
            bytes = bytes[3..];
        }
        else if (bytes.StartsWith(new byte[] { 0xfe, 0xff }))
        {
            encoding = Utf16Be;
            decision = "utf-16-be-bom";
            bytes = bytes[2..];
        }
        else if (bytes.StartsWith(new byte[] { 0xff, 0xfe }))
        {
            encoding = Utf16Le;
            decision = "utf-16-le-bom";
            bytes = bytes[2..];
        }
        else if (bytes.StartsWith(new byte[] { 0x00, 0x3c, 0x00, 0x3f }) || bytes.StartsWith(new byte[] { 0x00, 0x3c, 0x00 }))
        {
            encoding = Utf16Be;
            decision = "utf-16-be-signature";
        }
        else if (bytes.StartsWith(new byte[] { 0x3c, 0x00, 0x3f, 0x00 }) || bytes.StartsWith(new byte[] { 0x3c, 0x00 }))
        {
            encoding = Utf16Le;
            decision = "utf-16-le-signature";
        }
        else
        {
            var headerLength = Math.Min(bytes.Length, 512);
            var header = Encoding.ASCII.GetString(bytes[..headerLength]);
            var match = EncodingDeclaration().Match(header);
            if (match.Success && match.Groups[2].Value.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase))
            {
                encoding = Encoding.Latin1;
                decision = "iso-8859-1-declaration";
            }
            else
            {
                encoding = Utf8;
                decision = "utf-8-default";
            }
        }

        string source;
        try
        {
            source = encoding.GetString(bytes);
        }
        catch (DecoderFallbackException error)
        {
            return (null, new XmlDecodingDecision { Decision = decision, Status = "strict-decode-error", Detail = error.Message });
        }
        var declaration = EncodingDeclaration().Match(source);
        var declared = declaration.Success ? declaration.Groups[2].Value : "";
        if (declared.Length > 0)
        {
            if (!ValidEncodingName().IsMatch(declared))
                return (source, new XmlDecodingDecision { Decision = decision, Status = "decoded", Declared = declared });
            var normalized = declared.ToLowerInvariant().Replace('_', '-');
            var supported = encoding == Utf8
                ? normalized is "utf-8" or "utf8"
                : encoding == Encoding.Latin1
                    ? normalized == "iso-8859-1"
                    : normalized == "utf-16" || normalized == encoding.WebName;
            if (!supported && normalized != "ascii")
                return (null, new XmlDecodingDecision { Decision = decision, Status = "declared-encoding-review", Declared = declared });
        }
        return (source, new XmlDecodingDecision { Decision = decision, Status = "decoded", Declared = declared });
    }

    [GeneratedRegex("^<\\?xml\\s+[^?]*?\\bencoding\\s*=\\s*(['\"])([^'\"]+)\\1", RegexOptions.IgnoreCase)]
    private static partial Regex EncodingDeclaration();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]*$")]
    private static partial Regex ValidEncodingName();
}
