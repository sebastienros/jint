using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// One benchmark row's workload: the script the row measures, together with the <see cref="Jint.Engine"/>
/// that row — and only that row — ever runs it on.
///
/// <para>It exists to stop a whole family of measurement defects. A benchmark class that builds one
/// engine in <c>[GlobalSetup]</c> and warms it with <em>every</em> row's script hands each row an engine
/// carrying the other rows' state: their globals on the shared global object (nearly every row here
/// declares <c>var i</c>, <c>var s</c> or <c>function f</c>, so they collide outright), their entries in
/// the engine-owned handler-tree caches (<c>Engine._functionDefinitions</c>, <c>_scriptStatementLists</c>),
/// their per-call-site monomorphic caches, and their environment-reuse and constructor-shape state. A
/// row's number then depends on which sibling rows exist and what a change did to <em>them</em> — which
/// is how a call-path change that was 2.5-3.0% faster in isolation was reported by
/// <see cref="MethodCallBenchmark"/> as +5.6..+9.2% slower on three rows, reproducibly and in both A/B
/// orderings.</para>
///
/// <para>BenchmarkDotNet runs every benchmark case in its own process, so the leak is not the field being
/// shared — it is <c>[GlobalSetup]</c> doing other rows' work on the engine this row is about to be
/// measured on. Giving each row its own engine closes it at the source and is robust to that detail
/// changing.</para>
///
/// <para>Warming is preserved deliberately: these rows measure <em>warm</em> dispatch, and dropping the
/// warm-up would silently turn them into cold-start benchmarks and change what the suite is for. Only
/// the row's own script is evaluated, so engine construction and warm-up stay in <c>[GlobalSetup]</c>
/// and never enter the measurement. Where a benchmark genuinely wants cold-start behaviour, build the
/// engine inside the benchmark method instead (see <see cref="DromaeoBenchmark"/> and
/// <see cref="SunSpiderBenchmark"/>) — never with <c>[IterationSetup]</c>.</para>
/// </summary>
internal readonly struct IsolatedScript
{
    private readonly Engine _engine;
    private readonly Prepared<Script> _script;

    private IsolatedScript(Engine engine, Prepared<Script> script)
    {
        _engine = engine;
        _script = script;
    }

    /// <summary>The row's private engine, for the rare case a caller needs it after construction.</summary>
    public Engine Engine => _engine;

    /// <summary>
    /// Gives <paramref name="script"/> a private default engine and warms it by evaluating the script once.
    /// </summary>
    public static IsolatedScript Warm(Prepared<Script> script) => Warm(script, static () => new Engine());

    /// <summary>
    /// Gives <paramref name="script"/> a private engine built by <paramref name="engineFactory"/> — which
    /// must build a fresh engine per call, including any fixture every row shares — and warms it by
    /// evaluating the script once.
    /// </summary>
    public static IsolatedScript Warm(Prepared<Script> script, Func<Engine> engineFactory)
    {
        var engine = engineFactory();
        engine.Evaluate(script);
        return new IsolatedScript(engine, script);
    }

    /// <summary>Prepares <paramref name="source"/> and warms it on a private default engine.</summary>
    public static IsolatedScript Warm(string source) => Warm(Engine.PrepareScript(source));

    /// <summary>Prepares <paramref name="source"/> and warms it on a private engine from <paramref name="engineFactory"/>.</summary>
    public static IsolatedScript Warm(string source, Func<Engine> engineFactory)
        => Warm(Engine.PrepareScript(source), engineFactory);

    /// <summary>Evaluates the row's script on the row's engine and returns the completion value.</summary>
    public JsValue Run() => _engine.Evaluate(_script);

    /// <summary>Evaluates the row's script on the row's engine, discarding the completion value.</summary>
    public void Execute() => _engine.Execute(_script);
}
