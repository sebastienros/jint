namespace Jint.HtmlParser.Css.Values.References;

internal sealed partial class CssSubstitutedValue
{
    internal const int MaxTokens = 65_536;
    internal const int MaxSpelling = 1_048_576;
    private readonly CssProjectedTokenOrigin[] _origins;

    private CssSubstitutedValue(CssSegment root, CssComponentValueList components,
        CssProjectedTokenOrigin[] origins)
    {
        Root = root;
        Components = components;
        _origins = origins;
    }

    internal CssSegment Root { get; }
    internal CssComponentValueList Components { get; }
    internal int TokenCount => Root.TokenCount;
    internal int SpellingLength => Root.SpellingLength;

    // Validation reads the projected components directly: concatenated spelling is never retokenized.
    internal CssReferenceInput AsReferenceInput(CssValueWork work)
    {
        work.CheckCancellation();
        var source = new System.Text.StringBuilder(SpellingLength);
        foreach (var origin in _origins)
        {
            work.Charge(1);
            if (origin.IsSyntheticCloser) source.Append(origin.SyntheticCloser);
            else
            {
                var spelling = origin.Source.SourceSlice(origin.SourceSpan);
                work.Charge(spelling.Length);
                source.Append(spelling);
            }
        }
        work.CheckCancellation();
        var text = source.ToString();
        work.Charge(text.Length);
        return CssReferenceInput.FromComponents(text, Components, Root.Depth, work);
    }

    internal CssSourceOriginRange OriginsFor(CssSourceSpan projectionSpan)
    {
        if (projectionSpan.Start < 0 || projectionSpan.Length < 0 ||
            projectionSpan.Start > SpellingLength ||
            projectionSpan.Length > SpellingLength - projectionSpan.Start)
            throw new ArgumentOutOfRangeException(nameof(projectionSpan));
        if (projectionSpan.Length == 0) return new CssSourceOriginRange(_origins, 0, 0);
        var lo = 0;
        var hi = _origins.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            var span = _origins[mid].ProjectionSpan;
            if (span.Start + span.Length <= projectionSpan.Start) lo = mid + 1;
            else hi = mid;
        }
        var first = lo;
        lo = first;
        hi = _origins.Length;
        var end = projectionSpan.Start + projectionSpan.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_origins[mid].ProjectionSpan.Start < end) lo = mid + 1;
            else hi = mid;
        }
        return new CssSourceOriginRange(_origins, first, lo - first);
    }

    internal static CssSubstitutedValue Create(CssSegment root, int maxDepth, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        if (root.IsOversize) throw new ArgumentException("The substitution is oversized.", nameof(root));
        if (maxDepth > 0 && root.Depth > maxDepth)
            throw new ParseLimitException(ParseLimitKind.NestingDepth, maxDepth, root.Depth);
        var origins = new List<CssProjectedTokenOrigin>(root.TokenCount);
        var output = new List<List<CssComponentValue>> { new(root.TokenCount) };
        var tasks = new Stack<BuildTask>();
        tasks.Push(new BuildTask(root, false, 0));
        var position = 0;
        while (tasks.Count != 0)
        {
            work.Charge(1);
            var task = tasks.Pop();
            var segment = task.Segment;
            if (task.Close)
            {
                var original = segment.Original;
                var originalEnd = original.Span.Start + original.Span.Length;
                var closer = original.IsClosed
                    ? new CssSourceSpan(originalEnd - 1, 1)
                    : new CssSourceSpan(originalEnd, 0);
                origins.Add(new CssProjectedTokenOrigin(new CssSourceSpan(position, 1),
                    segment.Source!, closer, !original.IsClosed,
                    original.Kind == CssComponentKind.Function ? ')' : original.OpeningDelimiter switch
                    {
                        '(' => ')',
                        '[' => ']',
                        '{' => '}',
                        _ => throw new InvalidOperationException("Unknown CSS block delimiter.")
                    }));
                position++;
                work.CheckCancellation();
                var children = output[^1].ToArray();
                work.Charge(children.Length);
                work.CheckCancellation();
                output.RemoveAt(output.Count - 1);
                var projected = CssComponentValue.FromContainer(original.Kind,
                    new CssSourceSpan(task.Start, position - task.Start),
                    original.Kind == CssComponentKind.Function ? original.FunctionName : null,
                    original.Kind == CssComponentKind.SimpleBlock ? original.OpeningDelimiter : '\0',
                    new CssComponentValueList(children), true);
                output[^1].Add(projected);
                work.CheckCancellation();
                continue;
            }
            if (segment.Kind == CssSegmentKind.Trivia) continue;
            if (segment.Kind == CssSegmentKind.Concat)
            {
                for (var i = segment.Children.Length - 1; i >= 0; i--)
                {
                    work.Charge(1);
                    tasks.Push(new BuildTask(segment.Children[i], false, 0));
                }
                continue;
            }
            if (segment.Kind == CssSegmentKind.Token)
            {
                var token = segment.Original.Token;
                var span = new CssSourceSpan(position, token.Span.Length);
                var projected = new CssToken(token.Kind, span, token.Text, token.NumberText,
                    token.Unit, token.Delimiter, token.IsInteger, token.IsIdHash,
                    token.UnicodeRangeStart, token.UnicodeRangeEnd);
                origins.Add(new CssProjectedTokenOrigin(span, segment.Source!, token.Span, false));
                output[^1].Add(CssComponentValue.FromToken(projected));
                position += span.Length;
                continue;
            }
            var opener = segment.OpenerSpan;
            origins.Add(new CssProjectedTokenOrigin(new CssSourceSpan(position, opener.Length),
                segment.Source!, opener, false));
            var start = position;
            position += opener.Length;
            work.CheckCancellation();
            output.Add(new List<CssComponentValue>());
            tasks.Push(new BuildTask(segment, true, start));
            for (var i = segment.Children.Length - 1; i >= 0; i--)
            {
                work.Charge(1);
                tasks.Push(new BuildTask(segment.Children[i], false, 0));
            }
        }
        work.CheckCancellation();
        var list = new CssComponentValueList(output[0].ToArray());
        work.Charge(output[0].Count);
        work.CheckCancellation();
        var originCopy = origins.ToArray();
        work.Charge(originCopy.Length);
        work.CheckCancellation();
        var result = new CssSubstitutedValue(root, list, originCopy);
        work.CheckCancellation();
        return result;
    }

    private readonly record struct BuildTask(CssSegment Segment, bool Close, int Start);
}

