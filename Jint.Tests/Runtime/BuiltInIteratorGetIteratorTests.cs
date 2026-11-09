#nullable enable

using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins <a href="https://tc39.es/ecma262/#sec-getiterator">GetIterator</a> for the two values Jint answered without
/// it (sebastienros/jint#4405).
/// <para>
/// <b>A built-in iterator object as a <c>for…of</c> operand.</b> GetIterator reads <c>@@iterator</c> off the
/// operand and calls it, and GetIteratorFromMethod (https://tc39.es/ecma262/#sec-getiteratorfrommethod) reads
/// <c>next</c> off what that answered. The sync loop adopted an <see cref="IteratorInstance"/> as its iterator
/// record without either read, so an own or inherited <c>@@iterator</c> that is not
/// <c>%Iterator.prototype%[@@iterator]</c> (https://tc39.es/ecma262/#sec-iterator.prototype-%symbol.iterator%) was
/// never called, a missing or unusable one never threw, and an own or replaced <c>next</c> was never called either.
/// </para>
/// <para>
/// <b>A string primitive.</b> Its lane built the native string iterator whenever <c>@@iterator</c> was
/// <c>String.prototype</c>'s own, without asking whether <c>%StringIteratorPrototype%.next</c>
/// (https://tc39.es/ecma262/#sec-%stringiteratorprototype%.next) still was — so spread, <c>for…of</c>,
/// <c>Array.from</c>, <c>new Set</c> and <c>yield*</c> over <c>'ab'</c> ignored a replacement.
/// </para>
/// <para>
/// Every expected value is Node 26's. The harness is the one those values were taken with; a generator is stepped
/// with <c>next()</c> alone, since spreading it would read the very <c>@@iterator</c> some rows replace.
/// </para>
/// </summary>
public class BuiltInIteratorGetIteratorTests
{
    private const string Harness = """
        // A next() answering R1, R2 and then done, counting its calls.
        function replacement(calls) {
            let n = 0;
            return function () { calls.count++; n++; return n <= 2 ? { value: 'R' + n, done: false } : { value: undefined, done: true }; };
        }

        function forOf(iterable) { const out = []; for (const x of iterable) { out.push(x); } return out.join(','); }

        // Steps a generator through next() alone, so that nothing but the loop under test reads an @@iterator.
        function drain(gen) { const out = []; for (let r = gen.next(); !r.done; r = gen.next()) { out.push(r.value); } return out.join(','); }

        // The same loop in a generator, whose body suspends at every element, and in an async function, whose body awaits.
        function forOfInGenerator(iterable) { return drain((function* () { for (const x of iterable) { yield x; } })()); }
        async function forOfInAsync(iterable) { const out = []; for (const x of iterable) { out.push(await x); } return out.join(','); }

        const loops = { forOf: forOf, generator: forOfInGenerator, async: forOfInAsync };

        // Makes @@iterator resolve, for `it` (a fresh iterator), to `method`, and answers the function that puts every object
        // back. `reads` counts the reads of an accessor route.
        function applyIteratorRoute(route, it, method, reads) {
            const P = Object.getPrototypeOf(it);
            const IP = Object.getPrototypeOf(P);
            const saved = [];
            function save(o, k) { saved.push([o, k, Object.getOwnPropertyDescriptor(o, k)]); }
            const counted = { get() { reads.count++; return method; }, configurable: true };
            switch (route) {
                case 'own': it[Symbol.iterator] = method; break;
                case 'ownAccessor': Object.defineProperty(it, Symbol.iterator, counted); break;
                case 'kindPrototype': save(P, Symbol.iterator); P[Symbol.iterator] = method; break;
                case 'kindPrototypeAccessor': save(P, Symbol.iterator); Object.defineProperty(P, Symbol.iterator, counted); break;
                case 'iteratorPrototype': save(IP, Symbol.iterator); IP[Symbol.iterator] = method; break;
                case 'iteratorPrototypeData': save(IP, Symbol.iterator); Object.defineProperty(IP, Symbol.iterator, { value: method }); break;
                case 'iteratorPrototypeAccessor': save(IP, Symbol.iterator); Object.defineProperty(IP, Symbol.iterator, counted); break;
                case 'protoPlain': Object.setPrototypeOf(it, { __proto__: P, [Symbol.iterator]: method }); break;
                case 'kindPrototypeProto': saved.push([P, null, IP]); Object.setPrototypeOf(P, { __proto__: IP, [Symbol.iterator]: method }); break;
                case 'protoProxy': Object.setPrototypeOf(it, new Proxy(P, { get(t, k, r) { return k === Symbol.iterator ? method : Reflect.get(t, k, r); } })); break;
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

        // Iterates one iterator from make() first, so whatever the engine remembers about the kind is warm, then routes
        // @@iterator on a fresh one to a method answering ['X', 'Y'].values() and runs the loop over it. Answers
        // "<values> <method calls> <method saw the iterator as this> <reads>".
        async function ownIterable(make, route, loop) {
            forOf(make());
            const it = make();
            const calls = { count: 0 }, reads = { count: 0 };
            let receiver;
            const method = function () { calls.count++; receiver = this; return ['X', 'Y'].values(); };
            const restore = applyIteratorRoute(route, it, method, reads);
            try { return (await loops[loop](it)) + ' ' + calls.count + ' ' + (receiver === it) + ' ' + reads.count; }
            finally { restore(); }
        }

        // @@iterator on a fresh iterator from make() made to resolve to `value`: answers the error's constructor name, or
        // the values when nothing threw.
        function badIterable(make, value) {
            const it = make();
            Object.defineProperty(it, Symbol.iterator, { value: value, configurable: true });
            try { return forOf(it); } catch (e) { return e.constructor.name; }
        }

        // %Iterator.prototype%[@@iterator] deleted after a first loop warmed the kind, so a fresh iterator from make() is not
        // iterable any more.
        function deletedIterable(make) {
            forOf(make());
            const it = make();
            const IP = Object.getPrototypeOf(Object.getPrototypeOf(it));
            const saved = Object.getOwnPropertyDescriptor(IP, Symbol.iterator);
            delete IP[Symbol.iterator];
            try { return forOf(it); } catch (e) { return e.constructor.name; }
            finally { Object.defineProperty(IP, Symbol.iterator, saved); }
        }

        // Makes `next` resolve, for a fresh iterator from make(), to a replacement whose every call and read is counted, and
        // runs the loop over the iterator itself. Answers "<values> <calls> <reads>".
        async function replacedNext(make, route, loop) {
            forOf(make());
            const calls = { count: 0 };
            const next = replacement(calls);
            let reads = 0;
            const counted = { get() { reads++; return next; }, configurable: true };
            const it = make();
            const P = Object.getPrototypeOf(it);
            const saved = Object.getOwnPropertyDescriptor(P, 'next');
            switch (route) {
                case 'own': it.next = next; break;
                case 'ownAccessor': Object.defineProperty(it, 'next', counted); break;
                case 'prototype': P.next = next; break;
                case 'prototypeAccessor': Object.defineProperty(P, 'next', counted); break;
            }
            try { return (await loops[loop](it)) + ' ' + calls.count + ' ' + reads; }
            finally { Object.defineProperty(P, 'next', saved); }
        }

        // The consumers that take the string primitive's iterator lane.
        const stringConsumers = {
            forOf: function (s) { return forOf(s); },
            generator: function (s) { return forOfInGenerator(s); },
            async: function (s) { return forOfInAsync(s); },
            spread: function (s) { return [...s].join(','); },
            from: function (s) { return Array.from(s).join(','); },
            destructure: function (s) { const [a, b] = s; return a + ',' + b; },
            set: function (s) { return [...new Set(s)].join(','); },
            yieldStar: function (s) { return drain((function* () { yield* s; })()); }
        };

        // Makes %StringIteratorPrototype%.next resolve to a counted replacement - an assignment, a data redefinition, an
        // accessor, or a delete uncovering one on %Iterator.prototype% - after a first spread warmed the lane, runs the
        // consumer over the string primitive 'ab' and answers "<values> <calls> <reads>".
        async function replacedStringNext(route, consumer) {
            [...'warm'];
            const calls = { count: 0 };
            const next = replacement(calls);
            let reads = 0;
            const P = Object.getPrototypeOf(''[Symbol.iterator]());
            const IP = Object.getPrototypeOf(P);
            const saved = Object.getOwnPropertyDescriptor(P, 'next');
            const savedIP = Object.getOwnPropertyDescriptor(IP, 'next');
            switch (route) {
                case 'assign': P.next = next; break;
                case 'defineData': Object.defineProperty(P, 'next', { value: next }); break;
                case 'defineAccessor': Object.defineProperty(P, 'next', { get() { reads++; return next; }, configurable: true }); break;
                case 'delete': delete P.next; IP.next = next; break;
            }
            try { return (await stringConsumers[consumer]('ab')) + ' ' + calls.count + ' ' + reads; }
            finally {
                Object.defineProperty(P, 'next', saved);
                if (savedIP) { Object.defineProperty(IP, 'next', savedIP); } else { delete IP.next; }
            }
        }

        // A fresh iterator from make(), given an unrelated own property - a string key, a symbol key, or none - and iterated
        // by the loop. Nothing reroutes @@iterator or `next`, so the values are the iterator's own.
        async function untouched(make, extra, loop) {
            const it = make();
            if (extra === 'string') { it.unrelated = 1; } else if (extra === 'symbol') { it[Symbol('unrelated')] = 1; }
            return loops[loop](it);
        }
        """;

