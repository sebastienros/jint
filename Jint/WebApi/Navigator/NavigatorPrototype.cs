#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Navigator;

/// <summary>
/// <c>Navigator.prototype</c> — the interface prototype object, and where the one member of the
/// <c>navigator</c> object lives.
/// <para>
/// https://html.spec.whatwg.org/multipage/system-state.html#the-navigator-object
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface exists for exactly one member. WinterTC's Minimum Common API
/// (https://min-common-api.proposal.wintertc.org/) requires a conforming runtime to expose
/// <c>globalThis.navigator.userAgent</c>, and a great deal of published JavaScript feature-detects a runtime
/// through it; everything else the HTML Standard hangs off <c>Navigator</c> — <c>language</c>,
/// <c>onLine</c>, <c>hardwareConcurrency</c>, <c>clipboard</c>, <c>geolocation</c> — describes a user agent
/// with a user, a document and a network stack, none of which an embedded interpreter has. Those members are
/// <b>absent from this object</b> rather than present-and-lying, so feature detection sees the truth.
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
/// <b>The member is here, not on the instance</b>, which is where WebIDL puts it and what Node 24 shows:
/// <c>userAgent</c> is an enumerable accessor on <c>Navigator.prototype</c> while
/// <c>Reflect.ownKeys(navigator)</c> is the empty array. That is what lets it carry WebIDL's attributes
/// (https://webidl.spec.whatwg.org/#es-attributes) without <c>Object.keys(navigator)</c> reporting
/// <c>["userAgent"]</c>, which no implementation does — the enumerability is invisible from an instance with
/// no own properties. It still brand-checks its receiver, so extracting the getter and calling it on
/// something else raises a <c>TypeError</c> exactly as a browser does.
/// </para>
/// <para>
/// One documented simplification remains: the <c>navigator</c> object is installed as an ordinary enumerable
/// data property of the global rather than through the <c>[Replaceable]</c> accessor pair WebIDL gives it.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class NavigatorPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly NavigatorConstructor _constructor;

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-class-string — "the class string of an interface prototype object
    /// is the interface's qualified name", so <c>Object.prototype.toString.call(navigator)</c> answers
    /// <c>[object Navigator]</c>. Node 24 answers <c>[object Object]</c> here, because its <c>Navigator</c> is
    /// an ordinary JavaScript class rather than a WebIDL platform object; a browser answers
    /// <c>[object Navigator]</c>, and so does this.
    /// </summary>
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString NavigatorToStringTag = new("Navigator");

    internal NavigatorPrototype(
        Engine engine,
        Realm realm,
        NavigatorConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
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
    /// <remarks>
    /// The value is the engine's — <see cref="Engine.WebApiOperations.UserAgent"/>, which starts as
    /// <c>Options.WebApi.Navigator.UserAgent</c> and defaults to <see cref="ProductToken.UserAgent"/>. It is
    /// read here rather than captured, because a host may move it after the engine was built: a browser's is
    /// whatever a client's <c>Emulation.setUserAgentOverride</c> last said, and the page reads this one
    /// accessor rather than shadowing it with an own property of its own
    /// (<see href="https://github.com/sebastienros/jint/issues/3655">#3655</see>).
    /// </remarks>
    [JsAccessor("userAgent", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString UserAgentGet(JsValue thisObject)
    {
        if (thisObject is not JsNavigator navigator)
        {
            Throw.TypeError(_realm, "Failed to read the 'userAgent' property from 'Navigator': illegal invocation, receiver is not a Navigator object.");
            return JsString.Empty;
        }

        return navigator.Engine.NavigatorUserAgentValue;
    }
}
#endif
