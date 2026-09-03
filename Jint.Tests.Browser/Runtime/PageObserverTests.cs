using AngleSharp.Dom;
using Jint.Browser;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// What a page tells its one watcher, and when — the seam the whole protocol layer hangs off.
/// </summary>
public sealed class PageObserverTests
{
    /// <summary>
    /// <c>DocumentParsed</c> hands a watcher the runtime of a document it can actually look at, and it
    /// arrives before the commit phase that watcher forwards to a client.
    /// </summary>
    /// <remarks>
    /// <c>DocumentCreated</c> fires before the parse, when there is no tree, and <c>Phase</c> carries no
    /// runtime — so anything that wants to observe a document had to remember the runtime from the first and
    /// act on the second, which is a field that is a second answer to "which document is showing". The order
    /// asserted here is what makes the one call enough: parsed, then committed.
    /// </remarks>
    [Test]
    public async Task DocumentParsedCarriesTheParsedDocumentAndComesBeforeTheCommitPhase()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var recorder = new Recorder();

        // Registered on the loop, the way a protocol target registers itself, so no commit can slip between
        // taking the page and watching it.
        await page.RunOnLoopAsync(_ =>
        {
            page.Observe(recorder);
            return true;
        });

        await page.SetContentAsync("<p id='greeting'>hello</p>");

        recorder.Calls.Should().ContainInOrder("DocumentCreated", "DocumentParsed", "Phase(Committed)");
        recorder.Calls.Count(call => string.Equals(call, "DocumentParsed", StringComparison.Ordinal))
            .Should().Be(1, "one document is parsed once");

        // The runtime it carries is the document's, and the document is parsed: the element the markup
        // declares is there, where at DocumentCreated there was no document at all.
        recorder.CreatedGreeting.Should().BeNull("nothing of the document has been parsed yet");
        recorder.ParsedGreeting.Should().Be("hello");

        recorder.ParsedLoaderId.Should().Be(recorder.CreatedLoaderId, "it is the same document");
        recorder.ParsedLoaderId.Should().Be(recorder.CommittedLoaderId);
    }

    /// <summary>Records what it is told, on the loop it is told it on.</summary>
    private sealed class Recorder : IPageObserver
    {
        internal List<string> Calls { get; } = [];

        internal string? CreatedGreeting { get; private set; }

        internal string? ParsedGreeting { get; private set; }

        internal string? CreatedLoaderId { get; private set; }

        internal string? ParsedLoaderId { get; private set; }

        internal string? CommittedLoaderId { get; private set; }

        public void DocumentCreated(PageRuntime runtime, string loaderId)
        {
            Calls.Add("DocumentCreated");
            CreatedLoaderId = loaderId;
            CreatedGreeting = Greeting(runtime);
        }

        public void DocumentParsed(PageRuntime runtime, string loaderId)
        {
            Calls.Add("DocumentParsed");
            ParsedLoaderId = loaderId;
            ParsedGreeting = Greeting(runtime);
        }

        public void Phase(NavigationPhase phase, string loaderId)
        {
            Calls.Add("Phase(" + phase + ")");

            if (phase == NavigationPhase.Committed)
            {
                CommittedLoaderId = loaderId;
            }
        }

        private static string? Greeting(PageRuntime runtime)
            => runtime.Document?.QuerySelector("#greeting")?.TextContent;
    }
}
