using System.Globalization;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Fixtures;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// One fixture, open on one page of one browser, with the driver injected and the whole thing torn down
/// together.
/// </summary>
/// <remarks>
/// <para>
/// A course case is the same four steps every time — serve the corpus, open a page on it, drive it, read the
/// end state — and the last of them is the assertion that matters: the DOM says what the fixture's README row
/// says it should, and <see cref="Page.Errors"/> is empty. A fixture whose framework threw would still render
/// <i>something</i>, so the error sink is what tells a half-working page apart from a working one.
/// </para>
/// <para>
/// <b>The driver is injected, never loaded.</b> <c>Fixtures/harness.js</c> is evaluated into the page after
/// the load rather than referenced by the document, because the same fixtures are driven by PuppeteerSharp
/// and by Playwright over the protocol with no help from it — a fixture that needed the harness would be a
/// fixture only this suite could run.
/// </para>
/// </remarks>
internal sealed class FixtureCourse : IAsyncDisposable
{
    /// <summary>How long a wait may take before it is a failure rather than a slow machine.</summary>
    internal static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    private FixtureCourse(LoopbackServer server, Browser browser, BrowserContext context, Page page)
    {
        Server = server;
        Browser = browser;
        Context = context;
        Page = page;
    }

    internal LoopbackServer Server { get; }

    internal Browser Browser { get; }

    internal BrowserContext Context { get; }

    internal Page Page { get; }

    /// <summary>Serves the corpus, opens a page, navigates to one fixture and injects the driver.</summary>
    internal static async Task<FixtureCourse> OpenAsync(
        string fixture,
        Action<LoopbackServer>? routes = null,
        Action<BrowserOptions>? configureBrowser = null)
    {
        var server = FixtureOrigin.Serve(new LoopbackServer());
        routes?.Invoke(server);

        var options = new BrowserOptions();
        configureBrowser?.Invoke(options);

        var browser = new Browser(options);
        var context = await browser.NewContextAsync(new BrowserContextOptions { UrlFilter = server.Owns }).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        var course = new FixtureCourse(server, browser, context, page);

        await course.GoAsync(fixture).ConfigureAwait(false);
        return course;
    }

    /// <summary>The URL of a path on the origin serving the corpus.</summary>
    internal string Url(string path) => Server.Url(path);

