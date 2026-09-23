namespace Jint.HtmlParser.Css.Values;

internal static class CssPrimitiveParser
{
    // CSS Values 4, §§ 4–7; Editor's Draft, 2026-09-23. These match only
    // whole-list atoms; a surrounding grammar decides property admissibility.
    internal static CssPrimitiveResult<CssWideKeyword> ParseWideKeyword(CssComponentValueList values, CssValueWork work)
    {
        if (!TryFirst(values, work, out var component, out var trailing)) return NoMatch<CssWideKeyword>(default, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.Ident)
            return NoMatch<CssWideKeyword>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        var keyword = CssWideKeywords.Recognize(text);
        if (keyword == CssWideKeyword.None) return NoMatch<CssWideKeyword>(component.Span, work);
        if (trailing is { } span) return NoMatch<CssWideKeyword>(span, work);
        return Match(keyword, component.Span, work);
    }

    internal static CssPrimitiveResult<CssIdentifierValue> ParseIdentifier(CssComponentValueList values, CssValueWork work)
    {
        if (!TryFirst(values, work, out var component, out var trailing)) return NoMatch<CssIdentifierValue>(default, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.Ident)
            return NoMatch<CssIdentifierValue>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        if (trailing is { } span) return NoMatch<CssIdentifierValue>(span, work);
        return Match(new CssIdentifierValue(text, component.Span), component.Span, work);
    }

    internal static CssPrimitiveResult<CssIdentifierValue> ParseCustomIdentifier(
        CssComponentValueList values, ReadOnlySpan<string> excludedKeywords, CssValueWork work)
    {
        if (!TryFirst(values, work, out var component, out var trailing)) return NoMatch<CssIdentifierValue>(default, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.Ident)
            return NoMatch<CssIdentifierValue>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        if (CssWideKeywords.Recognize(text) != CssWideKeyword.None || AsciiEquals(text, "default", work))
            return NoMatch<CssIdentifierValue>(component.Span, work);
        foreach (var excluded in excludedKeywords)
        {
            work.Charge(1);
            if (AsciiEquals(text, excluded, work)) return NoMatch<CssIdentifierValue>(component.Span, work);
        }
        if (trailing is { } span) return NoMatch<CssIdentifierValue>(span, work);
        return Match(new CssIdentifierValue(text, component.Span), component.Span, work);
    }

    internal static CssPrimitiveResult<CssIdentifierValue> ParseDashedIdentifier(CssComponentValueList values, CssValueWork work)
    {
        if (!TryFirst(values, work, out var component, out var trailing)) return NoMatch<CssIdentifierValue>(default, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.Ident)
            return NoMatch<CssIdentifierValue>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        if (!text.StartsWith("--", StringComparison.Ordinal)) return NoMatch<CssIdentifierValue>(component.Span, work);
        if (trailing is { } span) return NoMatch<CssIdentifierValue>(span, work);
        return Match(new CssIdentifierValue(text, component.Span), component.Span, work);
    }

    internal static CssPrimitiveResult<CssStringValue> ParseString(CssComponentValueList values, CssValueWork work)
    {
        if (!TryFirst(values, work, out var component, out var trailing)) return NoMatch<CssStringValue>(default, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.String)
            return NoMatch<CssStringValue>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        if (trailing is { } span) return NoMatch<CssStringValue>(span, work);
        return Match(new CssStringValue(text, component.Span), component.Span, work);
    }

    internal static CssPrimitiveResult<CssNumericAtom> ParseNumericAtom(CssComponentValueList values, CssValueWork work)
    {
        if (!TryFirst(values, work, out var component, out var trailing)) return NoMatch<CssNumericAtom>(default, work);
        if (component.Kind != CssComponentKind.Token) return NoMatch<CssNumericAtom>(component.Span, work);
        var token = component.Token;
        CssNumericKind kind;
        CssUnit unit = CssUnit.None;
        switch (token.Kind)
        {
            case CssTokenKind.Number:
                kind = CssNumericKind.Number;
                break;
            case CssTokenKind.Percentage:
                kind = CssNumericKind.Percentage;
                break;
            case CssTokenKind.Dimension:
                kind = CssNumericKind.Dimension;
                unit = CssUnits.Recognize(token.Unit, work);
                if (unit == CssUnit.None) return NoMatch<CssNumericAtom>(component.Span, work);
                break;
            default:
                return NoMatch<CssNumericAtom>(component.Span, work);
        }
        if (trailing is { } span) return NoMatch<CssNumericAtom>(span, work);
        var number = CssNumber.FromValidatedToken(token.NumberText, work);
        return Match(new CssNumericAtom(kind, number, unit, token.IsInteger, component.Span), component.Span, work);
    }

    private static bool TryFirst(CssComponentValueList values, CssValueWork work,
        out CssComponentValue component, out CssSourceSpan? trailing)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        component = default;
        trailing = null;
        var found = false;
        for (var i = 0; i < values.Count; i++)
        {
            work.Charge(1);
            var current = values[i];
            if (current.Kind == CssComponentKind.Token && current.Token.Kind == CssTokenKind.Whitespace) continue;
            if (found)
            {
                trailing = current.Span;
                work.CheckCancellation();
                return true;
            }
            component = current;
            found = true;
        }
        work.CheckCancellation();
        return found;
    }

    private static bool AsciiEquals(string left, string right, CssValueWork work)
    {
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            work.Charge(2);
            var a = left[i];
            var b = right[i];
            if (a is >= 'A' and <= 'Z') a = (char) (a + ('a' - 'A'));
            if (b is >= 'A' and <= 'Z') b = (char) (b + ('a' - 'A'));
            if (a != b) return false;
        }
        return true;
    }

    private static CssPrimitiveResult<T> Match<T>(T value, CssSourceSpan span, CssValueWork work)
    {
        work.CheckCancellation();
        return CssPrimitiveResult<T>.Match(value, span);
    }

    private static CssPrimitiveResult<T> NoMatch<T>(CssSourceSpan span, CssValueWork work)
    {
        work.CheckCancellation();
        return CssPrimitiveResult<T>.NoMatch(span);
    }
}
