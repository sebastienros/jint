using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;

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
        if (!Documents.TryGetValue(document, out var resources)) return result;
        // DOM order, rather than load completion order, owns stylesheet order.
        var pending = new Stack<Node>();
        pending.Push(document);
        while (pending.TryPop(out var node))
        {
            work.Charge(1);
            if (node is Element element && resources.Owners.TryGetValue(element, out var entry))
            {
                if (entry.Sheet is null)
                {
                    entry.Sheet = CssStyleSheet.Parse(entry.Source, null, work, work.Token);
                    entry.Sheet.SetAttachment(entry.Attachment);
                    entry.Replaced = false;
                }
                else if (entry.Replaced)
                {
                    entry.Sheet.ReplaceText(entry.Source, null, work, work.Token);
                    entry.Sheet.SetAttachment(entry.Attachment);
                    entry.Replaced = false;
                }
                result.Add(new(entry.Sheet, NativeCssOrigin.Author));
            }
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling)
            {
                work.Charge(1);
                pending.Push(child);
            }
        }
        work.CheckCancellation();
        return result.AsReadOnly();
    }

    private sealed class Resources
    {
        internal ulong Version;
        internal Dictionary<Element, Resource> Owners { get; } = new();
    }
    private sealed class Resource(string source, CssStyleSheetAttachment attachment)
    {
        internal string Source = source;
        internal CssStyleSheetAttachment Attachment = attachment;
        internal CssStyleSheet? Sheet;
        internal bool Replaced;
    }
}
