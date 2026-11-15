#nullable enable
using Jint.Browser;

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// CSS Color Adjustment 1 §3.1 (#4641): with forced colors emulated, the cascade forces <c>background-image</c>,
/// the shadows, <c>color-scheme</c>, <c>accent-color</c> and <c>scrollbar-color</c> at computed-value time,
/// unless <c>forced-color-adjust</c> opts the element out. Every expected value is what Chromium reports.
/// </summary>
public sealed class ForcedColorsComputedStyleTests
{
    private const string Url = "url(\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\")";

    private static async Task<global::Jint.Browser.Page> OpenAsync(Browser browser, string style, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<!doctype html><style>{style}</style>{body}", "http://127.0.0.1/");
        return page;
    }

    private static Task<string> Read(global::Jint.Browser.Page page, string selector, string property, string? pseudo = null)
        => page.EvaluateAsync<string>(
            $"getComputedStyle(document.querySelector('{selector}'){(pseudo is null ? "" : ", '" + pseudo + "'")}).getPropertyValue('{property}')")!;

    private static Task ForceAsync(global::Jint.Browser.Page page, bool active)
        => page.EmulateMediaAsync(new MediaEmulation { ForcedColors = active ? "active" : "none" });

    /// <summary>The issue's repro: a gradient computes to <c>none</c>, a <c>url()</c> stays, and both come back.</summary>
    [Test]
    public async Task AGradientIsForcedToNoneAndAUrlIsKeptUntilTheEmulationIsCleared()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser,
            $"#g {{ background-image: linear-gradient(red, blue) }} #u {{ background-image: {Url} }}",
            "<div id=g></div><div id=u></div>");

        (await Read(page, "#g", "background-image")).Should().StartWith("linear-gradient(");

        await ForceAsync(page, true);
        (await page.EvaluateAsync<bool>("matchMedia('(forced-colors: active)').matches")).Should().BeTrue();
        (await Read(page, "#g", "background-image")).Should().Be("none");
        (await Read(page, "#u", "background-image")).Should().Be(Url);

        await ForceAsync(page, false);
        (await Read(page, "#g", "background-image")).Should().StartWith("linear-gradient(", "the forced value retires with the emulation");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// "UAs must not force background-image when it is a url": Chromium keeps a list with any <c>url()</c>
    /// layer whole, gradients included.
    /// </summary>
    [Test]
    public async Task AListWithAUrlLayerIsKeptWhole()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser,
            $"#m {{ background-image: linear-gradient(red, blue), {Url} }} #b {{ background: radial-gradient(red, blue) }}",
            "<div id=m></div><div id=b></div>");
        await ForceAsync(page, true);

        (await Read(page, "#m", "background-image")).Should().StartWith("linear-gradient(").And.EndWith(Url);
        (await Read(page, "#b", "background-image")).Should().Be("none", "a shorthand's layer is forced like the longhand");
    }

    /// <summary>The real failing case: a <c>::before</c> texture follows its originating element's adjustment.</summary>
    [Test]
    public async Task APseudoElementIsForcedLikeItsOriginatingElement()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser,
            """
            .card::before { content: ''; background-image: repeating-conic-gradient(red 0 10deg, blue 10deg 20deg) }
            #kept { forced-color-adjust: none }
            """,
            "<div class=card id=forced></div><div class=card id=kept></div>");

        (await Read(page, "#forced", "background-image", "::before")).Should().StartWith("repeating-conic-gradient(");

        await ForceAsync(page, true);
        (await Read(page, "#forced", "background-image", "::before")).Should().Be("none");
        (await Read(page, "#kept", "background-image", "::before")).Should().StartWith("repeating-conic-gradient(",
            "forced-color-adjust inherits from the originating element");

        await ForceAsync(page, false);
        (await Read(page, "#forced", "background-image", "::before")).Should().StartWith("repeating-conic-gradient(");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ShadowsColorSchemeAccentAndScrollbarColorsAreForced()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser,
            "#s { box-shadow: 1px 1px red; text-shadow: 1px 1px red; color-scheme: dark; accent-color: red; scrollbar-color: red blue }",
            "<div id=s></div>");
        await ForceAsync(page, true);

        (await Read(page, "#s", "box-shadow")).Should().Be("none");
        (await Read(page, "#s", "text-shadow")).Should().Be("none");
        (await Read(page, "#s", "color-scheme")).Should().Be("light dark");
        (await Read(page, "#s", "accent-color")).Should().Be("auto");
        (await Read(page, "#s", "scrollbar-color")).Should().Be("auto");

        await ForceAsync(page, false);
        (await Read(page, "#s", "box-shadow")).Should().Be("1px 1px red");
        (await Read(page, "#s", "color-scheme")).Should().Be("dark");
        (await Read(page, "#s", "accent-color")).Should().Be("rgb(255, 0, 0)");
    }

    /// <summary>
    /// <c>none</c> and <c>preserve-parent-color</c> opt out, and inherit. A descendant that opts out still inherits
    /// its parent's forced, inherited value (text-shadow), as in Chromium.
    /// </summary>
    [Test]
    public async Task ForcedColorAdjustOptsOutAndInherits()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser,
            """
            #n { forced-color-adjust: none; box-shadow: 1px 1px red }
            #n > i, #p { background-image: linear-gradient(red, blue) }
            #p { forced-color-adjust: preserve-parent-color }
            #t { text-shadow: 1px 1px red }
            #t > b { forced-color-adjust: none }
            """,
            "<div id=n><i></i></div><div id=p></div><div id=t><b></b></div>");

        (await Read(page, "#t", "forced-color-adjust")).Should().Be("auto");
        (await Read(page, "#n > i", "forced-color-adjust")).Should().Be("none");
        (await Read(page, "#t > b", "text-shadow")).Should().Be("1px 1px red", "text-shadow inherits");

        await ForceAsync(page, true);
        (await Read(page, "#n", "box-shadow")).Should().Be("1px 1px red");
        (await Read(page, "#n > i", "background-image")).Should().StartWith("linear-gradient(");
        (await Read(page, "#p", "background-image")).Should().StartWith("linear-gradient(");
        (await Read(page, "#t", "text-shadow")).Should().Be("none");
        (await Read(page, "#t > b", "text-shadow")).Should().Be("none", "the opted-out child inherits the forced value");
    }

    /// <summary>An author's own forced-colors rule still decides: a url() it chooses is kept, and it can opt out.</summary>
    [Test]
    public async Task AnAuthorForcedColorsRuleStillWins()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser,
            $$"""
            #a, #q { background-image: linear-gradient(red, blue) }
            @media (forced-colors: active) {
              #a { background-image: {{Url}} }
              #q { forced-color-adjust: none }
            }
            """,
            "<div id=a></div><div id=q></div>");
        await ForceAsync(page, true);

        (await Read(page, "#a", "background-image")).Should().Be(Url);
        (await Read(page, "#q", "background-image")).Should().StartWith("linear-gradient(");

        await ForceAsync(page, false);
        (await Read(page, "#a", "background-image")).Should().StartWith("linear-gradient(");
        (await Read(page, "#q", "forced-color-adjust")).Should().Be("auto");
    }

    /// <summary>Colors are not forced (Dom/divergences.md): they keep the cascade's value, as before #4641.</summary>
    [Test]
    public async Task ColorsAreNotForced()
    {
        await using var browser = new Browser();
        var page = await OpenAsync(browser, "#c { color: rgb(1, 2, 3); background-color: rgba(10, 20, 30, 0.5) }", "<div id=c></div>");
        await ForceAsync(page, true);

        (await Read(page, "#c", "color")).Should().Be("rgb(1, 2, 3)");
        (await Read(page, "#c", "background-color")).Should().Be("rgba(10, 20, 30, 0.5)");
    }
}
