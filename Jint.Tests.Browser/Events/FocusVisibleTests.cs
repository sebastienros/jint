using Jint.Browser;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>:focus-visible</c> (issue #4640): Selectors 4 §9.4 leaves when focus is indicated to the user agent,
/// and these pin Chromium's answers, each checked against Chromium itself.
/// </summary>
/// <remarks>
/// A key indicates focus and a pointer does not; a text-entry control always does; a script's
/// <c>focus()</c> indicates it when the element it takes focus from did, or when no pointer has moved focus
/// since the last key; and <c>focus({ focusVisible })</c> decides outright. Each assertion also reads a
/// computed style, because a rule keyed on <c>:focus-visible</c> is what a page actually observes and the
/// cascade has to be retired when the indication changes, including when the focused element does not.
/// </remarks>
public sealed class FocusVisibleTests
{
    private const string Markup = """
        <!doctype html>
        <style>
          :focus-visible { outline: 3px solid red }
          a:focus-visible span { color: rgb(0, 128, 0) }
          #btn:focus-visible { color: rgb(0, 0, 255) }
        </style>
        <input id=search><a id=link href="#x"><span>Title</span></a><button id=btn>b</button>
        <textarea id=area></textarea><input id=readonly readonly><input id=check type=checkbox>
        <div id=editor contenteditable>edit</div>
        """;

    private const string State = """
        (() => {
          const a = document.getElementById('link'), b = document.getElementById('btn');
          return [document.activeElement.id, a.matches(':focus-visible'), getComputedStyle(a.firstElementChild).color,
            b.matches(':focus-visible'), getComputedStyle(b).color,
            Array.from(document.querySelectorAll(':focus-visible'), e => e.id).join('+')].join(' ');
        })()
        """;

    private static async Task<Page> OpenAsync(Browser browser)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup, "http://127.0.0.1/");
        return page;
    }

    private static Task<string?> StateAsync(Page page) => page.EvaluateAsync<string>(State);

    [Test]
    public async Task TheIssueReproMatchesAfterAScriptFocusWithNoPointerBeforeIt()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.FocusAsync("#search")).Should().BeTrue();
        (await page.FocusAsync("#link")).Should().BeTrue();

        (await page.EvaluateAsync<string>("""
            (() => { const a = document.getElementById('link');
              return [document.activeElement.id, a.matches(':focus'), a.matches(':focus-visible'), getComputedStyle(a.firstElementChild).color].join(' '); })()
            """)).Should().Be("link true true rgb(0, 128, 0)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task APointerFocusIsNotIndicatedAndATabOrAnyKeyIs()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.ClickAsync("#link")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("link false rgb(0, 0, 0) false rgb(0, 0, 0) ");

        (await page.FocusAsync("#search")).Should().BeTrue();
        (await page.PressAsync("Tab")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("link true rgb(0, 128, 0) false rgb(0, 0, 0) link");

        // Clicking the element whose focus a key indicated leaves it indicated, as in Chromium.
        (await page.ClickAsync("#link")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("link true rgb(0, 128, 0) false rgb(0, 0, 0) link");

        // A key on an element a click focused indicates it — modifiers too — with no focus change at all,
        // which is what the cascade's focus witness has to notice.
        (await page.ClickAsync("#btn")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("btn false rgb(0, 0, 0) false rgb(0, 0, 0) ");
        (await page.PressAsync("Shift")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("btn false rgb(0, 0, 0) true rgb(0, 0, 255) btn");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AScriptFocusInheritsWhetherTheLastUserFocusWasAPointer()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.ClickAsync("#btn")).Should().BeTrue();
        (await page.FocusAsync("#link")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("link false rgb(0, 0, 0) false rgb(0, 0, 0) ");

        // A text field a click focused matches by itself, and hands nothing on to the next script focus.
        (await page.ClickAsync("#area")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("area false rgb(0, 0, 0) false rgb(0, 0, 0) area");
        (await page.FocusAsync("#link")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("link false rgb(0, 0, 0) false rgb(0, 0, 0) ");

        // An element whose focus is indicated hands the indication on.
        (await page.PressAsync("ArrowDown")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('btn').focus(), ''")).Should().Be("");
        (await StateAsync(page)).Should().Be("btn false rgb(0, 0, 0) true rgb(0, 0, 255) btn");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("#search", true)]
    [TestCase("#area", true)]
    [TestCase("#readonly", true)]
    [TestCase("#editor", true)]
    [TestCase("#check", false)]
    [TestCase("#btn", false)]
    public async Task ATextEntryControlAlwaysIndicatesFocus(string target, bool indicated)
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.ClickAsync(target)).Should().BeTrue();
        (await page.EvaluateAsync<bool>($"document.querySelector('{target}').matches(':focus-visible')"))
            .Should().Be(indicated);
        (await page.EvaluateAsync<bool>($"document.querySelector('{target}').matches(':focus')")).Should().BeTrue();
    }

    [Test]
    public async Task FocusOptionsFocusVisibleDecidesAndAKeyDoesNotOverrideIt()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.ClickAsync("#btn")).Should().BeTrue();
        await page.EvaluateAsync<string>("document.getElementById('link').focus({ focusVisible: true }), ''");
        (await StateAsync(page)).Should().Be("link true rgb(0, 128, 0) false rgb(0, 0, 0) link");

        await page.EvaluateAsync<string>("document.getElementById('btn').focus({ focusVisible: false }), ''");
        (await StateAsync(page)).Should().Be("btn false rgb(0, 0, 0) false rgb(0, 0, 0) ");
        (await page.PressAsync("a")).Should().BeTrue();
        (await StateAsync(page)).Should().Be("btn false rgb(0, 0, 0) false rgb(0, 0, 0) ");

        // On the element that already has focus the option still applies: HTML indicates focus after the
        // focusing steps, which return early there.
        await page.EvaluateAsync<string>("document.getElementById('btn').focus({ focusVisible: true }), ''");
        (await StateAsync(page)).Should().Be("btn false rgb(0, 0, 0) true rgb(0, 0, 255) btn");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// WebIDL's dictionary conversion: <see langword="undefined"/> and <see langword="null"/> are an empty
    /// FocusOptions, any other primitive is a <c>TypeError</c>, and the members are read in lexicographic
    /// order — <c>focusVisible</c> before <c>preventScroll</c>.
    /// </summary>
    [Test]
    public async Task FocusOptionsIsAWebIdlDictionary()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser);

        (await page.EvaluateAsync<string>("""
            (() => {
              const link = document.getElementById('link'), log = [];
              link.focus(undefined); link.focus(null);
              let error = 'none';
              try { link.focus(1); } catch (e) { error = e.constructor.name; }
              link.blur();
              link.focus({ get preventScroll() { log.push('preventScroll'); return false; },
                           get focusVisible() { log.push('focusVisible'); return 0; } });
              return [error, log.join(','), document.activeElement.id, link.matches(':focus-visible'), HTMLElement.prototype.focus.length].join(' ');
            })()
            """)).Should().Be("TypeError focusVisible,preventScroll link false 0");
        page.Errors.Should().BeEmpty();
    }
}
