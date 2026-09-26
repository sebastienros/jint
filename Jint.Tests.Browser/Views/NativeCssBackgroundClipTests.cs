#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssBackgroundClipTests
{
    [Test]
    public async Task LiveComputedListsAgreeWithSpecifiedValuesAndKeepEveryLayer()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#parent {background-clip:content-box,text,content-box} #child {background-clip:inherit}</style>"
            + "<div id=parent><span id=child>text</span></div>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).backgroundClip"))
            .Should().Be("content-box, text, content-box");
        await page.EvaluateAsync("document.getElementById('child').style.backgroundClip = 'text border-area, padding-box, text'");
        (await page.EvaluateAsync<string>("document.getElementById('child').style.backgroundClip"))
            .Should().Be("border-area text, padding-box, text");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).backgroundClip"))
            .Should().Be("border-area text, padding-box, text");
        await page.EvaluateAsync("document.getElementById('child').style.backgroundClip = ''");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).backgroundClip"))
            .Should().Be("content-box, text, content-box");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task JQueryCloneBootstrapProbeClearsOnlyTheCloneStyle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>probe</p>");
        (await page.EvaluateAsync<string>("(() => { const div = document.createElement('div');"
            + "div.style.backgroundClip = 'content-box'; const clone = div.cloneNode(true);"
            + "clone.style.backgroundClip = ''; return [div.style.backgroundClip, clone.style.backgroundClip].join('|'); })()"))
            .Should().Be("content-box|");
        page.Errors.Should().BeEmpty();
    }
}
