#nullable enable

using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the shape of the built-in iterator prototypes (sebastienros/jint#4407).
/// <a href="https://tc39.es/ecma262/#sec-%iteratorprototype%-object">%Iterator.prototype%</a> has no <c>next</c>: each
/// concrete prototype defines its own, so deleting one leaves nothing to inherit, and every consumer then fails with a
/// <c>TypeError</c>. A segment iterator inherits
/// <a href="https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%-object">%IntlSegmentIteratorPrototype%</a>,
/// one per realm, whose own <c>next</c> and <c>@@toStringTag</c> are its own, rather than %ArrayIteratorPrototype%
/// (<a href="https://tc39.es/ecma402/#sec-createsegmentiterator">CreateSegmentIterator</a>).
/// <para>
/// Every expected value is Node 26's, for the same harness. The realm tests say where theirs come from; the engine-flag
/// and native-lane tests have no Node counterpart and pin that the caches the lanes keep stay sound.
/// </para>
/// </summary>
public class IteratorPrototypeShapeTests
{
    private const string Harness = """
        // Each built-in iterator kind: a fresh iterator, and the collection a consumer takes as it is. Uint8Array.of
        // iterates nothing, so it can build a typed array while %ArrayIteratorPrototype%.next is deleted.
        const kinds = {
            array: { iterator: () => [1, 2].values(), iterable: () => [1, 2] },
            typedArray: { iterator: () => Uint8Array.of(1, 2).values(), iterable: () => Uint8Array.of(1, 2) },
            map: { iterator: () => new Map([[1, 'a'], [2, 'b']]).keys(), iterable: () => new Map([[1, 'a'], [2, 'b']]) },
            set: { iterator: () => new Set([1, 2]).values(), iterable: () => new Set([1, 2]) },
            string: { iterator: () => 'ab'[Symbol.iterator](), iterable: () => 'ab' },
            regExpString: { iterator: () => 'a1b2'.matchAll(/[0-9]/g), iterable: () => 'a1b2'.matchAll(/[0-9]/g) },
            segments: { iterator: () => new Intl.Segmenter().segment('ab')[Symbol.iterator](), iterable: () => new Intl.Segmenter().segment('ab') },
        };

        // A segment data object shows its segment, a match or a map entry its first element, anything else itself.
        function show(v) {
            if (typeof v !== 'object' || v === null) { return String(v); }
            return 'segment' in v ? v.segment : String(v[0]);
        }

        // Every consumer that takes an iterator, sync, in a generator and in an async function side by side.
        const consumers = {
            spread: x => [...x].map(show).join(','),
            forOf: x => { const out = []; for (const v of x) { out.push(show(v)); } return out.join(','); },
            from: x => Array.from(x).map(show).join(','),
            destructure: x => { const [a, b] = x; return show(a) + ',' + show(b); },
            yieldStar: x => { const out = []; const g = (function* () { yield* x; })(); for (let s = g.next(); !s.done; s = g.next()) { out.push(show(s.value)); } return out.join(','); },
            forOfInGenerator: x => (function* () { const out = []; for (const v of x) { out.push(show(v)); } yield out.join(','); })().next().value,
            forOfInAsync: x => (async function () { await 0; const out = []; for (const v of x) { out.push(show(v)); } return out.join(','); })(),
            forAwait: x => (async function () { const out = []; for await (const v of x) { out.push(show(v)); } return out.join(','); })(),
        };

        async function outcome(consumer, x) {
            try { return await consumers[consumer](x); } catch (e) { return e.constructor.name; }
        }

        // Deletes the kind's prototype's own `next` - after the consumer ran once when `warm` - and runs the consumer over a
        // fresh collection or iterator; under 'objectPrototype', with a counting `next` on Object.prototype as well. Puts
        // everything back and runs it again. Answers "<typeof next on a fresh iterator> <outcome> | <outcome once restored>".
        async function afterDelete(kind, source, consumer, route, warm) {
            const make = kinds[kind][source];
            if (warm) { await outcome(consumer, make()); }
            const P = Object.getPrototypeOf(kinds[kind].iterator());
            const saved = Object.getOwnPropertyDescriptor(P, 'next');
            let calls = 0;
            delete P.next;
            if (route === 'objectPrototype') {
                Object.defineProperty(Object.prototype, 'next', {
                    value: function () { calls++; return calls <= 2 ? { value: 'R' + calls, done: false } : { value: undefined, done: true }; },
                    configurable: true, writable: true
                });
            }
            let first;
            try {
                first = typeof kinds[kind].iterator().next + ' ' + (await outcome(consumer, make()));
            } finally {
                delete Object.prototype.next;
                Object.defineProperty(P, 'next', saved);
            }
            return first + ' | ' + (await outcome(consumer, make()));
        }

        // The shape of a segment iterator and of the prototype it inherits, for a segmenter of the given granularity.
        function segmentShape(granularity) {
            const it = new Intl.Segmenter('en', { granularity }).segment('ab')[Symbol.iterator]();
            const SP = Object.getPrototypeOf(it);
            const AIP = Object.getPrototypeOf([].values());
            const d = Object.getOwnPropertyDescriptor(SP, 'next');
            const t = Object.getOwnPropertyDescriptor(SP, Symbol.toStringTag);
            return [Object.prototype.toString.call(it), SP === AIP, Object.getPrototypeOf(SP) === Iterator.prototype,
                Object.getPrototypeOf(Iterator.prototype) === Object.prototype, Reflect.ownKeys(SP).map(String).join(' '),
                typeof d.value, d.value.name, d.value.length, d.writable, d.enumerable, d.configurable, d.value === AIP.next,
                t.value, t.writable, t.enumerable, t.configurable,
                SP === Object.getPrototypeOf(new Intl.Segmenter('en', { granularity: 'sentence' }).segment('x')[Symbol.iterator]()),
                it instanceof Iterator, Reflect.ownKeys(it).length, it[Symbol.iterator]() === it].join('|');
        }

        // The kind's prototype and the shape of its own `next`, and whether %Iterator.prototype% is the next link up.
        function concreteShape(make) {
            const it = make();
            const P = Object.getPrototypeOf(it);
            const d = Object.getOwnPropertyDescriptor(P, 'next');
            return [Object.prototype.toString.call(it), Object.hasOwn(P, 'next'), typeof d.value, d.value.name, d.value.length,
                d.writable, d.enumerable, d.configurable, Object.getPrototypeOf(P) === Iterator.prototype].join('|');
        }

        // Puts a counting `next` or `return` on %ArrayIteratorPrototype% ('array') or %IntlSegmentIteratorPrototype%
        // ('segment'), runs one consumer over a segments object - for a `return`, a for-of that breaks and a destructuring
        // that stops early as well - and answers "<outcome> <calls>". Nothing in the window iterates an array.
        async function reach(holder, what, consumer) {
            const P = holder === 'array'
                ? Object.getPrototypeOf([].values())
                : Object.getPrototypeOf(new Intl.Segmenter().segment('a')[Symbol.iterator]());
            const saved = Object.getOwnPropertyDescriptor(P, what);
            let calls = 0;
            if (what === 'next') {
                P.next = function () { calls++; return { done: true, value: undefined }; };
            } else {
                P.return = function () { calls++; return {}; };
            }
            let result;
            try {
                result = await outcome(consumer, new Intl.Segmenter().segment('ab'));
                if (what === 'return') {
                    for (const s of new Intl.Segmenter().segment('ab')) { break; }
                    const [a] = new Intl.Segmenter().segment('ab');
                }
            } finally {
                if (saved) { Object.defineProperty(P, what, saved); } else { delete P[what]; }
            }
            return result + ' ' + calls;
        }
        """;

    private static readonly string[] Kinds = ["array", "typedArray", "map", "set", "string", "regExpString", "segments"];

    private static readonly string[] Consumers =
        ["spread", "forOf", "from", "destructure", "yieldStar", "forOfInGenerator", "forOfInAsync", "forAwait"];

    // What each kind's consumers produce with the built-in `next`.
    private static string Values(string kind) => kind is "string" or "segments" ? "a,b" : "1,2";

    /// <summary>
    /// Node answers the same for every kind, source and consumer, warm or cold: with the prototype's own <c>next</c>
    /// deleted, a fresh iterator's <c>next</c> is undefined and the consumer fails with a <c>TypeError</c>; with a
    /// <c>next</c> on <c>Object.prototype</c> as well, that one is found and called. Either way the built-in is back once
    /// it is restored.
    /// </summary>
    public static TestCases<string, string, string, string> EveryKindAfterADelete
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var kind in Kinds)
            {
                foreach (var source in new[] { "iterable", "iterator" })
                {
                    foreach (var consumer in Consumers)
                    {
                        foreach (var mode in new[] { "cold", "warm" })
                        {
                            rows.Add(kind, source, consumer + " delete " + mode, "undefined TypeError | " + Values(kind));
                            rows.Add(kind, source, consumer + " objectPrototype " + mode, "function R1,R2 | " + Values(kind));
                        }
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// What Node answers for each holder and member: nothing on %ArrayIteratorPrototype% reaches a segment iterator, and
    /// a <c>next</c> or a <c>return</c> on %IntlSegmentIteratorPrototype% does - the destructuring of two segments
    /// closes once more, since it stops before the iterator is done.
    /// </summary>
    public static TestCases<string, string, string, string> EveryReach
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var consumer in Consumers)
            {
                var destructure = consumer == "destructure";
                rows.Add("array", "next", consumer, "a,b 0");
                rows.Add("array", "return", consumer, "a,b 0");
                rows.Add("segment", "next", consumer, destructure ? "undefined,undefined 1" : " 1");
                rows.Add("segment", "return", consumer, destructure ? "a,b 3" : "a,b 2");
            }

            return rows;
        }
    }

    private static string Run(Engine engine, string script) =>
        engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    private static bool StepsNatively(IteratorInstance instance) => ReferenceEquals(instance.ToIteratorRecord(), instance);

    /// <summary>
    /// %Iterator.prototype% has no <c>next</c> of its own, and nothing above it has one, so an <c>Iterator</c> subclass
    /// that defines none inherits none.
    /// </summary>
    [Test]
    public void IteratorPrototypeHasNoNext()
    {
        new Engine().Evaluate("""
            const IP = Iterator.prototype;
            class Subclass extends Iterator { }
            let iterated;
            try { for (const x of new Subclass()) { } iterated = 'no throw'; } catch (e) { iterated = e.constructor.name; }
            [Object.hasOwn(IP, 'next'), 'next' in IP, typeof IP.next, Object.getOwnPropertyNames(IP).includes('next'),
                Object.getOwnPropertyDescriptor(IP, 'next') === undefined,
                Object.getPrototypeOf(Object.getPrototypeOf([][Symbol.iterator]())) === IP,
                typeof new Subclass().next, iterated].join('|')
            """).AsString().Should().Be("false|false|undefined|false|true|true|undefined|TypeError");
    }

    /// <summary>
    /// Each concrete prototype keeps its own <c>next</c>, a writable, configurable, non-enumerable method named
    /// <c>next</c>, and inherits %Iterator.prototype% directly.
    /// </summary>
    [TestCase("[1, 2].values()", "[object Array Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("new Uint8Array([1]).values()", "[object Array Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("new Map().entries()", "[object Map Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("new Set().values()", "[object Set Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("''[Symbol.iterator]()", "[object String Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("'a'.matchAll(/a/g)", "[object RegExp String Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("new Intl.Segmenter().segment('a')[Symbol.iterator]()", "[object Segmenter String Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("[].values().map(x => x)", "[object Iterator Helper]|true|function|next|0|true|false|true|true")]
    [TestCase("Iterator.from({ next() { return { done: true }; } })", "[object Iterator]|true|function|next|0|true|false|true|true")]
    [TestCase("Object.create(Object.getPrototypeOf(Object.getPrototypeOf((function* () { })())))", "[object Generator]|true|function|next|1|true|false|true|true")]
    public void EveryConcretePrototypeHasANextOfItsOwn(string iterator, string expected)
    {
        Run(new Engine(), $"concreteShape(() => {iterator})").Should().Be(expected);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The WebIDL iterator prototypes of <c>Headers</c>, <c>FormData</c> and <c>URLSearchParams</c> keep theirs too, which
    /// WebIDL makes enumerable.
    /// </summary>
    [TestCase("new Headers([['a', '1']]).entries()", "[object Headers Iterator]|true|function|next|0|true|true|true|true")]
    [TestCase("new FormData().keys()", "[object FormData Iterator]|true|function|next|0|true|true|true|true")]
    [TestCase("new URLSearchParams('a=1').values()", "[object URLSearchParams Iterator]|true|function|next|0|true|true|true|true")]
    public void EveryWebIteratorPrototypeHasANextOfItsOwn(string iterator, string expected)
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Fetch | WebApiFeatures.Files | WebApiFeatures.Url));
        Run(engine, $"concreteShape(() => {iterator})").Should().Be(expected);
    }
#endif

    /// <summary>
    /// A segment iterator of every granularity inherits %IntlSegmentIteratorPrototype%: not %ArrayIteratorPrototype%, a
    /// prototype whose only own properties are its <c>next</c> and its <c>@@toStringTag</c>
    /// (<a href="https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.next">next</a>,
    /// <a href="https://tc39.es/ecma402/#sec-%intlsegmentiteratorprototype%.%symbol.tostringtag%">"Segmenter String Iterator"</a>,
    /// non-writable and configurable), and whose own prototype is %Iterator.prototype%. Every segment iterator of the realm
    /// shares it, and the instance itself has no own property.
    /// </summary>
    [TestCase("grapheme")]
    [TestCase("word")]
    [TestCase("sentence")]
    public void ASegmentIteratorInheritsTheSegmentIteratorPrototype(string granularity)
    {
        Run(new Engine(), $"segmentShape('{granularity}')").Should().Be(
            "[object Segmenter String Iterator]|false|true|true|next Symbol(Symbol.toStringTag)|function|next|0|true|false|true|false"
            + "|Segmenter String Iterator|false|false|true|true|true|0|true");
    }

    /// <summary>
    /// Deleting a concrete prototype's own <c>next</c> leaves nothing to inherit: a fresh iterator's <c>next</c> is
    /// undefined and every consumer throws a <c>TypeError</c>, whether it was handed the collection or the iterator and
    /// whether the consumer ran once before, which warms the caches every lane keeps of that <c>next</c>. A <c>next</c>
    /// on <c>Object.prototype</c> is then the one found. Until sebastienros/jint#4407 the delete uncovered a built-in
    /// <c>next</c> on %Iterator.prototype%, and every consumer iterated as if nothing had happened.
    /// </summary>
    [TestCaseSource(nameof(EveryKindAfterADelete))]
    public void DeletingAKindsNextLeavesNothingToInherit(string kind, string source, string how, string expected)
    {
        var parts = how.Split(' ');
        Run(new Engine(), $"afterDelete('{kind}', '{source}', '{parts[0]}', '{parts[1]}', {(parts[2] == "warm" ? "true" : "false")})")
            .Should().Be(expected);
    }

    /// <summary>
    /// The caches behind the native lanes follow a delete: an untouched iterator of every kind is its own iterator record,
    /// is not once its prototype's <c>next</c> is deleted, and is again once that <c>next</c> is restored; so is a string
    /// primitive's, and an array read by index in place of its iterator. Deleting %ArrayIteratorPrototype%'s sets
    /// <see cref="Engine._arrayIteratorNextMayBeReplaced"/>, and no other kind's delete does.
    /// </summary>
    [TestCase("[1, 2].values()", true)]
    [TestCase("Uint8Array.of(1, 2).values()", true)]
    [TestCase("new Map([[1, 'a'], [2, 'b']]).entries()", false)]
    [TestCase("new Set([1, 2]).values()", false)]
    [TestCase("'ab'[Symbol.iterator]()", false)]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", false)]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", false)]
    public void TheNativeLanesFollowADeletedNextAndComeBack(string iterator, bool isArrayIterator)
    {
        var engine = new Engine();
        engine.Execute($$"""
            function fresh() { return {{iterator}}; }
            var P = Object.getPrototypeOf(fresh());
            var nextDescriptor = Object.getOwnPropertyDescriptor(P, 'next');
            [...[1, 2]]; [...'ab']; var [a] = [1, 2];
            """);
        var array = (ObjectInstance) engine.Evaluate("[1, 2]");

        bool Native() => StepsNatively((IteratorInstance) engine.Evaluate("fresh()"));
        bool StringNative() => JsString.Create("ab").GetIterator(engine.Realm) is IteratorInstance.StringIterator;

        Native().Should().BeTrue();
        StringNative().Should().BeTrue();
        array.HasOriginalIterator.Should().BeTrue();

        engine.Execute("delete P.next;");
        Native().Should().BeFalse();
        StringNative().Should().Be(!iterator.StartsWith("'ab'", StringComparison.Ordinal));
        array.HasOriginalIterator.Should().Be(!isArrayIterator);
        engine._arrayIteratorNextMayBeReplaced.Should().Be(isArrayIterator);

        engine.Execute("Object.defineProperty(P, 'next', nextDescriptor);");
        Native().Should().BeTrue();
        StringNative().Should().BeTrue();
        array.HasOriginalIterator.Should().BeTrue();
        engine.Evaluate("[...fresh()].length + ',' + [...'ab'].length + ',' + [...[1, 2]].length").AsString().Should().Be("2,2,2");
    }

    /// <summary>
    /// A built-in iterator moved onto %Iterator.prototype% itself finds no <c>next</c>, and one defined there without a
    /// value is undefined, so no consumer steps it: it fails with a <c>TypeError</c>, as in Node 26. %Iterator.prototype%
    /// captured no built-in <c>next</c>, and a <c>next</c> holding no value must not pass for the one it did not capture.
    /// A <c>next</c> given a value there is called.
    /// </summary>
    [TestCase("", "spread", "TypeError")]
    [TestCase("", "forOf", "TypeError")]
    [TestCase("", "destructure", "TypeError")]
    [TestCase("", "forAwait", "TypeError")]
    [TestCase("Object.defineProperty(Iterator.prototype, 'next', { writable: true, configurable: true });", "spread", "TypeError")]
    [TestCase("Object.defineProperty(Iterator.prototype, 'next', { writable: true, configurable: true });", "forOf", "TypeError")]
    [TestCase("Object.defineProperty(Iterator.prototype, 'next', { writable: true, configurable: true });", "destructure", "TypeError")]
    [TestCase("Object.defineProperty(Iterator.prototype, 'next', { writable: true, configurable: true });", "forAwait", "TypeError")]
    [TestCase("Object.defineProperty(Iterator.prototype, 'next', { configurable: true, value: () => ({ done: true }) });", "spread", "")]
    [TestCase("Object.defineProperty(Iterator.prototype, 'next', { configurable: true, value: () => ({ done: true }) });", "forOf", "")]
    public void ABuiltInIteratorMovedOntoIteratorPrototypeStepsNothingNatively(string define, string consumer, string expected)
    {
        foreach (var kind in Kinds)
        {
            Run(new Engine(), $$"""
                (async function () {
                    {{define}}
                    const it = kinds['{{kind}}'].iterator();
                    await outcome('{{consumer}}', kinds['{{kind}}'].iterator());
                    Object.setPrototypeOf(it, Iterator.prototype);
                    return outcome('{{consumer}}', it);
                })()
                """).Should().Be(expected, kind);
        }
    }

    /// <summary>
    /// A <c>next</c> or a <c>return</c> put on %ArrayIteratorPrototype% no longer reaches a segment iterator, by any
    /// consumer; one put on %IntlSegmentIteratorPrototype% does. Until sebastienros/jint#4407 the two were one object.
    /// </summary>
    [TestCaseSource(nameof(EveryReach))]
    public void OnlyTheSegmentIteratorPrototypeReachesASegmentIterator(string holder, string what, string consumer, string expected)
    {
        Run(new Engine(), $"reach('{holder}', '{what}', '{consumer}')").Should().Be(expected);
    }

    /// <summary>
    /// %IntlSegmentIteratorPrototype% is a built-in iterator prototype to the engine: a <c>return</c> put on it, or a
    /// change to its [[Prototype]], sets <see cref="Engine._builtinIteratorChainMayHaveReturn"/>, and its untouched
    /// <c>next</c> is stepped natively, so replacing it takes a segment iterator off that lane and restoring it puts it
    /// back. No change to it reaches <see cref="Engine._arrayIteratorNextMayBeReplaced"/> or an array's lane any more.
    /// </summary>
    [Test]
    public void TheSegmentIteratorPrototypeIsABuiltInIteratorPrototypeOfItsOwn()
    {
        var engine = new Engine();
        engine.Execute("""
            function fresh() { return new Intl.Segmenter().segment('ab')[Symbol.iterator](); }
            var SP = Object.getPrototypeOf(fresh());
            var IP = Object.getPrototypeOf(SP);
            var nextDescriptor = Object.getOwnPropertyDescriptor(SP, 'next');
            [...fresh()];
            """);

        bool SegmentNative() => StepsNatively((IteratorInstance) engine.Evaluate("fresh()"));
        bool ArrayNative() => StepsNatively((IteratorInstance) engine.Evaluate("[1, 2].values()"));

        engine.Evaluate("SP").Should().BeAssignableTo<IteratorPrototype>()
            .And.NotBeSameAs(engine.Realm.Intrinsics.ArrayIteratorPrototype);
        SegmentNative().Should().BeTrue();
        ArrayNative().Should().BeTrue();

        engine.Execute("SP.next = function () { return { done: true, value: undefined }; };");
        SegmentNative().Should().BeFalse();
        ArrayNative().Should().BeTrue();
        engine._arrayIteratorNextMayBeReplaced.Should().BeFalse();
        engine.Evaluate("[...fresh()].length + ',' + [...[1, 2]].length").AsString().Should().Be("0,2");

        engine.Execute("Object.defineProperty(SP, 'next', nextDescriptor);");
        SegmentNative().Should().BeTrue();
        engine._builtinIteratorChainMayHaveReturn.Should().BeFalse();

        engine.Execute("Object.setPrototypeOf(SP, { __proto__: IP }); Object.setPrototypeOf(SP, IP);");
        engine._builtinIteratorChainMayHaveReturn.Should().BeTrue();

        var other = new Engine();
        other.Execute("var SP = Object.getPrototypeOf(new Intl.Segmenter().segment('ab')[Symbol.iterator]()); SP.return = function () { return {}; }; delete SP.return;");
        other._builtinIteratorChainMayHaveReturn.Should().BeTrue();
        other._arrayIteratorNextMayBeReplaced.Should().BeFalse();
    }

    /// <summary>
    /// A ShadowRealm has intrinsics of its own, %IntlSegmentIteratorPrototype% among them: its segment iterators inherit
    /// it, and through it that realm's %Iterator.prototype%, and replacing that realm's %ArrayIteratorPrototype%.next
    /// reaches its arrays and not its segment iterators. The principal realm is untouched. The expected value is what
    /// Node 26 gives under <c>--harmony-shadow-realm</c>.
    /// </summary>
    [Test]
    public void AShadowRealmHasASegmentIteratorPrototypeOfItsOwn()
    {
        new Engine().Evaluate("""
            const inner = new ShadowRealm().evaluate(`
                (function () {
                    const it = new Intl.Segmenter().segment('ab')[Symbol.iterator]();
                    const SP = Object.getPrototypeOf(it);
                    const AIP = Object.getPrototypeOf([].values());
                    AIP.next = function () { return { done: true, value: undefined }; };
                    const out = [Object.prototype.toString.call(it), Object.getPrototypeOf(SP) === Iterator.prototype, SP === AIP,
                        Object.hasOwn(Iterator.prototype, 'next'), Object.hasOwn(SP, 'next'),
                        [...new Intl.Segmenter().segment('ab')].map(s => s.segment).join(','), [...[1, 2]].length];
                    return out.join('|');
                })()
            `);
            const SP = Object.getPrototypeOf(new Intl.Segmenter().segment('ab')[Symbol.iterator]());
            inner + ' || ' + [[...new Intl.Segmenter().segment('ab')].map(s => s.segment).join(','), [...[1, 2]].length, Object.hasOwn(SP, 'next')].join('|');
            """).AsString().Should().Be("[object Segmenter String Iterator]|true|false|false|true|a,b|0 || a,b|2|true");
    }

    /// <summary>
    /// Which realm's %IntlSegmentIteratorPrototype% a segment iterator inherits: the realm of the segments object's
    /// <c>@@iterator</c>, which is the realm whose <c>Intl.Segmenter.prototype.segment</c> created the segments object,
    /// whichever realm the segmenter came from. A <c>next</c> replaced in one realm reaches only that realm's segment
    /// iterators. The second realm comes from <c>$262.createRealm()</c>, whose identities a ShadowRealm would hide; the
    /// expected value is Node 26's for the same probe over a <c>vm</c> context.
    /// </summary>
    [Test]
    public void EachRealmHasItsOwnSegmentIteratorPrototype()
    {
        var engine = new Engine();
        Test262Object.Install(engine);
        engine.Evaluate("""
            const other = $262.createRealm().global;
            const mainIt = new Intl.Segmenter().segment('a')[Symbol.iterator]();
            const mainSP = Object.getPrototypeOf(mainIt);
            const otherIt = new other.Intl.Segmenter().segment('a')[Symbol.iterator]();
            const otherSP = Object.getPrototypeOf(otherIt);
            const crossCall = Object.getPrototypeOf(other.Intl.Segmenter.prototype.segment.call(new Intl.Segmenter(), 'a')[Symbol.iterator]());
            const mainSegments = new Intl.Segmenter().segment('a');
            const otherSegments = new other.Intl.Segmenter().segment('a');
            const borrowed = Object.getPrototypeOf(mainSegments[Symbol.iterator].call(otherSegments));
            otherSP.next = function () { return { done: true, value: undefined }; };
            [otherSP !== mainSP, Object.getPrototypeOf(otherSP) === other.Iterator.prototype,
                otherIt instanceof other.Iterator, otherIt instanceof Iterator, crossCall === otherSP, borrowed === mainSP,
                [...new other.Intl.Segmenter().segment('ab')].length, [...new Intl.Segmenter().segment('ab')].length,
                other.eval("Object.prototype.toString.call(new Intl.Segmenter().segment('a')[Symbol.iterator]())")].join('|');
            """).AsString().Should().Be("true|true|true|false|true|true|0|2|[object Segmenter String Iterator]");
    }
}
