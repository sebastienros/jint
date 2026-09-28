using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Conditions;

// CSS Conditional 3 §§6, 7.5 and Conditional 4 §2. Capability queries only; no rule execution.
// https://drafts.csswg.org/css-conditional-3/#dom-css-supports
internal static class CssSupports
{
    internal static bool EvaluateDeclaration(string property, string value, CssParseOptions? options, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(value);
        work.CheckCancellation();
        work.Charge(value.Length);
        var name = CssPropertyRegistry.NormalizeName(property, work);
        var result = (name.StartsWith("--", StringComparison.Ordinal) || CssPropertyRegistry.Find(name) is not null)
            && !string.IsNullOrWhiteSpace(value);
        work.CheckCancellation();
        return result;
    }

    internal static bool EvaluateCondition(string text, CssParseOptions? options, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(text);
        work.CheckCancellation();
        var values = Parse(text, options, work);
        var result = ValidAnyValue(values, work) ? Condition(text, values, options, work) : Result.Invalid;
        if (result != Result.True)
        {
            // §7.5's implied parentheses can regroup unmatched delimiters in the
            // original input. Reparse the changed source, with the same work and limits.
            work.CheckCancellation();
            var wrapped = string.Concat("(", text, ")");
            work.Charge(wrapped.Length);
            work.CheckCancellation();
            values = Parse(wrapped, options, work);
            result = ValidAnyValue(values, work) ? Condition(wrapped, values, options, work) : Result.Invalid;
        }
        work.CheckCancellation();
        return result == Result.True;
    }

    // Unlike CSS.supports(), a rule prelude never retries with implied parentheses.
    // CSS Conditional 3 §3: invalid syntax is discarded, while valid false groups survive.
    // https://drafts.csswg.org/css-conditional-3/#at-supports
    internal static bool TryParseCondition(string source, CssComponentValueList values,
        CssParseOptions? options, CssValueWork work, out bool matches)
    {
        work.CheckCancellation();
        var result = ValidAnyValue(values, work) ? Condition(source, values, options, work) : Result.Invalid;
        work.CheckCancellation();
        matches = result == Result.True;
        return result != Result.Invalid;
    }

    private static CssComponentValueList Parse(string source, CssParseOptions? options, CssValueWork work)
    {
        using var parser = new CssSyntaxParser(source, options, work.Token, work.CheckCancellation);
        return parser.ParseComponentValues();
    }

    private static bool Declaration(string name, string source, CssComponentValueList values,
        CssParseOptions? options, CssValueWork work)
    {
        var normalized = CssPropertyRegistry.NormalizeName(name, work);
        foreach (var value in values)
        {
            work.Charge(1);
            if (Token(value, CssTokenKind.Semicolon)) return false;
        }
        return (normalized.StartsWith("--", StringComparison.Ordinal) || CssPropertyRegistry.Find(normalized) is not null)
            && Significant(values, work).Count != 0;
    }

    private static Result Condition(string source, CssComponentValueList values, CssParseOptions? options, CssValueWork work)
    {
        var stack = new Stack<Frame>();
        stack.Push(new Frame(Significant(values, work)));
        while (stack.Count != 0)
        {
            work.Charge(1);
            var frame = stack.Peek();
            if (frame.Index >= frame.Values.Count)
            {
                var result = frame.Finish();
                stack.Pop();
                if (stack.Count == 0) return result;
                stack.Peek().Accept(result);
                continue;
            }
            var item = frame.Values[frame.Index++];
            if (frame.Index > 1 && !frame.Negate && (frame.Index & 1) == 0)
            {
                var join = Ident(item, "and") ? Join.And : Ident(item, "or") ? Join.Or : Join.None;
                if (join == Join.None || frame.Join != Join.None && frame.Join != join) frame.Invalid = true;
                frame.Join = join;
                continue;
            }
            switch (item.Kind)
            {
                case CssComponentKind.Function:
                    {
                        frame.Accept(Function(source, item, options, work));
                        continue;
                    }
                case CssComponentKind.SimpleBlock when item.OpeningDelimiter == '(':
                    break;
                default:
                    {
                        frame.Accept(Result.Invalid);
                        continue;
                    }
            }
            var leaf = Operand(source, item.Values, options, work, out var nested);
            if (nested) stack.Push(new Frame(Significant(item.Values, work), generalEnclosed: true));
            else frame.Accept(leaf);
        }
        throw new InvalidOperationException();
    }

