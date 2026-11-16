#nullable enable
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Runtime;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Page.AddInitScriptAsync</c> and <c>BrowserContext.AddInitScriptAsync</c>: scripts that run in every new
/// document before the document's own, with no protocol client attached (#4620).
/// </summary>
public sealed class InitScriptTests
{
    [Test]
    public async Task AnInitScriptRunsWhileTheDocumentIsStillLoading()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/", "<html><body>page</body></html>"));

        await fixture.Page.AddInitScriptAsync("window.__early = document.readyState");
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAsync<string>("String(window.__early)")).Should().Be("loading");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnInitScriptRunsBeforeTheFirstParserInsertedScriptAndCanStubWhatItCalls()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<html><head><script>window.order = (window.order || '') + 'document';"
            + "document.documentElement.scrollIntoView();</script></head><body></body></html>"));

        await fixture.Page.AddInitScriptAsync("""
            window.order = 'init:';
            Element.prototype.scrollIntoView = function () { window.scrolled = this.localName; };
            """);
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAsync<string>("window.order")).Should().Be("init:document");
        (await fixture.Page.EvaluateAsync<string>("window.scrolled")).Should().Be("html");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnInitScriptIsNotRunInTheDocumentAlreadyShowingButSurvivesEveryLaterOne()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/one", "<html><body>one</body></html>")
            .MapHtml("/two", "<html><body>two</body></html>"));
        var page = fixture.Page;
        await page.NavigateAsync(fixture.Url("/one"));

        await page.AddInitScriptAsync("window.runs = (window.runs || 0) + 1; window.at = location.pathname;");
        (await page.EvaluateAsync<bool>("window.runs === undefined")).Should().BeTrue("the current document already ran its scripts");

        await page.NavigateAsync(fixture.Url("/two"));
        (await page.EvaluateAsync<string>("window.at + ':' + window.runs")).Should().Be("/two:1");

        await page.ReloadAsync();
        (await page.EvaluateAsync<string>("window.at + ':' + window.runs")).Should().Be("/two:1", "a reload is a new document with a new engine");

        await page.SetContentAsync("<p>content</p>", fixture.Url("/three"));
        (await page.EvaluateAsync<string>("window.at + ':' + window.runs")).Should().Be("/three:1");
    }

    [Test]
    public async Task AFunctionInitScriptReceivesItsArgumentAsDataAndCanSeedStorageBeforeThePageReadsIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<html><body><script>window.seen = localStorage.getItem('consent');</script></body></html>"));

        var argument = new { key = "consent", value = "granted'); throw new Error('interpolated" };
        await fixture.Page.AddInitScriptAsync("(entry) => localStorage.setItem(entry.key, entry.value)", argument);
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAsync<string>("window.seen")).Should().Be(argument.value);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ContextInitScriptsReachOpenAndNewPagesAndRunBeforeAPagesOwn()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<html><body><script>window.order = (window.order || '') + 'document';</script></body></html>"));
        var open = fixture.Page;

        await open.AddInitScriptAsync("window.order = (window.order || '') + 'page:';");
        await fixture.Context.AddInitScriptAsync("window.order = (window.order || '') + 'context:';");
        await fixture.Context.AddInitScriptAsync("(suffix) => { window.order += suffix; }", "argument:");

        await open.NavigateAsync(fixture.Url("/"));
        (await open.EvaluateAsync<string>("window.order")).Should().Be("context:argument:page:document");

        var later = await fixture.NewPageAsync();
        (await later.EvaluateAsync<string>("window.order")).Should().Be("context:argument:", "a new page's first about:blank is a new document of the context");
        await later.NavigateAsync(fixture.Url("/"));
        (await later.EvaluateAsync<string>("window.order")).Should().Be("context:argument:document", "a page's own init scripts are its alone");

        var isolated = await fixture.NewIsolatedPageAsync();
        await isolated.NavigateAsync(fixture.Url("/"));
        (await isolated.EvaluateAsync<string>("window.order")).Should().Be("document", "another context shares nothing");
    }

    [Test]
    public async Task AnInitScriptThatThrowsIsAPageErrorAndTheNextOneAndTheDocumentStillRun()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<html><body><script>window.documentRan = true;</script></body></html>"));

        await fixture.Page.AddInitScriptAsync("throw new Error('broken instrumentation')");
        await fixture.Page.AddInitScriptAsync("window.secondRan = true");
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAsync<bool>("window.secondRan === true && window.documentRan === true")).Should().BeTrue();
        var error = fixture.Page.Errors.Should().ContainSingle().Subject;
        error.Kind.Should().Be(PageErrorKind.ScriptError);
        error.Message.Should().Contain("broken instrumentation");
        error.Source.Should().Be("Page.AddInitScriptAsync#1");
    }

    [Test]
    public async Task AnInitScriptThatExhaustsItsBudgetIsBoundedAloneAndTheDocumentStillLoads()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/", "<html><body><script>window.documentRan = true;</script></body></html>"),
            configureBrowser: options => options.MaxTaskDuration = TimeSpan.FromMilliseconds(200));

        await fixture.Page.AddInitScriptAsync("for (;;) {}");
        await fixture.Page.AddInitScriptAsync("window.secondRan = true");
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAsync<bool>("window.secondRan === true && window.documentRan === true")).Should().BeTrue();
        fixture.Page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
    }

    [Test]
    public async Task AnInitScriptErrorIsReportedAtTheGlobalScopeAndDocumentWriteFromOneIsRefused()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/",
            "<html><body><p id=kept>kept</p></body></html>"));

        await fixture.Page.AddInitScriptAsync("window.heard = []; addEventListener('error', e => heard.push(e.message));");
        await fixture.Page.AddInitScriptAsync("document.write('<p id=written>written</p>')");
        await fixture.Page.AddInitScriptAsync("null.boom");
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.EvaluateAsync<string>(
            "[document.getElementById('kept')?.textContent, String(document.getElementById('written')), heard.length].join()"))
            .Should().Be("kept,null,1", "the parse fills the document it was handed, and the throw reached the window's listener");
        fixture.Page.Errors.Should().Contain(error => error.Kind == PageErrorKind.ScriptError && error.Source == "Page.AddInitScriptAsync#3");
    }

    [Test]
    public async Task AClosedPageOrContextRefusesAnInitScript()
    {
        await using var browser = new Browser();
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.CloseAsync();
        var onPage = () => page.AddInitScriptAsync("void 0");
        await onPage.Should().ThrowAsync<ObjectDisposedException>();

        await context.CloseAsync();
        var onContext = () => context.AddInitScriptAsync("void 0");
        await onContext.Should().ThrowAsync<ObjectDisposedException>();
    }
}
