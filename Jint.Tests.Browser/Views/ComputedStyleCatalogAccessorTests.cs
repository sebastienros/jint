#nullable enable

using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// CSSOM §6.7.1 (#4593): every supported property has a camel-cased attribute on CSSStyleDeclaration, and a
/// computed declaration reports resolved values for the image and grid-track properties it can resolve.
/// </summary>
/// <remarks>https://drafts.csswg.org/cssom/#dom-cssstyledeclaration-camel-cased-attribute</remarks>
public sealed class ComputedStyleCatalogAccessorTests
{
    [Test]
    public async Task EverySupportedPropertyHasACamelCasedAccessor()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=x></div>");
        var names = string.Join(",", CssPropertyCatalog.Names
            .Where(name => name != "float" && !name.StartsWith('-'))
            .Select(name => "'" + name + "'"));
        var missing = await page.EvaluateAsync<string>($$"""
            [{{names}}].filter(name => {
                const camel = name.replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());
                const descriptor = Object.getOwnPropertyDescriptor(CSSStyleDeclaration.prototype, camel);
                return !descriptor || typeof descriptor.get !== 'function' || typeof descriptor.set !== 'function';
            }).join(' ')
            """);
        missing.Should().BeEmpty();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task IssueReproReportsResolvedValuesThroughBothLanes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>
              #x { mask-image: linear-gradient(red, blue); object-fit: contain; display: grid;
                   grid-template-columns: 1fr 1fr; scroll-padding-top: 10px }
            </style>
            <div id=x></div>
            """);
        var result = await page.EvaluateAsync<string>("""
            (() => {
                const s = getComputedStyle(document.getElementById('x'));
                return ['maskImage', 'objectFit', 'gridTemplateColumns', 'scrollPaddingTop'].map(k =>
                    k + '=' + s[k] + '|' + s.getPropertyValue(k.replace(/[A-Z]/g, m => '-' + m.toLowerCase()))).join('\n');
            })()
            """);
        // Chromium answers 632px per track; the flat box model gives the block the whole 1280px viewport
        // (no body margin), and the tracks partition that same synthetic width getBoundingClientRect reports.
        (await page.EvaluateAsync<double>("document.getElementById('x').getBoundingClientRect().width")).Should().Be(1280);
        result.Should().Be(string.Join("\n",
            "maskImage=linear-gradient(rgb(255, 0, 0), rgb(0, 0, 255))|linear-gradient(rgb(255, 0, 0), rgb(0, 0, 255))",
            "objectFit=contain|contain",
            "gridTemplateColumns=640px 640px|640px 640px",
            "scrollPaddingTop=10px|10px"));
        page.Errors.Should().BeEmpty();
    }

    [TestCase("linear-gradient(to right, red 10%, #00f 50%)", "linear-gradient(to right, rgb(255, 0, 0) 10%, rgb(0, 0, 255) 50%)")]
    [TestCase("radial-gradient(circle, transparent, rgba(0,0,0,.5))", "radial-gradient(circle, rgba(0, 0, 0, 0), rgba(0, 0, 0, 0.5))")]
    [TestCase("repeating-linear-gradient(45deg, green 0 10px, white 10px 20px)", "repeating-linear-gradient(45deg, rgb(0, 128, 0) 0 10px, rgb(255, 255, 255) 10px 20px)")]
    [TestCase("linear-gradient(red,blue),conic-gradient(from 90deg, lime, navy)", "linear-gradient(rgb(255, 0, 0), rgb(0, 0, 255)), conic-gradient(from 90deg, rgb(0, 255, 0), rgb(0, 0, 128))")]
    [TestCase("none", "none")]
    public async Task GradientColorStopsResolveToSrgb(string specified, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=x></div>");
        await page.EvaluateAsync($"document.getElementById('x').style.backgroundImage = '{specified}'");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('x')).backgroundImage"))
            .Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }

    [TestCase("display:grid;width:300px;grid-template-columns:repeat(3, 1fr)", "100px 100px 100px")]
    [TestCase("display:grid;width:300px;grid-template-columns:100px 1fr;column-gap:20px", "100px 180px")]
    [TestCase("display:grid;width:400px;grid-template-columns:25% 2fr 1fr", "100px 200px 100px")]
    [TestCase("display:grid;width:300px;grid-template-columns:minmax(0, 1fr) auto", "minmax(0, 1fr) auto")]
    [TestCase("display:grid;width:300px;grid-template-columns:repeat(2, 50px 1fr)", "50px 100px 50px 100px")]
    [TestCase("display:grid;width:300px;grid-template-columns:repeat(1001, 1px)", "repeat(1001, 1px)")]
    [TestCase("display:block;width:300px;grid-template-columns:1fr 1fr", "1fr 1fr")]
    [TestCase("display:grid;width:300px", "none")]
    public async Task GridTrackListsResolveOnlyWhenEveryTrackIsDefinite(string style, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        // A horizontal flex row honours the authored width, so each case has a known container width.
        await page.SetContentAsync($"<div style='display:flex'><div id=x style='{style}'></div></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('x')).gridTemplateColumns"))
            .Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }

    [TestCase("display:grid;grid-template-rows:repeat(3, 240px)", "240px 240px 240px")]
    [TestCase("display:grid;--rows:2;grid-template-rows:repeat(var(--rows), 10px) 1em", "10px 10px 16px")]
    [TestCase("display:grid;grid-template-rows:1fr 20px", "1fr 20px")]
    [TestCase("display:grid;grid-template-rows:50% 20px", "50% 20px")]
    [TestCase("display:block;grid-template-rows:20px", "20px")]
    public async Task GridRowsResolveOnlyWhenEveryTrackIsAnAbsoluteLength(string style, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<div id=x style='{style}'></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('x')).gridTemplateRows"))
            .Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }
}