    // An operand's contents are either a declaration, a nested condition or general-enclosed.
    private static Result Operand(string source, CssComponentValueList values, CssParseOptions? options,
        CssValueWork work, out bool nested)
    {
        nested = false;
        var items = Significant(values, work);
        if (items.Count == 0) return Result.False;
        if (Token(items[0], CssTokenKind.Ident) && items.Count >= 2 && Token(items[1], CssTokenKind.Colon))
        {
            var end = items.Count;
            if (end >= 4 && Ident(items[end - 1], "important") && Delim(items[end - 2], '!')) end -= 2;
            var retained = new List<CssComponentValue>();
            // Keep whitespace within the value: numeric/math grammars observe token boundaries.
            var startOffset = items[1].Span.Start + items[1].Span.Length;
            var endOffset = end > 2 ? items[end - 1].Span.Start + items[end - 1].Span.Length : startOffset;
            foreach (var value in values)
            {
                work.Charge(1);
                if (value.Span.Start >= startOffset && value.Span.Start < endOffset) retained.Add(value);
            }
            var accepted = items[0].Token.Text != "--" && Declaration(items[0].Token.Text, source,
                new CssComponentValueList(retained.ToArray()), options, work);
            return accepted ? Result.True : Result.False;
        }
        if (items[0].Kind is CssComponentKind.Function or CssComponentKind.SimpleBlock || Ident(items[0], "not"))
        {
            nested = true;
            return Result.Invalid;
        }
        return Result.False;
    }

    private static Result Function(string source, CssComponentValue value, CssParseOptions? options, CssValueWork work)
    {
        work.Charge(value.FunctionName.Length);
        if (!CssAscii.EqualsIgnoreCase(value.FunctionName, "selector")) return Result.False;
        try
        {
            var selector = SelectorCompiler.CompileSupports(source, value.Values, options, work);
            return SelectorMatcher.Supports(selector, work) ? Result.True : Result.False;
        }
        catch (SelectorParseException)
        {
            work.CheckCancellation();
            return Result.False;
        }
    }

    private static CssComponentValueList Significant(CssComponentValueList values, CssValueWork work)
    {
        var result = new List<CssComponentValue>();
        foreach (var value in values)
        {
            work.Charge(1);
            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace) result.Add(value);
        }
        return new(result.ToArray());
    }

    private static bool ValidAnyValue(CssComponentValueList values, CssValueWork work)
    {
        var pending = new Stack<CssComponentValueList>();
        pending.Push(values);
        var valid = true;
        while (pending.TryPop(out var list))
        {
            foreach (var value in list)
            {
                work.Charge(1);
                if (value.Kind is CssComponentKind.Function or CssComponentKind.SimpleBlock) pending.Push(value.Values);
                else if (value.Token.Kind is CssTokenKind.BadString or CssTokenKind.BadUrl or
                    CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or CssTokenKind.CloseCurlyBracket) valid = false;
            }
        }
        work.CheckCancellation();
        return valid;
    }

    private static bool Token(CssComponentValue value, CssTokenKind kind) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == kind;
    private static bool Ident(CssComponentValue value, string text) =>
        Token(value, CssTokenKind.Ident) && CssAscii.EqualsIgnoreCase(value.Token.Text, text);
    private static bool Delim(CssComponentValue value, char character) =>
        Token(value, CssTokenKind.Delim) && value.Token.Delimiter == character;

    private enum Result { Invalid, False, True }
    private enum Join { None, And, Or }
    private sealed class Frame
    {
        internal Frame(CssComponentValueList values, bool generalEnclosed = false)
        {
            _generalEnclosed = generalEnclosed;
            Values = values;
            Negate = values.Count > 0 && Ident(values[0], "not");
            Index = Negate ? 1 : 0;
            Invalid = Negate ? values.Count != 2 : values.Count == 0 || (values.Count & 1) == 0;
        }
        internal CssComponentValueList Values { get; }
        internal bool Negate { get; }
        internal int Index { get; set; }
        internal bool Invalid { get; set; }
        internal Join Join { get; set; }
        private readonly bool _generalEnclosed;
        private bool _value;
        private bool _hasValue;
        internal void Accept(Result result)
        {
            Invalid |= result == Result.Invalid;
            var value = result == Result.True;
            _value = !_hasValue ? value : Join == Join.And ? _value & value : _value | value;
            _hasValue = true;
        }
        internal Result Finish() => Invalid || !_hasValue ? (_generalEnclosed ? Result.False : Result.Invalid) :
            (_value != Negate ? Result.True : Result.False);
    }
}
