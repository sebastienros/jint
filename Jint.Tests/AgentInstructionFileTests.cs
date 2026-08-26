#nullable enable

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jint.Tests;

/// <summary>
/// The repository's agent instruction files — the root <c>AGENTS.md</c> and the co-located ones it indexes —
/// held to the byte budgets that file states, and to the routing map that makes them reachable at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a test and not a build target.</b> The budget is not decorative: it is OpenAI Codex's
/// <c>project_doc_max_bytes</c> default and the Devin CLI's always-on rule-file cap, and crossing either
/// truncates the file mid-byte at a log level neither CLI prints. So the cost of crossing is invisible on both
/// sides — the tail of the file simply stops arriving, and the agent that never read it cannot know. It has
/// happened twice: <see href="https://github.com/sebastienros/jint/issues/3323">#3323</see> found
/// <c>Jint/AGENTS.md</c> over, and two pull requests after the relocation that fixed it,
/// <c>Jint/Runtime/Interop/AGENTS.md</c> was 1,689 bytes over with nothing saying so. Relocation is therefore
/// not a durable fix on its own; something has to fail. A test is what a developer already runs before pushing
/// — <c>dotnet test -c Release</c> is the root file's own instruction — so the signal arrives before the
/// branch leaves the machine rather than only in CI, and it costs one pass over fifteen small files. An
/// MSBuild target would beat it to the news by a few seconds and pay for that by failing an ordinary compile
/// on a documentation edit, which is the wrong error at the wrong moment; a CI step alone would be the gate
/// this repository explicitly does not want, since by then the work is already pushed.
/// </para>
/// <para>
/// <b>The budgets are not one number.</b> <see cref="AgentInstructionFiles.RootBudget"/> is 24 KiB and
/// <see cref="AgentInstructionFiles.CoLocatedBudget"/> is 32 KiB, because Codex spends one 32 KiB allowance
/// across the whole root-to-cwd chain rather than per file: a fat root file starves the nested one an agent
/// needs. <see cref="TheRootFileStatesTheBudgetsThisTestEnforces"/> holds the two constants to the prose that
/// explains them, so neither can drift into being the only place the number is written down.
/// </para>
/// <para>
/// <b>Line endings are counted as CRLF, always.</b> <c>.gitattributes</c> normalizes these files with
/// <c>text=auto</c>, so the same commit is LF on a Linux checkout and CRLF on a Windows one — a difference of
/// one byte per line, which is 300 bytes on a file this size. Measuring what happens to be on disk would let a
/// file pass in CI and truncate for every Windows user of the very agents the budget exists for. So
/// <see cref="AgentInstructionFiles.MeasureAsCrlf"/> counts the largest form a checkout can take, and the
/// verdict is the same on every operating system.
/// </para>
/// <para>
/// <b>When it fails it reports every file, not the offender.</b> The question at that moment is never "which
/// file is over" — the author knows, they just wrote it — but "where does this material belong instead", and
/// that is answered by headroom across the whole set. The three checks beside the budget are what make an
/// answer to it safe: a relocated file is worth nothing if no route leads to it, and only four of the fourteen
/// agent ecosystems the root file surveys load a nested <c>AGENTS.md</c> on their own. The rest arrive through
/// the root index, or — for Claude Code, which does not read <c>AGENTS.md</c> at all — through
/// <c>.claude/rules/*.md</c>. Both halves are checked, in both directions.
/// </para>
/// </remarks>
public class AgentInstructionFileTests
{
    /// <summary>
    /// No instruction file crosses the budget the root <c>AGENTS.md</c> sets for it.
    /// </summary>
    [Test]
    public void EveryInstructionFileFitsTheBudgetTheRootFileStates()
    {
        var over = AgentInstructionFiles.All
            .Where(file => file.Bytes > file.Budget)
            .ToArray();

        if (over.Length == 0)
        {
            return;
        }

        var offenders = string.Join(
            Environment.NewLine,
            over.Select(file => string.Format(
                CultureInfo.InvariantCulture,
                "  {0} is {1:N0} bytes, {2:N0} over its {3:N0} cap.",
                file.RelativePath,
                file.Bytes,
                file.Bytes - file.Budget,
                file.Budget)));

        Assert.Fail(
            $"""
            {over.Length} agent instruction file(s) cross the budget the root AGENTS.md states, so the tail of
            each one is silently truncated for the agents that read it - Codex stops at project_doc_max_bytes
            and the Devin CLI at its rule-file cap, neither with a message anybody sees.

            {offenders}

            Do not trim to fit. Relocate the material to the AGENTS.md that governs it, the way #3377 did,
            and update both halves of the routing map - the root index row and .claude/rules - so the moved
            rules stay reachable. Here is where there is room:

            {AgentInstructionFiles.Report()}
            """);
    }

