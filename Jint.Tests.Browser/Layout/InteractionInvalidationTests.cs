using Jint.Browser.Layout;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Layout;

/// <summary>
/// Focus, press and hover retire retained measurements only when a rule of the document that can move a box matches
/// on them (Selectors 4 §§11-12), and an attribute change re-measures only what its selectors can reach.
/// </summary>
public sealed class InteractionInvalidationTests
{
    private const string Markup = """
        <style>#later { height: 20px }</style>
        <input id="name"><button id="target">Save</button><div id="later">later</div>
        <ul><li>one</li><li>two</li><li>three</li></ul>
        """;

    [Test]
    public async Task PointerAndFocusChangesNoRuleObservesRetainMeasurements()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        var diagnostics = await InstallDiagnostics(page);

        for (var i = 0; i < 3; i++)
        {
            (await page.ClickAsync("#target")).Should().BeTrue();
        }

        (await page.HoverAsync("#later")).Should().BeTrue();
        (await page.FillAsync("#name", "Ada")).Should().BeTrue();
        (await page.ClickAsync("#target")).Should().BeTrue();

        var reasons = await page.RunOnLoopAsync(_ => diagnostics.MeasurementDiscardReasons.Keys.ToArray());
        reasons.Should().NotContain(["press", "hover", "focus"]);
        page.Errors.Should().BeEmpty();
    }

    [TestCase("#target:hover + #later")]
    [TestCase("body:has(#target:hover) #later")]
    [TestCase(":is(#target:hover) ~ #later")]
    [TestCase("body:not(:has(#target:hover)) #shown")]
    public async Task HoverRulesStillMoveBoxes(string selector)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        var hidesOnHover = !selector.Contains(":not(", StringComparison.Ordinal);
        await page.SetContentAsync($"<style>{selector}{{display:none}}</style>" + Markup + "<p id='shown'>shown</p>");
        var probe = hidesOnHover ? "#later" : "#shown";

        (await page.IsVisibleAsync(probe)).Should().Be(hidesOnHover, selector);
        (await page.HoverAsync("#target")).Should().BeTrue(selector);
        (await page.IsVisibleAsync(probe)).Should().Be(!hidesOnHover, selector);
        var height = await page.EvaluateAsync<double>($"document.querySelector('{probe}').getBoundingClientRect().height");
        if (hidesOnHover) height.Should().Be(0, selector);
        else height.Should().BeGreaterThan(0, selector);
        (await page.HoverAsync("#name")).Should().BeTrue(selector);
        (await page.IsVisibleAsync(probe)).Should().Be(hidesOnHover, selector);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AHoverRuleOnAPseudoElementStillReachesComputedStyle()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#target:hover::after{content:'on'}</style>" + Markup);
        const string Read = "getComputedStyle(document.getElementById('target'), '::after').content";

        var before = await page.EvaluateAsync<string>(Read);
        (await page.HoverAsync("#target")).Should().BeTrue();
        // Declaration text, the cascade's text boundary rather than a serialized computed value.
        (await page.EvaluateAsync<string>(Read)).Should().Be("'on'").And.NotBe(before);
        (await page.HoverAsync("#name")).Should().BeTrue();
        (await page.EvaluateAsync<string>(Read)).Should().Be(before);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnActiveRuleAddedLaterIsObserved()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.ClickAsync("#target")).Should().BeTrue();

        // The retained plan said nothing observes press; a sheet that starts to must be seen at once.
        await page.EvaluateAsync("""
            document.head.append(Object.assign(document.createElement('style'), { textContent: '#target:active{display:none}' }));
            window.released = '';
            document.addEventListener('pointerup', e => released = e.target.id);
            """);
        (await page.ClickAsync("#target")).Should().BeFalse();
        (await page.EvaluateAsync<string>("released")).Should().NotBe("target");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PointerAndFocusRulesThatMoveNoBoxRetainMeasurements()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#target:active{color:red} #target:hover{background-color:blue} input:focus{outline:1px solid}</style>" + Markup);
        var diagnostics = await InstallDiagnostics(page);

        for (var i = 0; i < 3; i++)
        {
            (await page.ClickAsync("#target")).Should().BeTrue();
        }

        (await page.HoverAsync("#target")).Should().BeTrue();
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('target')).backgroundColor")).Should().Be("rgb(0, 0, 255)");
        (await page.FillAsync("#name", "Ada")).Should().BeTrue();

        var reasons = await page.RunOnLoopAsync(_ => diagnostics.MeasurementDiscardReasons.Keys.ToArray());
        reasons.Should().NotContain(["press", "hover", "focus"]);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AHoverRuleSettingACustomPropertyALayoutValueReadsStillMovesBoxes()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#later{display:var(--shown,block)} #target:hover ~ #later{--shown:none}</style>" + Markup);
        const string Height = "document.getElementById('later').getBoundingClientRect().height";

        (await page.EvaluateAsync<double>(Height)).Should().BeGreaterThan(0);
        (await page.HoverAsync("#target")).Should().BeTrue();
        (await page.EvaluateAsync<double>(Height)).Should().Be(0);
        (await page.HoverAsync("#name")).Should().BeTrue();
        (await page.EvaluateAsync<double>(Height)).Should().BeGreaterThan(0);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AClassLeftOfASiblingCombinatorRestylesTheFollowingSiblingsAlone()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>.on ~ .hidden-after{display:none}</style><section><i id='switch'></i><p class='hidden-after' id='after'>after</p></section><p id='elsewhere'>elsewhere</p>");
        var diagnostics = await InstallDiagnostics(page);

        (await HeightOf(page, "after")).Should().BeGreaterThan(0);
        await page.EvaluateAsync("document.getElementById('switch').className = 'on'");
        (await HeightOf(page, "after")).Should().Be(0);
        (await HeightOf(page, "elsewhere")).Should().BeGreaterThan(0);
        await page.EvaluateAsync("document.getElementById('switch').className = ''");
        (await HeightOf(page, "after")).Should().BeGreaterThan(0);

        await ShouldHaveRetainedMeasurements(page, diagnostics);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AClassInAHasArgumentRestylesTheAnchorsAmongItsAncestorsAlone()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>.row:has(.show) > .cell{display:none} .row:has(.show) + .next{display:none}</style>
            <div class="row"><div><span id="inner"></span></div><p class="cell" id="cell">cell</p></div>
            <p class="next" id="next">next</p>
            <div><span id="outside"></span></div>
            """);
        var diagnostics = await InstallDiagnostics(page);

        (await HeightOf(page, "cell")).Should().BeGreaterThan(0);
        await page.EvaluateAsync("document.getElementById('outside').className = 'show'");
        (await HeightOf(page, "cell")).Should().BeGreaterThan(0);
        await page.EvaluateAsync("document.getElementById('inner').className = 'show'");
        (await HeightOf(page, "cell")).Should().Be(0);
        (await HeightOf(page, "next")).Should().Be(0);
        await page.EvaluateAsync("document.getElementById('inner').className = ''");
        (await HeightOf(page, "cell")).Should().BeGreaterThan(0);
        (await HeightOf(page, "next")).Should().BeGreaterThan(0);

        await ShouldHaveRetainedMeasurements(page, diagnostics);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnAriaAttributeNoStatePseudoClassReadsKeepsMeasurementsBesideACheckedRule()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#check:checked + #detail{display:none} [aria-expanded=true] > b{display:none}</style><input type='checkbox' id='check'><p id='detail'>detail</p><button id='toggle' aria-expanded='false'><b id='label'>Toggle</b></button>");
        var diagnostics = await InstallDiagnostics(page);

        (await HeightOf(page, "label")).Should().BeGreaterThan(0);
        await page.EvaluateAsync("document.getElementById('toggle').setAttribute('aria-expanded', 'true'); document.getElementById('toggle').dataset.state = 'open'");
        (await HeightOf(page, "label")).Should().Be(0);
        (await HeightOf(page, "detail")).Should().BeGreaterThan(0);
        await ShouldHaveRetainedMeasurements(page, diagnostics);

        (await page.ClickAsync("#check")).Should().BeTrue();
        (await HeightOf(page, "detail")).Should().Be(0);
        page.Errors.Should().BeEmpty();
    }

    private static Task<double> HeightOf(global::Jint.Browser.Page page, string id)
        => page.EvaluateAsync<double>($"document.getElementById('{id}').getBoundingClientRect().height");

    // The measurements were kept through each change and re-measured where it reached, rather than rebuilt.
    private static async Task ShouldHaveRetainedMeasurements(global::Jint.Browser.Page page, PageLayoutDiagnostics diagnostics)
    {
        var (retained, reasons) = await page.RunOnLoopAsync(_ => (diagnostics.RetainedQueries, diagnostics.MeasurementDiscardReasons.Keys.ToArray()));
        reasons.Should().NotContain(reason => reason.StartsWith("selector-", StringComparison.Ordinal));
        retained.Should().BeGreaterThan(0);
    }

    private static Task<PageLayoutDiagnostics> InstallDiagnostics(global::Jint.Browser.Page page) => page.RunOnLoopAsync(engine =>
    {
        var diagnostics = new PageLayoutDiagnostics();
        PageRuntime.Find(engine)!.Layout.Diagnostics = diagnostics;
        return diagnostics;
    });
}
