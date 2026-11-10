#nullable enable

using Jint.Tests.Runtime.WebApi;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Runs <see cref="WebIdlSequenceCloseMatrix"/>, the matrix <c>WebIdlSequenceCloseTests</c> runs over the web APIs,
/// over every sequence conversion in <c>Jint.Browser</c>: an element conversion that throws calls the iterator's
/// <c>return</c> exactly once, while a step that throws and a sequence that runs out call nothing
/// (sebastienros/jint#4431). One lane per conversion site, and a second where its elements can fail in another way (a
/// <c>Notification</c>'s <c>actions</c>, below); the generated bindings convert no sequence themselves, and
/// the two that take one (<c>getHTML</c>'s <c>shadowRoots</c> and the <c>Sanitizer</c> lists) reach hand-written
/// helpers, which are the lanes. <c>Window.postMessage</c>'s transfer list is the web-API helper and is pinned there.
/// <para>
/// A keyframes argument is not a WebIDL sequence: Web Animations iterates it with its own algorithm
/// (https://drafts.csswg.org/web-animations-1/#process-a-keyframes-argument) and converts each element to a
/// dictionary. It follows the same convention, since a failing element is a conversion failing all the same.
/// </para>
/// <para>
/// A <c>Notification</c>'s <c>actions</c> converts each element as a <c>NotificationAction</c> dictionary and only counts
/// the result (sebastienros/jint#4516), and a non-persistent notification then refuses any non-empty list. That refusal
/// follows the conversion, so the two lanes absorb it: the lane's good sequence is accepted, and the matrix sees the
/// conversion having completed. An element fails in two ways, a member that throws as it is converted and a required
/// member that is missing, and each has a lane; the other ways (a bad <c>type</c>, a Symbol <c>placeholder</c>) reach the
/// same <c>catch</c> and are pinned by name in <c>NotificationActionsTests</c>.
/// </para>
/// </summary>
public sealed class BrowserWebIdlSequenceCloseTests
{
    private const string Lanes = """
        const ctx = document.createElement('canvas').getContext('2d');

        lanes.ariaElements = () => ({
            goods: [document.createElement('span')],
            bad: {},
            run: it => { document.createElement('div').ariaControlsElements = it; },
        });
        lanes.cookieChangeEvent = () => ({ goods: [{ name: 'a', value: 'b' }], bad: 1, run: it => new CookieChangeEvent('change', { changed: it }) });
        lanes.customElementObservedAttributes = () => ({
            goods: ['a'],
            bad: throwsOnConversion,
            run: it => customElements.define('x-probe', class extends HTMLElement {
                static get observedAttributes() { return it; }
                attributeChangedCallback() { }
            }),
        });
        lanes.keyframes = () => ({
            goods: [{ opacity: 0 }],
            bad: { get composite() { throw marker('ConversionError'); } },
            run: it => new KeyframeEffect(null, it),
        });
        lanes.keyframesPropertyIndexed = () => ({ goods: ['0', '1'], bad: throwsOnConversion, run: it => new KeyframeEffect(null, { opacity: it }) });
        lanes.highEntropyHints = () => ({
            goods: ['architecture'],
            bad: throwsOnConversion,
            run: it => navigator.userAgentData.getHighEntropyValues(it),
        });
        lanes.notificationVibrate = () => ({ goods: [5], bad: throwsOnConversion, run: it => new Notification('x', { vibrate: it }) });
        // The constructor refuses a non-empty list of actions after converting it (a non-persistent notification has none
        // to offer), so a list that converted is the refusal and nothing else.
        const notificationActions = it => {
            try { new Notification('x', { actions: it }); }
            catch (e) { if (!e.message.includes('Actions are only supported')) throw e; }
        };
        lanes.notificationActions = () => ({
            goods: [{ action: 'a', title: 't' }],
            bad: { action: 'a', get title() { throw marker('ConversionError'); } },
            run: notificationActions,
        });
        lanes.notificationActionsMissingMember = () => ({ goods: [{ action: 'a', title: 't' }], bad: {}, run: notificationActions });
        lanes.getHtmlShadowRoots = () => ({
            goods: [document.createElement('div').attachShadow({ mode: 'open' })],
            bad: {},
            run: it => document.body.getHTML({ shadowRoots: it }),
        });
        lanes.sanitizerList = () => ({
            goods: ['script'],
            bad: { get name() { throw marker('ConversionError'); } },
            run: it => new Sanitizer({ removeElements: it }),
        });
        lanes.setLineDash = () => ({ goods: [1], bad: throwsOnConversion, run: it => ctx.setLineDash(it) });
        lanes.fontFaceSetLoadEvent = () => ({
            goods: [new FontFace('f', 'url(f)')],
            bad: {},
            run: it => new FontFaceSetLoadEvent('loading', { fontfaces: it }),
        });
        lanes.touchEvent = () => ({
            goods: [new Touch({ identifier: 1, target: document.body })],
            bad: {},
            run: it => new TouchEvent('touchstart', { touches: it }),
        });
        lanes.domMatrix = () => ({ goods: [1, 0, 0, 1, 5, 6], bad: throwsOnConversion, run: it => new DOMMatrix(it) });
        lanes.roundRect = () => ({
            goods: [1],
            bad: { get x() { throw marker('ConversionError'); } },
            run: it => ctx.roundRect(0, 0, 10, 10, it),
        });
        lanes.mutationObserverAttributeFilter = () => ({
            goods: ['a'],
            bad: throwsOnConversion,
            run: it => new MutationObserver(() => { }).observe(document.body, { attributeFilter: it }),
        });
        // observe's steps refuse a filter with attributes false only once the whole dictionary has been converted.
        lanes.mutationObserverAttributeFilterRefused = () => ({
            refused: ['a', 'b'],
            bad: throwsOnConversion,
            run: it => new MutationObserver(() => { }).observe(document.body, { attributes: false, attributeFilter: it }),
        });
        // A threshold out of [0, 1] is the constructor's RangeError, after the conversion; a non-finite one fails
        // the restricted double conversion itself.
        lanes.intersectionObserverThreshold = () => ({
            goods: [0.5],
            bad: throwsOnConversion,
            refused: [2, 0.5],
            run: it => new IntersectionObserver(() => { }, { threshold: it }),
        });
        lanes.intersectionObserverThresholdNonFinite = () => ({ goods: [0.5], bad: NaN, run: it => new IntersectionObserver(() => { }, { threshold: it }) });
        """;

