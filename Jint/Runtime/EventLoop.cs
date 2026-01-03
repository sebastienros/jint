using System.Collections.Concurrent;

namespace Jint.Runtime;

internal sealed record EventLoop
{
    internal readonly ConcurrentQueue<Action> Events = new();

    /// <summary>
    /// Tracks whether we are currently processing the event loop.
    /// Used to prevent re-entrant calls from causing stack overflow.
    /// </summary>
    internal bool IsProcessing;
}