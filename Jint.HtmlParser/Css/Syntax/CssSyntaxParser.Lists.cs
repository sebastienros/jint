namespace Jint.HtmlParser.Css.Syntax;

internal sealed partial class CssSyntaxParser
{
    // CSS Syntax Level 3, §5.5.1. CDO/CDC are ignored only at stylesheet level.
    internal CssRuleSyntax[] ParseStyleSheet()
    {
        var values = ConsumeAllComponents();
        var rules = new List<CssRuleSyntax>();
        var index = 0;
        while (index < values.Length)
        {
            PollCancellation();
            var value = values[index];
            if (IsToken(value, CssTokenKind.Whitespace) || IsToken(value, CssTokenKind.Cdo) ||
                IsToken(value, CssTokenKind.Cdc))
            {
                index++;
                continue;
            }
            var rule = IsToken(value, CssTokenKind.AtKeyword)
                ? ConsumeAtRule(values, ref index, _sourceLength)
                : ConsumeQualifiedRule(values, ref index, _sourceLength, nested: false);
            if (rule is not null) rules.Add(rule);
        }
        var result = Copy(rules);
        CheckCancellation();
        return result;
    }

    // §5.4.5 and §5.5.5: parse the mixed contents first, then project declarations.
    internal CssDeclarationSyntax[] ParseDeclarationList()
    {
        // §5.5.5 returns at the first top-level }, leaving subsequent input untouched.
        var values = ConsumeAllComponents(stopAtCloseCurly: true);
        var closed = Current.Kind == CssTokenKind.CloseCurlyBracket;
        var contents = ConsumeBlockContents(values,
            closed ? Current.Span.Start : _sourceLength, closed);
        var declarations = new List<CssDeclarationSyntax>();
        foreach (var item in contents)
        {
            PollCancellation();
            if (item.Kind == CssBlockItemKind.Rule)
            {
                if (item.Rule.Kind == CssRuleKind.AtRule)
                {
                    Report("css/discarded-at-rule-in-declaration-list", item.Rule.Span.Start);
                }
                continue;
            }
            foreach (var declaration in item.Declarations)
            {
                PollCancellation();
                declarations.Add(declaration);
            }
        }
        var result = Copy(declarations);
        CheckCancellation();
        return result;
    }

    // CSS Animations 1 §3 consumes a qualified-rule-list, not mixed style-block contents.
    internal CssRuleSyntax[] ParseQualifiedRuleList(CssComponentValue block)
    {
        CheckCancellation();
        var rules = new List<CssRuleSyntax>();
        var values = block.Values.AsSpan();
        var index = 0;
        var end = block.Span.Start + block.Span.Length - (block.IsClosed ? 1 : 0);
        var shared = _sharedValues;
        _sharedValues = block.Values.Storage;
        try
        {
            while (index < values.Length)
            {
                PollCancellation();
                var value = values[index];
                if (value.Kind == CssComponentKind.Token)
                {
                    switch (value.Token.Kind)
                    {
                        case CssTokenKind.Whitespace:
                            index++;
                            continue;
                        case CssTokenKind.AtKeyword:
                            // At-rules are consumed for recovery but never become keyframe children.
                            ConsumeAtRule(values, ref index, end, block.IsClosed);
                            continue;
                    }
                }
                var rule = ConsumeQualifiedRule(values, ref index, end, nested: false);
                if (rule is not null) rules.Add(rule);
            }
        }
        finally
        {
            _sharedValues = shared;
        }
        var result = Copy(rules);
        CheckCancellation();
        return result;
    }

    // §5.5.5 preserves declaration runs between rules in source order.
    internal CssBlockSyntax ParseBlockContents(CssComponentValue block)
    {
        if (block.Kind != CssComponentKind.SimpleBlock || block.OpeningDelimiter != '{' ||
            block.Span.Start < 0 || block.Span.Start >= _source.Length ||
            _source[block.Span.Start] != '{')
        {
            throw new ArgumentException("Expected a curly block from this input.", nameof(block));
        }

        CheckCancellation();
        var values = block.Values.AsSpan();
        var blockEnd = block.Span.Start + block.Span.Length;
        var closed = block.IsClosed;
        var shared = _sharedValues;
        _sharedValues = block.Values.Storage;
        try
        {
            return ConsumeBlockContents(values, closed ? blockEnd - 1 : blockEnd, closed);
        }
        finally
        {
            _sharedValues = shared;
        }
    }

