#nullable enable

using Jint.Native.Iterator;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins that each concrete iterator prototype's <c>next</c> steps only an iterator of its own kind
/// (sebastienros/jint#4451). <a href="https://tc39.es/ecma262/#sec-%arrayiteratorprototype%.next">%ArrayIteratorPrototype%.next</a>
/// and <a href="https://tc39.es/ecma262/#sec-%regexpstringiteratorprototype%.next">%RegExpStringIteratorPrototype%.next</a>
/// require the receiver's internal slots; <a href="https://tc39.es/ecma262/#sec-%mapiteratorprototype%.next">%MapIteratorPrototype%.next</a>,
/// <a href="https://tc39.es/ecma262/#sec-%setiteratorprototype%.next">%SetIteratorPrototype%.next</a> and
/// <a href="https://tc39.es/ecma262/#sec-%stringiteratorprototype%.next">%StringIteratorPrototype%.next</a> are GeneratorResume
/// with their own brand, which <a href="https://tc39.es/ecma262/#sec-generatorvalidate">GeneratorValidate</a> compares;
/// <a href="https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.next">%IntlSegmentIteratorPrototype%.next</a> requires
/// [[IteratingSegmenter]]; and a WebIDL iterator prototype's <c>next</c> refuses anything that is not a default iterator
/// object for its interface (https://webidl.spec.whatwg.org/#es-iterator-prototype-object). Jint's every built-in
/// <c>next</c> accepted any built-in iterator and stepped it.
/// <para>
/// The native lane steps a built-in iterator without calling <c>next</c>, which stands in for that call only while the
/// <c>next</c> it would make is the built-in one of the iterator's own kind: a kind's prototype given another kind's
/// <c>next</c>, and an iterator moved onto another kind's prototype, both have to throw where the call would.
/// </para>
/// <para>
/// Every expected value is Node 26's, for the same harness; the second realm is a <c>vm</c> context there and
/// <c>$262.createRealm()</c> here. A wrapped CLR collection's iterator has no Node counterpart: it inherits
/// %ArrayIteratorPrototype%, so it is expected to answer what an array iterator does.
/// </para>
/// </summary>
public class IteratorNextKindTests
{
    private const string Harness = """
        // Each built-in iterator kind: a fresh iterator made in the realm g, answering 1 and then 2, and an iterable a
        // consumer takes as it is, whose @@iterator hands one out.
        const kinds = {
            array: { iterator: g => g.eval('[1, 2].values()'), iterable: () => [1, 2] },
            arrayLike: { iterator: g => g.eval('Array.prototype.values.call({ length: 2, 0: 1, 1: 2 })'), iterable: () => ({ length: 2, 0: 1, 1: 2, [Symbol.iterator]: Array.prototype.values }) },
            typedArray: { iterator: g => g.eval('Uint8Array.of(1, 2).values()'), iterable: () => Uint8Array.of(1, 2) },
            map: { iterator: g => g.eval("new Map([['1', 1], ['2', 2]]).entries()"), iterable: () => new Map([['1', 1], ['2', 2]]) },
            set: { iterator: g => g.eval('new Set([1, 2]).values()'), iterable: () => new Set([1, 2]) },
            string: { iterator: g => g.eval("'12'[Symbol.iterator]()"), iterable: () => '12' },
            regExpString: { iterator: g => g.eval("'a1b2'.matchAll(/[0-9]/g)"), iterable: () => ({ [Symbol.iterator]: () => 'a1b2'.matchAll(/[0-9]/g) }) },
            segment: { iterator: g => g.eval("new Intl.Segmenter().segment('12')[Symbol.iterator]()"), iterable: () => new Intl.Segmenter().segment('12') },
        };
        if (typeof list !== 'undefined') {
            kinds.list = { iterator: g => g.eval('list[Symbol.iterator]()'), iterable: () => list };
            kinds.dictionary = { iterator: g => g.eval('dictionary[Symbol.iterator]()'), iterable: () => dictionary };
        }
        if (typeof Headers !== 'undefined') {
            kinds.headers = { iterator: g => g.eval("new Headers([['1', 'a'], ['2', 'b']]).entries()"), iterable: () => new Headers([['1', 'a'], ['2', 'b']]) };
            kinds.urlSearchParams = { iterator: g => g.eval("new URLSearchParams('1=a&2=b').entries()"), iterable: () => new URLSearchParams('1=a&2=b') };
            kinds.formData = { iterator: g => g.eval("(() => { const f = new FormData(); f.append('1', 'a'); f.append('2', 'b'); return f.entries(); })()"), iterable: () => { const f = new FormData(); f.append('1', 'a'); f.append('2', 'b'); return f; } };
        }

        // A segment data object shows its segment, a match or an entry its first element, anything else itself.
        function show(v) {
            if (typeof v !== 'object' || v === null) { return String(v); }
            return 'segment' in v ? v.segment : String(v[0]);
        }

        // The prototype of kind's iterators in the realm g.
        function protoOf(kind, g) { return Object.getPrototypeOf(kinds[kind].iterator(g || globalThis)); }

        // Every `next` a script can borrow: each kind's, and those of a generator, an iterator helper and an
        // Iterator.from wrapper.
        const nexts = {
            generator: () => Object.getPrototypeOf(Object.getPrototypeOf((function* () { })())).next,
            helper: () => Object.getPrototypeOf([].values().map(x => x)).next,
            wrap: () => Object.getPrototypeOf(Iterator.from({ next() { return { done: true }; } })).next,
        };
        for (const kind of Object.keys(kinds)) { nexts[kind] = () => protoOf(kind).next; }

        // Every receiver: each kind's iterator, an object that is not an iterator, one inheriting an array iterator, a
        // generator, an iterator helper and an Iterator.from wrapper, each answering 1 and then 2.
        const receivers = {
            plain: () => ({}),
            inheriting: () => Object.create([1, 2].values()),
            generator: () => (function* () { yield 1; yield 2; })(),
            helper: () => [1, 2].values().map(x => x),
            wrap: () => Iterator.from({ i: 0, next() { this.i++; return { value: this.i, done: this.i > 2 }; } }),
        };
        for (const kind of Object.keys(kinds)) { receivers[kind] = () => kinds[kind].iterator(globalThis); }

        // Calls nextKind's `next` on a fresh receiver, then the receiver's own `next`, and answers both: what the
        // borrowed call answered or threw, and what the receiver's next step is, which says whether the call stepped it.
        function borrow(nextKind, receiverKind) {
            const next = nexts[nextKind]();
            const receiver = receivers[receiverKind]();
            let first;
            try { const r = next.call(receiver); first = show(r.value) + '/' + r.done; } catch (e) { first = e.constructor.name; }
            const after = receiverKind === 'plain' || receiverKind === 'inheriting' ? '-' : show(receiver.next().value);
            return first + ' ' + after;
        }

        // Every consumer that builds an iterator record, sync, in a generator and in an async function side by side.
        const consumers = {
            forOf: x => { const out = []; for (const v of x) { out.push(show(v)); } return out.join(','); },
            spread: x => [...x].map(show).join(','),
            from: x => Array.from(x).map(show).join(','),
            destructure: x => { const [a, b] = x; return show(a) + ',' + show(b); },
            yieldStar: x => { const out = []; const g = (function* () { yield* x; })(); for (let s = g.next(); !s.done; s = g.next()) { out.push(show(s.value)); } return out.join(','); },
            forOfInGenerator: x => (function* () { const out = []; for (const v of x) { out.push(show(v)); } yield out.join(','); })().next().value,
            forOfInAsync: x => (async function () { await 0; const out = []; for (const v of x) { out.push(show(v)); } return out.join(','); })(),
            forAwait: x => (async function () { const out = []; for await (const v of x) { out.push(show(v)); } return out.join(','); })(),
            yieldStarInAsyncGenerator: x => (async function () { const out = []; for await (const v of (async function* () { yield* x; })()) { out.push(show(v)); } return out.join(','); })(),
        };

        async function outcome(consumer, x) {
            try { return await consumers[consumer](x); } catch (e) { return e.constructor.name; }
        }

        // One way of handing kind's iterators the `next` of foreign's prototype - or of kind's own prototype in the other
        // realm - after the consumer has run once over an untouched one, so that whatever the engine remembers about it
        // is warm: the kind's prototype given it, with the consumer over an iterator or over an iterable; the iterator
        // moved onto that prototype, alone, with an own string- or symbol-keyed property, or handed out by an iterable's
        // @@iterator; or the iterator given it as an own `next`. Puts the prototype back and answers the outcome.
        async function lane(kind, foreign, route, consumer) {
            await outcome(consumer, kinds[kind].iterator(globalThis));
            const P = protoOf(kind);
            const F = foreign === 'otherRealm' ? protoOf(kind, other) : protoOf(foreign);
            const saved = Object.getOwnPropertyDescriptor(P, 'next');
            const it = kinds[kind].iterator(globalThis);
            let target = it;
            switch (route) {
                case 'patchedIterator': P.next = F.next; break;
                case 'patchedIterable': P.next = F.next; target = kinds[kind].iterable(); break;
                case 'swapped': Object.setPrototypeOf(it, F); break;
                case 'swappedOwnProperty': it.unrelated = 1; Object.setPrototypeOf(it, F); break;
                case 'swappedOwnSymbol': it[Symbol('unrelated')] = 1; Object.setPrototypeOf(it, F); break;
                case 'swappedViaIterable': Object.setPrototypeOf(it, F); target = { [Symbol.iterator]() { return it; } }; break;
                case 'own': it.next = F.next; break;
            }
            try { return await outcome(consumer, target); }
            finally { Object.defineProperty(P, 'next', saved); }
        }

        // lane() for every foreign `next` and every consumer: "<combinations run> <each one that did not answer expected>".
        async function laneRow(kind, route, foreigns, expected) {
            let count = 0;
            const odd = [];
            for (const foreign of foreigns) {
                for (const consumer of Object.keys(consumers)) {
                    count++;
                    const o = await lane(kind, foreign, route, consumer);
                    if (o !== expected) { odd.push(foreign + '.' + consumer + '=' + o); }
                }
            }
            return (count + ' ' + odd.join(' ')).trim();
        }
        """;

