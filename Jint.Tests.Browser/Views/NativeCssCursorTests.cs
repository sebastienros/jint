#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssCursorTests
{
    [Test]
    public async Task InitialCursorIsAutoAndEnumerated()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span id=child>text</span>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).cursor"))
            .Should().Be("auto");
        (await page.EvaluateAsync<bool>("(() => { const style = getComputedStyle(document.getElementById('child'));"
            + "for (let i=0; i<style.length; i++) if (style.item(i) === 'cursor') return true; return false; })()"))
            .Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task LiveCursorUsesSpecifiedKeywordsAndInheritedUpdates()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#parent {cursor:pointer}</style><div id=parent><span id=child>text</span></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).cursor"))
            .Should().Be("pointer");
        await page.EvaluateAsync("document.getElementById('parent').style.cursor = 'GRAB'");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).cursor"))
            .Should().Be("grab");
        await page.EvaluateAsync("document.getElementById('child').style.cursor = 'auto'");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).cursor"))
            .Should().Be("auto");
        await page.EvaluateAsync("document.getElementById('child').style.cursor = 'pointer text'");
        (await page.EvaluateAsync<string>("document.getElementById('child').style.cursor"))
            .Should().Be("auto");
        await page.EvaluateAsync("document.getElementById('child').style.cursor = ''");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).cursor"))
            .Should().Be("grab");
        page.Errors.Should().BeEmpty();
    }
}
