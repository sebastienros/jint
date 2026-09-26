using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Transforms;

internal enum CssTransformKind { Translate, Rotate, Scale }

// CSS Transforms 2 §5: owned, fixed components, including every default axis.
// None is a separate property keyword and never an identity transform.
internal sealed class CssTransformValue
{
    private readonly CssPropertyValue? _angle;

    internal CssTransformValue(CssTransformKind kind, CssPropertyValue x, CssPropertyValue y,
        CssPropertyValue z, CssSourceSpan span, CssPropertyValue? angle = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Validate(x); Validate(y); Validate(z);
        if (kind == CssTransformKind.Rotate) Validate(angle ?? throw new ArgumentNullException(nameof(angle)));
        else if (angle is not null) throw new ArgumentException("Only rotation has an angle.", nameof(angle));
        Kind = kind; X = x; Y = y; Z = z; Span = span; _angle = angle;
    }

    internal CssTransformKind Kind { get; }
    internal CssPropertyValue X { get; }
    internal CssPropertyValue Y { get; }
    internal CssPropertyValue Z { get; }
    internal CssSourceSpan Span { get; }
    internal CssPropertyValue Angle => Kind == CssTransformKind.Rotate ? _angle! : throw new InvalidOperationException();

    private static void Validate(CssPropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind is not (CssPropertyValueKind.Numeric or CssPropertyValueKind.Math))
            throw new ArgumentException("A transform component must be a validated numeric value.", nameof(value));
    }
}