    // The kinds whose iterators the ECMAScript and ECMA-402 built-ins make. The three array kinds share
    // %ArrayIteratorPrototype%, so they are one kind to every `next`.
    private static readonly string[] LanguageKinds = ["array", "arrayLike", "typedArray", "map", "set", "string", "regExpString", "segment"];

    // A wrapped CLR list's and dictionary's iterators, which inherit %ArrayIteratorPrototype%.
    private static readonly string[] ClrKinds = ["list", "dictionary"];

    private static readonly string[] WebKinds = ["headers", "urlSearchParams", "formData"];

    private static readonly string[] OtherReceivers = ["plain", "inheriting", "generator", "helper", "wrap"];

    private static readonly string[] OtherNexts = ["generator", "helper", "wrap"];

    private static readonly string[] Routes =
        ["patchedIterator", "patchedIterable", "swapped", "swappedOwnProperty", "swappedOwnSymbol", "swappedViaIterable", "own"];

    private static readonly string[] Consumers =
        ["forOf", "spread", "from", "destructure", "yieldStar", "forOfInGenerator", "forOfInAsync", "forAwait", "yieldStarInAsyncGenerator"];

    // The `next` each kind's iterators inherit, by the kind its prototype belongs to.
    private static string Family(string kind) => kind switch
    {
        "arrayLike" or "typedArray" or "list" or "dictionary" => "array",
        _ => kind,
    };

