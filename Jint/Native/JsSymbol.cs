using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsSymbol : JsValue, IEquatable<JsSymbol>
{
    internal readonly JsValue _value;

    /// <summary>
    /// True once this symbol has been appended to the GlobalSymbolRegistry by Symbol.for
    /// (https://tc39.es/ecma262/#sec-symbol.for). The registry record's [[Key]] is the description
    /// the symbol was created with, so <see cref="_value"/> <em>is</em> that key; what a description
    /// on its own cannot say is whether <em>this</em> symbol is the one the registry holds under it.
    /// That is exactly what KeyForSymbol (https://tc39.es/ecma262/#sec-keyforsymbol) and
    /// CanBeHeldWeakly (https://tc39.es/ecma262/#sec-canbeheldweakly) ask, and a symbol from
    /// <c>Symbol("x")</c> must answer no even when <c>Symbol.for("x")</c> has also run.
    /// </summary>
    internal bool _registered;

    internal JsSymbol(string value) : this(new JsString(value))
    {
    }

    internal JsSymbol(JsValue value) : base(Types.Symbol)
    {
        _value = value;
    }

    public override object ToObject() => _value;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symboldescriptivestring
    /// </summary>
    public override string ToString()
    {
        var value = _value.IsUndefined() ? "" : _value.AsString();
        return "Symbol(" + value + ")";
    }

    public override bool Equals(object? obj) => Equals(obj as JsSymbol);

    public override bool Equals(JsValue? other) => Equals(other as JsSymbol);

    public bool Equals(JsSymbol? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
