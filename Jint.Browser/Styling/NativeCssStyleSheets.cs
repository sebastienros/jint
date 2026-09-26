using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Browser.Styling;

// HTML §4.2.6 link/style processing: the loader owns fetch and parser scheduling.
// Installation records source only. CSS syntax/validation is demanded by styling or CSSOM.
internal static partial class NativeCssStyleSheets
{
    private static readonly ConditionalWeakTable<Document, Resources> Documents = new();
    private static readonly ConditionalWeakTable<Element, InlineResource> InlineSources = new();

    internal static CssDeclarationBlock InlineOf(Element element, CssValueWork work)
    {
        var document = element.OwnerDocument;
        var stamp = document?.MutationStamp;
        var guarded = CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (!ReferenceEquals(element.OwnerDocument, document) || stamp == ulong.MaxValue || document?.MutationStamp != stamp)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
        guarded.CheckCancellation();
        var source = new DomReadWork(guarded.Charge, guarded.Token).Attribute(element, "style") ?? "";
        var resource = InlineResourceOf(element);
        if (resource.Block is null || resource.PublishedVersion != resource.Version || !CssSubstitutionArguments.Equals(resource.Source!, source, guarded))
        {
            var block = CssDeclarationBlock.ParseUnresolved(source, CssDeclarationContext.Style, null, guarded, guarded.Token);
            guarded.CheckCancellation();
            resource.Source = source;
            resource.Block = block;
            resource.PublishedVersion = resource.Version;
        }
        guarded.CheckCancellation();
        return resource.Block;
    }

    private static InlineResource InlineResourceOf(Element element) => InlineSources.GetValue(element, static owner =>
    {
        var resource = new InlineResource();
        var subscription = new MutationSubscription();
        subscription.Observe(owner, new MutationObserverOptions { Attributes = true, AttributeFilter = ["style"] });
        subscription.PendingRecord = pending =>
        {
            // Constant work on the native mutation stack, with no retained records or script callback.
            pending.TakeRecords();
            resource.Block = null;
            if (resource.Version != ulong.MaxValue) resource.Version++;
        };
        resource.Subscription = subscription;
        return resource;
    });

    internal static ulong InlineVersion(Element element)
    {
        var version = InlineResourceOf(element).Version;
        if (version == ulong.MaxValue) throw new InvalidOperationException(NativeCssQuery.Invalidated);
        return version;
    }

    internal static void RetainInline(Element element, string source, CssDeclarationBlock block,
        ulong beforeWrite, CssValueWork work)
    {
        // Publication follows this writer's single observed attribute mutation. No callback may
        // run between the native source commit and retaining its authoritative declaration block.
        var resource = InlineResourceOf(element);
        if (beforeWrite == ulong.MaxValue || resource.Version != beforeWrite + 1)
            throw new InvalidOperationException(NativeCssQuery.Invalidated);
        resource.Source = source;
        resource.Block = block;
        resource.PublishedVersion = resource.Version;
        work.CheckCancellation();
    }

    private sealed class InlineResource
    {
        internal string? Source;
        internal CssDeclarationBlock? Block;
        internal MutationSubscription? Subscription;
        internal ulong Version;
        internal ulong PublishedVersion;
    }

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
        AssociateOwner(document, owner, work);
        work.CheckCancellation();
    }

    internal static CssMutationStamp Stamp(Document document) =>
        Documents.TryGetValue(document, out var resources) ? new(resources.Version) : new(0);

    internal static IReadOnlyList<NativeCssSheet> Get(Document document, CssValueWork work, bool includeShadow = false)
    {
        return Get(document, document, work, includeShadow);
    }

    internal static IReadOnlyList<NativeCssSheet> Get(ShadowRoot root, CssValueWork work)
    {
        for (Node? node = root.Host; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
        {
            work.Charge(1);
            if (node is Document document) return Get(root, document, work, includeShadow: false);
        }
        work.CheckCancellation();
        return Array.Empty<NativeCssSheet>();
    }

    private static IReadOnlyList<NativeCssSheet> Get(Node root, Document document, CssValueWork work, bool includeShadow)
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
        var parsing = CssValueWork.Guard(work, Verify);
        // DOM order, rather than load completion order, owns stylesheet order.
        var pending = new Stack<Node>();
        pending.Push(root);
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
                var embedded = element.LocalName == "style" && element.NamespaceUri is Namespaces.Html or Namespaces.Svg;
                if (element.NamespaceUri == Namespaces.Html && element.LocalName == "link" &&
                    !HasStyleSheetRelation(ownerWork.Attribute(element, "rel"), work))
                    continue;
                if ((embedded || element.NamespaceUri == Namespaces.Html && element.LocalName == "link") &&
                    !string.IsNullOrEmpty(type) && !ownerWork.EqualAsciiIgnoreCase(type, "text/css"))
                    continue;
                var known = resources.Owners.TryGetValue(element, out var entry);
                if (embedded &&
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
                if (known && entry is not null) AssociateOwner(document, element, work);
                if (known && entry is { } resource)
                {
                    if (resource.Sheet is null)
                    {
                        var sheet = CssStyleSheet.Parse(resource.Source, null, parsing, work.Token);
                        sheet.SetAttachment(resource.Attachment);
                        Verify();
                        sheet.Disabled = resource.Disabled;
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
                    if (element.NamespaceUri == Namespaces.Html && element.LocalName == "link")
                    {
                        var disabled = ownerWork.Attribute(element, "disabled") is not null;
                        if (resource.DisabledDirty || resource.DisabledSource != disabled)
                        {
                            Verify();
                            resource.Sheet.Disabled = disabled;
                            resource.DisabledSource = disabled;
                            resource.DisabledDirty = false;
                        }
                    }
                    ObserveDisabled(element, resource);
                    result.Add(new(resource.Sheet, NativeCssOrigin.Author));
                }
            }
        }
        Verify();
        return result.AsReadOnly();
    }

    private static void ObserveDisabled(Element owner, Resource resource)
    {
        if (owner.LocalName != "link" || owner.NamespaceUri != Namespaces.Html || resource.DisabledSubscription is not null) return;
        var subscription = new MutationSubscription();
        subscription.Observe(owner, new MutationObserverOptions { Attributes = true, AttributeFilter = ["disabled"] });
        subscription.PendingRecord = pending => { pending.TakeRecords(); resource.DisabledDirty = true; };
        resource.DisabledSubscription = subscription;
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
        internal NativeCssSheetSets? Sets;
        internal ConditionalWeakTable<Element, Resource> Owners { get; } = new();
    }
    internal sealed class Resource(string source, CssStyleSheetAttachment attachment)
    {
        internal string Source = source;
        internal CssStyleSheetAttachment Attachment = attachment;
        internal CssStyleSheet? Sheet;
        internal bool Replaced;
        internal ulong? NativeStamp;
        internal string? MediaSource;
        internal bool? DisabledSource;
        internal bool DisabledDirty;
        internal bool Disabled;
        internal bool Associated;
        internal MutationSubscription? DisabledSubscription;
    }
}
