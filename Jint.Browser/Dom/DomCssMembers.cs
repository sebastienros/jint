using Jint.HtmlParser.Css.Model;
using Jint.Browser.Styling;
using Jint.Native;

namespace Jint.Browser.Dom;

// A dormant contract brand, never a semantic CSS model. Its private constructor prevents
// instances and subclasses; an own member must fail its WebIDL receiver check first.
internal abstract class NativeCssUnavailable
{
    private NativeCssUnavailable() { }
}

internal static class DomCssMembers
{
    // CSSOM §6.4: expose the native child list with stable cssRules/rules aliases,
    // including opaque nested at-rules retained by the renderless parser.
    internal static CssRuleList Rules(DomRealm realm, CssStyleRule rule)
        => NativeCssBindings.ReadRules(realm, rule);

    internal static JsValue Unavailable(DomRealm realm, string member)
        => DomFailures.Refuse(realm, member, "NotSupportedError", "This CSS interface has no native semantic producer.");
}
