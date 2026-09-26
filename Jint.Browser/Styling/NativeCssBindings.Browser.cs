using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// CSSOM metadata/operation adapters used by generated bindings. Models remain engine-free.
internal static class NativeCssBindings
{
    internal static CssValueWork Work(DomRealm realm) => new(realm.CancellationToken, realm.Engine.Constraints.Check);
    internal static string? Href(CssStyleSheet sheet) => sheet.Attachment.OwnerNode is Element { LocalName: "style" }
        ? null : sheet.Attachment.SourceUrl?.AbsoluteUri;
    internal static string? Title(DomRealm realm, CssStyleSheet sheet) => sheet.Attachment.OwnerNode is Element owner
        ? new DomReadWork(Work(realm).Charge, realm.CancellationToken).Attribute(owner, "title") : null;
    internal static Node? OwnerNode(CssStyleSheet sheet) => sheet.Attachment.OwnerNode;
    internal static CssRule? OwnerRule(CssStyleSheet sheet) => sheet.Attachment.ImportOwner;
    internal static CssStyleSheet? ParentStyleSheet(CssStyleSheet sheet) => sheet.Attachment.ImportOwner?.ParentStyleSheet;
    internal static string Type(CssStyleSheet sheet) => "text/css";
    internal static CssRule? Item(CssRuleList rules, int index) => (uint) index < (uint) rules.Count ? rules[index] : null;
    internal static string? Item(CssMediaList media, int index) => (uint) index < (uint) media.Count ? media[index] : null;
    internal static string MediaText(DomRealm realm, CssMediaList media) => media.Serialize(Work(realm));
    internal static void SetMediaText(DomRealm realm, CssMediaList media, string text)
    {
        var work = Work(realm);
        media.SetMediaText(text, null, work, work.Token);
    }
    // CSSOM §6.4: setting a rule's cssText intentionally does nothing.
    internal static void SetCssText(DomRealm realm, CssRule rule, string text) => realm.Engine.Constraints.Check();
}
