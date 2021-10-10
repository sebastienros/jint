using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-generatorfunction-prototype-object
/// </summary>
internal sealed class GeneratorFunctionPrototype : ObjectInstance
{
    private readonly GeneratorFunctionConstructor? _constructor;

    internal GeneratorFunctionPrototype(
        Engine engine,
        GeneratorFunctionConstructor constructor,
        FunctionPrototype prototype,
        IteratorPrototype iteratorPrototype) : base(engine)
    {
        _constructor = constructor;
        _prototype = prototype;
        PrototypeObject = new GeneratorPrototype(engine, this, iteratorPrototype);
    }

    public GeneratorPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.Configurable),
            ["prototype"] = new PropertyDescriptor(PrototypeObject, PropertyFlag.Configurable)
        };
        SetProperties(properties);
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("GeneratorFunction", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }
}
