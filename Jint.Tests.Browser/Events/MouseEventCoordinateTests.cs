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
