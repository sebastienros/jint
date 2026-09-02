#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// The <c>ProgressEvent</c> interface object.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-progressevent
/// </para>
/// </summary>
/// <remarks>
/// <c>ProgressEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c> interface
/// object rather than <c>%Function.prototype%</c> — https://webidl.spec.whatwg.org/#interface-object. It
/// declares no static member of its own, so unlike its prototype it needs nothing from the source generator.
/// </remarks>
internal sealed class ProgressEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("ProgressEvent");
    private static readonly JsString _lengthComputable = new("lengthComputable");
    private static readonly JsString _loaded = new("loaded");
    private static readonly JsString _total = new("total");

    internal ProgressEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new ProgressEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ProgressEventPrototype PrototypeObject { get; }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-progressevent-progressevent
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "ProgressEvent");
        var initArgument = arguments.At(1);

        // The inherited members are converted before the interface's own, which is the order
        // https://webidl.spec.whatwg.org/#es-dictionary puts an inherited dictionary's members in.
        var init = EventConstructor.ReadEventInit(_realm, initArgument, "ProgressEvent");
        var progress = ReadProgressInit(initArgument);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.ProgressEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, ProgressAmounts Progress) state)
                => new JsProgressEvent(
                    engine,
                    state.Type,
                    state.Init,
                    state.TimeStamp,
                    state.Progress.LengthComputable,
                    state.Progress.Loaded,
                    state.Progress.Total),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Progress: progress));
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#progresseventinit, whose three members all have IDL defaults — so an
    /// absent dictionary gives <c>false</c>, <c>0</c> and <c>0</c>.
    /// </summary>
    /// <remarks>
    /// <c>loaded</c> and <c>total</c> are <c>unsigned long long</c>, so a negative or fractional value wraps
    /// modulo 2^64 rather than being refused — <see cref="ToUnsignedLongLong"/> is that conversion.
    /// </remarks>
    private static ProgressAmounts ReadProgressInit(JsValue init)
    {
        if (init is not ObjectInstance dictionary)
        {
            return default;
        }

        return new ProgressAmounts(
            TypeConverter.ToBoolean(dictionary.Get(_lengthComputable)),
            ToUnsignedLongLong(dictionary.Get(_loaded)),
            ToUnsignedLongLong(dictionary.Get(_total)));
    }

    /// <summary>
    /// WebIDL's <c>unsigned long long</c> conversion, https://webidl.spec.whatwg.org/#idl-unsigned-long-long:
    /// truncate towards zero and wrap modulo 2^64.
    /// </summary>
    /// <remarks>
    /// The result is kept as a <see cref="double"/> rather than a <see cref="ulong"/>, because that is what
    /// the attribute reads back as and because every value a real transfer reaches is exactly representable.
    /// A value past 2^53 is already imprecise as a JavaScript number, so nothing is lost that the language
    /// still had.
    /// </remarks>
    internal static double ToUnsignedLongLong(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            return 0;
        }

        var truncated = System.Math.Truncate(number);
        if (truncated is >= 0 and < 18446744073709551616.0)
        {
            return truncated;
        }

        // Outside the range the modulo is what the conversion asks for, and it is reached through decimal
        // arithmetic because a double past 2^64 cannot be cast to one.
        var wrapped = truncated % 18446744073709551616.0;
        return wrapped < 0 ? wrapped + 18446744073709551616.0 : wrapped;
    }

    /// <summary>
    /// The three members of <c>ProgressEventInit</c> the interface's own IDL declares, read together so that
    /// the constructor's <c>OrdinaryCreateFromConstructor</c> state stays one value.
    /// </summary>
    private readonly record struct ProgressAmounts(bool LengthComputable, double Loaded, double Total);

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire for a progress event the engine creates itself, so
    /// <c>isTrusted</c> is true. Not reachable from script.
    /// </summary>
    internal JsProgressEvent CreateTrustedProgressEvent(JsString type, bool lengthComputable, double loaded, double total)
    {
        return new JsProgressEvent(_engine, type, default, EventConstructor.TimeStampNow(_engine), lengthComputable, loaded, total)
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }
}
#endif
