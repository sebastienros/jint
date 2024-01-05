using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.String;

/// <summary>
/// https://tc39.es/ecma262/#sec-%stringiteratorprototype%-object
/// </summary>
internal sealed class StringIteratorPrototype : IteratorPrototype
{
    internal StringIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new(new ClrFunction(Engine, "next", Next, 0, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("String Iterator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    public ObjectInstance Construct(string str)
    {
        var instance = new IteratorInstance.StringIterator(Engine, str)
        {
            _prototype = this
        };

        return instance;
    }
}