    /// <summary>
    /// Every relative link and heading anchor between the instruction files resolves.
    /// </summary>
    /// <remarks>
    /// Relocation is the sanctioned way to fit the budget, and relocation is exactly what breaks a link: the
    /// depth of a <c>../../..</c> changes with the destination's directory, and a moved section takes its
    /// anchor with it. Both were checked by hand in #3377, which is not a thing to rely on twice.
    /// </remarks>
    [Test]
    public void EveryLinkAndAnchorBetweenInstructionFilesResolves()
    {
        var broken = new List<string>();

        foreach (var document in AgentInstructionFiles.RoutingDocuments)
        {
            var directory = Path.GetDirectoryName(document.FullPath)!;

            foreach (var link in AgentInstructionFiles.LinksIn(document.FullPath))
            {
                var separator = link.IndexOf('#');
                var path = separator < 0 ? link : link.Substring(0, separator);
                var anchor = separator < 0 ? null : link.Substring(separator + 1);

                var target = path.Length == 0
                    ? document.FullPath
                    : Path.GetFullPath(Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)));

                if (!File.Exists(target))
                {
                    broken.Add($"  {document.RelativePath} -> {link} (no such file)");
                    continue;
                }

                if (anchor is null || !target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!AgentInstructionFiles.HeadingSlugsOf(target).Contains(anchor))
                {
                    broken.Add($"  {document.RelativePath} -> {link} (the file has no heading with that anchor)");
                }
            }
        }

        Assert.That(
            broken.Count == 0,
            $"""
            {broken.Count} link(s) between the agent instruction files do not resolve. A broken one is worse
            than no link: it is how a rule that was relocated rather than deleted becomes unreachable.

            {string.Join(Environment.NewLine, broken)}
            """);
    }

    /// <summary>
    /// Every co-located instruction file is named by the root index and by a <c>.claude/rules</c> file, and
    /// neither names one that does not exist.
    /// </summary>
    /// <remarks>
    /// These are the two halves of the routing map, and they serve disjoint audiences: ten of the fourteen
    /// ecosystems the root file surveys reach a co-located file only because the index names it, while Claude
    /// Code reads <c>CLAUDE.md</c> and the path-triggered rules and never <c>AGENTS.md</c> at all. A file
    /// missing from either half is invisible to everyone on that side of the split, which is the same outcome
    /// as truncation and just as quiet.
    /// </remarks>
    [Test]
    public void EveryInstructionFileIsReachableFromBothHalvesOfTheRoutingMap()
    {
        var coLocated = new HashSet<string>(
            AgentInstructionFiles.All.Where(file => !file.IsRoot).Select(file => file.RelativePath),
            StringComparer.Ordinal);

        var indexed = AgentInstructionFiles.InstructionFilesNamedBy(AgentInstructionFiles.Root);
        var ruled = new HashSet<string>(StringComparer.Ordinal);
        var silentRules = new List<string>();

        foreach (var rule in AgentInstructionFiles.ClaudeRules)
        {
            var named = AgentInstructionFiles.InstructionFilesNamedBy(rule);
            if (named.Count == 0)
            {
                silentRules.Add(rule.RelativePath);
            }

            ruled.UnionWith(named);
        }

        var complaints = new List<string>();
        complaints.AddRange(coLocated.Except(indexed).Select(path => $"  {path} exists but the root index does not name it."));
        complaints.AddRange(indexed.Except(coLocated).Select(path => $"  the root index names {path}, which is not a co-located instruction file."));
        complaints.AddRange(coLocated.Except(ruled).Select(path => $"  {path} exists but no .claude/rules file points at it."));
        complaints.AddRange(silentRules.Select(path => $"  {path} is a rule that names no AGENTS.md, so it routes nowhere."));

        Assert.That(
            complaints.Count == 0,
            $"""
            The routing map has {complaints.Count} inconsistency(ies). An instruction file is only read because
            something points at it: the root index for the ecosystems that never descend into a subdirectory,
            and .claude/rules for Claude Code, which does not read AGENTS.md at all.

            {string.Join(Environment.NewLine, complaints)}
            """);
    }

    /// <summary>
    /// The budgets this test enforces are the ones the root file states in prose.
    /// </summary>
    /// <remarks>
    /// The prose is where the numbers are argued — which agent truncates at which byte, and why the root file's
    /// allowance is the smaller one. A constant that quietly disagreed with it would enforce a rule nobody had
    /// read, so the two are held together rather than merely written twice.
    /// </remarks>
    [Test]
    public void TheRootFileStatesTheBudgetsThisTestEnforces()
    {
        var text = File.ReadAllText(AgentInstructionFiles.Root.FullPath);

        foreach (var budget in new[] { AgentInstructionFiles.RootBudget, AgentInstructionFiles.CoLocatedBudget })
        {
            var stated = string.Format(CultureInfo.InvariantCulture, "under **{0} KiB**", budget / 1024);

            Assert.That(
                text.Contains(stated, StringComparison.Ordinal),
                $"""
                The root AGENTS.md no longer says "{stated}", but this test still enforces {budget:N0} bytes.
                Whichever of the two is wrong, they have to say the same thing: the prose is where the number
                is argued and the constant is what fails, and a rule nobody can read is not enforced.
                """);
        }
    }
}

