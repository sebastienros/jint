using System.Diagnostics;
using System.Text.Json;
using Jint.Tests.Browser.Mcp;
using Jint.Tests.Browser.Navigation;
using ModelContextProtocol.Client;
using PuppeteerSharp;

namespace Jint.Tests.Browser.Tool;

/// <summary>Drives the installed distribution, not the in-process copy of the command line.</summary>
public sealed class PublishedToolTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static bool HasPublishedTool => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JINT_BROWSER_TOOL"));
    private static string Executable => Path.GetFullPath(Environment.GetEnvironmentVariable("JINT_BROWSER_TOOL")!);

    [Test]
    [IgnoreUnless(nameof(HasPublishedTool), "Set JINT_BROWSER_TOOL to an installed or published executable.")]
    public async Task TheVersionIsThePackageVersion()
    {
        var result = await RunAsync("--version");

        result.ExitCode.Should().Be(0, result.Error);
        result.Error.Should().BeEmpty();
        result.Output.Trim().Split('+')[0].Should().Be(
            Environment.GetEnvironmentVariable("JINT_BROWSER_TOOL_VERSION"),
            "the distribution must carry the release tag's version");
    }

    [TestCase("html")]
    [TestCase("text")]
    [TestCase("markdown")]
    [TestCase("ax")]
    [IgnoreUnless(nameof(HasPublishedTool), "Set JINT_BROWSER_TOOL to an installed or published executable.")]
    public async Task FetchRunsScriptsAndExtractsTheDocument(string format)
    {
        using var server = Serve();

        var result = await RunAsync("fetch", server.Url("/"), "--dump", format, "--timeout", "10s");

        result.ExitCode.Should().Be(0, result.Error);
        result.Error.Should().BeEmpty();
        result.Output.Should().Contain("Native ready");
        server.Received.Should().Contain(request => request.Path == "/app.js");
    }

    [Test]
    [IgnoreUnless(nameof(HasPublishedTool), "Set JINT_BROWSER_TOOL to an installed or published executable.")]
    public async Task EvaluationPreservesCssXmlXPathAndGlobalization()
    {
        using var server = Serve();

        var result = await RunAsync("eval", server.Url("/"), """
            [
                document.body.dataset.ready,
                getComputedStyle(document.querySelector('h1')).color,
                new DOMParser().parseFromString('<x>xml</x>', 'text/xml').documentElement.textContent,
                document.evaluate('count(//h1)', document, null, XPathResult.NUMBER_TYPE, null).numberValue,
                new Intl.NumberFormat('de-DE').format(1234.5)
            ]
            """, "--timeout", "10s");

        result.ExitCode.Should().Be(0, result.Error);
        result.Error.Should().BeEmpty();
        using var json = JsonDocument.Parse(result.Output);
        json.RootElement[0].GetString().Should().Be("yes");
        json.RootElement[1].GetString().Should().Be("rgba(255, 0, 0, 1)");
        json.RootElement[2].GetString().Should().Be("xml");
        json.RootElement[3].GetInt32().Should().Be(1);
        json.RootElement[4].GetString().Should().Be("1.234,5");
    }

    [Test]
    [IgnoreUnless(nameof(HasPublishedTool), "Set JINT_BROWSER_TOOL to an installed or published executable.")]
    public async Task UsageAndScriptErrorsKeepTheirExitCodes()
    {
        var usage = await RunAsync("--unknown");
        usage.ExitCode.Should().Be(1);
        usage.Error.Should().NotBeEmpty();

        var script = await RunAsync("eval", "about:blank", "missingFunction()", "--timeout", "10s");
        script.ExitCode.Should().Be(4);
        script.Output.Should().BeEmpty();
        script.Error.Should().Contain("missingFunction");
    }

    [Test]
    [IgnoreUnless(nameof(HasPublishedTool), "Set JINT_BROWSER_TOOL to an installed or published executable.")]
    public async Task ServeAcceptsARealCdpClient()
    {
        using var server = Serve();
        using var process = new ToolProcess("serve", "--port", "0");
        using var stopping = new CancellationTokenSource(Patience);
        string? endpoint = null;

        while (await process.Output.ReadLineAsync(stopping.Token) is { } line)
        {
            if (line.Trim().StartsWith("browser: ", StringComparison.Ordinal))
            {
                endpoint = line.Trim()["browser: ".Length..];
                break;
            }
        }

        endpoint.Should().NotBeNull("the published server must announce its WebSocket");
        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = endpoint,
            DefaultViewport = null,
        }).WaitAsync(Patience);
        await using var page = await browser.NewPageAsync().WaitAsync(Patience);
        await page.GoToAsync(server.Url("/")).WaitAsync(Patience);

        (await page.EvaluateExpressionAsync<string>("document.querySelector('h1').textContent")
            .WaitAsync(Patience)).Should().Be("Native ready");
        browser.Disconnect();
    }

    [Test]
    [IgnoreUnless(nameof(HasPublishedTool), "Set JINT_BROWSER_TOOL to an installed or published executable.")]
    public async Task McpPublishesSchemasAndDrivesTheBrowserOverStdio()
    {
        using var server = Serve();
        using var stopping = new CancellationTokenSource(Patience);
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = Executable,
            Arguments = ["mcp", "--allow-private-network"],
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: stopping.Token);

        var tools = await client.ListToolsAsync(cancellationToken: stopping.Token);
        tools.Select(tool => tool.Name).Should().Contain(["navigate", "snapshot", "fill", "click", "evaluate"]);

        var navigation = await client.CallToolAsync("navigate",
            new Dictionary<string, object?> { ["url"] = server.Url("/") }, cancellationToken: stopping.Token);
        navigation.IsError.Should().NotBe(true, McpFixture.TextOf(navigation));

        var fill = await client.CallToolAsync("fill",
            new Dictionary<string, object?> { ["target"] = "#name", ["text"] = "Native input" }, cancellationToken: stopping.Token);
        fill.IsError.Should().NotBe(true, McpFixture.TextOf(fill));
        var click = await client.CallToolAsync("click",
            new Dictionary<string, object?> { ["target"] = "#apply" }, cancellationToken: stopping.Token);
        click.IsError.Should().NotBe(true, McpFixture.TextOf(click));

        var snapshot = await client.CallToolAsync("snapshot", cancellationToken: stopping.Token);
        snapshot.IsError.Should().NotBe(true, McpFixture.TextOf(snapshot));
        McpFixture.TextOf(snapshot).Should().Contain("Native input");

        var resource = await client.ReadResourceAsync("jint://page/markdown", cancellationToken: stopping.Token);
        resource.Contents.OfType<global::ModelContextProtocol.Protocol.TextResourceContents>()
            .Single().Text.Should().Contain("Native input");
    }

    private static LoopbackServer Serve()
    {
        var server = new LoopbackServer();
        server.MapHtml("/", """
            <!doctype html><title>Native browser</title>
            <link rel="stylesheet" href="/style.css">
            <h1>Before scripts</h1><input id="name"><button id="apply">Apply</button>
            <script src="/app.js"></script>
            """);
        server.Map("/style.css", _ => LoopbackResponse.Css("h1 { color: red }"));
        server.Map("/app.js", _ => LoopbackResponse.Script("""
            document.body.dataset.ready = 'yes';
            document.querySelector('h1').textContent = 'Native ready';
            document.querySelector('#apply').addEventListener('click', () => {
                document.querySelector('h1').textContent = document.querySelector('#name').value;
            });
            """));
        return server;
    }

    private static async Task<ToolResult> RunAsync(params string[] arguments)
    {
        using var process = new ToolProcess(arguments);
        var output = process.Output.ReadToEndAsync();
        await process.Process.WaitForExitAsync().WaitAsync(Patience);
        return new ToolResult(process.Process.ExitCode, await output, await process.Error);
    }

    private sealed class ToolProcess : IDisposable
    {
        internal ToolProcess(params string[] arguments)
        {
            var start = new ProcessStartInfo(Executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            Process = Process.Start(start) ?? throw new InvalidOperationException("The browser tool did not start.");
            Error = Process.StandardError.ReadToEndAsync();
        }

        internal Process Process { get; }
        internal StreamReader Output => Process.StandardOutput;
        internal Task<string> Error { get; }

        public void Dispose()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                Process.WaitForExit((int) Patience.TotalMilliseconds).Should().BeTrue("the tool must not outlive its test");
            }

            Process.Dispose();
        }
    }
}
