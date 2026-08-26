#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Runtime.Coverage;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers script code coverage from where an integrator stands. This project has no
/// <c>InternalsVisibleTo</c>, so everything here compiling at all is the guarantee that the option, the readout
/// and the report types are reachable by a third party — a coverage tool, a CI gate, a test harness embedding
/// Jint — and not just from inside the assembly.
/// </summary>
public class CodeCoverageTests
{
    private const string Source = "host.js";

    private static Engine CreateEngine(CoverageGranularity? granularity = null) => new(options =>
    {
        options.Coverage.Enabled = true;
        if (granularity is not null)
        {
            options.Coverage.Granularity = granularity.Value;
        }
    });

    private static string Text(string code, CoverageEntry entry)
        => code.Substring(entry.Start.Index, entry.End.Index - entry.Start.Index);

    private static List<(string Text, CoverageEntryKind Kind, long Hits)> Rows(string code, CoverageReport report)
        => report.Sources
            .Single(s => s.Name == Source).Entries
            .Select(e => (Text(code, e), e.Kind, e.HitCount))
            .ToList();

    // ---- reachability ----

    [Test]
    public void TheOptionAndTheReadoutAreReachableFromOutsideTheJintAssembly()
    {
        var engine = new Engine(options =>
        {
            options.Coverage.Enabled = true;
            options.Coverage.Granularity = CoverageGranularity.Statements;
        });

        CoverageReport report = engine.Diagnostics.GetCoverage();
        report.Sources.Should().BeEmpty();

        engine.Diagnostics.ResetCoverage();
    }

    [Test]
    public void ADefaultEngineCollectsNothingAndSaysSo()
    {
        var engine = new Engine();
        engine.Execute("var a = 1;");

        Invoking(() => engine.Diagnostics.GetCoverage()).Should().Throw<InvalidOperationException>();
        Invoking(() => engine.Diagnostics.ResetCoverage()).Should().Throw<InvalidOperationException>();
    }

    // ---- the report a host actually consumes ----

    [Test]
    public void ARunReportsEveryStatementItExecutedAndHowOftenItRanIt()
    {
        const string Code =
            "function classify(n) {\n" +
            "    if (n > 0) { return 'positive'; }\n" +
            "    return 'other';\n" +
            "}\n" +
            "for (var i = -1; i < 2; i++) { classify(i); }";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var rows = Rows(Code, engine.Diagnostics.GetCoverage());

        // three calls, of which one takes the early return
        rows.Should().Contain(("classify(i);", CoverageEntryKind.Statement, 3L));
        rows.Should().Contain(("if (n > 0) { return 'positive'; }", CoverageEntryKind.Statement, 3L));
        rows.Should().Contain(("return 'positive';", CoverageEntryKind.Statement, 1L));
        rows.Should().Contain(("return 'other';", CoverageEntryKind.Statement, 2L));

        // the loop header runs once, its body statement once per iteration
        rows.Should().Contain(("for (var i = -1; i < 2; i++) { classify(i); }", CoverageEntryKind.Statement, 1L));

        // the sole function body is entered once per call; blocks are not reported, so it is the only
        // Function entry even though the script contains three block statements
        var functionEntries = rows.Where(r => r.Kind == CoverageEntryKind.Function).ToList();
        functionEntries.Should().ContainSingle();
        functionEntries[0].Hits.Should().Be(3);
    }

    [Test]
    public void APositionCarriesLineColumnAndIndex()
    {
        const string Code = "var a = 1;\nvar b = 2;";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        var entries = engine.Diagnostics.GetCoverage().Sources.Single().Entries;

        entries.Should().HaveCount(2);

        entries[0].Start.Should().Be(new CoveragePosition(Line: 1, Column: 0, Index: 0));
        entries[0].End.Line.Should().Be(1);
        entries[0].HitCount.Should().Be(1);

        entries[1].Start.Line.Should().Be(2);
        entries[1].Start.Column.Should().Be(0);
        entries[1].Start.Index.Should().Be(11);
    }

