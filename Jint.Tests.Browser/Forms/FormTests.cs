using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Forms;

/// <summary>
/// HTML's form submission algorithm, ending in a navigation the page runs: the <c>submit</c> event, the entry
/// list, the <c>formdata</c> event, and the three encodings.
/// </summary>
public sealed class FormTests
{
    /// <summary>Echoes what it was asked, so a test can read the request back as a document.</summary>
    private static LoopbackServer WithEcho(LoopbackServer server)
        => server.Map("/echo", request => LoopbackResponse.Html(
            "<title>echo</title><pre id='method'>" + request.Method + "</pre>"
            + "<pre id='query'>" + System.Net.WebUtility.HtmlEncode(request.Query) + "</pre>"
            + "<pre id='type'>" + (request.Header("Content-Type") ?? "") + "</pre>"
            + "<pre id='body'>" + System.Net.WebUtility.HtmlEncode(request.Body) + "</pre>"));

    [Test]
    public async Task AGetSubmissionPutsTheEntryListInTheQueryString()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <form id='f' action='/echo' method='get'>
              <input name='name' value='ada lovelace'>
              <input name='year' value='1843'>
              <input value='no name, no entry'>
              <input name='off' disabled value='disabled'>
            </form>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        var response = await fixture.Page.SubmitFormAsync("#f");

