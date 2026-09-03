namespace Jint.Tests.Browser.Tool;

/// <summary>
/// <c>jint-browser mcp</c>'s command line.
/// </summary>
/// <remarks>
/// What the command <i>serves</i> is covered by <c>Jint.Tests.Browser/Mcp/</c>, which drives the same server
/// through a real client over a stream transport. What is here is the half that only exists on a command
/// line: the options it accepts, the ones it refuses, and that it is named where a user would look.
/// </remarks>
public sealed class McpCommandTests
{
    [Test]
    public async Task TheHelpNamesTheCommandAndItsTransport()
    {
        var run = await ToolRun.RunAsync("--help");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Contain("mcp").And.Contain("Model Context Protocol");
        run.Output.Should().Contain("--stdio").And.Contain("--trusted");
    }

    [Test]
    public async Task AnUnknownCommandNamesMcpAmongTheOnesThereAre()
    {
        var run = await ToolRun.RunAsync("browse");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("serve, fetch, eval, mcp and version");
    }

    [Test]
    public async Task APositionalArgumentIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("mcp", "https://example.com");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("usage: jint-browser mcp");
    }

    [Test]
    public async Task AnUnknownOptionIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("mcp", "--http", "3001");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("unknown option '--http'", "the transport is stdio, and a switch for one there is not is a refusal rather than a silent stdio");
    }

    [Test]
    public async Task BlockAndAllowTogetherIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("mcp", "--block-private-network", "--allow-private-network");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("opposite things");
    }

    [Test]
    public async Task AValueThatIsNotADurationIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("mcp", "--timeout", "soon");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("is not a duration");
    }

    [Test]
    public async Task ASnapshotCeilingOfZeroIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("mcp", "--max-snapshot-length", "0");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("at least 1");
    }
}
