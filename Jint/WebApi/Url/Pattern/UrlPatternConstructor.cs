#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// The <c>URLPattern</c> interface object.
/// <para>
/// https://urlpattern.spec.whatwg.org/#urlpattern-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface declares two constructors —
/// <c>(URLPatternInput input, USVString baseURL, optional URLPatternOptions options)</c> and
/// <c>(optional URLPatternInput input, optional URLPatternOptions options)</c> — which
/// <a href="https://webidl.spec.whatwg.org/#es-overloads">WebIDL overload resolution</a> tells apart by the second
/// argument alone: <see langword="undefined"/>, <see langword="null"/> or any object selects the options
/// overload, anything else is a base URL string. The interface object's <c>length</c> is 0 because that is the
/// smallest number of required arguments across the two.
/// </para>
/// <para>
/// The interface object's <c>length</c> notwithstanding, a pattern built from a plain string is not the same as
/// one built from a <c>URLPatternInit</c>: the string form is a whole-URL shorthand parsed by
/// <see cref="UrlPatternConstructorString"/>, and it refuses to be relative unless it is given a base URL.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class UrlPatternConstructor : Constructor
{
    private static readonly JsString _functionName = new("URLPattern");

    internal UrlPatternConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new UrlPatternPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal UrlPatternPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#dom-urlpattern-urlpattern-input-baseurl-options and its sibling
    /// overload, both of which are the single step "run initialize".
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        // Overload resolution inspects the second argument's type before anything is converted, so this decision
        // comes first and the conversions below then run left to right as WebIDL requires.
        var takesBaseUrl = arguments.Length switch
        {
            0 or 1 => false,
            2 => !(arguments[1].IsUndefined() || arguments[1].IsNull() || arguments[1].IsObject()),
            _ => true,
        };

        var input = ReadInput(arguments.At(0));
        var baseUrl = takesBaseUrl ? UrlValues.ToUsvString(arguments[1]) : null;
        var ignoreCase = ReadIgnoreCase(_realm, takesBaseUrl ? arguments.At(2) : arguments.At(1));

        var pattern = UrlPatternRecord.Create(_engine, input.StringInput, input.Init, baseUrl, ignoreCase);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WebApiUrlPattern.PrototypeObject,
            static (Engine engine, Realm _, UrlPatternRecord? state) => new JsUrlPattern(engine, state!),
            pattern);
    }

    /// <summary>
    /// The <c>URLPatternInput</c> union, https://urlpattern.spec.whatwg.org/#typedefdef-urlpatterninput: an object
    /// — and <see langword="null"/> or <see langword="undefined"/>, which the union sends to the dictionary — is a
    /// <c>URLPatternInit</c>, and anything else is a <c>USVString</c>.
    /// </summary>
    internal static (string? StringInput, UrlPatternInit? Init) ReadInput(JsValue input)
    {
        if (input.IsUndefined() || input.IsNull() || input.IsObject())
        {
            return (null, ReadInit(input));
        }

        return (UrlValues.ToUsvString(input), null);
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#dictdef-urlpatterninit read per
    /// https://webidl.spec.whatwg.org/#es-dictionary: the members are read in declaration order, and a member
    /// whose value is <see langword="undefined"/> — including one that is simply absent — does not exist.
    /// </summary>
    private static UrlPatternInit ReadInit(JsValue value)
    {
        var init = new UrlPatternInit();
        if (value is not ObjectInstance dictionary)
        {
            return init;
        }

        init.Protocol = ReadMember(dictionary, UrlPatternProperties.Protocol);
        init.Username = ReadMember(dictionary, UrlPatternProperties.Username);
        init.Password = ReadMember(dictionary, UrlPatternProperties.Password);
        init.Hostname = ReadMember(dictionary, UrlPatternProperties.Hostname);
        init.Port = ReadMember(dictionary, UrlPatternProperties.Port);
        init.Pathname = ReadMember(dictionary, UrlPatternProperties.Pathname);
        init.Search = ReadMember(dictionary, UrlPatternProperties.Search);
        init.Hash = ReadMember(dictionary, UrlPatternProperties.Hash);
        init.BaseUrl = ReadMember(dictionary, UrlPatternProperties.BaseUrl);
        return init;
    }

    private static string? ReadMember(ObjectInstance dictionary, JsString name)
    {
        var value = dictionary.Get(name);
        return value.IsUndefined() ? null : UrlValues.ToUsvString(value);
    }

    /// <summary>
    /// https://urlpattern.spec.whatwg.org/#dictdef-urlpatternoptions — one member, defaulting to false, so a
    /// pattern is case-sensitive unless it is told otherwise.
    /// </summary>
    private static bool ReadIgnoreCase(Realm realm, JsValue options)
    {
        if (options.IsUndefined() || options.IsNull())
        {
            return false;
        }

        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm, "Failed to construct 'URLPattern': the provided value is not of type 'URLPatternOptions'.");
            return false;
        }

        var value = dictionary.Get(UrlPatternProperties.IgnoreCase);
        return !value.IsUndefined() && TypeConverter.ToBoolean(value);
    }
}
#endif
