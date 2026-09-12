#nullable enable
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Forms;

public sealed class DirectionNameTests
{
    [Test]
    public async Task DefaultDetachedBdiAndShadowHostDirections()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host' dir='rtl'></div>");
        (await page.EvaluateAsync<bool>("""
            const form = document.createElement('form');
            form.innerHTML = '<input name="v" dirname="d">';
            const read = () => new FormData(form).get('d');
            const detached = read();
            const shadow = host.attachShadow({mode:'open'}); shadow.append(form);
            const inherited = read(); host.dir = 'ltr'; const changed = read();
            const bdi = document.createElement('bdi'); bdi.append('אב', form); document.body.append(bdi);
            const isolated = read(); bdi.firstChild.data = '123'; const neutral = read();
            detached === 'ltr' && inherited === 'rtl' && changed === 'ltr' && isolated === 'rtl' && neutral === 'ltr'
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("hidden")]
    [TestCase("text")]
    [TestCase("search")]
    [TestCase("tel")]
    [TestCase("url")]
    [TestCase("email")]
    [TestCase("password")]
    [TestCase("submit")]
    [TestCase("textarea")]
    [TestCase("invalid")]
    public async Task SuccessfulControlsUseCurrentValueAndPreserveReflection(string type)
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f' dir='rtl'></form>");
        (await page.EvaluateAsync<bool>("""
            const c = document.createElement('TYPE' === 'textarea' ? 'textarea' : 'input');
            c.type = 'TYPE'; c.name = 'word'; c.setAttribute('dirname', 'word.dir');
            c.value = 'hello'; f.append(c);
            const read = () => new FormData(f, c.type === 'submit' ? c : undefined).get('word.dir');
            const initial = read();
            c.dir = 'auto'; c.value = '123 אב'; const rtl = read();
            c.value = '123 Aאב'; const ltr = read();
            c.value = ''; const empty = read();
            initial === ('TYPE' === 'tel' ? 'ltr' : 'rtl') && rtl === 'rtl' && ltr === 'ltr' && empty === 'ltr' &&
              c.dir === 'auto' && c.getAttribute('dirname') === 'word.dir'
            """.Replace("TYPE", type, StringComparison.Ordinal))).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AutoAncestorsFollowTextAndMutationsWithoutReadingIsolatedDescendants()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <form id='f' dir='auto'><bdi>A</bdi><span dir='ltr'>A</span><script type='text/plain'>A</script>
            <style>/* A */</style><textarea>A</textarea><span id='t'>123 אב</span>
            <input id='c' name='v' dirname='d'></form>
            """);
        (await page.EvaluateAsync<bool>("""
            const read = () => new FormData(f).get('d');
            const before = read(); t.textContent = 'A'; const after = read();
            f.dir = 'RTL'; const explicit = read(); c.dir = 'invalid'; const invalid = read();
            c.dir = 'auto'; c.value = '\u061c'; const arabicMark = read();
            c.value = '\u200f'; const rightMark = read();
            c.value = '\u200eאב'; const leftMark = read();
            c.value = '\u0661אב'; const arabicDigit = read();
            c.value = '\u{1E900}'; const supplementary = read();
            c.value = '\u0301אב'; const combining = read();
            before === 'rtl' && after === 'ltr' && explicit === 'rtl' && invalid === 'rtl' &&
              arabicMark === 'rtl' && rightMark === 'rtl' && leftMark === 'ltr' &&
              arabicDigit === 'rtl' && supplementary === 'rtl' && combining === 'rtl'
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OnlySuccessfulEligibleControlsContributeAndNamesAreScalarStrings()
    {
        await using var browser = new Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <form id='f' dir='rtl'>
            <input name='a' value='1' dirname='same'><textarea name='b' dirname='same'>2</textarea>
            <input name='empty' dirname=''><input name='absent'><input dirname='unnamed'>
            <input disabled name='disabled' dirname='disabled.dir'>
            <fieldset disabled><input name='fieldset' dirname='fieldset.dir'></fieldset>
            <datalist><input name='suggestion' dirname='suggestion.dir'></datalist>
            <input type='checkbox' name='check' dirname='check.dir' checked>
            <input type='number' name='number' dirname='number.dir' value='3'>
            <input type='reset' name='reset' dirname='reset.dir'><input type='button' name='button' dirname='button.dir'>
            <input id='send' type='submit' name='send' value='go' dirname='send.dir'>
            <input type='submit' name='other' dirname='other.dir'>
            <button name='htmlButton' dirname='htmlButton.dir'>go</button></form>
            """);
        (await page.EvaluateAsync<bool>("""
            send.setAttribute('dirname', 'send\uD800');
            const data = new FormData(f, send);
            const expected = [['a','1'],['same','rtl'],['b','2'],['same','rtl'],['empty',''],['absent',''],
              ['check','on'],['number','3'],['send','go'],['send�','rtl']];
            JSON.stringify([...data]) === JSON.stringify(expected) && !new FormData(f).has('send�')
            """)).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("get", "application/x-www-form-urlencoded")]
    [TestCase("post", "application/x-www-form-urlencoded")]
    [TestCase("post", "text/plain")]
    [TestCase("post", "multipart/form-data")]
    public async Task SubmissionAndConstructorExposeTheSameOrderedEntriesBeforeListeners(string method, string enctype)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/echo", "<title>submitted</title>")
            .MapHtml("/form", """
                <form id='f' action='/echo' dir='rtl'><input name='word' value='hello' dirname='d'>
                <textarea name='note' dirname='d' dir='auto'>A</textarea></form>
                """));
        await fixture.Page.NavigateAsync(fixture.Url("/form"));
        await fixture.Page.EvaluateAsync($"f.method = '{method}'; f.enctype = '{enctype}'");
        (await fixture.Page.EvaluateAsync<bool>("""
            f.addEventListener('formdata', e => {
              sessionStorage.setItem('entries', JSON.stringify([...e.formData]));
              e.formData.append('amended', 'yes');
            });
            const expected = [['word','hello'],['d','rtl'],['note','A'],['d','ltr']];
            JSON.stringify([...new FormData(f)]) === JSON.stringify([...expected, ['amended','yes']])
            """)).Should().BeTrue();
        await fixture.Page.SubmitFormAsync("#f");
        (await fixture.Page.EvaluateAsync<string>("sessionStorage.getItem('entries')")).Should()
            .Be("[[\"word\",\"hello\"],[\"d\",\"rtl\"],[\"note\",\"A\"],[\"d\",\"ltr\"]]");
        var request = fixture.Server.Received.Single(r => r.Path == "/echo");
        if (method == "get") request.Query.Should().Be("word=hello&d=rtl&note=A&d=ltr&amended=yes");
        else if (enctype == "application/x-www-form-urlencoded") request.Body.Should().Be("word=hello&d=rtl&note=A&d=ltr&amended=yes");
        else if (enctype == "text/plain") request.Body.Should().Be("word=hello\r\nd=rtl\r\nnote=A\r\nd=ltr\r\namended=yes\r\n");
        else
        {
            request.Body.Should().Contain("name=\"d\"\r\n\r\nrtl\r\n").And.Contain("name=\"d\"\r\n\r\nltr\r\n");
            request.Body.Should().Contain("name=\"amended\"\r\n\r\nyes\r\n");
        }
        fixture.Page.Errors.Should().BeEmpty();
    }
}
