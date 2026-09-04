using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// What the browser lane runs: the documents vendored under a suite, plus the wrapper the server synthesizes
/// for each of that suite's <c>.any.js</c> files.
/// </summary>
/// <remarks>
/// <para>
/// A <b>case</b> here is a path on the server rather than a file on disk, which is the one structural
/// difference from the <c>.any.js</c> lane. <c>dom/events/Event-propagation.html</c> is a vendored document;
/// <c>dom/events/Event-constructors.any.html</c> is not a file anywhere — it is what upstream's
/// <c>AnyHtmlHandler</c> manufactures for <c>Event-constructors.any.js</c>, and
/// <see cref="WptServerWrappers"/> is the port of it. Both are navigated to the same way and both report
/// through the same harness, so the driver treats them alike; only the inventory tells them apart, because
/// only one of the two is bytes in this repository.
/// </para>
/// <para>
/// The synthesized half is why the <c>.any.js</c> corpus is not vendored twice. A file the engine lane runs
/// in a bare engine runs here in a real <c>Window</c> realm under upstream's real <c>testharness.js</c>, and
/// the two lanes can disagree about it: a divergence that only a document exposes is exactly what this lane
/// exists to find.
/// </para>
/// </remarks>
internal static class WptBrowserCorpus
{
    /// <summary>
    /// Every case of one suite, vendored documents first and then the synthesized wrappers, each group
    /// ordered by path so the theory's cases are stable.
    /// </summary>
    internal static IReadOnlyList<string> Cases(string suite)
    {
        var cases = new List<string>();

        foreach (var file in WptCorpus.BrowserTestFiles(suite))
        {
            // A frame body is vendored and served and never run: it is the fixture a case loads, and running
            // it as one is a page that registers no test. WptBrowserExclusions.FrameBodies argues it.
            if (!IsFrameBody(file))
            {
                cases.Add(file);
            }
        }

        cases.AddRange(SynthesizedCases(suite));
        return cases;
    }

    /// <summary>Whether the path is one <see cref="WptBrowserExclusions.FrameBodies"/> names.</summary>
    internal static bool IsFrameBody(string path)
    {
        foreach (var (body, _) in WptBrowserExclusions.FrameBodies)
        {
            if (string.Equals(body, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The <c>&lt;name&gt;.any.html</c> wrappers for one suite's vendored <c>.any.js</c> files.
    /// </summary>
    /// <remarks>
    /// Only the window wrapper, and only where the file declares that global. A suite whose files are
    /// <c>// META: global=worker</c> contributes nothing here and is not silently run in a window, which is
    /// the refusal <c>WptServerWrappers</c> answers with a 404 — see its remarks for why the dedicated-worker
    /// wrapper is deliberately not generated at all.
    /// </remarks>
    internal static IEnumerable<string> SynthesizedCases(string suite)
    {
        foreach (var file in WptCorpus.TestFiles(suite))
        {
            var metadata = WptServerWrappers.ReadScriptMetadata(WptCorpus.Read(file));
            if (WptServerWrappers.IsExposedTo("window", metadata))
            {
                yield return file.Substring(0, file.Length - ".any.js".Length) + ".any.html";
            }
        }
    }

    /// <summary>Whether a case is a document this repository holds, rather than one the server makes up.</summary>
    internal static bool IsVendored(string path) => WptCorpus.Contains(path);
}
