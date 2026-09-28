namespace Jint.HtmlParser.Css.Syntax;

// CSS Syntax Level 3, §5.4–5.5: https://drafts.csswg.org/css-syntax/#parser-algorithms
internal sealed partial class CssSyntaxParser
{
    private readonly List<CssToken> _tokens;
    private readonly string _source;
    private readonly ParseDiagnosticCollector? _diagnostics;
    private readonly int _maxTokenCharacters;
    private readonly int _maxNestingDepth;
    private readonly CancellationToken _cancellationToken;
    private readonly Action? _checkpoint;
    private readonly int _sourceLength;
    private readonly string _eofRecoverySuffix = string.Empty;
    private int _index;
    private int _work;

    internal CssSyntaxParser(string source, CssParseOptions? options, CancellationToken cancellationToken,
        Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _checkpoint = checkpoint;
        _tokens = new List<CssToken>();
        cancellationToken.ThrowIfCancellationRequested();
        checkpoint?.Invoke();
        var limits = options?.Limits ?? ParseLimits.Unbounded;
        _diagnostics = options?.Diagnostics;
        _diagnostics?.Clear();
        _maxNestingDepth = limits.MaxNestingDepth;
        _maxTokenCharacters = limits.MaxTokenCharacters;
        _cancellationToken = cancellationToken;
        _sourceLength = source.Length;
        if (limits.MaxInputCharacters > 0 && source.Length > limits.MaxInputCharacters)
        {
            throw new ParseLimitException(ParseLimitKind.InputCharacters,
                limits.MaxInputCharacters, source.Length);
        }

        var tokenizer = new CssTokenizer(source, limits.MaxTokenCharacters, _diagnostics, cancellationToken, checkpoint: checkpoint);
        while (true)
        {
            var token = tokenizer.Next();
            if (token.Kind == CssTokenKind.None) break;
            _tokens.Add(token);
        }
        _eofRecoverySuffix = tokenizer.EofRecoverySuffix;
        _checkpoint?.Invoke();
    }

    internal string ValueTermination(CssComponentValueList components, CssSourceSpan retainedSpan,
        Values.CssValueWork work) =>
        CssValueTermination.Create(components, retainedSpan, _sourceLength, _eofRecoverySuffix, work);

    private CssSyntaxParser(List<CssToken> tokens, int sourceLength, int maxTokenCharacters,
        int maxNestingDepth, ParseDiagnosticCollector? diagnostics, Action? checkpoint, CancellationToken cancellationToken)
    {
        _source = string.Empty;
        _checkpoint = checkpoint;
        _tokens = tokens;
        _sourceLength = sourceLength;
        _maxTokenCharacters = maxTokenCharacters;
        _maxNestingDepth = maxNestingDepth;
        _diagnostics = diagnostics;
        _cancellationToken = cancellationToken;
    }

    internal CssComponentValueList ParseComponentValues()
    {
        var result = List(ConsumeAllComponents());
        CheckCancellation();
        return result;
    }

    private List<CssComponentValue> ConsumeAllComponents()
    {
        CheckCancellation();
        var values = new List<CssComponentValue>();
        while (Current.Kind != CssTokenKind.None) values.Add(ConsumeComponent());
        return values;
    }

    internal CssComponentValue ParseComponentValue()
    {
        CheckCancellation();
        SkipWhitespace();
        if (Current.Kind == CssTokenKind.None) throw Error("css/expected-component", _sourceLength);
        var value = ConsumeComponent();
        SkipWhitespace();
        if (Current.Kind != CssTokenKind.None) throw Error("css/trailing-input", Current.Span.Start);
        CheckCancellation();
        return value;
    }

