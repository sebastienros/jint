#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
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
/// driver has already run is tallied rather than re-run. The two classes are separate fixtures and the
/// runner runs them in parallel, so in a full <c>Jint.Tests</c> pass most of the corpus is already recorded by the
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
/// <para>
/// <b>Four of the five columns are equalities and the fifth is a ceiling</b>, and the split is what each
/// column counts rather than a concession. <c>Standard</c>, <c>Suites</c> and <c>Files</c> are read off the
/// embedded corpus, and <c>Assertions</c> counts <i>registrations</i> — a suite registers its cases at file
/// scope, so a file that reports at all reports every one of them, and a file that cannot report at all is a
/// harness error its own suite already fails on. <c>Not passing</c> is the only column that counts
/// <i>outcomes</i>, which is why it is the only one a loaded machine has ever moved:
/// <see href="https://github.com/sebastienros/jint/issues/3339">#3339</see> records it reddening three
/// unrelated pull requests in one afternoon, one of which changed nothing but three markdown headings. So a
/// rise fails as a regression, naming the suite and the size of it, and a fall fails as staleness, which is
/// the one case where "the table is out of date" is the truthful sentence. What a rise must never be is
/// something an author can make go away: <see cref="UpdateVariable"/><c>=update</c> refuses to write a larger
/// not-passing figure — see <see cref="RefusalToRaise"/> — because a check satisfiable by re-baselining a bad
/// run would ratchet the corpus quietly worse, which is the opposite of what this artefact is for.
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
        ("Performance Timeline", "performance-timeline/"),
        ("HTML — workers", "workers/"),
        ("HTML — timers, microtasks, structured clone", "html/webappapis/"),
        ("DOM", "dom/"),
        ("Fetch", "fetch/api/"),
        ("XMLHttpRequest", "xhr/"),
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

    // ---------------------------------------------------------------------------------------------
    // The drift recipe, one section further down the same README, held the same way and for the same
    // reason: a verification statement somebody typed is one nobody notices going stale.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The heading the generated drift block sits under, which is what makes the marker search unambiguous.
    /// </summary>
    private const string DriftHeading = "### Verifying that nothing has drifted";

    private const string DriftBeginMarker = "<!-- generated by WptCensus.RenderDrift: the drift recipe, and the corpus it walks -->";
    private const string DriftEndMarker = "<!-- end generated -->";

    /// <summary>
    /// The drift-verification recipe and the size of the corpus it walks, rendered from what is vendored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three parts of that recipe are facts about the corpus rather than prose: which file extensions the
    /// walk has to name to reach every vendored file, how many directories it therefore asks upstream about,
    /// and how large the tree it walked was. All three move whenever a suite is vendored, and all three were
    /// four suites out of date when <see href="https://github.com/sebastienros/jint/issues/3647">#3647</see>
    /// was filed — a verification claiming 359 files in 48 directories, against a corpus half as large again.
    /// A claim about what was checked is exactly the kind that must not be typed, so it is written from
    /// <see cref="WptCorpus.Paths"/> and held like the inventory table above it.
    /// </para>
    /// <para>
    /// The extension list is the half that was also <i>wrong</i> rather than merely stale: it named five
    /// extensions where the corpus vendors eight, so the <c>.asis</c> responses, the <c>.headers</c> sidecars
    /// and the two payload files the xhr suites read were outside every drift check ever run. Deriving it
    /// closes that for the next corpus too.
    /// </para>
    /// <para>
    /// What the block deliberately does <b>not</b> state is the outcome. Comparing against upstream needs the
    /// network and a token, which a unit test has neither of; the sentence after the block is a run somebody
    /// really did, and these figures are what say which corpus it was a run of — so when they move, that
    /// sentence is about a smaller tree and the walk has to be run again.
    /// </para>
    /// </remarks>
    internal static string RenderDrift()
    {
        var extensions = new SortedSet<string>(StringComparer.Ordinal);
        var directories = new HashSet<string>(StringComparer.Ordinal);
        var files = 0;
        var documents = 0;

        foreach (var path in WptCorpus.Paths)
        {
            files++;
            directories.Add(WptCorpus.DirectoryOf(path));
            extensions.Add(Path.GetExtension(path));

            if (WptCorpus.IsBrowserTestFile(path))
            {
                documents++;
            }
        }

        var types = new StringBuilder();
        foreach (var extension in extensions)
        {
            if (types.Length > 0)
            {
                types.Append(" -o ");
            }

            types.Append("-name *").Append(extension);
        }

        // A line of the block that begins with '#' would be read as a preprocessor directive rather than as a
        // shell comment: this whole file sits inside `#if NET8_0_OR_GREATER`, and the lexer scans a *disabled*
        // region for directives, where "# every extension" is CS1024. So the recipe's two comments open with an
        // interpolation.
        const string Comment = "#";

        // One trailing newline, because the block is compared with the README's own lines and each of those
        // carries one — and the whole thing normalised to LF, because a raw string literal keeps the source
        // file's line endings and this one is compared with a directory `.gitattributes` pins to LF.
        var block = $$"""
                 The recipe below is generated from the corpus: the extensions it names, the number of calls it makes and
                 the size of the tree it walks are read off what is vendored rather than typed, so it can never come to
                 cover less than the corpus holds. `JINT_WPT_CENSUS=update` rewrites it, and `WptCensusTests` fails
                 while it is stale.

                 ```bash
                 cd Jint.Tests/Wpt/Vendor
                 SHA=$(grep -oE '\b[0-9a-f]{40}\b' README.md | head -1)

                 {{Comment}} every extension the corpus vendors, so a bump that brings a new one in is walked rather than skipped
                 TYPES='{{types}}'

                 {{Comment}} one call per directory that holds a vendored file ({{Number(directories.Count)}} at this pin)
                 for d in $(find . -type f \( $TYPES \) -printf '%h\n' | sort -u | sed 's|^\./||'); do
                   gh api "repos/web-platform-tests/wpt/contents/$d?ref=$SHA" \
                      --jq '.[] | select(.type=="file") | "\(.sha) \(.path)"'
                 done > /tmp/upstream.txt

                 find . -type f \( $TYPES \) | sort | while read -r f; do
                   rel="${f#./}"
                   up=$(grep -m1 " ${rel}$" /tmp/upstream.txt | cut -d' ' -f1)
                   [ -z "$up" ] && { echo "NOT-IN-UPSTREAM: $rel"; continue; }
                   [ "$(git hash-object "$f")" = "$up" ] || echo "DRIFT: $rel"
                 done
                 ```

                 Silence is a clean corpus, and at this pin there are {{Number(files)}} files in {{Number(directories.Count)}} directories to be silent
                 about — {{Number(documents)}} of them the documents the browser lane navigates to, the rest the scripts, payloads and
                 sidecars every lane reads.

                 """;

        return block.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// The generated drift block exactly as the README states it, normalised to LF.
    /// </summary>
    internal static string ReadmeDrift(IReadOnlyList<string>? lines = null)
    {
        lines ??= ReadReadme().Split('\n');

        var (start, end) = LocateDrift(lines);
        var block = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            block.Append(lines[i].TrimEnd('\r')).Append('\n');
        }

        return block.ToString();
    }

    /// <summary>
    /// Where the generated drift block sits in the README, as a half-open line range between its markers.
    /// </summary>
    /// <remarks>
    /// Markers rather than a heading-and-header search, because unlike the inventory table this block has no
    /// shape of its own to find: it is a paragraph, a fenced script and a sentence, any of which a reader
    /// could reasonably reword. The markers say which of them may not be reworded by hand.
    /// </remarks>
    internal static (int Start, int End) LocateDrift(IReadOnlyList<string> lines)
    {
        var heading = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].TrimEnd('\r'), DriftHeading, StringComparison.Ordinal))
            {
                heading = i;
                break;
            }
        }

        if (heading < 0)
        {
            throw new InvalidOperationException(
                $"Vendor/README.md no longer has a \"{DriftHeading}\" section, which is where the drift recipe is written.");
        }

        var begin = -1;
        for (var i = heading; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].TrimEnd('\r'), DriftBeginMarker, StringComparison.Ordinal))
            {
                begin = i;
                break;
            }
        }

        if (begin < 0)
        {
            throw new InvalidOperationException(
                $"Vendor/README.md's \"{DriftHeading}\" section no longer holds the \"{DriftBeginMarker}\" marker.");
        }

        for (var i = begin + 1; i < lines.Count; i++)
        {
            if (!string.Equals(lines[i].TrimEnd('\r'), DriftEndMarker, StringComparison.Ordinal))
            {
                continue;
            }

            // The blank lines against the markers stay outside the region, so a rewrite leaves them where
            // they are: a comment marker needs one after it or the paragraph below becomes part of an HTML
            // block and stops being rendered as markdown at all.
            var start = begin + 1;
            var end = i;
            while (start < end && lines[start].TrimEnd('\r').Length == 0)
            {
                start++;
            }

            while (end > start && lines[end - 1].TrimEnd('\r').Length == 0)
            {
                end--;
            }

            return (start, end);
        }

        throw new InvalidOperationException(
            $"Vendor/README.md opens the drift recipe with \"{DriftBeginMarker}\" and never closes it with \"{DriftEndMarker}\".");
    }

    /// <summary>
    /// How a rendered drift block differs from the one the README states, as the lines that moved.
    /// </summary>
    internal static string? ReconcileDrift(string rendered, string stated)
    {
        if (string.Equals(rendered, stated, StringComparison.Ordinal))
        {
            return null;
        }

        var message = new StringBuilder();
        message.Append("Vendor/README.md's drift-verification recipe is out of date: it does not say what the corpus ")
            .Append("is now. It is generated rather than written, so the fix is to run it again rather than to edit it:")
            .Append(Environment.NewLine)
            .Append(Environment.NewLine)
            .Append($"    {UpdateVariable}=update dotnet test Jint.Tests/Jint.Tests.csproj -c Release --filter \"FullyQualifiedName~WptCensusTests\"")
            .Append(Environment.NewLine)
            .Append(Environment.NewLine)
            .Append("A figure that moved is a corpus that moved, so the verification sentence under the block is ")
            .Append("about a different tree and the comparison against upstream has to be run again too.")
            .Append(Environment.NewLine);

        var expected = rendered.TrimEnd('\n').Split('\n');
        var actual = stated.TrimEnd('\n').Split('\n');

        Separate(message);
        message.Append("The lines that differ, generated against stated:").Append(Environment.NewLine);

        for (var i = 0; i < Math.Max(expected.Length, actual.Length); i++)
        {
            var left = i < expected.Length ? expected[i] : "(nothing)";
            var right = i < actual.Length ? actual[i] : "(nothing)";
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                continue;
            }

            message.Append(Environment.NewLine)
                .Append("  line ").Append(i + 1).Append(':').Append(Environment.NewLine)
                .Append("    generated: ").Append(left).Append(Environment.NewLine)
                .Append("    stated:    ").Append(right).Append(Environment.NewLine);
        }

        return message.ToString();
    }

    /// <summary>
    /// The one spelling of <see cref="UpdateVariable"/> that may write a <i>larger</i> not-passing figure.
    /// See <see cref="RefusalToRaise"/> for why raising the ceiling has to be said out loud.
    /// </summary>
    internal const string RaiseVariableValue = "update-raising-the-ceiling";

    /// <summary>
    /// Whether <see cref="UpdateVariable"/> asked for the census at all — <c>1</c>, <c>true</c>, <c>update</c>
    /// or <see cref="RaiseVariableValue"/>. Unset is the default, and the reason the measured check costs a
    /// normal run nothing.
    /// </summary>
    internal static bool CensusRequested()
    {
        var value = Environment.GetEnvironmentVariable(UpdateVariable);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || UpdateRequested();
    }

    /// <summary>
    /// Whether it asked for the table to be rewritten rather than checked, in either spelling.
    /// </summary>
    internal static bool UpdateRequested()
    {
        var value = Environment.GetEnvironmentVariable(UpdateVariable);
        return string.Equals(value, "update", StringComparison.OrdinalIgnoreCase) || RaiseRequested();
    }

    /// <summary>
    /// Whether the rewrite was told, in as many words, that it may raise the ceiling.
    /// </summary>
    internal static bool RaiseRequested() =>
        string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), RaiseVariableValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the rendered table over the one in the working tree's <c>Vendor/README.md</c>, preserving the
    /// file's own line endings.
    /// </summary>
    internal static string WriteReadmeTable(string table) => WriteRegion(table, Locate);

    /// <summary>
    /// Writes the rendered drift recipe over the one between the markers in the working tree's
    /// <c>Vendor/README.md</c>.
    /// </summary>
    internal static string WriteReadmeDrift(string block) => WriteRegion(block, LocateDrift);

    /// <summary>
    /// Replaces one located region of the README, preserving the file's own line endings.
    /// </summary>
    private static string WriteRegion(string replacement, Func<IReadOnlyList<string>, (int Start, int End)> locate)
    {
        var path = LocateReadmeOnDisk();
        var original = File.ReadAllText(path);
        var crlf = original.Contains("\r\n", StringComparison.Ordinal);
        var lines = original.Split('\n');

        var (start, end) = locate(lines);
        var rebuilt = new List<string>(lines.Length);
        rebuilt.AddRange(lines[..start]);
        rebuilt.AddRange(replacement.TrimEnd('\n').Split('\n'));
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
    /// One line of a rendered table, read back so that two of them can be compared column by column and a
    /// difference can be named instead of a whole table being printed twice.
    /// </summary>
    /// <remarks>
    /// <c>Suites</c> stays text because that cell is not a number in a row — <c>`WebCryptoAPI/` ×8</c> — while
    /// it is one in the total; comparing it as written is right for both. The total row parses as an ordinary
    /// row called <c>total</c>, which is what keeps a table whose rows and total disagree failing.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TableRow(
        string Standard,
        string Suites,
        int Files,
        int Assertions,
        int NotPassing);

    /// <summary>
    /// How a measured table differs from a stated one, split by what each kind of difference <i>means</i> —
    /// which is the whole point, because the three call for three different things to be done.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Differences(
        List<string> Moved,
        List<string> Regressed,
        List<string> Stale)
    {
        internal bool Any => Moved.Count > 0 || Regressed.Count > 0 || Stale.Count > 0;
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
                || !TryParseNumber(cells[2], out var files)
                || !TryParseNumber(cells[3], out var assertions)
                || !TryParseNumber(cells[4], out var notPassing))
            {
                // The header and the divider, which carry no figures.
                continue;
            }

            rows.Add(new TableRow(cells[0].Trim('*'), cells[1], files, assertions, notPassing));
        }

        return rows;
    }

    /// <summary>
    /// Compares what the corpus measures against what the README states, column by column.
    /// </summary>
    /// <param name="countsIncluded">
    /// Whether the caller is entitled to an opinion on <c>Assertions</c> and <c>Not passing</c>. False for the
    /// free check, which has not run the suites and so knows nothing about either.
    /// </param>
    private static Differences Compare(string measured, string stated, bool countsIncluded)
    {
        var differences = new Differences([], [], []);

        var claimed = new Dictionary<string, TableRow>(StringComparer.Ordinal);
        foreach (var row in ParseRows(stated))
        {
            claimed[row.Standard] = row;
        }

        foreach (var row in ParseRows(measured))
        {
            if (!claimed.Remove(row.Standard, out var claim))
            {
                differences.Moved.Add($"{row.Standard}: the corpus has this standard and the table has no row for it");
                continue;
            }

            if (!string.Equals(row.Suites, claim.Suites, StringComparison.Ordinal))
            {
                differences.Moved.Add($"{row.Standard}: suites {claim.Suites} -> {row.Suites}");
            }

            if (row.Files != claim.Files)
            {
                differences.Moved.Add($"{row.Standard}: files {Number(claim.Files)} -> {Number(row.Files)}");
            }

            if (!countsIncluded)
            {
                continue;
            }

            if (row.Assertions != claim.Assertions)
            {
                differences.Moved.Add(
                    $"{row.Standard}: assertions {Number(claim.Assertions)} -> {Number(row.Assertions)}");
            }

            var delta = row.NotPassing - claim.NotPassing;
            if (delta > 0)
            {
                differences.Regressed.Add(
                    $"{row.Standard}: not passing {Number(claim.NotPassing)} -> {Number(row.NotPassing)} (+{Number(delta)})");
            }
            else if (delta < 0)
            {
                differences.Stale.Add(
                    $"{row.Standard}: not passing {Number(claim.NotPassing)} -> {Number(row.NotPassing)} (-{Number(-delta)})");
            }
        }

        foreach (var orphan in claimed.Values)
        {
            differences.Moved.Add($"{orphan.Standard}: the table has a row for it and the corpus does not");
        }

        return differences;
    }

    /// <summary>
    /// What a check reports, or <see langword="null"/> when the table is what the corpus says it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three failures rather than one, because they are three different facts and only one of them is the
    /// author's to fix by re-censusing. A <b>regression</b> is the corpus failing more than the table allows;
    /// it names the suite, the direction and the size, and it says outright not to write the larger number,
    /// because "the inventory table is out of date" is what invited exactly that on
    /// <see href="https://github.com/sebastienros/jint/issues/3339">#3339</see>. <b>Staleness</b> is the corpus
    /// failing less, which is the case the phrase actually fits and the one <c>update</c> exists for. And a
    /// <b>moved</b> column is one of the four equalities — a file vendored or removed, a suite regrouped, a
    /// standard renamed, or a run that did not register everything it should have.
    /// </para>
    /// </remarks>
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
                "The web-platform-tests corpus now fails more than Vendor/README.md's inventory table allows:");
            message.AppendLine();
            AppendAll(message, differences.Regressed);
            message.AppendLine();
            message.AppendLine(
                "That column is a ceiling, not a baseline. A rise is a regression to find — either in the "
                + "engine, or in a corpus entry whose outcome depends on the machine it ran on — and "
                + $"{UpdateVariable}=update refuses to write the larger figure, so re-censusing is not the "
                + "way out of this one.");
        }

        if (differences.Stale.Count > 0)
        {
            Separate(message);
            message.AppendLine(
                "Vendor/README.md's inventory table is out of date: the corpus fails less than it states, so "
                + "the table describes an engine that no longer exists.");
            message.AppendLine();
            AppendAll(message, differences.Stale);
            message.AppendLine();
            message.AppendLine($"Rewrite it by running the WPT suites with {UpdateVariable}=update set.");
        }

        if (differences.Moved.Count > 0)
        {
            Separate(message);
            message.AppendLine(
                "These columns are derived from the corpus and have to match it exactly:");
            message.AppendLine();
            AppendAll(message, differences.Moved);
            message.AppendLine();
            message.AppendLine($"Rewrite the table by running the WPT suites with {UpdateVariable}=update set.");
        }

        return message.ToString().TrimEnd();
    }

    /// <summary>
    /// Why <c>update</c> will not write this run's table, or <see langword="null"/> when it may.
    /// </summary>
    /// <remarks>
    /// <b>This is the half that makes the ceiling mean anything.</b> A check that goes red on a rise but can
    /// be satisfied by running the rewrite is not a ceiling, it is a suggestion — and the rewrite is exactly
    /// what "the inventory table is out of date" used to invite. So the rewrite lowers a not-passing figure
    /// and never raises one, and a rise that is genuinely intended — a corpus bump brings a batch of new
    /// failures with it — is spelled <see cref="RaiseVariableValue"/>, one deliberate spelling that cannot be
    /// typed by accident and that leaves the raised numbers visible in the diff.
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
            + "than the table states:");
        message.AppendLine();
        AppendAll(message, differences.Regressed);
        message.AppendLine();
        message.Append(
            "Find the regression instead. If the rise is genuinely intended — a corpus bump arriving with new "
            + $"failures, each of them named in the exclusion table — say so with {UpdateVariable}="
            + $"{RaiseVariableValue}.");
        return message.ToString();
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
#endif
