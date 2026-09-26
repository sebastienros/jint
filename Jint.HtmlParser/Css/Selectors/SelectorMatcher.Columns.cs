using System.Numerics;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;
using ComplexSelector = Jint.HtmlParser.Css.Selectors.CompiledSelector.Complex;

namespace Jint.HtmlParser.Css.Selectors;

// Selectors 5 §9: https://drafts.csswg.org/selectors-5/#table-pseudos
// A column relation is supplied by HTML table formation, not sibling position or layout.
internal static partial class SelectorMatcher
{
    private static bool TryCellGrid(Element cell, ref Work work, out HtmlTableGrid grid,
        out long start, out long end)
    {
        grid = null!;
        start = end = 0;
        work.Step();
        if (!IsTableRole(cell, "td") && !IsTableRole(cell, "th")) return false;
        if (cell.ParentNode is not Element row || !IsTableRole(row, "tr")) return false;
        work.Step();
        var parent = row.ParentNode as Element;
        if (parent is null) return false;
        Element? table;
        if (IsTableRole(parent, "table")) table = parent;
        else if (IsTableRole(parent, "thead") || IsTableRole(parent, "tbody") ||
                 IsTableRole(parent, "tfoot")) table = parent.ParentNode as Element;
        else return false;
        work.Step();
        if (!IsTableRole(table, "table")) return false;
        grid = work.GridFor(table!);
        return grid.TryGetCellColumns(cell, out start, out end);
    }

    private static bool TryColumnGrid(Element column, ref Work work, out HtmlTableGrid grid,
        out long start, out long end)
    {
        grid = null!;
        start = end = 0;
        work.Step();
        if (!IsTableRole(column, "col") || column.ParentNode is not Element group ||
            !IsTableRole(group, "colgroup") || group.ParentNode is not Element table ||
            !IsTableRole(table, "table")) return false;
        grid = work.GridFor(table);
        return grid.TryGetColumnColumns(column, out start, out end);
    }

    private static bool IsTableRole(Element? element, string name) =>
        element is not null && element.NamespaceUri == Namespaces.Html && element.LocalName == name;

    private static bool MatchColumnPosition(Predicate predicate, Element element, ref Work work)
    {
        if (!TryCellGrid(element, ref work, out var grid, out var start, out var end)) return false;
        var low = predicate.Kind == PredicateKind.NthLastCol ? grid.ColumnCount - end + 1 : start + 1;
        var high = predicate.Kind == PredicateKind.NthLastCol ? grid.ColumnCount - start : end;
        return IntersectsAnPlusB(low, high, predicate.A, predicate.B, ref work);
    }

    private static bool IntersectsAnPlusB(long low, long high, BigInteger a, BigInteger b, ref Work work)
    {
        work.Check();
        if (a.IsZero) return low <= b && b <= high;
        BigInteger minimum, maximum;
        if (a.Sign > 0)
        {
            minimum = CeilingDivide(low - b, a, ref work);
            maximum = FloorDivide(high - b, a, ref work);
        }
        else
        {
            var positive = BigInteger.Negate(a);
            work.Check();
            minimum = CeilingDivide(b - high, positive, ref work);
            maximum = FloorDivide(b - low, positive, ref work);
        }
        work.Check();
        return maximum >= BigInteger.Zero && BigInteger.Max(minimum, BigInteger.Zero) <= maximum;
    }

    private static BigInteger FloorDivide(BigInteger numerator, BigInteger positiveDenominator, ref Work work)
    {
        work.Check();
        var quotient = BigInteger.DivRem(numerator, positiveDenominator, out var remainder);
        work.Check();
        return remainder.Sign < 0 ? quotient - BigInteger.One : quotient;
    }

    private static BigInteger CeilingDivide(BigInteger numerator, BigInteger positiveDenominator, ref Work work)
    {
        work.Check();
        var quotient = BigInteger.DivRem(numerator, positiveDenominator, out var remainder);
        work.Check();
        return remainder.Sign > 0 ? quotient + BigInteger.One : quotient;
    }

