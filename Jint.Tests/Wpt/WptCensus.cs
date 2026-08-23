#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// The corpus inventory table in <c>Vendor/README.md</c> — "The whole corpus, standard by standard" — as a
/// derived artifact rather than as prose somebody keeps up to date by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every figure in that table is arithmetic the driver already does.</b> A row's <c>Files</c> is the
/// vendored <c>.any.js</c> count under its directory, its <c>Suites</c> is how many theories cover them, and
/// its <c>Assertions</c> and <c>Not passing</c> are the totals the shim reported while the suite ran. Left as
/// prose, all four drift the moment a fix removes an exclusion or a corpus grows a file, and the drift is
/// invisible: nothing goes red, the table just quietly describes an engine that no longer exists. It has
/// happened — the README's own note beside the table records four rows carrying a number a later fix had
/// already improved, and a re-census correcting 269/40,617/2,980 to 270/40,631/2,907 in one go.
/// </para>
/// <para>
/// <b>What is checked when</b> splits along what each column depends on.
/// <see cref="WptCensusTests.TheInventoryTableNamesEveryStandardAndCountsItsFilesAndSuites"/> holds the
/// <c>Standard</c>, <c>Suites</c> and <c>Files</c> columns — those three are read off the embedded corpus and
/// need no execution at all, so it runs on every platform, in a filtered run, and for free.
/// <see cref="WptCensusTests.TheInventoryTableMatchesWhatTheCorpusMeasures"/> holds all five, which needs the
/// suites to have run. It runs on Windows only: assertion counts move per platform — the WebCryptoAPI row most
/// of all, which is what the per-OS exclusion scoping exists for — and the table states in its own first line
/// that it is measured on Windows.
/// </para>
/// <para>
/// <b>The measured check pays for at most one pass over the corpus, and usually much less.</b> Every outcome
/// the theories produce is handed here on the way back out of <see cref="WptHarness.Run"/>, so a file the
/// driver has already run is tallied rather than re-run. The two classes are separate collections and xUnit
/// runs them in parallel, so in a full <c>Jint.Tests</c> pass most of the corpus is already recorded by the
/// time the census reaches it. What it cannot reuse it runs itself, which is what makes the check work as a
/// standalone command as well: filter to that class and it censuses the corpus from cold.
/// </para>
/// <para>
/// <b>It is still opt-in, because "much less" is not nothing:</b> always-on, it measured a full
/// <c>Jint.Tests</c> pass at 65 s → 85 s. So it is gated on <see cref="UpdateVariable"/> — <c>1</c> checks the
/// table, <c>update</c> rewrites it — and the PR workflow's Windows leg is what sets it, in the same shape as
/// the <c>JINT_HOST_CONTRACT_VERIFICATION</c> leg beside it. An ordinary developer run pays only the free
/// half.
/// </para>
/// </remarks>
internal static class WptCensus
{
    /// <summary>
    /// The environment variable that turns the check into a rewrite.
    /// </summary>
    internal const string UpdateVariable = "JINT_WPT_CENSUS";

    /// <summary>
    /// The README, embedded so that verification reads the same bytes the build compiled and needs no source
    /// tree at all. Only the rewrite goes looking for the file on disk.
    /// </summary>
    private const string ReadmeResourceName = "wpt-vendor-readme.md";

    private const string TableHeading = "## The whole corpus, standard by standard";
    private const string TableHeader = "| Standard | Suites | Files | Assertions | Not passing |";
    private const string TableDivider = "| --- | --- | --- | --- | --- |";

    /// <summary>
    /// Which README row a vendored path belongs to, in the order the table lists them.
    /// </summary>
    /// <remarks>
    /// This is the one editorial half of the census: a directory prefix carries no standard's name, and two
    /// of the rows deliberately group several ("HTML" is three <c>html/webappapis/</c> suites in one row and
    /// <c>workers/</c> in another). The prefixes must still partition every vendored <c>.any.js</c> exactly —
    /// a corpus arriving under a directory no row claims fails the check rather than going uncounted, which is
    /// what stops a new standard being vendored with no row of its own.
    /// </remarks>
    private static readonly (string Standard, string Prefix)[] _standards =
    [
        ("URL", "url/"),
        ("URL Pattern", "urlpattern/"),
        ("Encoding", "encoding/"),
        ("Web Cryptography", "WebCryptoAPI/"),
        ("Streams", "streams/"),
        ("Compression", "compression/"),
        ("File API", "FileAPI/"),
        ("High Resolution Time", "hr-time/"),
        ("User Timing", "user-timing/"),
        ("HTML — workers", "workers/"),
        ("HTML — timers, microtasks, structured clone", "html/webappapis/"),
        ("DOM", "dom/"),
        ("Fetch", "fetch/api/"),
    ];

