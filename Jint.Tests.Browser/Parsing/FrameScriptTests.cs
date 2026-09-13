using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public class FrameScriptTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task AFrameCreatedAfterPageLoadRunsItsScriptAndLoadsOnce(bool srcdoc)
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<!doctype html><title>parent</title>")
            .MapHtml("/child", "<!doctype html><script>parent.scriptRuns++; var answer = 42;</script>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("""
            var loads = 0; var scriptRuns = 0;
            var f = document.createElement('iframe');
            f.onload = () => { loads++; window.answer = f.contentWindow.answer; };
            """ + (srcdoc
                ? "f.srcdoc = '<script>parent.scriptRuns++; var answer = 42;</script>';"
                : "f.src = '/child';") + "document.body.append(f);");
        (await loopback.Page.WaitForAsync("loads > 0", TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("loads === 1 && scriptRuns === 1 && answer === 42 && f.contentDocument.readyState === 'complete'"))
            .Should().BeTrue();
        loopback.Page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SandboxedFrameScriptsStayDisabled(bool srcdoc)
    {
        const string child = "<!doctype html><script>parent.leaked = true;</script><button onclick='parent.leaked = true'>test</button>";
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", child)
            .MapHtml("/", srcdoc
                ? "<!doctype html><iframe sandbox srcdoc=\"" + child + "\"></iframe>"
                : "<!doctype html><iframe sandbox src=/child></iframe>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("document.querySelector('iframe').contentDocument.querySelector('button').dispatchEvent(new Event('click'))");
        (await loopback.Page.EvaluateAsync<bool>("typeof leaked === 'undefined'")).Should().BeTrue();
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ChildMarkupHandlersUseTheirOwnGlobal()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", """
                <!doctype html><title>child</title><body onload="window.loaded = document.title">
                <button onclick="window.clicked = document.title">test</button>
                """)
            .MapHtml("/", "<!doctype html><iframe src=/child></iframe>"));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        await loopback.Page.EvaluateAsync("frames[0].document.querySelector('button').dispatchEvent(new Event('click'))");
        (await loopback.Page.EvaluateAsync<bool>("frames[0].loaded === 'child' && frames[0].clicked === 'child' && typeof loaded === 'undefined' && typeof clicked === 'undefined'"))
            .Should().BeTrue();
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AChildScriptBudgetFailureDoesNotEndTheDocumentLoad()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
                .MapHtml("/child", "<!doctype html><script>while (true) {}</script><script>var continued = true;</script>")
                .MapHtml("/", "<!doctype html><iframe src=/child></iframe><script>var continued = true;</script>"),
            configureBrowser: options => options.MaxTaskDuration = TimeSpan.FromMilliseconds(100));
        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.EvaluateAsync<bool>("continued && frames[0].continued")).Should().BeTrue();
        loopback.Page.Errors.Should().ContainSingle(e => e.Kind == PageErrorKind.BudgetExceeded);
    }

    [Test]
    public async Task ClassicScriptsUseTheirDocumentGlobalAndIntrinsics()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/child/deferred.js", _ => LoopbackResponse.Script("order.push('defer');"))
            .MapHtml("/child/page", """
                <!doctype html><script>
                var order = ['inline'];
                const lexical = 42;
                window.read = () => lexical;
                window.scriptOwner = document.currentScript.ownerDocument === document;
                window.hadFrameElement = frameElement !== null;
                window.array = [];
                window.error = new Error('child');
                window.parentSecret = typeof secret;
                window.same = window === self && self === globalThis && frames === window;
                </script><script defer src="deferred.js"></script><p id=child>child</p>
                """)
            .MapHtml("/", """
                <!doctype html><script>var secret = 'parent';</script>
                <iframe id=f src=/child/page></iframe>
                <script>window.restored = document.currentScript.ownerDocument === document;</script>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.EvaluateAsync<bool>("""
            (() => {
              const w = frames[0];
              return w.same && w.scriptOwner && w.hadFrameElement && restored && w.read() === 42
                && w.parentSecret === 'undefined' && w.order.join() === 'inline,defer'
                && w.array instanceof w.Array && !(w.array instanceof Array)
                && w.error instanceof w.Error && !(w.error instanceof Error)
                && w.eval('globalThis') === w && w.Function('return globalThis')() === w
                && w instanceof w.Window && !(w instanceof Window)
                && w.document.defaultView === w && w.child === w.document.getElementById('child')
                && typeof child === 'undefined';
            })()
            """)).Should().BeTrue();
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NestedFramesUseTheirImmediateParentAndIndependentWindowListeners()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/inner", """
                <!doctype html><script>
                var events = [];
                document.addEventListener('DOMContentLoaded', () => events.push('dcl'));
                addEventListener('load', e => events.push('load:' + (e.target === window)));
                onload = () => events.push('handler');
                var parentTitle = parent.document.title;
                </script>
                """)
            .MapHtml("/outer", "<!doctype html><title>outer</title><iframe src=/inner></iframe>")
            .MapHtml("/", "<!doctype html><title>top</title><iframe src=/outer></iframe>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.EvaluateAsync<bool>("""
            (() => {
              const a = frames[0], b = a.frames[0];
              return b.parent === a && b.top === window && b.parentTitle === 'outer'
                && b.frameElement === a.document.querySelector('iframe')
                && b.events.join() === 'dcl,load:true,handler'
                && a.length === 1 && b.length === 0 && window.onload === null;
            })()
            """)).Should().BeTrue();
        loopback.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AChildScriptErrorReachesOnlyItsOwnWindowAndParsingContinues()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", """
                <!doctype html><script>
                var errors = [];
                addEventListener('error', e => { errors.push(e.error instanceof Error); e.preventDefault(); });
                throw new Error('child failure');
                </script><script>var continued = true;</script>
                """)
            .MapHtml("/", """
                <!doctype html><script>var errors = []; onerror = e => errors.push(e);</script>
                <iframe src=/child></iframe><script>var continued = true;</script>
                """));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.EvaluateAsync<bool>(
            "errors.length === 0 && frames[0].errors.join() === 'true' && continued && frames[0].continued"))
            .Should().BeTrue();
        loopback.Page.Errors.Should().ContainSingle(e => e.Kind == PageErrorKind.ScriptError);
    }

    [Test]
    public async Task ChildTimersAndMicrotasksKeepTheirGlobalAfterTheParse()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child", """
                <!doctype html><script>
                var result = [];
                Promise.resolve().then(() => result.push(document.title));
                setTimeout(() => { result.push(window === globalThis); window.done = true; }, 0);
                </script><title>child</title>
                """)
            .MapHtml("/", "<!doctype html><title>parent</title><iframe src=/child></iframe>"));

        await loopback.Page.NavigateAsync(loopback.Url("/"));
        (await loopback.Page.WaitForAsync("frames[0].done === true", TimeSpan.FromSeconds(30))).Should().BeTrue();
        (await loopback.Page.EvaluateAsync<bool>("frames[0].result[1] === true && typeof result === 'undefined'"))
            .Should().BeTrue();
        loopback.Page.Errors.Should().BeEmpty();
    }
}
