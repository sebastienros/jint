using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AsyncFunction;

/// <summary>
/// https://tc39.es/ecma262/#sec-async-function-constructor
/// </summary>
internal sealed class AsyncFunctionConstructor : Constructor
{
    private static readonly JsString _functionName = new("AsyncFunction");

    public AsyncFunctionConstructor(Engine engine, Realm realm, FunctionConstructor functionConstructor) : base(engine, realm, _functionName)
    {
        PrototypeObject = new AsyncFunctionPrototype(engine, realm, this, functionConstructor.PrototypeObject);
        _prototype = functionConstructor;
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    public AsyncFunctionPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var function = CreateDynamicFunction(
            this,
            newTarget,
            FunctionKind.Async,
            arguments);

        return function;
    }
}
