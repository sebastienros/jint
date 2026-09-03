#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Wpt;

/// <summary>
/// Holds <c>Vendor/README.md</c>'s corpus inventory — "The whole corpus, standard by standard" — to what the
/// corpus actually is, so the table is a derived artifact rather than prose somebody keeps up to date by hand.
/// </summary>
/// <remarks>
/// <para>
/// Two tests, split by what each column of that table costs to know. The standards, their suite counts and
/// their file counts are read straight off the embedded corpus and are checked always. The assertion and
/// not-passing totals need the suites to have run, so they are checked once the corpus has reported — see
/// <see cref="WptCensus"/> for how that is paid for, and why it is nearly free in a full run.
/// </para>
/// <para>
/// Neither test touches the driver's rules. The exclusion table still decides what is forgiven and a failing
/// test that no entry names still fails its own suite in <see cref="WptTestRunner"/>, whatever these say.
/// </para>
/// </remarks>
public class WptCensusTests
{
    /// <summary>
    /// The inventory table must name every standard the corpus holds, and its <c>Suites</c> and <c>Files</c>
    /// columns must be what is actually vendored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These three columns are derived without running anything — a row's files are its directory's
    /// <c>.any.js</c> files, and its suites are the directories those files sit in, because
    /// <see cref="WptCorpus.TestFiles"/> lists a directory's own files and never descends. So this holds on
    /// every platform and in every filtered run, for the cost of reading an embedded string.
    /// </para>
    /// <para>
    /// It is also what catches a corpus arriving with no row of its own: the census prefixes have to partition
    /// every vendored <c>.any.js</c>, so a new standard fails here rather than being silently left out of the
    /// inventory. The two counted columns are carried through from what the README already claims, so this
    /// test cannot fail for them — but the total row is recomputed from the rows above it, which means a table
    /// whose rows and total disagree fails here too.
    /// </para>
    /// </remarks>
    [Test]
    public void TheInventoryTableNamesEveryStandardAndCountsItsFilesAndSuites()
    {
        var lines = WptCensus.ReadReadme().Split('\n');
        var stated = WptCensus.ReadmeTable(lines);
        var derived = WptCensus.Render(measured: false, lines);

        if (WptCensus.Reconcile(derived, stated, countsIncluded: false) is { } differences)
        {
            Assert.Fail(differences);
        }
    }

