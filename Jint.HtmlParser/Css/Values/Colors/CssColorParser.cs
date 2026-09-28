using Jint.HtmlParser.Css.Values.Math;

namespace Jint.HtmlParser.Css.Values.Colors;

// CSS Color 4 §§4–8. This matches specified CSS colors, not §4.5's
// environment-dependent HTML/Canvas algorithm for a used color.
internal static class CssColorParser
{
    internal static CssColorParseResult Parse(CssComponentValueList values, int maximumNestingDepth, CssValueWork work)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumNestingDepth);
        work.CheckCancellation();
        var parts = Significant(values, 1, work);
        var result = parts.Count == 1 ? Parse(parts[0], maximumNestingDepth, work) : CssColorParseResult.NoMatch();
        work.CheckCancellation();
        return result;
    }

    private static CssColorParseResult Parse(CssComponentValue value, int maximumNestingDepth, CssValueWork work)
    {
        switch (value.Kind)
        {
            case CssComponentKind.Token:
                {
                    switch (value.Token.Kind)
                    {
                        case CssTokenKind.Hash:
                            return Hex(value.Token, work);
                        case not CssTokenKind.Ident:
                            return CssColorParseResult.NoMatch();
                    }
                    var name = Lower(value.Token.Text, work);
                    work.Charge(name.Length);
                    if (CssNamedColorLookup.Match(name) is { } rgb)
                        return CssColorParseResult.Match(CssColorValue.Identity(CssColorKind.Named, name, value.Span, rgb));
                    return CssColorKeywords.ContextualKind(name) is { } kind
                        ? CssColorParseResult.Match(CssColorValue.Identity(kind, name, value.Span)) : CssColorParseResult.NoMatch();
                }
            case not CssComponentKind.Function:
                return CssColorParseResult.NoMatch();
        }
        var function = Lower(value.FunctionName, work);
        if (CssLabLchOklabNames.Match(function))
            return CssColorParseResult.Pending("color:" + function);
        var space = CssColorFunctionLookup.Match(function);
        if (space is null) return CssColorParseResult.NoMatch();
        var parts = Significant(value.Values, 8, work);
        if (parts.Count != 0 && IsKeyword(parts[0], "from", work))
            return CssColorParseResult.Pending("color:relative-" + function);
        if (space == CssColorSpace.Srgb)
        {
            if (parts.Count == 0 || parts[0].Kind != CssComponentKind.Token || parts[0].Token.Kind != CssTokenKind.Ident)
                return CssColorParseResult.NoMatch();
            if (!IsKeyword(parts[0], "srgb", work))
            {
                var name = Lower(parts[0].Token.Text, work);
                return CssSrgbLinearDisplayP3DisplayP3LinearNames.Match(name) || name.StartsWith("--", StringComparison.Ordinal)
                    ? CssColorParseResult.Pending("color:color") : CssColorParseResult.NoMatch();
            }
            work.Charge(parts.Count);
            parts.RemoveAt(0);
        }
        var legacy = false;
        foreach (var part in parts)
        {
            work.Charge(1);
            legacy |= IsComma(part);
        }
        if (legacy && space is CssColorSpace.Hwb or CssColorSpace.Srgb) return CssColorParseResult.NoMatch();
        if (legacy)
        {
            if (parts.Count is not (5 or 7) || !IsComma(parts[1]) || !IsComma(parts[3]) ||
                parts.Count == 7 && !IsComma(parts[5])) return CssColorParseResult.NoMatch();
        }
        else if (parts.Count is not (3 or 5) || parts.Count == 5 && !IsSlash(parts[3]))
            return CssColorParseResult.NoMatch();
        var channels = new CssColorChannel[4];
        string? blocker = null;
        for (var i = 0; i < 4; i++)
        {
            work.Charge(1);
            if (i == 3 && parts.Count == (legacy ? 5 : 3))
            { channels[i] = CssColorChannel.Implicit(1, value.Span); continue; }
            var component = parts[legacy ? i * 2 : i == 3 ? 4 : i];
            var hue = i == 0 && space is CssColorSpace.Hsl or CssColorSpace.Hwb;
            var percentOnly = legacy && i is 1 or 2 && space == CssColorSpace.Hsl;
            var parsed = Channel(component, hue, percentOnly, !legacy, maximumNestingDepth, work, out channels[i], out var pending);
            if (!parsed && pending is null) return CssColorParseResult.NoMatch();
            blocker ??= pending;
        }
        if (legacy && space == CssColorSpace.Rgb && channels[0].Kind != CssColorChannelKind.Uninitialized &&
            channels[1].Kind != CssColorChannelKind.Uninitialized && channels[2].Kind != CssColorChannelKind.Uninitialized &&
            (channels[0].IsPercentage != channels[1].IsPercentage || channels[0].IsPercentage != channels[2].IsPercentage))
            return CssColorParseResult.NoMatch();
        if (blocker is not null) return CssColorParseResult.Pending(blocker);
        return CssColorParseResult.Match(CssColorValue.Absolute(space.Value, channels[0], channels[1], channels[2], channels[3], value.Span, legacy));
    }

    private static bool Channel(CssComponentValue value, bool hue, bool percentOnly, bool allowMissing,
        int maximumNestingDepth, CssValueWork work, out CssColorChannel channel, out string? blocker)
    {
        channel = default;
        blocker = null;
        if (allowMissing && IsKeyword(value, "none", work))
        { channel = CssColorChannel.Missing(value.Span); return true; }
        if (value.Kind == CssComponentKind.Token)
        {
            var token = value.Token;
            var kind = token.Kind switch
            {
                CssTokenKind.Number => CssNumericKind.Number,
                CssTokenKind.Percentage => CssNumericKind.Percentage,
                CssTokenKind.Dimension => CssNumericKind.Dimension,
                _ => (CssNumericKind?) null
            };
            if (kind is null || hue && kind == CssNumericKind.Percentage || percentOnly && kind != CssNumericKind.Percentage)
                return false;
            var unit = kind == CssNumericKind.Dimension ? CssUnits.Recognize(token.Unit, work) : CssUnit.None;
            if (kind == CssNumericKind.Dimension && (!hue || unit.Category() != CssUnitCategory.Angle)) return false;
            var number = CssNumber.FromValidatedToken(token.NumberText, work);
            channel = CssColorChannel.Number(new CssNumericAtom(kind.Value, number, unit, token.IsInteger, value.Span),
                CssMathNumbers.ParseFinite(number, unit, work));
            return true;
        }
        var production = hue ? CssMathProduction.Number : percentOnly ? CssMathProduction.Percentage : CssMathProduction.NumberOrPercentage;
        var percentages = hue ? CssMathPercentageMode.Forbidden : CssMathPercentageMode.Raw;
        var context = new CssMathContext(production, percentages, maximumNestingDepth: maximumNestingDepth, ancestorNestingDepth: 1);
        var math = CssMathParser.ParseMath(value, context, work);
        if (hue && math.Status == CssMathParseStatus.NoMatch)
            math = CssMathParser.ParseMath(value, new CssMathContext(CssMathProduction.Angle, percentages,
                maximumNestingDepth: maximumNestingDepth, ancestorNestingDepth: 1), work);
        switch (math.Status)
        {
            case CssMathParseStatus.RequiresLaterGrammar:
                { blocker = "math:" + math.PendingFunction; return false; }
            case not CssMathParseStatus.Match:
                return false;
        }
        if (!CssColorMath.TryEvaluate(math.Value, work, out var resolved))
        { channel = CssColorChannel.Calculation(math.Value, 0); blocker = "color:channel-environment"; return false; }
        channel = CssColorChannel.Calculation(math.Value, resolved);
        return true;
    }

    private static CssColorParseResult Hex(CssToken token, CssValueWork work)
    {
        var text = token.Text;
        if (text.Length is not (3 or 4 or 6 or 8)) return CssColorParseResult.NoMatch();
        Span<int> bytes = stackalloc int[4];
        bytes[3] = 255;
        var shortForm = text.Length <= 4;
        for (var i = 0; i < text.Length; i++)
        {
            work.Charge(1);
            var digit = text[i] switch
            {
                >= '0' and <= '9' => text[i] - '0',
                >= 'a' and <= 'f' => text[i] - 'a' + 10,
                >= 'A' and <= 'F' => text[i] - 'A' + 10,
                _ => -1
            };
            if (digit < 0) return CssColorParseResult.NoMatch();
            if (shortForm) bytes[i] = digit * 17;
            else if ((i & 1) == 0) bytes[i / 2] = digit * 16;
            else bytes[i / 2] += digit;
        }
        return CssColorParseResult.Match(CssColorValue.Absolute(CssColorSpace.Rgb,
            CssColorChannel.Implicit(bytes[0], token.Span), CssColorChannel.Implicit(bytes[1], token.Span),
            CssColorChannel.Implicit(bytes[2], token.Span), CssColorChannel.Implicit(bytes[3] / 255d, token.Span), token.Span, true));
    }

    // Shared color matching does not initialize the property registry or any model.
    // Keep only the finite grammar prefix; C1 already owns all syntax quotas.
    private static List<CssComponentValue> Significant(CssComponentValueList values, int maximum, CssValueWork work)
    {
        var parts = new List<CssComponentValue>();
        for (var i = 0; i < values.Count; i++)
        {
            work.Charge(1);
            var value = values[i];
            if (value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace) continue;
            parts.Add(value);
            if (parts.Count > maximum) break;
        }
        return parts;
    }

    private static string Lower(string text, CssValueWork work)
    {
        work.CheckCancellation();
        var result = string.Create(text.Length, (Text: text, Work: work), static (target, state) =>
        {
            for (var i = 0; i < target.Length; i++)
            {
                state.Work.Charge(1);
                var ch = state.Text[i];
                target[i] = ch is >= 'A' and <= 'Z' ? (char) (ch + 32) : ch;
            }
        });
        work.CheckCancellation();
        return result;
    }

    private static bool IsKeyword(CssComponentValue value, string expected, CssValueWork work)
    {
        if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Ident) return false;
        var text = value.Token.Text;
        if (text.Length != expected.Length) return false;
        for (var i = 0; i < text.Length; i++)
        {
            work.Charge(1);
            var ch = text[i];
            if ((ch is >= 'A' and <= 'Z' ? (char) (ch + 32) : ch) != expected[i]) return false;
        }
        return true;
    }

    private static bool IsComma(CssComponentValue value) => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Comma;
    private static bool IsSlash(CssComponentValue value) => value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Delim && value.Token.Delimiter == '/';
}
