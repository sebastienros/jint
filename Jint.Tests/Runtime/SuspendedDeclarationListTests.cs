#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A <c>let</c>/<c>const</c>/<c>var</c> declaration <em>list</em> — one statement, several declarators — whose
/// later declarator suspends must resume at that declarator, not at the first one.
/// <para>
/// Jint resumes a suspended body by replaying the statement it suspended in, and
/// <c>JintVariableDeclaration</c> replays from its first declarator every time: it records nothing about
/// which declarator suspended, where a sequence expression parks the operand index it was at and resumes
/// there. The awaits that already completed come back from the completed-await memo, so the answer is
/// still right, and the whole defect is in the declarators that do <em>not</em> await — each runs once
/// more for every suspension in a later declarator of the same list, side effects and all, and a
/// <c>let</c>/<c>const</c> is initialized again with whatever it produces this time. The <c>var</c> lane
/// adds one more: its identifier-assignment helper hands back the suspension placeholder without the
/// handler testing <c>IsSuspended</c>, so the loop goes on to the <em>next</em> declarators while the frame
/// is already suspended, and they run once more again on top. A single declarator with two awaits, the
/// same declarators as separate statements, and a declarator after the list's only await are all correct.
/// </para>
/// <para>
/// Each probe counts how many times <c>side()</c> ran, pushes <c>pre</c> before the list and <c>post</c>
/// after it, and asserts <c>&lt;report&gt;|pre,post</c>, the way
/// <see cref="SuspendedDestructuringAssignmentTests"/> does for the placeholder consumers. The value
/// tests pin the shape that made this a real failure: an earlier declarator minted a token, the resume
/// minted another, and the thing stamped with the first was handed to a caller holding the second. The
/// tests named <c>…Control</c> pass on the unfixed tree and bound the claim. The handler reserves the
/// bookkeeping at build time for lists that can suspend part-way through, and a default inside a binding
/// pattern suspends as much as an initializer does, which the pattern-default tests pin.
/// </para>
/// </summary>
public class SuspendedDeclarationListTests
{
    private const string Setup = """
        var log = [];
        var runs = 0;
        var n = 0;
        var calls = 0;
        function side(v) { runs++; return v; }
        function mint() { return ++n; }
        function f() { calls++; return Promise.resolve(1); }
        """;

    /// <summary>
    /// Runs <paramref name="body"/> inside an async function bracketed by the <c>pre</c>/<c>post</c>
    /// markers and returns <c>"&lt;report&gt;|&lt;log&gt;"</c>.
    /// </summary>
    private static string AsyncProbe(string body, string report) => new Engine().Evaluate($$"""
        {{Setup}}
        async function m() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        m().then(function (r) { return String(r) + '|' + log.join(','); });
        """).UnwrapIfPromise().AsString();

    /// <summary>
    /// The synchronous-generator twin of <see cref="AsyncProbe"/>: every <c>yield</c> is resumed with its
    /// own yielded value, standing in for the awaited one.
    /// </summary>
    private static string GeneratorProbe(string body, string report) => new Engine().Evaluate($$"""
        {{Setup}}
        function* g() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        var it = g();
        var res = it.next();
        for (var i = 0; !res.done && i < 8; i++) res = it.next(res.value);
        String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
        """).AsString();

    /// <summary>
    /// The async-generator twin: <c>await</c>s and <c>yield</c>s both suspend it, and the consumer drives it
    /// with <c>next()</c> until it is done. Both drivers stop after a fixed number of steps, so a generator
    /// that never finishes fails the assertion instead of spinning the test host.
    /// </summary>
    private static string AsyncGeneratorProbe(string body, string report) => new Engine().Evaluate($$"""
        {{Setup}}
        async function* ag() {
          log.push('pre');
          {{body}}
          log.push('post');
          return {{report}};
        }
        async function drive() {
          var it = ag();
          var res = await it.next();
          for (var i = 0; !res.done && i < 8; i++) res = await it.next(res.value);
          return String(res.value) + (res.done ? '' : ' (not done after 8 steps)') + '|' + log.join(',');
        }
        drive();
        """).UnwrapIfPromise().AsString();

    // ------------------------------------------------------------------ the reported shapes, by position

