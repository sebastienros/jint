using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.instant
/// </summary>
internal sealed class InstantConstructor : Constructor
{
    private static readonly JsString _functionName = new("Instant");

    internal InstantConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new InstantPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public InstantPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["from"] = new(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags),
            ["fromEpochMilliseconds"] = new(new ClrFunction(Engine, "fromEpochMilliseconds", FromEpochMilliseconds, 1, LengthFlags), PropertyFlags),
            ["fromEpochNanoseconds"] = new(new ClrFunction(Engine, "fromEpochNanoseconds", FromEpochNanoseconds, 1, LengthFlags), PropertyFlags),
            ["compare"] = new(new ClrFunction(Engine, "compare", Compare, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);
    }

    private JsValue From(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Instant.from is not yet implemented");
        return Undefined;
    }

    private JsValue FromEpochMilliseconds(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Instant.fromEpochMilliseconds is not yet implemented");
        return Undefined;
    }

    private JsValue FromEpochNanoseconds(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Instant.fromEpochNanoseconds is not yet implemented");
        return Undefined;
    }

    private JsValue Compare(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Instant.compare is not yet implemented");
        return Undefined;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Instant cannot be called as a function");
        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Temporal.Instant constructor is not yet implemented");
        return null!;
    }
}
