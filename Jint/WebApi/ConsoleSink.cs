#if NET8_0_OR_GREATER
using Jint.Runtime;

namespace Jint.WebApi;

/// <summary>
/// Where the engine's <c>console</c> object sends what a script logged. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// The engine does all of the work the <see href="https://console.spec.whatwg.org/">Console Standard</see>
/// calls <i>Formatter</i> and <i>Printer</i> — format specifiers, group indentation, counter and timer
/// labels — and hands the sink one finished line per emitted record. A sink therefore never has to parse
/// anything; it decides only where the string goes and, from the level it is handed, how loud it is.
/// </para>
/// <para>
/// <b>Thread safety.</b> An engine only ever calls its sink from the thread running that engine. A sink
/// installed on an <see cref="Options"/> instance shared by engines that run concurrently is called from
/// each of their threads, so such a sink must be thread-safe; <see cref="Null"/> is,
/// <see cref="FromTextWriter"/>'s is only as far as the writer it wraps is (use
/// <see cref="System.IO.TextWriter.Synchronized"/> when it is not).
/// </para>
/// <para>
/// <b>A sink may take the record instead of the line.</b> <see cref="Write(in ConsoleRecord)"/> carries the
/// method that was called, its raw arguments as <see cref="Native.JsValue"/>s, the group depth and the
/// captured frames — everything a structured log or a debugger protocol needs and a finished string has
/// already thrown away. It is what the engine calls, for every invocation including the ones that print
/// nothing, and it forwards to the string overload by default. Frames reach a method other than
/// <c>console.trace</c> only when <see cref="WantsStackTrace"/> asks for them.
/// </para>
/// </remarks>
public abstract class ConsoleSink
{
    /// <summary>
    /// A sink that discards everything. This is the default, so enabling
    /// <see cref="WebApiFeatures.Console"/> never starts writing to the host's standard output by surprise.
    /// </summary>
    public static ConsoleSink Null { get; } = new NullConsoleSink();

    /// <summary>
    /// A sink that writes each record to <paramref name="writer"/> as one line, ignoring the level.
    /// </summary>
    /// <param name="writer">The destination, e.g. <c>Console.Out</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public static ConsoleSink FromTextWriter(TextWriter writer)
    {
        if (writer is null)
        {
            Throw.ArgumentNullException(nameof(writer));
        }

        return new TextWriterConsoleSink(writer);
    }

    /// <summary>
    /// Receives one finished console record.
    /// </summary>
    /// <param name="level">The severity the console method implies.</param>
    /// <param name="message">
    /// The whole record as a single string, already formatted and already carrying any
    /// <c>console.group</c> indentation. It may contain line breaks and may be empty; it is never
    /// <see langword="null"/>.
    /// </param>
    public abstract void Write(ConsoleLogLevel level, string message);

    /// <summary>
    /// Receives the same record as its structured form, with the method, the raw arguments and the group
    /// depth the string overload cannot carry.
    /// </summary>
    /// <param name="record">
    /// The call as the engine saw it. Its <see cref="ConsoleRecord.Arguments"/> are only valid for the
    /// duration of this call; see the remarks on <see cref="ConsoleRecord"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>This is the overload the engine calls</b>, once for every <c>console</c> call the Console Standard
    /// does not abandon at its first step — the ones that print nothing included, such as <c>groupEnd</c>.
    /// The default implementation forwards to <see cref="Write(ConsoleLogLevel, string)"/> only when there
    /// was a line to print, which is what keeps a sink that overrides the string overload alone seeing
    /// exactly the traffic it always did.
    /// </para>
    /// <para>
    /// A sink that overrides this one receives everything: call <c>base.Write(in record)</c> to keep the
    /// forwarding, or handle the record outright and leave the string overload unreachable.
    /// </para>
    /// </remarks>
    public virtual void Write(in ConsoleRecord record)
    {
        if (record.Message is { } message)
        {
            Write(record.Level, message);
        }
    }

    /// <summary>
    /// Whether every record should carry <see cref="ConsoleRecord.StackTrace"/>, not just <c>console.trace</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A call site is what a debugger protocol anchors a console message to, and the frames are only readable
    /// while the call is still on the stack — so the engine has to capture them before the sink is reached,
    /// or not at all. Walking the call stack for every <c>console.log</c> is not free, so it is off unless a
    /// sink says it reads them.
    /// </para>
    /// <para>
    /// The value is read once per record, so a sink may change its mind between calls. A sink that answers
    /// <see langword="true"/> and never reads <see cref="ConsoleRecord.StackTrace"/> is paying for nothing.
    /// </para>
    /// </remarks>
    public virtual bool WantsStackTrace => false;

    private sealed class NullConsoleSink : ConsoleSink
    {
        public override void Write(ConsoleLogLevel level, string message)
        {
        }
    }

    private sealed class TextWriterConsoleSink : ConsoleSink
    {
        private readonly TextWriter _writer;

        internal TextWriterConsoleSink(TextWriter writer)
        {
            _writer = writer;
        }

        public override void Write(ConsoleLogLevel level, string message)
        {
            _writer.WriteLine(message);
        }
    }
}

/// <summary>
/// The severity a console method implies, mirroring the Console Standard's log levels. Requires .NET 8 or
/// higher.
/// </summary>
public enum ConsoleLogLevel
{
    /// <summary><c>console.debug</c>.</summary>
    Debug,

    /// <summary><c>console.log</c>, and the methods that report state rather than severity — <c>dir</c>,
    /// <c>group</c>, <c>groupCollapsed</c>, <c>count</c>, <c>time*</c>.</summary>
    Log,

    /// <summary><c>console.info</c>.</summary>
    Info,

    /// <summary><c>console.warn</c>, and the Console Standard's warnings about a missing counter or timer.</summary>
    Warn,

    /// <summary><c>console.error</c>, <c>console.assert</c> on a failed assertion, and <c>console.trace</c>.</summary>
    Error,
}
#endif