    private static Engine NewEngine(bool web = false)
    {
        var engine = web
#if NET8_0_OR_GREATER
            ? new Engine(options => options.UseWebApis(WebApiFeatures.Fetch | WebApiFeatures.Files | WebApiFeatures.Url))
#else
            ? throw new InvalidOperationException("The web APIs need net8.0 or later.")
#endif
            : new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.SetValue("dictionary", new Dictionary<string, int> { ["1"] = 1, ["2"] = 2 });
        Test262Object.Install(engine);
        engine.Execute("var other = $262.createRealm().global;");
        return engine;
    }

    private static string Run(Engine engine, string script) =>
        engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    private static TestCases<string, string, string> BorrowRows(string[] nextKinds, string[] receiverKinds)
    {
        var rows = new TestCases<string, string, string>();
        foreach (var next in nextKinds)
        {
            foreach (var receiver in receiverKinds)
            {
                string expected;
                if (receiver is "plain" or "inheriting")
                {
                    expected = "TypeError -";
                }
                else if (Family(receiver) == Family(next))
                {
                    // Stepped: the call answers the first value, and the receiver's own `next` the second.
                    expected = "1/false 2";
                }
                else
                {
                    // Refused before anything is stepped, so the receiver's own `next` still answers the first value.
                    expected = "TypeError 1";
                }

                rows.Add(next, receiver, expected);
            }
        }

        return rows;
    }

