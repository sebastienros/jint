using System.Globalization;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values;

/// <summary>The transform functions of CSS Transforms 1 §13 and CSS Transforms 2 §12.</summary>
internal enum CssTransformFunctionKind : byte
{
    Matrix,
    Matrix3d,
    Translate,
    Translate3d,
    TranslateX,
    TranslateY,
    TranslateZ,
    Scale,
    Scale3d,
    ScaleX,
    ScaleY,
    ScaleZ,
    Rotate,
    Rotate3d,
    RotateX,
    RotateY,
    RotateZ,
    Skew,
    SkewX,
    SkewY,
    Perspective,
}

/// <summary>
/// One parsed <c>&lt;transform-function&gt;</c>, its arguments normalized: lengths in CSS pixels, angles in
/// degrees, percentages of <c>scale*</c> as numbers, omitted optional arguments filled in. A
/// <c>perspective(none)</c> carries positive infinity.
/// </summary>
internal readonly struct CssTransformFunction
{
    internal CssTransformFunction(CssTransformFunctionKind kind, double[] arguments)
    {
        Kind = kind;
        Arguments = arguments;
    }

    internal CssTransformFunctionKind Kind { get; }

    internal double[] Arguments { get; }

    /// <summary>https://drafts.csswg.org/css-transforms-2/#twod-transform-functions.</summary>
    internal bool Is2D => Kind is CssTransformFunctionKind.Matrix or CssTransformFunctionKind.Translate
        or CssTransformFunctionKind.TranslateX or CssTransformFunctionKind.TranslateY or CssTransformFunctionKind.Scale
        or CssTransformFunctionKind.ScaleX or CssTransformFunctionKind.ScaleY or CssTransformFunctionKind.Rotate
        or CssTransformFunctionKind.Skew or CssTransformFunctionKind.SkewX or CssTransformFunctionKind.SkewY;
}

/// <summary>
/// Reads the grammar of the CSS <c>transform</c> property, <c>none | &lt;transform-list&gt;</c>, on demand.
/// </summary>
/// <remarks>
/// <para>
/// https://drafts.csswg.org/css-transforms-1/#transform-property and
/// https://drafts.csswg.org/css-transforms-2/#transform-functions. The only consumer is Geometry's
/// <c>DOMMatrix</c> string initializer (https://drafts.fxtf.org/geometry/#create-a-dommatrix-from-the-2d-dictionary),
/// which refuses any length not in an absolute unit — resolving one needs a box, and there is none — so this
/// reader refuses them too, together with percentages in a translation and math functions. Unitless zero is
/// accepted wherever a length or angle is, as the grammar's legacy <c>&lt;zero&gt;</c> allowance says.
/// </para>
/// <para>
/// Nothing in parsing calls it: a style sheet's <c>transform</c> declaration stays text, per the
/// <a href="../../README.md#renderless-css-boundary">renderless boundary</a>.
/// </para>
/// </remarks>
internal static class CssTransformList
{
    /// <summary>
    /// Parses <paramref name="source"/>. <see langword="false"/> is failure; <c>none</c> succeeds with no
    /// functions.
    /// </summary>
    internal static bool TryParse(string source, CssValueWork work, out CssTransformFunction[] functions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(work);
        functions = [];
        work.CheckCancellation();
        CssComponentValueList values;
        using (var parser = new CssSyntaxParser(source, options: null, work.Token, work.CheckCancellation))
        {
            values = parser.ParseComponentValues();
        }

        var list = new List<CssTransformFunction>();
        var sawNone = false;
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind == CssComponentKind.Token)
            {
                var token = value.Token;
                if (token.Kind == CssTokenKind.Whitespace)
                {
                    continue;
                }

                if (token.Kind == CssTokenKind.Ident && list.Count == 0 && !sawNone && IsAscii(token.Text, "none"))
                {
                    sawNone = true;
                    continue;
                }

                return false;
            }

            if (sawNone || value.Kind != CssComponentKind.Function || !TryFunction(value, work, out var function))
            {
                return false;
            }

