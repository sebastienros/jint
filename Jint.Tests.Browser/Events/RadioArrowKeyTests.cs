using Jint.Browser;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The arrow keys on a radio button select the next or previous button of its group, as every engine does: a
/// sort control's "Newest first" and ArrowDown checks "Oldest first".
/// </summary>
public sealed class RadioArrowKeyTests
{
    private const string Fixture = """
        <!doctype html>
        <form id="form">
          <input type="radio" name="sort" id="a" checked>
          <input type="radio" name="sort" id="b" disabled>
          <input type="radio" name="sort" id="c">
          <input type="radio" name="sort" id="d" hidden>
          <input type="radio" name="other" id="x">
        </form>
        <script>
          window.log = [];
          for (const input of document.querySelectorAll('input')) {
            for (const type of ['click', 'input', 'change']) input.addEventListener(type, (e) => log.push(type + ':' + e.target.id));
          }
        </script>
        """;

    private static async Task<string> State(Page page)
        => await page.EvaluateAsync<string>(
            "document.activeElement.id + ' ' + [...document.querySelectorAll('[name=sort]')].filter((r) => r.checked).map((r) => r.id).join() + ' ' + log.splice(0).join()") ?? "";

    [Test]
    public async Task ArrowKeysSelectTheNextAndPreviousRadioOfTheGroup()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Fixture, "http://127.0.0.1/");

        // Forward skips the disabled and the unrendered buttons, and stays in its own group.
        (await page.PressAsync("#a", "ArrowDown")).Should().BeTrue();
        (await State(page)).Should().Be("c c click:c,input:c,change:c");

        // …and wraps around.
        (await page.PressAsync("ArrowRight")).Should().BeTrue();
        (await State(page)).Should().Be("a a click:a,input:a,change:a");

        // Back the other way, wrapping too.
        (await page.PressAsync("ArrowUp")).Should().BeTrue();
        (await State(page)).Should().Be("c c click:c,input:c,change:c");
        (await page.PressAsync("ArrowLeft")).Should().BeTrue();
        (await State(page)).Should().Be("a a click:a,input:a,change:a");
    }

    [Test]
    public async Task InARightToLeftControlTheHorizontalArrowsSwap()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Fixture.Replace("<form id=\"form\">", "<form id=\"form\" dir=\"rtl\">"), "http://127.0.0.1/");

        (await page.PressAsync("#a", "ArrowLeft")).Should().BeTrue();
        (await State(page)).Should().Be("c c click:c,input:c,change:c");
        (await page.PressAsync("ArrowRight")).Should().BeTrue();
        (await State(page)).Should().Be("a a click:a,input:a,change:a");
    }

    [Test]
    public async Task ACancelledKeydownSelectsNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Fixture + "<script>document.getElementById('a').addEventListener('keydown', (e) => e.preventDefault())</script>", "http://127.0.0.1/");

        (await page.PressAsync("#a", "ArrowDown")).Should().BeTrue();
        (await State(page)).Should().Be("a a ");
    }
}
