namespace Jint.HtmlParser.Css.Values.References;

internal sealed class CssSubstitutedValue
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
                    segment.Source!, closer, !original.IsClosed));
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
        CssSourceSpan sourceSpan, bool isSyntheticCloser)
    {
        ProjectionSpan = projectionSpan;
        Source = source;
        SourceSpan = sourceSpan;
        IsSyntheticCloser = isSyntheticCloser;
    }

    internal CssSourceSpan ProjectionSpan { get; }
    internal CssReferenceInput Source { get; }
    internal CssSourceSpan SourceSpan { get; }
    internal bool IsSyntheticCloser { get; }
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

internal enum CssSegmentKind { Token, Container, Concat }

/// <summary>Immutable shared replacement tree; metrics saturate at one beyond the selected ceilings.</summary>
internal sealed class CssSegment
{
    private CssSegment(CssSegmentKind kind, CssReferenceInput? source, CssComponentValue original,
        CssSourceSpan openerSpan, CssSegmentList children, int tokenCount, int spellingLength, int depth)
    {
        Kind = kind;
        Source = source;
        Original = original;
        OpenerSpan = openerSpan;
        Children = children;
        TokenCount = tokenCount;
        SpellingLength = spellingLength;
        Depth = depth;
    }

    internal CssSegmentKind Kind { get; }
    internal CssReferenceInput? Source { get; }
    internal CssComponentValue Original { get; }
    internal CssSourceSpan OpenerSpan { get; }
    internal CssSegmentList Children { get; }
    internal int TokenCount { get; }
    internal int SpellingLength { get; }
    internal int Depth { get; }
    internal bool IsOversize => TokenCount > CssSubstitutedValue.MaxTokens ||
        SpellingLength > CssSubstitutedValue.MaxSpelling;

    internal static CssSegment Token(CssReferenceInput source, CssComponentValue original) =>
        new(CssSegmentKind.Token, source, original, default, CssSegmentList.Empty, 1,
            System.Math.Min(original.Token.Span.Length, CssSubstitutedValue.MaxSpelling + 1), 0);

    internal static CssSegment Container(CssReferenceInput source, CssComponentValue original,
        CssSegment[] children, CssValueWork work)
    {
        var openerEnd = original.Kind == CssComponentKind.Function
            ? FunctionOpenerEnd(source, original.Span, work)
            : original.Span.Start + 1;
        var opener = new CssSourceSpan(original.Span.Start, openerEnd - original.Span.Start);
        return BuildContainer(source, original, opener, children, work);
    }

    internal static CssSegment Rebuild(CssSegment original, CssSegment[] children, CssValueWork work) =>
        BuildContainer(original.Source!, original.Original, original.OpenerSpan, children, work);

    private static CssSegment BuildContainer(CssReferenceInput source, CssComponentValue original,
        CssSourceSpan opener, CssSegment[] children, CssValueWork work)
    {
        var tokens = 2;
        var spelling = System.Math.Min(CssSubstitutedValue.MaxSpelling + 1, opener.Length + 1);
        var depth = 1;
        foreach (var child in children)
        {
            work.Charge(1);
            tokens = Saturate(tokens, child.TokenCount, CssSubstitutedValue.MaxTokens);
            spelling = Saturate(spelling, child.SpellingLength, CssSubstitutedValue.MaxSpelling);
            depth = System.Math.Max(depth, child.Depth + 1);
        }
        return new CssSegment(CssSegmentKind.Container, source, original, opener,
            new CssSegmentList(children, work), tokens, spelling, depth);
    }

    internal static CssSegment Concat(CssSegment[] children, CssValueWork work)
    {
        if (children.Length == 1) return children[0];
        var tokens = 0;
        var spelling = 0;
        var depth = 0;
        foreach (var child in children)
        {
            work.Charge(1);
            tokens = Saturate(tokens, child.TokenCount, CssSubstitutedValue.MaxTokens);
            spelling = Saturate(spelling, child.SpellingLength, CssSubstitutedValue.MaxSpelling);
            depth = System.Math.Max(depth, child.Depth);
        }
        return new CssSegment(CssSegmentKind.Concat, null, default, default,
            new CssSegmentList(children, work), tokens, spelling, depth);
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
            values.Push(Container(input, component, children, work));
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
        return Concat(rootChildren, work);
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
