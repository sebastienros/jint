#if NET8_0_OR_GREATER
using System.Globalization;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Navigator;

/// <summary>
/// The <c>navigator</c> object.
/// <para>
/// https://html.spec.whatwg.org/multipage/system-state.html#navigator
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// It exists for exactly one member. WinterTC's Minimum Common API
/// (https://min-common-api.proposal.wintertc.org/) requires a conforming runtime to expose
/// <c>globalThis.navigator.userAgent</c>, and a great deal of published JavaScript feature-detects a runtime
/// through it; everything else the HTML Standard hangs off <c>Navigator</c> — <c>language</c>,
/// <c>onLine</c>, <c>hardwareConcurrency</c>, <c>clipboard</c>, <c>geolocation</c> — describes a user agent
/// with a user, a document and a network stack, none of which an embedded interpreter has. Those members are
/// <b>absent</b> rather than present-and-lying, so feature detection sees the truth.
/// </para>
/// <para>
/// The value is <c>Jint/&lt;version&gt;</c>. WinterTC asks for a value conforming to RFC 7231's
/// <c>User-Agent</c> construction and recommends "the value be limited to a single <c>product</c> token
/// excluding the optional <c>product-version</c>"; the version is kept anyway, because a
/// <c>product "/" product-version</c> pair is exactly what RFC 7231 defines a product token to be and because
/// a bare <c>"Jint"</c> tells a script nothing it can branch on. It carries no <c>comment</c> component, which
/// is the part of that recommendation that matters — nothing here reveals the host operating system or the
/// embedding application. Read it as WinterTC says to: "a single, complete, opaque, unstructured value".
/// </para>
/// <para>
/// <b>There is no <c>Navigator</c> interface object and no <c>Navigator.prototype</c></b>, so
/// <c>userAgent</c> is an own accessor of this object rather than one on an interface prototype; it still
/// brand-checks its receiver.
/// </para>
/// <para>
/// <b>That is why it keeps an ECMAScript built-in's property attributes</b> — non-enumerable — where every
/// other attribute under <c>Jint/WebApi/</c> carries WebIDL's <c>{ [[Enumerable]]: true,
/// [[Configurable]]: true }</c> (https://webidl.spec.whatwg.org/#es-attributes). WebIDL's rule assumes the
/// member is where the specification puts it, and Node 24 is the oracle for what that looks like:
/// <c>userAgent</c> is an enumerable accessor on <c>Navigator.prototype</c>, and
/// <c>Object.keys(navigator)</c> is nevertheless the empty array, because the instance has no own properties
/// at all. Non-enumerable <i>here</i> is what reproduces that answer; declaring it enumerable would make
/// <c>Object.keys(navigator)</c> report <c>["userAgent"]</c>, which no implementation does, so a blanket flip
/// would move this object further from a browser rather than closer. What fixes it is the interface object,
/// not a flag. The exemption is recorded, and checked for staleness, in
/// <c>Jint.Tests.Runtime.WebApi.WebIdlPropertyAttributeTests</c>.
/// </para>
/// <para>
/// The object is also installed as an ordinary enumerable data property of the global rather than through the
/// <c>[Replaceable]</c> accessor pair WebIDL gives it.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class NavigatorInstance : BuiltinShapeObject
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString NavigatorToStringTag = new("Navigator");

    /// <summary>
    /// The user agent string, built once for the process: the assembly version cannot change while it is
    /// loaded, so every engine and every realm hands out this one <see cref="JsString"/>.
    /// </summary>
    private static readonly JsString UserAgent = new("Jint/" + ProductVersion());

    private readonly Realm _realm;

    internal NavigatorInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-useragent — the
    /// <c>NavigatorID</c> mixin's <c>userAgent</c> attribute, which is where WinterTC's
    /// <c>globalThis.navigator.userAgent</c> requirement lands.
    /// </summary>
    [JsAccessor("userAgent")]
    private JsString UserAgentGet(JsValue thisObject)
    {
        if (thisObject is not NavigatorInstance)
        {
            Throw.TypeError(_realm, "Failed to read the 'userAgent' property from 'Navigator': illegal invocation, receiver is not a Navigator object.");
        }

        return UserAgent;
    }

    /// <summary>
    /// The <c>product-version</c> half of the token: Jint's own assembly version, as
    /// <c>major.minor.patch</c>. The fourth component is dropped because Jint never sets one, and the whole
    /// thing degrades to <c>"0.0.0"</c> rather than throwing if the assembly somehow carries no version.
    /// </summary>
    private static string ProductVersion()
    {
        var version = typeof(Engine).Assembly.GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        var build = version.Build < 0 ? 0 : version.Build;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{build}");
    }
}
#endif
