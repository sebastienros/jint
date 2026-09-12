using System.Text.Json;
using AngleSharp.Html.Dom;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Tests.Browser.Navigation;
using Jint.Tests.Browser.Parsing;

namespace Jint.Tests.Browser.Forms;

/// <summary>
/// HTML §4.10.22.4's image entries and §4.10.5.1.20's selected coordinate. Exercise the shared builder
/// through FormData construction as well as actual navigation.
/// </summary>
public sealed class ImageSubmitTests
{
    /// <summary>
    /// A 64 × 32 PNG inline in the markup. HTML §4.8.4.3's image request is complete for it — a
    /// <c>data:</c> URL carries its own bytes — so the availability the selected coordinate depends on
    /// costs no server, and the image states a size the flat box model deliberately does not use.
    /// </summary>
    private static readonly string AvailableImage =
        "src='data:image/png;base64," + Convert.ToBase64String(ImageBytes.Png(64, 32)) + "'";

    private static Task<string?> Entries(Page page, string submitter = "image", string form = "f")
        => page.EvaluateAsync<string>($"JSON.stringify([...new FormData({form}, {submitter})])");

    private static Task<bool> PointerClick(Page page, double x, double y)
        => page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var input = (IHtmlInputElement) runtime.Document!.GetElementById("image")!;
            var box = runtime.Layout.Current().ClientBoxOf(input)!.Value;
            InputDispatcher.DispatchMouse(runtime, new MouseInput(
                MouseInputKind.Released, box.X + x, box.Y + y, 0, 0, 1, EventModifiers.None, 0, 0));
            return true;
        });

    [TestCase("name='go'", "go.x", "go.y")]
    [TestCase("name=''", "x", "y")]
    [TestCase("", "x", "y")]
    public async Task AnImageSubmitterStartsAtZeroAndDoesNotSubmitItsValue(string nameAttribute, string x, string y)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image {nameAttribute} value=ignored></form>");

        (await Entries(page)).Should().Be(JsonSerializer.Serialize(new[] { new[] { x, "0" }, new[] { y, "0" } }));
        (await page.EvaluateAsync<int>("f.elements.length")).Should().Be(0, "image buttons remain excluded from form.elements");
    }

    [TestCase("name='go'", "go.x", "go.y")]
    [TestCase("name=''", "x", "y")]
    [TestCase("", "x", "y")]
    public async Task APointerClickInsideAnAvailableImageSelectsTheCoordinateItLandedOn(string nameAttribute, string x, string y)
    {
        // https://html.spec.whatwg.org/multipage/input.html#image-button-state-(type=image): the image is
        // available and the activation came from a pointing device, so the selected coordinate is the
        // position relative to the image rather than the fallback submit button's (0, 0). Its components
        // are integers, so 7.75 and 5.5 are 7 and 5.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image {nameAttribute} {AvailableImage}></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(page, 7.75, 5.5);

        (await Entries(page)).Should().Be(JsonSerializer.Serialize(new[] { new[] { x, "7" }, new[] { y, "5" } }));
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A single-finger tap on the image, driven through the same dispatcher <c>Input.dispatchTouchEvent</c>
    /// reaches: down at the point, then up, which is what leaves Touch Events §8's compatibility mouse
    /// events behind.
    /// </summary>
    private static Task<bool> Tap(Page page, double x, double y)
        => page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var input = (IHtmlInputElement) runtime.Document!.GetElementById("image")!;
            var box = runtime.Layout.Current().ClientBoxOf(input)!.Value;
            InputDispatcher.DispatchTouch(runtime, new TouchInput(
                TouchInputKind.Start, [TouchPointInput.At(box.X + x, box.Y + y)], EventModifiers.None));
            InputDispatcher.DispatchTouch(runtime, TouchInput.Of(TouchInputKind.End));
            return true;
        });

    [TestCase("name='go'", "go.x", "go.y")]
    [TestCase("name=''", "x", "y")]
    [TestCase("", "x", "y")]
    public async Task ATapInsideAnAvailableImageSelectsTheCoordinateItLandedOn(string nameAttribute, string x, string y)
    {
        // https://html.spec.whatwg.org/multipage/input.html#image-button-state-(type=image) asks whether
        // "the user activated the button using a pointing device", and a finger is one: Touch Events §8's
        // compatibility `click` is the activation, so the tap selects the point it came off at exactly as
        // the identical mouse release does.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image {nameAttribute} {AvailableImage}></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await Tap(page, 7.75, 5.5);

        (await Entries(page)).Should().Be(JsonSerializer.Serialize(new[] { new[] { x, "7" }, new[] { y, "5" } }));
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ATapAndAClickAtOnePointSelectTheSameCoordinate()
    {
        // The two paths reach one activation behaviour, so they cannot disagree about the coordinate; this
        // is the assertion that would have failed silently while only the mouse path measured.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image name=go {AvailableImage}></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");

        await PointerClick(page, 12, 3);
        var clicked = await Entries(page);

        await page.EvaluateAsync("window.tapped = null");
        await Tap(page, 12, 3);

        (await Entries(page)).Should().Be(clicked).And.Be("""[["go.x","12"],["go.y","3"]]""");
    }

    [Test]
    public async Task ATapTheDocumentCancelledSelectsNothingBecauseItActivatesNothing()
    {
        // §8: `preventDefault()` on the `touchstart` means no compatibility mouse events at all, so there is
        // no click, no activation behaviour and nothing to select — the button keeps the (0, 0) every
        // unactivated image button has. The listener has to say `passive: false`, because HTML makes a
        // `touchstart` listener on the window, the document or the body passive by default and a passive
        // listener's `preventDefault()` does nothing.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image name=go {AvailableImage}></form>");
        await page.EvaluateAsync("""
            window.submitted = false;
            f.onsubmit = e => { window.submitted = true; e.preventDefault(); };
            document.addEventListener('touchstart', e => e.preventDefault(), { passive: false });
            """);
        await Tap(page, 7.75, 5.5);

        (await page.EvaluateAsync<bool>("window.submitted")).Should().BeFalse();
        (await Entries(page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");
    }

    [Test]
    public async Task ATapThatLiftsOffTheImageSelectsNothingForIt()
    {
        // The compatibility events are dispatched where the finger came off, not where it went down, so a
        // finger that slid off the button before lifting activates nothing and the button stays at (0, 0).
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $"<form id=f><input id=image type=image name=go {AvailableImage}></form><p id=elsewhere>elsewhere</p>");
        await page.EvaluateAsync("""
            window.submitted = false;
            window.clicked = '';
            f.onsubmit = e => { window.submitted = true; e.preventDefault(); };
            document.addEventListener('click', e => { window.clicked = e.target.id; });
            """);

        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var layout = runtime.Layout.Current();
            var image = (IHtmlInputElement) runtime.Document!.GetElementById("image")!;
            var elsewhere = runtime.Document.GetElementById("elsewhere")!;
            var from = layout.ClientBoxOf(image)!.Value;
            var to = layout.ClientBoxOf(elsewhere)!.Value;

            InputDispatcher.DispatchTouch(runtime, new TouchInput(
                TouchInputKind.Start, [TouchPointInput.At(from.X + 7, from.Y + 5)], EventModifiers.None));
            InputDispatcher.DispatchTouch(runtime, new TouchInput(
                TouchInputKind.Move, [TouchPointInput.At(to.X + 2, to.Y + 2)], EventModifiers.None));
            InputDispatcher.DispatchTouch(runtime, TouchInput.Of(TouchInputKind.End));
            return true;
        });

        (await page.EvaluateAsync<string>("window.clicked")).Should().Be("elsewhere");
        (await page.EvaluateAsync<bool>("window.submitted")).Should().BeFalse();
        (await Entries(page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");
    }

    [Test]
    public async Task TheCoordinateATapSelectedReachesAnActualSubmission()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", $"<form id=f action=/echo><input name=a value=1>"
                + $"<input id=image type=image name=go {AvailableImage}></form>")
            .MapHtml("/echo", "<p>echoed</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));

        var navigated = fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
        await Tap(fixture.Page, 7.75, 5.5);
        (await navigated).Should().BeTrue("a tap's compatibility click runs the image button's activation behaviour");

        fixture.Server.Received.Single(request => request.Path == "/echo").Query.Should().Be("a=1&go.x=7&go.y=5");
    }

    [Test]
    public async Task TheFormdataEventCarriesTheSelectedCoordinateLongAfterTheClick()
    {
        // The coordinate is the element's state, not the click's: everything that builds an entry list —
        // the FormData constructor, the formdata event it fires, requestSubmit — reads the same slot,
        // whenever it asks. This is the shape the issue reproduces with, over an image.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image name=go {AvailableImage}></form>");
        await page.EvaluateAsync(
            "f.onsubmit = e => e.preventDefault(); f.onformdata = e => window.heard = JSON.stringify([...e.formData])");
        await PointerClick(page, 7.75, 5.5);

        await page.EvaluateAsync("new FormData(f, image)");
        (await page.EvaluateAsync<string>("window.heard")).Should().Be("""[["go.x","7"],["go.y","5"]]""");

        // Moving the input inside its form is not an activation, so it selects nothing and changes nothing.
        await page.EvaluateAsync("window.heard = null; f.append(document.createElement('span'), image)");
        (await Entries(page)).Should().Be("""[["go.x","7"],["go.y","5"]]""");
        (await page.EvaluateAsync<string>("window.heard")).Should().Be("""[["go.x","7"],["go.y","5"]]""");
    }

    [Test]
    public async Task ARequestSubmitLongAfterTheClickCarriesTheCoordinateTheClickSelected()
    {
        // The issue's own reproduction, over an available image: requestSubmit is not a pointer activation
        // and selects nothing of its own, so what it submits is what the click before it selected.
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", $"<form id=f action=/echo><input id=image type=image name=go {AvailableImage}></form>")
            .MapHtml("/echo", "<p>echoed</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.EvaluateAsync("window.cancel = e => e.preventDefault(); f.addEventListener('submit', cancel)");
        await PointerClick(fixture.Page, 7.75, 5.5);

        await fixture.Page.EvaluateAsync("f.removeEventListener('submit', cancel)");
        await fixture.NavigateByScriptAsync("f.requestSubmit(image)");

        fixture.Server.Received.Single(request => request.Path == "/echo").Query.Should().Be("go.x=7&go.y=5");
    }

    [TestCase("get")]
    [TestCase("post")]
    public async Task TheSelectedCoordinateReachesAnActualSubmission(string method)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", $"<form id=f action=/echo method={method}><input name=a value=1>"
                + $"<input id=image type=image name=go {AvailableImage}></form>")
            .MapHtml("/echo", "<p>echoed</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));

        var navigated = fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
        await PointerClick(fixture.Page, 7.75, 5.5);
        (await navigated).Should().BeTrue("an image button's activation behaviour submits its form");

        var echo = fixture.Server.Received.Single(request => request.Path == "/echo");
        (method == "get" ? echo.Query : echo.Body).Should().Be("a=1&go.x=7&go.y=5");
    }

    [Test]
    public async Task AMultipartSubmissionCarriesTheSameTwoEntries()
    {
        // One entry-list algorithm behind all three encodings: the parts differ, the pair does not.
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", "<form id=f action=/echo method=post enctype='multipart/form-data'>"
                + $"<input id=image type=image name=go {AvailableImage}></form>")
            .MapHtml("/echo", "<p>echoed</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));

        var navigated = fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
        await PointerClick(fixture.Page, 7.75, 5.5);
        (await navigated).Should().BeTrue();

        var body = fixture.Server.Received.Single(request => request.Path == "/echo").Body;
        body.Should().Contain("Content-Disposition: form-data; name=\"go.x\"\r\n\r\n7\r\n");
        body.Should().Contain("Content-Disposition: form-data; name=\"go.y\"\r\n\r\n5\r\n");
    }

    [Test]
    public async Task ExternalAssociationTreeOrderAndDuplicateNamesArePreserved()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <input form=f name=go.x value=before>
            <input form=f id=image type=image name=go>
            <form id=f>
              <input name=go.x value=inside>
              <input type=image name=other>
              <button name=button value=ignored>other submitter</button>
            </form>
            <form id=other><input name=alsoWrong value=ignored></form>
            <input form=f name=go.y value=after>
            """);

        (await Entries(page)).Should().Be("""[["go.x","before"],["go.x","0"],["go.y","0"],["go.x","inside"],["go.y","after"]]""");
    }

    [Test]
    public async Task AnImageAssociatedByTheFormAttributeSelectsACoordinateToo()
    {
        // The form owner is the form= attribute's and the image sits outside the form element entirely.
        // The coordinate is the input's own state, so where it sits in the tree changes nothing about it.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $"<input form=f id=image type=image name=go {AvailableImage}><form id=f><input name=a value=1></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(page, 2.5, 9);

        (await Entries(page)).Should().Be("""[["go.x","2"],["go.y","9"],["a","1"]]""");
    }

    [Test]
    public async Task ADetachedFormStillIncludesItsImageInTreeOrder()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id=f><input name=a value=1><input id=image type=image><input name=a value=2></form>");
        await page.EvaluateAsync("window.detached = f; window.image = document.getElementById('image'); detached.remove()");

        (await Entries(page, form: "detached")).Should().Be("""[["a","1"],["x","0"],["y","0"],["a","2"]]""");
    }

    [TestCase("<input id=image type=image disabled>")]
    [TestCase("<fieldset disabled><input id=image type=image></fieldset>")]
    [TestCase("<datalist><input id=image type=image></datalist>")]
    public async Task DisabledAndDatalistImagesAreNotSuccessfulControls(string markup)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input name=ok value=yes>{markup}</form>");

        (await Entries(page)).Should().Be("""[["ok","yes"]]""");
    }

    [Test]
    public async Task AnImageInTheFirstLegendOfADisabledFieldsetIsSuccessful()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id=f><fieldset disabled><legend><input id=image type=image></legend></fieldset></form>");

        (await Entries(page)).Should().Be("""[["x","0"],["y","0"]]""");
    }

    [Test]
    public async Task NonSelectedImagesContributeNoEntries()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input name=a value=1><input id=image type=image name=go {AvailableImage}></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(page, 7.75, 5.5);

        // An image that has selected a coordinate is still nothing at all to a submission it is not the
        // submitter of: entry construction asks whether the field is the submitter before anything else.
        (await Entries(page, "null")).Should().Be("""[["a","1"]]""");
    }

    [TestCase("pointerup")]
    [TestCase("mouseup")]
    [TestCase("click")]
    public async Task TheCoordinateIsCapturedBeforeAListenerOfTheReleaseCanMoveTheInput(string eventName)
    {
        // Each of these three listeners runs before the activation behaviour that reads the coordinate,
        // and removing the element above the input moves the input's box up one 16 px row. The coordinate
        // is the hit test's, which preceded all three, so it is unchanged; measuring it at activation time
        // would answer 21 instead of 5.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $"<div id=before></div><form id=f><input id=image type=image name=go {AvailableImage}></form>");
        await page.EvaluateAsync($$"""
            f.onsubmit = e => e.preventDefault();
            image.addEventListener('{{eventName}}', () => document.getElementById('before').remove(), {once:true});
            """);

        await PointerClick(page, 7.75, 5.5);
        (await Entries(page)).Should().Be("""[["go.x","7"],["go.y","5"]]""");

        // A script's click is not a pointing device, whatever coordinates it carries, so the activation it
        // runs selects (0, 0) over the top of the coordinate the real click selected.
        await page.EvaluateAsync("image.dispatchEvent(new MouseEvent('click', {clientX:99,clientY:99}))");
        (await Entries(page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");
    }

    [Test]
    public async Task ASyntheticActivationSelectsNothingEvenOnAnAvailableImage()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image {AvailableImage}></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(page, 11, 6);
        (await Entries(page)).Should().Be("""[["x","11"],["y","6"]]""");

        await page.EvaluateAsync("image.click()");
        (await Entries(page)).Should().Be("""[["x","0"],["y","0"]]""");
    }

    [Test]
    public async Task CancelingAClickPreventsSubmissionAndSelectsNoCoordinateOfItsOwn()
    {
        // A canceled click runs no activation behaviour, so it selects nothing — and what the element
        // still holds is what its last activation selected.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image {AvailableImage}></form>");
        await page.EvaluateAsync("window.submissions = 0; f.onsubmit = e => { submissions++; e.preventDefault(); }");
        await PointerClick(page, 3, 4);
        await page.EvaluateAsync("image.onclick = e => e.preventDefault()");
        await PointerClick(page, 9, 10);

        (await page.EvaluateAsync<int>("submissions")).Should().Be(1);
        (await Entries(page)).Should().Be("""[["x","3"],["y","4"]]""");
    }

    [Test]
    public async Task FallbackCoordinatesRemainZeroAcrossAdoptionAndSyntheticActivation()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id=f><input id=image type=image></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(page, 11, 6);
        await page.EvaluateAsync("""
            window.image = document.getElementById('image');
            const otherDocument = document.implementation.createHTMLDocument('other');
            otherDocument.body.append(otherDocument.adoptNode(image));
            f.append(document.adoptNode(image));
            """);

        (await Entries(page)).Should().Be("""[["x","0"],["y","0"]]""");
        await page.RunOnLoopAsync(engine =>
        {
            InputDispatcher.FireSyntheticClick((DomNodeObject) engine.Evaluate("image"), trusted: true);
            return true;
        });
        (await Entries(page)).Should().Be("""[["x","0"],["y","0"]]""");
    }

    [Test]
    public async Task AdoptionIntoAnInactiveDocumentBeforeActivationDoesNotSubmit()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image {AvailableImage}></form>");
        await page.EvaluateAsync("""
            window.image = document.getElementById('image');
            window.inactiveSubmissions = 0;
            image.onclick = () => {
              const other = document.implementation.createHTMLDocument('inactive');
              window.otherForm = other.createElement('form');
              otherForm.onsubmit = e => { inactiveSubmissions++; e.preventDefault(); };
              other.body.append(otherForm);
              otherForm.append(other.adoptNode(image));
            };
            """);
        await PointerClick(page, 3, 4);

        // The activation behaviour returned before selecting anything, which is the image button's own
        // step 2: the input's node document is no longer fully active.
        (await page.EvaluateAsync<int>("inactiveSubmissions")).Should().Be(0);
        (await Entries(page, form: "otherForm")).Should().Be("""[["x","0"],["y","0"]]""");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("src=''")]
    [TestCase("src='data:image/png;base64,bm90IGFuIGltYWdl'")]
    public async Task APointerClickOnAnUnavailableImageSelectsTheDefaultCoordinate(string srcAttribute)
    {
        // No source, a source that selects no candidate, and bytes in no container this browser reads.
        // None of the three is an image a coordinate can be selected within, so the element behaves as the
        // fallback submit button HTML says it is.
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<form id=f><input id=image type=image name=go {srcAttribute}></form>");
        await page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(page, 7.75, 5.5);

        (await Entries(page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");
    }

    [Test]
    public async Task AnImageWhoseFetchFailedSelectsTheDefaultCoordinate()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", "<form id=f><input id=image type=image name=go src=/missing.png></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.EvaluateAsync("f.onsubmit = e => e.preventDefault()");
        await PointerClick(fixture.Page, 7.75, 5.5);

        (await Entries(fixture.Page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");
    }

    [Test]
    public async Task APageClickSelectsTheCentreOfTheFlatBoxRatherThanOfTheImage()
    {
        // Page.ClickAsync — which is what the CDP Input domain and the Playwright adapter's
        // ILocator.ClickAsync reach — clicks the centre of the element's box, and the flat box model makes
        // that box a 16 px row the viewport wide whatever the image's own 64 × 32 says. So the coordinate
        // is in the box model's geometry and can exceed naturalWidth; Dom/divergences.md records it.
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", $"<form id=f action=/echo><input name=a value=1><input id=image type=image name=go {AvailableImage}></form>")
            .Map("/echo", request => LoopbackResponse.Html("<pre>" + System.Net.WebUtility.HtmlEncode(request.Query) + "</pre>")));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        (await fixture.Page.ClickAsync("#image")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("document.querySelector('pre').textContent")).Should().Be("a=1&go.x=640&go.y=8");
    }

    [TestCase("get", false)]
    [TestCase("post", false)]
    [TestCase("get", true)]
    [TestCase("post", true)]
    public async Task ConstructorAndRequestSubmitShareScalarNamesAndListenerAmendments(string method, bool loneSurrogate)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", $"<form id=f action=/echo method={method}><input name=a value=1><input id=image type=image name=go></form>"
                + "<script>f.onformdata=e=>e.formData.append(e.formData.keys().toArray()[1],'listener')</script>")
            .Map("/echo", request => LoopbackResponse.Html("<pre>" + System.Net.WebUtility.HtmlEncode(
                request.Method == "GET" ? request.Query : request.Body) + "</pre>")));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        if (loneSurrogate)
        {
            await fixture.Page.EvaluateAsync("image.name = 'go\\uD800'");
        }
        var prefix = loneSurrogate ? "go�" : "go";
        (await Entries(fixture.Page)).Should().Be($"""[["a","1"],["{prefix}.x","0"],["{prefix}.y","0"],["{prefix}.x","listener"]]""");
        await fixture.NavigateByScriptAsync("f.requestSubmit(image)");

        var encodedPrefix = loneSurrogate ? "go%EF%BF%BD" : "go";
        (await fixture.Page.EvaluateAsync<string>("document.querySelector('pre').textContent"))
            .Should().Be($"a=1&{encodedPrefix}.x=0&{encodedPrefix}.y=0&{encodedPrefix}.x=listener");
    }
}
