using Jint.Browser.Cookies;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Events;

/// <summary>https://cookiestore.spec.whatwg.org/#cookiechangeevent - immutable array identities, mutable dictionary elements.</summary>
internal sealed class JsCookieChangeEvent : JsEvent
{
    internal JsCookieChangeEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsArray changed, JsArray deleted)
        : base(engine, type, init, timeStamp)
    {
        Changed = changed;
        Deleted = deleted;
    }

    internal JsArray Changed { get; }
    internal JsArray Deleted { get; }

    /// <summary>https://cookiestore.spec.whatwg.org/#fire-a-change-event and #prepare-lists.</summary>
    internal static JsCookieChangeEvent CreateTrusted(DomRealm dom, List<SetCookie> changed, List<SetCookie> deleted)
    {
        using var scope = new RealmScope(dom.Engine, dom.OwningRealm);
        var events = BrowserEventRealm.Of(dom.Engine, dom.OwningRealm);
        return new JsCookieChangeEvent(dom.Engine, new JsString("change"), new EventInit(false, false, false),
            EventConstructor.TimeStampNow(dom.Engine), Items(changed, false), Items(deleted, true))
        {
            IsTrusted = true,
            _prototype = events.PrototypeOf(BrowserEventInterfaces.CookieChangeEvent),
        };

        JsArray Items(List<SetCookie> cookies, bool omitValue)
        {
            var values = new JsValue[cookies.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = CookieValues.Item(dom.Engine, dom.OwningRealm, cookies[i].Name, omitValue ? null : cookies[i].Value);
            }
            var array = dom.OwningRealm.Intrinsics.Array.ConstructFast(values);
            array.SetIntegrityLevel(IntegrityLevel.Frozen);
            return array;
        }
    }
}
