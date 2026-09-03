using Jint.Runtime;

namespace Jint.Browser.Tool;

/// <summary>
/// <c>jint-browser eval</c>: loads one page, runs one expression in it, and writes the result as JSON.
/// </summary>
/// <remarks>
/// <para>
/// <b>The result is serialized by the page, not by this process.</b> <c>JSON.stringify</c> runs in the
/// document's own realm, so what comes out is what a script in the page would have got — a <c>Date</c>
/// through its <c>toJSON</c>, an object through its own, a <c>NaN</c> as <c>null</c> — rather than whatever
/// a CLR conversion of the value happened to produce. It also means nothing that belongs to the engine has
/// to cross out of it; a string does.
/// </para>
/// <para>
/// A value <c>JSON.stringify</c> answers nothing for — <c>undefined</c>, a function, a symbol — is written
/// as <c>null</c>, which is what that function already does for the same value inside an array.
/// </para>
/// </remarks>
internal static class EvalCommand
{
    /// <summary>Every option <c>eval</c> accepts, which is every option a load takes.</summary>
    internal static Dictionary<string, OptionKind> Syntax()
    {
        var syntax = new Dictionary<string, OptionKind>(StringComparer.Ordinal);
        BrowserSettings.Declare(syntax);
        LoadSettings.Declare(syntax);
        return syntax;
    }

    /// <summary>Loads the page, evaluates the expression, and writes its JSON form.</summary>
    internal static async Task<int> RunAsync(CommandLine line, TextWriter output, TextWriter error)
    {
        if (line.Positional.Count != 2)
        {
            throw new ToolUsageException("usage: jint-browser eval <url|file> <expression> [options]");
        }

        var source = PageSource.Resolve(line.Positional[0]);
        var expression = line.Positional[1];
        var browser = BrowserSettings.Read(line);
        var load = LoadSettings.Read(line);

        await using var run = await PageRun.OpenAsync(browser, load, source).ConfigureAwait(false);

        string? json;

        try
        {
            // The expression is parenthesized so that an object literal is one rather than a block, and the
            // whole thing is an arrow body so that a bare `await` is still a syntax error the page reports
            // rather than something this silently allowed.
            json = await run.Page.EvaluateAsync<string>(
                "(() => { const value = (" + expression + "); const json = JSON.stringify(value); return json === undefined ? 'null' : json; })()")
                .ConfigureAwait(false);
        }
        catch (JavaScriptException exception)
        {
            error.WriteLine(exception.Message);
            run.ReportErrors(error);
            return ExitCode.Evaluation;
        }

        output.WriteLine(json ?? "null");

        return run.ReportErrors(error) ? ExitCode.Budget : ExitCode.Ok;
    }
}
