using Jint.Native.Iterator;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 3 of <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a>: the
/// iterator record's [[NextMethod]] is whatever <c>next</c> resolves to on the object <c>@@iterator</c> handed
/// back, and every step calls it. Jint steps a built-in iterator natively instead, and may do so only while that
/// read would still find the built-in <c>next</c> — <see cref="IteratorInstance.HasNativeNext"/>.
/// <para>
/// Only the array iterators answered that question. Every other built-in kind answered <see langword="true"/>
/// unconditionally, so a <c>next</c> the script set on the iterator, or one it put on the kind's prototype in
/// place of the built-in, was never called by spread, <c>Array.from</c>, destructuring, a <c>for…of</c> over the
/// collection or — since sebastienros/jint#4392 sent built-in iterators through GetIterator — a <c>for await</c>.
/// Each kind's prototype now captures its built-in <c>next</c> and compares, as the array iterator's always did.
/// Every expected value is Node 26's.
/// </para>
/// </summary>
public class ReplacedIteratorNextTests
{
    private const string Harness = """
        // A next() answering R1, R2 and then done, counting its calls, so a consumer that stepped the
        // iterator natively instead shows as the original values and a count of zero.
        function replacement(calls) {
            let n = 0;
            return function () { calls.count++; n++; return n <= 2 ? { value: 'R' + n, done: false } : { value: undefined, done: true }; };
        }

        // The four consumers that build an iterator record through GetIterator from an iterator object.
        const consumers = {
            spread: function (it) { return [...it].join(','); },
            from: function (it) { return Array.from(it).join(','); },
            destructure: function (it) { const [a, b] = it; return a + ',' + b; },
            forAwait: async function (it) { const out = []; for await (const x of it) { out.push(x); } return out.join(','); }
        };

        // Replaces `next` on a fresh iterator from make() ('own') or on its prototype ('proto'), runs the
        // consumer over that iterator, puts the prototype back and answers "<values> <calls>".
        async function replacedNext(make, where, consumer) {
            const calls = { count: 0 };
            const it = make();
            let proto, original;
            if (where === 'own') {
                it.next = replacement(calls);
            } else {
                proto = Object.getPrototypeOf(it);
                original = Object.getOwnPropertyDescriptor(proto, 'next');
                proto.next = replacement(calls);
            }
            try { return (await consumers[consumer](it)) + ' ' + calls.count; }
            finally { if (proto) { Object.defineProperty(proto, 'next', original); } }
        }

        // A for-of over the collection itself, whose @@iterator hands out an iterator from the prototype the
        // replacement went on.
        function replacedPrototypeNextForOf(collection, makeIterator) {
            const calls = { count: 0 };
            const proto = Object.getPrototypeOf(makeIterator());
            const original = Object.getOwnPropertyDescriptor(proto, 'next');
            proto.next = replacement(calls);
            try { const out = []; for (const x of collection) { out.push(x); } return out.join(',') + ' ' + calls.count; }
            finally { Object.defineProperty(proto, 'next', original); }
        }
        """;

    private const string InvalidationHarness = """
        // Applies one way of making `next` resolve to `next` for `it`, a fresh iterator, and answers the object to
        // iterate plus the function that puts every prototype back. `makeOther` hands out an iterator of another kind.
        function applyRoute(route, it, next, makeOther) {
            const P = Object.getPrototypeOf(it);
            const IP = Object.getPrototypeOf(P);
            const saved = [];
            function save(o, k) { saved.push([o, k, Object.getOwnPropertyDescriptor(o, k)]); }
            let target = it;
            switch (route) {
                case 'assign': save(P, 'next'); P.next = next; break;
                case 'defineData': save(P, 'next'); Object.defineProperty(P, 'next', { value: next }); break;
                case 'defineAccessor': save(P, 'next'); Object.defineProperty(P, 'next', { get() { return next; } }); break;
                case 'delete': save(P, 'next'); save(IP, 'next'); delete P.next; IP.next = next; break;
                case 'ownAssign': it.next = next; break;
                case 'ownAccessor': Object.defineProperty(it, 'next', { get() { return next; } }); break;
                case 'protoPlain': Object.setPrototypeOf(it, { __proto__: IP, next: next }); break;
                case 'protoOtherKind': { const other = Object.getPrototypeOf(makeOther()); save(other, 'next'); other.next = next; Object.setPrototypeOf(it, other); break; }
                case 'protoProxy': Object.setPrototypeOf(it, new Proxy(P, { get(t, k, r) { return k === 'next' ? next : Reflect.get(t, k, r); } })); break;
                case 'proxy': target = new Proxy(it, { get(t, k, r) { return k === 'next' ? next : Reflect.get(t, k, r); } }); break;
            }
            return {
                target: target,
                restore: function () { for (let i = saved.length - 1; i >= 0; i--) { const s = saved[i]; if (s[2]) { Object.defineProperty(s[0], s[1], s[2]); } else { delete s[0][s[1]]; } } }
            };
        }

        // Iterates one iterator of the kind first, so that whatever the engine remembers about the prototype's
        // `next` is warm, then takes that route on a fresh one, runs the consumer and answers "<values> <calls>".
        async function invalidated(make, makeOther, route, consumer) {
            [...make()];
            const calls = { count: 0 };
            const applied = applyRoute(route, make(), replacement(calls), makeOther);
            try { return (await consumers[consumer](applied.target)) + ' ' + calls.count; }
            finally { applied.restore(); }
        }
        """;

