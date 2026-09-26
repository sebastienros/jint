using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

internal static partial class NativeCssStyleSheets
{
    internal static NativeCssSheetSets SetsOf(Document document)
    {
        var resources = Documents.GetValue(document, static _ => new Resources());
        return resources.Sets ??= new(document);
    }

    internal static void SetDefaultStyle(Document document, string name, CssValueWork work) => SetsOf(document).SetDefaultStyle(name, work);

    // Browser association-time seam: source and metadata only, never CSS syntax or value validation.
    internal static void AssociateOwner(Document document, Element owner, CssValueWork work)
    {
        var stamp = document.MutationStamp;
        var parentWork = work;
        work = CssValueWork.Guard(parentWork, () =>
        {
            parentWork.CheckCancellation();
            if (stamp == ulong.MaxValue || stamp != document.MutationStamp || !ReferenceEquals(document, owner.OwnerDocument))
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
        if (!ReferenceEquals(document, owner.OwnerDocument)) throw new ArgumentException("A stylesheet belongs to another document.", nameof(owner));
        var resources = Documents.GetValue(document, static _ => new Resources());
        var reads = new DomReadWork(work.Charge, work.Token);
        if (!EligibleOwner(owner, reads, work)) return;
        Node root = owner;
        while (root.ParentNode is { } parent) { work.Charge(1); root = parent; }
        if (root is not Document && root is not ShadowRoot) return;
        if (root is ShadowRoot)
        {
            Node connected = root;
            while ((connected.ParentNode ?? (connected as ShadowRoot)?.Host) is { } parent)
            { work.Charge(1); connected = parent; }
            if (connected is not Document) return;
        }
        if (!resources.Owners.TryGetValue(owner, out var resource))
        {
            if (owner.LocalName == "link") return; // A link has no associated sheet before its fetch succeeds.
            resource = new Resource("", new() { OwnerNode = owner });
            resources.Owners.Add(owner, resource);
        }
        ObserveDisabled(owner, resource);
        if (root is Document) SetsOf(document).Associate(owner, resource, work);
        else if (!resource.Associated)
        {
            resource.Associated = true;
            resource.Disabled = owner.LocalName == "link" && reads.Attribute(owner, "disabled") is not null;
        }
        work.CheckCancellation();
    }

    internal static bool EligibleOwner(Element owner, DomReadWork reads, CssValueWork work)
    {
        if (owner.LocalName == "style" && owner.NamespaceUri is Namespaces.Html or Namespaces.Svg ||
            owner.LocalName == "link" && owner.NamespaceUri == Namespaces.Html && HasStyleSheetRelation(reads.Attribute(owner, "rel"), work))
        {
            var type = reads.Attribute(owner, "type");
            return string.IsNullOrEmpty(type) || reads.EqualAsciiIgnoreCase(type, "text/css");
        }
        return false;
    }

    internal static bool DisabledOf(Resource resource) => resource.Sheet?.Disabled ?? resource.Disabled;
    internal static void SetDisabled(Resource resource, bool disabled)
    {
        if (DisabledOf(resource) == disabled) return;
        if (resource.Sheet is { } sheet) sheet.Disabled = disabled;
        else resource.Disabled = disabled;
    }

    internal static void RefreshDisabled(Element owner, Resource resource, DomReadWork reads)
    {
        if (owner.NamespaceUri != Namespaces.Html || owner.LocalName != "link") return;
        var disabled = reads.Attribute(owner, "disabled") is not null;
        if (resource.DisabledDirty || resource.DisabledSource != disabled)
        {
            SetDisabled(resource, disabled);
            resource.DisabledSource = disabled;
            resource.DisabledDirty = false;
        }
    }
}

// CSSOM 2013 §6.2.3: document stylesheet-set history shares the actual resource disabled authority.
internal sealed class NativeCssSheetSets(Document document)
{
    private readonly List<WeakReference<Element>> _owners = [];
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Element, NativeCssStyleSheets.Resource> _resources = new();
    private string _preferred = "";
    private string? _last;
    private NativeCssStyleSetList? _names;
    internal NativeCssStyleSetList Names => _names ??= new(this);

    internal void Associate(Element owner, NativeCssStyleSheets.Resource resource, CssValueWork work)
    {
        if (_resources.TryGetValue(owner, out var existing) && ReferenceEquals(resource, existing)) return;
        var reads = new DomReadWork(work.Charge, work.Token);
        var title = reads.Attribute(owner, "title") ?? "";
        var alternate = IsAlternate(reads.Attribute(owner, "rel"), work);
        var explicitlyDisabled = owner.LocalName == "link" && reads.Attribute(owner, "disabled") is not null;
        var preferred = _preferred;
        if (!explicitlyDisabled && preferred.Length == 0 && title.Length != 0 && !alternate) preferred = title;
        var chosen = _last ?? preferred;
        var disabled = explicitlyDisabled ||
            title.Length != 0 && !CssSubstitutionArguments.Equals(title, chosen, work);
        work.CheckCancellation();
        _resources.Remove(owner);
        _resources.Add(owner, resource);
        _owners.Add(new(owner));
        resource.Associated = true;
        resource.DisabledSource = owner.LocalName == "link" ? reads.Attribute(owner, "disabled") is not null : null;
        NativeCssStyleSheets.SetDisabled(resource, disabled);
        if (!CssSubstitutionArguments.Equals(_preferred, preferred, work)) SetDefaultStyle(preferred, work);
    }

    internal string Preferred(CssValueWork work) { work = Guard(work); work.Charge(_preferred.Length); work.CheckCancellation(); return _preferred; }
    internal string? Last(CssValueWork work) { work = Guard(work); work.Charge(_last?.Length ?? 0); work.CheckCancellation(); return _last; }
    internal string? Selected(CssValueWork work)
    {
        work = Guard(work);
        var sheets = Read(work);
        var groups = new Dictionary<string, (bool Any, bool All)>(new NamesComparer(work));
        foreach (var item in sheets)
        {
            if (item.Title.Length == 0) continue;
            var enabled = !item.Disabled;
            if (groups.TryGetValue(item.Title, out var group)) groups[item.Title] = (group.Any || enabled, group.All && enabled);
            else groups.Add(item.Title, (enabled, enabled));
        }
        string? selected = null;
        var fullyEnabled = false;
        foreach (var group in groups)
        {
            work.Charge(1);
            if (!group.Value.Any) continue;
            if (selected is not null) { Verify(sheets, work); return null; }
            selected = group.Key;
            fullyEnabled = group.Value.All;
        }
        work.CheckCancellation();
        Verify(sheets, work);
        return fullyEnabled ? selected : "";
    }

    internal IReadOnlyList<string> NamesOf(CssValueWork work)
    {
        work = Guard(work);
        var sheets = Read(work);
        var result = new List<string>();
        var seen = new HashSet<string>(new NamesComparer(work));
        foreach (var item in sheets)
            if (item.Title.Length != 0 && seen.Add(item.Title)) result.Add(item.Title);
        work.CheckCancellation();
        Verify(sheets, work);
        return result;
    }

    internal void SetSelected(string? name, CssValueWork work)
    {
        if (name is null) return;
        work = Guard(work);
        EnableForSet(name, work);
        work.CheckCancellation();
        _last = name;
    }

    internal void EnableForSet(string? name, CssValueWork work)
    {
        if (name is null) return;
        work = Guard(work);
        work.Charge(name.Length);
        var changes = new List<(NativeCssStyleSheets.Resource Resource, bool Disabled)>();
        foreach (var item in Read(work))
            if (item.Title.Length != 0) changes.Add((item.Resource, !CssSubstitutionArguments.Equals(item.Title, name, work)));
        foreach (var change in changes)
        {
            work.Charge(1);
            NativeCssStyleSheets.SetDisabled(change.Resource, change.Disabled);
        }
        work.CheckCancellation();
    }

    internal void SetDefaultStyle(string name, CssValueWork work)
    {
        work = Guard(work);
        work.Charge(name.Length);
        var changed = !CssSubstitutionArguments.Equals(_preferred, name, work);
        work.CheckCancellation();
        _preferred = name;
        if (changed && _last is null) EnableForSet(name, work);
    }

    private List<SheetState> Read(CssValueWork work)
    {
        var stamp = document.MutationStamp;
        var reads = new DomReadWork(work.Charge, work.Token);
        var result = new List<SheetState>();
        foreach (var weak in _owners)
        {
            work.Charge(1);
            if (!weak.TryGetTarget(out var owner) || !ReferenceEquals(owner.OwnerDocument, document) ||
                !NativeCssStyleSheets.EligibleOwner(owner, reads, work)) continue;
            Node root = owner;
            while (root.ParentNode is { } parent) { work.Charge(1); root = parent; }
            if (!ReferenceEquals(root, document) || !_resources.TryGetValue(owner, out var resource)) continue;
            NativeCssStyleSheets.RefreshDisabled(owner, resource, reads);
            result.Add(new(reads.Attribute(owner, "title") ?? "", resource, NativeCssStyleSheets.DisabledOf(resource), resource.Sheet?.Stamp));
        }
        work.CheckCancellation();
        if (stamp == ulong.MaxValue || stamp != document.MutationStamp) throw new InvalidOperationException(NativeCssQuery.Invalidated);
        return result;
    }

    private CssValueWork Guard(CssValueWork work)
    {
        var stamp = document.MutationStamp;
        return CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (stamp == ulong.MaxValue || stamp != document.MutationStamp) throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
    }

    private static void Verify(List<SheetState> sheets, CssValueWork work)
    {
        foreach (var sheet in sheets)
        {
            work.Charge(1);
            if (sheet.Disabled != NativeCssStyleSheets.DisabledOf(sheet.Resource) || sheet.Stamp != sheet.Resource.Sheet?.Stamp)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        }
        work.CheckCancellation();
    }

    private readonly record struct SheetState(string Title, NativeCssStyleSheets.Resource Resource, bool Disabled,
        Jint.HtmlParser.Css.Model.Syntax.CssMutationStamp? Stamp);

    private static bool IsAlternate(string? rel, CssValueWork work)
    {
        if (rel is null) return false;
        var start = 0;
        var reads = new DomReadWork(work.Charge, work.Token);
        for (var end = 0; end <= rel.Length; end++)
        {
            work.Charge(1);
            if (end != rel.Length && rel[end] is not (' ' or '\t' or '\r' or '\n' or '\f')) continue;
            if (end - start == 9 && reads.EqualAsciiIgnoreCase(rel.Substring(start, 9), "alternate")) return true;
            start = end + 1;
        }
        return false;
    }

    private sealed class NamesComparer(CssValueWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? left, string? right) => left is not null && right is not null && CssSubstitutionArguments.Equals(left, right, work);
        public int GetHashCode(string value) => unchecked((int) CssSubstitutionArguments.Hash(value, work));
    }
}

// Browser supplies the work object per operation, retaining a stable list receiver.
internal sealed class NativeCssStyleSetList(NativeCssSheetSets sets)
{
    internal IReadOnlyList<string> Read(CssValueWork work) => sets.NamesOf(work);
}