    [Test]
    public void ADeclaratorBetweenTwoAwaitedDeclaratorsRunsOnce()
    {
        AsyncProbe("let a = await f(), s = side(0), b = await f();", "runs")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void ADeclaratorBeforeTwoAwaitedDeclaratorsRunsOnce()
    {
        AsyncProbe("let s = side(0), a = await f(), b = await f();", "runs")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void ADeclaratorBeforeTheListsOnlyAwaitedDeclaratorRunsOnce()
    {
        AsyncProbe("let s = side(0), a = await f();", "runs")
            .Should().Be("1|pre,post");
    }

    /// <summary>
    /// One replay per later await: on the unfixed tree this reads 4.
    /// </summary>
    [Test]
    public void ADeclaratorBeforeThreeAwaitedDeclaratorsRunsOnce()
    {
        AsyncProbe("let s = side(0), a = await f(), b = await f(), c = await f();", "runs")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void AConstDeclaratorBetweenTwoAwaitedDeclaratorsRunsOnce()
    {
        AsyncProbe("const a = await f(), s = side(0), b = await f();", "runs")
            .Should().Be("1|pre,post");
    }

    /// <summary>
    /// The <c>var</c> lane runs the declarator once more than <c>let</c> does (3 on the unfixed tree
    /// against 2), because the loop continues past the suspended declarator. The awaited call itself is
    /// never made while suspended — the call expression bails on the placeholder — so the call count is
    /// pinned alongside to say the fix changes nothing there.
    /// </summary>
    [Test]
    public void AVarDeclaratorBetweenTwoAwaitedDeclaratorsRunsOnce()
    {
        AsyncProbe("var a = await f(), s = side(0), b = await f();", "runs + '/' + calls")
            .Should().Be("1/2|pre,post");
    }

    [Test]
    public void AVarDeclaratorBeforeTheListsOnlyAwaitedDeclaratorRunsOnce()
    {
        AsyncProbe("var s = side(0), a = await f();", "runs + '/' + calls")
            .Should().Be("1/1|pre,post");
    }

    // ------------------------------------------------------------------ the value, not just the count

    /// <summary>
    /// The replay does not merely repeat a side effect: it re-initializes the earlier binding with a new
    /// value. On the unfixed tree <c>t</c> is the second token minted.
    /// </summary>
    [Test]
    public void AnEarlierDeclaratorKeepsItsValueAcrossTheResume()
    {
        AsyncProbe("let t = mint(), a = await f();", "t + '/' + n")
            .Should().Be("1/1|pre,post");
    }

    [Test]
    public void AConstDeclaratorIsInitializedOnceAcrossTheResume()
    {
        AsyncProbe("const t = mint(), a = await f(), u = mint();", "t + '/' + u + '/' + n")
            .Should().Be("1/2/2|pre,post");
    }

    /// <summary>
    /// The shape that failed in production: a token is minted, something is stamped with it behind an
    /// <c>await</c>, and the caller is handed the token. With the replay the stamp carries the first token
    /// and the caller the second.
    /// </summary>
    [Test]
    public void AValueStampedBehindAnAwaitMatchesTheTokenTheCallerGets()
    {
        AsyncProbe(
            "const data = (await f()), token = mint(), stamped = await Promise.resolve('stamp:' + token);",
            "stamped + ' token:' + token")
            .Should().Be("stamp:1 token:1|pre,post");
    }

    // ------------------------------------------------------------------ other declarator shapes and places

    [Test]
    public void ADestructuringDeclaratorBeforeAnAwaitedDeclaratorRunsOnce()
    {
        AsyncProbe("const { x } = { x: side(1) }, y = await f();", "runs + '/' + x")
            .Should().Be("1/1|pre,post");
    }

    /// <summary>
    /// The <c>await</c> is in a default inside the binding pattern, not in the initializer: the handler
    /// has to look at the whole declarator to know the list can suspend part-way through.
    /// </summary>
    [Test]
    public void ADeclaratorBeforeAnAwaitInABindingPatternsDefaultRunsOnce()
    {
        AsyncProbe("let s = side(0), { x = await f() } = {};", "runs + '/' + x")
            .Should().Be("1/1|pre,post");
    }

    /// <summary>
    /// A list that can suspend does not have to: the parked index is read by the resume it was parked
    /// for, and an iteration that never suspended leaves nothing behind for the next one.
    /// </summary>
    [Test]
    public void ADeclaratorBeforeAConditionallyAwaitedDeclaratorRunsOncePerIteration()
    {
        AsyncProbe(
            "var seen = []; for (let i = 0; i < 3; i++) { let s = side(i), a = i % 2 ? await f() : 2; seen.push(a); }",
            "runs + '/' + seen.join(',')")
            .Should().Be("3/2,1,2|pre,post");
    }

    /// <summary>
    /// Inside a loop it is once per iteration: 4 on the unfixed tree.
    /// </summary>
    [Test]
    public void ADeclarationListInsideALoopRunsEachDeclaratorOncePerIteration()
    {
        AsyncProbe("for (let i = 0; i < 2; i++) { let s = side(i), a = await f(); }", "runs")
            .Should().Be("2|pre,post");
    }

    /// <summary>
    /// A primitive operand takes <c>JintAwaitExpression</c>'s shortcut lane, which suspends through the
    /// same placeholder.
    /// </summary>
    [Test]
    public void ADeclaratorBeforeAnAwaitedPrimitiveRunsOnce()
    {
        AsyncProbe("let s = side(0), a = await 'abc';", "runs + '/' + a")
            .Should().Be("1/abc|pre,post");
    }

    // ------------------------------------------------------------------ a for head

    /// <summary>
    /// A <c>for</c> head is a declaration list the loop statement replays itself, in a loop environment
    /// of its own. The resume has to land in the environment the first attempt initialized <c>i</c> in,
    /// or the test reads a binding still in its temporal dead zone; and <c>j</c> must not leak past the
    /// loop, which the loop's own fixture pins too.
    /// </summary>
    [Test]
    public void ADeclaratorBeforeAnAwaitedDeclaratorInAForHeadRunsOnce()
    {
        AsyncProbe(
            "var seen = []; for (let i = side(0), j = await f(); i < 2; i++) seen.push(i + ':' + j);",
            "runs + '/' + seen.join(',') + '/' + typeof j")
            .Should().Be("1/0:1,1:1/undefined|pre,post");
    }

    /// <summary>
    /// A closure made in the head captures the loop environment, not a per-iteration copy, so it keeps
    /// reading the head's own value; reusing that environment across the resume must not change that.
    /// </summary>
    [Test]
    public void AClosureMadeInAForHeadBeforeTheAwaitKeepsReadingTheHeadsValue()
    {
        AsyncProbe(
            "var get = null; for (let i = 0, g = (get = function () { return i; }), j = await f(); i < 2; i++) {}",
            "get() + '/' + runs")
            .Should().Be("0/0|pre,post");
    }

    [Test]
    public void AVarDeclaratorBeforeAnAwaitedDeclaratorInAForHeadRunsOnce()
    {
        AsyncProbe(
            "var seen = []; for (var i = side(0), j = await f(); i < 2; i++) seen.push(i + ':' + j);",
            "runs + '/' + seen.join(',')")
            .Should().Be("1/0:1,1:1|pre,post");
    }

    [Test]
    public void ADeclaratorBeforeAYieldingDeclaratorInAForHeadRunsOnceInAGenerator()
    {
        GeneratorProbe(
            "var seen = []; for (let i = side(0), j = yield 1; i < 2; i++) seen.push(i + ':' + j);",
            "runs + '/' + seen.join(',') + '/' + typeof j")
            .Should().Be("1/0:1,1:1/undefined|pre,post");
    }

    // ------------------------------------------------------------------ the generator twins

    /// <summary>
    /// The resume mechanism is the generators', so a <c>yield</c> in a later declarator replays the same
    /// way: 3 on the unfixed tree.
    /// </summary>
    [Test]
    public void ADeclaratorBeforeTwoYieldingDeclaratorsRunsOnceInAGenerator()
    {
        GeneratorProbe("let s = side(0), a = yield 1, b = yield 2;", "runs")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void AnEarlierDeclaratorKeepsItsValueAcrossAGeneratorResume()
    {
        GeneratorProbe("let t = mint(), a = yield 1;", "t + '/' + n")
            .Should().Be("1/1|pre,post");
    }

    [Test]
    public void AVarDeclaratorBeforeTwoYieldingDeclaratorsRunsOnceInAGenerator()
    {
        GeneratorProbe("var s = side(0), a = yield 1, b = yield 2;", "runs")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void ADeclaratorBeforeAYieldInABindingPatternsDefaultRunsOnceInAGenerator()
    {
        GeneratorProbe("let s = side(0), [x = yield 1] = [];", "runs + '/' + x")
            .Should().Be("1/1|pre,post");
    }

    [Test]
    public void ADeclaratorBeforeAnAwaitedAndAYieldingDeclaratorRunsOnceInAnAsyncGenerator()
    {
        AsyncGeneratorProbe("let s = side(0), a = await f(), b = yield 1;", "runs")
            .Should().Be("1|pre,post");
    }

    // ------------------------------------------------------------------ the shapes that were always right

    [Test]
    public void ADeclaratorAfterTheListsOnlyAwaitedDeclaratorIsAControl()
    {
        AsyncProbe("let a = await f(), s = side(0);", "runs")
            .Should().Be("1|pre,post");
    }

    [Test]
    public void TheSameDeclaratorsAsSeparateStatementsAreAControl()
    {
        AsyncProbe("let a = await f(); let s = side(0); let b = await f();", "runs")
            .Should().Be("1|pre,post");
    }

    /// <summary>
    /// One declarator, two awaits: the initializer is a sequence expression, which parks the operand index
    /// it suspended at. Control.
    /// </summary>
    [Test]
    public void OneDeclaratorWithTwoAwaitsIsAControl()
    {
        AsyncProbe("let s = (side(0), (await f()) + (await f()));", "runs + '/' + s")
            .Should().Be("1/2|pre,post");
    }
}
