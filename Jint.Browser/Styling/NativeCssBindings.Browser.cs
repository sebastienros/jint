using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// CSSOM metadata/operation adapters used by generated bindings. Models remain engine-free.
internal static class NativeCssBindings
{
    internal static CssValueWork Work(DomRealm realm) => new(realm.CancellationToken, realm.Engine.Constraints.Check);
    private static CssValueWork MutationWork(DomRealm realm, Func<CssMutationStamp> read)
    {
        var stamp = read();
        return new(realm.CancellationToken, () =>
        {
            realm.Engine.Constraints.Check();
            if (!stamp.CanReuse || read() != stamp) throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
    }
    internal static CssStyleSheet Reconcile(DomRealm realm, CssStyleSheet sheet)
    {
        if (sheet.Attachment.OwnerNode is Element owner) NativeCssStyleSheets.SheetOf(realm, owner);
        return sheet;
    }
    internal static CssRuleList Rules(DomRealm realm, CssStyleSheet sheet) => Reconcile(realm, sheet).Rules;
    internal static bool StyleDisabled(DomRealm realm, Element owner) =>
        NativeCssStyleSheets.SheetOf(realm, owner)?.Disabled ?? false;
    internal static void SetStyleDisabled(DomRealm realm, Element owner, bool disabled)
    {
        if (NativeCssStyleSheets.SheetOf(realm, owner) is { } sheet)
        {
            realm.Engine.Constraints.Check();
            sheet.Disabled = disabled;
        }
    }
    internal static string CssText(DomRealm realm, CssRule rule) => CssRuleSerializer.Serialize(rule, Work(realm));
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
        var work = MutationWork(realm, () => media.Stamp);
        media.SetMediaText(text, null, work, work.Token);
    }
    internal static void AppendMedium(DomRealm realm, CssMediaList media, string text)
    {
        var work = MutationWork(realm, () => media.Stamp);
        media.AppendMedium(text, null, work, work.Token);
    }
    internal static void DeleteMedium(DomRealm realm, CssMediaList media, string text)
    {
        var work = MutationWork(realm, () => media.Stamp);
        media.DeleteMedium(text, null, work, work.Token);
    }
    internal static void SetSelectorText(DomRealm realm, CssStyleRule rule, string text)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        rule.SetSelectorText(text, null, work, work.Token);
    }
    internal static int InsertRule(DomRealm realm, CssStyleSheet sheet, string text, int index)
    {
        Reconcile(realm, sheet);
        var work = MutationWork(realm, () => sheet.Stamp);
        return sheet.InsertRule(text, index, null, work, work.Token);
    }
    internal static int InsertRule(DomRealm realm, CssMediaRule rule, string text, int index)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        return rule.InsertRule(text, index, null, work, work.Token);
    }
    internal static void DeleteRule(DomRealm realm, CssStyleSheet sheet, int index)
    {
        Reconcile(realm, sheet);
        realm.Engine.Constraints.Check();
        sheet.DeleteRule(index);
    }
    internal static void DeleteRule(DomRealm realm, CssMediaRule rule, int index)
    {
        realm.Engine.Constraints.Check();
        rule.DeleteRule(index);
    }
    // CSSOM §6.4: setting a rule's cssText intentionally does nothing.
    internal static void SetCssText(DomRealm realm, CssRule rule, string text) => realm.Engine.Constraints.Check();
}
