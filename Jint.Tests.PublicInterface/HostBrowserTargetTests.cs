#nullable enable
#if NET8_0_OR_GREATER
using Jint.Browser;
using Jint.HtmlParser;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// An embedder driving a page in process can name an element by its role, its label or its text — the
/// target syntax every <see cref="Page"/> member that takes a target accepts — through the published surface
/// alone.
/// </summary>
public sealed class HostBrowserTargetTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task RoleLabelAndTextTargetsReachEveryTargetMember()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <h2>Filters</h2>
            <div data-filter-group=type>
                <label><input type=checkbox> Integration</label>
                <label><input type=checkbox> Hosting</label>
            </div>
            <label for=q>Search</label><input id=q>
            <button onclick="document.title='clicked'" aria-label="Next">›</button>
            """, "http://127.0.0.1/");

        (await page.ClickAsync("role=button[name=\"Next\"]")).Should().BeTrue();
        (await page.TitleAsync()).Should().Be("clicked");

        (await page.ClickAsync("[data-filter-group=type] >> role=checkbox[name=\"Integration\"]")).Should().BeTrue();
        (await page.CountAsync("role=checkbox[checked]")).Should().Be(1);
        (await page.CountAsync("internal:role=checkbox[name=\"integration\"i]")).Should().Be(1);

        (await page.FillAsync("label=Search", "aspire")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('q').value")).Should().Be("aspire");

        (await page.IsVisibleAsync("role=heading[level=2][name=\"Filters\"s]")).Should().BeTrue();
        (await page.WaitForVisibleAsync("text=›", Patience)).Should().BeTrue();
        (await page.TextsAsync("role=checkbox[include-hidden]")).Should().HaveCount(2);

        var thrown = await Caught.ExceptionAsync(() => page.CountAsync("role=button[unknown]"));
        thrown.Should().BeOfType<DomException>().Which.Name.Should().Be("SyntaxError");
    }
}
#endif
