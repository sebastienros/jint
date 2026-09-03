using System.Text;
using ToolProgram = global::Jint.Browser.Tool.ToolProgram;

namespace Jint.Tests.Browser.Tool;

/// <summary>What one run of <c>jint-browser</c> left behind.</summary>
/// <param name="ExitCode">The code the process would have exited with.</param>
/// <param name="Output">Everything written to standard output.</param>
/// <param name="Error">Everything written to standard error.</param>
internal readonly record struct ToolResult(int ExitCode, string Output, string Error);

/// <summary>
/// Runs the tool's entry point in this process, with writers a test can read.
/// </summary>
/// <remarks>
/// <b>In process rather than as a child process, deliberately.</b> The exit code, standard output and
/// standard error are one assertion that way; the suite needs no published binary and no path to one; a
/// failure names a line of the tool; and a <c>serve</c> that would not stop is a cancelled token rather than
/// a process nobody killed. What it does not cover is the process shell — the UTF-8 writers and the
/// Ctrl+C handler in <c>Program.cs</c> — which is eleven lines with no branch in them.
/// </remarks>
internal static class ToolRun
{
    /// <summary>Runs a command to completion.</summary>
    internal static async Task<ToolResult> RunAsync(params string[] arguments)
    {
        var output = new RecordingWriter();
        var error = new RecordingWriter();

        var exitCode = await ToolProgram.RunAsync(arguments, output, error).ConfigureAwait(false);

        return new ToolResult(exitCode, output.Text, error.Text);
    }

    /// <summary>Starts a command that runs until it is stopped, and answers its writers while it does.</summary>
    internal static (Task<int> Exit, RecordingWriter Output, RecordingWriter Error) Start(
        CancellationToken stopping,
        params string[] arguments)
    {
        var output = new RecordingWriter();
        var error = new RecordingWriter();
        var exit = Task.Run(() => ToolProgram.RunAsync(arguments, output, error, stopping), CancellationToken.None);

        return (exit, output, error);
    }
}

/// <summary>A <see cref="TextWriter"/> a test can read while the tool is still writing to it.</summary>
/// <remarks>
/// <c>StringWriter</c> would do for a command that has finished, and not for <c>serve</c>: the banner is
/// written from the thread the command is running on and read from the thread the test is on, so both sides
/// take the same lock.
/// </remarks>
internal sealed class RecordingWriter : TextWriter
{
    private readonly StringBuilder _text = new();
    private readonly object _gate = new();

    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>Everything written so far.</summary>
    internal string Text
    {
        get
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        lock (_gate)
        {
            _text.Append(value);
        }
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        lock (_gate)
        {
            _text.Append(value);
        }
    }

    /// <summary>Waits until <paramref name="needle"/> has been written, or gives up.</summary>
    internal async Task<bool> WaitForAsync(string needle, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (Text.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        return Text.Contains(needle, StringComparison.Ordinal);
    }
}
