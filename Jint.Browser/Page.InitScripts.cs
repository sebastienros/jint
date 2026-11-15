using Jint.Browser.Runtime;
using Jint.Browser.Runtime.Parsing;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>The scripts run before each new document's own: this page's init scripts and a protocol client's.</summary>
    internal NewDocumentScripts NewDocumentScripts { get; } = new();

    /// <summary>Adds a script that runs in every new document of this page, before any of the document's own scripts.</summary>
    /// <param name="script">The script source.</param>
    /// <returns>A task that completes once the script is installed.</returns>
    /// <remarks>
    /// <para>
    /// It runs in the main frame of the next document onwards — a navigation, a reload, a history traversal
    /// or <see cref="SetContentAsync"/> — never in the document already showing. The document exists and
    /// <c>document.readyState</c> is <c>loading</c>, but none of its tree has been parsed.
    /// </para>
    /// <para>
    /// Scripts run in the order they were added, after the <see cref="BrowserContext.AddInitScriptAsync(string)"/>
    /// scripts of this page's context. One that throws is recorded in <see cref="Errors"/> and does not stop
    /// the document loading. Child frames do not run init scripts.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task AddInitScriptAsync(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        ObjectDisposedException.ThrowIf(_closed, this);
        NewDocumentScripts.Add(this, "Page.AddInitScriptAsync", script);
        return Task.CompletedTask;
    }

    /// <summary>Adds a function invoked with one JSON-data argument in every new document of this page, before the document's own scripts.</summary>
    /// <param name="function">A JavaScript function expression.</param>
    /// <param name="argument">The argument, snapshotted as JSON data now and bound in each document.</param>
    /// <returns>A task that completes once the script is installed.</returns>
    /// <remarks>
    /// The argument is bound as <see cref="EvaluateAsync(string, object?)"/> binds one, never interpolated
    /// into source; a later change to the CLR object is not seen. Otherwise this runs where and when
    /// <see cref="AddInitScriptAsync(string)"/> does.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task AddInitScriptAsync(string function, object? argument)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(_closed, this);
        NewDocumentScripts.Add(this, "Page.AddInitScriptAsync", function, JsonSerializer.SerializeToElement(argument));
        return Task.CompletedTask;
    }

    /// <summary>Runs the context's init scripts, then this page's, into the empty document the driver is about to parse.</summary>
    private void RunNewDocumentScripts(ParserDriver driver)
    {
        Context.NewDocumentScripts.Run(driver);
        NewDocumentScripts.Run(driver);
    }
}
