namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// UI Events' legacy <c>TextEvent</c>, HTML's <c>ToggleEvent</c> and <c>CommandEvent</c>, CSS's
/// <c>AnimationEvent</c> and <c>TransitionEvent</c>, and the Gamepad API's <c>GamepadEvent</c>: each built from
/// its own dictionary, and the toggle one fired by <c>&lt;details&gt;</c> as well as by <c>&lt;dialog&gt;</c>.
/// </summary>
public sealed class HtmlAndCssEventInterfaceTests
{
    [Test]
    public async Task EveryInterfaceIsAGlobalWithTheChainItsIdlDeclares()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            [
              Object.getPrototypeOf(TextEvent.prototype) === UIEvent.prototype,
              Object.getPrototypeOf(ToggleEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(CommandEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(AnimationEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(TransitionEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(GamepadEvent.prototype) === Event.prototype,
              TextEvent.length, ToggleEvent.length, GamepadEvent.length,
              Object.getOwnPropertyDescriptor(ToggleEvent.prototype, 'newState').enumerable,
              typeof Gamepad
            ].join(',')
            """)).Should().Be("true,true,true,true,true,true,0,1,2,true,function");
    }

    [Test]
    public async Task TextEventHasNoConstructorButTheLegacyFactoryBuildsOne()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let refused = '';
              try { new TextEvent('textInput'); } catch (e) { refused = e.name; }
              const e = document.createEvent('TextEvent');
              const before = [Object.getPrototypeOf(e) === TextEvent.prototype, e.type === '', e.data === ''].join('|');
              e.initTextEvent('textInput', true, true, window, 'hi');
              const d = document.createEvent('textevent');
              d.initTextEvent('textInput');
              return [refused, before, e.type, e.bubbles, e.cancelable, e.view === window, e.data, d.data].join(',');
            })()
            """)).Should().Be("TypeError,true|true|true,textInput,true,true,true,hi,undefined");
    }

    [Test]
    public async Task ToggleAndCommandEventsReadTheirDictionaries()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<button id=b>b</button>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const b = document.getElementById('b');
              const t = new ToggleEvent('beforetoggle', { oldState: 'closed', newState: 'open', source: b, cancelable: true });
              const bare = new ToggleEvent('toggle');
              const c = new CommandEvent('command', { command: '--custom', source: b });
              let refused = '';
              try { new ToggleEvent('toggle', { source: {} }); } catch (e) { refused = e.name; }
              return [
                t.oldState, t.newState, t.source === b, t.cancelable,
                JSON.stringify([bare.oldState, bare.newState, bare.source]),
                c.command, c.source === b, new CommandEvent('command').source,
                refused
              ].join(',');
            })()
            """)).Should().Be("closed,open,true,true,[\"\",\"\",null],--custom,true,,TypeError");
    }

    [Test]
    public async Task ASourceInsideAShadowTreeIsRetargetedToItsHost()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const host = document.getElementById('host');
              const inner = host.attachShadow({ mode: 'open' }).appendChild(document.createElement('button'));
              const e = new CommandEvent('command', { source: inner });
              let heard = null;
              document.body.addEventListener('command', ev => heard = ev.source === host);
              document.body.dispatchEvent(e);
              return [e.source === host, heard].join(',');
            })()
            """)).Should().Be("true,true");
    }

    [Test]
    public async Task AnimationAndTransitionEventsReadTheirDictionaries()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const a = new AnimationEvent('animationend', { animationName: 'spin', elapsedTime: 1.5, pseudoElement: '::before' });
              const t = new TransitionEvent('transitionend', { propertyName: 'opacity', elapsedTime: 0.25 });
              let refused = '';
              try { new AnimationEvent('animationend', { elapsedTime: NaN }); } catch (e) { refused = e.name; }
              const bare = new TransitionEvent('transitionrun');
              return [
                a.animationName, a.elapsedTime, a.pseudoElement,
                t.propertyName, t.elapsedTime, JSON.stringify(t.pseudoElement),
                JSON.stringify(bare.propertyName), bare.elapsedTime, refused,
                Object.prototype.toString.call(a), Object.prototype.toString.call(t)
              ].join(',');
            })()
            """)).Should().Be("spin,1.5,::before,opacity,0.25,\"\",\"\",0,TypeError,[object AnimationEvent],[object TransitionEvent]");
    }

    [Test]
    public async Task AGamepadEventNeedsAGamepadNobodyCanMake()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const out = [];
              for (const make of [
                () => new GamepadEvent('gamepadconnected'),
                () => new GamepadEvent('gamepadconnected', {}),
                () => new GamepadEvent('gamepadconnected', { gamepad: {} }),
                () => new Gamepad(),
              ]) {
                try { make(); out.push('built'); } catch (e) { out.push(e.name); }
              }
              return out.join(',');
            })()
            """)).Should().Be("TypeError,TypeError,TypeError,TypeError");
    }

    [Test]
    public async Task DetailsToggleIsAToggleEventCarryingTheEarliestOldState()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<details id=d><summary>s</summary>body</details>");

        (await page.EvaluateAndAwaitAsync<string>(
            """
            new Promise(resolve => {
              const d = document.getElementById('d');
              const seen = [];
              d.addEventListener('toggle', e => {
                seen.push([e instanceof ToggleEvent, e.isTrusted, e.oldState, e.newState, e.cancelable].join('|'));
                if (seen.length === 1) {
                  d.open = false;
                  d.open = true;
                  d.open = false;
                } else {
                  resolve(seen.join(';'));
                }
              });
              d.open = true;
            })
            """)).Should().Be("true|true|closed|open|false;true|true|open|closed|false");
    }

    [Test]
    public async Task DialogToggleIsStillAToggleEvent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<dialog id=d>x</dialog>");

        (await page.EvaluateAndAwaitAsync<string>(
            """
            new Promise(resolve => {
              const d = document.getElementById('d');
              const seen = [];
              d.addEventListener('beforetoggle', e => seen.push(['before', e instanceof ToggleEvent, e.oldState, e.newState].join('|')));
              d.addEventListener('toggle', e => {
                seen.push(['toggle', e instanceof ToggleEvent, e.oldState, e.newState].join('|'));
                resolve(seen.join(';'));
              });
              d.show();
            })
            """)).Should().Be("before|true|closed|open;toggle|true|closed|open");
    }
}
