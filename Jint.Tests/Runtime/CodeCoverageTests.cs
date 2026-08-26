#nullable enable

using System.Collections.Generic;
using System.Linq;
using Jint.Runtime.Coverage;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

/// <summary>
/// Covers <c>Options.Coverage</c> and <see cref="Engine.DiagnosticOperations.GetCoverage"/>. The behaviour a third
/// party can see is pinned from outside the assembly in Jint.Tests.PublicInterface/CodeCoverageTests.cs; what
/// only this project can see — and what the design rests on — is where the counters live and which interpreter
/// lane they ride on, so those are pinned here.
/// </summary>
public class CodeCoverageTests
{
    private const string Source = "coverage.js";

    private static Engine CreateEngine(CoverageGranularity granularity = CoverageGranularity.Statements)
        => new(options =>
        {
            options.Coverage.Enabled = true;
            options.Coverage.Granularity = granularity;
        });

    /// <summary>The exact source text an entry spans, which makes an expectation readable.</summary>
    private static string Text(string code, CoverageEntry entry)
        => code.Substring(entry.Start.Index, entry.End.Index - entry.Start.Index);

    private static IReadOnlyList<CoverageEntry> EntriesOf(CoverageReport report, string source = Source)
        => report.Sources.Single(s => s.Name == source).Entries;

    private static List<(string Text, CoverageEntryKind Kind, long Hits)> Rows(string code, CoverageReport report)
        => EntriesOf(report).Select(e => (Text(code, e), e.Kind, e.HitCount)).ToList();

    // ---- what a hit count means ----

    [Test]
    public void EveryExecutedStatementIsCountedOnce()
    {
        const string Code = "var a = 1; var b = 2; a + b;";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Equal(
            ("var a = 1;", CoverageEntryKind.Statement, 1L),
            ("var b = 2;", CoverageEntryKind.Statement, 1L),
            ("a + b;", CoverageEntryKind.Statement, 1L));
    }

    [Test]
    public void ALoopBodyCountsOncePerIteration()
    {
        const string Code = "var total = 0; for (var i = 0; i < 3; i++) { total += i; }";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        // the loop statement is entered once, its body statement once per iteration
        rows.Should().Contain(("for (var i = 0; i < 3; i++) { total += i; }", CoverageEntryKind.Statement, 1L));
        rows.Should().Contain(("total += i;", CoverageEntryKind.Statement, 3L));
    }

    /// <summary>
    /// <c>for</c>, <c>while</c> and <c>do..while</c> each have their own tight-loop lane, which runs a
    /// structurally-simple body with the per-statement ceremony skipped entirely — including the counting.
    /// Enabling coverage has to disarm all three, so all three are pinned.
    /// </summary>
    [TestCase("for (var i = 0; i < 3; i++) { total += 1; }")]
    [TestCase("for (var i = 0; i < 3; i++) total += 1;")]
    [TestCase("var i = 0; while (i++ < 3) { total += 1; }")]
    [TestCase("var i = 0; do { total += 1; } while (++i < 3);")]
    public void EveryLoopShapeCountsItsBodyPerIteration(string loop)
    {
        // inside a function body, so completion values are unobservable and the tight lane is otherwise
        // eligible - running the same loop at script top level takes the generic path and proves nothing
        var code = "var total = 0; function run() { " + loop + " } run();";

        var engine = CreateEngine();
        engine.Execute(code, Source);

        engine.GetValue("total").AsNumber().Should().Be(3);
        Rows(code, engine.Diagnostics.GetCoverage()).Should().Contain(("total += 1;", CoverageEntryKind.Statement, 3L));
    }

    [Test]
    public void OnlyTheBranchThatRanIsReported()
    {
        const string Code = "var x = 1; if (x) { x = 2; } else { x = 3; }";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        rows.Should().Contain(("x = 2;", CoverageEntryKind.Statement, 1L));
        rows.Select(r => r.Text).Should().NotContain("x = 3;");
    }

    [Test]
    public void AFunctionBodyCountsOncePerCall()
    {
        const string Code = "function f(n) { return n + 1; } f(1); f(2);";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        rows.Should().Contain(("{ return n + 1; }", CoverageEntryKind.Function, 2L));
        rows.Should().Contain(("return n + 1;", CoverageEntryKind.Statement, 2L));
    }

