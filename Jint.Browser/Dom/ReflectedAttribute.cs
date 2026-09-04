using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>Which of HTML §2.6.1's per-type reflection algorithms an IDL attribute takes.</summary>
internal enum ReflectedKind
{
    /// <summary>A <c>DOMString</c>, transparently and case-preservingly.</summary>
    Text,

    /// <summary>A <c>DOMString?</c>: absent is <see langword="null"/> and setting null removes.</summary>
    NullableText,

    /// <summary>A <c>USVString</c> whose content attribute is defined to contain a URL.</summary>
    Url,

    /// <summary>An enumerated attribute limited to known values.</summary>
    Enumerated,

    /// <summary>A <c>boolean</c>: the attribute's presence.</summary>
    Boolean,

    /// <summary>A <c>long</c>.</summary>
    Long,

    /// <summary>A <c>long</c> limited to only non-negative numbers.</summary>
    LimitedLong,

    /// <summary>An <c>unsigned long</c>.</summary>
    UnsignedLong,

    /// <summary>An <c>unsigned long</c> limited to only positive numbers.</summary>
    LimitedUnsignedLong,

    /// <summary>An <c>unsigned long</c> limited to only positive numbers, with fallback.</summary>
    LimitedUnsignedLongWithFallback,

    /// <summary>An <c>unsigned long</c> clamped to a range.</summary>
    ClampedUnsignedLong,

    /// <summary>A <c>double</c>.</summary>
    Double,

    /// <summary>A <c>double</c> limited to only positive numbers.</summary>
    LimitedDouble,
}

/// <summary>
/// One IDL attribute that
/// <a href="https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes">reflects</a>
/// one content attribute, and the algorithm HTML §2.6.1 gives its type.
/// </summary>
/// <remarks>
/// <para>
/// <b>The content attribute is the only storage.</b> A getter is <c>getAttribute</c> plus one parse; a setter
/// is <c>setAttribute</c> plus one serialization. Nothing here holds state, which is what makes the two
/// directions agree by construction — <c>el.setAttribute('dir', 'RTL')</c> and <c>el.dir = 'RTL'</c> are the
/// same write, an attribute the parser produced is visible through the IDL attribute with nothing having to
/// synchronise, and <c>[CEReactions]</c> comes free because the write goes through AngleSharp's attribute
/// observer, which is where a custom element's <c>attributeChangedCallback</c> already arrives.
/// </para>
/// <para>
/// <b>Every instance is process-shared and immutable</b>, because the generated shape member that names one
/// is instantiated once per engine: the descriptors live in <c>Generated/DomReflected.g.cs</c> as static
/// readonly fields, and the whole of an instance's state is the two names and the parameters of its type.
/// </para>
/// <para>
/// <b>Why a descriptor rather than emitted code.</b> The generator could inline each algorithm into the member
/// body it already emits. One shared implementation instead means HTML's rules for parsing integers,
/// non-negative integers and floating-point number values exist once — six of these thirteen types are the
/// same parse with a different range — and a fix reaches every reflected attribute rather than the ones
/// somebody remembered to regenerate.
/// </para>
/// </remarks>
internal sealed class ReflectedAttribute
{
    /// <summary>WebIDL's <c>long</c> range, which is also the range a reflected integer is limited to.</summary>
    private const long MaxInt = 2147483647;

    private const long MinInt = -2147483648;

    private readonly ReflectedKind _kind;
    private readonly string _attribute;
    private readonly string[] _keywords;
    private readonly string? _missing;
    private readonly string? _invalid;
    private readonly double _default;
    private readonly long _min;
    private readonly long _max;

    private ReflectedAttribute(
        string member,
        string attribute,
        ReflectedKind kind,
        string[]? keywords = null,
        string? missing = null,
        string? invalid = null,
        double fallback = 0,
        long min = 0,
        long max = 0)
    {
        Member = member;
        _attribute = attribute;
        _kind = kind;
        _keywords = keywords ?? [];
        _missing = missing;
        _invalid = invalid;
        _default = fallback;
        _min = min;
        _max = max;
    }

    /// <summary>The qualified member name — <c>HTMLElement.dir</c> — as a refusal names it.</summary>
    internal string Member { get; }

    /// <summary>A <c>DOMString</c>, or a <c>DOMString?</c> when <paramref name="nullable"/>.</summary>
    internal static ReflectedAttribute Text(string member, string attribute, bool nullable = false)
        => new(member, attribute, nullable ? ReflectedKind.NullableText : ReflectedKind.Text);

