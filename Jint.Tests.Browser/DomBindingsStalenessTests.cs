using System.Text;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Jint.Browser.BindingGenerator;

namespace Jint.Tests.Browser;

/// <summary>
/// The checked-in bindings are a picture of the pinned AngleSharp assemblies' <c>[DomName]</c> surface. This
/// runs the same emitter in memory and fails on any difference, which is what makes them a picture rather
/// than a memory.
/// </summary>
/// <remarks>
/// Set <c>JINT_DOM_BINDINGS=update</c> to write the difference back instead of failing — the discipline
/// <c>JINT_SPEC_ANCHORS</c> and <c>JINT_WPT_CENSUS</c> already carry. That is also the shortest regeneration
/// path after an <c>overrides.json</c> edit or an AngleSharp bump; <c>tools/dom-bindings/README.md</c> has
/// the command-line one.
/// </remarks>
public sealed class DomBindingsStalenessTests
{
    [Test]
    public void TheCheckedInBindingsMatchTheEmitter()
    {
        var result = Generate();
        var updating = IsUpdating();
        var differences = new List<string>();

        foreach (var (name, expected) in result.Files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(RepositoryPaths.GeneratedDirectory, name);
            var actual = File.Exists(path) ? RepositoryPaths.NormalizeNewlines(File.ReadAllText(path)) : null;

            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                continue;
            }

            if (updating)
            {
                Directory.CreateDirectory(RepositoryPaths.GeneratedDirectory);
                File.WriteAllText(path, expected, new UTF8Encoding(false));
                continue;
            }

            differences.Add(actual is null ? name + " is missing" : name + " differs");
        }

        foreach (var path in Directory.GetFiles(RepositoryPaths.GeneratedDirectory, "*.g.cs"))
        {
            var name = Path.GetFileName(path);
            if (result.Files.ContainsKey(name))
            {
                continue;
            }

            if (updating)
            {
                File.Delete(path);
                continue;
            }

            differences.Add(name + " is checked in but the emitter no longer produces it");
        }

        differences.Should().BeEmpty(
            "the checked-in DOM bindings must equal what the emitter produces from the pinned AngleSharp assemblies; run the suite again with JINT_DOM_BINDINGS=update to write the difference back, then read the diff");
    }

    [Test]
    public void TheGeneratorReportsNoDiagnostics()
    {
        // It used to report two, and both were the same thing: one WebIDL member spelled as two CLR overloads
        // sharing a [DomName] — HTML's `select.add((HTMLOptionElement or HTMLOptGroupElement) element, …)` is
        // a union type and AngleSharp models it as two methods, so one half always lost and
        // `select.add(optgroup)` was a TypeError where a browser accepts it. Both are `skip` + `additions`
        // entries over DomUnionMembers now, which is what turns the collision into a decision.
        //
        // Empty is therefore the assertion, and it is a stronger one than a pinned list: ANY diagnostic means
        // the generator found something nobody has looked at.
        Generate().Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void EverySkippedMemberCarriesAReason()
    {
        var skipped = Generate().Skipped;

        skipped.Should().NotBeEmpty("some AngleSharp members cannot cross the boundary, and the report is where they are named");
        skipped.Should().AllSatisfy(entry => entry.Should().Contain(" — ", "a skip without a reason is an omission nobody can review"));
    }

    internal static BindingGeneratorResult Generate() => BindingGenerator.Run(new BindingGeneratorOptions
    {
        // The assemblies this test process resolved, which are the ones Directory.Packages.props pinned:
        // asking the loaded types where they came from is what keeps the emitter and the runtime looking at
        // one version, without this suite having to know anything about the NuGet cache layout.
        CoreAssembly = typeof(IElement).Assembly.Location,
        CssAssembly = typeof(ICssStyleDeclaration).Assembly.Location,
        OverridesPath = RepositoryPaths.OverridesPath,
    });

    private static bool IsUpdating()
        => Environment.GetEnvironmentVariable("JINT_DOM_BINDINGS") is { } value
           && (value.Equals("update", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.Ordinal));
}
