using System.Text;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Files;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// HTML's File Upload state, driven from the host: <c>Page.SetInputFilesAsync</c>, the two events it fires,
/// and what a submission builds out of the selection.
/// </summary>
/// <remarks>
/// The page half of the same state is <see cref="FileTransferTests"/>, which sets <c>input.files</c> from a
/// <c>DataTransfer</c>. Both end in one selection owned by one place, which is why these assert the same
/// three observables: the <c>FileList</c>, <c>input.value</c>, and the entries a form is built from.
/// </remarks>
public sealed class FileInputSelectionTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "jint-file-input-" + Guid.NewGuid().ToString("N"));

    public FileInputSelectionTests() => Directory.CreateDirectory(_directory);

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
    public async Task SelectingHostFilesFillsTheFileListAndFiresInputThenChange()
    {
        var first = Write("greeting.txt", "hello from the host");
        var second = Write("data.json", "{\"answer\":42}");

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        (await page.SetInputFilesAsync("#upload", new[] { first, second })).Should().BeTrue();

        (await Report(page)).Should().Be(
            "true#2#true#true#greeting.txt|text/plain|hello from the host;"
            + "data.json|application/json|{\"answer\":42}#input:upload:true,change:upload:true");

        // HTML's own answer for the value of a file input: a fake path, and only the first file's name.
        (await page.EvaluateAsync<string>("document.getElementById('upload').value"))
            .Should().Be(@"C:\fakepath\greeting.txt");
    }

    [Test]
    public async Task TheTimestampIsTheFileSystemsAndTheTypeComesFromTheExtension()
    {
        var path = Write("notes.md", "# heading");
        var modified = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, modified);

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        await page.SetInputFilesAsync("#upload", new[] { path });

        (await page.EvaluateAsync<string>(
                "(() => { const f = document.getElementById('upload').files[0]; return [f.type, f.lastModified].join('|'); })()"))
            .Should().Be(
                "text/markdown|"
                + new DateTimeOffset(modified).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task AnInputWithoutMultipleTakesTheFirstFileAndNoMore()
    {
        var first = Write("first.txt", "one");
        var second = Write("second.txt", "two");

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        (await page.SetInputFilesAsync("#single", new[] { first, second })).Should().BeTrue();

        (await page.EvaluateAsync<string>(
                "(() => { const files = document.getElementById('single').files; "
                + "return [files.length, ...Array.from(files, f => f.name)].join(','); })()"))
            .Should().Be("1,first.txt");
    }

    [Test]
    public async Task SelectingNoFilesClearsTheSelectionAndStillFiresBothEvents()
    {
        var path = Write("gone.txt", "here");

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        await page.SetInputFilesAsync("#upload", new[] { path });
        await page.EvaluateAsync("window.fileEvents = []");

        (await page.SetInputFilesAsync("#upload", Array.Empty<string>())).Should().BeTrue();

        (await page.EvaluateAsync<string>(
                "(() => { const input = document.getElementById('upload'); "
                + "return [input.files.length, input.value, window.fileEvents.join(',')].join('#'); })()"))
            .Should().Be("0##input:upload:true,change:upload:true");
    }

    [Test]
    public async Task FilesHeldInMemoryCarryTheirOwnNameTypeAndTimestamp()
    {
        var modified = new DateTimeOffset(2019, 7, 8, 9, 10, 11, TimeSpan.Zero);

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        (await page.SetInputFilesAsync(
                "#upload",
                new[]
                {
                    new PageFile("report.csv", "text/csv", Encoding.UTF8.GetBytes("a,b\n1,2"), modified),
                })).Should().BeTrue();

        (await page.EvaluateAndAwaitAsync<string>(
                "(async () => { const f = document.getElementById('upload').files[0]; "
                + "return [f.name, f.type, f.lastModified, await f.text()].join('|'); })()"))
            .Should().Be(
                "report.csv|text/csv|"
                + modified.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|a,b\n1,2");
    }

    [Test]
    public async Task SomethingThatIsNotAFileInputIsAnAnswerRatherThanAFault()
    {
        var path = Write("ignored.txt", "x");

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        (await page.SetInputFilesAsync("#text", new[] { path })).Should().BeFalse("a text input is not a file input");
        (await page.SetInputFilesAsync("#nothing-matches-this", new[] { path })).Should().BeFalse();
    }

    [Test]
    public async Task APathThatNamesNoFileThrowsAndLeavesTheSelectionAlone()
    {
        var path = Write("kept.txt", "kept");

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);

        await page.SetInputFilesAsync("#upload", new[] { path });

        var act = () => page.SetInputFilesAsync("#upload", new[] { Path.Combine(_directory, "absent.txt") });

        await act.Should().ThrowAsync<FileNotFoundException>();
        (await page.EvaluateAsync<string>("document.getElementById('upload').files[0].name")).Should().Be("kept.txt");
    }

    [Test]
    public async Task AFormDataCarriesOneEntryPerSelectedFileAndAnEmptyOneForNone()
    {
        var first = Write("one.txt", "one");
        var second = Write("two.txt", "two");

        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f'>
              <input id='upload' name='attachment' type='file' multiple>
              <input id='empty' name='nothing' type='file'>
            </form>
            """);

        await page.SetInputFilesAsync("#upload", new[] { first, second });

        (await page.EvaluateAsync<string>(
                "(() => { const data = new FormData(document.getElementById('f')); "
                + "return data.getAll('attachment').map(f => f.name + ':' + f.size).join(',') + '#' "
                + "+ data.getAll('nothing').map(f => [f.constructor.name, f.name, f.size, f.type].join('|')).join(','); })()"))
            .Should().Be("one.txt:3,two.txt:3#File||0|application/octet-stream");
    }

    [Test]
    public async Task AMultipartSubmissionCarriesTheFileNameTypeAndBytes()
    {
        var path = Write("upload.txt", "the bytes that were chosen");

        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/echo", request => LoopbackResponse.Html(
                "<title>echo</title><pre id='type'>" + (request.Header("Content-Type") ?? "") + "</pre>"
                + "<pre id='body'>" + System.Net.WebUtility.HtmlEncode(request.Body) + "</pre>"))
            .MapHtml(
                "/form.html",
                """
                <form id='f' action='/echo' method='post' enctype='multipart/form-data'>
                  <input id='upload' name='attachment' type='file'>
                </form>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SetInputFilesAsync("#upload", new[] { path });
        (await fixture.Page.SubmitFormAsync("#f")).Should().NotBeNull();

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('type').textContent"))
            .Should().StartWith("multipart/form-data; boundary=");

        var body = await fixture.Page.EvaluateAsync<string>("document.getElementById('body').textContent");
        body.Should().Contain("Content-Disposition: form-data; name=\"attachment\"; filename=\"upload.txt\"");
        body.Should().Contain("Content-Type: text/plain");
        body.Should().Contain("the bytes that were chosen");
    }

    /// <summary>One file input that takes many, one that takes one, one that is not a file input at all.</summary>
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

    /// <summary>The same report the Playwright course asks for, so the two lanes assert one shape.</summary>
    private static async Task<string?> Report(Page page)
        => await page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const files = document.getElementById('upload').files;
              const details = await Promise.all(Array.from(files, async file =>
                [file.name, file.type, await file.text()].join('|')));
              return [
                files instanceof FileList,
                files.length,
                files.item(0) === files[0],
                files.item(files.length) === null,
                details.join(';'),
                window.fileEvents.join(',')
              ].join('#');
            })()
            """);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
