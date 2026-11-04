#nullable enable

using Jint.Native.Iterator;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 3 of <a href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</a> for the built-in iterators:
/// a consumer that leaves an iterator early reads <c>return</c> off it with
/// <a href="https://tc39.es/ecma262/#sec-getmethod">GetMethod</a> and calls what it finds (sebastienros/jint#4406).
/// <para>
/// No built-in iterator prototype has a <c>return</c>, so Jint closed a natively stepped iterator without looking: the
/// base close was empty. A <c>return</c> the script put on the iterator, on its prototype, on
/// <c>%Iterator.prototype%</c> or on <c>Object.prototype</c> was never called by a <c>for…of</c> left by
/// <c>break</c>, <c>return</c> or a throw, by destructuring that stops early, by <c>Array.from</c> with a throwing
/// mapper, by <c>yield*</c> or by <c>for await</c>. Nor was it when the async-from-sync wrapper closed the sync
/// iterator because an element was a rejected promise
/// (<a href="https://tc39.es/ecma262/#sec-asyncfromsynciteratorcontinuation">AsyncFromSyncIteratorContinuation</a>),
/// nor by <a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a> when a script
/// <c>@@asyncIterator</c> answered a built-in iterator.
/// </para>
/// <para>
/// Every row first runs its consumer over an iterator nothing has touched, so whatever the engine cached about the
/// kind is warm when the route then adds a <c>return</c>; that is what makes each route a test of the cache's
/// invalidation. Every expected value is Node 26's, taken with this harness; the wrapped CLR collections have no Node
/// counterpart and are expected to answer what every other kind does.
/// </para>
/// </summary>
public class BuiltInIteratorReturnTests
{
    private const string Harness = """
        function drain(gen) { const out = []; for (let r = gen.next(); !r.done; r = gen.next()) { out.push(r.value); } return out.join(','); }

        // The consumers, each answering what it evaluated to. `src` is the iterable: an iterator object, or a collection
        // whose @@iterator hands one out. All but the last four close what they iterate.
        const consumers = {
            forOfBreak: function (src) { for (const x of src) { break; } return 'done'; },
            forOfReturn: function (src) { return (function () { for (const x of src) { return 'R'; } })(); },
            forOfThrow: function (src) { try { for (const x of src) { throw 'E'; } } catch (e) { return 'caught:' + e; } },
            forOfContinueOuter: function (src) { let n = 0; outer: for (const y of [0, 1]) { n++; for (const x of src) { continue outer; } } return String(n); },
            generatorBreak: function (src) { return drain((function* () { for (const x of src) { yield 'y'; break; } })()); },
            generatorReturn: function (src) { const g = (function* () { for (const x of src) { yield 'y'; } })(); g.next(); return g.return('R').value; },
            asyncBreak: async function (src) { for (const x of src) { await x; break; } return 'done'; },
            asyncThrow: async function (src) { try { for (const x of src) { await x; throw 'E'; } } catch (e) { return 'caught:' + e; } },
            destructureBinding: function (src) { const [a] = src; return 'bound'; },
            destructureAssignment: function (src) { let a; [a] = src; return 'assigned'; },
            destructureParameter: function (src) { return (function ([a]) { return 'parameter'; })(src); },
            destructureEmpty: function (src) { const [] = src; return 'empty'; },
            parameterEmpty: function (src) { return (function ([]) { return 'parameterEmpty'; })(src); },
            destructureInGenerator: function (src) { return drain((function* () { const [a = yield 'y'] = src; })()); },
            arrayFromThrows: function (src) { try { Array.from(src, function () { throw 'M'; }); } catch (e) { return 'caught:' + e; } },
            yieldStarThrow: function (src) { const g = (function* () { yield* src; })(); g.next(); try { g.throw('T'); } catch (e) { return e.constructor.name; } },
            forAwaitBreak: async function (src) { for await (const x of src) { break; } return 'done'; },
            asyncYieldStarThrow: async function (src) {
                const g = (async function* () { yield* src; })();
                await g.next();
                try { await g.throw('T'); } catch (e) { return e.constructor.name; }
            },
            asyncIteratorBreak: async function (src) {
                const it = typeof src.next === 'function' ? src : src[Symbol.iterator]();
                for await (const x of { [Symbol.asyncIterator]() { return it; } }) { break; }
                return 'done';
            },
            asyncIteratorThrow: async function (src) {
                const it = typeof src.next === 'function' ? src : src[Symbol.iterator]();
                try { for await (const x of { [Symbol.asyncIterator]() { return it; } }) { throw 'E'; } } catch (e) { return 'caught:' + e; }
            },
            runToTheEnd: function (src) { let n = 0; for (const x of src) { n++; } return String(n); },
            spread: function (src) { return String([...src].length); },
            destructureToTheEnd: function (src) { const [a, b, c, d] = src; return String(d); },
            parameterToTheEnd: function (src) { return (function ([a, b, c, d]) { return String(d); })(src); },
        };

        // Makes `return` resolve, for the iterator `it`, to `method`, and answers the function that puts every object
        // back. The prototype routes reach every iterator of the kind. `reads` counts the reads of an accessor route.
        function applyReturnRoute(route, it, method, reads) {
            const P = Object.getPrototypeOf(it);
            const IP = Object.getPrototypeOf(P);
            const OP = Object.getPrototypeOf(IP);
            const saved = [];
            function save(o, k) { saved.push([o, k, Object.getOwnPropertyDescriptor(o, k)]); }
            const counted = { get() { reads.count++; return method; }, configurable: true };
            switch (route) {
                case 'own': it.return = method; break;
                case 'ownAccessor': Object.defineProperty(it, 'return', counted); break;
                case 'kindPrototype': save(P, 'return'); P.return = method; break;
                case 'kindPrototypeAccessor': save(P, 'return'); Object.defineProperty(P, 'return', counted); break;
                case 'iteratorPrototype': save(IP, 'return'); IP.return = method; break;
                case 'iteratorPrototypeAccessor': save(IP, 'return'); Object.defineProperty(IP, 'return', counted); break;
                case 'objectPrototype': save(OP, 'return'); Object.defineProperty(OP, 'return', { value: method, configurable: true, writable: true }); break;
                case 'objectPrototypeAssigned': save(OP, 'return'); OP.return = method; break;
                case 'protoPlain': Object.setPrototypeOf(it, { __proto__: P, return: method }); break;
                case 'protoProxy': Object.setPrototypeOf(it, new Proxy(P, { get(t, k, r) { return k === 'return' ? method : Reflect.get(t, k, r); } })); break;
                case 'kindPrototypeProto': saved.push([P, null, IP]); Object.setPrototypeOf(P, { __proto__: IP, return: method }); break;
                case 'kindPrototypeProtoProxy': saved.push([P, null, IP]); Object.setPrototypeOf(P, new Proxy(IP, { get(t, k, r) { return k === 'return' ? method : Reflect.get(t, k, r); } })); break;
                case 'iteratorPrototypeProto': saved.push([IP, null, OP]); Object.setPrototypeOf(IP, { __proto__: OP, return: method }); break;
                case 'iteratorPrototypeProtoReflect': saved.push([IP, null, OP]); Reflect.setPrototypeOf(IP, { __proto__: OP, return: method }); break;
                case 'iteratorPrototypeProtoAssigned': saved.push([IP, null, OP]); IP.__proto__ = { __proto__: OP, return: method }; break;
            }
            return function () {
                for (let i = saved.length - 1; i >= 0; i--) {
                    const s = saved[i];
                    if (s[1] === null) { Object.setPrototypeOf(s[0], s[2]); }
                    else if (s[2]) { Object.defineProperty(s[0], s[1], s[2]); }
                    else { delete s[0][s[1]]; }
                }
            };
        }

        // A `return` counting its calls, its receiver and its argument count.
        function counter() {
            const c = { calls: 0, receiver: undefined, argc: -1 };
            c.method = function () { c.calls++; c.receiver = this; c.argc = arguments.length; return {}; };
            return c;
        }

        // Runs the consumer over a fresh iterable from make() that nothing has touched, then routes `return` for a fresh
        // one and runs the consumer over that. Answers "<outcome> <calls> <receiver> <argc> <reads>": the receiver is
        // "it" when it is the iterator the route went on, and its toString tag otherwise. With `viaIterator` the route
        // goes on the iterator the collection's @@iterator answers and the consumer takes the collection, which hands
        // it a fresh iterator of the same kind, so only the prototype routes apply.
        async function closed(make, route, consumer, viaIterator) {
            await consumers[consumer](make());
            const src = make();
            const it = viaIterator ? src[Symbol.iterator]() : src;
            const c = counter(), reads = { count: 0 };
            const restore = applyReturnRoute(route, it, c.method, reads);
            try {
                const outcome = await consumers[consumer](src);
                const receiver = c.calls === 0 ? '-' : (c.receiver === it ? 'it' : Object.prototype.toString.call(c.receiver));
                return outcome + ' ' + c.calls + ' ' + receiver + ' ' + (c.calls === 0 ? '-' : c.argc) + ' ' + reads.count;
            } finally { restore(); }
        }
        """;

