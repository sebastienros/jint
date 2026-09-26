#nullable enable

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssTextDecorationTests
{
    [Test]
    public async Task LiveShorthandLonghandsAndResolvedColorAgreeWhileSpecifiedValuesStayAuthored()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>#t {font-size:20px;color:blue;text-decoration:underline 2em wavy currentcolor}</style>"
            + "<span id=t>text</span>");
        var read = "(() => { const s = getComputedStyle(document.getElementById('t')); return ["
            + "s.textDecorationLine,s.getPropertyValue('text-decoration-thickness'),s.textDecorationStyle,"
            + "s.textDecorationColor,s.getPropertyValue('text-decoration')].join('|') })()";
        (await page.EvaluateAsync<string>(read))
            .Should().Be("underline|40px|wavy|rgb(0, 0, 255)|underline 40px wavy rgb(0, 0, 255)");
        await page.EvaluateAsync("document.getElementById('t').style.color = 'red'");
        (await page.EvaluateAsync<string>(read))
            .Should().Be("underline|40px|wavy|rgb(255, 0, 0)|underline 40px wavy rgb(255, 0, 0)");
        await page.EvaluateAsync("document.getElementById('t').style.textDecoration = 'overline'");
        (await page.EvaluateAsync<string>(read))
            .Should().Be("overline|auto|solid|rgb(255, 0, 0)|overline auto solid rgb(255, 0, 0)");
        (await page.EvaluateAsync<string>("document.getElementById('t').style.textDecorationColor")).Should().Be("currentcolor");
        page.Errors.Should().BeEmpty();
    }
}
