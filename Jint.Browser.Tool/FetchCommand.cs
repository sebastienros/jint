namespace Jint.Browser.Tool;

/// <summary>Which representation of a loaded document <c>fetch</c> writes.</summary>
internal enum DumpFormat
{
    /// <summary>The document's serialized markup, doctype included.</summary>
    Html,

    /// <summary>The document's rendered text.</summary>
    Text,

    /// <summary>The document as CommonMark.</summary>
    Markdown,

    /// <summary>The document's accessibility tree, as an indented snapshot.</summary>
    Ax,
}

/// <summary>
/// <c>jint-browser fetch</c>: loads one page and writes it to standard output.
/// </summary>
/// <remarks>
/// The document goes to standard output and everything the page got wrong goes to standard error, so the
/// output is pipeable whatever the page did. The exit code says which kind of thing went wrong — see
/// <see cref="ExitCode"/>, whose four values are what a script driving this needs to tell "I asked for the
/// wrong thing" from "the site is down" from "the page ran away with itself".
/// </remarks>
internal static class FetchCommand
{
    /// <summary>Every option <c>fetch</c> accepts.</summary>
    internal static Dictionary<string, OptionKind> Syntax()
    {
        var syntax = new Dictionary<string, OptionKind>(StringComparer.Ordinal)
        {
            ["dump"] = OptionKind.Value,
            ["main-content"] = OptionKind.Flag,
            ["max-length"] = OptionKind.Value,
        };

        BrowserSettings.Declare(syntax);
        LoadSettings.Declare(syntax);
        return syntax;
    }

    /// <summary>Loads the page the command line named and writes the representation it asked for.</summary>
    internal static async Task<int> RunAsync(CommandLine line, TextWriter output, TextWriter error)
    {
        if (line.Positional.Count != 1)
        {
            throw new ToolUsageException("usage: jint-browser fetch <url|file> [options]");
        }

        var format = line.Value("dump") is { } dump
            ? ValueSyntax.Word(
                "dump",
                dump,
                ("html", DumpFormat.Html),
                ("text", DumpFormat.Text),
                ("markdown", DumpFormat.Markdown),
                ("ax", DumpFormat.Ax))
            : DumpFormat.Markdown;

        var mainContentOnly = line.Flag("main-content");
        var maxLength = line.Value("max-length") is { } length ? ValueSyntax.Integer("max-length", length, minimum: 0) : 0;

        // Refused rather than ignored. A narrowed or truncated document is not a document — the serialization
        // would be missing its own closing tags — and an option a command silently did nothing with is the
        // worst answer of the three.
        if (format == DumpFormat.Html && (mainContentOnly || maxLength > 0))
        {
            throw new ToolUsageException("'--dump html' is the whole document; '--main-content' and '--max-length' belong to text, markdown and ax");
        }

        var source = PageSource.Resolve(line.Positional[0]);
        var browser = BrowserSettings.Read(line);
        var load = LoadSettings.Read(line);

        await using var run = await PageRun.OpenAsync(browser, load, source).ConfigureAwait(false);

        var text = format switch
        {
            DumpFormat.Html => await run.Page.ContentAsync().ConfigureAwait(false),
            DumpFormat.Text => await run.Page.TextAsync(mainContentOnly, maxLength).ConfigureAwait(false),
            DumpFormat.Ax => await run.Page.AccessibilitySnapshotAsync(mainContentOnly, maxLength).ConfigureAwait(false),
            _ => await run.Page.MarkdownAsync(mainContentOnly, maxLength).ConfigureAwait(false),
        };

        output.WriteLine(text);

        // After the dump rather than instead of it: a page whose script ran out of budget still parsed, and
        // the half of it that loaded is usually the half the caller wanted.
        return run.ReportErrors(error) ? ExitCode.Budget : ExitCode.Ok;
    }
}
