using System.Text.Json;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// <c>DOM.setFileInputFiles</c> over the real protocol — the command Playwright's <c>setInputFiles</c> and
/// Puppeteer's <c>uploadFile</c> send.
/// </summary>
/// <remarks>
/// These assert the envelope as text for the reason <c>DomDomainTests</c> gives, and they address the node
/// by all three of the identifiers the command takes, because a client library picks a different one from
/// the next: Puppeteer sends <c>objectId</c> and <c>backendNodeId</c> together, Playwright sends
/// <c>objectId</c>, and a front end sends a <c>nodeId</c>.
/// </remarks>
[NonParallelizable]
public sealed class DomFileInputTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "jint-dom-file-input-" + Guid.NewGuid().ToString("N"));

    public DomFileInputTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A temporary directory that outlives one test run is not a test failure.
        }
    }

    [Test]
    public async Task FilesReachThePageAsAFileListAndTheEventsAUserWouldCause()
    {
        var first = Write("greeting.txt", "hello from the protocol");
        var second = Write("data.json", "{\"answer\":42}");

        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, Markup);

        var objectId = await ObjectIdAsync(session, attachment, "document.getElementById('upload')");

        await session.ResultAsync(
            "DOM.setFileInputFiles",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["objectId"] = objectId,
                ["files"] = new[] { first, second },
            }),
            attachment);

        var report = await session.EvaluateAsync(
            """
            (() => {
              const input = document.getElementById('upload');
              return [
                input.files instanceof FileList,
                input.files.length,
                Array.from(input.files, f => f.name + '|' + f.type + '|' + f.size).join(';'),
                input.value,
                window.fileEvents.join(',')
              ].join('#');
            })()
            """,
            attachment);

        report.GetProperty("value").GetString().Should().Be(
            "true#2#greeting.txt|text/plain|23;data.json|application/json|13#"
            + @"C:\fakepath\greeting.txt#input:upload:true,change:upload:true");
    }

    [Test]
    public async Task ANodeIdAndABackendNodeIdAddressTheSameInput()
    {
        var path = Write("by-id.txt", "one");

        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, Markup);
        await session.ResultAsync("DOM.enable", "{}", attachment);

        var objectId = await ObjectIdAsync(session, attachment, "document.getElementById('upload')");
        var described = (await session.ResultAsync(
            "DOM.describeNode", $$"""{"objectId":"{{objectId}}"}""", attachment)).GetProperty("node");
        var backendNodeId = described.GetProperty("backendNodeId").GetInt32();

        await session.ResultAsync(
            "DOM.setFileInputFiles",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["backendNodeId"] = backendNodeId,
                ["files"] = new[] { path },
            }),
            attachment);

        (await NamesAsync(session, attachment)).Should().Be("by-id.txt");

        var nodeId = (await session.ResultAsync(
            "DOM.requestNode", $$"""{"objectId":"{{objectId}}"}""", attachment)).GetProperty("nodeId").GetInt32();

        await session.ResultAsync(
            "DOM.setFileInputFiles",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["nodeId"] = nodeId,
                ["files"] = Array.Empty<string>(),
            }),
            attachment);

        (await NamesAsync(session, attachment)).Should().BeEmpty("an empty list is a selection, and it clears the input");
    }

    [Test]
    public async Task AnInputWithoutMultipleKeepsTheFirstFileOnly()
    {
        var first = Write("first.txt", "one");
        var second = Write("second.txt", "two");

        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, Markup);

        await session.ResultAsync(
            "DOM.setFileInputFiles",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["objectId"] = await ObjectIdAsync(session, attachment, "document.getElementById('single')"),
                ["files"] = new[] { first, second },
            }),
            attachment);

        (await session.EvaluateAsync(
                "Array.from(document.getElementById('single').files, f => f.name).join(',')",
                attachment))
            .GetProperty("value").GetString().Should().Be("first.txt");
    }

    [Test]
    public async Task ANodeThatIsNotAFileInputIsRefusedInChromesWording()
    {
        var path = Write("ignored.txt", "x");

        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, Markup);

        foreach (var expression in new[] { "document.getElementById('text')", "document.body" })
        {
            var error = await session.ErrorAsync(
                "DOM.setFileInputFiles",
                JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["objectId"] = await ObjectIdAsync(session, attachment, expression),
                    ["files"] = new[] { path },
                }),
                attachment);

            error.GetProperty("message").GetString().Should().Be("Node is not a file input element");
        }
    }

    [Test]
    public async Task APathThatNamesNoFileIsRefusedAndTheSelectionStands()
    {
        var kept = Write("kept.txt", "kept");

        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, Markup);

        var objectId = await ObjectIdAsync(session, attachment, "document.getElementById('upload')");

        await session.ResultAsync(
            "DOM.setFileInputFiles",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["objectId"] = objectId,
                ["files"] = new[] { kept },
            }),
            attachment);

        var missing = Path.Combine(_directory, "absent.txt");
        var error = await session.ErrorAsync(
            "DOM.setFileInputFiles",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["objectId"] = objectId,
                ["files"] = new[] { missing },
            }),
            attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Cannot read file " + missing);

        (await NamesAsync(session, attachment)).Should().Be("kept.txt", "a failed read changes nothing");
    }

    [Test]
    public async Task NoIdentifierAtAllIsRefusedTheWayEveryOtherDomCommandRefusesIt()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, Markup);

        var error = await session.ErrorAsync("DOM.setFileInputFiles", """{"files":[]}""", attachment);

        error.GetProperty("message").GetString()
            .Should().Be("Either nodeId, backendNodeId or objectId must be specified");
    }

    /// <summary>A file input that takes many, one that takes one, and a control that is not one at all.</summary>
    private const string Markup =
        """
        <input id='upload' type='file' multiple>
        <input id='single' type='file'>
        <input id='text'>
        <script>
          window.fileEvents = [];
          for (const type of ['input', 'change']) {
            document.body.addEventListener(type, event => {
              window.fileEvents.push(type + ':' + event.target.id + ':' + event.bubbles);
            });
          }
        </script>
        """;

    private static async Task Content(PageSession session, string attachment, string body)
        => await session.ResultAsync(
            "Page.setDocumentContent",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["frameId"] = "", ["html"] = body }),
            attachment);

    private static async Task<string> ObjectIdAsync(PageSession session, string attachment, string expression)
    {
        var result = await session.EvaluateAsync(expression, attachment, returnByValue: false);
        return result.GetProperty("objectId").GetString()!;
    }

    private static async Task<string> NamesAsync(PageSession session, string attachment)
        => (await session.EvaluateAsync(
                "Array.from(document.getElementById('upload').files, f => f.name).join(',')",
                attachment))
            .GetProperty("value").GetString()!;

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
