using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    // CSS Transforms 2 §§5–5.1. This computes individual values only; layout does not apply them.
    private CssPropertyValue ComputeTransform(string name, CssTransformValue value,
        NativeCssMetrics metrics, double? percentageBasis)
    {
        var x = Compute(name, value.X, metrics, percentageBasis);
        var y = Compute(name, value.Y, metrics, percentageBasis);
        var z = Compute(name, value.Z, metrics, percentageBasis);
        var computed = new CssTransformValue(value.Kind, x, y, z, value.Span,
            value.Kind == CssTransformKind.Rotate ? Compute(name, value.Angle, metrics, percentageBasis) : null);
        return CssPropertyValue.TransformValue(computed, CssTransformSerializer.Serialize(computed, _work));
    }
}
