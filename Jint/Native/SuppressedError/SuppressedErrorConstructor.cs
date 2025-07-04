using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.SuppressedError;

internal sealed class SuppressedErrorConstructor : Constructor
{
    private static readonly JsString _name = new("SuppressedError");

    internal SuppressedErrorConstructor(
        Engine engine,
        Realm realm,
        ErrorConstructor errorConstructor)
        : base(engine, realm, _name)
    {
        _prototype = errorConstructor;
        PrototypeObject = new SuppressedErrorPrototype(engine, realm, this, errorConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveThree, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private SuppressedErrorPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-nativeerror
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var error = arguments.At(0);
        var suppressed = arguments.At(1);
        var message = arguments.At(2);

        return Construct(newTarget, message, error, suppressed);
    }

    internal JsError Construct(JsValue newTarget, JsValue message, JsValue error, JsValue suppressed)
    {
        var o = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.SuppressedError.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsError(engine));

        if (!message.IsUndefined())
        {
            var msg = TypeConverter.ToString(message);
            o.CreateNonEnumerableDataPropertyOrThrow(CommonProperties.Message, msg);
        }

        o.DefinePropertyOrThrow("error", new PropertyDescriptor(error, configurable: true, enumerable: false, writable: true));
        o.DefinePropertyOrThrow("suppressed", new PropertyDescriptor(suppressed, configurable: true, enumerable: false, writable: true));

        return o;
    }
}
