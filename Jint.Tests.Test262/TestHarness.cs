using Test262Harness;
using Zio;

namespace Jint.Tests.Test262;

/// <summary>
/// Handles initializing testing state.
/// </summary>
public partial class TestHarness
{
    private const string HarnessRoot = "/harness/";

    private static partial Task InitializeCustomState()
    {
        // Test262Harness hands us State.HarnessFiles from the top level of harness/ only, and keys
        // nothing: an include is looked up by the exact string the test's frontmatter wrote. The
        // staging/ tests ported from SpiderMonkey include their helpers as "sm/non262-Set-shell.js",
        // and those live in harness/sm/, so enumerate the whole tree ourselves and key on the path
        // relative to harness/. A top-level file's relative path is its file name, so the existing
        // includes keep resolving exactly as before.
        var fileSystem = State.Test262Stream.Options.FileSystem;

        foreach (var path in fileSystem.EnumerateFiles(HarnessRoot, "*.js", SearchOption.AllDirectories))
        {
            var fullName = path.FullName;

            using var stream = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            foreach (var file in Test262File.FromStream(stream, fullName, generateInverseStrictTestCase: false))
            {
                var script = Engine.PrepareScript(file.Program, source: fullName);
                State.Sources[fullName.Substring(HarnessRoot.Length)] = script;
            }
        }

        return Task.CompletedTask;
    }
}
