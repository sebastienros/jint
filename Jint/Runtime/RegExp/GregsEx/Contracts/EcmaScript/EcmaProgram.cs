#nullable enable
using Jint.Runtime.RegExp.GregsEx.IR;

namespace Jint.Runtime.RegExp.GregsEx.Contracts.EcmaScript;

// Validated immutable semantic tree; no executor or production fallback lives here.
internal sealed class EcmaProgram
{
    private readonly EcmaNode[] _nodes;
    private readonly EcmaSetSlice[] _sets;
    private readonly EcmaInterval[] _intervals;
    private readonly EcmaStringSlice[] _strings;
    private readonly int[] _points;
    private readonly EcmaNameSlice[] _names;
    private readonly int[] _aliases;
    internal EcmaScriptProfile Profile { get; }
    internal int Root { get; }
    internal int GroupCount { get; }
    internal int PatternLength { get; }
    internal long RetainedBytes { get; }
    internal ReadOnlySpan<EcmaNode> Nodes => _nodes;
    // The program's own array, for an executor that only reads it: a copy per compile cost as much as the nodes.
    internal EcmaNode[] NodeArray => _nodes;
    internal ReadOnlySpan<EcmaSetSlice> Sets => _sets;
    internal ReadOnlySpan<EcmaInterval> Intervals => _intervals;
    internal ReadOnlySpan<EcmaStringSlice> Strings => _strings;
    internal ReadOnlySpan<int> StringCodePoints => _points;
    internal ReadOnlySpan<EcmaNameSlice> Names => _names;
    internal ReadOnlySpan<int> NameAliases => _aliases;

    private EcmaProgram(EcmaScriptProfile profile, int root, int groupCount, int patternLength,
        long bytes, ReadOnlySpan<EcmaNode> nodes, ReadOnlySpan<EcmaSetSlice> sets,
        ReadOnlySpan<EcmaInterval> intervals, ReadOnlySpan<EcmaStringSlice> strings,
        ReadOnlySpan<int> points, ReadOnlySpan<EcmaNameSlice> names, ReadOnlySpan<int> aliases)
    {
        Profile = profile;
        Root = root;
        GroupCount = groupCount;
        PatternLength = patternLength;
        RetainedBytes = bytes;
        _nodes = nodes.ToArray();
        _sets = sets.ToArray();
        _intervals = intervals.ToArray();
        _strings = strings.ToArray();
        _points = points.ToArray();
        _names = names.ToArray();
        _aliases = aliases.ToArray();
    }

