using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// Captured on the page loop. No engine, callback, revision scan, or read-time association in IsCurrent.
internal sealed class CssImportSource(Document document, Element owner, NativeCssStyleSheets.Resource resource)
{
    internal Document Document { get; } = document;
    internal Element Owner { get; } = owner;
    internal NativeCssStyleSheets.Resource Resource { get; } = resource;
    internal object SourceGeneration { get; } = resource.SourceGeneration;
    internal string Source { get; } = resource.Source;
    internal CssStyleSheetAttachment Attachment { get; } = resource.Attachment;
    internal CssStyleSheet? Sheet { get; private set; } = resource.Sheet;
    internal void Materialized(CssStyleSheet sheet) => Sheet = sheet;
}

internal sealed class CssImportSourceStaleException() : InvalidOperationException(NativeCssQuery.Invalidated);

// A charged composed-root proof is reusable only while the document and source stay unchanged.
// Parser checkpoints may inspect this witness, but never rebuild an ancestor walk.
internal sealed class CssImportConnectivity(CssImportSource source, ulong documentStamp, bool connected)
{
    internal bool Connected { get; } = connected;
    internal bool IsCurrent => documentStamp != ulong.MaxValue && source.Document.MutationStamp == documentStamp &&
        NativeCssStyleSheets.IsCurrent(source);
}

internal static partial class NativeCssStyleSheets
{
    // Called by the loader's owner-specific mutation-arrival lane before deferred delivery.
    // Equal final text does not revive an in-flight graph. No DOM walk, callback, or read-time
    // association: the caller identifies the exact owner and filters stale watched roots.
    internal static void InvalidateImportSourceAtArrival(Document document, Element owner)
    {
        if (!ReferenceEquals(owner.OwnerDocument, document) || !Documents.TryGetValue(document, out var resources) ||
            !resources.Owners.TryGetValue(owner, out var resource) || !resource.Loaded || !resource.Associated) return;
        var generation = new object();
        resource.SourceGeneration = generation;
    }

    internal static CssImportConnectivity CaptureImportConnectivity(CssImportSource source, CssValueWork work)
    {
        var guarded = CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (!IsCurrent(source)) throw new CssImportSourceStaleException();
        });
        while (true)
        {
            guarded.CheckCancellation();
            var stamp = source.Document.MutationStamp;
            if (stamp == ulong.MaxValue) throw new CssImportSourceStaleException();
            Node node = source.Owner;
            while ((node.ParentNode ?? (node as ShadowRoot)?.Host) is { } parent)
            {
                guarded.Charge(1);
                node = parent;
            }
            guarded.CheckCancellation();
            if (stamp != source.Document.MutationStamp) continue;
            if (!IsCurrent(source)) throw new CssImportSourceStaleException();
            guarded.Token.ThrowIfCancellationRequested();
            return new(source, stamp, ReferenceEquals(node, source.Document));
        }
    }

    internal static CssImportSource? CaptureImportSource(Document document, Element owner, CssValueWork work)
    {
        work.CheckCancellation();
        if (!ReferenceEquals(owner.OwnerDocument, document) || !Documents.TryGetValue(document, out var resources) ||
            !resources.Owners.TryGetValue(owner, out var resource) || !resource.Loaded || !resource.Associated) return null;
        return new(document, owner, resource);
    }

    internal static bool IsCurrent(CssImportSource source) =>
        ReferenceEquals(source.Owner.OwnerDocument, source.Document) &&
        Documents.TryGetValue(source.Document, out var resources) &&
        resources.Owners.TryGetValue(source.Owner, out var resource) && ReferenceEquals(resource, source.Resource) &&
        ReferenceEquals(resource.SourceGeneration, source.SourceGeneration) && resource.Loaded && resource.Associated &&
        (source.Sheet is null || ReferenceEquals(source.Sheet, resource.Sheet));

    internal static bool MayContainImport(CssImportSource source, CssValueWork work)
    {
        var guarded = CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (!IsCurrent(source)) throw new CssImportSourceStaleException();
        });
        guarded.CheckCancellation();
        var resource = source.Resource;
        if (ReferenceEquals(resource.ImportHintGeneration, source.SourceGeneration)) return resource.ImportHint;
        var result = CssImportPrelude.MayContainImport(source.Source, null, guarded, guarded.Token);
        guarded.CheckCancellation();
        if (!IsCurrent(source)) throw new CssImportSourceStaleException();
        resource.ImportHint = result;
        resource.ImportHintGeneration = source.SourceGeneration;
        return result;
    }
}
