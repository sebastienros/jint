namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The UI event interfaces: constructing one from its init dictionary, reading every member back, and the
/// prototype and interface-object chains that make <c>instanceof</c> and <c>Object.prototype.toString</c>
/// answer what a browser answers.
/// </summary>
public sealed class UiEventTests
{
    [Test]
    public async Task EveryUiEventInterfaceIsAGlobalWhosePrototypeChainReachesEvent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            [
              Object.getPrototypeOf(MouseEvent.prototype) === UIEvent.prototype,
              Object.getPrototypeOf(UIEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(PointerEvent.prototype) === MouseEvent.prototype,
              Object.getPrototypeOf(WheelEvent.prototype) === MouseEvent.prototype,
              Object.getPrototypeOf(KeyboardEvent.prototype) === UIEvent.prototype,
              Object.getPrototypeOf(MouseEvent) === UIEvent,
              Object.getPrototypeOf(UIEvent) === Event,
              new PointerEvent('pointerdown') instanceof MouseEvent,
              new PointerEvent('pointerdown') instanceof UIEvent,
              new PointerEvent('pointerdown') instanceof Event,
              new SubmitEvent('submit') instanceof Event,
              MouseEvent.prototype.constructor === MouseEvent
            ].join(',')
            """)).Should().Be("true,true,true,true,true,true,true,true,true,true,true,true");
    }

    [Test]
    public async Task TheToStringTagIsTheInterfaceName()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            [
              new UIEvent('x'), new MouseEvent('x'), new PointerEvent('x'), new WheelEvent('x'),
              new KeyboardEvent('x'), new InputEvent('x'), new CompositionEvent('x'), new FocusEvent('x'),
              new SubmitEvent('x'), new HashChangeEvent('x'), new PopStateEvent('x'),
              new PageTransitionEvent('x'), new BeforeUnloadEvent('x')
            ].map(e => Object.prototype.toString.call(e)).join(',')
            """)).Should().Be(
            "[object UIEvent],[object MouseEvent],[object PointerEvent],[object WheelEvent]," +
            "[object KeyboardEvent],[object InputEvent],[object CompositionEvent],[object FocusEvent]," +
            "[object SubmitEvent],[object HashChangeEvent],[object PopStateEvent]," +
            "[object PageTransitionEvent],[object BeforeUnloadEvent]");
    }

    [Test]
    public async Task AMouseEventReadsBackEveryMemberOfItsInit()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='a'></div><div id='b'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const related = document.getElementById('b');
              const e = new MouseEvent('click', {
                bubbles: true, cancelable: true, composed: true,
                view: window, detail: 2,
                screenX: 11, screenY: 12, clientX: 13, clientY: 14,
                button: 2, buttons: 3,
                ctrlKey: true, shiftKey: true, altKey: false, metaKey: true,
                relatedTarget: related
              });
              return [
                e.type, e.bubbles, e.cancelable, e.composed, e.view === window, e.detail,
                e.screenX, e.screenY, e.clientX, e.clientY,
                e.pageX, e.pageY, e.offsetX, e.offsetY, e.x, e.y, e.movementX, e.movementY,
                e.button, e.buttons,
                e.ctrlKey, e.shiftKey, e.altKey, e.metaKey,
                e.relatedTarget === related,
                e.getModifierState('Control'), e.getModifierState('Alt'), e.getModifierState('CapsLock'),
                e.isTrusted
              ].join(',');
            })()
            """)).Should().Be(
            "click,true,true,true,true,2,11,12,13,14,13,14,13,14,13,14,0,0,2,3,true,true,false,true,true,true,false,false,false");
    }

    [Test]
    public async Task ThePointerAndWheelMembersReadBack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const p = new PointerEvent('pointerdown', {
                pointerId: 7, pointerType: 'pen', isPrimary: true,
                width: 3, height: 4, pressure: 0.5, tangentialPressure: 0.25, tiltX: -10, tiltY: 20, twist: 90
              });
              const w = new WheelEvent('wheel', { deltaX: 1, deltaY: 2, deltaZ: 3, deltaMode: 1 });
              return [
                p.pointerId, p.pointerType, p.isPrimary, p.width, p.height, p.pressure,
                p.tangentialPressure, p.tiltX, p.tiltY, p.twist,
                p.getCoalescedEvents().length, p.getPredictedEvents().length,
                w.deltaX, w.deltaY, w.deltaZ, w.deltaMode,
                WheelEvent.DOM_DELTA_PIXEL, WheelEvent.DOM_DELTA_LINE, WheelEvent.DOM_DELTA_PAGE,
                w.DOM_DELTA_PAGE
              ].join(',');
            })()
            """)).Should().Be("7,pen,true,3,4,0.5,0.25,-10,20,90,0,0,1,2,3,1,0,1,2,2");
    }

    [Test]
    public async Task APointerEventThatWasNotGivenSizesTakesTheSpecificationsDefaults()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            "(() => { const p = new PointerEvent('pointerdown'); return [p.width, p.height, p.pressure, p.pointerType, p.isPrimary].join(','); })()"))
            .Should().Be("1,1,0,,false");
    }

    [Test]
    public async Task AKeyboardEventReadsBackEveryMemberOfItsInit()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const e = new KeyboardEvent('keydown', {
                key: 'a', code: 'KeyA', location: 3, repeat: true, isComposing: true,
                ctrlKey: true, modifierCapsLock: true
              });
              return [
                e.key, e.code, e.location, e.repeat, e.isComposing,
                e.ctrlKey, e.shiftKey, e.altKey, e.metaKey,
                e.getModifierState('Control'), e.getModifierState('CapsLock'), e.getModifierState('Nonsense'),
                KeyboardEvent.DOM_KEY_LOCATION_NUMPAD, e.DOM_KEY_LOCATION_LEFT
              ].join(',');
            })()
            """)).Should().Be("a,KeyA,3,true,true,true,false,false,false,true,true,false,3,1");
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#legacy-key-attributes — <c>charCode</c> is zero except on
    /// <c>keypress</c>, <c>keyCode</c> is the virtual key code, and <c>which</c> is whichever is non-zero.
    /// </summary>
    [Test]
    public async Task TheLegacyKeyCodesFollowTheUiEventsTables()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const down = k => new KeyboardEvent('keydown', { key: k });
              const press = k => new KeyboardEvent('keypress', { key: k });
              return [
                down('Enter').keyCode, down('Tab').keyCode, down('Escape').keyCode,
                down('Backspace').keyCode, down('Delete').keyCode,
                down('ArrowLeft').keyCode, down('ArrowUp').keyCode,
                down('ArrowRight').keyCode, down('ArrowDown').keyCode,
                down('Home').keyCode, down('End').keyCode, down('F5').keyCode,
                down('a').keyCode, down('A').keyCode, down('7').keyCode, down(';').keyCode, down('?').keyCode,
                down('a').charCode, down('a').which,
                press('a').charCode, press('a').keyCode, press('a').which,
                press('Enter').charCode
              ].join(',');
            })()
            """)).Should().Be("13,9,27,8,46,37,38,39,40,36,35,116,65,65,55,186,191,0,65,97,97,97,0");
    }

    [Test]
    public async Task TheDictionaryCanPinALegacyKeyCodeAndAnExplicitZeroIsHonoured()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            [
              new KeyboardEvent('keydown', { key: 'a', keyCode: 999 }).keyCode,
              new KeyboardEvent('keydown', { key: 'a', keyCode: 0 }).keyCode,
              new KeyboardEvent('keydown', { key: 'a' }).keyCode
            ].join(',')
            """)).Should().Be("999,0,65");
    }

    /// <summary>
    /// https://w3c.github.io/uievents/#dom-uievent-which — a legacy attribute the standard leaves to the
    /// interface: zero on a plain <c>UIEvent</c>, the button plus one on a mouse event, and the character or
    /// key code on a keyboard event. An explicit value in the dictionary wins over all three.
    /// </summary>
    [Test]
    public async Task TheLegacyWhichIsPerInterface()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            [
              new UIEvent('x').which,
              new UIEvent('x', { which: 7 }).which,
              new MouseEvent('click').which,
              new MouseEvent('click', { button: 2 }).which,
              new MouseEvent('click', { button: 2, which: 9 }).which,
              new KeyboardEvent('keydown', { key: 'Enter' }).which,
              new KeyboardEvent('keypress', { key: 'a' }).which,
              new PointerEvent('pointerdown', { button: 1 }).which
            ].join(',')
            """)).Should().Be("0,7,1,3,9,13,97,2");
    }

    [Test]
    public async Task TheHtmlEventsReadBackTheirOwnMembers()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f'><button id='b'>go</button></form>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const button = document.getElementById('b');
              const submit = new SubmitEvent('submit', { submitter: button, cancelable: true });
              const hash = new HashChangeEvent('hashchange', { oldURL: 'a#1', newURL: 'a#2' });
              const pop = new PopStateEvent('popstate', { state: { n: 4 } });
              const transition = new PageTransitionEvent('pageshow', { persisted: true });
              const before = new BeforeUnloadEvent('beforeunload');
              before.returnValue = 'stay';
              const input = new InputEvent('input', { data: 'x', inputType: 'insertText', isComposing: true });
              return [
                submit.submitter === button, submit.cancelable,
                new SubmitEvent('submit').submitter,
                hash.oldURL, hash.newURL,
                pop.state.n, pop.hasUAVisualTransition,
                transition.persisted, new PageTransitionEvent('pagehide').persisted,
                before.returnValue,
                input.data, input.inputType, input.isComposing, input.dataTransfer, input.getTargetRanges().length,
                new CompositionEvent('compositionstart', { data: 'ab' }).data,
                new FocusEvent('focus', { relatedTarget: button }).relatedTarget === button,
                new FocusEvent('focus').relatedTarget
              ].join(',');
            })()
            """)).Should().Be("true,true,,a#1,a#2,4,false,true,false,stay,x,insertText,true,,0,ab,true,");
    }

    /// <summary>
    /// <c>formData</c> has no default, so <c>FormDataEvent</c> is the one interface here whose init dictionary
    /// is required — https://html.spec.whatwg.org/multipage/form-events.html#formdataeventinit.
    /// </summary>
    [Test]
    public async Task AFormDataEventRequiresItsFormData()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const data = new FormData();
              const ok = new FormDataEvent('formdata', { formData: data });
              let message = '';
              try { new FormDataEvent('formdata'); } catch (e) { message = e.constructor.name; }
              return [ok.formData === data, message, FormDataEvent.length].join(',');
            })()
            """)).Should().Be("true,TypeError,2");
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-dispatch step 6.4: activation behaviour runs only for a
    /// <c>MouseEvent</c> named <c>click</c>. A plain <c>Event</c> of the same name activates nothing, which is
    /// the difference a page can observe.
    /// </summary>
    [Test]
    public async Task OnlyAMouseEventNamedClickIsAnActivationEvent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='c' type='checkbox'><input id='d' type='checkbox'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const c = document.getElementById('c');
              const d = document.getElementById('d');
              c.dispatchEvent(new Event('click', { bubbles: true, cancelable: true }));
              d.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
              return [c.checked, d.checked].join(',');
            })()
            """)).Should().Be("false,true");
    }

    [Test]
    public async Task AConstructedEventIsNotTrustedAndAnInterfaceObjectRefusesACallWithoutNew()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let message = '';
              try { MouseEvent('click'); } catch (e) { message = e.constructor.name; }
              let missing = '';
              try { new MouseEvent(); } catch (e) { missing = e.constructor.name; }
              return [new MouseEvent('click').isTrusted, message, missing, MouseEvent.length, MouseEvent.name].join(',');
            })()
            """)).Should().Be("false,TypeError,TypeError,1,MouseEvent");
    }

    /// <summary>
    /// <c>DragEvent</c> is a stated v1 non-goal: drag and drop has no <c>DataTransfer</c> behind it, so an
    /// interface object would be a constructor for an event nothing can mean. Feature detection has to be
    /// honest about it.
    /// </summary>
    [Test]
    public async Task DragEventIsAbsentSoFeatureDetectionIsHonest()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            "[typeof DragEvent, typeof ClipboardEvent, typeof MouseEvent].join(',')"))
            .Should().Be("undefined,undefined,function");
    }
}
