using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Serialization;

// CSS Syntax Level 3, §9: https://drafts.csswg.org/css-syntax/#serialization
internal static class CssSyntaxSerializer
{
    internal static string SerializeString(string value, CssValueWork work)
    {
        var builder = new ValueStringBuilder(stackalloc char[256]);
        AppendString(ref builder, value, work);
        var result = builder.ToString();
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
    }

    internal static string SerializeIdentifier(string value, CssValueWork? work = null)
    {
        work?.CheckCancellation();
        if (IsPlainIdentifier(value))
        {
            // Nothing to escape: charge the same per-character and result work as the copy.
            work?.Charge(value.Length);
            work?.CheckCancellation();
            work?.Charge(value.Length);
            work?.CheckCancellation();
            return value;
        }
        var builder = new ValueStringBuilder(stackalloc char[256]);
        AppendIdentifier(ref builder, value, work: work);
        work?.CheckCancellation();
        var result = builder.ToString();
        work?.Charge(result.Length);
        work?.CheckCancellation();
        return result;
    }

    internal static string SerializeComponents(CssComponentValueList values, CssValueWork work)
    {
        work.CheckCancellation();
        var builder = new ValueStringBuilder(stackalloc char[256]);
        AppendValues(ref builder, values, work);
        work.CheckCancellation();
        var result = builder.ToString();
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
    }

    internal static string SerializeStyleSheet(IReadOnlyList<CssSyntaxRule> rules)
    {
        var builder = new ValueStringBuilder(stackalloc char[256]);
        for (var index = 0; index < rules.Count; index++)
        {
            if (index != 0) builder.Append('\n');
            AppendRule(ref builder, rules[index].Syntax);
        }
        return builder.ToString();
    }

    internal static string SerializeRule(CssRuleSyntax rule)
    {
        var builder = new ValueStringBuilder(stackalloc char[256]);
        AppendRule(ref builder, rule);
        return builder.ToString();
    }

    internal static string SerializeDeclarationList(IReadOnlyList<CssDeclarationSyntax> declarations)
    {
        var builder = new ValueStringBuilder(stackalloc char[256]);
        for (var index = 0; index < declarations.Count; index++)
        {
            if (index != 0) builder.Append(' ');
            AppendIdentifier(ref builder, declarations[index].Name);
            builder.Append(':');
            AppendValues(ref builder, declarations[index].Value);
            if (declarations[index].IsImportant) builder.Append(" !important");
            builder.Append(';');
        }
        return builder.ToString();
    }

    private static void AppendRule(ref ValueStringBuilder builder, CssRuleSyntax rule)
    {
        if (rule.Kind == CssRuleKind.AtRule)
        {
            builder.Append('@');
            AppendIdentifier(ref builder, rule.Name);
            if (rule.Prelude.Count != 0) builder.Append("/**/");
        }
        AppendValues(ref builder, rule.Prelude);
        if (rule.Block is { } block)
        {
            AppendContainer(ref builder, block);
        }
        else
        {
            builder.Append(';');
        }
    }

