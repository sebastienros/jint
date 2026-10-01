using System.Globalization;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values;

internal enum CssEasingKind : byte
{
    Linear,
    CubicBezier,
    Steps,
    PiecewiseLinear,
}

/// <summary>An immutable easing function, parsed only when a consumer asks for its grammar.</summary>
internal sealed class CssEasing
{
    internal static readonly CssEasing Linear = new(CssEasingKind.Linear, "linear");

    private readonly double _x1;
    private readonly double _y1;
    private readonly double _x2;
    private readonly double _y2;
    private readonly string _position;
    private readonly (double Input, double Output)[] _points;

    internal CssEasing(CssEasingKind kind, string text, double x1 = 0, double y1 = 0, double x2 = 0,
        double y2 = 0, string position = "", (double Input, double Output)[]? points = null)
    {
        Kind = kind;
        Serialization = text;
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
        _position = position;
        _points = points ?? [];
    }

    internal CssEasingKind Kind { get; }
    internal string Serialization { get; }

    /// <summary>
    /// https://drafts.csswg.org/css-easing-1/#cubic-bezier-algo,
    /// https://drafts.csswg.org/css-easing-1/#step-easing-algo and
    /// https://drafts.csswg.org/css-easing-2/#linear-easing-function-output.
    /// </summary>
    internal double Evaluate(double input, bool before = false)
    {
        switch (Kind)
        {
            case CssEasingKind.Linear:
                return input;
            case CssEasingKind.Steps:
                var step = System.Math.Floor(input * _x1);
                if (_position is "start" or "jump-start" or "jump-both")
                {
                    step++;
                }

                if (before && input * _x1 % 1 == 0)
                {
                    step--;
                }

                var jumps = _x1 + (_position is "jump-both" ? 1 : _position is "jump-none" ? -1 : 0);
                if (input >= 0)
                {
                    step = System.Math.Max(step, 0);
                }

                if (input <= 1)
                {
                    step = System.Math.Min(step, jumps);
                }

                return step / jumps;
            case CssEasingKind.CubicBezier:
                if (input < 0)
                {
                    return _x1 > 0 ? input * _y1 / _x1 : _x2 > 0 ? input * _y2 / _x2 : 0;
                }

                if (input > 1)
                {
                    return _x2 < 1 ? 1 + (input - 1) * (1 - _y2) / (1 - _x2)
                        : _x1 < 1 ? 1 + (input - 1) * (1 - _y1) / (1 - _x1) : 1;
                }

                if (input is 0 or 1)
                {
                    return input;
                }

                // Bisection remains well-conditioned at vertical tangents, unlike Newton iteration.
                double low = 0, high = 1;
                for (var i = 0; i < 48; i++)
                {
                    var t = (low + high) * 0.5;
                    if (Bezier(t, _x1, _x2) < input)
                    {
                        low = t;
                    }
                    else
                    {
                        high = t;
                    }
                }

                return Bezier((low + high) * 0.5, _y1, _y2);
            default:
                if (_points.Length == 1 || before && input == _points[0].Input)
                {
                    return _points[0].Output;
                }

                var index = 0;
                var end = _points.Length;
                while (index < end)
                {
                    var middle = index + (end - index) / 2;
                    if (_points[middle].Input <= input)
                    {
                        index = middle + 1;
                    }
                    else
                    {
                        end = middle;
                    }
                }

                index = System.Math.Clamp(index - 1, 0, _points.Length - 2);
                var a = _points[index];
                var b = _points[index + 1];
                return a.Input == b.Input ? (input < a.Input ? a.Output : b.Output)
                    : a.Output + (input - a.Input) / (b.Input - a.Input) * (b.Output - a.Output);
        }
    }

    private static double Bezier(double t, double a, double b)
        => 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t;
}

