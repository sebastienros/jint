using System.Reflection;
using System.Text.RegularExpressions;

namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// <c>Fixtures/README.md</c> is the artefact, and this is what stops it from being a description.
/// </summary>
/// <remarks>
/// The same discipline the web-platform-tests lane's exclusion table is under: a table nothing checks is a
/// table that is wrong within two pull requests. Four properties are held — every fixture has a row and every
/// row a fixture, every fixture has an entry document, every vendored library has a row and a licence beside
/// it, and the set of fixtures the table calls <b>needs triage</b> is exactly the set whose case is marked
/// <c>[Explicit]</c>. The last is the one that matters: it is what makes quietly turning a failing fixture off
/// impossible.
/// </remarks>
public class FixtureInventoryTests
{
    private static readonly string _readme = FixtureCorpus.Read("README.md");

    [Test]
    public void EveryFixtureHasARowAndEveryRowAFixture()
    {
        RowNames().Should().BeEquivalentTo(
            FixtureCorpus.FixtureNames,
            "Fixtures/README.md's inventory is the list of fixtures, so a directory with no row is a fixture "
            + "nothing describes and a row with no directory is a fixture that was deleted without its row.");
    }

    [Test]
    public void EveryFixtureHasAnEntryDocument()
    {
        foreach (var fixture in FixtureCorpus.FixtureNames)
        {
            FixtureCorpus.Contains(fixture + "/index.html").Should().BeTrue(
                "a fixture is a directory a page can be navigated to, so '" + fixture + "' needs an index.html");
        }
    }

    [Test]
    public void EveryVendoredLibraryHasALicenceBesideItAndARowAboveIt()
    {
        FixtureCorpus.VendorNames.Should().NotBeEmpty();

        foreach (var library in FixtureCorpus.VendorNames)
        {
            FixtureCorpus.Contains("vendor/" + library + "/LICENSE").Should().BeTrue(
                "'" + library + "' is vendored, so its licence is vendored with it");

            var version = library[(library.LastIndexOf('-') + 1)..];

            _readme.Should().Contain(
                version,
                "Fixtures/README.md names every vendored library with the version it was taken at, and '"
                + library + "' is pinned at " + version);
        }
    }

    [Test]
    public void TheTriageTableIsExactlyTheSetOfExplicitCases()
    {
        var marked = ExplicitReasons().Select(FixtureOf).ToArray();

        marked.Should().NotContain(
            "",
            "an [Explicit] case in the obstacle course opens its reason with '<fixture>: ', so that the reason "
            + "can be matched against Fixtures/README.md's triage table: " + string.Join(" / ", ExplicitReasons()));

        marked.Should().BeEquivalentTo(
            TriageRows(),
            "the fixtures Fixtures/README.md calls 'needs triage' are exactly the ones whose case is skipped: "
            + "a fixture turned off without a row is a failure nobody owes anything for, and a row with no "
            + "skipped case is a debt that was paid without being written off.");
    }

    /// <summary>The reason on every <c>[Explicit]</c> case in this namespace.</summary>
    /// <remarks>
    /// Read through <see cref="CustomAttributeData"/> rather than off the attribute instance, because NUnit
    /// keeps an <c>ExplicitAttribute</c>'s reason private and turns it into a test property at discovery.
    /// </remarks>
    private static IReadOnlyList<string> ExplicitReasons()
        => typeof(FixtureInventoryTests).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(FixtureInventoryTests).Namespace)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributesData())
            .Where(data => data.AttributeType == typeof(ExplicitAttribute))
            .Select(data => data.ConstructorArguments.Count > 0 ? data.ConstructorArguments[0].Value as string ?? "" : "")
            .ToArray();

    /// <summary>The fixture an <c>[Explicit]</c> reason opens with, or the empty string if it names none.</summary>
    private static string FixtureOf(string reason)
    {
        var colon = reason.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? "" : reason[..colon].Trim();

        return FixtureCorpus.FixtureNames.Contains(name, StringComparer.Ordinal) ? name : "";
    }

    /// <summary>Every name in the first column of an inventory row.</summary>
    private static IReadOnlyList<string> RowNames()
        => Regex.Matches(_readme, @"^\| `(?<name>[a-z0-9-]+)` \|", RegexOptions.Multiline)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>The fixtures whose inventory row says they do not pass.</summary>
    private static IReadOnlyList<string> TriageRows()
        => Regex.Matches(_readme, @"^\| `(?<name>[a-z0-9-]+)` \|.*\| \*\*needs triage\*\* \|$", RegexOptions.Multiline)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
