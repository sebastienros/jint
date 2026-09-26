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

internal static partial class NativeCssStyleSheets
{
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
