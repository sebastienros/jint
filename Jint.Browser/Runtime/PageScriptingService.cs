using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using AngleSharp.Scripting;
using Jint.Runtime;

namespace Jint.Browser.Runtime;

/// <summary>
/// The <c>IScriptingService</c> AngleSharp's parser calls when it reaches a <c>&lt;/script&gt;</c>: it runs a
/// classic inline script on the page loop, synchronously, in document order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only a classic inline script runs in this version.</b> An external <c>src</c> needs the network, a
/// module needs the module loader against that network, and both belong to the parser driver. Everything the
/// parse skipped is recorded on the page and named, so a host reading
/// <see cref="Page.UnsupportedScripts"/> is told rather than left to guess why a page did nothing.
/// </para>
/// <para>
/// <b>The thread the parser calls this on is checked, not assumed.</b> Every await inside AngleSharp's parse
/// carries <c>ConfigureAwait(false)</c>, so a genuinely asynchronous step anywhere in it would resume the
/// parse — and this call — on a pool thread while the page loop sat blocked, and the engine would be entered
/// from two threads with nothing to say so. Nothing in this configuration is asynchronous, and
/// <see cref="Hopped"/> is what turns that from a belief into a check the loader asserts.
/// </para>
/// </remarks>
internal sealed class PageScriptingService : IScriptingService
{
    private readonly PageRuntime _runtime;
    private readonly string _source;
    private readonly string _url;
    private readonly int _loopThreadId;
    private volatile bool _hopped;

    internal PageScriptingService(PageRuntime runtime, string source, string url, int loopThreadId)
    {
        _runtime = runtime;

        // Normalized the way AngleSharp's own text source normalizes it, so that an index into the parser's
        // stream is an index into this string too and the line count below is the document's.
        _source = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        _url = url;
        _loopThreadId = loopThreadId;
    }

    /// <summary>
    /// Whether the parser ever called this on a thread that was not the page loop's — sticky, and written
    /// from whichever thread it happened on.
    /// </summary>
    internal bool Hopped => _hopped;

    /// <summary>How many inline scripts ran.</summary>
    internal int ScriptsRun { get; private set; }

    /// <summary>
    /// Whether the type names a classic script; anything else — <c>module</c>, <c>importmap</c>, a data block —
    /// answers no, which is what stops AngleSharp preparing it at all.
    /// </summary>
    public bool SupportsType(string mimeType)
        => mimeType is null
        || mimeType.Length == 0
        || MimeTypeNames.IsJavaScript(mimeType);

    /// <inheritdoc />
    public Task EvaluateScriptAsync(IResponse response, ScriptOptions options, CancellationToken cancel)
    {
        if (Environment.CurrentManagedThreadId != _loopThreadId)
        {
            _hopped = true;
        }

        // The document exists from the first token, but this is the earliest AngleSharp hands it over, and an
        // inline script needs `document` to answer before the parse it is running inside has finished.
        _runtime.Document ??= options.Document;

        var element = options.Element;
        if (element is null || !string.IsNullOrEmpty(element.Source))
        {
            return Task.CompletedTask;
        }

        Run(element);
        return Task.CompletedTask;
    }

    private void Run(IHtmlScriptElement element)
    {
        var text = element.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var previous = _runtime.CurrentScript;
        _runtime.CurrentScript = element;
        ScriptsRun++;

        try
        {
            _runtime.Engine.Execute(text, _url + ":" + LineOf(element, text));
        }
        catch (JavaScriptException exception)
        {
            // HTML's "report the exception" step: the script ends, the parse goes on, and the page is told.
            _runtime.Recorder.Add(new PageError(
                PageErrorKind.ScriptError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                _url));
        }
        finally
        {
            _runtime.CurrentScript = previous;
        }
    }

    /// <summary>
    /// The one-based line the script's first character is on.
    /// </summary>
    /// <remarks>
    /// AngleSharp records a source position only when the parser is asked to keep source references, which
    /// the default parser is not; what it does expose is the parser's index into the document source, and
    /// this call happens with that index just past the closing tag. Counting back over the script's own
    /// newlines from there is exact for a document the parse has not rewritten, which is every document
    /// until <c>document.write</c> is supported.
    /// </remarks>
    private int LineOf(IHtmlScriptElement element, string text)
    {
        var index = element.Owner?.Source?.Index ?? 0;
        if (index <= 0 || index > _source.Length)
        {
            return 1;
        }

        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (_source[i] == '\n')
            {
                line++;
            }
        }

        foreach (var c in text)
        {
            if (c == '\n')
            {
                line--;
            }
        }

        return line < 1 ? 1 : line;
    }
}