    /// <summary>
    /// The whole inventory table, including the assertion and not-passing totals, must be what the corpus
    /// measures right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the check the table has never had.</b> Every figure in it is arithmetic the driver already
    /// does, and left as prose it drifts silently: a fix that removes an exclusion moves a row, nothing goes
    /// red, and the table quietly describes an engine that no longer exists. The README records exactly that
    /// happening — four rows at once carrying a number a later fix had already improved, corrected only when
    /// somebody re-censused the whole corpus by hand.
    /// </para>
    /// <para>
    /// <b>Opt-in, because it is the one part of the census that costs anything.</b> The totals can only be
    /// known by running the corpus, and although this reuses every outcome the driver already produced, a full
    /// <c>Jint.Tests</c> pass still measured 65 s → 85 s with it always on. So it is gated on
    /// <c>JINT_WPT_CENSUS</c> and a developer's ordinary run pays nothing; the PR workflow's Windows leg sets
    /// the variable, which is where the table is actually enforced. That mirrors the
    /// <c>JINT_HOST_CONTRACT_VERIFICATION</c> leg beside it.
    /// </para>
    /// <para>
    /// <c>JINT_WPT_CENSUS=1</c> checks the table; <c>JINT_WPT_CENSUS=update</c> rewrites it from the run. The
    /// second is the whole maintenance procedure, and it replaces the hand re-census.
    /// </para>
    /// <para>
    /// <b>Four equalities and a ceiling.</b> <c>Suites</c>, <c>Files</c> and <c>Assertions</c> are held
    /// exactly, because the first two are read off the corpus and the third counts <i>registrations</i> — a
    /// suite registers its cases at file scope, so a file that reports at all reports every one of them.
    /// <c>Not passing</c> is the only column that counts <i>outcomes</i>, and so the only one a loaded machine
    /// has ever moved: <see href="https://github.com/sebastienros/jint/issues/3339">#3339</see> is three
    /// unrelated pull requests reddened by it in one afternoon, one of them a change to three markdown
    /// headings. A rise still fails — the census cannot tell a flake from a real regression, and must not
    /// try — but it fails <i>as a regression</i>, naming the suite, the direction and the size, where it used
    /// to say "the inventory table is out of date" and thereby invite the one response that would ratchet the
    /// number permanently worse. A fall fails as staleness, which is the case that phrase actually fits.
    /// </para>
    /// <para>
    /// <b>And the rewrite will not raise the ceiling.</b> Without that half, a rise would still be answerable
    /// by re-censusing, which is exactly what must not work; see <see cref="WptCensus.RefusalToRaise"/> and
    /// the one deliberate spelling that overrides it.
    /// </para>
    /// <para>
    /// Windows only, in both modes, because assertion counts move per operating system and the table says in
    /// its own first line that it is measured on Windows — updating it from a Linux run would write figures
    /// the table does not claim. That is a deliberate hole: the three columns above are platform-independent
    /// and are held everywhere.
    /// </para>
    /// </remarks>
    [Test]
    public void TheInventoryTableMatchesWhatTheCorpusMeasures()
    {
        if (!WptCensus.CensusRequested())
        {
            Assert.Ignore(
                $"the census runs the corpus to total it, so it is opt-in: set {WptCensus.UpdateVariable}=1 to "
                + $"check the inventory table, or {WptCensus.UpdateVariable}=update to rewrite it.");
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("the inventory table is measured on Windows, and assertion counts move per platform.");
        }

        var updating = WptCensus.UpdateRequested();
        WptCensus.Measure();

        var measured = WptCensus.Render(measured: true);
        var stated = WptCensus.ReadmeTable();

        if (updating)
        {
            if (!WptCensus.RaiseRequested() && WptCensus.RefusalToRaise(measured, stated) is { } refusal)
            {
                Assert.Fail(refusal);
            }

            var path = WptCensus.WriteReadmeTable(measured);
            Assert.Ignore($"{WptCensus.UpdateVariable} rewrote the inventory table in {path}.");
        }

        if (WptCensus.Reconcile(measured, stated, countsIncluded: true) is { } differences)
        {
            Assert.Fail(differences);
        }
    }

    /// <summary>
    /// The drift-verification recipe must walk the corpus that is actually vendored, and say how much of it
    /// it walked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A verification statement is the one kind of prose that must never be typed.</b> The section that
    /// checks the corpus against upstream used to end "verified at this pin: 359 files, 48 directories" —
    /// four vendored suites out of date by the time
    /// <see href="https://github.com/sebastienros/jint/issues/3647">#3647</see> was filed, and stale before
    /// that. Nothing went red, because nothing was reading it: a figure describing what was checked reads
    /// exactly the same whether it is true or three suites behind.
    /// </para>
    /// <para>
    /// So the recipe and its figures are rendered from <see cref="WptCorpus.Paths"/> and held here. Free, like
    /// the first test above and unlike the measured one: it counts what is embedded and runs no suite, so it
    /// holds on every platform and in every filtered run. <c>JINT_WPT_CENSUS=update</c> rewrites the block,
    /// the same maintenance procedure the inventory table has.
    /// </para>
    /// <para>
    /// The outcome — no drift, no file absent upstream — stays prose after the block, because reproducing it
    /// needs the network and a token. What the generated figures give it is a subject: when they move, the
    /// sentence is about a corpus that no longer exists and the comparison has to be run again.
    /// </para>
    /// </remarks>
    [Test]
    public void TheDriftRecipeWalksTheCorpusThatIsVendored()
    {
        var rendered = WptCensus.RenderDrift();

        if (WptCensus.UpdateRequested())
        {
            var path = WptCensus.WriteReadmeDrift(rendered);
            Assert.Ignore($"{WptCensus.UpdateVariable} rewrote the drift-verification recipe in {path}.");
        }

        if (WptCensus.ReconcileDrift(rendered, WptCensus.ReadmeDrift()) is { } differences)
        {
            Assert.Fail(differences);
        }
    }