    internal CssRuleSyntax ParseRule()
    {
        CheckCancellation();
        SkipWhitespace();
        if (Current.Kind == CssTokenKind.None) throw Error("css/expected-rule", _sourceLength);
        var first = Current;
        var isAtRule = first.Kind == CssTokenKind.AtKeyword;
        if (isAtRule) _index++;
        var prelude = new List<CssComponentValue>();
        CssComponentValue? block = null;
        var end = isAtRule ? first.Span.Start + first.Span.Length : first.Span.Start;
        while (true)
        {
            CheckCancellation();
            var token = Current;
            switch (token.Kind)
            {
                case CssTokenKind.None:
                    if (!isAtRule) throw Error("css/expected-rule-block", _sourceLength);
                    Report("css/unexpected-eof", _sourceLength);
                    end = _sourceLength;
                    break;
                case CssTokenKind.Semicolon when isAtRule:
                    _index++;
                    end = token.Span.Start + token.Span.Length;
                    break;
                case CssTokenKind.OpenCurlyBracket:
                    if (!isAtRule && StartsWithCustomPropertyDeclaration(prelude))
                    {
                        throw Error("css/custom-property-is-not-rule", first.Span.Start);
                    }
                    block = ConsumeComponent();
                    end = block.Value.Span.Start + block.Value.Span.Length;
                    break;
                default:
                    var value = ConsumeComponent();
                    prelude.Add(value);
                    end = value.Span.Start + value.Span.Length;
                    continue;
            }
            break;
        }
        SkipWhitespace();
        if (Current.Kind != CssTokenKind.None) throw Error("css/trailing-input", Current.Span.Start);
        var result = new CssRuleSyntax(isAtRule ? CssRuleKind.AtRule : CssRuleKind.QualifiedRule,
            isAtRule ? first.Text : string.Empty, List(prelude), block,
            new CssSourceSpan(first.Span.Start, end - first.Span.Start));
        CheckCancellation();
        return result;
    }

    internal CssDeclarationSyntax ParseDeclaration()
    {
        CheckCancellation();
        SkipWhitespace();
        var first = Current;
        if (first.Kind != CssTokenKind.Ident)
        {
            throw Error("css/expected-declaration-name", first.Kind == CssTokenKind.None ? _sourceLength : first.Span.Start);
        }
        _index++;
        SkipWhitespace();
        if (Current.Kind != CssTokenKind.Colon)
        {
            throw Error("css/expected-colon", Current.Kind == CssTokenKind.None ? _sourceLength : Current.Span.Start);
        }
        var colon = Current;
        _index++;
        SkipWhitespace();
        var valueStart = Current.Kind == CssTokenKind.None ? _sourceLength : Current.Span.Start;
        var values = new List<CssComponentValue>();
        var end = colon.Span.Start + colon.Span.Length;
        while (Current.Kind is not (CssTokenKind.None or CssTokenKind.Semicolon))
        {
            CheckCancellation();
            var value = ConsumeComponent();
            values.Add(value);
            if (!IsWhitespace(value)) end = value.Span.Start + value.Span.Length;
        }
        var valueEnd = Current.Kind == CssTokenKind.None ? _sourceLength : Current.Span.Start;
        if (Current.Kind == CssTokenKind.Semicolon) _index++;
        SkipWhitespace();
        if (Current.Kind != CssTokenKind.None) throw Error("css/trailing-input", Current.Span.Start);

        var result = FinalizeDeclaration(first, values, valueStart, valueEnd, end,
            colon.Span.Start + colon.Span.Length) ??
            throw Error("css/mixed-brace-declaration-value", first.Span.Start);
        CheckCancellation();
        return result;
    }

    private CssComponentValue ConsumeComponent()
    {
        PollCancellation();
        var token = Current;
        if (!OpensContainer(token.Kind))
        {
            if (IsClosing(token.Kind)) Report("css/unmatched-closing-token", token.Span.Start);
            _index++;
            return CssComponentValue.FromToken(token);
        }

        var stack = new List<Frame>();
        Push(token);
        _index++;
        while (stack.Count > 0)
        {
            PollCancellation();
            token = Current;
            var top = stack[^1];
            if (token.Kind == CssTokenKind.None || token.Kind == top.ClosingKind)
            {
                var end = token.Kind == CssTokenKind.None ? _sourceLength : token.Span.Start + token.Span.Length;
                if (token.Kind == CssTokenKind.None) Report("css/unexpected-eof", _sourceLength);
                else _index++;
                var completed = CssComponentValue.FromContainer(top.Kind,
                    new CssSourceSpan(top.Start, end - top.Start), top.FunctionName,
                    top.OpeningDelimiter, List(top.Values), token.Kind != CssTokenKind.None);
                stack.RemoveAt(stack.Count - 1);
                if (stack.Count == 0) return completed;
                stack[^1].Values.Add(completed);
                continue;
            }
            if (OpensContainer(token.Kind))
            {
                Push(token);
                _index++;
                continue;
            }
            if (IsClosing(token.Kind)) Report("css/unmatched-closing-token", token.Span.Start);
            top.Values.Add(CssComponentValue.FromToken(token));
            _index++;
        }
        throw new InvalidOperationException();

        void Push(CssToken opening)
        {
            var depth = stack.Count + 1;
            if (_maxNestingDepth > 0 && depth > _maxNestingDepth)
            {
                throw new ParseLimitException(ParseLimitKind.NestingDepth, _maxNestingDepth, depth);
            }
            stack.Add(new Frame(opening));
        }
    }