    private static string Run(Engine engine, string script) =>
        engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    private static Engine NewEngine()
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.SetValue("dictionary", new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        return engine;
    }

    /// <summary>
    /// An engine on which a script has once put a <c>return</c> on <c>Object.prototype</c> and taken it away again:
    /// <see cref="Engine._builtinIteratorChainMayHaveReturn"/> is set for good, so every close and every array
    /// destructuring answers from <see cref="IteratorPrototype.InstancesInheritNoReturnMethod"/>'s version-checked
    /// lookup, and a route then has to invalidate a cached answer. On an untouched engine the route itself is what sets
    /// the flag.
    /// </summary>
    private static Engine TouchedEngine()
    {
        var engine = NewEngine();
        engine.Execute("Object.defineProperty(Object.prototype, 'return', { value: undefined, configurable: true }); delete Object.prototype.return;");
        return engine;
    }

    // Each built-in iterator kind, with two elements, and the iterators of a wrapped CLR list and dictionary, which
    // inherit %ArrayIteratorPrototype%.
    private static readonly string[] Kinds =
    [
        "[1, 2].values()",
        "[1, 2].keys()",
        "new Uint8Array([1, 2]).values()",
        "new Map([[1, 'a'], [2, 'b']]).entries()",
        "new Set([1, 2]).values()",
        "'ab'[Symbol.iterator]()",
        "'a1b2'.matchAll(/[0-9]/g)",
        "new Intl.Segmenter().segment('ab')[Symbol.iterator]()",
        "list[Symbol.iterator]()",
        "dictionary[Symbol.iterator]()",
    ];

    // Collections a consumer takes as they are, so it creates the iterator itself - natively, for every one of these -
    // and what the toString tag of that iterator is. An array, a typed array and a CLR list are also what the
    // destructuring lanes read by index without creating any iterator, and a string primitive's lane creates no
    // iterator object.
    private static readonly (string Source, string Tag)[] Sources =
    [
        ("[1, 2]", "[object Array Iterator]"),
        ("new Uint8Array([1, 2])", "[object Array Iterator]"),
        ("new Map([[1, 'a'], [2, 'b']])", "[object Map Iterator]"),
        ("new Set([1, 2])", "[object Set Iterator]"),
        ("'ab'", "[object String Iterator]"),
        ("list", "[object Array Iterator]"),
    ];

