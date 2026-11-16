using Jint.Browser;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// <see cref="BrowserOptions.ShareParsedScripts"/>: a classic script of 1 KiB or more is parsed once per process.
/// Every case runs both ways, because the shared parse must be indistinguishable from the page's own.
/// </summary>
public sealed class SharedScriptParseTests
{
    // Big enough to be shared; unique per test so no other test's page warms the cache for this one.
    private static string Padding() => "/*" + Guid.NewGuid().ToString("N") + new string(' ', 1100) + "*/";

    [TestCase(true)]
    [TestCase(false)]
    public async Task AScriptOnALaterLineReportsTheDocumentsLine(bool shared)
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { ShareParsedScripts = shared });
        var markup = "<!doctype html>\n<p>one</p>\n<p>two</p>\n<script>window.line = 0; window.onerror = (m, s, line) => { window.line = line; };</script>\n"
            + "<script>" + Padding() + "\n\nnull.boom;</script>";
        var line = markup.Split('\n').Length;

        for (var i = 0; i < 2; i++)
        {
            var page = await browser.NewPageAsync();
            await page.SetContentAsync(markup);
            (await page.EvaluateAsync<int>("window.line")).Should().Be(line, "page {0}", i);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task EveryPageRunsTheScriptInItsOwnEngine(bool shared)
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { ShareParsedScripts = shared });
        var markup = "<script>" + Padding() + "function counted() { return 'kept'; } window.hits = (window.hits || 0) + 1;</script>";

        for (var i = 0; i < 2; i++)
        {
            var page = await browser.NewPageAsync();
            await page.SetContentAsync(markup);
            (await page.EvaluateAsync<int>("window.hits")).Should().Be(1);
            (await page.EvaluateAsync<string>("counted.toString()")).Should().Be("function counted() { return 'kept'; }");
            page.Errors.Should().BeEmpty();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AScriptThatDoesNotParseIsStillASyntaxErrorAndTheNextScriptRuns(bool shared)
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { ShareParsedScripts = shared });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            "<script>window.kind = ''; window.onerror = (m, s, l, c, e) => { window.kind = e && e.name; };</script>"
            + "<script>" + Padding() + "function (</script><script>window.after = true;</script>");

        (await page.EvaluateAsync<string>("window.kind")).Should().Be("SyntaxError");
        (await page.EvaluateAsync<bool>("window.after")).Should().BeTrue();
    }

    [Test]
    public void ScriptsAreSharedByDefault()
    {
        new BrowserOptions().ShareParsedScripts.Should().BeTrue();
    }
}
