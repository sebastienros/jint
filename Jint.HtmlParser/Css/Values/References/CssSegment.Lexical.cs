namespace Jint.HtmlParser.Css.Values.References;

// Lexical pieces travel through the same immutable replacement tree as the typed components.
// Trivia contributes no projected token or origin; gaps are captured before any substitution.
internal sealed partial class CssSegment
{
    private static CssSourceSpan Intersect(CssSourceSpan span, CssSourceSpan envelope)
    {
        var start = System.Math.Max(span.Start, envelope.Start);
        var end = System.Math.Min(span.Start + span.Length, envelope.Start + envelope.Length);
        return new(start, System.Math.Max(0, end - start));
    }

    private static CssSegment SourceTrivia(CssReferenceInput input, CssSourceSpan span) =>
        new(CssSegmentKind.Trivia, input, default, default, CssSegmentList.Empty, 0, 0, 0,
            lexicalSpan: span, lexicalLength: System.Math.Min(span.Length, CssSubstitutedValue.MaxSpelling + 1), lexicalPieces: 1);

    private static CssSegment Recovery(string text) =>
        new(CssSegmentKind.Trivia, null, default, default, CssSegmentList.Empty, 0, 0, 0,
            syntheticLexical: text, lexicalLength: System.Math.Min(text.Length, CssSubstitutedValue.MaxSpelling + 1), lexicalPieces: 1);

    // Boundaries add no authored spelling. At most one four-character separator is emitted for each
    // counted boundary, so MaxLexicalPieces also bounds output added by serialization joins.
    private static readonly CssSegment Boundary = new(CssSegmentKind.Trivia, null, default, default,
        CssSegmentList.Empty, 0, 0, 0, substitutionBoundary: true, lexicalPieces: 1);

    internal static CssSegment Substitution(CssSegment replacement, CssValueWork work) =>
        Concat([Boundary, replacement, Boundary], work);

    private static CssSegment[] CaptureGaps(CssReferenceInput input, CssComponentValueList originals,
        CssSegment[] values, CssSourceSpan envelope, CssValueWork work)
    {
        envelope = Intersect(envelope, input.SerializationSpan);
        var result = new List<CssSegment>();
        var position = envelope.Start;
        var end = position + envelope.Length;
        for (var i = 0; i < originals.Count; i++)
        {
            work.Charge(1);
            var span = originals[i].Span;
            var start = System.Math.Min(end, System.Math.Max(position, span.Start));
            if (start > position) result.Add(SourceTrivia(input, new(position, start - position)));
            result.Add(values[i]);
            position = System.Math.Max(position, System.Math.Min(end, span.Start + span.Length));
        }
        if (position < end) result.Add(SourceTrivia(input, new(position, end - position)));
        // EOF lexical recovery belongs to the deepest remaining value list. Containers supply their
        // own C1 implicit closers, so remove those known closers from the captured termination suffix.
        var originalEnd = input.SourceOffset + input.Source.Length;
        var nestedRecovery = false;
        if (originals.Count != 0)
        {
            var last = originals[originals.Count - 1];
            nestedRecovery = last.Kind != CssComponentKind.Token && !last.IsClosed && last.Span.Start + last.Span.Length == originalEnd;
        }
        if (end == originalEnd && !nestedRecovery && input.ValueTermination.Length != 0)
        {
            var unclosed = 0;
            var components = input.Components;
            while (components.Count != 0)
            {
                work.Charge(1);
                var tail = components[components.Count - 1];
                if (tail.Kind == CssComponentKind.Token || tail.IsClosed) break;
                unclosed++;
                components = tail.Values;
            }
            var length = System.Math.Max(0, input.ValueTermination.Length - unclosed);
            if (length != 0)
            {
                work.Charge(length);
                result.Add(Recovery(input.ValueTermination.Substring(0, length)));
            }
        }
        work.Charge(result.Count);
        work.CheckCancellation();
        return result.ToArray();
    }
}
