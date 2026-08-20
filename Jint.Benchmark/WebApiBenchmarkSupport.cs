using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Shared scaffolding for the rows that measure the opt-in WHATWG web APIs under <c>Jint/WebApi/</c>.
///
/// <para>Those APIs shipped with a strict no-cost-when-off discipline, and a default engine is still
/// byte-for-byte the engine it was before they existed. Nothing tracked their cost <em>when on</em>,
/// which is what these rows are for: a future <c>Jint/WebApi/</c> change has a table to move rather
/// than a claim to make. The mapping from row to feature flag is in
/// <c>Jint.Benchmark/README.md</c>.</para>
///
/// <para><b>One engine per row, warmed with that row's own workload.</b> Every row here goes through
/// <see cref="IsolatedScript"/> (or, where the row needs the host to pump the engine, through a
/// private <see cref="Engine"/> field warmed by invoking that row's own benchmark body once). No
/// engine ever sees a sibling row's script — the failure mode <see cref="IsolatedScript"/> documents
/// applies with full force here, because these globals are lazily installed and one row's touch is
/// what materializes them.</para>
///
/// <para><b>Each engine carries only the feature its row is about.</b> <see cref="Create"/> takes the
/// exact <see cref="WebApiFeatures"/> set rather than <see cref="WebApiFeatures.Default"/>, so a
/// change to, say, the URL parser cannot move the encoding rows through some shared installation
/// cost, and a row that starts depending on a second feature has to say so in its own declaration.</para>
///
/// <para><b>Engine construction and the fixture stay out of the measurement.</b> Both happen in
/// <c>[GlobalSetup]</c>, as everywhere else in this suite; the measured operation is one evaluation of
/// the row's own script on an already-warm engine.</para>
/// </summary>
internal static class WebApiBenchmarkSupport
{
    /// <summary>
    /// The BenchmarkDotNet category every web-API row carries, so the whole surface runs with
    /// <c>--allCategories WebApi</c> and nothing else has to be enumerated by name.
    /// </summary>
    internal const string Category = "WebApi";

    /// <summary>A private engine carrying exactly <paramref name="features"/> and no other web API.</summary>
    internal static Engine Create(WebApiFeatures features) => new(options => options.UseWebApis(features));

    /// <summary>
    /// An engine factory for <see cref="IsolatedScript"/>: a fresh engine carrying exactly
    /// <paramref name="features"/>, with <paramref name="fixture"/> already executed on it.
    /// </summary>
    /// <remarks>
    /// The fixture is the row's own one-off setup — its corpus, the function the row calls, and the
    /// reference value that function produced on its first run. It is deliberately per row rather than
    /// per class: a fixture shared by every row would warm each row's engine with its siblings' work,
    /// which is exactly what <see cref="IsolatedScript"/> exists to prevent.
    /// </remarks>
    internal static Func<Engine> WithFixture(WebApiFeatures features, string fixture)
        => WithFixture(() => Create(features), fixture);

    /// <summary>
    /// The same, for a row whose engine needs more than a feature flag — a fake clock, say. The factory
    /// must build a fresh engine per call.
    /// </summary>
    internal static Func<Engine> WithFixture(Func<Engine> engineFactory, string fixture)
    {
        return () =>
        {
            var engine = engineFactory();
            engine.Execute(fixture);
            return engine;
        };
    }

    /// <summary>
    /// A row whose workload is a single deterministic JavaScript function call. The fixture runs
    /// <paramref name="call"/> once and keeps its result as <c>REF</c>; the measured script calls it
    /// again and throws if the answer moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it pins is reproducibility, not correctness.</b> The reference is what this row produced
    /// on this engine on its first run, so it says nothing about whether that answer was right — the
    /// specification is the test suite's business, and duplicating an expected value here would only
    /// add a second place to be wrong. What it catches is the failure a benchmark cannot survive: a row
    /// that works once and then quietly does more work on every subsequent operation, because
    /// BenchmarkDotNet will average that without complaint. State accumulating on the row's engine, a
    /// queue that is never emptied, a buffer that was detached last time and is not now — all of them
    /// move the answer, and all of them are invisible in a table of means.
    /// </para>
    /// <para>
    /// It follows that the check can only bite from the <em>second</em> operation onwards, which is why
    /// <see cref="Smoke"/> runs each row more than once. It costs one comparison per operation, which is
    /// noise next to any of the workloads here. Where a row's answer is genuinely knowable in advance —
    /// the chunk total a stream pump owes, the number of timers that must have fired — the row asserts
    /// that too, through <see cref="Expect"/>.
    /// </para>
    /// </remarks>
    internal static IsolatedScript DeterministicRow(WebApiFeatures features, string fixture, string call)
        => DeterministicRow(() => Create(features), fixture, call);

