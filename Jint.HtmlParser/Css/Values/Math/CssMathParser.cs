namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §§10.8–10.9, Editor's Draft 20 August 2026.
internal static class CssMathParser
{
    internal static CssMathParseResult ParseMath(CssComponentValue value, CssMathContext context, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        context.Guard();
        work.CheckCancellation();
        var classification = Classify(value, context, work);
        if (classification.Status != CssMathParseStatus.None) return classification;
        if (value.Kind != CssComponentKind.Function || !IsImplemented(Recognize(value.FunctionName)))
            return Finish(CssMathParseResult.NoMatch(value.Span), work);
        var builder = new CssMathBuilder(work);
        var frames = new Stack<Frame>();
        frames.Push(new Frame(value, Recognize(value.FunctionName), 1));
        while (frames.Count > 0)
        {
            work.Charge(1);
            var frame = frames.Peek();
            if (frame.Index == frame.Values.Count)
            {
                if (!frame.Finish(builder, work, out var root, out var error))
                    return Finish(CssMathParseResult.NoMatch(error), work);
                frames.Pop();
                if (frames.Count == 0)
                {
                    if (!builder.Node(root).Type.Matches(context))
                        return Finish(CssMathParseResult.NoMatch(value.Span), work);
                    var accepted = CssMathSimplifier.Freeze(builder, root, context, value.Span, work);
                    return Finish(CssMathParseResult.Match(accepted), work);
                }
                frames.Peek().AddOperand(root);
                continue;
            }
            var component = frame.Values[frame.Index++];
            if (component.Kind == CssComponentKind.Token)
            {
                var token = component.Token;
                if (token.Kind == CssTokenKind.Whitespace)
                {
                    frame.PreviousWhitespace = true;
                    continue;
                }
                if (token.Kind == CssTokenKind.Comma)
                {
                    if (!frame.OnComma(builder, component.Span))
                        return Finish(CssMathParseResult.NoMatch(frame.FailureSpan ?? component.Span), work);
                    continue;
                }
                if (token.Kind == CssTokenKind.Delim && token.Delimiter is '+' or '-' or '*' or '/')
                {
                    var afterWhitespace = frame.Index < frame.Values.Count &&
                        frame.Values[frame.Index].Kind == CssComponentKind.Token &&
                        frame.Values[frame.Index].Token.Kind == CssTokenKind.Whitespace;
                    if (!frame.OnOperator(builder, token.Delimiter, component.Span, afterWhitespace))
                        return Finish(CssMathParseResult.NoMatch(frame.FailureSpan ?? component.Span), work);
                    continue;
                }
                if (!frame.ExpectingOperand)
                    return Finish(CssMathParseResult.NoMatch(component.Span), work);
                if (token.Kind is CssTokenKind.Number or CssTokenKind.Percentage or CssTokenKind.Dimension)
                {
                    if (token.Kind == CssTokenKind.Percentage && context.Percentages == CssMathPercentageMode.Forbidden)
                        return Finish(CssMathParseResult.NoMatch(component.Span), work);
                    var unit = token.Kind == CssTokenKind.Dimension ? CssUnits.Recognize(token.Unit, work) : CssUnit.None;
                    if (token.Kind == CssTokenKind.Dimension && unit == CssUnit.None)
                        return Finish(CssMathParseResult.NoMatch(component.Span), work);
                    var numeric = CssMathNumbers.FromToken(token, context.Percentages, work);
                    var type = token.Kind switch
                    {
                        CssTokenKind.Percentage => CssNumericType.Percentage(context.Percentages),
                        CssTokenKind.Dimension => CssNumericType.FromUnit(unit),
                        _ => default
                    };
                    frame.AddOperand(builder.Add(CssMathNodeKind.Numeric, type, component.Span, numeric));
                    continue;
                }
                if (token.Kind == CssTokenKind.Ident)
                {
                    var name = token.Text;
                    if (frame.TryStrategy(name, work)) continue;
                    if (AsciiEquals(name, "none") &&
                        frame.Kind == CssMathFunction.Clamp && frame.Operands.Count == 0 &&
                        frame.Arguments.Count is 0 or 2)
                    {
                        frame.AddOperand(builder.Add(CssMathNodeKind.AbsentBound, default, component.Span));
                        continue;
                    }
                    double? constant = AsciiEquals(name, "e") ? System.Math.E :
                        AsciiEquals(name, "pi") ? System.Math.PI :
                        AsciiEquals(name, "infinity") ? double.PositiveInfinity :
                        AsciiEquals(name, "-infinity") ? double.NegativeInfinity :
                        AsciiEquals(name, "NaN") ? double.NaN : null;
                    if (constant is null) return Finish(CssMathParseResult.NoMatch(component.Span), work);
                    frame.AddOperand(builder.Add(CssMathNodeKind.Numeric, default, component.Span,
                        new CssMathNumeric(constant.Value, CssNumericKind.Number, CssUnit.None, component.Span)));
                    continue;
                }
                return Finish(CssMathParseResult.NoMatch(component.Span), work);
            }
            if (!frame.ExpectingOperand)
                return Finish(CssMathParseResult.NoMatch(component.Span), work);
            if (component.Kind == CssComponentKind.SimpleBlock && component.OpeningDelimiter == '(')
            {
                frames.Push(new Frame(component, CssMathFunction.None, frame.Depth + 1));
                continue;
            }
            if (component.Kind == CssComponentKind.Function && IsImplemented(Recognize(component.FunctionName)))
            {
                frames.Push(new Frame(component, Recognize(component.FunctionName), frame.Depth + 1));
                continue;
            }
            return Finish(CssMathParseResult.NoMatch(component.Span), work);
        }
        throw new InvalidOperationException("Math parser lost its root frame.");
    }

