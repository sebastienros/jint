#if PUBLIC_API_BASELINES
#nullable enable

using System.Globalization;
using System.Reflection;
using System.Text;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Holds every declaration of Jint's approved public API surface to carrying a <c>&lt;summary&gt;</c>, and
/// keeps the ones that do not in a checked-in allowlist that may only ever shrink.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>CS1591</c>.</b> Turning the compiler's missing-comment warning into an error is the obvious
/// gate and the wrong one: it counts the public members of <em>internal</em> types too — 1,688 of them on
/// <c>net10.0</c> — which is not a contract anybody reads. The approved API baseline beside this file is
/// exactly the contract, so it is the denominator.
/// </para>
/// <para>
/// <b>How a baseline line is joined to a documentation comment.</b> It is not: the surface is enumerated out
/// of the assembly's metadata as documentation comment ids, which is the key <c>Jint.xml</c> is already keyed
/// by. Parsing the baseline instead would mean re-deriving an id from C#-like prose whose overload resolution
/// and generic arity are exactly the parts that are hard to get right. The baseline is still load-bearing:
/// <see cref="TheEnumeratedSurfaceIsTheApprovedBaseline"/> holds the two to the same declaration count, so an
/// enumerator that quietly stopped seeing part of the surface fails rather than reporting less debt.
/// </para>
/// <para>
/// <b>What a summary is not demanded of</b> is in <see cref="ApiExclusion"/>: compiler-synthesized record
/// members, delegate <c>Invoke</c>, overrides, and explicit interface implementations. Each of those has a
/// declaration elsewhere in this same surface that carries the documentation an IDE actually shows.
/// </para>
/// <para>
/// The house style the wave writes to is <c>docs/xml-doc-style.md</c>.
/// </para>
/// </remarks>
public class PublicApiDocumentationTest
{
    /// <summary>
    /// The enumerated surface has to be the surface the approved baselines pin, or the gate below measures
    /// its debt against a denominator nobody approved.
    /// </summary>
    /// <remarks>
    /// Counts rather than a set comparison, because the two describe the same declarations in two
    /// deliberately different vocabularies — a documentation comment id and a C# declaration line — and
    /// re-deriving one from the other is the work this test exists to make unnecessary. What it catches is
    /// the failure that matters: an enumerator that stops seeing a whole category of member.
    /// </remarks>
    [Fact]
    public void TheEnumeratedSurfaceIsTheApprovedBaseline()
    {
        var targetFramework = ShippedJintBuildOutput.NewestTargetFramework;
        var enumerated = Surface(targetFramework).Count(declaration => declaration.AppearsInBaseline);
        var approved = BaselineDeclarationCount(targetFramework);

        Assert.True(
            enumerated == approved,
            $"""
            The public API surface enumerated from the {targetFramework} assembly has {enumerated} declarations,
            while the approved baseline 'Verify/PublicApiTest_{targetFramework}.verified.txt' has {approved}.

            They describe the same thing, so a difference means PublicApiSurface has started seeing more or
            less than the baseline pins — and the documentation gate beside this test would then be measuring
            its debt against a surface nobody approved.
            """);
    }

