#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Pins the order in which <c>Jint.Browser</c> reads the members of the WebIDL dictionaries it converts by hand:
/// lexicographic order of the member names, inherited dictionaries first
/// (https://webidl.spec.whatwg.org/#es-dictionary step 4, sebastienros/jint#4530).
/// <para>
/// <c>MutationObserverInit</c>, <c>TouchInit</c>, <c>MessageEventInit</c>, <c>URLPatternInit</c>, <c>StaticRangeInit</c>,
/// <c>WindowPostMessageOptions</c> (whose inherited <c>transfer</c> comes first) and every UI event's dictionary were
/// read in source order, and every
/// UI event read its <c>UIEventInit</c> members before the <c>EventInit</c> ones it inherits and before converting its
/// <c>type</c> argument, so getters on an options object ran in a different order than in a browser and, when two
/// members failed, a different one threw first. <c>TouchInit</c> also read its two required members twice.
/// </para>
/// <para>
/// Each expected log is what Chrome for Testing (headless shell 1228) logs for the same row, restricted to the members
/// Jint implements: Chromium also reads <c>sourceCapabilities</c> (<c>UIEventInit</c>), <c>movementX</c>,
/// <c>movementY</c> and <c>region</c> (<c>MouseEventInit</c>), <c>altitudeAngle</c>, <c>azimuthAngle</c>,
/// <c>coalescedEvents</c>, <c>persistentDeviceId</c> and <c>predictedEvents</c> (<c>PointerEventInit</c>),
/// <c>wheelDeltaX</c> and <c>wheelDeltaY</c> (<c>WheelEventInit</c>), <c>dataTransfer</c> and <c>targetRanges</c>
/// (<c>InputEventInit</c>), <c>agentInvoked</c> (<c>SubmitEventInit</c>), <c>userActivation</c>
/// (<c>MessageEventInit</c>) and <c>includeUserActivation</c> and <c>delegate</c> (<c>WindowPostMessageOptions</c>),
/// none of which Jint has, and it does not
/// read <c>which</c> or the four modifiers it has dropped (<c>modifierFnLock</c>, <c>modifierHyper</c>,
/// <c>modifierSuper</c>, <c>modifierSymbolLock</c>), which UI Events still declares and Jint still reads. Every
/// member both read is read in the same relative order.
/// </para>
/// </summary>
public sealed class BrowserWebIdlDictionaryOrderTests
{
    internal const string Harness = """
        // The members a conversion read, in the order it read them: a Proxy over `values` logging every string-keyed Get.
        function reads(construct, values) {
            const log = [];
            const init = new Proxy(values || {}, {
                get(target, key, receiver) {
                    if (typeof key === 'string') { log.push(key); }
                    return Reflect.get(target, key, receiver);
                },
            });
            try { construct(init); } catch (e) { log.push('threw ' + (e && e.name ? e.name : String(e))); }
            return log.join();
        }

        const rows = {
            MutationObserverInit: () => reads(init => new MutationObserver(() => { }).observe(document.body, init), { childList: true }),
            MutationObserverInitFirstFailure: () => {
                try {
                    new MutationObserver(() => { }).observe(document.body, { get childList() { throw 'childList'; }, attributeFilter: null });
                    return 'no throw';
                } catch (e) { return typeof e === 'string' ? e : e.name; }
            },
            Event: () => reads(init => new Event('x', init)),
            CustomEvent: () => reads(init => new CustomEvent('x', init)),
            UIEvent: () => reads(init => new UIEvent('x', init)),
            MouseEvent: () => reads(init => new MouseEvent('x', init)),
            PointerEvent: () => reads(init => new PointerEvent('x', init)),
            WheelEvent: () => reads(init => new WheelEvent('x', init)),
            KeyboardEvent: () => reads(init => new KeyboardEvent('x', init)),
            InputEvent: () => reads(init => new InputEvent('x', init)),
            CompositionEvent: () => reads(init => new CompositionEvent('x', init)),
            FocusEvent: () => reads(init => new FocusEvent('x', init)),
            DragEvent: () => reads(init => new DragEvent('x', init)),
            TouchEvent: () => reads(init => new TouchEvent('x', init)),
            Touch: () => reads(init => new Touch(init), { identifier: 1, target: document.body }),
            TouchMissingIdentifier: () => reads(init => new Touch(init), { target: document.body }),
            SubmitEvent: () => reads(init => new SubmitEvent('x', init)),
            FormDataEvent: () => reads(init => new FormDataEvent('x', init), { formData: new FormData() }),
            HashChangeEvent: () => reads(init => new HashChangeEvent('x', init)),
            PopStateEvent: () => reads(init => new PopStateEvent('x', init)),
            PageTransitionEvent: () => reads(init => new PageTransitionEvent('x', init)),
            StorageEvent: () => reads(init => new StorageEvent('x', init)),
            DeviceMotionEvent: () => reads(init => new DeviceMotionEvent('x', init)),
            DeviceOrientationEvent: () => reads(init => new DeviceOrientationEvent('x', init)),
            ToggleEvent: () => reads(init => new ToggleEvent('x', init)),
            CommandEvent: () => reads(init => new CommandEvent('x', init)),
            AnimationEvent: () => reads(init => new AnimationEvent('x', init)),
            TransitionEvent: () => reads(init => new TransitionEvent('x', init)),
            MessageEvent: () => reads(init => new MessageEvent('x', init)),
        URLPatternInit: () => reads(init => new URLPattern(init)),
        StaticRangeInit: () => reads(init => new StaticRange(init), { startContainer: document.body, startOffset: 0, endContainer: document.body, endOffset: 0 }),
        StaticRangeMissingEnd: () => reads(init => new StaticRange(init), { startContainer: document.body, startOffset: 0 }),
        WindowPostMessageOptions: () => reads(init => window.postMessage('m', init), { targetOrigin: '*' }),
        TypeBeforeDictionary: () => {
                const log = [];
                const type = { toString() { log.push('type'); return 'x'; } };
                new MouseEvent(type, { get bubbles() { log.push('bubbles'); return false; }, get view() { log.push('view'); return null; } });
                return log.join();
            },
        };

        function row(name) { try { return rows[name](); } catch (e) { return 'harness threw ' + e; } }
        """;

