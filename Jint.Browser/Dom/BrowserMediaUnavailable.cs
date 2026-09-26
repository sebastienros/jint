using Jint.Native;

namespace Jint.Browser.Dom;

// A dormant binding brand, not a rendering context or text cue model. No instance or subclass
// can be created; generated members must perform their WebIDL receiver check before refusal.
internal abstract class NativeMediaUnavailable
{
    private NativeMediaUnavailable() { }
}

internal static class BrowserUnavailableMediaMembers
{
    internal static JsValue Refuse(DomRealm realm, string member)
        => DomFailures.Refuse(realm, member, "NotSupportedError", "This media interface has no native semantic producer.");
}