    /// <summary>
    /// Every extension the corpus vendors has to be one the recipe's <c>find</c> names.
    /// </summary>
    /// <remarks>
    /// The half of the stale claim that was not merely stale but wrong: the recipe named five extensions
    /// where the corpus vendors eight, so the <c>.asis</c> responses the xhr suites read as whole HTTP
    /// messages, the <c>.headers</c> sidecars beside upstream's harness and two payload files were outside
    /// every drift check ever run — silently, since a file the walk never lists cannot be reported missing.
    /// This asserts the property directly rather than through the block comparison above, so it survives the
    /// prose being reworded and names what is unwalked when it fails.
    /// </remarks>
    [Test]
    public void TheDriftRecipeNamesEveryExtensionTheCorpusVendors()
    {
        var stated = WptCensus.ReadmeDrift();
        var unwalked = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var path in WptCorpus.Paths)
        {
            var extension = Path.GetExtension(path);
            if (!stated.Contains($"-name *{extension}", StringComparison.Ordinal))
            {
                unwalked.Add(extension);
            }
        }

        unwalked.Should().BeEmpty(
            "a vendored file the drift recipe's find does not list is one the comparison against upstream "
            + "cannot report as drifted or as absent");
    }

    [Test]
    public void ARecipeTheCorpusAgreesWithIsNotADifference()
    {
        WptCensus.ReconcileDrift(WptCensus.RenderDrift(), WptCensus.RenderDrift()).Should().BeNull();
    }

    [Test]
    public void AStaleDriftRecipeSaysToRegenerateRatherThanToEdit()
    {
        // The failure mode this replaces was somebody typing the new numbers, so the message must not read
        // as an invitation to do that again: it names the command, and says the run behind the sentence
        // under the block was a run of a different corpus.
        var message = WptCensus.ReconcileDrift(WptCensus.RenderDrift(), "349 files in 47 directories\n");

        message.Should().NotBeNull();
        message.Should().Contain($"{WptCensus.UpdateVariable}=update");
        message.Should().Contain("run it again rather than to edit it");
        message.Should().Contain("has to be run again");
    }

    /// <summary>
    /// A one-row table plus its total, which is the smallest thing the comparison has an opinion about.
    /// </summary>
    private static string Table(int files, int assertions, int notPassing, string standard = "Fetch", string suites = "`fetch/api/` ×5") =>
        $"""
         | Standard | Suites | Files | Assertions | Not passing |
         | --- | --- | --- | --- | --- |
         | {standard} | {suites} | {files} | {assertions} | {notPassing} |
         | **total** | **5** | **{files}** | **{assertions}** | **{notPassing}** |

         """;

    [Test]
    public void ATableTheCorpusAgreesWithIsNotADifference()
    {
        WptCensus.Reconcile(Table(49, 714, 154), Table(49, 714, 154), countsIncluded: true).Should().BeNull();
    }

    [Test]
    public void MoreFailingAssertionsThanTheTableAllowsIsReportedAsARegression()
    {
        // The shape of every complaint on #3339: fourteen fetch assertions that passed on one run and did not
        // on the next. What the message must not do is present that as a stale table, because the response to
        // a stale table is to re-census — which would write the worse number in as the new floor.
        var message = WptCensus.Reconcile(Table(49, 714, 168), Table(49, 714, 154), countsIncluded: true);

        message.Should().NotBeNull();
        message.Should().Contain("fails more than");
        message.Should().Contain("Fetch: not passing 154 -> 168 (+14)", "the suite, the direction and the size");
        message.Should().Contain("total: not passing 154 -> 168 (+14)");
        message.Should().Contain("ceiling, not a baseline");
        message.Should().NotContain("out of date", "a regression is not the author forgetting to re-census");
    }

    [Test]
    public void FewerFailingAssertionsThanTheTableStatesIsReportedAsStaleness()
    {
        // The other direction, and the one case where "out of date" is the truthful sentence: a fix removed an
        // exclusion and nobody re-censused. This is what the update mode is for, and it says so.
        var message = WptCensus.Reconcile(Table(49, 714, 140), Table(49, 714, 154), countsIncluded: true);

        message.Should().NotBeNull();
        message.Should().Contain("out of date");
        message.Should().Contain("Fetch: not passing 154 -> 140 (-14)");
        message.Should().Contain($"{WptCensus.UpdateVariable}=update");
    }

    // One more file than the table names, and one more assertion. Neither counts an outcome, so both are
    // equalities in both directions and a change either way is the corpus having moved.
    [TestCase(50, 714, 154)]
    [TestCase(48, 714, 154)]
    [TestCase(49, 715, 154)]
    [TestCase(49, 713, 154)]
    public void AColumnThatIsNotAnOutcomeIsHeldExactly(int files, int assertions, int notPassing)
    {
        WptCensus.Reconcile(Table(files, assertions, notPassing), Table(49, 714, 154), countsIncluded: true)
            .Should().NotBeNull();
    }

    [Test]
    public void ASuiteRegroupedOrAStandardRenamedIsReportedByName()
    {
        // The other two equalities, which are text rather than figures: a directory split into one more
        // theory, and a row whose standard the census no longer names — the second reported from both ends,
        // because a rename is a row appearing and a row disappearing and a reader needs to see the pair.
        var regrouped = WptCensus.Reconcile(
            Table(49, 714, 154, suites: "`fetch/api/` ×6"), Table(49, 714, 154), countsIncluded: true);

        regrouped.Should().NotBeNull();
        regrouped.Should().Contain("Fetch: suites `fetch/api/` ×5 -> `fetch/api/` ×6");

        var renamed = WptCensus.Reconcile(
            Table(49, 714, 154, standard: "Fetch API"), Table(49, 714, 154), countsIncluded: true);

        renamed.Should().NotBeNull();
        renamed.Should().Contain("Fetch API: the corpus has this standard and the table has no row for it");
        renamed.Should().Contain("Fetch: the table has a row for it and the corpus does not");
    }

    [Test]
    public void TheFreeCheckHasNoOpinionOnTheTwoCountedColumns()
    {
        // It has not run the suites, so it knows nothing about either — which is what lets it run on every
        // platform and in a filtered run.
        WptCensus.Reconcile(Table(49, 0, 0), Table(49, 714, 154), countsIncluded: false).Should().BeNull();
        WptCensus.Reconcile(Table(50, 0, 0), Table(49, 714, 154), countsIncluded: false).Should().NotBeNull();
    }

    [Test]
    public void TheRewriteRefusesToRaiseTheCeilingAndSaysWhatWouldNotBe()
    {
        // Without this the ceiling is a suggestion: a rise fails the check, the author reaches for the one
        // command the failure mentions, and the worse number becomes the baseline. So the rewrite lowers and
        // never raises, and it names the deliberate spelling rather than leaving the door simply shut — a
        // corpus bump does arrive with new failures.
        var refusal = WptCensus.RefusalToRaise(Table(49, 714, 168), Table(49, 714, 154));

        refusal.Should().NotBeNull();
        refusal.Should().Contain("Fetch: not passing 154 -> 168 (+14)");
        refusal.Should().Contain(WptCensus.RaiseVariableValue);
    }

    // A fall and an exact match are both fine to write: neither can be a bad run being made the new floor.
    [TestCase(140)]
    [TestCase(154)]
    public void TheRewriteWritesAnythingThatIsNotARise(int notPassing)
    {
        WptCensus.RefusalToRaise(Table(49, 714, notPassing), Table(49, 714, 154)).Should().BeNull();
    }
}
#endif
