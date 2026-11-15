using Jint.Browser.Layout;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Layout;

/// <summary>
/// Focus, press and hover retire retained measurements and the shared style traversal only when a rule of the
/// document can match on them (Selectors 4 §§11-12).
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

    private static Task<PageLayoutDiagnostics> InstallDiagnostics(global::Jint.Browser.Page page) => page.RunOnLoopAsync(engine =>
    {
        var diagnostics = new PageLayoutDiagnostics();
        PageRuntime.Find(engine)!.Layout.Diagnostics = diagnostics;
        return diagnostics;
    });
}
