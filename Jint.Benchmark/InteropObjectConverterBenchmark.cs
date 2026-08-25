#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// Sizes what registering an <see cref="ObjectConverter"/> costs the compiled member-accessor lane
/// (<c>Runtime/Interop/Reflection/CompilableMemberAccessor.cs</c>). A registered converter is entitled to
/// see every CLR value before it becomes a <see cref="JsValue"/> and the compiled lane produces the
/// <see cref="JsValue"/> itself, so the lane has to decline for any member the converter could be handed.
///
/// <para>
/// The three rows separate the two things that used to be one. <see cref="ConverterKind.None"/> is the
/// unconverted baseline. <see cref="ConverterKind.Untyped"/> is a converter registered without declaring
/// its CLR types: it can be handed anything, so <b>every</b> CLR property read on <b>every</b> wrapped
/// object in the engine goes back through reflection — the behaviour every registration used to have.
/// <see cref="ConverterKind.Typed"/> declares a type this benchmark's host record never exposes, so every
/// read here keeps the lane; what it still pays is the per-read question "could this converter be handed a
/// value of this member's declared type?", and this row is what sizes that.
/// </para>
///
/// <para>
/// The converter installed here deliberately converts nothing: it returns <c>false</c> for every value, so
/// all three rows produce identical results and the deltas are purely the cost of the converter's
/// <i>presence</i>. Embedders routinely register exactly one narrow converter (for a single host type)
/// without knowing what it prices in.
/// </para>
///
/// <para>
/// Expected shape: <c>Untyped</c> well behind <c>None</c> on both time and <c>Allocated</c>, with the gap
/// proportional to the number of CLR member reads in the loop; <c>Typed</c> alongside <c>None</c> on
/// <c>Allocated</c> and within a small constant of it on time.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class InteropObjectConverterBenchmark
{
    private const int Iterations = 1_000;

    /// <summary>How (and whether) an object converter is registered on the engine under test.</summary>
    public enum ConverterKind
    {
        /// <summary>No converter at all — the lane is unconditionally available.</summary>
        None,

        /// <summary>A converter registered without declared types: claims every member, so the lane never runs.</summary>
        Untyped,

        /// <summary>A converter declaring a type no member here has: the lane runs, after answering the type question.</summary>
        Typed,
    }

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

    [Params(ConverterKind.None, ConverterKind.Untyped, ConverterKind.Typed)]
    public ConverterKind Converter { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var kind = Converter;
        _engine = new Engine(options =>
        {
            if (kind == ConverterKind.Untyped)
            {
                options.AddObjectConverter(new NeverConvertingObjectConverter());
            }
            else if (kind == ConverterKind.Typed)
            {
                // Uri is unrelated to every member HostRecord exposes (string, double, int), so the
                // filter answers "not claimed" for all of them and the lane stays available.
                options.AddObjectConverter(new NeverConvertingObjectConverter(), typeof(Uri));
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
    private sealed class NeverConvertingObjectConverter : ObjectConverter
    {
        // The interface annotates result with [NotNullWhen(true)]. That attribute cannot be spelled
        // in this project — YantraJS.Core (referenced for the engine-comparison lanes) ships its own
        // System.Diagnostics.CodeAnalysis.NotNullWhenAttribute, so the name is ambiguous — so the
        // parameter is declared non-nullable instead, which is a strictly stronger promise and
        // satisfies the annotation without needing it.
        public override bool TryConvert(Engine engine, object value, out JsValue result)
        {
            result = JsValue.Undefined;
            return false;
        }
    }
}
