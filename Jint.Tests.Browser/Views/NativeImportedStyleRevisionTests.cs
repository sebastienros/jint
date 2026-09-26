using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.Browser.Views;

public sealed class NativeImportedStyleRevisionTests
{
    [Test]
    public async Task WarmGeometryChecksTheImportedChildRevisionWithoutRootOrTreeWrites()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>@import 'child.css';</style><div id=box></div>");
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var document = runtime.Document!;
            var owner = ContentDom.Descendants(document).Single(element => element.LocalName == "style");
            var root = NativeCssStyleSheets.SheetOf(runtime.Dom, owner)!;
            var child = CssStyleSheet.Parse("#box { display:block }");
            ((CssImportRule) root.Rules[0]).SetStyleSheet(child, null, null, new CssValueWork(default));
            engine.SetValue("changeImportedStyle", () =>
            {
                var nativeStamp = document.MutationStamp;
                var rootStamp = root.Stamp;
                ((CssStyleRule) child.Rules[0]).Style.SetProperty("display", "none");
                root.Stamp.Should().Be(rootStamp);
                document.MutationStamp.Should().Be(nativeStamp);
            });
            return true;
        });
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                const before = box.getBoundingClientRect().height;
                changeImportedStyle();
                return [before, box.getBoundingClientRect().height, box.getBoundingClientRect().height].join(',');
            })()
            """)).Should().Be("16,0,0");
        page.Errors.Should().BeEmpty();
    }
}
