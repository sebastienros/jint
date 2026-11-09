using Jint.Native.Array;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AggregateError;

/// <summary>
/// https://tc39.es/ecma262/#sec-aggregate-error-constructor
/// </summary>
internal sealed class AggregateErrorConstructor : Constructor
{
    private static readonly JsString _name = new("AggregateError");

    internal AggregateErrorConstructor(
        Engine engine,
        Realm realm,
        ErrorConstructor errorConstructor)
        : base(engine, realm, _name)
    {
        _prototype = errorConstructor;
        PrototypeObject = new AggregateErrorPrototype(engine, realm, this, errorConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveTwo, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private AggregateErrorPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-nativeerror
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var errors = arguments.At(0);
        var message = arguments.At(1);
        var options = arguments.At(2);

        var o = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.AggregateError.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsError(engine));

        o._stackCapture = ErrorConstructor.BuildStackTraceCapture(_engine, this);

        if (!message.IsUndefined())
        {
            var msg = TypeConverter.ToJsString(message);
            o.SetVirtualMessage(msg);
        }

        o.InstallErrorCause(options);

        var errorsList = TypedArrayConstructor.IterableToList(_realm, errors);
        DefineErrors(o, _realm.Intrinsics.Array.CreateArrayFromList(errorsList));

        return o;
    }

    /// <summary>
    /// The "newly created AggregateError object" that <c>Promise.any</c> rejects with, built from the list of
    /// rejection reasons it collected (https://tc39.es/ecma262/#sec-performpromiseany).
    /// </summary>
    /// <remarks>
    /// This is not <see cref="Construct"/>: the specification creates the error and defines <c>errors</c> from
    /// <c>CreateArrayFromList</c> itself, so nothing here iterates <paramref name="errors"/> through
    /// <c>Array.prototype[Symbol.iterator]</c> or reads the <c>AggregateError</c> global, both of which a script
    /// can replace. The prototype is this realm's %AggregateError.prototype%, and no <c>message</c> is set.
    /// </remarks>
    internal JsError CreateFromList(List<JsValue> errors)
    {
        var o = new JsError(_engine) { _prototype = PrototypeObject };
        o._stackCapture = ErrorConstructor.BuildStackTraceCapture(_engine, this);

        DefineErrors(o, _realm.Intrinsics.Array.ConstructFast(errors));
        return o;
    }

    private static void DefineErrors(JsError error, JsArray errors)
    {
        error.DefinePropertyOrThrow("errors", new PropertyDescriptor(errors, configurable: true, enumerable: false, writable: true));
    }
}
