namespace Jint.HtmlParser.Css.Values.References;

/// <summary>Decoded argument grammar after early replacement; CSS Values 5 Appendix A.</summary>
internal static class CssSubstitutionArguments
{
    internal static bool IsCustomName(string? name) => name is { Length: >= 3 } &&
        name[0] == '-' && name[1] == '-';

    internal static string RequireCustomName(string name) => IsCustomName(name)
        ? name : throw new ArgumentException("A decoded custom-property name is required.", nameof(name));

    internal static string RequireEnvironmentName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || CssWideKeywords.Recognize(name) != CssWideKeyword.None ||
            name.Equals("default", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A nonreserved decoded environment name is required.", nameof(name));
        return name;
    }

    internal static uint Hash(string value, CssValueWork work)
    {
        uint hash = 2166136261;
        for (var i = 0; i < value.Length; i++)
        {
            work.Charge(1);
            hash = (hash ^ value[i]) * 16777619;
        }
        return hash;
    }

    internal static bool Equals(string left, string right, CssValueWork work)
    {
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            work.Charge(2);
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    internal static string? CanonicalIndex(string? spelling, CssValueWork work)
    {
        if (spelling is null || spelling.Length == 0) return null;
        var index = 0;
        var negative = spelling[0] == '-';
        if (negative || spelling[0] == '+') index = 1;
        if (index == spelling.Length) return null;
        var firstNonzero = -1;
        for (var i = index; i < spelling.Length; i++)
        {
            work.Charge(1);
            var ch = spelling[i];
            if (ch is < '0' or > '9') return null;
            if (firstNonzero < 0 && ch != '0') firstNonzero = i;
        }
        if (firstNonzero < 0) return "0";
        if (negative) return null;
        work.CheckCancellation();
        var result = spelling.Substring(firstNonzero);
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
    }

    internal static CssSegment[] Flatten(CssSegment segment, CssValueWork work)
    {
        var result = new List<CssSegment>();
        var stack = new Stack<CssSegment>();
        stack.Push(segment);
        while (stack.Count != 0)
        {
            work.Charge(1);
            var current = stack.Pop();
            if (current.Kind != CssSegmentKind.Concat)
            {
                result.Add(current);
                continue;
            }
            for (var i = current.Children.Length - 1; i >= 0; i--)
            {
                work.Charge(1);
                stack.Push(current.Children[i]);
            }
        }
        work.CheckCancellation();
        return result.ToArray();
    }

    internal static bool IsWhitespace(CssSegment segment) => segment.Kind == CssSegmentKind.Trivia ||
        segment.Kind == CssSegmentKind.Token && segment.Original.Token.Kind == CssTokenKind.Whitespace;

    internal static bool TrySpread(CssSegmentList values, int start, CssValueWork work, out int invocation)
    {
        invocation = start;
        if (!IsPeriod(values[start])) return false;
        for (var count = 0; count < 3; count++)
        {
            while (invocation < values.Length && values[invocation].Kind == CssSegmentKind.Trivia)
            { work.Charge(1); invocation++; }
            if (invocation == values.Length || !IsPeriod(values[invocation])) return false;
            invocation++;
        }
        while (invocation < values.Length && values[invocation].Kind == CssSegmentKind.Trivia)
        { work.Charge(1); invocation++; }
        return invocation < values.Length && IsReference(values[invocation], out _);
    }

    internal static bool IsPeriod(CssSegment segment) => segment.Kind == CssSegmentKind.Token &&
        segment.Original.Token.Kind == CssTokenKind.Delim && segment.Original.Token.Delimiter == '.';

    internal static bool IsComma(CssSegment segment) => segment.Kind == CssSegmentKind.Token &&
        segment.Original.Token.Kind == CssTokenKind.Comma;

    internal static bool IsReference(CssSegment segment, out CssReferenceKind kind)
    {
        if (segment.Kind == CssSegmentKind.Container &&
            segment.Original.Kind == CssComponentKind.Function)
        {
            var name = segment.Original.FunctionName;
            if (name.Equals("var", StringComparison.OrdinalIgnoreCase))
            {
                kind = CssReferenceKind.Var;
                return true;
            }
            if (name.Equals("env", StringComparison.OrdinalIgnoreCase))
            {
                kind = CssReferenceKind.Env;
                return true;
            }
        }
        kind = default;
        return false;
    }

    internal static bool IsArbitrary(CssSegment segment)
    {
        if (segment.Kind != CssSegmentKind.Container ||
            segment.Original.Kind != CssComponentKind.Function) return false;
        var name = segment.Original.FunctionName;
        return name.Equals("var", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("env", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("attr", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("if", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("inherit", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("ident", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("random-item", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("--", StringComparison.Ordinal);
    }

    internal static bool TryHeader(CssSegment[] components, CssReferenceKind kind,
        CssValueWork work, out string name, out string[] indices)
    {
        name = string.Empty;
        indices = [];
        var foundName = false;
        List<string>? list = null;
        foreach (var value in components)
        {
            work.Charge(1);
            if (IsWhitespace(value)) continue;
            if (value.Kind != CssSegmentKind.Token) return false;
            var token = value.Original.Token;
            if (!foundName)
            {
                if (token.Kind != CssTokenKind.Ident) return false;
                name = token.Text;
                work.Charge(name.Length);
                if (kind == CssReferenceKind.Var && !IsCustomName(name) ||
                    kind == CssReferenceKind.Env &&
                    (name.Length == 0 || CssWideKeywords.Recognize(name) != CssWideKeyword.None ||
                     name.Equals("default", StringComparison.OrdinalIgnoreCase))) return false;
                foundName = true;
                continue;
            }
            if (kind != CssReferenceKind.Env || token.Kind != CssTokenKind.Number || !token.IsInteger)
                return false;
            var index = CanonicalIndex(token.NumberText, work);
            if (index is null) return false;
            list ??= new List<string>();
            list.Add(index);
        }
        if (!foundName) return false;
        indices = list?.ToArray() ?? [];
        work.CheckCancellation();
        return true;
    }

    internal static bool TryHeaderArgument(CssSegment[] components, CssValueWork work,
        out CssSegment[] content)
    {
        content = HeaderContent(components, work) ?? [];
        var significant = false;
        foreach (var value in content)
        {
            work.Charge(1);
            if (!IsWhitespace(value)) significant = true;
        }
        return significant && ValidDeclaration(content, work);
    }

    private static CssSegment[]? HeaderContent(CssSegment[] components, CssValueWork work)
    {
        var first = -1;
        for (var i = 0; i < components.Length; i++)
        {
            work.Charge(1);
            if (!IsWhitespace(components[i])) { first = i; break; }
        }
        if (first < 0) return null;
        var firstValue = components[first];
        var wrapper = firstValue.Kind == CssSegmentKind.Container &&
            firstValue.Original.Kind == CssComponentKind.SimpleBlock &&
            firstValue.Original.OpeningDelimiter == '{';
        for (var i = first + 1; i < components.Length; i++)
        {
            work.Charge(1);
            if (IsWhitespace(components[i])) continue;
            if (wrapper || components[i].Kind == CssSegmentKind.Container &&
                components[i].Original.Kind == CssComponentKind.SimpleBlock &&
                components[i].Original.OpeningDelimiter == '{') return null;
        }
        return wrapper ? Flatten(CssSegment.Concat(firstValue.Children.ToArray(work), work), work) : components;
    }

    internal static bool TryFallback(CssSegment[] components, CssReferenceKind kind,
        CssValueWork work, out CssSegment[] content)
    {
        content = components;
        var first = -1;
        for (var i = 0; i < components.Length; i++)
        {
            work.Charge(1);
            if (!IsWhitespace(components[i])) { first = i; break; }
        }
        if (first < 0) return true;
        var value = components[first];
        var wrapper = value.Kind == CssSegmentKind.Container &&
            value.Original.Kind == CssComponentKind.SimpleBlock &&
            value.Original.OpeningDelimiter == '{';
        if (!wrapper && kind == CssReferenceKind.Var) return ValidDeclaration(content, work);
        for (var i = first; i < components.Length; i++)
        {
            work.Charge(1);
            if (IsWhitespace(components[i])) continue;
            if (wrapper)
            {
                if (i != first) return false;
            }
            else if (components[i].Kind == CssSegmentKind.Container &&
                     components[i].Original.Kind == CssComponentKind.SimpleBlock &&
                     components[i].Original.OpeningDelimiter == '{') return false;
        }
        if (wrapper) content = Flatten(CssSegment.Concat(value.Children.ToArray(work), work), work);
        return ValidDeclaration(content, work);
    }

    private static bool ValidDeclaration(CssSegment[] components, CssValueWork work)
    {
        foreach (var value in components)
        {
            work.Charge(1);
            if (value.Kind != CssSegmentKind.Token) continue;
            var token = value.Original.Token;
            if (token.Kind is CssTokenKind.BadString or CssTokenKind.BadUrl or
                CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or
                CssTokenKind.CloseCurlyBracket or CssTokenKind.Semicolon ||
                token.Kind == CssTokenKind.Delim && token.Delimiter == '!') return false;
        }
        return true;
    }
}
