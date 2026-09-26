using System.Runtime.CompilerServices;
using Jint.HtmlParser;
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

    internal static IReadOnlyList<NativeCssSheet> Get(Document document, CssValueWork work)
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
        // DOM order, rather than load completion order, owns stylesheet order.
        var pending = new Stack<Node>();
        pending.Push(document);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            if (node is Element element)
            {
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
                        var sheet = CssStyleSheet.Parse(resource.Source, null, work, work.Token);
                        sheet.SetAttachment(resource.Attachment);
                        Verify();
                        resource.Sheet = sheet;
                        resource.Replaced = false;
                    }
                    else if (resource.Replaced)
                    {
                        resource.Sheet.ReplaceText(resource.Source, null, work, work.Token);
                        Verify();
                        resource.Sheet.SetAttachment(resource.Attachment);
                        resource.Replaced = false;
                    }
                    result.Add(new(resource.Sheet, NativeCssOrigin.Author));
                }
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Charge(1);
                pending.Push(child);
            }
        }
        Verify();
        return result.AsReadOnly();
    }

    private static string ReadText(Element owner, CssValueWork work)
    {
        var text = new System.Text.StringBuilder();
        var pending = new Stack<Node>();
        pending.Push(owner);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            if (node is Text data)
                for (var i = 0; i < data.DataLength; i++)
                {
                    work.Charge(1);
                    text.Append(data.DataAt(i));
                }
            else if (node is CDataSection section)
            {
                var value = section.Data;
                for (var i = 0; i < value.Length; i++)
                {
                    work.Charge(1);
                    text.Append(value[i]);
                }
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Charge(1);
                pending.Push(child);
            }
        }
        work.CheckCancellation();
        var result = text.ToString();
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
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
    }
}
