using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi;
using Jint.WebApi.DomException;

namespace Jint.Browser.Runtime;

/// <summary>
/// Where a page's <c>localStorage</c> and <c>sessionStorage</c> come from, and what a document with no
/// origin gets instead.
/// </summary>
/// <remarks>
/// <para>
/// <b>Storage is partitioned by origin, which is what makes it storage rather than a per-engine scratchpad.</b>
/// <c>localStorage</c> comes from the context's <see cref="StoragePartitionProvider"/>, so two pages of one
/// origin in one context share it and it survives a navigation; <c>sessionStorage</c> comes from a map the
/// page owns, so it survives a navigation within the page and dies with the page — which is the lifetime
/// difference the two names carry and which the engine's own defaults cannot express, because the engine
/// has no notion of an origin or of a page.
/// </para>
/// <para>
/// <b>A document with an opaque origin gets neither, and says so.</b> <c>about:blank</c>, a <c>data:</c> URL
/// and a document built from markup have no origin to partition by — HTML gives each a fresh opaque one,
/// and no two opaque origins are ever equal — so <c>localStorage</c> throws <c>SecurityError</c> exactly as
/// it does in a browser. It is installed as a throwing accessor rather than left absent, because
/// <c>typeof localStorage</c> answering <c>"undefined"</c> would send a feature-detecting page down a path
/// it does not need, while the throw is the answer such a page is already written to catch.
/// </para>
/// </remarks>
internal static class PageStorage
{
    /// <summary>
    /// Points an engine's storage at the page's partition, and answers whether the feature can be granted.
    /// </summary>
    /// <param name="options">The engine options being built.</param>
    /// <param name="network">The context's network position, which owns the partition.</param>
    /// <param name="sessionStores">The page's own session stores, one per origin, created on demand.</param>
    /// <param name="origin">The document's serialized origin, or <c>"null"</c> when it has none.</param>
    /// <returns>
    /// <see langword="true"/> when the engine should carry <see cref="WebApiFeatures.Storage"/>;
    /// <see langword="false"/> when the document has no origin and the throwing accessors are installed
    /// instead.
    /// </returns>
    internal static bool Configure(
        Options options,
        PageNetwork network,
        Dictionary<string, StorageProvider> sessionStores,
        string origin)
    {
        if (string.Equals(origin, PageUrl.OpaqueOrigin, StringComparison.Ordinal))
        {
            return false;
        }

        var local = network.Storage.GetLocalStorage(origin);
        if (local is null)
        {
            // The partition refused this origin, which a browser expresses the same way it expresses no
            // origin at all.
            return false;
        }

        if (!sessionStores.TryGetValue(origin, out var session))
        {
            session = new InMemoryStorageProvider();
            sessionStores[origin] = session;
        }

        options.WebApi.Storage.LocalStorageProvider = local;
        options.WebApi.Storage.SessionStorageProvider = session;
        return true;
    }

    /// <summary>
    /// Installs the two globals a document with no origin has: accessors that throw <c>SecurityError</c>.
    /// </summary>
    /// <remarks>
    /// Installed through the same <c>SetProperty</c> path <c>Engine.AddLazyGlobal</c> uses, which is what
    /// keeps the global-identifier inline cache correct: every storage path it can take bumps the global's
    /// properties version, and a warmed read site revalidates against exactly that.
    /// </remarks>
    internal static void InstallOpaque(Engine engine)
    {
        var global = engine._mainRealm.GlobalObject;

        foreach (var name in (string[]) ["localStorage", "sessionStorage"])
        {
            var member = name;
            global.SetProperty(
                name,
                new GetSetPropertyDescriptor(
                    new ClrFunction(engine, "get " + name, (thisObject, _) => ThrowOpaque(thisObject, member)),
                    set: null,
                    PropertyFlag.Configurable));
        }
    }

    private static JsValue ThrowOpaque(JsValue thisObject, string member)
    {
        var engine = thisObject is ObjectInstance instance ? instance.Engine : null;
        if (engine is null)
        {
            Throw.TypeErrorNoEngine("Failed to read '" + member + "': the document has no origin.");
            return JsValue.Undefined;
        }

        var realm = engine._mainRealm;
        var exception = realm.Intrinsics.DomException.CreateException(
            DomExceptionNames.Security,
            "Failed to read the '" + member + "' property from 'Window': The document is sandboxed and lacks "
            + "the 'allow-same-origin' flag. A document loaded from about:blank, a data: URL or SetContentAsync "
            + "has an opaque origin, and storage is partitioned by origin.");

        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
        return JsValue.Undefined;
    }
}
