using System.Globalization;
using Jint.HtmlParser.Parsing;

namespace Jint.HtmlParser.Svg;

/// <summary>On-demand SVG attribute readers. No tree-building path invokes these raw-source APIs.</summary>
/// <remarks>
/// https://svgwg.org/svg2-draft/types.html#syntax,
/// https://svgwg.org/svg2-draft/shapes.html#DataTypePoints,
/// https://svgwg.org/svg2-draft/coords.html#ViewBoxAttribute and
/// https://www.w3.org/TR/SVG11/coords.html#TransformAttribute.
/// Lists reject invalid suffixes rather than publishing partially parsed state.
/// </remarks>
internal static class SvgParser
{
    internal static readonly string[] Alignments =
        ["", "none", "xMinYMin", "xMidYMin", "xMaxYMin", "xMinYMid", "xMidYMid", "xMaxYMid", "xMinYMax", "xMidYMax", "xMaxYMax"];

    internal static double? ParseNumber(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        return reader.Number(out var value) && reader.End ? value : null;
    }

    internal static SvgLength? ParseLength(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        return reader.Length(out var value) && reader.End ? value : null;
    }

    internal static (double First, double Second)? ParseNumberOptionalNumber(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        if (!reader.Number(out var first)) return null;
        if (reader.End) return (first, first);
        return reader.Separator() && reader.Number(out var second) && reader.End ? (first, second) : null;
    }

    internal static double[]? ParseNumberList(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        if (reader.End) return [];
        var list = new List<double>();
        do
        {
            if (!reader.Number(out var value)) return null;
            list.Add(value);
            if (reader.End) return [.. list];
        } while (reader.Separator());
        return null;
    }

    internal static SvgLength[]? ParseLengthList(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        if (reader.End) return [];
        var list = new List<SvgLength>();
        do
        {
            if (!reader.Length(out var value)) return null;
            list.Add(value);
            if (reader.End) return [.. list];
        } while (reader.Separator());
        return null;
    }

    internal static SvgPoint[]? ParsePoints(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        if (reader.End) return [];
        var points = new List<SvgPoint>();
        do
        {
            checkpoint?.Invoke();
            if (!reader.Number(out var x) || !reader.Separator() || !reader.Number(out var y)) return null;
            points.Add(new SvgPoint(x, y));
            if (reader.End) return [.. points];
        } while (reader.Separator());
        return null;
    }

    internal static SvgViewBox? ParseViewBox(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        Span<double> values = stackalloc double[4];
        for (var i = 0; i < values.Length; i++)
        {
            if (i != 0 && !reader.Separator() || !reader.Number(out values[i])) return null;
        }
        return reader.End && values[2] >= 0 && values[3] >= 0
            ? new SvgViewBox(values[0], values[1], values[2], values[3]) : null;
    }

    internal static SvgAspectRatio? ParsePreserveAspectRatio(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        var word = reader.Word();
        ushort align = 0;
        for (ushort i = 1; i < Alignments.Length; i++)
        {
            if (word.SequenceEqual(Alignments[i])) { align = i; break; }
        }
        if (align == 0) return null;
        if (reader.End) return new SvgAspectRatio(align, 1);
        if (!reader.Whitespace()) return null;
        var mode = reader.Word();
        var meetOrSlice = mode.SequenceEqual("meet") ? 1 : mode.SequenceEqual("slice") ? 2 : 0;
        return meetOrSlice != 0 && reader.End ? new SvgAspectRatio(align, (ushort) meetOrSlice) : null;
    }