    private static readonly ConcurrentDictionary<string, Counts> _observed = new(StringComparer.Ordinal);

    /// <summary>
    /// What one file contributed to its standard's row.
    /// </summary>
    private readonly record struct Counts(int Assertions, int NotPassing);

    /// <summary>
    /// One line of the table. <paramref name="Suites"/> and <paramref name="Files"/> are read off the corpus;
    /// the other two are only meaningful once every file of the row has run.
    /// </summary>
    private readonly record struct Row(
        string Standard,
        string Prefix,
        int Suites,
        int Files,
        int Assertions,
        int NotPassing);

    /// <summary>
    /// Records what a suite file produced. Called from <see cref="WptHarness.Run"/>, which is the single
    /// funnel every vendored file's outcome comes back through — so the census sees the whole corpus without
    /// the theories knowing it exists, and re-running one file simply overwrites its own entry.
    /// </summary>
    internal static void Record(string testFilePath, WptRunOutcome outcome)
    {
        var notPassing = 0;
        foreach (var result in outcome.Results)
        {
            if (!result.Passed)
            {
                notPassing++;
            }
        }

        _observed[testFilePath] = new Counts(outcome.Results.Count, notPassing);
    }

    /// <summary>
    /// Every vendored <c>.any.js</c>, which is one theory case each and one row's worth of files.
    /// </summary>
    internal static List<string> VendoredTestFiles()
    {
        var files = new List<string>();
        foreach (var path in WptCorpus.Paths)
        {
            if (path.EndsWith(".any.js", StringComparison.Ordinal))
            {
                files.Add(path);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// Makes sure every vendored file has reported, running only the ones the driver has not already run.
    /// </summary>
    /// <returns>How many files this had to run itself, which is what the census actually cost.</returns>
    /// <remarks>
    /// A file the theories already covered is tallied from what they produced, so the two chains overlap
    /// rather than duplicate. Racing a theory on the same file is harmless: a suite file's outcome does not
    /// depend on when it ran — nothing in the corpus is timing-dependent, which is the same property the
    /// harness deadline relies on — so both writers record the same pair.
    /// </remarks>
    internal static int Measure()
    {
        var ran = 0;
        foreach (var file in VendoredTestFiles())
        {
            if (!_observed.ContainsKey(file))
            {
                // Records itself on the way back out.
                WptHarness.Run(file);
                ran++;
            }
        }

        return ran;
    }

    /// <summary>
    /// Renders the table. <paramref name="measured"/> false leaves the two counted columns out of the
    /// comparison by rendering whatever the README already claims for them, so the caller can hold the three
    /// derived columns without pretending to know the other two.
    /// </summary>
    internal static string Render(bool measured, IReadOnlyList<string>? readmeLines = null)
    {
        var claimed = measured ? null : ParseClaimedCounts(readmeLines ?? ReadReadme().Split('\n'));

        var files = VendoredTestFiles();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<Row>();

        foreach (var (standard, prefix) in _standards)
        {
            var suites = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;
            var assertions = 0;
            var notPassing = 0;

            foreach (var file in files)
            {
                if (!file.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                seen.Add(file);

                // A suite is a directory, because WptCorpus.TestFiles lists a directory's own files and never
                // descends — so the theory count of a standard is the number of directories its files sit in.
                suites.Add(WptCorpus.DirectoryOf(file));
                count++;

                if (measured)
                {
                    var counts = _observed[file];
                    assertions += counts.Assertions;
                    notPassing += counts.NotPassing;
                }
            }

            if (!measured && claimed!.TryGetValue(standard, out var already))
            {
                (assertions, notPassing) = already;
            }

            rows.Add(new Row(standard, prefix, suites.Count, count, assertions, notPassing));
        }

        var unclaimed = new List<string>();
        foreach (var file in files)
        {
            if (!seen.Contains(file))
            {
                unclaimed.Add(file);
            }
        }

        if (unclaimed.Count > 0)
        {
            throw new InvalidOperationException(
                "These vendored files belong to no census row, so no line of the inventory table counts them. "
                + $"Add the standard to {nameof(WptCensus)}.{nameof(_standards)} and give it a row:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, unclaimed));
        }

        return RenderTable(rows);
    }

    private static string RenderTable(List<Row> rows)
    {
        var table = new StringBuilder();
        table.Append(TableHeader).Append('\n');
        table.Append(TableDivider).Append('\n');

        var suites = 0;
        var files = 0;
        var assertions = 0;
        var notPassing = 0;

        foreach (var row in rows)
        {
            var scope = row.Suites == 1 ? $"`{row.Prefix}`" : $"`{row.Prefix}` ×{row.Suites}";
            table.Append(
                    $"| {row.Standard} | {scope} | {Number(row.Files)} | {Number(row.Assertions)} | {Number(row.NotPassing)} |")
                .Append('\n');

            suites += row.Suites;
            files += row.Files;
            assertions += row.Assertions;
            notPassing += row.NotPassing;
        }

        table.Append(
                $"| **total** | **{Number(suites)}** | **{Number(files)}** | **{Number(assertions)}** | **{Number(notPassing)}** |")
            .Append('\n');

        return table.ToString();
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// The two measured columns as the README currently states them, keyed by standard, so a check that is
    /// not entitled to an opinion on them can carry them through unchanged.
    /// </summary>
    private static Dictionary<string, (int Assertions, int NotPassing)> ParseClaimedCounts(IReadOnlyList<string> lines)
    {
        var claimed = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

        foreach (var line in TableLines(lines))
        {
            var cells = Cells(line);
            if (cells.Length != 5 || cells[0].StartsWith("**", StringComparison.Ordinal) || cells[0] == "Standard"
                || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseNumber(cells[3], out var assertions) && TryParseNumber(cells[4], out var notPassing))
            {
                claimed[cells[0]] = (assertions, notPassing);
            }
        }

        return claimed;
    }

    private static bool TryParseNumber(string cell, out int value) =>
        int.TryParse(cell.Replace(",", "").Trim('*'), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static string[] Cells(string line) =>
        Array.ConvertAll(line.Trim().Trim('|').Split('|'), cell => cell.Trim());

    /// <summary>
    /// The table's own lines, which are every consecutive pipe-prefixed line from the header on. The heading
    /// above it is what makes the search unambiguous — the README carries a second table, of what is
    /// deliberately not vendored.
    /// </summary>
    private static IEnumerable<string> TableLines(IReadOnlyList<string> lines)
    {
        var (start, end) = Locate(lines);
        for (var i = start; i < end; i++)
        {
            yield return lines[i];
        }
    }

    /// <summary>
    /// Where the table sits in the README, as a half-open line range.
    /// </summary>
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
                $"Vendor/README.md no longer has a \"{TableHeading}\" section, which is where the census writes.");
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
            $"Vendor/README.md's \"{TableHeading}\" section no longer holds a \"{TableHeader}\" table.");
    }

    /// <summary>
    /// The inventory table exactly as the README states it, normalised to LF and ending in one newline.
    /// </summary>
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
        var assembly = typeof(WptCensus).Assembly;
        using var stream = assembly.GetManifestResourceStream(ReadmeResourceName)
            ?? throw new FileNotFoundException($"Embedded resource \"{ReadmeResourceName}\" is missing.", ReadmeResourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Whether <see cref="UpdateVariable"/> asked for the census at all — <c>1</c>, <c>true</c> or
    /// <c>update</c>. Unset is the default, and the reason the measured check costs a normal run nothing.
    /// </summary>
    internal static bool CensusRequested()
    {
        var value = Environment.GetEnvironmentVariable(UpdateVariable);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "update", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether it asked for the table to be rewritten rather than checked.
    /// </summary>
    internal static bool UpdateRequested() =>
        string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "update", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the rendered table over the one in the working tree's <c>Vendor/README.md</c>, preserving the
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

    /// <summary>
    /// Finds the README in the working tree. The test runs out of <c>artifacts/bin/…</c>, so this walks up
    /// looking for the repository rather than assuming a relative depth.
    /// </summary>
    private static string LocateReadmeOnDisk()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Jint.Tests", "Wpt", "Vendor", "README.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"{UpdateVariable} asked the census to rewrite Vendor/README.md, but no source tree was found above "
            + $"\"{AppContext.BaseDirectory}\". Run the suites from the repository to update the table.");
    }

    /// <summary>
    /// The message a mismatch reports: the two tables side by side, and how to rewrite it.
    /// </summary>
    internal static string Explain(string expected, string actual, string what)
    {
        var message = new StringBuilder();
        message.Append("Vendor/README.md's inventory table is out of date (").Append(what).AppendLine(").");
        message.AppendLine();
        message.AppendLine("It says:");
        message.AppendLine(actual.TrimEnd('\n'));
        message.AppendLine();
        message.AppendLine("The corpus says:");
        message.AppendLine(expected.TrimEnd('\n'));
        message.AppendLine();
        message.Append($"Rewrite it by running the WPT suites with {UpdateVariable}=update set.");
        return message.ToString();
    }
}
#endif
