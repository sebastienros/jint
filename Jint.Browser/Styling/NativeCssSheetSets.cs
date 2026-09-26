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
    internal static void AssociateOwner(Document document, Element owner, CssValueWork work) => AssociateOwner(document, owner, work, null);

    private static void AssociateOwner(Document document, Element owner, CssValueWork work, Node? knownRoot)
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
        var reads = new DomReadWork(work.Charge, work.Token);
        var resource = PrepareOwner(document, owner, work);
        if (!EligibleOwner(owner, reads, work)) return;
        Node root = knownRoot ?? owner;
        if (knownRoot is null)
            while (root.ParentNode is { } parent) { work.Charge(1); root = parent; }
        if (root is not Document && root is not ShadowRoot) return;
        if (knownRoot is null && root is ShadowRoot)
        {
            Node connected = root;
            while ((connected.ParentNode ?? (connected as ShadowRoot)?.Host) is { } parent)
            { work.Charge(1); connected = parent; }
            if (connected is not Document) return;
        }
        if (resource is null || owner.LocalName == "link" && !resource.Loaded) return;
        if (root is Document) SetsOf(document).Associate(owner, resource, work);
        else if (!resource.Associated)
        {
            var disabled = owner.LocalName == "link" && reads.Attribute(owner, "disabled") is not null;
            work.CheckCancellation();
            resource.Associated = true;
            resource.Disabled = disabled;
        }
    }

    internal static Resource? PrepareOwner(Document document, Element owner, CssValueWork work)
    {
        if (owner.NamespaceUri != Namespaces.Html && owner.NamespaceUri != Namespaces.Svg ||
            owner.LocalName != "style" && !(owner.LocalName == "link" && owner.NamespaceUri == Namespaces.Html)) return null;
        var resources = Documents.GetValue(document, static _ => new Resources());
        if (resources.Owners.TryGetValue(owner, out var known)) return known;
        var stamp = document.MutationStamp;
        var disabled = owner.LocalName == "link" && new DomReadWork(work.Charge, work.Token).Attribute(owner, "disabled") is not null;
        work.CheckCancellation();
        if (!ReferenceEquals(owner.OwnerDocument, document) || stamp != document.MutationStamp)
            throw new InvalidOperationException(NativeCssQuery.Invalidated);
        var resource = new Resource("", new() { OwnerNode = owner })
        { Loaded = owner.LocalName != "link", Disabled = disabled, DisabledSource = owner.LocalName == "link" ? disabled : null };
        ObserveDisabled(owner, resource);
        resources.Owners.Add(owner, resource);
        return resource;
    }

    // The Browser mutation hook passes the new null-namespace value at the actual transition.
    // This neither parses nor fetches and preserves later CSSOM writes against earlier attribute writes.
    internal static void OwnerAttributeChanged(Document document, Element owner, string? namespaceUri,
        string name, string? value)
    {
        if (namespaceUri is not null || name != "disabled" || owner.NamespaceUri != Namespaces.Html || owner.LocalName != "link" ||
            !Documents.TryGetValue(document, out var resources) || !resources.Owners.TryGetValue(owner, out var resource)) return;
        if (value is null) resource.ExplicitlyEnabled = true;
        if (resource.Associated) SetDisabled(resource, value is not null);
        resource.DisabledSource = value is not null;
        resource.DisabledObservedStamp = document.MutationStamp;
        resource.DisabledDirty = false;
    }

    internal static void DisassociateOwner(Document document, Element owner, CssValueWork work)
    {
        work.CheckCancellation();
        if (!Documents.TryGetValue(document, out var resources) || !resources.Owners.TryGetValue(owner, out var resource)) return;
        if (resource.Sheet is { } sheet) sheet.SetAttachment(resource.Attachment with { OwnerNode = null });
        resource.Sheet = null;
        resource.Associated = false;
        resources.Sets?.Removed(owner);
        resource.NativeStamp = null;
        resource.MediaSource = null;
        resource.DisabledObservedStamp = null;
        resource.Disabled = false;
        resource.Loaded = owner.LocalName != "link";
        resource.Replaced = false;
        Jint.HtmlParser.Css.Model.Syntax.CssMutationStamp.Advance(ref resources.Version);
    }

    internal static Resource? AssociatedOwner(Element owner, CssValueWork work)
    {
        if (owner.OwnerDocument is not { } document) return null;
        var reads = new DomReadWork(work.Charge, work.Token);
        if (!EligibleOwner(owner, reads, work)) return null;
        Node root = owner;
        while (root.ParentNode is { } parent) { work.Charge(1); root = parent; }
        if (root is not Document && root is not ShadowRoot) return null;
        if (root is ShadowRoot)
        {
            Node connected = root;
            while ((connected.ParentNode ?? (connected as ShadowRoot)?.Host) is { } parent)
            { work.Charge(1); connected = parent; }
            if (connected is not Document) return null;
        }
        AssociateOwner(document, owner, work, root);
        work.CheckCancellation();
        return Documents.TryGetValue(document, out var resources) && resources.Owners.TryGetValue(owner, out var resource)
            ? resource.Associated ? resource : null : null;
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

// CSSOM 2013 §6.2.3: history, current DOM ordering, and the actual resource disabled authority.
internal sealed class NativeCssSheetSets(Document document)
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Element, NativeCssStyleSheets.Resource> _resources = new();
    private string _preferred = "";
    private string? _last;
    private object _revision = new();
    private NativeCssStyleSetList? _names;
    internal NativeCssStyleSetList Names => _names ??= new(this);

    internal void Removed(Element owner)
    {
        if (_resources.Remove(owner)) _revision = new();
    }

    internal void Associate(Element owner, NativeCssStyleSheets.Resource resource, CssValueWork work)
    {
        if (_resources.TryGetValue(owner, out var existing) && ReferenceEquals(resource, existing) && resource.Associated) return;
        work = Guard(work);
        var reads = new DomReadWork(work.Charge, work.Token);
        var title = reads.Attribute(owner, "title") ?? "";
        var alternate = IsAlternate(reads.Attribute(owner, "rel"), work) && !resource.ExplicitlyEnabled;
        var disabledSource = owner.LocalName == "link" && reads.Attribute(owner, "disabled") is not null;
        var initiallyDisabled = disabledSource || alternate;
        var preferred = _preferred;
        if (!initiallyDisabled && preferred.Length == 0 && title.Length != 0 && !alternate) preferred = title;
        var preferredChanged = !CssSubstitutionArguments.Equals(_preferred, preferred, work);
        var incomingDisabled = initiallyDisabled || title.Length != 0 && !CssSubstitutionArguments.Equals(title, _last ?? preferred, work);
        var sheets = preferredChanged && _last is null ? Read(work) : new List<SheetState>();
        var changes = preferredChanged && _last is null ? Plan(sheets, preferred, work) : [];
        work.Charge(1); // The incoming flag update, charged before the coherent commit.
        Verify(sheets, work);
        // No callback, CSS parse or fetch can interrupt this metadata commit.
        _resources.Remove(owner);
        _resources.Add(owner, resource);
        resource.Associated = true;
        resource.DisabledSource = owner.LocalName == "link" ? disabledSource : null;
        Apply(changes);
        NativeCssStyleSheets.SetDisabled(resource, incomingDisabled);
        _preferred = preferred;
        _revision = new();
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
        var all = false;
        var multiple = false;
        foreach (var group in groups)
        {
            work.Charge(1);
            if (!group.Value.Any) continue;
            if (selected is not null) multiple = true;
            selected = group.Key;
            all = group.Value.All;
        }
        Verify(sheets, work);
        return multiple ? null : all ? selected : "";
    }

    internal IReadOnlyList<string> NamesOf(CssValueWork work)
    {
        work = Guard(work);
        var sheets = Read(work);
        var result = new List<string>();
        var seen = new HashSet<string>(new NamesComparer(work));
        foreach (var item in sheets)
            if (item.Title.Length != 0 && seen.Add(item.Title)) result.Add(item.Title);
        Verify(sheets, work);
        return result;
    }

    internal void SetSelected(string? name, CssValueWork work)
    {
        if (name is null) return;
        work = Guard(work);
        var sheets = Read(work);
        var changes = Plan(sheets, name, work);
        Verify(sheets, work);
        Apply(changes);
        _last = name;
        _revision = new();
    }

    internal void EnableForSet(string? name, CssValueWork work)
    {
        if (name is null) return;
        work = Guard(work);
        var sheets = Read(work);
        var changes = Plan(sheets, name, work);
        Verify(sheets, work);
        Apply(changes);
        _revision = new();
    }

    internal void SetDefaultStyle(string name, CssValueWork work)
    {
        work = Guard(work);
        work.Charge(name.Length);
        var changed = !CssSubstitutionArguments.Equals(_preferred, name, work);
        var sheets = Read(work);
        var changes = changed && _last is null ? Plan(sheets, name, work) : [];
        Verify(sheets, work);
        Apply(changes);
        _preferred = name;
        _revision = new();
    }

    private static List<(NativeCssStyleSheets.Resource Resource, bool Disabled)> Plan(List<SheetState> sheets, string name, CssValueWork work)
    {
        work.Charge(name.Length);
        var changes = new List<(NativeCssStyleSheets.Resource, bool)>();
        foreach (var item in sheets)
        {
            work.Charge(1); // Every potential flag update, before any flag is published.
            if (item.Title.Length != 0) changes.Add((item.Resource, !CssSubstitutionArguments.Equals(item.Title, name, work)));
        }
        return changes;
    }

    private static void Apply(List<(NativeCssStyleSheets.Resource Resource, bool Disabled)> changes)
    {
        foreach (var change in changes) NativeCssStyleSheets.SetDisabled(change.Resource, change.Disabled);
    }

    private List<SheetState> Read(CssValueWork work)
    {
        var reads = new DomReadWork(work.Charge, work.Token);
        var result = new List<SheetState>();
        // Only current ordinary-tree members are visited, in DOM order. There is no history-sized list.
        var pending = new Stack<Node>();
        pending.Push(document);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            { work.Charge(1); pending.Push(child); }
            if (node is not Element owner || !_resources.TryGetValue(owner, out var resource) || !resource.Associated ||
                !NativeCssStyleSheets.EligibleOwner(owner, reads, work)) continue;
            NativeCssStyleSheets.RefreshDisabled(owner, resource, reads);
            result.Add(new(reads.Attribute(owner, "title") ?? "", resource, NativeCssStyleSheets.DisabledOf(resource), resource.Sheet?.Stamp));
        }
        return result;
    }

    private CssValueWork Guard(CssValueWork work)
    {
        var stamp = document.MutationStamp;
        var revision = _revision;
        return CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (stamp == ulong.MaxValue || stamp != document.MutationStamp || !ReferenceEquals(revision, _revision))
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
    }

    private static void Verify(List<SheetState> sheets, CssValueWork work)
    {
        // Charge first and run the final callback before verifying. No callbacks can subsequently
        // mutate an already-verified member between this final verification and the commit/return.
        work.Charge(sheets.Count);
        work.CheckCancellation();
        foreach (var sheet in sheets)
            if (sheet.Disabled != NativeCssStyleSheets.DisabledOf(sheet.Resource) || sheet.Stamp != sheet.Resource.Sheet?.Stamp)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
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

internal sealed class NativeCssStyleSetList(NativeCssSheetSets sets)
{
    internal IReadOnlyList<string> Read(CssValueWork work) => sets.NamesOf(work);
}
