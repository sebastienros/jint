using System.Text.RegularExpressions;
using Jint.Tests.Browser.Navigation;
using PuppeteerSharp;

namespace Jint.Tests.Browser.Tool;

public sealed class HttpCacheCommandTests
{
    [TestCase("disabled", "", 2)]
    [TestCase("memory", "", 1)]
    [TestCase("memory", "http-cache-max-bytes", 2)]
    [TestCase("memory", "http-cache-max-entries", 2)]
    [TestCase("memory", "http-cache-max-entry-bytes", 2)]
    public async Task EvaluationHonorsStorageAndAdmissionLimits(string storage, string limit, int expected)
    {
        using var server = new LoopbackServer();
        server.MapHtml("/", "<script src='/a.js'></script><script src='/b.js'></script><script src='/a.js'></script>");
        server.Map("/a.js", _ => LoopbackResponse.Script("window.runs = (window.runs || 0) + 1;").With("Cache-Control", "max-age=60"));
        server.Map("/b.js", _ => LoopbackResponse.Script("window.runs = (window.runs || 0) + 1;").With("Cache-Control", "max-age=60"));
        var arguments = new List<string> { "eval", server.Url("/"), "window.runs", "--http-cache", storage };
        if (limit.Length > 0) arguments.AddRange(["--" + limit, "1"]);
        var result = await ToolRun.RunAsync(arguments.ToArray());
        result.ExitCode.Should().Be(0, result.Error);
        result.Output.Trim().Should().Be("3", "cached scripts must still execute each time");
        server.Received.Count(r => r.Path == "/a.js").Should().Be(expected);
    }

