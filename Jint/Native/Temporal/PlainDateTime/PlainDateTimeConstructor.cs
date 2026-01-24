using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime
/// </summary>
internal sealed class PlainDateTimeConstructor : Constructor
{
    private static readonly JsString _functionName = new("PlainDateTime");

    internal PlainDateTimeConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PlainDateTimePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(3), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PlainDateTimePrototype PrototypeObject { get; }

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

    private JsValue From(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDateTime.from is not yet implemented");
        return Undefined;
    }

    private JsValue Compare(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDateTime.compare is not yet implemented");
        return Undefined;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDateTime cannot be called as a function");
        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Temporal.PlainDateTime constructor is not yet implemented");
        return null!;
    }
}
