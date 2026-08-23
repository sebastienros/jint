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
    [Fact]
    public void TheInventoryTableNamesEveryStandardAndCountsItsFilesAndSuites()
    {
        var lines = WptCensus.ReadReadme().Split('\n');
        var stated = WptCensus.ReadmeTable(lines);
        var derived = WptCensus.Render(measured: false, lines);

        if (!string.Equals(derived, stated, StringComparison.Ordinal))
        {
            Assert.Fail(WptCensus.Explain(derived, stated, "the standards, suite counts or file counts have moved"));
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
    /// Windows only, in both modes, because assertion counts move per operating system and the table says in
    /// its own first line that it is measured on Windows — updating it from a Linux run would write figures
    /// the table does not claim. That is a deliberate hole: the three columns above are platform-independent
    /// and are held everywhere.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheInventoryTableMatchesWhatTheCorpusMeasures()
    {
        Assert.SkipUnless(
            WptCensus.CensusRequested(),
            $"the census runs the corpus to total it, so it is opt-in: set {WptCensus.UpdateVariable}=1 to "
            + $"check the inventory table, or {WptCensus.UpdateVariable}=update to rewrite it.");

        Assert.SkipUnless(
            OperatingSystem.IsWindows(),
            "the inventory table is measured on Windows, and assertion counts move per platform.");

        var updating = WptCensus.UpdateRequested();
        WptCensus.Measure();

        var measured = WptCensus.Render(measured: true);

        if (updating)
        {
            var path = WptCensus.WriteReadmeTable(measured);
            Assert.Skip($"{WptCensus.UpdateVariable} rewrote the inventory table in {path}.");
        }

        var stated = WptCensus.ReadmeTable();
        if (!string.Equals(measured, stated, StringComparison.Ordinal))
        {
            Assert.Fail(WptCensus.Explain(measured, stated, "the corpus was censused and the totals disagree"));
        }
    }
}
#endif
