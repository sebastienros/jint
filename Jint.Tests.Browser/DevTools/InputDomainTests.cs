using System.Text.Json;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>Input</c> domain's keyboard half over a real page: the four event types, the modifier bit field,
/// and the two commands that type without a key at all.
/// </summary>
/// <remarks>
/// <para>
/// These assert the envelope as text, for the reason <c>Jint.DevTools/AGENTS.md</c> gives: a client library
/// matches on <c>result</c> shapes and on <c>error.code</c>, and a test that called the domain method
/// directly would pass with the envelope broken.
/// </para>
/// <para>
/// What each command <i>does</i> below the element is <c>InputDispatcher</c>'s and is tested against it
/// directly in <c>Events/InputDispatcherTests</c> and <c>Events/EditingTests</c>; what is here is the mapping
/// — which protocol type fires which events, and which parameters reach them.
/// </para>
/// </remarks>
[NonParallelizable]
public class InputDomainTests
{
    [Test]
    public async Task KeyDownFiresKeydownAndKeypressAndEditsTheFocusedControl()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <input id='t' type='text'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              for (const type of ['keydown', 'keypress', 'beforeinput', 'input', 'keyup']) {
                t.addEventListener(type, e => window.log.push(type + ':' + (e.key ?? e.inputType)));
              }
            </script>
            """);

        await Key(session, attachment, """{"type":"keyDown","key":"h","code":"KeyH","text":"h","unmodifiedText":"h","windowsVirtualKeyCode":72}""");
        await Key(session, attachment, """{"type":"keyUp","key":"h","code":"KeyH","windowsVirtualKeyCode":72}""");

        (await Evaluate(session, attachment, "document.getElementById('t').value")).Should().Be("h");
        (await Evaluate(session, attachment, "window.log.join('|')"))
            .Should().Be("keydown:h|keypress:h|beforeinput:insertText|input:insertText|keyup:h");
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#legacy-key-attributes — on a <c>keypress</c> the legacy codes are the
    /// character the key really produced, which is what makes <kbd>Enter</kbd> answer 13 where its <c>key</c>
    /// is a name.
    /// </summary>
    [Test]
    public async Task AKeypressCarriesTheCharacterTheKeyProduced()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <input id='t' type='text'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.addEventListener('keypress', e => window.log.push(e.key + ':' + e.charCode + ':' + e.keyCode + ':' + e.which));
              t.addEventListener('keydown', e => window.log.push('down:' + e.key + ':' + e.charCode + ':' + e.keyCode));
            </script>
            """);

        await Key(session, attachment, """{"type":"keyDown","key":"h","code":"KeyH","text":"h","windowsVirtualKeyCode":72}""");
        await Key(session, attachment, """{"type":"keyDown","key":"Enter","code":"Enter","text":"\r","windowsVirtualKeyCode":13}""");
        await Key(session, attachment, """{"type":"rawKeyDown","key":"Tab","code":"Tab"}""");

        // Tab produces no text, so no keypress at all — which is what a browser does with it.
        (await Evaluate(session, attachment, "window.log.join('|')"))
            .Should().Be("down:h:0:72|h:104:104:104|down:Enter:0:13|Enter:13:13:13|down:Tab:0:9");
    }

    /// <summary>
    /// <c>rawKeyDown</c> is what a client sends for a key that produces no text, which is every editing key.
    /// </summary>
    [Test]
    public async Task RawKeyDownRunsTheEditingCommandWithoutAKeypress()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <input id='t' type='text' value='abc'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(3, 3);
              for (const type of ['keydown', 'keypress', 'input']) {
                t.addEventListener(type, e => window.log.push(type));
              }
            </script>
            """);

        await Key(session, attachment, """{"type":"rawKeyDown","key":"Backspace","code":"Backspace","windowsVirtualKeyCode":8}""");
        await Key(session, attachment, """{"type":"keyUp","key":"Backspace","code":"Backspace","windowsVirtualKeyCode":8}""");

        (await Evaluate(session, attachment, "document.getElementById('t').value")).Should().Be("ab");
        (await Evaluate(session, attachment, "window.log.join('|')")).Should().Be("keydown|input");
    }

    /// <summary>
    /// <c>char</c> is the other half of a <c>rawKeyDown</c>: the character, with no <c>keydown</c> of its own.
    /// </summary>
    [Test]
    public async Task CharFiresKeypressAloneAndInsertsTheText()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <input id='t' type='text'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              for (const type of ['keydown', 'keypress', 'input', 'keyup']) {
                t.addEventListener(type, e => window.log.push(type));
              }
            </script>
            """);

        await Key(session, attachment, """{"type":"char","key":"x","text":"x"}""");

        (await Evaluate(session, attachment, "document.getElementById('t').value")).Should().Be("x");
        (await Evaluate(session, attachment, "window.log.join('|')")).Should().Be("keypress|input");
    }

    /// <summary>
    /// The protocol's modifier bit field — <c>Alt=1</c>, <c>Ctrl=2</c>, <c>Meta=4</c>, <c>Shift=8</c> — is
    /// what a page reads back as the four modifier flags, and what decides an editing key's meaning.
    /// </summary>
    [Test]
    public async Task TheModifierBitFieldReachesTheEventAndTheEditor()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <input id='t' type='text' value='abcdef'>
            <script>
              window.flags = '';
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(6, 6);
              t.addEventListener('keydown', e => {
                window.flags = [e.altKey, e.ctrlKey, e.metaKey, e.shiftKey].join(',') + '/' + e.location + '/' + e.repeat;
              });
            </script>
            """);

        // Shift + ArrowLeft extends the selection rather than collapsing it, which is the modifier reaching
        // the editor rather than only the event.
        await Key(
            session,
            attachment,
            """{"type":"rawKeyDown","key":"ArrowLeft","code":"ArrowLeft","modifiers":9,"location":0,"autoRepeat":true}""");

        (await Evaluate(session, attachment, "window.flags")).Should().Be("true,false,false,true/0/true");

        (await Evaluate(
            session,
            attachment,
            "(() => { const t = document.getElementById('t'); return [t.selectionStart, t.selectionEnd].join(','); })()"))
            .Should().Be("5,6");
    }

    /// <summary>
    /// https://chromedevtools.github.io/devtools-protocol/tot/Input/#method-insertText — the IME commit
    /// shape: text arrives with no key events at all.
    /// </summary>
    [Test]
    public async Task InsertTextEditsWithoutAnyKeyEvent()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <input id='t' type='text'>
            <script>
              window.log = [];
              const t = document.getElementById('t');
              t.focus();
              for (const type of ['keydown', 'keypress', 'keyup', 'beforeinput', 'input']) {
                t.addEventListener(type, e => window.log.push(type + (e.data ? ':' + e.data : '')));
              }
            </script>
            """);

        await session.ResultAsync("Input.insertText", """{"text":"ありがとう"}""", attachment);

        (await Evaluate(session, attachment, "document.getElementById('t').value")).Should().Be("ありがとう");
        (await Evaluate(session, attachment, "window.log.join('|')"))
            .Should().Be("beforeinput:ありがとう|input:ありがとう");
    }

    /// <summary>
    /// <c>imeSetComposition</c> is accepted because a client that sends it and is refused stops typing; what
    /// it does is set the candidate text, and there is nothing to render a candidate with.
    /// </summary>
    [Test]
    public async Task ImeSetCompositionIsAcceptedAndChangesNothing()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(session, attachment, "<input id='t' type='text'><script>document.getElementById('t').focus();</script>");

        var reply = await session.SendAsync(
            "Input.imeSetComposition",
            """{"text":"あ","selectionStart":0,"selectionEnd":1}""",
            attachment);

        reply.TryGetProperty("error", out var error).Should().BeFalse("a client that is refused stops typing, and it answered {0}", error);
        (await Evaluate(session, attachment, "document.getElementById('t').value")).Should().Be("");
    }

    [Test]
    public async Task AnUnknownKeyEventTypeIsInvalidParams()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        var reply = await session.SendAsync("Input.dispatchKeyEvent", """{"type":"keyBoing","key":"a"}""", attachment);

        reply.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32602);
        reply.GetProperty("error").GetProperty("message").GetString().Should().Contain("keyBoing");
    }

    /// <summary>
    /// The editing commands a macOS client attaches to a key event: <c>selectAll</c> is honoured because it
    /// maps onto something this editor has, and everything else is accepted and ignored.
    /// </summary>
    [Test]
    public async Task TheSelectAllCommandIsHonouredAndTheRestAreIgnored()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await Content(
            session,
            attachment,
            """
            <input id='t' type='text' value='abcdef'>
            <script>
              const t = document.getElementById('t');
              t.focus();
              t.setSelectionRange(0, 0);
            </script>
            """);

        await Key(
            session,
            attachment,
            """{"type":"rawKeyDown","key":"a","code":"KeyA","modifiers":4,"commands":["selectAll"]}""");

        (await Evaluate(
            session,
            attachment,
            "(() => { const t = document.getElementById('t'); return [t.selectionStart, t.selectionEnd].join(','); })()"))
            .Should().Be("0,6");

        // A command nothing here maps is accepted, and the key's ordinary default action still runs.
        var reply = await session.SendAsync(
            "Input.dispatchKeyEvent",
            """{"type":"rawKeyDown","key":"ArrowRight","code":"ArrowRight","commands":["moveToEndOfLine"]}""",
            attachment);

        reply.TryGetProperty("error", out _).Should().BeFalse("an unmapped editing command is accepted, not refused");
    }

    /// <summary>A key with nothing focused goes to the body, which is what HTML's fallback amounts to.</summary>
    [Test]
    public async Task AKeyWithNothingFocusedReachesTheBody()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();

        await Content(
            session,
            attachment,
            """
            <p>nothing focused</p>
            <script>
              window.seen = '';
              document.addEventListener('keydown', e => { window.seen = e.target.tagName + ':' + e.key + ':' + e.isTrusted; });
            </script>
            """);

        await Key(session, attachment, """{"type":"rawKeyDown","key":"Escape","code":"Escape"}""");

        (await Evaluate(session, attachment, "window.seen")).Should().Be("BODY:Escape:true");
    }

    private static Task Key(PageSession session, string attachment, string parameters)
        => session.ResultAsync("Input.dispatchKeyEvent", parameters, attachment);

    private static async Task Content(PageSession session, string attachment, string body)
    {
        await session.ResultAsync(
            "Page.setDocumentContent",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["frameId"] = "", ["html"] = body }),
            attachment);
    }

    private static async Task<string?> Evaluate(PageSession session, string attachment, string expression)
    {
        var result = await session.ResultAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new Dictionary<string, object> { ["expression"] = expression, ["returnByValue"] = true }),
            attachment);

        return result.GetProperty("result").GetProperty("value").GetString();
    }
}
