using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// The lane's inventory table in <c>Wpt/README.md</c> — "The lane, suite by suite" — as a derived artifact
/// rather than as prose somebody keeps up to date by hand.
/// </summary>
/// <remarks>
/// <para>
/// Every figure in that table is arithmetic the driver already does. A row's <c>Documents</c> is the vendored
/// <c>.html</c> count under its suite, its <c>Synthesized</c> is how many <c>.any.html</c> wrappers the server
/// makes for that suite's scripts, and its <c>Tests</c> and <c>Not passing</c> are the totals the harness
/// reported while the suite ran. Left as prose, all four drift the moment a fix removes an exclusion or a
/// corpus bump adds a file, and the drift is invisible: nothing goes red, the table just quietly describes a
/// browser that no longer exists. That is the failure
/// <see href="https://github.com/sebastienros/jint/issues/3339">#3339</see> records for the engine lane's
/// table, and this one is built the same way so it cannot repeat.
/// </para>
/// <para>
/// <b>Three equalities and a ceiling.</b> <c>Documents</c> and <c>Synthesized</c> are read off the embedded
/// corpus, and <c>Tests</c> counts <i>registrations</i> — a document registers its cases as its scripts run,
/// and a document that reports at all reports every one of them, because a document that cannot is a harness
/// error its own suite already fails on. <c>Not passing</c> is the only column that counts <i>outcomes</i>,
/// so a rise fails as a regression naming the suite and the size of it, a fall fails as staleness, and
/// <see cref="UpdateVariable"/><c>=update</c> lowers that figure and refuses to raise it. A check satisfiable
/// by re-baselining a bad run is not a ceiling, it is a suggestion.
/// </para>
/// <para>
/// <b>It is opt-in</b>, like the engine lane's and for the same reason: totalling the table means running
/// every document, and although this reuses every outcome the theories already produced, a filtered run would
/// otherwise pay for the whole lane. The PR workflow's Windows leg is where the table is enforced.
/// </para>
/// </remarks>
internal static class WptBrowserCensus
{
    /// <summary>The environment variable that turns the check on, and <c>update</c> into a rewrite.</summary>
    internal const string UpdateVariable = "JINT_WPT_BROWSER_CENSUS";

    /// <summary>
    /// The one spelling that may write a <i>larger</i> not-passing figure, for a corpus bump that genuinely
    /// arrives with new failures. It cannot be typed by accident and it leaves the raised numbers in the diff.
    /// </summary>
    internal const string RaiseVariableValue = "update-raising-the-ceiling";

    private const string ReadmeResourceName = "wpt-browser-readme.md";

    private const string TableHeading = "## The lane, suite by suite";
    private const string TableHeader = "| Suite | Documents | Synthesized | Tests | Not passing |";
    private const string TableDivider = "| --- | --- | --- | --- | --- |";

    private static readonly ConcurrentDictionary<string, Counts> _observed = new(StringComparer.Ordinal);

    /// <summary>What one case contributed to its suite's row.</summary>
    private readonly record struct Counts(int Tests, int NotPassing);

    /// <summary>One line of the table, as the corpus and the run make it.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Row(string Suite, int Documents, int Synthesized, int Tests, int NotPassing);

    /// <summary>One line of a rendered table, read back so a difference can be named column by column.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TableRow(string Suite, int Documents, int Synthesized, int Tests, int NotPassing);

    /// <summary>
    /// How a measured table differs from a stated one, split by what each kind of difference <i>means</i> —
    /// which is the point, because the three call for three different things to be done.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Differences(List<string> Moved, List<string> Regressed, List<string> Stale)
    {
        internal bool Any => Moved.Count > 0 || Regressed.Count > 0 || Stale.Count > 0;
    }

    /// <summary>
    /// Records what one case produced. Called from the runner, which is the single funnel every case's
    /// outcome comes back through — so the census sees the whole lane without the theories knowing it exists,
    /// and re-running one document simply overwrites its own entry.
    /// </summary>
    internal static void Record(string path, WptBrowserOutcome outcome)
    {
        var notPassing = 0;
        foreach (var result in outcome.Results)
        {
            if (!result.Passed)
            {
                notPassing++;
            }
        }

        _observed[path] = new Counts(outcome.Results.Count, notPassing);
    }

