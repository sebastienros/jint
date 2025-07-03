using System.Diagnostics.CodeAnalysis;

namespace Jint.Native.Symbol;

public sealed class GlobalSymbolRegistry
{
    public static readonly JsSymbol AsyncDispose = new("Symbol.asyncDispose");
    public static readonly JsSymbol AsyncIterator = new("Symbol.asyncIterator");
    public static readonly JsSymbol Dispose = new("Symbol.dispose");
    public static readonly JsSymbol HasInstance = new("Symbol.hasInstance");
    public static readonly JsSymbol IsConcatSpreadable = new("Symbol.isConcatSpreadable");
    public static readonly JsSymbol Iterator = new("Symbol.iterator");
    public static readonly JsSymbol Match = new("Symbol.match");
    public static readonly JsSymbol MatchAll = new("Symbol.matchAll");
    public static readonly JsSymbol Replace = new("Symbol.replace");
    public static readonly JsSymbol Search = new("Symbol.search");
    public static readonly JsSymbol Species = new("Symbol.species");
    public static readonly JsSymbol Split = new("Symbol.split");
    public static readonly JsSymbol ToPrimitive = new("Symbol.toPrimitive");
    public static readonly JsSymbol ToStringTag = new("Symbol.toStringTag");
    public static readonly JsSymbol Unscopables = new("Symbol.unscopables");

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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-keyforsymbol
    /// </summary>
    internal JsValue KeyForSymbol(JsValue value)
    {
        if (value is JsSymbol symbol && _customSymbolLookup?.TryGetValue(symbol._value, out var s) == true)
        {
            return s._value;
        }

        return JsValue.Undefined;
    }
}
