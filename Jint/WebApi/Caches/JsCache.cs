#if NET8_0_OR_GREATER
using Jint.Native.Object;

namespace Jint.WebApi.Caches;

/// <summary>
/// A <c>Cache</c> instance.
/// <para>
/// https://w3c.github.io/ServiceWorker/#cache-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// "A <c>Cache</c> object represents a request response list. Multiple separate objects implementing the
/// <c>Cache</c> interface … can all be associated with the same request response list simultaneously" — which
/// is exactly the shape here: the state is the <see cref="Store"/>, which belongs to the host's
/// <see cref="CacheStorageProvider"/>, and every <c>caches.open(name)</c> hands out a new JavaScript object in
/// front of the same one.
/// </para>
/// <para>
/// The instance carries no own property; <see cref="CachePrototype"/> reaches the store through a brand
/// check.
/// </para>
/// </remarks>
internal sealed class JsCache : ObjectInstance
{
    internal JsCache(Engine engine, string name, CacheStore store) : base(engine, ObjectClass.Object)
    {
        Name = name;
        Store = store;
    }

    /// <summary>
    /// The name this cache was opened under. Kept for diagnostics only — the standard gives a <c>Cache</c>
    /// object no way to report it, and a cache deleted by name goes on working through an object a script
    /// still holds.
    /// </summary>
    internal string Name { get; }

    /// <summary>The host storage behind this object.</summary>
    internal CacheStore Store { get; }
}
#endif
