using Jint.Native;
using Jint.WebApi.Navigator;

namespace Jint.Browser.Dom;

internal static class DomNativeNavigatorMembers
{
    // https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-appversion
    // Browser uses the Chromium/WebKit compatibility branch: the current configured UA,
    // with its Mozilla/ prefix removed only when it begins with the specified prefix.
    internal static JsValue AppVersion(DomRealm realm, JsNavigator navigator)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var userAgent = navigator.Engine.NavigatorUserAgentValue.AsString();
        return AppVersion(userAgent, work);
    }

    internal static JsValue AppVersion(string userAgent, DomReadWork work)
    {
        work.Check();
        if (!userAgent.StartsWith("Mozilla/5.0 (", StringComparison.Ordinal))
        {
            work.Check();
            return JsString.Empty;
        }
        var value = string.Create(userAgent.Length - "Mozilla/".Length, (userAgent, work), static (destination, state) =>
        {
            for (var i = 0; i < destination.Length; i++)
            {
                state.work.Step();
                destination[i] = state.userAgent[i + "Mozilla/".Length];
            }
            state.work.Check();
        });
        work.Check();
        return JsString.Create(value);
    }
}
