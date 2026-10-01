using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>HTML §4.9 table operations over native child links, without a layout or grid model.</summary>
internal static class DomTableMembers
{
    // https://html.spec.whatwg.org/multipage/tables.html#htmltableelement
    internal static Element? Part(DomRealm realm, Element table, string name)
    {
        var work = Work(realm);
        foreach (var child in Children(table, work))
            if (Is(child, name)) { work.Check(); return child; }
        work.Check();
        return null;
    }

    internal static JsValue SetPart(DomRealm realm, Element table, string name, Element? value)
    {
        if (value is not null && !Is(value, name))
            throw new DomException("HierarchyRequestError", "The section has the wrong HTML local name.");
        DeletePart(realm, table, name);
        if (value is not null)
        {
            realm.RecordSubtree(value);
            table.InsertBefore(value, Reference(realm, table, name));
        }
        return JsValue.Undefined;
    }

    internal static Element CreatePart(DomRealm realm, Element table, string name)
    {
        if (name != "tbody" && Part(realm, table, name) is { } existing) return existing;
        var created = table.OwnerDocument!.CreateElementNS(Namespaces.Html, name);
        table.InsertBefore(created, Reference(realm, table, name));
        return created;
    }

    internal static JsValue DeletePart(DomRealm realm, Element table, string name)
    {
        if (Part(realm, table, name) is { } existing) table.RemoveChild(existing);
        return JsValue.Undefined;
    }

    private static Node? Reference(DomRealm realm, Element table, string name)
    {
        if (name == "caption") return table.FirstChild;
        if (name == "tfoot") return null;
        var work = Work(realm);
        Node? reference = null;
        foreach (var child in Children(table, work))
        {
            if (name == "thead" && !Is(child, "caption") && !Is(child, "colgroup"))
            { reference = child; break; }
            if (name == "tbody" && Is(child, "tbody")) reference = child.NextSibling;
        }
        work.Check();
        return reference;
    }

    internal static Element Insert(DomRealm realm, Element parent, int index, bool cell)
    {
        var items = DomTableCollection.Of(realm, parent, cell ? TableCollectionKind.Cells : TableCollectionKind.Rows).ToList();
        if (index < -1 || index > items.Count) throw IndexError();
        var created = parent.OwnerDocument!.CreateElementNS(Namespaces.Html, cell ? "td" : "tr");
        var before = index == -1 || index == items.Count ? null : items[index];
        Node destination = parent;
        if (Is(parent, "table"))
        {
            if (items.Count > 0) destination = (before ?? items[^1]).ParentNode!;
            else
            {
                Element? body = null;
                var work = Work(realm);
                foreach (var child in Children(parent, work)) if (Is(child, "tbody")) body = child;
                work.Check();
                if (body is null)
                {
                    body = parent.OwnerDocument.CreateElementNS(Namespaces.Html, "tbody");
                    body.AppendChild(created);
                    parent.AppendChild(body);
                    return created;
                }
                destination = body;
            }
        }
        destination.InsertBefore(created, before);
        return created;
    }

    internal static JsValue Delete(DomRealm realm, Element parent, int index, bool cell)
    {
        var items = DomTableCollection.Of(realm, parent, cell ? TableCollectionKind.Cells : TableCollectionKind.Rows).ToList();
        if (index == -1)
        {
            if (items.Count == 0) return JsValue.Undefined;
            index = items.Count - 1;
        }
        if ((uint) index >= (uint) items.Count) throw IndexError();
        var item = items[index];
        item.ParentNode!.RemoveChild(item);
        return JsValue.Undefined;
    }

    internal static int Index(DomRealm realm, Element element, bool section)
    {
        if (element.ParentNode is not Element parent) return -1;
        var cell = Is(element, "td") || Is(element, "th");
        if (cell && !Is(parent, "tr")) return -1;
        if (!cell && !Is(parent, "table") && !Is(parent, "thead") && !Is(parent, "tbody") && !Is(parent, "tfoot")) return -1;
        if (!cell && !section && !Is(parent, "table"))
        {
            if (parent.ParentNode is not Element table || !Is(table, "table")) return -1;
            parent = table;
        }
        var index = 0;
        foreach (var item in DomTableCollection.Of(realm, parent, cell ? TableCollectionKind.Cells : TableCollectionKind.Rows))
        {
            if (ReferenceEquals(element, item)) return index;
            index++;
        }
        return -1;
    }

    internal static bool Is(Element element, string name) => element.NamespaceUri == Namespaces.Html && element.LocalName == name;
    internal static DomReadWork Work(DomRealm realm) => new(realm.NativeReadCheckpoint, realm.CancellationToken);
    internal static IEnumerable<Element> Children(Element parent, DomReadWork work)
    {
        for (var child = parent.FirstChild; child is not null; child = child.NextSibling)
        {
            work.Step();
            if (child is Element element) yield return element;
        }
    }
    private static DomException IndexError() => new("IndexSizeError", "The table index is outside the collection.");
}

internal enum TableCollectionKind { Rows, Bodies, Cells }

/// <summary>The existing HTMLCollection wrapper reads these live native views.</summary>
internal sealed class DomTableCollection(DomRealm realm, Element root, TableCollectionKind kind) : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<DomRealm, ConditionalWeakTable<Element, Views>> _views = new();
    private sealed class Views
    {
        internal readonly DomTableCollection?[] Collections = new DomTableCollection?[3];
    }
    internal static DomTableCollection Of(DomRealm realm, Element root, TableCollectionKind kind)
        => _views.GetValue(realm, static _ => new()).GetValue(root, static _ => new()).Collections[(int) kind] ??= new(realm, root, kind);

    internal override int Length
    {
        get
        {
            var count = 0;
            foreach (var _ in this) count++;
            return count;
        }
    }

    public override IEnumerator<Element> GetEnumerator()
    {
        var work = DomTableMembers.Work(realm);
        work.Check();
        var tableRows = kind == TableCollectionKind.Rows && DomTableMembers.Is(root, "table");
        for (var pass = 0; pass < (tableRows ? 3 : 1); pass++)
            foreach (var child in DomTableMembers.Children(root, work))
            {
                if (kind == TableCollectionKind.Bodies && DomTableMembers.Is(child, "tbody") ||
                    kind == TableCollectionKind.Cells && (DomTableMembers.Is(child, "td") || DomTableMembers.Is(child, "th")) ||
                    kind == TableCollectionKind.Rows && DomTableMembers.Is(child, "tr") && (!tableRows || pass == 1))
                    yield return child;
                if (!tableRows || !DomTableMembers.Is(child, pass switch { 0 => "thead", 1 => "tbody", _ => "tfoot" })) continue;
                foreach (var row in DomTableMembers.Children(child, work))
                    if (DomTableMembers.Is(row, "tr")) yield return row;
            }
        work.Check();
    }
}
