namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The three commands a client sends instead of asking for a picture.
/// </summary>
/// <remarks>
/// <c>Page.captureScreenshot</c> and <c>Page.printToPDF</c> are refused because this browser renders no
/// pixels, and their refusal names these by name. So each of them has a test: an agent that follows the
/// refusal has to find something at the other end.
/// </remarks>
[NonParallelizable]
public class JintDomainTests
{
    private const string Document = """
        <html>
          <head><title>Extraction</title></head>
          <body>
            <nav><a href="/away">Away</a></nav>
            <main>
              <h1>Heading</h1>
              <p>A paragraph with <strong>emphasis</strong>.</p>
              <ul><li>one</li><li>two</li></ul>
              <img src="/logo.png" alt="Logo">
            </main>
          </body>
        </html>
        """;

    [Test]
    public async Task MarkdownRendersTheDocument()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var result = await session.ResultAsync("Jint.getMarkdown", "{}", attachment);
        var markdown = result.GetProperty("markdown").GetString()!;

        markdown.Should().Contain("# Heading");
        markdown.Should().Contain("**emphasis**");
        markdown.Should().Contain("- one");
        markdown.Should().Contain("![Logo](https://example.test/logo.png)", "a relative source is resolved against the document");
        markdown.Should().Contain("Away", "the whole document is rendered unless mainContentOnly says otherwise");
        result.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task MarkdownHonoursTheOptionsTheExtractorAlreadyHas()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var main = await session.ResultAsync("Jint.getMarkdown", """{"mainContentOnly":true,"includeImages":false}""", attachment);
        var markdown = main.GetProperty("markdown").GetString()!;

        markdown.Should().Contain("# Heading");
        markdown.Should().NotContain("Away", "mainContentOnly renders the <main> and nothing around it");
        markdown.Should().NotContain("![Logo]", "an image without includeImages is its alternative text alone");
        markdown.Should().Contain("Logo");

        var cut = await session.ResultAsync("Jint.getMarkdown", """{"maxLength":20}""", attachment);
        cut.GetProperty("truncated").GetBoolean().Should().BeTrue();
        cut.GetProperty("markdown").GetString()!.Length.Should().BeLessThanOrEqualTo(20);
    }

    [Test]
    public async Task TextRendersTheDocumentTheWayInnerTextDoes()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var text = (await session.ResultAsync("Jint.getText", "{}", attachment)).GetProperty("text").GetString()!;

        text.Should().Contain("Heading");
        text.Should().Contain("A paragraph with emphasis.");
        text.Should().NotContain("<p>", "it is text, not markup");
    }

    [Test]
    public async Task TheAccessibilitySnapshotIsTheTreeAnAgentReads()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var snapshot = (await session.ResultAsync("Jint.getAccessibilitySnapshot", "{}", attachment))
            .GetProperty("snapshot").GetString()!;

        snapshot.Should().Contain("heading", "a <h1> has a role and the snapshot names it");
        snapshot.Should().Contain("Heading");
        snapshot.Should().Contain("link");
        snapshot.Should().Contain("image \"Logo\"", "an image with alternative text is an image in the tree, named by it");

        // The three modes are the three presets, and they are not interchangeable.
        var pruned = (await session.ResultAsync("Jint.getAccessibilitySnapshot", """{"mode":"default"}""", attachment))
            .GetProperty("snapshot").GetString()!;

        var full = (await session.ResultAsync("Jint.getAccessibilitySnapshot", """{"mode":"full"}""", attachment))
            .GetProperty("snapshot").GetString()!;

        full.Length.Should().BeGreaterThan(pruned.Length, "the full tree keeps the nodes the pruned one drops");

        var error = await session.ErrorAsync("Jint.getAccessibilitySnapshot", """{"mode":"upside-down"}""", attachment);
        error.GetProperty("code").GetInt32().Should().Be(-32602);
    }

    [Test]
    public async Task TheRefusalToRenderPixelsNamesWhatToAskInstead()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await OpenAsync(session);

        var error = await session.ErrorAsync("Page.captureScreenshot", "{}", attachment);
        var message = error.GetProperty("message").GetString()!;

        // The one that closes the loop: what the refusal names has to be a command that answers.
        message.Should().Contain("Jint.getMarkdown");
        await session.ResultAsync("Jint.getMarkdown", "{}", attachment);
    }

    private static async Task<string> OpenAsync(PageSession session)
    {
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);

        await page.SetContentAsync(Document, "https://example.test/page");
        return attachment;
    }
}
