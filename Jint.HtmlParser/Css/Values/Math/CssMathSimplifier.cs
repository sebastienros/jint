namespace Jint.HtmlParser.Css.Values.Math;

// CSS Values 4 §10.10.1, Editor's Draft 20 August 2026.
internal static class CssMathSimplifier
{
    private const int UnitCount = (int) CssUnit.Fr + 1;

    internal static CssMathValue Freeze(CssMathBuilder source, int root, CssMathContext context,
        CssSourceSpan span, CssValueWork work)
    {
        work.CheckCancellation();
        var count = source.Count;
        var parentKind = new CssMathNodeKind[count];
        var parentIndex = new int[count];
        Array.Fill(parentIndex, -1);
        var hasParent = new bool[count];
        for (var i = 0; i < count; i++)
        {
            foreach (var child in source.Children(i))
            {
                work.Charge(1);
                parentKind[child] = source.Node(i).Kind;
                parentIndex[child] = i;
                hasParent[child] = true;
            }
        }
        // Transparent wrappers can forward a Sum to an outer Sum. Defer its
        // materialization so every term is visited once at that outer boundary.
        var underSum = new bool[count];
        for (var i = count - 1; i >= 0; i--)
        {
            work.Charge(1);
            var parent = parentIndex[i];
            if (parent < 0) continue;
            var parentNode = source.Node(parent);
            underSum[i] = parentNode.Kind == CssMathNodeKind.Sum ||
                underSum[parent] && (parentNode.Kind == CssMathNodeKind.Negate ||
                    (parentNode.Kind is CssMathNodeKind.Min or CssMathNodeKind.Max) &&
                    parentNode.ChildCount == 1 ||
                    parentNode.Kind == CssMathNodeKind.Clamp &&
                    source.Node(parentNode.FirstChild).Kind == CssMathNodeKind.AbsentBound &&
                    source.Node(parentNode.LastChild).Kind == CssMathNodeKind.AbsentBound);
        }
        var target = new CssMathBuilder(work);
        var mapped = new int[count];
        Array.Fill(mapped, -1);
        for (var i = 0; i < count; i++)
        {
            work.Charge(1);
            var node = source.Node(i);
            var negatesSum = node.Kind == CssMathNodeKind.Negate &&
                source.Node(node.FirstChild).Kind == CssMathNodeKind.Sum;
            if (hasParent[i] &&
                (node.Kind == CssMathNodeKind.Sum &&
                 (parentKind[i] is CssMathNodeKind.Sum or CssMathNodeKind.Negate) &&
                 !underSum[parentIndex[i]] ||
                 node.Kind == CssMathNodeKind.Product && parentKind[i] == CssMathNodeKind.Product ||
                 negatesSum && parentKind[i] == CssMathNodeKind.Sum && !underSum[parentIndex[i]]))
                continue;
            if (node.Kind is CssMathNodeKind.Sum or CssMathNodeKind.Product ||
                negatesSum && !underSum[i] ||
                node.Kind == CssMathNodeKind.Negate && !underSum[i] &&
                mapped[node.FirstChild] >= 0 &&
                target.Node(mapped[node.FirstChild]).Kind == CssMathNodeKind.Sum)
            {
                mapped[i] = SimplifyRun(source, target, mapped, i, underSum[i], work);
                continue;
            }
            if (node.Kind == CssMathNodeKind.Numeric || node.Kind == CssMathNodeKind.AbsentBound)
            {
                mapped[i] = target.Add(node.Kind, node.Type, node.Span, node.Numeric);
                continue;
            }
            var children = new List<int>(node.ChildCount);
            foreach (var child in source.Children(i))
            {
                work.Charge(1);
                children.Add(mapped[child]);
            }
            if (node.Kind == CssMathNodeKind.Negate && target.Node(children[0]).Kind == CssMathNodeKind.Numeric)
            {
                var original = target.Node(children[0]).Numeric;
                mapped[i] = target.Add(CssMathNodeKind.Numeric, node.Type, node.Span,
                    new CssMathNumeric(-original.Value, original.Kind, original.Unit, node.Span));
            }
            else if (node.Kind == CssMathNodeKind.Invert && target.Node(children[0]).Kind == CssMathNodeKind.Numeric &&
                     target.Node(children[0]).Type.IsScalar)
            {
                var original = target.Node(children[0]).Numeric;
                mapped[i] = target.Add(CssMathNodeKind.Numeric, node.Type, node.Span,
                    new CssMathNumeric(1d / original.Value, CssNumericKind.Number, CssUnit.None, node.Span));
            }
            else if (node.Kind is CssMathNodeKind.Min or CssMathNodeKind.Max or CssMathNodeKind.Clamp &&
                     TryFoldComparison(target, children, node.Kind, context, work, out var numeric))
            {
                mapped[i] = target.Add(CssMathNodeKind.Numeric, node.Type, node.Span, numeric);
            }
            else if (node.Kind == CssMathNodeKind.Clamp &&
                     (target.Node(children[0]).Kind == CssMathNodeKind.AbsentBound ||
                      target.Node(children[2]).Kind == CssMathNodeKind.AbsentBound))
            {
                var lowerMissing = target.Node(children[0]).Kind == CssMathNodeKind.AbsentBound;
                var upperMissing = target.Node(children[2]).Kind == CssMathNodeKind.AbsentBound;
                if (lowerMissing && upperMissing) mapped[i] = children[1];
                else
                {
                    var operation = lowerMissing ? CssMathNodeKind.Min : CssMathNodeKind.Max;
                    var pair = lowerMissing ? new List<int> { children[1], children[2] } :
                        new List<int> { children[0], children[1] };
                    mapped[i] = SimplifyComparison(target, operation, node.Type, node.Span, pair, context, work);
                }
            }
            else if (node.Kind is CssMathNodeKind.Min or CssMathNodeKind.Max)
            {
                mapped[i] = SimplifyComparison(target, node.Kind, node.Type, node.Span, children, context, work);
            }
            else
            {
                mapped[i] = target.Add(node.Kind, node.Type, node.Span);
                foreach (var child in children) target.AddChild(mapped[i], child);
            }
        }
        var simplifiedRoot = mapped[root];
        if (simplifiedRoot < 0) throw new InvalidOperationException("The math root was not simplified.");
        var seen = new bool[target.Count];
        var stack = new Stack<int>();
        var order = new List<int>();
        stack.Push(simplifiedRoot);
        while (stack.Count > 0)
        {
            work.Charge(1);
            var index = stack.Pop();
            if (seen[index]) continue;
            seen[index] = true;
            order.Add(index);
            foreach (var child in target.Children(index))
            {
                work.Charge(1);
                stack.Push(child);
            }
        }
        var indices = new int[target.Count];
        for (var i = 0; i < order.Count; i++)
        {
            work.Charge(1);
            indices[order[i]] = i;
        }
        var edges = 0;
        foreach (var index in order)
        {
            work.Charge(1);
            edges = checked(edges + target.Node(index).ChildCount);
        }
        work.CheckCancellation();
        var nodes = new CssMathNode[order.Count];
        var childrenArray = new int[edges];
        work.CheckCancellation();
        var offset = 0;
        for (var i = 0; i < order.Count; i++)
        {
            work.Charge(1);
            var node = target.Node(order[i]);
            nodes[i] = new CssMathNode(node.Kind, node.Type, node.Span, offset, node.ChildCount, node.Numeric);
            foreach (var child in target.Children(order[i]))
            {
                work.Charge(1);
                childrenArray[offset++] = indices[child];
            }
        }
        work.CheckCancellation();
        return new CssMathValue(nodes, childrenArray, 0, context, span);
    }

