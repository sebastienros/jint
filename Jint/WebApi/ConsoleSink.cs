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
/// This is an abstract class rather than a delegate so that later revisions can add richer overloads —
/// structured arguments, a timestamp — without breaking hosts that implement it today.
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