    internal static bool TryCreate(EcmaScriptProfile profile, int root, int groupCount,
        int patternLength, ReadOnlySpan<EcmaNode> nodes, ReadOnlySpan<EcmaSetSlice> sets,
        ReadOnlySpan<EcmaInterval> intervals, ReadOnlySpan<EcmaStringSlice> strings,
        ReadOnlySpan<int> points, ReadOnlySpan<EcmaNameSlice> names, ReadOnlySpan<int> aliases,
        ref CompileBudget budget, out EcmaProgram? program, out RegexDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(profile);
        program = null;
        diagnostic = default;
        var source = new SourceRange(0, Math.Max(0, patternLength), RegexOffsetUnit.Utf16CodeUnits);
        var limits = budget.Limits;
        var code = patternLength < 0 || nodes.IsEmpty || root != nodes.Length - 1 || groupCount < 1
            ? RegexDiagnosticCode.InvalidProgram :
            patternLength > limits.MaxPatternUnits ? RegexDiagnosticCode.PatternLimitExceeded :
            nodes.Length > limits.MaxInstructions ? RegexDiagnosticCode.ProgramLimitExceeded :
            groupCount > limits.MaxCaptureGroups ? RegexDiagnosticCode.CaptureLimitExceeded :
            intervals.Length > limits.MaxClassRanges ? RegexDiagnosticCode.ClassLimitExceeded : RegexDiagnosticCode.None;
        // Conservative metadata/array headers, node storage, owned names and finite strings.
        var bytes = 512L + 64L * nodes.Length + 16L * sets.Length + 8L * intervals.Length +
            8L * strings.Length + 4L * points.Length + 32L * names.Length + 4L * aliases.Length;
        foreach (var name in names)
        {
            if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
            {
                return false;
            }

            if (string.IsNullOrEmpty(name.Name))
            { code = RegexDiagnosticCode.InvalidProgram; break; }
            bytes += 32L + 2L * name.Name.Length;
        }
        if (code == RegexDiagnosticCode.None && bytes > limits.MaxPlanBytes)
        {
            code = RegexDiagnosticCode.PlanLimitExceeded;
        }

        if (code != RegexDiagnosticCode.None)
        {
            return Fail(code, source, out diagnostic);
        }

        foreach (var point in points)
        {
            if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
            {
                return false;
            }

            if ((uint)point > (profile.IsUnicodeAware ? 0x10FFFFu : 0xFFFFu))
            {
                return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
            }
        }
        foreach (var str in strings)
        {
            if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
            {
                return false;
            }

            if (!SliceFits(str.Offset, str.Length, points.Length))
            {
                return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
            }
        }
        foreach (var set in sets)
        {
            if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
            {
                return false;
            }

            if (!SliceFits(set.IntervalOffset, set.IntervalCount, intervals.Length) ||
                !SliceFits(set.StringOffset, set.StringCount, strings.Length) ||
                (set.StringCount > 0 && (profile.Flags & EcmaScriptFlags.UnicodeSets) == 0))
            {
                return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
            }

            var previous = -1;
            foreach (var range in intervals.Slice(set.IntervalOffset, set.IntervalCount))
            {
                if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
                {
                    return false;
                }

                if (range.First <= previous || range.Last < range.First ||
                    (uint)range.Last > (profile.IsUnicodeAware ? 0x10FFFFu : 0xFFFFu))
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
                }

                previous = range.Last;
            }
            for (var j = 1; j < set.StringCount; j++)
            {
                var a = strings[set.StringOffset + j - 1];
                var b = strings[set.StringOffset + j];
                if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
                {
                    return false;
                }

                if (a.Length < b.Length)
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
                }

                if (a.Length != b.Length)
                {
                    continue;
                }

                var comparison = 0;
                for (var k = 0; k < a.Length && comparison == 0; k++)
                {
                    if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
                    {
                        return false;
                    }

                    comparison = points[a.Offset + k].CompareTo(points[b.Offset + k]);
                }
                if (comparison >= 0)
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
                }
            }
        }
        for (var n = 0; n < names.Length; n++)
        {
            var name = names[n];
            if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
            {
                return false;
            }

            if (name.Length == 0 || !SliceFits(name.Offset, name.Length, aliases.Length))
            {
                return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
            }

            var previous = 0;
            foreach (var id in aliases.Slice(name.Offset, name.Length))
            {
                if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
                {
                    return false;
                }

                if (id <= previous || id >= groupCount)
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
                }

                previous = id;
            }
            for (var p = 0; p < n; p++)
            {
                if (!(budget.TryChargeFast(1L + Math.Min(name.Name.Length, names[p].Name.Length)) || budget.TryCharge(1L + Math.Min(name.Name.Length, names[p].Name.Length), source, out diagnostic)))
                {
                    return false;
                }

                if (name.Name == names[p].Name)
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
                }
            }
        }
        // Admitted temporary depth storage makes validation nonrecursive.
        var temporaryBytes = 32L + 4L * nodes.Length;
        if (!budget.TryReserveBytes(temporaryBytes, source, out diagnostic))
        {
            return false;
        }

        try
        {
            var depths = new int[nodes.Length];
            var captureNodes = 0;
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (!(budget.TryChargeFast(1) || budget.TryCharge(1, node.Source, out diagnostic)))
                {
                    return false;
                }

                if (!ValidNode(node, i, patternLength, groupCount, sets.Length, names.Length, profile))
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, node.Source, out diagnostic);
                }

                var depth = 1 + Math.Max(node.X < 0 ? 0 : depths[node.X], node.Y < 0 ? 0 : depths[node.Y]);
                if (depth > limits.MaxNestingDepth)
                {
                    return Fail(RegexDiagnosticCode.NestingLimitExceeded, node.Source, out diagnostic);
                }

                depths[i] = depth;
                if (node.Kind == EcmaNodeKind.Capture)
                {
                    captureNodes++;
                }

                var first = 0;
                var count = 0;
                if (node.Kind == EcmaNodeKind.Capture)
                { first = node.Value; count = 1; }
                if (node.X >= 0 && !AddCaptures(nodes[node.X], ref first, ref count))
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, node.Source, out diagnostic);
                }

                if (node.Y >= 0 && !AddCaptures(nodes[node.Y], ref first, ref count))
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, node.Source, out diagnostic);
                }

                if (first != node.FirstCapture || count != node.CaptureCount)
                {
                    return Fail(RegexDiagnosticCode.InvalidProgram, node.Source, out diagnostic);
                }

                if (node.Kind == EcmaNodeKind.Capture && node.NameIndex >= 0)
                {
                    var name = names[node.NameIndex];
                    var found = false;
                    foreach (var id in aliases.Slice(name.Offset, name.Length))
                    {
                        if (!(budget.TryChargeFast(1) || budget.TryCharge(1, node.Source, out diagnostic)))
                        {
                            return false;
                        }

                        found |= id == node.Value;
                    }
                    if (!found)
                    {
                        return Fail(RegexDiagnosticCode.InvalidProgram, node.Source, out diagnostic);
                    }
                }
            }
            if (captureNodes != groupCount - 1 || nodes[root].CaptureCount != groupCount - 1 ||
                (groupCount > 1 && nodes[root].FirstCapture != 1))
            {
                return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
            }

            for (var n = 0; n < names.Length; n++)
            {
                var name = names[n];
                foreach (var id in aliases.Slice(name.Offset, name.Length))
                {
                    var found = false;
                    foreach (var node in nodes)
                    {
                        if (!(budget.TryChargeFast(1) || budget.TryCharge(1, source, out diagnostic)))
                        {
                            return false;
                        }

                        found |= node.Kind == EcmaNodeKind.Capture && node.Value == id && node.NameIndex == n;
                    }
                    if (!found)
                    {
                        return Fail(RegexDiagnosticCode.InvalidProgram, source, out diagnostic);
                    }
                }
            }
        }
        finally { budget.ReleaseBytes(temporaryBytes); }
        if (!budget.TryCharge((long)nodes.Length + sets.Length + intervals.Length + strings.Length +
            points.Length + names.Length + aliases.Length, source, out diagnostic) ||
            !budget.TryReserveBytes(bytes, source, out diagnostic))
        {
            return false;
        }

        program = new(profile, root, groupCount, patternLength, bytes, nodes, sets, intervals, strings, points, names, aliases);
        return true;
    }

    private static bool AddCaptures(EcmaNode child, ref int first, ref int count)
    {
        if (child.CaptureCount == 0)
        {
            return true;
        }

        if (count == 0)
        {
            first = child.FirstCapture;
        }
        else if ((long)first + count != child.FirstCapture)
        {
            return false;
        }

        if ((long)count + child.CaptureCount > int.MaxValue)
        {
            return false;
        }

        count += child.CaptureCount;
        return true;
    }

    private static bool ValidNode(in EcmaNode n, int index, int patternLength, int groupCount,
        int sets, int names, EcmaScriptProfile profile)
    {
        // A range check, not Enum.IsDefined: the kinds are contiguous, and the lookup per node was most of validating a
        // program.
        if ((uint)n.Kind > (uint)EcmaNodeKind.NegativeLookbehind || n.Source.Unit != RegexOffsetUnit.Utf16CodeUnits ||
            !SliceFits(n.Source.Offset, n.Source.Length, patternLength) ||
            (n.EffectiveFlags & ~(EcmaScriptFlags.IgnoreCase | EcmaScriptFlags.Multiline | EcmaScriptFlags.DotAll)) != 0 ||
            n.CaptureCount < 0 || n.FirstCapture < 0 || (long)n.FirstCapture + n.CaptureCount > groupCount ||
            (n.CaptureCount == 0 && n.FirstCapture != 0))
        {
            return false;
        }

        var binary = n.Kind is EcmaNodeKind.Sequence or EcmaNodeKind.Alternative;
        var unary = n.Kind is EcmaNodeKind.Repeat or EcmaNodeKind.Capture or EcmaNodeKind.Lookahead or
            EcmaNodeKind.NegativeLookahead or EcmaNodeKind.Lookbehind or EcmaNodeKind.NegativeLookbehind;
        if ((binary || unary ? n.X < 0 || n.X >= index : n.X != -1) ||
            (binary ? n.Y < 0 || n.Y >= index : n.Y != -1))
        {
            return false;
        }

        if (n.Kind != EcmaNodeKind.Repeat && (n.Min != 0 || n.Max != 0 || n.Lazy))
        {
            return false;
        }

        if (n.Kind is not (EcmaNodeKind.Capture or EcmaNodeKind.Backreference) && n.NameIndex != -1)
        {
            return false;
        }

        if (n.NameIndex < -1 || n.NameIndex >= names)
        {
            return false;
        }

        return n.Kind switch
        {
            EcmaNodeKind.Literal => (uint)n.Value <= (profile.IsUnicodeAware ? 0x10FFFFu : 0xFFFFu),
            EcmaNodeKind.Set => (uint)n.Value < (uint)sets,
            EcmaNodeKind.Capture => n.Value > 0 && n.Value < groupCount,
            EcmaNodeKind.Backreference => n.NameIndex >= 0 ? n.Value == 0 : n.Value > 0 && n.Value < groupCount,
            EcmaNodeKind.Repeat => n.Value == 0 && n.Min >= 0 && (n.Max == -1 || n.Max >= n.Min),
            _ => n.Value == 0
        };
    }

    private static bool SliceFits(int offset, int length, int total)
        => offset >= 0 && length >= 0 && (long)offset + length <= total;
    private static bool Fail(RegexDiagnosticCode code, SourceRange source, out RegexDiagnostic diagnostic)
    {
        diagnostic = new(code, source.Offset, source.Length, source.Unit);
        return false;
    }
}
