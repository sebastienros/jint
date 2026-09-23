namespace Jint.HtmlParser.Css.Values;

internal static class CssPrimitiveParser
{
    // CSS Values 4, §§ 4–7; Editor's Draft, 2026-09-23. These match only
    // whole-list atoms; a surrounding grammar decides property admissibility.
    internal static CssPrimitiveResult<CssWideKeyword> ParseWideKeyword(CssComponentValueList values, CssValueWork work)
    {
        if (!TrySingle(values, work, out var component, out var failure)) return NoMatch<CssWideKeyword>(failure, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.Ident)
            return NoMatch<CssWideKeyword>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        var keyword = CssWideKeywords.Recognize(text);
        return keyword == CssWideKeyword.None ? NoMatch<CssWideKeyword>(component.Span, work) : Match(keyword, component.Span, work);
    }

    internal static CssPrimitiveResult<CssIdentifierValue> ParseIdentifier(CssComponentValueList values, CssValueWork work)
    {
        if (!TrySingle(values, work, out var component, out var failure)) return NoMatch<CssIdentifierValue>(failure, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.Ident)
            return NoMatch<CssIdentifierValue>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        return Match(new CssIdentifierValue(text, component.Span), component.Span, work);
    }

    internal static CssPrimitiveResult<CssIdentifierValue> ParseCustomIdentifier(
        CssComponentValueList values, ReadOnlySpan<string> excludedKeywords, CssValueWork work)
    {
        var result = ParseIdentifier(values, work);
        if (!result.IsMatch) return result;
        var text = result.Value.Text;
        if (CssWideKeywords.Recognize(text) != CssWideKeyword.None || AsciiEquals(text, "default", work))
            return NoMatch<CssIdentifierValue>(result.Span, work);
        foreach (var excluded in excludedKeywords)
        {
            work.Charge(1);
            if (AsciiEquals(text, excluded, work)) return NoMatch<CssIdentifierValue>(result.Span, work);
        }
        work.CheckCancellation();
        return result;
    }

    internal static CssPrimitiveResult<CssIdentifierValue> ParseDashedIdentifier(CssComponentValueList values, CssValueWork work)
    {
        var result = ParseCustomIdentifier(values, ReadOnlySpan<string>.Empty, work);
        if (!result.IsMatch) return result;
        var text = result.Value.Text;
        return text.StartsWith("--", StringComparison.Ordinal) ? result : NoMatch<CssIdentifierValue>(result.Span, work);
    }

    internal static CssPrimitiveResult<CssStringValue> ParseString(CssComponentValueList values, CssValueWork work)
    {
        if (!TrySingle(values, work, out var component, out var failure)) return NoMatch<CssStringValue>(failure, work);
        if (component.Kind != CssComponentKind.Token || component.Token.Kind != CssTokenKind.String)
            return NoMatch<CssStringValue>(component.Span, work);
        var text = component.Token.Text;
        work.Charge(text.Length);
        return Match(new CssStringValue(text, component.Span), component.Span, work);
    }

    internal static CssPrimitiveResult<CssNumericAtom> ParseNumericAtom(CssComponentValueList values, CssValueWork work)
    {
        if (!TrySingle(values, work, out var component, out var failure)) return NoMatch<CssNumericAtom>(failure, work);
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
        var number = CssNumber.FromValidatedToken(token.NumberText, work);
        return Match(new CssNumericAtom(kind, number, unit, token.IsInteger, component.Span), component.Span, work);
    }

    private static bool TrySingle(CssComponentValueList values, CssValueWork work,
        out CssComponentValue component, out CssSourceSpan failure)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        component = default;
        failure = default;
        var found = false;
        for (var i = 0; i < values.Count; i++)
        {
            work.Charge(1);
            var current = values[i];
            if (current.Kind == CssComponentKind.Token && current.Token.Kind == CssTokenKind.Whitespace) continue;
            if (found)
            {
                failure = current.Span;
                work.CheckCancellation();
                return false;
            }
            component = current;
            found = true;
        }
        if (!found) failure = default;
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
