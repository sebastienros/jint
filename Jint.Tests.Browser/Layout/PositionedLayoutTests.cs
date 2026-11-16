using Jint.Browser;

namespace Jint.Tests.Browser.Layout;

/// <summary>
/// An absolutely positioned box is placed horizontally in its containing block by <c>left</c> or <c>right</c>, then
/// moved by a translation (CSS 2 §10.3.7, CSS Transforms 1 §6), and <c>offsetParent</c> names that containing block.
/// </summary>
public sealed class PositionedLayoutTests
{
    private static async Task<global::Jint.Browser.Page> PageWithAsync(string body)
    {
        var browser = new global::Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(800, 600) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><style>body{margin:0} #box{min-width:100px}</style>" + body);
        return page;
    }

    private static Task<string?> RectOf(global::Jint.Browser.Page page, string id)
        => page.EvaluateAsync<string>($"(() => {{ const b = document.getElementById('{id}').getBoundingClientRect(); return b.left + ',' + b.right; }})()");

    [Test]
    public async Task ARightInsetEndsTheBoxThatFarFromItsContainingBlocksRightEdge()
    {
        var page = await PageWithAsync("<div style='display:flex'><i style='width:100px;flex:none'>a</i><div id='host' style='position:relative;width:400px;flex:none'><span id='box' style='position:absolute;right:10px'>x</span></div></div>");
        (await RectOf(page, "host")).Should().Be("100,500");
        (await RectOf(page, "box")).Should().Be("390,490");
        (await page.EvaluateAsync<string>("document.getElementById('box').offsetParent.id")).Should().Be("host");
        (await page.EvaluateAsync<double>("document.getElementById('box').offsetLeft")).Should().Be(290);
    }

    [Test]
    public async Task ALeftInsetStartsTheBoxFromItsContainingBlocksLeftEdge()
    {
        var page = await PageWithAsync("<div style='display:flex'><i style='width:100px;flex:none'>a</i><div id='host' style='position:relative;width:400px;flex:none'><div><span id='box' style='position:absolute;left:25%'>x</span></div></div></div>");
        (await RectOf(page, "box")).Should().Be("200,300");
        (await RectOf(page, "host")).Should().Be("100,500");
    }

    [Test]
    public async Task ABoxWithBothInsetsAutoKeepsItsStaticPosition()
    {
        var page = await PageWithAsync("<div style='display:flex'><i style='width:100px;flex:none'>a</i><div id='host' style='position:relative;width:400px;flex:none'><span id='box' style='position:absolute'>x</span></div></div>");
        (await RectOf(page, "box")).Should().Be("100,200");
    }

    [Test]
    public async Task WithNoPositionedAncestorTheViewportIsTheContainingBlock()
    {
        var page = await PageWithAsync("<div style='width:400px'><span id='box' style='position:absolute;right:0'>x</span></div>");
        (await RectOf(page, "box")).Should().Be("700,800");
        (await page.EvaluateAsync<string>("document.getElementById('box').offsetParent.tagName")).Should().Be("BODY");
    }

    [Test]
    public async Task ATranslationMovesTheBoxAndWhatIsInsideIt()
    {
        var page = await PageWithAsync("<div style='position:relative;width:400px'><div id='box' style='position:absolute;left:0;transform:translate(-20px, 5px)'><b id='inner'>x</b></div></div>"
            + "<p id='moved' style='position:absolute;left:300px;transform:translateX(-50%)'>y</p>");
        (await RectOf(page, "box")).Should().Be("-20,80");
        (await page.EvaluateAsync<double>("document.getElementById('inner').getBoundingClientRect().left")).Should().Be(-20);
        (await page.EvaluateAsync<double>("document.getElementById('moved').getBoundingClientRect().left")).Should().BeLessThan(300);
    }

    [Test]
    public async Task AMenuPopperAlignsToTheEndOfItsToggleOpensBackIntoTheViewport()
    {
        // Popper's bottom-end placement of a Bootstrap .dropdown-menu-end under a toggle at the viewport's right edge.
        var page = await PageWithAsync("""
            <div style="display:flex;justify-content:flex-end"><div style="width:16px">
              <button id="toggle">v</button>
              <ul id="menu" style="position:absolute;inset:0px 0px auto auto;margin:0;min-width:10rem;transform:translate(-32px, 40px)">
                <li><a id="item" href="#" onclick="window.picked = true; return false">Run now</a></li>
              </ul>
            </div></div>
            """);
        (await RectOf(page, "menu")).Should().Be("608,768");
        (await page.ClickAsync("#item")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window.picked === true")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnOverlayThatTakesNoPointerLetsAClickThroughAndItsChildBackIn()
    {
        // CSS UI 4 §6.2: a canvas grid laid over its toolbox, transparent to the pointer; a child can take it back.
        var page = await PageWithAsync("""
            <div style="display:flex">
              <button id="under" style="width:100px;flex:none" onclick="window.pressed = 'under'"><div>a</div><div>b</div><div>c</div></button>
              <div style="position:relative;width:300px;flex:none">
                <div id="overlay" style="position:absolute;left:-100px;width:400px;height:80px;pointer-events:none">
                  <div>grid</div><div>grid</div>
                  <a id="live" href="#" style="pointer-events:auto" onclick="window.pressed = 'live'; return false">live</a>
                </div>
              </div>
            </div>
            """);
        // The overlay covers the button's lower rows, where a press lands.
        (await RectOf(page, "overlay")).Should().Be("0,400");
        (await page.EvaluateAsync<string>("(() => { const b = document.getElementById('under').getBoundingClientRect(); return document.elementFromPoint(b.left + 2, b.top + b.height / 2).closest('button')?.id ?? 'covered'; })()"))
            .Should().Be("under");
        (await page.EvaluateAsync<string>("(() => { const b = document.getElementById('live').getBoundingClientRect(); return document.elementFromPoint(b.left + 1, b.top + b.height / 2).id; })()"))
            .Should().Be("live");
        (await page.ClickAsync("#under")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.pressed")).Should().Be("under");
        (await page.ClickAsync("#live")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.pressed")).Should().Be("live");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AStaticElementInATableCellMeasuresFromTheCellAndAFixedBoxHasNoOffsetParent()
    {
        var page = await PageWithAsync("<table><tr><td id='cell'><span id='inside'>x</span></td></tr></table><div id='fixed' style='position:fixed;left:0'>f</div>");
        (await page.EvaluateAsync<string>("document.getElementById('inside').offsetParent.id")).Should().Be("cell");
        (await page.EvaluateAsync<bool>("document.getElementById('fixed').offsetParent === null")).Should().BeTrue();
    }
}
