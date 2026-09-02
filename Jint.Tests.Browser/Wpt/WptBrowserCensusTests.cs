namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// Holds <c>Wpt/README.md</c>'s inventory — "The lane, suite by suite" — to what the lane actually is, so the
/// table is a derived artifact rather than prose somebody keeps up to date by hand.
/// </summary>
/// <remarks>
/// <para>
/// Two tests, split by what each column of that table costs to know. The suites, their vendored document
/// counts and their synthesized wrapper counts are read straight off the embedded corpus and are checked
/// always. The test and not-passing totals need the documents to have run, so they are checked once the lane
/// has reported — see <see cref="WptBrowserCensus"/> for how that is paid for.
/// </para>
/// <para>
/// Neither test touches the driver's rules. The exclusion table still decides what is forgiven, and a failing
/// test that no entry names still fails its own suite in <see cref="WptBrowserTestRunner"/>, whatever these
/// say.
/// </para>
/// </remarks>
public class WptBrowserCensusTests
{
    /// <summary>
    /// The table must name every suite the lane runs, and its <c>Documents</c> and <c>Synthesized</c> columns
    /// must be what the corpus actually holds.
    /// </summary>
    [Test]
    public void TheTableNamesEverySuiteAndCountsItsDocuments()
    {
        var lines = WptBrowserCensus.ReadReadme().Split('\n');
        var stated = WptBrowserCensus.ReadmeTable(lines);
        var derived = WptBrowserCensus.Render(measured: false, lines);

        if (WptBrowserCensus.Reconcile(derived, stated, countsIncluded: false) is { } differences)
        {
            Assert.Fail(differences);
        }
    }

    /// <summary>
    /// The whole table, including the test and not-passing totals, must be what the lane measures right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Opt-in</b>, because totalling it means running every document. It reuses every outcome the theories
    /// already produced — the runner records each one on the way out — so in a full <c>Jint.Tests.Browser</c>
    /// pass most of the lane is already counted by the time this reaches it, and what it cannot reuse it runs
    /// itself, which is what makes it work as a standalone command too.
    /// </para>
    /// <para>
    /// <c>JINT_WPT_BROWSER_CENSUS=1</c> checks the table; <c>=update</c> rewrites it from the run, lowering a
    /// not-passing figure and refusing to raise one. Windows only, in both modes, for the reason the engine
    /// lane's is: a not-passing figure counts outcomes, a <c>TIMEOUT</c> is an outcome a loaded machine can
    /// produce on its own, and the table says in its own first line which platform measured it.
    /// </para>
    /// </remarks>
    [Test]
    public async Task TheTableMatchesWhatTheLaneMeasures()
    {
        if (!WptBrowserCensus.CensusRequested())
        {
            Assert.Ignore(
                $"the census runs every document to total it, so it is opt-in: set {WptBrowserCensus.UpdateVariable}=1 "
                + $"to check the table, or {WptBrowserCensus.UpdateVariable}=update to rewrite it.");
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("the table is measured on Windows, and an outcome column moves with the machine.");
        }

        var updating = WptBrowserCensus.UpdateRequested();
        await WptBrowserCensus.MeasureAsync();

        var measured = WptBrowserCensus.Render(measured: true);
        var stated = WptBrowserCensus.ReadmeTable();

        if (updating)
        {
            if (!WptBrowserCensus.RaiseRequested() && WptBrowserCensus.RefusalToRaise(measured, stated) is { } refusal)
            {
                Assert.Fail(refusal);
            }

            var path = WptBrowserCensus.WriteReadmeTable(measured);
            Assert.Ignore($"{WptBrowserCensus.UpdateVariable} rewrote the table in {path}.");
        }

        if (WptBrowserCensus.Reconcile(measured, stated, countsIncluded: true) is { } differences)
        {
            Assert.Fail(differences);
        }
    }

    /// <summary>
    /// A one-row table plus its total, which is the smallest thing the comparison has an opinion about.
    /// </summary>
    private static string Table(int documents, int synthesized, int tests, int notPassing, string suite = "dom/events") =>
        $"""
         | Suite | Documents | Synthesized | Tests | Not passing |
         | --- | --- | --- | --- | --- |
         | `{suite}/` | {documents} | {synthesized} | {tests} | {notPassing} |
         | **total** | **{documents}** | **{synthesized}** | **{tests}** | **{notPassing}** |

         """;

