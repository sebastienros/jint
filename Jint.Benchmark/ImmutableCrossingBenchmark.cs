#nullable enable

using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The <see cref="InteropNestedDictionaryBenchmark"/> chain walk, with and without the host
/// declaring the document types immutable via <c>Options.AddImmutableCrossing</c>. The declared
/// rows are the feature: every level's member read memoizes on its wrapper after the first walk,
/// so the steady-state walk should stop allocating per read and stop paying the per-level
/// dictionary/indexer resolution. The undeclared rows must match
/// <see cref="InteropNestedDictionaryBenchmark"/> — they exist so a regression on the undeclared
/// path cannot hide inside a separate benchmark's noise floor.
///
/// <para>
/// Expected shape: undeclared rows rise linearly with <see cref="Depth"/> in both columns, as the
/// parent benchmark documents. Declared rows should be near-flat in <c>Allocated</c> across depth
/// (the memo allocates on the first walk only, which <c>GlobalSetup</c> already performed) and
/// materially below their undeclared partner in <c>Mean</c> at every depth.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class ImmutableCrossingBenchmark
{
    private const int Iterations = 1_000;

    private Engine _engine = null!;
    private Prepared<Script> _script;

    [Params(NestedSourceKind.Dictionary, NestedSourceKind.JsonObject)]
    public NestedSourceKind Source { get; set; }

    [Params(1, 4)]
    public int Depth { get; set; }

    [Params(false, true)]
    public bool Declared { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine(options =>
        {
            if (Declared)
            {
                options.AddImmutableCrossing(typeof(Dictionary<string, object>), typeof(JsonObject));
            }
        });

        // e.g. Depth 4 -> "o.a.b.c.d"
        var chain = "o";
        for (var level = 0; level < Depth; level++)
        {
            chain += "." + ((char) ('a' + level));
        }

        _engine.Execute($$"""
            function walk(o, n) {
              var acc = 0;
              for (var i = 0; i < n; i++) {
                acc += {{chain}};
              }
              return acc;
            }
            """);

        _engine.SetValue("doc", InteropNestedDictionaryBenchmark.BuildDocument(Source, Depth));
        _script = Engine.PrepareScript($"walk(doc, {Iterations});");
        _engine.Evaluate(_script);
    }

    [Benchmark]
    public JsValue WalkChain() => _engine.Evaluate(_script);
}
