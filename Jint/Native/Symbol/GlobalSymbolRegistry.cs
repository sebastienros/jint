using System.Diagnostics.CodeAnalysis;

namespace Jint.Native.Symbol;

public sealed class GlobalSymbolRegistry
{
    /// <summary>
    /// The registry an <see cref="Engine"/> builds for itself. The well-known symbols below are
    /// <see langword="static"/> and readable without one; the instance half — the symbols
    /// <c>Symbol.for</c> created — belongs to one engine, and <see cref="Engine.GlobalSymbolRegistry"/>
    /// is <see langword="internal"/>, so an instance a host constructed could never be installed.
    /// </summary>
    internal GlobalSymbolRegistry()
    {
    }

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
        symbol._registered = true;
    }

    internal static JsSymbol CreateSymbol(JsValue description)
    {
        return new JsSymbol(description);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-keyforsymbol
    /// Answers the [[Key]] of the registry record whose [[Symbol]] <em>is</em> the argument, so a
    /// symbol that merely shares a description with a registered one -- <c>Symbol("x")</c> after
    /// <c>Symbol.for("x")</c> -- is not a registered symbol and answers undefined.
    /// </summary>
    internal static JsValue KeyForSymbol(JsValue value)
    {
        if (value is JsSymbol { _registered: true } symbol)
        {
            // Symbol.for creates the symbol with the key as its description, so the two coincide.
            return symbol._value;
        }

        return JsValue.Undefined;
    }
}