    // Every way of making `return` resolve for an iterator that had none: an own data or accessor property, one added
    // to the kind's prototype, to %Iterator.prototype% or to Object.prototype, the iterator's prototype swapped for a
    // plain object or a Proxy that has one, and the kind's prototype's or %Iterator.prototype%'s own prototype swapped
    // for a plain object that has one, or the kind's prototype's for a Proxy that answers one.
    private static readonly string[] Routes =
    [
        "own", "ownAccessor", "kindPrototype", "kindPrototypeAccessor", "iteratorPrototype", "iteratorPrototypeAccessor",
        "objectPrototype", "objectPrototypeAssigned", "protoPlain", "protoProxy", "kindPrototypeProto", "kindPrototypeProtoProxy",
        "iteratorPrototypeProto", "iteratorPrototypeProtoReflect", "iteratorPrototypeProtoAssigned",
    ];

    // The routes that reach an iterator a collection creates, which the script never holds.
    private static readonly string[] PrototypeRoutes =
    [
        "kindPrototype", "kindPrototypeAccessor", "iteratorPrototype", "iteratorPrototypeAccessor", "objectPrototype", "objectPrototypeAssigned",
        "kindPrototypeProto", "kindPrototypeProtoProxy", "iteratorPrototypeProto",
    ];

    // What each consumer evaluates to over a two-element iterable.
    private static readonly Dictionary<string, string> Outcomes = new(StringComparer.Ordinal)
    {
        ["forOfBreak"] = "done",
        ["forOfReturn"] = "R",
        ["forOfThrow"] = "caught:E",
        ["forOfContinueOuter"] = "2",
        ["generatorBreak"] = "y",
        ["generatorReturn"] = "R",
        ["asyncBreak"] = "done",
        ["asyncThrow"] = "caught:E",
        ["destructureBinding"] = "bound",
        ["destructureAssignment"] = "assigned",
        ["destructureParameter"] = "parameter",
        ["destructureEmpty"] = "empty",
        ["parameterEmpty"] = "parameterEmpty",
        ["destructureInGenerator"] = "",
        ["arrayFromThrows"] = "caught:M",
        ["yieldStarThrow"] = "TypeError",
        ["forAwaitBreak"] = "done",
        ["asyncYieldStarThrow"] = "TypeError",
        ["asyncIteratorBreak"] = "done",
        ["asyncIteratorThrow"] = "caught:E",
        ["runToTheEnd"] = "2",
        ["spread"] = "2",
        ["destructureToTheEnd"] = "undefined",
        ["parameterToTheEnd"] = "undefined",
    };

    // The consumers that never close: the iterator runs out, or nothing closes at all.
    private static readonly HashSet<string> NeverClose = new(StringComparer.Ordinal)
    {
        "runToTheEnd", "spread", "destructureToTheEnd", "parameterToTheEnd",
    };

    private static readonly string[] Loops = ["forOfBreak", "generatorBreak", "asyncBreak"];

    /// <summary>
    /// What Node answers: a closing consumer calls <c>return</c> once per close, on the iterator, with no argument, and
    /// reads an accessor holding it once per close; a consumer that does not close calls nothing.
    /// </summary>
    private static string Expected(string consumer, string route, string receiver)
    {
        var outcome = Outcomes[consumer];
        if (NeverClose.Contains(consumer))
        {
            return outcome + " 0 - - 0";
        }

        var closes = string.Equals(consumer, "forOfContinueOuter", StringComparison.Ordinal) ? 2 : 1;
        var reads = route.EndsWith("Accessor", StringComparison.Ordinal) ? closes : 0;
        return $"{outcome} {closes} {receiver} 0 {reads}";
    }

