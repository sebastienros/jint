using System.Reflection;
using System.Text;
using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// Runs the vendored web-platform-tests <b>documents</b> — one theory case per <c>.html</c> file and per
/// synthesized <c>.any.html</c> wrapper — in a real <see cref="Page"/>, under upstream's own
/// <c>testharness.js</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The exclusion table is the point of the driver</b>, and it is the same rule the <c>.any.js</c> lane
/// enforces: a test that does not pass has to be named in <see cref="WptBrowserExclusions.All"/> with a
/// category, and every entry there must match at least one failing test and no passing one. So a fix, a
/// rename or a corpus bump makes the run fail until the table is brought back in line, and a <c>*</c> glob can
/// never widen into a blanket.
/// </para>
/// <para>
/// <b>A harness error is for the whole file and no per-test exclusion can name it</b>, so a document that
/// cannot produce a report belongs in <see cref="WptBrowserExclusions.NotVendored"/> with its reason rather
/// than in the exclusion table. That is the ninth rule of <c>Jint.Tests/Wpt/AGENTS.md</c>, and it decides more
/// here than it does there: a document is a whole environment, and the ways one can fail to report — a frame
/// that had to run script, a navigation the page really performed, a document that replaced itself — have no
/// analogue in a file handed to an engine.
/// </para>
/// <para>
/// <b><see cref="WptDivergence.NeedsTriage"/> is the debt.</b> A non-zero count there means this corpus has
/// found something and somebody still owes the engine or the package a fix; <c>Wpt/AGENTS.md</c> lists what
/// each of them is.
/// </para>
/// </remarks>
public class WptBrowserTestRunner
{
    /// <summary>
    /// How many results a case must produce at the least, so that a file which quietly stopped registering
    /// its tests fails rather than passing with nothing in it.
    /// </summary>
    /// <remarks>
    /// The same table the <c>.any.js</c> lane keeps, and it does the same two jobs: it is the floor a run is
    /// held to, and — because every case must have an entry — it is the declaration that makes a newly
    /// vendored document <i>run</i> rather than merely be embedded.
    /// </remarks>
    private static readonly Dictionary<string, int> _minimumTests = WptBrowserExclusions.MinimumTests;

    [TestCaseSource(nameof(DomEventsCases))]
    public Task RunsTheDomEventsSuite(string path) => RunCaseAsync(path);

    [TestCaseSource(nameof(ScriptingEventsCases))]
    public Task RunsTheScriptingEventsSuite(string path) => RunCaseAsync(path);

    [TestCaseSource(nameof(ScriptingProcessingModelCases))]
    public Task RunsTheScriptingProcessingModelSuite(string path) => RunCaseAsync(path);

    public static IEnumerable<object[]> DomEventsCases() => Cases("dom/events");

    public static IEnumerable<object[]> ScriptingEventsCases() => Cases("html/webappapis/scripting/events");

    public static IEnumerable<object[]> ScriptingProcessingModelCases() => Cases("html/webappapis/scripting/processing-model-2");

    private static IEnumerable<object[]> Cases(string suite)
    {
        foreach (var path in WptBrowserCorpus.Cases(suite))
        {
            yield return [path];
        }
    }

