using Jint.Browser;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The page API a host actually holds: load static content, run its scripts, read the result back as a CLR
/// value, and close it.
/// </summary>
public sealed class PageTests
{
    [Test]
    public async Task AnInlineScriptMutatesTheDomAndTheResultIsVisible()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            "<p id='greeting'>before</p><script>document.getElementById('greeting').textContent = 'hello'</script>");

        var text = await page.EvaluateAsync<string>("document.getElementById('greeting').textContent");

        text.Should().Be("hello");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ScriptsRunInDocumentOrderAsTheParserReachesThem()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // The second script sees what the first one did, and neither sees the paragraph that follows it: an
        // inline script runs at its own end tag, not after the parse.
        await page.SetContentAsync(
            """
            <script>window.order = ['first']; window.seen = []</script>
            <p id='one'></p>
            <script>window.order.push('second'); window.seen.push(document.getElementById('one') !== null, document.getElementById('two') !== null)</script>
            <p id='two'></p>
            """);

        var order = await page.EvaluateAsync<string>("window.order.join(',')");
        var seen = await page.EvaluateAsync<string>("window.seen.join(',')");

        order.Should().Be("first,second");
        seen.Should().Be("true,false");
    }

    [Test]
    public async Task EvaluateAnswersAClrValueAndNeverAJsValue()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var value = await page.EvaluateAsync("({ name: 'jint', version: 5 })");

        value.Should().NotBeNull();
        value!.GetType().Assembly.Should().NotBeSameAs(
            typeof(global::Jint.Native.JsValue).Assembly,
            "nothing belonging to an engine may leave the page loop");

        var bag = (IDictionary<string, object?>) value;
        bag["name"].Should().Be("jint");
        bag["version"].Should().Be(5d);
    }

    [Test]
    public async Task ATypedEvaluateConvertsNumbersAndStrings()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<int>("21 * 2")).Should().Be(42);
        (await page.EvaluateAsync<string>("'a' + 'b'")).Should().Be("ab");
        (await page.EvaluateAsync<bool>("1 < 2")).Should().BeTrue();
        (await page.EvaluateAsync<string>("null")).Should().BeNull();
    }

    [Test]
    public async Task TheDocumentUrlIsAboutBlankUntilSomethingElseIsLoaded()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        page.Url.Should().Be("about:blank");
        (await page.EvaluateAsync<string>("location.href")).Should().Be("about:blank");
        (await page.EvaluateAsync<string>("document.URL")).Should().Be("about:blank");
    }

    [Test]
    public async Task ADataUrlIsParsedAndReportsItsOwnHref()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        const string Url = "data:text/html,<title>from%20a%20data%20url</title><p>hi</p>";
        await page.NavigateAsync(Url);

        page.Url.Should().Be(Url);
        (await page.EvaluateAsync<string>("location.href")).Should().Be(Url);
        (await page.TitleAsync()).Should().Be("from a data url");
        (await page.EvaluateAsync<string>("document.querySelector('p').textContent")).Should().Be("hi");
    }

    [Test]
    public async Task ABase64DataUrlIsDecoded()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var payload = System.Convert.ToBase64String("<p id='x'>decoded</p>"u8.ToArray());
        await page.NavigateAsync("data:text/html;base64," + payload);

        (await page.EvaluateAsync<string>("document.getElementById('x').textContent")).Should().Be("decoded");
    }

    [Test]
    public async Task ASchemeThePageCannotReachIsRefusedWithASentence()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // http and https are a page's now; a scheme with no transport behind it still says what it can load.
        var act = async () => await page.NavigateAsync("ftp://example.com/file.txt");

        await act.Should().ThrowAsync<NavigationFailedException>().WithMessage("*http, https, about: and data:*");
    }

    [Test]
    public async Task ContentAnswersTheSerializedDocument()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p>content</p>");

        (await page.ContentAsync()).Should().Contain("<p>content</p>");
    }

    [Test]
    public async Task EverySetContentReplacesTheEngineSoNoGlobalSurvives()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>window.marker = 1</script>");
        (await page.EvaluateAsync<int>("window.marker")).Should().Be(1);

        await page.SetContentAsync("<p>second</p>");
        (await page.EvaluateAsync("typeof window.marker")).Should().Be("undefined");
    }

    [Test]
    public async Task ATypedEvaluateFillsANullable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<int?>("21 * 2")).Should().Be(42);
        (await page.EvaluateAsync<int?>("null")).Should().BeNull();
        (await page.EvaluateAsync<double?>("1.5")).Should().Be(1.5);
        (await page.EvaluateAsync<bool?>("undefined")).Should().BeNull();
    }

    [Test]
    public async Task AMalformedDataUrlAssignedFromScriptIsAPageErrorAndNotAFault()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // The assignment happens inside a running script, so a CLR exception here would erupt through the
        // parse and fault whatever the host was awaiting instead of the page.
        await page.SetContentAsync("<script>location.href = 'data:text/html;base64,!!!not base64!!!'</script>");

        // The navigation runs off the page's thread, so the error arrives shortly after the script does.
        (await page.WaitForNavigationAsync(TimeSpan.FromSeconds(2))).Should().BeFalse("the navigation never committed");

        page.Errors.Should().ContainSingle();
        page.Errors[0].Source.Should().Be("Navigation");
        page.Errors[0].Message.Should().Contain("not a valid data URL");
        (await page.EvaluateAsync<int>("1 + 1")).Should().Be(2);
    }

    [Test]
    public async Task WaitingForIdleDoesNotHoldUpClosing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script>setInterval(() => {}, 5)</script>");

        // A page with an interval is never idle, so this would wait out its whole ceiling; closing has to end
        // it rather than queue behind it.
        var idle = page.WaitForIdleAsync(TimeSpan.FromMinutes(5));

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await page.CloseAsync();
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);

        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "closing ends the wait rather than queueing behind it");

        var settled = await Task.WhenAny(idle, Task.Delay(TimeSpan.FromSeconds(30)));
        settled.Should().BeSameAs(idle, "the wait ends with the page rather than running out its own ceiling");

        // Whether it was cut short or never left the mailbox is a race with the loop, and both are answers.
        if (idle.IsCompletedSuccessfully)
        {
            idle.Result.Should().BeFalse();
        }
        else
        {
            idle.Exception!.InnerException.Should().BeOfType<ObjectDisposedException>();
        }
    }

    [Test]
    public async Task AClosedPageFailsEveryCall()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.CloseAsync();

        page.IsClosed.Should().BeTrue();

        var evaluate = async () => await page.EvaluateAsync("1");
        var content = async () => await page.ContentAsync();
        var navigate = async () => await page.SetContentAsync("<p></p>");

        await evaluate.Should().ThrowAsync<ObjectDisposedException>();
        await content.Should().ThrowAsync<ObjectDisposedException>();
        await navigate.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task TwoPagesEvaluateIndependently()
    {
        await using var browser = new Browser();
        var first = await browser.NewPageAsync();
        var second = await browser.NewPageAsync();

        await first.SetContentAsync("<script>window.who = 'first'</script>");
        await second.SetContentAsync("<script>window.who = 'second'</script>");

        (await first.EvaluateAsync<string>("window.who")).Should().Be("first");
        (await second.EvaluateAsync<string>("window.who")).Should().Be("second");
        browser.DefaultContext.Pages.Should().HaveCount(2);
    }

    [Test]
    public async Task EachPageRunsOnItsOwnThreadAndNeverTheCallers()
    {
        await using var browser = new Browser();
        var first = await browser.NewPageAsync();
        var second = await browser.NewPageAsync();

        var firstLoop = await LoopThreadIdAsync(first);
        var secondLoop = await LoopThreadIdAsync(second);

        firstLoop.Should().NotBe(Environment.CurrentManagedThreadId);
        secondLoop.Should().NotBe(Environment.CurrentManagedThreadId);
        firstLoop.Should().NotBe(secondLoop);
    }

    [Test]
    public async Task AContextClosesItsPages()
    {
        await using var browser = new Browser();
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await context.CloseAsync();

        page.IsClosed.Should().BeTrue();
        context.Pages.Should().BeEmpty();
        browser.Contexts.Should().NotContain(context);
    }

    [Test]
    public async Task AnExternalScriptIsNotRunAndTheReasonIsRecorded()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<script src='/app.js'></script><script type='module'>export const x = 1</script>");

        page.UnsupportedScripts.Should().HaveCount(2);
        page.UnsupportedScripts.Should().ContainSingle(entry => entry.Contains("/app.js", StringComparison.Ordinal));
        page.UnsupportedScripts.Should().ContainSingle(entry => entry.StartsWith("module script", StringComparison.Ordinal));
    }

    [Test]
    public async Task ChildFramesAreListedAndNoneOfThemIsScripted()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<iframe name='side' src='/side.html'></iframe><p>main</p>");

        page.MainFrame.IsScripted.Should().BeTrue();
        page.MainFrame.Frames.Should().ContainSingle();
        page.MainFrame.Frames[0].Name.Should().Be("side");
        page.MainFrame.Frames[0].Url.Should().Be("/side.html");
        page.MainFrame.Frames[0].IsScripted.Should().BeFalse();
        page.MainFrame.Frames[0].Parent.Should().BeSameAs(page.MainFrame);

        (await page.EvaluateAsync<int>("window.length")).Should().Be(1);
    }

    /// <summary>
    /// The id of the thread the page runs its script on, read from inside a dialog handler — which the
    /// specification requires to run synchronously inside the calling script.
    /// </summary>
    private static async Task<int> LoopThreadIdAsync(Page page)
    {
        var recorded = 0;

        void Handler(object? sender, DialogEventArgs args)
        {
            recorded = Environment.CurrentManagedThreadId;
            args.Accepted = true;
        }

        page.DialogOpened += Handler;

        try
        {
            await page.EvaluateAsync("confirm('which thread?')");
        }
        finally
        {
            page.DialogOpened -= Handler;
        }

        return recorded;
    }
}
