namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>selectionchange</c>: what fires it, where it is fired, and how often.
/// <para>
/// https://w3c.github.io/selection-api/#selectionchange-event
/// </para>
/// </summary>
public sealed class SelectionChangeTests
{
    /// <summary>
    /// Moving the document's selection fires <c>selectionchange</c> at the document, once per turn however
    /// many times it moved, and it does not bubble.
    /// </summary>
    [Test]
    public async Task MovingTheDocumentSelectionFiresOneEventPerTurnAtTheDocument()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <p id='p'>one two three</p>
            <script>
              window.seen = [];
              document.addEventListener('selectionchange', e =>
                window.seen.push([e.target === document, e.bubbles, e.cancelable, e.isTrusted].join(',')));
            </script>
            """);

        // Three moves in one turn. The specification's "has scheduled selectionchange event" flag is what
        // makes them one event, fired after the script that made them returns.
        await page.EvaluateAsync(
            """
            (() => {
              const s = getSelection();
              s.selectAllChildren(document.getElementById('p'));
              s.collapseToStart();
              s.removeAllRanges();
              return true;
            })()
            """);

        (await page.EvaluateAsync<string>("window.seen.join(';')")).Should().Be("true,false,false,true");

        // A later turn is a second event: the flag is cleared when the first one fires.
        await page.EvaluateAsync("getSelection().selectAllChildren(document.getElementById('p')) || true");
        (await page.EvaluateAsync<int>("window.seen.length")).Should().Be(2);

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A caret key in a text control fires <c>selectionchange</c> at the <i>control</i>, and it bubbles — so
    /// the one document-level listener every editor library writes hears it.
    /// </summary>
    /// <remarks>
    /// This is the half the issue was about: <c>window.getSelection()</c> reflected the caret the editor
    /// moved and a page listening on the document heard nothing. It is fired at the element rather than at
    /// the document because that is what the specification says for a text control, and it is what makes
    /// <c>e.target.selectionStart</c> readable from the listener.
    /// </remarks>
    [Test]
    public async Task ACaretKeyInATextControlFiresAtTheControlAndBubblesToTheDocument()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <input id='t' type='text' value='abcdef'>
            <script>
              window.seen = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(3, 3);
              document.addEventListener('selectionchange', e =>
                window.seen.push([e.target.id, e.bubbles, e.target.selectionStart].join(',')));
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowRight");

        (await page.EvaluateAsync<string>("window.seen.join(';')")).Should().Be("t,true,4");

        // Typing moves the caret too, so it is a second one.
        await BrowserTestAccess.DispatchKeyAsync(page, "x");
        (await page.EvaluateAsync<string>("window.seen.join(';')")).Should().Be("t,true,4;t,true,5");

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A move that moves nothing fires nothing, which is what "the selection changes in either extent or
    /// direction" means.
    /// </summary>
    [Test]
    public async Task AKeyThatLeavesTheSelectionWhereItIsFiresNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <input id='t' type='text' value='abc'>
            <script>
              window.seen = 0;
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(3, 3);
              document.addEventListener('selectionchange', () => window.seen++);
            </script>
            """);

        // The caret is already at the end, so the clamp turns the move into no move at all.
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowRight");
        (await page.EvaluateAsync<int>("window.seen")).Should().Be(0);

        // And one that does move is heard, so the assertion above is about the move and not about the wiring.
        await BrowserTestAccess.DispatchKeyAsync(page, "ArrowLeft");
        (await page.EvaluateAsync<int>("window.seen")).Should().Be(1);

        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The caret an editing host keeps is the document's own selection, so typing into a
    /// <c>contenteditable</c> is a document-level <c>selectionchange</c>.
    /// </summary>
    [Test]
    public async Task TypingIntoAnEditingHostMovesTheDocumentSelectionAndSaysSo()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id='host' contenteditable='true'>ab</div>
            <script>
              window.seen = [];
              document.getElementById('host').focus();
              document.addEventListener('selectionchange', e => window.seen.push(e.target === document));
            </script>
            """);

        await BrowserTestAccess.DispatchKeyAsync(page, "c");

        (await page.EvaluateAsync<int>("window.seen.length")).Should().BeGreaterThan(0);
        (await page.EvaluateAsync<bool>("window.seen.every(Boolean)")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('host').textContent")).Should().Be("abc");

        page.Errors.Should().BeEmpty();
    }
}
