using System.Buffers;

namespace Jint.HtmlParser.Css.Syntax;

// CSS Syntax Level 3, §5.4–5.5: https://drafts.csswg.org/css-syntax/#parser-algorithms
// Token and pending-value storage is pooled; dispose the parser once its results are built.
internal sealed partial class CssSyntaxParser : IDisposable
{
    private CssToken[] _tokens;
    private readonly int _tokenCount;
    private Frame[] _frames = [];
    private CssComponentValue[] _pending = [];
    private int _pendingCount;
    private int _tokenHint;
    private int _pendingHighWater;
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
        Action? checkpoint = null) : this(CssSourceText.From(source), options, cancellationToken, checkpoint)
    {
    }

    internal CssSyntaxParser(CssSourceText input, CssParseOptions? options, CancellationToken cancellationToken,
        Action? checkpoint = null)
    {
        _source = input.Source;
        _checkpoint = checkpoint;
        _tokens = [];
        cancellationToken.ThrowIfCancellationRequested();
        checkpoint?.Invoke();
        var limits = options?.Limits ?? ParseLimits.Unbounded;
        _diagnostics = options?.Diagnostics;
        _diagnostics?.Clear();
        _maxNestingDepth = limits.MaxNestingDepth;
        _maxTokenCharacters = limits.MaxTokenCharacters;
        _cancellationToken = cancellationToken;
        _sourceLength = input.End;
        if (limits.MaxInputCharacters > 0 && input.Span.Length > limits.MaxInputCharacters)
        {
            throw new ParseLimitException(ParseLimitKind.InputCharacters,
                limits.MaxInputCharacters, input.Span.Length);
        }

        var tokenizer = new CssTokenizer(input.Text, limits.MaxTokenCharacters, _diagnostics, cancellationToken,
            baseOffset: input.Span.Start, checkpoint: checkpoint);
        // Most stylesheets average several characters per token; growth covers the rest.
        (_tokens, _tokenCount) = Tokenize(tokenizer, input.Span.Length / 6);
        _eofRecoverySuffix = tokenizer.EofRecoverySuffix;
        _checkpoint?.Invoke();
    }

    internal string ValueTermination(CssComponentValueList components, CssSourceSpan retainedSpan,
        Values.CssValueWork work) =>
        CssValueTermination.Create(components, retainedSpan, _sourceLength, _eofRecoverySuffix, work);

    private CssSyntaxParser(CssToken[] tokens, int tokenCount, int sourceLength, int maxTokenCharacters,
        int maxNestingDepth, ParseDiagnosticCollector? diagnostics, Action? checkpoint, CancellationToken cancellationToken)
    {
        _source = string.Empty;
        _checkpoint = checkpoint;
        _tokens = tokens;
        _tokenCount = tokenCount;
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

    // The returned span lives on the pending stack: it stays valid until the next component is consumed.
    private ReadOnlySpan<CssComponentValue> ConsumeAllComponents(bool stopAtCloseCurly = false)
    {
        CheckCancellation();
        var start = _pendingCount;
        while (Current.Kind != CssTokenKind.None &&
               !(stopAtCloseCurly && Current.Kind == CssTokenKind.CloseCurlyBracket))
        {
            AddPending(ConsumeComponent(), ref _pendingCount);
        }
        return PendingSince(start);
    }

    private ReadOnlySpan<CssComponentValue> PendingSince(int start)
    {
        var values = _pending.AsSpan(start, _pendingCount - start);
        _pendingCount = start;
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
        var preludeStart = _pendingCount;
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
                    if (!isAtRule && StartsWithCustomPropertyDeclaration(_pending.AsSpan(preludeStart, _pendingCount - preludeStart)))
                    {
                        throw Error("css/custom-property-is-not-rule", first.Span.Start);
                    }
                    block = ConsumeComponent();
                    end = block.Value.Span.Start + block.Value.Span.Length;
                    break;
                default:
                    var value = ConsumeComponent();
                    AddPending(value, ref _pendingCount);
                    end = value.Span.Start + value.Span.Length;
                    continue;
            }
            break;
        }
        var prelude = PendingSince(preludeStart);
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
        var valuesStart = _pendingCount;
        var end = colon.Span.Start + colon.Span.Length;
        while (Current.Kind is not (CssTokenKind.None or CssTokenKind.Semicolon))
        {
            CheckCancellation();
            var value = ConsumeComponent();
            AddPending(value, ref _pendingCount);
            if (!IsWhitespace(value)) end = value.Span.Start + value.Span.Length;
        }
        var values = PendingSince(valuesStart);
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

        // Open containers share one frame stack and one pending-value stack; each container
        // owns the pending values above its frame's mark until it closes.
        var depth = 0;
        var pending = _pendingCount;
        Push(token, ref depth, pending);
        _index++;
        while (true)
        {
            PollCancellation();
            token = Current;
            ref var top = ref _frames[depth - 1];
            if (token.Kind == CssTokenKind.None || token.Kind == top.ClosingKind)
            {
                var closed = token.Kind != CssTokenKind.None;
                var end = closed ? token.Span.Start + token.Span.Length : _sourceLength;
                if (closed) _index++;
                else Report("css/unexpected-eof", _sourceLength);
                var completed = CssComponentValue.FromContainer(top.Kind,
                    new CssSourceSpan(top.Start, end - top.Start), top.FunctionName,
                    top.OpeningDelimiter, List(_pending.AsSpan(top.Mark, pending - top.Mark)), closed);
                pending = top.Mark;
                top = default;
                if (--depth == 0)
                {
                    return completed;
                }
                AddPending(completed, ref pending);
                continue;
            }
            if (OpensContainer(token.Kind))
            {
                Push(token, ref depth, pending);
                _index++;
                continue;
            }
            if (IsClosing(token.Kind)) Report("css/unmatched-closing-token", token.Span.Start);
            AddPending(CssComponentValue.FromToken(token), ref pending);
            _index++;
        }
    }

    private void Push(in CssToken opening, ref int depth, int mark)
    {
        if (_maxNestingDepth > 0 && depth + 1 > _maxNestingDepth)
        {
            throw new ParseLimitException(ParseLimitKind.NestingDepth, _maxNestingDepth, depth + 1);
        }
        if (depth == _frames.Length)
        {
            Array.Resize(ref _frames, Math.Max(8, depth * 2));
        }
        _frames[depth++] = new Frame(opening, mark);
    }

    private void AddPending(in CssComponentValue value, ref int count)
    {
        if (count == _pending.Length)
        {
            Grow(ref _pending, count);
        }
        _pending[count++] = value;
        _pendingHighWater = Math.Max(_pendingHighWater, count);
    }

    private CssToken Current => _index < _tokenCount ? _tokens[_index] : default;

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

    // Charges bulk work with the same checkpoint cadence as one poll per unit.
    private void PollCancellation(int units)
    {
        var before = _work;
        _work += units;
        for (var crossed = (_work >> 8) - (before >> 8); crossed > 0; crossed--)
        {
            CheckCancellation();
        }
    }

    private CssComponentValue[] RetokenizeUnicodeRangeValue(int start, int end)
    {
        var tokenizer = new CssTokenizer(_source.Substring(start, end - start),
            _maxTokenCharacters, _diagnostics, _cancellationToken,
            allowUnicodeRanges: true, baseOffset: start, checkpoint: _checkpoint);
        var (tokens, count) = Tokenize(tokenizer, 4);
        using var parser = new CssSyntaxParser(tokens, count, end, _maxTokenCharacters,
            _maxNestingDepth, _diagnostics, _checkpoint, _cancellationToken);
        return parser.ConsumeAllComponents().ToArray();
    }

    private static (CssToken[] Tokens, int Count) Tokenize(CssTokenizer tokenizer, int capacity)
    {
        var tokens = ArrayPool<CssToken>.Shared.Rent(Math.Max(capacity, 16));
        var count = 0;
        while (true)
        {
            var token = tokenizer.Next();
            if (token.Kind == CssTokenKind.None) return (tokens, count);
            if (count == tokens.Length) Grow(ref tokens, count);
            tokens[count++] = token;
        }
    }

    private static void Grow<T>(ref T[] array, int count)
    {
        var grown = ArrayPool<T>.Shared.Rent(Math.Max(16, count * 2));
        array.AsSpan(0, count).CopyTo(grown);
        Return(array, count);
        array = grown;
    }

    private static void Return<T>(T[] array, int used)
    {
        if (array.Length == 0) return;
        array.AsSpan(0, used).Clear();
        ArrayPool<T>.Shared.Return(array);
    }

    public void Dispose()
    {
        Return(_tokens, _tokenCount);
        _tokens = [];
        Return(_pending, _pendingHighWater);
        _pending = [];
    }

    private ReadOnlySpan<CssComponentValue> TrimTrailingWhitespace(ReadOnlySpan<CssComponentValue> values)
    {
        while (!values.IsEmpty && IsWhitespace(values[^1]))
        {
            PollCancellation();
            values = values[..^1];
        }
        return values;
    }

    private static bool IsWhitespace(in CssComponentValue value) => value.TokenKind == CssTokenKind.Whitespace;

    private static bool IsDelim(in CssComponentValue value, char delimiter) =>
        value.TokenKind == CssTokenKind.Delim && value.Token.Delimiter == delimiter;

    private static bool IsIdent(in CssComponentValue value, string text) =>
        value.TokenKind == CssTokenKind.Ident && CssAscii.EqualsIgnoreCase(value.Token.Text, text);

    private bool StartsWithCustomPropertyDeclaration(ReadOnlySpan<CssComponentValue> prelude)
    {
        CssComponentValue? first = null;
        foreach (ref readonly var value in prelude)
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

    private bool HasMixedTopLevelBrace(ReadOnlySpan<CssComponentValue> values)
    {
        var nonWhitespace = 0;
        var hasBrace = false;
        foreach (ref readonly var value in values)
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

    private CssComponentValueList List(ReadOnlySpan<CssComponentValue> values)
    {
        PollCancellation(values.Length);
        return new CssComponentValueList(values.ToArray());
    }

    private CssParseException Error(string code, int offset)
    {
        CheckCancellation();
        Report(code, offset);
        return new CssParseException(code, offset);
    }
    private void Report(string code, int offset) => _diagnostics?.Add(code, offset);

    private readonly struct Frame
    {
        internal Frame(in CssToken opening, int mark)
        {
            Start = opening.Span.Start;
            Mark = mark;
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
        internal int Mark { get; }
        internal CssComponentKind Kind { get; }
        internal string? FunctionName { get; }
        internal char OpeningDelimiter { get; }
        internal CssTokenKind ClosingKind { get; }
    }
}