    private static int SimplifyRun(CssMathBuilder source, CssMathBuilder target, int[] mapped,
        int root, bool underSum, CssValueWork work)
    {
        var sourceNode = source.Node(root);
        if (sourceNode.Kind == CssMathNodeKind.Sum && underSum)
        {
            var deferred = target.Add(CssMathNodeKind.Sum, sourceNode.Type, sourceNode.Span);
            foreach (var child in source.Children(root))
            {
                work.Charge(1);
                target.AddChild(deferred, mapped[child]);
            }
            return deferred;
        }
        var leaves = new List<int>();
        var stack = new Stack<(int Index, bool Negative)>();
        var immediate = new List<int>();
        if (sourceNode.Kind is CssMathNodeKind.Sum or CssMathNodeKind.Negate)
            stack.Push((root, false));
        else
        {
            foreach (var child in source.Children(root)) immediate.Add(child);
            for (var i = immediate.Count - 1; i >= 0; i--) stack.Push((immediate[i], false));
        }
        while (stack.Count > 0)
        {
            work.Charge(1);
            var (index, negative) = stack.Pop();
            var node = source.Node(index);
            if (sourceNode.Kind is CssMathNodeKind.Sum or CssMathNodeKind.Negate &&
                node.Kind == CssMathNodeKind.Sum ||
                sourceNode.Kind == CssMathNodeKind.Product && node.Kind == CssMathNodeKind.Product)
            {
                immediate.Clear();
                foreach (var child in source.Children(index)) immediate.Add(child);
                for (var i = immediate.Count - 1; i >= 0; i--) stack.Push((immediate[i], negative));
            }
            else if (sourceNode.Kind is CssMathNodeKind.Sum or CssMathNodeKind.Negate &&
                     node.Kind == CssMathNodeKind.Negate &&
                     (source.Node(node.FirstChild).Kind == CssMathNodeKind.Sum ||
                      mapped[node.FirstChild] >= 0 &&
                      target.Node(mapped[node.FirstChild]).Kind == CssMathNodeKind.Sum))
            {
                stack.Push((node.FirstChild, !negative));
            }
            else if (negative)
            {
                var mappedNode = target.Node(mapped[index]);
                if (mappedNode.Kind == CssMathNodeKind.Numeric)
                {
                    var numeric = mappedNode.Numeric;
                    leaves.Add(target.Add(CssMathNodeKind.Numeric, mappedNode.Type, mappedNode.Span,
                        new CssMathNumeric(-numeric.Value, numeric.Kind, numeric.Unit, numeric.Span)));
                }
                else if (mappedNode.Kind == CssMathNodeKind.Negate)
                    leaves.Add(mappedNode.FirstChild);
                else
                    leaves.Add(target.Add(CssMathNodeKind.Negate, mappedNode.Type, mappedNode.Span,
                        children: [mapped[index]]));
            }
            else leaves.Add(mapped[index]);
        }
        if (sourceNode.Kind is CssMathNodeKind.Sum or CssMathNodeKind.Negate)
        {
            // A child Product can simplify into a Sum after source-run flattening.
            // Expand those transformed children once in this maximal Sum run.
            var flattened = new List<int>(leaves.Count);
            var pending = new Stack<(int Index, bool Negative)>();
            for (var i = leaves.Count - 1; i >= 0; i--)
            {
                work.Charge(1);
                pending.Push((leaves[i], false));
            }
            while (pending.Count > 0)
            {
                work.Charge(1);
                var (candidate, negative) = pending.Pop();
                var candidateNode = target.Node(candidate);
                if (candidateNode.Kind == CssMathNodeKind.Negate &&
                    target.Node(candidateNode.FirstChild).Kind == CssMathNodeKind.Sum)
                {
                    pending.Push((candidateNode.FirstChild, !negative));
                    continue;
                }
                if (candidateNode.Kind != CssMathNodeKind.Sum)
                {
                    if (!negative) flattened.Add(candidate);
                    else if (candidateNode.Kind == CssMathNodeKind.Numeric)
                    {
                        var numeric = candidateNode.Numeric;
                        flattened.Add(target.Add(CssMathNodeKind.Numeric, candidateNode.Type, candidateNode.Span,
                            new CssMathNumeric(-numeric.Value, numeric.Kind, numeric.Unit, numeric.Span)));
                    }
                    else if (candidateNode.Kind == CssMathNodeKind.Negate)
                        flattened.Add(candidateNode.FirstChild);
                    else
                    {
                        // The containing Sum has already been expanded into pending.
                        // Move the opaque leaf so its former sibling is not adopted too.
                        target.DetachSibling(candidate);
                        flattened.Add(target.Add(CssMathNodeKind.Negate, candidateNode.Type, candidateNode.Span,
                            children: [candidate]));
                    }
                    continue;
                }
                immediate.Clear();
                foreach (var child in target.Children(candidate))
                {
                    work.Charge(1);
                    immediate.Add(child);
                }
                for (var i = immediate.Count - 1; i >= 0; i--)
                {
                    work.Charge(1);
                    pending.Push((immediate[i], negative));
                }
            }
            leaves = flattened;
        }
        if (leaves.Count == 1) return leaves[0];
        if (sourceNode.Kind is CssMathNodeKind.Sum or CssMathNodeKind.Negate)
        {
            const int specialKinds = 2;
            var bucketCount = specialKinds + UnitCount;
            var first = new int[bucketCount];
            var totals = new double[bucketCount];
            Array.Fill(first, -1);
            foreach (var leafIndex in leaves)
            {
                work.Charge(1);
                var leaf = target.Node(leafIndex);
                if (leaf.Kind != CssMathNodeKind.Numeric) continue;
                var bucket = leaf.Numeric.Kind switch
                {
                    CssNumericKind.Number => 0,
                    CssNumericKind.Percentage => 1,
                    _ => specialKinds + (int) leaf.Numeric.Unit
                };
                if (first[bucket] < 0) { first[bucket] = leafIndex; totals[bucket] = leaf.Numeric.Value; }
                else totals[bucket] += leaf.Numeric.Value;
            }
            var merged = new List<int>(leaves.Count);
            foreach (var leafIndex in leaves)
            {
                work.Charge(1);
                var leaf = target.Node(leafIndex);
                if (leaf.Kind != CssMathNodeKind.Numeric) { merged.Add(leafIndex); continue; }
                var bucket = leaf.Numeric.Kind switch
                {
                    CssNumericKind.Number => 0,
                    CssNumericKind.Percentage => 1,
                    _ => specialKinds + (int) leaf.Numeric.Unit
                };
                if (first[bucket] != leafIndex) continue;
                merged.Add(target.Add(CssMathNodeKind.Numeric, leaf.Type, leaf.Span,
                    new CssMathNumeric(totals[bucket], leaf.Numeric.Kind, leaf.Numeric.Unit, leaf.Span)));
            }
            leaves = merged;
        }
        else
        {
            if (TryFoldProduct(target, leaves, sourceNode.Type, sourceNode.Span, work, out var product))
                return target.Add(CssMathNodeKind.Numeric, sourceNode.Type, sourceNode.Span, product);
            var scalar = 1d;
            var numericCount = 0;
            var firstScalar = -1;
            foreach (var leafIndex in leaves)
            {
                work.Charge(1);
                var leaf = target.Node(leafIndex);
                if (leaf.Kind != CssMathNodeKind.Numeric || leaf.Numeric.Kind != CssNumericKind.Number) continue;
                numericCount++;
                if (firstScalar < 0) firstScalar = leafIndex;
                scalar *= leaf.Numeric.Value;
            }
            if (numericCount > 1)
            {
                var merged = new List<int>(leaves.Count - numericCount + 1);
                foreach (var leafIndex in leaves)
                {
                    work.Charge(1);
                    var leaf = target.Node(leafIndex);
                    if (leaf.Kind != CssMathNodeKind.Numeric || leaf.Numeric.Kind != CssNumericKind.Number)
                        merged.Add(leafIndex);
                    else if (leafIndex == firstScalar)
                        merged.Add(target.Add(CssMathNodeKind.Numeric, default, sourceNode.Span,
                            new CssMathNumeric(scalar, CssNumericKind.Number, CssUnit.None, sourceNode.Span)));
                }
                leaves = merged;
            }
            if (leaves.Count == 2)
            {
                var scalarIndex = target.Node(leaves[0]).Kind == CssMathNodeKind.Numeric &&
                    target.Node(leaves[0]).Numeric.Kind == CssNumericKind.Number ? leaves[0] : leaves[1];
                var sumIndex = scalarIndex == leaves[0] ? leaves[1] : leaves[0];
                if (target.Node(scalarIndex).Kind == CssMathNodeKind.Numeric &&
                    target.Node(scalarIndex).Numeric.Kind == CssNumericKind.Number &&
                    target.Node(sumIndex).Kind == CssMathNodeKind.Sum)
                {
                    var sumNode = target.Node(sumIndex);
                    var terms = new List<int>();
                    var distributable = true;
                    foreach (var term in target.Children(sumIndex))
                    {
                        work.Charge(1);
                        if (target.Node(term).Kind != CssMathNodeKind.Numeric) { distributable = false; break; }
                        terms.Add(term);
                    }
                    if (distributable)
                    {
                        var sum = target.Add(CssMathNodeKind.Sum, sumNode.Type, sourceNode.Span);
                        var factor = target.Node(scalarIndex).Numeric.Value;
                        foreach (var term in terms)
                        {
                            var numeric = target.Node(term).Numeric;
                            var scaled = target.Add(CssMathNodeKind.Numeric, target.Node(term).Type, numeric.Span,
                                new CssMathNumeric(factor * numeric.Value, numeric.Kind, numeric.Unit, numeric.Span));
                            target.AddChild(sum, scaled);
                        }
                        return sum;
                    }
                }
            }
        }
        if (leaves.Count == 1) return leaves[0];
        var resultNode = target.Add(sourceNode.Kind == CssMathNodeKind.Negate ? CssMathNodeKind.Sum :
            sourceNode.Kind, sourceNode.Type, sourceNode.Span);
        foreach (var leaf in leaves) target.AddChild(resultNode, leaf);
        return resultNode;
    }