    [Test]
    public async Task FetchPersistsOnlyWithinTheExplicitVisitorIdentity()
    {
        using var server = CachedOrigin();
        var directory = TempDirectory();
        try
        {
            foreach (var partition in new[] { "alice", "alice", "bob" })
            {
                var result = await ToolRun.RunAsync("fetch", server.Url("/"), "--http-cache-dir", directory, "--http-cache-partition", partition);
                result.ExitCode.Should().Be(0, result.Error);
                result.Output.Should().Contain("cached");
            }
            server.Received.Count(r => r.Path == "/").Should().Be(2);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task TemporaryDiskStorageIsRemovedWhenTheCommandEnds()
    {
        using var server = CachedOrigin();
        var directory = TempDirectory();
        try
        {
            var result = await ToolRun.RunAsync("fetch", server.Url("/"), "--http-cache-dir", directory, "--http-cache-temporary");
            result.ExitCode.Should().Be(0, result.Error);
            Directory.EnumerateFileSystemEntries(directory).Should().BeEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestCase("fetch")]
    [TestCase("eval")]
    [TestCase("serve")]
    [TestCase("mcp")]
    public async Task InvalidOrIneffectiveConfigurationIsAUsageError(string command)
    {
        string[][] invalid =
        [
            ["--http-cache", "unknown"],
            ["--http-cache", "disk"],
            ["--http-cache-dir", Path.GetTempPath()],
            ["--http-cache", "memory", "--http-cache-dir", Path.GetTempPath()],
            ["--http-cache", "disabled", "--http-cache-temporary"],
            ["--http-cache-temporary", "--http-cache-partition", "alice"],
            ["--http-cache-max-entries", "1"],
            ["--http-cache", "memory", "--http-cache-max-bytes", "0"],
            ["--http-cache", "memory", "--http-cache-max-entries", "2147483647"],
            ["--http-cache", "memory", "--http-cache-max-entry-bytes", "2gb"],
        ];
        foreach (var options in invalid)
        {
            var arguments = new List<string> { command };
            if (command is "fetch" or "eval") arguments.Add("https://example.invalid/");
            if (command is "eval") arguments.Add("1");
            arguments.AddRange(options);
            var result = await ToolRun.RunAsync(arguments.ToArray());
            result.ExitCode.Should().Be(1, string.Join(' ', arguments));
            result.Error.Should().Contain("http-cache");
            result.Output.Should().BeEmpty();
        }
    }

    [Test]
    public async Task AnUnavailableDiskDirectoryAnswersAUsageError()
    {
        var file = Path.GetTempFileName();
        try
        {
            var result = await ToolRun.RunAsync("fetch", "https://example.invalid/", "--http-cache-dir", file, "--http-cache-partition", "alice");
            result.ExitCode.Should().Be(1);
            result.Error.Should().Contain("storage");
            result.Output.Should().BeEmpty();
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task HelpDocumentsCacheSwitchesAndOwnership()
    {
        var result = await ToolRun.RunAsync("--help");
        result.Output.Should().Contain("--http-cache disabled|memory|disk").And.Contain("disabled by default");
        result.Output.Should().Contain("--http-cache-dir").And.Contain("--http-cache-partition").And.Contain("--http-cache-temporary");
        result.Output.Should().Contain("--http-cache-max-bytes").And.Contain("--http-cache-max-entries").And.Contain("--http-cache-max-entry-bytes");
        result.Output.Should().Contain("default context").And.Contain("other contexts use temporary caches");
    }

    [Test]
    public async Task ServeCachesProtocolCreatedPagesAndIsolatesAdditionalContexts()
    {
        using var origin = CachedOrigin();
        var directory = TempDirectory();
        var patience = TimeSpan.FromSeconds(30);
        try
        {
            for (var visit = 0; visit < 2; visit++)
            {
                using var stopping = new CancellationTokenSource();
                var (exit, output, error) = ToolRun.Start(stopping.Token, "serve", "--port", "0", "--http-cache-dir", directory, "--http-cache-partition", "visitor");
                try
                {
                    (await output.WaitForAsync("Ctrl+C to stop.", patience)).Should().BeTrue(error.Text);
                    var match = Regex.Match(output.Text, @"browser: (ws://\S+)", RegexOptions.None, patience);
                    match.Success.Should().BeTrue(output.Text);
                    await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint = match.Groups[1].Value, DefaultViewport = null }).WaitAsync(patience);
                    await using var page = await browser.NewPageAsync().WaitAsync(patience);
                    await page.GoToAsync(origin.Url("/")).WaitAsync(patience);
                    if (visit == 0)
                    {
                        await using var second = await browser.NewPageAsync().WaitAsync(patience);
                        await second.GoToAsync(origin.Url("/")).WaitAsync(patience);
                        origin.Received.Count(r => r.Path == "/").Should().Be(1);
                        await using (var isolated = await browser.CreateBrowserContextAsync().WaitAsync(patience))
                        {
                            await using var isolatedPage = await isolated.NewPageAsync().WaitAsync(patience);
                            await isolatedPage.GoToAsync(origin.Url("/")).WaitAsync(patience);
                            await isolatedPage.GoToAsync(origin.Url("/")).WaitAsync(patience);
                            origin.Received.Count(r => r.Path == "/").Should().Be(2);
                        }
                        await page.SetCacheEnabledAsync(false).WaitAsync(patience);
                        await page.GoToAsync(origin.Url("/")).WaitAsync(patience);
                        origin.Received.Count(r => r.Path == "/").Should().Be(3);
                    }
                    else origin.Received.Count(r => r.Path == "/").Should().Be(3, "the default visitor's persisted cache survives the serve restart");
                    browser.Disconnect();
                }
                finally
                {
                    await stopping.CancelAsync();
                    (await exit.WaitAsync(patience)).Should().Be(0, error.Text);
                }
                Directory.EnumerateDirectories(directory).Should().HaveCount(1, "additional contexts own temporary partitions");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jint-cli-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static LoopbackServer CachedOrigin()
    {
        var server = new LoopbackServer();
        server.Map("/", _ => LoopbackResponse.Html("<h1>cached</h1>").With("Cache-Control", "max-age=600"));
        return server;
    }
}
