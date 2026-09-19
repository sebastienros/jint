#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;

namespace Jint.WebApi.Locks;

/// <summary>
/// https://w3c.github.io/web-locks/#lock-request — "a struct with items agent, clientId, manager, name,
/// mode, callback, promise, and signal", split so that the manager holds only what it may read from another
/// engine's thread and <see cref="LockOperation"/> holds everything that is a <c>JsValue</c>.
/// </summary>
/// <remarks>
/// The manager is the object this lives in rather than an item on it, and the agent carries the
/// <c>clientId</c>, so what is left here is the scheduling key (name and mode) plus the handle back to the
/// engine that asked.
/// </remarks>
internal sealed class LockRequestEntry
{
    internal LockRequestEntry(LockAgent agent, string name, LockMode mode, LockOperation operation)
    {
        Agent = agent;
        Name = name;
        Mode = mode;
        Operation = operation;
        Generation = agent.Generation;
    }

    /// <summary>The engine that asked, and the only route back into it.</summary>
    internal LockAgent Agent { get; }

    /// <summary>https://w3c.github.io/web-locks/#resource-name — an arbitrary <c>DOMString</c>, compared ordinally.</summary>
    internal string Name { get; }

    internal LockMode Mode { get; }

    /// <summary>The engine-side half: the callback, the released promise and the signal.</summary>
    internal LockOperation Operation { get; }

    /// <summary>The evaluation cycle this request was made in; see <see cref="LockAgent.Generation"/>.</summary>
    internal int Generation { get; }

    internal string ClientId => Agent.ClientId;
}

/// <summary>
/// https://w3c.github.io/web-locks/#lock-concept — a granted lock, as the manager holds it: everything but
/// the two promises, which belong to <see cref="LockOperation"/> and to one engine.
/// </summary>
internal sealed class HeldLockEntry
{
    internal HeldLockEntry(LockRequestEntry request)
    {
        Agent = request.Agent;
        Name = request.Name;
        Mode = request.Mode;
        Operation = request.Operation;
        Generation = request.Generation;
    }

    internal LockAgent Agent { get; }

    internal string Name { get; }

    internal LockMode Mode { get; }

    internal LockOperation Operation { get; }

    internal int Generation { get; }

    internal string ClientId => Agent.ClientId;
}

/// <summary>
/// https://w3c.github.io/web-locks/#dictdef-lockinfo — one row of a snapshot, taken under the manager's
/// gate so that nothing it names can change while the list is being built.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LockInfo(string Name, LockMode Mode, string ClientId);

/// <summary>
/// https://w3c.github.io/web-locks/#dictdef-lockmanagersnapshot — what <c>query()</c> resolves with, as
/// plain data. It is deliberately not a <c>JsValue</c>: the snapshot is taken by the manager, which may be
/// shared, and turned into an object by the engine that asked.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LockStateSnapshot(IReadOnlyList<LockInfo> Held, IReadOnlyList<LockInfo> Pending);
#endif
