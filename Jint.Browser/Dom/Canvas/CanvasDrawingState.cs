using System.Globalization;
using Jint.Browser.Geometry;
using Jint.HtmlParser.Css.Values;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom.Canvas;

/// <summary>The complete saved drawing state of a non-rendering 2D context.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#the-canvas-state</remarks>
internal sealed class CanvasDrawingState
{
    internal static readonly Dictionary<string, JsValue> Defaults = new(StringComparer.Ordinal)
    {
        ["fillStyle"] = "#000000",
        ["strokeStyle"] = "#000000",
        ["globalAlpha"] = 1,
        ["globalCompositeOperation"] = "source-over",
        ["lineWidth"] = 1,
        ["lineCap"] = "butt",
        ["lineJoin"] = "miter",
        ["miterLimit"] = 10,
        ["lineDashOffset"] = 0,
        ["font"] = "10px sans-serif",
        ["textAlign"] = "start",
        ["textBaseline"] = "alphabetic",
        ["direction"] = "inherit",
        ["letterSpacing"] = "0px",
        ["wordSpacing"] = "0px",
        ["fontKerning"] = "auto",
        ["fontStretch"] = "normal",
        ["fontVariantCaps"] = "normal",
        ["textRendering"] = "auto",
        ["lang"] = "inherit",
        ["imageSmoothingEnabled"] = true,
        ["imageSmoothingQuality"] = "low",
        ["shadowBlur"] = 0,
        ["shadowColor"] = "rgba(0, 0, 0, 0)",
        ["shadowOffsetX"] = 0,
        ["shadowOffsetY"] = 0,
        ["filter"] = "none",
    };

    internal Dictionary<string, JsValue> Values { get; private init; } = new(Defaults, StringComparer.Ordinal);
    internal double[] Matrix { get; set; } = GeometryMatrix.Identity();
    internal double[] LineDash { get; set; } = [];
    internal double FontSize { get; private set; } = 10;

    internal CanvasDrawingState Copy() => new()
    {
        Values = new Dictionary<string, JsValue>(Values, StringComparer.Ordinal),
        Matrix = (double[]) Matrix.Clone(),
        LineDash = (double[]) LineDash.Clone(),
        FontSize = FontSize,
    };

    internal static JsValue? Convert(CanvasRealm owner, string name, JsValue value)
    {
        if (name == "imageSmoothingEnabled") return JsBoolean.Create(TypeConverter.ToBoolean(value));
        if (Defaults[name].IsNumber())
        {
            var number = TypeConverter.ToNumber(value);
            if (!double.IsFinite(number)) return null;
            if (name == "globalAlpha" && (number < 0 || number > 1)) return null;
            if (name is "lineWidth" or "miterLimit" && number <= 0) return null;
            if (name == "shadowBlur" && number < 0) return null;
            return JsNumber.Create(number);
        }
        if (name is "fillStyle" or "strokeStyle" && value is JsCanvasGradient or JsCanvasPattern) return value;
        var text = TypeConverter.ToString(value);
        if (name is "fillStyle" or "strokeStyle" or "shadowColor")
            return CanvasColor.TryParse(text, out var color) ? JsString.Create(color) : null;
        var allowed = name switch
        {
            "globalCompositeOperation" => "source-over source-in source-out source-atop destination-over destination-in destination-out destination-atop lighter copy xor multiply screen overlay darken lighten color-dodge color-burn hard-light soft-light difference exclusion hue saturation color luminosity",
            "lineCap" => "butt round square",
            "lineJoin" => "round bevel miter",
            "textAlign" => "start end left right center",
            "textBaseline" => "top hanging middle alphabetic ideographic bottom",
            "direction" => "ltr rtl inherit",
            "fontKerning" => "auto normal none",
            "fontStretch" => "ultra-condensed extra-condensed condensed semi-condensed normal semi-expanded expanded extra-expanded ultra-expanded",
            "fontVariantCaps" => "normal small-caps all-small-caps petite-caps all-petite-caps unicase titling-caps",
            "textRendering" => "auto optimizeSpeed optimizeLegibility geometricPrecision",
            "imageSmoothingQuality" => "low medium high",
            _ => null,
        };
        if (allowed is not null && !allowed.Split(' ').Contains(text, StringComparer.Ordinal)) return null;
        if (name == "font")
        {
            if (!CssFontFaceValues.TryParseFontFamilies(text, new CssValueWork(owner.Dom.CancellationToken), out _)) return null;
            text = text.Trim();
        }
        if (name is "letterSpacing" or "wordSpacing")
        {
            if (!CanvasText.TryLength(text, 10, out _)) return null;
            text = text.Trim();
            if (text == "0") text = "0px";
        }
        if (name == "filter" && !CanvasText.IsFilter(text)) return null;
        return JsString.Create(text);
    }

    internal void Assign(string name, JsValue value)
    {
        Values[name] = value;
        if (name == "font") FontSize = CanvasText.FontSize(value.AsString());
    }
}

/// <summary>Bounded CSS text parsing and deterministic font-size estimates, without font metrics or layout.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#text-preparation-algorithm</remarks>
internal static class CanvasText
{
    internal static double FontSize(string text)
    {
        foreach (var token in text.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries))
        {
            var size = token.Split('/')[0];
            if (size is "xx-small" or "x-small" or "small" or "medium" or "large" or "x-large" or "xx-large")
                return size switch { "xx-small" => 9, "x-small" => 10, "small" => 13, "large" => 18, "x-large" => 24, "xx-large" => 32, _ => 16 };
            if (TryLength(size, 16, out var pixels) && pixels >= 0) return pixels;
            if (size.EndsWith('%') && double.TryParse(size.AsSpan(0, size.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                return Math.Max(0, percent * 0.16);
        }
        return 10;
    }

    internal static bool TryLength(string text, double em, out double pixels)
    {
        text = text.Trim().ToLowerInvariant();
        pixels = 0;
        if (text == "0") return true;
        var suffix = text.EndsWith("rem", StringComparison.Ordinal) ? 3 : 2;
        if (text.Length <= suffix) return false;
        var factor = text[^suffix..] switch
        {
            "px" => 1d,
            "pt" => 96d / 72,
            "pc" => 16,
            "in" => 96,
            "cm" => 96d / 2.54,
            "mm" => 96d / 25.4,
            "em" => em,
            "ex" or "ch" => em / 2,
            "rem" => 16,
            _ => double.NaN,
        };
        if (double.IsNaN(factor) || !double.TryParse(text.AsSpan(0, text.Length - suffix), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number)) return false;
        pixels = number * factor;
        return double.IsFinite(pixels);
    }

    internal static bool IsFilter(string text)
    {
        text = text.Trim();
        if (text == "none") return true;
        if (text.Length == 0) return false;
        while (text.Length > 0)
        {
            var open = text.IndexOf('(');
            if (open <= 0) return false;
            var name = text[..open];
            var close = text.IndexOf(')', open);
            if (close < 0) return false;
            var arg = text[(open + 1)..close].Trim();
            if (name == "blur")
            {
                if (arg.Length > 0 && (!TryLength(arg, 10, out var px) || px < 0)) return false;
            }
            else if (name == "hue-rotate")
            {
                if (arg.Length > 0 && !CanvasColor.TryHue(arg, out _)) return false;
            }
            else if (name is "brightness" or "contrast" or "grayscale" or "invert" or "opacity" or "saturate" or "sepia")
            {
                if (arg.Length > 0 && (!CanvasColor.TryComponent(arg, 1, out var amount) || amount < 0)) return false;
            }
            else return false;
            text = text[(close + 1)..].TrimStart();
        }
        return true;
    }
}
