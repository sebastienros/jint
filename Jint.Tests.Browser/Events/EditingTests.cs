using Jint.Browser.Events;
using Jint.WebApi.Events;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;
using Page = global::Jint.Browser.Page;

/// <summary>
/// The editor under the keyboard: the selection a caret key moves, the three events every edit fires and in
/// what order, what a cancelation refuses, and the one editing host that is not a form control.
/// </summary>
/// <remarks>
/// <c>InputDispatcherTests</c> is the same editor reached through a key press; this is the table itself —
/// every command a key maps to, and the state it leaves behind. Both drive the dispatcher rather than
/// <c>TextEditing</c> directly, because the key is what decides which command runs.
/// </remarks>
public sealed class EditingTests
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#dom-textarea-input-selectiondirection —
    /// <kbd>Shift</kbd> extends from the anchor, so the direction says which end the caret is at and a
    /// reversal collapses through the anchor rather than around it.
    /// </summary>
    [Test]
    public async Task ShiftExtendsTheSelectionFromItsAnchorAndRecordsTheDirection()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='abcdef'>
            <script>
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(3, 3);
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowRight", EventModifiers.Shift);
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowRight", EventModifiers.Shift);
        (await Selection(page)).Should().Be("3,5,forward");

        // Back the other way through the anchor: the focus end crosses it and the direction flips.
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowLeft", EventModifiers.Shift);
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowLeft", EventModifiers.Shift);
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowLeft", EventModifiers.Shift);
        (await Selection(page)).Should().Be("2,3,backward");

        // Shift+Home takes the focus end to the start; Shift+End takes it to the end.
        await BrowserTestAccess.DispatchKeyAsync(page, "Home", EventModifiers.Shift);
        (await Selection(page)).Should().Be("0,3,backward");

        await BrowserTestAccess.DispatchKeyAsync(page, "End", EventModifiers.Shift);
        (await Selection(page)).Should().Be("3,6,forward");

        // And an unshifted arrow collapses to the near end rather than moving one place from it.
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowLeft");
        (await Selection(page)).Should().Be("3,3,none");
    }

    /// <summary>
    /// <kbd>ArrowUp</kbd> and <kbd>ArrowDown</kbd> in a <c>&lt;textarea&gt;</c> are line moves, computed from
    /// the newlines in the value rather than from a rendering, because nothing wraps here.
    /// </summary>
    [Test]
    public async Task TheVerticalArrowsMoveByLineInATextareaAndByEndInAnInput()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <textarea id='a'></textarea>
            <input id='b' type='text' value='abcdef'>
            <script>
              const a = document.getElementById('a');
              a.value = 'one\nthree\nfive';
              a.focus();
              a.setSelectionRange(2, 2);
            </script>
            """);

        // Column 2 of "one" is column 2 of "three".
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowDown");
        (await SelectionOf(page, "a")).Should().Be("6,6,none");

        // …and of "five", which is shorter than the column would need only when it is.
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowDown");
        (await SelectionOf(page, "a")).Should().Be("12,12,none");

        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowUp");
        (await SelectionOf(page, "a")).Should().Be("6,6,none");

        // Past the last line is the end of the value, and past the first is its start.
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowUp");
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowUp");
        (await SelectionOf(page, "a")).Should().Be("0,0,none");

        // A single-line control has one line, so the vertical arrows are Home and End.
        await page.EvaluateAsync("(() => { const b = document.getElementById('b'); b.focus(); b.setSelectionRange(3, 3); })()");
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowDown");
        (await SelectionOf(page, "b")).Should().Be("6,6,none");

        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowUp");
        (await SelectionOf(page, "b")).Should().Be("0,0,none");
    }

    /// <summary>
    /// <kbd>Ctrl</kbd>/<kbd>Meta</kbd>+<kbd>A</kbd> selects the value, and fires nothing: selecting is not an
    /// edit.
    /// </summary>
    [Test]
    public async Task SelectAllTakesTheWholeValueAndFiresNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' value='abcdef'>
            <script>
              window.edits = 0;
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(0, 0);
              t.addEventListener('input', () => window.edits++);
              t.addEventListener('beforeinput', () => window.edits++);
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "a", EventModifiers.Control);
        (await Selection(page)).Should().Be("0,6,forward");
        (await page.EvaluateAsync<string>("String(window.edits)")).Should().Be("0");

        // And the selection is what the next character replaces.
        await BrowserTestAccess.TypeAsync(page, "z");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("z");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-maxlength — the
    /// maximum applies to what a user types, so an insertion is truncated to what fits rather than refused.
    /// </summary>
    [Test]
    public async Task MaxlengthTruncatesAnInsertionAndStopsOneThatWouldNotFitAtAll()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text' maxlength='4' value='ab'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(2, 2);
              t.addEventListener('input', e => window.log.push(e.data));
            </script>
            """);

        await BrowserTestAccess.InsertTextAsync(page, "cdef");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("abcd");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("cd", "the event carries what was really inserted");

        // Nothing fits now, so nothing is inserted and nothing is reported.
        await BrowserTestAccess.TypeAsync(page, "z");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("abcd");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("cd");

        // The maximum bounds a user's edit, not the value: a script may still assign past it.
        await page.EvaluateAsync("document.getElementById('t').value = 'abcdefgh'");
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("abcdefgh");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps — <c>change</c> fires when
    /// the control loses focus, and on <kbd>Enter</kbd> in a single-line control, if the value moved.
    /// </summary>
    [Test]
    public async Task ChangeFiresOnEnterAndOnBlurAndOnlyOncePerEdit()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='t' type='text'>
            <input id='other' type='text'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.addEventListener('change', () => window.log.push('change:' + t.value));
            </script>
            """);

        // Enter with nothing typed changes nothing, so no change fires.
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("");

        await BrowserTestAccess.TypeAsync(page, "hi");
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("change:hi");

        // Enter again with nothing further typed does not fire a second one: the commit re-armed the value.
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("change:hi");

        // …and neither does moving focus away, for the same reason.
        await page.EvaluateAsync("document.getElementById('other').focus()");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("change:hi");

        // Typing again and then blurring does.
        await page.EvaluateAsync("document.getElementById('t').focus()");
        await BrowserTestAccess.TypeAsync(page, "!");
        await page.EvaluateAsync("document.getElementById('other').focus()");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("change:hi|change:hi!");
    }

    /// <summary>
    /// https://w3c.github.io/input-events/#event-type-beforeinput — every command fires it, so cancelling it
    /// refuses a deletion exactly as it refuses an insertion, and <c>input</c> does not follow.
    /// </summary>
    [Test]
    public async Task CancellingBeforeinputRefusesEveryCommandAndInputNeverFollows()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <textarea id='t'>abc</textarea>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(3, 3);
              t.addEventListener('beforeinput', e => { window.log.push('before:' + e.inputType + ':' + e.cancelable); e.preventDefault(); });
              t.addEventListener('input', e => window.log.push('input:' + e.inputType));
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "z");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        await BrowserTestAccess.DispatchKeyAsync(page, "Home");
        await BrowserTestAccess.DispatchKeyAsync(page, "Delete");
        await BrowserTestAccess.DispatchKeyAsync(page, "Enter");
        await BrowserTestAccess.InsertTextAsync(page, "q");

        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("abc");
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "before:insertText:true|before:deleteContentBackward:true|" +
            "before:deleteContentForward:true|before:insertLineBreak:true|before:insertText:true");
    }

    /// <summary>
    /// <c>contenteditable</c>, in the one shape a browser without a rendering can be exact about: a caret in
    /// a text node, and text spliced at it.
    /// </summary>
    [Test]
    public async Task ContentEditableTakesTextAtTheCaretAndDeletesAroundIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id='h' contenteditable>hello</div>
            <script>
              window.log = [];
              const h = document.getElementById('h');
              h.focus();
              h.addEventListener('beforeinput', e => window.log.push('before:' + e.inputType));
              h.addEventListener('input', e => window.log.push('input:' + e.inputType + ':' + (e.data ?? '')));
            </script>
            """);

        // Focusing an editing host puts the caret at the end of its text, which is where a click on the
        // empty space after it would put one.
        await BrowserTestAccess.TypeAsync(page, "!");
        (await page.EvaluateAsync<string>("document.getElementById('h').textContent")).Should().Be("hello!");

        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        (await page.EvaluateAsync<string>("document.getElementById('h').textContent")).Should().Be("hell");

        await BrowserTestAccess.DispatchKeyAsync(page, "Home");
        await BrowserTestAccess.InsertTextAsync(page, "S");
        (await page.EvaluateAsync<string>("document.getElementById('h').textContent")).Should().Be("Shell");

        await BrowserTestAccess.DispatchKeyAsync(page, "Delete");
        (await page.EvaluateAsync<string>("document.getElementById('h').textContent")).Should().Be("Sell");

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be(
            "before:insertText|input:insertText:!|" +
            "before:deleteContentBackward|input:deleteContentBackward:|" +
            "before:deleteContentBackward|input:deleteContentBackward:|" +
            "before:insertText|input:insertText:S|" +
            "before:deleteContentForward|input:deleteContentForward:");

        // The caret is the document's own selection, so a page reading it sees where typing goes.
        (await page.EvaluateAsync<string>(
            "(() => { const s = getSelection(); return [s.focusNode.nodeType, s.focusOffset, s.isCollapsed].join(','); })()"))
            .Should().Be("3,1,true");
    }

    /// <summary>
    /// A read-only or disabled control is not edited, and neither is an <c>&lt;input type=checkbox&gt;</c>,
    /// whose selection members HTML raises on at all.
    /// </summary>
    [Test]
    public async Task ARefusedControlIsNotEdited()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='ro' type='text' value='ro' readonly>
            <input id='cb' type='checkbox'>
            <input id='dis' type='text' value='dis' disabled>
            <script>
              window.edits = 0;
              document.getElementById('ro').focus();
              for (const id of ['ro', 'cb', 'dis']) {
                document.getElementById(id).addEventListener('beforeinput', () => window.edits++);
              }
            </script>
            """);

        await BrowserTestAccess.TypeAsync(page, "x");
        await BrowserTestAccess.DispatchKeyAsync(page, "Backspace");
        (await page.EvaluateAsync<string>("document.getElementById('ro').value")).Should().Be("ro");

        await page.EvaluateAsync("document.getElementById('cb').focus()");
        await BrowserTestAccess.TypeAsync(page, "x");
        await BrowserTestAccess.InsertTextAsync(page, "y");
        (await page.EvaluateAsync<string>("document.getElementById('cb').checked ? 'on' : 'off'")).Should().Be("off");

        // A disabled control cannot even be focused, so the key goes wherever focus really is.
        await page.EvaluateAsync("document.getElementById('dis').focus()");
        await BrowserTestAccess.TypeAsync(page, "x");
        (await page.EvaluateAsync<string>("document.getElementById('dis').value")).Should().Be("dis");

        (await page.EvaluateAsync<string>("String(window.edits)")).Should().Be("0", "nothing here is editable, so nothing fires beforeinput");
    }

    private static Task<string?> Selection(Page page) => SelectionOf(page, "t");

    private static Task<string?> SelectionOf(Page page, string id) => page.EvaluateAsync<string>(
        $"(() => {{ const t = document.getElementById('{id}'); return [t.selectionStart, t.selectionEnd, t.selectionDirection].join(','); }})()");
}
