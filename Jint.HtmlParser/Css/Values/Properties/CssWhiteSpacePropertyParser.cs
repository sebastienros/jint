namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Text 4 §§3, 4.1–4.2, 5.1: shorthand expansion and canonical CSSOM values, no layout.
internal static class CssWhiteSpacePropertyParser
{
    private const string Collapse = "collapse discard preserve preserve-breaks preserve-spaces break-spaces";
    private const string Trimming = "discard-before discard-after discard-inner";

    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count == 0) return Invalid();
        if (grammar is CssPropertyGrammar.WhiteSpaceCollapse or CssPropertyGrammar.TextWrapMode)
        {
            if (parts.Count != 1) return Invalid();
            var text = CssPropertyParser.Keyword(parts[0], grammar == CssPropertyGrammar.WhiteSpaceCollapse ? Collapse : "wrap nowrap", work);
            return text is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Keyword(text, parts[0].Span));
        }
        if (grammar == CssPropertyGrammar.WhiteSpaceTrim)
        {
            if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], "none", work) is not null)
                return CssPropertyResult.Accepted(CssPropertyValue.Keyword("none", parts[0].Span));
            var flags = 0;
            foreach (var part in parts)
                if (!AddTrim(part, work, ref flags)) return Invalid();
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(TrimText(flags), parts[0].Span));
        }
        string? collapse = null;
        string? wrap = null;
        var trim = 0;
        var trimNone = false;
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], "normal pre pre-wrap pre-line", work) is { } legacy)
        {
            collapse = legacy switch { "pre" or "pre-wrap" => "preserve", "pre-line" => "preserve-breaks", _ => "collapse" };
            wrap = legacy == "pre" ? "nowrap" : "wrap";
        }
        else
        {
            foreach (var part in parts)
            {
                work.Charge(1);
                if (CssPropertyParser.Keyword(part, Collapse, work) is { } c)
                {
                    if (collapse is not null) return Invalid();
                    collapse = c;
                }
                else if (CssPropertyParser.Keyword(part, "wrap nowrap", work) is { } w)
                {
                    if (wrap is not null) return Invalid();
                    wrap = w;
                }
                else if (CssPropertyParser.Keyword(part, "none", work) is not null)
                {
                    if (trimNone || trim != 0) return Invalid();
                    trimNone = true;
                }
                else if (trimNone || !AddTrim(part, work, ref trim)) return Invalid();
            }
        }
        collapse ??= "collapse";
        wrap ??= "wrap";
        var trimming = TrimText(trim);
        var span = parts[0].Span;
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(Serialize(collapse, wrap, trimming, work), span,
            CssPropertyValue.Keyword(collapse, span), CssPropertyValue.Keyword(wrap, span), CssPropertyValue.Keyword(trimming, span)));
    }

    private static bool AddTrim(CssComponentValue part, CssValueWork work, ref int flags)
    {
        var bit = CssPropertyParser.Keyword(part, Trimming, work) switch
        {
            "discard-before" => 1, "discard-after" => 2, "discard-inner" => 4, _ => 0
        };
        if (bit == 0 || (flags & bit) != 0) return false;
        flags |= bit;
        return true;
    }

    private static string TrimText(int flags) => flags switch
    {
        0 => "none", 1 => "discard-before", 2 => "discard-after", 3 => "discard-before discard-after",
        4 => "discard-inner", 5 => "discard-before discard-inner", 6 => "discard-after discard-inner",
        _ => "discard-before discard-after discard-inner"
    };

    internal static string Serialize(string collapse, string wrap, string trim, CssValueWork work)
    {
        work.CheckCancellation();
        if (trim == "none")
        {
            var legacy = (collapse, wrap) switch
            {
                ("collapse", "wrap") => "normal", ("collapse", "nowrap") => "nowrap",
                ("preserve", "nowrap") => "pre", ("preserve", "wrap") => "pre-wrap",
                ("preserve-breaks", "wrap") => "pre-line", _ => null
            };
            if (legacy is not null) return legacy;
        }
        var values = new List<string>(3);
        if (collapse != "collapse") values.Add(collapse);
        if (wrap != "wrap") values.Add(wrap);
        if (trim != "none") values.Add(trim);
        var text = string.Join(" ", values);
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
