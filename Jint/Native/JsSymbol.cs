using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsSymbol : JsValue, IEquatable<JsSymbol>
{
    internal readonly JsValue _value;

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
