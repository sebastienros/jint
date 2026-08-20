using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgeneratorfunction-constructor
/// </summary>
internal sealed class AsyncGeneratorFunctionConstructor : Constructor
{
    private static readonly JsString _functionName = new("AsyncGeneratorFunction");

    internal AsyncGeneratorFunctionConstructor(
        Engine engine,
        Realm realm,
        FunctionConstructor functionConstructor,
        AsyncIteratorPrototype asyncIteratorPrototype)
        : base(engine, realm, _functionName)
    {
        PrototypeObject = new AsyncGeneratorFunctionPrototype(engine, this, functionConstructor.PrototypeObject, asyncIteratorPrototype);
        // https://tc39.es/ecma262/#sec-asyncgeneratorfunction-constructor
        // The AsyncGeneratorFunction constructor has a [[Prototype]] internal slot whose value is %Function%,
        // i.e. the Function constructor itself and not %AsyncGeneratorFunction.prototype%.
        _prototype = functionConstructor;
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