    /// <summary>
    /// Makes sure every case has reported, running only the ones the theories have not already run.
    /// </summary>
    /// <returns>How many documents this had to run itself, which is what the census actually cost.</returns>
    internal static async Task<int> MeasureAsync()
    {
        var ran = 0;

        foreach (var suite in WptCorpus.BrowserSuites)
        {
            foreach (var path in WptBrowserCorpus.Cases(suite))
            {
                if (_observed.ContainsKey(path))
                {
                    continue;
                }

                // Records itself on the way back out.
                await WptBrowserHarness.Instance.RunAsync(path).ConfigureAwait(false);
                ran++;
            }
        }

        return ran;
    }

    /// <summary>
    /// Renders the table. <paramref name="measured"/> false leaves the two counted columns out of the
    /// comparison by rendering whatever the README already claims for them, so a caller that has not run the
    /// lane can still hold the two derived columns.
    /// </summary>
    internal static string Render(bool measured, IReadOnlyList<string>? readmeLines = null)
    {
        var claimed = measured ? null : ParseClaimedCounts(readmeLines ?? ReadReadme().Split('\n'));
        var rows = new List<Row>();

        foreach (var suite in WptCorpus.BrowserSuites)
        {
            var documents = 0;
            var synthesized = 0;
            var tests = 0;
            var notPassing = 0;

            foreach (var path in WptBrowserCorpus.Cases(suite))
            {
                if (WptBrowserCorpus.IsVendored(path))
                {
                    documents++;
                }
                else
                {
                    synthesized++;
                }

                if (measured)
                {
                    var counts = _observed[path];
                    tests += counts.Tests;
                    notPassing += counts.NotPassing;
                }
            }

            if (!measured && claimed!.TryGetValue(suite, out var already))
            {
                (tests, notPassing) = already;
            }

            rows.Add(new Row(suite, documents, synthesized, tests, notPassing));
        }

        return RenderTable(rows);
    }

    private static string RenderTable(List<Row> rows)
    {
        var table = new StringBuilder();
        table.Append(TableHeader).Append('\n');
        table.Append(TableDivider).Append('\n');

        var documents = 0;
        var synthesized = 0;
        var tests = 0;
        var notPassing = 0;

        foreach (var row in rows)
        {
            table.Append(
                    $"| `{row.Suite}/` | {Number(row.Documents)} | {Number(row.Synthesized)} | {Number(row.Tests)} | {Number(row.NotPassing)} |")
                .Append('\n');

            documents += row.Documents;
            synthesized += row.Synthesized;
            tests += row.Tests;
            notPassing += row.NotPassing;
        }

        table.Append(
                $"| **total** | **{Number(documents)}** | **{Number(synthesized)}** | **{Number(tests)}** | **{Number(notPassing)}** |")
            .Append('\n');

        return table.ToString();
    }

