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
/// <remarks>
/// Its own non-parallel collection, because the failure mode it targets is an uninterruptible spin.
/// <see cref="DedicatedThread"/> caps the join and drops the runaway thread's priority, but nothing
/// can stop it, so it keeps a core busy until the process exits. Running this class alone means a
/// regression cannot land that load on top of unrelated tests — which is the condition the suite's
/// known wall-clock flakes fail under. Against the unfixed engine a large fraction of these shapes
/// leave a spinning thread behind, so the load is a real quantity, not a hypothetical one.
/// </remarks>
[CollectionDefinition(nameof(ForLoopIterationEnvironmentAwaitTests), DisableParallelization = true)]
[Collection(nameof(ForLoopIterationEnvironmentAwaitTests))]
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

        DedicatedThread.Run(
            () => result = new Engine().Evaluate(script).UnwrapIfPromise(),
            joinTimeout: TestBudgets.WedgeCeiling,
            timeoutMessage: $"evaluation did not complete within {TestBudgets.WedgeCeiling} — the lexical "
                + "environment chain is most likely corrupt, leaving GetThisEnvironment() or an identifier "
                + "walk spinning");

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
        // Every iteration's body must observe the value the header reached for that trip. This does
        // NOT pin the per-iteration environment — each read of `i` happens in the iteration that
        // wrote it, so a single shared environment still produces "0,2,4" and this test still
        // passes. ShouldStillCaptureDistinctBindingsPerIterationWhenBodyAwaits below is the one
        // that actually guards against "fix it by never creating the environment", because only a
        // closure outliving its iteration can tell the two apart.
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

    // ---------------------------------------------------------------------------------------
    // An await in the for-INIT.
    //
    // The suspension-node range test that decides whether a re-entry is a replay
    // (IsNodeInsideForStatementExcludingInit) deliberately excludes the init, because a resume
    // from the init must re-run the init to complete its pending awaits. That exclusion used to
    // gate the oldEnv restore as well, so an init resume went on taking oldEnv from the ambient
    // execution context — which on a replay is the environment that was live at the await, i.e.
    // the ABANDONED first-attempt loop environment, whose header bindings never got past TDZ
    // (the init suspended part-way through initializing them).
    //
    // Restoring that on loop exit leaves a dead environment current after the loop, so a later
    // read of a name the header shadows resolves into it and throws ReferenceError — including
    // through `typeof`, which must never throw for an out-of-scope name. It is also
    // self-compounding: the value restored on exit is the same one written back into the suspend
    // data, so each further suspension chains another dead loop environment onto the next one's
    // outer link.
    //
    // Every shape below is checked against Node 24. The head is the trigger, so the matrix runs
    // it in each of its forms — simple, destructured, multi-declarator, multi-await and const —
    // crossed with whether the body awaits at all, since the defect does not need a body await.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ShouldNotLeakTheLoopHeaderNameAfterAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let i = await P(0); i < 3; i++) { await P(1); }
                return typeof i;
            })()
            """);

        result.Should().Be("undefined");
    }

    /// <summary>
    /// The body never awaits, so the loop runs to completion in one go on the init resume. The
    /// init suspension alone is enough — this shape threw before the iteration-environment work
    /// too, which is why it is a fix rather than a regression guard.
    /// </summary>
    [Fact]
    public void ShouldNotLeakTheLoopHeaderNameWhenOnlyTheInitAwaits()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let i = await P(0); i < 3; i++) { }
                return typeof i;
            })()
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotLeakADestructuredArrayHeadAfterAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let [a, b] = await P([0, 10]); a < 2; a++, b++) { await P(1); }
                return typeof a;
            })()
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotLeakADestructuredObjectHeadAfterAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let { a, b } = await P({ a: 0, b: 10 }); a < 2; a++, b++) { await P(1); }
                return typeof a;
            })()
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotLeakTheAwaitedDeclaratorOfAMultiDeclaratorHead()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let i = 0, j = await P(5); i < 3; i++, j++) { await P(1); }
                return typeof j;
            })()
            """);

        result.Should().Be("undefined");
    }

    /// <summary>
    /// Two awaits in the init means two consecutive init resumes. The second one is what proves
    /// the repair happens at the read: the first resume writes its own oldEnv back into the
    /// suspend data, so an entry left poisoned would be handed straight to the next resume.
    /// </summary>
    [Fact]
    public void ShouldNotLeakTheLoopHeaderNameAfterTwoAwaitsInTheInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let i = (await P(1)) + (await P(2)); i < 6; i++) { await P(1); }
                return typeof i;
            })()
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotLeakAConstHeadAfterAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (const i = await P(0); i < 3; ) { await P(1); break; }
                return typeof i;
            })()
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotLeakTheLoopHeaderNameAfterBreakingOutOfAnAwaitingInitLoop()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                for (let i = await P(0); i < 5; i++) { await P(1); if (i === 1) break; }
                return typeof i;
            })()
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotLeakTheLoopHeaderNameWhenTheAwaitingInitLoopIsInsideTry()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                try {
                    for (let i = await P(0); i < 2; i++) { await P(1); }
                    return typeof i;
                } finally { }
            })()
            """);

        result.Should().Be("undefined");
    }

    /// <summary>
    /// The silent half of the defect. With an outer binding of the same name the dead environment
    /// shadows it instead of throwing, so the loop's private counter is what a later read sees —
    /// a wrong value rather than an error, which is the shape an embedder would never notice.
    /// </summary>
    [Fact]
    public void ShouldNotShadowASameNamedOuterBindingAfterAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                let x = 'outer-visible';
                for (let x = await P(0); x < 3; x++) { await P(1); }
                return x;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldNotShadowASameNamedOuterBindingWhenOnlyTheInitAwaits()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                let x = 'outer-visible';
                for (let x = await P(0); x < 3; x++) { }
                return x;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    [Fact]
    public void ShouldNotShadowASameNamedOuterBindingAfterADestructuredAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                let a = 'outer-visible';
                for (let [a, b] = await P([0, 10]); a < 2; a++, b++) { await P(1); }
                return a;
            })()
            """);

        result.Should().Be("outer-visible");
    }

    /// <summary>
    /// The loop still has to compute the right answer: the environment repair must not cost the
    /// header its identity across resumes, or the trip count changes.
    /// </summary>
    [Fact]
    public void ShouldRunTheCorrectTripCountWithAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                let n = 0;
                for (let i = await P(0); i < 5; i++) { await P(1); n++; }
                return n;
            })()
            """);

        result.Should().Be(5);
    }

    [Fact]
    public void ShouldAccumulateAcrossAnAwaitingInitLoopWithABodyDeclaration()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                let s = 0;
                for (let i = await P(0); i < 3; i++) { const k = await P(i + 1); s += k; }
                return s;
            })()
            """);

        result.Should().Be(6);
    }

    /// <summary>
    /// Per-iteration bindings still fork correctly when the head awaits: each closure must see
    /// its own iteration's value, not the last one.
    /// </summary>
    [Fact]
    public void ShouldGivePerIterationClosuresTheirOwnBindingWithAnAwaitingInit()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                const fs = [];
                for (let i = await P(0); i < 3; i++) { fs.push(() => i); await P(1); }
                return fs.map(f => f()).join(',');
            })()
            """);

        result.Should().Be("0,1,2");
    }

    [Fact]
    public void ShouldReadThisAfterAnAwaitingInitLoop()
    {
        var result = RunAsync("""
            class K {
                async leaf() { return 1; }
                async run() {
                    for (let i = await this.leaf(); i < 3; i++) { await this.leaf(); }
                    return await this.leaf();
                }
            }
            new K().run()
            """);

        result.Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------
    // A suspension in the for-init used to throw its own TDZ error out of the loop.
    //
    // The suspend-data save in JintForStatement's finally reads every header binding through
    // GetBindingValue so a later resume can replay the values. A suspension *inside* the init
    // reaches that save with the declaration half-done, and GetBindingValue on an uninitialized
    // binding does not return a placeholder — it throws the TDZ ReferenceError.
    //
    // The throw was invisible, because of where it landed. It skipped the rest of the finally
    // (the lexical environment was never restored) and surfaced in JintStatementList's catch,
    // which clears the enclosing list's resume position. For an async function body that position
    // is the only record of how far the replay had progressed, so the next resume restarted the
    // body from its first statement. Three distinct symptoms followed, all from that one throw:
    //
    //   * a silent infinite re-suspension. Restarting the body re-ran any EARLIER awaiting loop,
    //     whose awaits were no longer memoized (a loop clears _completedAwaits each trip), so it
    //     suspended again and restarted the body again. `await` in a for-init after any earlier
    //     awaiting loop never settled — the engine live, making no progress, and a host without a
    //     TimeoutInterval hanging outright. Constraints do not save it: Jint does not evaluate
    //     them for event-loop jobs.
    //   * function-level `let`/`const` re-initialized. A restart re-ran the declarations before
    //     the loop, so accumulators silently reset mid-run.
    //   * generators broken by the same read. A generator never re-enters the loop statement, so
    //     it never reached the environment-restore path at all — the throw was its only symptom.
    //
    // Every shape below is checked against Node 24. They assert the exact sequence of awaited
    // calls, not merely that nothing threw, so a regression that merely re-orders work fails too.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Bounded probe: the counter throws after a small budget, so a regression to the infinite
    /// re-suspension fails in milliseconds with the observed sequence rather than spinning until
    /// <see cref="RunAsync"/>'s 30-second join expires.
    /// </summary>
    private const string Probe = """
        const seq = [];
        function T(x) {
            seq.push(x);
            if (seq.length > 12) { throw new Error('RUNAWAY seq=[' + seq.join(',') + ']'); }
            return Promise.resolve(x);
        }
        """;

    [Fact]
    public void ShouldSettleAnInitAwaitFollowingAnAwaitingForLoop()
    {
        var result = RunAsync($$"""
            {{Probe}}
            (async () => {
                for (let i = 0; i < 2; i++) { await T(1); }
                for (let i = await T(10); i < 12; i++) { }
                return seq.join(',');
            })()
            """);

        result.Should().Be("1,1,10");
    }

    [Fact]
    public void ShouldSettleAnInitAwaitFollowingAnAwaitingWhileLoop()
    {
        var result = RunAsync($$"""
            {{Probe}}
            (async () => {
                let k = 0;
                while (k < 2) { await T(1); k++; }
                for (let i = await T(10); i < 12; i++) { }
                return seq.join(',');
            })()
            """);

        result.Should().Be("1,1,10");
    }

    [Fact]
    public void ShouldSettleAnInitAwaitFollowingAnAwaitingForOfLoop()
    {
        var result = RunAsync($$"""
            {{Probe}}
            (async () => {
                for (const v of [1, 2]) { await T(1); }
                for (let i = await T(10); i < 12; i++) { }
                return seq.join(',');
            })()
            """);

        result.Should().Be("1,1,10");
    }

    [Fact]
    public void ShouldSettleAnInitAwaitFollowingAnAwaitingForAwaitOfLoop()
    {
        var result = RunAsync($$"""
            {{Probe}}
            (async () => {
                for await (const v of [1, 2]) { await T(1); }
                for (let i = await T(10); i < 12; i++) { }
                return seq.join(',');
            })()
            """);

        result.Should().Be("1,1,10");
    }

    [Fact]
    public void ShouldSettleTwoConsecutiveInitAwaitLoops()
    {
        var result = RunAsync($$"""
            {{Probe}}
            (async () => {
                for (let i = await T(0); i < 2; i++) { await T(1); }
                for (let j = await T(10); j < 12; j++) { await T(2); }
                return seq.join(',');
            })()
            """);

        result.Should().Be("0,1,1,10,2,2");
    }

    /// <summary>
    /// The nested shape, where the inner loop's init is what awaits. This is the case that showed
    /// the restart most clearly: the iterations themselves were all performed, but a function-level
    /// accumulator declared before the loops was re-initialized part-way through, so only the
    /// trailing iterations survived in it.
    /// </summary>
    [Fact]
    public void ShouldNotResetFunctionLocalsWhenANestedInitAwaits()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                const seen = [];
                for (let i = 0; i < 2; i++) {
                    for (let j = await P(0); j < 2; j++) { await P(1); seen.push(i + ':' + j); }
                }
                return seen.join(' ');
            })()
            """);

        result.Should().Be("0:0 0:1 1:0 1:1");
    }

    [Fact]
    public void ShouldNotResetFunctionLocalsAcrossAnInitAwaitLoopThatFollowsAnother()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                const seen = [];
                for (let i = 0; i < 2; i++) { await P(1); seen.push('a' + i); }
                for (let j = await P(0); j < 2; j++) { await P(1); seen.push('b' + j); }
                return seen.join(',');
            })()
            """);

        result.Should().Be("a0,a1,b0,b1");
    }

    [Fact]
    public void ShouldRunEveryIterationOfNestedInitAwaitLoops()
    {
        var result = RunAsync("""
            (async () => {
                async function P(x) { return x; }
                let n = 0;
                for (let i = await P(0); i < 2; i++) {
                    for (let j = await P(0); j < 2; j++) { await P(1); n++; }
                }
                return n;
            })()
            """);

        result.Should().Be(4);
    }

    /// <summary>
    /// A generator never re-enters the loop statement — it captures its context once at
    /// GeneratorStart and resumes inside the loop — so it never reached the environment-restore
    /// path these tests started with. The suspend-data read is the one part of that path a
    /// generator does execute, which is why a <c>yield</c> in a for-init failed and a
    /// <c>yield</c> in the body never did.
    /// </summary>
    [Fact]
    public void ShouldNotLeakTheLoopHeaderNameAfterAGeneratorYieldsInTheInit()
    {
        var result = RunAsync("""
            function* g() { for (let i = yield 0; i < 3; i++) { yield 1; } return typeof i; }
            const it = g();
            it.next();
            it.next(0);
            let r = it.next();
            while (!r.done) { r = it.next(); }
            r.value
            """);

        result.Should().Be("undefined");
    }

    [Fact]
    public void ShouldNotShadowASameNamedOuterBindingAfterAGeneratorYieldsInTheInit()
    {
        var result = RunAsync("""
            function* g() {
                let x = 'outer-visible';
                for (let x = yield 0; x < 3; x++) { yield 1; }
                return x;
            }
            const it = g();
            it.next();
            it.next(0);
            let r = it.next();
            while (!r.done) { r = it.next(); }
            r.value
            """);

        result.Should().Be("outer-visible");
    }

    /// <summary>
    /// The test is the third resume position, and the only one that legitimately re-runs the test
    /// from the top — so neither <c>skipTestOnce</c> nor <c>resumeUpdateOnce</c> is set for it, and
    /// the entry <c>CreatePerIterationEnvironment</c> keyed on those two ran anyway. That forked
    /// away from the iteration environment the resume had just reinstated, stranding every closure
    /// the test created before the await at the value its binding held when it was made: each one
    /// froze one iteration early. Keying the guard on the restored environment instead covers all
    /// three positions, since a restored environment means an iteration is already in progress.
    /// </summary>
    [Fact]
    public void ShouldNotStrandClosuresCreatedInTheTestBeforeAnAwait()
    {
        var result = RunAsync("""
            (async () => {
                const fns = [];
                for (let i = 0; (fns.push(() => i), await Promise.resolve(i < 2)); ) { i++; }
                return fns.map(f => f()).join(',');
            })()
            """);

        result.Should().Be("1,2,2");
    }

    [Fact]
    public void ShouldNotStrandClosuresCreatedInTheTestWhenTheLoopHasAnUpdate()
    {
        var result = RunAsync("""
            (async () => {
                const fns = [];
                for (let i = 0; (fns.push(() => i), await Promise.resolve(i < 3)); i++) { }
                return fns.map(f => f()).join(',');
            })()
            """);

        result.Should().Be("0,1,2,3");
    }

    [Fact]
    public void ShouldKeepClosuresFromBothSidesOfATestAwaitInTheirOwnIterations()
    {
        // A closure made in the test and one made in the body of the same iteration must observe
        // the same binding — the pair moves together from iteration to iteration.
        var result = RunAsync("""
            (async () => {
                const fns = [];
                for (let i = 0; (fns.push(() => 't' + i), await Promise.resolve(i < 2)); i++) { fns.push(() => 'b' + i); }
                return fns.map(f => f()).join(',');
            })()
            """);

        result.Should().Be("t0,b0,t1,b1,t2");
    }

    /// <summary>
    /// The loop's suspension check for the test used to sit inside the <c>if (!testValue)</c>
    /// branch, so it was only consulted when the test evaluated falsy. A suspending test that
    /// nevertheless leaves a truthy value behind — the trip straight after a resume, whose memo for
    /// the suspending expression is not cleared while the loop is still resuming — therefore fell
    /// through into the body, running one extra iteration per resume. A <c>yield</c> in the test
    /// showed it as a loop variable incremented twice between two yields (0, 1, 3, …).
    /// </summary>
    [Fact]
    public void ShouldNotRunAnExtraIterationWhenTheTestSuspendsWhileTruthy()
    {
        var result = RunAsync("""
            function* g() {
                const fns = [];
                const yields = [];
                for (let i = 0; (fns.push(() => i), yields.push(i), yield i); ) { i++; }
                return yields.join(',') + ' / ' + fns.map(f => f()).join(',');
            }
            const it = g();
            it.next();
            it.next(true);
            it.next(true);
            it.next(false).value
            """);

        result.Should().Be("0,1,2 / 1,2,2");
    }

    [Fact]
    public void ShouldRunTheBodyExactlyOncePerIterationWhenTheTestAwaits()
    {
        var result = RunAsync("""
            (async () => {
                let runs = 0;
                for (let i = 0; await Promise.resolve(i < 3); i++) { runs++; }
                return runs;
            })()
            """);

        result.Should().Be(3);
    }

    /// <summary>
    /// A generator needs the saved iteration environment exactly as much as an async function does.
    /// The "a generator re-enters one function-level context" argument is about the <em>outer</em>
    /// environment; the per-iteration environment is rebuilt on a generator's replay just the same.
    /// The write after the yield must land in the environment the closure captured, so the closure
    /// sees 42; without the save it is stranded on an abandoned environment and reports the stale 0.
    /// Pins the field against being guarded on the suspendable being an async function.
    /// </summary>
    [Fact]
    public void ShouldNotStrandAGeneratorClosureOverTheIterationEnvironment()
    {
        var result = RunAsync("""
            function* g() { let get; for (let i = 0; i < 1; i++) { get = () => i; yield i; i = 42; } return get(); }
            const it = g();
            it.next();
            it.next().value
            """);

        result.Should().Be(42);
    }

    /// <summary>
    /// A <c>yield</c> in the head of a generator's for statement. The loop used to read its header
    /// bindings on every suspension to save them, which threw the TDZ <c>ReferenceError</c> out of
    /// the finally when the init had not finished initializing them yet. Those saved values had no
    /// reader, so the read is gone rather than merely guarded.
    /// </summary>
    [Fact]
    public void ShouldCompleteAGeneratorThatYieldsInTheForInit()
    {
        var result = RunAsync("""
            function* g() { for (let i = yield 0; i < 1; i++) { } return 'done'; }
            const it = g();
            it.next();
            it.next(0).value
            """);

        result.Should().Be("done");
    }

    /// <summary>
    /// A <c>with</c> statement inside an async function used to build a fresh object environment on
    /// every replay, so the loop's saved outer environment pointed at one no longer on the chain.
    /// Restoring it detached the chain from the global environment, and the next identifier walk
    /// failed its <c>GlobalEnvironment</c> cast with an <see cref="InvalidCastException"/> — a .NET
    /// exception, invisible to a script <c>try</c>/<c>catch</c> and to an embedder catching
    /// <see cref="JavaScriptException"/>, so an embedder took an unhandled crash out of
    /// <c>Evaluate</c> instead of a rejected promise.
    /// </summary>
    [Fact]
    public void ShouldResumeIntoTheSameWithEnvironmentItSuspendedIn()
    {
        var result = RunAsync("""
            var obj = { v: 'V' };
            (async () => {
                var seen = [];
                with (obj) {
                    for (let i = 0; i < 2; i++) { let b = i; await Promise.resolve(); seen.push(v + '/' + b); }
                    seen.push('post:' + v);
                }
                return seen.join(',');
            })()
            """);

        result.Should().Be("V/0,V/1,post:V");
    }

    [Fact]
    public void ShouldKeepTheWithEnvironmentLiveForAForOfBodyThatAwaits()
    {
        var result = RunAsync("""
            var obj = { v: 'V' };
            (async () => {
                var seen = [];
                with (obj) {
                    for (const x of [1, 2]) { await Promise.resolve(); seen.push(v + x); }
                    seen.push('post:' + v);
                }
                return seen.join(',');
            })()
            """);

        result.Should().Be("V1,V2,post:V");
    }

    [Fact]
    public void ShouldResumeIntoNestedWithEnvironments()
    {
        var result = RunAsync("""
            var obj = { v: 'V' };
            (async () => {
                var o2 = { w: 'W' };
                var seen = [];
                with (obj) {
                    with (o2) { for (let i = 0; i < 2; i++) { await Promise.resolve(); seen.push(v + w + i); } }
                    seen.push('post:' + v);
                }
                return seen.join(',');
            })()
            """);

        result.Should().Be("VW0,VW1,post:V");
    }

    /// <summary>
    /// The <c>with</c> object expression can itself suspend. Building the environment over the
    /// placeholder value would run the body before the object is known, and again on resume.
    /// </summary>
    [Fact]
    public void ShouldHandleAnAwaitInTheWithObjectExpression()
    {
        var result = RunAsync("""
            var obj = { v: 'V' };
            (async () => {
                var seen = [];
                with (await Promise.resolve(obj)) {
                    for (let i = 0; i < 2; i++) { await Promise.resolve(); seen.push(v + i); }
                }
                return seen.join(',');
            })()
            """);

        result.Should().Be("V0,V1");
    }
}