    [Test]
    public void ATableTheLaneAgreesWithIsNotADifference()
    {
        WptBrowserCensus.Reconcile(Table(55, 9, 700, 140), Table(55, 9, 700, 140), countsIncluded: true).Should().BeNull();
    }

    [Test]
    public void MoreFailingTestsThanTheTableAllowsIsReportedAsARegression()
    {
        // What the message must not do is present a rise as a stale table, because the response to a stale
        // table is to re-census — which would write the worse number in as the new floor.
        var message = WptBrowserCensus.Reconcile(Table(55, 9, 700, 154), Table(55, 9, 700, 140), countsIncluded: true);

        message.Should().NotBeNull();
        message.Should().Contain("fails more than");
        message.Should().Contain("dom/events: not passing 140 -> 154 (+14)", "the suite, the direction and the size");
        message.Should().Contain("ceiling, not a baseline");
        message.Should().NotContain("out of date", "a regression is not the author forgetting to re-census");
    }

    [Test]
    public void FewerFailingTestsThanTheTableStatesIsReportedAsStaleness()
    {
        var message = WptBrowserCensus.Reconcile(Table(55, 9, 700, 126), Table(55, 9, 700, 140), countsIncluded: true);

        message.Should().NotBeNull();
        message.Should().Contain("out of date");
        message.Should().Contain("dom/events: not passing 140 -> 126 (-14)");
        message.Should().Contain($"{WptBrowserCensus.UpdateVariable}=update");
    }

    // One more document than the table names, one more wrapper, one more test. None of the three counts an
    // outcome, so all three are equalities in both directions and a change either way is the lane having moved.
    [TestCase(56, 9, 700, 140)]
    [TestCase(54, 9, 700, 140)]
    [TestCase(55, 10, 700, 140)]
    [TestCase(55, 9, 701, 140)]
    [TestCase(55, 9, 699, 140)]
    public void AColumnThatIsNotAnOutcomeIsHeldExactly(int documents, int synthesized, int tests, int notPassing)
    {
        WptBrowserCensus.Reconcile(Table(documents, synthesized, tests, notPassing), Table(55, 9, 700, 140), countsIncluded: true)
            .Should().NotBeNull();
    }

    [Test]
    public void ASuiteRenamedIsReportedFromBothEnds()
    {
        // A rename is a row appearing and a row disappearing, and a reader needs to see the pair.
        var renamed = WptBrowserCensus.Reconcile(
            Table(55, 9, 700, 140, suite: "dom/eventing"), Table(55, 9, 700, 140), countsIncluded: true);

        renamed.Should().NotBeNull();
        renamed.Should().Contain("dom/eventing: the lane has this suite and the table has no row for it");
        renamed.Should().Contain("dom/events: the table has a row for it and the lane does not");
    }

    [Test]
    public void TheFreeCheckHasNoOpinionOnTheTwoCountedColumns()
    {
        // It has not run the lane, so it knows nothing about either — which is what lets it run on every
        // platform and in a filtered run.
        WptBrowserCensus.Reconcile(Table(55, 9, 0, 0), Table(55, 9, 700, 140), countsIncluded: false).Should().BeNull();
        WptBrowserCensus.Reconcile(Table(56, 9, 0, 0), Table(55, 9, 700, 140), countsIncluded: false).Should().NotBeNull();
    }

    [Test]
    public void TheRewriteRefusesToRaiseTheCeilingAndSaysWhatWouldNotBe()
    {
        // Without this the ceiling is a suggestion: a rise fails the check, the author reaches for the one
        // command the failure mentions, and the worse number becomes the baseline.
        var refusal = WptBrowserCensus.RefusalToRaise(Table(55, 9, 700, 154), Table(55, 9, 700, 140));

        refusal.Should().NotBeNull();
        refusal.Should().Contain("dom/events: not passing 140 -> 154 (+14)");
        refusal.Should().Contain(WptBrowserCensus.RaiseVariableValue);
    }

    // A fall and an exact match are both fine to write: neither can be a bad run being made the new floor.
    [TestCase(126)]
    [TestCase(140)]
    public void TheRewriteWritesAnythingThatIsNotARise(int notPassing)
    {
        WptBrowserCensus.RefusalToRaise(Table(55, 9, 700, notPassing), Table(55, 9, 700, 140)).Should().BeNull();
    }
}
