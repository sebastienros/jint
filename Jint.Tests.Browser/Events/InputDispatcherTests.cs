using Jint.Browser.Events;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Editing without a layout: typing into a text control, the <c>beforeinput</c> / <c>input</c> / <c>change</c>
/// events each edit fires, and the two keys that are not edits at all — <kbd>Enter</kbd>'s implicit submission
/// and <kbd>Tab</kbd>'s focus traversal.
/// </summary>
/// <remarks>
/// These drive <c>InputDispatcher</c> directly, which is the surface CDP's <c>Input.dispatchKeyEvent</c> maps
/// onto (campaign item C4). What C4 adds above it is coordinates and the protocol's own modifier bookkeeping.
/// </remarks>
public sealed class InputDispatcherTests
{
    [Test]
    public async Task TypingIntoAFocusedInputProducesTheValueOneCharacterAtATime()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              for (const type of ['keydown', 'keypress', 'beforeinput', 'input', 'keyup']) {
                t.addEventListener(type, e =>
                  window.log.push(type + (e.inputType ? ':' + e.inputType + ':' + e.data : e.key ? ':' + e.key : '')));
              }
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "hi");

        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("hi");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "keydown:h|keypress:h|beforeinput:insertText:h|input:insertText:h|keyup:h|" +
            "keydown:i|keypress:i|beforeinput:insertText:i|input:insertText:i|keyup:i");
    }

    [Test]
    public async Task TheEditingKeysMoveTheCaretAndSpliceTheValue()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='t' type='text' value='abcdef'><script>document.getElementById('t').focus();</script>");

        // The caret starts at the beginning; End takes it to the far side, Backspace removes the character
        // before it, Home and ArrowRight put it back inside, Delete removes the one after.
        await BrowserTestAccess.DispatchKeyAsync(page, "End");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("abcde");

        await BrowserTestAccess.DispatchKeyAsync(page, "Home");
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowRight");
        await BrowserTestAccess.DispatchKeyAsync(page, "Delete");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("acde");

        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowLeft");
        await BrowserTestAccess.TypeAsync(page, "X");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("Xacde");

        // A Backspace at the start of the value is not an edit and fires nothing.
        (await page.EvaluateAsync<string>(
            """
            (() => {
              window.edits = 0;
              document.getElementById('t').addEventListener('input', () => window.edits++);
              return 'ready';
            })()
            """)).Should().Be("ready");

        await BrowserTestAccess.DispatchKeyAsync(page, "Home");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        (await page.EvaluateAsync<string>("[document.getElementById('t').value, window.edits].join(',')")).Should().Be("Xacde,0");
    }

    [Test]
    public async Task TypingReplacesTheSelectionAndTheSelectionApiDrivesIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='hello world'>
            <script>
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(0, 5);
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "bye");

        (await page.EvaluateAsync<string>(
            "(() => { const t = document.getElementById('t'); return [t.value, t.selectionStart, t.selectionEnd].join('|'); })()"))
            .Should().Be("bye world|3|3");

        // select() takes the whole value, and Backspace on a selection deletes it rather than one character.
        await page.EvaluateAsync("document.getElementById('t').select()");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("");
    }

    /// <summary>
    /// https://w3c.github.io/input-events/#event-type-beforeinput — cancelable, so a listener can refuse the
    /// edit outright.
    /// </summary>
    [Test]
    public async Task CancellingBeforeinputRefusesTheEdit()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='keep'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(4, 4);
              t.addEventListener('beforeinput', e => { window.log.push('beforeinput:' + e.inputType); e.preventDefault(); });
              t.addEventListener('input', () => window.log.push('input'));
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "x");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");

        (await page.EvaluateAsync<string>("[document.getElementById('t').value, window.log.join('|')].join(',')"))
            .Should().Be("keep,beforeinput:insertText|beforeinput:deleteContentBackward");
    }

    [Test]
    public async Task CancellingKeydownStopsTheDefaultActionAndKeyupStillFires()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='keep'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(4, 4);
              t.addEventListener('keydown', e => { window.log.push('keydown'); e.preventDefault(); });
              t.addEventListener('keypress', () => window.log.push('keypress'));
              t.addEventListener('input', () => window.log.push('input'));
              t.addEventListener('keyup', () => window.log.push('keyup'));
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "x");

        (await page.EvaluateAsync<string>("[document.getElementById('t').value, window.log.join('|')].join(',')"))
            .Should().Be("keep,keydown|keyup");
    }

    /// <summary>
    /// <c>change</c> fires when a control the user edited loses focus, and only then —
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps.
    /// </summary>
    [Test]
    public async Task ChangeFiresOnBlurOnlyWhenTheValueWasEdited()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='a'><input id='b'>
            <script>
              window.log = [];
              document.addEventListener('change', e => window.log.push('change:' + e.target.id));
              document.getElementById('a').focus();
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "hi");
        await page.EvaluateAsync("document.getElementById('b').focus()");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("change:a");

        // Focusing and leaving without typing fires nothing more.
        await page.EvaluateAsync("document.getElementById('a').focus(); document.getElementById('b').focus();");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("change:a");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#implicit-submission —
    /// <kbd>Enter</kbd> in a single-line control submits its form through the default button when there is one.
    /// </summary>
    [Test]
    public async Task EnterSubmitsThroughTheDefaultButton()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f' action='/go'>
              <input id='q' type='search'>
              <button id='b' type='submit'>Search</button>
            </form>
            <script>
              window.log = [];
              document.getElementById('f').addEventListener('submit', e => {
                window.log.push('submit:' + (e.submitter ? e.submitter.id : 'null'));
                e.preventDefault();
              });
              document.getElementById('q').focus();
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("submit:b");
    }

    /// <summary>
    /// With no submit button, HTML submits from the form itself only when exactly one field blocks implicit
    /// submission — which is what makes <kbd>Enter</kbd> submit a one-field search form and do nothing in a
    /// two-field login form.
    /// </summary>
    [Test]
    public async Task EnterSubmitsAOneFieldFormAndDoesNothingInATwoFieldOne()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='one' action='/a'><input id='q' type='search'></form>
            <form id='two' action='/b'><input id='u' type='text'><input id='p' type='password'></form>
            <script>
              window.log = [];
              for (const id of ['one', 'two']) {
                document.getElementById(id).addEventListener('submit', e => {
                  window.log.push('submit:' + e.target.id + ':' + (e.submitter ? e.submitter.id : 'null'));
                  e.preventDefault();
                });
              }
            </script>
            """);

        await page.EvaluateAsync("document.getElementById('q').focus()");
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");

        await page.EvaluateAsync("document.getElementById('u').focus()");
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("submit:one:null");
    }

    [Test]
    public async Task EnterInsertsALineBreakInATextarea()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form><textarea id='t'>ab</textarea></form>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(2, 2);
              t.addEventListener('input', e => window.log.push(e.inputType));
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        await BrowserTestAccess.TypeAsync(page, "c");

        (await page.EvaluateAsync<string>("[JSON.stringify(document.getElementById('t').value), window.log.join('|')].join(',')"))
            .Should().Be("\"ab\\nc\",insertLineBreak|insertText");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#sequential-focus-navigation — tree order, with
    /// a positive <c>tabindex</c> ahead of everything else and a negative one out of the sequence entirely.
    /// </summary>
    [Test]
    public async Task TabMovesFocusInSequentialOrderAndShiftTabMovesBack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='a'>
            <div id='skipped'>text</div>
            <input id='out' tabindex='-1'>
            <input id='b'>
            <button id='c'>go</button>
            <script>document.getElementById('a').focus();</script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");

        await BrowserTestAccess.DispatchKeyAsync(page, "Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("c");

        // The order wraps, because there is nothing above the document to hand focus to.
        await BrowserTestAccess.DispatchKeyAsync(page, "Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("a");

        await BrowserTestAccess.DispatchKeyAsync(page, "Tab", EventModifiers.Shift);
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("c");
    }

    [Test]
    public async Task APositiveTabIndexComesBeforeEverythingElse()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='first'>
            <input id='second' tabindex='2'>
            <input id='third' tabindex='1'>
            <script>document.getElementById('third').focus();</script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("second");

        await BrowserTestAccess.DispatchKeyAsync(page, "Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("first");
    }

    [Test]
    public async Task AKeyWithNothingFocusedGoesToTheBodyAndEditsNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='untouched'>
            <script>
              window.log = [];
              document.addEventListener('keydown', e => window.log.push('keydown:' + e.target.tagName + ':' + e.key));
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "x");

        (await page.EvaluateAsync<string>("[document.getElementById('t').value, window.log.join('|')].join(',')"))
            .Should().Be("untouched,keydown:BODY:x");
    }

    [Test]
    public async Task AReadOnlyOrDisabledControlIsNotEdited()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='r' type='text' value='ro' readonly><script>document.getElementById('r').focus();</script>");

        await BrowserTestAccess.TypeAsync(page, "x");
        (await page.EvaluateAsync<string>("document.getElementById('r').value")).Should().Be("ro");
    }

    /// <summary>
    /// A character typed with <kbd>Control</kbd> or <kbd>Meta</kbd> held is a shortcut, not an insertion, so a
    /// page's own handler sees it and the value does not change.
    /// </summary>
    [Test]
    public async Task AShortcutIsNotAnInsertion()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='keep'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.addEventListener('keydown', e => window.log.push(e.key + ':' + e.ctrlKey));
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "s", EventModifiers.Control);

        (await page.EvaluateAsync<string>("[document.getElementById('t').value, window.log.join('|')].join(',')"))
            .Should().Be("keep,s:true");
    }
}
