using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace Jint.Tests.Browser;

/// <summary>Historical upstream contract provenance: extraction pins and test-only assemblies agree.</summary>
/// <remarks>
/// The production Browser uses native models and the generator reads the explicit contract. A pin bump
/// requires an upstream provenance audit and contract diff; these assemblies are test-only oracles.
/// </remarks>
public sealed class DomBindingsPinTests
{
    [Test]
    public void ThePinAndTheCentralPackageVersionsAgree()
    {
        var pinned = ReadPin();
        var referenced = ReadPackageVersions();

        pinned.Should().Equal(referenced,
            "tools/dom-bindings/pin.json records the AngleSharp versions Jint.Browser/Dom/Generated was produced from, and Directory.Packages.props records the historical upstream versions this test-only provenance oracle loads; they are the same two numbers");
    }

    [Test]
    public void TheTestOnlyProvenanceAssembliesAreThePinnedOnes()
    {
        var pinned = ReadPin();

        // An assembly version carries a fourth revision component a package version never states, and it
        // carries no prerelease suffix at all — AngleSharp.Css 1.1.1-beta.310 is assembly 1.1.1.0 — so this
        // compares the three numeric parts of the release portion rather than the whole string.
        Release(typeof(IElement).Assembly.GetName().Version!.ToString()).Should().Be(Release(pinned["AngleSharp"]));
        Release(typeof(ICssStyleDeclaration).Assembly.GetName().Version!.ToString()).Should().Be(Release(pinned["AngleSharp.Css"]));
    }

    private static SortedDictionary<string, string> ReadPin()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.PinPath));
        var packages = document.RootElement.GetProperty("packages");

        var pinned = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in packages.EnumerateObject())
        {
            pinned[property.Name] = property.Value.GetString()!;
        }

        return pinned;
    }

    private static SortedDictionary<string, string> ReadPackageVersions()
    {
        var text = File.ReadAllText(RepositoryPaths.PackagesPath);
        var versions = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in new[] { "AngleSharp", "AngleSharp.Css" })
        {
            var match = Regex.Match(
                text,
                "<PackageVersion\\s+Include=\"" + Regex.Escape(name) + "\"\\s+Version=\"(?<version>[^\"]+)\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));

            match.Success.Should().BeTrue("Directory.Packages.props must pin " + name);
            versions[name] = match.Groups["version"].Value;
        }

        return versions;
    }

    /// <summary>Major.minor.patch, with any prerelease suffix and any revision component dropped.</summary>
    private static string Release(string version)
    {
        var release = version.Split('-')[0];
        return string.Join('.', release.Split('.').Take(3));
    }
}
