using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Runtime;

namespace Jint.Browser.Media;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/images.html#the-list-of-available-images">HTML §4.8.4.3</a>'s
/// current request, for every <c>&lt;img&gt;</c> of one document: what state it is in, what URL it settled
/// on, and the intrinsic size its container stated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all, when AngleSharp has an <c>ImageRequestProcessor</c>.</b> That processor is what
/// asks for the bytes, and it is kept — it is what turns a parsed <c>&lt;img src&gt;</c>, a
/// <c>img.src = …</c> and a <c>srcset</c> rewrite into one request, and it is what tells the document to
/// delay its <c>load</c> event while one is in flight. What it has no answer for is the *state machine*: its
/// <c>IsCompleted</c> is "a resource object exists", which is neither HTML's <c>complete</c> (true for an
/// <c>&lt;img&gt;</c> with no <c>src</c> at all, and true for a broken one) nor a state a page can act on;
/// and it can produce a size only through an <c>IResourceService&lt;IImageInfo&gt;</c> that decodes, which
/// this browser has none of and should not have. So AngleSharp keeps the request and this keeps the answer.
/// </para>
/// <para>
/// <b>Three of the four states are reachable and the fourth is honestly not.</b> A request is
/// <i>unavailable</i> while its bytes are on their way, <i>completely available</i> once
/// <see cref="ImageHeader"/> has read a size out of them, and <i>broken</i> when the fetch failed or the
/// container is one this browser does not recognise. <i>Partially available</i> means "enough has been
/// decoded to know the dimensions but not to paint" — it exists so that a browser can lay a page out before
/// the last scanline arrives, and a browser with no layout has no moment at which it is true.
/// </para>
/// <para>
/// <b>What cannot be honest without pixels</b>, stated here rather than discovered: the intrinsic size is
/// what the container's header <i>says</i>, so a file whose header disagrees with its pixel data is believed
/// and a truncated body whose header arrived is available rather than broken; an animated GIF is its logical
/// screen and has no frame count, no delay and no loop; and there is no colour, no alpha and no orientation,
/// so an EXIF rotation is not applied and a portrait photograph tagged sideways reports its stored
/// dimensions. <c>Jint.Browser/Dom/divergences.md</c> carries the rows a page can see.
/// </para>
/// <para>
/// One instance per <see cref="PageRuntime"/>, built on first use, so a document with no images allocates
/// nothing. The table is keyed on the AngleSharp element for the same reason the wrapper cache is: an element
/// dropped by both the tree and script takes its image state with it.
/// </para>
/// </remarks>
internal sealed class PageImages
{
    private readonly ConditionalWeakTable<IElement, ImageRequest> _requests = new();

    /// <summary>How many image requests this document has started, against <see cref="BrowserOptions.MaxImageRequests"/>.</summary>
    private int _started;

    /// <summary>The state of <paramref name="image"/>'s current request, or <see langword="null"/> if it has none.</summary>
    internal ImageRequest? Find(IElement image)
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
    internal ImageRequest Begin(IElement image, string url)
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
    /// <remarks>
    /// <b>No event is fired either, and that is the standard's own condition</b>: step 7.3.7.3 fires
    /// <c>load</c> only when the previous URL differs from this one, which for a re-run over an unchanged
    /// <c>src</c> it does not. It is what stops an <c>&lt;input type=image&gt;</c> costing two sockets,
    /// AngleSharp's own <c>UpdateType</c> running twice for one parsed element (see
    /// <c>Jint.Browser/Dom/divergences.md</c>).
    /// </remarks>
    internal bool IsAlreadyAvailable(IElement image, string url)
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
