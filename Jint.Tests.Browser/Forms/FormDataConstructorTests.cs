using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Forms;

public sealed class FormDataConstructorTests
{
    [Test]
    public async Task ReadsTheFormEntryList()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", "<form><input name='query' value='hello'></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>(
            "new FormData(document.querySelector('form')).get('query')")).Should().Be("hello");
    }

    [Test]
    public async Task UsesSuccessfulControlsAndAssociatedControlsInTreeOrder()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", """
            <input form='f' name='duplicate' value='before'>
            <form id='f'>
              <input name='duplicate' value='inside'>
              <input name='off' disabled value='excluded'>
              <input value='unnamed'>
              <fieldset disabled><input name='fieldset' value='excluded'>
                <legend><input name='legend' value='included'></legend>
              </fieldset>
              <input type='checkbox' name='checked' checked>
              <input type='checkbox' name='unchecked'>
              <input type='radio' name='radio' value='off'>
              <input type='radio' name='radio' value='on' checked>
              <select name='many' multiple>
                <option value='a' selected>a</option><option value='b' selected>b</option>
                <option value='disabled' disabled selected>excluded</option>
              </select>
              <input type='submit' name='inputButton' value='excluded'>
              <button name='button' value='excluded'>submit</button>
              <datalist><input name='suggestion' value='excluded'></datalist>
            </form>
            <input form='f' name='duplicate' value='after'>
            """));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>(
            "JSON.stringify([...new FormData(document.querySelector('form'))])"))
            .Should().Be("""[["duplicate","before"],["duplicate","inside"],["legend","included"],["checked","on"],["radio","on"],["many","a"],["many","b"],["duplicate","after"]]""");
    }

    [TestCase("button")]
    [TestCase("input")]
    public async Task IncludesOnlyTheChosenSubmitter(string tag)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", """
            <form id='f'><input name='query' value='hello'>
              <button name='action' value='button'>submit</button>
              <input type='submit' name='action' value='input'>
            </form>
            """));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>(
            "new FormData(document.querySelector('form'), document.querySelector('" +
            (tag == "input" ? "input[type=submit]" : tag) + "')).get('action')"))
            .Should().Be(tag);
    }

    [TestCase("null", "undefined", "TypeError")]
    [TestCase("{}", "undefined", "TypeError")]
    [TestCase("document.body", "undefined", "TypeError")]
    [TestCase("document.querySelector('form')", "{}", "TypeError")]
    [TestCase("document.querySelector('form')", "document.body", "TypeError")]
    [TestCase("document.querySelector('form')", "document.querySelector('input')", "TypeError")]
    [TestCase("document.querySelector('form')", "document.querySelector('#other')", "NotFoundError")]
    [TestCase("undefined", "{}", "TypeError")]
    public async Task ValidatesFormAndSubmitter(string form, string submitter, string error)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", "<form><input></form><form><button id='other'>submit</button></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>(
            "(() => { try { new FormData(" + form + ", " + submitter + "); return 'no error'; } " +
            "catch (e) { return [e.name, e instanceof " +
            (error == "TypeError" ? "TypeError" : "DOMException") + "].join('|'); } })()"))
            .Should().Be(error + "|true");
    }

    [Test]
    public async Task OmittedFormAndNullSubmitterKeepAnEmptyOrUnsubmittedEntryList()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", "<form><input name='query' value='hello'><button name='submit'>submit</button></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>("""
            JSON.stringify([
              [...new FormData()], [...new FormData(undefined, document.body)],
              [...new FormData(document.querySelector('form'), null)],
              [...new FormData(document.querySelector('form'), undefined)]
            ])
            """)).Should().Be("""[[],[],[["query","hello"]],[["query","hello"]]]""");
    }

    [Test]
    public async Task FormDataEventAmendsAnIndependentResultAndGuardsOnlyItsOwnForm()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", """
            <form id='f'><input name='query' value='hello' required><button>submit</button></form>
            <form id='g'><input name='other' value='world'></form>
            """));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>("""
            (() => {
              const form = document.querySelector('#f');
              const log = [];
              let eventData;
              form.addEventListener('submit', () => log.push('submit'));
              form.addEventListener('invalid', () => log.push('invalid'), true);
              document.addEventListener('formdata', e => log.push('bubble:' + e.target.id));
              form.addEventListener('formdata', e => {
                log.push([e.bubbles, e.cancelable, e.isTrusted, e.formData instanceof FormData].join(':'));
                eventData = e.formData;
                eventData.set('query', 'amended');
                for (let i = 0; i < 2; i++) {
                  try { new FormData(form); }
                  catch (error) { log.push(error.name + ':' + (error instanceof DOMException)); }
                }
                form.submit();
                form.requestSubmit();
                log.push(new FormData(document.querySelector('#g')).get('other'));
              }, { once: true });
              const data = new FormData(form);
              eventData.set('query', 'too late');
              log.push(data !== eventData, data.get('query'));
              log.push(new FormData(form).get('query'));
              return log.join('|');
            })()
            """)).Should().Be("true:false:true:true|InvalidStateError:true|InvalidStateError:true|bubble:g|world|bubble:f|true|amended|bubble:f|hello");
    }

    [Test]
    public async Task SubclassAndPrototypeGetterRunBeforeEntryConstruction()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", "<form><input name='query' value='hello'></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAsync<string>("""
            (() => {
              const form = document.querySelector('form');
              const log = [];
              form.addEventListener('formdata', () => log.push('event'));
              class Derived extends FormData {}
              const subclass = new Derived(form);
              log.push(subclass instanceof Derived, subclass instanceof FormData, subclass.get('query'));
              const target = new Proxy(function() {}, { get(target, key) {
                if (key === 'prototype') { log.push('prototype'); return FormData.prototype; }
                return target[key];
              }});
              const data = Reflect.construct(FormData, [form], target);
              log.push(data.get('query'));
              return log.join('|');
            })()
            """)).Should().Be("event|true|true|hello|prototype|event|hello");
    }

    [Test]
    public async Task FileInputsProduceRealmFilesAndTheResultSerializesAsARequestBody()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/form.html", "<form><input name='query' value='hello'><input type='file' name='upload'></form>"));
        await fixture.Page.NavigateAsync(fixture.Url("/form.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("""
            (async () => {
              const data = new FormData(document.querySelector('form'));
              const file = data.get('upload');
              const copy = await new Response(data).formData();
              return [file instanceof File, file.name, file.size, file.type, copy.get('query')].join('|');
            })()
            """)).Should().Be("true||0|application/octet-stream|hello");
    }
}
