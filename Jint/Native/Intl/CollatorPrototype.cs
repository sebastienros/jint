using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-the-intl-collator-prototype-object
/// </summary>
internal sealed class CollatorPrototype : Prototype
{
    private readonly CollatorConstructor _constructor;

    public CollatorPrototype(Engine engine,
        Realm realm,
        CollatorConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.Collator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }
}
