#nullable enable
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Forms;

/// <summary>
/// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-the-form-data-set
/// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#converting-an-entry-list-to-a-list-of-name-value-pairs
/// </summary>
public sealed class FormEntryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task EntryConstructionExposesScalarStringsApiNewlinesAndUtf8Charset(bool viaConstructor)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/echo", "<title>submitted</title>")
            .MapHtml("/form", """
                <form id="f" action="/echo" method="post">
                  <textarea name="note">one
                two</textarea>
                  <input type="hidden" name="_charset_" value="wrong">
                  <input type="hidden" name="_CHARSET_" value="also wrong">
                  <input name="_charset_" value="ordinary">
                  <input type="hidden" name="_charſet_" value="not-ascii">
                  <input id="scalar" type="hidden">
                  <select id="choice"><option selected>x</option></select>
                  <button id="send">send</button>
                  <input id="file" type="file">
                </form>
                """));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.EvaluateAsync("""
            for (const id of ['scalar', 'choice', 'send', 'file']) {
              document.getElementById(id).name = id + '\uD800';
            }
            scalar.value = '\uDC00😀';
            choice.options[0].value = '\uD800😀';
            send.value = '\uDC00😀';
            f.addEventListener('formdata', e => {
              sessionStorage.setItem('entries', JSON.stringify(Array.from(e.formData, ([k,v]) =>
                [k, typeof v === 'string' ? v : v.name])));
              e.formData.append('amended', 'yes');
            });
            """);
        if (viaConstructor)
        {
            (await fixture.Page.EvaluateAsync<bool>("""
                const constructed = new FormData(f, send);
                const expected = JSON.parse(sessionStorage.getItem('entries'));
                expected.push(['amended', 'yes']);
                JSON.stringify(Array.from(constructed, ([k,v]) =>
                  [k, typeof v === 'string' ? v : v.name])) === JSON.stringify(expected) &&
                  f.querySelector('textarea').value === 'one\ntwo' &&
                  f.querySelector('input').value === 'wrong' && scalar.value === '\uDC00😀'
                """)).Should().BeTrue();
        }
        else
        {
            await fixture.NavigateByScriptAsync("f.requestSubmit(send)");
        }
        (await fixture.Page.EvaluateAsync<bool>("""
            JSON.stringify(JSON.parse(sessionStorage.getItem('entries'))) === JSON.stringify([
              ['note', 'one\ntwo'], ['_charset_', 'UTF-8'], ['_CHARSET_', 'UTF-8'],
              ['_charset_', 'ordinary'], ['_charſet_', 'not-ascii'], ['scalar�', '�😀'], ['choice�', '�😀'],
              ['send�', '�😀'], ['file�', '']
            ])
            """)).Should().BeTrue();
        if (viaConstructor)
        {
            fixture.Server.Received.Should().NotContain(r => r.Path == "/echo");
        }
        else
        {
            fixture.Server.Received.Single(r => r.Path == "/echo").Body.Should().EndWith("amended=yes");
        }
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task GetSubmissionNormalizesEntryNamesAndListenerValues()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/echo", "<title>submitted</title>")
            .MapHtml("/form", "<form id='f' action='/echo'><input type='hidden' name='_charset_' value='wrong'></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.EvaluateAsync("""
            f.addEventListener('formdata', e => e.formData.append('n\rv\n', 'a\r\nb\rc\n'));
            """);
        await fixture.Page.SubmitFormAsync("#f");
        fixture.Server.Received.Single(r => r.Path == "/echo").Query.Should()
            .Be("_charset_=UTF-8&n%0D%0Av%0D%0A=a%0D%0Ab%0D%0Ac%0D%0A");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("application/x-www-form-urlencoded")]
    [TestCase("text/plain")]
    [TestCase("multipart/form-data")]
    public async Task SubmissionNormalizesListenerAmendmentsAtEncodingTime(string enctype)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/echo", "<title>submitted</title>")
            .MapHtml("/form", "<form id='f' action='/echo' method='post'></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.EvaluateAsync("f.enctype = '" + enctype + "'");
        await fixture.Page.EvaluateAsync("""
            f.addEventListener('formdata', e => {
              e.formData.append('a\rb\nc\r\nd', 'v\rx\ny\r\nz');
              e.formData.append('duplicate', 'first');
              e.formData.append('duplicate', 'second');
              e.formData.append('file', new File(['raw\rbytes\n'], 'f\rg\nh\r\ni'));
              sessionStorage.setItem('entries', JSON.stringify(Array.from(e.formData, ([k,v]) =>
                [k, typeof v === 'string' ? v : v.name])));
            });
            """);
        await fixture.Page.SubmitFormAsync("#f");
        var body = fixture.Server.Received.Single(r => r.Path == "/echo").Body;
        if (enctype == "application/x-www-form-urlencoded")
        {
            body.Should().Be("a%0D%0Ab%0D%0Ac%0D%0Ad=v%0D%0Ax%0D%0Ay%0D%0Az&duplicate=first&duplicate=second&file=f%0D%0Ag%0D%0Ah%0D%0Ai");
        }
        else if (enctype == "text/plain")
        {
            body.Should().Be("a\r\nb\r\nc\r\nd=v\r\nx\r\ny\r\nz\r\nduplicate=first\r\nduplicate=second\r\nfile=f\r\ng\r\nh\r\ni\r\n");
        }
        else
        {
            body.Should().Contain("name=\"a%0D%0Ab%0D%0Ac%0D%0Ad\"\r\n\r\nv\r\nx\r\ny\r\nz\r\n");
            body.Should().Contain("filename=\"f%0Dg%0Ah%0D%0Ai\"");
            body.Should().Contain("raw\rbytes\n\r\n");
            body.IndexOf("first", StringComparison.Ordinal).Should().BeLessThan(body.IndexOf("second", StringComparison.Ordinal));
        }
        (await fixture.Page.EvaluateAsync<bool>("""
            const entries = JSON.parse(sessionStorage.getItem('entries'));
            entries[0][0] === 'a\rb\nc\r\nd' && entries[0][1] === 'v\rx\ny\r\nz' &&
            entries[3][1] === 'f\rg\nh\r\ni'
            """)).Should().BeTrue();
        fixture.Page.Errors.Should().BeEmpty();
    }
}
