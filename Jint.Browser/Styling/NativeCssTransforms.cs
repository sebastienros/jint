using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private CssPropertyValue ComputeTransformList(CssTransformList list, NativeCssMetrics metrics)
    {
        var functions = new CssTransformFunction[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            _work.Charge(1);
            var function = list[i];
            var arguments = new CssPropertyValue[function.Arguments.Count];
            for (var j = 0; j < arguments.Length; j++)
            {
                _work.Charge(1);
                arguments[j] = Compute("transform", function.Arguments[j], metrics);
                if (function.Descriptor.Kind == CssTransformFunctionKind.Perspective &&
                    arguments[j].Kind == CssPropertyValueKind.Numeric && arguments[j].Numeric.Number.Sign < 0)
                    arguments[j] = CssTransformParser.Constant(0, CssUnit.Px, arguments[j].Span, _work);
            }
            functions[i] = new(function.Descriptor, arguments, function.Span);
        }
        var computed = new CssTransformList(functions, list.Span, _work);
        return CssPropertyValue.TransformListValue(computed, CssTransformListSerializer.Serialize(computed, _work));
    }

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