    private static bool TryFoldProduct(CssMathBuilder target, List<int> leaves, CssNumericType type,
        CssSourceSpan span, CssValueWork work, out CssMathNumeric result)
    {
        result = default;
        if (!type.IsPermissibleScalar) return false;
        var value = 1d;
        CssMathNumeric? relative = null;
        CssMathNumeric? percentage = null;
        foreach (var leafIndex in leaves)
        {
            work.Charge(1);
            var node = target.Node(leafIndex);
            var inverted = node.Kind == CssMathNodeKind.Invert;
            if (inverted) node = target.Node(node.FirstChild);
            if (node.Kind != CssMathNodeKind.Numeric) return false;
            var numeric = node.Numeric;
            if (numeric.Kind == CssNumericKind.Percentage)
            {
                if (inverted || percentage is not null) return false;
                percentage = numeric;
            }
            else if (numeric.Kind == CssNumericKind.Dimension &&
                     numeric.Unit is not (CssUnit.Px or CssUnit.Deg or CssUnit.S or CssUnit.Hz or CssUnit.Dppx))
            {
                if (inverted || relative is not null) return false;
                relative = numeric;
            }
            value = inverted ? value / numeric.Value : value * numeric.Value;
        }
        CssNumericKind kind;
        CssUnit unit;
        if (percentage is not null)
        {
            if (relative is not null) return false;
            kind = CssNumericKind.Percentage;
            unit = CssUnit.None;
            var percentageMode = type.Hint switch
            {
                CssPercentHint.Length => CssMathPercentageMode.Length,
                CssPercentHint.Angle => CssMathPercentageMode.Angle,
                CssPercentHint.Time => CssMathPercentageMode.Time,
                CssPercentHint.Frequency => CssMathPercentageMode.Frequency,
                CssPercentHint.Resolution => CssMathPercentageMode.Resolution,
                CssPercentHint.Flex => CssMathPercentageMode.Flex,
                _ => CssMathPercentageMode.Raw
            };
            if (!type.Equals(CssNumericType.Percentage(percentageMode))) return false;
        }
        else if (type.IsScalar)
        {
            if (relative is not null) return false;
            kind = CssNumericKind.Number;
            unit = CssUnit.None;
        }
        else
        {
            kind = CssNumericKind.Dimension;
            if (relative is { } unresolved && !type.Equals(CssNumericType.FromUnit(unresolved.Unit)))
                return false;
            unit = relative?.Unit ?? type switch
            {
                { Length: 1 } => CssUnit.Px,
                { Angle: 1 } => CssUnit.Deg,
                { Time: 1 } => CssUnit.S,
                { Frequency: 1 } => CssUnit.Hz,
                { Resolution: 1 } => CssUnit.Dppx,
                { Flex: 1 } => CssUnit.Fr,
                _ => CssUnit.None
            };
            if (unit == CssUnit.None) return false;
        }
        result = new CssMathNumeric(value, kind, unit, span);
        return true;
    }

