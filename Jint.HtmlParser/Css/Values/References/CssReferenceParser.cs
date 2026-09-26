namespace Jint.HtmlParser.Css.Values.References;

// CSS Variables 1 §§2–4, CSS Values 5 Appendix A and CSS Environment Variables 1 §3.
// This is syntax analysis. Selected fallback evaluation and final name validity belong to C6s.
internal static class CssReferenceParser
{
    internal static CssReferenceAnalysis Analyze(CssReferenceInput input,
        CssReferenceUse use, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(work);
        if (use is < CssReferenceUse.PropertyValue or > CssReferenceUse.DescriptorValue)
            throw new ArgumentOutOfRangeException(nameof(use));
        work.CheckCancellation();

        var occurrences = new List<CssReferenceOccurrence>();
        var stack = new List<Frame> { new(input.Components, -1, 0, false, -1) };
        work.CheckCancellation();
        CssSourceSpan? invalid = null;
        CssSourceSpan? pending = null;
        string? pendingName = null;
        while (stack.Count != 0)
        {
            work.Charge(1);
            var last = stack.Count - 1;
            var frame = stack[last];
            if (frame.Index == frame.Components.Count)
            {
                stack.RemoveAt(last);
                continue;
            }
            var componentIndex = frame.Index;
            var value = frame.Components[componentIndex];
            frame.Index++;
            stack[last] = frame;
            work.Charge(1);

            if (value.Kind == CssComponentKind.Token)
            {
                var kind = value.Token.Kind;
                if (kind is CssTokenKind.BadString or CssTokenKind.BadUrl or
                    CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or
                    CssTokenKind.CloseCurlyBracket ||
                    frame.Depth == 0 && (kind == CssTokenKind.Semicolon ||
                    kind == CssTokenKind.Delim && value.Token.Delimiter == '!'))
                    invalid ??= value.Span;
                continue;
            }

            var nextDepth = frame.Depth + 1;
            if (input.MaxNestingDepth > 0 && nextDepth > input.MaxNestingDepth)
                throw new ParseLimitException(ParseLimitKind.NestingDepth,
                    input.MaxNestingDepth, nextDepth);

            var owner = frame.ParentIndex;
            var inFallback = frame.InFallback || frame.FallbackStart >= 0 &&
                componentIndex > frame.FallbackStart;
            if (value.Kind == CssComponentKind.Function)
            {
                var name = value.FunctionName;
                work.Charge(name.Length);
                if (IsName(name, "var") || IsName(name, "env"))
                {
                    var referenceKind = IsName(name, "var") ? CssReferenceKind.Var : CssReferenceKind.Env;
                    if (referenceKind == CssReferenceKind.Var && use == CssReferenceUse.DescriptorValue)
                        invalid ??= value.Span;

                    var children = value.Values;
                    var comma = -1;
                    List<CssSourceSpan>? spreadLocations = null;
                    var headerHasSpread = false;
                    var headerDynamic = false;
                    for (var i = 0; i < children.Count; i++)
                    {
                        work.Charge(1);
                        var child = children[i];
                        if (comma < 0 && child.Kind == CssComponentKind.Token &&
                            child.Token.Kind == CssTokenKind.Comma) comma = i;
                        if (i + 3 < children.Count && IsPeriod(child) && IsPeriod(children[i + 1]) &&
                            IsPeriod(children[i + 2]) && children[i + 3].Kind == CssComponentKind.Function &&
                            IsArbitrary(children[i + 3].FunctionName))
                        {
                            work.CheckCancellation();
                            spreadLocations ??= new List<CssSourceSpan>();
                            var nestedSpan = children[i + 3].Span;
                            spreadLocations.Add(new CssSourceSpan(child.Span.Start,
                                nestedSpan.Start + nestedSpan.Length - child.Span.Start));
                            work.CheckCancellation();
                            if (comma < 0 || i < comma)
                            {
                                headerHasSpread = true;
                                headerDynamic = true;
                            }
                        }
                    }
                    var headerCount = comma < 0 ? children.Count : comma;
                    if (!headerHasSpread && !HasSignificant(children, 0, headerCount, work)) invalid ??= value.Span;
                    var contentStart = OpeningParenthesisEnd(input.Source, value.Span, work);
                    var contentEnd = value.Span.Start + value.Span.Length - (value.IsClosed ? 1 : 0);
                    var headerEnd = comma < 0 ? contentEnd : children[comma].Span.Start;
                    var header = Range(children, 0, headerCount, contentStart, headerEnd, work);
                    var fallback = comma < 0 ? default : Range(children, comma + 1,
                        children.Count - comma - 1,
                        children[comma].Span.Start + children[comma].Span.Length, contentEnd, work);
                    var staticName = headerDynamic ? null : StaticName(referenceKind, children, headerCount, work);
                    CssSourceSpan[]? spreads = null;
                    if (spreadLocations is not null)
                    {
                        work.CheckCancellation();
                        spreads = spreadLocations.ToArray();
                        work.Charge(spreads.Length);
                        work.CheckCancellation();
                    }
                    owner = occurrences.Count;
                    work.CheckCancellation();
                    occurrences.Add(new CssReferenceOccurrence(referenceKind, value.Span, frame.ParentIndex,
                        header, comma >= 0, fallback, staticName, headerDynamic, spreads, false));
                    work.CheckCancellation();
                    if (frame.ParentIndex >= 0)
                        occurrences[frame.ParentIndex] = inFallback
                            ? occurrences[frame.ParentIndex].WithNestedFallback()
                            : occurrences[frame.ParentIndex].WithDynamicHeader();
                }
                else if (IsPendingFamily(name))
                {
                    if (pending is null)
                    {
                        pending = value.Span;
                        pendingName = name;
                    }
                }
            }

            work.CheckCancellation();
            var fallbackStart = owner != frame.ParentIndex && occurrences[owner].HasFallback
                ? occurrences[owner].Header.Count : -1;
            stack.Add(new Frame(value.Values, owner, nextDepth,
                owner != frame.ParentIndex ? false : inFallback, fallbackStart));
            work.CheckCancellation();
        }

        work.CheckCancellation();
        if (invalid is { } invalidSpan) return CssReferenceAnalysis.Invalid(invalidSpan);
        if (pending is { } pendingSpan) return CssReferenceAnalysis.Pending(pendingSpan, pendingName!);
        work.CheckCancellation();
        var copy = new CssReferenceOccurrence[occurrences.Count];
        work.CheckCancellation();
        for (var i = 0; i < copy.Length; i++)
        {
            work.Charge(1);
            copy[i] = occurrences[i];
        }
        work.CheckCancellation();
        var result = CssReferenceAnalysis.Success(new CssReferenceProgram(input, copy), copy.Length != 0);
        work.CheckCancellation();
        return result;
    }

