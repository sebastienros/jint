using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Cascade 5 §4.1: invalid declarations never participate in the cascade.
// https://drafts.csswg.org/css-cascade-5/#declared
// A small renderless grammar gate, not a computed-value or math evaluator.
internal static class CssLayoutValues
{
    internal static bool AcceptsText(string name, string text, CssValueWork work, out string normalized)
    {
        normalized = text;
        if (!Checks(name)) return true;
        using var parser = new CssSyntaxParser(text, null, work.Token, work.CheckCancellation);
        var values = parser.ParseComponentValues();
        if (!Accepts(name, values, work)) return false;
        normalized = NormalizeKeywords(name, text, values, work);
        return true;
    }

    internal static bool Checks(string name) => name is
        "display" or "visibility" or "position" or "float" or "clear" or "box-sizing" or
        "direction" or "writing-mode" or "white-space" or "white-space-collapse" or "text-wrap-mode" or
        "width" or "height" or "min-width" or "min-height" or "max-width" or "max-height" or
        "top" or "right" or "bottom" or "left" or "inset" or "margin" or "padding" or
        "font-size" or "line-height" or "flex" or "flex-flow" or "flex-direction" or "flex-wrap" or
        "flex-grow" or "flex-shrink" or "flex-basis" or "gap" or "row-gap" or "column-gap" or
        "overflow" or "overflow-x" or "overflow-y" or "border-width" or "border-style" or "z-index" or
        "align-items" or "align-self" or "align-content" or "justify-items" or "justify-self" or "justify-content" ||
        name.StartsWith("margin-", StringComparison.Ordinal) || name.StartsWith("padding-", StringComparison.Ordinal) ||
        name.StartsWith("border-", StringComparison.Ordinal) && (name.EndsWith("-width", StringComparison.Ordinal) || name.EndsWith("-style", StringComparison.Ordinal));

    // Canonicalize only keyword grammars. Other values/custom properties retain declared text.
    internal static string NormalizeKeywords(string name, string text, CssComponentValueList values, CssValueWork work)
    {
        if (name.StartsWith("--", StringComparison.Ordinal)) return text;
        if (!Checks(name))
        {
            string? keyword = null;
            foreach (var item in values)
            {
                work.Charge(1);
                if (item.TokenKind == CssTokenKind.Whitespace) continue;
                if (keyword is not null || item.TokenKind != CssTokenKind.Ident) return text;
                keyword = CssWideKeywords.Recognize(item.Token.Text.AsSpan()) is { } wide && wide != CssWideKeyword.None
                    ? wide.CanonicalSpelling() : null;
                if (keyword is null) return text;
            }
            return keyword ?? text;
        }
        var words = new List<string>();
        foreach (var item in values)
        {
            work.Charge(1);
            if (item.TokenKind == CssTokenKind.Whitespace) continue;
            if (item.TokenKind != CssTokenKind.Ident) return text;
            work.Charge(item.Token.Text.Length);
            words.Add(Keyword(item));
        }
        return string.Join(" ", words);
    }

