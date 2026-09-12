using Jint.Browser.Events;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Touch without a digitizer: the four events Touch Events Level 2 defines, the three lists each carries, and
/// the compatibility mouse events a tap leaves behind.
/// </summary>
/// <remarks>
/// These drive <c>InputDispatcher.DispatchTouch</c> directly — the surface <c>Input.dispatchTouchEvent</c>
/// maps onto — and <c>Page.TapAsync</c>, which is the same dispatcher from the host's side. What the protocol
/// adds above them is the parameter mapping, and <c>DevTools/InputTouchDomainTests</c> is where that is
/// asserted over the envelope.
/// </remarks>
public sealed class TouchDispatcherTests
{
    /// <summary>
    /// https://w3c.github.io/touch-events/#mouse-events — a tap is <c>touchstart</c>, <c>touchend</c> and
    /// then the four compatibility mouse events, in that order.
    /// </summary>
    [Test]
    public async Task ATapFiresTheTouchEventsAndThenTheCompatibilityMouseEvents()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Recorder("<button id='b'>tap me</button>"));

        (await page.TapAsync("#b")).Should().BeTrue();

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "touchstart|touchend|mousemove|mousedown|mouseup|click");
    }

    /// <summary>Everything a client drives is trusted, because it stands in for a user.</summary>
    [Test]
    public async Task ATouchIsTrustedAndCarriesTheModifiersTheClientHeld()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id='d' style='width: 40px; height: 20px'></div>
            <script>
              window.seen = '';
              document.addEventListener('touchstart', e => {
                window.seen = [e.isTrusted, e.bubbles, e.cancelable, e.altKey, e.ctrlKey, e.metaKey, e.shiftKey].join(',');
              });
            </script>
            """);

        await BrowserTestAccess.DispatchTouchAsync(
            page,
            TouchInputKind.Start,
            [TouchPointInput.At(5, 5)],
            EventModifiers.Shift | EventModifiers.Control);

        (await page.EvaluateAsync<string>("window.seen")).Should().Be("true,true,true,false,true,false,true");
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#dom-touchevent-touches — <c>touches</c> is every contact on the
    /// surface after this event's change, <c>targetTouches</c> those of them that started on the target, and
    /// <c>changedTouches</c> the one contact the event is about.
    /// </summary>
    [Test]
    public async Task TheThreeListsAreTheGesturesRatherThanTheCommands()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id='a' style='width: 200px; height: 20px'>a</div>
            <div id='b' style='width: 200px; height: 20px'>b</div>
            <script>
              window.log = [];
              const render = e => e.type + ' t=' + [...e.touches].map(t => t.identifier).join('') +
                ' tt=' + [...e.targetTouches].map(t => t.identifier).join('') +
                ' ct=' + [...e.changedTouches].map(t => t.identifier).join('') +
                ' at=' + e.target.id;
              for (const type of ['touchstart', 'touchmove', 'touchend']) {
                document.addEventListener(type, e => window.log.push(render(e)));
              }
            </script>
            """);

        var boxes = await page.EvaluateAsync<string>(
            "[document.getElementById('a').getBoundingClientRect().top, document.getElementById('b').getBoundingClientRect().top].join(',')");
        var tops = boxes!.Split(',').Select(double.Parse).ToArray();

        // Two fingers, one on each element, so targetTouches and touches can differ.
        await BrowserTestAccess.DispatchTouchAsync(
            page,
            TouchInputKind.Start,
            [TouchPointInput.At(5, tops[0] + 2, identifier: 7), TouchPointInput.At(5, tops[1] + 2, identifier: 9)]);

        await BrowserTestAccess.DispatchTouchAsync(
            page,
            TouchInputKind.Move,
            [TouchPointInput.At(6, tops[0] + 2, identifier: 7), TouchPointInput.At(5, tops[1] + 2, identifier: 9)]);

        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.End, []);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(string.Join(
            '|',
            // The first contact down sees only itself; the second sees both, and only its own in targetTouches.
            "touchstart t=7 tt=7 ct=7 at=a",
            "touchstart t=79 tt=9 ct=9 at=b",
            // Only the contact that moved fires, and its identifier is the one the client gave it.
            "touchmove t=79 tt=7 ct=7 at=a",
            // A touchend does not list the finger it is announcing.
            "touchend t=7 tt= ct=9 at=b",
            "touchend t= tt= ct=7 at=a"));
    }

    /// <summary>
    /// §8: <c>preventDefault()</c> on the <c>touchstart</c> means no compatibility mouse events at all, which
    /// is how a page that handles the gesture itself stops the click it would otherwise be given.
    /// </summary>
    [Test]
    public async Task ACancelledTouchstartSuppressesTheMouseEvents()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Recorder(
            "<button id='b'>tap me</button>",
            // DOM's default passive value: a touchstart listener added at the document, the window or the
            // body cannot cancel unless it says so, which is what makes this the shape a page must write.
            "document.addEventListener('touchstart', e => e.preventDefault(), {passive: false});"));

        (await page.TapAsync("#b")).Should().BeTrue();

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("touchstart|touchend");
    }

    /// <summary>The same rule for the first <c>touchmove</c>, which is where a drag declares itself.</summary>
    [Test]
    public async Task ACancelledFirstTouchmoveSuppressesTheMouseEventsAndALaterOneDoesNot()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Recorder(
            "<div id='d' style='width: 200px; height: 40px'>drag</div>",
            "window.cancelFrom = 1; document.addEventListener('touchmove', e => { if (window.log.filter(x => x === 'touchmove').length > window.cancelFrom) e.preventDefault(); }, {passive: false});"));

        // The second move is cancelled and the first is not, so §8's first-move rule leaves the tap alone.
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Start, [TouchPointInput.At(5, 5)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Move, [TouchPointInput.At(6, 5)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Move, [TouchPointInput.At(7, 5)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.End, []);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "touchstart|touchmove|touchmove|touchend|mousemove|mousedown|mouseup|click");

        await page.SetContentAsync(Recorder(
            "<div id='d' style='width: 200px; height: 40px'>drag</div>",
            "window.cancelFrom = 0; document.addEventListener('touchmove', e => { if (window.log.filter(x => x === 'touchmove').length > window.cancelFrom) e.preventDefault(); }, {passive: false});"));

        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Start, [TouchPointInput.At(5, 5)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Move, [TouchPointInput.At(6, 5)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.End, []);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("touchstart|touchmove|touchend");
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#event-touchcancel — not cancelable, and never a tap, so the
    /// gesture ends with no click.
    /// </summary>
    [Test]
    public async Task TouchcancelEndsTheGestureWithNoMouseEvents()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Recorder(
            "<button id='b'>tap me</button>",
            "document.addEventListener('touchcancel', e => window.log.push('cancelable=' + e.cancelable));"));

        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Start, [TouchPointInput.At(5, 5)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Cancel, []);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("touchstart|touchcancel|cancelable=false");

        // …and the next gesture is not poisoned by it: a fresh tap still produces its click.
        await page.EvaluateAsync("window.log = []");
        (await page.TapAsync("#b")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "touchstart|touchend|mousemove|mousedown|mouseup|click");
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#dom-touch-target — a contact's target is where it went down, "even
    /// if the touch point has since moved outside the interactive area of that element".
    /// </summary>
    [Test]
    public async Task AContactKeepsTheTargetItStartedOnAndTheMouseEventsGoWhereItEnded()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id='a' style='width: 200px; height: 20px'>a</div>
            <div id='b' style='width: 200px; height: 20px'>b</div>
            <script>
              window.log = [];
              for (const type of ['touchstart', 'touchmove', 'touchend', 'mousedown', 'click']) {
                document.addEventListener(type, e => window.log.push(type + ':' + e.target.id));
              }
              document.addEventListener('touchmove', e => window.log.push('touch:' + e.changedTouches[0].target.id));
            </script>
            """);

        var boxes = await page.EvaluateAsync<string>(
            "[document.getElementById('a').getBoundingClientRect().top, document.getElementById('b').getBoundingClientRect().top].join(',')");
        var tops = boxes!.Split(',').Select(double.Parse).ToArray();

        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Start, [TouchPointInput.At(5, tops[0] + 2)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.Move, [TouchPointInput.At(5, tops[1] + 2)]);
        await BrowserTestAccess.DispatchTouchAsync(page, TouchInputKind.End, []);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(string.Join(
            '|',
            "touchstart:a",
            // The touch events stay at the element the finger went down on…
            "touchmove:a",
            "touch:a",
            "touchend:a",
            // …and the mouse events land where the finger really was when it came off.
            "mousedown:b",
            "click:b"));
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#touch-interface — every member is what the client sent, with the
    /// protocol's own defaults, and <c>pageY</c> adds the page's scroll offset while <c>pageX</c> adds
    /// nothing.
    /// </summary>
    [Test]
    public async Task ATouchCarriesTheCoordinatesTheRadiiAndTheForce()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            TallPage() +
            """
            <script>
              window.seen = '';
              document.addEventListener('touchstart', e => {
                const t = e.changedTouches[0];
                window.seen = [t.identifier, t.clientX, t.clientY, t.screenX, t.screenY, t.pageX, t.pageY,
                               t.radiusX, t.radiusY, t.rotationAngle, t.force, t.target.tagName].join(',');
              });
            </script>
            """);

        await page.ScrollToAsync(100);
        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(100, "the rest of this asserts what pageY adds to clientY");
        await BrowserTestAccess.DispatchTouchAsync(
            page,
            TouchInputKind.Start,
            [new TouchPointInput(Identifier: 4, X: 12, Y: 30, RadiusX: 6, RadiusY: 7, RotationAngle: 45, Force: 0.5)]);

        (await page.EvaluateAsync<string>("window.seen")).Should().Be("4,12,30,12,30,12,130,6,7,45,0.5,DIV");
    }

    /// <summary>
    /// The flat box model gives every rendered element a 16-pixel row in tree order and reads no CSS height
    /// at all, so a document that scrolls is one with many elements rather than one tall one.
    /// </summary>
    private static string TallPage(int rows = 120)
    {
        var html = new System.Text.StringBuilder();
        for (var i = 0; i < rows; i++)
        {
            html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<div id=\"row{i}\">row {i}</div>");
        }

        return html.ToString();
    }

    /// <summary>
    /// A tap runs the activation behaviour a click has, because the compatibility events are the click — so a
    /// checkbox toggles and a link is followed rather than a tap being a click that only looks like one.
    /// </summary>
    [Test]
    public async Task ATapActivatesWhatItLandsOn()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='c' type='checkbox'><input id='t' type='text'>");

        (await page.TapAsync("#c")).Should().BeTrue();

        (await page.EvaluateAsync<bool>("document.getElementById('c').checked")).Should().BeTrue();

        // The focusing steps run for the same reason: the compatibility mousedown is the mouse's own.
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("c");
    }

    /// <summary>
    /// A page that binds only touch handlers hears a tap, whether or not anybody asked for touch emulation —
    /// which is what makes the two commands independent.
    /// </summary>
    [Test]
    public async Task ATouchArrivesWithoutTouchEmulationAndDetectionStillFollowsIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Recorder("<button id='b'>tap me</button>"));

        (await page.EvaluateAsync<bool>("'ontouchstart' in window")).Should().BeFalse();
        (await page.TapAsync("#b")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().StartWith("touchstart|touchend");

        await page.SetTouchEmulationAsync(enabled: true, maxTouchPoints: 5);

        (await page.EvaluateAsync<bool>("'ontouchstart' in window")).Should().BeTrue();
        (await page.EvaluateAsync<double>("navigator.maxTouchPoints")).Should().Be(5);
        (await page.EvaluateAsync<bool>("matchMedia('(pointer: coarse)').matches")).Should().BeTrue();

        await page.SetTouchEmulationAsync(enabled: false);

        (await page.EvaluateAsync<bool>("'ontouchstart' in window")).Should().BeFalse();
        (await page.EvaluateAsync<double>("navigator.maxTouchPoints")).Should().Be(0);
    }

    /// <summary>
    /// <c>BrowserOptions.HasTouch</c> is the other half of a device profile, beside the viewport: a page opens
    /// as a touch device, so the detection is already true while its <i>first</i> document is parsing.
    /// </summary>
    /// <remarks>
    /// The distinction from <c>SetTouchEmulationAsync</c> is what the inline script measures — a responsive
    /// framework branches on these as it starts, and a page told afterwards has already decided.
    /// </remarks>
    [Test]
    public async Task ABrowserWithHasTouchOpensPagesThatAreAlreadyTouchDevices()
    {
        await using var browser = new Browser(new global::Jint.Browser.BrowserOptions { HasTouch = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <script>
              window.detected = ['ontouchstart' in window, 'ontouchmove' in document, navigator.maxTouchPoints,
                                 matchMedia('(pointer: coarse)').matches].join(',');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.detected")).Should().Be("true,true,1,true");
    }

    /// <summary>
    /// The handler content attribute touch emulation exposes is a real handler slot: assigning to
    /// <c>ontouchstart</c> runs on the next touch.
    /// </summary>
    [Test]
    public async Task AnOntouchstartHandlerRunsOnceTouchIsEmulated()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<button id='b'>tap me</button>");
        await page.SetTouchEmulationAsync(enabled: true);

        await page.EvaluateAsync("window.seen = ''; document.getElementById('b').ontouchstart = e => { window.seen = e.type + ':' + e.touches.length; };");

        (await page.TapAsync("#b")).Should().BeTrue();

        (await page.EvaluateAsync<string>("window.seen")).Should().Be("touchstart:1");
    }

    /// <summary>
    /// The coordinate form goes through the same hit test, so a point in an element's box reaches that
    /// element rather than the document.
    /// </summary>
    [Test]
    public async Task ATapAtACoordinateReachesWhatTheHitTestFinds()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Recorder("<p>above</p><button id='b'>tap me</button>"));

        var box = await page.EvaluateAsync<string>(
            "(() => { const r = document.getElementById('b').getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2].join(','); })()");
        var point = box!.Split(',').Select(double.Parse).ToArray();

        await page.TapAsync(point[0], point[1]);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "touchstart|touchend|mousemove|mousedown|mouseup|click");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes — the content
    /// attribute is markup a page wrote rather than a capability probe, so it is compiled and run whether or
    /// not anybody asked for touch emulation. What emulation decides is the <i>IDL</i> attribute, which is
    /// what a feature detection reads.
    /// </summary>
    [Test]
    public async Task AMarkupHandlerRunsWithoutEmulationAndTheIdlAttributeStillDoesNotExist()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<button id='b' ontouchstart=\"window.seen = event.type + ':' + event.touches.length\">tap</button>");

        (await page.EvaluateAsync<bool>("'ontouchstart' in document.getElementById('b')")).Should().BeFalse();

        (await page.TapAsync("#b")).Should().BeTrue();

        (await page.EvaluateAsync<string>("window.seen")).Should().Be("touchstart:1");
    }

    /// <summary>A document that records every event this file asserts on, in the order it hears them.</summary>
    private static string Recorder(string body, string extra = "")
        => $$"""
        {{body}}
        <script>
          window.log = [];
          for (const type of ['touchstart', 'touchmove', 'touchend', 'touchcancel', 'mousemove', 'mousedown', 'mouseup', 'click']) {
            document.addEventListener(type, e => window.log.push(type));
          }
          {{extra}}
        </script>
        """;
}
