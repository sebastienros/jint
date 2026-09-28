using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css;

internal static partial class CssParser
{
    // CSS Syntax 3 §5.4.1: only rule boundaries are interpreted by this first layer.
    // https://drafts.csswg.org/css-syntax/#consume-list-of-rules
    internal static CssRawRule[] ParseRuleList(string source, CssValueWork work, CssParseOptions? options = null) =>
        ParseRuleList(CssSourceText.From(source), work, options);

    internal static CssRawRule[] ParseRuleList(CssSourceText input, CssValueWork work, CssParseOptions? options = null)
    {
        work.CheckCancellation();
        var limits = options?.Limits ?? ParseLimits.Unbounded;
        if (limits.MaxInputCharacters > 0 && input.Span.Length > limits.MaxInputCharacters)
            throw new ParseLimitException(ParseLimitKind.InputCharacters, limits.MaxInputCharacters, input.Span.Length);
        options?.Diagnostics?.Clear();
        var scanner = new RuleScanner(input, limits, options?.Diagnostics, work);
        var result = new List<CssRawRule>();
        while (scanner.Next() is { } rule) result.Add(rule);
        work.Charge(result.Count);
        var rules = result.ToArray();
        work.CheckCancellation();
        return rules;
    }

    internal static CssRawRule ParseMediaRule(string source, CssValueWork work, CssParseOptions? options = null)
    {
        var rules = ParseRuleList(source, work, options);
        if (rules.Length != 1 || rules[0].Kind != CssRuleKind.AtRule ||
            !CssAscii.EqualsIgnoreCase(rules[0].Name, "media") || rules[0].Body is null)
            throw new CssParseException("css/expected-media-rule", 0);
        RequireTrivia(CssSourceText.From(source).Slice(0, rules[0].Text.Span.Start));
        RequireTrivia(CssSourceText.From(source).Slice(rules[0].Text.End, source.Length));
        work.CheckCancellation();
        return rules[0];

        void RequireTrivia(CssSourceText input)
        {
            var tokenizer = new CssTokenizer(input.Text, options?.Limits.MaxTokenCharacters ?? 0, null,
                work.Token, baseOffset: input.Span.Start, checkpoint: work.CheckCancellation);
            CssToken token;
            while ((token = tokenizer.Next()).Kind != CssTokenKind.None)
                if (token.Kind != CssTokenKind.Whitespace)
                    throw new CssParseException("css/expected-media-rule", token.Span.Start);
        }
    }

    // Mixed block contents have declaration-vs-selector recovery distinct from a stylesheet.
    // This explicit second-stage parse returns raw child slices, never retained component trees.
    internal static CssRawRule[] ParseRuleList(CssRuleBody body, CssValueWork work, CssParseOptions? options = null)
    {
        var input = body.Text;
        var parser = new CssSyntaxParser(input, options, work.Token, work.CheckCancellation);
        var values = parser.ParseComponentValues();
        var block = CssComponentValue.FromContainer(CssComponentKind.SimpleBlock,
            new CssSourceSpan(input.Span.Start - 1, input.Span.Length + (body.IsClosed ? 2 : 1)),
            null, '{', values, body.IsClosed);
        var rules = new List<CssRawRule>();
        if (body.Kind == CssRuleBodyKind.Keyframes)
        {
            foreach (var rule in parser.ParseQualifiedRuleList(block))
            {
                work.Charge(1);
                rules.Add(Raw(input.Source, rule));
            }
        }
        else
        {
            foreach (var item in parser.ParseBlockContents(block))
            {
                work.Charge(1);
                if (item.Kind == CssBlockItemKind.Rule) rules.Add(Raw(input.Source, item.Rule));
            }
        }
        work.Charge(rules.Count);
        var result = rules.ToArray();
        work.CheckCancellation();
        return result;
    }

    private static CssRawRule Raw(string source, CssRuleSyntax rule)
    {
        var text = new CssSourceText(source, rule.Span);
        var preludeStart = rule.Kind == CssRuleKind.QualifiedRule ? rule.Span.Start
            : rule.Prelude.Count != 0 ? rule.Prelude[0].Span.Start
            : rule.Block?.Span.Start ?? text.End;
        var preludeEnd = rule.Block?.Span.Start ??
            (text.End > preludeStart && source[text.End - 1] == ';' ? text.End - 1 : text.End);
        var body = rule.Block is { } block
            ? text.Slice(block.Span.Start + 1, block.Span.Start + block.Span.Length - (block.IsClosed ? 1 : 0))
            : (CssSourceText?) null;
        return new(rule.Kind, rule.Name, text, text.Slice(preludeStart, preludeEnd), body, rule.Block?.IsClosed ?? false);
    }

    private sealed class RuleScanner
    {
        private readonly CssSourceText _input;
        private readonly ParseLimits _limits;
        private readonly ParseDiagnosticCollector? _diagnostics;
        private readonly CssValueWork _work;
        private readonly CssTokenizer _tokenizer;
        private CssToken _current;

