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

        o._stackCapture = ErrorConstructor.BuildStackTraceCapture(_engine, this);

        if (!message.IsUndefined())
        {
            var msg = TypeConverter.ToJsString(message);
            o.SetVirtualMessage(msg);
        }

        DefineErrorAndSuppressed(o, error, suppressed);

        return o;
    }

    /// <summary>
    /// The "newly created SuppressedError object" that DisposeResources throws when a dispose method throws over an
    /// earlier throw completion (https://tc39.es/ecma262/#sec-disposeresources), with <c>error</c> and
    /// <c>suppressed</c> defined and nothing else.
    /// </summary>
    /// <remarks>
    /// This is not <see cref="Construct(JsValue, JsValue, JsValue, JsValue)"/>: the specification creates the object and
    /// defines the two properties itself, so there is no own <c>message</c>. The error inherits
    /// <c>SuppressedError.prototype.message</c>, the empty string, which the constructor's
    /// <c>message</c> argument would shadow with an own property for any value but <c>undefined</c>. The prototype is
    /// that of the realm this constructor belongs to, which is the realm the caller chose: disposal passes the realm of
    /// the running code.
    /// </remarks>
    internal JsError CreateForDisposal(JsValue error, JsValue suppressed)
    {
        var o = new JsError(_engine) { _prototype = PrototypeObject };
        o._stackCapture = ErrorConstructor.BuildStackTraceCapture(_engine, this);

        DefineErrorAndSuppressed(o, error, suppressed);

        return o;
    }

    private static void DefineErrorAndSuppressed(JsError o, JsValue error, JsValue suppressed)
    {
        o.DefinePropertyOrThrow("error", new PropertyDescriptor(error, configurable: true, enumerable: false, writable: true));
        o.DefinePropertyOrThrow("suppressed", new PropertyDescriptor(suppressed, configurable: true, enumerable: false, writable: true));
    }
}
