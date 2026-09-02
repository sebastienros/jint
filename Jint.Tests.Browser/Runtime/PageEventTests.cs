using Jint.Browser;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The one event bus: every script-visible event is a Jint event dispatched over the DOM tree, and the window
/// is what the path ends at.
/// </summary>
public sealed class PageEventTests
{
    [Test]
    public async Task AnEventDispatchedOnAnElementBubblesToTheWindow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id='outer'><span id='inner'></span></div>
            <script>
              window.log = [];
              window.addEventListener('ping', e => {
                window.log.push('window');
                window.targetIsSpan = e.target === document.getElementById('inner');
                window.currentIsWindow = e.currentTarget === window;
                window.path = e.composedPath().map(t => t === window ? 'window' : (t.nodeName || String(t)));
              });
              document.getElementById('outer').addEventListener('ping', () => window.log.push('outer'));
              document.getElementById('inner').dispatchEvent(new Event('ping', { bubbles: true }));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join(',')")).Should().Be("outer,window");
        (await page.EvaluateAsync<bool>("window.targetIsSpan")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window.currentIsWindow")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.path.join(',')")).Should().Be("SPAN,DIV,BODY,HTML,#document,window");
    }

    [Test]
    public async Task LoadDoesNotBubbleFromTheDocumentToTheWindow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // https://dom.spec.whatwg.org/#get-the-parent — a document's parent is the window for every event but
        // `load`, which is why a `load` listener on the window does not see a document's own load event.
        await page.SetContentAsync(
            """
            <script>
              window.seen = [];
              window.addEventListener('load', () => window.seen.push('window'));
              document.addEventListener('load', () => window.seen.push('document'));
              document.dispatchEvent(new Event('load', { bubbles: true }));
              window.duringScript = window.seen.join(',');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.duringScript")).Should().Be("document");
    }

    [Test]
    public async Task TheLoadLifecycleFiresInOrderAfterTheInlineScripts()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.lifecycle = ['script'];
              document.addEventListener('readystatechange', () => window.lifecycle.push('readystatechange'));
              window.addEventListener('DOMContentLoaded', () => window.lifecycle.push('DOMContentLoaded@window'));
              document.addEventListener('DOMContentLoaded', () => window.lifecycle.push('DOMContentLoaded@document'));
              window.addEventListener('load', () => window.lifecycle.push('load'));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.lifecycle.join(',')")).Should()
            .Be("script,readystatechange,DOMContentLoaded@document,DOMContentLoaded@window,load");
    }

    [Test]
    public async Task AnOnHandlerAttributeOnTheWindowIsAListener()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.hits = 0;
              window.onclick = () => { window.hits++ };
              window.dispatchEvent(new Event('click'));
              window.stored = typeof window.onclick;
              window.onclick = null;
              window.dispatchEvent(new Event('click'));
            </script>
            """);

        (await page.EvaluateAsync<int>("window.hits")).Should().Be(1);
        (await page.EvaluateAsync<string>("window.stored")).Should().Be("function");
        (await page.EvaluateAsync("window.onclick")).Should().BeNull();
    }

    [Test]
    public async Task DocumentCurrentScriptIsTheRunningScript()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script id='first'>window.first = document.currentScript && document.currentScript.id</script>
            <script id='second'>window.second = document.currentScript && document.currentScript.id</script>
            """);

        (await page.EvaluateAsync<string>("window.first")).Should().Be("first");
        (await page.EvaluateAsync<string>("window.second")).Should().Be("second");

        // Outside a script it is null, which is what a browser answers for an evaluation the page did not run.
        (await page.EvaluateAsync("document.currentScript")).Should().BeNull();
    }
}
