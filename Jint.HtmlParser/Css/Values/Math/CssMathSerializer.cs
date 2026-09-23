using System.Globalization;
using System.Text;

namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §10.13 and CSSOM component serialization, checked 2026-09-23.
internal static class CssMathSerializer
{
    internal static string SerializeSpecified(CssMathValue value, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var builder = new StringBuilder();
        var root = value.GetNode(value.RootIndex);
        var outerFunction = root.Kind switch
        {
            CssMathNodeKind.Min => "min(",
            CssMathNodeKind.Max => "max(",
            CssMathNodeKind.Clamp => "clamp(",
            _ => "calc("
        };
        Append(builder, outerFunction, work);
        var stack = new Stack<Frame>();
        stack.Push(new Frame(value.RootIndex, true));
        while (stack.Count > 0)
        {
            work.Charge(1);
            var frame = stack.Peek();
            var node = value.GetNode(frame.Index);
            if (!frame.Started)
            {
                frame.Started = true;
                if (node.Kind == CssMathNodeKind.Numeric)
                {
                    AppendNumeric(builder, node.Numeric, frame.IsTop, work);
                    stack.Pop();
                    continue;
                }
                if (node.Kind == CssMathNodeKind.AbsentBound)
                {
                    Append(builder, "none", work); stack.Pop(); continue;
                }
                frame.Children = SortedChildren(value, node, work);
                switch (node.Kind)
                {
                    case CssMathNodeKind.Min: Append(builder, frame.IsTop ? "" : "min(", work); break;
                    case CssMathNodeKind.Max: Append(builder, frame.IsTop ? "" : "max(", work); break;
                    case CssMathNodeKind.Clamp: Append(builder, frame.IsTop ? "" : "clamp(", work); break;
                    case CssMathNodeKind.Sum:
                    case CssMathNodeKind.Product:
                        if (!frame.IsTop && !frame.Unwrap) Append(builder, "(", work);
                        break;
                    case CssMathNodeKind.Negate: Append(builder, "(-1 * ", work); break;
                    case CssMathNodeKind.Invert: Append(builder, "(1 / ", work); break;
                }
            }
            if (frame.Position == frame.Children.Length)
            {
                if (!frame.IsTop && !frame.Unwrap && node.Kind != CssMathNodeKind.Numeric ||
                    !frame.IsTop && node.Kind is CssMathNodeKind.Min or CssMathNodeKind.Max or CssMathNodeKind.Clamp)
                    Append(builder, ")", work);
                stack.Pop();
                continue;
            }
            var childIndex = frame.Children[frame.Position];
            var child = value.GetNode(childIndex);
            if (frame.Position > 0)
            {
                if (node.Kind == CssMathNodeKind.Sum)
                {
                    if (child.Kind == CssMathNodeKind.Negate)
                    {
                        Append(builder, " - ", work);
                        childIndex = value.GetChild(child.ChildStart);
                    }
                    else if (child.Kind == CssMathNodeKind.Numeric && child.Numeric.Value < 0)
                    {
                        Append(builder, " - ", work);
                        AppendNumeric(builder, new CssMathNumeric(-child.Numeric.Value, child.Numeric.Kind,
                            child.Numeric.Unit, child.Numeric.Span), false, work);
                        frame.Position++;
                        continue;
                    }
                    else Append(builder, " + ", work);
                }
                else if (node.Kind == CssMathNodeKind.Product)
                {
                    if (child.Kind == CssMathNodeKind.Invert)
                    {
                        Append(builder, " / ", work);
                        childIndex = value.GetChild(child.ChildStart);
                    }
                    else Append(builder, " * ", work);
                }
                else if (node.Kind is CssMathNodeKind.Min or CssMathNodeKind.Max or CssMathNodeKind.Clamp)
                    Append(builder, ", ", work);
            }
            frame.Position++;
            stack.Push(new Frame(childIndex, false,
                node.Kind is CssMathNodeKind.Min or CssMathNodeKind.Max or CssMathNodeKind.Clamp &&
                value.GetNode(childIndex).Kind is CssMathNodeKind.Sum or CssMathNodeKind.Product));
        }
        Append(builder, ")", work);
        work.CheckCancellation();
        var result = builder.ToString();
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
    }

