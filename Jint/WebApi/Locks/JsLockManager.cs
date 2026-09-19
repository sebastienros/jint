#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Locks;

/// <summary>
/// The <c>navigator.locks</c> object — the realm's one instance of the <c>LockManager</c> interface, and the
/// handle on the lock state this engine's agent cluster shares.
/// <para>
/// https://w3c.github.io/web-locks/#lockmanager-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The split against <see cref="LockManagerPrototype"/> is the one WebIDL draws: the <i>members</i> are the
/// interface's and live on the prototype, while the manager and this engine's agent are state of this
/// object. So it carries no own properties at all — <c>Reflect.ownKeys(navigator.locks)</c> is the empty
/// array — and what it <i>is</i> is the brand both operations check their receiver against.
/// </para>
/// <para>
/// Both fields belong to the <i>engine</i> rather than to this object, which is why they are read from
/// <c>Engine._webApi</c> once, here: the manager is the host's, or the private one this engine defaulted,
/// and the agent carries the <c>clientId</c> every <c>query()</c> reports these locks under.
/// </para>
/// </remarks>
internal sealed class JsLockManager : ObjectInstance
{
    private JsLockManager(Engine engine, LockManager manager, LockAgent agent) : base(engine, ObjectClass.Object)
    {
        Manager = manager;
        Agent = agent;
    }

    /// <summary>The lock state this engine shares, https://w3c.github.io/web-locks/#lock-managers.</summary>
    internal LockManager Manager { get; }

    /// <summary>This engine's standing in it: the client id, and the route a grant comes back through.</summary>
    internal LockAgent Agent { get; }

    internal static JsLockManager Create(Engine engine, Realm realm)
    {
        var state = engine._webApi;
        if (state is null)
        {
            // Unreachable: the accessor that reaches this property is installed only where the feature is
            // on, and that is one of the features WebApiRegistration.NeedsEngineState names.
            Throw.InvalidOperationException("The lock manager was reached on an engine that has no web-API state.");
            return null!;
        }

        return new JsLockManager(engine, state.Locks, state.LockAgent)
        {
            _prototype = realm.Intrinsics.LockManager.PrototypeObject,
        };
    }
}
#endif
