#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssFontSizeTests
{
    [Test]
    public async Task ComputedFontSizesAndRelativeLengthsObserveTheLiveCascade()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>html {font-size:20px} #child {font-size:150%;width:2em;height:2rem}"
            + "#grand {font-size:2em}</style><div id=child><span id=grand>text</span></div>");
        var read = "[getComputedStyle(document.documentElement).fontSize,"
            + "getComputedStyle(document.getElementById('child')).fontSize,"
            + "getComputedStyle(document.getElementById('child')).width,"
            + "getComputedStyle(document.getElementById('child')).height,"
            + "getComputedStyle(document.getElementById('grand')).fontSize].join('|')";
        (await page.EvaluateAsync<string>(read)).Should().Be("20px|30px|60px|40px|60px");
        await page.EvaluateAsync("document.documentElement.style.fontSize = '2rem'");
        (await page.EvaluateAsync<string>(read)).Should().Be("32px|48px|96px|64px|96px");
        (await page.EvaluateAsync<string>("document.documentElement.style.fontSize")).Should().Be("2rem");
        page.Errors.Should().BeEmpty();
    }
}
