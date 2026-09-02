using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The <c>change</c> event a <c>MediaQueryList</c> fires: the query, and whether it now matches.
/// </summary>
/// <remarks>
/// <a href="https://drafts.csswg.org/cssom-view/#mediaquerylistevent">CSSOM View</a> gives the event its own
/// interface because <c>mql.addEventListener('change', e =&gt; e.matches)</c> is the idiomatic read — the
/// listener is usually written without a reference to the list. Both members are also readable off the list
/// itself, and they agree, because the list recomputes rather than caching.
/// </remarks>
internal sealed class JsMediaQueryListEvent : JsEvent
{
    internal JsMediaQueryListEvent(Engine engine, JsString type, EventInit init, double timeStamp, string media, bool matches)
        : base(engine, type, init, timeStamp)
    {
        Media = media;
        Matches = matches;
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-mediaquerylistevent-media.</summary>
    internal string Media { get; }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-mediaquerylistevent-matches.</summary>
    internal bool Matches { get; }

    /// <summary>The receiver check the two members start with.</summary>
    internal static JsMediaQueryListEvent Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsMediaQueryListEvent ev)
        {
            return ev;
        }

        var message = "Failed to read the '" + member + "' property from 'MediaQueryListEvent': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }
}
