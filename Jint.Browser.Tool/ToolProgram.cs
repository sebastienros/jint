using System.Reflection;

namespace Jint.Browser.Tool;

/// <summary>What the process exits with, and what each value tells a script driving it.</summary>
/// <remarks>
/// They are separated so that a caller can tell its own mistake from the site's from the page's: a script
/// that retried a <see cref="Navigation"/> would be right to, and one that retried a <see cref="Usage"/>
/// would loop for ever.
/// </remarks>
internal static class ExitCode
{
    /// <summary>The page loaded and the command answered.</summary>
    internal const int Ok = 0;

    /// <summary>The command line was wrong, or named something that does not exist.</summary>
    internal const int Usage = 1;

    /// <summary>There was no document to show: a refused URL, a transport failure, or a timeout.</summary>
    internal const int Navigation = 2;

    /// <summary>The page loaded, and something it ran exceeded its time or allocation budget.</summary>
    internal const int Budget = 3;

    /// <summary>The expression <c>eval</c> was given threw.</summary>
    internal const int Evaluation = 4;
}

/// <summary>
/// The whole of <c>jint-browser</c>, as a function of its arguments and two writers.
/// </summary>
/// <remarks>
/// <b>Everything is here rather than in <c>Program</c>, and that is what makes the tool testable.</b> A test
/// calls <see cref="RunAsync"/> with the arguments a user would type and reads the exit code, the standard
/// output and the standard error together, in process — so the suite exercises the same code path a user
/// does without spawning anything, and a failure names a line rather than a process.
/// </remarks>
internal static class ToolProgram
{
    /// <summary>Runs one command.</summary>
    /// <param name="arguments">The command line, without the program name.</param>
    /// <param name="output">Where a document, a result or a banner is written.</param>
    /// <param name="error">Where a usage message and everything the page got wrong is written.</param>
    /// <param name="stopping">Cancelled to stop a <c>serve</c>; Ctrl+C in a real run.</param>
    /// <returns>The process exit code, which is one of <see cref="ExitCode"/>'s.</returns>
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Count == 0 || arguments[0] is "-h" or "--help" or "help")
        {
            PrintHelp(output);
            return arguments.Count == 0 ? ExitCode.Usage : ExitCode.Ok;
        }

        var command = arguments[0];
        var rest = arguments.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "serve" => await RunAsync(ServeCommand.Syntax(), rest, line => ServeCommand.RunAsync(line, output, error, stopping)).ConfigureAwait(false),
                "fetch" => await RunAsync(FetchCommand.Syntax(), rest, line => FetchCommand.RunAsync(line, output, error)).ConfigureAwait(false),
                "eval" => await RunAsync(EvalCommand.Syntax(), rest, line => EvalCommand.RunAsync(line, output, error)).ConfigureAwait(false),
                "version" or "--version" => PrintVersion(output),
                _ => throw new ToolUsageException($"'{command}' is not a command; they are serve, fetch, eval and version"),
            };
        }
        catch (ToolUsageException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine("Run 'jint-browser --help' for the commands and their options.");
            return ExitCode.Usage;
        }
        catch (NavigationFailedException exception)
        {
            error.WriteLine(exception.Message);
            return ExitCode.Navigation;
        }
        catch (TimeoutException exception)
        {
            // A Page call that ran out of MaxTaskDuration faults with this, which is a budget failure the
            // caller was handed rather than one the page absorbed into its error list.
            error.WriteLine(exception.Message);
            return ExitCode.Budget;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return ExitCode.Ok;
        }
    }

    private static async Task<int> RunAsync(
        Dictionary<string, OptionKind> syntax,
        IReadOnlyList<string> arguments,
        Func<CommandLine, Task<int>> run)
    {
        if (!CommandLine.TryParse(arguments, syntax, out var line, out var error))
        {
            throw new ToolUsageException(error);
        }

        return await run(line).ConfigureAwait(false);
    }

    private static int PrintVersion(TextWriter output)
    {
        output.WriteLine(Version);
        return ExitCode.Ok;
    }

    /// <summary>The informational version of the package this assembly was built from.</summary>
    private static string Version
        => typeof(ToolProgram).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ToolProgram).Assembly.GetName().Version?.ToString()
            ?? "unknown";

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine($"jint-browser {Version} - a headless browser on Jint and AngleSharp");
        output.WriteLine();
        output.WriteLine("Usage: jint-browser <command> [options]");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  serve                     Publish a browser on the Chrome DevTools Protocol");
        output.WriteLine("  fetch <url|file>          Load one page and write it to standard output");
        output.WriteLine("  eval <url|file> <expr>    Load one page, evaluate an expression, write JSON");
        output.WriteLine("  version                   Print the version and exit");
        output.WriteLine();
        output.WriteLine("serve:");
        output.WriteLine("  --port <n>                Port to listen on; 9222, or 0 for one the banner names");
        output.WriteLine("  --host <address>          Address to bind; 127.0.0.1");
        output.WriteLine();
        output.WriteLine("fetch:");
        output.WriteLine("  --dump html|text|markdown|ax   What to write; markdown");
        output.WriteLine("  --main-content            Only the first <main>, [role=main] or <article>");
        output.WriteLine("  --max-length <n>          Cut the answer at n characters");
        output.WriteLine("                            Both are refused with --dump html, which is the whole document");
        output.WriteLine();
        output.WriteLine("fetch and eval:");
        output.WriteLine("  --wait-until commit|domcontentloaded|load|networkidle   How far to wait; load");
        output.WriteLine("  --timeout <duration>      Ceiling on the load; 30s");
        output.WriteLine("  --header 'Name: value'    A header every request carries; repeatable");
        output.WriteLine("  --cookie name=value       A cookie seeded before the load; repeatable");
        output.WriteLine();
        output.WriteLine("Every command:");
        output.WriteLine("  --untrusted               Harden the pages for content nobody vouches for");
        output.WriteLine("  --user-agent <string>     What a page reports itself as, in script and on the wire");
        output.WriteLine("  --max-task-duration <d>   Ceiling on one turn of a page; 5s");
        output.WriteLine("  --memory-limit <size>     Allocation budget for one turn of a page");
        output.WriteLine("  --block-private-network   Refuse loopback and private addresses");
        output.WriteLine("  --allow-private-network   Allow them, even under --untrusted");
        output.WriteLine();
        output.WriteLine("Durations are 30s, 500ms, 5m or a number of seconds; sizes are 256mb, 512kb or bytes.");
        output.WriteLine();
        output.WriteLine("Exit codes: 0 ok, 1 usage, 2 the page did not load, 3 a budget was exceeded,");
        output.WriteLine("            4 the expression threw.");
        output.WriteLine();
        output.WriteLine("Examples:");
        output.WriteLine("  jint-browser serve --port 9222");
        output.WriteLine("  jint-browser fetch https://example.com --dump markdown --main-content");
        output.WriteLine("  jint-browser fetch ./page.html --dump ax");
        output.WriteLine("  jint-browser eval https://example.com 'document.title'");
        output.WriteLine();
        output.WriteLine("This browser renders nothing: there are no screenshots and no PDFs. What it answers");
        output.WriteLine("instead is the page as text, as markdown and as its accessibility tree.");
    }
}
