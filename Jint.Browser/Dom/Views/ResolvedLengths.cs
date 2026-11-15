using System.Globalization;
using System.Text;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// Absolutizes the lengths of a computed value without a layout: CSS Values 4 §6 font- and viewport-relative
/// units and the absolute units become <c>px</c>, and a math function whose arguments are then all of one
/// type is reduced (§10.13). A percentage stays a percentage: what it resolves against needs a box.
/// </summary>
/// <remarks>
/// The inputs are the element's and root's computed <c>font-size</c>, and the page's viewport. Fonts are not
/// measured, so <c>ex</c> and <c>ch</c> take CSS Values 4's 0.5em fallback, and with no container query
/// support a container unit takes its small-viewport fallback (CSS Contain 3 §6). A unit outside that set
/// (<c>lh</c>, <c>cap</c>, an angle, a time) and a token inside a string or <c>url()</c> keep their text.
/// </remarks>
internal static class ResolvedLengths
{
    /// <summary>The initial <c>font-size</c>, <c>medium</c> (CSS Fonts 4 §2.5: Chromium's default of 16px).</summary>
    internal const double MediumFontSize = 16;

    // Bounds a math expression's nesting, as the textual var() substitution is bounded.
    private const int MaxDepth = 32;

    /// <summary>The lengths one computed value resolves against, computed only when a unit needs them.</summary>
    internal sealed class Basis(NativeCssComputedStyle style, Viewport viewport, double? em = null, double? percent = null,
        bool nonNegative = false)
    {
        private double? _em = em;
        private double? _rem;

        internal Viewport Viewport { get; } = viewport;
        internal double? Percent { get; } = percent;
        // CSS Values 4 §10.12: a math result outside the property's allowed range clamps to it.
        internal bool NonNegative { get; } = nonNegative;
        internal NativeCssComputedStyle Style { get; } = style;
        internal double Em => _em ??= FontSizeOf(Style, Viewport);
        internal double Rem => _rem ??= RootFontSizeOf(Style, Viewport);
    }

    /// <summary>
    /// The resolved value of a longhand or of a shorthand recomposed from its longhands; the property's text
    /// unchanged when nothing in it needs resolving.
    /// </summary>
    internal static string Resolve(string name, NativeCssProperty property, NativeCssComputedStyle style, Viewport viewport)
    {
        var text = property.Text;
        if (name == "font-size") return Serialize(FontSizeOf(style, viewport)) + "px";
        // font's size and line height are relative to the parent's font, not this element's: it keeps its text.
        if (name.StartsWith("--", StringComparison.Ordinal) || name is "content" or "font" or "font-family" or "quotes" or
            "grid-template-areas" || !HasDigit(text)) return text;
        if (CssPropertyRegistry.Find(name) is { Longhands.Count: > 0 } shorthand)
        {
            var entries = new CssDeclaration[shorthand.Longhands.Count];
            var changed = false;
            for (var i = 0; i < entries.Length; i++)
            {
                style.Work.Charge(1);
                var longhand = style.GetNormalizedProperty(shorthand.Longhands[i]);
                var value = Resolve(longhand.Name, longhand, style, viewport);
                changed |= !string.Equals(value, longhand.Text, StringComparison.Ordinal);
                entries[i] = new(longhand.Name, value, false, default);
            }
            return changed ? CssDeclarationBlock.ShorthandValue(entries, shorthand, style.Work) : text;
        }
        // An inherited computed value was absolutized where it was declared (CSS Cascade 5 §7.2).
        var declaring = style;
        while (InheritsText(property) && declaring.InheritanceParent() is { } parent)
        {
            style.Work.Charge(1);
            declaring = parent;
            property = parent.GetNormalizedProperty(name);
        }
        return Absolutize(property.Text, new Basis(declaring, viewport, nonNegative: IsNonNegative(name)));
    }

    // CSS Variables 1 §3.1: an inherited property invalid at computed-value time inherits, like an unset one.
    private static bool InheritsText(NativeCssProperty property) =>
        property.Disposition == NativeCssDisposition.Inherited ||
        property.Disposition == NativeCssDisposition.InvalidAtComputedValue &&
        CssPropertyRegistry.Find(property.Name) is { Inherited: true };

