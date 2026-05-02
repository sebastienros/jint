using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-generatorfunction-prototype-object
/// </summary>
[JsObject]
internal sealed partial class GeneratorFunctionPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.Configurable)]
    private readonly GeneratorFunctionConstructor _constructor;

    [JsProperty(Name = "prototype", Flags = PropertyFlag.Configurable)]
    private readonly GeneratorPrototype _prototypeObject;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ToStringTagValue = new("GeneratorFunction");

    internal GeneratorFunctionPrototype(
        Engine engine,
        GeneratorFunctionConstructor constructor,
        FunctionPrototype prototype,
        IteratorPrototype iteratorPrototype) : base(engine, engine.Realm)
    {
        _constructor = constructor;
        _prototype = prototype;
        _prototypeObject = new GeneratorPrototype(engine, engine.Realm, this, iteratorPrototype);
    }

    public GeneratorPrototype PrototypeObject => _prototypeObject;

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