    internal static CssCustomPropertyResult ParseCustomProperty(string decodedName,
        CssReferenceInput input, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(decodedName);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        work.Charge(decodedName.Length);
        if (decodedName.Length < 3 || decodedName[0] != '-' || decodedName[1] != '-')
            throw new ArgumentException("A decoded custom-property identifier is required.", nameof(decodedName));

        var analysis = Analyze(input, CssReferenceUse.CustomPropertyValue, work);
        if (analysis.Kind == CssReferenceAnalysisKind.InvalidSyntax)
        {
            work.CheckCancellation();
            return CssCustomPropertyResult.Invalid(analysis.Span);
        }
        if (analysis.Kind == CssReferenceAnalysisKind.PendingFeature)
        {
            work.CheckCancellation();
            return CssCustomPropertyResult.Pending(analysis.Span, analysis.PendingFunction!);
        }

        if (analysis.Kind == CssReferenceAnalysisKind.Literal)
        {
            var keyword = CssPrimitiveParser.ParseWideKeyword(input.Components, work);
            if (keyword.IsMatch)
            {
                work.CheckCancellation();
                return CssCustomPropertyResult.FromKeyword(keyword.Value, input);
            }
        }
        work.CheckCancellation();
        var result = CssCustomPropertyResult.FromValue(decodedName, analysis.Program);
        work.CheckCancellation();
        return result;
    }

    private static bool HasSignificant(CssComponentValueList values, int start, int count, CssValueWork work)
    {
        for (var i = start; i < start + count; i++)
        {
            work.Charge(1);
            if (values[i].Kind != CssComponentKind.Token || values[i].Token.Kind != CssTokenKind.Whitespace)
                return true;
        }
        return false;
    }

    private static CssReferenceRange Range(CssComponentValueList values, int start, int count,
        int sourceStart, int sourceEnd, CssValueWork work)
    {
        work.Charge(1);
        return new CssReferenceRange(values, start, count,
            new CssSourceSpan(sourceStart, sourceEnd - sourceStart));
    }

    private static int OpeningParenthesisEnd(string source, CssSourceSpan functionSpan, CssValueWork work)
    {
        var end = functionSpan.Start + functionSpan.Length;
        for (var i = functionSpan.Start; i < end; i++)
        {
            work.Charge(1);
            if (source[i] == '\\')
            {
                if (i + 1 < end)
                {
                    i++;
                    work.Charge(1);
                }
            }
            else if (source[i] == '(') return i + 1;
        }
        throw new InvalidOperationException("A C1 function has no opening parenthesis.");
    }

    private static string? StaticName(CssReferenceKind kind, CssComponentValueList values,
        int count, CssValueWork work)
    {
        string? name = null;
        for (var i = 0; i < count; i++)
        {
            work.Charge(1);
            var component = values[i];
            if (component.Kind != CssComponentKind.Token) return null;
            var token = component.Token;
            if (token.Kind == CssTokenKind.Whitespace) continue;
            if (name is null)
            {
                if (token.Kind != CssTokenKind.Ident) return null;
                name = token.Text;
                if (kind == CssReferenceKind.Var &&
                    (name.Length < 3 || name[0] != '-' || name[1] != '-')) return null;
            }
            else if (kind != CssReferenceKind.Env || token.Kind != CssTokenKind.Number ||
                !token.IsInteger || token.NumberText.Length > 0 && token.NumberText[0] == '-') return null;
        }
        return name;
    }

    private static bool IsPeriod(CssComponentValue component) =>
        component.Kind == CssComponentKind.Token && component.Token.Kind == CssTokenKind.Delim &&
        component.Token.Delimiter == '.';

    private static bool IsName(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static bool IsPendingFamily(string name) => IsName(name, "attr") || IsName(name, "if") ||
        IsName(name, "inherit") || IsName(name, "ident") || IsName(name, "random-item") ||
        name.StartsWith("--", StringComparison.Ordinal);

    private static bool IsArbitrary(string name) => IsName(name, "var") || IsName(name, "env") ||
        IsPendingFamily(name);

    private struct Frame
    {
        internal Frame(CssComponentValueList components, int parentIndex, int depth,
            bool inFallback, int fallbackStart)
        {
            Components = components;
            ParentIndex = parentIndex;
            Depth = depth;
            InFallback = inFallback;
            FallbackStart = fallbackStart;
            Index = 0;
        }

        internal CssComponentValueList Components;
        internal int ParentIndex;
        internal int Depth;
        internal bool InFallback;
        internal int FallbackStart;
        internal int Index;
    }
}