    private const string EventInit = "bubbles,cancelable,composed";
    private const string Ui = EventInit + ",detail,view,which";
    private const string Modifiers = "altKey,ctrlKey,metaKey,modifierAltGraph,modifierCapsLock,modifierFn,modifierFnLock,modifierHyper,modifierNumLock,modifierScrollLock,modifierSuper,modifierSymbol,modifierSymbolLock,shiftKey";
    private const string Mouse = Ui + "," + Modifiers + ",button,buttons,clientX,clientY,relatedTarget,screenX,screenY";

    private static readonly Dictionary<string, string> Rows = new()
    {
        ["MutationObserverInit"] = "attributeFilter,attributeOldValue,attributes,characterData,characterDataOldValue,childList,subtree",
        ["MutationObserverInitFirstFailure"] = "TypeError",
        ["Event"] = EventInit,
        ["CustomEvent"] = EventInit + ",detail",
        ["UIEvent"] = Ui,
        ["MouseEvent"] = Mouse,
        ["PointerEvent"] = Mouse + ",height,isPrimary,pointerId,pointerType,pressure,tangentialPressure,tiltX,tiltY,twist,width",
        ["WheelEvent"] = Mouse + ",deltaMode,deltaX,deltaY,deltaZ",
        ["KeyboardEvent"] = Ui + "," + Modifiers + ",charCode,code,isComposing,key,keyCode,location,repeat",
        ["InputEvent"] = Ui + ",data,inputType,isComposing",
        ["CompositionEvent"] = Ui + ",data",
        ["FocusEvent"] = Ui + ",relatedTarget",
        ["DragEvent"] = Mouse + ",dataTransfer",
        ["TouchEvent"] = Ui + "," + Modifiers + ",changedTouches,targetTouches,touches",
        ["Touch"] = "clientX,clientY,force,identifier,pageX,pageY,radiusX,radiusY,rotationAngle,screenX,screenY,target",
        ["TouchMissingIdentifier"] = "clientX,clientY,force,identifier,threw TypeError",
        ["SubmitEvent"] = EventInit + ",submitter",
        ["FormDataEvent"] = EventInit + ",formData",
        ["HashChangeEvent"] = EventInit + ",newURL,oldURL",
        ["PopStateEvent"] = EventInit + ",hasUAVisualTransition,state",
        ["PageTransitionEvent"] = EventInit + ",persisted",
        ["StorageEvent"] = EventInit + ",key,newValue,oldValue,storageArea,url",
        ["DeviceMotionEvent"] = EventInit + ",acceleration,accelerationIncludingGravity,interval,rotationRate",
        ["DeviceOrientationEvent"] = EventInit + ",absolute,alpha,beta,gamma",
        ["ToggleEvent"] = EventInit + ",newState,oldState,source",
        ["CommandEvent"] = EventInit + ",command,source",
        ["AnimationEvent"] = EventInit + ",animationName,elapsedTime,pseudoElement",
        ["TransitionEvent"] = EventInit + ",elapsedTime,propertyName,pseudoElement",
        ["MessageEvent"] = EventInit + ",data,lastEventId,origin,ports,source",
        ["URLPatternInit"] = "baseURL,hash,hostname,password,pathname,port,protocol,search,username",
        ["StaticRangeInit"] = "endContainer,endOffset,startContainer,startOffset",
        ["StaticRangeMissingEnd"] = "endContainer,threw TypeError",
        ["WindowPostMessageOptions"] = "transfer,targetOrigin",
        ["TypeBeforeDictionary"] = "type,bubbles,view",
    };

    public static TestCases<string, string> EveryRow
    {
        get
        {
            var rows = new TestCases<string, string>();
            foreach (var (name, expected) in Rows)
            {
                rows.Add(name, expected);
            }

            return rows;
        }
    }

    /// <summary>Each row reads its members in WebIDL's order.</summary>
    [TestCaseSource(nameof(EveryRow))]
    public async Task EveryDictionaryIsReadInLexicographicOrder(string row, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>x</p>");
        await page.EvaluateAsync(Harness);

        (await page.EvaluateAsync<string>($"row('{row}')")).Should().Be(expected);
    }

    /// <summary>
    /// The harness names exactly the rows <see cref="Rows"/> expects, so a row added to one and not the other fails
    /// here rather than being silently skipped.
    /// </summary>
    [Test]
    public async Task TheHarnessAndTheExpectationsNameTheSameRows()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync(Harness);

        (await page.EvaluateAsync<string>("Object.keys(rows).sort().join()"))
            .Should().Be(string.Join(",", Rows.Keys.OrderBy(k => k, StringComparer.Ordinal)));
    }
}