    public static TestCases<string, string, string, string> RouteRows
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var kind in Kinds)
            {
                foreach (var route in Routes)
                {
                    foreach (var loop in Loops)
                    {
                        rows.Add(kind, route, loop, Expected(loop, route, "it"));
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// A <c>for…of</c> left by <c>break</c> calls the <c>return</c> every route makes resolve, after a first loop over
    /// the same kind closed an untouched iterator - in a plain function, in a generator that yields before the
    /// <c>break</c> and in an async function that awaits before it.
    /// </summary>
    [TestCaseSource(nameof(RouteRows))]
    public void ALoopLeftEarlyCallsTheReturnEveryRouteAdds(string kind, string route, string loop, string expected)
    {
        Run(NewEngine(), $"closed(function () {{ return {kind}; }}, '{route}', '{loop}', false)").Should().Be(expected);
    }

    /// <summary>
    /// The same rows on an engine whose built-in iterator chains were touched before the first loop, so that what the
    /// route has to invalidate is the version-checked lookup's cached answer (<see cref="TouchedEngine"/>).
    /// </summary>
    [TestCaseSource(nameof(RouteRows))]
    public void ALoopLeftEarlyCallsTheReturnEveryRouteAddsOnceTheChainsWereTouched(string kind, string route, string loop, string expected)
    {
        Run(TouchedEngine(), $"closed(function () {{ return {kind}; }}, '{route}', '{loop}', false)").Should().Be(expected);
    }

    public static TestCases<string, string, string> ConsumerRows
    {
        get
        {
            var rows = new TestCases<string, string, string>();
            foreach (var kind in Kinds)
            {
                foreach (var consumer in Outcomes.Keys)
                {
                    var expected = Expected(consumer, "kindPrototype", "it");
                    if (string.Equals(consumer, "forOfContinueOuter", StringComparison.Ordinal)
                        && (kind.StartsWith("list", StringComparison.Ordinal) || kind.StartsWith("dictionary", StringComparison.Ordinal)))
                    {
                        // A CLR collection's iterator disposes its enumerator when it is closed, after the `return`, and
                        // answers done from then on: the outer loop's second pass finds nothing to step, and nothing to close.
                        expected = "2 1 it 0 0";
                    }

                    rows.Add(kind, consumer, expected);
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// Every consumer that leaves an iterator before it is done closes it, and calls a <c>return</c> on the kind's
    /// prototype: the <c>for…of</c> exits, a generator's <c>return()</c> during a loop, the destructuring forms (an
    /// empty pattern closes too), <c>Array.from</c> with a throwing mapper, <c>yield*</c> handed a <c>throw()</c> its
    /// delegate cannot take, in a sync and an async generator, a <c>for await</c> through the async-from-sync wrapper,
    /// and one whose <c>@@asyncIterator</c> answers the built-in iterator itself. Running out, and spreading, close
    /// nothing.
    /// </summary>
    [TestCaseSource(nameof(ConsumerRows))]
    public void EveryConsumerThatLeavesEarlyCallsAReturnOnTheKindsPrototype(string kind, string consumer, string expected)
    {
        Run(NewEngine(), $"closed(function () {{ return {kind}; }}, 'kindPrototype', '{consumer}', false)").Should().Be(expected);
    }

    public static TestCases<string, string, string> ArrayIteratorRows
    {
        get
        {
            var rows = new TestCases<string, string, string>();
            foreach (var route in Routes)
            {
                foreach (var consumer in Outcomes.Keys)
                {
                    rows.Add(route, consumer, Expected(consumer, route, "it"));
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// Every route against every consumer, for an array iterator.
    /// </summary>
    [TestCaseSource(nameof(ArrayIteratorRows))]
    public void EveryConsumerCallsAReturnOnAnArrayIteratorWhicheverRouteAddsIt(string route, string consumer, string expected)
    {
        Run(NewEngine(), $"closed(function () {{ return [1, 2].values(); }}, '{route}', '{consumer}', false)").Should().Be(expected);
    }

    public static TestCases<string, string, string, string> SourceRows
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var (source, tag) in Sources)
            {
                foreach (var consumer in Outcomes.Keys)
                {
                    rows.Add(source, "iteratorPrototype", consumer, Expected(consumer, "iteratorPrototype", tag));
                }

                foreach (var route in PrototypeRoutes)
                {
                    foreach (var consumer in new[] { "forOfBreak", "destructureBinding", "destructureParameter", "parameterEmpty" })
                    {
                        rows.Add(source, route, consumer, Expected(consumer, route, tag));
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// A consumer handed the collection rather than an iterator creates the iterator itself, and closes it as it
    /// closes one it was handed. For an array, a typed array and a CLR list, destructuring reads elements by index
    /// in place of that iterator, which it may do only while the iterator would have no <c>return</c> to call; for a
    /// string primitive the iterator is the native one its lane builds, which inherits
    /// <c>%StringIteratorPrototype%</c> as the specified object does.
    /// </summary>
    [TestCaseSource(nameof(SourceRows))]
    public void AConsumerOfACollectionCallsAReturnOnTheIteratorItCreated(string source, string route, string consumer, string expected)
    {
        Run(NewEngine(), $"closed(function () {{ return {source}; }}, '{route}', '{consumer}', true)").Should().Be(expected);
    }

    /// <summary>
    /// The same rows on an engine whose built-in iterator chains were touched first (<see cref="TouchedEngine"/>), where
    /// the destructuring lanes ask the version-checked lookup whether they may read by index.
    /// </summary>
    [TestCaseSource(nameof(SourceRows))]
    public void AConsumerOfACollectionCallsAReturnOnTheIteratorItCreatedOnceTheChainsWereTouched(string source, string route, string consumer, string expected)
    {
        Run(TouchedEngine(), $"closed(function () {{ return {source}; }}, '{route}', '{consumer}', true)").Should().Be(expected);
    }

    /// <summary>
    /// IteratorClose steps 4-7 for a <c>return</c> that misbehaves. A loop left by <c>break</c> propagates what the
    /// <c>return</c> lookup or call throws, and a <c>TypeError</c> for a <c>return</c> that is not callable or answers a
    /// non-object; a loop left by a throw keeps its own throw whatever the close did (step 5). <c>null</c> and
    /// <c>undefined</c> are no method at all.
    /// </summary>
    [TestCase("P.return = function () { throw 'X'; }", "threw:X", "caught:E")]
    [TestCase("P.return = function () { return 1; }", "threw:TypeError", "caught:E")]
    [TestCase("P.return = 1", "threw:TypeError", "caught:E")]
    [TestCase("P.return = null", "done", "caught:E")]
    [TestCase("P.return = undefined", "done", "caught:E")]
    [TestCase("Object.defineProperty(P, 'return', { get() { throw 'G'; }, configurable: true })", "threw:G", "caught:E")]
    public void AMisbehavingReturnIsHandledAsIteratorCloseSays(string install, string onBreak, string onThrow)
    {
        foreach (var source in new[] { "[1, 2]", "[1, 2].values()", "new Map([[1, 2]]).keys()", "'ab'", "list" })
        {
            var script = $$"""
                const make = function () { return {{source}}; };
                const P = Object.getPrototypeOf(make()[Symbol.iterator]());
                function brk() { try { for (const x of make()) { break; } return 'done'; } catch (e) { return 'threw:' + (typeof e === 'string' ? e : e.constructor.name); } }
                function thr() { try { for (const x of make()) { throw 'E'; } } catch (e) { return 'caught:' + e; } }
                brk();
                {{install}};
                try { brk() + ' ' + thr(); } finally { delete P.return; }
                """;
            NewEngine().Evaluate(script).AsString().Should().Be(onBreak + " " + onThrow, source);
        }
    }

    /// <summary>
    /// A for-in loop's iterator is never closed (ForIn/OfBodyEvaluation returns before IteratorClose for the
    /// enumerate kind), whatever <c>%ArrayIteratorPrototype%</c>, whose instance Jint's for-in iterator is, carries.
    /// </summary>
    [Test]
    public void AForInLoopLeftEarlyCallsNoReturn()
    {
        NewEngine().Evaluate("""
            const AIP = Object.getPrototypeOf([].values());
            let calls = 0;
            for (const k in { a: 1, b: 2 }) { break; }
            AIP.return = function () { calls++; return {}; };
            Object.prototype.return = AIP.return;
            try { for (const k in { a: 1, b: 2 }) { break; } (function () { for (const k in { a: 1 }) { return; } })(); }
            finally { delete AIP.return; delete Object.prototype.return; }
            calls;
            """).AsNumber().Should().Be(0);
    }

    /// <summary>
    /// The native iterator a string primitive's lane builds stands in for a %StringIteratorPrototype% object
    /// (https://tc39.es/ecma262/#sec-createstringiterator), so its close finds a <c>return</c> on that prototype and
    /// never one on <c>%ArrayIteratorPrototype%</c>.
    /// </summary>
    [Test]
    public void AStringPrimitivesIteratorInheritsTheStringIteratorPrototype()
    {
        NewEngine().Evaluate("""
            const AIP = Object.getPrototypeOf([].values());
            const SIP = Object.getPrototypeOf(''[Symbol.iterator]());
            const log = [];
            AIP.return = function () { log.push('array'); return {}; };
            for (const c of 'ab') { break; }
            const [a] = 'ab';
            delete AIP.return;
            SIP.return = function () { log.push(Object.prototype.toString.call(this)); return {}; };
            for (const c of 'ab') { break; }
            const [b] = 'ab';
            delete SIP.return;
            log.join();
            """).AsString().Should().Be("[object String Iterator],[object String Iterator]");
    }

    /// <summary>
    /// Adding, deleting and re-adding a <c>return</c> on two levels of the chain, closing an array iterator after each
    /// change: every change is seen by the next close.
    /// </summary>
    [Test]
    public void EachChangeToTheChainIsSeenByTheNextClose()
    {
        NewEngine().Evaluate("""
            const AIP = Object.getPrototypeOf([].values());
            const IP = Object.getPrototypeOf(AIP);
            const log = [];
            function brk() { for (const x of [1, 2]) { break; } }
            brk();
            AIP.return = function () { log.push('A'); return {}; };
            brk();
            delete AIP.return;
            brk();
            IP.return = function () { log.push('I'); return {}; };
            brk();
            AIP.return = function () { log.push('A2'); return {}; };
            brk();
            delete AIP.return;
            brk();
            delete IP.return;
            brk();
            Object.defineProperty(Object.prototype, 'return', { value: function () { log.push('O'); return {}; }, configurable: true });
            brk();
            delete Object.prototype.return;
            brk();
            log.join();
            """).AsString().Should().Be("A,I,A2,I,O");
    }

    /// <summary>
    /// A <c>return</c> that resolves to <c>undefined</c> - here an accessor on the iterator answering it - is read once
    /// per close and calls nothing, whether the record is the iterator itself or, with <c>next</c> replaced, the
    /// object record around it: the native close step 4.b leaves to the built-in iterator does not read it again.
    /// </summary>
    [TestCase("[1, 2].values()", false)]
    [TestCase("[1, 2].values()", true)]
    [TestCase("list[Symbol.iterator]()", false)]
    [TestCase("list[Symbol.iterator]()", true)]
    public void AReturnResolvingToUndefinedIsReadOncePerClose(string kind, bool replaceNext)
    {
        Run(NewEngine(), $$"""
            (async function () {
                const ways = {
                    forOfBreak: async function (it) { for (const x of it) { break; } },
                    destructureBinding: async function (it) { const [a] = it; },
                    parameterEmpty: async function (it) { (function ([]) { })(it); },
                    forAwaitBreak: async function (it) { for await (const x of it) { break; } },
                    asyncIteratorBreak: async function (it) { for await (const x of { [Symbol.asyncIterator]() { return it; } }) { break; } }
                };
                const out = [];
                for (const way in ways) {
                    const it = {{kind}};
                    let reads = 0;
                    if ({{(replaceNext ? "true" : "false")}}) { const original = it.next; it.next = function () { return original.call(this); }; }
                    Object.defineProperty(it, 'return', { get() { reads++; return undefined; }, configurable: true });
                    await ways[way](it);
                    out.push(way + '=' + reads);
                }
                return out.join(' ');
            })()
            """).Should().Be("forOfBreak=1 destructureBinding=1 parameterEmpty=1 forAwaitBreak=1 asyncIteratorBreak=1");
    }

    /// <summary>
    /// A host object on the chain whose own properties are its own business - it answers <c>return</c> from native
    /// state, which no own-property version witnesses - is asked on every close, so a <c>return</c> it starts or stops
    /// answering is seen by the next one. It has no Node counterpart.
    /// </summary>
    [Test]
    public void AHostObjectOnTheChainIsAskedOnEveryClose()
    {
        var engine = NewEngine();
        var holder = new SwitchableReturn(engine);
        engine.SetValue("holder", holder);
        engine.Execute("""
            var AIP = Object.getPrototypeOf([].values());
            var IP = Object.getPrototypeOf(AIP);
            var log = [];
            function brk() { for (const x of [1, 2]) { break; } }
            Object.setPrototypeOf(holder, Object.prototype);
            Object.setPrototypeOf(AIP, holder);
            brk();
            """);

        holder.Method = engine.Evaluate("(function () { log.push(Object.prototype.toString.call(this)); return {}; })");
        engine.Execute("brk();");
        holder.Method = null;
        engine.Execute("brk();");

        engine.Evaluate("Object.setPrototypeOf(AIP, IP); brk(); log.join()").AsString().Should().Be("[object Array Iterator]");
    }

    /// <summary>
    /// A host link on the chain is asked for <c>return</c> as often as GetMethod asks it - once per close - and never
    /// by the cached lookup too: a link whose version cannot witness the answer is refused on its type flags alone,
    /// before anything is asked of it. That holds for a loop's close and for the array destructuring lanes, which ask
    /// the lookup before they decide to read by index.
    /// </summary>
    [Test]
    public void AHostLinkIsAskedForReturnOncePerClose()
    {
        var engine = NewEngine();
        var holder = new CountingReturnProbe(engine);
        engine.SetValue("holder", holder);
        engine.Execute("""
            var AIP = Object.getPrototypeOf([].values());
            var IP = Object.getPrototypeOf(AIP);
            Object.setPrototypeOf(holder, Object.prototype);
            Object.setPrototypeOf(AIP, holder);
            """);

        try
        {
            foreach (var way in new[] { "for (const x of [1, 2]) { break; }", "const [a] = [1, 2];", "let b; [b] = [1, 2];", "(function ([c]) { })([1, 2]);" })
            {
                holder.Probes = 0;
                engine.Execute(way);
                holder.Probes.Should().Be(1, way);
            }
        }
        finally
        {
            engine.Execute("Object.setPrototypeOf(AIP, IP);");
        }
    }

    // Counts the reads of its own `return`, which it does not have.
    private sealed class CountingReturnProbe(Engine engine) : Jint.Native.Object.ObjectInstance(engine)
    {
        public int Probes;

        public override Jint.Runtime.Descriptors.PropertyDescriptor GetOwnProperty(Jint.Native.JsValue property)
        {
            if (property.IsString() && string.Equals(property.ToString(), "return", StringComparison.Ordinal))
            {
                Probes++;
            }

            return base.GetOwnProperty(property);
        }
    }

    /// <summary>
    /// An array pattern reads <c>@@iterator</c> once, as GetIterator does, when an accessor on <c>Array.prototype</c>
    /// answers the original <c>values</c> and a <c>return</c> on the chain sends the pattern to GetIterator: the
    /// lane asks whether there is a <c>return</c> before it reads <c>@@iterator</c> to decide whether it may read by
    /// index.
    /// </summary>
    [Test]
    public void AnArrayPatternReadsSymbolIteratorOnceWhenTheChainHasAReturn()
    {
        NewEngine().Evaluate("""
            const AIP = Object.getPrototypeOf([].values());
            const values = Array.prototype[Symbol.iterator];
            const out = [];
            let reads = 0, calls = 0;
            Object.defineProperty(Array.prototype, Symbol.iterator, { get() { reads++; return values; }, configurable: true });
            AIP.return = function () { calls++; return {}; };
            try {
                const [a] = [1, 2];
                out.push('binding=' + reads + '/' + calls);
                reads = 0; calls = 0;
                let b; [b] = [1, 2];
                out.push('assignment=' + reads + '/' + calls);
                reads = 0; calls = 0;
                (function ([c]) { })([1, 2]);
                out.push('parameter=' + reads + '/' + calls);
            } finally {
                delete AIP.return;
                Object.defineProperty(Array.prototype, Symbol.iterator, { value: values, writable: true, configurable: true });
            }
            out.join(' ');
            """).AsString().Should().Be("binding=1/1 assignment=1/1 parameter=1/1");
    }

    /// <summary>
    /// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization">IteratorBindingInitialization</a>
    /// for a parameter's array pattern: each element steps the iterator and then evaluates its initializer, an elision
    /// steps too, and only once every element is bound is the iterator closed - or, when an initializer throws, closed
    /// with that throw before it propagates. Jint used to step every element first and close before any initializer
    /// ran. Every expected value is Node 26's.
    /// </summary>
    [TestCase("(function ([a = log.push('init a'), , b = log.push('init b')]) { })(iterable)", "next1,init a,next2,next3,init b,return")]
    [TestCase("try { (function ([a = (() => { log.push('init a'); throw 'D'; })(), b = log.push('init b')]) { })(iterable); } catch (e) { log.push('caught:' + e); }", "next1,init a,return,caught:D")]
    [TestCase("AIP.return = function () { log.push('return'); return {}; }; try { (function ([a = log.push('init a')]) { })([undefined, 2]); } finally { delete AIP.return; }", "init a,return")]
    public void AParameterPatternClosesItsIteratorAfterItsInitializers(string call, string expected)
    {
        NewEngine().Evaluate($$"""
            const AIP = Object.getPrototypeOf([].values());
            const log = [];
            const iterable = { [Symbol.iterator]() { let n = 0; return { next() { log.push('next' + (++n)); return { value: undefined, done: false }; }, return() { log.push('return'); return {}; } }; } };
            {{call}};
            log.join();
            """).AsString().Should().Be(expected);
    }

    /// <summary>
    /// A parameter's array pattern that runs its iterator out leaves it unclosed, with or without a rest element, and
    /// one that stops early - an empty pattern included - closes it. Jint closed an iterator the elements had run out,
    /// calling its <c>return</c> after it had answered done, and never closed for an empty pattern. Node 26 answers the
    /// same.
    /// </summary>
    [Test]
    public void AParameterPatternClosesOnlyAnIteratorItDidNotRunOut()
    {
        NewEngine().Evaluate("""
            let calls = 0;
            const iterable = { [Symbol.iterator]() { let n = 0; return { next() { n++; return { value: n, done: n > 2 }; }, return() { calls++; return {}; } }; } };
            const out = [];
            (function ([a, b, c]) { })(iterable);
            (function ([a, b, c, d]) { })(iterable);
            (function ([a, ...rest]) { })(iterable);
            out.push(calls);
            (function ([a]) { })(iterable);
            (function ([]) { })(iterable);
            out.push(calls);
            out.join();
            """).AsString().Should().Be("0,2");
    }

    /// <summary>
    /// <a href="https://tc39.es/ecma262/#sec-math.sumprecise">Math.sumPrecise</a> closes the iterator only for its own
    /// errors - here the <c>TypeError</c> for a value that is not a Number - and not one it ran out, nor one whose step
    /// threw. With a <c>return</c> on <c>%ArrayIteratorPrototype%</c>, closing a plain array's iterator after the end
    /// is observable. Node 26 has no <c>Math.sumPrecise</c>; the expected values are the algorithm's.
    /// </summary>
    [TestCase("String(Math.sumPrecise([1, 2]))", "3 0")]
    [TestCase("(function () { try { Math.sumPrecise([1, 'x']); } catch (e) { return e.constructor.name; } })()", "TypeError 1")]
    [TestCase("(function () { const it = { [Symbol.iterator]() { return this; }, n: 0, next() { if (++this.n > 1) { throw 'S'; } return { value: 1, done: false }; }, return() { calls++; return {}; } }; try { Math.sumPrecise(it); } catch (e) { return 'caught:' + e; } })()", "caught:S 0")]
    public void MathSumPreciseClosesOnlyForItsOwnErrors(string call, string expected)
    {
        NewEngine().Evaluate($$"""
            const AIP = Object.getPrototypeOf([].values());
            var calls = 0;
            AIP.return = function () { calls++; return {}; };
            let outcome;
            try { outcome = {{call}}; } finally { delete AIP.return; }
            outcome + ' ' + calls;
            """).AsString().Should().Be(expected);
    }

    // Answers an own `return` from a field the engine never sees change.
    private sealed class SwitchableReturn(Engine engine) : Jint.Native.Object.ObjectInstance(engine)
    {
        public Jint.Native.JsValue? Method;

        public override Jint.Runtime.Descriptors.PropertyDescriptor GetOwnProperty(Jint.Native.JsValue property)
            => Method is not null && property.IsString() && string.Equals(property.ToString(), "return", StringComparison.Ordinal)
                ? new Jint.Runtime.Descriptors.PropertyDescriptor(Method, Jint.Runtime.Descriptors.PropertyFlag.ConfigurableEnumerableWritable)
                : base.GetOwnProperty(property);
    }

    /// <summary>
    /// A chain whose third link is an object with no prototype is watched at its end as well: giving that object a
    /// prototype that has a <c>return</c> is seen by the next close, as is taking it away again.
    /// </summary>
    [Test]
    public void AChainEndingInAnObjectWithoutAPrototypeIsWatchedAtItsEnd()
    {
        NewEngine().Evaluate("""
            const AIP = Object.getPrototypeOf([].values());
            const IP = Object.getPrototypeOf(AIP);
            const OP = Object.prototype;
            const log = [];
            function brk() { for (const x of [1, 2]) { break; } }
            const X = Object.create(null);
            Object.setPrototypeOf(IP, X);
            try {
                brk();
                Object.setPrototypeOf(X, { return() { log.push('X'); return {}; } });
                brk();
                Object.setPrototypeOf(X, null);
                brk();
                X.return = function () { log.push('X2'); return {}; };
                brk();
            } finally { Object.setPrototypeOf(IP, OP); }
            brk();
            log.join();
            """).AsString().Should().Be("X,X2");
    }

    /// <summary>
    /// <a href="https://tc39.es/ecma262/#sec-asyncfromsynciteratorcontinuation">AsyncFromSyncIteratorContinuation</a>
    /// with closeOnRejection: an element that is a rejected promise closes the sync iterator before the rejection
    /// reaches the loop - for a plain array under <c>for await</c>, and for <c>yield*</c> in an async generator.
    /// </summary>
    [TestCase("(async function () { for await (const x of [Promise.reject('R'), 2]) { } })()")]
    [TestCase("(async function () { for await (const x of [Promise.reject('R'), 2].values()) { } })()")]
    [TestCase("(async function () { async function* g() { yield* [Promise.reject('R'), 2]; } for await (const x of g()) { } })()")]
    [TestCase("(async function () { for await (const x of list) { } })()")]
    public void ARejectedElementUnderForAwaitClosesTheSyncIterator(string loop)
    {
        var engine = NewEngine();
        engine.SetValue("list", new List<object> { engine.Evaluate("Promise.reject('R')"), 2 });
        engine.Evaluate($$"""
            (async function () {
                const AIP = Object.getPrototypeOf([].values());
                let calls = 0;
                await (async function () { for await (const x of [1, 2]) { } })();
                AIP.return = function () { calls++; return {}; };
                try { await {{loop}}; return 'no throw'; }
                catch (e) { return 'caught:' + e + ' ' + calls; }
                finally { delete AIP.return; }
            })()
            """).UnwrapIfPromise().AsString().Should().Be("caught:R 1");
    }

    /// <summary>
    /// The guard that skips the <c>return</c> lookup answers without any lookup for every built-in kind on an engine
    /// whose chains nobody touched - <see cref="Engine._builtinIteratorChainMayHaveReturn"/> stays clear and nothing is
    /// cached - and once the script has touched them and put them back, its version-checked lookup arms and answers from
    /// the cache. So the lookup really is skipped, rather than merely answering the same.
    /// </summary>
    [TestCase("[1, 2].values()")]
    [TestCase("new Uint8Array([1, 2]).values()")]
    [TestCase("new Map([[1, 'a']]).entries()")]
    [TestCase("new Set([1, 2]).values()")]
    [TestCase("'ab'[Symbol.iterator]()")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()")]
    [TestCase("list[Symbol.iterator]()")]
    [TestCase("dictionary[Symbol.iterator]()")]
    public void TheReturnLookupIsSkippedForAnUntouchedKindAndAgainOnceTheChainIsRestored(string iterator)
    {
        var engine = NewEngine();
        if (engine.Evaluate(iterator) is not IteratorInstance instance || instance.Prototype is not IteratorPrototype prototype)
        {
            Assert.Fail(iterator + " is not a built-in iterator instance");
            return;
        }

        engine.Execute("for (const x of [1, 2]) { break; } const [a] = [1, 2]; (function ([b]) { })(new Set([1]));");
        prototype.InstancesInheritNoReturnMethod().Should().BeTrue();
        engine._builtinIteratorChainMayHaveReturn.Should().BeFalse();
        prototype.CachesReturnLookup.Should().BeFalse();

        engine.SetValue("it", instance);
        engine.Execute("Object.getPrototypeOf(Object.getPrototypeOf(it)).return = function () { return {}; };");
        engine._builtinIteratorChainMayHaveReturn.Should().BeTrue();
        prototype.InstancesInheritNoReturnMethod().Should().BeFalse();

        engine.Execute("delete Object.getPrototypeOf(Object.getPrototypeOf(it)).return;");
        prototype.InstancesInheritNoReturnMethod().Should().BeTrue();
        prototype.CachesReturnLookup.Should().BeTrue();
    }

    /// <summary>
    /// A realm of its own - a ShadowRealm's - has iterator prototypes and an <c>Object.prototype</c> of its own, and a
    /// <c>return</c> put on one of them reaches every close of that realm's iterators: in a loop, in a destructuring
    /// and in a parameter pattern, after the same consumers ran there untouched. The principal realm, whose chains
    /// stay untouched, still finds none.
    /// </summary>
    [TestCase("Object.getPrototypeOf([].values())")]
    [TestCase("Object.getPrototypeOf(Object.getPrototypeOf([].values()))")]
    [TestCase("Object.prototype")]
    public void AReturnAddedInAnotherRealmIsCalledByThatRealmsCloses(string holder)
    {
        NewEngine().Evaluate($$"""
            const realm = new ShadowRealm();
            const ways = "for (const x of [1, 2]) { break; } const [a] = [1, 2]; (function ([b]) { })([1, 2]);";
            realm.evaluate(ways);
            realm.evaluate("var calls = 0; Object.defineProperty({{holder}}, 'return', { value: function () { calls++; return {}; }, configurable: true, writable: true }); undefined");
            realm.evaluate(ways);
            const AIP = Object.getPrototypeOf([].values());
            for (const x of [1, 2]) { break; }
            const [c] = [1, 2];
            (realm.evaluate('calls') + ' ' + ('return' in AIP));
            """).AsString().Should().Be("3 false");
    }

    /// <summary>
    /// Iterating, closing and destructuring every kind with every consumer, and giving the iterator prototypes and
    /// <c>Object.prototype</c> properties of other names - what a polyfill does - leave
    /// <see cref="Engine._builtinIteratorChainMayHaveReturn"/> clear, so none of it costs the cheap lane. A <c>return</c>
    /// on an iterator instance does not set it either: an instance with an own property never takes that lane.
    /// </summary>
    [Test]
    public void OrdinaryIterationAndUnrelatedPrototypePropertiesLeaveTheFlagClear()
    {
        var engine = NewEngine();
        engine.Evaluate(Harness + """

            (async function () {
                const kinds = [
                    () => [1, 2].values(), () => new Uint8Array([1, 2]).values(), () => new Map([[1, 'a'], [2, 'b']]).entries(),
                    () => new Set([1, 2]).values(), () => 'ab'[Symbol.iterator](), () => 'a1b2'.matchAll(/[0-9]/g),
                    () => list[Symbol.iterator](), () => [1, 2], () => 'ab', () => list
                ];
                for (const make of kinds) {
                    for (const consumer in consumers) {
                        await consumers[consumer](make());
                    }
                }
                const AIP = Object.getPrototypeOf([].values());
                AIP.polyfilled = 1;
                Object.getPrototypeOf(AIP).polyfilled = function () { };
                Object.defineProperty(Object.prototype, 'polyfilled', { value: 1, configurable: true });
                delete Object.prototype.polyfilled;
                const it = [1, 2].values();
                it.return = function () { return {}; };
                for (const x of it) { break; }
            })()
            """).UnwrapIfPromise();

        engine._builtinIteratorChainMayHaveReturn.Should().BeFalse();
    }

#if NET8_0_OR_GREATER
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Fetch | WebApiFeatures.Files | WebApiFeatures.Url));

    private static readonly string[] WebKinds =
    [
        "new Headers([['a', '1'], ['b', '2']]).entries()",
        "(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()",
        "new URLSearchParams('a=1&b=2').values()",
    ];

    public static TestCases<string, string, string, string> WebRows
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var kind in WebKinds)
            {
                foreach (var route in Routes)
                {
                    rows.Add(kind, route, "forOfBreak", Expected("forOfBreak", route, "it"));
                }

                foreach (var consumer in Outcomes.Keys)
                {
                    rows.Add(kind, "kindPrototype", consumer, Expected(consumer, "kindPrototype", "it"));
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// The <c>entryTypes</c> sequence conversion of <c>PerformanceObserver.prototype.observe</c> closes its iterator only
    /// when a conversion throws, as the other sequence conversions do, and not once it has run out - which a script
    /// <c>return</c> on <c>%ArrayIteratorPrototype%</c> would see for a plain array.
    /// <para>
    /// The <c>caught:S 1</c> row pins this repository's WebApi convention, which closes the iterator when a conversion
    /// throws, and not literal WebIDL: <a href="https://webidl.spec.whatwg.org/#js-sequence">creating a sequence from an
    /// iterable</a> closes nothing, so a literal reading would answer <c>caught:S 0</c>. Which of the two the WebApi
    /// conversions should follow is a separate decision, outside sebastienros/jint#4406.
    /// </para>
    /// </summary>
    [TestCase("new PerformanceObserver(function () { }).observe({ entryTypes: ['mark'] }); 'observed'", "observed 0")]
    [TestCase("try { new PerformanceObserver(function () { }).observe({ entryTypes: [{ toString() { throw 'S'; } }] }); 'observed'; } catch (e) { 'caught:' + e; }", "caught:S 1")]
    public void APerformanceObserversEntryTypesAreClosedOnlyWhenAConversionThrows(string call, string expected)
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));
        engine.Evaluate($$"""
            const AIP = Object.getPrototypeOf([].values());
            let calls = 0;
            AIP.return = function () { calls++; return {}; };
            let outcome;
            try { outcome = eval({{System.Text.Json.JsonSerializer.Serialize(call)}}); } finally { delete AIP.return; }
            outcome + ' ' + calls;
            """).AsString().Should().Be(expected);
    }

    /// <summary>
    /// The WebIDL iterators of <c>Headers</c>, <c>FormData</c> and <c>URLSearchParams</c>, whose prototypes are
    /// built-in iterator prototypes too.
    /// </summary>
    [TestCaseSource(nameof(WebRows))]
    public void AWebApiIteratorLeftEarlyCallsItsReturn(string kind, string route, string consumer, string expected)
    {
        Run(WebEngine(), $"closed(function () {{ return {kind}; }}, '{route}', '{consumer}', false)").Should().Be(expected);
    }
#endif
}
