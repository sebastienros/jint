#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssTransformTests
{
    [Test]
    public async Task FontSizeDeclarationsAndMutationsReachEveryTransformComponent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>html { font-size:12px } #parent { font-size:20px }</style>"
            + "<div id=parent><span id=child style='translate:2em 10% 3rem;"
            + "scale:calc(2em / 1px);rotate:calc(2em / 1px) 1 0 45deg'>a</span></div>");
        var read = "(() => { const style = getComputedStyle(document.getElementById('child'));"
            + "return [style.translate, style.scale, style.rotate].join('|'); })()";
        (await page.EvaluateAsync<string>(read)).Should().Be("40px 10% 36px|40|40 1 0 45deg");
        await page.EvaluateAsync("document.getElementById('parent').style.fontSize = '30px'");
        (await page.EvaluateAsync<string>(read)).Should().Be("60px 10% 36px|60|60 1 0 45deg");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("translate", "0 -50%", "0px -50%")]
    [TestCase("translate", "-1in 20% 2px", "-96px 20% 2px")]
    [TestCase("rotate", "45deg 0 2 0", "y 45deg")]
    [TestCase("rotate", "-.25turn", "-90deg")]
    [TestCase("scale", "-50% 200% 100%", "-0.5 2")]
    [TestCase("scale", "calc(-2 * .25)", "-0.5")]
    public async Task InlineMutationsPriorityRemovalAndDetachedReadsUseCanonicalValues(string name, string declared, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#box { " + name + ":none !important }</style><div id=box>a</div>");
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                box.style.setProperty('NAME', 'DECLARED');
                return getComputedStyle(box).getPropertyValue('NAME');
            })()
            """.Replace("NAME", name).Replace("DECLARED", declared))).Should().Be("none");
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                box.style.setProperty('NAME', 'DECLARED', 'important');
                return getComputedStyle(box).getPropertyValue('NAME') + '|' + box.style.getPropertyPriority('NAME');
            })()
            """.Replace("NAME", name).Replace("DECLARED", declared))).Should().Be(expected + "|important");
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                box.style.setProperty('NAME', 'bogus');
                return getComputedStyle(box).getPropertyValue('NAME');
            })()
            """.Replace("NAME", name))).Should().Be(expected);
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.getElementById('box');
                box.style.removeProperty('NAME');
                return getComputedStyle(box).getPropertyValue('NAME');
            })()
            """.Replace("NAME", name))).Should().Be("none");
        // Keep the established resolved-style contract for a detached node.
        (await page.EvaluateAsync<string>("""
            (() => {
                const box = document.createElement('div');
                box.style.setProperty('NAME', 'DECLARED');
                return getComputedStyle(box).getPropertyValue('NAME');
            })()
            """.Replace("NAME", name).Replace("DECLARED", declared))).Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }

    [TestCase("translate", "10px 20%")]
    [TestCase("rotate", "x 45deg")]
    [TestCase("scale", "2")]
    public async Task ExplicitInheritanceAndInvalidVariableWinnersSurviveBrowserReads(string name, string value)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#parent { " + name + ":" + value + " } #child { " + name + ":inherit }</style>"
            + "<div id=parent><span id=child>a</span></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).getPropertyValue('" + name + "')"))
            .Should().Be(value);
        (await page.EvaluateAsync<string>("""
            (() => {
                const child = document.getElementById('child');
                child.style.setProperty('--bad', 'bogus');
                child.style.setProperty('NAME', 'var(--bad)', 'important');
                return getComputedStyle(child).getPropertyValue('NAME');
            })()
            """.Replace("NAME", name))).Should().Be("none");
        page.Errors.Should().BeEmpty();
    }
}
