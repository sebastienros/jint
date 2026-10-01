using System.Runtime.CompilerServices;
using Jint.HtmlParser.Css.Model;
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
    private static readonly ConditionalWeakTable<CssStyleRule, CssRuleList> EmptyStyleRules = new();

    // The native stylesheet builder refuses nested rule grammar before publishing a style
    // rule. CSSStyleRule's two legacy list accessors therefore expose its actual empty list,
    // with one identity per owner (CSSOM §6.4 and the contract's SameObject aliases).
    internal static CssRuleList Rules(DomRealm realm, CssStyleRule rule)
    {
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        return EmptyStyleRules.GetValue(rule, static _ => new CssRuleList([]));
    }

    internal static JsValue Unavailable(DomRealm realm, string member)
        => DomFailures.Refuse(realm, member, "NotSupportedError", "This CSS interface has no native semantic producer.");
}
