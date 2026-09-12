using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// What a member read costs as a function of <b>how far up the prototype chain the member lives</b>, with no
/// host object anywhere: four plain <c>class</c> declarations, one <c>new</c>, and a loop.
///
/// <para>
/// The member-read inline cache on <c>JintMemberExpression</c> used to be able to remember a holder only when
/// it was the receiver's <em>direct</em> prototype; anything deeper re-walked the chain and paid a
/// <c>GetOwnProperty</c> at every level, on every read, forever. That is not a niche shape — it is what
/// <c>class Leaf extends Derived extends Middle extends Base</c> does with every member <c>Base</c> declares,
/// and what a DOM binding does with every member declared on an interface its element does not directly
/// implement (<c>nodeName</c> on <c>Node.prototype</c>, read off an <c>HTMLParagraphElement</c>, is four links
/// up). This class is the engine-only statement of that cost, so the win is demonstrable without a host
/// object, a wrapper or a binding generator in the picture.
/// </para>
///
/// <para><b>The rows</b></para>
/// <list type="bullet">
/// <item><description>
/// <see cref="OwnPropertyRead"/> — the baseline floor: the loop, the accumulate, and a read that resolves in
/// the receiver's own shape slot. Every other row is this plus one prototype resolution, so the baseline ratio
/// is the honest way to read the table. <b>A control: nothing in this area may move it.</b>
/// </description></item>
/// <item><description>
/// <see cref="DirectPrototypeGetterRead"/> — a getter declared on the receiver's own class prototype, one link
/// up. <b>The comparison, and the second control:</b> this is the shape that already cached, so the deep row
/// converging on it is the result, and this row moving is a regression.
/// </description></item>
/// <item><description>
/// <see cref="RootPrototypeGetterRead"/> — the same getter, declared on the <em>root</em> of a four-level
/// hierarchy and read off a leaf instance, so it sits three links behind the holder. The subject.
/// </description></item>
/// <item><description>
/// <see cref="DirectPrototypeMethodCall"/> / <see cref="RootPrototypeMethodCall"/> — the same pair for a method
/// <em>call</em>, which resolves its callee through a different entry point on the same node
/// (<c>GetCalleeForCall</c>) and reads a data property rather than invoking an accessor. Both lanes share the
/// cache fields, so the pair is what proves the second one was not left behind.
/// </description></item>
/// </list>
///
/// <para>
/// <b>Why a class of its own rather than more rows on <c>HostPrototypeShapeBenchmark</c>.</b> That class is
/// parameterised on <c>HostPrototypeKind</c>, and these rows contain no host object, so every one of them
/// would be reported twice with identical work under two labels that mean nothing to it. Its chain rows are
/// also not comparable to these: they drive <c>('name' in obj)</c>, which resolves through
/// <c>ObjectInstance.HasProperty</c> and never reaches the member-read lane at all.
/// </para>
///
/// <para>
/// <b>Engine isolation.</b> One <see cref="Engine"/> per row, built and warmed in <c>[GlobalSetup]</c> with
/// that row's script and nothing else (<see cref="IsolatedScript"/>). These rows measure <em>warm</em> reads —
/// a per-site inline cache is the whole subject, and a site that never warms measures nothing about it — so
/// engine construction and the first evaluation stay outside the measurement. Sharing one engine would be
/// actively wrong here: the rows read the same property names off the same hierarchy, so one row's warm-up
/// would populate the caches of the next.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class PrototypeChainReadBenchmark
{
    private const int LoopIterations = 20000;

    /// <summary>
    /// Four levels, so a member declared on <c>Base</c> sits three links behind the leaf instance's direct
    /// prototype: instance → <c>Leaf.prototype</c> → <c>Derived.prototype</c> → <c>Middle.prototype</c> →
    /// <c>Base.prototype</c>. The two intermediate classes declare nothing, which is exactly what an
    /// interface hierarchy looks like from the point of view of one inherited member: levels that have to be
    /// asked and have nothing to say.
    /// <para>
    /// The leaf's own <c>value</c> is assigned in the constructor so the receiver is an ordinary shaped
    /// object with one own property — the floor row's target, and the own-property miss every other row's
    /// read has to establish before it may look at a prototype.
    /// </para>
    /// </summary>
    private const string Hierarchy = """
        class Base {
          constructor() { this.value = 1; }
          get rootAttribute() { return this.value; }
          rootOperation() { return this.value; }
        }
        class Middle extends Base { }
        class Derived extends Middle { }
        class Leaf extends Derived {
          get leafAttribute() { return this.value; }
          leafOperation() { return this.value; }
        }
        var leaf = new Leaf();
        """;

    private IsolatedScript _ownRead;
    private IsolatedScript _directGetterRead;
    private IsolatedScript _rootGetterRead;
    private IsolatedScript _directMethodCall;
    private IsolatedScript _rootMethodCall;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _ownRead = Loop("obj.value");
        _directGetterRead = Loop("obj.leafAttribute");
        _rootGetterRead = Loop("obj.rootAttribute");
        _directMethodCall = Loop("obj.leafOperation()");
        _rootMethodCall = Loop("obj.rootOperation()");
    }

    /// <summary>
    /// One row's script and its private engine: the shared hierarchy, then a loop whose body is nothing but
    /// the read under test. The read happens inside a function so the member node is a handler-tree node that
    /// warms like one in real code, and the accumulation keeps the result observable so nothing can be
    /// optimised away.
    /// </summary>
    private static IsolatedScript Loop(string read) => IsolatedScript.Warm(
        Engine.PrepareScript($$"""
            (function (obj) {
              var total = 0;
              for (var i = 0; i < {{LoopIterations}}; i++) {
                total += {{read}};
              }
              return total;
            })(leaf)
            """),
        static () =>
        {
            var engine = new Engine();
            engine.Execute(Hierarchy);
            return engine;
        });

    /// <summary>
    /// Each row owns an <see cref="Engine"/>, and an engine is <see cref="IDisposable"/>. BenchmarkDotNet runs
    /// every case in its own process, so this changes nothing about the measurement — it is here so the class
    /// does not model a lifecycle an embedder should not copy.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _ownRead.Engine.Dispose();
        _directGetterRead.Engine.Dispose();
        _rootGetterRead.Engine.Dispose();
        _directMethodCall.Engine.Dispose();
        _rootMethodCall.Engine.Dispose();
    }

    /// <summary>The floor: an own-property read, resolved in the receiver's own shape slot.</summary>
    [Benchmark(Baseline = true)]
    public JsValue OwnPropertyRead() => _ownRead.Run();

    /// <summary>The comparison: a getter one link up, the shape that already cached.</summary>
    [Benchmark]
    public JsValue DirectPrototypeGetterRead() => _directGetterRead.Run();

    /// <summary>The subject: the same getter, four links up.</summary>
    [Benchmark]
    public JsValue RootPrototypeGetterRead() => _rootGetterRead.Run();

    /// <summary>The comparison for the call lane: a method one link up.</summary>
    [Benchmark]
    public JsValue DirectPrototypeMethodCall() => _directMethodCall.Run();

    /// <summary>The subject for the call lane: the same method, four links up.</summary>
    [Benchmark]
    public JsValue RootPrototypeMethodCall() => _rootMethodCall.Run();
}
