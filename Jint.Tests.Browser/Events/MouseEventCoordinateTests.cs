using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// CSSOM View §10's <c>pageX</c>/<c>pageY</c> and <c>offsetX</c>/<c>offsetY</c>, which are the four
/// <c>MouseEvent</c> members whose answer depends on something other than the event.
/// <para>
/// https://drafts.csswg.org/cssom-view/#extensions-to-the-mouseevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one of the four is two algorithms, and the dispatch flag chooses.</b> While it is set — which is
/// the whole of the time a listener can read the event — each returns "the position where the event
/// occurred": <c>pageY</c> relative to the initial containing block, <c>offsetY</c> relative to the padding
/// edge of the target node. Only once the dispatch is over does <c>pageY</c> become <c>clientY</c> plus the
/// window's <i>current</i> <c>scrollY</c>, and <c>offsetY</c> become <c>pageY</c>.
/// </para>
/// <para>
/// So the question these ask is whether a listener can move the answer out from under itself, which is what
/// <see href="https://github.com/sebastienros/jint/issues/3698">#3698</see> item 4 records: the values were
/// computed on every read, so scrolling — or moving the target — inside a listener changed the coordinates of
/// the event that listener was handling.
/// </para>
/// </remarks>
public sealed class MouseEventCoordinateTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OffsetPreparationOnlyMeasuresTheListenedToTarget(bool listen)
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser,
            "<style>#target { display: block } #unrelated { display: block }</style><button id=target>Save</button>"
            + "<main id=unrelated>" + string.Concat(Enumerable.Repeat("<div>row</div>", 100)) + "</main>");
        await page.EvaluateAsync($$"""
            const target = document.getElementById('target');
            if ({{(listen ? "true" : "false")}}) target.addEventListener('click', () => {});
            """);
        var used = await page.RunOnLoopAsync(engine =>
        {
            var tracker = new CssRuleUsageTracker();
            tracker.Rebind(PageRuntime.Find(engine)!.Document!);
            CssRuleUsage.Arm(tracker);
            try
            {
                engine.Evaluate("target.dispatchEvent(new MouseEvent('click'))");
                return tracker.TakeDelta().Select(rule => rule.SelectorText).ToArray();
            }
            finally
            {
                CssRuleUsage.Disarm(tracker);
            }
        });
        used.Length.Should().Be(listen ? 1 : 0);
        used.Should().NotContain("#unrelated");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CallbackObjectGetterCannotMoveTheFirstOffset()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser, "<div id=before></div><div id=target></div>");
        (await page.EvaluateAsync<double>("""
            (() => {
              const target = document.getElementById('target');
              const y = target.getBoundingClientRect().y + 3;
              let seen;
              target.addEventListener('click', {get handleEvent() {
                target.remove();
                return e => { seen = e.offsetY; };
              }});
              target.dispatchEvent(new MouseEvent('click', {clientY: y}));
              return seen;
            })()
            """)).Should().Be(3);
    }

    [TestCase("target.remove()")]
    [TestCase("document.getElementById('before').remove()")]
    [TestCase("target.style.display = 'none'")]
    public async Task MutationBeforeTheFirstReadKeepsSyntheticOffsets(string mutation)
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser, "<div id=before></div><div id=target></div>");

        (await page.EvaluateAsync<string>($$"""
            (() => {
              const target = document.getElementById('target');
              const box = target.getBoundingClientRect();
              let seen;
              document.addEventListener('click', e => { {{mutation}}; }, true);
              target.addEventListener('click', e => { seen = e.offsetX + '|' + e.offsetY; });
              target.dispatchEvent(new MouseEvent('click', {
                clientX: box.x + 7, clientY: box.y + 3, bubbles: true
              }));
              return seen;
            })()
            """)).Should().Be("7|3");
    }

    [Test]
    public async Task RedispatchMeasuresTheNewTargetBeforeItsListener()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser, "<div id=first></div><div id=second></div>");

        (await page.EvaluateAsync<bool>("""
            (() => {
              const first = document.getElementById('first');
              const second = document.getElementById('second');
              const e = new MouseEvent('click', {clientX: 7, clientY: 100});
              let a, b;
              const expectedA = 100 - first.getBoundingClientRect().y;
              first.addEventListener('click', e => { first.remove(); a = e.offsetY; });
              first.dispatchEvent(e);
              const expectedB = 100 - second.getBoundingClientRect().y;
              second.addEventListener('click', e => { second.remove(); b = e.offsetY; });
              second.dispatchEvent(e);
              return a === expectedA && b === expectedB && e.offsetY === e.pageY;
            })()
            """)).Should().BeTrue();
    }

    /// <summary>
    /// A document tall enough to scroll, with a target well down it.
    /// </summary>
    /// <remarks>
    /// It is built out of <i>many</i> elements rather than one tall one, because the flat box model gives
    /// every rendered element a 16-pixel row in tree order and reads no CSS height at all — a
    /// <c>style="height: 400px"</c> block is 16 pixels here, and a document of three of them does not scroll.
    /// </remarks>
    private static string TallPage(int rows = 120)
    {
        var html = new System.Text.StringBuilder();
        for (var i = 0; i < rows; i++)
        {
            html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<div id=\"row{i}\">row {i}</div>");
        }

        html.Append("<div id=\"target\">click me</div>");
        return html.ToString();
    }

    private static async Task<global::Jint.Browser.Page> PageAsync(Browser browser, string? html = null)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html ?? TallPage());
        return page;
    }

    /// <summary>
    /// <c>pageY</c> step 1: with the dispatch flag set it is the position the event occurred at, so a
    /// listener that scrolls and reads again reads the same number.
    /// </summary>
    [Test]
    public async Task ScrollingInsideAListenerDoesNotMoveThePageCoordinate()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<string>("""
            (() => {
              const seen = [];
              const target = document.getElementById('target');
              target.addEventListener('click', e => {
                seen.push(e.pageY);
                window.scrollTo(0, 120);
                seen.push(e.pageY);
              });

              window.scrollTo(0, 0);
              target.dispatchEvent(new MouseEvent('click', { clientY: 40, bubbles: true }));
              return seen.join('|') + ' scrollY=' + window.scrollY;
            })()
            """)).Should().Be("40|40 scrollY=120", "the coordinate is the position the event occurred at");
    }

    /// <summary>
    /// And the scroll that was in force when the dispatch began <i>is</i> part of it — a page already
    /// scrolled reports the document coordinate rather than the viewport one.
    /// </summary>
    [Test]
    public async Task ThePageCoordinateIsTheDocumentPositionTheEventOccurredAt()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<double>("""
            (() => {
              let seen = 0;
              const target = document.getElementById('target');
              target.addEventListener('click', e => { seen = e.pageY; });

              window.scrollTo(0, 100);
              target.dispatchEvent(new MouseEvent('click', { clientY: 40, bubbles: true }));
              return seen;
            })()
            """)).Should().Be(140);
    }

    /// <summary>
    /// <c>offsetY</c> step 1: it is relative to the target's padding edge as the event found it, so a
    /// listener that moves the target and reads again reads the same number.
    /// </summary>
    /// <remarks>
    /// The mutation here removes a block <i>above</i> the target, which in the flat box model moves every
    /// element after it — the cheapest way for a listener to change its own box without touching itself.
    /// </remarks>
    [Test]
    public async Task MovingTheTargetInsideAListenerDoesNotMoveTheOffsetCoordinate()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<string>("""
            (() => {
              const seen = [];
              const target = document.getElementById('target');
              target.addEventListener('click', e => {
                seen.push(e.offsetY);
                document.getElementById('row0').remove();
                seen.push(e.offsetY);
              });

              window.scrollTo(0, 0);
              target.dispatchEvent(new MouseEvent('click', { clientY: 40, bubbles: true }));
              return seen[0] === seen[1] ? 'same:' + seen[0] : 'moved:' + seen[0] + '->' + seen[1];
            })()
            """)).Should().StartWith(
            "same:",
            "the coordinate is relative to the padding edge the event found, not to the one the listener made");
    }

    [TestCase(0)]
    [TestCase(100)]
    public async Task ScrollingBeforeTheFirstOffsetReadUsesTheDispatchScroll(int initialScroll)
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<bool>($$"""
            (() => {
              const target = document.getElementById('target');
              window.scrollTo(0, {{initialScroll}});
              const expected = 40 - target.getBoundingClientRect().top;
              let offset;
              target.addEventListener('click', e => {
                window.scrollTo(0, 200);
                offset = e.offsetY;
              });

              target.dispatchEvent(new MouseEvent('click', { clientY: 40, bubbles: true }));
              return window.scrollY === 200 && offset === expected;
            })()
            """)).Should().BeTrue("scrolling cannot change the position where the event occurred");
    }

    /// <summary>
    /// Two listeners of one dispatch agree, which is the same rule seen from the other side: the second
    /// reader is handed what the first was, however much the first moved the page.
    /// </summary>
    [Test]
    public async Task TwoListenersOfOneDispatchReadTheSameCoordinates()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<bool>("""
            (() => {
              const seen = [];
              const target = document.getElementById('target');
              target.addEventListener('click', e => { seen.push(e.pageY); window.scrollTo(0, 200); });
              target.addEventListener('click', e => { seen.push(e.pageY); });

              window.scrollTo(0, 0);
              target.dispatchEvent(new MouseEvent('click', { clientY: 40, bubbles: true }));
              return seen.length === 2 && seen[0] === seen[1];
            })()
            """)).Should().BeTrue();
    }

    /// <summary>
    /// Steps 2 and 3, the half that must stay live: an event nobody is dispatching answers <c>clientY</c>
    /// plus the window's <i>current</i> scroll offset, so reading one after scrolling moves it.
    /// </summary>
    [Test]
    public async Task OutsideADispatchThePageCoordinateFollowsTheWindowsScroll()
    {
        await using var browser = new Browser();
        var page = await PageAsync(browser);

        (await page.EvaluateAsync<string>("""
            (() => {
              const e = new MouseEvent('click', { clientY: 40 });
              window.scrollTo(0, 0);
              const before = e.pageY;
              window.scrollTo(0, 150);
              return before + '|' + e.pageY;
            })()
            """)).Should().Be("40|190", "with the dispatch flag unset the sum is taken afresh");
    }
}
