#nullable enable

namespace Jint.Tests.Browser;

/// <summary>DOM scope matching rejects unsupported selectors with catchable SyntaxErrors.</summary>
public sealed class UnsupportedSelectorTests
{
    [TestCase(":host-context(p)")]
    [TestCase("#target, :host-context(p)")]
    [TestCase(":not(:host-context(p))")]
    [TestCase(":has(:host-context(p))")]
    [TestCase(@":HoSt-\63 ontext(p)")]
    [TestCase(":unchecked")]
    [TestCase("#target, :unchecked")]
    public async Task UnsupportedPredicatesAreCatchableAcrossDomEntryPoints(string selector)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id='target'></p>");
        var literal = System.Text.Json.JsonSerializer.Serialize(selector);
        (await page.EvaluateAsync<string>($$"""
            (() => {
                const selector = {{literal}}, target = document.getElementById('target');
                const empty = document.createDocumentFragment();
                const shadow = target.attachShadow({ mode: 'open' });
                const calls = [
                    () => document.querySelector(selector), () => document.querySelectorAll(selector),
                    () => target.querySelector(selector), () => target.querySelectorAll(selector),
                    () => target.matches(selector), () => target.webkitMatchesSelector(selector),
                    () => target.closest(selector),
                    () => empty.querySelector(selector), () => empty.querySelectorAll(selector),
                    () => shadow.querySelector(selector), () => shadow.querySelectorAll(selector)
                ];
                return calls.map(call => {
                    try { call(); return 'accepted'; } catch (e) { return e.name; }
                }).join('/');
            })()
            """)).Should().Be(string.Join("/", Enumerable.Repeat("SyntaxError", 11)));
        (await page.EvaluateAsync<string>("document.querySelector('#target').id")).Should().Be("target");
        page.Errors.Should().BeEmpty();
    }
}