    internal static bool Accepts(string name, CssComponentValueList values, CssValueWork work)
    {
        if (!Checks(name)) return true;
        var items = new List<CssComponentValue>();
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.TokenKind != CssTokenKind.Whitespace) items.Add(value);
        }
        if (items.Count == 0) return false;
        if (items.Count == 1 && CssText.IsWide(Keyword(items[0]))) return true;
        // Variables defer grammar checking until substitution, including inside math functions.
        Stack<CssComponentValueList>? pending = null;
        var list = values;
        while (true)
        {
            foreach (var item in list)
            {
                work.Charge(1);
                if (item.Kind != CssComponentKind.Function) continue;
                if (CssAscii.EqualsIgnoreCase(item.FunctionName, "var")) return true;
                (pending ??= new()).Push(item.Values);
            }
            if (pending is null || !pending.TryPop(out list!)) break;
        }
        if (name == "display") return Display(items);
        if (name == "white-space") return WhiteSpace(items);
        if (name.StartsWith("align-", StringComparison.Ordinal) || name.StartsWith("justify-", StringComparison.Ordinal))
            return Alignment(name, items);
        if (name == "flex-flow") return items.Count <= 2 &&
            (items.Count == 1 && (Direction(items[0]) || Wrap(items[0])) ||
             items.Count == 2 && (Direction(items[0]) && Wrap(items[1]) || Wrap(items[0]) && Direction(items[1])));
        if (name == "flex")
        {
            if (items.Count == 1 && Keyword(items[0]) is "none" or "auto") return true;
            var numbers = 0;
            var bases = 0;
            var math = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (Math(items[i])) math++;
                else if (Number(items[i], work)) numbers++;
                else if (Size(items[i], "flex-basis", work))
                {
                    bases++;
                    // The two flex numbers must remain adjacent.
                    if (i == 1 && items.Count == 3) return false;
                }
                else return false;
            }
            return items.Count <= 3 && numbers <= 2 && bases <= 1 && (bases == 0 || math <= 2);
        }
        var count = name is "margin" or "padding" or "inset" or "border-width" or "border-style" ? 4 :
            name is "gap" or "overflow" || name is "margin-block" or "margin-inline" or "padding-block" or "padding-inline" ? 2 : 1;
        if (items.Count > count) return false;
        foreach (var item in items)
        {
            work.Charge(1);
            var keyword = Keyword(item);
            var valid = name switch
            {
                "visibility" => keyword is "visible" or "hidden" or "collapse",
                "position" => keyword is "static" or "relative" or "absolute" or "fixed" or "sticky",
                "float" => keyword is "none" or "left" or "right" or "inline-start" or "inline-end",
                "clear" => keyword is "none" or "left" or "right" or "both" or "inline-start" or "inline-end",
                "box-sizing" => keyword is "content-box" or "border-box",
                "direction" => keyword is "ltr" or "rtl",
                "writing-mode" => keyword is "horizontal-tb" or "vertical-rl" or "vertical-lr" or "sideways-rl" or "sideways-lr",
                "white-space-collapse" => keyword is "collapse" or "preserve" or "preserve-breaks" or "preserve-spaces" or "break-spaces" or "discard",
                "text-wrap-mode" => keyword is "wrap" or "nowrap",
                "flex-direction" => Direction(item),
                "flex-wrap" => Wrap(item),
                "flex-grow" or "flex-shrink" => Number(item, work),
                "overflow" or "overflow-x" or "overflow-y" => keyword is "visible" or "hidden" or "clip" or "scroll" or "auto" or "overlay",
                "z-index" => keyword == "auto" || Math(item) || item.TokenKind == CssTokenKind.Number && item.Token.IsInteger,
                _ when name.EndsWith("-style", StringComparison.Ordinal) => keyword is "none" or "hidden" or "dotted" or "dashed" or "solid" or "double" or "groove" or "ridge" or "inset" or "outset",
                _ => Size(item, name, work)
            };
            if (!valid) return false;
        }
        return true;
    }

    private static bool Display(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0]) is
            "-webkit-box" or "-webkit-inline-box" or "none" or "contents" or "block" or "inline" or "run-in" or "flow" or "flow-root" or "table" or "flex" or "grid" or "ruby" or
            "list-item" or "inline-block" or "inline-table" or "inline-flex" or "inline-grid" or
            "table-row-group" or "table-header-group" or "table-footer-group" or "table-row" or "table-cell" or "table-column-group" or "table-column" or "table-caption" or
            "ruby-base" or "ruby-text" or "ruby-base-container" or "ruby-text-container") return true;
        var outside = false;
        var inside = false;
        var list = false;
        foreach (var item in items)
        {
            switch (Keyword(item))
            {
                case "block" or "inline" or "run-in" when !outside: outside = true; break;
                case "flow" or "flow-root" or "table" or "flex" or "grid" or "ruby" when !inside: inside = true; break;
                case "list-item" when !list: list = true; break;
                default: return false;
            }
        }
        return !list || items.All(static item => Keyword(item) is "list-item" or "block" or "inline" or "run-in" or "flow" or "flow-root");
    }

    private static bool WhiteSpace(List<CssComponentValue> items)
    {
        if (items.Count == 1 && Keyword(items[0]) is "normal" or "pre" or "pre-wrap" or "pre-line" or "nowrap" or "break-spaces") return true;
        var collapse = false;
        var wrap = false;
        var trim = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var keyword = Keyword(item);
            switch (keyword)
            {
                case "collapse" or "preserve" or "preserve-breaks" or "preserve-spaces" or "break-spaces" or "discard" when !collapse: collapse = true; break;
                case "wrap" or "nowrap" when !wrap: wrap = true; break;
                case "none" or "discard-before" or "discard-after" or "discard-inner" when trim.Add(keyword): break;
                default: return false;
            }
        }
        return !trim.Contains("none") || trim.Count == 1;
    }

    private static bool Alignment(string name, List<CssComponentValue> items)
    {
        var content = name.EndsWith("-content", StringComparison.Ordinal);
        var justify = name.StartsWith("justify-", StringComparison.Ordinal);
        if (items.Count == 1)
        {
            var keyword = Keyword(items[0]);
            return keyword is "normal" or "stretch" or "center" or "start" or "end" or "flex-start" or "flex-end" ||
                keyword == "auto" && name.EndsWith("-self", StringComparison.Ordinal) ||
                keyword == "baseline" && (!justify || !content) ||
                keyword is "left" or "right" && justify ||
                keyword is "self-start" or "self-end" && !content ||
                keyword is "space-between" or "space-around" or "space-evenly" && content ||
                keyword == "legacy" && name == "justify-items" ||
                keyword is "anchor-center" or "dialog" && !content;
        }
        if (items.Count != 2) return false;
        var first = Keyword(items[0]);
        var second = Keyword(items[1]);
        if (first is "first" or "last" && second == "baseline") return !justify || !content;
        if (name == "justify-items" && (first == "legacy" && second is "left" or "right" or "center" ||
            second == "legacy" && first is "left" or "right" or "center")) return true;
        return first is "safe" or "unsafe" && (second is "center" or "start" or "end" or "flex-start" or "flex-end" ||
            second is "left" or "right" && justify || second is "self-start" or "self-end" && !content);
    }

    private static bool Size(CssComponentValue item, string name, CssValueWork work)
    {
        var keyword = Keyword(item);
        var margin = name.StartsWith("margin", StringComparison.Ordinal);
        var inset = name is "top" or "right" or "bottom" or "left" or "inset";
        var dimension = name.Contains("width", StringComparison.Ordinal) || name.Contains("height", StringComparison.Ordinal) || name == "flex-basis";
        var border = name.StartsWith("border-", StringComparison.Ordinal);
        if (keyword == "auto") return margin || inset || dimension && !border && !name.StartsWith("max-", StringComparison.Ordinal);
        if (keyword == "none") return name.StartsWith("max-", StringComparison.Ordinal);
        if (keyword is "min-content" or "max-content" or "fit-content" or "stretch" or "contain" or "-webkit-fill-available" or "-moz-available" or "-moz-fit-content") return dimension && !border;
        if (keyword == "content") return name == "flex-basis";
        if (border && keyword is "thin" or "medium" or "thick") return true;
        if (keyword == "normal") return name is "gap" or "row-gap" or "column-gap" or "line-height";
        if (keyword is "xx-small" or "x-small" or "small" or "medium" or "large" or "x-large" or "xx-large" or "xxx-large" or "smaller" or "larger") return name == "font-size";
        if (item.Kind == CssComponentKind.Function)
        {
            // Math remains declared text: do not evaluate or reject relative/font/container units.
            return Math(item) || CssAscii.EqualsIgnoreCase(item.FunctionName, "env") || CssAscii.EqualsIgnoreCase(item.FunctionName, "attr") ||
                dimension && !border && (CssAscii.EqualsIgnoreCase(item.FunctionName, "fit-content") || CssAscii.EqualsIgnoreCase(item.FunctionName, "calc-size") || CssAscii.EqualsIgnoreCase(item.FunctionName, "anchor-size")) ||
                inset && CssAscii.EqualsIgnoreCase(item.FunctionName, "anchor");
        }
        if (item.TokenKind is not (CssTokenKind.Number or CssTokenKind.Dimension or CssTokenKind.Percentage)) return false;
        var number = CssNumber.FromValidatedToken(item.Token.NumberText, work);
        if (!margin && !inset && number.Sign < 0) return false;
        return item.TokenKind switch
        {
            CssTokenKind.Number => number.Sign == 0 || name == "line-height",
            CssTokenKind.Percentage => !border,
            _ => CssUnits.Recognize(item.Token.Unit, work).Category() == CssUnitCategory.Length
        };
    }

    private static bool Number(CssComponentValue item, CssValueWork work) =>
        Math(item) || item.TokenKind == CssTokenKind.Number && CssNumber.FromValidatedToken(item.Token.NumberText, work).Sign >= 0;
    private static bool Math(CssComponentValue item) => item.Kind == CssComponentKind.Function &&
        item.FunctionName.ToLowerInvariant() is "calc" or "min" or "max" or "clamp" or "round" or "mod" or "rem" or
            "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or "atan2" or "pow" or "sqrt" or "hypot" or
            "log" or "exp" or "abs" or "sign";
    private static string Keyword(CssComponentValue item)
    {
        if (item.TokenKind != CssTokenKind.Ident) return "";
        var text = item.Token.Text;
        // All recognized keywords are short ASCII names; CSS case folding is ASCII-only.
        if (text.Length > 24) return "";
        foreach (var c in text)
        {
            if (c > 0x7f) return "";
        }
        return text.ToLowerInvariant();
    }
    private static bool Direction(CssComponentValue item) => Keyword(item) is "row" or "row-reverse" or "column" or "column-reverse";
    private static bool Wrap(CssComponentValue item) => Keyword(item) is "nowrap" or "wrap" or "wrap-reverse";
}
