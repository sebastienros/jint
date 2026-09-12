using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The membership test of a <see cref="DomLiveHtmlCollection"/> — the "filter" of DOM's
/// <a href="https://dom.spec.whatwg.org/#concept-collection">collection</a> concept.
/// </summary>
/// <remarks>
/// <para>
/// It is an object with a method rather than a <c>Func&lt;IElement, bool&gt;</c> so that a read costs one
/// virtual call per element and <b>no allocation</b>. A closure would be allocated per read wherever the
/// predicate depends on something read from the tree at the moment of the read — which is every one of them:
/// <c>getElementsByClassName</c>'s comparison is ASCII case-insensitive exactly while the root's node
/// document is in quirks mode, and a root adopted into another document takes that document's mode with it.
/// <see cref="BeginRead"/> is where such state is refreshed, once per read rather than once per element.
/// </para>
/// <para>
/// A filter is owned by exactly one collection, and a collection is owned by one page's engine on one
/// thread. Nothing between <see cref="BeginRead"/> and the end of the walk it opens can run script or mutate
/// the document, so state a filter carries across that span is not shared state.
/// </para>
/// </remarks>
internal abstract class DomElementFilter
{
    /// <summary>The filter of an empty collection, which DOM still requires to be a collection.</summary>
    internal static DomElementFilter None { get; } = new NothingFilter();

    /// <summary>
    /// Whether this filter can never match, which lets a read answer without walking the tree at all.
    /// </summary>
    internal virtual bool MatchesNothing => false;

    /// <summary>Refreshes whatever the filter reads from the tree once per read, before the walk begins.</summary>
    internal virtual void BeginRead()
    {
    }

    /// <summary>Whether <paramref name="element"/> is in the collection.</summary>
    internal abstract bool Matches(IElement element);

    private sealed class NothingFilter : DomElementFilter
    {
        internal override bool MatchesNothing => true;

        internal override bool Matches(IElement element) => false;
    }
}