    public static TestCases<string, string, string> EveryNextOnEveryReceiver
        => BorrowRows(
            ["array", "map", "set", "string", "regExpString", "segment", .. OtherNexts],
            [.. LanguageKinds, .. ClrKinds, .. OtherReceivers]);

    /// <summary>
    /// Every borrowable <c>next</c> called on every kind of receiver: only an iterator of the <c>next</c>'s own kind is
    /// stepped, and any other receiver is refused with a <c>TypeError</c> before it is stepped. The generator, helper and
    /// wrapper rows were already right and are the controls.
    /// </summary>
    [TestCaseSource(nameof(EveryNextOnEveryReceiver))]
    public void ANextStepsOnlyAnIteratorOfItsOwnKind(string next, string receiver, string expected)
    {
        Run(NewEngine(), $"borrow('{next}', '{receiver}')").Should().Be(expected);
    }

    public static TestCases<string, string> ForeignLaneRows
    {
        get
        {
            var rows = new TestCases<string, string>();
            foreach (var kind in (string[]) [.. LanguageKinds, .. ClrKinds])
            {
                foreach (var route in Routes)
                {
                    rows.Add(kind, route);
                }
            }

            return rows;
        }
    }

    // The kinds of `next` foreign to kind's iterators: one per prototype, the array iterators' standing for all three.
    private static string[] ForeignsOf(string kind, string[] kinds)
        => kinds.Where(k => k is not ("arrayLike" or "typedArray" or "list" or "dictionary") && Family(k) != Family(kind)).ToArray();

    private static string Quoted(string[] values) => "[" + string.Join(", ", values.Select(v => "'" + v + "'")) + "]";

