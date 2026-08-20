#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// The <c>URL</c> interface object.
/// <para>
/// https://url.spec.whatwg.org/#url-class
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(USVString url, optional USVString base)</c>, plus the two statics
/// <c>URL.parse</c> and <c>URL.canParse</c>. As a WebIDL interface object its <c>[[Prototype]]</c> is
/// <c>%Function.prototype%</c> and calling it without <c>new</c> raises a <c>TypeError</c>, which
/// <see cref="Constructor"/> already does.
/// <para>
/// <c>URL.createObjectURL</c> and <c>URL.revokeObjectURL</c> are deliberately absent rather than present and
/// throwing: they belong to the File API's blob URL store, which this engine has none of, and an absent member
/// is what feature detection expects to find.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class UrlConstructor : Constructor
{
    private static readonly JsString _functionName = new("URL");

    internal UrlConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new UrlPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal UrlPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-url
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        // WebIDL converts the arguments left to right before any of them is used, so a throwing toString on
        // the base is observed after the one on the url.
        var input = UrlValues.ToUsvString(arguments.At(0));
        var baseHref = UrlValues.ToOptionalUsvString(arguments.At(1));

        var record = UrlParser.ParseApi(input, baseHref);
        if (record is null)
        {
            Throw.TypeError(_realm, InvalidUrlMessage(input, baseHref));
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WebApiUrl.PrototypeObject,
            static (Engine engine, Realm realm, UrlRecord? state) => new JsUrl(engine, realm, state!),
            record);
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-parse
    /// </summary>
    [JsFunction(Name = "parse", Length = 1)]
    private JsValue Parse(JsValue thisObject, JsValue url, JsValue baseUrl)
    {
        var record = UrlParser.ParseApi(UrlValues.ToUsvString(url), UrlValues.ToOptionalUsvString(baseUrl));
        if (record is null)
        {
            return Null;
        }

        return new JsUrl(_engine, _realm, record) { _prototype = PrototypeObject };
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-url-canparse
    /// </summary>
    [JsFunction(Name = "canParse", Length = 1)]
    private static JsBoolean CanParse(JsValue thisObject, JsValue url, JsValue baseUrl)
    {
        var record = UrlParser.ParseApi(UrlValues.ToUsvString(url), UrlValues.ToOptionalUsvString(baseUrl));
        return JsBoolean.Create(record is not null);
    }

    /// <summary>
    /// The message every failing parse reports. Both parts are already-coerced strings, so building it runs
    /// no user code.
    /// </summary>
    internal static string InvalidUrlMessage(string input, string? baseHref)
        => baseHref is null ? $"Invalid URL: {input}" : $"Invalid URL: {input} (against base {baseHref})";
}
#endif
