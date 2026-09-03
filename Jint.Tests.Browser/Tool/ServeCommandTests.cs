using System.Net.Http;
using System.Text.RegularExpressions;

namespace Jint.Tests.Browser.Tool;

/// <summary>
/// <c>jint-browser serve</c>: a browser on a port, and a command that stops when it is told to.
/// </summary>
/// <remarks>
/// Every run here takes port <b>0</b> and reads the port out of the banner, which is the same reason the
/// rest of this suite's servers do: a fixed 9222 would collide with whatever else is on the machine, and a
/// test that only passes on an idle machine is not one.
/// </remarks>
public sealed class ServeCommandTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Test]
    public async Task ThePortIsAnnouncedAndAnswersTheVersionDocument()
    {
        using var stopping = new CancellationTokenSource();
        var (exit, output, _) = ToolRun.Start(stopping.Token, "serve", "--port", "0");

        var version = await ReadVersionUrlAsync(output);

        using var client = new HttpClient();
        var document = await client.GetStringAsync(new Uri(version));

        document.Should().Contain("webSocketDebuggerUrl").And.Contain("Protocol-Version");

        await stopping.CancelAsync();
        (await exit).Should().Be(0);
    }

    [Test]
    public async Task ABrowserOpensWithATabSoAClientListingTargetsFindsOne()
    {
        using var stopping = new CancellationTokenSource();
        var (exit, output, _) = ToolRun.Start(stopping.Token, "serve", "--port", "0");

        var version = await ReadVersionUrlAsync(output);
        var list = version.Replace("/json/version", "/json/list", StringComparison.Ordinal);

        using var client = new HttpClient();
        var document = await client.GetStringAsync(new Uri(list));

        document.Should().Contain("\"type\":\"page\"").And.Contain("about:blank");

        await stopping.CancelAsync();
        (await exit).Should().Be(0);
    }

    [Test]
    public async Task CancellationStopsItCleanly()
    {
        using var stopping = new CancellationTokenSource();
        var (exit, output, error) = ToolRun.Start(stopping.Token, "serve", "--port", "0");

        (await output.WaitForAsync("Ctrl+C to stop.", Patience)).Should().BeTrue(output.Text);

        await stopping.CancelAsync();

        var completed = await Task.WhenAny(exit, Task.Delay(Patience));
        completed.Should().BeSameAs((Task) exit, "the command has to end when it is told to, not when the process does");

        (await exit).Should().Be(0);
        output.Text.Should().Contain("Stopping.");
        error.Text.Should().BeEmpty();
    }

    [Test]
    public async Task TheBannerSaysWhetherThePagesAreHardened()
    {
        using var stopping = new CancellationTokenSource();
        var (exit, output, _) = ToolRun.Start(stopping.Token, "serve", "--port", "0", "--untrusted");

        (await output.WaitForAsync("Ctrl+C to stop.", Patience)).Should().BeTrue(output.Text);
        output.Text.Should().Contain("hardened profile for content nobody vouches for");

        await stopping.CancelAsync();
        (await exit).Should().Be(0);
    }

    [Test]
    public async Task APositionalArgumentIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("serve", "https://example.com");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("usage: jint-browser serve");
    }

    [Test]
    public async Task APortThatIsNotOneIsExitCodeOne()
    {
        var run = await ToolRun.RunAsync("serve", "--port", "99999");

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("is not a port");
    }

    private static async Task<string> ReadVersionUrlAsync(RecordingWriter output)
    {
        (await output.WaitForAsync("Ctrl+C to stop.", Patience)).Should().BeTrue(output.Text);

        // The banner is what a user reads the endpoint off, so the test reads it the same way rather than
        // reaching into the server: a banner that stopped naming the port would fail here.
        var match = Regex.Match(output.Text, @"version: (http://\S+)", RegexOptions.None, TimeSpan.FromSeconds(5));
        match.Success.Should().BeTrue(output.Text);

        new Uri(match.Groups[1].Value).Port.Should().BeGreaterThan(0, "--port 0 asks the operating system for one");
        return match.Groups[1].Value;
    }
}
