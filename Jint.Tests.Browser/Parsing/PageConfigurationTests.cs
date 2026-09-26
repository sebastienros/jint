using System.Collections.Concurrent;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Parsing;

// The removed AngleSharp service enumeration/factory tests policed that implementation.
// These native tests preserve isolation and configuration order; they do not claim the
// old enumeration-count performance evidence.
public sealed class PageConfigurationTests
{
    [Test]
    public async Task ManufacturedDocumentsDoNotReplayConfigurationOnAnExistingEngine()
    {
        var order = new ConcurrentQueue<string>();
        await using var browser = new global::Jint.Browser.Browser(ConfiguredOptions(order));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>page</p>");
        var before = order.ToArray();
        await page.RunOnLoopAsync(engine =>
        {
            var marker = engine.GetValue("configurationMarker").ToObject();
            engine.Evaluate("""
                var retained = new DOMParser().parseFromString('<Root>original</Root>', 'text/xml');
                for (var i=0; i<8; i++) {
                    new DOMParser().parseFromString('<p>html</p>', 'text/html');
                    new DOMParser().parseFromString('<Other>xml</Other>', 'application/xml');
                    document.implementation.createHTMLDocument('manufactured');
                    document.implementation.createDocument('urn:example', 'm:Root', null);
                }
                retained.documentElement.localName === 'Root' && retained.documentElement.textContent === 'original'
                """).AsBoolean().Should().BeTrue();
            engine.GetValue("configurationMarker").ToObject().Should().BeSameAs(marker);
            return true;
        });
        order.ToArray().Should().Equal(before);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DistinctPageEnginesKeepMarkersAndConfigureBeforeObserverAndFirstScript()
    {
        var order = new ConcurrentQueue<string>();
        await using var browser = new global::Jint.Browser.Browser(ConfiguredOptions(order));
        await using var firstContext = await browser.NewContextAsync();
        await using var secondContext = await browser.NewContextAsync();
        var first = await firstContext.NewPageAsync();
        var second = await secondContext.NewPageAsync();
        object? firstMarker = null;
        foreach (var page in new[] { first, second })
        {
            order.Clear();
            page.Observe(new ConfigurationObserver(order));
            await page.SetContentAsync("<script>window.firstMarker=configurationMarker; recordFirstScript();</script>");
            order.ToArray().Should().Equal("options-first", "options-second", "engine-first", "engine-second", "observer", "script");
            var marker = await page.RunOnLoopAsync(engine =>
            {
                var configured = engine.GetValue("configurationMarker").ToObject();
                engine.GetValue("firstMarker").ToObject().Should().BeSameAs(configured);
                return configured;
            });
            marker.Should().NotBeNull();
            if (firstMarker is null) firstMarker = marker;
            else marker.Should().NotBeSameAs(firstMarker);
            page.Errors.Should().BeEmpty();
        }
    }

    [Test]
    public async Task InterleavedMimeParsingKeepsDocumentKindsNamespacesAndTreesIndependent()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var first = await browser.NewPageAsync();
        var second = await browser.NewPageAsync();
        var retained = new Dictionary<Page, List<Document>> { [first] = [], [second] = [] };
        foreach (var mime in new[] { "text/html", "text/xml", "application/xml", "application/xhtml+xml", "image/svg+xml", "text/html" })
        {
            var html = mime == "text/html";
            var namespaceUri = mime == "image/svg+xml" ? Namespaces.Svg
                : mime == "application/xhtml+xml" || html ? Namespaces.Html : "urn:example";
            var markup = html ? "<MiXeD>original</MiXeD>" : "<MiXeD xmlns='" + namespaceUri + "'>original</MiXeD>";
            foreach (var page in new[] { first, second })
            {
                await page.RunOnLoopAsync(engine =>
                {
                    engine.SetValue("inputMarkup", markup);
                    engine.SetValue("inputMime", mime);
                    var document = DomBindings.Bind<Document>(engine.Evaluate(
                        "new DOMParser().parseFromString(inputMarkup, inputMime)"), "configuration regression").Target;
                    document.Kind.Should().Be(html ? DocumentKind.Html : DocumentKind.Xml);
                    document.ContentType.Should().Be(mime);
                    var root = html ? document.DocumentElement!.LastChild!.FirstChild! : document.DocumentElement!;
                    var element = (Element) root;
                    element.LocalName.Should().Be(html ? "mixed" : "MiXeD");
                    element.NamespaceUri.Should().Be(namespaceUri);
                    foreach (var earlier in retained[page])
                    {
                        document.Should().NotBeSameAs(earlier);
                        DomDescendantText.Read(earlier, null, default).Should().Be("original");
                    }
                    retained[page].Add(document);
                    return true;
                });
            }
        }
    }

    private static BrowserOptions ConfiguredOptions(ConcurrentQueue<string> order) => new BrowserOptions()
        .ConfigureEngine(options =>
        {
            order.Enqueue("options-first");
            var marker = new object();
            options.Configure(engine =>
            {
                order.Enqueue("engine-first");
                engine.SetValue("configurationMarker", marker);
                engine.SetValue("recordFirstScript", (Action) (() => order.Enqueue("script")));
            });
        })
        .ConfigureEngine(options =>
        {
            order.Enqueue("options-second");
            options.Configure(_ => order.Enqueue("engine-second"));
        });

    private sealed class ConfigurationObserver(ConcurrentQueue<string> order) : IPageObserver
    {
        public void DocumentCreated(PageRuntime runtime, string loaderId)
        {
            runtime.Document.Should().BeNull();
            runtime.Engine.GetValue("configurationMarker").ToObject().Should().NotBeNull();
            order.Enqueue("observer");
        }
    }
}