    internal static SvgTransform[] ParseTransformList(string source, Action? checkpoint = null)
    {
        var reader = new Reader(source, checkpoint);
        if (reader.End) return [];
        var list = new List<SvgTransform>();
        Span<double> args = stackalloc double[6];
        do
        {
            checkpoint?.Invoke();
            var kind = reader.Word() switch
            {
                "matrix" => SvgTransformKind.Matrix,
                "translate" => SvgTransformKind.Translate,
                "scale" => SvgTransformKind.Scale,
                "rotate" => SvgTransformKind.Rotate,
                "skewX" => SvgTransformKind.SkewX,
                "skewY" => SvgTransformKind.SkewY,
                _ => SvgTransformKind.Unknown,
            };
            if (kind == SvgTransformKind.Unknown || !reader.Take('(')) return [];
            args.Clear();
            var count = 0;
            do
            {
                if (count == 6 || !reader.Number(out args[count++])) return [];
                if (reader.Take(')')) break;
                if (!reader.Separator()) return [];
            } while (true);
            var valid = kind switch
            {
                SvgTransformKind.Matrix => count == 6,
                SvgTransformKind.Translate or SvgTransformKind.Scale => count is 1 or 2,
                SvgTransformKind.Rotate => count is 1 or 3,
                _ => count == 1,
            };
            if (!valid) return [];
            if (kind == SvgTransformKind.Scale && count == 1) args[1] = args[0];
            list.Add(new SvgTransform(kind, args[0], args[1], args[2], args[3], args[4], args[5]));
            if (reader.End) return [.. list];
        } while (reader.Separator());
        return [];
    }

    /// <summary>The comma-wsp grammar, deliberately not CSS tokenization or path-data tokenization.</summary>
    /// <remarks>https://svgwg.org/svg2-draft/types.html#syntax</remarks>
    private ref struct Reader
    {
        private readonly ReadOnlySpan<char> _source;
        private readonly Action? _checkpoint;
        private int _position;

        internal Reader(string source, Action? checkpoint)
        {
            ArgumentNullException.ThrowIfNull(source);
            _source = source;
            _checkpoint = checkpoint;
            _position = 0;
            checkpoint?.Invoke();
            Whitespace();
        }

        internal bool End
        {
            get
            {
                var i = _position;
                while (i < _source.Length && IsWhitespace(_source[i]))
                    if ((++i & 1023) == 0) _checkpoint?.Invoke();
                return i == _source.Length;
            }
        }

        internal bool Whitespace()
        {
            var start = _position;
            while (_position < _source.Length && IsWhitespace(_source[_position]))
            {
                if ((++_position & 1023) == 0) _checkpoint?.Invoke();
            }
            return _position != start;
        }

        internal bool Separator()
        {
            var whitespace = Whitespace();
            if (_position < _source.Length && _source[_position] == ',')
            {
                _position++;
                Whitespace();
                return !End;
            }
            return whitespace && !End;
        }

        internal bool Take(char c)
        {
            var saved = _position;
            Whitespace();
            if (_position < _source.Length && _source[_position] == c)
            {
                _position++;
                return true;
            }
            _position = saved;
            return false;
        }

        internal ReadOnlySpan<char> Word()
        {
            Whitespace();
            var start = _position;
            while (_position < _source.Length && char.IsAsciiLetter(_source[_position]))
                if ((++_position & 1023) == 0) _checkpoint?.Invoke();
            return _source[start.._position];
        }

        internal bool Number(out double value)
        {
            Whitespace();
            var length = NumberScanner.Scan(_source[_position..], trailingPoint: true, out _, _checkpoint);
            value = 0;
            if (length == 0) return false;
            var valid = double.TryParse(_source.Slice(_position, length), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && double.IsFinite(value);
            _position += length;
            return valid;
        }

        internal bool Length(out SvgLength value)
        {
            value = default;
            if (!Number(out var number)) return false;
            var start = _position;
            while (_position < _source.Length && char.IsAsciiLetter(_source[_position]))
                if ((++_position & 1023) == 0) _checkpoint?.Invoke();
            if (_position == start && _position < _source.Length && _source[_position] == '%') _position++;
            var unit = _source[start.._position] switch
            {
                "" => SvgLengthUnit.Number,
                "%" => SvgLengthUnit.Percentage,
                "em" => SvgLengthUnit.Ems,
                "ex" => SvgLengthUnit.Exs,
                "px" => SvgLengthUnit.Px,
                "cm" => SvgLengthUnit.Cm,
                "mm" => SvgLengthUnit.Mm,
                "in" => SvgLengthUnit.In,
                "pt" => SvgLengthUnit.Pt,
                "pc" => SvgLengthUnit.Pc,
                _ => SvgLengthUnit.Unknown,
            };
            value = new SvgLength(number, unit);
            return unit != SvgLengthUnit.Unknown;
        }

        private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';
    }
}
