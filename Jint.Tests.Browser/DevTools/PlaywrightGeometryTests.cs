using Jint.Browser;
using Microsoft.Playwright;

namespace Jint.Tests.Browser.DevTools;

public partial class PlaywrightCourseTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task PlaywrightClicksThePrimaryRegionOfAFlexWrapperAndItsSeparateTrailingControl(bool offscreen)
    {
        var spacer = offscreen ? string.Concat(Enumerable.Repeat("<p>Spacer</p>", 20)) : "";
        await using var lane = await ClientLane.OpenAsync(server => server.MapHtml("/row",
            spacer
            + """
              <div id="summary" style="display:flex">
                <button id="expand" style="flex:1" onclick="document.getElementById('controls').hidden=false">
                  <span>Expand operation</span>
                </button>
                <button id="trailing" style="flex:0 0 64px" onclick="window.trailingClicks++">
                  <span>More</span><span>options</span><span>here</span>
                </button>
              </div>
              <div id="controls" hidden><input aria-label="Parameter"><button id="execute">Execute</button></div>
              <script>
                window.trailingClicks = 0;
                window.clicked = [];
                document.addEventListener('click', e => clicked.push(e.target.closest('button')?.id));
              </script>
              """
            + string.Concat(Enumerable.Repeat("<p>Footer</p>", 20))),
            new BrowserOptions { Viewport = new Viewport(800, 160) });
        var page = await lane.Context.NewPageAsync();
        await page.GotoAsync(lane.Server.Url("/row"));

        await page.Locator("#summary").ClickAsync();
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Parameter" }).FillAsync("value");
        await page.Locator("#execute").ClickAsync();
        await page.Locator("#trailing").ClickAsync();
        (await page.EvaluateAsync<string>("() => clicked.join(',')")).Should().Be("expand,execute,trailing");
        (await page.EvaluateAsync<int>("() => trailingClicks")).Should().Be(1);
        (await page.GetByRole(AriaRole.Textbox, new() { Name = "Parameter" }).InputValueAsync()).Should().Be("value");
        foreach (var hostPage in lane.Pages.Contexts.SelectMany(context => context.Pages))
        {
            hostPage.Errors.Should().BeEmpty();
        }
    }
}