        internal RuleScanner(CssSourceText input, ParseLimits limits, ParseDiagnosticCollector? diagnostics, CssValueWork work)
        {
            _input = input;
            _limits = limits;
            _diagnostics = diagnostics;
            _work = work;
            _tokenizer = new CssTokenizer(input.Text, limits.MaxTokenCharacters, diagnostics, work.Token,
                baseOffset: input.Span.Start, checkpoint: work.CheckCancellation);
            Advance();
        }

        private void Advance() => _current = _tokenizer.Next();

        internal CssRawRule? Next()
        {
            while (true)
            {
                while (_current.Kind is CssTokenKind.Whitespace or CssTokenKind.Cdo or CssTokenKind.Cdc) Advance();
                if (_current.Kind == CssTokenKind.None) return null;
                var first = _current;
                var atRule = first.Kind == CssTokenKind.AtKeyword;
                var preludeStart = atRule ? first.Span.Start + first.Span.Length : first.Span.Start;
                if (atRule) Advance();
                var significant = 0;
                var customName = false;
                var customDeclaration = false;
                while (true)
                {
                    var token = _current;
                    switch (token.Kind)
                    {
                        case CssTokenKind.None:
                            Report(atRule ? "css/unexpected-eof" : "css/expected-rule-block", _input.End);
                            return atRule ? Rule(_input.End, _input.End, null, false) : null;
                        case CssTokenKind.Semicolon when atRule:
                            Advance();
                            return Rule(token.Span.Start + token.Span.Length, token.Span.Start, null, false);
                        case CssTokenKind.OpenCurlyBracket:
                            var (end, closed) = ConsumeContainer();
                            if (customDeclaration)
                            {
                                Report("css/discarded-custom-property-rule", first.Span.Start);
                                break;
                            }
                            return Rule(end, token.Span.Start,
                                _input.Slice(token.Span.Start + 1, end - (closed ? 1 : 0)), closed);
                        default:
                            if (token.Kind != CssTokenKind.Whitespace)
                            {
                                if (significant == 0) customName = token.Kind == CssTokenKind.Ident &&
                                    token.Text.StartsWith("--", StringComparison.Ordinal);
                                if (significant == 1) customDeclaration = !atRule && customName && token.Kind == CssTokenKind.Colon;
                                significant++;
                            }
                            if (Closer(token.Kind) != CssTokenKind.None) ConsumeContainer();
                            else
                            {
                                if (IsClosing(token.Kind))
                                {
                                    Report("css/unmatched-closing-token", token.Span.Start);
                                    if (!atRule && token.Kind == CssTokenKind.CloseCurlyBracket)
                                        Report("css/unexpected-closing-token", token.Span.Start);
                                }
                                Advance();
                            }
                            continue;
                    }
                    break;
                }

                CssRawRule Rule(int end, int preludeEnd, CssSourceText? body, bool closed) =>
                    new(atRule ? CssRuleKind.AtRule : CssRuleKind.QualifiedRule, atRule ? first.Text : "",
                        _input.Slice(first.Span.Start, end), _input.Slice(preludeStart, preludeEnd), body, closed);
            }
        }

        private (int End, bool Closed) ConsumeContainer()
        {
            var closers = new Stack<CssTokenKind>();
            Push(_current.Kind);
            Advance();
            while (true)
            {
                _work.Charge(1);
                var token = _current;
                if (token.Kind == CssTokenKind.None)
                {
                    foreach (var _ in closers) { _work.Charge(1); Report("css/unexpected-eof", _input.End); }
                    return (_input.End, false);
                }
                if (token.Kind == closers.Peek())
                {
                    closers.Pop();
                    Advance();
                    if (closers.Count == 0) return (token.Span.Start + token.Span.Length, true);
                }
                else if (Closer(token.Kind) != CssTokenKind.None)
                {
                    Push(token.Kind);
                    Advance();
                }
                else
                {
                    if (IsClosing(token.Kind)) Report("css/unmatched-closing-token", token.Span.Start);
                    Advance();
                }
            }

            void Push(CssTokenKind kind)
            {
                var depth = closers.Count + 1;
                if (_limits.MaxNestingDepth > 0 && depth > _limits.MaxNestingDepth)
                    throw new ParseLimitException(ParseLimitKind.NestingDepth, _limits.MaxNestingDepth, depth);
                closers.Push(Closer(kind));
            }
        }

        private void Report(string code, int offset) => _diagnostics?.Add(code, offset);
        private static bool IsClosing(CssTokenKind kind) => kind is CssTokenKind.CloseParenthesis or
            CssTokenKind.CloseSquareBracket or CssTokenKind.CloseCurlyBracket;
        private static CssTokenKind Closer(CssTokenKind kind) => kind switch
        {
            CssTokenKind.Function or CssTokenKind.OpenParenthesis => CssTokenKind.CloseParenthesis,
            CssTokenKind.OpenSquareBracket => CssTokenKind.CloseSquareBracket,
            CssTokenKind.OpenCurlyBracket => CssTokenKind.CloseCurlyBracket,
            _ => CssTokenKind.None
        };
    }
}
