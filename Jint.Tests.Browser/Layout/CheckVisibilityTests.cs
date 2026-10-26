namespace Jint.Tests.Browser.Layout;

/// <summary>
/// <c>Element.checkVisibility()</c>, https://drafts.csswg.org/cssom-view/#dom-element-checkvisibility.
/// </summary>
/// <remarks>
/// The point most worth pinning is that <c>visibility: hidden</c> alone does not remove a box, so the answer
/// stays true until the page asks for the visibility check, even though the flat layout gives such an
/// element no row.
/// </remarks>
public class CheckVisibilityTests
{
    private const string Markup = """
        <div id="plain">a</div>
        <div id="none" style="display: none">b</div>
        <div id="child-of-none" style="display: none"><span id="inner">c</span></div>
        <div id="hiddenattr" hidden>d</div>
        <div id="invisible" style="visibility: hidden">e</div>
        <div id="invisible-parent" style="visibility: hidden"><span id="visible-child" style="visibility: visible">f</span></div>
        <div id="transparent" style="opacity: 0">g</div>
        <div id="transparent-parent" style="opacity: 0"><span id="faded-child">h</span></div>
        <div id="half" style="opacity: 0.5">i</div>
        <div id="contents" style="display: contents"><span id="in-contents">j</span></div>
        """;

    private static async Task<global::Jint.Browser.Page> PageAsync(global::Jint.Browser.Browser browser)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        return page;
    }

    private static Task<string?> Answer(global::Jint.Browser.Page page, string id, string options)
        => page.EvaluateAsync<string>($"String(document.getElementById('{id}').checkVisibility({options}))");

    [Test]
    public async Task AnElementWithABoxIsVisibleWithAndWithoutOptions()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        (await Answer(page, "plain", "")).Should().Be("true");
        (await Answer(page, "plain", "undefined")).Should().Be("true");
        (await Answer(page, "plain", "null")).Should().Be("true");
        (await Answer(page, "plain", "{}")).Should().Be("true");
        (await Answer(page, "plain", "{ checkOpacity: true, checkVisibilityCSS: true, contentVisibilityAuto: true }")).Should().Be("true");
        (await Answer(page, "half", "{ opacityProperty: true }")).Should().Be("true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task DisplayNoneHiddenAttributeAndMetadataHaveNoBox()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        foreach (var id in new[] { "none", "hiddenattr", "contents" })
        {
            (await Answer(page, id, "")).Should().Be("false", id);
            (await Answer(page, id, "{ opacityProperty: true, visibilityProperty: true }")).Should().Be("false", id);
        }

        (await page.EvaluateAsync<string>("String(document.head.checkVisibility())")).Should().Be("false");
        (await Answer(page, "in-contents", "")).Should().Be("true", "display: contents removes only the element's own box");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ADisplayNoneAncestorRemovesTheBox()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        (await Answer(page, "inner", "")).Should().Be("false");
        (await Answer(page, "inner", "{ visibilityProperty: true }")).Should().Be("false");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task VisibilityHiddenKeepsTheBoxUnlessTheVisibilityCheckIsRequested()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        (await Answer(page, "invisible", "")).Should().Be("true");
        (await Answer(page, "invisible", "{ opacityProperty: true }")).Should().Be("true");
        (await Answer(page, "invisible", "{ visibilityProperty: true }")).Should().Be("false");
        (await Answer(page, "invisible", "{ checkVisibilityCSS: true }")).Should().Be("false");
        (await Answer(page, "visible-child", "{ visibilityProperty: true }")).Should().Be("true", "visibility: visible escapes a hidden parent");
        (await Answer(page, "visible-child", "")).Should().Be("true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OpacityZeroIsOnlyConsideredWhenRequestedAndIncludesAncestors()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        (await Answer(page, "transparent", "")).Should().Be("true");
        (await Answer(page, "transparent", "{ visibilityProperty: true }")).Should().Be("true");
        (await Answer(page, "transparent", "{ opacityProperty: true }")).Should().Be("false");
        (await Answer(page, "transparent", "{ checkOpacity: true }")).Should().Be("false");
        (await Answer(page, "faded-child", "")).Should().Be("true");
        (await Answer(page, "faded-child", "{ opacityProperty: true }")).Should().Be("false");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ADetachedElementOrANonObjectOptionsArgument()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<string>("String(document.createElement('div').checkVisibility())")).Should().Be("false");
        (await page.EvaluateAsync<string>("""
            const e = document.createElement('div'); document.body.appendChild(e); const before = e.checkVisibility();
            e.remove(); String([before, e.checkVisibility()])
            """)).Should().Be("true,false");
        (await page.EvaluateAsync<string>("""
            (() => { try { document.body.checkVisibility(1); return 'no throw'; } catch (e) { return e.name; } })()
            """)).Should().Be("TypeError");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheMemberIsAnEnumerableOperationOfLengthZero()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<string>("""
            const d = Object.getOwnPropertyDescriptor(Element.prototype, 'checkVisibility');
            String([typeof d.value, d.value.length, d.enumerable, d.configurable, d.writable, d.value.name])
            """)).Should().Be("function,0,true,true,true,checkVisibility");
        page.Errors.Should().BeEmpty();
    }
}