    private static int[] SortedChildren(CssMathValue value, CssMathNode node, CssValueWork work)
    {
        var children = new int[node.ChildCount];
        for (var i = 0; i < children.Length; i++) { work.Charge(1); children[i] = value.GetChild(node.ChildStart + i); }
        if (node.Kind is not (CssMathNodeKind.Sum or CssMathNodeKind.Product) || children.Length < 2) return children;
        // The finite unit inventory bounds the bucket walk independently of child count.
        var sorted = new int[children.Length];
        var count = 0;
        for (var bucket = 0; bucket < 2; bucket++)
        {
            foreach (var child in children)
            {
                work.Charge(1);
                var n = value.GetNode(child);
                if (n.Kind == CssMathNodeKind.Numeric && n.Numeric.Kind == (bucket == 0 ? CssNumericKind.Number : CssNumericKind.Percentage))
                    sorted[count++] = child;
            }
        }
        // Unit names are sorted by ASCII-insensitive spelling, as CSS Values requires.
        foreach (var unit in OrderedUnits)
        {
            var name = UnitName(unit);
            foreach (var child in children)
            {
                work.Charge(1);
                var n = value.GetNode(child);
                if (n.Kind == CssMathNodeKind.Numeric && n.Numeric.Kind == CssNumericKind.Dimension &&
                    UnitName(n.Numeric.Unit).Equals(name, StringComparison.OrdinalIgnoreCase)) sorted[count++] = child;
            }
        }
        foreach (var child in children)
        {
            work.Charge(1);
            var n = value.GetNode(child);
            if (n.Kind != CssMathNodeKind.Numeric) sorted[count++] = child;
        }
        return sorted;
    }

    private static void AppendNumeric(StringBuilder builder, CssMathNumeric numeric, bool top, CssValueWork work)
    {
        var value = numeric.Value;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            Append(builder, double.IsNaN(value) ? "NaN" : value < 0 ? "-infinity" : "infinity", work);
            if (numeric.Kind != CssNumericKind.Number)
            {
                Append(builder, " * 1", work);
                Append(builder, numeric.Kind == CssNumericKind.Percentage ? "%" : UnitName(numeric.Unit), work);
            }
            return;
        }
        if (value == 0 && double.IsNegative(value) && !top)
        {
            Append(builder, "(-1 * 0", work);
            Append(builder, numeric.Kind == CssNumericKind.Percentage ? "%" :
                numeric.Kind == CssNumericKind.Dimension ? UnitName(numeric.Unit) : "", work);
            Append(builder, ")", work);
            return;
        }
        if (value == 0) value = 0;
        Span<char> scratch = stackalloc char[384];
        work.CheckCancellation();
        if (!value.TryFormat(scratch, out var length, "F6", CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Finite CSS number exceeded the formatter bound.");
        work.CheckCancellation();
        while (length > 0 && scratch[length - 1] == '0' && scratch[..length].IndexOf('.') >= 0) length--;
        if (length > 0 && scratch[length - 1] == '.') length--;
        if (scratch[..length].SequenceEqual("-0"))
        {
            Append(builder, "0", work);
        }
        else
        {
            work.CheckCancellation();
            var grows = length > builder.Capacity - builder.Length;
            builder.Append(scratch[..length]);
            work.Charge(length);
            if (grows) work.CheckCancellation();
        }
        if (numeric.Kind == CssNumericKind.Percentage) Append(builder, "%", work);
        else if (numeric.Kind == CssNumericKind.Dimension) Append(builder, UnitName(numeric.Unit), work);
    }

    private static readonly string[] UnitNames = Enum.GetValues<CssUnit>()
        .Select(static unit => unit.ToString().ToLowerInvariant()).ToArray();

    private static readonly CssUnit[] OrderedUnits = Enum.GetValues<CssUnit>()
        .Where(static unit => unit != CssUnit.None)
        .OrderBy(UnitName, StringComparer.OrdinalIgnoreCase).ToArray();

    private static string UnitName(CssUnit unit) => UnitNames[(int) unit];

    private static void Append(StringBuilder builder, string text, CssValueWork work)
    {
        work.CheckCancellation();
        var grows = text.Length > builder.Capacity - builder.Length;
        builder.Append(text);
        work.Charge(text.Length);
        if (grows) work.CheckCancellation();
    }

    private sealed class Frame(int index, bool isTop, bool unwrap = false)
    {
        internal int Index { get; } = index;
        internal bool IsTop { get; } = isTop;
        internal bool Unwrap { get; } = unwrap;
        internal bool Started { get; set; }
        internal int Position { get; set; }
        internal int[] Children { get; set; } = [];
    }
}
