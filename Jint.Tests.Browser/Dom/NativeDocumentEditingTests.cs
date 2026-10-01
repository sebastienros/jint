using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeDocumentEditingTests
{
    [Test]
    public async Task EnablingDesignModeResetsTheExistingRangeOnlyOnTheOffToOnTransition()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='p'>abc</p>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var text = DomDocumentReads.ById(runtime.Dom, document, "p")!.FirstChild!;
            var range = document.CreateRange();
            range.SetStart(new DomNodeIdentity(text), 1);
            range.SetEnd(new DomNodeIdentity(text), 2);
            runtime.Views.Selection.Range = range;
            DomDocumentEditing.Get(document).Should().Be("off");
            DomDocumentEditing.Set(runtime.Dom, document, "ON");
            DomDocumentEditing.Get(document).Should().Be("on");
            runtime.Views.Selection.Range.Should().BeSameAs(range);
            range.Start.Container.Node.Should().BeSameAs(document);
            range.End.Container.Node.Should().BeSameAs(document);
            range.Start.Offset.Should().Be(0);
            range.End.Offset.Should().Be(0);
            range.SetStart(new DomNodeIdentity(text), 1);
            range.SetEnd(new DomNodeIdentity(text), 2);
            DomDocumentEditing.Set(runtime.Dom, document, "on");
            range.Start.Container.Node.Should().BeSameAs(text);
            range.Start.Offset.Should().Be(1);
            DomDocumentEditing.Set(runtime.Dom, document, "OFF");
            DomDocumentEditing.Set(runtime.Dom, document, " on ");
            DomDocumentEditing.Get(document).Should().Be("off");
            range.Start.Container.Node.Should().BeSameAs(text);
            return true;
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ASecondaryDocumentCannotResetThePrincipalSelection(bool child)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='p'>abc</p><iframe id='f' srcdoc='<p>child</p>'></iframe>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var principal = runtime.Document!;
            var text = DomDocumentReads.ById(runtime.Dom, principal, "p")!.FirstChild!;
            var range = principal.CreateRange();
            range.SetStart(new DomNodeIdentity(text), 1);
            range.SetEnd(new DomNodeIdentity(text), 2);
            runtime.Views.Selection.Range = range;
            var other = child
                ? DomBrowsingContext.OfFrame(DomDocumentReads.ById(runtime.Dom, principal, "f")!)!.Active!
                : Document.CreateHtml();
            DomDocumentEditing.Set(runtime.Dom, other, "on");
            DomDocumentEditing.Get(other).Should().Be("on");
            DomDocumentEditing.Get(principal).Should().Be("off");
            range.Start.Container.Node.Should().BeSameAs(text);
            range.Start.Offset.Should().Be(1);
            range.End.Offset.Should().Be(2);
            DomViewMembers.GetSelection(runtime.Dom, other).Should().Be(Jint.Native.JsValue.Null);
            return true;
        });
    }
}