    /// <summary>What the lane measured for the named suites, totalled.</summary>
    /// <remarks>
    /// The same three quantities the table renders, for a caller that wants to hold a <i>sentence</i> to them
    /// rather than a row — <c>WptBrowserCauseTests</c> is the one, and the sentence is the one above the cause
    /// table. Read from the corpus and the run, never from the table, so the two cannot agree by copying.
    /// </remarks>
    internal static (int Documents, int Tests, int NotPassing) MeasuredTotals(IReadOnlyList<string> suites)
    {
        var documents = 0;
        var tests = 0;
        var notPassing = 0;

        foreach (var suite in suites)
        {
            var name = suite.TrimEnd('/');

            foreach (var path in WptBrowserCorpus.Cases(name))
            {
                if (WptBrowserCorpus.IsVendored(path))
                {
                    documents++;
                }

                if (_observed.TryGetValue(path, out var counts))
                {
                    tests += counts.Tests;
                    notPassing += counts.NotPassing;
                }
            }
        }

        return (documents, tests, notPassing);
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static Dictionary<string, (int Tests, int NotPassing)> ParseClaimedCounts(IReadOnlyList<string> lines)
    {
        var claimed = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

        foreach (var row in ParseRows(ReadmeTable(lines)))
        {
            claimed[row.Suite] = (row.Tests, row.NotPassing);
        }

        return claimed;
    }

    private static List<TableRow> ParseRows(string table)
    {
        var rows = new List<TableRow>();

        foreach (var rawLine in table.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = Cells(line);
            if (cells.Length != 5
                || !TryParseNumber(cells[1], out var documents)
                || !TryParseNumber(cells[2], out var synthesized)
                || !TryParseNumber(cells[3], out var tests)
                || !TryParseNumber(cells[4], out var notPassing))
            {
                // The header and the divider, which carry no figures.
                continue;
            }

            rows.Add(new TableRow(cells[0].Trim('*', '`').TrimEnd('/'), documents, synthesized, tests, notPassing));
        }

        return rows;
    }

    private static bool TryParseNumber(string cell, out int value) =>
        int.TryParse(cell.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static string[] Cells(string line) =>
        Array.ConvertAll(line.Trim().Trim('|').Split('|'), static cell => cell.Trim());

    private static IEnumerable<string> TableLines(IReadOnlyList<string> lines)
    {
        var (start, end) = Locate(lines);
        for (var i = start; i < end; i++)
        {
            yield return lines[i];
        }
    }

    /// <summary>Where the table sits in the README, as a half-open line range.</summary>
    internal static (int Start, int End) Locate(IReadOnlyList<string> lines)
    {
        var heading = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].TrimEnd('\r'), TableHeading, StringComparison.Ordinal))
            {
                heading = i;
                break;
            }
        }

        if (heading < 0)
        {
            throw new InvalidOperationException(
                $"Wpt/README.md no longer has a \"{TableHeading}\" section, which is where the census writes.");
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
            $"Wpt/README.md's \"{TableHeading}\" section no longer holds a \"{TableHeader}\" table.");
    }

    /// <summary>The table exactly as the README states it, normalised to LF and ending in one newline.</summary>
    internal static string ReadmeTable(IReadOnlyList<string>? lines = null)
    {
        lines ??= ReadReadme().Split('\n');

        var table = new StringBuilder();
        foreach (var line in TableLines(lines))
        {
            table.Append(line.TrimEnd('\r')).Append('\n');
        }

        return table.ToString();
    }

    internal static string ReadReadme()
    {
        var assembly = typeof(WptBrowserCensus).Assembly;
        using var stream = assembly.GetManifestResourceStream(ReadmeResourceName)
            ?? throw new FileNotFoundException($"Embedded resource \"{ReadmeResourceName}\" is missing.", ReadmeResourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Whether <see cref="UpdateVariable"/> asked for the census at all.</summary>
    internal static bool CensusRequested()
    {
        var value = Environment.GetEnvironmentVariable(UpdateVariable);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || UpdateRequested();
    }

    /// <summary>Whether it asked for the table to be rewritten rather than checked, in either spelling.</summary>
    internal static bool UpdateRequested()
    {
        var value = Environment.GetEnvironmentVariable(UpdateVariable);
        return string.Equals(value, "update", StringComparison.OrdinalIgnoreCase) || RaiseRequested();
    }

    /// <summary>Whether the rewrite was told, in as many words, that it may raise the ceiling.</summary>
    internal static bool RaiseRequested() =>
        string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), RaiseVariableValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the rendered table over the one in the working tree's <c>Wpt/README.md</c>, preserving the
    /// file's own line endings.
    /// </summary>
    internal static string WriteReadmeTable(string table)
    {
        var path = LocateReadmeOnDisk();
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

    internal static string LocateReadmeOnDisk()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Jint.Tests.Browser", "Wpt", "README.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"{UpdateVariable} asked the census to rewrite Wpt/README.md, but no source tree was found above "
            + $"\"{AppContext.BaseDirectory}\". Run the lane from the repository to update the table.");
    }

    private static Differences Compare(string measured, string stated, bool countsIncluded)
    {
        var differences = new Differences([], [], []);

        var claimed = new Dictionary<string, TableRow>(StringComparer.Ordinal);
        foreach (var row in ParseRows(stated))
        {
            claimed[row.Suite] = row;
        }

        foreach (var row in ParseRows(measured))
        {
            if (!claimed.Remove(row.Suite, out var claim))
            {
                differences.Moved.Add($"{row.Suite}: the lane has this suite and the table has no row for it");
                continue;
            }

            if (row.Documents != claim.Documents)
            {
                differences.Moved.Add($"{row.Suite}: documents {Number(claim.Documents)} -> {Number(row.Documents)}");
            }

            if (row.Synthesized != claim.Synthesized)
            {
                differences.Moved.Add($"{row.Suite}: synthesized {Number(claim.Synthesized)} -> {Number(row.Synthesized)}");
            }

            if (!countsIncluded)
            {
                continue;
            }

            if (row.Tests != claim.Tests)
            {
                differences.Moved.Add($"{row.Suite}: tests {Number(claim.Tests)} -> {Number(row.Tests)}");
            }

            var delta = row.NotPassing - claim.NotPassing;
            if (delta > 0)
            {
                differences.Regressed.Add(
                    $"{row.Suite}: not passing {Number(claim.NotPassing)} -> {Number(row.NotPassing)} (+{Number(delta)})");
            }
            else if (delta < 0)
            {
                differences.Stale.Add(
                    $"{row.Suite}: not passing {Number(claim.NotPassing)} -> {Number(row.NotPassing)} (-{Number(-delta)})");
            }
        }

        foreach (var orphan in claimed.Values)
        {
            differences.Moved.Add($"{orphan.Suite}: the table has a row for it and the lane does not");
        }

        return differences;
    }

    /// <summary>
    /// What a check reports, or <see langword="null"/> when the table is what the lane says it is.
    /// </summary>
    internal static string? Reconcile(string measured, string stated, bool countsIncluded)
    {
        var differences = Compare(measured, stated, countsIncluded);
        if (!differences.Any)
        {
            return null;
        }

        var message = new StringBuilder();

        if (differences.Regressed.Count > 0)
        {
            message.AppendLine(
                "The web-platform-tests browser lane now fails more than Wpt/README.md's table allows:");
            message.AppendLine();
            AppendAll(message, differences.Regressed);
            message.AppendLine();
            message.AppendLine(
                "That column is a ceiling, not a baseline. A rise is a regression to find — either in the "
                + "engine, in Jint.Browser, or in a document whose outcome depends on the machine it ran on — "
                + $"and {UpdateVariable}=update refuses to write the larger figure, so re-censusing is not the "
                + "way out of this one.");
        }

        if (differences.Stale.Count > 0)
        {
            Separate(message);
            message.AppendLine(
                "Wpt/README.md's table is out of date: the lane fails less than it states, so the table "
                + "describes a browser that no longer exists.");
            message.AppendLine();
            AppendAll(message, differences.Stale);
            message.AppendLine();
            message.AppendLine($"Rewrite it by running the lane with {UpdateVariable}=update set.");
        }

        if (differences.Moved.Count > 0)
        {
            Separate(message);
            message.AppendLine("These columns are derived from the corpus and have to match it exactly:");
            message.AppendLine();
            AppendAll(message, differences.Moved);
            message.AppendLine();
            message.AppendLine($"Rewrite the table by running the lane with {UpdateVariable}=update set.");
        }

        return message.ToString().TrimEnd();
    }

    /// <summary>
    /// Why <c>update</c> will not write this run's table, or <see langword="null"/> when it may.
    /// </summary>
    /// <remarks>
    /// This is the half that makes the ceiling mean anything. A check that goes red on a rise but can be
    /// satisfied by running the rewrite is not a ceiling; so the rewrite lowers a not-passing figure and never
    /// raises one, and a rise that is genuinely intended is spelled <see cref="RaiseVariableValue"/>.
    /// </remarks>
    internal static string? RefusalToRaise(string measured, string stated)
    {
        var differences = Compare(measured, stated, countsIncluded: true);
        if (differences.Regressed.Count == 0)
        {
            return null;
        }

        var message = new StringBuilder();
        message.AppendLine(
            $"{UpdateVariable}=update will not raise a not-passing figure, and this run measured more failures "
            + "than Wpt/README.md's table allows:");
        message.AppendLine();
        AppendAll(message, differences.Regressed);
        message.AppendLine();
        message.AppendLine(
            "Find the regression instead. If the rise is genuinely intended — a corpus bump that arrives with "
            + $"new failures — say so out loud: {UpdateVariable}={RaiseVariableValue}.");

        return message.ToString().TrimEnd();
    }

    private static void AppendAll(StringBuilder message, List<string> lines)
    {
        foreach (var line in lines)
        {
            message.Append("  ").AppendLine(line);
        }
    }

    private static void Separate(StringBuilder message)
    {
        if (message.Length > 0)
        {
            message.AppendLine();
        }
    }
}
