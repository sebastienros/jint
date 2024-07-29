using Jint.Native;

namespace Jint.Tests.Runtime.Domain;

public sealed class JsUuid : JsValue, IEquatable<JsUuid>
{
    internal readonly Guid _value;
    public static readonly JsUuid Empty = new JsUuid(Guid.Empty);

    public JsUuid(Guid value) : base(Jint.Runtime.Types.String) => _value = value;

    public static implicit operator JsUuid(Guid g) => new JsUuid(g);

    public override bool Equals(JsValue other) => Equals(other as JsUuid);

    public bool Equals(JsUuid other) => other?._value == _value;

    public override int GetHashCode() => _value.GetHashCode();

    public override object ToObject() => _value;

    public override string ToString() => _value.ToString();

    public override bool Equals(object obj) => Equals(obj as JsUuid);
}