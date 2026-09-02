using AngleSharp;
using AngleSharp.Dom;
using Jint.Browser.Runtime.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// The page's entry into a parse: hands the markup to <see cref="ParserDriver"/> and answers what it built.
/// </summary>
/// <remarks>
/// It is one method because the parse is one thing now: the driver owns the parser thread, the baton, script
/// scheduling, the subresource loads and the load lifecycle. What stays here is the shape a navigation
/// commits through, so that <c>Page.Navigation</c> has one call and not five.
/// </remarks>
internal static class PageDocument
{
    /// <summary>Opens <paramref name="html"/> as <paramref name="url"/> and runs its scripts.</summary>
    internal static PageLoad Load(PageRuntime runtime, string html, string url, Action<NavigationPhase>? onPhase = null)
        => ParserDriver.Load(runtime, html, url, onPhase);
}

/// <summary>What one parse produced: the document, the context that owns it, and how much script ran.</summary>
internal sealed record PageLoad(IDocument Document, IBrowsingContext Context, int ScriptsRun);

/// <summary>
/// How far a load has got, so that <see cref="WaitUntilState"/> can answer at three different points.
/// </summary>
/// <remarks>
/// They are three separate moments now that the parser driver owns the parse: <see cref="Committed"/> is the
/// end of the parse, with every parser-blocking and deferred classic script run;
/// <see cref="DomContentLoaded"/> follows the module scripts; and <see cref="Loaded"/> follows every
/// subresource the page fetched.
/// </remarks>
internal enum NavigationPhase
{
    /// <summary>The document exists and is the page's; the parse and its classic scripts have finished.</summary>
    Committed,

    /// <summary><c>DOMContentLoaded</c> has been dispatched.</summary>
    DomContentLoaded,

    /// <summary><c>load</c> and <c>pageshow</c> have been dispatched at the window.</summary>
    Loaded,
}