    /// <summary>
    /// Every declaration of the public surface carries a <c>&lt;summary&gt;</c>, except the ones
    /// <c>UndocumentedPublicApi.txt</c> still names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three ways to fail, and all three are fixed by reviewing the diff and re-running with
    /// <c>JINT_PUBLIC_API_DOCS=update</c>: a declaration shipped undocumented that the allowlist does not
    /// name; a declaration the allowlist names that is now documented, so the file has to shrink; and a
    /// declaration the allowlist names that is no longer in the surface at all.
    /// </para>
    /// <para>
    /// The second and third are failures on purpose. An allowlist that silently absorbs its own progress
    /// stops being a count of what is left.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryDeclarationOfThePublicApiSurfaceCarriesASummary()
    {
        var targetFramework = ShippedJintBuildOutput.NewestTargetFramework;
        var documented = XmlDocumentationFile.DeclarationsWithASummary(Documentation(targetFramework));

        var wanted = Surface(targetFramework).Where(declaration => declaration.NeedsSummary).ToList();
        var undocumented = wanted
            .Where(declaration => !documented.Contains(declaration.Id))
            .Select(declaration => declaration.Id)
            .ToList();

        if (UndocumentedPublicApiAllowlist.UpdateRequested())
        {
            var written = UndocumentedPublicApiAllowlist.Write(undocumented);
            Assert.Skip($"{UndocumentedPublicApiAllowlist.UpdateVariable} rewrote {written} with {undocumented.Count} entries.");
        }

        var allowed = UndocumentedPublicApiAllowlist.Read();
        var known = allowed.Entries.ToHashSet(StringComparer.Ordinal);
        var present = wanted.Select(declaration => declaration.Id).ToHashSet(StringComparer.Ordinal);

        var appeared = undocumented.Where(id => !known.Contains(id)).ToList();
        var nowDocumented = allowed.Entries.Where(id => present.Contains(id) && documented.Contains(id)).ToList();
        var gone = allowed.Entries.Where(id => !present.Contains(id)).ToList();

        if (appeared.Count == 0 && nowDocumented.Count == 0 && gone.Count == 0 && allowed.Stated == allowed.Entries.Count)
        {
            return;
        }

        var report = new StringBuilder();
        report.Append(CultureInfo.InvariantCulture, $"'{UndocumentedPublicApiAllowlist.Path()}' no longer describes the {targetFramework} public API surface.");
        report.AppendLine().AppendLine();

        Section(report, appeared, "shipped with no <summary> and not named by the allowlist", """
            Write one — docs/xml-doc-style.md says what a good one looks like. Adding these to the allowlist
            instead is the one edit it does not accept: it is a register of debt taken on before it existed.
            """);

        Section(report, nowDocumented, "named by the allowlist but now documented", """
            This is the wave working. Re-run with JINT_PUBLIC_API_DOCS=update so the count at the top of the
            file goes down by exactly this many.
            """);

        Section(report, gone, "named by the allowlist but no longer in the public API surface", """
            The declaration was removed or made internal. Re-run with JINT_PUBLIC_API_DOCS=update, and check
            that the removal is in docs/v5-migration.md.
            """);

        if (allowed.Stated != allowed.Entries.Count)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"The file states {allowed.Stated} entries and holds {allowed.Entries.Count}, so it was hand-edited.").AppendLine();
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"""
            Review the diff, then:

                {UndocumentedPublicApiAllowlist.UpdateVariable}=update dotnet test -c Release Jint.Tests.PublicInterface/Jint.Tests.PublicInterface.csproj -f {ShippedJintBuildOutput.NewestTargetFramework}
            """);

        Assert.Fail(report.ToString());
    }

    /// <summary>
    /// No target framework is documented less than the newest one, which is what makes running the gate on
    /// that one alone enough.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Documentation is not conditional the way the surface is, so the natural assumption is that one target
    /// framework's <c>Jint.xml</c> stands for all five. It does not follow on its own: a doc comment inside
    /// <c>#if NET8_0_OR_GREATER</c> — or on the only part of a <c>partial</c> type that is gated — is absent
    /// from a downlevel consumer's IntelliSense while the gate above sees it. This test found exactly two.
    /// </para>
    /// <para>
    /// It also pins the wider claim the single-target gate rests on: every downlevel surface is a
    /// <em>subset</em> of the newest one, so nothing is measured on a target framework nobody looked at.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoTargetFrameworkIsDocumentedLessThanTheNewest()
    {
        var newest = ShippedJintBuildOutput.NewestTargetFramework;
        var newestDocumented = XmlDocumentationFile.DeclarationsWithASummary(Documentation(newest));
        var newestSurface = Surface(newest).Where(d => d.NeedsSummary).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        var report = new StringBuilder();
        foreach (var targetFramework in ShippedJintBuildOutput.TargetFrameworks)
        {
            if (targetFramework == newest)
            {
                continue;
            }

            var documented = XmlDocumentationFile.DeclarationsWithASummary(Documentation(targetFramework));
            var surface = Surface(targetFramework).Where(d => d.NeedsSummary).Select(d => d.Id).ToList();

            var absent = surface.Where(id => !newestSurface.Contains(id)).ToList();
            var lessDocumented = surface
                .Where(id => newestSurface.Contains(id) && newestDocumented.Contains(id) && !documented.Contains(id))
                .ToList();

            Section(report, absent, $"in the {targetFramework} surface but not in the {newest} one", $"""
                The gate runs on {newest} because every other shipped surface is a subset of it. These are not,
                so either they are new downlevel-only API — which needs its own gate — or the enumerator is
                wrong.
                """);

            Section(report, lessDocumented, $"documented on {newest} but not on {targetFramework}", $"""
                The comment is behind a preprocessor directive, or on the only part of a partial declaration
                that is. Move it to a part every target framework compiles, so the consumers that resolve the
                {targetFramework} asset see it too.
                """);
        }

        if (report.Length > 0)
        {
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// No documentation comment nests a <c>&lt;para&gt;</c> inside another one.
    /// </summary>
    /// <remarks>
    /// Well-formed XML, so the build says nothing, and invalid in every renderer downstream: a paragraph
    /// cannot contain a paragraph, and the one that did closed three of them into the wrong place. There is
    /// no allowlist because there is nothing to allow — the count was one, and it is now zero.
    /// </remarks>
    [Fact]
    public void NoDocumentationCommentNestsAParagraph()
    {
        var nesting = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var targetFramework in ShippedJintBuildOutput.TargetFrameworks)
        {
            nesting.UnionWith(XmlDocumentationFile.DeclarationsNestingAParagraph(Documentation(targetFramework)));
        }

        var report = new StringBuilder();
        Section(report, nesting.ToList(), "nest a <para> inside another <para>", """
            Close the outer paragraph before the inner one opens. docs/xml-doc-style.md caps <remarks> at four
            paragraphs, which is usually the real fix.
            """);

        if (report.Length > 0)
        {
            Assert.Fail(report.ToString());
        }
    }

    private static void Section(StringBuilder report, IReadOnlyList<string> ids, string headline, string guidance)
    {
        if (ids.Count == 0)
        {
            return;
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"{ids.Count} declaration(s) {headline}:").AppendLine();
        foreach (var id in ids.OrderBy(id => id, StringComparer.Ordinal).Take(50))
        {
            report.Append("    ").AppendLine(id);
        }

        if (ids.Count > 50)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    … and {ids.Count - 50} more");
        }

        report.AppendLine().AppendLine(guidance).AppendLine();
    }

    private static List<ApiDeclaration> Surface(string targetFramework)
    {
        var assemblyPath = ShippedJintBuildOutput.AssemblyPath(targetFramework);
        if (!File.Exists(assemblyPath))
        {
            Assert.Fail(ShippedJintBuildOutput.MissingBuildOutput(targetFramework, assemblyPath));
        }

        using var context = new MetadataLoadContext(new PathAssemblyResolver(ShippedJintBuildOutput.ResolverPaths(assemblyPath)));
        return PublicApiSurface.Enumerate(context.LoadFromAssemblyPath(assemblyPath));
    }

    /// <summary>
    /// The XML documentation file beside a shipped assembly, refusing early and by name if it is not there —
    /// which means <c>GenerateDocumentationFile</c> stopped being set, and every check here would otherwise
    /// report the whole surface as undocumented.
    /// </summary>
    private static string Documentation(string targetFramework)
    {
        var path = ShippedJintBuildOutput.DocumentationPath(targetFramework);
        if (!File.Exists(path))
        {
            Assert.Fail($"""
                Jint has no {targetFramework} XML documentation file at '{path}'.

                It is what <GenerateDocumentationFile> in Jint/Jint.csproj produces, and it is the only place
                the documentation comments exist as data. Build with:

                    dotnet build -c Release Jint/Jint.csproj
                """);
        }

        return path;
    }

    /// <summary>
    /// How many declarations the approved baseline writes down. Every line of it is one, except the braces,
    /// the namespace headers, the attributes, and a generic constraint continued onto its own line.
    /// </summary>
    private static int BaselineDeclarationCount(string targetFramework)
    {
        var path = Path.Combine(
            ShippedJintBuildOutput.RepositoryRoot,
            "Jint.Tests.PublicInterface",
            "Verify",
            $"PublicApiTest_{targetFramework}.verified.txt");

        var count = 0;
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0
                || trimmed is "{" or "}"
                || trimmed[0] == '['
                || trimmed.StartsWith("namespace ", StringComparison.Ordinal)
                || trimmed.StartsWith("where ", StringComparison.Ordinal))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}

