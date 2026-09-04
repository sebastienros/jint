using System.Text;
using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// Holds <c>Wpt/README.md</c>'s cause table and <see cref="WptBrowserExclusions.Causes"/> to each other.
/// </summary>
/// <remarks>
/// <para>
/// Two of these cost nothing and always run: a cause's name has to be unique and a row has to name one that
/// exists. The third totals the table, so it runs the whole lane and is opt-in behind the census's own
/// variable — one switch for both tables, because they are two readings of one run.
/// </para>
/// <para>
/// <b>Every column here is an equality in both directions.</b> A cause that grew and a cause that shrank are
/// equally a table that has stopped describing this browser, which is what makes this different from the
/// census's <c>Not passing</c> ceiling: that one bounds how much fails, and this one says what each failure
/// <i>is</i>.
/// </para>
/// </remarks>
public class WptBrowserCauseTests
{
    /// <summary>
    /// A cause name is a key, so two causes may not share one and a row may not name a cause that is not there.
    /// </summary>
    /// <remarks>
    /// Free, because it reads two tables and runs nothing. It is also the check that catches the shape of
    /// mistake this whole mechanism exists for: a group renamed in the code and not in the README would
    /// otherwise leave a row silently counting nothing at all, which is exactly how the figures rotted before
    /// anything counted them.
    /// </remarks>
    [Test]
    public void EveryRowNamesACauseAndEveryCauseIsNamedOnce()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cause in WptBrowserExclusions.Causes)
        {
            if (!seen.Add(cause.Name))
            {
                problems.Add($"two causes are called \"{cause.Name}\"; a name is what a README row keys on");
            }

            if (cause.Exclusions.Length == 0)
            {
                problems.Add($"the cause \"{cause.Name}\" holds no exclusion, so nothing is it");
            }
        }

        var rows = WptBrowserCauses.ParseRows(WptBrowserCauses.ReadmeTable());
        rows.Should().NotBeEmpty("the README's cause table is what this test is about");

        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.Cause.Length == 0)
            {
                problems.Add($"a row of the cause table names no cause: {Excerpt(row.Prose)}");
                continue;
            }

            if (!seen.Contains(row.Cause))
            {
                problems.Add($"the row \"{row.Cause}\" names a cause WptBrowserExclusions.Causes does not have");
            }

            if (!claimed.Add(row.Cause))
            {
                problems.Add($"two rows name the cause \"{row.Cause}\"");
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty();

        // The flattened array is what the runner enforces against, so the split into causes may not lose or
        // reorder a single row of it.
        var flattened = new List<WptExclusion>();
        foreach (var cause in WptBrowserExclusions.Causes)
        {
            flattened.AddRange(cause.Exclusions);
        }

        WptBrowserExclusions.All.Should().Equal(flattened, "All is the causes flattened, in order");
    }

    /// <summary>
    /// The two columns, and the order, are what a run says they are — and every failing test belongs to
    /// exactly one cause.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The partition is the load-bearing half.</b> Without it a figure is an arbitrary sum: a test two
    /// causes both claim would be counted twice, and one no cause claims would be counted nowhere while the
    /// table still added up to something plausible. With it, the column totals what the lane reported.
    /// </para>
    /// <para>
    /// Opt-in and Windows-only for the census's reasons: totalling means running every document, and a
    /// <c>TIMEOUT</c> is an outcome a loaded machine can produce on its own.
    /// </para>
    /// </remarks>
    [Test]
    public async Task TheCauseTableMatchesWhatTheLaneMeasures()
    {
        if (!WptBrowserCensus.CensusRequested())
        {
            Assert.Ignore(
                $"Set {WptBrowserCensus.UpdateVariable}=1 to check the cause table, or =update to rewrite its two columns.");
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The cause table is measured on Windows, like the census it is rendered beside.");
        }

        await WptBrowserCensus.MeasureAsync().ConfigureAwait(false);

        var measured = WptBrowserCauses.Measure();
        var problems = new List<string>();

        // Every failing test in the whole lane is claimed by exactly one cause. The runner already refuses a
        // failing test no exclusion names; what this adds is that no two causes name the same one, which is
        // the difference between a sum and a partition.
        var byPath = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (path, test) in WptBrowserCauses.Failures())
        {
            if (!byPath.TryGetValue(path, out var tests))
            {
                tests = [];
                byPath[path] = tests;
            }

            tests.Add(test);
        }

        var owners = new Dictionary<string, string?[]>(StringComparer.Ordinal);

        foreach (var (path, tests) in byPath)
        {
            owners[path] = new string?[tests.Count];
        }

        foreach (var cause in WptBrowserExclusions.Causes)
        {
            foreach (var exclusion in cause.Exclusions)
            {
                if (!byPath.TryGetValue(exclusion.File, out var tests))
                {
                    continue;
                }

                var claimants = owners[exclusion.File];

                for (var i = 0; i < tests.Count; i++)
                {
                    if (!exclusion.Matches(tests[i]))
                    {
                        continue;
                    }

                    if (claimants[i] is { } already && !string.Equals(already, cause.Name, StringComparison.Ordinal))
                    {
                        problems.Add(
                            $"{exclusion.File}: \"{tests[i]}\" is claimed by both \"{already}\" and \"{cause.Name}\"");
                        continue;
                    }

                    claimants[i] = cause.Name;
                }
            }
        }

        foreach (var (path, claimants) in owners)
        {
            for (var i = 0; i < claimants.Length; i++)
            {
                if (claimants[i] is null)
                {
                    problems.Add($"{path}: \"{byPath[path][i]}\" fails and no cause claims it");
                }
            }
        }

        // A cause that touches the six suites this section is about must be in the table, and one in the
        // table must touch them: a cause outside them is a real cause and simply not what this counts.
        var rows = WptBrowserCauses.ParseRows(WptBrowserCauses.ReadmeTable());

        problems.AddRange(WptBrowserCauses.Reconcile(rows, measured, WptBrowserExclusions.Causes));

        string.Join(Environment.NewLine, problems).Should().BeEmpty();

        // The sentence above the table carries the same three figures the census table does, and it is
        // typed. It is checked here rather than rendered, because generating a sentence is a worse job than
        // generating a table — and it is checked at all because one of its figures went stale within a day
        // of the table below it being made generated.
        if (WptBrowserCauses.StatedTotals() is { } sentence)
        {
            var totals = WptBrowserCensus.MeasuredTotals(WptBrowserCauses.Suites);

            if (sentence != totals)
            {
                problems.Add(
                    $"the sentence above the cause table says {sentence.Documents} documents, {sentence.Tests} tests "
                    + $"and {sentence.NotPassing} not passing, where the census measured {totals.Documents}, "
                    + $"{totals.Tests} and {totals.NotPassing}");
            }
        }
        else
        {
            problems.Add("the sentence above the cause table no longer states three figures to check");
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty();

        var rendered = WptBrowserCauses.Render(rows, measured);
        var claimedTable = WptBrowserCauses.ReadmeTable();

        if (string.Equals(rendered, claimedTable, StringComparison.Ordinal))
        {
            return;
        }

        if (WptBrowserCensus.UpdateRequested())
        {
            var path = WptBrowserCauses.WriteReadmeTable(rendered);
            Assert.Fail($"Rewrote the cause table in {path}. Review the diff and run again.");
        }

        Assert.Fail(Difference(rows, measured));
    }

    // -----------------------------------------------------------------------------------------------
    // Self-tests. The reconciler and the renderer are pure, so these hold them to a table nobody has to run
    // the lane to build — which is the only way the interesting cases get covered at all, since a cause
    // going spent happens once every few months and always on somebody else's branch.
    // -----------------------------------------------------------------------------------------------

    private static WptBrowserCauses.Row Row(int tests, int documents, string prose, string cause)
        => new(tests, documents, prose + " <!-- cause: " + cause + " -->", cause);

    private static WptCause Cause(string name) => new(name, [new("a/b.html", "*", WptDivergence.NeedsTriage)]);

    /// <summary>
    /// A cause whose last failing test a fix retired is <b>reported</b>, not re-derived to zero and not
    /// quietly deleted.
    /// </summary>
    /// <remarks>
    /// This is the case the ARIA mixin reached the day after the table was made generated: its row still read
    /// 65 while the fix had taken every one of them. A generator that emitted <c>| 0 | 0 |</c> would leave a
    /// table of causes with a cause that causes nothing, and one that dropped the row would throw away a
    /// paragraph a person wrote — so it does neither and says which row to delete.
    /// </remarks>
    [Test]
    public void ACauseWhoseRowsAreAllFixedIsReportedRatherThanRenderedAsZero()
    {
        var stated = new List<WptBrowserCauses.Row> { Row(65, 3, "**The ARIA mixin is not there.**", "the ARIA mixin") };
        var measured = new Dictionary<string, WptBrowserCauses.Counts>(StringComparer.Ordinal)
        {
            ["the ARIA mixin"] = new(0, 0, 0),
        };

        var problems = WptBrowserCauses.Reconcile(stated, measured, [Cause("the ARIA mixin")]);

        problems.Should().ContainSingle().Which.Should().Contain("spent").And.Contain("Delete the row");
    }

    /// <summary>A cause the run finds and the table does not name is reported from the other side.</summary>
    [Test]
    public void ACauseTheTableDoesNotNameIsReported()
    {
        var measured = new Dictionary<string, WptBrowserCauses.Counts>(StringComparer.Ordinal)
        {
            ["a new defect"] = new(12, 2, 12),
        };

        var problems = WptBrowserCauses.Reconcile([], measured, [Cause("a new defect")]);

        problems.Should().ContainSingle().Which.Should().Contain("12 failing test(s)").And.Contain("no row");
    }

    /// <summary>
    /// A table that agrees with the run reports nothing, which is what makes the two above mean something.
    /// </summary>
    [Test]
    public void ATableTheRunAgreesWithIsNotADifference()
    {
        var stated = new List<WptBrowserCauses.Row> { Row(12, 2, "**Something.**", "a cause") };
        var measured = new Dictionary<string, WptBrowserCauses.Counts>(StringComparer.Ordinal)
        {
            ["a cause"] = new(12, 2, 12),
        };

        WptBrowserCauses.Reconcile(stated, measured, [Cause("a cause")]).Should().BeEmpty();
    }

    /// <summary>
    /// The renderer writes the measured figures and the order they imply, and copies the prose through
    /// untouched — including the key, which is what the next run reads the row back by.
    /// </summary>
    [Test]
    public void TheRendererWritesTheFiguresAndKeepsTheProse()
    {
        var stated = new List<WptBrowserCauses.Row>
        {
            Row(1, 1, "**Small.**", "small"),
            Row(2, 1, "**Large.**", "large"),
        };

        var measured = new Dictionary<string, WptBrowserCauses.Counts>(StringComparer.Ordinal)
        {
            ["small"] = new(7, 3, 7),
            ["large"] = new(1_234, 9, 1_234),
        };

        var rendered = WptBrowserCauses.Render(stated, measured);

        // Descending by tests, so the row the README had second comes first; the thousands separator is the
        // census's own formatting; and both prose cells arrive verbatim, key and all.
        rendered.Should().Be(
            "| Tests | Documents | What it is |\n"
            + "| ---: | ---: | --- |\n"
            + "| 1,234 | 9 | **Large.** <!-- cause: large --> |\n"
            + "| 7 | 3 | **Small.** <!-- cause: small --> |\n");
    }

    /// <summary>A row that names no cause is caught for free, because the key is what joins the two tables.</summary>
    [Test]
    public void ARowWithNoKeyNamesNoCause()
    {
        WptBrowserCauses.CauseOf("| 5 | 1 | **Prose with no key.** |").Should().BeEmpty();
        WptBrowserCauses.CauseOf("**Prose.** <!-- cause: the name -->").Should().Be("the name");
    }

    /// <summary>What moved, cause by cause, so a failure names the rows rather than the whole table.</summary>
    private static string Difference(IReadOnlyList<WptBrowserCauses.Row> stated, IReadOnlyDictionary<string, WptBrowserCauses.Counts> measured)
    {
        var message = new StringBuilder();
        message.Append("Wpt/README.md's cause table no longer says what a run says. Its two columns are derived, ")
            .Append("so the fix is to run it again rather than to edit it:").Append(Environment.NewLine)
            .Append(Environment.NewLine)
            .Append("    ").Append(WptBrowserCensus.UpdateVariable).Append("=update dotnet test Jint.Tests.Browser -c Release")
            .Append(Environment.NewLine).Append(Environment.NewLine);

        foreach (var row in stated)
        {
            var counts = measured.TryGetValue(row.Cause, out var found) ? found : default;
            if (counts.Tests == row.Tests && counts.Documents == row.Documents)
            {
                continue;
            }

            message.Append("    ").Append(row.Cause)
                .Append(": ").Append(row.Tests).Append(" | ").Append(row.Documents)
                .Append(" -> ").Append(counts.Tests).Append(" | ").Append(counts.Documents)
                .Append(Environment.NewLine);
        }

        return message.ToString();
    }

    private static string Excerpt(string prose) => prose.Length <= 60 ? prose : prose[..60] + "…";
}
