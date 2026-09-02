using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jint.Runtime.Coverage;

/// <summary>
/// The per-engine hit counters behind <see cref="Engine.DiagnosticOperations.GetCoverage"/>. One instance exists
/// per engine, and only when <see cref="Options.CoverageOptions.Enabled"/> was set: on every other engine
/// <c>Engine._coverage</c> is <see langword="null"/> and nothing here is ever reached.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the counters live here and not on the handler nodes.</b> Interpreter handler trees
/// (<c>JintStatement</c>, <c>JintExpression</c>) are engine-owned, so a counter field on
/// <c>JintStatement</c> would be per-engine for <em>almost</em> every node — but not all of them.
/// <c>ConstantStatement</c> is the exception: the static analysis pass stores that handler on the AST node's
/// <c>UserData</c> (<c>Engine.Ast.cs</c>), and a prepared script's AST is shared by every engine that runs it,
/// so one such handler instance is shared across engines. A counter field would silently pool the hit counts
/// of every engine running the same <c>return &lt;literal&gt;;</c>. Keying engine-side on node identity has no
/// such hole and keeps AST <c>UserData</c> free of anything engine-affine, which is the invariant
/// <c>JintStatement.Build</c> documents.
/// </para>
/// <para>
/// A plain <see cref="Dictionary{TKey,TValue}"/> rather than a <see cref="ConditionalWeakTable{TKey,TValue}"/>:
/// the report has to enumerate what was counted, which a weak table cannot do on every target framework, and
/// the AST is rooted by whatever prepared script or parse the host is holding anyway. The direction of the
/// reference matters and is the safe one — the engine points at AST nodes, never the reverse — so an engine
/// collecting coverage stays collectable while the script it ran is still rooted.
/// </para>
/// <para>
/// Single-threaded, like everything else on an <see cref="Engine"/>.
/// </para>
/// </remarks>
internal sealed class CoverageCollector
{
    private sealed class Counter
    {
        internal Counter(CoverageEntryKind kind, bool reported, Program? program)
        {
            Kind = kind;
            Reported = reported;
            Program = program;
        }

        internal readonly CoverageEntryKind Kind;

        /// <summary>
        /// The program the node was parsed as part of, read once when the counter is made: a node belongs
        /// to one program for as long as it exists, so this never has to be re-asked.
        /// </summary>
        internal readonly Program? Program;

        /// <summary>
        /// False for a node the granularity excludes. Such a node still gets an entry here so the
        /// classification below runs once per node instead of once per execution; it is skipped at readout.
        /// </summary>
        internal readonly bool Reported;

        internal long Hits;
    }

    private sealed class NodeReferenceComparer : IEqualityComparer<Node>
    {
        internal static readonly NodeReferenceComparer Instance = new();

        public bool Equals(Node? x, Node? y) => ReferenceEquals(x, y);

