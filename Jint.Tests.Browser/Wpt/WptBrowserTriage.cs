namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// Runs the documents <c>JINT_WPT_DOCUMENT</c> names and prints <b>every</b> result each one reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because the lane's own driver only ever tells you about the failures it did not expect.</b>
/// <see cref="WptBrowserTestRunner"/> reports a failing test that no exclusion covers and an exclusion that
/// covers nothing failing — which is exactly right for a gate and useless for triage, because the two things
/// a new document needs are the count for its minimum-test entry and the <i>names</i> of everything in it.
/// Worse, a freshly vendored document is not a case at all until that minimum-test entry exists, so the only
/// way to find out what to put in the table was to put something in the table and read the failure. This runs
/// any path the server can serve, whether or not anything else knows about it.
/// </para>
/// <para>
/// <b><c>[Explicit]</c>, and it has to be.</b> The driver funnels every run through
/// <c>WptBrowserHarness.RunAsync</c>, which records the outcome in the lane's census — so a triage run that
/// happened during an ordinary pass would put a file in the census that no theory produced. Naming this test
/// explicitly is what keeps the census the tally of a real run.
/// </para>
/// <example>
/// One document, or several separated by <c>;</c> or <c>,</c>:
/// <code>
/// JINT_WPT_DOCUMENT=dom/nodes/Node-cloneNode.html \
///   dotnet test Jint.Tests.Browser/Jint.Tests.Browser.csproj -c Release -nr:false \
///     --filter "FullyQualifiedName~WptBrowserTriage" -l "console;verbosity=detailed"
/// </code>
/// A path is what the server serves: a vendored document (<c>dom/events/Event-propagation.html</c>) or a
/// wrapper it synthesizes for a vendored script (<c>dom/events/Event-constructors.any.html</c>).
/// </example>
/// </remarks>
[Explicit("Triage: prints every result of the documents JINT_WPT_DOCUMENT names, and records them in the census.")]
[NonParallelizable]
public class WptBrowserTriage
{
    /// <summary>The environment variable naming what to run, because a test takes no arguments.</summary>
    private const string Variable = "JINT_WPT_DOCUMENT";

    [Test]
    public async Task PrintsEveryResultOfTheNamedDocuments()
    {
        var documents = Documents();

        if (documents.Count == 0)
        {
            Assert.Ignore(
                $"Set {Variable} to the wpt path to triage — for example "
                + $"{Variable}=dom/nodes/Node-cloneNode.html — separating several with ';' or ','.");
        }

        foreach (var path in documents)
        {
            await ReportAsync(path).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one document and writes everything it said to the test output.</summary>
    private static async Task ReportAsync(string path)
    {
        var outcome = await WptBrowserHarness.Instance.RunAsync(path).ConfigureAwait(false);
        var output = TestContext.Out;

        output.WriteLine();
        output.WriteLine(path);
        output.WriteLine(new string('-', path.Length));

        if (outcome.HarnessError is { } error)
        {
            // A harness error covers the whole file and no per-test exclusion can name it, so it is the first
            // thing a triager has to see: the answer for such a file is a not-vendored reason, never a row in
            // the exclusion table.
            output.WriteLine($"  HARNESS ERROR: {error}");
        }

        var byStatus = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var result in outcome.Results)
        {
            byStatus[result.StatusName] = byStatus.TryGetValue(result.StatusName, out var seen) ? seen + 1 : 1;

            output.WriteLine($"  [{result.StatusName}] {result.Name}");

            if (result.Message is { Length: > 0 } message)
            {
                output.WriteLine($"      {message.Replace("\n", "\n      ", StringComparison.Ordinal)}");
            }
        }

        output.WriteLine();
        output.WriteLine($"  {outcome.Results.Count} result(s): {Tally(byStatus)}");
        output.WriteLine(
            $"  The minimum-test entry for {path} is {outcome.Results.Count}, which is the floor a file that "
            + "quietly stops registering its tests has to fall through.");
    }

    /// <summary>The per-status counts as one line, or a note that nothing reported at all.</summary>
    private static string Tally(SortedDictionary<string, int> byStatus)
        => byStatus.Count == 0 ? "nothing reported" : string.Join(", ", byStatus.Select(entry => $"{entry.Value} {entry.Key}"));

    /// <summary>The paths <c>JINT_WPT_DOCUMENT</c> names, in the order it named them.</summary>
    private static IReadOnlyList<string> Documents()
    {
        var value = Environment.GetEnvironmentVariable(Variable);

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var paths = new List<string>();

        foreach (var entry in value!.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var path = entry.Trim().Replace('\\', '/').TrimStart('/');

            if (path.Length != 0)
            {
                paths.Add(path);
            }
        }

        return paths;
    }
}