    private static int SimplifyComparison(CssMathBuilder target, CssMathNodeKind kind,
        CssNumericType type, CssSourceSpan span, List<int> children, CssMathContext context, CssValueWork work)
    {
        var first = new int[2 + UnitCount];
        Array.Fill(first, -1);
        var best = new double[first.Length];
        foreach (var child in children)
        {
            work.Charge(1);
            var node = target.Node(child);
            if (node.Kind != CssMathNodeKind.Numeric ||
                node.Numeric.Kind == CssNumericKind.Percentage && context.Percentages != CssMathPercentageMode.Raw)
                continue;
            var bucket = node.Numeric.Kind switch
            {
                CssNumericKind.Number => 0,
                CssNumericKind.Percentage => 1,
                _ => 2 + (int) node.Numeric.Unit
            };
            if (first[bucket] < 0) { first[bucket] = child; best[bucket] = node.Numeric.Value; }
            else best[bucket] = kind == CssMathNodeKind.Min ?
                CssMathNumbers.CssMin(best[bucket], node.Numeric.Value) :
                CssMathNumbers.CssMax(best[bucket], node.Numeric.Value);
        }
        var reduced = new List<int>(children.Count);
        foreach (var child in children)
        {
            work.Charge(1);
            var node = target.Node(child);
            if (node.Kind != CssMathNodeKind.Numeric ||
                node.Numeric.Kind == CssNumericKind.Percentage && context.Percentages != CssMathPercentageMode.Raw)
            { reduced.Add(child); continue; }
            var bucket = node.Numeric.Kind switch
            {
                CssNumericKind.Number => 0,
                CssNumericKind.Percentage => 1,
                _ => 2 + (int) node.Numeric.Unit
            };
            if (first[bucket] == child)
                reduced.Add(target.Add(CssMathNodeKind.Numeric, node.Type, node.Span,
                    new CssMathNumeric(best[bucket], node.Numeric.Kind, node.Numeric.Unit, node.Span)));
        }
        if (reduced.Count == 1) return reduced[0];
        var result = target.Add(kind, type, span);
        foreach (var child in reduced) target.AddChild(result, child);
        return result;
    }