    private static CssMathParseResult Classify(CssComponentValue root, CssMathContext context, CssValueWork work)
    {
        var stack = new Stack<(CssComponentValue Component, int Depth)>();
        CssMathFunction pending = CssMathFunction.None;
        CssSourceSpan pendingSpan = default;
        stack.Push((root, context.AncestorNestingDepth));
        while (stack.Count > 0)
        {
            work.Charge(1);
            var (component, parentDepth) = stack.Pop();
            if (component.Kind is not (CssComponentKind.Function or CssComponentKind.SimpleBlock)) continue;
            var depth = checked(parentDepth + 1);
            if (context.MaximumNestingDepth != 0 && depth > context.MaximumNestingDepth)
                throw new ParseLimitException(ParseLimitKind.NestingDepth, context.MaximumNestingDepth, depth);
            if (component.Kind == CssComponentKind.Function)
            {
                var function = Recognize(component.FunctionName);
                if (function != CssMathFunction.None && !IsImplemented(function) && pending == CssMathFunction.None)
                {
                    pending = function;
                    pendingSpan = component.Span;
                }
            }
            var values = component.Values;
            for (var i = values.Count - 1; i >= 0; i--)
            {
                work.Charge(1);
                stack.Push((values[i], depth));
            }
        }
        work.CheckCancellation();
        return pending == CssMathFunction.None ? default :
            Finish(CssMathParseResult.Pending(pendingSpan, pending), work);
    }

    private static CssMathParseResult Finish(CssMathParseResult result, CssValueWork work)
    {
        work.CheckCancellation();
        return result;
    }

    private static bool IsImplemented(CssMathFunction function) => function is
        CssMathFunction.Calc or CssMathFunction.Min or CssMathFunction.Max or CssMathFunction.Clamp or
        CssMathFunction.Round or CssMathFunction.Mod or CssMathFunction.Rem;

