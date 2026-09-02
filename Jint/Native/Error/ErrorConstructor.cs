using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error;

[JsObject(UseShape = true)]
public sealed partial class ErrorConstructor : Constructor
{
    private readonly Func<Intrinsics, ObjectInstance> _intrinsicDefaultProto;

    internal ErrorConstructor(
        Engine engine,
        Realm realm,
        ObjectInstance functionPrototype,
        ObjectInstance objectPrototype,
        JsString name, Func<Intrinsics, ObjectInstance> intrinsicDefaultProto)
        : base(engine, realm, name)
    {
        _intrinsicDefaultProto = intrinsicDefaultProto;
        _prototype = functionPrototype;
        PrototypeObject = new ErrorPrototype(engine, realm, this, objectPrototype, name);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ErrorPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    public ObjectInstance Construct(string? message = null)
    {
        return Construct(message != null ? [message] : [], this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-nativeerror
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var o = OrdinaryCreateFromConstructor(
            newTarget,
            _intrinsicDefaultProto,
            static (Engine engine, Realm _, object? _) => new JsError(engine));

        var jsValue = arguments.At(0);
        if (!jsValue.IsUndefined())
        {
            var msg = TypeConverter.ToJsString(jsValue);
            o.SetVirtualMessage(msg);
        }

        // Per the error-stack-accessor proposal, "stack" is no longer an own data property of the
        // instance; it is exposed via the get/set accessor on %Error.prototype%. The captured trace
        // is stored in the [[ErrorData]]-bearing instance and rendered lazily by that getter — most
        // thrown errors are caught without ever reading .stack, so the string is never built.
        o._stackCapture = BuildStackTraceCapture(_engine, this);

        var options = arguments.At(1);
        if (!options.IsUndefined())
        {
            o.InstallErrorCause(options);
        }

        return o;
    }

    /// <summary>
    /// Captures a deferred snapshot of the call stack for an error being constructed by
    /// <paramref name="constructor"/>, so its implementation-defined stack-trace string can be rendered on
    /// first read. Returns <c>null</c> when there is no active script location.
    /// </summary>
    internal static ErrorStackCapture? BuildStackTraceCapture(Engine engine, JsValue constructor)
    {
        var callStack = engine.CallStack;
        var currentFunction = callStack.TryPeek(out var element) ? element.Function : null;

        // If the current function is the error constructor itself (i.e. "throw new Error(...)" was called
        // from script), exclude it from the stack trace, because the trace should begin at the throw point.
        return BuildStackTraceCapture(engine, currentFunction == constructor ? 1 : 0);
    }

    /// <summary>
    /// Captures a deferred snapshot of the call stack with the top <paramref name="excludeTop"/> frames left
    /// out, so a dispatcher that is itself on the stack can report the call site rather than itself.
    /// </summary>
    /// <remarks>Returns <see langword="null"/> when there is no active script location.</remarks>
    internal static ErrorStackCapture? BuildStackTraceCapture(Engine engine, int excludeTop)
    {
        var lastSyntaxNode = engine.GetLastSyntaxElement();
        if (lastSyntaxNode == null)
        {
            return null;
        }

        var frames = engine.CallStack.SnapshotFrames(excludeTop);
        return new ErrorStackCapture(engine, lastSyntaxNode.Location, frames);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-error.iserror — "If arg is not an Object, return false. If arg does not
    /// have an [[ErrorData]] internal slot, return false. Return true."
    /// </summary>
    /// <remarks>
    /// <see cref="JsError"/> is tested first and by its sealed exact type, which is a single cheap check and
    /// answers every ECMAScript error; the <see cref="IErrorData"/> arm behind it is what a platform object
    /// carrying the slot is recognized by — a <c>DOMException</c>, which
    /// https://webidl.spec.whatwg.org/#internally-create-a-new-object-implementing-the-interface gives
    /// <c>[[ErrorData]]</c> ("If interface is DOMException, append [[ErrorData]] to slots").
    /// </remarks>
    [JsFunction]
    private static JsValue IsError(JsValue thisObject, JsValue arg) => arg is JsError or IErrorData;
}