    /// <summary>
    /// The static analysis pass pre-resolves <c>return &lt;literal&gt;;</c> to its value, and the statement list
    /// then hands that value over WITHOUT executing the statement. Debug mode already forced the real execution
    /// so the debugger could step onto it; coverage needs the same, or a body of nothing but a literal return
    /// would report its function as called and its only statement as never run.
    /// </summary>
    [Test]
    public void APreResolvedLiteralReturnIsStillCounted()
    {
        const string Code = "function f() { return 42; } f(); f();";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Contain(("return 42;", CoverageEntryKind.Statement, 2L));
    }

    [Test]
    public void AConciseArrowBodyIsReportedAsAFunctionEntry()
    {
        const string Code = "var double = x => x * 2; double(1); double(2); double(3);";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Contain(("x * 2", CoverageEntryKind.Function, 3L));
    }

    /// <summary>
    /// A generator body is genuinely re-entered by every resumption, and the counter says so. Pinned because it
    /// is the one place a hit count is not "how many times was this called".
    /// </summary>
    [Test]
    public void AGeneratorBodyCountsOncePerResumption()
    {
        const string Code = "function* g() { yield 1; yield 2; } var it = g(); it.next(); it.next(); it.next();";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        // g() itself does not run the body; the three next() calls each enter it
        rows.Should().Contain(("{ yield 1; yield 2; }", CoverageEntryKind.Function, 3L));
    }

    /// <summary>
    /// An async body is re-entered on every resumption after an await, so its Function entry counts them; the
    /// statements themselves are not re-counted, because execution picks up where it suspended.
    /// </summary>
    [Test]
    public void AnAsyncBodyCountsItsResumptions()
    {
        const string Code = "async function f() { var a = 1; await 0; var b = 2; } f();";

        var engine = CreateEngine();
        engine.Execute(Code, Source);
        engine.Tasks.ProcessTasks();

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        rows.Should().Contain(("{ var a = 1; await 0; var b = 2; }", CoverageEntryKind.Function, 2L));
        rows.Should().Contain(("var a = 1;", CoverageEntryKind.Statement, 1L));
        rows.Should().Contain(("var b = 2;", CoverageEntryKind.Statement, 1L));
    }

    [Test]
    public void EvaluatedCodeIsCountedUnderItsOwnSourceName()
    {
        var engine = CreateEngine();
        engine.Execute("eval('var a = 1; var b = 2;');", Source);

        var report = engine.Diagnostics.GetCoverage();

        // the eval text is a source of its own; what matters is that its statements were counted somewhere
        report.Sources.SelectMany(s => s.Entries).Should().HaveCountGreaterThan(1);
        report.Sources.SelectMany(s => s.Entries).Should().AllSatisfy(e => e.HitCount.Should().Be(1));
    }

    [Test]
    public void ModuleStatementsAreCountedUnderTheModuleLocation()
    {
        var engine = CreateEngine();
        engine.Modules.Add("lib", "export const value = 1; export function twice(n) { return n * 2; }");
        engine.Modules.Add("main", "import { twice } from 'lib'; twice(1); twice(2);");

        engine.Modules.Import("main");

        var report = engine.Diagnostics.GetCoverage();

        report.Sources.Select(s => s.Name).Should().Contain("lib");
        var libEntries = report.Sources.Single(s => s.Name == "lib").Entries;
        libEntries.Should().Contain(e => e.Kind == CoverageEntryKind.Function && e.HitCount == 2);
    }

    // ---- what is not reported ----

    [Test]
    public void BlockStatementsAreNotReported()
    {
        const string Code = "var a = 0; { a = 1; a = 2; }";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        rows.Select(r => r.Text).Should().NotContain("{ a = 1; a = 2; }");
        rows.Should().Contain(("a = 1;", CoverageEntryKind.Statement, 1L));
        rows.Should().Contain(("a = 2;", CoverageEntryKind.Statement, 1L));
    }

    /// <summary>
    /// A one-statement block runs through a different internal lane than a multi-statement one (the block node
    /// is never entered), which is exactly why blocks are excluded from the report: the statements inside are
    /// reported identically either way.
    /// </summary>
    [Test]
    public void ASingleStatementBlockReportsItsContentsLikeAnyOther()
    {
        const string Code = "var a = 0; if (true) { a = 1; }";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        rows.Select(r => r.Text).Should().NotContain("{ a = 1; }");
        rows.Should().Contain(("a = 1;", CoverageEntryKind.Statement, 1L));
    }

    // ---- granularity ----

    [Test]
    public void FunctionGranularityReportsFunctionBodiesAndNothingElse()
    {
        const string Code = "function f() { var x = 1; return x; } f(); f();";

        var engine = CreateEngine(CoverageGranularity.Functions);
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Equal(
            ("{ var x = 1; return x; }", CoverageEntryKind.Function, 2L));
    }

