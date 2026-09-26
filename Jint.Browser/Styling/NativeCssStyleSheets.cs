using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

// HTML §4.2.6 link/style processing: the loader owns fetch and parser scheduling.
// Installation records source only. CSS syntax/validation is demanded by styling or CSSOM.
internal static partial class NativeCssStyleSheets
{
    private static readonly ConditionalWeakTable<Document, Resources> Documents = new();

    internal static void Install(Document document, Element owner, string text, string sourceUrl,
        string baseUrl, CssValueWork work)
    {
        if (!ReferenceEquals(owner.OwnerDocument, document))
            throw new ArgumentException("A stylesheet owner belongs to another document.", nameof(owner));
        work.CheckCancellation();
        work.Charge(text.Length);
        work.Charge(sourceUrl.Length);
        work.Charge(baseUrl.Length);
        var resources = Documents.GetValue(document, static _ => new Resources());
        var source = Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ? uri : null;
        var attachment = new CssStyleSheetAttachment
        {
            OwnerNode = owner,
            SourceUrl = source,
            BaseUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ? baseUri : source
        };
        if (resources.Owners.TryGetValue(owner, out var entry))
        {
            entry.Source = text;
            entry.Attachment = attachment;
            entry.Replaced = true;
            entry.NativeStamp = null;
        }
        else resources.Owners.Add(owner, new Resource(text, attachment));
        CssMutationStamp.Advance(ref resources.Version);
        work.CheckCancellation();
    }

    internal static CssMutationStamp Stamp(Document document) =>
        Documents.TryGetValue(document, out var resources) ? new(resources.Version) : new(0);

    internal static IReadOnlyList<NativeCssSheet> Get(Document document, CssValueWork work, bool includeShadow = false)
    {
        var result = new List<NativeCssSheet>();
        var resources = Documents.GetValue(document, static _ => new Resources());
        var revision = new CssMutationStamp(resources.Version);
        var documentStamp = document.MutationStamp;
        void Verify()
        {
            work.CheckCancellation();
            if (!revision.CanReuse || resources.Version != revision.Value ||
                documentStamp == ulong.MaxValue || document.MutationStamp != documentStamp)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        }
        Verify();
        var parsing = new CssValueWork(work.Token, Verify);
        // DOM order, rather than load completion order, owns stylesheet order.
        var pending = new Stack<Node>();
        pending.Push(document);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            if (includeShadow && node is Element { AttachedShadowRoot: { } shadow }) pending.Push(shadow);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Charge(1);
                pending.Push(child);
            }
            if (node is Element element)
            {
                var ownerWork = new DomReadWork(work.Charge, work.Token);
                var type = ownerWork.Attribute(element, "type");
                if (element.NamespaceUri == Namespaces.Html && element.LocalName == "link" &&
                    !HasStyleSheetRelation(ownerWork.Attribute(element, "rel"), work))
                    continue;
                if (element.NamespaceUri == Namespaces.Html && element.LocalName is "style" or "link" &&
                    !string.IsNullOrEmpty(type) && !ownerWork.EqualAsciiIgnoreCase(type, "text/css"))
                    continue;
                var known = resources.Owners.TryGetValue(element, out var entry);
                if (element.NamespaceUri == Namespaces.Html && element.LocalName == "style" &&
                    (!known || entry!.NativeStamp != documentStamp))
                {
                    var text = ReadText(element, work);
                    Verify();
                    if (!known)
                    {
                        entry = new Resource(text, new CssStyleSheetAttachment { OwnerNode = element });
                        resources.Owners.Add(element, entry);
                        CssMutationStamp.Advance(ref resources.Version);
                        revision = new(resources.Version);
                    }
                    else if (!CssSubstitutionArguments.Equals(entry!.Source, text, work))
                    {
                        Verify();
                        entry.Source = text;
                        entry.Replaced = true;
                        CssMutationStamp.Advance(ref resources.Version);
                        revision = new(resources.Version);
                    }
                    entry!.NativeStamp = documentStamp;
                    known = true;
                }
                if (known && entry is { } resource)
                {
                    if (resource.Sheet is null)
                    {
                        var sheet = CssStyleSheet.Parse(resource.Source, null, parsing, work.Token);
                        sheet.SetAttachment(resource.Attachment);
                        Verify();
                        resource.Sheet = sheet;
                        resource.Replaced = false;
                    }
                    else if (resource.Replaced)
                    {
                        resource.Sheet.ReplaceText(resource.Source, null, parsing, work.Token);
                        Verify();
                        resource.Sheet.SetAttachment(resource.Attachment);
                        resource.Replaced = false;
                    }
                    var media = ownerWork.Attribute(element, "media") ?? "";
                    if (resource.MediaSource is null || !CssSubstitutionArguments.Equals(resource.MediaSource, media, work))
                    {
                        resource.Sheet.Media.SetMediaText(media, null, parsing, work.Token);
                        Verify();
                        resource.MediaSource = media;
                    }
                    result.Add(new(resource.Sheet, NativeCssOrigin.Author));
                }
            }
        }
        Verify();
        return result.AsReadOnly();
    }

    private static string ReadText(Element owner, CssValueWork work) =>
        DomDescendantText.Read(owner, work.Charge, work.Token);

    // HTML's space-separated rel tokens use ASCII case-insensitive matching.
    private static bool HasStyleSheetRelation(string? value, CssValueWork work)
    {
        if (value is null) return false;
        const string expected = "stylesheet";
        var start = 0;
        for (var end = 0; end <= value.Length; end++)
        {
            work.Charge(1);
            if (end != value.Length && value[end] is not (' ' or '\t' or '\n' or '\r' or '\f')) continue;
            if (end - start == expected.Length)
            {
                var matches = true;
                for (var i = 0; i < expected.Length; i++)
                {
                    work.Charge(1);
                    var c = value[start + i];
                    if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
                    if (c != expected[i]) { matches = false; break; }
                }
                if (matches) return true;
            }
            start = end + 1;
        }
        return false;
    }

    private sealed class Resources
    {
        internal ulong Version;
        internal ConditionalWeakTable<Element, Resource> Owners { get; } = new();
    }
    private sealed class Resource(string source, CssStyleSheetAttachment attachment)
    {
        internal string Source = source;
        internal CssStyleSheetAttachment Attachment = attachment;
        internal CssStyleSheet? Sheet;
        internal bool Replaced;
        internal ulong? NativeStamp;
        internal string? MediaSource;
    }
}