    private static string Run(Engine engine, string script) =>
        engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    private static Engine NewEngine()
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        return engine;
    }

    // Each built-in kind a for…of may be handed as its operand, and a wrapped CLR list's iterator, which inherits
    // %ArrayIteratorPrototype% and has no Node counterpart: its expected values are the ones the spec gives every
    // other kind.
    private static readonly string[] Kinds =
    [
        "[1, 2].values()",
        "[1, 2].keys()",
        "new Uint8Array([1, 2]).values()",
        "new Map([[1, 'a']]).entries()",
        "new Set([1, 2]).values()",
        "'ab'[Symbol.iterator]()",
        "'a1b2'.matchAll(/[0-9]/g)",
        "new Intl.Segmenter().segment('ab')[Symbol.iterator]()",
        "list[Symbol.iterator]()",
    ];

    private static readonly string[] Loops = ["forOf", "generator", "async"];

    // Every way of making @@iterator resolve to something other than %Iterator.prototype%[@@iterator] for a fresh
    // iterator: an own data or accessor property, one added to the kind's prototype, %Iterator.prototype%'s own
    // assigned (in place), redefined as data (in place) or as an accessor, the iterator's prototype swapped for a
    // plain object or a Proxy, and the kind's prototype's own prototype swapped for a plain object.
    private static readonly string[] IteratorRoutes =
    [
        "own", "ownAccessor", "kindPrototype", "kindPrototypeAccessor", "iteratorPrototype", "iteratorPrototypeData",
        "iteratorPrototypeAccessor", "protoPlain", "kindPrototypeProto", "protoProxy",
    ];

    public static TestCases<string, string, string, string> OwnIterableRows
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var kind in Kinds)
            {
                foreach (var route in IteratorRoutes)
                {
                    foreach (var loop in Loops)
                    {
                        // The method runs once, sees the iterator as its receiver, and is read once when an accessor holds it.
                        rows.Add(kind, route, loop, "X,Y 1 true " + (route.EndsWith("Accessor", StringComparison.Ordinal) ? "1" : "0"));
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// The loop calls whatever <c>@@iterator</c> resolves to, with the iterator as its receiver, and iterates what it
    /// answers — after a first loop over the same kind warmed whatever the engine remembers about it, in a plain
    /// function, a generator that yields every element and an async function that awaits every element.
    /// </summary>
    [TestCaseSource(nameof(OwnIterableRows))]
    public void AForOfCallsTheIteratorObjectsIteratorMethod(string kind, string route, string loop, string expected)
    {
        Run(NewEngine(), $"ownIterable(function () {{ return {kind}; }}, '{route}', '{loop}')").Should().Be(expected);
    }

    public static TestCases<string, string> BadIterableRows
    {
        get
        {
            var rows = new TestCases<string, string>();
            foreach (var kind in Kinds)
            {
                // A method answering a primitive or undefined, a value that is not callable, and null, which GetMethod
                // reads as no method at all: each is a TypeError.
                foreach (var value in new[] { "function () { return 1; }", "function () { return undefined; }", "1", "null" })
                {
                    rows.Add(kind, value);
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// GetIterator's own errors: an <c>@@iterator</c> that is not callable or answers a non-object.
    /// </summary>
    [TestCaseSource(nameof(BadIterableRows))]
    public void AnUnusableIteratorMethodThrows(string kind, string value)
    {
        Run(NewEngine(), $"badIterable(function () {{ return {kind}; }}, {value})").Should().Be("TypeError");
    }

    /// <summary>
    /// With <c>%Iterator.prototype%[@@iterator]</c> deleted, a built-in iterator is not iterable at all.
    /// </summary>
    [TestCaseSource(nameof(Kinds))]
    public void ADeletedIteratorMethodMakesTheIteratorNotIterable(string kind)
    {
        Run(NewEngine(), $"deletedIterable(function () {{ return {kind}; }})").Should().Be("TypeError");
    }

    public static TestCases<string, string, string, string> ReplacedNextRows
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var kind in Kinds)
            {
                foreach (var route in new[] { "own", "ownAccessor", "prototype", "prototypeAccessor" })
                {
                    foreach (var loop in Loops)
                    {
                        // R1, R2 and the done result: three calls, and one read when an accessor holds the replacement.
                        rows.Add(kind, route, loop, "R1,R2 3 " + (route.EndsWith("Accessor", StringComparison.Ordinal) ? "1" : "0"));
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// GetIteratorFromMethod reads <c>next</c> off the iterator once and every step calls what it read: an own
    /// <c>next</c>, or one replacing the kind prototype's, drives the loop over the iterator object.
    /// </summary>
    [TestCaseSource(nameof(ReplacedNextRows))]
    public void AForOfOverAnIteratorObjectCallsAReplacedNext(string kind, string route, string loop, string expected)
    {
        Run(NewEngine(), $"replacedNext(function () {{ return {kind}; }}, '{route}', '{loop}')").Should().Be(expected);
    }

    /// <summary>
    /// The controls: an iterator object nothing rerouted iterates its own values, with or without an unrelated own
    /// string or symbol property.
    /// </summary>
    [TestCase("[1, 2].values()", "1,2")]
    [TestCase("new Map([[1, 'a']]).entries()", "1,a")]
    [TestCase("new Set([1, 2]).values()", "1,2")]
    [TestCase("'ab'[Symbol.iterator]()", "a,b")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "1,2")]
    [TestCase("list[Symbol.iterator]()", "1,2")]
    public void AnUntouchedIteratorObjectIteratesItsOwnValues(string kind, string expected)
    {
        foreach (var extra in new[] { "none", "string", "symbol" })
        {
            foreach (var loop in Loops)
            {
                Run(NewEngine(), $"untouched(function () {{ return {kind}; }}, '{extra}', '{loop}')").Should().Be(expected, $"{extra} {loop}");
            }
        }
    }

    /// <summary>
    /// The issue's probe.
    /// </summary>
    [Test]
    public void TheIssuesProbe()
    {
        new Engine().Evaluate("""
            const a = [1, 2].values();
            a[Symbol.iterator] = () => [9].values();
            const log = [];
            for (const x of a) log.push(x);
            log.join(',');
            """).AsString().Should().Be("9");
    }

    public static TestCases<string, string, string> ReplacedStringNextRows
    {
        get
        {
            var rows = new TestCases<string, string, string>();
            foreach (var route in new[] { "assign", "defineData", "defineAccessor", "delete" })
            {
                foreach (var consumer in new[] { "forOf", "generator", "async", "spread", "from", "destructure", "set", "yieldStar" })
                {
                    // Destructuring two elements takes two steps and closes; everything else runs to the done result.
                    var calls = consumer == "destructure" ? "2" : "3";
                    rows.Add(route, consumer, "R1,R2 " + calls + " " + (route == "defineAccessor" ? "1" : "0"));
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// A string primitive's iterator is a %StringIteratorPrototype% object, so every consumer calls a replaced
    /// <c>next</c> - assigned, redefined as data or as an accessor, or uncovered on <c>%Iterator.prototype%</c> by a
    /// delete - after a first spread warmed the lane.
    /// </summary>
    [TestCaseSource(nameof(ReplacedStringNextRows))]
    public void EveryConsumerOfAStringPrimitiveCallsAReplacedStringIteratorNext(string route, string consumer, string expected)
    {
        Run(new Engine(), $"replacedStringNext('{route}', '{consumer}')").Should().Be(expected);
    }

    /// <summary>
    /// The issue comment's probe, and the string back to its own values once the built-in <c>next</c> is restored.
    /// </summary>
    [Test]
    public void TheCommentsProbe()
    {
        new Engine().Evaluate("""
            const P = Object.getPrototypeOf(String.prototype[Symbol.iterator]());
            const saved = Object.getOwnPropertyDescriptor(P, 'next');
            P.next = () => ({ done: true });
            const replaced = JSON.stringify([...'ab']);
            Object.defineProperty(P, 'next', saved);
            replaced + ' ' + JSON.stringify([...'ab']);
            """).AsString().Should().Be("""[] ["a","b"]""");
    }

    /// <summary>
    /// The other half of the contract: the cheap checks answer the native lane for an untouched built-in iterator of
    /// every kind and for a string primitive, lose it while anything reroutes <c>@@iterator</c> or <c>next</c>, and
    /// get it back once the built-ins are back.
    /// </summary>
    [TestCaseSource(nameof(Kinds))]
    public void AnUntouchedIteratorObjectIsItsOwnIteratorRecordAgainOnceTheBuiltInsAreBack(string kind)
    {
        var engine = NewEngine();
        engine.Execute($$"""
            var it = {{kind}};
            var P = Object.getPrototypeOf(it);
            var IP = Object.getPrototypeOf(P);
            var iteratorDescriptor = Object.getOwnPropertyDescriptor(IP, Symbol.iterator);
            var nextDescriptor = Object.getOwnPropertyDescriptor(P, 'next');
            function fresh() { return {{kind}}; }
            """);

        bool Native()
        {
            var instance = (IteratorInstance) engine.Evaluate("fresh()");
            return ReferenceEquals(instance.GetIterator(engine.Realm), instance);
        }

        Native().Should().BeTrue();
        engine.Execute("IP[Symbol.iterator] = function () { return [].values(); };");
        Native().Should().BeFalse();
        engine.Execute("Object.defineProperty(IP, Symbol.iterator, iteratorDescriptor);");
        Native().Should().BeTrue();
        engine.Execute("Object.defineProperty(IP, Symbol.iterator, { get() { return iteratorDescriptor.value; } });");
        Native().Should().BeTrue("an accessor answering the built-in method answers the iterator itself");
        engine.Execute("Object.defineProperty(IP, Symbol.iterator, iteratorDescriptor);");
        Native().Should().BeTrue();
        engine.Execute("P[Symbol.iterator] = function () { return [].values(); };");
        Native().Should().BeFalse();
        engine.Execute("delete P[Symbol.iterator];");
        Native().Should().BeTrue();
        engine.Execute("Object.setPrototypeOf(P, { __proto__: IP, [Symbol.iterator]: function () { return [].values(); } });");
        Native().Should().BeFalse();
        engine.Execute("Object.setPrototypeOf(P, IP);");
        Native().Should().BeTrue();
        engine.Execute("P.next = function () { return { done: true }; };");
        Native().Should().BeFalse();
        engine.Execute("Object.defineProperty(P, 'next', nextDescriptor);");
        Native().Should().BeTrue();
        engine.Evaluate("[...fresh()].length").AsNumber().Should().BeGreaterThan(0);
    }

    [Test]
    public void AStringPrimitiveTakesTheNativeLaneAgainOnceTheBuiltInNextIsBack()
    {
        var engine = new Engine();
        engine.Execute("""
            var P = Object.getPrototypeOf(''[Symbol.iterator]());
            var nextDescriptor = Object.getOwnPropertyDescriptor(P, 'next');
            """);

        bool Native() => JsString.Create("ab").GetIterator(engine.Realm) is IteratorInstance.StringIterator;

        Native().Should().BeTrue();
        engine.Execute("P.next = function () { return { done: true }; };");
        Native().Should().BeFalse();
        engine.Execute("Object.defineProperty(P, 'next', nextDescriptor);");
        Native().Should().BeTrue();
        engine.Execute("delete P.next;");
        Native().Should().BeFalse();
        engine.Execute("Object.defineProperty(P, 'next', nextDescriptor);");
        Native().Should().BeTrue();
        engine.Evaluate("[...'ab'].join()").AsString().Should().Be("a,b");
    }
}