    /// <summary>A <c>USVString</c> whose content attribute is defined to contain a URL.</summary>
    internal static ReflectedAttribute Url(string member, string attribute)
        => new(member, attribute, ReflectedKind.Url);

    /// <summary>An enumerated attribute limited to known values.</summary>
    /// <param name="member">The qualified member name.</param>
    /// <param name="attribute">The content attribute reflected.</param>
    /// <param name="keywords">The keywords, in the canonical case the getter answers.</param>
    /// <param name="missing">
    /// The missing value default, or <see langword="null"/> for a nullable enumeration — the one whose IDL
    /// type is <c>DOMString?</c>, whose absent state is <c>null</c> rather than the empty string, and whose
    /// setter therefore takes <c>null</c> as a removal.
    /// </param>
    /// <param name="invalid">The invalid value default; the missing value default when there is none.</param>
    internal static ReflectedAttribute Enumerated(string member, string attribute, string[] keywords, string? missing, string? invalid)
        => new(member, attribute, ReflectedKind.Enumerated, keywords, missing, invalid);

    /// <summary>A <c>boolean</c> attribute: the attribute's presence and nothing else.</summary>
    internal static ReflectedAttribute Boolean(string member, string attribute)
        => new(member, attribute, ReflectedKind.Boolean);

    /// <summary>One of the numeric types, with its default and — when it clamps — its range.</summary>
    internal static ReflectedAttribute Numeric(string member, string attribute, ReflectedKind kind, double fallback, long min = 0, long max = 0)
        => new(member, attribute, kind, fallback: fallback, min: min, max: max);

    /// <summary>The IDL attribute's value: the content attribute, through this type's algorithm.</summary>
    internal JsValue Get(IElement element)
    {
        var value = element.GetAttribute(_attribute);

        switch (_kind)
        {
            case ReflectedKind.Text:
                return DomConvert.Text(value);

            case ReflectedKind.NullableText:
                return DomConvert.NullableText(value);

            case ReflectedKind.Boolean:
                return DomConvert.Bool(value is not null);

            case ReflectedKind.Url:
                return DomConvert.Text(ResolveUrl(element, value));

            case ReflectedKind.Enumerated:
                return Enumerate(value);

            case ReflectedKind.Double:
            case ReflectedKind.LimitedDouble:
                return DomConvert.Number(GetDouble(value));

            default:
                return DomConvert.Number(GetInteger(value));
        }
    }

