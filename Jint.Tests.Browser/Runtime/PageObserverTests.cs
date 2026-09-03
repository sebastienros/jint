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

    /// <summary>
    /// A title the markup declares is reported once, at the phase that already reported it — the end of the
    /// turn looks again and finds nothing has moved.
    /// </summary>
    /// <remarks>
    /// The page looks at <c>document.title</c> at the end of every one of its loop's turns, so that a script
    /// which sets it after <c>load</c> is not sat on until the next navigation. The three navigation phases
    /// look at it too, and a document's <c>&lt;title&gt;</c> is in place by the commit — so without one place
    /// deciding, one title would be announced four times over. <c>Page.ReportTitle</c> is that place, and
    /// this is what holds it to one call per move.
    /// </remarks>
    [Test]
    public async Task ATitleTheMarkupDeclaresIsReportedOnceAtThePhaseThatAlreadyReportedIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var recorder = new Recorder();
        await page.RunOnLoopAsync(_ =>
        {
            page.Observe(recorder);
            return true;
        });

        await page.SetContentAsync("<html><head><title>Parsed</title></head><body><p>x</p></body></html>");
        await page.WaitForIdleAsync(TimeSpan.FromSeconds(5));

        // One document's title is one move, however many places look at it.
        recorder.Titles.Should().Equal(new[] { "Parsed" });
        recorder.Calls.Should().ContainInOrder("Phase(Committed)", "TitleChanged(Parsed)");
    }

    /// <summary>
    /// A page nobody is watching is never asked what its title is, and one that is watched again resumes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The end-of-turn look is not free: <c>IDocument.Title</c> is AngleSharp's depth-first search for the
    /// first <c>&lt;title&gt;</c> element, which stops at one and walks the whole tree when there is none. So
    /// the observer is checked <i>first</i> and a page with none returns before touching the document at all
    /// — which is most pages, because <c>Page.Observer</c> is null for every page nobody is driving over the
    /// protocol.
    /// </para>
    /// <para>
    /// What is asserted is the seam rather than the timing: the title moves while nothing is watching and no
    /// call is made about it, and the move is announced on the next turn once a watcher is back.
    /// </para>
    /// </remarks>
    [Test]
    public async Task APageNobodyIsWatchingIsNeverAskedForItsTitle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var recorder = new Recorder();
        await page.RunOnLoopAsync(_ =>
        {
            page.Observe(recorder);
            return true;
        });

        await page.SetContentAsync("<html><head><title>Watched</title></head><body></body></html>");
        await page.WaitForIdleAsync(TimeSpan.FromSeconds(5));
        recorder.Titles.Should().Equal(new[] { "Watched" });

        await page.RunOnLoopAsync(_ =>
        {
            page.Observe(null);
            return true;
        });

        await page.EvaluateAsync("document.title = 'Unwatched'");

        // A second request, so that the turn the assignment ran in has ended before the assertion: the look
        // this test says does not happen is the one at the end of that turn.
        await page.EvaluateAsync("1");
        (await page.TitleAsync()).Should().Be("Unwatched", "the title really did move");
        // A page with no observer looks at nothing and tells nobody.
        recorder.Titles.Should().Equal(new[] { "Watched" });

        await page.RunOnLoopAsync(_ =>
        {
            page.Observe(recorder);
            return true;
        });

        await page.EvaluateAsync("1");
        await page.EvaluateAsync("1");

        // A watcher that is back is told on the next turn.
        recorder.Titles.Should().Equal(new[] { "Watched", "Unwatched" });
    }

    /// <summary>Records what it is told, on the loop it is told it on.</summary>
    private sealed class Recorder : IPageObserver
    {
        internal List<string> Calls { get; } = [];

        /// <summary>Every title it has been told, in order, so a second report of one is visible.</summary>
        internal List<string> Titles { get; } = [];

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

        public void TitleChanged(string title)
        {
            Calls.Add("TitleChanged(" + title + ")");
            Titles.Add(title);
        }

        private static string? Greeting(PageRuntime runtime)
            => runtime.Document?.QuerySelector("#greeting")?.TextContent;
    }
}
