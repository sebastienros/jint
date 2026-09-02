using Jint.Browser;
using Jint.Native.Object;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The window a page's scripts see: one global object with <c>Window.prototype</c> in its chain, the
/// per-document singletons as own globals, and the members a page reads off it.
/// </summary>
public sealed class WindowTests
{
    [Test]
    public async Task TheGlobalObjectIsTheWindow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("window === globalThis")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("self === window")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("top === window && parent === window && frames === window")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.defaultView === window")).Should().BeTrue();
    }

    [Test]
    public async Task TheWindowIsAWindowAndAnEventTarget()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("window instanceof Window")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window instanceof EventTarget")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("Object.getPrototypeOf(window) === Window.prototype")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("Object.getPrototypeOf(Window.prototype) === EventTarget.prototype")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("Object.getPrototypeOf(Window) === EventTarget")).Should().BeTrue();
        (await page.EvaluateAsync<string>("Object.prototype.toString.call(window)")).Should().Be("[object Window]");
    }

    [Test]
    public async Task AGlobalVariableStillResolvesThroughTheGlobalObject()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // The prototype swap must not disturb how a name resolves: a var declaration is an own property of
        // the global, an assignment without one is too, and both are readable as bare identifiers.
        await page.SetContentAsync("<script>var declared = 1; assigned = 2;</script>");

        (await page.EvaluateAsync<int>("declared + assigned")).Should().Be(3);
        (await page.EvaluateAsync<bool>("Object.getOwnPropertyNames(window).includes('declared')")).Should().BeTrue();

        // And a name that exists only on Window.prototype resolves as a bare identifier too, which is what
        // the global environment record's prototype lookup is for.
        (await page.EvaluateAsync<int>("innerWidth")).Should().Be(1280);
    }

    [Test]
    public async Task ThePrototypeSwapCostsNeitherShapeItsSharedLayout()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // A shaped object is a valid holder for the inline caches the whole design exists for, and a
        // dictionary-mode one is not. The global takes a prototype and eight own lazy globals here, and
        // Window.prototype takes the constructor slot; neither may cost its layout.
        await page.SetContentAsync("<script>var declared = 1; innerWidth</script>");

        var shaped = await page.RunOnLoopAsync(engine => new
        {
            Global = engine.Advanced.HasSharedShape(engine.Realm.GlobalObject),
            WindowPrototype = engine.Advanced.HasSharedShape((ObjectInstance) engine.Evaluate("Window.prototype")),
        });

        var control = await ControlEngineGlobalIsShaped();

        control.Should().BeTrue("an ordinary web-API engine keeps its global object's shared layout, which is what makes the comparison below say anything");
        shaped.WindowPrototype.Should().BeTrue("Window.prototype is instantiated from a JsObjectShape and only takes the constructor slot the shape declared");
        shaped.Global.Should().Be(
            control,
            "installing a Window prototype and the per-document singletons must leave the global object's layout exactly as an ordinary web-API engine has it");
    }

    /// <summary>
    /// Whether a plain web-API engine's global object has a shared shape, so that the assertion above
    /// compares against what the engine does rather than against a guess.
    /// </summary>
    private static Task<bool> ControlEngineGlobalIsShaped()
    {
        using var engine = new Engine(options => options.UseWebApis());
        engine.Evaluate("var declared = 1; typeof globalThis");
        return Task.FromResult(engine.Advanced.HasSharedShape(engine.Realm.GlobalObject));
    }

    [Test]
    public async Task TheViewportAnswersDimensionsAndTheScreen()
    {
        var options = new BrowserOptions { Viewport = new Viewport(800, 600, 2) };
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<int>("window.innerWidth")).Should().Be(800);
        (await page.EvaluateAsync<int>("window.innerHeight")).Should().Be(600);
        (await page.EvaluateAsync<int>("window.outerWidth")).Should().Be(800);
        (await page.EvaluateAsync<double>("window.devicePixelRatio")).Should().Be(2);
        (await page.EvaluateAsync<int>("window.scrollX + window.scrollY + window.pageXOffset")).Should().Be(0);
        (await page.EvaluateAsync<int>("screen.width")).Should().Be(800);
        (await page.EvaluateAsync<int>("screen.availHeight")).Should().Be(600);
        (await page.EvaluateAsync<int>("screen.colorDepth")).Should().Be(24);
        (await page.EvaluateAsync("window.scrollTo(0, 100)")).Should().BeNull();
    }

    [Test]
    public async Task MatchMediaAnswersFromTheViewport()
    {
        var options = new BrowserOptions { Viewport = new Viewport(800, 600) };
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("matchMedia('(min-width: 600px)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(min-width: 1000px)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(max-width: 1000px)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('screen and (orientation: landscape)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-color-scheme: dark)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: no-preference)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('print').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('not screen').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(min-width:600px)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(min-aspect-ratio: 1/1)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(max-aspect-ratio: 1/1)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(hover: none)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(hover)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(monochrome)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('(color)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('(min-width: 2000px), (min-height: 100px)').matches")).Should().BeTrue();
        (await page.EvaluateAsync<string>("matchMedia('(min-width: 1px)').media")).Should().Be("(min-width: 1px)");
        (await page.EvaluateAsync<bool>("matchMedia('all') instanceof MediaQueryList")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('all') instanceof EventTarget")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("matchMedia('all').constructor === MediaQueryList")).Should().BeTrue();

        // Media Queries 4: an unknown feature makes its query false however it is written, so `not` over one
        // does not turn it into a match.
        (await page.EvaluateAsync<bool>("matchMedia('(bogus-feature: 1)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('not (bogus-feature: 1)').matches")).Should().BeFalse();
        (await page.EvaluateAsync<bool>("matchMedia('only').matches")).Should().BeFalse();
    }

    [Test]
    public async Task AWindowErrorHandlerAttributeIsAListener()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.reported = null;
              window.onerror = e => { window.reported = 'seen' };
              setTimeout(() => { throw new Error('from a timer') }, 1);
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await page.EvaluateAsync<string>("window.reported")).Should().Be("seen");
        page.Errors.Should().ContainSingle();
    }

    [Test]
    public async Task TheOwnMembersTheRuntimeAddsAreInvisibleToEnumeration()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // A browser has defaultView and currentScript on Document.prototype, so neither shows up in an
        // enumeration of the document's own keys. Making them non-enumerable own properties keeps that true —
        // and keeps a conversion of the document from walking into the window and back.
        (await page.EvaluateAsync<string>("Object.keys(document).join(',')")).Should().BeEmpty();
        (await page.EvaluateAsync<bool>("Object.getOwnPropertyNames(document).includes('defaultView')")).Should().BeTrue();

        // Location's members are [LegacyUnforgeable]: enumerable, and a page cannot delete them.
        (await page.EvaluateAsync<bool>("Object.keys(location).includes('assign')")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("delete location.assign")).Should().BeFalse();
        (await page.EvaluateAsync<string>("typeof location.assign")).Should().Be("function");
    }

    [Test]
    public async Task GetComputedStyleReadsTheCascadeWithNoLayoutBehindIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<style>.big { font-weight: bold }</style><p class='big' id='p'>text</p>");

        var weight = await page.EvaluateAsync<string>(
            "getComputedStyle(document.getElementById('p')).getPropertyValue('font-weight')");

        weight.Should().Be("bold");
    }

    [Test]
    public async Task ADialogIsDismissedWhenNothingIsListening()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<bool>("confirm('really?')")).Should().BeFalse();
        (await page.EvaluateAsync("prompt('name?')")).Should().BeNull();
        (await page.EvaluateAsync("alert('hello')")).Should().BeNull();
    }

    [Test]
    public async Task ADialogHandlerDecidesWhatTheScriptSees()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var seen = new List<string>();
        page.DialogOpened += (_, args) =>
        {
            seen.Add(args.Kind + ":" + args.Message);
            args.Accepted = true;
            args.PromptText = "answered";
        };

        (await page.EvaluateAsync<bool>("confirm('really?')")).Should().BeTrue();
        (await page.EvaluateAsync<string>("prompt('name?', 'default')")).Should().Be("answered");

        seen.Should().Equal("Confirm:really?", "Prompt:name?");
    }

    [Test]
    public async Task WindowOpenAnswersNullAndGetSelectionIsAStub()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync("window.open('/other')")).Should().BeNull();
        (await page.EvaluateAsync("window.getSelection()")).Should().BeNull();
        (await page.EvaluateAsync("window.event")).Should().BeNull();
        (await page.EvaluateAsync<bool>("window.closed")).Should().BeFalse();
    }

    [Test]
    public async Task PostMessageDeliversAMessageEventToTheWindow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <script>
              window.received = null;
              window.addEventListener('message', e => { window.received = e.data.value });
              window.postMessage({ value: 'sent' }, '*');
              window.duringScript = window.received;
            </script>
            """);

        // The message is a queued task rather than an inline dispatch, so the script that posted it does not
        // see the listener run; a turn of the loop is what delivers it.
        (await page.EvaluateAsync("window.duringScript")).Should().BeNull();

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.received")).Should().Be("sent");
    }

    [Test]
    public async Task LocationExposesItsPartsAndItsNavigatingMembers()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.NavigateAsync("data:text/html,<p>x</p>");

        (await page.EvaluateAsync<string>("location.protocol")).Should().Be("data:");
        (await page.EvaluateAsync<string>("String(location)")).Should().Be(page.Url);
        (await page.EvaluateAsync<string>("typeof location.assign")).Should().Be("function");
        (await page.EvaluateAsync<string>("typeof location.replace")).Should().Be("function");
        (await page.EvaluateAsync<string>("typeof location.reload")).Should().Be("function");
        (await page.EvaluateAsync<bool>("location === document.location")).Should().BeTrue();
    }

    [Test]
    public async Task ANavigationAPageCannotReachIsRecordedRatherThanIgnored()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // A scheme a page has no transport for; http and https really do navigate now.
        await page.EvaluateAsync("location.href = 'ftp://example.com/file.txt'");
        (await page.WaitForNavigationAsync(TimeSpan.FromSeconds(2))).Should().BeFalse("the navigation never committed");

        page.Errors.Should().ContainSingle();
        page.Errors[0].Message.Should().Contain("ftp://example.com/file.txt");
        page.Errors[0].Message.Should().Contain("http, https, about: and data:");
        page.Url.Should().Be("about:blank");
    }

    [Test]
    public async Task AScriptedNavigationToASupportedUrlHappens()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // The wait is armed before the script that triggers it, which is the only race-free order: a
        // navigation a script starts runs off the page's thread, so it can commit before a wait registered
        // afterwards would have seen it.
        var navigated = page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
        await page.EvaluateAsync("location.assign('data:text/html,<p id=\\'moved\\'>moved</p>')");
        (await navigated).Should().BeTrue();

        (await page.EvaluateAsync<string>("document.getElementById('moved').textContent")).Should().Be("moved");
    }
}
