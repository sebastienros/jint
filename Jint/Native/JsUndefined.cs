using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsUndefined : JsValue, IEquatable<JsUndefined>
{
    internal JsUndefined() : base(Types.Undefined)
    {
    }

    public override object? ToObject() => null;

    public override string ToString() => "undefined";

    protected internal override bool IsLooselyEqual(JsValue value)
    {
        return ReferenceEquals(Undefined, value) || ReferenceEquals(Null, value);
    }

    public override bool Equals(object? obj) => Equals(obj as JsUndefined);

    public override bool Equals(JsValue? other) => Equals(other as JsUndefined);

    public bool Equals(JsUndefined? other) => !ReferenceEquals(null, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