    private static void AppendValues(ref ValueStringBuilder builder, CssComponentValueList values, CssValueWork? work = null)
    {
        var stack = new List<ValueFrame> { new(values, '\0') };
        while (stack.Count != 0)
        {
            work?.Charge(1);
            ref var top = ref CollectionsMarshal.AsSpan(stack)[^1];
            if (top.Index == top.Values.Count)
            {
                if (top.Closing != '\0') builder.Append(top.Closing);
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            // A whitespace token already separates its neighbors. In particular, url( "x")
            // must keep the quote as the next non-whitespace character after url(.
            if (top.Index != 0 && !IsWhitespace(top.Values[top.Index - 1]) &&
                !IsWhitespace(top.Values[top.Index])) builder.Append("/**/");
            var value = top.Values[top.Index++];
            if (value.Kind == CssComponentKind.Token)
            {
                AppendToken(ref builder, value.Token, work);
                continue;
            }

            var closing = AppendContainerOpening(ref builder, value, work);
            stack.Add(new ValueFrame(value.Values, closing));
        }
    }

    private static void AppendContainer(ref ValueStringBuilder builder, CssComponentValue value)
    {
        var closing = AppendContainerOpening(ref builder, value);
        AppendValues(ref builder, value.Values);
        builder.Append(closing);
    }

    private static bool IsWhitespace(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace;

    private static char AppendContainerOpening(ref ValueStringBuilder builder, CssComponentValue value, CssValueWork? work = null)
    {
        if (value.Kind == CssComponentKind.Function)
        {
            AppendIdentifier(ref builder, value.FunctionName, work: work);
            builder.Append('(');
            return ')';
        }

        var opening = value.OpeningDelimiter;
        builder.Append(opening);
        return opening switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            _ => throw new InvalidOperationException("Unknown CSS block delimiter.")
        };
    }

    private static void AppendToken(ref ValueStringBuilder builder, CssToken token, CssValueWork? work = null)
    {
        work?.Charge(token.NumberText.Length);
        switch (token.Kind)
        {
            case CssTokenKind.Ident:
                AppendIdentifier(ref builder, token.Text, work: work);
                break;
            case CssTokenKind.AtKeyword:
                builder.Append('@');
                AppendIdentifier(ref builder, token.Text, work: work);
                break;
            case CssTokenKind.Hash:
                builder.Append('#');
                AppendIdentifier(ref builder, token.Text, allowLeadingDigit: !token.IsIdHash, work: work);
                break;
            case CssTokenKind.String:
                AppendString(ref builder, token.Text, work);
                break;
            case CssTokenKind.BadString:
                builder.Append("\"\n");
                break;
            case CssTokenKind.Url:
                builder.Append("url(");
                AppendUrl(ref builder, token.Text, work);
                builder.Append(')');
                break;
            case CssTokenKind.BadUrl:
                builder.Append("url(a b)");
                break;
            case CssTokenKind.Number:
                builder.Append(token.NumberText);
                break;
            case CssTokenKind.Percentage:
                builder.Append(token.NumberText);
                builder.Append('%');
                break;
            case CssTokenKind.Dimension:
                builder.Append(token.NumberText);
                AppendIdentifier(ref builder, token.Unit, escapeFirst: true, work: work);
                break;
            case CssTokenKind.UnicodeRange:
                work?.Charge(token.Text.Length);
                builder.Append(token.Text);
                break;
            case CssTokenKind.Whitespace:
                builder.Append(' ');
                break;
            case CssTokenKind.Cdo:
                builder.Append("<!--");
                break;
            case CssTokenKind.Cdc:
                builder.Append("-->");
                break;
            case CssTokenKind.Delim:
                if (token.Delimiter == '\\') builder.Append("\\\n");
                else builder.Append(token.Delimiter);
                break;
            case CssTokenKind.Colon:
                builder.Append(':');
                break;
            case CssTokenKind.Semicolon:
                builder.Append(';');
                break;
            case CssTokenKind.Comma:
                builder.Append(',');
                break;
            case CssTokenKind.OpenParenthesis:
                builder.Append('(');
                break;
            case CssTokenKind.CloseParenthesis:
                builder.Append(')');
                break;
            case CssTokenKind.OpenSquareBracket:
                builder.Append('[');
                break;
            case CssTokenKind.CloseSquareBracket:
                builder.Append(']');
                break;
            case CssTokenKind.OpenCurlyBracket:
                builder.Append('{');
                break;
            case CssTokenKind.CloseCurlyBracket:
                builder.Append('}');
                break;
            default:
                throw new InvalidOperationException("Unknown CSS token kind.");
        }
    }

    private static readonly SearchValues<char> IdentifierChars =
        SearchValues.Create("-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");

    // True when AppendIdentifier would copy the value unchanged.
    private static bool IsPlainIdentifier(string value)
    {
        if (value.Length == 0) return true;
        if (value[0] is >= '0' and <= '9' || value == "-" ||
            value.Length > 1 && value[0] == '-' && value[1] is >= '0' and <= '9') return false;
        var index = value.AsSpan().IndexOfAnyExcept(IdentifierChars);
        if (index < 0) return true;
        foreach (var character in value.AsSpan(index))
        {
            // Surrogates take the general path, which validates their pairing.
            if (character < '\u0080' && !IdentifierChars.Contains(character) || char.IsSurrogate(character)) return false;
        }
        return true;
    }

    private static void AppendIdentifier(ref ValueStringBuilder builder, string value, bool escapeFirst = false,
        bool allowLeadingDigit = false, CssValueWork? work = null)
    {
        for (var index = 0; index < value.Length; index++)
        {
            work?.Charge(1);
            var character = value[index];
            if (char.IsHighSurrogate(character) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                builder.Append(character);
                builder.Append(value[++index]);
                continue;
            }

            var mustEscape = escapeFirst && index == 0 || character is '\0' or >= '\u0001' and <= '\u001f' or '\u007f' ||
                !allowLeadingDigit && index == 0 && character is >= '0' and <= '9' ||
                !allowLeadingDigit && index == 1 && value[0] == '-' && character is >= '0' and <= '9' ||
                !allowLeadingDigit && index == 0 && character == '-' && value.Length == 1;
            if (mustEscape || char.IsSurrogate(character))
            {
                AppendHexEscape(ref builder, char.IsSurrogate(character) ? 0xfffd : character);
            }
            else if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or >= '\u0080')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('\\');
                builder.Append(character);
            }
        }
    }

    internal static void AppendString(ref ValueStringBuilder builder, string value, CssValueWork? work = null)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            work?.Charge(1);
            if (character is '\0' or '\n' or '\r' or '\f' or >= '\u0001' and <= '\u001f' or '\u007f')
                AppendHexEscape(ref builder, character);
            else if (character is '"' or '\\')
            {
                builder.Append('\\');
                builder.Append(character);
            }
            else builder.Append(character);
        }
        builder.Append('"');
    }

    private static void AppendUrl(ref ValueStringBuilder builder, string value, CssValueWork? work = null)
    {
        foreach (var character in value)
        {
            work?.Charge(1);
            if (character is '\0' or ' ' or '\t' or '\n' or '\r' or '\f' or >= '\u0001' and <= '\u001f' or '\u007f')
                AppendHexEscape(ref builder, character);
            else if (character is '"' or '\'' or '(' or ')' or '\\')
            {
                builder.Append('\\');
                builder.Append(character);
            }
            else builder.Append(character);
        }
    }

    private static void AppendHexEscape(ref ValueStringBuilder builder, int value)
    {
        Span<char> digits = stackalloc char[8];
        value.TryFormat(digits, out var written, "x", CultureInfo.InvariantCulture);
        builder.Append('\\');
        builder.Append(digits[..written]);
        builder.Append(' ');
    }

    private struct ValueFrame(CssComponentValueList values, char closing)
    {
        internal readonly CssComponentValueList Values = values;
        internal readonly char Closing = closing;
        internal int Index;
    }
}
