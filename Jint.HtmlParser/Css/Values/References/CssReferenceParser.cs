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
        var earlyLocations = new List<List<CssSourceSpan>?>();
        var earlyHeaders = new List<bool>();
        var earlyFallbacks = new List<bool>();
        var stack = new List<Frame> { new(input.Components, -1, -1, 0, false, -1, true, false, -1, -1) };
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

            if (frame.EarlyOwnerIndex >= 0 && componentIndex + 3 < frame.Components.Count &&
                IsPeriod(value) && IsPeriod(frame.Components[componentIndex + 1]) &&
                IsPeriod(frame.Components[componentIndex + 2]) &&
                frame.Components[componentIndex + 3].Kind == CssComponentKind.Function &&
                IsArbitrary(frame.Components[componentIndex + 3].FunctionName))
            {
                var nestedSpan = frame.Components[componentIndex + 3].Span;
                var span = new CssSourceSpan(value.Span.Start,
                    nestedSpan.Start + nestedSpan.Length - value.Span.Start);
                work.CheckCancellation();
                var locations = earlyLocations[frame.EarlyOwnerIndex];
                if (locations is null)
                {
                    locations = new List<CssSourceSpan>();
                    earlyLocations[frame.EarlyOwnerIndex] = locations;
                }
                locations.Add(span);
                if (frame.ReferenceArguments)
                {
                    if (frame.FallbackStart >= 0 && componentIndex > frame.FallbackStart)
                        earlyFallbacks[frame.EarlyOwnerIndex] = true;
                    else earlyHeaders[frame.EarlyOwnerIndex] = true;
                }
                work.CheckCancellation();
            }

            if (value.Kind == CssComponentKind.Token)
            {
                var kind = value.Token.Kind;
                if (kind is CssTokenKind.BadString or CssTokenKind.BadUrl or
                    CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or
                    CssTokenKind.CloseCurlyBracket ||
                    frame.DeclarationRoot && (kind == CssTokenKind.Semicolon ||
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
            var earlyOwner = frame.EarlyOwnerIndex;
            var declarationRoot = false;
            var headerWrapperIndex = -1;
            var fallbackWrapperIndex = -1;
            switch (value.Kind)
            {
                case CssComponentKind.Function:
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
                            for (var i = 0; i < children.Count; i++)
                            {
                                work.Charge(1);
                                var child = children[i];
                                if (comma < 0 && child.Kind == CssComponentKind.Token &&
                                    child.Token.Kind == CssTokenKind.Comma) comma = i;
                            }
                            var headerCount = comma < 0 ? children.Count : comma;
                            var contentStart = OpeningParenthesisEnd(input, value.Span, work);
                            var contentEnd = value.Span.Start + value.Span.Length - (value.IsClosed ? 1 : 0);
                            var headerEnd = comma < 0 ? contentEnd : children[comma].Span.Start;
                            var header = Range(children, 0, headerCount, contentStart, headerEnd, work);
                            var fallback = comma < 0 ? default : Range(children, comma + 1,
                                children.Count - comma - 1,
                                children[comma].Span.Start + children[comma].Span.Length, contentEnd, work);
                            var staticName = StaticName(referenceKind, children, headerCount, work);
                            headerWrapperIndex = WrapperIndex(header, work);
                            fallbackWrapperIndex = comma < 0 ? -1 : WrapperIndex(fallback, work);
                            owner = occurrences.Count;
                            earlyOwner = owner;
                            declarationRoot = true;
                            work.CheckCancellation();
                            occurrences.Add(new CssReferenceOccurrence(referenceKind, value.Span, frame.ParentIndex,
                                header, comma >= 0, fallback, staticName, false, null, false));
                            earlyLocations.Add(null);
                            earlyHeaders.Add(false);
                            earlyFallbacks.Add(false);
                            work.CheckCancellation();
                            if (frame.ParentIndex >= 0)
                                occurrences[frame.ParentIndex] = inFallback
                                    ? occurrences[frame.ParentIndex].WithNestedFallback()
                                    : occurrences[frame.ParentIndex].WithDynamicHeader();
                        }
                        else if (IsPendingFamily(name))
                        {
                            earlyOwner = -1;
                            if (pending is null)
                            {
                                pending = value.Span;
                                pendingName = name;
                            }
                        }
                    }
                    break;
                case CssComponentKind.SimpleBlock when value.OpeningDelimiter == '{' &&
                    (componentIndex == frame.HeaderWrapperIndex || componentIndex == frame.FallbackWrapperIndex):
                    {
                        // A direct early invocation in the header can introduce the first comma.
                        // In that case this raw fallback block may be ordinary nested content,
                        // rather than the wrapper of a declaration-value argument.
                        declarationRoot = componentIndex != frame.FallbackWrapperIndex ||
                            !frame.ReferenceArguments || !earlyHeaders[frame.EarlyOwnerIndex];
                    }
                    break;
            }

            work.CheckCancellation();
            var fallbackStart = owner != frame.ParentIndex && occurrences[owner].HasFallback
                ? occurrences[owner].Header.Count : -1;
            stack.Add(new Frame(value.Values, owner, earlyOwner, nextDepth,
                owner != frame.ParentIndex ? false : inFallback, fallbackStart,
                declarationRoot, owner != frame.ParentIndex,
                headerWrapperIndex, fallbackWrapperIndex));
            work.CheckCancellation();
        }

        work.CheckCancellation();
        for (var i = 0; i < occurrences.Count; i++)
        {
            work.Charge(1);
            var occurrence = occurrences[i];
            if (!earlyHeaders[i] && !ValidHeader(occurrence.Header, work))
                invalid ??= occurrence.Span;
            if (occurrence.HasFallback && !earlyFallbacks[i] && !earlyHeaders[i] &&
                !ValidFallback(occurrence.Fallback, occurrence.Kind, work))
                invalid ??= occurrence.Span;
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
            CssSourceSpan[]? spans = null;
            if (earlyLocations[i] is { } locations)
            {
                work.CheckCancellation();
                spans = locations.ToArray();
                work.Charge(spans.Length);
                work.CheckCancellation();
            }
            copy[i] = occurrences[i].WithEarlySubstitutions(spans, earlyHeaders[i]);
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
        switch (analysis.Kind)
        {
            case CssReferenceAnalysisKind.InvalidSyntax:
                {
                    work.CheckCancellation();
                    return CssCustomPropertyResult.Invalid(analysis.Span);
                }
            case CssReferenceAnalysisKind.PendingFeature:
                {
                    work.CheckCancellation();
                    return CssCustomPropertyResult.Pending(analysis.Span, analysis.PendingFunction!);
                }
            case CssReferenceAnalysisKind.Literal:
                {
                    var keyword = CssPrimitiveParser.ParseWideKeyword(input.Components, work);
                    if (keyword.IsMatch)
                    {
                        work.CheckCancellation();
                        return CssCustomPropertyResult.FromKeyword(keyword.Value, input);
                    }
                }
                break;
        }
        work.CheckCancellation();
        var result = CssCustomPropertyResult.FromValue(decodedName, analysis.Program);
        work.CheckCancellation();
        return result;
    }

    private static int FirstSignificantIndex(CssReferenceRange range, CssValueWork work)
    {
        for (var i = range.Start; i < range.Start + range.Count; i++)
        {
            work.Charge(1);
            var value = range.Components[i];
            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace)
                return i;
        }
        return -1;
    }

    private static int WrapperIndex(CssReferenceRange range, CssValueWork work)
    {
        var first = FirstSignificantIndex(range, work);
        if (first < 0) return -1;
        var value = range.Components[first];
        return value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '{' ? first : -1;
    }

    private static bool ValidHeader(CssReferenceRange range, CssValueWork work)
    {
        var first = FirstSignificantIndex(range, work);
        if (first < 0) return false;
        var wrapper = WrapperIndex(range, work);
        if (wrapper >= 0 && !HasSignificantComponents(range.Components[wrapper].Values, work)) return false;
        for (var i = first + 1; i < range.Start + range.Count; i++)
        {
            work.Charge(1);
            var value = range.Components[i];
            if (value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace)
                continue;
            if (wrapper >= 0 || value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '{')
                return false;
        }
        return true;
    }

    private static bool HasSignificantComponents(CssComponentValueList components, CssValueWork work)
    {
        for (var i = 0; i < components.Count; i++)
        {
            work.Charge(1);
            var value = components[i];
            if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Whitespace)
                return true;
        }
        return false;
    }

    private static bool ValidFallback(CssReferenceRange range, CssReferenceKind kind, CssValueWork work)
    {
        var wrapper = WrapperIndex(range, work);
        if (wrapper < 0 && kind == CssReferenceKind.Var) return true;
        for (var i = range.Start; i < range.Start + range.Count; i++)
        {
            work.Charge(1);
            var value = range.Components[i];
            if (value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace)
                continue;
            if (wrapper >= 0)
            {
                if (i != wrapper) return false;
            }
            else if (value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '{')
                return false;
        }
        return true;
    }

    private static CssReferenceRange Range(CssComponentValueList values, int start, int count,
        int sourceStart, int sourceEnd, CssValueWork work)
    {
        work.Charge(1);
        return new CssReferenceRange(values, start, count,
            new CssSourceSpan(sourceStart, sourceEnd - sourceStart));
    }

    private static int OpeningParenthesisEnd(CssReferenceInput input, CssSourceSpan functionSpan, CssValueWork work)
    {
        var source = input.SourceSlice(functionSpan);
        for (var i = 0; i < source.Length; i++)
        {
            work.Charge(1);
            if (source[i] == '\\')
            {
                if (i + 1 < source.Length)
                {
                    i++;
                    work.Charge(1);
                }
            }
            else if (source[i] == '(') return functionSpan.Start + i + 1;
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
        if (kind == CssReferenceKind.Env && name is not null &&
            (CssWideKeywords.Recognize(name) != CssWideKeyword.None ||
            name.Equals("default", StringComparison.OrdinalIgnoreCase))) return null;
        return name;
    }

    private static bool IsPeriod(CssComponentValue component) =>
        component.Kind == CssComponentKind.Token && component.Token.Kind == CssTokenKind.Delim &&
        component.Token.Delimiter == '.';

    private static bool IsName(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static bool IsPendingFamily(string name) =>
        name.StartsWith("--", StringComparison.Ordinal) || CssPendingReferenceLookup.Match(name) ||
        // Preserve the existing ordinal-ignore-case behavior for non-ASCII spellings.
        name.Length <= 11 && !System.Text.Ascii.IsValid(name) &&
        (IsName(name, "attr") || IsName(name, "if") || IsName(name, "inherit") || IsName(name, "ident") || IsName(name, "random-item"));

    private static bool IsArbitrary(string name) => IsName(name, "var") || IsName(name, "env") ||
        IsPendingFamily(name);

    private struct Frame
    {
        internal Frame(CssComponentValueList components, int parentIndex, int earlyOwnerIndex,
            int depth, bool inFallback, int fallbackStart, bool declarationRoot,
            bool referenceArguments, int headerWrapperIndex, int fallbackWrapperIndex)
        {
            Components = components;
            ParentIndex = parentIndex;
            EarlyOwnerIndex = earlyOwnerIndex;
            Depth = depth;
            InFallback = inFallback;
            FallbackStart = fallbackStart;
            DeclarationRoot = declarationRoot;
            ReferenceArguments = referenceArguments;
            HeaderWrapperIndex = headerWrapperIndex;
            FallbackWrapperIndex = fallbackWrapperIndex;
            Index = 0;
        }

        internal CssComponentValueList Components;
        internal int ParentIndex;
        internal int EarlyOwnerIndex;
        internal int Depth;
        internal bool InFallback;
        internal int FallbackStart;
        internal bool DeclarationRoot;
        internal bool ReferenceArguments;
        internal int HeaderWrapperIndex;
        internal int FallbackWrapperIndex;
        internal int Index;
    }
}