    private static string Run(Engine engine, string script) =>
        engine.Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// Each built-in kind, with <c>next</c> replaced on the iterator and on its prototype, under each consumer.
    /// The array and typed-array rows are the controls: they were already right.
    /// </summary>
    [TestCase("new Map([[1, 'a']]).entries()", "own", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "own", "from", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "own", "destructure", "R1,R2 2")]
    [TestCase("new Map([[1, 'a']]).entries()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "proto", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "proto", "from", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new Map([[1, 'a']]).entries()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).values()", "own", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).values()", "own", "from", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).values()", "own", "destructure", "R1,R2 2")]
    [TestCase("new Map([[1, 'a']]).values()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).values()", "proto", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).values()", "proto", "from", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).values()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new Map([[1, 'a']]).values()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "own", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "own", "from", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "own", "destructure", "R1,R2 2")]
    [TestCase("new Set([1, 2]).values()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "proto", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "proto", "from", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new Set([1, 2]).values()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "own", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "own", "from", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "own", "destructure", "R1,R2 2")]
    [TestCase("'ab'[Symbol.iterator]()", "own", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "proto", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "proto", "from", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "proto", "destructure", "R1,R2 2")]
    [TestCase("'ab'[Symbol.iterator]()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "own", "spread", "R1,R2 3")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "own", "from", "R1,R2 3")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "own", "destructure", "R1,R2 2")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "own", "forAwait", "R1,R2 3")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "proto", "spread", "R1,R2 3")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "proto", "from", "R1,R2 3")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "proto", "destructure", "R1,R2 2")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)", "proto", "forAwait", "R1,R2 3")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "own", "spread", "R1,R2 3")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "own", "from", "R1,R2 3")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "own", "destructure", "R1,R2 2")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "proto", "spread", "R1,R2 3")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "proto", "from", "R1,R2 3")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("[1, 2].values()", "own", "spread", "R1,R2 3")]
    [TestCase("[1, 2].values()", "own", "from", "R1,R2 3")]
    [TestCase("[1, 2].values()", "own", "destructure", "R1,R2 2")]
    [TestCase("[1, 2].values()", "own", "forAwait", "R1,R2 3")]
    [TestCase("[1, 2].values()", "proto", "spread", "R1,R2 3")]
    [TestCase("[1, 2].values()", "proto", "from", "R1,R2 3")]
    [TestCase("[1, 2].values()", "proto", "destructure", "R1,R2 2")]
    [TestCase("[1, 2].values()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("new Uint8Array([1, 2]).values()", "own", "spread", "R1,R2 3")]
    [TestCase("new Uint8Array([1, 2]).values()", "own", "from", "R1,R2 3")]
    [TestCase("new Uint8Array([1, 2]).values()", "own", "destructure", "R1,R2 2")]
    [TestCase("new Uint8Array([1, 2]).values()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new Uint8Array([1, 2]).values()", "proto", "spread", "R1,R2 3")]
    [TestCase("new Uint8Array([1, 2]).values()", "proto", "from", "R1,R2 3")]
    [TestCase("new Uint8Array([1, 2]).values()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new Uint8Array([1, 2]).values()", "proto", "forAwait", "R1,R2 3")]
    public void AReplacedNextOnABuiltInIteratorIsCalled(string iterator, string where, string consumer, string expected)
    {
        Run(new Engine(), $"replacedNext(function () {{ return {iterator}; }}, '{where}', '{consumer}')").Should().Be(expected);
    }

    /// <summary>
    /// A <c>for…of</c> over the collection reaches the replacement on the prototype through the collection's
    /// own <c>@@iterator</c>. The array row is the control.
    /// </summary>
    [TestCase("new Map([[1, 'a']])", "new Map().entries()", "R1,R2 3")]
    [TestCase("new Set([1, 2])", "new Set().values()", "R1,R2 3")]
    [TestCase("[1, 2]", "[].values()", "R1,R2 3")]
    public void AForOfOverTheCollectionCallsAReplacedPrototypeNext(string collection, string makeIterator, string expected)
    {
        Run(new Engine(), $"replacedPrototypeNextForOf({collection}, function () {{ return {makeIterator}; }})").Should().Be(expected);
    }

    /// <summary>
    /// A wrapped CLR collection's <c>@@iterator</c> hands out a built-in iterator too. Node has no counterpart;
    /// the expected value is the one GetIteratorFromMethod step 3 gives every other iterator.
    /// </summary>
    [TestCase("list", "spread", "R1,R2 3")]
    [TestCase("list", "from", "R1,R2 3")]
    [TestCase("list", "destructure", "R1,R2 2")]
    [TestCase("list", "forAwait", "R1,R2 3")]
    [TestCase("dictionary", "spread", "R1,R2 3")]
    [TestCase("dictionary", "from", "R1,R2 3")]
    [TestCase("dictionary", "destructure", "R1,R2 2")]
    [TestCase("dictionary", "forAwait", "R1,R2 3")]
    public void AnOwnNextOnAClrCollectionsIteratorIsCalled(string name, string consumer, string expected)
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.SetValue("dictionary", new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

        Run(engine, $"replacedNext(function () {{ return {name}[Symbol.iterator](); }}, 'own', '{consumer}')").Should().Be(expected);
    }

    /// <summary>
    /// A replaced <c>%ArrayIteratorPrototype%.next</c> sends a wrapped CLR enumerable's iterator through the
    /// <c>ObjectIterator</c>, which closes by calling a script <c>return</c>. The CLR iterator has none, but it holds
    /// a host enumerator only its native close disposes, so a <c>break</c> must still reach it: here a
    /// <c>yield</c> method's <c>finally</c>. The untouched row is the control.
    /// </summary>
    [TestCase(false, "1 0")]
    [TestCase(true, "1 1")]
    public void ABreakDisposesAClrEnumeratorWhetherOrNotTheArrayIteratorsNextWasReplaced(bool replaceNext, string expected)
    {
        var probe = new DisposalProbe();
        var engine = new Engine();
        engine.SetValue("items", probe.Items());

        var result = engine.Evaluate($$"""
            (function () {
                const proto = Object.getPrototypeOf([].values());
                const original = proto.next;
                let calls = 0;
                if ({{(replaceNext ? "true" : "false")}}) {
                    proto.next = function () { calls++; return original.call(this); };
                }
                try {
                    const seen = [];
                    for (const x of items) { seen.push(x); break; }
                    return seen.join(',') + ' ' + calls;
                } finally {
                    proto.next = original;
                }
            })()
            """).AsString();

        result.Should().Be(expected);
        probe.Disposed.Should().BeTrue();
    }

    private sealed class DisposalProbe
    {
        public bool Disposed;

        public IEnumerable<int> Items()
        {
            try
            {
                yield return 1;
                yield return 2;
                yield return 3;
            }
            finally
            {
                Disposed = true;
            }
        }
    }

    /// <summary>
    /// Every way of making <c>next</c> resolve to something else, each taken after an iteration of the same kind
    /// has warmed whatever the engine remembers about the prototype's <c>next</c>: an assignment and a data
    /// redefinition (both change the cached descriptor's value in place), an accessor redefinition, a delete
    /// that uncovers a <c>next</c> further up, an own data or accessor <c>next</c> on the iterator, a prototype
    /// swapped for a plain object, for another kind's prototype whose <c>next</c> was replaced or for a Proxy, and
    /// the iterator itself behind a Proxy. Every expected value is Node 26's.
    /// </summary>
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "assign", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "assign", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "defineData", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "defineData", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "defineAccessor", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "defineAccessor", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "delete", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "delete", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "ownAssign", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "ownAssign", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "ownAccessor", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "ownAccessor", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "protoPlain", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "protoPlain", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "protoOtherKind", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "protoOtherKind", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "protoProxy", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "protoProxy", "forAwait", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "proxy", "spread", "R1,R2 3")]
    [TestCase("new Set([1, 2]).values()", "new Map([[1, 'a']]).entries()", "proxy", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "assign", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "assign", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "defineData", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "defineData", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "defineAccessor", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "defineAccessor", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "delete", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "delete", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "ownAssign", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "ownAssign", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "ownAccessor", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "ownAccessor", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "protoPlain", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "protoPlain", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "protoOtherKind", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "protoOtherKind", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "protoProxy", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "protoProxy", "forAwait", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "proxy", "spread", "R1,R2 3")]
    [TestCase("new Map([[1, 'a']]).entries()", "new Set([1, 2]).values()", "proxy", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "assign", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "assign", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "defineData", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "defineData", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "defineAccessor", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "defineAccessor", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "delete", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "delete", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "ownAssign", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "ownAssign", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "ownAccessor", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "ownAccessor", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "protoPlain", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "protoPlain", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "protoOtherKind", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "protoOtherKind", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "protoProxy", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "protoProxy", "forAwait", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "proxy", "spread", "R1,R2 3")]
    [TestCase("'ab'[Symbol.iterator]()", "new Set([1, 2]).values()", "proxy", "forAwait", "R1,R2 3")]
    public void EveryRouteToAReplacedNextIsSeenAfterTheCheckIsWarm(string iterator, string otherKind, string route, string consumer, string expected)
    {
        Run(new Engine(), InvalidationHarness + "\n" + $"invalidated(function () {{ return {iterator}; }}, function () {{ return {otherKind}; }}, '{route}', '{consumer}')")
            .Should().Be(expected);
    }

    /// <summary>
    /// The cheap check is a cache, so it has to come back as well as go away: once the built-in <c>next</c> is
    /// back in place - assigned, redefined, or deleted and defined again - a fresh iterator steps natively again,
    /// and an accessor that answers the built-in function is a replacement the cheap check does not see through
    /// (the read through the <c>ObjectIterator</c> still finds the built-in function, so only the lane differs).
    /// </summary>
    [Test]
    public void TheNativeLaneComesBackOnceTheBuiltInNextIsBack()
    {
        var engine = new Engine();
        engine.Execute("""
            var P = Object.getPrototypeOf(new Set().values());
            var original = P.next;
            var originalDescriptor = Object.getOwnPropertyDescriptor(P, 'next');
            function fresh() { return new Set([1, 2]).values(); }
            """);

        bool Native() => ((IteratorInstance) engine.Evaluate("fresh()")).HasNativeNext;

        Native().Should().BeTrue();
        engine.Execute("P.next = function () { return { done: true }; };");
        Native().Should().BeFalse();
        engine.Execute("P.next = original;");
        Native().Should().BeTrue();
        engine.Execute("Object.defineProperty(P, 'next', { get() { return original; } });");
        Native().Should().BeFalse();
        engine.Evaluate("[...fresh()].join()").AsString().Should().Be("1,2");
        engine.Execute("Object.defineProperty(P, 'next', originalDescriptor);");
        Native().Should().BeTrue();
        engine.Execute("delete P.next;");
        Native().Should().BeFalse();
        engine.Execute("Object.defineProperty(P, 'next', originalDescriptor);");
        Native().Should().BeTrue();
        engine.Evaluate("[...fresh()].join()").AsString().Should().Be("1,2");
    }

    /// <summary>
    /// The other half of the contract: an untouched built-in iterator of every kind keeps stepping natively, so
    /// the comparison costs a read and never the wrapper. A kind whose prototype stopped capturing its
    /// <c>next</c> would still iterate correctly, through the wrapper, and would show up only here.
    /// </summary>
    [TestCase("[1, 2].values()")]
    [TestCase("new Uint8Array([1, 2]).values()")]
    [TestCase("new Map([[1, 'a']]).entries()")]
    [TestCase("new Map([[1, 'a']]).keys()")]
    [TestCase("new Set([1, 2]).values()")]
    [TestCase("new Set([1, 2]).entries()")]
    [TestCase("'ab'[Symbol.iterator]()")]
    [TestCase("'a1b2'.matchAll(/[0-9]/g)")]
    [TestCase("new Intl.Segmenter().segment('ab')[Symbol.iterator]()")]
    [TestCase("list[Symbol.iterator]()")]
    [TestCase("dictionary[Symbol.iterator]()")]
    public void AnUntouchedBuiltInIteratorStepsNativelyAndAnOwnNextDisablesThat(string iterator)
    {
        var engine = new Engine();
        engine.SetValue("list", new List<int> { 1, 2 });
        engine.SetValue("dictionary", new Dictionary<string, int> { ["a"] = 1 });

        if (engine.Evaluate(iterator) is not IteratorInstance instance)
        {
            Assert.Fail(iterator + " is not a built-in iterator instance");
            return;
        }

        instance.HasNativeNext.Should().BeTrue();

        engine.SetValue("it", instance);
        engine.Execute("it.next = function () { return { done: true }; };");
        instance.HasNativeNext.Should().BeFalse();
    }

