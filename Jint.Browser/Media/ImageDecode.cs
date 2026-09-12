using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Media;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/embedded-content.html#dom-img-decode">HTML §4.8.4.2</a>'s
/// <c>img.decode()</c>: the promise a page awaits instead of racing the <c>load</c> event.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it promises here is availability, not a decoded bitmap.</b> The standard's contract is that the
/// image can be painted without the paint causing a delay — a browser answers it by having decoded the
/// pixels, and this browser answers it by knowing the request is completely available, because there is no
/// paint to delay and no bitmap to have made. A page cannot tell the two apart: what it does with the
/// resolution is draw, measure or reveal, and the first has no canvas here while the other two read exactly
/// the numbers this state is about.
/// </para>
/// <para>
/// <b>Every answer is asynchronous, and that is load-bearing rather than tidy.</b> Step 2 queues a microtask
/// before deciding anything, so <c>img.src = …; img.decode()</c> settles after the script that wrote both
/// returns — which is the ordering a page written against a real browser depends on, and the one thing a
/// subresource fetch that is synchronous with its turn would otherwise take away.
/// </para>
/// <para>
/// <b>A request that never started rejects rather than hanging.</b> HTML rejects a broken request with an
/// <c>EncodingError</c> and otherwise waits for the current request to become completely available. Here
/// every way of staying unavailable is permanent — an <c>&lt;img&gt;</c> with no source at all, one whose
/// <c>&lt;picture&gt;</c> ruled every candidate out, and one past
/// <see cref="BrowserOptions.MaxImageRequests"/> — so a promise that waited would never settle. Both major
/// engines reject <c>new Image().decode()</c> for the same reason.
/// </para>
/// </remarks>
internal static class ImageDecode
{
    /// <summary>The body of <c>HTMLImageElement.decode</c>.</summary>
    internal static JsValue Decode(DomRealm realm, IHtmlImageElement image)
    {
        var engine = realm.Engine;
        var (promise, resolve, reject) = engine.RegisterPromise();

        engine.AddToEventLoop(
            () =>
            {
                if (Available(realm, image))
                {
                    resolve(JsValue.Undefined);
                    return;
                }

                // The PRINCIPAL realm, for the reason DomFailures.Refuse gives: a DOMException a DOM member
                // raises has to come from the engine's own realm or `e instanceof DOMException` in the page
                // answers false.
                reject(engine._mainRealm.Intrinsics.DomException.CreateException(
                    DomExceptionNames.Encoding,
                    "Failed to execute 'HTMLImageElement.decode': The source image cannot be decoded."));
            },
            EventLoopJobKind.Microtask);

        return promise;
    }

    /// <summary>Whether the element's current request is completely available right now.</summary>
    private static bool Available(DomRealm realm, IHtmlImageElement image)
        => PageRuntime.Find(realm.Engine, image.Owner)?.ImagesIfLoaded?.Find(image)
            is { State: ImageAvailability.CompletelyAvailable };
}
