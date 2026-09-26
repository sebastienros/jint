using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Views;

public sealed class NativeControlFactsSeedTests
{
    [Test]
    public void CachedComputedPropertyRejectsChangedHostValidityWithoutATreeMutation()
    {
        using var fixture = DomTestFixture.Create("<input id=i style='opacity:.5'>");
        var realm = DomRealm.Of(fixture.Engine);
        NativeCssStyleSheets.Associate(realm, fixture.Document);
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var query = NativeCssStyleSheets.CreateQuery(fixture.Document, realm);
        query.Query.GetProperty(input, "opacity", ref query.Matching).Text.Should().Be("0.5");
        var stamp = fixture.Document.MutationStamp;
        BrowserControlValidation.SetCustomValidity(input, "host error");
        fixture.Document.MutationStamp.Should().Be(stamp);
        Action cachedRead = () => query.Query.GetProperty(input, "opacity", ref query.Matching);
        cachedRead.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FileTypeHistoryIsReconciledBeforeDomOrCssSeedCapture(bool css)
    {
        using var fixture = DomTestFixture.Create("<input id=i type=file required><style>input:invalid { opacity:.5 }</style>");
        var realm = DomRealm.Of(fixture.Engine);
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var files = FileTransferRealm.Of(fixture.Engine);
        var selected = files.NewFileList();
        selected.Add(new Jint.WebApi.Files.JsFile(fixture.Engine, new byte[] { 1 }, "text/plain", "kept.txt", 0));
        files.SetInputFiles(input, selected);
        input.SetAttribute("type", "text");
        input.SetAttribute("type", "file");
        if (css)
        {
            NativeCssStyleSheets.Associate(realm, fixture.Document);
            var style = ContentDom.Descendants(fixture.Document).Single(element => element.LocalName == "style");
            NativeCssStyleSheets.Install(realm, style, ContentDom.TextContent(style), "about:blank");
            var query = NativeCssStyleSheets.CreateQuery(fixture.Document, realm);
            query.Query.GetProperty(input, "opacity", ref query.Matching).Text.Should().Be("0.5");
            query.Query.GetProperty(input, "opacity", ref query.Matching).Text.Should().Be("0.5");
        }
        else
        {
            DomSelectors.QuerySelector(realm, fixture.Document, "input:invalid").Should().BeSameAs(input);
            DomSelectors.Matches(realm, input, ":invalid").Should().BeTrue();
        }
        selected.Length.Should().Be(1, "the externally retained list must survive input cleanup");
        files.InputFiles(input, create: true)!.Length.Should().Be(0);
    }

    [Test]
    public async Task WarmGeometryRefreshesAfterHostValidityChangesOnlyTheSemanticRevision()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box { height:10px } input:invalid + #box { height:20px }</style><input id=i><div id=box></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var input = ContentDom.ElementById(document, "i")!;
            engine.SetValue("hostValidity", () =>
            {
                var stamp = document.MutationStamp;
                BrowserControlValidation.SetCustomValidity(input, "host error");
                document.MutationStamp.Should().Be(stamp);
            });
            return true;
        });
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                const before = box.getBoundingClientRect().height;
                hostValidity();
                const after = box.getBoundingClientRect().height;
                return [before, after, box.getBoundingClientRect().height].join(',');
            })()
            """)).Should().Be("10,20,20");
    }
}