/// <summary>
/// The on-demand &lt;easing-function&gt; reader:
/// https://drafts.csswg.org/css-easing-1/#typedef-easing-function.
/// Also accepts Level 2's <c>linear()</c>; no stylesheet or declaration eagerly invokes this reader.
/// </summary>
internal static class CssEasingValues
{
    internal static bool TryParse(string source, CssValueWork work, out string serialization, out CssEasing easing)
    {
        work.CheckCancellation();
        serialization = "";
        easing = CssEasing.Linear;
        CssComponentValueList values;
        using (var parser = new CssSyntaxParser(source, options: null, work.Token, work.CheckCancellation))
        {
            values = parser.ParseComponentValues();
        }

        var items = Significant(values, work);
        if (items.Count != 1)
        {
            return false;
        }

        var item = items[0];
        if (item.Kind == CssComponentKind.Token && item.Token.Kind == CssTokenKind.Ident)
        {
            var keyword = CssEasingKeywordLookup.Match(item.Token.Text);
            var parsed = keyword switch
            {
                "linear" => CssEasing.Linear,
                "ease" => new CssEasing(CssEasingKind.CubicBezier, keyword, 0.25, 0.1, 0.25, 1),
                "ease-in" => new CssEasing(CssEasingKind.CubicBezier, keyword, 0.42, 0, 1, 1),
                "ease-out" => new CssEasing(CssEasingKind.CubicBezier, keyword, 0, 0, 0.58, 1),
                "ease-in-out" => new CssEasing(CssEasingKind.CubicBezier, keyword, 0.42, 0, 0.58, 1),
                "step-start" => new CssEasing(CssEasingKind.Steps, keyword, 1, position: "start"),
                "step-end" => new CssEasing(CssEasingKind.Steps, keyword, 1, position: "end"),
                _ => null,
            };
            if (parsed is null)
            {
                return false;
            }

            easing = parsed;
        }
        else if (item.Kind == CssComponentKind.Function)
        {
            var arguments = Significant(item.Values, work);
            switch (CssEasingFunctionLookup.Match(item.FunctionName))
            {
                case "cubic-bezier":
                    if (arguments.Count != 7
                        || !Number(arguments[0], out var x1) || !Comma(arguments[1])
                        || !Number(arguments[2], out var y1) || !Comma(arguments[3])
                        || !Number(arguments[4], out var x2) || !Comma(arguments[5])
                        || !Number(arguments[6], out var y2) || x1 is < 0 or > 1 || x2 is < 0 or > 1)
                    {
                        return false;
                    }

                    easing = new CssEasing(CssEasingKind.CubicBezier,
                        $"cubic-bezier({Format(x1)}, {Format(y1)}, {Format(x2)}, {Format(y2)})", x1, y1, x2, y2);
                    break;
                case "steps":
                    if (arguments.Count is not (1 or 3) || !Number(arguments[0], out var count)
                        || count < 1 || count != System.Math.Floor(count)
                        || arguments[0].Token.NumberText.AsSpan().IndexOfAny('.', 'e', 'E') >= 0)
                    {
                        return false;
                    }

                    string? position = "end";
                    if (arguments.Count == 3)
                    {
                        if (!Comma(arguments[1]) || arguments[2].Kind != CssComponentKind.Token
                            || arguments[2].Token.Kind != CssTokenKind.Ident)
                        {
                            return false;
                        }

                        position = CssStepPositionLookup.Match(arguments[2].Token.Text);
                    }

                    if (position is null || position == "jump-none" && count < 2)
                    {
                        return false;
                    }

                    easing = new CssEasing(CssEasingKind.Steps,
                        "steps(" + Format(count) + (position == "end" ? "" : ", " + position) + ")", count, position: position);
                    break;
                case "linear":
                    if (!TryLinear(arguments, work, out easing))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }
        else
        {
            return false;
        }

        serialization = easing.Serialization;
        return true;
    }

    /// <summary>https://drafts.csswg.org/css-easing-2/#the-linear-easing-function</summary>
    private static bool TryLinear(List<CssComponentValue> arguments, CssValueWork work, out CssEasing easing)
    {
        easing = CssEasing.Linear;
        var stops = new List<(double? Input, double Output)>();
        var start = 0;
        for (var i = 0; i <= arguments.Count; i++)
        {
            work.Charge(1);
            if (i < arguments.Count && !Comma(arguments[i]))
            {
                continue;
            }

            double? output = null, first = null, second = null;
            var outputIndex = -1;
            for (var j = start; j < i; j++)
            {
                work.Charge(1);
                if (Number(arguments[j], out var number) && output is null)
                {
                    output = number;
                    outputIndex = j;
                }
                else if (Number(arguments[j], out number, percentage: true) && second is null)
                {
                    if (first is null)
                    {
                        first = number / 100;
                    }
                    else
                    {
                        second = number / 100;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (output is null || i - start == 3 && outputIndex == start + 1)
            {
                return false;
            }

            stops.Add((first, output.Value));
            if (second is not null)
            {
                stops.Add((second, output.Value));
            }

            start = i + 1;
        }

        var explicitInputs = new bool[stops.Count];
        var largest = double.NegativeInfinity;
        for (var i = 0; i < stops.Count; i++)
        {
            work.Charge(1);
            var stop = stops[i];
            explicitInputs[i] = stop.Input is not null;
            var input = stop.Input ?? (i == 0 ? 0 : i == stops.Count - 1 ? System.Math.Max(1, largest) : (double?) null);
            if (input is { } specified)
            {
                largest = System.Math.Max(largest, specified);
                stops[i] = (largest, stop.Output);
            }
        }

        var points = new (double Input, double Output)[stops.Count];
        var previous = 0;
        for (var i = 0; i < stops.Count; i++)
        {
            work.Charge(1);
            if (stops[i].Input is not { } input)
            {
                continue;
            }

            points[i] = (input, stops[i].Output);
            for (var j = previous + 1; j < i; j++)
            {
                work.Charge(1);
                points[j] = (points[previous].Input + (input - points[previous].Input) * (j - previous) / (i - previous), stops[j].Output);
            }

            previous = i;
        }

        var texts = new string[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            work.Charge(1);
            texts[i] = Format(points[i].Output) + (explicitInputs[i] ? " " + Format(points[i].Input * 100) + "%" : "");
        }

        easing = new CssEasing(CssEasingKind.PiecewiseLinear, "linear(" + string.Join(", ", texts) + ")", points: points);
        return true;
    }

    private static List<CssComponentValue> Significant(CssComponentValueList values, CssValueWork work)
    {
        var result = new List<CssComponentValue>();
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace)
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static bool Number(CssComponentValue value, out double number, bool percentage = false)
    {
        number = 0;
        return value.Kind == CssComponentKind.Token
            && value.Token.Kind == (percentage ? CssTokenKind.Percentage : CssTokenKind.Number)
            && double.TryParse(value.Token.NumberText, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && double.IsFinite(number);
    }

    private static bool Comma(CssComponentValue value)
        => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Comma;

    private static string Format(double number) => number == 0 ? "0" : number.ToString("R", CultureInfo.InvariantCulture);
}
