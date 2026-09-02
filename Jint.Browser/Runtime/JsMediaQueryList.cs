using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.Events;

namespace Jint.Browser.Runtime;

/// <summary>
/// What <c>matchMedia</c> answers: a query, whether it matches the page's viewport, and a place to listen.
/// </summary>
/// <remarks>
/// <para>
/// It is a real <c>EventTarget</c>, so <c>addEventListener('change', …)</c> and <c>onchange</c> both work,
/// and it is an event nothing can fire in this version: the viewport is fixed for a page's lifetime, so a
/// query's answer never changes. Emulation changing the viewport is what will fire it.
/// </para>
/// <para>
/// <c>matches</c> is computed on every read rather than captured, so a page reading it after a viewport
/// change gets the new answer even before the event exists.
/// </para>
/// </remarks>
internal sealed class JsMediaQueryList : JsEventTarget
{
    private readonly PageRuntime _runtime;

    internal JsMediaQueryList(PageRuntime runtime, ObjectInstance prototype, string media)
        : base(runtime.Engine, runtime.Engine._mainRealm)
    {
        _runtime = runtime;
        Media = media;
        Prototype = prototype;
    }

    /// <summary>The serialized query, as the page wrote it.</summary>
    internal string Media { get; }

    /// <summary>Whether the query matches the page's viewport right now.</summary>
    internal bool Matches => MediaQuery.Matches(Media, _runtime.Viewport);

    /// <summary>The event type a <c>MediaQueryList</c> fires, which is also what <c>onchange</c> keys on.</summary>
    internal const string ChangeEventType = "change";

    /// <inheritdoc />
    public override string ToString() => "[object MediaQueryList]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsMediaQueryList Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsMediaQueryList list)
        {
            return list;
        }

        if (thisObject is ObjectInstance instance)
        {
            Jint.Runtime.Throw.TypeError(
                instance.Engine.Realm,
                "Failed to execute '" + member + "' on 'MediaQueryList': Illegal invocation");
        }

        Jint.Runtime.Throw.TypeErrorNoEngine(
            "Failed to execute '" + member + "' on 'MediaQueryList': Illegal invocation");
        return null!;
    }
}