internal readonly struct CssProjectedTokenOrigin
{
    internal CssProjectedTokenOrigin(CssSourceSpan projectionSpan, CssReferenceInput source,
        CssSourceSpan sourceSpan, bool isSyntheticCloser, char syntheticCloser = '\0')
    {
        ProjectionSpan = projectionSpan;
        Source = source;
        SourceSpan = sourceSpan;
        IsSyntheticCloser = isSyntheticCloser;
        SyntheticCloser = syntheticCloser;
    }

    internal CssSourceSpan ProjectionSpan { get; }
    internal CssReferenceInput Source { get; }
    internal CssSourceSpan SourceSpan { get; }
    internal bool IsSyntheticCloser { get; }
    internal char SyntheticCloser { get; }
}

internal readonly struct CssSourceOriginRange
{
    private readonly CssProjectedTokenOrigin[] _origins;
    private readonly int _offset;

    internal CssSourceOriginRange(CssProjectedTokenOrigin[] origins, int offset, int count)
    {
        _origins = origins;
        _offset = offset;
        Count = count;
    }

    internal int Count { get; }
    internal CssProjectedTokenOrigin this[int index] => (uint) index < (uint) Count
        ? _origins[_offset + index] : throw new ArgumentOutOfRangeException(nameof(index));
}

internal enum CssSegmentKind { Token, Container, Concat, Trivia }

/// <summary>Immutable shared replacement tree; metrics saturate at one beyond the selected ceilings.</summary>
internal sealed partial class CssSegment
{
    private CssSegment(CssSegmentKind kind, CssReferenceInput? source, CssComponentValue original,
        CssSourceSpan openerSpan, CssSegmentList children, int tokenCount, int spellingLength, int depth,
        CssSourceSpan lexicalSpan = default, string? syntheticLexical = null, bool substitutionBoundary = false,
        int lexicalLength = 0, bool startsBoundary = false, bool endsBoundary = false)
    {
        Kind = kind;
        Source = source;
        Original = original;
        OpenerSpan = openerSpan;
        Children = children;
        TokenCount = tokenCount;
        SpellingLength = spellingLength;
        Depth = depth;
        LexicalSpan = lexicalSpan;
        SyntheticLexical = syntheticLexical;
        IsSubstitutionBoundary = substitutionBoundary;
        LexicalLength = lexicalLength;
        IsEmpty = kind == CssSegmentKind.Concat && children.Length == 0 ||
            kind == CssSegmentKind.Trivia && !substitutionBoundary && lexicalLength == 0;
        BoundaryOnly = substitutionBoundary;
        StartsBoundary = substitutionBoundary || startsBoundary;
        EndsBoundary = substitutionBoundary || endsBoundary;
    }

