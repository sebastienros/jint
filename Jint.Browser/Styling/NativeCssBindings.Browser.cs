using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// CSSOM metadata/operation adapters used by generated bindings. Models remain engine-free.
internal static partial class NativeCssBindings
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
    internal static CssRuleList ReadRules(DomRealm realm, CssRuleList rules)
    {
        // Array-like indexed/length fast paths do not pass through the generated member guard.
        try { return NativeCssParsing.ReadRules(rules, Work(realm)); }
        catch (NotSupportedException exception)
        {
            DomFailures.Refuse(realm, "CSSRuleList", "NotSupportedError", exception.Message);
            throw;
        }
    }
    internal static CssRuleList ReadRules(DomRealm realm, CssRule rule) => ReadRules(realm, rule.Rules);
    internal static CssMediaList ReadMedia(DomRealm realm, CssMediaList media) => NativeCssParsing.ReadMedia(media, Work(realm));
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
    internal static string CssText(DomRealm realm, CssRule rule)
    {
        var work = Work(realm);
        NativeCssParsing.ReadRule(rule, work);
        return CssRuleSerializer.Serialize(rule, work);
    }
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
    internal static CssRule? Item(DomRealm realm, CssRuleList rules, int index)
    {
        NativeCssParsing.ReadRules(rules, Work(realm));
        return (uint) index < (uint) rules.Count ? rules[index] : null;
    }
    internal static string? Item(DomRealm realm, CssMediaList media, int index)
    {
        NativeCssParsing.ReadMedia(media, Work(realm));
        return (uint) index < (uint) media.Count ? media[index] : null;
    }
    internal static string ConditionText(DomRealm realm, CssConditionRule rule)
    {
        if (rule is CssMediaRule media) return MediaText(realm, media.Media);
        var work = Work(realm);
        work.Charge(rule.ConditionText.Length);
        work.CheckCancellation();
        return rule.ConditionText;
    }
    internal static string MediaText(DomRealm realm, CssMediaList media)
    {
        var work = Work(realm);
        return NativeCssParsing.ReadMedia(media, work).Serialize(work);
    }
    internal static void SetMediaText(DomRealm realm, CssMediaList media, string text)
    {
        var work = MutationWork(realm, () => media.Stamp);
        media.SetMediaText(text, null, work, work.Token);
    }
    internal static void AppendMedium(DomRealm realm, CssMediaList media, string text)
    {
        var work = MutationWork(realm, () => media.Stamp);
        NativeCssParsing.ReadMedia(media, work);
        media.AppendMedium(text, null, work, work.Token);
    }
    internal static void DeleteMedium(DomRealm realm, CssMediaList media, string text)
    {
        var work = MutationWork(realm, () => media.Stamp);
        NativeCssParsing.ReadMedia(media, work);
        media.DeleteMedium(text, null, work, work.Token);
    }
    internal static void SetSelectorText(DomRealm realm, CssStyleRule rule, string text)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        NativeCssParsing.ReadTree(rule.Rules, work);
        rule.SetSelectorText(text, null, work, work.Token);
    }
    internal static int InsertRule(DomRealm realm, CssStyleSheet sheet, string text, int index)
    {
        Reconcile(realm, sheet);
        var work = MutationWork(realm, () => sheet.Stamp);
        if (index >= 0) NativeCssParsing.ReadRules(sheet.Rules, work);
        var inserted = sheet.InsertRule(text, index, null, work, work.Token);
        QueueImports(realm, sheet);
        return inserted;
    }
    internal static int InsertRule(DomRealm realm, CssGroupingRule rule, string text, int index)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        if (index >= 0) NativeCssParsing.ReadRules(rule.Rules, work);
        return rule.InsertRule(text, index, null, work, work.Token);
    }
    internal static void DeleteRule(DomRealm realm, CssStyleSheet sheet, int index)
    {
        Reconcile(realm, sheet);
        var work = MutationWork(realm, () => sheet.Stamp);
        if (index >= 0) NativeCssParsing.ReadRules(sheet.Rules, work);
        sheet.DeleteRule(index, work);
        QueueImports(realm, sheet);
    }
    private static void QueueImports(DomRealm realm, CssStyleSheet sheet)
        => PageRuntime.Find(realm.Engine)?.Parser?.QueueCssImports(sheet);
    internal static void DeleteRule(DomRealm realm, CssGroupingRule rule, int index)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        if (index >= 0) NativeCssParsing.ReadRules(rule.Rules, work);
        rule.DeleteRule(index, work);
    }
    internal static string KeyText(DomRealm realm, CssKeyframeRule rule)
    {
        var text = rule.KeyText;
        var work = Work(realm);
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }
    internal static string Name(DomRealm realm, CssKeyframesRule rule)
    {
        var name = rule.Name;
        var work = Work(realm);
        work.Charge(name.Length);
        work.CheckCancellation();
        return name;
    }
    internal static string LayerName(DomRealm realm, CssLayerBlockRule rule)
    {
        var work = Work(realm);
        work.Charge(rule.Name.Length);
        work.CheckCancellation();
        return rule.Name;
    }
    internal static Native.Object.ObjectInstance LayerNames(DomRealm realm, CssLayerStatementRule rule)
    {
        var work = Work(realm);
        work.CheckCancellation();
        if (realm.CssLayerNames.TryGetValue(rule, out var cached)) return cached;
        var values = new Native.JsValue[rule.Names.Count];
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(rule.Names[i].Text.Length + 1);
            values[i] = rule.Names[i].Text;
        }
        var array = realm.OwningRealm.Intrinsics.Array.Construct(values);
        array.SetIntegrityLevel(Native.Object.ObjectInstance.IntegrityLevel.Frozen);
        work.CheckCancellation();
        realm.CssLayerNames.Add(rule, array);
        return array;
    }
    internal static void SetName(DomRealm realm, CssKeyframesRule rule, string name)
        => rule.SetName(name, MutationWork(realm, () => rule.Stamp));
    internal static void SetKeyText(DomRealm realm, CssKeyframeRule rule, string text)
        => rule.SetKeyText(text, null, MutationWork(realm, () => rule.Stamp));
    internal static void AppendKeyframe(DomRealm realm, CssKeyframesRule rule, string text)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        NativeCssParsing.ReadRules(rule.Rules, work);
        rule.AppendRule(text, null, work);
    }
    internal static void DeleteKeyframe(DomRealm realm, CssKeyframesRule rule, string text)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        NativeCssParsing.ReadRules(rule.Rules, work);
        rule.DeleteRule(text, null, work);
    }
    internal static CssKeyframeRule? FindRule(DomRealm realm, CssKeyframesRule rule, string text)
    {
        var work = MutationWork(realm, () => rule.Stamp);
        NativeCssParsing.ReadRules(rule.Rules, work);
        return rule.FindRule(text, null, work);
    }
    // CSSOM §6.4: setting a rule's cssText intentionally does nothing.
    internal static void SetCssText(DomRealm realm, CssRule rule, string text) => realm.Engine.Constraints.Check();
}
