namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Event handler content attributes and the IDL attributes that share their slot —
/// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-attributes.
/// </summary>
public sealed class EventHandlerAttributeTests
{
    [Test]
    public async Task AContentAttributeRunsWithTheEventTheElementAndTheDocumentInScope()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id='target' onclick="window.log = [event.type, this.id, getElementById('other').id, readyState, typeof URL].join(',')"></div>
            <div id='other'></div>
            """);

        await page.EvaluateAsync("document.getElementById('target').dispatchEvent(new MouseEvent('click', { bubbles: true }))");

        // `event` is the sole argument, `this` is the element, and an unqualified name resolves through
        // `with (document) { with (this) { … } }`: getElementById and readyState are the document's, and
        // `URL` answers the document's URL string rather than the global URL constructor, which is exactly
        // what that scope chain means.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("click,target,other,complete,string");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#the-event-handler-processing-algorithm step 5:
    /// an event handler that returns <see langword="false"/> cancels the event, which is what
    /// <c>&lt;a onclick="return false"&gt;</c> has always meant.
    /// </summary>
    [Test]
    public async Task ReturningFalseFromAContentAttributeCancelsTheEvent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <input id='keep' type='checkbox' onclick='return false'>
            <input id='toggle' type='checkbox' onclick='return true'>
            <input id='zero' type='checkbox' onclick='return 0'>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const ids = ['keep', 'toggle', 'zero'];
              const cancelled = ids.map(id =>
                !document.getElementById(id).dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true })));
              const checked = ids.map(id => document.getElementById(id).checked);
              return cancelled.join(',') + '|' + checked.join(',');
            })()
            """))
            // `return 0` cancels nothing: the algorithm tests for the boolean false itself, not for falsiness.
            .Should().Be("true,false,false|false,true,true");
    }

    [Test]
    public async Task AssigningTheIdlAttributeReplacesTheContentAttributesHandlerAndLeavesTheAttributeAlone()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='d' onclick=\"window.log.push('markup')\"></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.log = [];
              const d = document.getElementById('d');
              const fromMarkup = d.onclick;
              d.onclick = function () { window.log.push('assigned'); };
              d.dispatchEvent(new MouseEvent('click'));
              return [
                typeof fromMarkup,
                window.log.join(','),
                d.getAttribute('onclick'),
                d.onclick === null ? 'null' : typeof d.onclick
              ].join('|');
            })()
            """)).Should().Be("function|assigned|window.log.push('markup')|function");
    }

    [Test]
    public async Task AssigningNullRemovesTheHandlerAndANonObjectClearsIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='d' onclick=\"window.ran = true\"></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.ran = false;
              const d = document.getElementById('d');
              d.onclick = null;
              d.dispatchEvent(new MouseEvent('click'));
              const afterNull = window.ran;
              d.onclick = 42;
              const afterNumber = d.onclick;
              return [afterNull, afterNumber === null].join(',');
            })()
            """)).Should().Be("false,true");
    }

    /// <summary>
    /// Changing the content attribute after the handler was assigned from script puts the attribute back in
    /// charge, because HTML's "set the content attribute" step replaces whatever the slot held.
    /// </summary>
    [Test]
    public async Task ChangingTheContentAttributeAfterAScriptAssignmentTakesTheSlotBack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='d' onclick=\"window.log.push('first')\"></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.log = [];
              const d = document.getElementById('d');
              d.dispatchEvent(new MouseEvent('click'));
              d.onclick = () => window.log.push('assigned');
              d.dispatchEvent(new MouseEvent('click'));
              d.setAttribute('onclick', "window.log.push('second')");
              d.dispatchEvent(new MouseEvent('click'));
              d.removeAttribute('onclick');
              d.dispatchEvent(new MouseEvent('click'));
              return window.log.join(',');
            })()
            """)).Should().Be("first,assigned,second");
    }

    /// <summary>
    /// A handler is one entry of the target's listener list, so it takes its turn in registration order — and
    /// a handler the markup declared was registered before any script could run.
    /// </summary>
    [Test]
    public async Task AMarkupHandlerRunsBeforeAListenerAddedLater()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id='d' onclick="window.log.push('attribute')"></div>
            <script>
              window.log = [];
              document.getElementById('d').addEventListener('click', () => window.log.push('listener'));
            </script>
            """);

        await page.EvaluateAsync("document.getElementById('d').dispatchEvent(new MouseEvent('click'))");
        (await page.EvaluateAsync<string>("window.log.join(',')")).Should().Be("attribute,listener");
    }

    [Test]
    public async Task AHandlerReassignedKeepsItsPositionInTheListenerList()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='d'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.log = [];
              const d = document.getElementById('d');
              d.onclick = () => window.log.push('first');
              d.addEventListener('click', () => window.log.push('listener'));
              d.onclick = () => window.log.push('second');
              d.dispatchEvent(new MouseEvent('click'));
              return window.log.join(',');
            })()
            """)).Should().Be("second,listener");
    }

    [Test]
    public async Task TheHandlerIdlAttributesAreEnumerableAccessorsOnThePrototype()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='d'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const d = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'onclick');
              return [
                typeof d.get, typeof d.set, d.enumerable, d.configurable,
                'onclick' in document.getElementById('d'),
                Object.prototype.hasOwnProperty.call(document.getElementById('d'), 'onclick'),
                typeof Object.getOwnPropertyDescriptor(Document.prototype, 'onreadystatechange').get
              ].join(',');
            })()
            """)).Should().Be("function,function,true,true,true,false,function");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/sections.html#the-body-element — <c>&lt;body onload&gt;</c> and
    /// its kind set the <b>Window</b>'s handler, which is the only reason they work at all: <c>load</c> fires
    /// at the window and never reaches the body.
    /// </summary>
    [Test]
    public async Task ABodyHandlerHtmlRedirectsLandsOnTheWindow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <body onload="window.loaded = 'from the body attribute'" onclick="window.clicked = this.tagName">
            <script>window.loaded = 'not yet';</script>
            </body>
            """);

        // onload was redirected to the window and ran when `load` fired there...
        (await page.EvaluateAsync<string>("window.loaded")).Should().Be("from the body attribute");
        (await page.EvaluateAsync<bool>("window.onload !== null")).Should().BeTrue();

        // ...while onclick is not on the redirect list, so it stays the body's own handler.
        await page.EvaluateAsync("document.body.dispatchEvent(new MouseEvent('click'))");
        (await page.EvaluateAsync<string>("window.clicked")).Should().Be("BODY");
        (await page.EvaluateAsync<bool>("window.onclick === null")).Should().BeTrue();
    }

    /// <summary>
    /// HTML compiles a handler when it is first needed, so a body that does not parse is reported then and the
    /// handler is null rather than the dispatch throwing.
    /// </summary>
    [Test]
    public async Task AHandlerWhoseBodyDoesNotParseBecomesNullAndIsReported()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='bad' onclick='this is not javascript('></div><div id='good'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.log = [];
              document.getElementById('good').addEventListener('click', () => window.log.push('other listener'));
              const dispatched = document.getElementById('bad').dispatchEvent(new MouseEvent('click'));
              document.getElementById('good').dispatchEvent(new MouseEvent('click'));
              return [dispatched, document.getElementById('bad').onclick === null, window.log.join(',')].join('|');
            })()
            """)).Should().Be("true|true|other listener");

        page.Errors.Should().NotBeEmpty();
    }

    [Test]
    public async Task AnAttributeWhoseNameIsNotAnHtmlEventHandlerIsJustAnAttribute()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='d' onwhatever=\"window.ran = true\"></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.ran = false;
              const d = document.getElementById('d');
              d.dispatchEvent(new Event('whatever'));
              return [window.ran, d.onwhatever === undefined, d.getAttribute('onwhatever')].join(',');
            })()
            """)).Should().Be("false,true,window.ran = true");
    }
}
