using System.Runtime.InteropServices;

namespace Jint.Profiling;

/// <summary>
/// Which end of a function's activation a <see cref="ScriptProfileEvent"/> marks.
/// </summary>
public enum ScriptProfileEventKind
{
    /// <summary>
    /// The function was entered. Corresponds to speedscope's <c>"O"</c> event.
    /// </summary>
    Open,

    /// <summary>
    /// The function was left, by return or by exception. Corresponds to speedscope's <c>"C"</c> event.
    /// </summary>
    Close,
}

/// <summary>
/// One function enter or leave, timestamped against the start of the profiling session.
/// </summary>
/// <param name="Kind">Whether the frame was entered or left.</param>
/// <param name="FrameIndex">Index into <see cref="ScriptProfile.Frames"/> of the function involved.</param>
/// <param name="TimestampNanoseconds">
/// Nanoseconds since the session started, derived from <see cref="System.Diagnostics.Stopwatch"/>
/// timestamps and therefore monotonically non-decreasing across the event list.
/// </param>
/// <remarks>
/// The events of a session are well-formed as a stream: every <see cref="ScriptProfileEventKind.Open"/> is
/// matched by a later <see cref="ScriptProfileEventKind.Close"/> of the same frame, and the matching is
/// properly nested, so the list can be replayed into a call tree by pushing and popping. That holds through
/// exceptions, through <see cref="Engine.AdvancedOperations.ResetCallStack"/>, through truncation and
/// through a session stopped while script is still running.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ScriptProfileEvent(
    ScriptProfileEventKind Kind,
    int FrameIndex,
    long TimestampNanoseconds);
