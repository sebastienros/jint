using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-displaynames-prototype-object
/// </summary>
internal sealed class DisplayNamesPrototype : Prototype
{
    private readonly DisplayNamesConstructor _constructor;

    public DisplayNamesPrototype(Engine engine,
        Realm realm,
        DisplayNamesConstructor constructor,
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
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.DisplayNames", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }
}
