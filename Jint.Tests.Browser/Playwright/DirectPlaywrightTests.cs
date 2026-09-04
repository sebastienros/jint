using Jint.Tests.Browser.Navigation;
using Microsoft.Playwright;

namespace Jint.Tests.Browser.PlaywrightAdapter;

/// <summary>
/// Microsoft.Playwright's public interfaces backed directly by Jint.Browser, without its Node driver or CDP.
/// </summary>
public sealed class DirectPlaywrightTests
{
    [Test]
    public async Task BrowserTypeCreatesTheExpectedObjectGraph()
    {
        IBrowserType browserType = global::Jint.Browser.Playwright.JintPlaywright.BrowserType;
        browserType.Name.Should().Be("jint");
        browserType.ExecutablePath.Should().BeEmpty();

        await using var browser = await browserType.LaunchAsync();
        IBrowserContext? createdContext = null;
        IPage? createdPage = null;
        var disconnected = false;
        var contextClosed = false;
        var pageClosed = false;

        browser.Context += (_, context) => createdContext = context;
        browser.Disconnected += (_, _) => disconnected = true;

        var context = await browser.NewContextAsync();
        context.Page += (_, page) => createdPage = page;
        context.Close += (_, _) => contextClosed = true;

        var page = await context.NewPageAsync();
        page.Close += (_, _) => pageClosed = true;

        createdContext.Should().BeSameAs(context);
        createdPage.Should().BeSameAs(page);
        browser.Contexts.Should().ContainSingle().Which.Should().BeSameAs(context);
        context.Pages.Should().ContainSingle().Which.Should().BeSameAs(page);
        context.Browser.Should().BeSameAs(browser);
        page.Context.Should().BeSameAs(context);
        page.MainFrame.Page.Should().BeSameAs(page);
        page.Frames.Should().ContainSingle().Which.Should().BeSameAs(page.MainFrame);
        browser.IsConnected.Should().BeTrue();
        context.IsClosed.Should().BeFalse();
        page.IsClosed.Should().BeFalse();

        await browser.CloseAsync();

        browser.IsConnected.Should().BeFalse();
        context.IsClosed.Should().BeTrue();
        page.IsClosed.Should().BeTrue();
        browser.Contexts.Should().BeEmpty();
        disconnected.Should().BeTrue();
        contextClosed.Should().BeTrue();
        pageClosed.Should().BeTrue();
    }

