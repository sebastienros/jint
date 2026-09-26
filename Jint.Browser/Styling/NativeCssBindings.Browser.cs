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
    internal static bool StyleDisabled(DomRealm realm, Element owner)
    {
        if (owner.NamespaceUri == Namespaces.Html && owner.LocalName == "link")
            return new DomReadWork(Work(realm).Charge, realm.CancellationToken).Attribute(owner, "disabled") is not null;
        return NativeCssStyleSheets.AssociatedOwner(owner, Work(realm)) is { } resource && NativeCssStyleSheets.DisabledOf(resource);
    }
    internal static void SetStyleDisabled(DomRealm realm, Element owner, bool disabled)
    {
        if (owner.NamespaceUri == Namespaces.Html && owner.LocalName == "link")
        {
            NativeCssStyleSheets.PrepareOwner(realm, owner);
            DomLegacyHtmlAttributes.SetFlag(realm, owner, "disabled", disabled);
            return;
        }
        if (NativeCssStyleSheets.AssociatedOwner(owner, Work(realm)) is { } resource)
        {
            realm.Engine.Constraints.Check();
            NativeCssStyleSheets.SetDisabled(resource, disabled);
        }
    }
    internal static NativeCssStyleSetList StyleSheetSets(DomRealm realm, Document document)
    {
        realm.Engine.Constraints.Check();
        return NativeCssStyleSheets.SetsOf(document).Names;
    }
    internal static string? SelectedStyleSheetSet(DomRealm realm, Document document) => NativeCssStyleSheets.SetsOf(document).Selected(Work(realm));
    internal static void SetSelectedStyleSheetSet(DomRealm realm, Document document, string? name) => NativeCssStyleSheets.SetsOf(document).SetSelected(name, Work(realm));
    internal static string? LastStyleSheetSet(DomRealm realm, Document document) => NativeCssStyleSheets.SetsOf(document).Last(Work(realm));
    internal static string PreferredStyleSheetSet(DomRealm realm, Document document) => NativeCssStyleSheets.SetsOf(document).Preferred(Work(realm));
    internal static void EnableStyleSheetsForSet(DomRealm realm, Document document, string? name) => NativeCssStyleSheets.SetsOf(document).EnableForSet(name, Work(realm));
    internal static int Length(DomRealm realm, NativeCssStyleSetList list) => list.Read(Work(realm)).Count;
    internal static string? Item(DomRealm realm, NativeCssStyleSetList list, int index)
    {
        var names = list.Read(Work(realm));
        return (uint) index < (uint) names.Count ? names[index] : null;
    }
    internal static bool Contains(DomRealm realm, NativeCssStyleSetList list, string name)
    {
        var work = Work(realm);
        foreach (var entry in list.Read(work))
            if (Jint.HtmlParser.Css.Values.References.CssSubstitutionArguments.Equals(entry, name, work)) return true;
        return false;
    }
    internal static string CssText(DomRealm realm, CssRule rule) => CssRuleSerializer.Serialize(rule, Work(realm));
    internal static string? Href(CssStyleSheet sheet) => sheet.Attachment.OwnerNode is Element { LocalName: "style" }
        ? null : sheet.Attachment.SourceUrl?.AbsoluteUri;
    internal static string? Title(DomRealm realm, CssStyleSheet sheet)
    {
        if (sheet.Attachment.OwnerNode is not Element owner || owner.TreeShadowRoot is not null) return null;
        var title = new DomReadWork(Work(realm).Charge, realm.CancellationToken).Attribute(owner, "title");
        return string.IsNullOrEmpty(title) ? null : title;
    }
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
