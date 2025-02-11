using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Error;

public sealed class ErrorConstructor : Constructor
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

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["isError"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "isError", IsError, 1), PropertyFlag.NonEnumerable)),
        };
        SetProperties(properties);
    }

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

        var stackString = BuildStackString();
        if (stackString is not null)
        {
            var stackDesc = new PropertyDescriptor(stackString, PropertyFlag.NonEnumerable);
            o.DefinePropertyOrThrow(CommonProperties.Stack, stackDesc);
        }

        var options = arguments.At(1);
        if (!options.IsUndefined())
        {
            o.InstallErrorCause(options);
        }

        return o;

        JsValue? BuildStackString()
        {
            var lastSyntaxNode = _engine.GetLastSyntaxElement();
            if (lastSyntaxNode == null)
            {
                return null;
            }

            var callStack = _engine.CallStack;
            var currentFunction = callStack.TryPeek(out var element) ? element.Function : null;

            // If the current function is the ErrorConstructor itself (i.e. "throw new Error(...)" was called
            // from script), exclude it from the stack trace, because the trace should begin at the throw point.
            return callStack.BuildCallStackString(_engine, lastSyntaxNode.Location, currentFunction == this ? 1 : 0);
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-is-error/
    /// </summary>
    private static JsValue IsError(JsValue? thisObj, JsCallArguments arguments)
    {
        return arguments.At(0) is JsError;
    }
}