    /// <summary>Navigates to a fixture's entry document and re-injects the driver.</summary>
    /// <remarks>
    /// The driver is per document: a navigation replaces the page's engine, so what an earlier document was
    /// given is gone with it. Every navigation a case makes therefore goes through here.
    /// </remarks>
    internal async Task GoAsync(string fixture)
    {
        var response = await Page.NavigateAsync(FixtureOrigin.Url(Server, fixture)).ConfigureAwait(false);

        response.Should().NotBeNull("'" + fixture + "' should have loaded");
        response!.Ok.Should().BeTrue("'" + fixture + "' answered " + response.Status.ToString(CultureInfo.InvariantCulture));

        await InjectAsync().ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Puts the driver back on a document that replaced the one it was injected into.</summary>
    internal Task InjectAsync() => Page.EvaluateAsync(FixtureCorpus.Read("harness.js"));

    /// <summary>Runs the page until it has nothing scheduled, or until the bound runs out.</summary>
    /// <remarks>
    /// Not asserted: a fixture with a running interval is never idle, and that is a property of the fixture
    /// rather than a failure. What a case asserts is the end state, through <see cref="UntilAsync"/>.
    /// </remarks>
    internal Task SettleAsync() => Page.WaitForIdleAsync(TimeSpan.FromSeconds(2));

    /// <summary>Clicks the first match and lets whatever it started finish.</summary>
    internal async Task ClickAsync(string selector)
    {
        await Page.EvaluateAsync("__h.click(" + Literal(selector) + ")").ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Clicks one match of a repeated selector and lets whatever it started finish.</summary>
    internal async Task ClickAtAsync(string selector, int index)
    {
        await Page.EvaluateAsync("__h.clickAt(" + Literal(selector) + ", " + index.ToString(CultureInfo.InvariantCulture) + ")").ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Types into a field and lets whatever it started finish.</summary>
    internal async Task TypeAsync(string selector, string text)
    {
        await Page.EvaluateAsync("__h.type(" + Literal(selector) + ", " + Literal(text) + ")").ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Types into a field and then presses Enter, as two turns.</summary>
    /// <remarks>
    /// <b>Two turns, not one, and it is not a nicety.</b> A user's keystroke and the <c>input</c> that
    /// follows it are separate tasks, so a browser runs a microtask checkpoint between them — and a library
    /// that re-renders on a microtask (Preact does; React flushes a discrete event synchronously and does
    /// not) would otherwise still be showing the previous render when the <c>keydown</c> handler runs, and
    /// would read a draft it had already cleared. Driving both from one script is the thing a real client
    /// cannot do, so the driver does not do it either.
    /// </remarks>
    internal async Task EnterAsync(string selector, string text)
    {
        await TypeAsync(selector, text).ConfigureAwait(false);
        await PressAsync(selector, "Enter").ConfigureAwait(false);
    }

    /// <summary>Clicks something that navigates, waits for the commit and re-injects the driver.</summary>
    /// <remarks>
    /// The wait is armed <i>before</i> the click, which is the order every navigation test here uses: a
    /// navigation a script started runs off the page's own thread, so one registered afterwards can miss a
    /// commit that already happened.
    /// </remarks>
    internal async Task ClickAndNavigateAsync(string selector)
    {
        var navigated = Page.WaitForNavigationAsync(Bound);
        await Page.EvaluateAsync("__h.click(" + Literal(selector) + ")").ConfigureAwait(false);

        (await navigated.ConfigureAwait(false)).Should().BeTrue("clicking '" + selector + "' should have navigated");

        await InjectAsync().ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Presses a key at an element and lets whatever it started finish.</summary>
    internal async Task PressAsync(string selector, string key)
    {
        await Page.EvaluateAsync("__h.press(" + Literal(selector) + ", " + Literal(key) + ")").ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>The first match's trimmed text.</summary>
    internal Task<string?> TextAsync(string selector)
        => Page.EvaluateAsync<string>("__h.text(" + Literal(selector) + ")");

    /// <summary>Every match's trimmed text, joined with <c>|</c>.</summary>
    internal Task<string?> TextsAsync(string selector)
        => Page.EvaluateAsync<string>("__h.texts(" + Literal(selector) + ")");

    /// <summary>How many elements match.</summary>
    internal Task<int> CountAsync(string selector)
        => Page.EvaluateAsync<int>("__h.count(" + Literal(selector) + ")");

    /// <summary>One property of the first match, stringified.</summary>
    internal Task<string?> PropertyAsync(string selector, string name)
        => Page.EvaluateAsync<string>("__h.prop(" + Literal(selector) + ", " + Literal(name) + ")");

    /// <summary>One attribute of the first match.</summary>
    internal Task<string?> AttributeAsync(string selector, string name)
        => Page.EvaluateAsync<string>("__h.attr(" + Literal(selector) + ", " + Literal(name) + ")");

    /// <summary>The first match's inner markup.</summary>
    internal Task<string?> HtmlAsync(string selector)
        => Page.EvaluateAsync<string>("__h.html(" + Literal(selector) + ")");

    /// <summary>
    /// Pumps until an expression answers <paramref name="expected"/>, failing rather than hanging.
    /// </summary>
    /// <remarks>
    /// A framework's re-render is not always on a lane <see cref="SettleAsync"/> can see the end of — React
    /// schedules through a task, htmx through a fetch, an observer through a timer entry — so a case that
    /// asserts an end state waits for it rather than assuming one settle was enough.
    /// </remarks>
    internal async Task UntilAsync(string expression, string expected)
    {
        var deadline = Environment.TickCount64 + (long) Bound.TotalMilliseconds;
        string? seen = null;

        while (Environment.TickCount64 < deadline)
        {
            seen = await Page.EvaluateAsync<string>("String(" + expression + ")").ConfigureAwait(false);

            if (string.Equals(seen, expected, StringComparison.Ordinal))
            {
                return;
            }

            await SettleAsync().ConfigureAwait(false);
            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Fail($"'{expression}' was still '{seen}' rather than '{expected}' after {Bound}. Errors: {Errors()}");
    }

    /// <summary>Pumps until a condition the <i>host</i> can see holds, failing rather than hanging.</summary>
    /// <remarks>
    /// <see cref="UntilAsync"/>'s sibling for what only this side knows — what the origin was asked for,
    /// what the request log holds. The page is settled between polls, because a page nobody is pumping makes
    /// no requests.
    /// </remarks>
    internal async Task WaitForAsync(Func<bool> condition, string because)
    {
        var deadline = Environment.TickCount64 + (long) Bound.TotalMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return;
            }

            await SettleAsync().ConfigureAwait(false);
            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Fail($"{because} — still false after {Bound}. Errors: {Errors()}");
    }

    /// <summary>Fails unless the page reported nothing at all.</summary>
    /// <remarks>
    /// The whole point of the course: a page that renders the right markup while reporting an unhandled
    /// rejection is a page that half worked, and a fixture that is documented to produce an error names
    /// exactly that one instead of calling this.
    /// </remarks>
    internal void ShouldHaveReportedNothing()
        => Page.Errors.Should().BeEmpty("a fixture that works reports nothing: " + Errors());

    /// <summary>Every error the page recorded, as one line each.</summary>
    internal string Errors() => Page.Errors.Count == 0 ? "(none)" : string.Join("; ", Page.Errors.Select(e => e.ToString()));

    public async ValueTask DisposeAsync()
    {
        await Browser.CloseAsync().ConfigureAwait(false);
        Server.Dispose();
    }

    /// <summary>A JavaScript string literal for a value a test wrote.</summary>
    private static string Literal(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
