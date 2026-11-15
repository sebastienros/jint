#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Pins six <c>Jint.Browser</c> conversions to WebIDL's sequence conversion (https://webidl.spec.whatwg.org/#es-sequence)
/// and, for <c>threshold</c>, its union conversion (https://webidl.spec.whatwg.org/#es-union)
/// (sebastienros/jint#4492).
/// <para>
/// <c>MutationObserver.observe</c>'s <c>attributeFilter</c> and <c>IntersectionObserver</c>'s <c>threshold</c> read
/// <c>length</c> and the indices, as if the value were array-like, so a <c>Set</c> converted to nothing, an array-like
/// object converted to its indices and <c>attributeFilter: null</c> counted as absent. Both now read <c>@@iterator</c>
/// once with <a href="https://tc39.es/ecma262/#sec-getmethod">GetMethod</a> and create the sequence from the method
/// that read found (https://webidl.spec.whatwg.org/#create-sequence-from-iterable). <c>threshold</c> is a
/// <c>(double or sequence&lt;double&gt;)</c>: an object with no <c>@@iterator</c>, and every other value, takes the
/// <c>double</c> arm, which is restricted, so a non-finite number is a <c>TypeError</c> where it was a
/// <c>RangeError</c>. The whole dictionary is converted before the constructor's steps parse <c>rootMargin</c> and
/// check the range.
/// </para>
/// <para>
/// <c>observedAttributes</c> and <c>disabledFeatures</c> (<c>customElements.define</c>), a <c>TouchEvent</c>'s three
/// touch lists, a <c>FontFaceSetLoadEvent</c>'s <c>fontfaces</c> and the ARIA element arrays skipped the conversion's
/// step 1, "If V is not an Object, throw a TypeError", so a string primitive was iterated by code point: an empty one
/// was an empty sequence, and <c>observedAttributes</c> of <c>'abc'</c> observed <c>a</c>, <c>b</c> and <c>c</c>. None
/// of the six is a union with a string arm.
/// </para>
/// <para>
/// Every expected value is what Microsoft Edge 156 (Chromium) and Chrome for Testing 153 print for the same rows, and
/// the messages are Chromium's. Whether a conversion closes its iterator when an element fails is
/// <see cref="BrowserWebIdlSequenceCloseTests"/>; how often a union reads <c>@@iterator</c> under every getter answer is
/// <see cref="BrowserWebIdlIteratorReadTests"/>.
/// </para>
/// </summary>
public sealed class BrowserWebIdlSequenceConversionTests
{
    internal const string Harness = """
        // What a row produced: its result, 'ok' for none, or the name of what it threw.
        function outcome(f) {
            try { const r = f(); return r === undefined ? 'ok' : String(r); }
            catch (e) { return e !== null && typeof e === 'object' ? String(e.name) : String(e); }
        }

        // The message of what f threw.
        function message(f) {
            try { f(); return 'no throw'; } catch (e) { return String(e.message); }
        }

        // Runs f with String.prototype[Symbol.iterator] behind a getter counting its reads: a conversion that iterates
        // a string primitive reads it, and one that refuses the primitive first does not.
        function stringIteratorReads(f) {
            const original = String.prototype[Symbol.iterator];
            let reads = 0;
            Object.defineProperty(String.prototype, Symbol.iterator, { configurable: true, get() { reads++; return original; } });
            try {
                return outcome(f) + ' stringIteratorReads=' + reads;
            } finally {
                Object.defineProperty(String.prototype, Symbol.iterator, { configurable: true, writable: true, value: original });
            }
        }

        // An object whose Symbol.iterator is a getter answering `method` and counting its reads in `state.reads`.
        function counted(method, own) {
            const state = { reads: 0 };
            const value = Object.assign({}, own);
            Object.defineProperty(value, Symbol.iterator, { get() { state.reads++; return method; } });
            return [value, state];
        }

        // The attributes a MutationObserver observing with `options` records when a and b are set.
        function attributeRecords(options) {
            const element = document.createElement('div');
            const observer = new MutationObserver(() => { });
            observer.observe(element, options);
            element.setAttribute('a', '1');
            element.setAttribute('b', '1');
            return '[' + observer.takeRecords().map(r => r.attributeName).join() + ']';
        }

        // The thresholds an IntersectionObserver constructed with `options` reflects.
        function thresholds(options) {
            return new IntersectionObserver(() => { }, options).thresholds.join();
        }

        // Defines a fresh custom element whose constructor answers `statics`, and says whether it was defined.
        let definitions = 0;
        function define(statics) {
            const name = 'x-sequence-' + definitions++;
            const result = outcome(() => customElements.define(name, class extends HTMLElement {
                static get observedAttributes() { return statics.observedAttributes; }
                static get disabledFeatures() { return statics.disabledFeatures; }
                attributeChangedCallback() { }
            }));
            return result + ' defined=' + (customElements.get(name) !== undefined);
        }

        function touchEvent(init) { new TouchEvent('touchstart', init); }

        function fontFaceSetLoadEvent(init) { return new FontFaceSetLoadEvent('loading', init).fontfaces.length; }

        // Sets ariaControlsElements on a fresh element, and answers what was thrown and the content attribute after.
        function ariaControls(value) {
            const element = document.createElement('div');
            return outcome(() => { element.ariaControlsElements = value; }) + ' attr=' + JSON.stringify(element.getAttribute('aria-controls'));
        }

        const rows = {
            // MutationObserverInit's attributeFilter, a sequence<DOMString>.
            attributeFilterSet: () => attributeRecords({ attributeFilter: new Set(['a']) }),
            attributeFilterArray: () => attributeRecords({ attributeFilter: ['a'] }),
            attributeFilterUndefined: () => attributeRecords({ attributes: true, attributeFilter: undefined }),
            attributeFilterNull: () => attributeRecords({ attributes: true, attributeFilter: null }),
            attributeFilterNullWithChildList: () => attributeRecords({ childList: true, attributeFilter: null }),
            attributeFilterNullAlone: () => attributeRecords({ attributeFilter: null }),
            attributeFilterString: () => stringIteratorReads(() => attributeRecords({ attributeFilter: 'ab' })),
            attributeFilterEmptyString: () => stringIteratorReads(() => attributeRecords({ attributeFilter: '' })),
            attributeFilterNumber: () => attributeRecords({ attributeFilter: 1 }),
            attributeFilterArrayLike: () => attributeRecords({ attributeFilter: { length: 1, 0: 'a' } }),
            attributeFilterNotIterable: () => attributeRecords({ attributeFilter: {} }),
            attributeFilterReads: () => {
                const [value, state] = counted(function* () { yield 'a'; });
                return attributeRecords({ attributeFilter: value }) + ' reads=' + state.reads;
            },
            attributeFilterConvertedBeforeObserveRefuses: () => {
                const [value, state] = counted(function* () { yield 'a'; });
                return outcome(() => attributeRecords({ attributes: false, attributeFilter: value })) + ' reads=' + state.reads;
            },
            attributeFilterStringMessage: () => message(() => attributeRecords({ attributeFilter: 'ab' })),
            attributeFilterNotIterableMessage: () => message(() => attributeRecords({ attributeFilter: {} })),

            // IntersectionObserverInit's threshold, a (double or sequence<double>).
            thresholdSet: () => thresholds({ threshold: new Set([0.5]) }),
            thresholdSetSorted: () => thresholds({ threshold: new Set([1, 0.5, 0]) }),
            thresholdValueOf: () => thresholds({ threshold: { valueOf() { return 0.5; } } }),
            thresholdIteratorUndefined: () => thresholds({ threshold: { [Symbol.iterator]: undefined, valueOf() { return 0.25; } } }),
            thresholdIteratorNull: () => thresholds({ threshold: { [Symbol.iterator]: null, valueOf() { return 0.25; } } }),
            thresholdIteratorNotCallable: () => thresholds({ threshold: { [Symbol.iterator]: 1, valueOf() { return 0.25; } } }),
            thresholdArrayLike: () => thresholds({ threshold: { length: 1, 0: 0.5 } }),
            thresholdNotIterable: () => thresholds({ threshold: {} }),
            thresholdString: () => thresholds({ threshold: '0.5' }),
            thresholdNull: () => thresholds({ threshold: null }),
            thresholdBoolean: () => thresholds({ threshold: true }),
            thresholdNaN: () => thresholds({ threshold: NaN }),
            thresholdInfinity: () => thresholds({ threshold: Infinity }),
            thresholdSequenceNaN: () => thresholds({ threshold: [NaN] }),
            thresholdOutOfRange: () => thresholds({ threshold: 2 }),
            thresholdSequenceOutOfRange: () => thresholds({ threshold: [2] }),
            thresholdSequenceOutOfRangeThenNaN: () => thresholds({ threshold: [2, NaN] }),
            thresholdEmptySequence: () => thresholds({ threshold: [] }),
            thresholdReads: () => {
                const [value, state] = counted(function* () { yield 0.5; }, { valueOf() { return 0.25; } });
                return thresholds({ threshold: value }) + ' reads=' + state.reads;
            },
            thresholdConvertedBeforeRootMarginIsParsed: () => {
                const [value, state] = counted(function* () { yield 0.5; });
                return outcome(() => thresholds({ rootMargin: 'bogus', threshold: value })) + ' reads=' + state.reads;
            },
            thresholdNaNBeforeRootMarginIsParsed: () => thresholds({ rootMargin: 'bogus', threshold: NaN }),
            rootMarginParsedBeforeTheRangeCheck: () => thresholds({ rootMargin: 'bogus', threshold: 2 }),
            rootConvertedBeforeThreshold: () => {
                const [value, state] = counted(function* () { yield 0.5; });
                return outcome(() => thresholds({ root: 1, threshold: value })) + ' reads=' + state.reads;
            },
            thresholdNaNMessage: () => message(() => thresholds({ threshold: NaN })),

            // customElements.define's observedAttributes and disabledFeatures, each a sequence<DOMString>.
            observedAttributesString: () => stringIteratorReads(() => define({ observedAttributes: 'abc' })),
            observedAttributesEmptyString: () => stringIteratorReads(() => define({ observedAttributes: '' })),
            disabledFeaturesString: () => stringIteratorReads(() => define({ disabledFeatures: 'shadow' })),
            observedAttributesSet: () => define({ observedAttributes: new Set(['a']) }),
            observedAttributesNotIterable: () => define({ observedAttributes: {} }),
            observedAttributesNull: () => define({ observedAttributes: null }),
            observedAttributesNumber: () => define({ observedAttributes: 1 }),
            observedAttributesReads: () => {
                const [value, state] = counted(function* () { yield 'a'; });
                return define({ observedAttributes: value }) + ' reads=' + state.reads;
            },
            observedAttributesStringObservesNothing: () => {
                const name = 'x-sequence-' + definitions++;
                const log = [];
                return outcome(() => {
                    customElements.define(name, class extends HTMLElement {
                        static get observedAttributes() { return 'ab'; }
                        attributeChangedCallback(attribute) { log.push(attribute); }
                    });
                    const element = document.createElement(name);
                    element.setAttribute('a', '1');
                    element.setAttribute('b', '1');
                    return '[' + log.join() + ']';
                });
            },
            observedAttributesStringMessage: () => message(() => customElements.define('x-sequence-' + definitions++, class extends HTMLElement {
                static get observedAttributes() { return 'abc'; }
                attributeChangedCallback() { }
            })),
            observedAttributesNotIterableMessage: () => message(() => customElements.define('x-sequence-' + definitions++, class extends HTMLElement {
                static get observedAttributes() { return {}; }
                attributeChangedCallback() { }
            })),

            // TouchEventInit's touches, targetTouches and changedTouches, each a sequence<Touch>.
            touchesString: () => stringIteratorReads(() => touchEvent({ touches: 'x' })),
            touchesEmptyString: () => stringIteratorReads(() => touchEvent({ touches: '' })),
            targetTouchesEmptyString: () => stringIteratorReads(() => touchEvent({ targetTouches: '' })),
            changedTouchesEmptyString: () => stringIteratorReads(() => touchEvent({ changedTouches: '' })),
            touchesNull: () => touchEvent({ touches: null }),
            touchesNotIterable: () => touchEvent({ touches: {} }),
            touchesSet: () => new TouchEvent('touchstart', { touches: new Set([new Touch({ identifier: 1, target: document.body })]) }).touches.length,
            touchesStringMessage: () => message(() => touchEvent({ touches: '' })),

            // FontFaceSetLoadEventInit's fontfaces, a sequence<FontFace>.
            fontFacesString: () => stringIteratorReads(() => fontFaceSetLoadEvent({ fontfaces: 'x' })),
            fontFacesEmptyString: () => stringIteratorReads(() => fontFaceSetLoadEvent({ fontfaces: '' })),
            fontFacesNull: () => fontFaceSetLoadEvent({ fontfaces: null }),
            fontFacesNotIterable: () => fontFaceSetLoadEvent({ fontfaces: {} }),
            fontFacesSet: () => fontFaceSetLoadEvent({ fontfaces: new Set([new FontFace('f', 'url(f)')]) }),
            fontFacesStringMessage: () => message(() => fontFaceSetLoadEvent({ fontfaces: '' })),

            // ARIAMixin's element arrays, each a FrozenArray<Element>?.
            ariaElementsString: () => stringIteratorReads(() => ariaControls('x')),
            ariaElementsEmptyString: () => stringIteratorReads(() => ariaControls('')),
            ariaElementsNull: () => ariaControls(null),
            ariaElementsNotIterable: () => ariaControls({}),
            ariaElementsSet: () => ariaControls(new Set([document.createElement('span')])),
            ariaElementsStringMessage: () => message(() => { document.createElement('div').ariaControlsElements = ''; }),
        };

        function row(name) { return outcome(rows[name]); }
        """;

