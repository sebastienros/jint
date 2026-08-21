#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.DomException;

/// <summary>
/// The <c>QuotaExceededError</c> interface object.
/// <para>
/// https://webidl.spec.whatwg.org/#quotaexceedederror
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>constructor(optional DOMString message = "", optional QuotaExceededErrorOptions options = {})</c>, so
/// <c>length</c> is 0. <c>QuotaExceededError</c> inherits from <c>DOMException</c>, so its
/// <c>[[Prototype]]</c> is the <c>DOMException</c> interface object rather than <c>%Function.prototype%</c>
/// (https://webidl.spec.whatwg.org/#interface-object) — which is what makes
/// <c>Object.getPrototypeOf(QuotaExceededError) === DOMException</c> hold, and what gives the interface object
/// the 25 legacy constants without declaring one of them here.
/// </para>
/// <para>
/// It is the only interface https://webidl.spec.whatwg.org/#idl-DOMException-derived-predefineds defines, and
/// it exists because the name alone could not say <i>how far</i> over a quota a request was. The name stays in
/// the error-names table as <b>deprecated</b>, which is why <c>code</c> still answers 22 where the general
/// rule for a derived interface is 0.
/// </para>
/// </remarks>
internal sealed class QuotaExceededErrorConstructor : Constructor
{
    /// <summary>
    /// The interface's identifier, which is also the <c>[[Name]]</c> every instance carries — constructor step
    /// 1 is "Set this's name to <c>QuotaExceededError</c>", and a derived interface is required to do exactly
    /// that (https://webidl.spec.whatwg.org/#idl-DOMException-derived-interfaces).
    /// </summary>
    internal static readonly JsString ErrorName = new("QuotaExceededError");

    private static readonly JsString _quotaKey = new("quota");
    private static readonly JsString _requestedKey = new("requested");

    internal QuotaExceededErrorConstructor(Engine engine, Realm realm, DomExceptionConstructor domException)
        : base(engine, realm, ErrorName)
    {
        _prototype = domException;
        PrototypeObject = new QuotaExceededErrorPrototype(engine, realm, this, domException.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal QuotaExceededErrorPrototype PrototypeObject { get; }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-quotaexceedederror-quotaexceedederror
    /// </summary>
    /// <remarks>
    /// The dictionary is converted before any of the numbered steps runs, which is what orders the failures:
    /// every <c>TypeError</c> the conversion can raise — a non-object <c>options</c>, a member that is
    /// <c>NaN</c> or infinite, a member getter that throws — happens before the first <c>RangeError</c>, and
    /// the members are read in lexicographical order (<c>quota</c>, then <c>requested</c>) rather than in
    /// declaration order. Only then do steps 3.1, 4.1 and 5 run, in that order.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var messageArgument = arguments.At(0);

        // The IDL default is ""; an explicitly passed undefined takes it too, which is what an optional
        // argument with a default value means in WebIDL.
        var message = messageArgument.IsUndefined() ? JsString.Empty : TypeConverter.ToJsString(messageArgument);

        var dictionary = ToOptionsDictionary(arguments.At(1));
        var quota = ReadMember(dictionary, _quotaKey, "quota");
        var requested = ReadMember(dictionary, _requestedKey, "requested");

        // Step 3.1.
        if (quota is { } quotaValue && quotaValue < 0)
        {
            Throw.RangeError(_realm, "Failed to construct 'QuotaExceededError': the 'quota' option must not be negative.");
        }

        // Step 4.1.
        if (requested is { } requestedValue && requestedValue < 0)
        {
            Throw.RangeError(_realm, "Failed to construct 'QuotaExceededError': the 'requested' option must not be negative.");
        }

        // Step 5: a request smaller than the quota it exceeded is not a state any caller can be describing.
        if (quota is { } quotaBound && requested is { } requestedAmount && requestedAmount < quotaBound)
        {
            Throw.RangeError(_realm, "Failed to construct 'QuotaExceededError': the 'requested' option must not be less than the 'quota' option.");
        }

        var exception = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.QuotaExceededError.PrototypeObject,
            static (Engine engine, Realm _, (JsString Message, JsValue Quota, JsValue Requested) state)
                => new JsQuotaExceededError(engine, state.Message, state.Quota, state.Requested),
            (Message: message, Quota: NumberOrNull(quota), Requested: NumberOrNull(requested)));

        DomExceptionConstructor.AttachStack(_engine, this, exception);

        return exception;
    }

    /// <summary>
    /// Builds a <c>QuotaExceededError</c> from engine code, for the limits the web APIs enforce themselves.
    /// </summary>
    /// <param name="message">The message, or the empty string.</param>
    /// <param name="quota">
    /// How much was available, or <see langword="null"/> when the throwing algorithm does not say. Callers owe
    /// the same well-formedness the constructor enforces: not negative, and not greater than
    /// <paramref name="requested"/> when both are given — "Specifications that create or throw a
    /// QuotaExceededError must not provide a requested and quota that are both non-null and where requested is
    /// less than quota."
    /// </param>
    /// <param name="requested">How much was asked for, or <see langword="null"/>.</param>
    internal JsQuotaExceededError CreateException(string message, double? quota = null, double? requested = null)
    {
        var exception = new JsQuotaExceededError(_engine, JsString.Create(message), NumberOrNull(quota), NumberOrNull(requested))
        {
            _prototype = PrototypeObject,
        };

        DomExceptionConstructor.AttachStack(_engine, this, exception);

        return exception;
    }

    private static JsValue NumberOrNull(double? value) => value is { } number ? JsNumber.Create(number) : JsValue.Null;

    /// <summary>
    /// Step 1 of https://webidl.spec.whatwg.org/#js-dictionary: "If jsDict is not an Object and jsDict is
    /// neither undefined nor null, then throw a TypeError." <see langword="null"/> here stands for the two
    /// values that convert to an empty dictionary.
    /// </summary>
    private ObjectInstance? ToOptionsDictionary(JsValue options)
    {
        if (options is ObjectInstance dictionary)
        {
            return dictionary;
        }

        if (!options.IsUndefined() && !options.IsNull())
        {
            Throw.TypeError(_realm, "Failed to construct 'QuotaExceededError': parameter 2 is not an object.");
        }

        return null;
    }

    /// <summary>
    /// One <c>double</c> dictionary member: absent — which an explicit <c>undefined</c> also is — or a finite
    /// number. https://webidl.spec.whatwg.org/#js-double refuses <c>NaN</c> and the infinities with a
    /// <c>TypeError</c>, which is what makes <c>{ quota: NaN }</c> a <c>TypeError</c> where <c>{ quota: -1 }</c>
    /// is a <c>RangeError</c>.
    /// </summary>
    private double? ReadMember(ObjectInstance? dictionary, JsString key, string name)
    {
        if (dictionary is null)
        {
            return null;
        }

        var value = dictionary.Get(key);
        if (value.IsUndefined())
        {
            return null;
        }

        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            Throw.TypeError(_realm, $"Failed to construct 'QuotaExceededError': the '{name}' option is not a finite number.");
        }

        return number;
    }
}
#endif
