using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace Jint.Tests.Browser;

/// <summary>
/// The pin: <c>tools/dom-bindings/pin.json</c>, the central package versions, and the assemblies this
/// process actually loaded all have to say the same thing.
/// </summary>
/// <remarks>
/// A pin that drifts from the reference is the failure mode this exists for: the checked-in bindings would
/// still be a faithful picture of <em>some</em> version of AngleSharp, and nothing would say which. Bumping
/// is a code change — regenerate, read the diff of <c>Jint.Browser/Dom/Generated/</c>, and fix what it broke
/// in the same pull request.
/// </remarks>
public sealed class DomBindingsPinTests
{
    [Test]
    public void ThePinAndTheCentralPackageVersionsAgree()
    {
        var pinned = ReadPin();
        var referenced = ReadPackageVersions();

        pinned.Should().Equal(referenced,
            "tools/dom-bindings/pin.json records the AngleSharp versions Jint.Browser/Dom/Generated was produced from, and Directory.Packages.props records the ones Jint.Browser compiles against; they are the same two numbers");
    }

    [Test]
    public void TheLoadedAssembliesAreThePinnedOnes()
    {
        var pinned = ReadPin();

        // The assembly version drops the patch component for a prerelease suffix, so this compares the three
        // numeric parts rather than the whole string.
        Major(typeof(IElement).Assembly.GetName().Version!.ToString()).Should().Be(Major(pinned["AngleSharp"]));
        Major(typeof(ICssStyleDeclaration).Assembly.GetName().Version!.ToString()).Should().Be(Major(pinned["AngleSharp.Css"]));
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

    private static string Major(string version)
    {
        var parts = version.Split('.');
        return string.Join('.', parts.Take(3));
    }
}