        public int GetHashCode(Node obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private readonly bool _statements;
    private readonly Dictionary<Node, Counter> _counters = new(NodeReferenceComparer.Instance);

    internal CoverageCollector(CoverageGranularity granularity)
    {
        _statements = granularity != CoverageGranularity.Functions;
    }

    /// <summary>
    /// Counts one entry into <paramref name="node"/>. Reached from <see cref="Engine.RunPerStatementChecks"/>,
    /// which the evaluation context only calls when the per-statement lane is armed — and enabling coverage is
    /// one of the three things that arms it, so this runs for every executed statement and every function-body
    /// entry.
    /// </summary>
    internal void Record(StatementOrExpression node, Engine engine)
    {
        if (_counters.TryGetValue(node, out var counter))
        {
            counter.Hits++;
            return;
        }

        AddCounter(node, engine);
    }

    /// <summary>
    /// Makes the counter for a node seen for the first time, and settles which program it belongs to while
    /// the context that is running it is still the top of the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddCounter(Node node, Engine engine)
    {
        var reported = TryClassify(node, out var kind);
        var program = engine.ExecutionContext.ScriptOrModule.OwningProgramOf(node);
        _counters[node] = new Counter(kind, reported, program) { Hits = 1 };
    }

    /// <summary>
    /// Decides what a node reaching the per-statement lane represents, and whether the configured granularity
    /// reports it.
    /// </summary>
    /// <remarks>
    /// Only four shapes of node ever get here (the call sites are <c>JintStatement.Execute</c>,
    /// <c>JintStatementList.Execute</c> and the concise-body arms of <c>JintFunctionDefinition</c>):
    /// a <see cref="FunctionBody"/>, some other <see cref="BlockStatement"/>, a non-block
    /// <see cref="Statement"/>, or — for a concise arrow body — the body <see cref="Expression"/> itself.
    /// Blocks other than a function body are excluded because the engine has several lanes for running one
    /// (a single-statement block is executed directly, a loop body block can be flattened into the loop's
    /// environment) and they differ in whether the block node is entered at all; reporting it would make the
    /// report depend on an internal lane choice. Nothing inside the block is lost by that.
    /// </remarks>
    private bool TryClassify(Node node, out CoverageEntryKind kind)
    {
        if (node is FunctionBody || node is not Statement)
        {
            // A function's block body, or the expression that is a concise arrow body.
            kind = CoverageEntryKind.Function;
            return true;
        }

        kind = CoverageEntryKind.Statement;
        return _statements && node is not BlockStatement;
    }

    /// <summary>
    /// Drops every counter, so the next report covers only what runs from here on.
    /// </summary>
    internal void Reset() => _counters.Clear();

    /// <summary>
    /// One counted node on its way into the report, before positions that name the same construct are folded
    /// together.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Raw(CoverageEntryKind Kind, CoveragePosition Start, CoveragePosition End, long Hits);

    internal CoverageReport BuildReport()
    {
        var bySource = new Dictionary<SourceKey, Group>(SourceKeyComparer.Instance);

        foreach (var pair in _counters)
        {
            var counter = pair.Value;
            if (!counter.Reported)
            {
                continue;
            }

            var node = pair.Key;
            var location = node.Location;
            var key = new SourceKey(counter.Program, location.SourceFile ?? string.Empty);

            if (!bySource.TryGetValue(key, out var group))
            {
                bySource[key] = group = new Group(bySource.Count);
            }

            group.Raws.Add(new Raw(
                counter.Kind,
                new CoveragePosition(location.Start.Line, location.Start.Column, node.Start),
                new CoveragePosition(location.End.Line, location.End.Column, node.End),
                counter.Hits));
        }

        var sources = new List<(SourceKey Key, Group Group)>(bySource.Count);
        foreach (var pair in bySource)
        {
            sources.Add((pair.Key, pair.Value));
        }

        // By name first, which is the order that reads; several programs parsed under one name are then in
        // the order they were first counted, so the report stays reproducible without inventing an order
        // between two trees that have none.
        sources.Sort(static (a, b) =>
        {
            var result = string.CompareOrdinal(a.Key.Name, b.Key.Name);
            return result != 0 ? result : a.Group.Ordinal.CompareTo(b.Group.Ordinal);
        });

        var reported = new List<CoverageSource>(sources.Count);
        foreach (var source in sources)
        {
            reported.Add(new CoverageSource(source.Key.Name, source.Key.Program, Coalesce(source.Group.Raws)));
        }

        return new CoverageReport(reported);
    }

    /// <summary>
    /// What one <see cref="CoverageSource"/> is made of: the program the nodes were parsed as part of, and
    /// the name they carry. Both, because a program the engine cannot name still has a source name to
    /// report under, and two such parses must not be folded into one.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct SourceKey(Program? Program, string Name);

    private sealed class Group(int ordinal)
    {
        internal readonly int Ordinal = ordinal;
        internal readonly List<Raw> Raws = new();
    }

    /// <summary>
    /// Reference identity for the program half of a key, spelt out rather than left to the default
    /// comparer: two abstract syntax trees with identical contents are two parses, and one source name
    /// covering both is exactly the case this key exists to keep apart.
    /// </summary>
    private sealed class SourceKeyComparer : IEqualityComparer<SourceKey>
    {
        internal static readonly SourceKeyComparer Instance = new();

        public bool Equals(SourceKey x, SourceKey y)
            => ReferenceEquals(x.Program, y.Program) && string.Equals(x.Name, y.Name, StringComparison.Ordinal);

        public int GetHashCode(SourceKey obj)
        {
            var program = obj.Program is null ? 0 : RuntimeHelpers.GetHashCode(obj.Program);
            return (program * 397) ^ StringComparer.Ordinal.GetHashCode(obj.Name);
        }
    }

    /// <summary>
    /// Orders the raw counts and sums the ones that name the same construct.
    /// </summary>
    /// <remarks>
    /// The counters are keyed on AST node identity, and several parses can land in one group: the entries of
    /// every program the engine cannot name — <c>eval</c> and the <c>Function</c> constructor — are grouped
    /// by source name alone, and a host evaluating the same string twice there gets a fresh node per parse
    /// for the same construct. Reporting those separately would answer "this statement ran once, twice over"
    /// instead of "twice". Within one program the fold is a no-op: no two distinct nodes of the same kind
    /// share a range.
    /// </remarks>
    private static List<CoverageEntry> Coalesce(List<Raw> raws)
    {
        raws.Sort(CompareRaws);

        var entries = new List<CoverageEntry>(raws.Count);
        for (var i = 0; i < raws.Count;)
        {
            var current = raws[i];
            var hits = current.Hits;

            var j = i + 1;
            while (j < raws.Count)
            {
                var next = raws[j];
                if (!SameConstruct(in current, in next))
                {
                    break;
                }

                hits += next.Hits;
                j++;
            }

            entries.Add(new CoverageEntry(current.Kind, current.Start, current.End, hits));
            i = j;
        }

        return entries;
    }

    private static bool SameConstruct(in Raw a, in Raw b)
        => a.Kind == b.Kind && a.Start.Index == b.Start.Index && a.End.Index == b.End.Index;

    /// <summary>
    /// Total, so the report is byte-for-byte reproducible across runs even though the dictionary it is built
    /// from enumerates in insertion-dependent order.
    /// </summary>
    private static int CompareRaws(Raw a, Raw b)
    {
        var result = a.Start.Index.CompareTo(b.Start.Index);
        if (result != 0)
        {
            return result;
        }

        result = a.End.Index.CompareTo(b.End.Index);
        return result != 0 ? result : ((int) a.Kind).CompareTo((int) b.Kind);
    }
}