    /// <summary>
    /// The inventory check: what is vendored, what is run, and what is deliberately absent must all agree.
    /// </summary>
    /// <remarks>
    /// This is what a re-vendor runs into. A document that arrives without a minimum-test entry would
    /// otherwise be embedded and never run, and one that arrives against a
    /// <see cref="WptBrowserExclusions.NotVendored"/> reason would quietly go red for a cause somebody already
    /// decided about. The <c>.any.js</c> half of the corpus is not this lane's business and is checked by
    /// <c>WptTestRunner</c>; what <i>is</i> checked here is that every document under a suite this lane claims
    /// is either a case or accounted for.
    /// </remarks>
    [Test]
    public void EveryVendoredDocumentIsAccountedFor()
    {
        var problems = new List<string>();
        var cases = new HashSet<string>(AllCases(), StringComparer.Ordinal);

        foreach (var path in WptCorpus.Paths)
        {
            foreach (var (pattern, reason) in WptBrowserExclusions.NotVendored)
            {
                if (WptExclusion.MatchesPattern(pattern, path))
                {
                    problems.Add($"{path} is vendored although \"{pattern}\" says it should not be ({reason})");
                }
            }

            if (!WptCorpus.IsBrowserTestFile(path) || !WptCorpus.IsUnderABrowserSuite(path))
            {
                continue;
            }

            // A document directly under a suite is a case; one under its resources/ or support/ directory is
            // a helper the cases load, which is why WptCorpus.BrowserTestFiles never descends.
            if (Array.Exists(WptCorpus.BrowserSuites, suite => string.Equals(WptCorpus.DirectoryOf(path), suite, StringComparison.Ordinal))
                && !cases.Contains(path))
            {
                problems.Add($"{path} is vendored under a browser-lane suite but no theory case reaches it");
            }
        }

        foreach (var path in cases)
        {
            if (!_minimumTests.ContainsKey(path))
            {
                problems.Add($"{path} is a case with no entry in the minimum-test table, so nothing holds it to anything");
            }
        }

        foreach (var declared in _minimumTests.Keys)
        {
            if (!cases.Contains(declared))
            {
                problems.Add($"{declared} has a minimum-test entry but is not a case of any suite");
            }
        }

        foreach (var exclusion in WptBrowserExclusions.All)
        {
            if (!cases.Contains(exclusion.File))
            {
                problems.Add($"{exclusion.File} carries an exclusion but is not a case of any suite");
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty();

        // The theory cases are generated from the corpus, so an empty corpus would be an empty, green run.
        cases.Should().HaveCountGreaterThan(60, "the lane runs the documents of three suites");
    }

    /// <summary>
    /// Every case is reached by exactly one test-case source, and every source reaches only cases.
    /// </summary>
    /// <remarks>
    /// The check above proves a document is <i>declared</i>. It cannot prove anything <i>runs</i> it: a suite
    /// is prose until a <c>[TestCaseSource]</c> naming a member that produces its cases exists, and deleting
    /// the test — or its attribute, or renaming the member it names — would leave a whole suite silently unrun
    /// with the inventory still green. It is also what stops two sources overlapping, since a case covered
    /// twice would make an exclusion that is stale in one test look live because the other still matched it.
    /// </remarks>
    [Test]
    public void EveryCaseIsReachedByExactlyOneSource()
    {
        var reachedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        var sources = 0;

        foreach (var method in typeof(WptBrowserTestRunner).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var testCaseSources = method.GetCustomAttributes<TestCaseSourceAttribute>().ToArray();
            if (testCaseSources.Length == 0)
            {
                continue;
            }

            sources++;

            foreach (var attribute in testCaseSources)
            {
                var member = typeof(WptBrowserTestRunner).GetMethod(
                    attribute.SourceName!,
                    BindingFlags.Public | BindingFlags.Static);
                member.Should().NotBeNull($"{method.Name} names \"{attribute.SourceName}\"");

                var any = false;
                foreach (var row in (IEnumerable<object[]>) member!.Invoke(null, null)!)
                {
                    any = true;
                    var path = (string) row[0];
                    reachedBy.TryGetValue(path, out var already).Should().BeFalse(
                        $"{path} is reached by both {already} and {method.Name}");
                    reachedBy[path] = method.Name;
                }

                any.Should().BeTrue($"{attribute.SourceName} must produce cases");
            }
        }

        sources.Should().Be(WptCorpus.BrowserSuites.Length, "each browser-lane suite is one test-case source");
        reachedBy.Keys.Should().BeEquivalentTo(AllCases(), "a source must reach every case and nothing else");
    }

    /// <summary>
    /// Every synthesized case is a wrapper the server really generates, and no vendored document pretends to
    /// be one.
    /// </summary>
    /// <remarks>
    /// A wrapper exists because upstream's <c>AnyHtmlHandler</c> would generate it, and the two conditions it
    /// puts on that are the underlying file being there and its <c>// META: global=</c> naming the window. A
    /// case that met neither would be a 404 the driver reported as a harness error, which reads as a defect in
    /// the page rather than as a case that should not have been generated — so both are checked here, from the
    /// server's own port of the rule.
    /// </remarks>
    [Test]
    public void EverySynthesizedCaseIsAWrapperTheServerGenerates()
    {
        var synthesized = 0;

        foreach (var path in AllCases())
        {
            if (WptBrowserCorpus.IsVendored(path))
            {
                WptServerWrappers.IsWrapperPath(path).Should().BeFalse(
                    $"{path} is a vendored document, so nothing should be synthesizing it");
                continue;
            }

            synthesized++;
            WptServerWrappers.IsWrapperPath(path).Should().BeTrue($"{path} is neither vendored nor a wrapper path");

            var underlying = WptServerWrappers.UnderlyingFile(path);
            WptCorpus.Contains(underlying).Should().BeTrue($"{path} wraps {underlying}, which is not vendored");

            WptServerWrappers.Window(path, WptCorpus.Read(underlying)).Should().NotBeNull(
                $"{underlying} does not declare the window global, so upstream would answer 404 for {path}");
        }

        synthesized.Should().BeGreaterThan(0, "the .any.js corpus of a browser-lane suite runs here as well");
    }

    private static IReadOnlyCollection<string> AllCases()
    {
        var cases = new List<string>();
        foreach (var suite in WptCorpus.BrowserSuites)
        {
            cases.AddRange(WptBrowserCorpus.Cases(suite));
        }

        return cases;
    }

    /// <summary>
    /// Runs one document and holds it to two rules. Every failing test must be named by an exclusion, and
    /// every exclusion must match at least one failing test and no passing one.
    /// </summary>
    private static async Task RunCaseAsync(string path)
    {
        var outcome = await WptBrowserHarness.Instance.RunAsync(path).ConfigureAwait(false);

        outcome.HarnessError.Should().BeNull($"{path} must run to completion");

        var exclusions = Array.FindAll(WptBrowserExclusions.All,
            e => string.Equals(e.File, path, StringComparison.Ordinal) && e.AppliesOnThisPlatform);
        var matchedFailing = new bool[exclusions.Length];
        var matchedPassing = new List<string>?[exclusions.Length];
        var failures = new List<string>();

        foreach (var result in outcome.Results)
        {
            var excluded = false;
            for (var i = 0; i < exclusions.Length; i++)
            {
                if (!exclusions[i].Matches(result.Name))
                {
                    continue;
                }

                excluded = true;
                if (result.Passed)
                {
                    (matchedPassing[i] ??= []).Add(result.Name);
                }
                else
                {
                    matchedFailing[i] = true;
                }
            }

            if (!result.Passed && !excluded)
            {
                failures.Add($"[{result.StatusName}] {result.Name}: {result.Message}");
            }
        }

        var stale = new List<string>();
        for (var i = 0; i < exclusions.Length; i++)
        {
            if (matchedPassing[i] is { } passing)
            {
                stale.Add($"\"{exclusions[i].TestName}\" ({exclusions[i].Divergence}) covers {passing.Count} test(s) that pass, "
                    + $"the first being \"{passing[0]}\"");
            }
            else if (!matchedFailing[i])
            {
                stale.Add($"\"{exclusions[i].TestName}\" ({exclusions[i].Divergence}) matches no test in the file");
            }
        }

        // Before the failure and staleness reports, because a document that produced nothing would otherwise
        // be reported as a wall of exclusions that match no test rather than as the empty run it is.
        outcome.Results.Count.Should().BeGreaterThanOrEqualTo(
            _minimumTests[path],
            $"{path} must actually have run its tests");

        Report(path, failures, stale);
    }

    private static void Report(string path, List<string> failures, List<string> stale)
    {
        if (failures.Count == 0 && stale.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.Append(path).AppendLine(":");

        if (stale.Count > 0)
        {
            message.Append("  ").Append(stale.Count)
                .AppendLine(" exclusion(s) that no longer apply — remove or narrow them:");
            foreach (var entry in stale)
            {
                message.Append("    ").AppendLine(entry);
            }
        }

        if (failures.Count > 0)
        {
            message.Append("  ").Append(failures.Count)
                .AppendLine(" failing test(s) — fix them, or add them to the exclusion table with a category:");
            foreach (var entry in failures)
            {
                message.Append("    ").AppendLine(entry);
            }
        }

        message.ToString().Should().BeEmpty();
    }
}
