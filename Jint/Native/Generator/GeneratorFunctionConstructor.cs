using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-generatorfunction-constructor
/// </summary>
internal sealed class GeneratorFunctionConstructor : Constructor
{
    private static readonly JsString _functionName = new("GeneratorFunction");

    internal GeneratorFunctionConstructor(
        Engine engine,
        Realm realm,
        FunctionConstructor functionConstructor,
        IteratorPrototype iteratorPrototype)
        : base(engine, realm, _functionName)
    {
        PrototypeObject = new GeneratorFunctionPrototype(engine, this, functionConstructor.PrototypeObject, iteratorPrototype);
        // https://tc39.es/ecma262/#sec-generatorfunction-constructor
        // The GeneratorFunction constructor has a [[Prototype]] internal slot whose value is %Function%,
        // i.e. the Function constructor itself and not %GeneratorFunction.prototype%.
        _prototype = functionConstructor;
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    public GeneratorFunctionPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var function = _realm.Intrinsics.Function.CreateDynamicFunction(
            this,
            newTarget,
            FunctionKind.Generator,
            arguments);

        return function;
    }
}