    [Test]
    public void StatementGranularityIsTheDefault()
    {
        var options = new Options();
        options.Coverage.Granularity.Should().Be(CoverageGranularity.Statements);
        options.Coverage.Enabled.Should().BeFalse();
    }

    // ---- reset and readout ----

    [Test]
    public void ResetCoverageDropsEveryCount()
    {
        const string Code = "var a = 1;";

        var engine = CreateEngine();
        engine.Execute(Code, Source);
        engine.Diagnostics.GetCoverage().Sources.Should().NotBeEmpty();

        engine.Diagnostics.ResetCoverage();
        engine.Diagnostics.GetCoverage().Sources.Should().BeEmpty();

        engine.Execute(Code, Source);
        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Equal(("var a = 1;", CoverageEntryKind.Statement, 1L));
    }

    /// <summary>
    /// The counters key on AST node identity, and <c>Execute(string)</c> re-parses, so the same construct is a
    /// different node on every call. The report folds those together by position — otherwise a host that does
    /// not cache a <c>Prepared&lt;Script&gt;</c> would read "ran once" ten times over instead of "ran ten
    /// times".
    /// </summary>
    [Test]
    public void ReParsingTheSameSourceAddsToTheSameEntry()
    {
        const string Code = "function f() { return 1; } f(); f();";

        var engine = CreateEngine();
        engine.Execute(Code, Source);
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        rows.Count(r => r.Text == "return 1;").Should().Be(1);
        rows.Should().Contain(("return 1;", CoverageEntryKind.Statement, 4L));
        rows.Should().Contain(("{ return 1; }", CoverageEntryKind.Function, 4L));
    }

    [Test]
    public void SourcesAreKeyedByTheNameTheCodeWasParsedUnder()
    {
        var engine = CreateEngine();
        engine.Execute("var a = 1;", "b.js");
        engine.Execute("var b = 2;", "a.js");

        var report = engine.Diagnostics.GetCoverage();

        report.Sources.Select(s => s.Name).Should().Equal("a.js", "b.js");
    }

    [Test]
    public void EntriesAreOrderedBySourcePosition()
    {
        const string Code = "var a = 1;\nvar b = 2;\nvar c = 3;";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var entries = EntriesOf(engine.Diagnostics.GetCoverage());

        entries.Select(e => e.Start.Line).Should().Equal(1, 2, 3);
        entries.Select(e => e.Start.Index).Should().BeInAscendingOrder();
        entries.Select(e => e.Start.Column).Should().AllSatisfy(c => c.Should().Be(0));
    }

    // ---- the off switch ----

    [Test]
    public void ReadingCoverageFromAnEngineThatDoesNotCollectItThrows()
    {
        var engine = new Engine();
        engine.Execute("var a = 1;");

        Invoking(() => engine.Diagnostics.GetCoverage()).Should().Throw<System.InvalidOperationException>();
        Invoking(() => engine.Diagnostics.ResetCoverage()).Should().Throw<System.InvalidOperationException>();
    }

    // ---- the interpreter lane ----

    /// <summary>
    /// The design claim in one assertion: the counters are reached from the per-statement lane, so enabling
    /// coverage has to arm that lane — and an engine that did not enable it must be left exactly as it was,
    /// because that lane not being armed is what makes the off path free rather than merely cheap.
    /// </summary>
    [Test]
    public void CoverageArmsThePerStatementLaneAndNothingElseDoes()
    {
        new EvaluationContext(new Engine()).ShouldRunPerStatementChecks.Should().BeFalse();
        new EvaluationContext(new Engine()).BypassStatementFastPaths.Should().BeFalse();

        var covering = CreateEngine();
        new EvaluationContext(covering).ShouldRunPerStatementChecks.Should().BeTrue();
        new EvaluationContext(covering).BypassStatementFastPaths.Should().BeTrue();

        // the Functions granularity rides the same lane; it filters the report, not the execution
        var functions = CreateEngine(CoverageGranularity.Functions);
        new EvaluationContext(functions).ShouldRunPerStatementChecks.Should().BeTrue();
    }

    /// <summary>
    /// A lone <c>MaxStatements</c> constraint normally keeps the per-statement lane DISARMED — it is charged
    /// inline instead, which is what lets the tight-loop lanes stay armed under a statement budget. Coverage
    /// has to take that shortcut away, or an engine configured with both would silently collect nothing.
    /// </summary>
    [Test]
    public void CoverageIsStillCollectedUnderALoneStatementLimit()
    {
        const string Code = "var a = 1; var b = 2;";

        var engine = new Engine(options =>
        {
            options.LimitStatements(1000);
            options.Coverage.Enabled = true;
        });
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Equal(
            ("var a = 1;", CoverageEntryKind.Statement, 1L),
            ("var b = 2;", CoverageEntryKind.Statement, 1L));
    }

