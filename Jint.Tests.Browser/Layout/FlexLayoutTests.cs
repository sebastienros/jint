using Jint.Browser;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Layout;

public class FlexLayoutTests
{
    [TestCase("column", "nowrap")]
    [TestCase("column-reverse", "nowrap")]
    [TestCase("row", "wrap")]
    [TestCase("row", "wrap-reverse")]
    public async Task UnsupportedFlexDirectionsAndWrappingKeepTheFlatModel(string direction, string wrap)
    {
        await using var browser = new global::Jint.Browser.Browser(
            new BrowserOptions { Viewport = new Viewport(800, 600) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <div style="display:flex;flex-direction:{{direction}};flex-wrap:{{wrap}}">
              <button id="a" style="flex:1">A</button>
              <button id="b" style="flex:1">B</button>
            </div>
            """);
        (await page.EvaluateAsync<string>(
            """
            JSON.stringify(['a','b'].map(id => {
              const r = document.getElementById(id).getBoundingClientRect();
              return [r.x,r.y,r.width,r.height];
            }))
            """)).Should().Be("[[0,48,800,16],[0,64,800,16]]");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("row", "ltr", 0, 736)]
    [TestCase("row-reverse", "ltr", 64, 0)]
    [TestCase("row", "rtl", 64, 0)]
    [TestCase("row-reverse", "rtl", 0, 736)]
    public async Task FlexChildrenShareARowAndTheTrailingControlDoesNotCoverThePrimary(
        string direction, string textDirection, int primaryX, int trailingX)
    {
        await using var browser = new global::Jint.Browser.Browser(
            new BrowserOptions { Viewport = new Viewport(800, 600) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <div id="row" style="display:flex;flex-direction:{{direction}};direction:{{textDirection}}">
              <button id="primary" style="flex:1"><span>Expand</span></button>
              <button id="trailing" style="flex:0 0 64px"><span>A</span><span>B</span><span>C</span></button>
            </div>
            """);

        (await page.EvaluateAsync<string>(
            """
            JSON.stringify(['row','primary','trailing'].map(id => {
              const r = document.getElementById(id).getBoundingClientRect();
              return [r.x,r.y,r.width,r.height];
            }))
            """)).Should().Be($"[[0,32,800,80],[{primaryX},48,736,64],[{trailingX},48,64,64]]");
        (await page.EvaluateAsync<bool>(
            """
            (() => {
              const row = document.getElementById('row').getBoundingClientRect();
              return document.elementFromPoint(row.x + row.width / 2, row.y + row.height / 2).closest('button').id === 'primary';
            })()
            """)).Should().BeTrue();
        (await page.EvaluateAsync<int>("document.getElementById('trailing').offsetLeft")).Should().Be(trailingX);
        (await page.EvaluateAsync<string>(
            $"document.elementFromPoint({trailingX + 32}, 80).closest('button').id")).Should().Be("trailing");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("0 0 100px", "0 0 100px", 100, 100)]
    [TestCase("0.25 1 0px", "0.25 1 0px", 200, 200)]
    [TestCase("0 1 600px", "0 1 600px", 400, 400)]
    [TestCase("0 0.25 600px", "0 0.25 600px", 500, 500)]
    [TestCase("1 1 0px", "3 1 0px", 200, 600)]
    public async Task BasesGrowthShrinkageAndUnusedSpaceDetermineHorizontalPositions(
        string firstFlex, string secondFlex, int firstWidth, int secondWidth)
    {
        await using var browser = new global::Jint.Browser.Browser(
            new BrowserOptions { Viewport = new Viewport(800, 600) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <div style="display:flex">
              <button id="a" style="flex:{{firstFlex}}">A</button>
              <button id="b" style="flex:{{secondFlex}}">B</button>
            </div>
            """);
        (await page.EvaluateAsync<string>(
            """
            ['a','b'].map(id => {
              const r = document.getElementById(id).getBoundingClientRect();
              return [r.x,r.width].join(',');
            }).join('|')
            """)).Should().Be($"0,{firstWidth}|{firstWidth},{secondWidth}");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("flex-start", 48, 16)]
    [TestCase("center", 64, 16)]
    [TestCase("flex-end", 80, 16)]
    [TestCase("stretch", 48, 48)]
    public async Task CrossAxisAlignmentKeepsHitTestingInsideTheAssignedBox(string alignment, int y, int height)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <div style="display:flex;align-items:{{alignment}}">
              <button id="a" style="flex:1">A</button>
              <button style="flex:1"><span>B</span><span>C</span></button>
            </div>
            """);
        (await page.EvaluateAsync<string>(
            "(() => { const r = document.getElementById('a').getBoundingClientRect(); return r.y + ',' + r.height; })()"))
            .Should().Be($"{y},{height}");
        (await page.EvaluateAsync<string>($"document.elementFromPoint(10, {y + 8}).id")).Should().Be("a");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NestedRowMeasurementsAgreeAcrossDomAndResizeQueriesAndObserveMutations()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>.row { display:flex } .grow { flex:1 } .fixed { flex:0 0 80px }</style>
            <div id="outer" class="row">
              <div id="inner" class="row grow">
                <button id="primary" class="grow"><span id="label">Expand</span></button>
                <button id="secondary" class="fixed">More</button>
              </div>
              <button id="trailing" class="fixed"><span>A</span><span>B</span><span>C</span></button>
              <button id="hidden" hidden>Hidden</button>
            </div>
            """);

        foreach (var mutation in new[]
        {
            "void 0",
            "document.getElementById('trailing').style.flexBasis = '160px'",
            "document.getElementById('secondary').hidden = true",
            "document.getElementById('outer').style.display = 'block'",
        })
        {
            await page.EvaluateAsync(mutation);
            await page.RunOnLoopAsync(engine =>
            {
                var runtime = PageRuntime.Find(engine)!;
                var layout = runtime.Layout.Current();
                var sizes = runtime.Layout.MeasureSizes();
                foreach (var id in new[] { "outer", "inner", "primary", "label", "secondary", "trailing", "hidden" })
                {
                    var target = runtime.Document!.GetElementById(id)!;
                    var expected = layout.ClientBoxOf(target) ?? global::Jint.Browser.Layout.FlatBox.Empty;
                    var measured = sizes.Measure(target);
                    measured.Width.Should().Be(expected.Width, id + " width");
                    measured.Height.Should().Be(expected.Height, id + " height");
                }

                return true;
            });
        }

        page.Errors.Should().BeEmpty();
    }
}