/// <summary>
/// The register of declarations in Jint's approved public API surface that still have no
/// <c>&lt;summary&gt;</c>.
/// </summary>
internal static class UndocumentedPublicApiAllowlist
{
    /// <summary>Set to <c>update</c> to rewrite the file from what the run measured.</summary>
    public const string UpdateVariable = "JINT_PUBLIC_API_DOCS";

    private const string CountPrefix = "undocumented: ";

    private static readonly string[] _header =
    [
        "# Declarations of Jint's approved public API surface that still have no <summary>.",
        "#",
        "# This is a register of debt, not a configuration knob. The documentation wave deletes lines from",
        "# it; nothing may be added. A new public declaration ships documented, or it does not ship.",
        "#",
        "# The gate is Jint.Tests.PublicInterface/PublicApiDocumentationTest.cs and the house style it is",
        "# written to is docs/xml-doc-style.md. To regenerate after documenting something, and then review",
        "# the diff:",
        "#",
        "#     JINT_PUBLIC_API_DOCS=update dotnet test -c Release \\",
        "#         Jint.Tests.PublicInterface/Jint.Tests.PublicInterface.csproj -f net10.0",
        "#",
        "# The names are ISO documentation comment ids, which is what the compiler writes into Jint.xml.",
        "#",
    ];

