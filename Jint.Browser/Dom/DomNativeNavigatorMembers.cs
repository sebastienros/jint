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
        var value = userAgent.StartsWith("Mozilla/5.0 (", StringComparison.Ordinal)
            ? JsString.Create(userAgent["Mozilla/".Length..])
            : JsString.Empty;
        work.Check();
        return value;
    }
}
