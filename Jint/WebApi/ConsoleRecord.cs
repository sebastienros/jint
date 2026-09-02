#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.WebApi;

/// <summary>
/// Which <c>console</c> method produced a record, named as the Console Standard names it. Requires .NET 8
/// or higher.
/// </summary>
/// <remarks>
/// A member exists only once the engine implements the method behind it, so naming one can never compile
/// against a <c>console</c> that silently lacks it; the enum may therefore gain members, and a sink should
/// treat one it does not recognize as an ordinary record rather than as an error. <c>clear</c>,
/// <c>dirxml</c>, <c>profile</c> and <c>profileEnd</c> are absent because the engine implements none of
/// them.
/// </remarks>
public enum ConsoleMethod
{
    /// <summary><c>console.log</c>.</summary>
    Log,

    /// <summary><c>console.debug</c>.</summary>
    Debug,

    /// <summary><c>console.info</c>.</summary>
    Info,

    /// <summary><c>console.warn</c>.</summary>
    Warn,

    /// <summary><c>console.error</c>.</summary>
    Error,

    /// <summary><c>console.trace</c>.</summary>
    Trace,

    /// <summary><c>console.assert</c>, reported only when the assertion failed.</summary>
    Assert,

    /// <summary><c>console.dir</c>.</summary>
    Dir,

    /// <summary><c>console.table</c>.</summary>
    Table,

    /// <summary><c>console.group</c>.</summary>
    Group,

    /// <summary><c>console.groupCollapsed</c>.</summary>
    GroupCollapsed,

    /// <summary><c>console.groupEnd</c>, which prints nothing.</summary>
    GroupEnd,

    /// <summary><c>console.count</c>.</summary>
    Count,

    /// <summary><c>console.countReset</c>, which prints only when the counter did not exist.</summary>
    CountReset,

    /// <summary><c>console.time</c>, which prints only when the timer already existed.</summary>
    Time,

    /// <summary><c>console.timeLog</c>.</summary>
    TimeLog,

    /// <summary><c>console.timeEnd</c>.</summary>
    TimeEnd,

    /// <summary><c>console.timeStamp</c>.</summary>
    TimeStamp,
}

/// <summary>
/// One <c>console</c> call as the engine saw it: which method, its raw arguments, and the line the printer
/// made of them. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// <b>The values in <see cref="Arguments"/> are valid for the duration of the
/// <see cref="ConsoleSink.Write(in ConsoleRecord)"/> call and no longer.</b> They belong to the engine that
/// reported them: read them inside the call, and note that converting one to text can itself run script,
/// since <c>ToString()</c> on a <see cref="JsValue"/> calls the object's own <c>toString</c>. Use
/// <see cref="Diagnostics.ValueInspector.Describe"/> for a snapshot that runs nothing and outlives the call.
/// </para>
/// <para>
/// <see cref="Message"/> is <see langword="null"/> exactly when the method printed nothing —
/// <c>groupEnd</c>, a <c>time</c> that started a timer, a <c>countReset</c> that reset one — and is
/// otherwise the very string <see cref="ConsoleSink.Write(ConsoleLogLevel, string)"/> is handed, group
/// indentation included. No record at all is produced for the two calls the Console Standard abandons at
/// its first step: a logging method with no arguments, and <c>assert</c> with a truthy condition.
/// </para>
/// <para>
/// The struct may gain members, which is why its constructor is internal: only the engine creates one.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly struct ConsoleRecord
{
    internal ConsoleRecord(
        ConsoleMethod method,
        ConsoleLogLevel level,
        IReadOnlyList<JsValue> arguments,
        string? message,
        int groupDepth,
        IReadOnlyList<ConsoleStackFrame>? stackTrace = null)
    {
        Method = method;
        Level = level;
        Arguments = arguments;
        Message = message;
        GroupDepth = groupDepth;
        StackTrace = stackTrace;
    }

    /// <summary>Gets which console method was called.</summary>
    public ConsoleMethod Method { get; }

    /// <summary>Gets the severity that method implies.</summary>
    public ConsoleLogLevel Level { get; }

    /// <summary>
    /// Gets the arguments the caller passed, with the ones the Console Standard consumes as control removed.
    /// </summary>
    /// <remarks>
    /// That means <c>assert</c>'s condition, and nothing else: <c>count</c> and the timer methods carry
    /// their label, <c>table</c> its data and columns. An argument the caller omitted is absent rather than
    /// <c>undefined</c>, so a bare <c>console.count()</c> reports an empty list. Never
    /// <see langword="null"/>.
    /// </remarks>
    public IReadOnlyList<JsValue> Arguments { get; }

    /// <summary>
    /// Gets the finished line the printer produced, or <see langword="null"/> when the method printed
    /// nothing.
    /// </summary>
    public string? Message { get; }

    /// <summary>Gets how many <c>console.group</c>s were open when this record was printed.</summary>
    /// <remarks>
    /// It is the indentation <see cref="Message"/> already carries, so <c>group</c> reports the depth it
    /// opened at and <c>groupEnd</c> the depth it closed to.
    /// </remarks>
    public int GroupDepth { get; }

    /// <summary>
    /// Gets the call stack the console method was called from, innermost frame first, or
    /// <see langword="null"/> when none was captured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>console.trace</c> record always carries them, and they are the frames rendered into
    /// <see cref="Message"/> — read from the same capture, so the two describe one call stack. Every other
    /// method carries them only when the sink asked, through
    /// <see cref="ConsoleSink.WantsStackTrace"/>: walking the stack for each call costs something, and a
    /// host that never reads the frames should not pay it.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ConsoleStackFrame>? StackTrace { get; }
}

/// <summary>
/// One frame of a console record's call stack. Requires .NET 8 or higher.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ConsoleStackFrame
{
    internal ConsoleStackFrame(string functionName, string source, int line, int column)
    {
        FunctionName = functionName;
        Source = source;
        Line = line;
        Column = column;
    }

    /// <summary>Gets what the frame is called, or the empty string for the top-level program.</summary>
    public string FunctionName { get; }

    /// <summary>Gets the source the frame's location belongs to, or the empty string when there is none.</summary>
    public string Source { get; }

    /// <summary>Gets the one-based line number.</summary>
    public int Line { get; }

    /// <summary>Gets the one-based column number.</summary>
    public int Column { get; }
}
#endif
