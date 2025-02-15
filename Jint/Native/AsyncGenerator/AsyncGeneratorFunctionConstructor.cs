using Jint.Native.AsyncFunction;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgeneratorfunction-constructor
/// </summary>
internal sealed class AsyncGeneratorFunctionConstructor : Constructor
{
    private static readonly JsString _functionName = new("AsyncGeneratorFunction");

    internal AsyncGeneratorFunctionConstructor(
        Engine engine,
        Realm realm,
        AsyncFunctionPrototype prototype,
        IteratorPrototype iteratorPrototype)
        : base(engine, realm, _functionName)
    {
        PrototypeObject = new AsyncGeneratorFunctionPrototype(engine, this, prototype, iteratorPrototype);
        _prototype = PrototypeObject;
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    public AsyncGeneratorFunctionPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var function = _realm.Intrinsics.Function.CreateDynamicFunction(
            this,
            newTarget,
            FunctionKind.AsyncGenerator,
            arguments);

        return function;
    }
}
