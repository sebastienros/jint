using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Layout;

/// <summary>
/// Pages that load the same sheets share their rule index, measurement dependencies, grouping bodies and media lists;
/// what a script changes in one page's CSSOM stays that page's.
/// </summary>
public sealed class SharedStylePlanTests
{
    private const string Sheet = """
        .a { color: red }
        .host { color: blue; .nested { display: none } }
        @media screen { .b { color: green } }
        """;

    private static Task<LoopbackPage> CreateAsync() => LoopbackPage.CreateAsync(server => server
        .Map("/page", _ => LoopbackResponse.Html("<link rel='stylesheet' href='/sheet.css'><p class='a' id='a'>a</p><div class='host'><p class='nested' id='nested'>n</p></div><p class='b' id='b'>b</p>"))
        .Map("/sheet.css", _ => LoopbackResponse.Css(Sheet).With("Cache-Control", "max-age=60")),
        options => options.HttpCache.Storage = BrowserHttpCacheStorage.Memory);

    private static Task<double> HeightOf(Page page, string id)
        => page.EvaluateAsync<double>($"document.getElementById('{id}').getBoundingClientRect().height");

    [Test]
    public async Task ARuleAScriptMakesALayoutRuleMovesItsBoxInThatPageAlone()
    {
        await using var fixture = await CreateAsync();
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await second.NavigateAsync(fixture.Url("/page"));
        (await HeightOf(fixture.Page, "a")).Should().BeGreaterThan(0);
        (await HeightOf(second, "a")).Should().BeGreaterThan(0);

        // The rule declared no layout property when both pages shared its plan.
        await second.EvaluateAsync("document.styleSheets[0].cssRules[0].style.display = 'none'");
        (await HeightOf(second, "a")).Should().Be(0);
        (await HeightOf(fixture.Page, "a")).Should().BeGreaterThan(0);

        // A page loaded after the edit reads the sheet as served.
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await HeightOf(fixture.Page, "a")).Should().BeGreaterThan(0);
        fixture.Page.Errors.Should().BeEmpty();
        second.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ANestedRuleStillAppliesAndADeclarationOnlyRuleHasNoChildRules()
    {
        await using var fixture = await CreateAsync();
        for (var i = 0; i < 2; i++)
        {
            await fixture.Page.NavigateAsync(fixture.Url("/page"));
            (await HeightOf(fixture.Page, "nested")).Should().Be(0);
            (await fixture.Page.EvaluateAsync<int>("document.styleSheets[0].cssRules[0].cssRules.length")).Should().Be(0);
            (await fixture.Page.EvaluateAsync<int>("document.styleSheets[0].cssRules[1].cssRules.length")).Should().Be(1);
        }

        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AMediaListAScriptChangesStaysThatPagesOwn()
    {
        await using var fixture = await CreateAsync();
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        await second.NavigateAsync(fixture.Url("/page"));
        const string Media = "document.styleSheets[0].cssRules[2].media.mediaText";

        await second.EvaluateAsync("document.styleSheets[0].cssRules[2].media.appendMedium('print')");
        (await second.EvaluateAsync<string>(Media)).Should().Be("screen, print");
        (await fixture.Page.EvaluateAsync<string>(Media)).Should().Be("screen");

        await fixture.Page.NavigateAsync(fixture.Url("/page"));
        (await fixture.Page.EvaluateAsync<string>(Media)).Should().Be("screen");
        fixture.Page.Errors.Should().BeEmpty();
        second.Errors.Should().BeEmpty();
    }
}
