#nullable enable
#if NET8_0_OR_GREATER
using System.Text.RegularExpressions;
using Jint.Browser;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// An embedder asserting on a page's URL, attributes, focus, values and checkedness reaches every native
/// reader and wait from outside the package, with no JavaScript predicate (#4626).
/// </summary>
public sealed class HostBrowserStateWaitTests
{
    // A wedge ceiling: each wait below is expected to succeed, and a failing one answers false long before it.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task AnEmbedderWaitsForPageStateNatively()
    {
        await using var browser = new Jint.Browser.Browser();
        Page page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><input id=c type=checkbox><input id=v><button id=b data-busy>Next</button>
            <script>setTimeout(() => { history.pushState(null, '', '?page=2'); c.checked = true; v.value = 'typed'; b.removeAttribute('data-busy'); b.setAttribute('aria-current', 'page'); b.focus(); }, 200)</script>
            """, "http://127.0.0.1/");

        var waits = new[]
        {
            page.WaitForUrlAsync("?page=2", Patience),
            page.WaitForUrlAsync(new Regex(@"\?page=2$"), Patience),
            page.WaitForUrlAsync(uri => uri.Query == "?page=2", Patience),
            page.WaitForCheckedAsync("#c", true, Patience),
            page.WaitForValueAsync("#v", "typed", Patience),
            page.WaitForValueAsync("#v", new Regex("^ty"), Patience),
            page.WaitForAttributeAsync("#b", "aria-current", "page", Patience),
            page.WaitForAttributeAsync("#b", "aria-current", new Regex("^pa"), Patience),
            page.WaitForAttributeAsync("#b", "data-busy", value: null, Patience),
            page.WaitForFocusAsync("#b", Patience),
            page.WaitForFocusAsync("#c", Patience, focused: false),
        };

        (await Task.WhenAll(waits)).Should().AllSatisfy(satisfied => satisfied.Should().BeTrue());
        (await page.GetAttributeAsync("#b", "aria-current")).Should().Be("page");
        (await page.InputValueAsync("#v")).Should().Be("typed");
        (await page.IsCheckedAsync("#c")).Should().BeTrue();
        (await page.IsFocusedAsync("#b")).Should().BeTrue();
        (await page.WaitForCheckedAsync("#c", false, TimeSpan.Zero)).Should().BeFalse();
    }
}
#endif
