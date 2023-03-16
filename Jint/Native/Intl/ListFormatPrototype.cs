using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-listformat-prototype-object
/// </summary>
internal sealed class ListFormatPrototype : Prototype
{
    private readonly ListFormatConstructor _constructor;

    public ListFormatPrototype(Engine engine,
        Realm realm,
        ListFormatConstructor constructor,
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
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.ListFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }
}
