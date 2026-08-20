#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The <c>TextDecoder</c> interface object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>constructor(optional DOMString label = "utf-8", optional TextDecoderOptions options = {})</c>. The
/// two arguments are converted first and the constructor steps run after, which is the order WebIDL
/// specifies and the reason a bad <c>label</c> raises its <c>RangeError</c> only once <c>options</c> has
/// been read — a getter on <c>options</c> runs even when the label is nonsense.
/// </para>
/// <para>
/// Called without <c>new</c> it raises a <c>TypeError</c>, which <see cref="Constructor"/> already does
/// for every interface object shape.
/// </para>
/// </remarks>
internal sealed class TextDecoderConstructor : Constructor
{
    private static readonly JsString _functionName = new("TextDecoder");
    private static readonly JsString _fatal = new("fatal");
    private static readonly JsString _ignoreBom = new("ignoreBOM");

    internal TextDecoderConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new TextDecoderPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TextDecoderPrototype PrototypeObject { get; }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var labelArgument = arguments.At(0);
        var optionsArgument = arguments.At(1);

        // `optional DOMString label = "utf-8"`: an omitted argument and an explicitly passed undefined
        // both take the default.
        var label = labelArgument.IsUndefined() ? EncodingLabels.Utf8 : TypeConverter.ToString(labelArgument);

        // The dictionary conversion, https://webidl.spec.whatwg.org/#es-dictionary: undefined and null
        // are the empty dictionary, anything else that is not an object is a TypeError, and the members
        // are read in lexicographic order — "fatal" before "ignoreBOM".
        var fatal = false;
        var ignoreBom = false;
        if (!optionsArgument.IsUndefined() && !optionsArgument.IsNull())
        {
            if (optionsArgument is not ObjectInstance options)
            {
                Throw.TypeError(_realm, "TextDecoder: options must be an object");
                return null!;
            }

            fatal = TypeConverter.ToBoolean(options.Get(_fatal));
            ignoreBom = TypeConverter.ToBoolean(options.Get(_ignoreBom));
        }

        // Step 1 and 2: get an encoding from the label, and refuse anything the table does not name.
        var encoding = EncodingLabels.Lookup(label);
        if (encoding is null)
        {
            Throw.RangeError(_realm, "TextDecoder: the encoding label provided ('" + label + "') is invalid");
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TextDecoder.PrototypeObject,
            static (Engine engine, Realm _, (string Encoding, bool Fatal, bool IgnoreBom) state)
                => new JsTextDecoder(engine, state.Encoding, state.Fatal, state.IgnoreBom),
            (Encoding: encoding!, Fatal: fatal, IgnoreBom: ignoreBom));
    }
}
#endif