    private const string NotASequence = "The provided value cannot be converted to a sequence.";
    private const string NoIterator = "The object must have a callable @@iterator property.";
    private const string MutationObserverInit = "Failed to execute 'observe' on 'MutationObserver': Failed to read the 'attributeFilter' property from 'MutationObserverInit': ";
    private const string Define = "Failed to execute 'define' on 'CustomElementRegistry': ";

    // Each row with what Edge 156 and Chrome for Testing 153 answer for it.
    private static readonly Dictionary<string, string> Rows = new()
    {
        ["attributeFilterSet"] = "[a]",
        ["attributeFilterArray"] = "[a]",
        ["attributeFilterUndefined"] = "[a,b]",
        ["attributeFilterNull"] = "TypeError",
        ["attributeFilterNullWithChildList"] = "TypeError",
        ["attributeFilterNullAlone"] = "TypeError",
        ["attributeFilterString"] = "TypeError stringIteratorReads=0",
        ["attributeFilterEmptyString"] = "TypeError stringIteratorReads=0",
        ["attributeFilterNumber"] = "TypeError",
        ["attributeFilterArrayLike"] = "TypeError",
        ["attributeFilterNotIterable"] = "TypeError",
        ["attributeFilterReads"] = "[a] reads=1",
        ["attributeFilterConvertedBeforeObserveRefuses"] = "TypeError reads=1",
        ["attributeFilterStringMessage"] = MutationObserverInit + NotASequence,
        ["attributeFilterNotIterableMessage"] = MutationObserverInit + NoIterator,

        ["thresholdSet"] = "0.5",
        ["thresholdSetSorted"] = "0,0.5,1",
        ["thresholdValueOf"] = "0.5",
        ["thresholdIteratorUndefined"] = "0.25",
        ["thresholdIteratorNull"] = "0.25",
        ["thresholdIteratorNotCallable"] = "TypeError",
        ["thresholdArrayLike"] = "TypeError",
        ["thresholdNotIterable"] = "TypeError",
        ["thresholdString"] = "0.5",
        ["thresholdNull"] = "0",
        ["thresholdBoolean"] = "1",
        ["thresholdNaN"] = "TypeError",
        ["thresholdInfinity"] = "TypeError",
        ["thresholdSequenceNaN"] = "TypeError",
        ["thresholdOutOfRange"] = "RangeError",
        ["thresholdSequenceOutOfRange"] = "RangeError",
        ["thresholdSequenceOutOfRangeThenNaN"] = "TypeError",
        ["thresholdEmptySequence"] = "0",
        ["thresholdReads"] = "0.5 reads=1",
        ["thresholdConvertedBeforeRootMarginIsParsed"] = "SyntaxError reads=1",
        ["thresholdNaNBeforeRootMarginIsParsed"] = "TypeError",
        ["rootMarginParsedBeforeTheRangeCheck"] = "SyntaxError",
        ["rootConvertedBeforeThreshold"] = "TypeError reads=0",
        ["thresholdNaNMessage"] = "Failed to construct 'IntersectionObserver': Failed to read the 'threshold' property from 'IntersectionObserverInit': The provided double value is non-finite.",

        ["observedAttributesString"] = "TypeError defined=false stringIteratorReads=0",
        ["observedAttributesEmptyString"] = "TypeError defined=false stringIteratorReads=0",
        ["disabledFeaturesString"] = "TypeError defined=false stringIteratorReads=0",
        ["observedAttributesSet"] = "ok defined=true",
        ["observedAttributesNotIterable"] = "TypeError defined=false",
        ["observedAttributesNull"] = "TypeError defined=false",
        ["observedAttributesNumber"] = "TypeError defined=false",
        ["observedAttributesReads"] = "ok defined=true reads=1",
        ["observedAttributesStringObservesNothing"] = "TypeError",
        ["observedAttributesStringMessage"] = Define + NotASequence,
        ["observedAttributesNotIterableMessage"] = Define + NoIterator,

        ["touchesString"] = "TypeError stringIteratorReads=0",
        ["touchesEmptyString"] = "TypeError stringIteratorReads=0",
        ["targetTouchesEmptyString"] = "TypeError stringIteratorReads=0",
        ["changedTouchesEmptyString"] = "TypeError stringIteratorReads=0",
        ["touchesNull"] = "TypeError",
        ["touchesNotIterable"] = "TypeError",
        ["touchesSet"] = "1",
        ["touchesStringMessage"] = "Failed to construct 'TouchEvent': Failed to read the 'touches' property from 'TouchEventInit': " + NotASequence,

        ["fontFacesString"] = "TypeError stringIteratorReads=0",
        ["fontFacesEmptyString"] = "TypeError stringIteratorReads=0",
        ["fontFacesNull"] = "TypeError",
        ["fontFacesNotIterable"] = "TypeError",
        ["fontFacesSet"] = "1",
        ["fontFacesStringMessage"] = "Failed to construct 'FontFaceSetLoadEvent': Failed to read the 'fontfaces' property from 'FontFaceSetLoadEventInit': " + NotASequence,

        ["ariaElementsString"] = "TypeError attr=null stringIteratorReads=0",
        ["ariaElementsEmptyString"] = "TypeError attr=null stringIteratorReads=0",
        ["ariaElementsNull"] = "ok attr=null",
        ["ariaElementsNotIterable"] = "TypeError attr=null",
        ["ariaElementsSet"] = "ok attr=\"\"",
        ["ariaElementsStringMessage"] = "Failed to set the 'ariaControlsElements' property on 'Element': " + NotASequence,
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

    /// <summary>Each row answers what Chromium answers.</summary>
    [TestCaseSource(nameof(EveryRow))]
    public async Task EveryRowConvertsAsWebIdlDoes(string row, string expected)
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
