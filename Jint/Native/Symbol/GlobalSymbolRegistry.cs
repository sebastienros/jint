using System.Diagnostics.CodeAnalysis;

namespace Jint.Native.Symbol;

public sealed class GlobalSymbolRegistry
{
    public static readonly JsSymbol AsyncIterator = new JsSymbol("Symbol.asyncIterator");
    public static readonly JsSymbol HasInstance = new JsSymbol("Symbol.hasInstance");
    public static readonly JsSymbol IsConcatSpreadable = new JsSymbol("Symbol.isConcatSpreadable");
    public static readonly JsSymbol Iterator = new JsSymbol("Symbol.iterator");
    public static readonly JsSymbol Match = new JsSymbol("Symbol.match");
    public static readonly JsSymbol MatchAll = new JsSymbol("Symbol.matchAll");
    public static readonly JsSymbol Replace = new JsSymbol("Symbol.replace");
    public static readonly JsSymbol Search = new JsSymbol("Symbol.search");
    public static readonly JsSymbol Species = new JsSymbol("Symbol.species");
    public static readonly JsSymbol Split = new JsSymbol("Symbol.split");
    public static readonly JsSymbol ToPrimitive = new JsSymbol("Symbol.toPrimitive");
    public static readonly JsSymbol ToStringTag = new JsSymbol("Symbol.toStringTag");
    public static readonly JsSymbol Unscopables = new JsSymbol("Symbol.unscopables");

    // engine-specific created by scripts
    private Dictionary<JsValue, JsSymbol>? _customSymbolLookup;

    internal bool TryGetSymbol(JsValue key, [NotNullWhen(true)] out JsSymbol? symbol)
    {
        symbol = null;
        return _customSymbolLookup != null
               && _customSymbolLookup.TryGetValue(key, out symbol);
    }

    internal void Add(JsSymbol symbol)
    {
        _customSymbolLookup ??= new Dictionary<JsValue, JsSymbol>();
        _customSymbolLookup[symbol._value] = symbol;
    }

    internal static JsSymbol CreateSymbol(JsValue description)
    {
        return new JsSymbol(description);
    }

    internal bool ContainsCustom(JsValue value)
    {
        return value is JsSymbol symbol && _customSymbolLookup?.ContainsKey(symbol._value) == true;
    }
}