    private CssToken Current => _index < _tokens.Count ? _tokens[_index] : default;

    private void SkipWhitespace()
    {
        while (Current.Kind == CssTokenKind.Whitespace)
        {
            PollCancellation();
            _index++;
        }
    }

    private void CheckCancellation()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _checkpoint?.Invoke();
        _cancellationToken.ThrowIfCancellationRequested();
    }

    private void PollCancellation()
    {
        if ((++_work & 255) == 0) CheckCancellation();
    }

    private List<CssComponentValue> RetokenizeUnicodeRangeValue(int start, int end)
    {
        var tokenizer = new CssTokenizer(_source.Substring(start, end - start),
            _maxTokenCharacters, _diagnostics, _cancellationToken,
            allowUnicodeRanges: true, baseOffset: start, checkpoint: _checkpoint);
        var tokens = new List<CssToken>();
        while (true)
        {
            var token = tokenizer.Next();
            if (token.Kind == CssTokenKind.None) break;
            tokens.Add(token);
        }
        var parser = new CssSyntaxParser(tokens, end, _maxTokenCharacters,
            _maxNestingDepth, _diagnostics, _checkpoint, _cancellationToken);
        return parser.ConsumeAllComponents();
    }

    private void TrimTrailingWhitespace(List<CssComponentValue> values)
    {
        while (values.Count > 0 && IsWhitespace(values[^1]))
        {
            PollCancellation();
            values.RemoveAt(values.Count - 1);
        }
    }

    private static bool IsWhitespace(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace;

    private static bool IsDelim(CssComponentValue value, char delimiter) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Delim && value.Token.Delimiter == delimiter;

    private static bool IsIdent(CssComponentValue value, string text) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Ident &&
        CssAscii.EqualsIgnoreCase(value.Token.Text, text);

    private bool StartsWithCustomPropertyDeclaration(List<CssComponentValue> prelude)
    {
        CssComponentValue? first = null;
        foreach (var value in prelude)
        {
            PollCancellation();
            if (IsWhitespace(value)) continue;
            if (first is null)
            {
                first = value;
                continue;
            }
            return first.Value.Kind == CssComponentKind.Token &&
                first.Value.Token.Kind == CssTokenKind.Ident &&
                first.Value.Token.Text.StartsWith("--", StringComparison.Ordinal) &&
                value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Colon;
        }
        return false;
    }

    private bool HasMixedTopLevelBrace(List<CssComponentValue> values)
    {
        var nonWhitespace = 0;
        var hasBrace = false;
        foreach (var value in values)
        {
            PollCancellation();
            if (IsWhitespace(value)) continue;
            nonWhitespace++;
            hasBrace |= value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '{';
            if (hasBrace && nonWhitespace > 1) return true;
        }
        return false;
    }

    private static bool OpensContainer(CssTokenKind kind) => kind is CssTokenKind.Function or
        CssTokenKind.OpenParenthesis or CssTokenKind.OpenSquareBracket or CssTokenKind.OpenCurlyBracket;

    private static bool IsClosing(CssTokenKind kind) => kind is CssTokenKind.CloseParenthesis or
        CssTokenKind.CloseSquareBracket or CssTokenKind.CloseCurlyBracket;

    private CssComponentValueList List(List<CssComponentValue> values)
    {
        var copy = new CssComponentValue[values.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            PollCancellation();
            copy[index] = values[index];
        }
        return new CssComponentValueList(copy);
    }

    private CssParseException Error(string code, int offset)
    {
        CheckCancellation();
        Report(code, offset);
        return new CssParseException(code, offset);
    }
    private void Report(string code, int offset) => _diagnostics?.Add(code, offset);

    private sealed class Frame
    {
        internal Frame(CssToken opening)
        {
            Start = opening.Span.Start;
            Kind = opening.Kind == CssTokenKind.Function ? CssComponentKind.Function : CssComponentKind.SimpleBlock;
            FunctionName = opening.Kind == CssTokenKind.Function ? opening.Text : null;
            OpeningDelimiter = opening.Delimiter;
            ClosingKind = opening.Kind switch
            {
                CssTokenKind.OpenSquareBracket => CssTokenKind.CloseSquareBracket,
                CssTokenKind.OpenCurlyBracket => CssTokenKind.CloseCurlyBracket,
                _ => CssTokenKind.CloseParenthesis
            };
        }

        internal int Start { get; }
        internal CssComponentKind Kind { get; }
        internal string? FunctionName { get; }
        internal char OpeningDelimiter { get; }
        internal CssTokenKind ClosingKind { get; }
        internal List<CssComponentValue> Values { get; } = new();
    }
}
