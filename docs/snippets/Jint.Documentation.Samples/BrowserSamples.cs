using Jint.Browser;
using Jint.Browser.Playwright;

namespace Documentation.Samples;

public static class BrowserSamples
{
    public static async Task BrowserLandingPage()
    {
        #region docs:package-browser-first-page

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.NavigateAsync("https://example.org/");
        Console.WriteLine(await page.MarkdownAsync());

        #endregion
    }

    public static async Task PlaywrightLandingPage()
    {
        #region docs:package-playwright-first-page

        await using var browser = await JintPlaywright.BrowserType.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<button id='save'>Save</button>");
        await page.Locator("#save").ClickAsync();

        #endregion
    }
}
