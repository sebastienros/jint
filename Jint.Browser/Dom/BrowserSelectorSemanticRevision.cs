using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Browser-owned facts that can change without a native tree mutation.</summary>
internal static class BrowserSelectorSemanticRevision
{
    private static readonly ConditionalWeakTable<Document, Revision> Revisions = new();

    // A selector read on an untouched document neither allocates nor retains an engine.
    internal static ulong Read(Document document)
        => Revisions.TryGetValue(document, out var revision) ? revision.Value : 0;

    internal static void Advance(Document document)
    {
        var revision = Revisions.GetOrCreateValue(document);
        // Saturation is permanent: no consumer may reuse a view captured at this value.
        if (revision.Value != ulong.MaxValue) revision.Value++;
    }

    private sealed class Revision
    {
        internal ulong Value;
    }
}