#if NET8_0_OR_GREATER
    private static Engine WebEngine() => new(options => options.UseWebApis(WebApiFeatures.Fetch | WebApiFeatures.Files | WebApiFeatures.Url));

    /// <summary>
    /// The WebIDL iterators of <c>Headers</c>, <c>FormData</c> and <c>URLSearchParams</c>, whose prototypes are
    /// built-in iterator prototypes too.
    /// </summary>
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "own", "spread", "R1,R2 3")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "own", "from", "R1,R2 3")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "own", "destructure", "R1,R2 2")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "proto", "spread", "R1,R2 3")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "proto", "from", "R1,R2 3")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new Headers([['a', '1'], ['b', '2']]).entries()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "own", "spread", "R1,R2 3")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "own", "from", "R1,R2 3")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "own", "destructure", "R1,R2 2")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "own", "forAwait", "R1,R2 3")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "proto", "spread", "R1,R2 3")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "proto", "from", "R1,R2 3")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "proto", "destructure", "R1,R2 2")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); f.append('b', '2'); return f.keys(); })()", "proto", "forAwait", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "own", "spread", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "own", "from", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "own", "destructure", "R1,R2 2")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "own", "forAwait", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "proto", "spread", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "proto", "from", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "proto", "destructure", "R1,R2 2")]
    [TestCase("new URLSearchParams('a=1&b=2').values()", "proto", "forAwait", "R1,R2 3")]
    public void AReplacedNextOnAWebApiIteratorIsCalled(string iterator, string where, string consumer, string expected)
    {
        Run(WebEngine(), $"replacedNext(function () {{ return {iterator}; }}, '{where}', '{consumer}')").Should().Be(expected);
    }

    [TestCase("new Headers([['a', '1']])", "new Headers().entries()", "R1,R2 3")]
    [TestCase("new URLSearchParams('a=1')", "new URLSearchParams().entries()", "R1,R2 3")]
    public void AForOfOverAWebApiCollectionCallsAReplacedPrototypeNext(string collection, string makeIterator, string expected)
    {
        Run(WebEngine(), $"replacedPrototypeNextForOf({collection}, function () {{ return {makeIterator}; }})").Should().Be(expected);
    }

    [TestCase("new Headers([['a', '1']]).entries()")]
    [TestCase("(function () { const f = new FormData(); f.append('a', '1'); return f.keys(); })()")]
    [TestCase("new URLSearchParams('a=1').values()")]
    public void AnUntouchedWebApiIteratorStepsNativelyAndAnOwnNextDisablesThat(string iterator)
    {
        var engine = WebEngine();
        if (engine.Evaluate(iterator) is not IteratorInstance instance)
        {
            Assert.Fail(iterator + " is not a built-in iterator instance");
            return;
        }

        instance.HasNativeNext.Should().BeTrue();

        engine.SetValue("it", instance);
        engine.Execute("it.next = function () { return { done: true }; };");
        instance.HasNativeNext.Should().BeFalse();
    }
#endif
}
