#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssTextAlignTests
{
    [Test]
    public async Task LogicalInheritanceMatchParentAndShorthandResetRemainDistinct()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#parent {direction:rtl;text-align:start} #child {direction:ltr}</style>"
            + "<div id=parent><span id=child>text</span></div>");
        var read = "[getComputedStyle(document.getElementById('child')).getPropertyValue('text-align-all'),"
            + "getComputedStyle(document.getElementById('child')).getPropertyValue('text-align-last'),"
            + "getComputedStyle(document.getElementById('child')).getPropertyValue('text-align')].join('|')";
        (await page.EvaluateAsync<string>(read)).Should().Be("start|auto|start");
        await page.EvaluateAsync("document.getElementById('child').style.textAlign = 'match-parent'");
        (await page.EvaluateAsync<string>(read)).Should().Be("right|auto|right");
        await page.EvaluateAsync("document.getElementById('child').style.textAlign = 'justify-all'");
        (await page.EvaluateAsync<string>(read)).Should().Be("justify|justify|justify-all");
        await page.EvaluateAsync("document.getElementById('child').style.textAlign = 'center'");
        (await page.EvaluateAsync<string>(read)).Should().Be("center|auto|center");
        page.Errors.Should().BeEmpty();
    }
}