    private CssBlockSyntax ConsumeBlockContents(ReadOnlySpan<CssComponentValue> values,
        int terminalOffset, bool closed)
    {
        var items = new List<CssBlockItemSyntax>();
        var declarationRun = new List<CssDeclarationSyntax>();
        var index = 0;

        while (index < values.Length)
        {
            PollCancellation();
            switch (values[index].TokenKind)
            {
                case CssTokenKind.Whitespace or CssTokenKind.Semicolon:
                    index++;
                    continue;
                case CssTokenKind.AtKeyword:
                    FlushRun();
                    items.Add(CssBlockItemSyntax.FromRule(ConsumeAtRule(values, ref index, terminalOffset, closed)));
                    continue;
            }

            var end = index;
            CssDeclarationSyntax? declaration = null;
            if (CouldStartDeclaration(values, index))
            {
                end = FindBlockDeclarationBoundary(values, index);
                var declarationEnd = end < values.Length && IsToken(values[end], CssTokenKind.Semicolon)
                    ? values[end].Span.Start : end == values.Length ? terminalOffset
                    : values[end - 1].Span.Start + values[end - 1].Span.Length;
                declaration = TryBuildDeclaration(values, index, end, declarationEnd);
            }
            if (declaration is not null)
            {
                declarationRun.Add(declaration);
                index = end < values.Length ? end + 1 : end;
                continue;
            }

            var ruleResult = ConsumeQualifiedRule(values, ref index, terminalOffset, nested: true);
            if (ruleResult is not null)
            {
                FlushRun();
                items.Add(CssBlockItemSyntax.FromRule(ruleResult));
            }
        }
        FlushRun();
        var result = new CssBlockSyntax(Copy(items));
        CheckCancellation();
        return result;

        void FlushRun()
        {
            if (declarationRun.Count == 0) return;
            items.Add(CssBlockItemSyntax.FromDeclarations(Copy(declarationRun)));
            declarationRun.Clear();
        }
    }

    private CssRuleSyntax ConsumeAtRule(ReadOnlySpan<CssComponentValue> values,
        ref int index, int terminalOffset, bool closed = false)
    {
        var first = values[index++].Token;
        var preludeStart = index;
        var end = first.Span.Start + first.Span.Length;
        while (index < values.Length)
        {
            PollCancellation();
            ref readonly var value = ref values[index];
            if (IsToken(value, CssTokenKind.Semicolon) || IsCurlyBlock(value))
            {
                CssComponentValue? block = IsCurlyBlock(value) ? value : null;
                end = value.Span.Start + value.Span.Length;
                var prelude = values[preludeStart..index++];
                return NewRule(CssRuleKind.AtRule, first.Text, prelude, block, first.Span.Start, end);
            }
            end = value.Span.Start + value.Span.Length;
            index++;
        }
        if (!closed) Report("css/unexpected-eof", terminalOffset);
        return NewRule(CssRuleKind.AtRule, first.Text, values[preludeStart..], null,
            first.Span.Start, Math.Max(end, terminalOffset));
    }

    private CssRuleSyntax? ConsumeQualifiedRule(ReadOnlySpan<CssComponentValue> values,
        ref int index, int terminalOffset, bool nested)
    {
        var preludeStart = index;
        var start = values[index].Span.Start;
        while (index < values.Length)
        {
            PollCancellation();
            ref readonly var value = ref values[index];
            if (IsCurlyBlock(value))
            {
                var prelude = values[preludeStart..index++];
                if (StartsWithCustomPropertyDeclaration(prelude))
                {
                    Report("css/discarded-custom-property-rule", start);
                    return null;
                }
                return NewRule(CssRuleKind.QualifiedRule, string.Empty, prelude, value,
                    start, value.Span.Start + value.Span.Length);
            }
            if (nested && IsToken(value, CssTokenKind.Semicolon))
            {
                index++;
                Report("css/discarded-qualified-rule", start);
                return null;
            }
            if (IsToken(value, CssTokenKind.CloseCurlyBracket))
            {
                Report("css/unexpected-closing-token", value.Span.Start);
                if (nested)
                {
                    index++;
                    return null;
                }
            }
            index++;
        }
        Report("css/expected-rule-block", terminalOffset);
        return null;
    }

