#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Locks;

/// <summary>
/// A <c>Lock</c> object — "a new Lock object associated with lock", handed to the callback when a request is
/// granted.
/// <para>
/// https://w3c.github.io/web-locks/#lock-class
/// </para>
/// </summary>
/// <remarks>
/// It carries the two things the interface exposes and nothing else: the name and the mode. Both are
/// <see cref="LockPrototype"/>'s attributes rather than own properties of this object, which is where
/// WebIDL puts them, so <c>Reflect.ownKeys(lock)</c> is the empty array — and what this object <i>is</i> is
/// the brand those attributes check their receiver against.
/// <para>
/// The object deliberately holds no handle on the lock's lifetime. The specification's <c>Lock</c> has an
/// associated lock and two promises, but nothing script can reach through it releases anything: the
/// callback's return value is the only thing that does, which is what makes a lock impossible to leak past
/// the call that granted it.
/// </para>
/// </remarks>
internal sealed class JsLock : ObjectInstance
{
    private JsLock(Engine engine, string name, LockMode mode) : base(engine, ObjectClass.Object)
    {
        Name = JsString.Create(name);
        Mode = LockModeNames.ToJsString(mode);
    }

    /// <summary>https://w3c.github.io/web-locks/#dom-lock-name.</summary>
    internal JsString Name { get; }

    /// <summary>https://w3c.github.io/web-locks/#dom-lock-mode.</summary>
    internal JsString Mode { get; }

    internal static JsLock Create(Engine engine, Realm realm, string name, LockMode mode)
        => new(engine, name, mode)
        {
            _prototype = realm.Intrinsics.Lock.PrototypeObject,
        };
}
#endif
