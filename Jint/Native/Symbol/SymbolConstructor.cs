#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Symbol;

/// <summary>
/// 19.4
/// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-objects
/// </summary>
[JsObject]
internal sealed partial class SymbolConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Symbol");

    // Well-known symbol aliases. Each forwards a JsSymbol from GlobalSymbolRegistry under the
    // Symbol.X spec name, with PropertyFlag.AllForbidden (non-writable, non-enumerable, non-configurable).
    [JsProperty(Name = "hasInstance")] private static readonly JsSymbol _hasInstance = GlobalSymbolRegistry.HasInstance;
    [JsProperty(Name = "isConcatSpreadable")] private static readonly JsSymbol _isConcatSpreadable = GlobalSymbolRegistry.IsConcatSpreadable;
    [JsProperty(Name = "iterator")] private static readonly JsSymbol _iterator = GlobalSymbolRegistry.Iterator;
    [JsProperty(Name = "match")] private static readonly JsSymbol _match = GlobalSymbolRegistry.Match;
    [JsProperty(Name = "matchAll")] private static readonly JsSymbol _matchAll = GlobalSymbolRegistry.MatchAll;
    [JsProperty(Name = "replace")] private static readonly JsSymbol _replace = GlobalSymbolRegistry.Replace;
    [JsProperty(Name = "search")] private static readonly JsSymbol _search = GlobalSymbolRegistry.Search;
    [JsProperty(Name = "species")] private static readonly JsSymbol _species = GlobalSymbolRegistry.Species;
    [JsProperty(Name = "split")] private static readonly JsSymbol _split = GlobalSymbolRegistry.Split;
    [JsProperty(Name = "toPrimitive")] private static readonly JsSymbol _toPrimitive = GlobalSymbolRegistry.ToPrimitive;
    [JsProperty(Name = "toStringTag")] private static readonly JsSymbol _toStringTag = GlobalSymbolRegistry.ToStringTag;
    [JsProperty(Name = "unscopables")] private static readonly JsSymbol _unscopables = GlobalSymbolRegistry.Unscopables;
    [JsProperty(Name = "asyncIterator")] private static readonly JsSymbol _asyncIterator = GlobalSymbolRegistry.AsyncIterator;
    [JsProperty(Name = "dispose")] private static readonly JsSymbol _dispose = GlobalSymbolRegistry.Dispose;
    [JsProperty(Name = "asyncDispose")] private static readonly JsSymbol _asyncDispose = GlobalSymbolRegistry.AsyncDispose;

    internal SymbolConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new SymbolPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public SymbolPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-description
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var description = arguments.At(0);
        var descString = description.IsUndefined()
            ? Undefined
            : TypeConverter.ToJsString(description);

        var value = GlobalSymbolRegistry.CreateSymbol(descString);
        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.for
    /// </summary>
    [JsFunction(Length = 1, Name = "for")]
    private JsValue For(JsValue thisObject, JsValue key)
    {
        var stringKey = TypeConverter.ToJsString(key);

        // 2. ReturnIfAbrupt(stringKey).

        if (!_engine.GlobalSymbolRegistry.TryGetSymbol(stringKey, out var symbol))
        {
            symbol = GlobalSymbolRegistry.CreateSymbol(stringKey);
            _engine.GlobalSymbolRegistry.Add(symbol);
        }

        return symbol;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.keyfor
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue KeyFor(JsValue thisObject, JsValue sym)
    {
        var symbol = sym as JsSymbol;
        if (symbol is null)
        {
            Throw.TypeError(_realm, $"{sym} is not a symbol");
        }

        if (_engine.GlobalSymbolRegistry.TryGetSymbol(symbol._value, out var e))
        {
            return e._value;
        }

        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Symbol is not a constructor");
        return null;
    }

    public SymbolInstance Construct(JsSymbol symbol)
    {
        return new SymbolInstance(Engine, PrototypeObject, symbol);
    }
}