    private static bool AsciiEquals(string source, string expected)
    {
        if (source.Length != expected.Length) return false;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch is >= 'A' and <= 'Z') ch = (char) (ch + ('a' - 'A'));
            if (ch != expected[i]) return false;
        }
        return true;
    }

    internal static CssMathFunction Recognize(string name)
    {
        // Match only ASCII spellings; decoded non-ASCII identifiers stay outside this grammar.
        return name.Length switch
        {
            3 when AsciiEquals(name, "min") => CssMathFunction.Min,
            3 when AsciiEquals(name, "max") => CssMathFunction.Max,
            3 when AsciiEquals(name, "mod") => CssMathFunction.Mod,
            3 when AsciiEquals(name, "rem") => CssMathFunction.Rem,
            3 when AsciiEquals(name, "sin") => CssMathFunction.Sin,
            3 when AsciiEquals(name, "cos") => CssMathFunction.Cos,
            3 when AsciiEquals(name, "tan") => CssMathFunction.Tan,
            3 when AsciiEquals(name, "pow") => CssMathFunction.Pow,
            3 when AsciiEquals(name, "log") => CssMathFunction.Log,
            3 when AsciiEquals(name, "exp") => CssMathFunction.Exp,
            3 when AsciiEquals(name, "abs") => CssMathFunction.Abs,
            4 when AsciiEquals(name, "calc") => CssMathFunction.Calc,
            4 when AsciiEquals(name, "asin") => CssMathFunction.Asin,
            4 when AsciiEquals(name, "acos") => CssMathFunction.Acos,
            4 when AsciiEquals(name, "atan") => CssMathFunction.Atan,
            4 when AsciiEquals(name, "sqrt") => CssMathFunction.Sqrt,
            4 when AsciiEquals(name, "sign") => CssMathFunction.Sign,
            5 when AsciiEquals(name, "clamp") => CssMathFunction.Clamp,
            5 when AsciiEquals(name, "round") => CssMathFunction.Round,
            5 when AsciiEquals(name, "atan2") => CssMathFunction.Atan2,
            5 when AsciiEquals(name, "hypot") => CssMathFunction.Hypot,
            _ => CssMathFunction.None
        };
    }

    private sealed class Frame
    {
        internal Frame(CssComponentValue component, CssMathFunction kind, int depth)
        {
            Component = component; Values = component.Values; Kind = kind; Depth = depth;
        }

        internal CssComponentValue Component { get; }
        internal CssComponentValueList Values { get; }
        internal CssMathFunction Kind { get; }
        internal int Depth { get; }
        internal int Index { get; set; }
        internal bool ExpectingOperand { get; private set; } = true;
        internal bool PreviousWhitespace { get; set; }
        internal List<int> Operands { get; } = [];
        internal List<(char Op, CssSourceSpan Span)> Operators { get; } = [];
        internal List<int> Arguments { get; } = [];
        internal CssSourceSpan? FailureSpan { get; private set; }
        internal CssRoundingStrategy Strategy { get; private set; } = CssRoundingStrategy.Nearest;
        private bool _hasStrategy;
        private bool _strategyConsumed;

        internal bool TryStrategy(string name, CssValueWork work)
        {
            if (Kind != CssMathFunction.Round || Arguments.Count != 0 || Operands.Count != 0 ||
                Operators.Count != 0 || _strategyConsumed) return false;
            var strategy = name.Length switch
            {
                2 when AsciiEquals(name, "up") => CssRoundingStrategy.Up,
                4 when AsciiEquals(name, "down") => CssRoundingStrategy.Down,
                7 when AsciiEquals(name, "nearest") => CssRoundingStrategy.Nearest,
                7 when AsciiEquals(name, "to-zero") => CssRoundingStrategy.ToZero,
                10 when AsciiEquals(name, "line-width") => CssRoundingStrategy.LineWidth,
                _ => (CssRoundingStrategy) (-1)
            };
            if ((int) strategy < 0) return false;
            work.Charge(name.Length);
            // A strategy is a whole first comma-delimited argument.
            var next = Index;
            while (next < Values.Count && Values[next].Kind == CssComponentKind.Token &&
                   Values[next].Token.Kind == CssTokenKind.Whitespace)
            {
                work.Charge(1);
                next++;
            }
            if (next >= Values.Count || Values[next].Kind != CssComponentKind.Token ||
                Values[next].Token.Kind != CssTokenKind.Comma) return false;
            Strategy = strategy;
            _hasStrategy = true;
            _strategyConsumed = true;
            return true;
        }

        internal void AddOperand(int operand)
        {
            Operands.Add(operand);
            ExpectingOperand = false;
            PreviousWhitespace = false;
        }

        internal bool OnOperator(CssMathBuilder builder, char op, CssSourceSpan span, bool afterWhitespace)
        {
            if (ExpectingOperand || op is '+' or '-' && (!PreviousWhitespace || !afterWhitespace)) return false;
            if (builder.Node(Operands[^1]).Kind == CssMathNodeKind.AbsentBound) return false;
            var precedence = op is '*' or '/' ? 2 : 1;
            while (Operators.Count > 0 && (Operators[^1].Op is '*' or '/' ? 2 : 1) >= precedence)
            {
                if (!Reduce(builder)) return false;
            }
            Operators.Add((op, span));
            ExpectingOperand = true;
            PreviousWhitespace = false;
            return true;
        }

        internal bool OnComma(CssMathBuilder builder, CssSourceSpan span)
        {
            if (Kind == CssMathFunction.Round && _hasStrategy && Arguments.Count == 0 && ExpectingOperand)
            {
                _hasStrategy = false;
                return true;
            }
            if (Kind is not (CssMathFunction.Min or CssMathFunction.Max or CssMathFunction.Clamp or
                CssMathFunction.Round or CssMathFunction.Mod or CssMathFunction.Rem)) return false;
            if (!CompleteArgument(builder, out var root)) return false;
            Arguments.Add(root);
            Operands.Clear(); Operators.Clear(); ExpectingOperand = true; PreviousWhitespace = false;
            return Kind switch
            {
                CssMathFunction.Clamp => Arguments.Count < 3,
                CssMathFunction.Round or CssMathFunction.Mod or CssMathFunction.Rem => Arguments.Count < 2,
                _ => true
            };
        }

        internal bool Finish(CssMathBuilder builder, CssValueWork work, out int root, out CssSourceSpan error)
        {
            root = -1;
            error = new CssSourceSpan(Component.Span.Start + Component.Span.Length - (Component.IsClosed ? 1 : 0), 0);
            if (!CompleteArgument(builder, out var argument))
            {
                error = FailureSpan ?? error;
                return false;
            }
            if (Kind is CssMathFunction.None or CssMathFunction.Calc)
            {
                if (Arguments.Count != 0 || builder.Node(argument).Kind == CssMathNodeKind.AbsentBound) return false;
                root = argument;
                if (Kind == CssMathFunction.Calc && !builder.Node(root).Type.IsPermissibleScalar)
                { error = Component.Span; return false; }
                return true;
            }
            Arguments.Add(argument);
            if (Kind == CssMathFunction.Clamp && Arguments.Count != 3 ||
                Kind is CssMathFunction.Mod or CssMathFunction.Rem && Arguments.Count != 2 ||
                Kind == CssMathFunction.Round && Arguments.Count is < 1 or > 2 ||
                Kind is CssMathFunction.Min or CssMathFunction.Max && Arguments.Count < 1)
                return false;
            CssNumericType? type = null;
            foreach (var child in Arguments)
            {
                work.Charge(1);
                var node = builder.Node(child);
                if (node.Kind == CssMathNodeKind.AbsentBound)
                {
                    if (Kind != CssMathFunction.Clamp || child == Arguments[1])
                    { error = node.Span; return false; }
                    continue;
                }
                if (!node.Type.IsPermissibleScalar) { error = node.Span; return false; }
                if (type is null) type = node.Type;
                else if (!type.Value.TryAdd(node.Type, out var sumType)) { error = node.Span; return false; }
                else type = sumType;
            }
            if (type is null) return false;
            if (Kind == CssMathFunction.Round && Arguments.Count == 1 &&
                (Strategy == CssRoundingStrategy.LineWidth ? type.Value.Length != 1 ||
                    !type.Value.IsPermissibleScalar : !type.Value.IsScalar))
            { error = builder.Node(Arguments[0]).Span; return false; }
            if (Kind == CssMathFunction.Round && Strategy == CssRoundingStrategy.LineWidth &&
                (builder.Node(Arguments[0]).Type.Length != 1 ||
                 !builder.Node(Arguments[0]).Type.IsPermissibleScalar))
            { error = builder.Node(Arguments[0]).Span; return false; }
            if (Kind == CssMathFunction.Round && Arguments.Count == 2)
            {
                var step = builder.Node(Arguments[1]);
                if (step.Kind == CssMathNodeKind.Numeric && step.Numeric.Kind == CssNumericKind.Number &&
                    step.Numeric.Value == 1 && builder.Node(Arguments[0]).Type.IsScalar)
                    Arguments.RemoveAt(1);
            }
            root = builder.Add(Kind switch
            {
                CssMathFunction.Min => CssMathNodeKind.Min,
                CssMathFunction.Max => CssMathNodeKind.Max,
                CssMathFunction.Clamp => CssMathNodeKind.Clamp,
                CssMathFunction.Round => CssMathNodeKind.Round,
                CssMathFunction.Mod => CssMathNodeKind.Mod,
                _ => CssMathNodeKind.Rem
            }, type.Value, Component.Span, children: Arguments, roundingStrategy: Strategy);
            return true;
        }

        private bool CompleteArgument(CssMathBuilder builder, out int root)
        {
            root = -1;
            if (ExpectingOperand) return false;
            while (Operators.Count > 0) if (!Reduce(builder)) return false;
            if (Operands.Count != 1) return false;
            root = Operands[0];
            return true;
        }

        private bool Reduce(CssMathBuilder builder)
        {
            if (Operands.Count < 2) return false;
            var (op, span) = Operators[^1]; Operators.RemoveAt(Operators.Count - 1);
            var right = Operands[^1]; Operands.RemoveAt(Operands.Count - 1);
            var left = Operands[^1]; Operands.RemoveAt(Operands.Count - 1);
            var leftNode = builder.Node(left);
            var rightNode = builder.Node(right);
            if (rightNode.Kind == CssMathNodeKind.AbsentBound) return false;
            CssNumericType type;
            if (op is '+' or '-')
            {
                if (!leftNode.Type.TryAdd(rightNode.Type, out type))
                {
                    FailureSpan = span;
                    return false;
                }
                if (op == '-') right = builder.Add(CssMathNodeKind.Negate, rightNode.Type, span, children: [right]);
                Operands.Add(builder.Add(CssMathNodeKind.Sum, type, span, children: [left, right]));
            }
            else
            {
                if (op == '/')
                {
                    right = builder.Add(CssMathNodeKind.Invert, rightNode.Type.Invert(), span, children: [right]);
                    rightNode = builder.Node(right);
                }
                if (!leftNode.Type.TryMultiply(rightNode.Type, out type))
                {
                    FailureSpan = span;
                    return false;
                }
                Operands.Add(builder.Add(CssMathNodeKind.Product, type, span, children: [left, right]));
            }
            return true;
        }
    }
}

