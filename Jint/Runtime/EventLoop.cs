using System.Collections.Concurrent;

namespace Jint.Runtime;

internal sealed record EventLoop
{
    internal readonly ConcurrentQueue<Action> Events = new();
}