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
        await page.SetContentAsync("<iframe id=b></iframe><iframe id=s srcdoc='<p>child</p>'></iframe><iframe id=x name=blocked sandbox srcdoc='<p>child</p>'></iframe>");
        (await page.EvaluateAsync<bool>("typeof frames[2] === 'undefined' && typeof window[2] === 'undefined' && blocked === document.getElementById('x')")).Should().BeTrue();
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
                FrameWindows.For(runtime, frame).IsNull().Should().Be(id == "x");
                frame.RemoveAttribute("sandbox");
                origin.IsSameOrigin(DomDocumentState.Of(child).Origin).Should().Be(id != "x");
            }
            return true;
        });
    }

    [Test]
    public async Task NestedSandboxFlagsAreFrozenAndBlankVariantsInherit()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <iframe id=b src='about:blank?query#fragment'></iframe>
            <iframe id=s sandbox srcdoc="<iframe id=n srcdoc='child'></iframe>"></iframe>
            <iframe id=a sandbox='allow-same-origin' srcdoc='child'></iframe>
            """, "https://creator.test/path");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var principal = runtime.Document!;
            var blankFrame = DomDocumentReads.ById(runtime.Dom, principal, "b")!;
            var blank = DomBrowsingContext.OfFrame(blankFrame)!.Active!;
            DomDocumentMetadata.Origin(blank).Should().Be("https://creator.test");
            DomDocumentState.FallbackBaseUri(blank).Should().Be("https://creator.test/path");
            var sandboxFrame = DomDocumentReads.ById(runtime.Dom, principal, "s")!;
            var sandbox = DomBrowsingContext.OfFrame(sandboxFrame)!.Active!;
            var nestedFrame = DomDocumentReads.ById(runtime.Dom, sandbox, "n")!;
            var nested = DomBrowsingContext.OfFrame(nestedFrame)!.Active!;
            DomDocumentState.Of(sandbox).Origin.IsSameOrigin(DomDocumentState.Of(nested).Origin).Should().BeFalse();
            DomDocumentState.Of(nested).HasSandboxedOrigin.Should().BeTrue();
            DomDocumentState.Of(nested).ScriptsBlockedBySandbox.Should().BeTrue();
            sandboxFrame.RemoveAttribute("sandbox");
            var allowedFrame = DomDocumentReads.ById(runtime.Dom, principal, "a")!;
            var allowed = DomBrowsingContext.OfFrame(allowedFrame)!.Active!;
            allowedFrame.RemoveAttribute("sandbox");
            DomDocumentState.Of(principal).Origin.IsSameOrigin(DomDocumentState.Of(allowed).Origin).Should().BeTrue();
            FrameWindows.CanRunScripts(runtime, allowed).Should().BeFalse();
            FrameWindows.CanRunScripts(runtime, sandbox).Should().BeFalse();
            FrameWindows.For(runtime, nestedFrame).IsNull().Should().BeTrue();
            return true;
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FrameCreationFactsPrecedeTheFetchPumpAndLaterAdoption(bool sandboxed)
    {
        var bodyReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new LoopbackResponse
        {
            Body = "<p>child</p>",
            WriteBodyAsync = async (stream, token) =>
            {
                await bodyReleased.Task.WaitAsync(token);
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("<p>child</p>"), token);
            },
        }.With("Content-Type", "text/html");
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/child", _ => response)
            .MapHtml("/", """
                <script>
                setTimeout(() => {
                    window.savedFrame = document.getElementById('f');
                    savedFrame.removeAttribute('sandbox');
                    hostOtherDocument.adoptNode(savedFrame);
                    releaseFrameBody();
                }, 0);
                </script>
                """ + "<iframe id=f " + (sandboxed ? "sandbox " : "") + "src=/child></iframe>"));
        fixture.Page.Observe(new FetchPumpObserver(bodyReleased));
        try
        {
            await fixture.Page.NavigateAsync(fixture.Url("/")).WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            fixture.Page.Errors.Should().BeEmpty("the frame fetch pump must run the scheduled adoption callback without a script or load failure");
            await fixture.Page.RunOnLoopAsync(engine =>
            {
                var runtime = PageRuntime.Find(engine)!;
                var frame = DomBindings.Bind<Element>(engine.GetValue("savedFrame"), "metadata regression").Target;
                var child = DomBrowsingContext.OfFrame(frame)!.Active!;
                var metadata = DomDocumentState.Of(child);
                metadata.HasSandboxedOrigin.Should().Be(sandboxed);
                metadata.ScriptsBlockedBySandbox.Should().Be(sandboxed);
                metadata.Origin.IsOpaque.Should().Be(sandboxed);
                frame.OwnerDocument.Should().NotBeSameAs(runtime.Document);
                if (!sandboxed)
                    metadata.Origin.IsSameOrigin(DomDocumentState.Of(runtime.Document!).Origin).Should().BeTrue();
                return true;
            });
        }
        finally
        {
            bodyReleased.TrySetResult();
        }
    }

    private sealed class FetchPumpObserver(TaskCompletionSource release) : IPageObserver
    {
        public void DocumentCreated(PageRuntime runtime, string loaderId)
        {
            runtime.Engine.SetValue("releaseFrameBody", (Action) (() => release.TrySetResult()));
            runtime.Engine.SetValue("hostOtherDocument", runtime.Dom.WrapNodeValue(Document.CreateHtml()));
        }
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
        DomDocumentOrigin.InheritsCreator("about:blank?query#fragment").Should().BeTrue();
        DomDocumentOrigin.InheritsCreator("about:srcdoc#fragment").Should().BeTrue();
        DomDocumentOrigin.InheritsCreator("about:srcdoc?query").Should().BeFalse();
    }
}
