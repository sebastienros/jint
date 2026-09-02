using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// HTML's <i>prepare a script element</i>, over a real socket: what runs, in what order, and what a page
/// hears when one of them cannot be loaded.
/// </summary>
/// <remarks>
/// Everything here needs an origin — a relative <c>src</c> against <c>about:blank</c> resolves to nothing —
/// so every case navigates to the loopback server rather than setting content.
/// </remarks>
public class ScriptLoadingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task ExternalScriptsRunInDocumentOrderAndBlockTheParser()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/first.js", _ => LoopbackResponse.Script("window.log.push('first:' + Boolean(document.getElementById('p')));"))
            .Map("/second.js", _ => LoopbackResponse.Script("window.log.push('second:' + Boolean(document.getElementById('p')));"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>window.log = [];</script>
                <script src="/first.js"></script>
                <script src="/second.js"></script>
                </head><body><p id="p">here</p>
                <script>window.log.push('inline:' + Boolean(document.getElementById('p')));</script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The first two run while the parser is still in <head>, so the paragraph below them does not exist
        // yet: that is what "parser-blocking" means, observed from inside the scripts themselves.
        (await loopback.Page.EvaluateAsync<string>("window.log.join(',')"))
            .Should().Be("first:false,second:false,inline:true");
    }

    [Test]
    public async Task DeferredScriptsRunAfterTheParseInOrderAndBeforeDomContentLoaded()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/d1.js", _ => LoopbackResponse.Script("window.log.push('defer1');"))
            .Map("/d2.js", _ => LoopbackResponse.Script("window.log.push('defer2');"))
            .Map("/blocking.js", _ => LoopbackResponse.Script("window.log.push('blocking');"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.log = [];
                  document.addEventListener('DOMContentLoaded', () => window.log.push('DOMContentLoaded'));
                  window.addEventListener('load', () => window.log.push('load'));
                </script>
                <script defer src="/d1.js"></script>
                <script defer src="/d2.js"></script>
                <script src="/blocking.js"></script>
                </head><body>
                <script>window.log.push('inline');</script>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("window.log.join(',')"))
            .Should().Be("blocking,inline,defer1,defer2,DOMContentLoaded,load");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnAsyncScriptRunsBeforeLoad()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/async.js", _ => LoopbackResponse.Script("window.log.push('async');"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.log = [];
                  window.addEventListener('load', () => window.log.push('load'));
                  document.addEventListener('DOMContentLoaded', () => window.log.push('DOMContentLoaded'));
                </script>
                <script async src="/async.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        var log = await loopback.Page.EvaluateAsync<string>("window.log.join(',')");
        log.Should().Contain("async");
        log.IndexOf("async", StringComparison.Ordinal).Should()
            .BeLessThan(log.IndexOf("load", StringComparison.Ordinal), "an async script runs before the load event");
    }

    [Test]
    public async Task ANomoduleScriptIsNeitherRunNorFetched()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/legacy.js", _ => LoopbackResponse.Script("window.legacyExternal = true;"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script nomodule>window.legacyInline = true;</script>
                <script nomodule src="/legacy.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync("typeof window.legacyInline")).Should().Be("undefined");
        (await loopback.Page.EvaluateAsync("typeof window.legacyExternal")).Should().Be("undefined");
        loopback.Server.Received.Should().NotContain(request => request.Path == "/legacy.js");
    }

    [Test]
    public async Task AScriptThatCannotBeLoadedFiresErrorAndThePageStillLoads()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>
                  window.log = [];
                  window.addEventListener('error', e => window.log.push('error:' + e.target.id), true);
                  window.addEventListener('load', () => window.log.push('load'));
                </script>
                <script id="bad" src="/missing.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("window.log.join(',')")).Should().Be("error:bad,load");
        loopback.Page.Errors.Should().ContainSingle(error => error.Message.Contains("/missing.js", StringComparison.Ordinal));
    }

    [Test]
    public async Task AScriptIsDecodedWithTheCharsetItsResponseDeclares()
    {
        // "café" in ISO-8859-1: the é is one byte, 0xE9, which is not valid UTF-8 — so a page reading this as
        // UTF-8 would answer a replacement character instead.
        var latin1 = new byte[] { 0x77, 0x2E, 0x63 } // w.c
            .Concat("harset='caf"u8.ToArray())
            .Concat([(byte) 0xE9])
            .Concat("';"u8.ToArray())
            .ToArray();

        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/latin1.js", _ => LoopbackResponse.Raw(latin1, "text/javascript; charset=iso-8859-1"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>window.w = {};</script>
                <script src="/latin1.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync("window.w.charset")).Should().Be("café");
    }

    [Test]
    public async Task ModulesResolveThroughAnImportMapAndAClassicScriptCanImportDynamically()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/vendor/dep.js", _ => LoopbackResponse.Script("export const value = 'from-dep'; export const n = 41;"))
            .Map("/entry.js", _ => LoopbackResponse.Script("import { value } from 'dep'; window.fromEntry = value;"))
            .MapHtml("/", """
                <!doctype html><html><head>
                <script type="importmap">{ "imports": { "dep": "/vendor/dep.js" } }</script>
                <script>
                  window.dynamic = 'pending';
                  import('dep').then(m => { window.dynamic = m.value + ':' + (m.n + 1); });
                </script>
                <script type="module" src="/entry.js"></script>
                <script type="module">
                  import { value } from 'dep';
                  window.fromInline = value + '/inline';
                </script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.WaitForIdleAsync(Timeout);

        loopback.Page.Errors.Should().BeEmpty();
        (await loopback.Page.EvaluateAsync("window.fromEntry")).Should().Be("from-dep");
        (await loopback.Page.EvaluateAsync("window.fromInline")).Should().Be("from-dep/inline");
        (await loopback.Page.EvaluateAsync("window.dynamic")).Should().Be("from-dep:42");
    }

    [Test]
    public async Task AModuleThatCannotBeLoadedIsReportedAndThePageStillLoads()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html><html><head>
                <script>window.log = []; window.addEventListener('load', () => window.log.push('load'));</script>
                <script type="module" src="/missing-module.js"></script>
                </head><body>done</body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("window.log.join(',')")).Should().Be("load");
        loopback.Page.Errors.Should().ContainSingle(error => error.Message.Contains("missing-module.js", StringComparison.Ordinal));
    }

    [Test]
    public async Task AnInsertedScriptRunsAndAnInnerHtmlScriptDoesNot()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/late.js", _ => LoopbackResponse.Script("window.ran.push('external-inserted');"))
            .MapHtml("/", "<!doctype html><html><head></head><body><div id='host'></div></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        await loopback.Page.EvaluateAsync(
            """
            window.ran = [];

            var inline = document.createElement('script');
            inline.textContent = "window.ran.push('inline-inserted');";
            document.head.appendChild(inline);

            var external = document.createElement('script');
            external.src = '/late.js';
            document.head.appendChild(external);

            // https://html.spec.whatwg.org/multipage/scripting.html#already-started: a script the fragment
            // parser created starts out "already started", so adopting it into the tree never runs it.
            document.getElementById('host').innerHTML = "<scr" + "ipt>window.ran.push('innerHTML');</scr" + "ipt>";
            """);

        (await loopback.Page.EvaluateAsync<string>("window.ran.join(',')"))
            .Should().Be("inline-inserted,external-inserted");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DocumentWriteDuringTheParseInsertsAtTheInsertionPoint()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html><html><body><p id="before">before</p>
                <script>document.write('<p id="written">written</p>');</script>
                <p id="after">after</p>
                </body></html>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // The written markup is parsed where the script was, not appended at the end — which is the whole
        // difference between an insertion point and a document.body.innerHTML +=.
        (await loopback.Page.EvaluateAsync<string>(
            "Array.prototype.map.call(document.querySelectorAll('p'), p => p.id).join(',')"))
            .Should().Be("before,written,after");
    }

    [Test]
    public async Task AScriptStoppedByAConstraintIsRecordedAndTheParseGoesOn()
    {
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/", """
                <!doctype html><html><head>
                <script>window.before = true;</script>
                <script>var n = 0; while (true) { n++; }</script>
                </head><body><p id="p">parsed</p>
                <script>window.after = true;</script>
                </body></html>
                """),
            configureBrowser: options => options.ConfigureEngine(engine => engine.LimitStatements(5000)));

        await loopback.Page.NavigateAsync(loopback.Url("/"));

        // A constraint does not throw a JavaScriptException, and it reaches the driver on the page loop
        // inside a baton hand-off — so letting it out would fault AngleSharp's parse and fail the whole
        // navigation. The contract is the one HTML gives a script that threw: recorded, and the page lives.
        loopback.Page.Errors.Should().ContainSingle();
        (await loopback.Page.EvaluateAsync<bool>("window.before === true")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("window.after === true")).Should().BeTrue();
        (await loopback.Page.EvaluateAsync("document.getElementById('p').textContent")).Should().Be("parsed");
    }

    [Test]
    public async Task DocumentWriteAfterTheParseIsRefusedWithAReason()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<!doctype html><html><body><p id='p'>kept</p></body></html>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("document.write('<p id=\"late\">late</p>')");

        (await loopback.Page.EvaluateAsync("document.getElementById('p').textContent")).Should().Be("kept");
        (await loopback.Page.EvaluateAsync("document.getElementById('late')")).Should().BeNull();
        loopback.Page.Errors.Should().ContainSingle(error => error.Message.Contains("document.open()", StringComparison.Ordinal));
    }
}
