#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Caches;

/// <summary>
/// The <c>caches</c> object — an instance of the <c>CacheStorage</c> interface.
/// <para>
/// https://w3c.github.io/ServiceWorker/#cachestorage-interface
/// </para>
/// </summary>
/// <remarks>
/// One per realm, holding the host's <see cref="CacheStorageProvider"/> and nothing else: the name-to-cache
/// map the standard talks about is the provider's, so two engines sharing a provider share the map and an
/// engine with the default provider has one of its own.
/// </remarks>
internal sealed class JsCacheStorage : ObjectInstance
{
    private JsCacheStorage(Engine engine, CacheStorageProvider provider) : base(engine, ObjectClass.Object)
    {
        Provider = provider;
    }

    /// <summary>Where the caches live.</summary>
    internal CacheStorageProvider Provider { get; }

    internal static JsCacheStorage Create(Engine engine, Realm realm)
    {
        var provider = engine._webApi?.CacheProvider;
        if (provider is null)
        {
            // Unreachable: the global that reaches this is installed only where the provider was resolved, in
            // the same block of WebApiRegistration.
            Throw.InvalidOperationException("The caches global was reached on an engine that has no cache storage provider.");
        }

        return new JsCacheStorage(engine, provider)
        {
            _prototype = realm.Intrinsics.CacheStorage.PrototypeObject,
        };
    }
}
#endif
