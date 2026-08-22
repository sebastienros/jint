#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Compression;

/// <summary>
/// The <c>CompressionFormat</c> enumeration — https://compression.spec.whatwg.org/#supported-formats.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>deflate</c> is ZLIB, not raw DEFLATE.</b> The standard defines <c>"deflate"</c> as the "ZLIB
/// Compressed Data Format" of [RFC1950] — a two-byte header, the DEFLATE body, and an ADLER32 checksum —
/// and notes that it is named that way "for consistency with HTTP Content-Encodings". Raw DEFLATE, the
/// [RFC1951] bit stream with no wrapper at all, is the separate <c>"deflate-raw"</c> value. Mapping
/// <c>"deflate"</c> onto a bare <see cref="System.IO.Compression.DeflateStream"/> is the classic mistake
/// here, and it produces bytes no other implementation can read.
/// </para>
/// <para>
/// <b>The standard fixes no compression quality.</b> Its compress step is "compress <i>chunk</i> with
/// <i>format</i> and <i>context</i>" and nothing anywhere names a level, so every value the enumeration
/// carries is driven at the BCL's own default — <see cref="System.IO.Compression.CompressionMode.Compress"/>,
/// which is <see cref="System.IO.Compression.CompressionLevel.Optimal"/>. Implementations differ here and
/// are entitled to: only the bytes' decodability is normative, not their density.
/// </para>
/// </remarks>
internal enum CompressionFormat
{
    /// <summary>"GZIP file format" [RFC1952] — <see cref="System.IO.Compression.GZipStream"/>.</summary>
    Gzip,

    /// <summary>"ZLIB Compressed Data Format" [RFC1950] — <see cref="System.IO.Compression.ZLibStream"/>.</summary>
    Deflate,

    /// <summary>"The DEFLATE algorithm" [RFC1951] — <see cref="System.IO.Compression.DeflateStream"/>.</summary>
    DeflateRaw,

    /// <summary>
    /// "Brotli Compressed Data Format" [RFC7932] — <see cref="System.IO.Compression.BrotliStream"/>. It is
    /// the enumeration's fourth value and the one that needs no wrapper argument at all: the format is a
    /// single self-delimiting stream, so unlike the deflate family there is no container to choose.
    /// </summary>
    Brotli,
}

/// <summary>
/// The WebIDL enumeration conversion for <see cref="CompressionFormat"/>.
/// </summary>
internal static class CompressionFormats
{
    /// <summary>
    /// The <c>format</c> argument both constructors take: a required <c>CompressionFormat</c>, so a missing
    /// argument and an unrecognized one are both a <c>TypeError</c>. All four values the enumeration names
    /// are supported, which leaves the constructors' own step 1 — "if <i>format</i> is unsupported in
    /// <c>CompressionStream</c>, then throw a <c>TypeError</c>" — with nothing left to refuse.
    /// </summary>
    internal static CompressionFormat ReadFormat(Realm realm, JsCallArguments arguments, string interfaceName)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(realm, "Failed to construct '" + interfaceName + "': 1 argument required, but only 0 present");
        }

        var value = TypeConverter.ToString(arguments.At(0));
        if (!TryParse(value, out var format))
        {
            Throw.TypeError(
                realm,
                "Failed to construct '" + interfaceName + "': the provided value '" + value +
                "' is not a supported CompressionFormat; use 'gzip', 'deflate', 'deflate-raw' or 'brotli'");
        }

        return format;
    }

    /// <summary>
    /// Matches one of the four identifiers the standard defines, exactly and case-sensitively, as
    /// https://webidl.spec.whatwg.org/#es-enumeration requires.
    /// </summary>
    private static bool TryParse(string value, out CompressionFormat format)
    {
        switch (value)
        {
            case "gzip":
                format = CompressionFormat.Gzip;
                return true;
            case "deflate":
                format = CompressionFormat.Deflate;
                return true;
            case "deflate-raw":
                format = CompressionFormat.DeflateRaw;
                return true;
            case "brotli":
                format = CompressionFormat.Brotli;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
#endif
