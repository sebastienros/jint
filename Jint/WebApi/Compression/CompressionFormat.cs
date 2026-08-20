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
/// The standard's fourth value, <c>"brotli"</c> ([RFC7932]), is not implemented. A value the standard
/// names but an implementation does not support is a <c>TypeError</c> ("If format is unsupported in
/// CompressionStream, then throw a TypeError"), which is the same failure a value outside the enumeration
/// gets from the WebIDL conversion — so a script sees one answer either way.
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
}

/// <summary>
/// The WebIDL enumeration conversion for <see cref="CompressionFormat"/>.
/// </summary>
internal static class CompressionFormats
{
    /// <summary>
    /// The <c>format</c> argument both constructors take: a required <c>CompressionFormat</c>, so a
    /// missing argument and an unrecognized one are both a <c>TypeError</c> — and so is
    /// <c>"brotli"</c>, which the enumeration names but this implementation does not support.
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
                "' is not a supported CompressionFormat; use 'gzip', 'deflate' or 'deflate-raw'");
        }

        return format;
    }

    /// <summary>
    /// Matches one of the three identifiers the standard defines, exactly and case-sensitively, as
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
            default:
                format = default;
                return false;
        }
    }
}
#endif
