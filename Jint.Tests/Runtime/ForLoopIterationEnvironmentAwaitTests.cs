using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A C-style <c>for</c> loop restores, on exit, the environment that was current when it was
/// entered. It used to read that environment from the execution context on every entry —
/// including a re-entry that is an async <em>replay</em>.
///
/// That is unsound for an async function, because <c>JintAwaitExpression.SuspendForAwait</c>
/// re-captures <c>_savedContext</c> at <em>every</em> await, so the environment current at the top
/// of a replayed body is the one that was live at the await. When the body declares a
/// <c>let</c>/<c>const</c>, that is the body block's environment, and the loop took it as its own
/// outer environment — then restored it on exit.
///
/// Three observable failures followed, the first two loud and the third silent:
/// reading an outer binding after the loop threw <see cref="InvalidCastException"/> from
/// <c>JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue</c>, which casts to
/// <c>GlobalEnvironment</c> on the assumption that only the global environment has a null
/// <c>_outerEnv</c> — but a block environment parked into <c>JintBlockStatement._cachedEnv</c> is
/// detached (<c>_outerEnv = null</c>) at park time, and has exactly that shape; reading
/// <c>this</c> hung forever in <c>ExecutionContext.GetThisEnvironment()</c>, whose
/// <c>while (true)</c> walk relies on the chain ending at the global environment; and the block's
/// own bindings stayed visible after the loop.
///
/// A closure anywhere in the loop body masked the first two entirely — not by disabling the
/// iteration-environment reuse optimisation (forcing that off changes nothing), but because
/// <c>BlockState</c> only takes slot-backed storage when
/// <c>EnvironmentEscapeAstVisitor.MayEscape</c> is false, and only a slot-backed block
/// environment is ever parked and detached. The wrong environment was restored either way, which
/// is why the closure shapes below assert the block's bindings are gone rather than merely that
/// nothing threw.
///
/// The fix mirrors <c>JintForInForOfStatement.BodyEvaluation</c>, which restores its
/// <c>oldEnv</c> from <c>ForOfSuspendData.OuterEnv</c> for this same reason (commit f089071fc).
/// Generators need no such thing: they capture their context once at GeneratorStart and re-enter
/// that same function-level context on every resume.
/// </summary>
public class ForLoopIterationEnvironmentAwaitTests
{
    /// <summary>
    /// Evaluates <paramref name="script"/> on a dedicated background thread and fails on a join
    /// timeout rather than hanging the run.
    /// <para>
    /// This is not defensive padding. One manifestation of the defect these tests cover is an
    /// infinite walk in <c>ExecutionContext.GetThisEnvironment()</c>, and a <c>TimeoutInterval</c>
    /// constraint cannot stop it — Jint does not evaluate constraints for event-loop jobs, so it
    /// cannot interrupt a continuation. Without this, a single regression wedges the whole test
    /// class (xUnit runs a class's tests sequentially) and CI hangs instead of reporting.
    /// </para>
    /// </summary>
    private static JsValue RunAsync(string script)
    {
        JsValue result = null;
        Exception failure = null;

        var worker = new Thread(() =>
        {
            try
            {
                result = new Engine().Evaluate(script).UnwrapIfPromise();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        {
            // If it is spinning, it must not keep the test process alive.
            IsBackground = true,
        };

        worker.Start();

        if (!worker.Join(TimeSpan.FromSeconds(30)))
        {
            throw new Xunit.Sdk.XunitException(
                "evaluation did not complete within 30s — the lexical environment chain is most "
                + "likely corrupt, leaving GetThisEnvironment() or an identifier walk spinning");
        }

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"evaluation threw: {failure}");
        }

        return result;
    }

    [Fact]
    public void ShouldSeeOuterBindingAfterAwaitingLoopWithBlockScopedDeclaration()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    await Promise.resolve();
                    const x = i + 1;
                    void x;
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWhenDeclarationPrecedesTheAwait()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    const x = i + 1;
                    void x;
                    await Promise.resolve();
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWithLetDeclarationInBody()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    await Promise.resolve();
                    let x = i + 1;
                    void x;
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWhenAwaitIsNestedDeeperThanTheDeclaration()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    const x = i + 1;
                    if (x > 0) { if (x > 0) { await Promise.resolve(); } }
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWhenDeclarationAndAwaitShareANestedBlock()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    { const x = i + 1; void x; await Promise.resolve(); }
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    /// <summary>
    /// The <c>this</c> manifestation. Before the fix this did not throw — it spun forever in
    /// <c>ExecutionContext.GetThisEnvironment()</c>. A <c>TimeoutInterval</c> constraint does NOT
    /// help: Jint does not evaluate constraints for event-loop jobs, so it cannot interrupt a
    /// continuation, which is precisely the case here. The evaluation therefore runs on its own
    /// background thread and the test fails on a join timeout instead of hanging the run.
    /// </summary>
    [Fact]
    public void ShouldReadThisAfterAwaitingLoopWithBlockScopedDeclaration()
    {
        var result = RunAsync("""
            class K {
                async leaf() { return 1; }
                async run() {
                    for (let i = 0; i < 1; i++) {
                        const x = i;
                        void x;
                        await this.leaf();
                    }
                    return await this.leaf();
                }
            }
            new K().run()
            """);

        result.Should().Be(1);
    }

    [Fact]
    public void ShouldPreserveLoopVariableSemanticsAcrossAwait()
    {
        // The per-iteration environment must still be per-iteration: each iteration's `i` is a
        // distinct binding. Guards against "fix it by never creating the environment".
        var result = RunAsync("""
            (async () => {
                const seen = [];
                for (let i = 0; i < 3; i++) {
                    await Promise.resolve();
                    const doubled = i * 2;
                    seen.push(doubled);
                }
                return seen.join(',');
            })()
            """);

        result.Should().Be("0,2,4");
    }

    [Fact]
    public void ShouldStillCaptureDistinctBindingsPerIterationWhenBodyAwaits()
    {
        // The reuse optimisation exists to avoid allocating an environment per iteration when
        // nothing captures it. Where something DOES capture it, each closure must still see its
        // own `i` — the classic per-iteration-binding guarantee — even with an await in the body.
        var result = RunAsync("""
            (async () => {
                const fns = [];
                for (let i = 0; i < 3; i++) {
                    await Promise.resolve();
                    const captured = i;
                    fns.push(() => `${i}:${captured}`);
                }
                return fns.map(f => f()).join(',');
            })()
            """);

        result.Should().Be("0:0,1:1,2:2");
    }

    [Fact]
    public void ShouldHandleNestedForLoopsWhereOnlyTheInnerOneAwaits()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 2; i++) {
                    for (let j = 0; j < 2; j++) {
                        await Promise.resolve();
                        const x = j;
                        void x;
                    }
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingAfterAForAwaitOfInsideAForLoopBody()
    {
        // `for await...of` carries an implicit await, so it suspends the body the same way.
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            async function* gen() { yield 1; }
            (async () => {
                for (let i = 0; i < 1; i++) {
                    const x = i;
                    void x;
                    for await (const v of gen()) { void v; }
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    /// <summary>
    /// The silent manifestation. A closure in the body keeps the block environment out of the
    /// park-and-detach path, so nothing throws — but the loop still restored the block's
    /// environment on exit, leaving the block's own bindings resolvable afterwards. Asserting
    /// only "it did not throw" passes over this, which is why it is asserted directly.
    /// </summary>
    [Fact]
    public void ShouldNotLeakBlockScopedDeclarationPastTheLoopWhenAClosureMasksTheThrow()
    {
        var result = RunAsync("""
            (async () => {
                for (let i = 0; i < 1; i++) {
                    const g = () => i;
                    void g;
                    await Promise.resolve();
                    const leaked = i + 1;
                    void leaked;
                }
                try { return 'LEAKED:' + leaked; }
                catch (e) { return e.name; }
            })()
            """);

        result.Should().Be("ReferenceError");
    }

    [Fact]
    public void ShouldNotLeakBlockScopedDeclarationPastTheLoopWithoutAClosure()
    {
        var result = RunAsync("""
            (async () => {
                for (let i = 0; i < 1; i++) {
                    await Promise.resolve();
                    const leaked = i + 1;
                    void leaked;
                }
                try { return 'LEAKED:' + leaked; }
                catch (e) { return e.name; }
            })()
            """);

        result.Should().Be("ReferenceError");
    }

    /// <summary>
    /// The header suspends rather than the body, so no block environment is involved and nothing
    /// is ever parked — but the loop still took the suspension-point environment (here its own
    /// iteration environment) as its outer one, and restored it on exit. That leaves the loop
    /// variable itself resolvable afterwards.
    /// </summary>
    [Fact]
    public void ShouldNotLeakTheLoopVariableWhenTheTestExpressionAwaits()
    {
        var result = RunAsync("""
            (async () => {
                for (let i = 0; i < await Promise.resolve(1); i++) { }
                try { return 'LEAKED:' + i; }
                catch (e) { return e.name; }
            })()
            """);

        result.Should().Be("ReferenceError");
    }

    [Fact]
    public void ShouldNotLeakTheLoopVariableWhenTheUpdateExpressionAwaits()
    {
        var result = RunAsync("""
            (async () => {
                for (let i = 0; i < 2; i = i + await Promise.resolve(1)) { }
                try { return 'LEAKED:' + i; }
                catch (e) { return e.name; }
            })()
            """);

        result.Should().Be("ReferenceError");
    }

    /// <summary>
    /// The loop must restore its outer environment across more than one suspension — the saved
    /// outer environment has to stay stable when it is re-saved on each subsequent await.
    /// </summary>
    [Fact]
    public void ShouldRestoreTheOuterEnvironmentAcrossManyIterationsThatEachAwait()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 5; i++) {
                    await Promise.resolve();
                    const x = i;
                    await Promise.resolve();
                    void x;
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    /// <summary>
    /// An outer binding declared in a block between the function and the loop must still resolve
    /// after the loop: the restored environment has to be the loop's real outer environment, not
    /// merely some environment that happens to reach the global one.
    /// </summary>
    [Fact]
    public void ShouldSeeAnEnclosingBlockBindingAfterTheLoop()
    {
        var result = RunAsync("""
            (async () => {
                {
                    const enclosing = 'enclosing-visible';
                    for (let i = 0; i < 1; i++) {
                        await Promise.resolve();
                        const x = i;
                        void x;
                    }
                    return enclosing;
                }
            })()
            """);

        result.Should().Be("enclosing-visible");
    }

    /// <summary>
    /// A closure created in the body BEFORE the await captures that iteration's environment, so a
    /// write to the loop variable AFTER the await must be visible through it. Rebuilding the
    /// iteration environment on resume — instead of resuming into the one the suspension left —
    /// stranded the closure on the old environment and silently lost the write. Node returns 42.
    /// </summary>
    [Fact]
    public void ShouldSeeAPostAwaitWriteThroughAClosureCapturedBeforeTheAwait()
    {
        var result = RunAsync("""
            (async () => {
                let get;
                for (let i = 0; i < 1; i++) {
                    get = function () { return i; };
                    await Promise.resolve();
                    i = 42;
                }
                return get();
            })()
            """);

        result.Should().Be(42);
    }

    /// <summary>
    /// The same lost write, but observed as control flow: writing the loop variable through a
    /// closure after the await must end the loop. Before the fix the write went to an abandoned
    /// environment, the loop's own test kept reading the live one, and it ran the full four trips.
    /// </summary>
    [Fact]
    public void ShouldEndTheLoopWhenAClosureWritesTheLoopVariableAfterTheAwait()
    {
        var result = RunAsync("""
            (async () => {
                let setter = null, trips = 0;
                for (let i = 0; i < 4; i++) {
                    if (!setter) { setter = function (v) { i = v; }; }
                    await Promise.resolve();
                    setter(100);
                    trips++;
                }
                return trips;
            })()
            """);

        result.Should().Be(1);
    }

    /// <summary>
    /// A resume lands mid-iteration, where the spec creates no new per-iteration environment.
    /// Creating one anyway forks away from the environment the body already captured, so closures
    /// pushed on either side of the await must still agree on the iteration they belong to.
    /// </summary>
    [Fact]
    public void ShouldKeepClosuresOnBothSidesOfTheAwaitInTheSameIteration()
    {
        var result = RunAsync("""
            (async () => {
                const before = [], after = [];
                for (let i = 0; i < 3; i++) {
                    before.push(() => i);
                    await Promise.resolve();
                    after.push(() => i);
                }
                return before.map(f => f()).join(',') + '|' + after.map(f => f()).join(',');
            })()
            """);

        result.Should().Be("0,1,2|0,1,2");
    }

    // ---- shapes that already worked; kept so a fix cannot regress them ----

    [Fact]
    public void ShouldSeeOuterBindingWhenLoopBodyHasNoDeclaration()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 256; i++) { await Promise.resolve(); }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWhenDeclarationBlockClosesBeforeTheAwait()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    if (i >= 0) { const x = i; void x; }
                    await Promise.resolve();
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWithVarInitialiser()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (var i = 0; i < 1; i++) {
                    await Promise.resolve();
                    const x = i + 1;
                    void x;
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingWhenBodyContainsAClosure()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                for (let i = 0; i < 1; i++) {
                    const f = () => i;
                    void f();
                    await Promise.resolve();
                }
                return OUTER;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldSeeOuterBindingFromAGeneratorLoopThatYields()
    {
        var result = RunAsync("""
            const OUTER = 'outer-visible';
            (async () => {
                function* g() {
                    for (let i = 0; i < 1; i++) { yield i; const x = i + 1; void x; }
                    return OUTER;
                }
                const it = g();
                let r = it.next();
                while (!r.done) r = it.next();
                return r.value;
            })()
            """);

        result.Should().Be("outer-visible");
    }
}
