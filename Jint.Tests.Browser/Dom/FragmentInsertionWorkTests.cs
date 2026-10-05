using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class FragmentInsertionWorkTests
{
    [TestCase(256)]
    [TestCase(512)]
    [TestCase(1024)]
    public async Task InnerHtmlWithPageObserverHasLinearMutationMatching(int depth)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=h></div>");
        var visits = await page.RunOnLoopAsync(engine =>
        {
            var document = PageRuntime.Find(engine)!.Document!;
            var count = 0;
            document.MutationAncestorVisited = () => count++;
            try
            {
                engine.SetValue("depth", depth);
                engine.Execute("""
                    var h = document.getElementById('h');
                    var observer = new MutationObserver(() => {});
                    observer.observe(h, {childList: true, subtree: true});
                    h.innerHTML = '<div>'.repeat(depth);
                    var records = observer.takeRecords();
                    var node = h, count = 0;
                    while (node.firstChild) { node = node.firstChild; count++; }
                    """);
                return count;
            }
            finally { document.MutationAncestorVisited = null; }
        });
        (await page.EvaluateAsync<bool>("count === depth && records.length === 1 && records[0].target === h && records[0].addedNodes[0] === h.firstChild")).Should().BeTrue();
        visits.Should().BeLessThanOrEqualTo(4 * depth + 32);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task FragmentInsertionPreservesControlsStylesAndInertScripts()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=h></div>");
        await page.EvaluateAsync("""
            var h = document.getElementById('h'), ran = false;
            h.innerHTML = '<style>#h { color: red; }</style><form id=f><input id=i><select><option>a</option><option selected>b</option></select></form><script>ran = true;</script>';
            var input = document.getElementById('i');
            """);
        (await page.EvaluateAsync<string>("[input.form === document.getElementById('f'), h.querySelector('select').selectedIndex, getComputedStyle(h).color, ran, input.ownerDocument === document].join('|')")).Should().Be("true|1|rgb(255, 0, 0)|false|true");
        page.Errors.Should().BeEmpty();
    }
}
