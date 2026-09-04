using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>One cause the corpus found: a name, and the exclusions that <i>are</i> it.</summary>
/// <remarks>
/// The name is what a <c>Wpt/README.md</c> row keys on, so it has to be unique — three groups shared the
/// header "a frame that runs script" and two of them are qualified now. It is never rendered: what a reader
/// sees is the row's own prose.
/// </remarks>
internal readonly record struct WptCause(string Name, WptExclusion[] Exclusions);

/// <summary>
/// The cause table in <c>Wpt/README.md</c> — "What the DOM corpus says about this browser" — as a derived
/// artifact rather than as two columns somebody keeps up to date by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because those two columns rotted, silently, for exactly as long as nothing counted them.</b>
/// The suite table above them has been generated since it was written; this one was typed, and by the time
/// anybody looked, <b>nine of its seventeen rows were wrong and the drift went in both directions</b> — one
/// cause had grown from 165 tests to 478 as a fix uncovered a table a missing member had been hiding, and
/// another had shrunk from 65 to 24 as its fix landed. A row reads exactly the same whether it is true or
/// four merged fixes out of date, which is the failure
/// <see href="https://github.com/sebastienros/jint/issues/3647">#3647</see> records for the engine lane's
/// drift recipe and <see href="https://github.com/sebastienros/jint/issues/3339">#3339</see> for its census.
/// </para>
/// <para>
/// <b>What is generated is the two numbers and the order; the prose stays hand-written.</b> A row carries its
/// cause's name in an HTML comment, which the rendered page does not show and which is the only thing
/// connecting a paragraph a person wrote to a group a run can count. Everything else in the row is copied
/// through untouched.
/// </para>
/// <para>
/// <b>The counting rule is the exclusion table's own.</b> A cause accounts for a test when one of its
/// patterns names it, and <see cref="WptBrowserCauseTests.TheCauseTableMatchesWhatTheLaneMeasures"/> is what
/// makes that a partition rather than a hope: no test is counted twice and none is orphaned. The
/// <c>Tests</c> column counts <i>results</i> and not distinct names, because two tests of one document may
/// share a name and the census counts both.
/// </para>
/// <para>
/// <b>Neither column is a ceiling.</b> Both are equalities in both directions, like <c>Documents</c> and
/// <c>Tests</c> in the suite table: a cause that grew and a cause that shrank are equally a table that has
/// stopped describing this browser. The ceiling belongs to the census's <c>Not passing</c>, which is a
/// different question — how much fails — from this one, which is what each failure <i>is</i>.
/// </para>
/// </remarks>
internal static class WptBrowserCauses
{
    private const string SectionHeading = "## What the DOM corpus says about this browser";
    private const string TableHeader = "| Tests | Documents | What it is |";
    private const string TableDivider = "| ---: | ---: | --- |";

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> _failures = new(StringComparer.Ordinal);

    /// <summary>
    /// The six suites the section is about, which is the sentence above the table read as a list.
    /// </summary>
    /// <remarks>
    /// It decides which causes must have a row: one that names a failing test in any of these has to be in
    /// the table, and one in the table has to name a failing test in one of these. A cause outside them —
    /// <c>custom-elements/</c>'s own frames, say — is a real cause of a real failure and is simply not what
    /// this section counts. Two rows here also reach a <c>custom-elements/reactions/</c> document the same
    /// cause covers, which is why the column sums past the census's own figure for these six; the paragraph
    /// under the table says by how much.
    /// </remarks>
    internal static readonly string[] Suites =
    [
        "dom/nodes/",
        "dom/collections/",
        "dom/lists/",
        "dom/traversal/",
        "dom/ranges/",
        "html/dom/",
    ];

