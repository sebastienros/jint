using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Browser-owned association between a browsing context and its active native document.</summary>
/// <remarks>
/// HTML §7.3: a DOMParser result or a manufactured document has no browsing context. The
/// native DOM therefore carries no context field. This table is populated only when the page
/// runtime commits a document or creates a child context, and retains the previous document's
/// association without treating that document as active after replacement.
/// </remarks>
internal sealed class DomBrowsingContext : IDisposable
{
    private static readonly ConditionalWeakTable<Document, DomBrowsingContext> Documents = new();
    private static readonly ConditionalWeakTable<Element, DomBrowsingContext> Frames = new();

    internal DomBrowsingContext(Document document, DomBrowsingContext? parent = null, Element? frameElement = null)
    {
        Parent = parent;
        FrameElement = frameElement;
        if (frameElement is not null)
        {
            Frames.Add(frameElement, this);
        }
        Activate(document);
    }

    internal Document? Active { get; private set; }

    internal DomBrowsingContext? Parent { get; }

    internal Element? FrameElement { get; }

    internal void Activate(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Documents.TryGetValue(document, out var existing) && !ReferenceEquals(existing, this))
        {
            throw new ArgumentException("The document is associated with another browsing context.", nameof(document));
        }
        Documents.GetValue(document, _ => this);
        Active = document;
    }

    /// <summary>The context whose active document is the supplied document, or null for an inert document.</summary>
    internal static DomBrowsingContext? Of(Document? document)
        => document is not null && Documents.TryGetValue(document, out var context) && ReferenceEquals(context.Active, document)
            ? context
            : null;

    internal static DomBrowsingContext? OfFrame(Element frame)
        => Frames.TryGetValue(frame, out var context) ? context : null;

    public void Dispose()
    {
        Active = null;
        if (FrameElement is not null)
        {
            Frames.Remove(FrameElement);
        }
    }
}