    public static bool UpdateRequested()
        => string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "update", StringComparison.OrdinalIgnoreCase);

    public static string Path()
        => System.IO.Path.Combine(ShippedJintBuildOutput.RepositoryRoot, "Jint.Tests.PublicInterface", "UndocumentedPublicApi.txt");

    public static (int Stated, List<string> Entries) Read()
    {
        var stated = -1;
        var entries = new List<string>();

        foreach (var raw in File.ReadLines(Path()))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line.StartsWith(CountPrefix, StringComparison.Ordinal))
            {
                stated = int.Parse(line.AsSpan(CountPrefix.Length), CultureInfo.InvariantCulture);
                continue;
            }

            entries.Add(line);
        }

        return (stated, entries);
    }

    public static string Write(IReadOnlyList<string> entries)
    {
        var path = Path();

        // The working tree is CRLF on Windows and LF elsewhere; keep whichever the file already uses, so a
        // regeneration is a diff of the entries and nothing else.
        var newLine = File.Exists(path) && File.ReadAllText(path).Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var builder = new StringBuilder();
        foreach (var line in _header)
        {
            builder.Append(line).Append(newLine);
        }

        builder.Append(CountPrefix).Append(entries.Count.ToString(CultureInfo.InvariantCulture)).Append(newLine);
        foreach (var entry in entries.OrderBy(entry => entry, StringComparer.Ordinal))
        {
            builder.Append(entry).Append(newLine);
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }
}
#endif
