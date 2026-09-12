using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The five event interfaces <c>document.createEvent</c>'s alias table names that this browser used to refuse
/// — <c>DragEvent</c>, <c>StorageEvent</c>, <c>TouchEvent</c> and the two device events — plus the four
/// non-<c>Event</c> interfaces two of them carry.
/// </summary>
/// <remarks>
/// Every one of them is constructible and dispatchable and none of them is ever <i>fired</i> by the runtime:
/// there is no drag, no second document sharing a storage area, no touch input and no sensor. What is under
/// test is therefore exactly what a page can do — build one from its dictionary, read every member back, and
/// reach it through the legacy factory — and each assertion is the standard's construction steps rather than
/// a value some hardware would have supplied.
/// </remarks>
public sealed class EventInterfaceCoverageTests
{
    [Test]
    public async Task EveryNewInterfaceIsAGlobalWithTheChainItsIdlDeclares()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            [
              Object.getPrototypeOf(DragEvent.prototype) === MouseEvent.prototype,
              Object.getPrototypeOf(DragEvent) === MouseEvent,
              Object.getPrototypeOf(TouchEvent.prototype) === UIEvent.prototype,
              Object.getPrototypeOf(TouchEvent) === UIEvent,
              Object.getPrototypeOf(StorageEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(DeviceMotionEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(DeviceOrientationEvent.prototype) === Event.prototype,
              Object.getPrototypeOf(Touch.prototype) === Object.prototype,
              Object.getPrototypeOf(TouchList.prototype) === Object.prototype,
              Object.getPrototypeOf(DeviceMotionEventAcceleration.prototype) === Object.prototype,
              Object.getPrototypeOf(DeviceMotionEventRotationRate.prototype) === Object.prototype,
              new DragEvent('drop') instanceof MouseEvent,
              new TouchEvent('touchstart') instanceof UIEvent,
              new StorageEvent('storage') instanceof Event,
              DragEvent.prototype.constructor === DragEvent,
              TouchList.prototype.constructor === TouchList
            ].join(',')
            """)).Should().Be(string.Join(',', Enumerable.Repeat("true", 16)));
    }

    [Test]
    public async Task TheToStringTagIsTheInterfaceName()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const t = new Touch({ identifier: 1, target: document.body });
              const e = new TouchEvent('touchstart', { changedTouches: [t] });
              const m = new DeviceMotionEvent('devicemotion', { acceleration: { x: 1 }, rotationRate: {} });
              return [
                new DragEvent('drop'), new StorageEvent('storage'), e,
                m, new DeviceOrientationEvent('deviceorientation'),
                t, e.changedTouches, m.acceleration, m.rotationRate
              ].map(v => Object.prototype.toString.call(v)).join(',');
            })()
            """)).Should().Be(
            "[object DragEvent],[object StorageEvent],[object TouchEvent],"
            + "[object DeviceMotionEvent],[object DeviceOrientationEvent],"
            + "[object Touch],[object TouchList],[object DeviceMotionEventAcceleration],"
            + "[object DeviceMotionEventRotationRate]");
    }

    /// <summary>
    /// DOM §4.5's alias table, matched ASCII-case-insensitively, for the five rows that used to answer
    /// <c>NotSupportedError</c>. The event a legacy factory makes has the empty type and its initialized flag
    /// unset, which is the whole reason the member exists.
    /// </summary>
    [Test]
    public async Task CreateEventBuildsEveryAliasTheTableNames()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const names = ['DragEvent', 'StorageEvent', 'TouchEvent', 'DeviceMotionEvent', 'DeviceOrientationEvent'];
              const out = [];
              for (const name of names) {
                for (const spelling of [name, name.toLowerCase(), name.toUpperCase()]) {
                  const e = document.createEvent(spelling);
                  out.push(Object.getPrototypeOf(e) === window[name].prototype);
                  out.push(e.type === '' && e.bubbles === false && e.cancelable === false && e.isTrusted === false);
                }
              }
              return out.join(',');
            })()
            """)).Should().Be(string.Join(',', Enumerable.Repeat("true", 30)));
    }

    [Test]
    public async Task ACreatedEventIsUndispatchableUntilInitEventNamesIt()
    {
        // A real origin, because initStorageEvent takes a Storage and an opaque origin has none.
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<title>storage</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              const e = document.createEvent('StorageEvent');
              let refused = '';
              try { document.body.dispatchEvent(e); } catch (err) { refused = err.name; }
              e.initStorageEvent('storage', true, false, 'k', 'old', 'new', 'https://example.test/', localStorage);
              let heard = '';
              document.body.addEventListener('storage', ev => {
                heard = [ev.type, ev.bubbles, ev.key, ev.oldValue, ev.newValue, ev.url, ev.storageArea === localStorage].join('|');
              });
              document.body.dispatchEvent(e);
              return refused + ';' + heard;
            })()
            """)).Should().Be("InvalidStateError;storage|true|k|old|new|https://example.test/|true");
    }

    /// <summary>
    /// <c>StorageEventInit</c>'s three nullable strings, its <c>USVString</c> and its <c>Storage?</c>: an
    /// absent member is <c>null</c> rather than the empty string, which is what tells "one key changed" from
    /// "the area was cleared".
    /// </summary>
    [Test]
    public async Task AStorageEventReadsBackEveryMemberOfItsInit()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<title>storage</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              const bare = new StorageEvent('storage');
              const full = new StorageEvent('storage', {
                bubbles: true, key: 'k', oldValue: 'o', newValue: null,
                url: 'https://example.test/x', storageArea: sessionStorage
              });
              let refused = '';
              try { new StorageEvent('storage', { storageArea: {} }); } catch (e) { refused = e.constructor.name; }
              return [
                bare.key, bare.oldValue, bare.newValue, JSON.stringify(bare.url), bare.storageArea,
                full.key, full.oldValue, full.newValue, full.url, full.storageArea === sessionStorage,
                refused
              ].map(String).join(',');
            })()
            """)).Should().Be("null,null,null,\"\",null,k,o,null,https://example.test/x,true,TypeError");
    }

    /// <summary>
    /// <c>DragEventInit</c>'s <c>DataTransfer?</c>. The drag data store is the one this package already builds
    /// for file inputs, so what a page puts in is what a listener reads out.
    /// </summary>
    [Test]
    public async Task ADragEventCarriesTheDataTransferItWasGiven()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='drop'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const dt = new DataTransfer();
              dt.setData('text/plain', 'payload');
              const e = new DragEvent('drop', { bubbles: true, cancelable: true, clientX: 7, dataTransfer: dt });
              let heard = '';
              document.getElementById('drop').addEventListener('drop', ev => {
                heard = [ev.dataTransfer === dt, ev.dataTransfer.getData('text/plain'), ev.clientX, ev.type].join('|');
              });
              document.getElementById('drop').dispatchEvent(e);
              let refused = '';
              try { new DragEvent('drop', { dataTransfer: {} }); } catch (err) { refused = err.constructor.name; }
              return heard + ';' + new DragEvent('drop').dataTransfer + ';' + refused;
            })()
            """)).Should().Be("true|payload|7|drop;null;TypeError");
    }

    /// <summary>
    /// Touch Events' <c>TouchInit</c>: <c>identifier</c> and <c>target</c> are required members, and the rest
    /// default to zero. Nothing measures any of it — there is no touch input here — so every value is the
    /// dictionary's.
    /// </summary>
    [Test]
    public async Task ATouchReadsBackEveryMemberOfItsRequiredInit()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='a'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const target = document.getElementById('a');
              const t = new Touch({
                identifier: 4, target,
                screenX: 1, screenY: 2, clientX: 3, clientY: 4, pageX: 5, pageY: 6,
                radiusX: 7, radiusY: 8, rotationAngle: 9, force: 0.5
              });
              const bare = new Touch({ identifier: 0, target });
              const refusals = [];
              for (const bad of [undefined, { target }, { identifier: 1 }, { identifier: 1, target: 5 }]) {
                try { new Touch(bad); refusals.push('none'); } catch (e) { refusals.push(e.constructor.name); }
              }
              return [
                t.identifier, t.target === target,
                t.screenX, t.screenY, t.clientX, t.clientY, t.pageX, t.pageY,
                t.radiusX, t.radiusY, t.rotationAngle, t.force,
                bare.pageX, bare.force,
                refusals.join('/')
              ].join(',');
            })()
            """)).Should().Be("4,true,1,2,3,4,5,6,7,8,9,0.5,0,0,TypeError/TypeError/TypeError/TypeError");
    }

    /// <summary>
    /// <c>TouchEventInit</c>'s three <c>sequence&lt;Touch&gt;</c> members and the <c>TouchList</c> they become:
    /// a length on the prototype as WebIDL requires, an <c>item()</c> that answers <c>null</c> past the end,
    /// and the <c>@@iterator</c> an interface supporting indexed properties carries.
    /// </summary>
    [Test]
    public async Task ATouchEventCarriesTheThreeListsAndTheModifiers()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='a'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const target = document.getElementById('a');
              const one = new Touch({ identifier: 1, target, clientX: 10 });
              const two = new Touch({ identifier: 2, target, clientX: 20 });
              const e = new TouchEvent('touchmove', {
                bubbles: true, touches: [one, two], targetTouches: [one], changedTouches: new Set([two]),
                ctrlKey: true, shiftKey: true
              });
              const empty = new TouchEvent('touchend');
              let refused = '';
              try { new TouchEvent('touchstart', { touches: [{}] }); } catch (err) { refused = err.constructor.name; }
              return [
                e.touches.length, e.touches[0] === one, e.touches[1] === two, e.touches[2],
                e.touches.item(0) === one, e.touches.item(5),
                e.targetTouches.length, e.changedTouches.length, e.changedTouches[0] === two,
                [...e.touches].map(t => t.clientX).join('+'),
                Object.getOwnPropertyDescriptor(TouchList.prototype, 'length') !== undefined,
                e.ctrlKey, e.shiftKey, e.altKey, e.metaKey,
                empty.touches.length, empty.type,
                refused
              ].map(String).join(',');
            })()
            """)).Should().Be(
            "2,true,true,undefined,true,null,1,1,true,10+20,true,true,true,false,false,0,touchend,TypeError");
    }

    /// <summary>
    /// The device events. <c>acceleration</c>, <c>accelerationIncludingGravity</c> and <c>rotationRate</c> are
    /// dictionary members with no default, so an absent one leaves the attribute null while a present one
    /// builds a real reading whose unstated axes are null — never zero.
    /// </summary>
    [Test]
    public async Task TheDeviceEventsBuildTheirReadingsFromTheDictionary()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const bare = new DeviceMotionEvent('devicemotion');
              const full = new DeviceMotionEvent('devicemotion', {
                acceleration: { x: 1, y: 2, z: 3 },
                accelerationIncludingGravity: { x: 4 },
                rotationRate: { alpha: 5, beta: 6, gamma: 7 },
                interval: 16
              });
              const o = new DeviceOrientationEvent('deviceorientation', { alpha: 1, gamma: null, absolute: true });
              const bareO = new DeviceOrientationEvent('deviceorientation');
              return [
                bare.acceleration, bare.accelerationIncludingGravity, bare.rotationRate, bare.interval,
                full.acceleration.x, full.acceleration.y, full.acceleration.z,
                full.accelerationIncludingGravity.x, full.accelerationIncludingGravity.y,
                full.rotationRate.alpha, full.rotationRate.beta, full.rotationRate.gamma, full.interval,
                o.alpha, o.beta, o.gamma, o.absolute,
                bareO.alpha, bareO.absolute
              ].map(String).join(',');
            })()
            """)).Should().Be("null,null,null,0,1,2,3,4,null,5,6,7,16,1,null,null,true,null,false");
    }

    /// <summary>
    /// The WebIDL brand check every member of a hand-written shape performs: a receiver that is not an
    /// instance of the interface is a <c>TypeError</c> rather than a value read off the wrong object.
    /// </summary>
    [Test]
    public async Task AMemberCalledOnAForeignReceiverIsATypeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const reads = [
                () => Object.getOwnPropertyDescriptor(Touch.prototype, 'identifier').get.call({}),
                () => Object.getOwnPropertyDescriptor(TouchList.prototype, 'length').get.call({}),
                () => Object.getOwnPropertyDescriptor(DragEvent.prototype, 'dataTransfer').get.call(new Event('x')),
                () => Object.getOwnPropertyDescriptor(StorageEvent.prototype, 'key').get.call(new Event('x')),
                () => TouchList.prototype.item.call({}, 0)
              ];
              return reads.map(read => { try { read(); return 'none'; } catch (e) { return e.constructor.name; } }).join(',');
            })()
            """)).Should().Be("TypeError,TypeError,TypeError,TypeError,TypeError");
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-interface-call — an interface object WebIDL gives no constructor
    /// answers <c>Illegal constructor</c>, and none of them is callable without <c>new</c>.
    /// </summary>
    [Test]
    public async Task AnInterfaceWithNoConstructorRefusesNew()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const attempts = [
                () => new TouchList(),
                () => new DeviceMotionEventAcceleration(),
                () => new DeviceMotionEventRotationRate(),
                () => Touch({ identifier: 1, target: document.body }),
                () => DragEvent('drop')
              ];
              return attempts.map(run => { try { run(); return 'none'; } catch (e) { return e.constructor.name; } }).join(',');
            })()
            """)).Should().Be("TypeError,TypeError,TypeError,TypeError,TypeError");
    }
}