internal sealed class CssMathBuilder(CssValueWork work)
{
    internal struct NodeData
    {
        internal CssMathNodeKind Kind;
        internal CssNumericType Type;
        internal CssSourceSpan Span;
        internal CssMathNumeric Numeric;
        internal CssRoundingStrategy RoundingStrategy;
        internal int FirstChild;
        internal int LastChild;
        internal int NextSibling;
        internal int ChildCount;
    }

    private readonly List<NodeData> _nodes = [];
    internal int Count => _nodes.Count;
    internal NodeData Node(int index) => _nodes[index];
    internal int Add(CssMathNodeKind kind, CssNumericType type, CssSourceSpan span,
        CssMathNumeric numeric = default, List<int>? children = null,
        CssRoundingStrategy roundingStrategy = CssRoundingStrategy.Nearest)
    {
        work.Charge(1);
        var node = new NodeData
        {
            Kind = kind,
            Type = type,
            Span = span,
            Numeric = numeric,
            RoundingStrategy = roundingStrategy,
            FirstChild = -1,
            LastChild = -1,
            NextSibling = -1
        };
        var index = _nodes.Count;
        _nodes.Add(node);
        if (children is not null)
        {
            foreach (var child in children) AddChild(index, child);
        }
        return index;
    }

    internal void AddChild(int parent, int child)
    {
        work.Charge(1);
        var node = _nodes[parent];
        if (node.FirstChild < 0) node.FirstChild = child;
        else
        {
            var last = _nodes[node.LastChild]; last.NextSibling = child; _nodes[node.LastChild] = last;
        }
        node.LastChild = child; node.ChildCount++;
        _nodes[parent] = node;
    }

    internal IEnumerable<int> Children(int parent)
    {
        var child = _nodes[parent].FirstChild;
        while (child >= 0) { yield return child; child = _nodes[child].NextSibling; }
    }

    internal void DetachSibling(int index)
    {
        var node = _nodes[index];
        node.NextSibling = -1;
        _nodes[index] = node;
    }
}
