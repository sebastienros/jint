using System.Text.Json;
using AngleSharp.Html.Dom;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Forms;

/// <summary>
/// HTML §4.10.22.4's image entries and §4.10.5.1.19's selected coordinate. Exercise the shared builder
/// through FormData construction as well as actual navigation.
/// </summary>
public sealed class ImageSubmitTests
{
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
        await page.SetContentAsync("<form id=f><input name=a value=1><input id=image type=image name=go></form>");

        (await Entries(page, "null")).Should().Be("""[["a","1"]]""");
    }

    [TestCase("pointerup")]
    [TestCase("mouseup")]
    [TestCase("click")]
    public async Task MovingAFallbackButtonDuringPointerEventsCannotSelectAnImageCoordinate(string eventName)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=before></div><form id=f><input id=image type=image name=go></form>");
        await page.EvaluateAsync($$"""
            f.onsubmit = e => e.preventDefault();
            image.addEventListener('{{eventName}}', () => document.getElementById('before').remove(), {once:true});
            """);

        await PointerClick(page, 7.75, 5.5);
        (await Entries(page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");

        // An unavailable image cannot acquire a coordinate through synthetic event data either.
        await page.EvaluateAsync("image.onclick = null; image.click(); image.dispatchEvent(new MouseEvent('click', {clientX:99,clientY:99}))");
        (await Entries(page)).Should().Be("""[["go.x","0"],["go.y","0"]]""");
    }

    [Test]
    public async Task CancelingAFallbackClickPreventsSubmissionWithoutSelectingACoordinate()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id=f><input id=image type=image></form>");
        await page.EvaluateAsync("window.submissions = 0; f.onsubmit = e => { submissions++; e.preventDefault(); }");
        await PointerClick(page, 3, 4);
        await page.EvaluateAsync("image.onclick = e => e.preventDefault()");
        await PointerClick(page, 9, 10);

        (await page.EvaluateAsync<int>("submissions")).Should().Be(1);
        (await Entries(page)).Should().Be("""[["x","0"],["y","0"]]""");
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
        await page.SetContentAsync("<form id=f><input id=image type=image></form>");
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

        (await page.EvaluateAsync<int>("inactiveSubmissions")).Should().Be(0);
        (await Entries(page, form: "otherForm")).Should().Be("""[["x","0"],["y","0"]]""");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("src=''")]
    [TestCase("src='/not-loaded.png'")]
    [TestCase("src='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aP9sAAAAASUVORK5CYII='")]
    public async Task APagePointerClickOnAnUnavailableImageSubmitsTheDefaultCoordinate(string srcAttribute)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/form", $"<form id=f action=/echo><input name=a value=1><input id=image type=image name=go {srcAttribute}></form>")
            .Map("/echo", request => LoopbackResponse.Html("<pre>" + System.Net.WebUtility.HtmlEncode(request.Query) + "</pre>")));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        (await fixture.Page.ClickAsync("#image")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("document.querySelector('pre').textContent")).Should().Be("a=1&go.x=0&go.y=0");
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