    // The longhands whose grammar refuses a negative length, so that a reduced calc() clamps at zero.
    private static bool IsNonNegative(string name) =>
        name is "row-gap" or "column-gap" or "width" or "height" or "min-width" or "min-height" or "max-width" or
            "max-height" or "block-size" or "inline-size" or "min-block-size" or "min-inline-size" or "max-block-size" or
            "max-inline-size" or "flex-basis" or "flex-grow" or "flex-shrink" or "line-height" or "outline-width" or
            "column-rule-width" or "column-width" or "perspective" or "tab-size" ||
        name.StartsWith("padding-", StringComparison.Ordinal) || name.StartsWith("scroll-padding-", StringComparison.Ordinal) ||
        name.StartsWith("border-", StringComparison.Ordinal) && (name.EndsWith("-width", StringComparison.Ordinal) ||
            name.EndsWith("-radius", StringComparison.Ordinal));

    /// <summary>The element's computed <c>font-size</c> in pixels (CSS Fonts 4 §2.5).</summary>
    internal static double FontSizeOf(NativeCssComputedStyle style, Viewport viewport)
    {
        var chain = new List<NativeCssComputedStyle>();
        for (var current = style; current is not null; current = current.InheritanceParent())
        {
            style.Work.Charge(1);
            chain.Add(current);
        }
        var size = MediumFontSize;
        var documentElement = style.Element.OwnerDocument?.DocumentElement;
        double? root = null;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var property = chain[i].GetNormalizedProperty("font-size");
            if (InheritsText(property)) continue;
            // §2.5: rem in the root element's own font-size refers to the initial font-size.
            var rem = chain[i].Pseudo is null && ReferenceEquals(chain[i].Element, documentElement)
                ? MediumFontSize
                : root ??= RootFontSizeOf(chain[i], viewport);
            size = OwnFontSize(property.Text.Trim(), size, rem, chain[i], viewport);
        }
        return size;
    }

    private static double RootFontSizeOf(NativeCssComputedStyle style, Viewport viewport)
    {
        var root = style.Element.OwnerDocument?.DocumentElement;
        if (root is null) return MediumFontSize;
        var view = style.For(root);
        var property = view.GetNormalizedProperty("font-size");
        return InheritsText(property)
            ? MediumFontSize
            : OwnFontSize(property.Text.Trim(), MediumFontSize, MediumFontSize, view, viewport);
    }

    // §2.5: an absolute-size keyword uses Chromium's medium-relative table; a relative size and a percentage
    // resolve against the parent's computed font-size, as em does within font-size.
    private static double OwnFontSize(string text, double parent, double rem, NativeCssComputedStyle style, Viewport viewport)
    {
        switch (text.ToLowerInvariant())
        {
            case "xx-small": return 9;
            case "x-small": return 10;
            case "small": return 13;
            case "medium": return MediumFontSize;
            case "large": return 18;
            case "x-large": return 24;
            case "xx-large": return 32;
            case "xxx-large": return 48;
            case "smaller": return parent / 1.2;
            case "larger": return parent * 1.2;
        }
        var basis = new Basis(style, viewport, em: parent, percent: parent);
        var resolved = Absolutize(text, basis, rem);
        if (resolved.EndsWith("px", StringComparison.Ordinal) &&
            double.TryParse(resolved.AsSpan(0, resolved.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var px) &&
            double.IsFinite(px))
            return Math.Max(0, px);
        if (resolved.EndsWith('%') &&
            double.TryParse(resolved.AsSpan(0, resolved.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) &&
            double.IsFinite(percent))
            return Math.Max(0, parent * percent / 100);
        return parent;
    }

    /// <summary>Rewrites every convertible length in <paramref name="text"/> to px and reduces resolvable math.</summary>
    internal static string Absolutize(string text, Basis basis) => Absolutize(text, basis, rem: null);

    private static string Absolutize(string text, Basis basis, double? rem, int depth = 0)
    {
        if (depth > MaxDepth || !HasDigit(text)) return text;
        basis.Style.Work.Charge(text.Length);
        StringBuilder? builder = null;
        var copied = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                i = SkipString(text, i);
                continue;
            }
            if (c == '#' || IsNameStart(text, i))
            {
                var start = i;
                if (c == '#') i++;
                while (i < text.Length && IsNameChar(text[i])) i++;
                if (c == '#' || i >= text.Length || text[i] != '(') continue;
                var function = text.Substring(start, i - start).ToLowerInvariant();
                var opaque = function is "url" or "src" or "attr" or "env" or "format" or "local";
                if (!opaque && !IsMath(function))
                {
                    // Any other function's arguments are scanned in place, as the rest of the value is.
                    i++;
                    continue;
                }
                var close = Close(text, i);
                if (close < 0) return builder is null ? text : Flush(builder, text, copied, text.Length);
                if (opaque)
                {
                    i = close + 1;
                    continue;
                }
                var parser = new Parser(text, i + 1, close, basis, rem);
                string? replacement = null;
                if (parser.TryFunction(function, out var value) && parser.AtEnd)
                    replacement = Format(basis.NonNegative && value.Amount < 0 ? value with { Amount = 0 } : value);
                else
                {
                    var inner = text.Substring(i + 1, close - i - 1);
                    var rewritten = Absolutize(inner, basis, rem, depth + 1);
                    if (!ReferenceEquals(rewritten, inner)) replacement = string.Concat(text.AsSpan(start, i + 1 - start), rewritten, ")");
                }
                if (replacement is not null)
                {
                    builder ??= new StringBuilder(text.Length);
                    builder.Append(text, copied, start - copied).Append(replacement);
                    copied = close + 1;
                }
                i = close + 1;
                continue;
            }
            if (IsNumberStart(text, i))
            {
                var start = i;
                var number = ReadNumber(text, ref i, out var unit);
                if (unit.Length > 0 && unit != "%" && !unit.Equals("px", StringComparison.OrdinalIgnoreCase) &&
                    TryFactor(unit, basis, rem, out var factor) && double.IsFinite(number * factor))
                {
                    builder ??= new StringBuilder(text.Length);
                    builder.Append(text, copied, start - copied).Append(Serialize(number * factor)).Append("px");
                    copied = i;
                }
                continue;
            }
            i++;
        }
        return builder is null ? text : Flush(builder, text, copied, text.Length);
    }

    private static string Flush(StringBuilder builder, string text, int from, int to) =>
        builder.Append(text, from, to - from).ToString();

    private enum Kind { Number, Length, Percent }

    private readonly record struct Value(double Amount, Kind Kind);

    private static string Format(Value value) => value.Kind switch
    {
        Kind.Length => Serialize(value.Amount) + "px",
        Kind.Percent => Serialize(value.Amount) + "%",
        _ => Serialize(value.Amount)
    };

    /// <summary>A number the way the other resolved values serialize it: at most six decimals, no exponent.</summary>
    internal static string Serialize(double value)
    {
        var text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    private static bool IsMath(string function) => function is "calc" or "min" or "max" or "clamp" or "-webkit-calc";

    // CSS Values 4 §6.1-§6.2 and §7.1: px per unit, or false when the unit needs a measurement this model lacks.
    private static bool TryFactor(string unit, Basis basis, double? rem, out double factor)
    {
        var viewport = basis.Viewport;
        factor = unit.ToLowerInvariant() switch
        {
            "px" => 1,
            "cm" => 96 / 2.54,
            "mm" => 96 / 25.4,
            "q" => 96 / 101.6,
            "in" => 96,
            "pt" => 96d / 72,
            "pc" => 16,
            "em" or "ic" => basis.Em,
            "ex" or "ch" => basis.Em / 2,
            "rem" or "ric" => rem ?? basis.Rem,
            "rex" or "rch" => (rem ?? basis.Rem) / 2,
            "vw" or "svw" or "lvw" or "dvw" or "vi" or "svi" or "lvi" or "dvi" or "cqw" or "cqi" => viewport.Width / 100d,
            "vh" or "svh" or "lvh" or "dvh" or "vb" or "svb" or "lvb" or "dvb" or "cqh" or "cqb" => viewport.Height / 100d,
            "vmin" or "svmin" or "lvmin" or "dvmin" or "cqmin" => Math.Min(viewport.Width, viewport.Height) / 100d,
            "vmax" or "svmax" or "lvmax" or "dvmax" or "cqmax" => Math.Max(viewport.Width, viewport.Height) / 100d,
            _ => double.NaN
        };
        return !double.IsNaN(factor);
    }

    private static bool HasDigit(string text)
    {
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c)) return true;
        }
        return false;
    }

    private static bool IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '\\' || c >= 0x80;

    private static bool IsNameStart(string text, int i)
    {
        var c = text[i];
        if (char.IsAsciiLetter(c) || c == '_' || c >= 0x80) return true;
        return c == '-' && i + 1 < text.Length && (char.IsAsciiLetter(text[i + 1]) || text[i + 1] is '-' or '_' || text[i + 1] >= 0x80);
    }

    private static bool IsNumberStart(string text, int i)
    {
        var c = text[i];
        if (char.IsAsciiDigit(c)) return true;
        if (c == '.') return i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]);
        if (c is '+' or '-' && i + 1 < text.Length)
            return char.IsAsciiDigit(text[i + 1]) || text[i + 1] == '.' && i + 2 < text.Length && char.IsAsciiDigit(text[i + 2]);
        return false;
    }

    // CSS Syntax 3 §4.3.12-§4.3.13: a number, then its unit (an identifier) or a percent sign.
    private static double ReadNumber(string text, ref int i, out string unit)
    {
        var start = i;
        if (text[i] is '+' or '-') i++;
        while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
        if (i + 1 < text.Length && text[i] == '.' && char.IsAsciiDigit(text[i + 1]))
        {
            i++;
            while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
        }
        if (i + 1 < text.Length && text[i] is 'e' or 'E' &&
            (char.IsAsciiDigit(text[i + 1]) || text[i + 1] is '+' or '-' && i + 2 < text.Length && char.IsAsciiDigit(text[i + 2])))
        {
            i += 2;
            while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
        }
        var number = double.Parse(text.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        var unitStart = i;
        if (i < text.Length && text[i] == '%') i++;
        else if (i < text.Length && IsNameStart(text, i))
        {
            while (i < text.Length && IsNameChar(text[i])) i++;
        }
        unit = text.Substring(unitStart, i - unitStart);
        return number;
    }

    private static int SkipString(string text, int i)
    {
        var quote = text[i++];
        while (i < text.Length)
        {
            if (text[i] == '\\') i += 2;
            else if (text[i++] == quote) break;
        }
        return Math.Min(i, text.Length);
    }

    // The index of the parenthesis that closes the one at `open`, or -1.
    private static int Close(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                i = SkipString(text, i) - 1;
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    // CSS Values 4 §10.8-§10.9: a calc-sum of products, typed as one of number, length or percentage. The
    // parser refuses rather than guesses: a mixed length-percentage sum keeps its text with its units absolutized.
    private struct Parser(string text, int position, int end, Basis basis, double? rem)
    {
        private int _position = position;
        private int _depth;

        internal readonly bool AtEnd => _position == end;

        internal bool TryFunction(string function, out Value value)
        {
            value = default;
            if (++_depth > MaxDepth) return false;
            basis.Style.Work.Charge(1);
            var ok = function switch
            {
                "calc" or "-webkit-calc" => TrySum(out value),
                "min" or "max" => TryMinMax(function == "min", out value),
                "clamp" => TryClamp(out value),
                _ => false
            };
            _depth--;
            SkipSpace();
            return ok && _position == end;
        }

        private bool TryMinMax(bool min, out Value value)
        {
            if (!TrySum(out value)) return false;
            while (Accept(','))
            {
                if (!TrySum(out var next) || next.Kind != value.Kind) return false;
                value = min ? (next.Amount < value.Amount ? next : value) : (next.Amount > value.Amount ? next : value);
            }
            return true;
        }

        private bool TryClamp(out Value value)
        {
            value = default;
            if (!TrySum(out var low) || !Accept(',') || !TrySum(out var middle) || !Accept(',') || !TrySum(out var high))
                return false;
            if (low.Kind != middle.Kind || middle.Kind != high.Kind) return false;
            // §10.4: clamp(MIN, VAL, MAX) is max(MIN, min(VAL, MAX)).
            value = new(Math.Max(low.Amount, Math.Min(middle.Amount, high.Amount)), middle.Kind);
            return true;
        }

        private bool TrySum(out Value value)
        {
            if (!TryProduct(out value)) return false;
            while (true)
            {
                // §10.8: + and - must be surrounded by white space.
                var save = _position;
                if (!SkipSpace() || _position >= end || text[_position] is not ('+' or '-'))
                {
                    _position = save;
                    return true;
                }
                var sign = text[_position] == '-' ? -1 : 1;
                _position++;
                if (_position >= end || !char.IsWhiteSpace(text[_position])) return false;
                if (!TryProduct(out var right) || right.Kind != value.Kind) return false;
                value = new(value.Amount + sign * right.Amount, value.Kind);
            }
        }

        private bool TryProduct(out Value value)
        {
            if (!TryTerm(out value)) return false;
            while (true)
            {
                var save = _position;
                SkipSpace();
                if (_position >= end || text[_position] is not ('*' or '/'))
                {
                    _position = save;
                    return true;
                }
                var multiply = text[_position++] == '*';
                if (!TryTerm(out var right)) return false;
                if (multiply)
                {
                    if (value.Kind != Kind.Number && right.Kind != Kind.Number) return false;
                    value = new(value.Amount * right.Amount, value.Kind == Kind.Number ? right.Kind : value.Kind);
                }
                else
                {
                    if (right.Kind != Kind.Number || right.Amount == 0) return false;
                    value = new(value.Amount / right.Amount, value.Kind);
                }
                if (!double.IsFinite(value.Amount)) return false;
            }
        }

        private bool TryTerm(out Value value)
        {
            value = default;
            SkipSpace();
            if (_position >= end) return false;
            if (text[_position] == '(')
            {
                var close = Close(text, _position);
                if (close < 0 || close >= end) return false;
                var inner = new Parser(text, _position + 1, close, basis, rem) { _depth = _depth + 1 };
                if (inner._depth > MaxDepth || !inner.TrySum(out value)) return false;
                inner.SkipSpace();
                if (!inner.AtEnd) return false;
                _position = close + 1;
                return true;
            }
            if (IsNameStart(text, _position))
            {
                var start = _position;
                while (_position < end && IsNameChar(text[_position])) _position++;
                if (_position >= end || text[_position] != '(') return false;
                var function = text.Substring(start, _position - start).ToLowerInvariant();
                var close = Close(text, _position);
                if (!IsMath(function) || close < 0 || close >= end) return false;
                var inner = new Parser(text, _position + 1, close, basis, rem) { _depth = _depth };
                if (!inner.TryFunction(function, out value)) return false;
                _position = close + 1;
                return true;
            }
            if (!IsNumberStart(text, _position)) return false;
            var amount = ReadNumber(text, ref _position, out var unit);
            if (_position > end) return false;
            if (unit.Length == 0) value = new(amount, Kind.Number);
            else if (unit == "%")
                value = basis.Percent is { } percent ? new(amount * percent / 100, Kind.Length) : new(amount, Kind.Percent);
            else if (TryFactor(unit, basis, rem, out var factor)) value = new(amount * factor, Kind.Length);
            else return false;
            return double.IsFinite(value.Amount);
        }

        private bool Accept(char c)
        {
            SkipSpace();
            if (_position >= end || text[_position] != c) return false;
            _position++;
            return true;
        }

        // True when at least one white-space character was skipped.
        private bool SkipSpace()
        {
            var start = _position;
            while (_position < end && char.IsWhiteSpace(text[_position])) _position++;
            return _position > start;
        }
    }
}