/// <summary>
/// The agent instruction files on disk, their budgets, and the links between them.
/// </summary>
internal static class AgentInstructionFiles
{
    /// <summary>
    /// What the root <c>AGENTS.md</c> may weigh. Smaller than the co-located allowance because Codex spends one
    /// budget across the whole root-to-cwd chain, so the root file's size comes out of every nested file's.
    /// </summary>
    internal const int RootBudget = 24 * 1024;

    /// <summary>
    /// What a co-located <c>AGENTS.md</c> may weigh — Codex's <c>project_doc_max_bytes</c> default, which is
    /// also the Devin CLI's always-on rule-file cap.
    /// </summary>
    internal const int CoLocatedBudget = 32 * 1024;

    /// <summary>
    /// Directories a repository checkout carries that are not the repository: build output, package caches and
    /// version control. A restored package can perfectly well ship an <c>AGENTS.md</c> of its own, and
    /// <c>tools/package-consumer</c> restores into the tree.
    /// </summary>
    private static readonly string[] NotSourceDirectories =
    [
        ".git", ".vs", "artifacts", "bin", "obj", "node_modules", "packages"
    ];

    private static readonly Regex LinkTarget = new(@"\]\(([^)\s]+)\)", RegexOptions.Compiled);

    private static readonly Dictionary<string, HashSet<string>> SlugCache = new(StringComparer.OrdinalIgnoreCase);

    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Every <c>AGENTS.md</c> in the repository, the root one first.
    /// </summary>
    internal static IReadOnlyList<InstructionFile> All { get; } = Discover();

    internal static InstructionFile Root { get; } = All.Single(file => file.IsRoot);

    /// <summary>
    /// The path-triggered rule files Claude Code reads in place of a nested <c>AGENTS.md</c>.
    /// </summary>
    internal static IReadOnlyList<InstructionFile> ClaudeRules { get; } = DiscoverClaudeRules();

    /// <summary>
    /// Everything that routes an agent somewhere: the instruction files themselves and the rules that point at
    /// them. These are the documents whose links have to resolve.
    /// </summary>
    internal static IReadOnlyList<InstructionFile> RoutingDocuments { get; } = All.Concat(ClaudeRules).ToList();

