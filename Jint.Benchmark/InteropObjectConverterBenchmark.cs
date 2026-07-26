#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// Sizes an engine-wide de-optimization that has no other coverage: registering a single
/// <see cref="IObjectConverter"/> makes <c>Engine._objectConverters</c> non-null, and the compiled
/// member-accessor lane bails out whenever that field is set
/// (<c>Runtime/Interop/Reflection/CompilableMemberAccessor.cs</c>). It has to — a registered
/// converter is entitled to see every CLR value before it becomes a <see cref="JsValue"/>, and the
/// compiled lane produces the <see cref="JsValue"/> itself — but the consequence is that
/// <b>one</b> converter, however narrow, sends <b>every</b> CLR property read on <b>every</b>
/// wrapped object in the engine back through reflection.
///
/// <para>
/// The converter installed here deliberately converts nothing: it returns <c>false</c> for every
/// value, so both rows produce identical results and the delta is purely the cost of the
/// converter's <i>presence</i>. Embedders routinely register exactly one narrow converter (for a
/// single host type) without knowing it prices in every other read.
/// </para>
///
/// <para>
/// Expected shape: <c>ObjectConverterRegistered=true</c> slower than <c>false</c> on both time and
/// <c>Allocated</c>, with the gap proportional to the number of CLR member reads in the loop.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class InteropObjectConverterBenchmark
{
    private const int Iterations = 1_000;

    private const string Source = """
        function readAll(o, n) {
          var acc = 0;
          for (var i = 0; i < n; i++) {
            acc += o.Amount + o.Rate + o.Count;
            if (o.Name.length > 0) { acc += 1; }
          }
          return acc;
        }
        """;

    private Engine _engine = null!;
    private Prepared<Script> _script;

    [Params(false, true)]
    public bool ObjectConverterRegistered { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var converterRegistered = ObjectConverterRegistered;
        _engine = new Engine(options =>
        {
            if (converterRegistered)
            {
                options.AddObjectConverter(new NeverConvertingObjectConverter());
            }
        });

        _engine.Execute(Source);
        _engine.SetValue("record", new HostRecord { Name = "record name", Amount = 12.5, Rate = 1.5, Count = 3 });

        _script = Engine.PrepareScript($"readAll(record, {Iterations});");
        _engine.Evaluate(_script);
    }

    [Benchmark]
    public JsValue ReadClrProperties() => _engine.Evaluate(_script);

    /// <summary>A plain host POCO — the shape most embedders hand across the boundary.</summary>
    public sealed class HostRecord
    {
        public string Name { get; set; } = "";
        public double Amount { get; set; }
        public double Rate { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Converts nothing, so the two rows are behaviourally identical and the measurement isolates
    /// the cost of a converter merely being registered.
    /// </summary>
    private sealed class NeverConvertingObjectConverter : IObjectConverter
    {
        // The interface annotates result with [NotNullWhen(true)]. That attribute cannot be spelled
        // in this project — YantraJS.Core (referenced for the engine-comparison lanes) ships its own
        // System.Diagnostics.CodeAnalysis.NotNullWhenAttribute, so the name is ambiguous — so the
        // parameter is declared non-nullable instead, which is a strictly stronger promise and
        // satisfies the annotation without needing it.
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            result = JsValue.Undefined;
            return false;
        }
    }
}
