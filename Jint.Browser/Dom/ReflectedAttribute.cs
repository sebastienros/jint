using System.Globalization;
using AngleSharp.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// Which element a reflected IDL attribute's content attribute lives on, when it is not the one the IDL
/// attribute was read from.
/// </summary>
/// <remarks>
/// HTML has six of these and all six are on <c>Document</c>: §3.2.6.4's <c>dir</c>, which reflects the
/// <c>html</c> element's attribute, and §16.3's five obsolete colours, which reflect the <c>body</c>
/// element's. All six are a string or an enumeration, which is why only those two factories take one — a
/// numeric or boolean member reflecting onto another element does not exist, and the generator refuses to
/// invent one.
/// </remarks>
internal enum ReflectedTarget
{
    /// <summary>The element the IDL attribute was read from, which is every row but six.</summary>
    Self,

    /// <summary>The document element — the <c>html</c> element, when there is one.</summary>
    DocumentElement,

    /// <summary>The body element.</summary>
    Body,
}

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

    /// <summary>
    /// HTML's <c>[[CryptographicNonce]]</c> slot, whose setter deliberately does not write the attribute.
    /// </summary>
    Nonce,

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
    private readonly bool _legacyNull;
    private readonly bool _documentUrlWhenEmpty;
    private readonly ReflectedTarget _target;

    private ReflectedAttribute(
        string member,
        string attribute,
        ReflectedKind kind,
        string[]? keywords = null,
        string? missing = null,
        string? invalid = null,
        double fallback = 0,
        long min = 0,
        long max = 0,
        bool legacyNull = false,
        bool documentUrlWhenEmpty = false,
        ReflectedTarget target = ReflectedTarget.Self)
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
        _legacyNull = legacyNull;
        _documentUrlWhenEmpty = documentUrlWhenEmpty;
        _target = target;
    }

    /// <summary>The qualified member name — <c>HTMLElement.dir</c> — as a refusal names it.</summary>
    internal string Member { get; }

    /// <summary>
    /// Whether the IDL type is a <c>USVString</c> whose content attribute contains a URL, which is the one
    /// kind whose <em>getter</em> has to resolve against the page runtime's current base URL rather than the
    /// parsed document's. The generated getters make exactly this distinction — <c>ModelBuilder</c> emits
    /// the realm overload for a <c>url</c> row and for no other — and it is here so that a shape written by
    /// hand can make it too, rather than paying for the lookup on every reflected read.
    /// </summary>
    internal bool ReflectsUrl => _kind == ReflectedKind.Url;

    /// <summary>A <c>DOMString</c>, or a <c>DOMString?</c> when <paramref name="nullable"/>.</summary>
    /// <param name="member">The qualified member name.</param>
    /// <param name="attribute">The content attribute reflected.</param>
    /// <param name="nullable">Whether the IDL type is <c>DOMString?</c>, whose setter takes null as a removal.</param>
    /// <param name="legacyNullToEmptyString">
    /// WebIDL's <c>[LegacyNullToEmptyString]</c>: the null value converts to the empty string rather than to
    /// <c>"null"</c>. It is only ever on a <c>DOMString</c>, never on a <c>DOMString?</c>, and it says nothing
    /// about <c>undefined</c>, which still converts to <c>"undefined"</c>.
    /// </param>
    /// <param name="target">Which element the content attribute lives on.</param>
    internal static ReflectedAttribute Text(
        string member,
        string attribute,
        bool nullable = false,
        bool legacyNullToEmptyString = false,
        ReflectedTarget target = ReflectedTarget.Self)
        => new(
            member,
            attribute,
            nullable ? ReflectedKind.NullableText : ReflectedKind.Text,
            legacyNull: legacyNullToEmptyString,
            target: target);

    /// <summary>A <c>USVString</c> whose content attribute is defined to contain a URL.</summary>
    /// <param name="member">The qualified member name.</param>
    /// <param name="attribute">The content attribute reflected.</param>
    /// <param name="documentUrlWhenEmpty">
    /// HTML §4.10.18.6's exception, which <c>form.action</c> and <c>formAction</c> are the only members
    /// with: "on getting, when the content attribute is missing or its value is the empty string, the
    /// element's node document's URL must be returned instead". It is the document's URL and not the base
    /// URL, so a <c>&lt;base href&gt;</c> does not move it — and for a document with a browsing context it
    /// is the URL <em>the page</em> holds, which a same-document navigation moves and AngleSharp's document
    /// address does not; <see cref="Get(DomRealm, IElement)"/> is where the two are told apart.
    /// </param>
    internal static ReflectedAttribute Url(string member, string attribute, bool documentUrlWhenEmpty = false)
        => new(member, attribute, ReflectedKind.Url, documentUrlWhenEmpty: documentUrlWhenEmpty);

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
    /// <param name="target">Which element the content attribute lives on.</param>
    internal static ReflectedAttribute Enumerated(
        string member,
        string attribute,
        string[] keywords,
        string? missing,
        string? invalid,
        ReflectedTarget target = ReflectedTarget.Self)
        => new(member, attribute, ReflectedKind.Enumerated, keywords, missing, invalid, target: target);

    /// <summary>A <c>boolean</c> attribute: the attribute's presence and nothing else.</summary>
    internal static ReflectedAttribute Boolean(string member, string attribute)
        => new(member, attribute, ReflectedKind.Boolean);

    /// <summary>
    /// HTML §2.5.3's <c>nonce</c>, which answers <see cref="CryptographicNonce"/>'s slot rather than the
    /// content attribute. It is in this table because it is the same accessor pair over the same attribute
    /// name, and out of <see cref="ReflectedKind"/>'s other twelve because it is the one member whose setter
    /// must leave the content attribute alone.
    /// </summary>
    internal static ReflectedAttribute Nonce(string member, string attribute)
        => new(member, attribute, ReflectedKind.Nonce);

    /// <summary>One of the numeric types, with its default and — when it clamps — its range.</summary>
    internal static ReflectedAttribute Numeric(string member, string attribute, ReflectedKind kind, double fallback, long min = 0, long max = 0)
        => new(member, attribute, kind, fallback: fallback, min: min, max: max);

    /// <summary>The IDL attribute's value outside a page runtime, resolved against its node document.</summary>
    internal JsValue Get(IElement element)
    {
        var owner = element.Owner;
        return Get(element, CurrentBaseUri(owner, element.BaseUri), owner?.Url);
    }

    /// <summary>The IDL attribute's value inside a page runtime, resolved against its current document base.</summary>
    /// <remarks>
    /// Two values come from the runtime and they are different values. The base URL is what a relative
    /// content attribute resolves against; the document's URL is what <see cref="_documentUrlWhenEmpty"/>
    /// answers instead of resolving anything, and a <c>&lt;base href&gt;</c> does not move it. Both are the
    /// runtime's rather than AngleSharp's because a same-document navigation moves
    /// <see cref="PageRuntime.DocumentUrl"/> and leaves AngleSharp's document address at whatever the parse
    /// was given — so after <c>history.pushState</c> the AngleSharp answer is the address the page was
    /// loaded at, which for <c>formAction</c> is the one URL a form posting to itself must not read.
    /// </remarks>
    internal JsValue Get(DomRealm realm, IElement element)
    {
        var owner = element.Owner;
        var runtime = PageRuntime.Find(realm.Engine, owner);
        return Get(element, runtime?.BaseUri ?? CurrentBaseUri(owner, element.BaseUri), runtime?.DocumentUrl ?? owner?.Url);
    }

    /// <summary>
    /// The IDL attribute of one of HTML's six <c>Document</c> members that reflect an attribute of
    /// <b>another</b> element — the document element, or the body element.
    /// </summary>
    /// <remarks>
    /// "If there is no such element, then the attribute must return the empty string and do nothing on
    /// setting" (HTML §3.2.6.4, and §16.3 for the colours): a missing target reads exactly as an absent
    /// content attribute, which is what passing no element to the shared getter says.
    /// </remarks>
    internal JsValue Get(IDocument document)
        => Get(ElementIn(document), CurrentBaseUri(document, document.BaseUri), document.Url);

    /// <summary>The same member's setter, which does nothing when the target element is absent.</summary>
    internal JsValue Set(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        var element = ElementIn(document);
        return element is null ? JsValue.Undefined : Set(realm, element, arguments);
    }

    /// <summary>The element a <see cref="ReflectedTarget"/> names in <paramref name="document"/>.</summary>
    private IElement? ElementIn(IDocument document) => _target switch
    {
        ReflectedTarget.DocumentElement => document.DocumentElement,
        ReflectedTarget.Body => document.Body,
        _ => null,
    };

    /// <summary>
    /// The node document's current base URL, derived without AngleSharp's cached <c>Node.BaseUri</c>.
    /// </summary>

    private static string? CurrentBaseUri(IDocument? document, string? fallback)
    {
        if (document is null)
        {
            return fallback;
        }

        var address = document.Url;
        var href = document.QuerySelector("base[href]")?.GetAttribute("href");
        if (string.IsNullOrEmpty(href))
        {
            return address;
        }

        return PageUrl.Resolve(href, address) ?? address;
    }

    private JsValue Get(IElement? element, string? baseUri, string? documentUrl)
    {
        var value = element?.GetAttribute(_attribute);

        switch (_kind)
        {
            case ReflectedKind.Text:
                return DomConvert.Text(value);

            case ReflectedKind.NullableText:
                return DomConvert.NullableText(value);

            case ReflectedKind.Boolean:
                return DomConvert.Bool(value is not null);

            case ReflectedKind.Nonce:
                return DomConvert.Text(element is null ? "" : CryptographicNonce.Get(element));

            case ReflectedKind.Url when _documentUrlWhenEmpty && string.IsNullOrEmpty(value):
                // "...the element's node document's URL must be returned instead." The caller resolved which
                // URL that is, because for the page's own document it is the runtime's and not AngleSharp's.
                return DomConvert.Text(documentUrl ?? "");

            case ReflectedKind.Url:
                return DomConvert.Text(ResolveUrl(value, baseUri));

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
            // "On setting, set this's [[CryptographicNonce]] to the given value." The content attribute is
            // untouched, which is what keeps a header-delivered policy's nonce out of a CSS selector.
            case ReflectedKind.Nonce:
                CryptographicNonce.Set(element, TypeConverter.ToString(value));
                return JsValue.Undefined;

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

            // WebIDL's [LegacyNullToEmptyString]: null converts to "" rather than to "null". Only null,
            // and pointedly not undefined, which is what the corpus asserts of <body text> either way.
            case ReflectedKind.Text when _legacyNull && value.IsNull():
                element.SetAttribute(_attribute, "");
                return JsValue.Undefined;

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
                // `unsigned long` and `clamped unsigned long` both set as a plain unsigned integer, out of
                // range answering the default; the clamping to a narrower range is the getter's.
                return SetInteger(element, InRange(TypeConverter.ToUint32(value)));
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
    /// The parser is the same WHATWG parser the page runtime uses for its document and base URLs. The
    /// descriptor deliberately bypasses AngleSharp's convenience URL properties because their cached base
    /// can survive removal of the document's first <c>base[href]</c>.
    /// </remarks>
    private static string ResolveUrl(string? value, string? baseUri)
    {
        if (value is null)
        {
            return "";
        }

        return PageUrl.Resolve(value, baseUri) ?? value;
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

        return SetInteger(element, InRange(value));
    }

    /// <summary>The value a with-fallback setter writes: the new value, or the default when out of range.</summary>
    private long Fallback(long value) => value is < 1 or > MaxInt ? (long) _default : value;

    /// <summary>
    /// "If the new value is in the range 0 to 2147483647, then let n be the new value, otherwise let n be the
    /// default value": an unsigned reflected integer writes its <i>default</i> rather than the number it was
    /// given when that number is outside the reflected range.
    /// </summary>
    /// <remarks>
    /// It has to be tested here rather than left to the conversion, because WebIDL's <c>unsigned long</c> is
    /// modulo 2<sup>32</sup>: <c>el.hspace = 4294967295</c> arrives as 4294967295 and not as −1. A signed
    /// <c>long</c> cannot leave its range at all, so this is a no-op for one and the whole rule for the other.
    /// </remarks>
    private long InRange(long value) => value > MaxInt ? (long) _default : value;

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
