using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Transforms;

internal enum CssTransformFunctionKind
{
    Matrix, Matrix3d, Translate, TranslateX, TranslateY, TranslateZ, Translate3d,
    Scale, ScaleX, ScaleY, ScaleZ, Scale3d, Rotate, RotateX, RotateY, RotateZ, Rotate3d,
    Skew, SkewX, SkewY, Perspective
}

// CSS Transforms 2 §12: function identity and authored arity survive computation.
internal readonly record struct CssTransformFunctionDescriptor(CssTransformFunctionKind Kind, string Name,
    int MinimumArguments, int MaximumArguments)
{
    internal static CssTransformFunctionDescriptor? Find(string name) => CssTransformFunctionLookup.Match(name);

    internal CssMathProduction Production(int argument) => Kind switch
    {
        CssTransformFunctionKind.Translate or CssTransformFunctionKind.TranslateX or CssTransformFunctionKind.TranslateY => CssMathProduction.LengthPercentage,
        CssTransformFunctionKind.Translate3d when argument < 2 => CssMathProduction.LengthPercentage,
        CssTransformFunctionKind.TranslateZ or CssTransformFunctionKind.Translate3d or CssTransformFunctionKind.Perspective => CssMathProduction.Length,
        CssTransformFunctionKind.Scale or CssTransformFunctionKind.ScaleX or CssTransformFunctionKind.ScaleY or CssTransformFunctionKind.ScaleZ or CssTransformFunctionKind.Scale3d => CssMathProduction.NumberOrPercentage,
        CssTransformFunctionKind.Rotate3d when argument < 3 => CssMathProduction.Number,
        CssTransformFunctionKind.Matrix or CssTransformFunctionKind.Matrix3d => CssMathProduction.Number,
        _ => CssMathProduction.Angle
    };

    internal int TranslationAxis(int argument) => Kind switch
    {
        CssTransformFunctionKind.Translate or CssTransformFunctionKind.Translate3d when argument < 2 => argument,
        CssTransformFunctionKind.TranslateX => 0,
        CssTransformFunctionKind.TranslateY => 1,
        _ => -1
    };
}

internal sealed class CssTransformFunction
{
    internal CssTransformFunction(CssTransformFunctionDescriptor descriptor, CssPropertyValue[] arguments, CssSourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Enum.IsDefined(descriptor.Kind) || descriptor.MinimumArguments < 1 || descriptor.MaximumArguments > 16 ||
            arguments.Length < descriptor.MinimumArguments || arguments.Length > descriptor.MaximumArguments)
            throw new ArgumentException("Wrong transform arity.", nameof(arguments));
        foreach (var argument in arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
            if (argument.Kind is not (CssPropertyValueKind.Numeric or CssPropertyValueKind.Math) &&
                !(descriptor.Kind == CssTransformFunctionKind.Perspective && argument.Kind == CssPropertyValueKind.Keyword && argument.Text == "none"))
                throw new ArgumentException("A transform argument must be a typed numeric value or perspective none.", nameof(arguments));
        }
        Descriptor = descriptor;
        Arguments = Array.AsReadOnly((CssPropertyValue[]) arguments.Clone());
        Span = span;
    }

    internal CssTransformFunctionDescriptor Descriptor { get; }
    internal IReadOnlyList<CssPropertyValue> Arguments { get; }
    internal CssSourceSpan Span { get; }
}

internal sealed class CssTransformList
{
    private readonly CssTransformFunction[] _functions;
    internal CssTransformList(IReadOnlyList<CssTransformFunction> functions, CssSourceSpan span, CssValueWork work)
    {
        if (functions.Count == 0) throw new ArgumentException("None is a separate keyword.", nameof(functions));
        work.CheckCancellation();
        _functions = new CssTransformFunction[functions.Count];
        for (var i = 0; i < functions.Count; i++)
        {
            work.Charge(1);
            _functions[i] = functions[i];
        }
        Span = span;
        work.CheckCancellation();
    }

    internal int Count => _functions.Length;
    internal CssTransformFunction this[int index] => _functions[index];
    internal CssSourceSpan Span { get; }
}
