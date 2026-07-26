#nullable enable

using System.Globalization;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Walking a chain of string keys down an untyped host document — <c>o.a.b.c.d</c> — which is how
/// an embedder that passes JSON-shaped data (a deserialized payload, a document, a config tree)
/// exposes it. No existing benchmark measures a <b>chain</b>: <see cref="InteropBenchmark"/> reads
/// one level off a dictionary, and <see cref="InteropLambdaBenchmark"/> reads one level then
/// iterates an array.
///
/// <para>
/// Depth is the whole point. Every level is an independent wrapper lookup, and none of them is a
/// property on a declared type, so nothing along the chain can use the compiled member accessor:
/// <list type="bullet">
/// <item><description>
/// <see cref="NestedSourceKind.Dictionary"/> routes each level through
/// <c>TypeDescriptor.TryGetDictionaryValue</c>, which invokes the dictionary's <c>TryGetValue</c>
/// via <c>MethodInfo.Invoke</c> with a freshly boxed <c>object[]</c> parameter array — per level,
/// per read.
/// </description></item>
/// <item><description>
/// <see cref="NestedSourceKind.JsonObject"/> routes each level through the string-indexer accessor
/// that Jint special-cases for <c>System.Text.Json.Nodes.JsonNode</c>, then converts the resulting
/// node. Same shape from the script's point of view, different machinery underneath — worth having
/// both rows so an improvement to one is not mistaken for an improvement to the other.
/// </description></item>
/// </list>
/// </para>
///
/// <para>
/// Expected shape: <c>Mean</c> and <c>Allocated</c> should both rise roughly linearly with
/// <see cref="Depth"/>. Compare the <c>Depth=4</c> row against four times the <c>Depth=1</c> row —
/// anything super-linear means an intermediate wrapper is being rebuilt rather than reused, and the
/// per-level allocation slope is the boxed-argument churn.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class InteropNestedDictionaryBenchmark
{
    private const int Iterations = 1_000;
    private const int MaxDepth = 4;

    private Engine _engine = null!;
    private Prepared<Script> _script;

    [Params(NestedSourceKind.Dictionary, NestedSourceKind.JsonObject)]
    public NestedSourceKind Source { get; set; }

    [Params(1, 2, 4)]
    public int Depth { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();

        // e.g. Depth 4 -> "o.a.b.c.d"
        var chain = "o";
        for (var level = 0; level < Depth; level++)
        {
            chain += "." + KeyAt(level);
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

        _engine.SetValue("doc", BuildDocument(Source, Depth));
        _script = Engine.PrepareScript($"walk(doc, {Iterations});");
        _engine.Evaluate(_script);
    }

    [Benchmark]
    public JsValue WalkChain() => _engine.Evaluate(_script);

    internal static object BuildDocument(NestedSourceKind kind, int depth)
    {
        if (kind == NestedSourceKind.JsonObject)
        {
            JsonNode leaf = JsonValue.Create(1.5);
            for (var level = depth - 1; level >= 0; level--)
            {
                leaf = new JsonObject { [KeyAt(level)] = leaf };
            }

            return leaf;
        }

        object node = 1.5d;
        for (var level = depth - 1; level >= 0; level--)
        {
            node = new Dictionary<string, object>(StringComparer.Ordinal) { [KeyAt(level)] = node };
        }

        return node;
    }

    private static string KeyAt(int level)
    {
        // a, b, c, d ... then k0, k1 ... if the matrix ever grows past MaxDepth.
        return level < MaxDepth
            ? ((char) ('a' + level)).ToString()
            : "k" + level.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>Untyped host document shapes an embedder passes across the boundary.</summary>
public enum NestedSourceKind
{
    /// <summary>A string-keyed generic dictionary tree.</summary>
    Dictionary,

    /// <summary>A <see cref="JsonObject"/> tree.</summary>
    JsonObject,
}
