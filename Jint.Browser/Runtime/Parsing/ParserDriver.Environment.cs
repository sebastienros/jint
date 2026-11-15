using Jint.HtmlParser;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/images.html#img-environment-changes — when the viewport or the
    /// media an image selected against moves, queues a fetch for every image of the displayed document that
    /// now selects another candidate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PageRuntime.SetMedia"/> calls this after the window and every <c>MediaQueryList</c> have
    /// heard the change. HTML lets a user agent run this algorithm "at any time", so this runs it on every
    /// change. Only the images <see cref="Media.PageImages.RememberSourceSet"/> recorded are considered,
    /// meaning those whose selection can depend on the environment. No tree walk is made, and a page without
    /// a <c>srcset</c> or a <c>&lt;picture&gt;</c> pays nothing. An image is queued when its selection now
    /// names a URL other than its current one. That includes an image whose earlier selection named nothing.
    /// A selection of nothing keeps the current image, as step 5 of the algorithm does.
    /// </para>
    /// <para>
    /// <b>The fetch is a task, and not part of the change.</b> HTML fetches in parallel and swaps in the
    /// new image once it has loaded, so the command or host call that moved the viewport returns without
    /// waiting on the network. Each image gets its own task, which <see cref="Page.WaitForIdleAsync"/> waits
    /// for and which has its own page budget. The task selects again when it runs, so a second change that
    /// arrives first wins, and <see cref="FetchImage"/> does the work every other source change uses: the
    /// request log, <see cref="BrowserOptions.MaxImageRequests"/>, the document's list of available images and
    /// the queued <c>load</c>/<c>error</c>. Because a change can arrive before a parse finishes, that parse's
    /// images are covered too.
    /// </para>
    /// <para>
    /// <b>The principal document only</b>, its shadow trees included. A child frame's images select against
    /// the page's media as well, but the frame is not this document's viewport, so they are not selected
    /// again.
    /// </para>
    /// </remarks>
    internal void ReactToEnvironmentChange()
    {
        if (_disposed || _cancellationToken.IsCancellationRequested || _runtime.ImagesIfLoaded is not { } images)
        {
            return;
        }

        foreach (var image in images.SourceSetImages())
        {
            if (!IsDisplayedImage(image))
            {
                continue;
            }

            var current = images.Find(image) is { } request ? request.CurrentUrl : null;
            if (Media.ImageSourceSet.Select(_runtime, image) is { } url
                && !string.Equals(url, current, StringComparison.Ordinal))
            {
                _runtime.Engine.Tasks.Post(() => SelectAgain(image));
            }
        }
    }

    /// <summary>The queued half of <see cref="ReactToEnvironmentChange"/>, for one image.</summary>
    private void SelectAgain(Element image)
    {
        if (!_disposed && !_cancellationToken.IsCancellationRequested && IsDisplayedImage(image))
        {
            FetchImage(image, Attribute(image, "src") ?? "");
        }
    }

    private bool IsDisplayedImage(Element image)
        => _runtime.Document is { } document
           && ReferenceEquals(image.OwnerDocument, document)
           && IsResourceConnected(image);
}
