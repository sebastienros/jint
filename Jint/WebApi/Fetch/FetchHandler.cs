#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The script function an inbound request is routed to, plus the <c>this</c> it is called with — the resolved
/// form of whatever a host handed <c>Engine.WebApi.SetFetchHandler</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The resolution happens once, when the handler is registered</b>, rather than on every request: a host
/// that passed something unusable learns about it at the call it made, not on the first request in
/// production, and a member read here cannot be observed per request by a script that made <c>fetch</c> a
/// getter.
/// </para>
/// <para>
/// This is a host-facing convention, not a specification: nothing in the Fetch Standard says how a runtime
/// finds a handler. The shape is the one Cloudflare Workers established and Deno adopted —
/// <c>export default { fetch(request) { … } }</c> — because that is what scripts written for those runtimes
/// already look like.
/// </para>
/// </remarks>
internal sealed class FetchHandler
{
    private FetchHandler(JsValue callable, JsValue thisObject)
    {
        Callable = callable;
        ThisObject = thisObject;
    }

    /// <summary>The function itself, already known to be callable.</summary>
    internal JsValue Callable { get; }

    /// <summary>
    /// The receiver the function is called with: the object it was found on for the
    /// <c>{ fetch(request) { … } }</c> shape, so a handler written as a method may use <c>this</c> to reach
    /// its siblings, and <c>undefined</c> for a bare function.
    /// </summary>
    internal JsValue ThisObject { get; }

    /// <summary>
    /// Resolves the value a host registered into the function to call. The three accepted shapes, in the order
    /// they are tried:
    /// <list type="number">
    /// <item>a callable — used as the handler, with <c>this</c> undefined;</item>
    /// <item>an object with a callable <c>fetch</c> property — the Workers convention, called with the object
    /// as <c>this</c>;</item>
    /// <item>an object with a <c>default</c> property matching either of the two above — a module namespace,
    /// so <c>engine.WebApi.SetFetchHandler(engine.Modules.Import("./worker.js"))</c> works directly.</item>
    /// </list>
    /// </summary>
    /// <exception cref="ArgumentException">The value matches none of the three shapes.</exception>
    internal static FetchHandler Resolve(JsValue handler, string paramName)
    {
        var resolved = TryResolve(handler, allowDefault: true);
        if (resolved is null)
        {
            Throw.ArgumentException(
                "The fetch handler must be a function, an object with a callable 'fetch' property (the 'export default { fetch(request) { … } }' convention), or a module namespace whose default export is one of those.",
                paramName);
        }

        return resolved;
    }

    private static FetchHandler? TryResolve(JsValue handler, bool allowDefault)
    {
        if (handler.HasCall)
        {
            return new FetchHandler(handler, JsValue.Undefined);
        }

        if (handler is not ObjectInstance holder)
        {
            return null;
        }

        var member = holder.Get("fetch");
        if (member.HasCall)
        {
            return new FetchHandler(member, holder);
        }

        // One level only. A module namespace's default export may itself be the object carrying fetch, but a
        // default export whose own default export carries it is not a shape anything writes, and chasing it
        // would turn a typo into a silent walk through the script's object graph.
        return allowDefault ? TryResolve(holder.Get("default"), allowDefault: false) : null;
    }
}
#endif
