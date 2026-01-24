using System.Globalization;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-segmenter-objects
/// Represents an Intl.Segmenter instance for locale-aware text segmentation.
/// </summary>
internal sealed class JsSegmenter : ObjectInstance
{
    internal JsSegmenter(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string granularity,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Granularity = granularity;
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// The locale used for segmentation.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The granularity: "grapheme", "word", or "sentence".
    /// </summary>
    internal string Granularity { get; }

    /// <summary>
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Segments the input string and returns a Segments object.
    /// </summary>
    internal JsSegments Segment(Engine engine, string input)
    {
        return new JsSegments(engine, this, input);
    }
}
