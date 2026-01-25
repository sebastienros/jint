using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.duration
/// </summary>
internal sealed class DurationConstructor : Constructor
{
    private static readonly JsString _functionName = new("Duration");

    internal DurationConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DurationPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public DurationPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["from"] = new(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags),
            ["compare"] = new(new ClrFunction(Engine, "compare", Compare, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.from
    /// </summary>
    private JsValue From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        // TODO: Implement ToTemporalDuration
        Throw.TypeError(_realm, "Temporal.Duration.from is not yet implemented");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.compare
    /// </summary>
    private JsValue Compare(JsValue thisObject, JsCallArguments arguments)
    {
        // TODO: Implement Duration comparison
        Throw.TypeError(_realm, "Temporal.Duration.compare is not yet implemented");
        return Undefined;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Duration cannot be called as a function");
        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        // TODO: Implement Duration construction
        Throw.TypeError(_realm, "Temporal.Duration constructor is not yet implemented");
        return null!;
    }

    internal JsDuration Construct(DurationRecord duration)
    {
        return new JsDuration(_engine, PrototypeObject, duration);
    }
}
