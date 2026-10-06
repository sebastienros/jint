using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Runtime;

namespace Jint.Browser.Media;

/// <summary>
/// Keeps fetched image status and intrinsic dimensions for a page's native image elements.
/// </summary>
/// <remarks>
/// The parser driver owns requests and this table keeps their results. Header decoding supplies dimensions
/// without rendering pixels. Entries are keyed by native element identity, so wrappers share one answer
/// and navigation releases the page's image state.
/// </remarks>
internal sealed class PageImages
{
    private readonly ConditionalWeakTable<Element, ImageRequest> _requests = new();

    /// <summary>How many image requests this document has started, against <see cref="BrowserOptions.MaxImageRequests"/>.</summary>
    private int _started;

    /// <summary>The state of <paramref name="image"/>'s current request, or <see langword="null"/> if it has none.</summary>
    internal ImageRequest? Find(Element image)
        => _requests.TryGetValue(image, out var request) ? request : null;

    /// <summary>
    /// Whether one more request may be started, and counts it when it may.
    /// </summary>
    /// <remarks>
    /// The ceiling is over the document rather than per element, because the quantity worth bounding is the
    /// traffic one document can ask for — <see cref="BrowserOptions.MaxSubresourceBytes"/> already bounds each
    /// response and <c>SubresourceTimeout</c> each wait, and neither bounds a thousand of them. It counts
    /// what is <i>started</i>, so a page that rewrites one element's <c>src</c> in a loop is bounded by the
    /// same number as one with a thousand elements.
    /// </remarks>
    internal bool TryStart(int ceiling) => _started++ < ceiling;

    /// <summary>
    /// Puts <paramref name="image"/>'s current request into the unavailable state, which is where
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#update-the-image-data">update the image
    /// data</a> step 12 puts it when a new URL is selected.
    /// </summary>
    internal ImageRequest Begin(Element image, string url)
    {
        var request = _requests.GetValue(image, static _ => new ImageRequest());
        request.State = ImageAvailability.Unavailable;
        request.CurrentUrl = url;
        request.Requested = false;
        request.NaturalWidth = 0;
        request.NaturalHeight = 0;
        return request;
    }

    /// <summary>
    /// Whether the image <paramref name="url"/> names is already this element's, in which case
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#update-the-image-data">update the image
    /// data</a> step 7.3 takes it from the list of available images and opens no socket.
    /// </summary>
    internal bool IsAlreadyAvailable(Element image, string url)
        => Find(image) is { State: ImageAvailability.CompletelyAvailable } request
            && string.Equals(request.CurrentUrl, url, StringComparison.Ordinal);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/images.html#update-the-image-data's success arm: the
    /// dimensions are known, so the request is completely available and <c>load</c> is what the element hears.
    /// </summary>
    internal static void Complete(ImageRequest request, int width, int height)
    {
        request.State = ImageAvailability.CompletelyAvailable;
        request.NaturalWidth = width;
        request.NaturalHeight = height;
    }

    /// <summary>
    /// The same step's failure arm: a fetch that failed, a status the server called an error, or a container
    /// <see cref="ImageHeader"/> does not recognise. All three are the broken state and all three fire
    /// <c>error</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>naturalWidth</c> of a broken image is 0 and cannot be anything else.</b> The bytes either never
    /// arrived or state no size this browser can read, so there is no number to answer with; HTML's own
    /// answer for a request that is not completely available is the same 0.
    /// </para>
    /// <para>
    /// <b>The current URL survives</b>, and that is the standard's own wording: the error arm changes the
    /// current request's current URL <i>to</i> the selected source and then fires <c>error</c>. So
    /// <c>currentSrc</c> names the candidate that was tried even when trying it failed, which is what makes
    /// it usable for telling <i>which</i> of a <c>srcset</c>'s candidates a page ended up on.
    /// </para>
    /// </remarks>
    internal static void Break(ImageRequest request)
    {
        request.State = ImageAvailability.Broken;
        request.NaturalWidth = 0;
        request.NaturalHeight = 0;
    }
}

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/images.html#img-req-state">The state of an image
/// request</a>.
/// </summary>
internal enum ImageAvailability
{
    /// <summary>Nothing is known yet: the request has been started and its bytes have not arrived.</summary>
    Unavailable,

    /// <summary>
    /// Enough of the image has been decoded to know its dimensions but not to present it. Never reached
    /// here — see <see cref="PageImages"/> — and declared so that the state machine is the standard's rather
    /// than a subset somebody has to recognise as one.
    /// </summary>
    PartiallyAvailable,

    /// <summary>The image is entirely available: its intrinsic dimensions are known.</summary>
    CompletelyAvailable,

    /// <summary>The fetch failed, or the bytes are not an image this browser recognises.</summary>
    Broken,
}

/// <summary>One <c>&lt;img&gt;</c>'s current request.</summary>
/// <remarks>
/// <b>There is no pending request.</b> HTML keeps a second request while a new URL is being fetched and the
/// old image is still being shown, so that <c>complete</c> stays false and the old dimensions keep answering
/// across a <c>src</c> rewrite. Nothing here shows an image, and every fetch this browser makes for one
/// settles inside the turn that started it, so the moment the pending request exists for never lasts long
/// enough for a script to observe. The consequence is stated on <see cref="PageImages"/>.
/// </remarks>
internal sealed class ImageRequest
{
    /// <summary>Where this request has got to.</summary>
    internal ImageAvailability State { get; set; } = ImageAvailability.Unavailable;

    /// <summary>
    /// HTML's <i>current URL</i>: the selected source, resolved, and <b>before any redirect the fetch
    /// followed</b>. The specification sets it from <c>urlString</c> in both the success and the failure
    /// arm and never from the response, because what <c>currentSrc</c> is for is saying which candidate of
    /// a source set won rather than where the bytes came from. It is also the key this element's entry in
    /// the list of available images is found under.
    /// </summary>
    internal string CurrentUrl { get; set; } = "";

    /// <summary>
    /// Whether a request was actually started for <see cref="CurrentUrl"/>, which is what separates "this
    /// image is loading or has loaded" from "this browser declined to ask" — a ceiling refusal leaves the
    /// element exactly as it was before there was an image model at all.
    /// </summary>
    internal bool Requested { get; set; }

    /// <summary>What <c>img.currentSrc</c> answers: the current URL once one has been asked for.</summary>
    internal string CurrentSrc => Requested ? CurrentUrl : "";

    /// <summary>The intrinsic width the container stated, in CSS pixels.</summary>
    internal int NaturalWidth { get; set; }

    /// <summary>The intrinsic height the container stated, in CSS pixels.</summary>
    internal int NaturalHeight { get; set; }
}
