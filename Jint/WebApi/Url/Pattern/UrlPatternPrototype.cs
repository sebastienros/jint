#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// <c>URLPattern.prototype</c> — the interface prototype object.
/// <para>
/// https://urlpattern.spec.whatwg.org/#urlpattern-class
/// </para>
/// </summary>
/// <remarks>
/// The nine attributes are read-only, so each is an accessor pair with no setter, and each brand-checks its
/// receiver as WebIDL requires. The two operations carry the same documented simplification the rest of this
/// subtree carries: they are non-enumerable, where https://webidl.spec.whatwg.org/#es-operations makes an
/// interface's operations enumerable.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class UrlPatternPrototype : Prototype
{
    private const PropertyFlag AttributeFlags = PropertyFlag.Configurable | PropertyFlag.Enumerable;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly UrlPatternConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString UrlPatternToStringTag = new("URLPattern");

    internal UrlPatternPrototype(
        Engine engine,
        Realm realm,
        UrlPatternConstructor constructor,
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

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-protocol</summary>
    [JsAccessor("protocol", Flags = AttributeFlags)]
    private JsString ProtocolGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Protocol.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-username</summary>
    [JsAccessor("username", Flags = AttributeFlags)]
    private JsString UsernameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Username.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-password</summary>
    [JsAccessor("password", Flags = AttributeFlags)]
    private JsString PasswordGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Password.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-hostname</summary>
    [JsAccessor("hostname", Flags = AttributeFlags)]
    private JsString HostnameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Hostname.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-port</summary>
    [JsAccessor("port", Flags = AttributeFlags)]
    private JsString PortGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Port.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-pathname</summary>
    [JsAccessor("pathname", Flags = AttributeFlags)]
    private JsString PathnameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Pathname.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-search</summary>
    [JsAccessor("search", Flags = AttributeFlags)]
    private JsString SearchGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Search.PatternString);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-hash</summary>
    [JsAccessor("hash", Flags = AttributeFlags)]
    private JsString HashGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Pattern.Hash.PatternString);

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#dom-urlpattern-hasregexpgroups — whether any component uses a
    /// "<c>(regexp)</c>" group, which is the question an API that will not evaluate author regular expressions
    /// asks before accepting a pattern.
    /// </summary>
    [JsAccessor("hasRegExpGroups", Flags = AttributeFlags)]
    private JsBoolean HasRegExpGroupsGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Pattern.HasRegexpGroups);

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-test</summary>
    [JsFunction(Name = "test", Length = 0)]
    private JsBoolean Test(JsValue thisObject, JsValue input, JsValue baseUrl)
    {
        var pattern = Brand(thisObject);
        var readInput = UrlPatternConstructor.ReadInput(input);
        var baseUrlString = UrlValues.ToOptionalUsvString(baseUrl);
        var result = pattern.Pattern.Match(_engine, readInput.StringInput, readInput.Init, baseUrlString);
        return JsBoolean.Create(!result.IsNull());
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#dom-urlpattern-exec</summary>
    [JsFunction(Name = "exec", Length = 0)]
    private JsValue Exec(JsValue thisObject, JsValue input, JsValue baseUrl)
    {
        var pattern = Brand(thisObject);
        var readInput = UrlPatternConstructor.ReadInput(input);
        var baseUrlString = UrlValues.ToOptionalUsvString(baseUrl);
        return pattern.Pattern.Match(_engine, readInput.StringInput, readInput.Init, baseUrlString);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing the
    /// interface raises a <c>TypeError</c>.
    /// </summary>
    private JsUrlPattern Brand(JsValue thisObject)
    {
        if (thisObject is JsUrlPattern pattern)
        {
            return pattern;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a URLPattern");
        return null!;
    }
}
#endif
