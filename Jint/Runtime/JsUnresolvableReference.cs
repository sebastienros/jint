using Jint.Native;

namespace Jint.Runtime;

/// <summary>
/// Marker value used as the base of a Reference when the binding could not be resolved.
/// Distinct from <see cref="JsUndefined"/> to correctly implement ECMAScript semantics
/// for IsUnresolvableReference and IsPropertyReference.
/// </summary>
internal sealed class JsUnresolvableReference : JsValue
{
    internal static readonly JsUnresolvableReference Instance = new();

    private JsUnresolvableReference() : base(InternalTypes.Unresolvable)
    {
    }

    public override object ToObject() => null!;

    public override string ToString() => "unresolvable";
}