    // Each lane, named with the error its bad element throws.
    private static readonly Dictionary<string, string> SyncLanes = new()
    {
        ["ariaElements"] = "TypeError",
        ["cookieChangeEvent"] = "TypeError",
        ["customElementObservedAttributes"] = "ConversionError",
        ["keyframes"] = "ConversionError",
        ["keyframesPropertyIndexed"] = "ConversionError",
        ["highEntropyHints"] = "ConversionError",
        ["notificationVibrate"] = "ConversionError",
        ["notificationActions"] = "ConversionError",
        ["notificationActionsMissingMember"] = "TypeError",
        ["getHtmlShadowRoots"] = "TypeError",
        ["sanitizerList"] = "ConversionError",
        ["setLineDash"] = "ConversionError",
        ["fontFaceSetLoadEvent"] = "TypeError",
        ["touchEvent"] = "TypeError",
        ["domMatrix"] = "ConversionError",
        ["roundRect"] = "ConversionError",
        ["mutationObserverAttributeFilter"] = "ConversionError",
        ["intersectionObserverThreshold"] = "ConversionError",
        ["intersectionObserverThresholdNonFinite"] = "TypeError",
    };

    // The lanes whose algorithm refuses what the conversion produced: each named with the error the refusal throws
    // and the one its bad element does.
    private static readonly Dictionary<string, (string Refusal, string Error)> RefusalLanes = new()
    {
        ["mutationObserverAttributeFilterRefused"] = ("TypeError", "ConversionError"),
        ["intersectionObserverThreshold"] = ("RangeError", "ConversionError"),
    };

    public static TestCases<string, string, string, string> EveryLaneUnderEveryProbe
        => WebIdlSequenceCloseMatrix.Cases(SyncLanes, refusalLanes: RefusalLanes);

    /// <summary>
    /// A conversion that fails on an element closes the iterator once and propagates its own error; a step that
    /// throws, and a sequence that runs out, close nothing.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneUnderEveryProbe))]
    public async Task EveryLaneClosesOnlyWhenAnElementFailsToConvert(string lane, string probe, string row, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");
        await page.EvaluateAsync(WebIdlSequenceCloseMatrix.Harness);
        await page.EvaluateAsync(Lanes);

        (await page.EvaluateAndAwaitAsync<string>($"probe('{lane}', '{probe}', '{row}')")).Should().Be(expected);
    }
}
