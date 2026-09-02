namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Focus without a layout — https://html.spec.whatwg.org/multipage/interaction.html#focus.
/// </summary>
public sealed class FocusTests
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps — the old chain first, so
    /// <c>blur</c> then <c>focusout</c>, then the new chain's <c>focus</c> then <c>focusin</c>. The first two
    /// of each pair do not bubble and the second two do.
    /// </summary>
    [Test]
    public async Task MovingFocusFiresBlurFocusoutFocusAndFocusinInThatOrder()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='box'><input id='a'><input id='b'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const box = document.getElementById('box');
              const a = document.getElementById('a');
              const b = document.getElementById('b');

              // Capturing on the container catches the non-bubbling pair too, which is the only way to see
              // `focus` and `blur` from an ancestor.
              for (const type of ['focus', 'blur', 'focusin', 'focusout']) {
                box.addEventListener(type, e =>
                  seen.push(type + ':' + e.target.id + ':' + (e.relatedTarget ? e.relatedTarget.id : 'null') + ':' + e.bubbles), true);
              }

              a.focus();
              const first = document.activeElement.id;
              b.focus();
              const second = document.activeElement.id;
              b.blur();
              return [first, second, document.activeElement.tagName, seen.join('|')].join(',');
            })()
            """)).Should().Be(
            "a,b,BODY," +
            "focus:a:null:false|focusin:a:null:true|" +
            "blur:a:b:false|focusout:a:b:true|focus:b:a:false|focusin:b:a:true|" +
            "blur:b:null:false|focusout:b:null:true");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-document-activeelement — the body when
    /// nothing is focused, which AngleSharp's own <c>ActiveElement</c> never answers because it never assigns
    /// one.
    /// </summary>
    [Test]
    public async Task ActiveElementIsTheBodyUntilSomethingTakesFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");

        (await page.EvaluateAsync<string>(
            """
            [
              document.activeElement.tagName,
              document.hasFocus(),
              (document.getElementById('a').focus(), document.activeElement.id),
              (document.getElementById('a').blur(), document.activeElement.tagName)
            ].join(',')
            """)).Should().Be("BODY,true,a,BODY");
    }

    /// <summary>
    /// Focusability without a rendering: the element's own kind, or a <c>tabindex</c> content attribute.
    /// AngleSharp's <c>TabIndex</c> cannot decide it — it answers 0 for every element, including a bare
    /// <c>&lt;div&gt;</c>, where HTML says −1.
    /// </summary>
    [Test]
    public async Task OnlyAFocusableElementTakesFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id='plain'>text</div>
            <div id='indexed' tabindex='0'>text</div>
            <div id='negative' tabindex='-1'>text</div>
            <a id='link' href='#x'>link</a>
            <a id='anchor'>no href</a>
            <button id='button'>go</button>
            <button id='disabled' disabled>no</button>
            <input id='hiddenInput' type='hidden'>
            <select id='select'></select>
            <textarea id='textarea'></textarea>
            <details><summary id='summary'>s</summary></details>
            <span id='hidden' tabindex='0' hidden>x</span>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const ids = ['plain', 'indexed', 'negative', 'link', 'anchor', 'button', 'disabled',
                           'hiddenInput', 'select', 'textarea', 'summary', 'hidden'];
              return ids.map(id => {
                document.body.focus();
                document.activeElement && document.activeElement.blur && document.activeElement.blur();
                document.getElementById(id).focus();
                return document.activeElement.id === id ? id : '-';
              }).join(',');
            })()
            """)).Should().Be("-,indexed,negative,link,-,button,-,-,select,textarea,summary,-");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#the-autofocus-attribute — the first focusable
    /// element asking for it takes focus once the document has parsed.
    /// </summary>
    [Test]
    public async Task AutofocusTakesEffectOnLoad()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id='before' autofocus>not focusable</div>
            <input id='first' autofocus>
            <input id='second' autofocus>
            <script>
              window.log = [];
              document.addEventListener('focusin', e => window.log.push('focusin:' + e.target.id));
            </script>
            """);

        (await page.EvaluateAsync<string>("[document.activeElement.id, window.log.join('|')].join(',')"))
            .Should().Be("first,focusin:first");
    }

    [Test]
    public async Task AutofocusDoesNothingWhenTheDocumentAlreadyMovedFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <input id='a'>
            <input id='b' autofocus>
            <script>document.getElementById('a').focus();</script>
            """);

        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("a");
    }

    /// <summary>
    /// A focused element taken out of the tree stops being the active element, which is what HTML's "no longer
    /// being rendered" clause amounts to with no rendering.
    /// </summary>
    [Test]
    public async Task RemovingTheFocusedElementReturnsFocusToTheBody()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const a = document.getElementById('a');
              a.focus();
              const before = document.activeElement.id;
              a.remove();
              return [before, document.activeElement.tagName].join(',');
            })()
            """)).Should().Be("a,BODY");
    }

    [Test]
    public async Task FocusingTheAlreadyFocusedElementFiresNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let count = 0;
              const a = document.getElementById('a');
              a.addEventListener('focus', () => count++);
              a.focus();
              a.focus();
              a.focus();
              return String(count);
            })()
            """)).Should().Be("1");
    }

    /// <summary>
    /// A click focuses what was clicked, and a click on a non-focusable descendant focuses the nearest
    /// focusable ancestor — which is why clicking the text inside a button focuses the button.
    /// </summary>
    [Test]
    public async Task AClickThroughTheInputDispatcherFocusesTheNearestFocusableAncestor()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<button id='b'><span id='label'>Go</span></button><p id='p'>text</p>");

        await BrowserTestAccess.DispatchClickAsync(page, "label");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");

        // Clicking something with no focusable ancestor leaves focus where it was, which is a browser's
        // behaviour for a click on inert text.
        await BrowserTestAccess.DispatchClickAsync(page, "p");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");
    }

    /// <summary>
    /// A click from the input dispatcher is trusted, because a protocol client driving a page stands in for a
    /// user; <c>element.click()</c> is not.
    /// </summary>
    [Test]
    public async Task AClickFromTheInputDispatcherIsTrustedAndActivates()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='c' type='checkbox'>
            <script>
              window.log = [];
              document.getElementById('c').addEventListener('click', e => window.log.push(e.isTrusted + ':' + e.detail));
            </script>
            """);

        await BrowserTestAccess.DispatchClickAsync(page, "c");

        (await page.EvaluateAsync<string>("[document.getElementById('c').checked, document.activeElement.id, window.log.join('|')].join(',')"))
            .Should().Be("true,c,true:1");
    }
}