    internal CssSegmentKind Kind { get; }
    internal CssReferenceInput? Source { get; }
    internal CssComponentValue Original { get; }
    internal CssSourceSpan OpenerSpan { get; }
    internal CssSegmentList Children { get; }
    internal int TokenCount { get; }
    internal int SpellingLength { get; }
    internal int Depth { get; }
    internal CssSourceSpan LexicalSpan { get; }
    internal string? SyntheticLexical { get; }
    internal bool IsSubstitutionBoundary { get; }
    internal int LexicalLength { get; }
    // Memoized summaries inspect no descendants. Token-bearing empty lexical spans stay nonempty.
    internal bool IsEmpty { get; }
    internal bool BoundaryOnly { get; }
    internal bool StartsBoundary { get; }
    internal bool EndsBoundary { get; }
    internal bool IsOversize => TokenCount > CssSubstitutedValue.MaxTokens ||
        SpellingLength > CssSubstitutedValue.MaxSpelling ||
        LexicalLength > CssSubstitutedValue.MaxSpelling;

    internal static CssSegment Token(CssReferenceInput source, CssComponentValue original)
    {
        var lexicalSpan = Intersect(original.Token.Span, source.SerializationSpan);
        return new(CssSegmentKind.Token, source, original, default, CssSegmentList.Empty, 1,
            System.Math.Min(original.Token.Span.Length, CssSubstitutedValue.MaxSpelling + 1), 0,
            lexicalSpan: lexicalSpan, lexicalLength: System.Math.Min(lexicalSpan.Length, CssSubstitutedValue.MaxSpelling + 1));
    }

    internal static CssSegment Container(CssReferenceInput source, CssComponentValue original,
        CssSegment[] children, CssValueWork work)
    {
        var openerEnd = original.Kind == CssComponentKind.Function
            ? FunctionOpenerEnd(source, original.Span, work)
            : original.Span.Start + 1;
        var opener = new CssSourceSpan(original.Span.Start, openerEnd - original.Span.Start);
        return BuildContainer(source, original, opener, children, work);
    }

    // Only FromInput has one source-aligned child per original C1 value. A replacement factory
    // accepts arbitrary child counts and must never zip them against original.Values.
    private static CssSegment ContainerFromInput(CssReferenceInput source, CssComponentValue original,
        CssSegment[] children, CssValueWork work)
    {
        var openerEnd = original.Kind == CssComponentKind.Function
            ? FunctionOpenerEnd(source, original.Span, work)
            : original.Span.Start + 1;
        var opener = new CssSourceSpan(original.Span.Start, openerEnd - original.Span.Start);
        var contentEnd = original.Span.Start + original.Span.Length - (original.IsClosed ? 1 : 0);
        var aligned = CaptureGaps(source, original.Values, children,
            new CssSourceSpan(openerEnd, contentEnd - openerEnd), work);
        return BuildContainer(source, original, opener, aligned, work);
    }

    internal static CssSegment Rebuild(CssSegment original, CssSegment[] children, CssValueWork work) =>
        BuildContainer(original.Source!, original.Original, original.OpenerSpan, children, work);

    private static CssSegment BuildContainer(CssReferenceInput source, CssComponentValue original,
        CssSourceSpan opener, CssSegment[] children, CssValueWork work)
    {
        var tokens = 2;
        var spelling = System.Math.Min(CssSubstitutedValue.MaxSpelling + 1, opener.Length + 1);
        var depth = 1;
        var lexicalLength = spelling;
        foreach (var child in children)
        {
            work.Charge(1);
            tokens = Saturate(tokens, child.TokenCount, CssSubstitutedValue.MaxTokens);
            spelling = Saturate(spelling, child.SpellingLength, CssSubstitutedValue.MaxSpelling);
            depth = System.Math.Max(depth, child.Depth + 1);
            lexicalLength = Saturate(lexicalLength, child.LexicalLength, CssSubstitutedValue.MaxSpelling);
        }
        return new CssSegment(CssSegmentKind.Container, source, original, opener,
            new CssSegmentList(children, work), tokens, spelling, depth,
            lexicalLength: lexicalLength);
    }

    private static readonly CssSegment Empty = new(CssSegmentKind.Concat, null, default, default,
        CssSegmentList.Empty, 0, 0, 0);

