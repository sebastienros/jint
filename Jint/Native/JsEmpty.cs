using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Special null object pattern for spec's EMPTY.
/// </summary>
internal sealed class JsEmpty : JsValue
{
    internal static readonly JsValue Instance = new JsEmpty();

    private JsEmpty() : base(Types.Empty)
    {
    }

    public override object? ToObject() => null;
}
