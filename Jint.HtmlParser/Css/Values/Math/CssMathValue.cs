namespace Jint.HtmlParser.Css.Values.Math;

internal enum CssMathNodeKind { Numeric, Sum, Product, Negate, Invert, Min, Max, Clamp, AbsentBound, Round, Mod, Rem }

internal enum CssRoundingStrategy { Nearest, Up, Down, ToZero, LineWidth }

internal readonly struct CssMathNumeric
{
    internal CssMathNumeric(double value, CssNumericKind kind, CssUnit unit, CssSourceSpan span,
        CssNumber? provenance = null)
    {
        Value = double.IsNaN(value) ? double.NaN : value;
        Kind = kind; Unit = unit; Span = span; Provenance = provenance;
    }

    internal double Value { get; }
    internal CssNumericKind Kind { get; }
    internal CssUnit Unit { get; }
    internal CssSourceSpan Span { get; }
    internal CssNumber? Provenance { get; }
}

internal readonly struct CssMathNode
{
    private readonly CssMathNumeric _numeric;
    internal CssMathNode(CssMathNodeKind kind, CssNumericType type, CssSourceSpan span,
        int childStart, int childCount, CssMathNumeric numeric = default,
        CssRoundingStrategy roundingStrategy = CssRoundingStrategy.Nearest)
    {
        Kind = kind; Type = type; Span = span; ChildStart = childStart; ChildCount = childCount;
        _numeric = numeric; _roundingStrategy = roundingStrategy;
    }

    internal CssMathNodeKind Kind { get; }
    internal CssNumericType Type { get; }
    internal CssSourceSpan Span { get; }
    internal int ChildStart { get; }
    internal int ChildCount { get; }
    internal CssMathNumeric Numeric => Kind == CssMathNodeKind.Numeric ? _numeric :
        throw new InvalidOperationException("This node has no numeric payload.");
    private readonly CssRoundingStrategy _roundingStrategy;
    internal CssRoundingStrategy RoundingStrategy => Kind == CssMathNodeKind.Round ? _roundingStrategy :
        throw new InvalidOperationException("This node has no rounding strategy.");
}

internal sealed class CssMathValue
{
    private readonly CssMathNode[] _nodes;
    private readonly int[] _children;

    internal CssMathValue(CssMathNode[] nodes, int[] children, int rootIndex,
        CssMathContext context, CssSourceSpan span)
    {
        context.Guard();
        _nodes = nodes; _children = children; RootIndex = rootIndex; Context = context; Span = span;
    }

    internal CssNumericType Type => _nodes[RootIndex].Type;
    internal CssMathContext Context { get; }
    internal CssSourceSpan Span { get; }
    internal int NodeCount => _nodes.Length;
    internal int RootIndex { get; }
    internal CssMathNode GetNode(int index) => _nodes[index];
    internal int GetChild(int index) => _children[index];
}
