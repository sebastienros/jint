using System.Text.RegularExpressions;
using Jint.Browser;
using Jint.HtmlParser;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;
using NativeBrowser = Jint.Browser.Browser;

namespace Jint.Tests.Browser;

/// <summary>
/// The pieces of the in-process API a test suite written against Playwright reaches for: a context base URL,
/// the locator filter parts, a coordinate mouse with boxes, a response wait, console levels and a text pattern
/// wait.
/// </summary>
public sealed class PageAutomationTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task ARelativeAddressResolvesAgainstTheContextBaseUrl()
    {
        await using var site = await LoopbackPage.CreateAsync(
            server => server
                .MapHtml("/admin/index", "<!doctype html><title>index</title>")
                .MapHtml("/admin/users", "<!doctype html><title>users</title>"),
            context => context.BaseUrl = "http://127.0.0.1:1/unused");

        // A base URL on another port proves an absolute address is left alone.
        (await site.Page.NavigateAsync(site.Url("/admin/index")))!.Ok.Should().BeTrue();

        await using var browser = new NativeBrowser();
        var context = await browser.NewContextAsync(new BrowserContextOptions
        {
            BaseUrl = site.Server.Origin,
            UrlFilter = site.Server.Owns,
        });
        var page = await context.NewPageAsync();

        (await page.NavigateAsync("/admin/index"))!.Url.Should().Be(site.Url("/admin/index"));
        (await page.TitleAsync()).Should().Be("index");

        // Relative to the base URL, not to the current document: the host's address bar, not a link.
        (await page.NavigateAsync("admin/users"))!.Url.Should().Be(site.Url("/admin/users"));
        (await page.WaitForUrlAsync("/admin/users", Patience)).Should().BeTrue();
    }

    [Test]
    public void ABaseUrlMustBeAbsolute()
    {
        var options = new BrowserContextOptions();
        var assign = () => options.BaseUrl = "/relative";
        assign.Should().Throw<ArgumentException>();
        options.BaseUrl = null;
        options.BaseUrl.Should().BeNull();
    }

    [Test]
    public async Task FilterPartsNarrowThePreviousPartsMatches()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <ul>
              <li id=a>Alpha <b>one</b></li>
              <li id=b hidden>Beta one</li>
              <li id=c>Gamma two</li>
              <li id=d style="display:none">Delta two</li>
            </ul>
            """);

        (await page.GetAttributeAsync("li >> nth=1", "id")).Should().Be("b");
        (await page.GetAttributeAsync("li >> nth=-1", "id")).Should().Be("d");
        (await page.CountAsync("li >> nth=9")).Should().Be(0);
        (await page.TextsAsync("li >> visible=true")).Should().Equal("Alpha one", "Gamma two");
        (await page.CountAsync("li >> visible=false")).Should().Be(2);
        (await page.CountAsync("li >> has-text=one")).Should().Be(2);
        (await page.CountAsync("li >> has-text=\"ONE\"")).Should().Be(2);
        (await page.CountAsync("li >> has-text=\"Alpha one\"s")).Should().Be(1);
        (await page.CountAsync("li >> internal:has-text=\"two\"i")).Should().Be(2);
        (await page.CountAsync("li >> has-not-text=one")).Should().Be(2);

        // Filters compose with each other and with the parts after them.
        (await page.GetAttributeAsync("li >> visible=true >> has-text=two", "id")).Should().Be("c");
        (await page.InnerTextAsync("li >> has-text=one >> nth=0 >> b")).Should().Be("one");
    }

    [TestCase("nth=0")]
    [TestCase("visible=true")]
    [TestCase("has-text=x >> li")]
    public async Task AFilterWithNothingBeforeItMatchesNothing(string target)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><li>x</li>");

        (await page.ClickAsync(target)).Should().BeFalse();
        await FluentActions.Awaiting(() => page.CountAsync(target)).Should().ThrowAsync<DomException>();
    }

    [Test]
    public async Task TheCoordinateMouseDragsWithItsButtonHeld()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id=handle>handle</div>
            <div id=target>target</div>
            <script>
              window.log = [];
              for (const type of ['pointerdown', 'mousedown', 'mousemove', 'mouseup', 'click']) {
                document.addEventListener(type, e => log.push(type + ':' + e.target.id + ':' + e.buttons));
              }
            </script>
            """);

        var handle = await page.BoundingBoxAsync("#handle");
        var target = await page.BoundingBoxAsync("#target");
        handle.Should().NotBeNull();
        target.Should().NotBeNull();
        target!.Value.Y.Should().BeGreaterThan(handle!.Value.Y);
        (await page.BoundingBoxAsync("#missing")).Should().BeNull();

        await page.MouseMoveAsync(handle.Value.X + 1, handle.Value.Y + 1);
        await page.MouseDownAsync();
        await page.MouseMoveAsync(target.Value.X + 1, target.Value.Y + 1);
        await page.MouseUpAsync();

        var log = await page.EvaluateAsync<string>("log.join(' ')");
        // The press and the release hit different elements, so the click goes to their nearest common
        // ancestor - the body, which has no id - as UI Events says and Chrome does.
        log.Should().Be("mousemove:handle:0 pointerdown:handle:1 mousedown:handle:1 mousemove:target:1 mouseup:target:0 click::0");

        // A glide is made of evenly spaced moves, each carrying the held buttons.
        await page.EvaluateAsync("log.length = 0");
        await page.MouseDownAsync();
        await page.MouseMoveAsync(handle.Value.X + 1, handle.Value.Y + 1, steps: 4);
        await page.MouseUpAsync();
        (await page.EvaluateAsync<int>("log.filter(e => e.startsWith('mousemove') && e.endsWith(':1')).length")).Should().Be(4);
        (await page.EvaluateAsync<string>("log.slice(-2).join(' ')")).Should().Be("mouseup:handle:0 click::0");

        // A press and a release on the same element is a click.
        await page.EvaluateAsync("log.length = 0");
        await page.MouseDownAsync();
        await page.MouseUpAsync();
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be("pointerdown:handle:1 mousedown:handle:1 mouseup:handle:0 click:handle:0");
    }

    [Test]
    public async Task AResponseWaitAnswersOnlyARequestSentAfterIt()
    {
        await using var site = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<!doctype html><title>home</title>")
            .Map("/api/data", _ => LoopbackResponse.Json("{\"n\":1}"))
            .Map("/api/missing", _ => new LoopbackResponse { Status = 404, Reason = "Not Found" }));

        await site.Page.NavigateAsync(site.Url("/"));
        await site.Page.EvaluateAndAwaitAsync("fetch('/api/data').then(r => r.text())");

        // Already answered before the wait started: never the answer.
        var stale = site.Page.WaitForResponseAsync(r => r.Url.EndsWith("/api/data", StringComparison.Ordinal), TimeSpan.FromMilliseconds(300));
        (await stale).Should().BeNull();

        var missing = site.Page.WaitForResponseAsync(r => r.Url.EndsWith("/api/missing", StringComparison.Ordinal), Patience);
        await site.Page.EvaluateAsync("fetch('/api/missing')");
        var response = await missing;
        response.Should().NotBeNull();
        response!.Status.Should().Be(404);
    }

    [Test]
    public async Task ConsoleEntriesKeepTheLevelOfEachMessage()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><script>console.log('a'); console.error('b', 1); console.warn('c')</script>");

        page.ConsoleEntries.Should().Equal(
            new PageConsoleMessage(ConsoleLogLevel.Log, "a"),
            new PageConsoleMessage(ConsoleLogLevel.Error, "b 1"),
            new PageConsoleMessage(ConsoleLogLevel.Warn, "c"));
        page.ConsoleMessages.Should().Equal("a", "b 1", "c");
    }

    [Test]
    public async Task ATextPatternWaitMatchesTheRenderedText()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><span id=count>13</span><script>setTimeout(() => count.textContent = '3', 50)</script>");

        // "3" is a substring of "13": an exact count needs an anchored pattern.
        (await page.WaitForTextAsync("#count", new Regex("^3$"), Patience)).Should().BeTrue();
        (await page.WaitForTextAsync("#missing", new Regex(".*"), TimeSpan.FromMilliseconds(100))).Should().BeFalse();
    }

    /// <summary>
    /// A mouse event moves <c>:hover</c> before its listeners run, which ends the style query the hit test
    /// ran on. The offsets every pointer event carries are measured from that hit test, so they are read
    /// before the move: read after it, they were a read of a stale query, and the click threw
    /// "The native CSS query was invalidated by mutation" instead of clicking.
    /// </summary>
    [Test]
    public async Task AClickThatMovesHoverStillMeasuresItsOffsetsFromTheHitTest()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        var rows = string.Concat(Enumerable.Range(0, 60).Select(i => $"<li><button id=b{i}>Item {i}</button></li>"));
        await page.SetContentAsync($$"""
            <!doctype html>
            <style>button:hover { outline: 1px solid red } li:hover > button { color: blue }</style>
            <ul>{{rows}}</ul>
            <script>
              window.offsets = [];
              document.addEventListener('pointerdown', e => offsets.push(e.offsetY >= 0));
            </script>
            """);

        for (var i = 59; i >= 0; i -= 7)
        {
            (await page.ClickAsync($"#b{i}")).Should().BeTrue();
        }

        (await page.EvaluateAsync<int>("offsets.filter(Boolean).length")).Should().Be(9);
    }

    /// <summary>
    /// A mouse drag that starts on a draggable element runs HTML's drag-and-drop processing model: dragstart
    /// once the pointer has moved, drag events instead of mouse events while it moves, a drop where the last
    /// dragover was cancelled, then dragend — and no mouseup and no click. What a sortable list using native
    /// drag and drop (SortableJS) needs to reorder at all.
    /// </summary>
    [Test]
    public async Task AMouseDragOnADraggableElementRunsTheDragAndDropModel()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id=source draggable=true>source</div>
            <div id=middle>middle</div>
            <div id=zone>zone</div>
            <script>
              window.log = [];
              for (const type of ['dragstart', 'drag', 'dragenter', 'dragleave', 'dragover', 'drop', 'dragend', 'mouseup', 'click']) {
                document.addEventListener(type, e => {
                  if (type !== 'drag') log.push(type + ':' + e.target.id);
                  if (type === 'dragstart') e.dataTransfer.setData('text/plain', 'payload');
                  if (type === 'dragover' && e.target.id === 'zone') e.preventDefault();
                  if (type === 'drop') log.push('data:' + e.dataTransfer.getData('text/plain'));
                });
              }
            </script>
            """);

        var source = (await page.BoundingBoxAsync("#source"))!.Value;
        var middle = (await page.BoundingBoxAsync("#middle"))!.Value;
        var zone = (await page.BoundingBoxAsync("#zone"))!.Value;

        await page.MouseMoveAsync(source.X + 5, source.Y + 5);
        await page.MouseDownAsync();
        await page.MouseMoveAsync(source.X + 6, source.Y + 6); // under the threshold: still a press
        await page.MouseMoveAsync(middle.X + 5, middle.Y + 5);
        await page.MouseMoveAsync(zone.X + 5, zone.Y + 5);
        await page.MouseUpAsync();

        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be(
            "dragstart:source dragenter:middle dragover:middle " +
            "dragenter:zone dragleave:middle dragover:zone drop:zone data:payload dragend:source");

        // A drag nobody accepts ends with a dragleave instead of a drop.
        await page.EvaluateAsync("log.length = 0");
        await page.MouseMoveAsync(source.X + 5, source.Y + 5);
        await page.MouseDownAsync();
        await page.MouseMoveAsync(middle.X + 5, middle.Y + 5);
        await page.MouseUpAsync();
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be("dragstart:source dragenter:middle dragover:middle dragleave:middle dragend:source");

        // A press and release without moving is still a click.
        await page.EvaluateAsync("log.length = 0");
        await page.MouseDownAsync();
        await page.MouseUpAsync();
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be("mouseup:middle click:middle");
    }

    [Test]
    public async Task TypingWithoutATargetTypesAtTheFocusedElement()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id=editor>click me</div><textarea id=hidden></textarea>
            <script>editor.addEventListener('mousedown', e => { e.preventDefault(); hidden.focus(); });</script>
            """);

        (await page.ClickAsync("#editor")).Should().BeTrue();
        (await page.TypeAsync("héllo")).Should().BeTrue();
        (await page.InputValueAsync("#hidden")).Should().Be("héllo");
    }

    [Test]
    public async Task ActionsWaitForTheirTargetWithAnActionTimeout()
    {
        await using var browser = new NativeBrowser();
        var waiting = await (await browser.NewContextAsync(new BrowserContextOptions { ActionTimeout = Patience })).NewPageAsync();
        var looking = await browser.NewPageAsync();
        const string html = """
            <!doctype html>
            <button id=go hidden onclick="document.title = 'clicked'">Go</button>
            <script>
              setTimeout(() => {
                go.hidden = false;
                document.body.insertAdjacentHTML('beforeend', '<input id=late><select id=pick><option>a<option>b</select>');
              }, 100);
            </script>
            """;

        // Looking once, the default, answers false while the button is hidden and the controls are missing.
        await looking.SetContentAsync(html);
        (await looking.ClickAsync("#go")).Should().BeFalse();
        (await looking.FillAsync("#late", "x")).Should().BeFalse();

        await waiting.SetContentAsync(html);
        (await waiting.ClickAsync("#go")).Should().BeTrue();
        (await waiting.TitleAsync()).Should().Be("clicked");
        (await waiting.FillAsync("#late", "x")).Should().BeTrue();
        (await waiting.SelectAsync("#pick", "b")).Should().BeTrue();
        (await waiting.InputValueAsync("#pick")).Should().Be("b");

        // A target that never comes is false once the timeout is up.
        var options = new BrowserContextOptions { ActionTimeout = TimeSpan.FromMilliseconds(200) };
        var bounded = await (await browser.NewContextAsync(options)).NewPageAsync();
        await bounded.SetContentAsync("<!doctype html>");
        (await bounded.ClickAsync("#never")).Should().BeFalse();
    }

    [Test]
    public async Task OptionConstructsAnOptionElement()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><select id=s><option>a</option></select>");

        (await page.EvaluateAsync<string>("""
            const o = new Option('Label', 'v', true, true);
            s.add(o);
            [o instanceof HTMLOptionElement, o.text, o.value, o.defaultSelected, o.selected, s.value, new Option().outerHTML].join('|')
            """)).Should().Be("true|Label|v|true|true|v|<option></option>");
    }

    /// <summary>
    /// A descendant whose content is only whitespace has a name from content — the whitespace — so its
    /// title is not reached (accname step 2F.v): an icon span with a tooltip does not name a button twice.
    /// </summary>
    [Test]
    public async Task ADescendantsTitleIsNotItsNameWhenItHasWhitespaceContent()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=menu><span class=icon title="Content">
                <i aria-hidden="true"></i>
            </span>
            <span class=title>Content</span></button>
            <button id=bare><span title="Tip"></span>Bare</button>
            <a href=#><span style="display:flex" title="Tip"><i aria-hidden=true></i></span>Flex</a>
            <bdi id=label>x</bdi>
            """);

        (await page.CountAsync("role=button[name=\"Content\"s]")).Should().Be(1);
        (await page.CountAsync("role=button[name=\"TipBare\"s]")).Should().Be(1); // an empty inline descendant falls back to its title
        (await page.CountAsync("role=link[name=\"Flex\"s]")).Should().Be(1); // a block-level one does not, as in Chromium
        (await page.EvaluateAsync<string>("getComputedStyle(label).unicodeBidi")).Should().Be("isolate");
    }

    [Test]
    public async Task ADoubleClickIsTwoClicksThenADblclick()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id=node>node</div>
            <script>
              window.log = [];
              for (const type of ['mousedown', 'mouseup', 'click', 'dblclick']) node.addEventListener(type, e => log.push(type + e.detail));
            </script>
            """);

        (await page.DoubleClickAsync("#node")).Should().BeTrue();
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be("mousedown1 mouseup1 click1 mousedown2 mouseup2 click2 dblclick2");
    }

    /// <summary>
    /// The pointer moving onto an element moves :hover, which can hide content above it - a toolbar revealed
    /// while another element was hovered - and move the element. The press goes where the element is once the
    /// pointer is over it.
    /// </summary>
    [Test]
    public async Task AClickAimsAtTheElementAfterThePointerMovedOverIt()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <style>.tools { display: none } #zone:hover .tools { display: block }</style>
            <div id=zone>zone<div class=tools><p>a</p><p>b</p><p>c</p><p>d</p><p>e</p></div></div>
            <button id=save onclick="document.title = 'saved'">Save</button>
            """);

        (await page.HoverAsync("#zone")).Should().BeTrue();
        (await page.ClickAsync("#save")).Should().BeTrue();
        (await page.TitleAsync()).Should().Be("saved");
    }

    [Test]
    public async Task HidingACaretWithVisibilityMovesNothingAClickAimsAt()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();

        // The blur the press causes hides a caret above the button; a visibility: hidden box keeps its place.
        await page.SetContentAsync("""
            <!doctype html>
            <style>.caret { visibility: hidden } .editor:focus-within .caret { visibility: visible }</style>
            <div class=editor><textarea id=input></textarea><div class=caret><p>a</p><p>b</p></div></div>
            <button id=save onclick="document.title = 'saved'">Save</button>
            """);

        (await page.ClickAsync("#input")).Should().BeTrue();
        (await page.ClickAsync("#save")).Should().BeTrue();
        (await page.TitleAsync()).Should().Be("saved");
    }
}
