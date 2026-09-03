using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// An <c>Input</c> command is addressed to the <b>page</b> rather than to an execution context, so a
/// navigation replacing the document under it is never a reason to refuse it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape came out of a client suite.</b> <c>Keyboard.PressAsync("Enter")</c> is a <c>keyDown</c> and a
/// <c>keyUp</c>; the <c>keyDown</c> runs HTML's implicit submission, which navigates; and the <c>keyUp</c>
/// then arrives while the document it was sent for is being replaced. Chrome dispatches it to whatever
/// document is current when it runs — or to none, silently — and never answers
/// <c>Execution context was destroyed</c>, which is a sentence about a context a command named.
/// </para>
/// <para>
/// <b>Every one of these is deterministic rather than timed.</b> The outgoing document blocks the page loop
/// inside its own <c>pagehide</c> handler, so the commit is suspended at exactly the point the client's next
/// command has to arrive at, and the test releases it. A test that raced the commit would be a
/// continuous-integration leg that passes on a fast machine.
/// </para>
/// </remarks>
[NonParallelizable]
public class InputDuringNavigationTests
{
    /// <summary>How long the blocked page loop waits to be released before it gives up on the test.</summary>
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The flake's own shape: the key is queued for the engine that is going away, and is answered rather
    /// than refused.
    /// </summary>
    [Test]
    public async Task AKeyQueuedWhileTheDocumentIsReplacedIsAnsweredRatherThanRefused()
    {
        using var server = Origin();
        var gate = new CommitGate();

        await using var session = await PageSession.CreateAsync(Isolated(server), gate.Options());
        var attachment = await session.OpenPageAsync();
        await Watch(session, attachment);
        await session.ResultAsync("Page.navigate", Navigate(server, "/start.html"), attachment);

        var navigating = session.SendAsync("Page.navigate", Navigate(server, "/next.html"), attachment);
        await gate.Unloading;

        // The page loop is inside the commit now, held in the outgoing document's `pagehide` handler, so this
        // key queues for an engine that is about to be replaced.
        var key = session.SendAsync("Input.dispatchKeyEvent", KeyUp("Enter"), attachment);
        await gate.LetTheCommandArriveAsync();
        gate.Release();

        var reply = await key;
        await navigating;

        reply.TryGetProperty("error", out var error).Should().BeFalse(
            "an Input command is addressed to the page and it answered {0}", error);

        // And the page is the new document, still driveable: a key sent now reaches it, which is what says
        // the command ran against the target's current runtime rather than against a captured one.
        await session.ResultAsync("Input.dispatchKeyEvent", KeyUp("a"), attachment);
        (await Seen(session, attachment)).Should().Be("a");
        (await Evaluate(session, attachment, "location.pathname")).Should().Be("/next.html");
    }

    /// <summary>
    /// The other order: the key arrives while the document it was sent for is still the page's, so it is
    /// delivered to that one — and lost with it, as it is in a browser.
    /// </summary>
    [Test]
    public async Task AKeySentBeforeTheDocumentIsReplacedReachesTheOneThatIsStillThere()
    {
        using var server = Origin();
        var gate = new CommitGate();

        await using var session = await PageSession.CreateAsync(Isolated(server), gate.Options());
        var attachment = await session.OpenPageAsync();
        await Watch(session, attachment);
        await session.ResultAsync("Page.navigate", Navigate(server, "/start.html"), attachment);

        var key = session.SendAsync("Input.dispatchKeyEvent", KeyUp("Escape"), attachment);
        (await key).TryGetProperty("error", out _).Should().BeFalse();
        gate.SeenInTheOutgoingDocument.Should().Be("Escape");

        var navigating = session.SendAsync("Page.navigate", Navigate(server, "/next.html"), attachment);
        await gate.Unloading;
        gate.Release();
        await navigating;

        (await Seen(session, attachment)).Should().BeEmpty("the new document was not there when the key was dispatched");
    }

    /// <summary>
    /// The line this draws, from the other side: a command that really does name a context keeps Chrome's
    /// refusal when the document it named is replaced.
    /// </summary>
    [Test]
    public async Task AnEvaluationQueuedWhileTheDocumentIsReplacedIsStillRefused()
    {
        using var server = Origin();
        var gate = new CommitGate();

        await using var session = await PageSession.CreateAsync(Isolated(server), gate.Options());
        var attachment = await session.OpenPageAsync();
        await session.ResultAsync("Page.navigate", Navigate(server, "/start.html"), attachment);

        var navigating = session.SendAsync("Page.navigate", Navigate(server, "/next.html"), attachment);
        await gate.Unloading;

        var evaluating = session.SendAsync(
            "Runtime.evaluate",
            """{"expression":"1 + 1","returnByValue":true}""",
            attachment);

        await gate.LetTheCommandArriveAsync();
        gate.Release();

        var reply = await evaluating;
        await navigating;

        reply.TryGetProperty("error", out var error).Should().BeTrue("an evaluation names the context it runs in");
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Execution context was destroyed.");
    }