    /// <summary>
    /// The size of a file measured with CRLF line endings, which is the largest form a checkout can take.
    /// </summary>
    /// <remarks>
    /// <c>.gitattributes</c> normalizes markdown with <c>text=auto</c>, so the bytes on disk depend on the
    /// operating system that checked them out. Measuring those would make a file that truncates for every
    /// Windows agent pass on a Linux runner.
    /// </remarks>
    internal static int MeasureAsCrlf(byte[] bytes)
    {
        var bare = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte) '\n' && (i == 0 || bytes[i - 1] != (byte) '\r'))
            {
                bare++;
            }
        }

        return bytes.Length + bare;
    }

    /// <summary>
    /// Every file's size against its budget, fullest first — the table to read when deciding where relocated
    /// material can go.
    /// </summary>
    internal static string Report()
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-42} {1,8} {2,8} {3,10} {4,7}", "file", "bytes", "cap", "headroom", "used"));

        foreach (var file in All.OrderByDescending(file => (double) file.Bytes / file.Budget))
        {
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-42} {1,8:N0} {2,8:N0} {3,10:N0} {4,6:F1}%{5}",
                file.RelativePath,
                file.Bytes,
                file.Budget,
                file.Budget - file.Bytes,
                100.0 * file.Bytes / file.Budget,
                file.Bytes > file.Budget ? "  <-- over" : ""));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The link targets in a markdown file, ignoring fenced code and anything with a scheme.
    /// </summary>
    internal static IEnumerable<string> LinksIn(string path)
    {
        var fenced = false;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced)
            {
                continue;
            }

            foreach (Match match in LinkTarget.Matches(line))
            {
                var target = match.Groups[1].Value;
                if (target.IndexOf("://", StringComparison.Ordinal) < 0 && !target.StartsWith("mailto:", StringComparison.Ordinal))
                {
                    yield return target;
                }
            }
        }
    }

    /// <summary>
    /// The repository-relative paths of the <c>AGENTS.md</c> files a document links to.
    /// </summary>
    internal static HashSet<string> InstructionFilesNamedBy(InstructionFile document)
    {
        var directory = Path.GetDirectoryName(document.FullPath)!;
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in LinksIn(document.FullPath))
        {
            var separator = link.IndexOf('#');
            var path = separator < 0 ? link : link.Substring(0, separator);
            if (!path.EndsWith("AGENTS.md", StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)));
            named.Add(Relative(target));
        }

        return named;
    }

    /// <summary>
    /// The GitHub heading anchors a markdown file offers.
    /// </summary>
    internal static HashSet<string> HeadingSlugsOf(string path)
    {
        lock (SlugCache)
        {
            if (SlugCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var slugs = new HashSet<string>(StringComparer.Ordinal);
            var fenced = false;

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                    continue;
                }

                if (!fenced && line.StartsWith("#", StringComparison.Ordinal))
                {
                    slugs.Add(Slug(line.TrimStart('#').Trim()));
                }
            }

            SlugCache[path] = slugs;
            return slugs;
        }
    }

    /// <summary>
    /// GitHub's heading-to-anchor rule: lower-case, drop everything that is not a letter, a digit, a hyphen or
    /// an underscore, and turn spaces into hyphens.
    /// </summary>
    private static string Slug(string heading)
    {
        var builder = new StringBuilder(heading.Length);

        foreach (var character in heading)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (character == ' ')
            {
                builder.Append('-');
            }
            else if (character is '-' or '_')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<InstructionFile> Discover()
    {
        var found = new List<InstructionFile>();
        Collect(RepositoryRoot, found);

        return found
            .OrderBy(file => file.IsRoot ? 0 : 1)
            .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static void Collect(string directory, List<InstructionFile> found)
    {
        var candidate = Path.Combine(directory, "AGENTS.md");
        if (File.Exists(candidate))
        {
            found.Add(Describe(candidate, budget: directory == RepositoryRoot ? RootBudget : CoLocatedBudget));
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            if (Array.IndexOf(NotSourceDirectories, Path.GetFileName(child)) < 0)
            {
                Collect(child, found);
            }
        }
    }

    private static IReadOnlyList<InstructionFile> DiscoverClaudeRules()
    {
        var directory = Path.Combine(RepositoryRoot, ".claude", "rules");

        return Directory.GetFiles(directory, "*.md")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Describe(path, budget: CoLocatedBudget))
            .ToList();
    }

    private static InstructionFile Describe(string path, int budget)
    {
        return new InstructionFile(Relative(path), path, MeasureAsCrlf(File.ReadAllBytes(path)), budget);
    }

    private static string Relative(string path)
    {
        return path.Length > RepositoryRoot.Length && path.StartsWith(RepositoryRoot, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(RepositoryRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/')
            : path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, ".claude", "rules")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No repository root with an 'AGENTS.md' and a '.claude/rules' directory above '{AppContext.BaseDirectory}'. The agent instruction files are checked in the repository they live in and cannot be checked from a detached copy of this assembly.");
    }
}

/// <summary>
/// One instruction file: where it is, what it weighs measured as CRLF, and what it may weigh.
/// </summary>
internal sealed record InstructionFile(string RelativePath, string FullPath, int Bytes, int Budget)
{
    /// <summary>
    /// Whether this is the repository-root <c>AGENTS.md</c>, which carries the smaller budget and the index.
    /// </summary>
    internal bool IsRoot => RelativePath.Equals("AGENTS.md", StringComparison.Ordinal);
}