    /// <summary>What one case reported that did not pass, kept per path so a re-run overwrites its own.</summary>
    /// <remarks>
    /// Recorded from the same funnel the census records through, so this sees the whole lane without any
    /// theory knowing it exists. A harness error contributes nothing: a document that reported no test has no
    /// failing test to attribute, and its answer is a not-vendored reason rather than a row anywhere.
    /// </remarks>
    internal static void Record(string path, WptBrowserOutcome outcome)
    {
        var failing = new List<string>();

        foreach (var result in outcome.Results)
        {
            if (!result.Passed)
            {
                failing.Add(result.Name);
            }
        }

        _failures[path] = failing;
    }

    /// <summary>What the lane has reported so far, as (path, failing test name) pairs.</summary>
    internal static IEnumerable<(string Path, string Test)> Failures()
    {
        foreach (var (path, tests) in _failures)
        {
            foreach (var test in tests)
            {
                yield return (path, test);
            }
        }
    }

    /// <summary>Whether a path is one of the six suites this section counts.</summary>
    internal static bool IsCounted(string path)
    {
        foreach (var suite in Suites)
        {
            if (path.StartsWith(suite, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What one cause accounts for: how many failing results, over how many documents.</summary>
    internal readonly record struct Counts(int Tests, int Documents, int Counted);

    /// <summary>Counts every cause against what the lane reported.</summary>
    /// <remarks>
    /// A cause's exclusions are anchored to a file, so a pattern can only ever claim a test of the document
    /// it names — which is what keeps the partition cheap to state and impossible to get accidentally wide.
    /// </remarks>
    internal static Dictionary<string, Counts> Measure()
    {
        var counts = new Dictionary<string, Counts>(StringComparer.Ordinal);
        var byPath = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (path, tests) in _failures)
        {
            byPath[path] = new List<string>(tests);
        }

        foreach (var cause in WptBrowserExclusions.Causes)
        {
            var tests = 0;
            var counted = 0;
            var documents = new HashSet<string>(StringComparer.Ordinal);

            // One failing result is one test however many of the cause's patterns happen to name it, which
            // is what makes the column a count of the run rather than a count of the table.
            var claimed = new Dictionary<string, bool[]>(StringComparer.Ordinal);

            foreach (var exclusion in cause.Exclusions)
            {
                if (!byPath.TryGetValue(exclusion.File, out var failing))
                {
                    continue;
                }

                if (!claimed.TryGetValue(exclusion.File, out var already))
                {
                    already = new bool[failing.Count];
                    claimed[exclusion.File] = already;
                }

                for (var i = 0; i < failing.Count; i++)
                {
                    if (already[i] || !exclusion.Matches(failing[i]))
                    {
                        continue;
                    }

                    already[i] = true;
                    tests++;
                    documents.Add(exclusion.File);

                    if (IsCounted(exclusion.File))
                    {
                        counted++;
                    }
                }
            }

            counts[cause.Name] = new Counts(tests, documents.Count, counted);
        }

        return counts;
    }

    /// <summary>
    /// The sentence above the table, and the three figures in it: how many documents these six suites are,
    /// how many tests they register and how many of those do not pass.
    /// </summary>
    /// <remarks>
    /// Every one of the three is a column of the census table directly above, so none of them is a second
    /// answer to anything — and all three were typed. Generating a sentence is a worse job than generating a
    /// table, so this is <i>checked</i> rather than rendered: the numbers are read back out and compared with
    /// what the census measured, and a failure says which one moved. It exists because a hand-written figure
    /// in that sentence went stale within a day of the table above it being made generated, which is the same
    /// mistake at a smaller size.
    /// </remarks>
    internal static (int Documents, int Tests, int NotPassing)? StatedTotals(IReadOnlyList<string>? lines = null)
    {
        lines ??= WptBrowserCensus.ReadReadme().Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var start = line.IndexOf(Preamble, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            var numbers = new List<int>();
            var i = start;

            while (i < line.Length && numbers.Count < 3)
            {
                if (!char.IsAsciiDigit(line[i]))
                {
                    i++;
                    continue;
                }

                var from = i;
                while (i < line.Length && (char.IsAsciiDigit(line[i]) || line[i] == ','))
                {
                    i++;
                }

                if (TryParseNumber(line[from..i], out var value))
                {
                    numbers.Add(value);
                }
            }

            if (numbers.Count == 3)
            {
                return (numbers[0], numbers[1], numbers[2]);
            }
        }

        return null;
    }

    /// <summary>
    /// The opening of the sentence the three <i>live</i> figures are in, and it is deliberately not a phrase
    /// in the past tense.
    /// </summary>
    /// <remarks>
    /// The anchor used to be <c>"They arrived together,"</c>, which read as a record of when the six suites
    /// were vendored — so a reader met two figure-carrying sentences beside a generated table with nothing
    /// saying which was which, and the checked one was the one that looked historical. The paragraph now
    /// states both: these three figures are live and this check enforces them, while the arrival figures
    /// beside them are a record of a moment that nothing checks and nothing may re-derive. Only the anchored
    /// line is read, so the historical clause on its own line cannot be mistaken for it.
    /// </remarks>
    private const string Preamble = "Across the six of them there are";

    /// <summary>One row of the table as the README states it: the two figures, the prose, and the key.</summary>
    internal readonly record struct Row(int Tests, int Documents, string Prose, string Cause);

    /// <summary>The table exactly as the README states it, normalised to LF and ending in one newline.</summary>
    internal static string ReadmeTable(IReadOnlyList<string>? lines = null)
    {
        lines ??= WptBrowserCensus.ReadReadme().Split('\n');
        var (start, end) = Locate(lines);

        var table = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            table.Append(lines[i].TrimEnd('\r')).Append('\n');
        }

        return table.ToString();
    }

    /// <summary>Where the table sits in the README, as a half-open line range.</summary>
    internal static (int Start, int End) Locate(IReadOnlyList<string> lines)
    {
        var heading = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].TrimEnd('\r'), SectionHeading, StringComparison.Ordinal))
            {
                heading = i;
                break;
            }
        }

        if (heading < 0)
        {
            throw new InvalidOperationException(
                $"Wpt/README.md no longer has a \"{SectionHeading}\" section, which is where the cause table lives.");
        }

        for (var i = heading; i < lines.Count; i++)
        {
            if (!string.Equals(lines[i].TrimEnd('\r'), TableHeader, StringComparison.Ordinal))
            {
                continue;
            }

            var end = i;
            while (end < lines.Count && lines[end].StartsWith("|", StringComparison.Ordinal))
            {
                end++;
            }

            return (i, end);
        }

        throw new InvalidOperationException(
            $"Wpt/README.md's \"{SectionHeading}\" section no longer holds a \"{TableHeader}\" table.");
    }

    /// <summary>The rows the README states, in the order it states them.</summary>
    internal static List<Row> ParseRows(string table)
    {
        var rows = new List<Row>();

        foreach (var rawLine in table.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith("|", StringComparison.Ordinal) || !line.EndsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            // Only the first two cells are split on, because the third is prose and prose may hold a pipe.
            var body = line[1..^1];
            var first = body.IndexOf('|', StringComparison.Ordinal);
            if (first < 0)
            {
                continue;
            }

            var second = body.IndexOf('|', first + 1);
            if (second < 0)
            {
                continue;
            }

            if (!TryParseNumber(body[..first], out var tests) || !TryParseNumber(body[(first + 1)..second], out var documents))
            {
                // The header and the divider, which carry no figures.
                continue;
            }

            var prose = body[(second + 1)..].Trim();
            rows.Add(new Row(tests, documents, prose, CauseOf(prose)));
        }

        return rows;
    }

    /// <summary>The cause a row names itself with, or the empty string when it names none.</summary>
    /// <remarks>
    /// An HTML comment, so the rendered page shows the prose and nothing else. It is deliberately not derived
    /// from the prose — a row's wording changes when what it describes changes, and a key that moved with it
    /// would silently start counting something else.
    /// </remarks>
    internal static string CauseOf(string prose)
    {
        const string Open = "<!-- cause: ";
        const string Close = " -->";

        var start = prose.IndexOf(Open, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var end = prose.IndexOf(Close, start, StringComparison.Ordinal);
        return end < 0 ? "" : prose[(start + Open.Length)..end];
    }

    /// <summary>
    /// Which causes the table names that a run says are spent, and which it does not name that a run says are
    /// live.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A spent cause is reported, never rendered as a zero row and never silently dropped.</b> The two
    /// numbers are generated and the prose is not, so a cause whose last row a fix retired leaves a paragraph
    /// somebody wrote about a defect that no longer exists — and deleting a paragraph is a human act, not a
    /// generator's. Rendering it as <c>| 0 | 0 |</c> would be worse than either: a table of causes with a
    /// cause that causes nothing.
    /// </para>
    /// <para>
    /// It is a function rather than a loop inside the test so that
    /// <c>WptBrowserCauseTests</c> can hold it to a table nobody has to run the lane to build.
    /// </para>
    /// </remarks>
    internal static List<string> Reconcile(IReadOnlyList<Row> stated, IReadOnlyDictionary<string, Counts> measured, IReadOnlyList<WptCause> causes)
    {
        var problems = new List<string>();
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in stated)
        {
            named.Add(row.Cause);
        }

        foreach (var cause in causes)
        {
            var counted = measured.TryGetValue(cause.Name, out var found) ? found.Counted : 0;

            if (counted > 0 && !named.Contains(cause.Name))
            {
                problems.Add(
                    $"\"{cause.Name}\" accounts for {counted} failing test(s) of the six suites this section counts "
                    + "and has no row in the cause table. Write one, with the two figures left at 0 for the rewrite to fill.");
            }

            if (counted == 0 && named.Contains(cause.Name))
            {
                problems.Add(
                    $"\"{cause.Name}\" has a row in the cause table and accounts for nothing in the six suites it "
                    + "counts, so the cause is spent. Delete the row — the rewrite will not, because the prose in it "
                    + "is hand-written and a generator may not throw away a paragraph somebody wrote.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Renders the table: the README's own prose, the measured figures, and the order the figures put them in.
    /// </summary>
    internal static string Render(IReadOnlyList<Row> stated, IReadOnlyDictionary<string, Counts> measured)
    {
        var rendered = new List<Row>(stated.Count);

        foreach (var row in stated)
        {
            var counts = measured.TryGetValue(row.Cause, out var found) ? found : default;
            rendered.Add(row with { Tests = counts.Tests, Documents = counts.Documents });
        }

        var table = new StringBuilder();
        table.Append(TableHeader).Append('\n');
        table.Append(TableDivider).Append('\n');

        // Descending by tests, then by documents, and stable over the order the README already had — so a
        // pair that ties keeps the place a person chose for it.
        foreach (var row in rendered.OrderByDescending(r => r.Tests).ThenByDescending(r => r.Documents))
        {
            table.Append($"| {Number(row.Tests)} | {Number(row.Documents)} | {row.Prose} |").Append('\n');
        }

        return table.ToString();
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static bool TryParseNumber(string cell, out int value) =>
        int.TryParse(cell.Trim().Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>Writes the rendered table over the one in the working tree's README.</summary>
    internal static string WriteReadmeTable(string table)
    {
        var path = WptBrowserCensus.LocateReadmeOnDisk();
        var original = File.ReadAllText(path);
        var crlf = original.Contains("\r\n", StringComparison.Ordinal);
        var lines = original.Split('\n');

        var (start, end) = Locate(lines);
        var rebuilt = new List<string>(lines.Length);
        rebuilt.AddRange(lines[..start]);
        rebuilt.AddRange(table.TrimEnd('\n').Split('\n'));
        rebuilt.AddRange(lines[end..]);

        var joined = string.Join("\n", rebuilt.ConvertAll(line => line.TrimEnd('\r')));
        File.WriteAllText(path, crlf ? joined.Replace("\n", "\r\n") : joined, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }
}