        response.Should().NotBeNull();
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('method').textContent")).Should().Be("GET");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('query').textContent"))
            .Should().Be("name=ada+lovelace&year=1843");
    }

    [Test]
    public async Task AGetSubmissionReplacesAnExistingQueryString()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            "<form id='f' action='/echo?stale=1' method='get'><input name='fresh' value='2'></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('query').textContent")).Should().Be("fresh=2");
    }

    [Test]
    public async Task CheckboxesRadiosAndMultipleSelectsFollowTheEntryListRules()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <form id='f' action='/echo' method='get'>
              <input type='checkbox' name='on' checked>
              <input type='checkbox' name='off'>
              <input type='checkbox' name='valued' value='yes' checked>
              <input type='radio' name='pick' value='a'>
              <input type='radio' name='pick' value='b' checked>
              <select name='many' multiple>
                <option value='1' selected>one</option>
                <option value='2'>two</option>
                <option value='3' selected>three</option>
              </select>
              <select name='one'><option value='x'>x</option><option value='y' selected>y</option></select>
              <textarea name='note'>two
            lines</textarea>
            </form>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var query = await fixture.Page.EvaluateAsync<string>("document.getElementById('query').textContent");

        query.Should().Contain("on=on", "a checkbox with no value attribute submits \"on\"");
        query.Should().NotContain("off=", "an unchecked checkbox contributes nothing");
        query.Should().Contain("valued=yes");
        query.Should().Contain("pick=b");
        query.Should().NotContain("pick=a");
        query.Should().Contain("many=1&many=3", "a multiple select contributes one entry per selected option");
        query.Should().Contain("one=y");
        query.Should().Contain("note=two%0D%0Alines", "a textarea's newlines normalize to CRLF");
    }

    [Test]
    public async Task AControlInsideADisabledFieldsetContributesNothing()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <form id='f' action='/echo' method='get'>
              <fieldset disabled><input name='hidden' value='1'></fieldset>
              <fieldset><input name='shown' value='2'></fieldset>
            </form>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('query').textContent")).Should().Be("shown=2");
    }

    [Test]
    public async Task APostSubmissionSendsUrlEncodedByDefault()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            "<form id='f' action='/echo' method='post'><input name='a' value='one two'><input name='b' value='&amp;'></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('method').textContent")).Should().Be("POST");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('type').textContent"))
            .Should().Be("application/x-www-form-urlencoded;charset=UTF-8");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('body').textContent"))
            .Should().Be("a=one+two&b=%26");
    }

    [Test]
    public async Task APostSubmissionCanBeMultipartAndCarriesAnEmptyFileForAnEmptyFileInput()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            "<form id='f' action='/echo' method='post' enctype='multipart/form-data'>"
            + "<input name='who' value='ada'><input type='file' name='upload'></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var contentType = await fixture.Page.EvaluateAsync<string>("document.getElementById('type').textContent");
        contentType.Should().StartWith("multipart/form-data; boundary=");

        var body = await fixture.Page.EvaluateAsync<string>("document.getElementById('body').textContent");
        body.Should().Contain("Content-Disposition: form-data; name=\"who\"");
        body.Should().Contain("ada");
        body.Should().Contain("name=\"upload\"; filename=\"\"", "a file input with no file contributes an empty File");
        body.Should().Contain("Content-Type: application/octet-stream");
    }

    [Test]
    public async Task APostSubmissionCanBeTextPlain()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            "<form id='f' action='/echo' method='post' enctype='text/plain'><input name='a' value='1'><input name='b' value='2'></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('type').textContent")).Should().Be("text/plain;charset=UTF-8");

        // What went on the wire is the CRLF the text/plain encoding algorithm asks for; what the echo
        // document reports back is a bare LF, because the parse normalizes newlines in text.
        fixture.Server.Received.Single(r => r.Path == "/echo").Body.Should().Be("a=1\r\nb=2\r\n");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('body').textContent")).Should().Be("a=1\nb=2\n");
    }

    [Test]
    public async Task AFormDataListenerCanAmendTheEntryListBeforeItIsSent()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <form id='f' action='/echo' method='post'><input name='keep' value='1'><input name='drop' value='2'></form>
            <script>
              document.getElementById('f').addEventListener('formdata', function (e) {
                e.formData.delete('drop');
                e.formData.append('added', 'by-listener');
              });
            </script>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var body = await fixture.Page.EvaluateAsync<string>("document.getElementById('body').textContent");
        body.Should().Be("keep=1&added=by-listener");
    }

    [Test]
    public async Task ASubmitListenerCanCancelTheSubmission()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <title>still here</title>
            <form id='f' action='/echo' method='get'><input name='a' value='1'></form>
            <script>document.getElementById('f').addEventListener('submit', function (e) { e.preventDefault(); });</script>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        var response = await fixture.Page.SubmitFormAsync("#f");

        response.Should().BeNull("a cancelled submission navigates nowhere");
        (await fixture.Page.TitleAsync()).Should().Be("still here");
        fixture.Server.Received.Should().NotContain(r => r.Path == "/echo");
    }

    [Test]
    public async Task TheSubmitEventCarriesItsSubmitterAndFormDotSubmitFiresNoEvent()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <form id='f' action='/echo' method='get'><input name='a' value='1'><button id='b' type='submit'>go</button></form>
            <script>
              window.seen = [];
              document.getElementById('f').addEventListener('submit', function (e) {
                window.seen.push(e.submitter === null ? 'null' : e.submitter.id);
                e.preventDefault();
              });
            </script>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        // requestSubmit(button) fires the event with that button as the submitter.
        await fixture.Page.EvaluateAsync("document.getElementById('f').requestSubmit(document.getElementById('b'))");
        (await fixture.Page.EvaluateAsync<string>("window.seen.join('|')")).Should().Be("b");

        // submit() skips the event entirely, which is the whole difference between the two.
        await fixture.Page.EvaluateAsync("document.getElementById('f').submit()");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('query').textContent")).Should().Be("a=1");
    }

    [Test]
    public async Task RequestSubmitRefusesAnElementThatIsNotASubmitButton()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            "<form id='f' action='/echo'><input id='plain' name='a' value='1'></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        var name = await fixture.Page.EvaluateAsync<string>(
            "try { document.getElementById('f').requestSubmit(document.getElementById('plain')); 'no throw' } catch (e) { e.name }");

        name.Should().Be("TypeError");
    }

    [Test]
    public async Task ASubmitButtonWithFormActionAndFormMethodWins()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server)
            .Map("/other", request => LoopbackResponse.Html("<title>other</title><pre id='m'>" + request.Method + "</pre>"))
            .MapHtml(
                "/form.html",
                "<form id='f' action='/echo' method='get'><input name='a' value='1'>"
                + "<button id='b' type='submit' formaction='/other' formmethod='post'>go</button></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.EvaluateAsync("document.getElementById('f').requestSubmit(document.getElementById('b'))");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.TitleAsync()).Should().Be("other");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('m').textContent")).Should().Be("POST");
    }

    [Test]
    public async Task ASubmitButtonContributesItsOwnNameAndValueAndOtherButtonsDoNot()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            """
            <form id='f' action='/echo' method='get'>
              <button id='save' type='submit' name='action' value='save'>save</button>
              <button id='delete' type='submit' name='action' value='delete'>delete</button>
            </form>
            """));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.EvaluateAsync("document.getElementById('f').requestSubmit(document.getElementById('delete'))");
        (await fixture.Page.WaitForNavigationAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('query').textContent")).Should().Be("action=delete");
    }

    [Test]
    public async Task AnInvalidFormDoesNotSubmitUnlessItIsNovalidate()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server)
            .MapHtml("/strict.html", "<title>strict</title><form id='f' action='/echo' method='get'><input name='a' required value=''></form>")
            .MapHtml("/lax.html", "<title>lax</title><form id='f' action='/echo' method='get' novalidate><input name='a' required value=''></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/strict.html"));
        (await fixture.Page.SubmitFormAsync("#f")).Should().BeNull();
        (await fixture.Page.TitleAsync()).Should().Be("strict");

        await fixture.Page.NavigateAsync(fixture.Url("/lax.html"));
        (await fixture.Page.SubmitFormAsync("#f")).Should().NotBeNull();
        (await fixture.Page.TitleAsync()).Should().Be("echo");
    }

    [Test]
    public async Task ASubmissionSendsTheDocumentAsItsRefererAndOrigin()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => WithEcho(server).MapHtml(
            "/form.html",
            "<form id='f' action='/echo' method='post'><input name='a' value='1'></form>"));

        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));
        await fixture.Page.SubmitFormAsync("#f");

        var echo = fixture.Server.Received.Single(r => r.Path == "/echo");
        echo.Header("Referer").Should().Be(fixture.Url("/form.html"));
        echo.Header("Origin").Should().Be(fixture.Server.Origin);
    }

    [Test]
    public async Task SubmittingAFormThatDoesNotExistAnswersNothing()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<title>index</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.SubmitFormAsync("#nothing")).Should().BeNull();
    }
}
