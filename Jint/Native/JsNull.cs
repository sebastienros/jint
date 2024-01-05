using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsNull : JsValue, IEquatable<JsNull>
{
    internal JsNull() : base(Types.Null)
    {
    }

    public override object? ToObject() => null;

    public override string ToString() => "null";

    protected internal override bool IsLooselyEqual(JsValue value)
    {
        return ReferenceEquals(Null, value) || ReferenceEquals(Undefined, value);
    }

    public override bool Equals(object? obj) => Equals(obj as JsNull);

    public override bool Equals(JsValue? other) => Equals(other as JsNull);

    public bool Equals(JsNull? other) => other is not null;

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