    private CssRuleSyntax NewRule(CssRuleKind kind, string name,
        ReadOnlySpan<CssComponentValue> prelude, CssComponentValue? block, int start, int end) =>
        new(kind, name, List(prelude), block, new CssSourceSpan(start, end - start));

    private CssDeclarationSyntax? TryBuildDeclaration(ReadOnlySpan<CssComponentValue> values,
        int start, int end, int terminalOffset)
    {
        while (start < end && IsWhitespace(values[start])) { PollCancellation(); start++; }
        if (start >= end || !IsToken(values[start], CssTokenKind.Ident)) return null;
        var name = values[start++].Token;
        while (start < end && IsWhitespace(values[start])) { PollCancellation(); start++; }
        if (start >= end || !IsToken(values[start], CssTokenKind.Colon)) return null;
        var colon = values[start++].Token;
        while (start < end && IsWhitespace(values[start])) { PollCancellation(); start++; }
        var valueStart = start < end ? values[start].Span.Start : colon.Span.Start + colon.Span.Length;
        var declarationValues = values[start..end];
        var spanEnd = colon.Span.Start + colon.Span.Length;
        for (var cursor = declarationValues.Length - 1; cursor >= 0; cursor--)
        {
            if (!IsWhitespace(declarationValues[cursor]))
            {
                spanEnd = declarationValues[cursor].Span.Start + declarationValues[cursor].Span.Length;
                break;
            }
        }
        PollCancellation(declarationValues.Length);
        return FinalizeDeclaration(name, declarationValues, valueStart,
            terminalOffset, spanEnd, colon.Span.Start + colon.Span.Length);
    }

    private CssDeclarationSyntax? FinalizeDeclaration(CssToken name,
        ReadOnlySpan<CssComponentValue> values, int valueStart, int valueEnd, int spanEnd, int lexicalValueStart)
    {
        values = TrimTrailingWhitespace(values);
        var important = false;
        var retokenizeEnd = valueEnd;
        if (values.Length > 0 && IsIdent(values[^1], "important"))
        {
            var bang = values.Length - 2;
            while (bang >= 0 && IsWhitespace(values[bang]))
            {
                PollCancellation();
                bang--;
            }
            if (bang >= 0 && IsDelim(values[bang], '!'))
            {
                retokenizeEnd = values[bang].Span.Start;
                values = TrimTrailingWhitespace(values[..bang]);
                important = true;
            }
        }
        if (!name.Text.StartsWith("--", StringComparison.Ordinal) &&
            HasMixedTopLevelBrace(values)) return null;
        if (CssAscii.EqualsIgnoreCase(name.Text, "unicode-range"))
        {
            values = TrimTrailingWhitespace(RetokenizeUnicodeRangeValue(valueStart, retokenizeEnd));
        }
        var components = List(values);
        var lexicalValue = new CssSourceSpan(lexicalValueStart, retokenizeEnd - lexicalValueStart);
        return new CssDeclarationSyntax(name.Text, components, important,
            new CssSourceSpan(name.Span.Start, spanEnd - name.Span.Start),
            lexicalValue,
            TrimLexicalBoundaryWhitespace(lexicalValueStart, retokenizeEnd, components),
            DeclarationValueTermination(components, lexicalValue));
    }

    // Only a value running to the end of the input can need terminators.
    private string DeclarationValueTermination(CssComponentValueList components, CssSourceSpan lexicalValue)
    {
        if ((long) lexicalValue.Start + lexicalValue.Length == _sourceLength)
        {
            return ValueTermination(components, lexicalValue, new Values.CssValueWork(_cancellationToken));
        }
        _cancellationToken.ThrowIfCancellationRequested();
        return string.Empty;
    }