    internal static CssSegment Concat(CssSegment[] children, CssValueWork work)
    {
        work.CheckCancellation();
        // Normalize immediate children only. Shared DAGs are never flattened to find an edge marker.
        var retained = new List<CssSegment>(children.Length);
        foreach (var child in children)
        {
            work.Charge(1);
            if (child.IsEmpty) continue;
            if (retained.Count != 0)
            {
                var previous = retained[^1];
                if (child.IsSubstitutionBoundary && previous.EndsBoundary) continue;
                if (previous.IsSubstitutionBoundary && child.StartsBoundary) retained.RemoveAt(retained.Count - 1);
            }
            retained.Add(child);
        }
        work.CheckCancellation();
        if (retained.Count == 0) return Empty;
        if (retained.Count == 1) return retained[0];
        var tokens = 0;
        var spelling = 0;
        var depth = 0;
        var lexicalLength = 0;
        foreach (var child in retained)
        {
            work.Charge(1);
            tokens = Saturate(tokens, child.TokenCount, CssSubstitutedValue.MaxTokens);
            spelling = Saturate(spelling, child.SpellingLength, CssSubstitutedValue.MaxSpelling);
            depth = System.Math.Max(depth, child.Depth);
            lexicalLength = Saturate(lexicalLength, child.LexicalLength, CssSubstitutedValue.MaxSpelling);
        }
        work.Charge(retained.Count);
        var normalized = retained.ToArray();
        work.CheckCancellation();
        return new CssSegment(CssSegmentKind.Concat, null, default, default,
            new CssSegmentList(normalized, work), tokens, spelling, depth,
            lexicalLength: lexicalLength, startsBoundary: normalized[0].StartsBoundary,
            endsBoundary: normalized[^1].EndsBoundary);
    }

    internal static CssSegment FromInput(CssReferenceInput input, CssValueWork work)
    {
        work.CheckCancellation();
        var tasks = new Stack<(CssComponentValue Component, bool Exit)>();
        var values = new Stack<CssSegment>();
        for (var i = input.Components.Count - 1; i >= 0; i--)
        {
            work.Charge(1);
            tasks.Push((input.Components[i], false));
        }
        while (tasks.Count != 0)
        {
            work.Charge(1);
            var (component, exit) = tasks.Pop();
            if (component.Kind == CssComponentKind.Token)
            {
                values.Push(Token(input, component));
                continue;
            }
            if (!exit)
            {
                tasks.Push((component, true));
                for (var i = component.Values.Count - 1; i >= 0; i--)
                {
                    work.Charge(1);
                    tasks.Push((component.Values[i], false));
                }
                continue;
            }
            work.CheckCancellation();
            var children = new CssSegment[component.Values.Count];
            work.CheckCancellation();
            for (var i = children.Length - 1; i >= 0; i--)
            {
                work.Charge(1);
                children[i] = values.Pop();
            }
            values.Push(ContainerFromInput(input, component, children, work));
        }
        work.CheckCancellation();
        var rootChildren = new CssSegment[input.Components.Count];
        work.CheckCancellation();
        for (var i = rootChildren.Length - 1; i >= 0; i--)
        {
            work.Charge(1);
            rootChildren[i] = values.Pop();
        }
        work.CheckCancellation();
        return Concat(CaptureGaps(input, input.Components, rootChildren, input.SerializationSpan, work), work);
    }

    private static int FunctionOpenerEnd(CssReferenceInput input, CssSourceSpan span, CssValueWork work)
    {
        var source = input.SourceSlice(span);
        for (var i = 0; i < source.Length; i++)
        {
            work.Charge(1);
            if (source[i] == '\\')
            {
                if (i + 1 < source.Length) { i++; work.Charge(1); }
            }
            else if (source[i] == '(') return span.Start + i + 1;
        }
        throw new InvalidOperationException("A C1 function has no opening parenthesis.");
    }

    private static int Saturate(int left, int right, int ceiling) =>
        left > ceiling - right ? ceiling + 1 : left + right;
}

/// <summary>An indexed immutable owner; no segment array escapes publication.</summary>
internal sealed class CssSegmentList
{
    private readonly CssSegment[] _values;
    private CssSegmentList() => _values = [];
    internal static CssSegmentList Empty { get; } = new();
    internal CssSegmentList(CssSegment[] values, CssValueWork work)
    {
        work.CheckCancellation();
        _values = new CssSegment[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            _values[i] = values[i];
        }
        work.CheckCancellation();
    }
    internal int Length => _values.Length;
    internal CssSegment this[int index] => _values[index];
    internal void CopyTo(int start, CssSegment[] destination, int offset, int count, CssValueWork work)
    {
        work.CheckCancellation();
        for (var i = 0; i < count; i++)
        {
            work.Charge(1);
            destination[offset + i] = _values[start + i];
        }
        work.CheckCancellation();
    }
    internal CssSegment[] ToArray(CssValueWork work)
    {
        work.CheckCancellation();
        var result = new CssSegment[Length];
        CopyTo(0, result, 0, Length, work);
        return result;
    }
}
