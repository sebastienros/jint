using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-asyncgeneratorfunction-prototype
/// </summary>
internal sealed class AsyncGeneratorFunctionPrototype : Prototype
{
    private readonly AsyncGeneratorFunctionConstructor? _constructor;

    internal AsyncGeneratorFunctionPrototype(
        Engine engine,
        AsyncGeneratorFunctionConstructor constructor,
        FunctionPrototype prototype,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine, engine.Realm)
    {
        _constructor = constructor;
        _prototype = prototype;
        PrototypeObject = new AsyncGeneratorPrototype(engine, this, asyncIteratorPrototype);
    }

    public AsyncGeneratorPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            [KnownKeys.Constructor] = new PropertyDescriptor(_constructor, PropertyFlag.Configurable),
            [KnownKeys.Prototype] = new PropertyDescriptor(PrototypeObject, PropertyFlag.Configurable)
        };
        SetProperties(properties);
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("AsyncGeneratorFunction", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }
}
