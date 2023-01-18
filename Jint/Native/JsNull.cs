using Jint.Runtime;

namespace Jint.Native;

public sealed class JsNull : JsValue, IEquatable<JsNull>
{
    internal JsNull() : base(Types.Null)
    {
    }

    public override object ToObject() => null!;

    public override string ToString() => "null";

    public override bool IsLooselyEqual(JsValue value)
    {
        return ReferenceEquals(Null, value) || ReferenceEquals(Undefined, value);
    }

    public override bool Equals(JsValue? obj)
    {
        return Equals(obj as JsNull);
    }

    public bool Equals(JsNull? other)
    {
        return other is not null;
    }
}
