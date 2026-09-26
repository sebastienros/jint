using System.Globalization;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeDocumentMetadataTests
{
    [Test]
    public async Task ResponseMetadataIsAvailableToTheFirstScriptAndToAChildDocument()
    {
        const string modified = "Wed, 21 Oct 2015 07:28:00 GMT";
        var expected = DateTimeOffset.Parse(modified, CultureInfo.InvariantCulture).ToLocalTime()
            .ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/child", _ => LoopbackResponse.Html("<script>window.seenModified = document.lastModified;</script>").With("Last-Modified", modified))
            .Map("/", _ => LoopbackResponse.Html("<script>window.seenOrigin = document.origin; window.seenModified = document.lastModified;</script><iframe src=/child></iframe>")
                .With("Last-Modified", modified)));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("seenOrigin")).Should().Be(new Uri(fixture.Url("/")).GetLeftPart(UriPartial.Authority));
        (await fixture.Page.EvaluateAsync<string>("seenModified")).Should().Be(expected);
        (await fixture.Page.EvaluateAsync<string>("frames[0].seenModified")).Should().Be(expected);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task BlankAndSrcdocInheritOpaqueIdentityWhileSandboxCreatesAnotherOrigin()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<iframe id=b></iframe><iframe id=s srcdoc='<p>child</p>'></iframe><iframe id=x sandbox srcdoc='<p>child</p>'></iframe>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var origin = DomDocumentState.Of(document).Origin;
            foreach (var id in new[] { "b", "s", "x" })
            {
                var frame = DomDocumentReads.ById(runtime.Dom, document, id)!;
                var child = DomBrowsingContext.OfFrame(frame)!.Active!;
                DomDocumentMetadata.Origin(child).Should().Be("null");
                origin.IsSameOrigin(DomDocumentState.Of(child).Origin).Should().Be(id != "x");
                DomFrameMembers.ContentDocument(runtime.Dom, frame).IsNull().Should().Be(id == "x");
                frame.RemoveAttribute("sandbox");
                origin.IsSameOrigin(DomDocumentState.Of(child).Origin).Should().Be(id != "x");
            }
            return true;
        });
    }

    [Test]
    public async Task ManufacturedDocumentsAndClonesUseTheAssociatedOriginAndParserUrl()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>page</p>", "https://EXAMPLE.test:443/path");
        (await page.EvaluateAsync<bool>("""
            var xml = new Document();
            var parsed = new DOMParser().parseFromString('<p>parsed</p>', 'text/html');
            var made = document.implementation.createHTMLDocument();
            var cloned = document.cloneNode(false);
            xml.origin === 'https://example.test' && made.origin === xml.origin &&
            cloned.origin === xml.origin && parsed.origin === xml.origin && parsed.URL === document.URL
            """)).Should().BeTrue();
        await page.EvaluateAsync("history.pushState({}, '', '#later');");
        (await page.EvaluateAsync<string>("document.origin")).Should().Be("https://example.test");
        (await page.EvaluateAsync<string>("origin")).Should().Be("https://example.test");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DomainRelaxationFailsWithoutChangingTheOrigin()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("", "https://sub.example.test/");
        (await page.EvaluateAsync<bool>("""
            var converted = false; var failure;
            try { document.domain = { toString() { converted = true; return 'example.test'; } }; }
            catch (e) { failure = e.name; }
            converted && failure === 'NotSupportedError' && document.domain === 'sub.example.test' &&
            document.origin === 'https://sub.example.test'
            """)).Should().BeTrue();
    }

    [Test]
    public async Task NewDocumentScriptsUseThePendingOriginAndTheParserUrlBeforeTheTreeExists()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        var observer = new MetadataObserver();
        page.Observe(observer);
        await page.SetContentAsync("<script>window.firstScriptOrigin = document.origin;</script>", "https://creator.test/a");
        (await page.EvaluateAsync<bool>("""
            beforeDocument.origin === firstScriptOrigin && beforeParsed.origin === firstScriptOrigin &&
            beforeParsed.URL === document.URL && beforeWindowOrigin === firstScriptOrigin
            """)).Should().BeTrue();
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.DocumentCreationOrigin.Should().BeNull();
            return true;
        });
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AReplacementBlankDocumentInheritsTheFrozenCreatorOriginAndRetainedMetadataStaysPut()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("", "https://original.test/path");
        Document? retained = null;
        await page.RunOnLoopAsync(engine =>
        {
            retained = PageRuntime.Find(engine)!.Document!;
            return true;
        });
        await page.SetContentAsync("");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            DomDocumentState.Of(runtime.Document!).Origin.Should().BeSameAs(DomDocumentState.Of(retained!).Origin);
            DomDocumentMetadata.Domain(runtime.Document!).Should().Be("original.test");
            DomDocumentState.Of(retained!).Url.Should().Be("https://original.test/path");
            return true;
        });
        (await page.EvaluateAsync<string>("origin")).Should().Be("https://original.test");
        (await page.EvaluateAsync<string>("location.origin")).Should().Be("null");
        await page.SetContentAsync("", "https://replacement.test/");
        await page.RunOnLoopAsync(_ =>
        {
            DomDocumentMetadata.Origin(retained!).Should().Be("https://original.test");
            return true;
        });
    }

    private sealed class MetadataObserver : IPageObserver
    {
        public void DocumentCreated(PageRuntime runtime, string loaderId)
        {
            runtime.Document.Should().BeNull();
            runtime.Engine.Evaluate("""
                var beforeWindowOrigin = origin;
                var beforeDocument = new Document();
                var beforeParsed = new DOMParser().parseFromString('<p>before</p>', 'text/html');
                """);
        }
    }

    [Test]
    public void UnknownModificationTimeIsReadAtTheGetterAndDoesNotBecomeStoredMetadata()
    {
        var document = Document.CreateHtml();
        var before = DateTimeOffset.Now.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var value = DomDocumentMetadata.LastModified(document);
        var after = DateTimeOffset.Now.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        new[] { before, after }.Should().Contain(value);
        DomDocumentState.Of(document).SourceLastModified.Should().BeNull();
        DomDocumentMetadata.ParseLastModified("not a date").Should().BeNull();
        DomDocumentOrigin.FromUrl("https://EXAMPLE.test:443/a").IsSameOrigin(
            DomDocumentOrigin.FromUrl("https://example.test/b")).Should().BeTrue();
        DomDocumentOrigin.Opaque().IsSameOrigin(DomDocumentOrigin.Opaque()).Should().BeFalse();
    }
}