    [Test]
    public void ANeverExecutedStatementHasNoEntry()
    {
        const string Code = "if (false) { var unreachable = 1; }";

        var engine = CreateEngine();
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage())
            .Select(r => r.Text)
            .Should().NotContain("var unreachable = 1;");
    }

    [Test]
    public void FunctionGranularityReportsFunctionBodiesOnly()
    {
        const string Code = "function f() { var x = 1; return x; } f(); f(); f();";

        var engine = CreateEngine(CoverageGranularity.Functions);
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Equal(
            ("{ var x = 1; return x; }", CoverageEntryKind.Function, 3L));
    }

    [Test]
    public void ResetStartsAFreshMeasurement()
    {
        const string Code = "var a = 1;";

        var engine = CreateEngine();
        engine.Execute(Code, Source);
        engine.Diagnostics.ResetCoverage();
        engine.Execute(Code, Source);
        engine.Execute(Code, Source);

        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Equal(("var a = 1;", CoverageEntryKind.Statement, 2L));
    }

    [Test]
    public void EachSourceIsReportedUnderTheNameItWasParsedWith()
    {
        var engine = CreateEngine();
        engine.Execute("var a = 1;", "alpha.js");
        engine.Execute("var b = 2;", "beta.js");
        engine.Execute("var c = 3;");

        engine.Diagnostics.GetCoverage().Sources.Select(s => s.Name)
            .Should().Equal("<anonymous>", "alpha.js", "beta.js");
    }

    // ---- engine isolation, which is what makes the feature usable at all ----

    /// <summary>
    /// <see cref="Options"/> is documented as shareable across engines, so the coverage option has to be too:
    /// each engine gets its own counters and one engine's report never contains another's executions.
    /// </summary>
    [Test]
    public void OneOptionsInstanceGivesEveryEngineItsOwnCounters()
    {
        const string Code = "var a = 1;";

        var options = new Options();
        options.Coverage.Enabled = true;

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute(Code, Source);
        first.Execute(Code, Source);
        second.Execute(Code, Source);

        Rows(Code, first.Diagnostics.GetCoverage()).Should().Equal(("var a = 1;", CoverageEntryKind.Statement, 2L));
        Rows(Code, second.Diagnostics.GetCoverage()).Should().Equal(("var a = 1;", CoverageEntryKind.Statement, 1L));
    }

    /// <summary>
    /// The invariant a host relies on when it caches a <c>Prepared&lt;Script&gt;</c> and runs it on a pool of
    /// engines: the prepared AST is shared, the counts are not. One engine collecting coverage must not see the
    /// executions of the engines that are not, and must not disturb them either.
    /// </summary>
    [Test]
    public void APreparedScriptSharedAcrossEnginesKeepsItsCountsPerEngine()
    {
        const string Code = "function add(a, b) { return a + b; } add(1, 2);";
        var prepared = Engine.PrepareScript(Code, Source);

        var covering = CreateEngine();
        var plain = new Engine();

        for (var i = 0; i < 10; i++)
        {
            plain.Execute(prepared);
        }

        covering.Execute(prepared);
        covering.Execute(prepared);

        // the plain engine's ten runs left nothing behind for the covering one to find
        var rows = Rows(Code, covering.Diagnostics.GetCoverage());
        rows.Should().Contain(("return a + b;", CoverageEntryKind.Statement, 2L));
        rows.Should().Contain(("{ return a + b; }", CoverageEntryKind.Function, 2L));

        // and the plain engine still runs the shared script correctly afterwards
        plain.Evaluate("add(2, 3)").AsNumber().Should().Be(5);
    }

    /// <summary>
    /// Coverage rides the same per-statement lane the execution constraints use, so the two have to keep
    /// working together: a budgeted run still gets counted, and the budget still applies.
    /// </summary>
    [Test]
    public void CoverageAndExecutionConstraintsCoexist()
    {
        var engine = new Engine(options =>
        {
            options.Coverage.Enabled = true;
            options.LimitStatements(10_000);
            options.LimitExecutionTime(TimeSpan.FromSeconds(10));
        });

        const string Code = "var n = 0; for (var i = 0; i < 5; i++) { n += i; }";
        engine.Execute(Code, Source);

        engine.GetValue("n").AsNumber().Should().Be(10);
        Rows(Code, engine.Diagnostics.GetCoverage()).Should().Contain(("n += i;", CoverageEntryKind.Statement, 5L));
    }
}