    /// <summary>
    /// Another kind's built-in <c>next</c>, reaching a kind's iterators by each route, is called by every consumer and
    /// throws a <c>TypeError</c> - including where the iterator was moved onto that kind's prototype, which the native
    /// lane used to step without calling anything.
    /// </summary>
    [TestCaseSource(nameof(ForeignLaneRows))]
    public void AnotherKindsNextIsNeverSteppedNatively(string kind, string route)
    {
        var foreigns = ForeignsOf(kind, LanguageKinds);
        Run(NewEngine(), $"laneRow('{kind}', '{route}', {Quoted(foreigns)}, 'TypeError')")
            .Should().Be((foreigns.Length * Consumers.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static TestCases<string, string> OtherRealmLaneRows
    {
        get
        {
            var rows = new TestCases<string, string>();
            foreach (var kind in LanguageKinds)
            {
                foreach (var route in Routes)
                {
                    rows.Add(kind, route);
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// A brand is the same in every realm, so the same kind's <c>next</c> from another realm, reaching an iterator by
    /// each of those routes, steps it under every consumer. These are the controls for the rows above, through the same
    /// harness.
    /// </summary>
    [TestCaseSource(nameof(OtherRealmLaneRows))]
    public void TheSameKindsNextFromAnotherRealmStepsIt(string kind, string route)
    {
        Run(NewEngine(), $"laneRow('{kind}', '{route}', ['otherRealm'], '1,2')")
            .Should().Be(Consumers.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The issue's probe, and the brief's: a <c>for…of</c> over an array whose iterator prototype was given the
    /// <c>Map</c> iterator's <c>next</c>.
    /// </summary>
    [TestCase("Object.getPrototypeOf([].values()).next.call(new Map([[1, 2]]).entries())", "TypeError")]
    [TestCase("Object.getPrototypeOf([].values()).next = Object.getPrototypeOf(new Map().entries()).next; for (const x of [1]) { }", "TypeError")]
    [TestCase("Object.setPrototypeOf(new Map([[1, 2]]).entries(), Object.getPrototypeOf([].values())).next()", "TypeError")]
    public void TheIssuesProbesThrow(string script, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"(function () {{ try {{ {script}; return 'none'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// The <c>TypeError</c> is the step's, never GetIterator's: <c>next</c> is read, called and throws, so the loop's
    /// record is done and no <c>return</c> is called, sync and async alike. The two <c>for await</c> rows are the
    /// specification's rather than Node's: %AsyncFromSyncIteratorPrototype%.next rejects on a throwing <c>next</c>
    /// without closing (https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.next, IfAbruptRejectPromise), and
    /// Node 26 calls <c>return</c> there after any <c>next</c> that throws, a plain object's included.
    /// </summary>
    [TestCase("forOf")]
    [TestCase("spread")]
    [TestCase("destructure")]
    [TestCase("yieldStar")]
    [TestCase("forOfInGenerator")]
    [TestCase("forOfInAsync")]
    [TestCase("forAwait")]
    [TestCase("yieldStarInAsyncGenerator")]
    public void ARefusedStepClosesNothing(string consumer)
    {
        Run(NewEngine(), $$"""
            (async function () {
                let closed = 0;
                const it = kinds.map.iterator(globalThis);
                it.return = function () { closed++; return {}; };
                Object.setPrototypeOf(it, protoOf('array'));
                return (await outcome('{{consumer}}', it)) + ' ' + closed;
            })()
            """).Should().Be("TypeError 0");
    }

    // The record GetIterator builds around a built-in iterator, by both of its lanes: the iterator handed back by an
    // @@iterator method (ToIteratorRecord), and the iterator object itself as the operand (TryGetIterator).
    private static (bool Record, bool Operand) Lanes(Engine engine, IteratorInstance instance)
    {
        var record = ReferenceEquals(instance.ToIteratorRecord(), instance);
        instance.TryGetIterator(engine.Realm, out var operand).Should().BeTrue();
        return (record, ReferenceEquals(operand, instance));
    }

    /// <summary>
    /// Which prototype the native lane stands in for: an untouched iterator, or one moved onto its own kind's prototype
    /// of another realm, is stepped natively by both lanes; one moved onto another kind's prototype by neither, until it
    /// is moved back. No Node counterpart: this pins the lane, whose answers the tests above check.
    /// </summary>
    [Test]
    public void TheNativeLaneStepsAnIteratorOnlyUnderAPrototypeOfItsOwnKind(
        [Values("array", "arrayLike", "typedArray", "map", "set", "string", "regExpString", "segment", "list", "dictionary")] string kind)
    {
        var engine = NewEngine();
        engine.Execute(Harness);
        var instance = (IteratorInstance) engine.Evaluate($"var it = kinds['{kind}'].iterator(globalThis); it").AsObject();
        var foreign = Family(kind) == "map" ? "set" : "map";

        Lanes(engine, instance).Should().Be((true, true), "untouched");

        engine.Execute($"Object.setPrototypeOf(it, protoOf('{foreign}'));");
        Lanes(engine, instance).Should().Be((false, false), "under another kind's prototype");

        engine.Execute($"Object.setPrototypeOf(it, protoOf('{kind}'));");
        Lanes(engine, instance).Should().Be((true, true), "moved back");

        engine.Execute($"Object.setPrototypeOf(it, protoOf('{Family(kind)}', other));");
        Lanes(engine, instance).Should().Be((true, true), "under its own kind's prototype of another realm");
    }

#if NET8_0_OR_GREATER
    public static TestCases<string, string, string> WebNextsOnEveryReceiver
        => BorrowRows([.. WebKinds], [.. LanguageKinds, .. ClrKinds, .. WebKinds, .. OtherReceivers]);

    public static TestCases<string, string, string> EveryNextOnWebReceivers
        => BorrowRows(["array", "map", "set", "string", "regExpString", "segment", .. OtherNexts], [.. WebKinds]);

    /// <summary>
    /// The WebIDL iterators of <c>Headers</c>, <c>URLSearchParams</c> and <c>FormData</c>: each <c>next</c> steps only a
    /// default iterator object of its own interface.
    /// </summary>
    [TestCaseSource(nameof(WebNextsOnEveryReceiver))]
    [TestCaseSource(nameof(EveryNextOnWebReceivers))]
    public void AWebApiNextStepsOnlyAnIteratorOfItsOwnInterface(string next, string receiver, string expected)
    {
        Run(NewEngine(web: true), $"borrow('{next}', '{receiver}')").Should().Be(expected);
    }

    public static TestCases<string, string> WebLaneRows
    {
        get
        {
            var rows = new TestCases<string, string>();
            foreach (var kind in WebKinds)
            {
                foreach (var route in Routes)
                {
                    rows.Add(kind, route);
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// Another kind's <c>next</c>, the language's or another interface's, reaching a WebIDL iterator by each route
    /// throws under every consumer, and a language kind's iterator given a WebIDL interface's <c>next</c> throws too.
    /// </summary>
    [TestCaseSource(nameof(WebLaneRows))]
    public void AnotherKindsNextIsNeverSteppedNativelyForAWebApiIterator(string kind, string route)
    {
        var foreigns = ForeignsOf(kind, [.. LanguageKinds, .. WebKinds]);
        Run(NewEngine(web: true), $"laneRow('{kind}', '{route}', {Quoted(foreigns)}, 'TypeError')")
            .Should().Be((foreigns.Length * Consumers.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public void AWebApiNextIsNeverSteppedNativelyForALanguageIterator(
        [Values("array", "map", "string", "segment")] string kind,
        [Values("patchedIterable", "swapped", "swappedViaIterable")] string route)
    {
        Run(NewEngine(web: true), $"laneRow('{kind}', '{route}', {Quoted([.. WebKinds])}, 'TypeError')")
            .Should().Be((WebKinds.Length * Consumers.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public void TheNativeLaneStepsAWebApiIteratorOnlyUnderAPrototypeOfItsOwnInterface([Values("headers", "urlSearchParams", "formData")] string kind)
    {
        var engine = NewEngine(web: true);
        engine.Execute(Harness);
        var instance = (IteratorInstance) engine.Evaluate($"var it = kinds['{kind}'].iterator(globalThis); it").AsObject();
        var foreign = kind == "headers" ? "formData" : "headers";

        Lanes(engine, instance).Should().Be((true, true), "untouched");

        engine.Execute($"Object.setPrototypeOf(it, protoOf('{foreign}'));");
        Lanes(engine, instance).Should().Be((false, false), "under another interface's prototype");

        engine.Execute("Object.setPrototypeOf(it, protoOf('map'));");
        Lanes(engine, instance).Should().Be((false, false), "under a language kind's prototype");

        engine.Execute($"Object.setPrototypeOf(it, protoOf('{kind}'));");
        Lanes(engine, instance).Should().Be((true, true), "moved back");
    }
#endif
}
