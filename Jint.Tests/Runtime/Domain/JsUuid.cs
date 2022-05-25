using System;
using Jint.Native;

namespace Jint.Tests.Runtime.Domain
{
public sealed class JsUuid : IEquatable<JsUuid>
{
    internal readonly Guid _value;

    public static readonly JsUuid Empty = new JsUuid(Guid.Empty);

    public static JsUuid Parse(JsValue value)
    {
        return value switch
        {
            { } js when Guid.TryParse(js.AsString(), out var res) => new JsUuid(res),
            _ => null
        };
    }

    public JsUuid() : this(Guid.Empty)
    {
    }

    public JsUuid(Guid value) => _value = value;

    public static implicit operator JsUuid(Guid g) => new JsUuid(g);

    public static implicit operator Guid(JsUuid jsUuid) => jsUuid._value;


    public bool Equals(JsUuid other) => other?._value == _value;

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    public override bool Equals(object obj) => Equals(obj as JsUuid);
}
}
