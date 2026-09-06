using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public class StyleSheetLoadingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task AParserStyleSheetLoadsAfterItsCssomIsReadyAndBeforeWindowLoad()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/site.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.events = [];
                  document.addEventListener('load', e => {
                    if (e.target.tagName === 'LINK') {
                      events.push('capture:' + e.isTrusted + ':' + e.bubbles + ':' + e.cancelable);
                    }
                  }, true);
                  document.addEventListener('load', () => events.push('bubbled'));
                  window.addEventListener('load', () => events.push('window'));
                </script>
                <link rel="stylesheet" href="/site.css"
                  onload="events.push('sheet:' + this.sheet.cssRules.length + ':' + (document.currentScript === null))">
                </head><body></body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("events.join(',')"))
            .Should().Be("capture:true:false:false,sheet:1:true,window");
        (await loopback.Page.EvaluateAsync<string>("getComputedStyle(document.body).fontSize")).Should().Be("33px");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AnInsertedStyleSheetNotifiesAfterTheScriptAndItsMicrotasks(bool missing)
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/site.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
            .MapHtml("/", "<!doctype html><html><head></head><body></body></html>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("window.href = '" + (missing ? "/missing.css" : "/site.css") + "';");

        await loopback.Page.EvaluateAsync("""
            window.events = [];
            var link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = href;
            link.onload = e => events.push('load:' + link.sheet.cssRules.length + ':' + e.isTrusted);
            link.onerror = e => events.push('error:' + (link.sheet === null) + ':' + e.isTrusted);
            document.head.appendChild(link);
            events.push('inserted');
            // Even listeners installed after insertion must hear the queued resource event.
            link.addEventListener('load', () => events.push('late-load'));
            link.addEventListener('error', () => events.push('late-error'));
            Promise.resolve().then(() => events.push('microtask'));
            """);
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("events.join(',')")).Should().Be(missing
            ? "inserted,microtask,error:true:true,late-error"
            : "inserted,microtask,load:1:true,late-load");

        if (missing)
        {
            loopback.Page.Errors.Should().ContainSingle(error => error.Message.Contains("/missing.css", StringComparison.Ordinal));
        }
        else
        {
            loopback.Page.Errors.Should().BeEmpty();
        }
    }

    [Test]
    public async Task AStyleSheetInsertedByANestedScriptWaitsForTheOutermostScript()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/site.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
            .Map("/insert.js", _ => LoopbackResponse.Script("""
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = '/site.css';
                link.onload = () => events.push('load:' + (document.currentScript === null));
                document.head.appendChild(link);
                events.push('inner');
                """))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.events = ['before'];
                  const script = document.createElement('script');
                  script.src = '/insert.js';
                  document.head.appendChild(script);
                  events.push('after');
                  queueMicrotask(() => events.push('microtask'));
                </script>
                </head><body></body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("events.join(',')"))
            .Should().Be("before,inner,after,microtask,load:true");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AStyleSheetLoadedByALoadHandlerAlsoDelaysWindowLoad(bool microtask)
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/first.css", _ => LoopbackResponse.Css("body { font-size: 22px; }"))
            .Map("/second.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.events = [];
                  window.addEventListener('load', () => events.push('window'));
                  function next() {
                    events.push('first');
                    const link = document.createElement('link');
                    link.rel = 'stylesheet';
                    link.href = '/second.css';
                    link.onload = () => events.push('second:' + getComputedStyle(document.body).fontSize);
                    document.head.appendChild(link);
                    events.push('inserted');
                  }
                </script>
                <link rel="stylesheet" href="/first.css" onload="MICROTASK">
                </head><body></body></html>
                """.Replace("MICROTASK", microtask ? "queueMicrotask(next)" : "next()", StringComparison.Ordinal)));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("events.join(',')"))
            .Should().Be("first,inserted,second:33px,window");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AStyleSheetLoadDuringAParserNetworkWaitSeesTheInstalledSheet()
    {
        using var ready = new ManualResetEventSlim();
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server
                .Map("/site.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
                .Map("/probe.js", _ =>
                {
                    ready.Wait(Timeout).Should().BeTrue("the stylesheet event must run while the parser waits");
                    return LoopbackResponse.Script("window.probe = window.rules;");
                })
                .MapHtml("/", """
                    <!doctype html><html><head>
                    <link rel="stylesheet" href="/site.css" onload="window.rules = this.sheet.cssRules.length; __ready()">
                    <script src="/probe.js"></script>
                    </head><body></body></html>
                    """),
            configureBrowser: options => options.ConfigureEngine(engineOptions =>
                engineOptions.Configure(engine => engine.SetValue("__ready", () => ready.Set()))));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<int>("probe")).Should().Be(1);
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AFailedParserStyleSheetFiresErrorExactlyOnceBeforeWindowLoad()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server.MapHtml("/", """
            <!doctype html><html><head>
            <script>
              window.events = [];
              window.addEventListener('load', () => events.push('window'));
            </script>
            <link rel="stylesheet" href="/missing.css"
              onload="events.push('unexpected-load')" onerror="events.push('error:' + (this.sheet === null))">
            </head><body></body></html>
            """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<string>("events.join(',')")).Should().Be("error:true,window");
        loopback.Page.Errors.Should().ContainSingle(error => error.Message.Contains("/missing.css", StringComparison.Ordinal));
    }

    [Test]
    public async Task AThrowingLoadHandlerDoesNotPreventOtherSheetsOrWindowLoad()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/site.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.events = [];
                  window.addEventListener('load', () => events.push('window'));
                </script>
                <link rel="stylesheet" href="/site.css" onload="throw new Error('stylesheet handler failed')">
                <link rel="stylesheet" href="/site.css" onload="events.push('second')">
                </head><body></body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("events.join(',')")).Should().Be("second,window");
        loopback.Page.Errors.Should().ContainSingle(error =>
            error.Message.Contains("stylesheet handler failed", StringComparison.Ordinal));
    }

    [Test]
    public async Task MovingALoadedLinkDoesNotLoadItAgain()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/site.css", _ => LoopbackResponse.Css("body { font-size: 33px; }"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>window.loads = 0;</script>
                <link id="sheet" rel="stylesheet" href="/site.css" onload="loads++">
                </head><body></body></html>
                """));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("document.body.appendChild(document.getElementById('sheet'));");
        await loopback.Page.WaitForIdleAsync(Timeout);

        (await loopback.Page.EvaluateAsync<int>("loads")).Should().Be(1);
        loopback.Server.Received.Should().ContainSingle(request => request.Path == "/site.css");
        loopback.Page.Errors.Should().BeEmpty();
    }
}
