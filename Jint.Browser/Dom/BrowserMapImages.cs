using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>The live, SameObject image view for a native map, through the existing HTMLCollection wrapper.</summary>
internal sealed class BrowserMapImages(Element map) : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<Element, BrowserMapImages> Views = new();
    internal static BrowserMapImages Of(Element map) => Views.GetValue(map, static target => new(target));

    internal override int Length => Count(Matches(new DomReadWork(null, default)));
    internal override int GetLength(DomRealm realm) => Count(Read(realm));
    internal override Element? GetItem(DomRealm realm, uint index)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        Element? result = null;
        foreach (var image in Matches(work))
        {
            if (index-- != 0) continue;
            result = image;
            break;
        }
        work.Check();
        return result;
    }
    internal override IEnumerable<Element> Read(DomRealm realm)
        => Matches(new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken));
    public override IEnumerator<Element> GetEnumerator() => Matches(new DomReadWork(null, default)).GetEnumerator();

    // HTML §4.8.13 + common microsyntaxes' hash-name reference: literal suffix of the first '#'.
    private IEnumerable<Element> Matches(DomReadWork work)
    {
        work.Check();
        var root = work.Root(map);
        var id = work.Attribute(map, "id");
        var name = work.Attribute(map, "name");
        var idWins = !string.IsNullOrEmpty(id);
        var nameWins = !string.IsNullOrEmpty(name);
        // Resolve the map's two possible names once; matching each img never rescans the map tree.
        foreach (var candidate in Elements(root, work))
        {
            if (ReferenceEquals(candidate, map)) break;
            if (candidate.NamespaceUri != Namespaces.Html || candidate.LocalName != "map") continue;
            var candidateId = work.Attribute(candidate, "id");
            var candidateName = work.Attribute(candidate, "name");
            if (idWins && (work.Equal(candidateId, id!) || work.Equal(candidateName, id!))) idWins = false;
            if (nameWins && (work.Equal(candidateId, name!) || work.Equal(candidateName, name!))) nameWins = false;
        }
        if (idWins || nameWins)
        {
            foreach (var candidate in Elements(root, work))
            {
                if (candidate.NamespaceUri != Namespaces.Html || candidate.LocalName != "img") continue;
                var reference = HashName(work.Attribute(candidate, "usemap"), work);
                if (reference is { } suffix && (idWins && suffix.Matches(id!, work) || nameWins && suffix.Matches(name!, work)))
                    yield return candidate;
            }
        }
        work.Check();
    }

    private static IEnumerable<Element> Elements(Node root, DomReadWork work)
    {
        work.Step();
        if (root is Element element) yield return element;
        foreach (var descendant in NodeTraversal.DescendantElements(root, work.Check, work.Token)) yield return descendant;
    }

    internal static HashNameReference? HashName(string? value, DomReadWork work)
    {
        if (value is null) return null;
        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            if (value[i] != '#') continue;
            return i + 1 < value.Length ? new HashNameReference(value, i + 1) : null;
        }
        return null;
    }

    internal readonly struct HashNameReference(string source, int start)
    {
        internal bool Matches(string name, DomReadWork work)
        {
            if (source.Length - start != name.Length) return false;
            for (var i = 0; i < name.Length; i++)
            {
                work.Step();
                if (source[start + i] != name[i]) return false;
            }
            return true;
        }
    }

    private static int Count(IEnumerable<Element> images)
    {
        var count = 0;
        foreach (var unused in images) count++;
        return count;
    }
}
