#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Symbol;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-symbol-prototype-object
/// </summary>
[JsObject]
internal sealed partial class SymbolPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly SymbolConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString SymbolToStringTag = new("Symbol");

    internal SymbolPrototype(
        Engine engine,
        Realm realm,
        SymbolConstructor symbolConstructor,
        ObjectPrototype objectPrototype)
        : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = symbolConstructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.prototype.description
    /// </summary>
    [JsAccessor("description")]
    private JsValue Description(JsValue thisObject)
    {
        var sym = ThisSymbolValue(thisObject);
        return sym._value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.prototype.tostring
    /// </summary>
    [JsFunction(Length = 0, Name = "toString")]
    private JsValue ToSymbolString(JsValue thisObject)
    {
        var sym = ThisSymbolValue(thisObject);
        return new JsString(sym.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.prototype.valueof
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue ValueOf(JsValue thisObject)
    {
        return ThisSymbolValue(thisObject);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.prototype-@@toprimitive
    /// </summary>
    [JsSymbolFunction("ToPrimitive", Length = 1, Flags = PropertyFlag.Configurable)]
    private JsValue ToPrimitive(JsValue thisObject)
    {
        return ThisSymbolValue(thisObject);
    }

    private JsSymbol ThisSymbolValue(JsValue thisObject)
    {
        if (thisObject is JsSymbol s)
        {
            return s;
        }

        if (thisObject is SymbolInstance instance)
        {
            return instance.SymbolData;
        }

        Throw.TypeError(_realm, "Symbol.prototype.valueOf requires that 'this' be a Symbol");
        return null;
    }
}