    // Comments occupy source gaps, not tokens. Trim only whitespace tokens touching the
    // boundary; an unterminated comment's trailing spaces are part of the comment.
    internal CssSourceSpan TrimLexicalBoundaryWhitespace(int start, int end, CssComponentValueList components)
    {
        var firstComponent = 0;
        var lastComponent = components.Count - 1;
        while (firstComponent <= lastComponent && IsWhitespace(components[firstComponent]))
        { PollCancellation(); firstComponent++; }
        while (lastComponent >= firstComponent && IsWhitespace(components[lastComponent]))
        { PollCancellation(); lastComponent--; }
        var leftLimit = firstComponent > lastComponent ? end : components[firstComponent].Span.Start;
        var rightLimit = firstComponent > lastComponent ? start : components[lastComponent].Span.Start +
            components[lastComponent].Span.Length;
        var first = TokenAtOrAfter(start);
        while (first < _tokenCount && _tokens[first].Kind == CssTokenKind.Whitespace &&
               _tokens[first].Span.Start == start && start < leftLimit && start < end)
        {
            PollCancellation();
            start += _tokens[first++].Span.Length;
        }
        var last = TokenAtOrAfter(end) - 1;
        while (last >= 0 && _tokens[last].Kind == CssTokenKind.Whitespace &&
               _tokens[last].Span.Start + _tokens[last].Span.Length == end && end > rightLimit && end > start)
        {
            PollCancellation();
            end = _tokens[last--].Span.Start;
        }
        return new CssSourceSpan(start, end - start);
    }

    private int TokenAtOrAfter(int offset)
    {
        var lo = 0;
        var hi = _tokenCount;
        // Declarations are trimmed in source order, so the answer is usually just past the last one.
        var hint = Math.Min(_tokenHint, hi);
        if (hint < hi && _tokens[hint].Span.Start < offset)
        {
            lo = hint + 1;
            var probeEnd = Math.Min(hi, lo + 8);
            for (; lo < probeEnd; lo++)
            {
                if (_tokens[lo].Span.Start >= offset) return _tokenHint = lo;
            }
        }
        else if (hint == 0 || _tokens[hint - 1].Span.Start < offset)
        {
            return hint;
        }
        else
        {
            hi = hint;
        }
        while (lo < hi)
        {
            PollCancellation();
            var mid = lo + ((hi - lo) >> 1);
            if (_tokens[mid].Span.Start < offset) lo = mid + 1;
            else hi = mid;
        }
        return _tokenHint = lo;
    }

    private bool CouldStartDeclaration(ReadOnlySpan<CssComponentValue> values, int start)
    {
        while (start < values.Length && IsToken(values[start], CssTokenKind.Whitespace))
        {
            PollCancellation();
            start++;
        }
        if (start >= values.Length || !IsToken(values[start], CssTokenKind.Ident)) return false;
        start++;
        while (start < values.Length && IsToken(values[start], CssTokenKind.Whitespace))
        {
            PollCancellation();
            start++;
        }
        return start < values.Length && IsToken(values[start], CssTokenKind.Colon);
    }

    private int FindBlockDeclarationBoundary(ReadOnlySpan<CssComponentValue> values, int start)
    {
        var custom = values[start].Token.Text.StartsWith("--", StringComparison.Ordinal);
        var cursor = start + 1;
        while (cursor < values.Length && IsToken(values[cursor], CssTokenKind.Whitespace))
        {
            PollCancellation();
            cursor++;
        }
        cursor++; // colon, checked by CouldStartDeclaration
        var nonWhitespace = 0;
        var braceSeen = false;
        var bangAfterBrace = false;
        for (; cursor < values.Length; cursor++)
        {
            PollCancellation();
            var value = values[cursor];
            if (IsToken(value, CssTokenKind.Semicolon)) return cursor;
            if (IsToken(value, CssTokenKind.Whitespace)) continue;
            if (!custom && IsCurlyBlock(value))
            {
                if (nonWhitespace > 0) return cursor + 1;
                braceSeen = true;
            }
            else if (!custom && braceSeen)
            {
                if (nonWhitespace == 1 && IsDelim(value, '!')) bangAfterBrace = true;
                else if (!bangAfterBrace || !IsIdent(value, "important") || nonWhitespace > 2)
                {
                    return cursor + 1;
                }
            }
            nonWhitespace++;
        }
        return cursor;
    }

    private static bool IsToken(in CssComponentValue value, CssTokenKind kind) => value.TokenKind == kind;

    private static bool IsCurlyBlock(in CssComponentValue value) =>
        value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '{';

    private T[] Copy<T>(List<T> values)
    {
        PollCancellation(values.Count);
        return values.ToArray();
    }
}