    private static bool TryFoldComparison(CssMathBuilder target, List<int> children,
        CssMathNodeKind kind, CssMathContext context, CssValueWork work, out CssMathNumeric result)
    {
        result = default;
        var found = false;
        foreach (var child in children)
        {
            work.Charge(1);
            var node = target.Node(child);
            if (node.Kind == CssMathNodeKind.AbsentBound) continue;
            if (node.Kind != CssMathNodeKind.Numeric ||
                node.Numeric.Kind == CssNumericKind.Percentage && context.Percentages is not CssMathPercentageMode.Raw)
                return false;
            if (!found) { result = node.Numeric; found = true; continue; }
            if (result.Unit != node.Numeric.Unit || result.Kind != node.Numeric.Kind) return false;
            var value = kind switch
            {
                CssMathNodeKind.Min => CssMathNumbers.CssMin(result.Value, node.Numeric.Value),
                CssMathNodeKind.Max => CssMathNumbers.CssMax(result.Value, node.Numeric.Value),
                _ => node.Numeric.Value
            };
            result = new CssMathNumeric(value, result.Kind, result.Unit, node.Span);
        }
        if (!found) return false;
        if (kind == CssMathNodeKind.Clamp)
        {
            var lower = target.Node(children[0]);
            var middle = target.Node(children[1]);
            var upper = target.Node(children[2]);
            var value = middle.Numeric.Value;
            if (upper.Kind != CssMathNodeKind.AbsentBound) value = CssMathNumbers.CssMin(value, upper.Numeric.Value);
            if (lower.Kind != CssMathNodeKind.AbsentBound) value = CssMathNumbers.CssMax(value, lower.Numeric.Value);
            result = new CssMathNumeric(value, middle.Numeric.Kind, middle.Numeric.Unit, middle.Span);
        }
        return true;
    }
}
