namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// The libraries that are not a rendering framework: React's hydration of server markup, jQuery, htmx and
/// Alpine — and custom elements, which are the platform rather than a library.
/// </summary>
/// <remarks>
/// Each is here because it reaches a part of the browser the TodoMVC four do not: hydration adopts nodes the
/// parser made, jQuery makes a <i>synchronous</i> XMLHttpRequest, htmx swaps fragments in from the network
/// and boosts an ordinary link into one, and Alpine compiles attributes it found by walking the document and
/// keeps watching it with a <c>MutationObserver</c>.
/// </remarks>
public class LibraryFixtureTests
{
    /// <summary>
    /// React hydrates the server's markup rather than replacing it, and reports nothing while doing so.
    /// </summary>
    /// <remarks>
    /// The stamp is what makes this a hydration test rather than a rendering one: every node the document
    /// arrived with is marked before <c>hydrateRoot</c> runs, and a node React threw away and rebuilt would
    /// lose its mark. <c>onRecoverableError</c> is React's own mismatch report and has to stay empty.
    /// </remarks>
    [Test]
    public async Task ReactHydratesServerRenderedMarkupWithoutAWarning()
    {
        await using var course = await FixtureCourse.OpenAsync("ssr-hydration");

        await course.UntilAsync("window.__recovered && window.__recovered.length", "0");
        await course.UntilAsync("window.__stampsKept()", "7");

        (await course.TextAsync("#count")).Should().Be("count: 2");
        (await course.TextsAsync(".notes li")).Should().Be("alpha|beta");

        // And it is live: the adopted button carries the listener hydration attached to it.
        await course.ClickAsync("#inc");
        await course.UntilAsync("document.querySelector('#count').textContent", "count: 3");

        // Still the server's nodes, after a client render on top of them.
        await course.UntilAsync("window.__stampsKept()", "7");

        course.Page.ConsoleMessages.Should().NotContain(message => message.Contains("Hydration", StringComparison.Ordinal));
        course.ShouldHaveReportedNothing();
    }

    /// <summary>jQuery's synchronous <c>$.ajax</c>, its ready callback and its delegated events.</summary>
    /// <remarks>
    /// <c>async: false</c> is the interesting one. It is an <c>XMLHttpRequest</c> that blocks the page's own
    /// thread until the origin answers, from inside a callback the page loop is running — so a page that
    /// pumped its event loop to serve it would run its own timers in the middle of a script. It does not:
    /// the request blocks on the transport directly.
    /// </remarks>
    [Test]
    public async Task JQueryMakesASynchronousRequestAndDelegatesEvents()
    {
        await using var course = await FixtureCourse.OpenAsync("jquery");

        await course.UntilAsync("document.querySelector('#ready').textContent", "yes");
        (await course.TextAsync("#synchronous")).Should().Be("alpha,beta,gamma");

        // A row that was in the document when the delegated handler was registered.
        await course.ClickAtAsync("#list li", 0);
        (await course.TextAsync("#clicked")).Should().Be("one");

        // …and one that jQuery added afterwards, which is the whole reason delegation exists.
        await course.ClickAsync("#add");
        await course.UntilAsync("document.querySelectorAll('#list li').length", "3");

        await course.ClickAtAsync("#list li", 2);
        (await course.TextAsync("#clicked")).Should().Be("three");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>htmx's three entry points: a load trigger, a click that swaps, and a boosted link.</summary>
    /// <remarks>
    /// <b>This is the case DOM XPath bought.</b> htmx 2 evaluates
    /// <c>const … = (new XPathEvaluator).createExpression(…)</c> at the top level of its bundle, to find the
    /// <c>hx-on:</c> attributes, so the whole library was a <c>ReferenceError</c> before any <c>hx-</c>
    /// attribute was read, and this was the corpus's <c>needs triage</c> row for a feature that did not
    /// exist. Everything below was unreached rather than refuted.
    /// </remarks>
    [Test]
    public async Task HtmxSwapsFragmentsAndBoostsALink()
    {
        await using var course = await FixtureCourse.OpenAsync("htmx");

        // hx-trigger="load": nobody interacted, and the fragment is already in.
        await course.UntilAsync("document.querySelector('#on-load').textContent.trim()", "hello from the server");

        await course.ClickAsync("#fetch-rows");
        await course.UntilAsync("document.querySelectorAll('#rows .rows li').length", "3");
        (await course.TextsAsync("#rows .rows li")).Should().Be("alpha|beta|gamma");

        // hx-boost turns the link into a fetch and a pushState: the document is never replaced, so this is
        // not a navigation, and the URL moves anyway.
        await course.ClickAsync("#boosted");
        await course.UntilAsync("document.querySelector('#heading').textContent", "next page");
        await course.UntilAsync("location.pathname", "/htmx/next.html");
        (await course.TextAsync("#arrived")).Should().Be("arrived by boost");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>Alpine's <c>x-data</c>, <c>x-model</c>, <c>x-text</c> and <c>x-show</c>.</summary>
    [Test]
    public async Task AlpineBindsAModelAndShowsAndHides()
    {
        await using var course = await FixtureCourse.OpenAsync("alpine");

        await course.UntilAsync("document.querySelector('#greeting').textContent", "nobody");

        // x-show is a `display` on the element's own style, so this is also a test that the style attribute
        // Alpine writes reaches the CSSOM the page reads back.
        await course.UntilAsync("document.querySelector('#details').style.display", "none");

        await course.TypeAsync("#name", "Ada");
        await course.UntilAsync("document.querySelector('#greeting').textContent", "hello Ada");
        await course.UntilAsync("document.querySelector('#length').textContent", "3");

        await course.ClickAsync("#toggle");
        await course.UntilAsync("document.querySelector('#details').style.display", "");

        await course.ClickAsync("#toggle");
        await course.UntilAsync("document.querySelector('#details').style.display", "none");

        course.ShouldHaveReportedNothing();
    }

    /// <summary>
    /// A custom element upgraded by the parser, moved through the tree, and told its attribute changed.
    /// </summary>
    /// <remarks>
    /// <b>The fixture was checked in before the feature was.</b> It was written against a branch with no
    /// <c>customElements</c> at all, marked <c>[Explicit]</c> with a <c>needs triage</c> row beside it, and
    /// turned on by deleting one attribute once the registry, the upgrade and the four reactions landed —
    /// which is the shape a triage row is supposed to have.
    /// </remarks>
    [Test]
    public async Task ACustomElementIsUpgradedAndHearsItsReactions()
    {
        await using var course = await FixtureCourse.OpenAsync("custom-elements");

        await course.UntilAsync("document.querySelector('#reactions').textContent", "defined");

        // The parser made `<my-counter id="first">` before the definition existed, so this is an upgrade.
        await course.UntilAsync("document.querySelector('#first').textContent", "count 2");
        (await course.TextAsync("#reactions")).Should().Be("defined");

        await course.Page.EvaluateAsync("__addOne()");
        await course.UntilAsync("document.querySelector('#second').textContent", "count 10");

        await course.ClickAsync("#first");
        await course.UntilAsync("document.querySelector('#first').textContent", "count 3");

        await course.Page.EvaluateAsync("__removeOne()");
        await course.UntilAsync("window.__reactions().indexOf('disconnected:second') >= 0", "true");

        course.ShouldHaveReportedNothing();
    }
}
