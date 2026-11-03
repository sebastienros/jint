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
