using System.Runtime.CompilerServices;
using Jint.Browser.CustomElements;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

internal enum DomHtmlElementCollectionKind { DatalistOptions, MapAreas, FieldsetControls }

/// <summary>Live HTML element collections derived only when observed from the native light tree.</summary>
internal sealed class DomHtmlElementCollection(DomRealm realm, Element root, DomHtmlElementCollectionKind kind)
    : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<DomRealm, ConditionalWeakTable<Element, Views>> _views = new();
    private sealed class Views
    {
        internal readonly DomHtmlElementCollection?[] Collections = new DomHtmlElementCollection?[3];
    }

    internal static DomHtmlElementCollection Of(DomRealm realm, Element root, DomHtmlElementCollectionKind kind)
    {
        var views = _views.GetValue(realm, static _ => new()).GetValue(root, static _ => new());
        return views.Collections[(int) kind] ??= new(realm, root, kind);
    }

    internal override int Length
    {
        get
        {
            var count = 0;
            foreach (var unused in this) count++;
            return count;
        }
    }

    public override IEnumerator<Element> GetEnumerator()
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        var registry = CustomElementRegistry.Of(realm.Engine);
        work.Check();
        foreach (var element in NodeTraversal.DescendantElements(root, work.Check, work.Token))
        {
            work.Step();
            var matches = kind switch
            {
                DomHtmlElementCollectionKind.DatalistOptions => element is { NamespaceUri: Namespaces.Html, LocalName: "option" },
                DomHtmlElementCollectionKind.MapAreas => element is { NamespaceUri: Namespaces.Html, LocalName: "area" },
                DomHtmlElementCollectionKind.FieldsetControls => HtmlFormOwner.IsListed(element)
                    || registry?.TryGetRecord(element) is { State: CustomElementState.Custom, FormAssociated: true },
                _ => throw new InvalidOperationException("Unknown HTML collection kind."),
            };
            if (matches)
            {
                work.Check();
                yield return element;
            }
        }
        work.Check();
    }
}
