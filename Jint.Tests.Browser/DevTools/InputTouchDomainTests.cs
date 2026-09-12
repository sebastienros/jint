using System.Text.Json;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>Input</c> domain's touch half over a real page: the four types, the contacts each carries, the two
/// rules the protocol states about how many there may be, and the compatibility mouse events a tap leaves.
/// <para>
/// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-dispatchTouchEvent
/// </para>
/// </summary>
/// <remarks>
/// These assert the envelope as text, for the reason <c>Jint.DevTools/AGENTS.md</c> gives: a client library
/// matches on <c>result</c> shapes and on <c>error.code</c>, and a test that called the domain method
/// directly would pass with the envelope broken. What the command <i>does</i> below the element is
/// <c>InputDispatcher</c>'s and is tested against it directly in <c>Events/TouchDispatcherTests</c>; what is
/// here is the mapping — which protocol type fires which events, and which parameters reach them.
/// </remarks>
[NonParallelizable]
public class InputTouchDomainTests
{
    /// <summary>
    /// https://w3c.github.io/touch-events/#mouse-events — a tap is <c>touchstart</c>, <c>touchend</c>, and
    /// then the four compatibility mouse events, with the three touch lists §5.2 defines.
    /// </summary>
    [Test]
    public async Task ATapReportsTheThreeListsAndThenTheCompatibilityMouseEvents()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <button id='b'>tap me</button>
            <script>
              window.log = [];
              const render = e => e.type + ' t=' + e.touches.length + ' tt=' + e.targetTouches.length +
                ' ct=' + e.changedTouches.length + ' at=' + e.target.id + ' trusted=' + e.isTrusted;
              for (const type of ['touchstart', 'touchend']) {
                document.addEventListener(type, e => window.log.push(render(e)));
              }
              for (const type of ['mousemove', 'mousedown', 'mouseup', 'click']) {
                document.addEventListener(type, e => window.log.push(type + ':' + e.target.id));
              }
            </script>
            """);

        var point = await PointAsync(session, attachment, "b");

        await Touch(session, attachment, $$"""{"type":"touchStart","touchPoints":[{"x":{{J(point.X)}},"y":{{J(point.Y)}}}]}""");
        await Touch(session, attachment, """{"type":"touchEnd","touchPoints":[]}""");

        (await Evaluate(session, attachment, "window.log.join('|')")).Should().Be(string.Join(
            '|',
            "touchstart t=1 tt=1 ct=1 at=b trusted=true",
            // The finger this event announces the lifting of is in changedTouches and in neither of the
            // other two, which is what §5.2 says about a touchend.
            "touchend t=0 tt=0 ct=1 at=b trusted=true",
            "mousemove:b",
            "mousedown:b",
            "mouseup:b",
            "click:b"));
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#dom-touch-identifier — the protocol's <c>id</c> is what tracks one
    /// finger across a gesture, and an omitted one is the contact's place in the list.
    /// </summary>
    [Test]
    public async Task MultiTouchKeepsTheIdentifiersTheClientGaveEachContact()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <div id='a'>a</div>
            <div id='b'>b</div>
            <script>
              window.log = [];
              const ids = list => [...list].map(t => t.identifier).join('');
              for (const type of ['touchstart', 'touchmove', 'touchend']) {
                document.addEventListener(type, e =>
                  window.log.push(e.type + ' t=' + ids(e.touches) + ' ct=' + ids(e.changedTouches) + ' at=' + e.target.id));
              }
            </script>
            """);

        var first = await PointAsync(session, attachment, "a");
        var second = await PointAsync(session, attachment, "b");

        await Touch(
            session,
            attachment,
            $$"""
            {"type":"touchStart","touchPoints":[
              {"x":{{J(first.X)}},"y":{{J(first.Y)}},"id":3},
              {"x":{{J(second.X)}},"y":{{J(second.Y)}},"id":8}]}
            """);

        // Only the contact that moved fires, which is the protocol's "one event per any changed point".
        await Touch(
            session,
            attachment,
            $$"""
            {"type":"touchMove","touchPoints":[
              {"x":{{J(first.X + 3)}},"y":{{J(first.Y)}},"id":3},
              {"x":{{J(second.X)}},"y":{{J(second.Y)}},"id":8}]}
            """);

        await Touch(session, attachment, """{"type":"touchEnd","touchPoints":[]}""");

        (await Evaluate(session, attachment, "window.log.join('|')")).Should().Be(string.Join(
            '|',
            "touchstart t=3 ct=3 at=a",
            "touchstart t=38 ct=8 at=b",
            "touchmove t=38 ct=3 at=a",
            "touchend t=3 ct=8 at=b",
            "touchend t= ct=3 at=a"));

        // Two fingers is not a tap, so §8's compatibility events are not owed and no click was dispatched.
        (await Evaluate(session, attachment, "String(window.log.some(x => x.startsWith('click')))")).Should().Be("false");
    }

    /// <summary>
    /// §8: <c>preventDefault()</c> on the <c>touchstart</c> means no compatibility mouse events, which is how
    /// a page that handles the gesture itself stops the click it would otherwise be given.
    /// </summary>
    [Test]
    public async Task ACancelledTouchstartSuppressesTheMouseCompatibilityEvents()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <button id='b'>tap me</button>
            <script>
              window.log = [];
              for (const type of ['touchstart', 'touchend', 'mousemove', 'mousedown', 'mouseup', 'click']) {
                document.addEventListener(type, e => window.log.push(type));
              }
              // DOM's default passive value: a touchstart listener at the document cannot cancel unless the
              // page says so, which is the shape a page that handles its own gestures really writes.
              document.getElementById('b').addEventListener('touchstart', e => e.preventDefault(), {passive: false});
            </script>
            """);

        var point = await PointAsync(session, attachment, "b");

        await Touch(session, attachment, $$"""{"type":"touchStart","touchPoints":[{"x":{{J(point.X)}},"y":{{J(point.Y)}}}]}""");
        await Touch(session, attachment, """{"type":"touchEnd","touchPoints":[]}""");

        (await Evaluate(session, attachment, "window.log.join('|')")).Should().Be("touchstart|touchend");
    }

    /// <summary>
    /// https://w3c.github.io/touch-events/#event-touchcancel — not cancelable, and never a tap, so the
    /// gesture ends with no click at all.
    /// </summary>
    [Test]
    public async Task TouchCancelEndsTheGestureAndOwesNoMouseEvents()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <button id='b'>tap me</button>
            <script>
              window.log = [];
              for (const type of ['touchstart', 'touchcancel', 'click']) {
                document.addEventListener(type, e => window.log.push(type + ':' + e.cancelable + ':' + e.touches.length));
              }
            </script>
            """);

        var point = await PointAsync(session, attachment, "b");

        await Touch(session, attachment, $$"""{"type":"touchStart","touchPoints":[{"x":{{J(point.X)}},"y":{{J(point.Y)}}}]}""");
        await Touch(session, attachment, """{"type":"touchCancel","touchPoints":[]}""");

        (await Evaluate(session, attachment, "window.log.join('|')"))
            .Should().Be("touchstart:true:1|touchcancel:false:0");
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#type-TouchPoint — the members a client
    /// omits arrive as the protocol's own defaults rather than as zero, and a page reads back what it sent.
    /// </summary>
    [Test]
    public async Task TheContactCarriesTheProtocolDefaultsAndEveryMemberTheClientSent()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <div id='d'>d</div>
            <script>
              window.seen = [];
              document.addEventListener('touchstart', e => {
                const t = e.changedTouches[0];
                window.seen.push([t.identifier, t.clientX, t.clientY, t.screenX, t.screenY, t.pageX, t.pageY,
                                  t.radiusX, t.radiusY, t.rotationAngle, t.force].join(','));
              });
            </script>
            """);

        var point = await PointAsync(session, attachment, "d");

        await Touch(session, attachment, $$"""{"type":"touchStart","touchPoints":[{"x":{{J(point.X)}},"y":{{J(point.Y)}}}]}""");
        await Touch(session, attachment, """{"type":"touchEnd","touchPoints":[]}""");

        await Touch(
            session,
            attachment,
            $$"""
            {"type":"touchStart","touchPoints":[
              {"x":{{J(point.X)}},"y":{{J(point.Y)}},"id":11,"radiusX":9,"radiusY":4,"rotationAngle":30,"force":0.25,
               "tangentialPressure":0.5,"tiltX":10,"tiltY":-10,"twist":90}]}
            """);

        (await Evaluate(session, attachment, "window.seen.join('|')")).Should().Be(string.Join(
            '|',
            // The omitted radii and force are 1, and the omitted rotation is 0 — Chrome's documented defaults.
            $"0,{J(point.X)},{J(point.Y)},{J(point.X)},{J(point.Y)},{J(point.X)},{J(point.Y)},1,1,0,1",
            // A stylus's four members are accepted and reported nowhere: Touch Events publishes no member for
            // any of them.
            $"11,{J(point.X)},{J(point.Y)},{J(point.X)},{J(point.Y)},{J(point.X)},{J(point.Y)},9,4,30,0.25"));
    }

    /// <summary>
    /// The protocol's own two rules about the list, which a client that breaks is describing a gesture that
    /// cannot happen.
    /// </summary>
    [Test]
    public async Task TheTwoRulesAboutTheContactListAreInvalidParams()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        var empty = await session.SendAsync("Input.dispatchTouchEvent", """{"type":"touchStart","touchPoints":[]}""", attachment);
        empty.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        empty.GetProperty("error").GetProperty("message").GetString().Should().Contain("at least one");

        var full = await session.SendAsync(
            "Input.dispatchTouchEvent",
            """{"type":"touchEnd","touchPoints":[{"x":1,"y":1}]}""",
            attachment);
        full.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        full.GetProperty("error").GetProperty("message").GetString().Should().Contain("must not have");

        var unknown = await session.SendAsync(
            "Input.dispatchTouchEvent",
            """{"type":"touchBoing","touchPoints":[{"x":1,"y":1}]}""",
            attachment);
        unknown.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        unknown.GetProperty("error").GetProperty("message").GetString().Should().Contain("touchBoing");
    }

    /// <summary>
    /// A tap runs the activation behaviour a click has, so a client driving a page with touch reaches the
    /// same document a client driving it with a mouse does — including the navigation a link starts.
    /// </summary>
    [Test]
    public async Task ATapActivatesWhatItLandsOn()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(session, attachment, "<input id='c' type='checkbox'>");
        var point = await PointAsync(session, attachment, "c");

        await Touch(session, attachment, $$"""{"type":"touchStart","touchPoints":[{"x":{{J(point.X)}},"y":{{J(point.Y)}}}]}""");
        await Touch(session, attachment, """{"type":"touchEnd","touchPoints":[]}""");

        (await Evaluate(session, attachment, "String(document.getElementById('c').checked)")).Should().Be("true");
        (await Evaluate(session, attachment, "document.activeElement.id")).Should().Be("c");
    }

    /// <summary>
    /// The two commands are independent: a touch arrives without emulation, and emulation is what a page
    /// detects it by. <c>Emulation.setTouchEmulationEnabled</c>'s handler attributes are real slots, so a
    /// framework that assigns one is heard.
    /// </summary>
    [Test]
    public async Task TouchEmulationAddsTheHandlerAttributesTheDispatchThenRuns()
    {
        await using var session = await PageSession.CreateAsync();
        var page = await session.NewPageAsync();
        var attachment = await session.AttachAsync(await session.TargetForAsync(page));

        await Content(session, attachment, "<button id='b'>tap me</button>");

        (await Evaluate(session, attachment, "['ontouchstart', 'ontouchend', 'ontouchmove', 'ontouchcancel'].map(n => n in window).join(',')"))
            .Should().Be("false,false,false,false");

        await session.ResultAsync("Emulation.setTouchEmulationEnabled", """{"enabled":true,"maxTouchPoints":5}""", attachment);

        (await Evaluate(session, attachment, "['ontouchstart', 'ontouchend', 'ontouchmove', 'ontouchcancel'].map(n => n in window).join(',')"))
            .Should().Be("true,true,true,true");
        (await Evaluate(session, attachment, "['ontouchstart', 'ontouchend', 'ontouchmove', 'ontouchcancel'].map(n => n in document.getElementById('b')).join(',')"))
            .Should().Be("true,true,true,true");

        await session.ResultAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["expression"] =
                    """
                    window.log = [];
                    document.getElementById('b').ontouchstart = e => window.log.push('handler:' + e.touches.length);
                    window.ontouchend = e => window.log.push('window:' + e.changedTouches.length);
                    """,
            }),
            attachment);

        var point = await PointAsync(session, attachment, "b");

        await Touch(session, attachment, $$"""{"type":"touchStart","touchPoints":[{"x":{{J(point.X)}},"y":{{J(point.Y)}}}]}""");
        await Touch(session, attachment, """{"type":"touchEnd","touchPoints":[]}""");

        // The element's own handler, and the window's — the second is what says the event reaches the window.
        (await Evaluate(session, attachment, "window.log.join('|')")).Should().Be("handler:1|window:1");

        // Reading the IDL attribute back answers the function, which is what makes it a slot rather than a
        // marker: a framework that stores and restores a handler gets its own back.
        (await Evaluate(session, attachment, "typeof document.getElementById('b').ontouchstart")).Should().Be("function");

        await session.ResultAsync("Emulation.setTouchEmulationEnabled", """{"enabled":false}""", attachment);

        (await Evaluate(session, attachment, "['ontouchstart', 'ontouchend', 'ontouchmove', 'ontouchcancel'].map(n => n in window).join(',')"))
            .Should().Be("false,false,false,false");
    }

    private static Task Touch(PageSession session, string attachment, string parameters)
        => session.ResultAsync("Input.dispatchTouchEvent", parameters, attachment);

    private static async Task Content(PageSession session, string attachment, string body)
    {
        await session.ResultAsync(
            "Page.setDocumentContent",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["frameId"] = "", ["html"] = body }),
            attachment);
    }

    /// <summary>The centre of an element's box, read the way a client reads it before driving input.</summary>
    private static async Task<(double X, double Y)> PointAsync(PageSession session, string attachment, string id)
    {
        var point = await Evaluate(
            session,
            attachment,
            $"(() => {{ const r = document.getElementById('{id}').getBoundingClientRect(); return [r.left + r.width / 2, r.top + r.height / 2].join(','); }})()");

        var parts = point!.Split(',');
        return (N(parts[0]), N(parts[1]));
    }

    /// <summary>A number as JSON and as JavaScript both write it, whatever culture the runner has.</summary>
    private static string J(double value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static double N(string value)
        => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<string?> Evaluate(PageSession session, string attachment, string expression)
    {
        var result = await session.ResultAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["expression"] = expression, ["returnByValue"] = true }),
            attachment);

        return result.GetProperty("result").GetProperty("value").GetString();
    }
}
