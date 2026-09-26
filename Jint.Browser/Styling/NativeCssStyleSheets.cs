using System.Collections.ObjectModel;
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
    private static readonly ConditionalWeakTable<Element, LinkHistory> LinkHistories = new();
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
        string baseUrl, CssValueWork work, Document? verifiedRoot = null)
    {
        if (verifiedRoot is not null)
        {
            if (!ReferenceEquals(verifiedRoot, document)) throw new ArgumentException("A verified root belongs to another document.", nameof(verifiedRoot));
            var stamp = document.MutationStamp;
            var parentWork = work;
            work = CssValueWork.Guard(parentWork, () =>
            {
                parentWork.CheckCancellation();
                if (stamp == ulong.MaxValue || stamp != document.MutationStamp || !ReferenceEquals(owner.OwnerDocument, document))
                    throw new InvalidOperationException(NativeCssQuery.Invalidated);
            });
        }
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
        var generation = new object();
        if (resources.Owners.TryGetValue(owner, out var entry))
        {
            entry.Loaded = true;
            entry.Source = text;
            entry.Attachment = attachment;
            entry.SourceGeneration = generation;
            entry.Replaced = true;
        }
        else resources.Owners.Add(owner, new Resource(text, attachment) { SourceGeneration = generation });
        CssMutationStamp.Advance(ref resources.Version);
        AssociateOwner(document, owner, work, verifiedRoot);
        work.CheckCancellation();
    }

    internal static CssMutationStamp Stamp(Document document) =>
        Documents.TryGetValue(document, out var resources) ? new(resources.Version) : new(0);

    internal static ReadOnlyCollection<NativeCssSheet> Get(Document document, CssValueWork work, bool includeShadow = false)
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

    private static ReadOnlyCollection<NativeCssSheet> Get(Node root, Document document, CssValueWork work, bool includeShadow)
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
                if (!resources.Owners.TryGetValue(element, out var resource) || !resource.Associated || !resource.Loaded)
                    continue;
                var ownerWork = new DomReadWork(work.Charge, work.Token);
                if (!EligibleOwner(element, ownerWork, work)) continue;
                var source = CaptureImportSource(document, element, parsing)
                    ?? throw new InvalidOperationException(NativeCssQuery.Invalidated);
                result.Add(new(EnsureSheet(source, parsing), NativeCssOrigin.Author));
            }
        }
        Verify();
        return result.AsReadOnly();
    }

    // Queries and the import loader share the actual model. Source-only installation remains lazy.
    internal static CssStyleSheet EnsureSheet(CssImportSource source, CssValueWork work)
    {
        var expectedSheet = source.Resource.Sheet;
        var guarded = CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (!IsCurrent(source) || !ReferenceEquals(source.Resource.Sheet, expectedSheet))
                throw new CssImportSourceStaleException();
        });
        guarded.CheckCancellation();
        var resource = source.Resource;
        var cold = resource.Sheet is null;
        var sheet = resource.Sheet ?? CssStyleSheet.Parse(source.Source, null, guarded, guarded.Token);
        if (!cold && resource.Replaced)
        {
            sheet.ReplaceText(source.Source, null, guarded, guarded.Token);
            // ReplaceText already passed its final guarded callback and committed. Record that
            // progress before any fallible metadata read, so recovery cannot replay the source
            // over a CSSOM edit made by a later callback on this same sheet.
            if (!IsCurrent(source) || !ReferenceEquals(resource.Sheet, sheet)) throw new CssImportSourceStaleException();
            sheet.SetAttachment(source.Attachment);
            resource.Replaced = false;
            source.Materialized(sheet);
        }
        while (true)
        {
            // Source identity does not change for mutable owner metadata. Read media under a
            // document witness and retry an intervening DOM write before committing that text.
            var documentStamp = source.Document.MutationStamp;
            if (documentStamp == ulong.MaxValue) throw new CssImportSourceStaleException();
            var metadataWork = CssValueWork.Guard(guarded, () =>
            {
                guarded.CheckCancellation();
                if (documentStamp != source.Document.MutationStamp) throw new CssImportMetadataChangedException();
            });
            string media;
            try
            {
                metadataWork.CheckCancellation();
                media = new DomReadWork(metadataWork.Charge, metadataWork.Token).Attribute(source.Owner, "media") ?? "";
                if (resource.MediaSource is null || !CssSubstitutionArguments.Equals(resource.MediaSource, media, metadataWork))
                {
                    var mediaStamp = sheet.Media.Stamp;
                    var producerWork = CssValueWork.Guard(metadataWork, () =>
                    {
                        metadataWork.CheckCancellation();
                        if (!mediaStamp.CanReuse || sheet.Media.Stamp != mediaStamp) throw new CssImportMediaChangedException();
                    });
                    try { sheet.Media.SetMediaText(media, null, producerWork, producerWork.Token); }
                    catch (CssImportMediaChangedException)
                    {
                        // An existing sheet's reentrant CSSOM media write wins over this earlier
                        // owner read. A private candidate cannot receive such a write.
                        if (cold) throw new CssImportSourceStaleException();
                    }
                }
                // Media's own producer has now committed. A later CSSOM edit is authoritative;
                // only another DOM metadata write requires rereading the attribute.
                guarded.CheckCancellation();
                if (documentStamp != source.Document.MutationStamp) continue;
            }
            catch (CssImportMetadataChangedException) { continue; }
            if (!IsCurrent(source)) throw new CssImportSourceStaleException();
            guarded.Token.ThrowIfCancellationRequested();
            // Callback-free publication: disabled is still the resource's authority while cold.
            // Attachment is immutable for this generation; media was proved against its DOM read.
            sheet.SetAttachment(source.Attachment);
            if (cold)
            {
                sheet.Disabled = resource.Disabled;
                resource.Sheet = sheet;
                expectedSheet = sheet;
            }
            source.Materialized(sheet);
            resource.Replaced = false;
            resource.MediaSource = media;
            return sheet;
        }
    }

    private sealed class CssImportMetadataChangedException : Exception { }
    private sealed class CssImportMediaChangedException : Exception { }

    private static void ObserveDisabled(Element owner)
    {
        if (owner.LocalName != "link" || owner.NamespaceUri != Namespaces.Html) return;
        var history = LinkHistories.GetValue(owner, static _ => new());
        if (history.Subscription is not null) return;
        var subscription = new MutationSubscription();
        subscription.Observe(owner, new MutationObserverOptions { Attributes = true, AttributeFilter = ["disabled"] });
        subscription.PendingRecord = pending =>
        {
            // Native mutation matching enqueues every subscription before notifying any of them.
            // Drain every frozen transition: a removal remains history even if a later add wins.
            foreach (var record in pending.TakeRecords())
            {
                if (record.AttributeNamespace is null && record.AttributeName == "disabled")
                    ApplyDisabledTransition(owner, record.AttributeNewValue);
            }
        };
        history.Subscription = subscription;
    }

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
    private sealed class LinkHistory
    {
        internal bool ExplicitlyEnabled;
        internal MutationSubscription? Subscription;
    }
    internal sealed class Resource(string source, CssStyleSheetAttachment attachment)
    {
        internal string Source = source;
        internal CssStyleSheetAttachment Attachment = attachment;
        internal CssStyleSheet? Sheet;
        internal object SourceGeneration = new();
        internal object? ImportHintGeneration;
        internal bool ImportHint;
        internal bool Replaced;
        internal string? MediaSource;
        internal bool Disabled;
        internal bool Associated;
        internal bool Loaded = true;
        private readonly LinkHistory? _history = attachment.OwnerNode is Element { NamespaceUri: Namespaces.Html, LocalName: "link" } owner
            ? LinkHistories.GetValue(owner, static _ => new()) : null;
        internal bool ExplicitlyEnabled => _history?.ExplicitlyEnabled ?? false;
    }
}
