using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/images.html#img-environment-changes — fetches the candidate
    /// every image of the displayed document now selects, when the viewport or the media it selected against
    /// moved under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called by <see cref="PageRuntime.SetMedia"/> once the window and every <c>MediaQueryList</c> have heard
    /// the change, which is where HTML leaves it to the user agent: "may at any time". Only an
    /// <c>&lt;img&gt;</c> that already has a request and whose selection can depend on the environment — a
    /// non-empty <c>srcset</c>, or a <c>&lt;picture&gt;</c> parent with a <c>&lt;source&gt;</c> before it —
    /// is looked at, and only one whose selection names a URL other than its current one is fetched. A
    /// selection of nothing keeps the current image, as step 5 of that algorithm does.
    /// </para>
    /// <para>
    /// <b>The fetch is <see cref="FetchImage"/>'s</b>, so the request log, <see cref="BrowserOptions.MaxImageRequests"/>,
    /// the document's list of available images and the queued <c>load</c>/<c>error</c> are the ones any other
    /// source change gets. It blocks the loop rather than pumping, which is the rule for every fetch started
    /// outside a parser wait, and it means <c>currentSrc</c> names the new candidate once the change returns:
    /// HTML swaps the current request only when the new one has loaded, and a page cannot observe the
    /// difference without a turn running in between, which this does not allow.
    /// </para>
    /// <para>
    /// <b>The principal document only, and not while it is still being parsed.</b> A parse in progress selects
    /// each image against the environment as it reaches it; images in shadow trees and child frames are not
    /// revisited. Pages that never started an image request pay a single field read.
    /// </para>
    /// </remarks>
    internal void ReactToEnvironmentChange()
    {
        if (_disposed
            || _drainingResourceRecords
            || _runtime.Document is not { } document
            || _nativeParses.ContainsKey(document)
            || _runtime.ImagesIfLoaded is not { HasStarted: true } images)
        {
            return;
        }

        var work = new DomReadWork(_runtime.Dom.NativeReadCheckpoint, _cancellationToken);
        List<Element>? changed = null;

        foreach (var element in NodeTraversal.DescendantElements(document, work.Check, work.Token))
        {
            if (element is not { NamespaceUri: Namespaces.Html, LocalName: "img" }
                || images.Find(element) is not { } request
                || !Media.ImageSourceSet.UsesSourceSet(element, work))
            {
                continue;
            }

            if (Media.ImageSourceSet.Select(_runtime, element) is { } url
                && !string.Equals(url, request.CurrentUrl, StringComparison.Ordinal))
            {
                (changed ??= []).Add(element);
            }
        }

        if (changed is null)
        {
            return;
        }

        // Selected first and fetched after, so nothing a fetch records can disturb the walk.
        foreach (var image in changed)
        {
            FetchImage(image, Attribute(image, "src") ?? "");
        }
    }
}
