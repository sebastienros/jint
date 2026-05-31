using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error;

[JsObject]
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
            o.CreateNonEnumerableDataPropertyOrThrow(CommonProperties.Message, msg);
        }

        // Per the error-stack-accessor proposal, "stack" is no longer an own data property of the
        // instance; it is exposed via the get/set accessor on %Error.prototype%. The captured trace
        // is stored in the [[ErrorData]]-bearing instance and returned by that getter.
        o._stack = BuildStackTraceString(_engine, this);

        var options = arguments.At(1);
        if (!options.IsUndefined())
        {
            o.InstallErrorCause(options);
        }

        return o;
    }

    /// <summary>
    /// Builds the implementation-defined stack trace string for an error being constructed by
    /// <paramref name="constructor"/>. Returns <c>null</c> when there is no active script location.
    /// </summary>
    internal static JsValue? BuildStackTraceString(Engine engine, JsValue constructor)
    {
        var lastSyntaxNode = engine.GetLastSyntaxElement();
        if (lastSyntaxNode == null)
        {
            return null;
        }

        var callStack = engine.CallStack;
        var currentFunction = callStack.TryPeek(out var element) ? element.Function : null;

        // If the current function is the error constructor itself (i.e. "throw new Error(...)" was called
        // from script), exclude it from the stack trace, because the trace should begin at the throw point.
        return callStack.BuildCallStackString(engine, lastSyntaxNode.Location, currentFunction == constructor ? 1 : 0);
    }

    /// <summary>
    /// https://tc39.es/proposal-is-error/
    /// </summary>
    [JsFunction]
    private static JsValue IsError(JsValue thisObject, JsValue arg) => arg is JsError;
}