    private static IEnumerator<Element>? ColumnPredecessors(Node node, ref Work work)
    {
        if (node is not Element cell || !TryCellGrid(cell, ref work, out var grid,
                out var start, out var end)) return null;
        return grid.ColumnsOverlapping(start, end, work.Checkpoint, work.Token).GetEnumerator();
    }

    private static bool ColumnLeadingMatches(Node first, Node anchor, ref Work work)
    {
        if (first is not Element cell || anchor is not Element column ||
            !TryCellGrid(cell, ref work, out var cellGrid, out var cellStart, out var cellEnd) ||
            !TryColumnGrid(column, ref work, out var columnGrid, out var columnStart, out var columnEnd) ||
            !ReferenceEquals(cellGrid, columnGrid)) return false;
        return cellStart < columnEnd && columnStart < cellEnd;
    }

    private static IEnumerator<Element>? ColumnSuccessors(Node node, ref Work work)
    {
        if (node is not Element column || !TryColumnGrid(column, ref work, out var grid,
                out var start, out var end)) return null;
        return grid.CellsOverlapping(start, end, work.Checkpoint, work.Token).GetEnumerator();
    }

    private static bool HasColumnEdge(ComplexSelector branch, ref Work work)
    {
        if (branch.LeadingCombinator == Combinator.Column) return true;
        foreach (var edge in branch.Combinators)
        {
            work.Step();
            if (edge == Combinator.Column) return true;
        }
        return false;
    }

    // Explore structural paths from the anchor. Predicate and scope checks stay
    // in the backwards evaluator; this walk only discovers possible subjects.
    private static Element? NextColumnRelativeCandidate(EvaluationFrame frame, ComplexSelector branch, ref Work work)
    {
        frame.Forward ??= new List<ForwardState> { new(frame.Node, 0) };
        frame.ForwardVisited ??= new HashSet<(int Position, Node Node)>();
        while (frame.Forward.Count != 0)
        {
            work.Step();
            var state = frame.Forward[^1];
            var edge = state.Position == 0
                ? branch.LeadingCombinator ?? Combinator.Descendant
                : branch.Combinators[state.Position - 1];
            var candidate = NextForwardCandidate(state, edge, ref work);
            if (candidate is null)
            {
                state.Columns?.Dispose();
                frame.Forward.RemoveAt(frame.Forward.Count - 1);
                continue;
            }
            if (!frame.ForwardVisited.Add((state.Position, candidate))) continue;
            if (state.Position == branch.Compounds.Count - 1) return candidate;
            frame.Forward.Add(new ForwardState(candidate, state.Position + 1));
        }
        return null;
    }

    private static Element? NextForwardCandidate(ForwardState state, Combinator edge, ref Work work)
    {
        switch (edge)
        {
            case Combinator.NextSibling:
                if (state.Started) return null;
                state.Started = true;
                return NextElementSibling(state.Source, ref work);
            case Combinator.SubsequentSibling:
                state.Cursor = NextElementSibling(state.Cursor ?? state.Source, ref work);
                return state.Cursor as Element;
            case Combinator.Column:
                if (!state.Started)
                {
                    state.Started = true;
                    state.Columns = ColumnSuccessors(state.Source, ref work);
                }
                return state.Columns is not null && state.Columns.MoveNext() ? state.Columns.Current : null;
            case Combinator.Child:
            case Combinator.Descendant:
                var next = state.Started
                    ? edge == Combinator.Child ? state.Cursor?.NextSibling
                        : state.Cursor is null ? null : NextWithin(state.Source, state.Cursor, ref work)
                    : state.Source.FirstChild;
                state.Started = true;
                while (next is not null)
                {
                    work.Step();
                    state.Cursor = next;
                    if (next is Element element) return element;
                    next = edge == Combinator.Child ? next.NextSibling : NextWithin(state.Source, next, ref work);
                }
                return null;
            default:
                throw Unsupported(edge.ToString());
        }
    }
}
