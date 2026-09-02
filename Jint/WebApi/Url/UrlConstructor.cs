#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Files;
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
/// <c>URL.createObjectURL</c> and <c>URL.revokeObjectURL</c> come from the File API rather than from the URL
/// standard — <c>partial interface URL</c>, https://w3c.github.io/FileAPI/#creating-revoking — and are
/// installed only on an engine that has <b>both</b> <see cref="WebApiFeatures.Url"/> and
/// <see cref="WebApiFeatures.Files"/>. Neither is any use without the other: the store's entries are
/// <c>Blob</c>s, and the URL it mints has to be parseable. Absent rather than present-and-throwing on an
/// engine with only one of them, which is what feature detection expects to find, and what makes this a pair
/// of conditional members rather than a feature closure that would give every <c>URL</c> user a <c>Blob</c>.
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

    protected override void Initialize()
    {
        CreateProperties_Generated();

        const WebApiFeatures BlobUrls = WebApiFeatures.Url | WebApiFeatures.Files;
        if ((_engine._webApiFeatures & BlobUrls) != BlobUrls)
        {
            return;
        }

        // Static operations, so the same attributes a regular one gets —
        // https://webidl.spec.whatwg.org/#es-operations. Added after the shape rather than declared in it
        // because their presence is conditional; the shaped host keeps its shape and holds these two beside
        // it.
        SetProperty("createObjectURL", new PropertyDescriptor(
            new ClrFunction(_engine, _realm, "createObjectURL", CreateObjectUrl, length: 1, PropertyFlag.Configurable),
            PropertyFlag.ConfigurableEnumerableWritable));

        SetProperty("revokeObjectURL", new PropertyDescriptor(
            new ClrFunction(_engine, _realm, "revokeObjectURL", RevokeObjectUrl, length: 1, PropertyFlag.Configurable),
            PropertyFlag.ConfigurableEnumerableWritable));
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-createObjectURL — "return the result of adding an entry to the blob
    /// URL store for obj".
    /// </summary>
    /// <remarks>
    /// The IDL argument is <c>(Blob or MediaSource)</c> and this engine has no <c>MediaSource</c>, so the
    /// union conversion reduces to the <c>Blob</c> arm and everything else is the <c>TypeError</c> WebIDL
    /// raises for a union nothing in it matches.
    /// </remarks>
    private JsValue CreateObjectUrl(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.At(0) is not JsBlob blob)
        {
            Throw.TypeError(_realm, "Failed to execute 'createObjectURL' on 'URL': parameter 1 is not of type 'Blob'.");
            return Undefined;
        }

        return JsString.Create(FileApi.RequireState(_engine).BlobUrls.Add(blob));
    }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#dfn-revokeObjectURL — parse, and remove the entry the serialization
    /// names.
    /// </summary>
    /// <remarks>
    /// A url that does not parse, or that parses to something other than a <c>blob:</c> URL, is a silent
    /// no-op rather than an error; and the removal is by the serialization <i>including</i> the fragment, so
    /// only an exact match revokes. Both are the algorithm's, and both are asserted by
    /// <c>FileAPI/url/resources/fetch-tests.js</c>.
    /// </remarks>
    private JsValue RevokeObjectUrl(JsValue thisObject, JsCallArguments arguments)
    {
        var record = UrlParser.Parse(UrlValues.ToUsvString(arguments.At(0)));
        if (record is not null)
        {
            FileApi.RequireState(_engine).BlobUrls.Remove(record);
        }

        return Undefined;
    }

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
    [JsFunction(Name = "parse", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
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
    [JsFunction(Name = "canParse", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
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