    /// <summary>
    /// The same, for a row whose engine needs more than a feature flag. The factory must build a fresh
    /// engine per call.
    /// </summary>
    internal static IsolatedScript DeterministicRow(Func<Engine> engineFactory, string fixture, string call)
    {
        var measured =
            $$"""
              var n = {{call}};
              if (n !== REF) { throw new Error('{{call}} produced ' + n + ', expected ' + REF); }
              n;
              """;

        return IsolatedScript.Warm(measured, WithFixture(engineFactory, fixture + Environment.NewLine + $"var REF = {call};"));
    }

    /// <summary>
    /// Checks a value an asynchronous row left in a global once the drain had finished — the rows whose
    /// answer cannot be the script's own completion value, because the script completes before the event
    /// loop runs.
    /// </summary>
    /// <remarks>
    /// Called from <c>[GlobalSetup]</c> only, and deliberately kept to a bare identifier read: it runs on
    /// the row's own engine, so anything heavier would put work on that engine which the row itself never
    /// does.
    /// </remarks>
    internal static void Expect(Engine engine, string expression, string expected)
    {
        var actual = engine.Evaluate(expression).ToString();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Web-API benchmark row is not doing its work: '{expression}' evaluated to '{actual}', expected '{expected}'.");
        }
    }

    /// <summary>
    /// Runs every web-API class's <c>[GlobalSetup]</c>, then every one of its <c>[Benchmark]</c> rows a few
    /// times over, and reports pass/fail per class — the <c>--smoke-webapi</c> mode in <c>Program.cs</c>.
    /// Takes a few seconds and measures nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setup is where most of the risk lives: it builds each row's engine, executes each row's fixture, and
    /// evaluates each row's script once to warm it. So a script that throws, a built-in absent because the
    /// row asked for the wrong feature flag, or an asynchronous row whose total is wrong all fail there —
    /// in seconds, instead of half an hour into a BenchmarkDotNet run.
    /// </para>
    /// <para>
    /// <b>Running the rows repeatedly is the other half, and it is not redundant.</b> The failure a
    /// benchmark cannot afford is a row that works once and drifts afterwards, because BenchmarkDotNet
    /// will happily average an operation that is doing more work every time it runs — state accumulating
    /// on the row's engine, a queue that is never emptied, a buffer that was detached last time and is not
    /// now. That is precisely what <see cref="DeterministicRow"/>'s reference check catches, and it can
    /// only catch it on the second and later operations. Three operations per row is a cheap number that
    /// exercises it and still leaves room for a defect that needs one more turn to surface — a leak
    /// bounded by a quota, say.
    /// </para>
    /// <para>
    /// The precedent is <c>--smoke-comparison</c>, which exists so a new comparison script is known to run
    /// on every engine before it joins the table. This is the same idea for the web-API rows, and it is why
    /// <c>Jint.Benchmark/README.md</c> tells a <c>Jint/WebApi/</c> PR to run it first.
    /// </para>
    /// </remarks>
    internal static int Smoke()
    {
        object[] classes =
        [
            new WebApiUrlBenchmark(),
            new WebApiEncodingBenchmark(),
            new WebApiStructuredCloneBenchmark(),
            new WebApiFetchObjectModelBenchmark(),
            new WebApiStreamsBenchmark(),
            new WebApiTimerBenchmark(),
            new WebApiCryptoBenchmark(),
        ];

        var failures = 0;
        foreach (var instance in classes)
        {
            var type = instance.GetType();
            var stopwatch = Stopwatch.StartNew();
            try
            {
                Invoke(instance, typeof(GlobalSetupAttribute), times: 1);
                Invoke(instance, typeof(BenchmarkAttribute), times: 3);
                Console.WriteLine($"  {type.Name,-34} OK    {stopwatch.ElapsedMilliseconds,6} ms");
            }
            catch (Exception ex)
            {
                failures++;
                var reported = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                Console.WriteLine(
                    $"  {type.Name,-34} FAIL  {stopwatch.ElapsedMilliseconds,6} ms  {reported.GetType().Name}: {reported.Message.Split('\n')[0]}");
            }
        }

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Invokes every parameterless method carrying <paramref name="attribute"/>, <paramref name="times"/>
    /// times each. Reflection order is unspecified and deliberately not relied on: no row here depends on
    /// another having run.
    /// </summary>
    private static void Invoke(object instance, Type attribute, int times)
    {
        foreach (var method in instance.GetType().GetMethods())
        {
            if (method.GetParameters().Length != 0 || !method.IsDefined(attribute, inherit: false))
            {
                continue;
            }

            for (var i = 0; i < times; i++)
            {
                method.Invoke(instance, null);
            }
        }
    }
}