    [Test]
    public async Task PageSupportsContentEvaluationAndIndexedLocators()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <!doctype html>
            <title>Direct Playwright</title>
            <ul>
              <li>first</li>
              <li>second</li>
              <li>third</li>
            </ul>
            """);

        (await page.TitleAsync()).Should().Be("Direct Playwright");
        (await page.ContentAsync()).Should().Contain("<li>second</li>");
        (await page.EvaluateAsync<int>("() => 6 * 7")).Should().Be(42);
        (await page.EvaluateAsync<string>("value => value + '!'", "jint")).Should().Be("jint!");
        (await page.EvaluateAsync<int>("() => Promise.resolve(42)")).Should().Be(42);

        var items = page.Locator("li");
        (await items.CountAsync()).Should().Be(3);
        (await items.AllTextContentsAsync()).Should().Equal("first", "second", "third");
        (await items.First.TextContentAsync()).Should().Be("first");
        (await items.Nth(1).TextContentAsync()).Should().Be("second");
        (await items.Nth(1).CountAsync()).Should().Be(1);
        (await items.Nth(1).AllTextContentsAsync()).Should().Equal("second");
        (await items.Last.TextContentAsync()).Should().Be("third");
        (await items.Nth(-2).TextContentAsync()).Should().Be("second");
        (await items.First.IsVisibleAsync()).Should().BeTrue();

        var strict = async () => await items.TextContentAsync();
        await strict.Should().ThrowAsync<PlaywrightException>().WithMessage("*resolved to 3 elements*");
    }

    [Test]
    public async Task LocatorValueReadsWaitForElementsToAttach()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <script>
              setTimeout(() => {
                document.body.insertAdjacentHTML(
                  'beforeend',
                  '<p id="late-text">ready</p><input id="late-input" value="filled">');
              }, 20);
            </script>
            """);

        (await page.Locator("#late-text").TextContentAsync(
            new LocatorTextContentOptions { Timeout = 1_000 })).Should().Be("ready");
        (await page.Locator("#late-input").InputValueAsync(
            new LocatorInputValueOptions { Timeout = 1_000 })).Should().Be("filled");
    }

    [Test]
    public async Task BrowserConfigurationFlowsIntoJintBrowserOptions()
    {
        var browserType = global::Jint.Browser.Playwright.JintPlaywright.CreateBrowserType(
            options => options.UserAgent = "Jint.Browser.Playwright test");

        await using var browser = await browserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>configured</p>");

        (await page.EvaluateAsync<string>("() => navigator.userAgent")).Should().Be("Jint.Browser.Playwright test");
    }

    [Test]
    public async Task BrowserNewPageClosesItsImplicitContextWithThePage()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        var context = page.Context;

        browser.Contexts.Should().ContainSingle().Which.Should().BeSameAs(context);

        await page.CloseAsync();

        page.IsClosed.Should().BeTrue();
        context.IsClosed.Should().BeTrue();
        browser.Contexts.Should().BeEmpty();
    }

    [Test]
    public async Task ThrowingCreationHandlersCloseTheCreatedResources()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        IBrowserContext? createdContext = null;
        EventHandler<IBrowserContext> contextHandler = (_, context) =>
        {
            createdContext = context;
            throw new InvalidOperationException("context creation failed");
        };
        browser.Context += contextHandler;

        var newContext = async () => await browser.NewContextAsync();

        await newContext.Should().ThrowAsync<InvalidOperationException>().WithMessage("context creation failed");
        createdContext.Should().NotBeNull();
        createdContext!.IsClosed.Should().BeTrue();
        browser.Contexts.Should().BeEmpty();

        browser.Context -= contextHandler;
        var context = await browser.NewContextAsync();
        IPage? createdPage = null;
        context.Page += (_, page) =>
        {
            createdPage = page;
            throw new InvalidOperationException("page creation failed");
        };

        var newPage = async () => await context.NewPageAsync();

        await newPage.Should().ThrowAsync<InvalidOperationException>().WithMessage("page creation failed");
        createdPage.Should().NotBeNull();
        createdPage!.IsClosed.Should().BeTrue();
        context.Pages.Should().BeEmpty();
    }

    [Test]
    public async Task ThrowingPageCloseHandlerDoesNotLeakTheImplicitContext()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        var context = page.Context;
        page.Close += (_, _) => throw new InvalidOperationException("handler failed");

        var close = async () => await page.CloseAsync();

        await close.Should().ThrowAsync<InvalidOperationException>().WithMessage("handler failed");
        page.IsClosed.Should().BeTrue();
        context.IsClosed.Should().BeTrue();
        browser.Contexts.Should().BeEmpty();
    }

    [Test]
    public async Task ThrowingCloseHandlersDoNotStopContextCleanup()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var context = await browser.NewContextAsync();
        var first = await context.NewPageAsync();
        var second = await context.NewPageAsync();
        var secondClosed = false;
        var contextClosed = false;

        first.Close += (_, _) => throw new InvalidOperationException("page handler failed");
        second.Close += (_, _) => secondClosed = true;
        context.Close += (_, _) =>
        {
            contextClosed = true;
            throw new InvalidOperationException("context handler failed");
        };

        var close = async () => await context.CloseAsync();

        await close.Should().ThrowAsync<AggregateException>();
        first.IsClosed.Should().BeTrue();
        second.IsClosed.Should().BeTrue();
        secondClosed.Should().BeTrue();
        contextClosed.Should().BeTrue();
        context.IsClosed.Should().BeTrue();
        context.Pages.Should().BeEmpty();
        browser.Contexts.Should().BeEmpty();
    }

    [Test]
    public async Task ThrowingPageCloseHandlerDoesNotStopBrowserCleanup()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var firstContext = await browser.NewContextAsync();
        var secondContext = await browser.NewContextAsync();
        var first = await firstContext.NewPageAsync();
        var second = await secondContext.NewPageAsync();
        var secondClosed = false;
        var disconnected = false;

        first.Close += (_, _) => throw new InvalidOperationException("page handler failed");
        second.Close += (_, _) => secondClosed = true;
        browser.Disconnected += (_, _) => disconnected = true;

        var close = async () => await browser.CloseAsync();

        await close.Should().ThrowAsync<InvalidOperationException>().WithMessage("page handler failed");
        first.IsClosed.Should().BeTrue();
        second.IsClosed.Should().BeTrue();
        secondClosed.Should().BeTrue();
        firstContext.IsClosed.Should().BeTrue();
        secondContext.IsClosed.Should().BeTrue();
        browser.Contexts.Should().BeEmpty();
        browser.IsConnected.Should().BeFalse();
        disconnected.Should().BeTrue();
    }

    [Test]
    public async Task LocatorActionsUseJintsTrustedInputPath()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <!doctype html>
            <input class="editor" value="first">
            <input class="editor" value="second">
            <button>Save</button>
            <button>Save</button>
            <p id="log"></p>
            <script>
              document.querySelectorAll('button')[1].addEventListener('click', event => {
                document.getElementById('log').textContent = event.isTrusted ? 'trusted' : 'synthetic';
              });
              document.querySelectorAll('.editor')[1].addEventListener('keydown', event => {
                if (event.key === 'Enter') document.body.dataset.entered = 'yes';
              });
            </script>
            """);

        var editor = page.Locator(".editor").Last;
        await editor.FillAsync("changed");
        await editor.PressAsync("Enter");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).Last.ClickAsync();

        (await editor.InputValueAsync()).Should().Be("changed");
        (await page.EvaluateAsync<string>("() => document.body.dataset.entered")).Should().Be("yes");
        (await page.Locator("#log").TextContentAsync()).Should().Be("trusted");
    }

    /// <summary>A navigation that runs out of the action's time is this API's timeout, not the page's.</summary>
    /// <remarks>
    /// <para>
    /// <c>GotoAsync</c> hands the page the caller's deadline and waits on nothing else, so before the
    /// translation existed the only thing a caller could catch was <c>Jint.Browser</c>'s own
    /// <c>NavigationFailedException</c> — a type Playwright's contract never mentions.
    /// </para>
    /// <para>
    /// It is the deterministic half of <see cref="LocatorClickTimeoutIncludesNavigation"/>, which had two
    /// clocks expiring together: the action's <c>WaitAsync</c> and the page's navigation deadline. Which one
    /// was seen first was a race, so the click reported <see cref="TimeoutException"/> on one platform and
    /// <c>NavigationFailedException</c> on another. Both are the timeout now.
    /// </para>
    /// </remarks>
    [Test]
    public async Task GotoTimeoutIsAPlaywrightTimeout()
    {
        using var release = new SemaphoreSlim(0, 1);
        using var server = new LoopbackServer();
        server.Map("/slow.html", _ =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            return LoopbackResponse.Html("<title>slow</title>");
        });

        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        var goTo = async () => await page.GotoAsync(
            server.Url("/slow.html"),
            new PageGotoOptions { Timeout = 50 });

        await goTo.Should().ThrowAsync<System.TimeoutException>();

        release.Release();
    }

    [Test]
    public async Task LocatorClickTimeoutIncludesNavigation()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/index.html", "<a href='/slow.html'>slow</a>");
        server.Map("/slow.html", _ =>
        {
            Thread.Sleep(200);
            return LoopbackResponse.Html("<title>slow</title>");
        });

        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(server.Url("/index.html"));

        var click = async () => await page.Locator("a").ClickAsync(
            new LocatorClickOptions { Timeout = 50 });

        await click.Should().ThrowAsync<System.TimeoutException>();
    }

    [Test]
    public async Task WaitsNavigationAndHandlesRunInProcess()
    {
        using var server = new LoopbackServer();
        server.MapHtml(
            "/index.html",
            "<title>navigated</title><script>setTimeout(() => window.answer = 42, 20)</script>");

        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        var response = await page.GotoAsync(server.Url("/index.html"), new PageGotoOptions { Timeout = 0 });

        response.Should().NotBeNull();
        response!.Status.Should().Be(200);
        response.Ok.Should().BeTrue();
        response.Url.Should().Be(server.Url("/index.html"));
        response.Frame.Should().BeSameAs(page.MainFrame);
        (await response.FinishedAsync()).Should().BeNull();
        (await page.TitleAsync()).Should().Be("navigated");

        await using var handle = await page.WaitForFunctionAsync("() => window.answer");
        (await handle.JsonValueAsync<int>()).Should().Be(42);

        await page.SetContentAsync(
            "<title>replacement</title>",
            new PageSetContentOptions { Timeout = 0 });
        page.Url.Should().Be(server.Url("/index.html"));
        (await page.TitleAsync()).Should().Be("replacement");
    }

    [Test]
    public async Task WaitForFunctionReturnsTheValueThatSatisfiedTheWait()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<script>window.calls = 0; setTimeout(() => window.ready = true, 20)</script>");

        await using var handle = await page.WaitForFunctionAsync("() => Promise.resolve(++window.calls)");
        await page.WaitForFunctionAsync(
            "() => window.ready",
            options: new PageWaitForFunctionOptions { Timeout = 0 });

        (await handle.JsonValueAsync<int>()).Should().Be(1);
        (await page.EvaluateAsync<int>("() => window.calls")).Should().Be(1);
    }

    [Test]
    public async Task WaitForFunctionTimeoutInterruptsAPendingPromise()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        var wait = async () => await page.WaitForFunctionAsync(
            "() => new Promise(() => {})",
            options: new PageWaitForFunctionOptions { Timeout = 50 });

        await wait.Should().ThrowAsync<System.TimeoutException>();
    }

    [Test]
    public async Task WaitForFunctionTimeoutDoesNotWaitForSynchronousEvaluation()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        var wait = async () => await page.WaitForFunctionAsync(
            "() => { const end = Date.now() + 1000; while (Date.now() < end) {} return false; }",
            options: new PageWaitForFunctionOptions { Timeout = 50 });

        await wait.Should().ThrowAsync<System.TimeoutException>();
        System.Diagnostics.Stopwatch.GetElapsedTime(started).Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Test]
    public async Task UnsupportedOperationsNameThePlaywrightMember()
    {
        await using var browser = await global::Jint.Browser.Playwright.JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        var act = async () => await page.ScreenshotAsync();

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*IPage.ScreenshotAsync*");

        var option = () => page.Locator("p", new PageLocatorOptions { HasText = "text" });
        option.Should().Throw<NotSupportedException>().WithMessage("*PageLocatorOptions.HasText*");

        var frameOption = () => page.MainFrame.Locator(
            "p",
            new FrameLocatorOptions { HasText = "text" });
        frameOption.Should().Throw<NotSupportedException>().WithMessage("*FrameLocatorOptions.HasText*");
    }
}
