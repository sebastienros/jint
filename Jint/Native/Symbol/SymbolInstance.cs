using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Symbol;

internal sealed class SymbolInstance : ObjectInstance, IJsPrimitive
{
    internal SymbolInstance(
        Engine engine,
        SymbolPrototype prototype,
        JsSymbol symbol) : base(engine)
    {
        _prototype = prototype;
        SymbolData = symbol;
    }

    Types IJsPrimitive.Type => Types.Symbol;

    JsValue IJsPrimitive.PrimitiveValue => SymbolData;

    public JsSymbol SymbolData { get; }
}