    /// <summary>
    /// A page that has <i>closed</i> keeps the refusal, which is the half of this that must not move: there
    /// is no document to dispatch to and no runtime to hand the command on to.
    /// </summary>
    /// <remarks>
    /// Closing unpublishes the target and detaches every session addressing it, so what the client is told is
    /// that its session is gone rather than that a context was destroyed. Either way it is an error and not a
    /// silent success — a client whose page has closed must not be told its key was delivered.
    /// </remarks>
    [Test]
    public async Task AKeyForAPageThatHasClosedIsRefused()
    {
        using var server = Origin();
        var gate = new CommitGate();

        await using var session = await PageSession.CreateAsync(Isolated(server), gate.Options());
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Page.navigate", Navigate(server, "/next.html"), attachment);
        await page.CloseAsync();

        var reply = await session.SendAsync("Input.dispatchKeyEvent", KeyUp("Enter"), attachment);

        reply.TryGetProperty("error", out var error).Should().BeTrue("there is no page left to dispatch to");
        error.GetProperty("code").GetInt32().Should().Be(-32001);
        error.GetProperty("message").GetString().Should().Be("Session with given id not found.");
    }

    /// <summary>
    /// The two documents: one that records what it is sent and blocks the loop while it unloads, and one that
    /// only records.
    /// </summary>
    private static LoopbackServer Origin()
    {
        var server = new LoopbackServer();

        server.MapHtml(
            "/start.html",
            """
            <html><body><p>start</p><script>
              window.addEventListener('keyup', e => __record(e.key));
              window.addEventListener('pagehide', () => { __unloading(); __release(); });
            </script></body></html>
            """);

        server.MapHtml("/next.html", "<html><body><p>next</p></body></html>");
        return server;
    }

    private static BrowserContextOptions Isolated(LoopbackServer server) => new() { UrlFilter = server.Owns };

    private static string Navigate(LoopbackServer server, string path) => $$"""{"url":"{{server.Url(path)}}"}""";

    private static string KeyUp(string key) => $$"""{"type":"keyUp","key":"{{key}}","code":"Key"}""";

    /// <summary>Installs the recorder every new document of the page gets, before its own scripts run.</summary>
    private static Task Watch(PageSession session, string attachment) => session.ResultAsync(
        "Page.addScriptToEvaluateOnNewDocument",
        """{"source":"window.__seen = ''; window.addEventListener('keyup', e => { window.__seen += e.key; });"}""",
        attachment);

    private static Task<string> Seen(PageSession session, string attachment)
        => Evaluate(session, attachment, "String(window.__seen || '')");

    private static async Task<string> Evaluate(PageSession session, string attachment, string expression)
    {
        var result = await session.EvaluateAsync(expression, attachment);
        return result.GetProperty("value").GetString() ?? "";
    }

    /// <summary>
    /// The page loop, stopped inside the outgoing document's <c>pagehide</c> handler until the test lets it
    /// go — which is what makes "a command that arrives while the document is being replaced" a moment a test
    /// can address rather than a race it has to win.
    /// </summary>
    private sealed class CommitGate
    {
        private readonly TaskCompletionSource _unloading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new(initialState: false);

        private string _seen = "";

        /// <summary>Completes when the page loop is held inside the outgoing document's unload.</summary>
        internal Task Unloading => _unloading.Task;

        /// <summary>What the outgoing document has been sent, readable from off the page's thread.</summary>
        /// <remarks>
        /// Recorded by the document into a field rather than read back with <c>Runtime.evaluate</c>, because
        /// by the time the commit is released the document that saw it is gone.
        /// </remarks>
        internal string SeenInTheOutgoingDocument => Volatile.Read(ref _seen);

        /// <summary>Options whose pages carry the three host functions the gate is built out of.</summary>
        internal BrowserOptions Options()
        {
            // The bracket around a page turn is what would otherwise abandon the request the gate holds still.
            var options = new BrowserOptions { MaxTaskDuration = Timeout.InfiniteTimeSpan };

            options.ConfigureEngine(engineOptions => engineOptions.Configure(engine =>
            {
                engine.SetValue("__unloading", new Action(() => _unloading.TrySetResult()));
                engine.SetValue("__release", new Action(() => _release.Wait(_bound)));
                engine.SetValue("__record", new Action<string>(seen => Volatile.Write(ref _seen, seen)));
            }));

            return options;
        }

        /// <summary>Gives the command sent on the line above time to reach the mailbox it queues on.</summary>
        /// <remarks>
        /// The enqueue is synchronous on the sending thread and does no I/O, so this is slack rather than a
        /// race; what it must not do is release the commit while the command is still on its way, which would
        /// test nothing at all. The first test it guards fails against the unfixed code with exactly the
        /// error the issue reported, which is what says the moment is really being hit.
        /// </remarks>
        internal Task LetTheCommandArriveAsync() => Task.Delay(TimeSpan.FromMilliseconds(250));

        /// <summary>Lets the commit finish.</summary>
        internal void Release() => _release.Set();
    }
}
