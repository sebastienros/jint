namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class CustomPropertyTests
{
    [Test]
    public async Task CustomPropertiesInheritDeclaredTextWithoutComputingIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>main { --tone: RED; --alias: var(--tone); --size: calc(1px + 2px); color: red; }
            span { --tone: blue; }</style><main><span id="t"></span></main>
            """);
        (await page.EvaluateAsync<string>("""
            (() => { const s = getComputedStyle(document.getElementById('t'));
            return [s.getPropertyValue('--tone'), s.getPropertyValue('--alias'),
            s.getPropertyValue('--size'), s.getPropertyValue('--missing'), s.color].join('|'); })()
            """)).Should().Be("blue|var(--tone)|calc(1px + 2px)||red");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("var(--mode)", "none")]
    [TestCase("var(--missing, block)", "block")]
    [TestCase("var(--cycle, none)", "none")]
    [TestCase("var(--missing, var(--mode))", "none")]
    public async Task TextualVariablesSupportVisibilityAndFallbacks(string value, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<span id='t' style='--mode:none;--cycle:var(--cycle);display:{value}'></span>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).display")).Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InlineMutationInvalidatesInheritedText()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<main style='--x:one'><span></span></main>");
        (await page.EvaluateAsync<string>("""
            (() => { const s = getComputedStyle(document.querySelector('span'));
            const old = s.getPropertyValue('--x');
            document.querySelector('main').style.setProperty('--x', 'two');
            return old + '|' + s.getPropertyValue('--x'); })()
            """)).Should().Be("one|two");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("initial")]
    [TestCase("inherit")]
    [TestCase("unset")]
    [TestCase("revert")]
    [TestCase("revert-layer")]
    public async Task CustomWideKeywordsAreTextRatherThanCascadeInstructions(string text)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<main style='--x:{text}'><span></span></main>");
        (await page.EvaluateAsync<string>("getComputedStyle(document.querySelector('span')).getPropertyValue('--x')"))
            .Should().Be(text);
    }

    [Test]
    public async Task TextualExpansionBoundsDeepAndExponentialChains()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span id=t></span>");
        (await page.EvaluateAsync<string>("""
            (() => {
              const el = document.getElementById('t');
              for (let i = 0; i < 33; i++)
                el.style.setProperty('--v' + i, i === 32 ? 'block' : 'var(--v' + (i + 1) + ')');
              el.style.display = 'var(--v0, none)';
              const deep = getComputedStyle(el).display;
              el.style.setProperty('--v0', 'x');
              for (let i = 1; i <= 20; i++)
                el.style.setProperty('--v' + i, 'var(--v' + (i - 1) + ')var(--v' + (i - 1) + ')');
              el.style.display = 'var(--v20, none)';
              return deep + '|' + getComputedStyle(el).display;
            })()
            """)).Should().Be("none|none");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("var(--missing, /* ) , */ none)", "/* ) , */ none")]
    [TestCase("'var(--x)'", "'var(--x)'")]
    [TestCase("var(--missing, var(--x))", "none")]
    public async Task TextualExpansionRespectsStringsAndCommentDelimiters(string text, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span id=t style='--x:none'></span>");
        await page.EvaluateAsync("document.getElementById('t').style.opacity = " + System.Text.Json.JsonSerializer.Serialize(text));
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).opacity")).Should().Be(expected);
    }
}
