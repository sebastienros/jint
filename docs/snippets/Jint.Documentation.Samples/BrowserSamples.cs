using Jint.Browser;

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
}