            list.Add(function);
        }

        if (!sawNone && list.Count == 0)
        {
            return false;
        }

        functions = [.. list];
        return true;
    }

    private static bool TryFunction(CssComponentValue value, CssValueWork work, out CssTransformFunction function)
    {
        function = default;
        if (!TryKind(value.FunctionName, out var kind) || !TrySplit(value.Values, work, out var arguments))
        {
            return false;
        }

        double[]? result = kind switch
        {
            CssTransformFunctionKind.Matrix => Numbers(arguments, 6),
            CssTransformFunctionKind.Matrix3d => Numbers(arguments, 16),
            CssTransformFunctionKind.Translate => arguments.Length is 1 or 2
                ? Collect(Length(arguments[0]), arguments.Length == 2 ? Length(arguments[1]) : 0)
                : null,
            CssTransformFunctionKind.Translate3d => arguments.Length == 3
                ? Collect(Length(arguments[0]), Length(arguments[1]), Length(arguments[2]))
                : null,
            CssTransformFunctionKind.TranslateX or CssTransformFunctionKind.TranslateY or CssTransformFunctionKind.TranslateZ
                => arguments.Length == 1 ? Collect(Length(arguments[0])) : null,
            CssTransformFunctionKind.Scale => arguments.Length is 1 or 2
                ? Collect(Scale(arguments[0]), Scale(arguments[arguments.Length - 1]))
                : null,
            CssTransformFunctionKind.Scale3d => arguments.Length == 3
                ? Collect(Scale(arguments[0]), Scale(arguments[1]), Scale(arguments[2]))
                : null,
            CssTransformFunctionKind.ScaleX or CssTransformFunctionKind.ScaleY or CssTransformFunctionKind.ScaleZ
                => arguments.Length == 1 ? Collect(Scale(arguments[0])) : null,
            CssTransformFunctionKind.Rotate or CssTransformFunctionKind.RotateX or CssTransformFunctionKind.RotateY
                or CssTransformFunctionKind.RotateZ or CssTransformFunctionKind.SkewX or CssTransformFunctionKind.SkewY
                => arguments.Length == 1 ? Collect(Angle(arguments[0])) : null,
            CssTransformFunctionKind.Rotate3d => arguments.Length == 4
                ? Collect(Number(arguments[0]), Number(arguments[1]), Number(arguments[2]), Angle(arguments[3]))
                : null,
            CssTransformFunctionKind.Skew => arguments.Length is 1 or 2
                ? Collect(Angle(arguments[0]), arguments.Length == 2 ? Angle(arguments[1]) : 0)
                : null,
            CssTransformFunctionKind.Perspective => arguments.Length == 1 ? Collect(Perspective(arguments[0])) : null,
            _ => null,
        };

        if (result is null)
        {
            return false;
        }

        function = new CssTransformFunction(kind, result);
        return true;
    }

    /// <summary>Splits a function's arguments on commas; each argument is exactly one token.</summary>
    private static bool TrySplit(CssComponentValueList values, CssValueWork work, out CssToken[] arguments)
    {
        arguments = [];
        var list = new List<CssToken>();
        var expectValue = true;
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind != CssComponentKind.Token)
            {
                return false;
            }

            var token = value.Token;
            switch (token.Kind)
            {
                case CssTokenKind.Whitespace:
                    continue;
                case CssTokenKind.Comma:
                    if (expectValue)
                    {
                        return false;
                    }

                    expectValue = true;
                    continue;
                default:
                    if (!expectValue)
                    {
                        return false;
                    }

                    list.Add(token);
                    expectValue = false;
                    continue;
            }
        }

        if (expectValue)
        {
            return false;
        }

        arguments = [.. list];
        return true;
    }

    private static double[]? Collect(params double[] values)
    {
        foreach (var value in values)
        {
            if (double.IsNaN(value))
            {
                return null;
            }
        }

        return values;
    }

    private static double[]? Numbers(CssToken[] arguments, int count)
    {
        if (arguments.Length != count)
        {
            return null;
        }

        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = Number(arguments[i]);
        }

        return Collect(values);
    }

    private static double Number(CssToken token)
        => token.Kind == CssTokenKind.Number ? Value(token) : double.NaN;

    private static double Scale(CssToken token) => token.Kind switch
    {
        CssTokenKind.Number => Value(token),
        CssTokenKind.Percentage => Value(token) / 100,
        _ => double.NaN,
    };

    /// <summary>An absolute <c>&lt;length&gt;</c> in CSS pixels (https://drafts.csswg.org/css-values-4/#absolute-lengths).</summary>
    private static double Length(CssToken token)
    {
        if (token.Kind == CssTokenKind.Number)
        {
            return Value(token) == 0 ? 0 : double.NaN;
        }

        if (token.Kind != CssTokenKind.Dimension)
        {
            return double.NaN;
        }

        var unit = token.Unit;
        var factor = IsAscii(unit, "px") ? 1
            : IsAscii(unit, "cm") ? 96 / 2.54
            : IsAscii(unit, "mm") ? 96 / 25.4
            : IsAscii(unit, "q") ? 96 / 101.6
            : IsAscii(unit, "in") ? 96
            : IsAscii(unit, "pt") ? 96.0 / 72
            : IsAscii(unit, "pc") ? 16
            : double.NaN;
        return Value(token) * factor;
    }

    /// <summary>An <c>&lt;angle&gt;</c> in degrees (https://drafts.csswg.org/css-values-4/#angles).</summary>
    private static double Angle(CssToken token)
    {
        if (token.Kind == CssTokenKind.Number)
        {
            return Value(token) == 0 ? 0 : double.NaN;
        }

        if (token.Kind != CssTokenKind.Dimension)
        {
            return double.NaN;
        }

        var unit = token.Unit;
        var value = Value(token);
        return IsAscii(unit, "deg") ? value
            : IsAscii(unit, "grad") ? value * 0.9
            : IsAscii(unit, "rad") ? value * (180 / Math.PI)
            : IsAscii(unit, "turn") ? value * 360
            : double.NaN;
    }

    /// <summary>https://drafts.csswg.org/css-transforms-2/#funcdef-perspective: a non-negative length or <c>none</c>.</summary>
    private static double Perspective(CssToken token)
    {
        if (token.Kind == CssTokenKind.Ident)
        {
            return IsAscii(token.Text, "none") ? double.PositiveInfinity : double.NaN;
        }

        var length = Length(token);
        return length >= 0 ? length : double.NaN;
    }

    private static double Value(CssToken token)
        => double.Parse(token.NumberText, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool TryKind(string name, out CssTransformFunctionKind kind)
    {
        kind = name.Length switch
        {
            4 when IsAscii(name, "skew") => CssTransformFunctionKind.Skew,
            5 when IsAscii(name, "scale") => CssTransformFunctionKind.Scale,
            5 when IsAscii(name, "skewx") => CssTransformFunctionKind.SkewX,
            5 when IsAscii(name, "skewy") => CssTransformFunctionKind.SkewY,
            6 when IsAscii(name, "matrix") => CssTransformFunctionKind.Matrix,
            6 when IsAscii(name, "rotate") => CssTransformFunctionKind.Rotate,
            6 when IsAscii(name, "scalex") => CssTransformFunctionKind.ScaleX,
            6 when IsAscii(name, "scaley") => CssTransformFunctionKind.ScaleY,
            6 when IsAscii(name, "scalez") => CssTransformFunctionKind.ScaleZ,
            7 when IsAscii(name, "rotatex") => CssTransformFunctionKind.RotateX,
            7 when IsAscii(name, "rotatey") => CssTransformFunctionKind.RotateY,
            7 when IsAscii(name, "rotatez") => CssTransformFunctionKind.RotateZ,
            7 when IsAscii(name, "scale3d") => CssTransformFunctionKind.Scale3d,
            8 when IsAscii(name, "matrix3d") => CssTransformFunctionKind.Matrix3d,
            8 when IsAscii(name, "rotate3d") => CssTransformFunctionKind.Rotate3d,
            9 when IsAscii(name, "translate") => CssTransformFunctionKind.Translate,
            10 when IsAscii(name, "translatex") => CssTransformFunctionKind.TranslateX,
            10 when IsAscii(name, "translatey") => CssTransformFunctionKind.TranslateY,
            10 when IsAscii(name, "translatez") => CssTransformFunctionKind.TranslateZ,
            11 when IsAscii(name, "translate3d") => CssTransformFunctionKind.Translate3d,
            11 when IsAscii(name, "perspective") => CssTransformFunctionKind.Perspective,
            _ => (CssTransformFunctionKind) byte.MaxValue,
        };
        return kind != (CssTransformFunctionKind) byte.MaxValue;
    }

    private static bool IsAscii(string value, string lowercase) => CssAscii.EqualsIgnoreCase(value, lowercase);
}