    /// <summary>Sets the IDL attribute, which is one write of the content attribute.</summary>
    internal JsValue Set(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var value = DomConvert.At(arguments, 0);

        switch (_kind)
        {
            case ReflectedKind.Boolean:
                // "The content attribute must be removed if the IDL attribute is set to false, and must be
                // set to the empty string if the IDL attribute is set to true."
                if (TypeConverter.ToBoolean(value))
                {
                    element.SetAttribute(_attribute, "");
                }
                else
                {
                    element.RemoveAttribute(_attribute);
                }

                return JsValue.Undefined;

            // A `DOMString?` setter — a nullable string, and a nullable enumeration, which is the same
            // setter: null and undefined remove the attribute and everything else is written verbatim. An
            // enumeration's setter is transparent; it is the getter that maps an unknown value onto a default.
            case ReflectedKind.NullableText:
            case ReflectedKind.Enumerated when _missing is null:
                return SetOrRemove(element, value);

            // On setting, a URL attribute takes the value as given; resolution is the getter's business.
            case ReflectedKind.Text:
            case ReflectedKind.Enumerated:
            case ReflectedKind.Url:
                element.SetAttribute(_attribute, TypeConverter.ToString(value));
                return JsValue.Undefined;

            case ReflectedKind.Double:
            case ReflectedKind.LimitedDouble:
                return SetDouble(element, TypeConverter.ToNumber(value));

            case ReflectedKind.Long:
                return SetInteger(element, TypeConverter.ToInt32(value));

            // "On setting, if the value is negative, the user agent must throw an IndexSizeError."
            case ReflectedKind.LimitedLong:
                return SetLimited(realm, element, TypeConverter.ToInt32(value), floor: 0, "the value must not be negative.");

            // "On setting, if the value is zero, the user agent must throw an IndexSizeError."
            case ReflectedKind.LimitedUnsignedLong:
                return SetLimited(realm, element, TypeConverter.ToUint32(value), floor: 1, "the value must be greater than zero.");

            // The same rule with the refusal replaced by the default: "if the new value is in the range 1 to
            // 2147483647, then let n be the new value, otherwise let n be the default value".
            case ReflectedKind.LimitedUnsignedLongWithFallback:
                return SetInteger(element, Fallback(TypeConverter.ToUint32(value)));

            default:
                // `unsigned long` and `clamped unsigned long` both set as a plain unsigned integer; the
                // clamping is the getter's.
                return SetInteger(element, TypeConverter.ToUint32(value));
        }
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-integers">The
    /// rules for parsing integers</a>. Trailing characters are permitted and ignored, which is why
    /// <c>tabindex="5%"</c> is 5 and <c>tabindex="1.5"</c> is 1.
    /// </summary>
    /// <returns><see langword="false"/> when the value is not an integer at all.</returns>
    private static bool TryParseInteger(string input, out long value)
    {
        value = 0;
        var position = SkipWhitespace(input);
        var negative = false;

        if (position < input.Length && input[position] == '-')
        {
            negative = true;
            position++;
        }
        else if (position < input.Length && input[position] == '+')
        {
            position++;
        }

        if (position >= input.Length || !char.IsAsciiDigit(input[position]))
        {
            return false;
        }

        // Saturating rather than overflowing: a value outside the IDL type's range is out of range whether it
        // is 2147483648 or a hundred digits, and every caller answers its default for both.
        while (position < input.Length && char.IsAsciiDigit(input[position]))
        {
            if (value <= MaxInt)
            {
                value = (value * 10) + (input[position] - '0');
            }

            position++;
        }

        value = negative ? -value : value;
        return true;
    }

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-non-negative-integers">The
    /// rules for parsing non-negative integers</a>: the integer parser, refusing a negative result.
    /// </summary>
    private static bool TryParseNonNegative(string input, out long value)
        => TryParseInteger(input, out value) && value >= 0;

    /// <summary>
    /// <a href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-floating-point-number-values">The
    /// rules for parsing floating-point number values</a>. Like the integer rules it takes a prefix, so
    /// <c>"1e2x"</c> is 100 and <c>"1 e2"</c> is 1.
    /// </summary>
    private static bool TryParseDouble(string input, out double value)
    {
        value = 0;
        var position = SkipWhitespace(input);
        var sign = 1d;
        var divisor = 1d;
        var exponentSign = 1;

        if (position < input.Length && input[position] == '-')
        {
            sign = -1;
            divisor = -1;
            position++;
        }
        else if (position < input.Length && input[position] == '+')
        {
            position++;
        }

        if (position >= input.Length)
        {
            return false;
        }

        if (input[position] == '.' && position + 1 < input.Length && char.IsAsciiDigit(input[position + 1]))
        {
            value = 0;
        }
        else if (!char.IsAsciiDigit(input[position]))
        {
            return false;
        }
        else
        {
            var whole = 0d;
            while (position < input.Length && char.IsAsciiDigit(input[position]))
            {
                whole = (whole * 10) + (input[position] - '0');
                position++;
            }

            value = sign * whole;
        }

        if (position < input.Length && input[position] == '.')
        {
            position++;
            while (position < input.Length && char.IsAsciiDigit(input[position]))
            {
                divisor *= 10;
                value += (input[position] - '0') / divisor;
                position++;
            }
        }

        if (position < input.Length && (input[position] == 'e' || input[position] == 'E'))
        {
            position++;

            if (position < input.Length)
            {
                if (input[position] == '-')
                {
                    exponentSign = -1;
                    position++;
                }
                else if (input[position] == '+')
                {
                    position++;
                }

                if (position < input.Length && char.IsAsciiDigit(input[position]))
                {
                    var exponent = 0d;
                    do
                    {
                        exponent = (exponent * 10) + (input[position] - '0');
                        position++;
                    }
                    while (position < input.Length && char.IsAsciiDigit(input[position]));

                    value *= Math.Pow(10, exponentSign * exponent);
                }
            }
        }

        // "If value is outside the range of the double type, return an error": an overflow to infinity is
        // that range being left, and the caller answers its default.
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>ASCII whitespace, which is the only whitespace HTML's microsyntaxes skip.</summary>
    private static int SkipWhitespace(string input)
    {
        var position = 0;
        while (position < input.Length && input[position] is ' ' or '\t' or '\n' or '\f' or '\r')
        {
            position++;
        }

        return position;
    }

    /// <summary>Whether every character is ASCII, which is what makes an ordinal-ignore-case match ASCII's.</summary>
    private static bool IsAsciiOnly(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAscii(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A URL attribute's getter: parse the content attribute against the element's node document, and answer
    /// the resulting URL string — or, when parsing fails, the content attribute as it stands.
    /// </summary>
    /// <remarks>
    /// The parser is AngleSharp's own <c>AngleSharp.Url</c> rather than the engine's WHATWG one,
    /// deliberately: <c>a.protocol</c>, <c>a.host</c>, <c>a.pathname</c>, <c>a.search</c> and <c>a.hash</c>
    /// are AngleSharp's, so a second parser here would leave the components of one URL disagreeing with the
    /// URL itself. Which parser this package's URLs should come from is the runtime's question, not the
    /// binding's.
    /// </remarks>
    private static string ResolveUrl(IElement element, string? value)
    {
        if (value is null)
        {
            return "";
        }

        var baseUri = element.BaseUri;
        var resolved = string.IsNullOrEmpty(baseUri) ? new Url(value) : new Url(new Url(baseUri), value);

        return resolved.IsInvalid ? value : resolved.Href;
    }

    /// <summary>
    /// The state an
    /// <a href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#enumerated-attribute">enumerated
    /// attribute</a> is in, as the conforming value in its canonical case.
    /// </summary>
    private JsValue Enumerate(string? value)
    {
        if (value is null)
        {
            return _missing is null ? JsValue.Null : JsString.Create(_missing);
        }

        foreach (var keyword in _keywords)
        {
            // ASCII case-insensitive, and pointedly not culture- or Unicode-insensitive: U+017F LATIN SMALL
            // LETTER LONG S case-folds to "s" under Unicode's rules, so `kind="ſubtitles"` would match the
            // `subtitles` keyword under every comparison but this one — and the corpus tests exactly that.
            if (IsAsciiOnly(value) && value.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return JsString.Create(keyword);
            }
        }

        return (_invalid ?? _missing) is { } fallback ? JsString.Create(fallback) : JsValue.Null;
    }

    /// <summary>The integer types' getter: one parse, one range test, and the default for everything else.</summary>
    private long GetInteger(string? value)
    {
        var fallback = (long) _default;

        if (value is null)
        {
            return fallback;
        }

        var parsed = _kind == ReflectedKind.Long
            ? TryParseInteger(value, out var signed) ? signed : (long?) null
            : TryParseNonNegative(value, out var unsigned) ? unsigned : (long?) null;

        if (parsed is not { } number)
        {
            return fallback;
        }

        return _kind switch
        {
            ReflectedKind.Long => number is < MinInt or > MaxInt ? fallback : number,
            ReflectedKind.LimitedLong or ReflectedKind.UnsignedLong => number > MaxInt ? fallback : number,
            ReflectedKind.LimitedUnsignedLong or ReflectedKind.LimitedUnsignedLongWithFallback
                => number is < 1 or > MaxInt ? fallback : number,
            ReflectedKind.ClampedUnsignedLong => Math.Clamp(number, _min, _max),
            _ => number,
        };
    }

    /// <summary>The floating-point types' getter.</summary>
    private double GetDouble(string? value)
    {
        if (value is null || !TryParseDouble(value, out var parsed))
        {
            return _default;
        }

        return _kind == ReflectedKind.LimitedDouble && parsed <= 0 ? _default : parsed;
    }

    /// <summary>A limited integer type's setter: below the floor is <c>IndexSizeError</c>, not a clamp.</summary>
    private JsValue SetLimited(DomRealm realm, IElement element, long value, long floor, string detail)
    {
        if (value < floor)
        {
            DomFailures.Refuse(realm.Engine, Member, DomExceptionNames.IndexSize, detail);
        }

        return SetInteger(element, value);
    }

    /// <summary>The value a with-fallback setter writes: the new value, or the default when out of range.</summary>
    private long Fallback(long value) => value is < 1 or > MaxInt ? (long) _default : value;

    /// <summary>
    /// The shortest string representing an integer, which is what every numeric reflected attribute writes.
    /// </summary>
    private JsValue SetInteger(IElement element, long value)
    {
        element.SetAttribute(_attribute, value.ToString(CultureInfo.InvariantCulture));
        return JsValue.Undefined;
    }

    private JsValue SetDouble(IElement element, double value)
    {
        // "If the value is not greater than 0, then return": the attribute keeps whatever it had, which is
        // the one setter in this file that can decline to write.
        if (_kind == ReflectedKind.LimitedDouble && !(value > 0))
        {
            return JsValue.Undefined;
        }

        element.SetAttribute(_attribute, TypeConverter.ToString(value));
        return JsValue.Undefined;
    }

    private JsValue SetOrRemove(IElement element, JsValue value)
    {
        if (value.IsNullOrUndefined())
        {
            element.RemoveAttribute(_attribute);
        }
        else
        {
            element.SetAttribute(_attribute, TypeConverter.ToString(value));
        }

        return JsValue.Undefined;
    }
}
