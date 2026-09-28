using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Transforms;

// https://drafts.csswg.org/css-transforms-2/#individual-transforms
internal static class CssTransformParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int maximumDepth, CssValueWork work)
    {
        work.CheckCancellation();
        if (grammar is not (CssPropertyGrammar.Translate or CssPropertyGrammar.Rotate or CssPropertyGrammar.Scale))
            throw new ArgumentOutOfRangeException(nameof(grammar));
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], CssKeywordSet.None, work) is not null)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword("none", parts[0].Span));
        if (parts.Count == 0) return Invalid();
        var span = new CssSourceSpan(parts[0].Span.Start,
            parts[^1].Span.Start + parts[^1].Span.Length - parts[0].Span.Start);
        if (grammar == CssPropertyGrammar.Rotate) return Rotate(parts, maximumDepth, span, work);
        if (parts.Count > 3) return Invalid();
        var scale = grammar == CssPropertyGrammar.Scale;
        var components = new CssPropertyValue[3];
        for (var i = 0; i < parts.Count; i++)
        {
            work.Charge(1);
            var parsed = Numeric(parts[i], scale ? CssMathProduction.NumberOrPercentage :
                i == 2 ? CssMathProduction.Length : CssMathProduction.LengthPercentage, maximumDepth, work);
            if (parsed.Status != CssPropertyStatus.Valid) return parsed;
            components[i] = parsed.Value;
        }
        if (parts.Count == 1) components[1] = scale ? components[0] : Constant(0, CssUnit.Px, span, work);
        if (parts.Count < 3) components[2] = Constant(scale ? 1 : 0, scale ? CssUnit.None : CssUnit.Px, span, work);
        return Accepted(new(scale ? CssTransformKind.Scale : CssTransformKind.Translate,
            components[0], components[1], components[2], span), work);
    }

    private static CssPropertyResult Rotate(List<CssComponentValue> parts, int maximumDepth,
        CssSourceSpan span, CssValueWork work)
    {
        if (parts.Count is not (1 or 2 or 4)) return Invalid();
        // CSS Transforms 2 §5 requires an angle, including at zero. Try both complete
        // productions while keeping the three numeric axis components together.
        var first = RotateAt(parts, parts.Count - 1, maximumDepth, span, work);
        if (first.Status == CssPropertyStatus.Valid || parts.Count == 1) return first;
        var last = RotateAt(parts, 0, maximumDepth, span, work);
        return last.Status == CssPropertyStatus.Invalid && first.Status == CssPropertyStatus.UnimplementedGrammar
            ? first : last;
    }

    private static CssPropertyResult RotateAt(List<CssComponentValue> parts, int angleIndex,
        int maximumDepth, CssSourceSpan span, CssValueWork work)
    {
        var angle = Numeric(parts[angleIndex], CssMathProduction.Angle, maximumDepth, work);
        if (angle.Status != CssPropertyStatus.Valid) return angle;
        var x = Constant(0, CssUnit.None, span, work);
        var y = x;
        var z = Constant(1, CssUnit.None, span, work);
        var axisStart = angleIndex == 0 ? 1 : 0;
        if (parts.Count == 2)
        {
            var axis = CssPropertyParser.Keyword(parts[axisStart], CssKeywordSet.XYZ, work);
            if (axis is null) return Invalid();
            x = Constant(axis == "x" ? 1 : 0, CssUnit.None, parts[axisStart].Span, work);
            y = Constant(axis == "y" ? 1 : 0, CssUnit.None, parts[axisStart].Span, work);
            z = Constant(axis == "z" ? 1 : 0, CssUnit.None, parts[axisStart].Span, work);
        }
        else if (parts.Count == 4)
        {
            var axis = new CssPropertyValue[3];
            for (var i = 0; i < 3; i++)
            {
                var parsed = Numeric(parts[axisStart + i], CssMathProduction.Number, maximumDepth, work);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                axis[i] = parsed.Value;
            }
            x = axis[0]; y = axis[1]; z = axis[2];
        }
        return Accepted(new(CssTransformKind.Rotate, x, y, z, span, angle.Value), work);
    }

    internal static CssPropertyResult Numeric(CssComponentValue part, CssMathProduction production,
        int maximumDepth, CssValueWork work, bool functionAngle = false, int ancestorDepth = 0, CssMathRange range = default)
    {
        work.CheckCancellation();
        if (part.Kind == CssComponentKind.Token &&
            part.Token.Kind is CssTokenKind.Number or CssTokenKind.Percentage or CssTokenKind.Dimension)
        {
            var token = part.Token;
            var parsed = CssPrimitiveParser.ParseNumericAtom(new CssComponentValueList([part]), work);
            if (!parsed.IsMatch) return Invalid();
            var atom = parsed.Value;
            var unit = atom.Unit;
            var number = atom.Number;
            var admissible = production switch
            {
                CssMathProduction.Number => token.Kind == CssTokenKind.Number,
                CssMathProduction.NumberOrPercentage => token.Kind is CssTokenKind.Number or CssTokenKind.Percentage,
                CssMathProduction.Angle => token.Kind == CssTokenKind.Dimension && unit.Category() == CssUnitCategory.Angle ||
                    functionAngle && token.Kind == CssTokenKind.Number && number.Sign == 0,
                _ => token.Kind == CssTokenKind.Dimension && unit.Category() == CssUnitCategory.Length ||
                    token.Kind == CssTokenKind.Number && number.Sign == 0 ||
                    production == CssMathProduction.LengthPercentage && token.Kind == CssTokenKind.Percentage
            };
            if (!admissible) return Invalid();
            var kind = atom.Kind;
            if (kind == CssNumericKind.Number && production is CssMathProduction.Length or CssMathProduction.LengthPercentage)
            {
                kind = CssNumericKind.Dimension;
                unit = CssUnit.Px;
            }
            if (production == CssMathProduction.Angle && kind == CssNumericKind.Number)
            {
                kind = CssNumericKind.Dimension;
                unit = CssUnit.Deg;
            }
            var finite = CssMathNumbers.ParseFinite(number, CssUnit.None, work);
            if (production == CssMathProduction.NumberOrPercentage && kind == CssNumericKind.Percentage)
                return CssPropertyResult.Accepted(Constant(finite / 100, CssUnit.None, part.Span, work));
            var text = CssMathSerializer.SerializeFiniteNumber(finite, work);
            switch (kind)
            {
                case CssNumericKind.Percentage:
                    text += "%";
                    break;
                case CssNumericKind.Dimension:
                    text += unit.ToString().ToLowerInvariant();
                    break;
            }
            work.Charge(text.Length);
            return CssPropertyResult.Accepted(CssPropertyValue.Number(new(kind, number, unit, token.IsInteger, part.Span), text));
        }
        var percentages = production == CssMathProduction.LengthPercentage ? CssMathPercentageMode.Length :
            production == CssMathProduction.NumberOrPercentage ? CssMathPercentageMode.Raw : CssMathPercentageMode.Forbidden;
        var math = CssMathParser.ParseMath(part, new(production, percentages, range, maximumNestingDepth: maximumDepth, ancestorNestingDepth: ancestorDepth), work);
        switch (math.Status)
        {
            case CssMathParseStatus.RequiresLaterGrammar:
                return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "math:" + math.PendingFunction);
            case not CssMathParseStatus.Match:
                return Invalid();
        }
        var value = production == CssMathProduction.NumberOrPercentage ? ScaleMath(math.Value, work) : math.Value;
        return CssPropertyResult.Accepted(CssPropertyValue.Calculation(value, CssMathSerializer.SerializeSpecified(value, work)));
    }

    // Scale percentages are factors even in specified serialization. Map the typed tree,
    // preserving spans and avoiding either reparsing or a text-based calculation rewrite.
    private static CssMathValue ScaleMath(CssMathValue math, CssValueWork work)
    {
        var builder = new CssMathBuilder(work);
        var mapped = new int[math.NodeCount];
        for (var i = math.NodeCount - 1; i >= 0; i--)
        {
            work.Charge(1);
            var node = math.GetNode(i);
            var children = new List<int>(node.ChildCount);
            for (var j = 0; j < node.ChildCount; j++)
            {
                work.Charge(1);
                children.Add(mapped[math.GetChild(node.ChildStart + j)]);
            }
            var numeric = node.Kind == CssMathNodeKind.Numeric ? node.Numeric : default;
            if (node.Kind == CssMathNodeKind.Numeric && numeric.Kind == CssNumericKind.Percentage)
                numeric = new(numeric.Value / 100, CssNumericKind.Number, CssUnit.None, numeric.Span);
            var type = node.Type;
            mapped[i] = builder.Add(node.Kind, new(type.Length, type.Angle, type.Time, type.Frequency,
                type.Resolution, type.Flex), node.Span, numeric, children,
                node.Kind == CssMathNodeKind.Round ? node.RoundingStrategy : CssRoundingStrategy.Nearest);
        }
        return CssMathSimplifier.Freeze(builder, mapped[math.RootIndex],
            new(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
                maximumNestingDepth: math.Context.MaximumNestingDepth), math.Span, work);
    }

    internal static CssPropertyValue Constant(double value, CssUnit unit, CssSourceSpan span, CssValueWork work)
    {
        var text = CssMathSerializer.SerializeFiniteNumber(value, work);
        var number = CssMathNumbers.FromFiniteNumber(value, work);
        var kind = unit == CssUnit.None ? CssNumericKind.Number : CssNumericKind.Dimension;
        if (kind == CssNumericKind.Dimension) text += unit.ToString().ToLowerInvariant();
        work.Charge(text.Length);
        return CssPropertyValue.Number(new(kind, number, unit, value == System.Math.Truncate(value), span), text);
    }

    private static CssPropertyResult Accepted(CssTransformValue value, CssValueWork work) =>
        CssPropertyResult.Accepted(CssPropertyValue.TransformValue(value, CssTransformSerializer.Serialize(value, work)));
    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