    /// <summary>
    /// The other half of the same interaction: taking that shortcut away moves the counter from its inline
    /// lane into the exact-constraint walk, and both routes must charge exactly the same statements — or
    /// turning coverage on would move where the limit fires.
    /// </summary>
    [Test]
    public void AStatementLimitFiresAtTheSameStatementWithCoverageOnAndOff()
    {
        const string Code = "var n = 0; for (var i = 0; i < 1000; i++) { n += i; }";

        static int RunToLimit(bool coverage)
        {
            var engine = new Engine(options =>
            {
                options.LimitStatements(50);
                options.Coverage.Enabled = coverage;
            });

            Assert.Throws<Jint.Runtime.StatementsCountOverflowException>(() => engine.Execute(Code));

            // how far the loop got before the limit hit is the observable proof both routes charge alike
            return (int) engine.GetValue("n").AsNumber();
        }

        RunToLimit(coverage: true).Should().Be(RunToLimit(coverage: false));
    }

    // ---- the critical invariant: a shared AST carries no counts ----

    /// <summary>
    /// A <c>Prepared&lt;Script&gt;</c> is documented as shareable across engines, so the counters may not live
    /// on anything the AST reaches. They live in a per-engine dictionary keyed on node identity, and this is
    /// the test that would fail if that ever became a field on a handler node: <c>ConstantStatement</c> — the
    /// handler for <c>return &lt;literal&gt;;</c> — is stored on AST <c>UserData</c> and is therefore ONE
    /// instance shared by every engine running the script.
    /// </summary>
    [Test]
    public void TwoEnginesSharingOnePreparedScriptCountIndependently()
    {
        const string Code = "function f() { return 7; } f();";
        var prepared = Engine.PrepareScript(Code, Source);

        var counting = CreateEngine();
        var notCounting = new Engine();

        counting.Execute(prepared);
        for (var i = 0; i < 5; i++)
        {
            notCounting.Execute(prepared);
        }
        counting.Execute(prepared);

        // the counting engine sees its own two runs, never the other engine's five
        var rows = Rows(Code, counting.Diagnostics.GetCoverage());
        rows.Should().Contain(("return 7;", CoverageEntryKind.Statement, 2L));
        rows.Should().Contain(("{ return 7; }", CoverageEntryKind.Function, 2L));

        // and a third engine starting now sees only what it runs itself
        var third = CreateEngine();
        third.Execute(prepared);
        Rows(Code, third.Diagnostics.GetCoverage()).Should().Contain(("return 7;", CoverageEntryKind.Statement, 1L));
    }

    /// <summary>
    /// The premise behind the test above, asserted directly rather than inferred. The static analysis pass
    /// parks a <c>ConstantStatement</c> handler on the AST node's <c>UserData</c>, so that ONE handler instance
    /// is what every engine running the prepared script executes. A hit-count field on a handler node would
    /// therefore be per-engine for most nodes and shared for these — the worst of both — which is why the
    /// counters are keyed engine-side on node identity instead.
    /// </summary>
    [Test]
    public void TheHandlerForAPreResolvedReturnIsParkedOnTheSharedAst()
    {
        var prepared = Engine.PrepareScript("function f() { return 7; }", Source);

        var returnStatement = Descendants(prepared.Program!).OfType<ReturnStatement>().Single();

        returnStatement.UserData.Should().BeAssignableTo<JintStatement>(
            "the handler for a pre-resolved literal return is shared through the AST, not owned by an engine");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.ChildNodes)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// The same invariant from the other side: nothing the engine wrote while collecting coverage is reachable
    /// through the shared AST, so a second engine's report is a function of its own executions alone even when
    /// the first engine ran the script a different number of times.
    /// </summary>
    [Test]
    public void ASharedPreparedScriptIsUnchangedByHavingBeenCovered()
    {
        const string Code = "var a = 0; while (a < 3) { a++; }";
        var prepared = Engine.PrepareScript(Code, Source);

        var first = CreateEngine();
        first.Execute(prepared);
        first.Execute(prepared);

        var second = CreateEngine();
        second.Execute(prepared);

        Rows(Code, first.Diagnostics.GetCoverage()).Should().Contain(("a++;", CoverageEntryKind.Statement, 6L));
        Rows(Code, second.Diagnostics.GetCoverage()).Should().Contain(("a++;", CoverageEntryKind.Statement, 3L));
    }
}
