#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssFontWeightTests
{
    [Test]
    public async Task SpecifiedKeywordsRemainWritableWhileComputedWeightsAreNumericAndInherited()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#parent { font-weight:bold; } #child { font-weight:bolder; }</style>"
            + "<div id=parent><span id=child>text</span></div>");
        (await page.EvaluateAsync<string>(
            "[getComputedStyle(document.getElementById('parent')).fontWeight,"
            + "getComputedStyle(document.getElementById('child')).fontWeight].join('|')"))
            .Should().Be("700|900");
        await page.EvaluateAsync("document.getElementById('parent').style.fontWeight = '456.5'");
        (await page.EvaluateAsync<string>(
            "[document.getElementById('parent').style.fontWeight,"
            + "getComputedStyle(document.getElementById('parent')).fontWeight,"
            + "getComputedStyle(document.getElementById('child')).fontWeight].join('|')"))
            .Should().Be("456.5|456.5|700");
        await page.EvaluateAsync("document.getElementById('child').style.setProperty('font-weight', 'bold')");
        (await page.EvaluateAsync<string>(
            "[document.getElementById('child').style.getPropertyValue('font-weight'),"
            + "getComputedStyle(document.getElementById('child')).getPropertyValue('font-weight')].join('|')"))
            .Should().Be("bold|700");
        page.Errors.Should().BeEmpty();
    }
}
