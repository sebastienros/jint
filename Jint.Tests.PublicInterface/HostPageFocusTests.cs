#nullable enable
#if NET8_0_OR_GREATER
using Jint.Browser;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The page-level focus members — <see cref="Page.FocusAsync"/>, <see cref="Page.BlurAsync"/> and the targeted
/// <see cref="Page.PressAsync(string, string, KeyModifiers, int, NavigationOptions, TimeSpan?)"/> — driven
/// from an assembly with no <c>InternalsVisibleTo</c> grant from <c>Jint.Browser</c>.
/// </summary>
/// <remarks>
/// The behaviour is held in <c>Jint.Tests.Browser</c>; what this holds is that an embedder can reach it: focus
/// an element by selector, press a key at another, read where focus went, and be told <see langword="false"/>
/// for a target nothing focusable matches.
/// </remarks>
public sealed class HostPageFocusTests
{
    [Test]
    public async Task AnUnsignedConsumerCanFocusBlurAndPressAtATarget()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<!doctype html><input id=a><input id=b onkeydown=\"if(event.key==='Escape')document.title='esc'\">"
            + "<div id=plain>plain</div><div inert><button id=behind>behind</button></div>",
            "http://127.0.0.1/");

        (await page.FocusAsync("#a")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("a");

        (await page.PressAsync("#b", "Escape", KeyModifiers.None)).Should().BeTrue();
        (await page.TitleAsync()).Should().Be("esc");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");

        (await page.BlurAsync("#b")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.localName")).Should().Be("body");

        (await page.FocusAsync("#plain", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.FocusAsync("#behind", 0, TimeSpan.Zero)).Should().BeFalse();
        (await page.PressAsync("#missing", "Enter", timeout: TimeSpan.Zero)).Should().BeFalse();
    }
}
#endif
