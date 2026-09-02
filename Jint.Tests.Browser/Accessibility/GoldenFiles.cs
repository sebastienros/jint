namespace Jint.Tests.Browser.Accessibility;

/// <summary>
/// The fixture pages and their approved output, read from the checkout rather than from the test binary.
/// </summary>
/// <remarks>
/// <para>
/// Setting <c>JINT_BROWSER_GOLDEN=update</c> rewrites the approved file instead of failing, which is the
/// same discipline <c>JINT_SPEC_ANCHORS</c> and <c>JINT_DOM_BINDINGS</c> already use in this repository: the
/// diff is what gets reviewed, so a change to a snapshot has to be looked at rather than merely re-run.
/// </para>
/// <para>
/// Line endings are normalized on both sides, because a Windows checkout stores these as CRLF and the
/// renderers emit LF.
/// </para>
/// </remarks>
internal static class GoldenFiles
{
    private static string Directory => Path.Combine(RepositoryPaths.Root, "Jint.Tests.Browser", "Accessibility", "Golden");

    /// <summary>Whether the run was asked to rewrite the approved files.</summary>
    internal static bool Updating { get; } =
        string.Equals(Environment.GetEnvironmentVariable("JINT_BROWSER_GOLDEN"), "update", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the fixture page named <paramref name="name"/>.</summary>
    internal static string Page(string name) => Read(name + ".html");

    /// <summary>
    /// Asserts <paramref name="actual"/> against the approved file, or rewrites it when the run was asked to.
    /// </summary>
    internal static void Approve(string fileName, string actual)
    {
        var normalized = Normalize(actual);
        var path = Path.Combine(Directory, fileName);

        if (Updating || !File.Exists(path))
        {
            File.WriteAllText(path, normalized);
            if (!Updating)
            {
                Assert.Fail($"'{fileName}' did not exist and has been written. Review it, then run again.");
            }

            return;
        }

        Normalize(File.ReadAllText(path)).Should().Be(normalized,
            "the approved snapshot in '{0}' is the reviewed one; rerun with JINT_BROWSER_GOLDEN=update to rewrite it, then read the diff", fileName);
    }

    private static string Read(string fileName)
    {
        var path = Path.Combine(Directory, fileName);
        return File.Exists(path)
            ? Normalize(File.ReadAllText(path))
            : throw new FileNotFoundException($"No fixture '{fileName}' under '{Directory}'.", path);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
}
