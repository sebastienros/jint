using AngleSharp.Io;
using AngleSharp.Scripting;
using Jint.Browser.Runtime.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// The <c>IScriptingService</c> AngleSharp's parser calls when a classic script is ready to run: it hands
/// the work to the page loop through the parser driver's baton and waits there until it is done.
/// </summary>
/// <remarks>
/// <para>
/// <b>What reaches here and what does not.</b> <see cref="SupportsType"/> is the gate, and it admits classic
/// JavaScript only — so an inline script, an external <c>src</c>, a <c>defer</c> and an <c>async</c> one all
/// arrive, in the order and at the moment HTML says they run, while <c>type="module"</c>,
/// <c>type="importmap"</c> and every unknown type never reach AngleSharp's <i>prepare a script element</i> at
/// all and are the driver's own business afterwards. That is not a limitation being worked around: modules
/// are deferred by definition, and AngleSharp has no module graph to run them against.
/// </para>
/// <para>
/// <b>The thread this is called on is the parser's, not the loop's</b>, and the whole class exists to say so
/// once: everything it does goes through <see cref="ParserDriver"/>, which parks this thread while the loop
/// runs the script. Returning a completed task afterwards is what keeps AngleSharp's parse from suspending
/// and resuming somewhere nobody expected.
/// </para>
/// </remarks>
internal sealed class PageScriptingService : IScriptingService
{
    private readonly ParserDriver _driver;

    internal PageScriptingService(ParserDriver driver)
    {
        _driver = driver;
    }

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
        // AngleSharp advances its own readiness before it runs the deferred queue, which is the one moment
        // this driver cannot observe from outside the parse — so it is read here, on the way in.
        _driver.ObserveReadiness(options.Document);
        _driver.RunClassicScript(response, options);
        return Task.CompletedTask;
    }
}
