using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>HTML form controls from the existing native owner and Browser FACE registry.</summary>
internal sealed class DomFormControlsCollection(DomRealm realm, Element form) : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<DomRealm, ConditionalWeakTable<Element, DomFormControlsCollection>> _views = new();
    internal Element Form => form;
    internal DomRealm Realm => realm;
    // Allocated only by a successful single-node form named getter, never by elements.
    private List<PastName>? _pastNames;
    private sealed record PastName(string Name, Element Element, ulong Revision);

    internal static DomFormControlsCollection Of(DomRealm realm, Element form)
    {
        var views = _views.GetValue(realm, static _ => new());
        return views.TryGetValue(form, out var existing) ? existing : Create(views, realm, form);
    }

    private static DomFormControlsCollection Create(ConditionalWeakTable<Element, DomFormControlsCollection> views,
        DomRealm realm, Element form) => views.GetValue(form, value => new(realm, value));

    internal override int Length => GetLength(realm);
    internal override int GetLength(DomRealm caller)
    {
        var count = 0;
        foreach (var unused in Read(caller)) count++;
        return count;
    }
    internal override Element? GetItem(DomRealm caller, uint index)
    {
        foreach (var element in Read(caller))
        {
            if (index == 0) return element;
            index--;
        }
        return null;
    }
    public override IEnumerator<Element> GetEnumerator() => Read(realm).GetEnumerator();
    internal override IEnumerable<Element> Read(DomRealm caller)
        => Elements(images: false, new(caller.NativeReadCheckpoint, caller.CancellationToken));

    internal override JsValue? GetNamedItem(DomRealm caller, string name) => NamedItem(caller, name);

    internal IEnumerable<Element> Elements(bool images, DomReadWork work)
    {
        work.Check();
        foreach (var element in HtmlFormOwner.ControlsOf(form, checkpoint: _ => work.Check(), token: work.Token))
        {
            work.Step();
            var eligible = images
                ? element is { NamespaceUri: Namespaces.Html, LocalName: "img" }
                : HtmlFormOwner.IsListed(element)
                  && !(element is { NamespaceUri: Namespaces.Html, LocalName: "input" }
                      && HtmlInputTypes.Parse(work.Attribute(element, "type")) == HtmlInputType.Image);
            if (!eligible) continue;
            work.Check();
            yield return element;
        }
        work.Check();
    }

    internal IEnumerable<Element> Matching(string name, bool images, DomReadWork work)
    {
        if (name.Length == 0) yield break;
        foreach (var element in Elements(images, work))
        {
            if (!work.Equal(work.Attribute(element, "id"), name) && !work.Equal(work.Attribute(element, "name"), name)) continue;
            work.Check();
            yield return element;
        }
        work.Check();
    }

    // HTML §4.10.3's form named getter and past names, over the one native owner history.
    internal JsValue? FormNamedItem(DomRealm caller, string name)
    {
        var work = new DomReadWork(caller.NativeReadCheckpoint, caller.CancellationToken);
        work.Check();
        PrunePast(work);
        for (var imagePass = 0; imagePass < 2; imagePass++)
        {
            using var matches = Matching(name, images: imagePass == 1, work).GetEnumerator();
            if (!matches.MoveNext()) continue;
            var first = matches.Current;
            if (matches.MoveNext()) return caller.Wrap(new DomRadioNodeList(this, name, images: imagePass == 1), DomManualInterfaces.RadioNodeList);
            Remember(name, first, work);
            return caller.WrapNode(first);
        }
        if (_pastNames is not null)
        {
            foreach (var entry in _pastNames)
            {
                work.Step();
                if (work.Equal(entry.Name, name)) { work.Check(); return caller.WrapNode(entry.Element); }
            }
        }
        work.Check();
        return null;
    }

    internal IReadOnlyList<string> FormNames(DomRealm caller)
    {
        var work = new DomReadWork(caller.NativeReadCheckpoint, caller.CancellationToken);
        work.Check();
        PrunePast(work);
        var names = new List<string>();
        var seen = new HashSet<string>(new BoundedNames(work));
        Dictionary<Element, List<PastName>>? pastByElement = null;
        if (_pastNames is not null)
        {
            pastByElement = new();
            foreach (var entry in _pastNames)
            {
                work.Step();
                if (!pastByElement.TryGetValue(entry.Element, out var entries))
                    pastByElement.Add(entry.Element, entries = []);
                entries.Add(entry);
            }
        }
        var root = work.Root(form);
        if (root is Element element) AddElement(element);
        foreach (var candidate in NodeTraversal.DescendantElements(root, work.Check, work.Token)) AddElement(candidate);
        work.Check();
        return names;

        void AddElement(Element candidate)
        {
            work.Step();
            if (!ReferenceEquals(HtmlFormOwner.Of(candidate), form)) return;
            if ((HtmlFormState.IsListed(candidate)
                    && !(candidate is { NamespaceUri: Namespaces.Html, LocalName: "input" }
                        && HtmlInputTypes.Parse(work.Attribute(candidate, "type")) == HtmlInputType.Image))
                || candidate is { NamespaceUri: Namespaces.Html, LocalName: "img" })
            {
                Add(work.Attribute(candidate, "id"));
                Add(work.Attribute(candidate, "name"));
            }
            if (pastByElement is null || !pastByElement.TryGetValue(candidate, out var past)) return;
            foreach (var entry in past)
            {
                work.Step();
                Add(entry.Name);
            }
        }
        void Add(string? name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (seen.Add(name)) names.Add(name);
        }
    }

    internal bool HasFormName(DomRealm caller, string name)
    {
        if (name.Length == 0) return false;
        var work = new DomReadWork(caller.NativeReadCheckpoint, caller.CancellationToken);
        work.Check();
        PrunePast(work);
        // A visibility probe neither realizes every name nor adds a past-name entry.
        for (var imagePass = 0; imagePass < 2; imagePass++)
        {
            using var matches = Matching(name, images: imagePass == 1, work).GetEnumerator();
            if (matches.MoveNext()) { work.Check(); return true; }
        }
        if (_pastNames is not null)
        {
            foreach (var entry in _pastNames)
            {
                work.Step();
                if (work.Equal(entry.Name, name)) { work.Check(); return true; }
            }
        }
        work.Check();
        return false;
    }

    private void PrunePast(DomReadWork work)
    {
        if (_pastNames is null) return;
        List<PastName>? retained = null;
        for (var index = 0; index < _pastNames.Count; index++)
        {
            work.Step();
            var entry = _pastNames[index];
            var state = entry.Element.FormAssociationState;
            var valid = state is not null && state.OwnerRevision != ulong.MaxValue
                && state.OwnerRevision == entry.Revision && ReferenceEquals(state.Owner, form);
            if (valid)
            {
                retained?.Add(entry);
            }
            else if (retained is null)
            {
                retained = new();
                for (var prefix = 0; prefix < index; prefix++)
                {
                    work.Step();
                    retained.Add(_pastNames[prefix]);
                }
            }
        }
        work.Check();
        // A canceled scan cannot publish a partially compacted map.
        if (retained is not null) _pastNames = retained;
    }

    private sealed class BoundedNames(DomReadWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? left, string? right)
            => ReferenceEquals(left, right) || right is not null && work.Equal(left, right);
        public int GetHashCode(string value)
        {
            var hash = new HashCode();
            foreach (var character in value)
            {
                work.Step();
                hash.Add(character);
            }
            return hash.ToHashCode();
        }
    }

    private void Remember(string name, Element element, DomReadWork work)
    {
        var state = element.FormAssociationState!;
        if (state.OwnerRevision == ulong.MaxValue) return;
        if (_pastNames is not null)
        {
            for (var index = 0; index < _pastNames.Count; index++)
            {
                work.Step();
                if (!work.Equal(_pastNames[index].Name, name)) continue;
                // Replacing a mapping resets its age, so the oldest entries stay first.
                work.Check();
                _pastNames.RemoveAt(index);
                break;
            }
        }
        work.Check();
        (_pastNames ??= []).Add(new(name, element, state.OwnerRevision));
    }

    // HTML §2.7.2.2: duplicate namedItem calls create new live RadioNodeList objects.
    internal JsValue NamedItem(DomRealm caller, string name)
    {
        var work = new DomReadWork(caller.NativeReadCheckpoint, caller.CancellationToken);
        work.Check();
        using var matches = Matching(name, images: false, work).GetEnumerator();
        if (!matches.MoveNext()) return JsValue.Null;
        var first = matches.Current;
        if (!matches.MoveNext()) return caller.WrapNode(first);
        return caller.Wrap(new DomRadioNodeList(this, name, images: false), DomManualInterfaces.RadioNodeList);
    }
}
