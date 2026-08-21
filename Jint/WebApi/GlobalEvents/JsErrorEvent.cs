#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Error;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// An <c>ErrorEvent</c> instance: the event HTML's <i>report an exception</i> fires at the global scope.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#the-errorevent-interface
/// </para>
/// </summary>
internal sealed class JsErrorEvent : JsEvent
{
    internal JsErrorEvent(Engine engine, JsString type, EventInit init, double timeStamp, in ErrorEventDetails details)
        : base(engine, type, init, timeStamp)
    {
        Message = details.Message;
        Filename = details.Filename;
        Lineno = details.Lineno;
        Colno = details.Colno;
        Error = details.Error;
    }

    /// <summary>https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-message</summary>
    internal string Message { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-filename</summary>
    internal string Filename { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-lineno</summary>
    internal uint Lineno { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-colno</summary>
    internal uint Colno { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-errorevent-error — the value that was
    /// thrown, or that <c>reportError</c> was handed. <c>any</c>, so it defaults to <c>undefined</c> rather
    /// than to null.
    /// </summary>
    internal JsValue Error { get; }
}

/// <summary>
/// The five members of <c>ErrorEventInit</c> that describe the failure, after conversion —
/// https://html.spec.whatwg.org/multipage/webappapis.html#erroreventinit. Also what the engine fills in for
/// the trusted <c>error</c> event it fires itself.
/// </summary>
/// <param name="Message">A description of the failure; see <see cref="FromException"/> for where it comes from.</param>
/// <param name="Filename">The script the failure came from, or the empty string when nothing named one.</param>
/// <param name="Lineno">The 1-based line, or 0 when the location is unknown.</param>
/// <param name="Colno">The 1-based column, or 0 when the location is unknown.</param>
/// <param name="Error">The thrown or reported value itself.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ErrorEventDetails(string Message, string Filename, uint Lineno, uint Colno, JsValue Error)
{
    /// <summary>
    /// What HTML's <i>report an exception</i> knows about an exception that escaped a callback the engine
    /// invoked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here runs a line of script.</b> <see cref="Exception.Message"/> on a
    /// <see cref="JavaScriptException"/> is a CLR string materialized when the exception was <i>constructed</i>
    /// — at the throw, long before this — so reading it now cannot reach a <c>message</c> getter or a
    /// <c>toString</c>, which is the hazard <see cref="Throw.SafeToDisplayString"/> exists for. A listener that
    /// wants the error itself has <c>event.error</c>.
    /// </para>
    /// <para>
    /// The message is the error's own <c>message</c> rather than a browser's rendering of it: Chrome reports
    /// "Uncaught TypeError: …" where this reports "…", because prefixing it would mean deciding what the
    /// error's <i>name</i> is, and asking an arbitrary thrown object for its <c>name</c> is exactly the script
    /// call that must not happen here.
    /// </para>
    /// </remarks>
    internal static ErrorEventDetails FromException(JavaScriptException exception)
    {
        var (filename, lineno, colno) = ReadLocation(exception.Location);
        return new ErrorEventDetails(exception.Message ?? string.Empty, filename, lineno, colno, exception.Error);
    }

    /// <summary>
    /// The same for a value handed to <c>reportError</c>, where there is no exception to read a message or a
    /// location from — so the location is the call site the engine last saw, and the message is derived from
    /// the value without running script.
    /// </summary>
    /// <remarks>
    /// An in-box <c>Error</c> keeps its <c>message</c> as an ordinary own data property, so reading the
    /// descriptor is a dictionary lookup and cannot run a getter. Anything else — a plain object, a proxy, a
    /// host object — is rendered by <see cref="Throw.SafeToDisplayString"/>, which reports an object as its
    /// shape rather than its contents for precisely that reason. This is a divergence from a browser, which
    /// stringifies the value; <c>event.error</c> carries the value itself either way.
    /// </remarks>
    internal static ErrorEventDetails FromReportedValue(JsValue value, in SourceLocation location)
    {
        var (filename, lineno, colno) = ReadLocation(in location);
        return new ErrorEventDetails(Describe(value), filename, lineno, colno, value);
    }

    private static string Describe(JsValue value)
    {
        if (value is ErrorInstance error
            && error.GetOwnProperty(CommonProperties.Message) is { } descriptor
            && descriptor.IsDataDescriptor()
            && descriptor.Value is JsString message)
        {
            return message.ToString();
        }

        return Throw.SafeToDisplayString(value);
    }

    /// <summary>
    /// <c>filename</c>, <c>lineno</c> and <c>colno</c> out of one <see cref="SourceLocation"/>.
    /// </summary>
    /// <remarks>
    /// A default <see cref="SourceLocation"/> — what an exception the engine could not place carries — reports
    /// line 0, and 0 is exactly the "unknown" a browser reports for all three. So the line decides: without
    /// one there is no column either, which is why the 1-based conversion is not applied to a column that
    /// names nothing. Acornima counts columns from zero and HTML's <c>colno</c> from one, the same conversion
    /// the call-stack renderer makes.
    /// </remarks>
    private static (string Filename, uint Lineno, uint Colno) ReadLocation(in SourceLocation location)
    {
        var line = location.Start.Line;
        if (line <= 0)
        {
            return (location.SourceFile ?? string.Empty, 0u, 0u);
        }

        var column = location.Start.Column;
        return (location.SourceFile ?? string.Empty, (uint) line, column >= 0 ? (uint) column + 1u : 0u);
    }
}
#endif
