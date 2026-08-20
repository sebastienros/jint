#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// <c>URL.prototype</c> — the interface prototype object.
/// <para>
/// https://url.spec.whatwg.org/#url-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every attribute of the interface is an accessor pair here, as WebIDL specifies attributes, each brand-checking
/// its receiver and raising a <c>TypeError</c> for anything that is not a <c>URL</c> — including
/// <c>URL.prototype</c> itself, which is not one. The <c>stringifier attribute USVString href</c> declaration is
/// what gives the prototype its <c>toString</c>, and <c>toJSON</c> returns the same serialization.
/// </para>
/// <para>
/// The setters are all defined as "basic URL parse the given value with this's URL as url and <i>some</i> state
/// as state override"; that half of each lives in <see cref="UrlSetters"/>, engine-free, so the Web Platform
/// Tests setter corpus can drive it directly. What stays here is the WebIDL skin: the brand check, the USVString
/// conversion, and — for <c>href</c> and <c>search</c> — keeping the query object's list in step.
/// </para>
/// <para>
/// One documented simplification, the same one <c>console</c> carries: the operations (<c>toString</c>,
/// <c>toJSON</c>) are non-enumerable, where https://webidl.spec.whatwg.org/#es-operations makes an interface's
/// operations enumerable. The attributes above are enumerable and configurable as WebIDL specifies. Neither is
/// observable except to code inspecting property attributes or enumerating the prototype.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class UrlPrototype : Prototype
{
    private const PropertyFlag AttributeFlags = PropertyFlag.Configurable | PropertyFlag.Enumerable;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly UrlConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString UrlToStringTag = new("URL");

    internal UrlPrototype(
        Engine engine,
        Realm realm,
        UrlConstructor constructor,
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
    /// https://url.spec.whatwg.org/#dom-url-href
    /// </summary>
    [JsAccessor("href", Flags = AttributeFlags)]
    private JsString HrefGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.Serialize());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-href
    /// </summary>
    [JsAccessor("href", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue HrefSet(JsValue thisObject, JsValue value)
    {
        var url = Brand(thisObject);
        var input = UrlValues.ToUsvString(value);

        var parsed = UrlParser.Parse(input);
        if (parsed is null)
        {
            Throw.TypeError(_realm, UrlConstructor.InvalidUrlMessage(input, baseHref: null));
        }

        url.Url = parsed;
        url.ReplaceQueryObjectList(FormUrlEncoded.Parse(parsed.Query ?? string.Empty));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-origin
    /// </summary>
    [JsAccessor("origin", Flags = AttributeFlags)]
    private JsString OriginGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializeOrigin());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-protocol
    /// </summary>
    [JsAccessor("protocol", Flags = AttributeFlags)]
    private JsString ProtocolGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializeProtocol());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-protocol
    /// </summary>
    [JsAccessor("protocol", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue ProtocolSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetProtocol(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-username
    /// </summary>
    [JsAccessor("username", Flags = AttributeFlags)]
    private JsString UsernameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.Username);

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-username
    /// </summary>
    [JsAccessor("username", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue UsernameSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetUsername(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-password
    /// </summary>
    [JsAccessor("password", Flags = AttributeFlags)]
    private JsString PasswordGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.Password);

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-password
    /// </summary>
    [JsAccessor("password", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue PasswordSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetPassword(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-host
    /// </summary>
    [JsAccessor("host", Flags = AttributeFlags)]
    private JsString HostGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializeHostAndPort());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-host
    /// </summary>
    [JsAccessor("host", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue HostSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetHost(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-hostname
    /// </summary>
    [JsAccessor("hostname", Flags = AttributeFlags)]
    private JsString HostnameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializeHost());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-hostname
    /// </summary>
    [JsAccessor("hostname", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue HostnameSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetHostname(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-port
    /// </summary>
    [JsAccessor("port", Flags = AttributeFlags)]
    private JsString PortGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializePort());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-port
    /// </summary>
    [JsAccessor("port", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue PortSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetPort(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-pathname
    /// </summary>
    [JsAccessor("pathname", Flags = AttributeFlags)]
    private JsString PathnameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializePath());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-pathname
    /// </summary>
    [JsAccessor("pathname", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue PathnameSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetPathname(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-search
    /// </summary>
    [JsAccessor("search", Flags = AttributeFlags)]
    private JsString SearchGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializeSearch());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-search
    /// </summary>
    [JsAccessor("search", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue SearchSet(JsValue thisObject, JsValue value)
    {
        var url = Brand(thisObject);
        url.ReplaceQueryObjectList(UrlSetters.SetSearch(url.Url, UrlValues.ToUsvString(value)));
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-searchparams — <c>[SameObject]</c>, so the same object every time.
    /// </summary>
    [JsAccessor("searchParams", Flags = AttributeFlags)]
    private JsUrlSearchParams SearchParamsGet(JsValue thisObject) => Brand(thisObject).QueryObject;

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-hash
    /// </summary>
    [JsAccessor("hash", Flags = AttributeFlags)]
    private JsString HashGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.SerializeHash());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-hash
    /// </summary>
    [JsAccessor("hash", AccessorKind.Set, Flags = AttributeFlags)]
    private JsValue HashSet(JsValue thisObject, JsValue value)
    {
        UrlSetters.SetHash(Brand(thisObject).Url, UrlValues.ToUsvString(value));
        return Undefined;
    }

    /// <summary>
    /// The stringification behaviour of <c>stringifier attribute USVString href</c>,
    /// https://webidl.spec.whatwg.org/#idl-stringifiers.
    /// </summary>
    [JsFunction(Name = "toString", Length = 0)]
    private JsString Stringify(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.Serialize());

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-tojson
    /// </summary>
    [JsFunction(Name = "toJSON", Length = 0)]
    private JsString ToJsonMethod(JsValue thisObject) => JsString.Create(Brand(thisObject).Url.Serialize());

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing the
    /// interface raises a <c>TypeError</c>.
    /// </summary>
    private JsUrl Brand(JsValue thisObject)
    {
        if (thisObject is JsUrl url)
        {
            return url;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a URL");
        return null!;
    }
}
#endif
