#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://tc39.es/ecma262/#sec-array.prototype.values">Array.prototype.values</see> — and
/// <c>keys</c> and <c>entries</c> with it — is two steps: <i>ToObject(this)</i> and
/// <i>CreateArrayIterator(O, kind)</i>. Neither reads <c>length</c>, and there is no array-like
/// precondition the receiver can fail. The <c>length</c> read belongs to the iterator's own closure,
/// whose step 1.b is <i>LengthOfArrayLike</i> — a <c>Get</c> plus a <c>ToLength</c> — performed afresh on
/// <em>every</em> <c>next()</c>.
/// <para>
/// Jint gated all three on <c>ObjectInstance.IsArrayLike</c>, which demands a <c>length</c> that is
/// present, is <em>already</em> a <c>JsNumber</c>, and is non-negative, and threw
/// <c>TypeError: cannot construct iterator</c> otherwise. That was wrong in six ways at once: absent means
/// zero, a string or a boolean coerces, a negative clamps to zero, and the read — with any throw from a
/// <c>length</c> getter — belongs to the first <c>next()</c> rather than to <c>values()</c>. Found by the
/// FileAPI web-platform-tests corpus, where <c>new Blob({[Symbol.iterator]: Array.prototype[Symbol.iterator]})</c>
/// is exactly this shape, and reproducible with no web API enabled at all.
/// </para>
/// </summary>
public class ArrayIteratorReceiverTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// The seven shapes named in the issue, each with what the specification requires of it.
    /// </summary>
    [Theory]
    // No `length` at all: LengthOfArrayLike is ToLength(undefined), which is 0, so the closure returns
    // on its very first step.
    [InlineData("[...Array.prototype.values.call({})]", "")]
    // A string `length` coerces: ToLength('3') is 3.
    [InlineData("[...Array.prototype.values.call({length: '3', 0: 'a', 1: 'b', 2: 'c'})]", "a,b,c")]
    // ToLength clamps a negative to +0 rather than rejecting it.
    [InlineData("[...Array.prototype.values.call({length: -1})]", "")]
    // ToLength(null) is ToIntegerOrInfinity(ToNumber(null)) = 0.
    [InlineData("[...Array.prototype.values.call({length: null})]", "")]
    // ToLength(true) is 1.
    [InlineData("[...Array.prototype.values.call({length: true, 0: 'a'})]", "a")]
    // The whole reason the WPT corpus reached this: an object borrowing Array.prototype's @@iterator is a
    // sequence of length 0, not a TypeError.
    [InlineData("[...{[Symbol.iterator]: Array.prototype.values}]", "")]
    // ToObject boxes a primitive, so a String object's own indices and length are what gets iterated.
    [InlineData("[...Array.prototype.values.call('abc')]", "a,b,c")]
    public void ANonArrayLikeReceiverIteratesRatherThanThrowing(string script, string expected)
    {
        _engine.Evaluate(script + ".join(',')").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A Number object carries no <c>length</c>, so it is the "absent means zero" case reached through
    /// <i>ToObject</i> rather than directly.
    /// </summary>
    [Fact]
    public void APrimitiveWithNoLengthBoxesToAnEmptyIteration()
    {
        _engine.Evaluate("[...Array.prototype.values.call(5)].length").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// <i>ToObject</i> is still step 1, so <c>undefined</c> and <c>null</c> remain a <c>TypeError</c> — the
    /// one receiver rejection the algorithm does have.
    /// </summary>
    [Theory]
    [InlineData("undefined")]
    [InlineData("null")]
    public void AnUndefinedOrNullReceiverIsStillATypeError(string receiver)
    {
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate($"Array.prototype.values.call({receiver})"));
        ex.Error.Get("name").AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// <c>keys</c> and <c>entries</c> have the same two-step body, so the same receiver reaches all three.
    /// </summary>
    [Fact]
    public void KeysAndEntriesTakeTheSameReceivers()
    {
        _engine.Evaluate("[...Array.prototype.keys.call({})].length").AsNumber().Should().Be(0);
        _engine.Evaluate("[...Array.prototype.keys.call({length: '2'})].join(',')").AsString().Should().Be("0,1");
        _engine.Evaluate("[...Array.prototype.entries.call({})].length").AsNumber().Should().Be(0);
        _engine.Evaluate("""
            [...Array.prototype.entries.call({length: '2', 0: 'x', 1: 'y'})].map(e => e.join(':')).join(',')
            """).AsString().Should().Be("0:x,1:y");
    }

    /// <summary>
    /// The read is the iterator's, not <c>values</c>'s, so a throwing <c>length</c> getter has to survive
    /// <c>values()</c> and erupt from the first <c>next()</c>. This is the ordering half of the defect and
    /// the half a "throw earlier instead" fix would get wrong.
    /// </summary>
    [Fact]
    public void AThrowingLengthGetterThrowsFromTheFirstNextNotFromValues()
    {
        _engine.Evaluate("""
            var sentinel = new Error('boom');
            var receiver = { get length() { throw sentinel; } };
            var constructed = false, thrownFromNext = null;
            var iterator = Array.prototype.values.call(receiver);
            constructed = true;
            try { iterator.next(); } catch (e) { thrownFromNext = e; }
            constructed + ' ' + (thrownFromNext === sentinel);
            """).AsString().Should().Be("true true");
    }

    /// <summary>
    /// The same for a <c>length</c> whose coercion throws: <i>ToLength</i> runs inside the closure, so
    /// <c>values()</c> is clean and <c>next()</c> carries the exception. (This is the shape
    /// <c>Blob-constructor.any.js</c>'s "ToUint32 should be applied to the length" row hands the engine.)
    /// </summary>
    [Fact]
    public void AThrowingLengthCoercionThrowsFromTheFirstNext()
    {
        _engine.Evaluate("""
            var sentinel = new Error('boom');
            var receiver = { length: { valueOf: null, toString: function () { throw sentinel; } } };
            var iterator = Array.prototype.values.call(receiver);
            var caught = null;
            try { iterator.next(); } catch (e) { caught = e; }
            caught === sentinel;
            """).AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A <c>length</c> that cannot be coerced at all — a Symbol — is a <c>TypeError</c> from <c>next()</c>,
    /// again not from <c>values()</c>.
    /// </summary>
    [Fact]
    public void ASymbolLengthIsATypeErrorFromNext()
    {
        _engine.Evaluate("""
            var iterator = Array.prototype.values.call({ length: Symbol('nope') });
            var name = null;
            try { iterator.next(); } catch (e) { name = e.name; }
            name;
            """).AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// Per-<c>next()</c> means per-<c>next()</c>: an array-like that grows between two steps yields the
    /// elements it grew by. Caching the length at <c>values()</c> — or even at the first step — would stop
    /// after the element that existed then.
    /// </summary>
    [Fact]
    public void ALengthThatGrowsBetweenStepsYieldsTheNewElements()
    {
        _engine.Evaluate("""
            var receiver = { length: 0 };
            var iterator = Array.prototype.values.call(receiver);

            receiver.length = 1; receiver[0] = 'a';
            var first = iterator.next();

            receiver.length = 2; receiver[1] = 'b';
            var second = iterator.next();

            var third = iterator.next();
            first.value + ',' + second.value + ',' + third.value + ',' + third.done;
            """).AsString().Should().Be("a,b,undefined,true");
    }

    /// <summary>
    /// And the other direction: a shrink completes the iteration at the step that observes it.
    /// </summary>
    [Fact]
    public void ALengthThatShrinksBetweenStepsCompletesTheIteration()
    {
        _engine.Evaluate("""
            var receiver = { length: 3, 0: 'a', 1: 'b', 2: 'c' };
            var iterator = Array.prototype.values.call(receiver);

            var first = iterator.next();
            receiver.length = 1;
            var second = iterator.next();

            first.value + ',' + second.value + ',' + second.done;
            """).AsString().Should().Be("a,undefined,true");
    }

    /// <summary>
    /// Once the closure has returned, the generator is completed and observes nothing further — so a
    /// <c>length</c> that grows back after exhaustion does not restart the iteration, and the getter is not
    /// even consulted.
    /// </summary>
    [Fact]
    public void AnExhaustedArrayLikeIteratorDoesNotReadLengthAgain()
    {
        _engine.Evaluate("""
            var reads = 0;
            var backing = 1;
            var receiver = { 0: 'a', 1: 'b', get length() { reads++; return backing; } };
            var iterator = Array.prototype.values.call(receiver);

            iterator.next();
            iterator.next();          // observes length 1, completes
            var readsAtCompletion = reads;

            backing = 2;
            var after = iterator.next();
            after.done + ' ' + (reads === readsAtCompletion);
            """).AsString().Should().Be("true true");
    }

    /// <summary>
    /// The exact ordering <c>Blob-constructor.any.js</c>'s "Getters and value conversions should happen in
    /// order until an exception is thrown" asserts, reduced to the language: the <c>length</c> getter and
    /// its <c>valueOf</c> run once per step, interleaved with the index reads, and nothing runs at
    /// <c>values()</c> time.
    /// </summary>
    [Fact]
    public void LengthIsReadOncePerStepInterleavedWithTheIndexReads()
    {
        _engine.Evaluate("""
            var received = [];
            var receiver = {
                get length() {
                    received.push('length getter');
                    return { valueOf: function () { received.push('length valueOf'); return 3; } };
                },
                get 0() { received.push('0 getter'); return 'a'; },
                get 1() { received.push('1 getter'); return 'b'; },
                get 2() { received.push('2 getter'); return 'c'; }
            };

            var iterator = Array.prototype.values.call(receiver);
            received.push('|values returned|');
            iterator.next();
            iterator.next();
            received.join(',');
            """).AsString().Should().Be(
            "|values returned|,length getter,length valueOf,0 getter,length getter,length valueOf,1 getter");
    }

    /// <summary>
    /// A key+value step re-reads the length like the other two kinds, and reads the element exactly once.
    /// </summary>
    [Fact]
    public void EntriesReReadsLengthPerStep()
    {
        _engine.Evaluate("""
            var reads = 0;
            var receiver = { 0: 'x', 1: 'y', get length() { reads++; return 2; } };
            var seen = [...Array.prototype.entries.call(receiver)].map(e => e.join(':')).join(',');
            seen + ' ' + reads;
            """).AsString().Should().Be("0:x,1:y 3");
    }

    /// <summary>
    /// Step 10.d.v of the closure is a bare <c>Get</c>, never a <c>HasProperty</c> first, so a hole in an
    /// array-like is <c>undefined</c> and not a skipped element.
    /// </summary>
    [Fact]
    public void AHoleInAnArrayLikeYieldsUndefined()
    {
        _engine.Evaluate("[...Array.prototype.values.call({length: 3, 0: 'a', 2: 'c'})].join('|')")
            .AsString().Should().Be("a||c");
    }

    /// <summary>
    /// A key-kind step reads no element at all — only the length.
    /// </summary>
    [Fact]
    public void KeysReadsNoElement()
    {
        _engine.Evaluate("""
            var reads = 0;
            var receiver = { length: 2, get 0() { reads++; return 'a'; }, get 1() { reads++; return 'b'; } };
            [...Array.prototype.keys.call(receiver)].join(',') + ' ' + reads;
            """).AsString().Should().Be("0,1 0");
    }

    /// <summary>
    /// The control: a real array is still iterated by the dense-array lane and is untouched by any of this,
    /// and so is a typed array, whose own <c>%TypedArray%.prototype.values</c> keeps its
    /// <i>ValidateTypedArray</i> precondition.
    /// </summary>
    [Fact]
    public void RealArraysAndTypedArraysAreUnaffected()
    {
        _engine.Evaluate("[...[1, 2, 3]].join(',')").AsString().Should().Be("1,2,3");
        _engine.Evaluate("[...[10, 20].entries()].map(e => e.join(':')).join(',')").AsString().Should().Be("0:10,1:20");
        _engine.Evaluate("[...[7, 8].keys()].join(',')").AsString().Should().Be("0,1");
        _engine.Evaluate("[...new Uint8Array([1, 2, 3]).values()].join(',')").AsString().Should().Be("1,2,3");

        var detached = Assert.Throws<JavaScriptException>(() => _engine.Evaluate("""
            var buffer = new ArrayBuffer(4);
            var array = new Uint8Array(buffer);
            buffer.transfer();
            array.values();
            """));
        detached.Error.Get("name").AsString().Should().Be("TypeError");
    }

    /// <summary>
    /// The sharpest probe for the ordering half: a <c>Proxy</c> logs every property access its receiver sees,
    /// through both the <c>get</c> and the <c>getOwnPropertyDescriptor</c> trap — the second because the gate
    /// this change removes probed <c>length</c> through <c>TryGetValue</c>, which walks own descriptors and
    /// never fires <c>get</c>. <c>values()</c> must log nothing at all (<i>ToObject</i> and
    /// <i>CreateArrayIterator</i> touch no property), and the first <c>next()</c> must log exactly a
    /// <c>get</c> of <c>length</c> and then one of the index — steps 1.b and 10.d.v of the closure, in that
    /// order and with no existence probe between them.
    /// </summary>
    [Fact]
    public void ValuesReadsNothingAndTheFirstNextReadsLengthThenTheIndex()
    {
        _engine.Evaluate("""
            var log = [];
            var target = { length: 1, 0: 'a' };
            var proxy = new Proxy(target, {
                get: function (t, k) { log.push('get:' + String(k)); return t[k]; },
                getOwnPropertyDescriptor: function (t, k) {
                    log.push('gopd:' + String(k));
                    return Object.getOwnPropertyDescriptor(t, k);
                }
            });

            var iterator = Array.prototype.values.call(proxy);
            var atConstruction = log.join(',');
            var first = iterator.next();

            '[' + atConstruction + '] [' + log.join(',') + '] ' + first.value;
            """).AsString().Should().Be("[] [get:length,get:0] a");
    }

    /// <summary>
    /// <i>ToLength</i> is <i>ToIntegerOrInfinity</i> clamped into <c>[0, 2^53-1]</c>, not <i>ToNumber</i>:
    /// anything coercing to <c>NaN</c> is zero, a fraction truncates, and a negative clamps.
    /// </summary>
    [Theory]
    [InlineData("{length: NaN, 0: 'a'}", "")]
    [InlineData("{length: 'abc', 0: 'a'}", "")]
    [InlineData("{length: {}, 0: 'a'}", "")]
    [InlineData("{length: [], 0: 'a'}", "")]
    [InlineData("{length: undefined, 0: 'a'}", "")]
    [InlineData("{length: -Infinity, 0: 'a'}", "")]
    [InlineData("{length: -0.5, 0: 'a'}", "")]
    [InlineData("{length: false, 0: 'a'}", "")]
    [InlineData("{length: 2.9, 0: 'a', 1: 'b', 2: 'c'}", "a,b")]
    [InlineData("{length: ' 2 ', 0: 'a', 1: 'b', 2: 'c'}", "a,b")]
    [InlineData("{length: '0x2', 0: 'a', 1: 'b', 2: 'c'}", "a,b")]
    [InlineData("{length: ['2'], 0: 'a', 1: 'b', 2: 'c'}", "a,b")]
    public void TheLengthIsCoercedByToLength(string receiver, string expected)
    {
        _engine.Evaluate($"[...Array.prototype.values.call({receiver})].join(',')").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A very large <c>length</c> is clamped by <i>ToLength</i> — never rejected, and never read as zero —
    /// so the first step still yields index 0. Exactly one step is taken: the iteration itself is unbounded
    /// by construction.
    /// <para>
    /// The rows stop below 2^32 deliberately. <i>ToLength</i>'s specified ceiling is 2^53-1, but the
    /// array-like lane behind the iterator carries its length as a <c>uint</c>
    /// (<c>ArrayOperations.GetLength</c>), so a length above 2^32-1 is decided by an out-of-range
    /// double-to-integer conversion — saturating on .NET, unspecified on .NET Framework, where 2^53 reads
    /// back as 0. That is pre-existing and independent of the receiver gate this class is about: such a
    /// <c>length</c> is a non-negative <c>JsNumber</c> and so reached the very same iterator before the gate
    /// was dropped. The sibling generics do not share it — <c>at</c> and <c>includes</c> go through
    /// <c>GetLongLength</c>, which clamps at <i>MaxArrayLikeLength</i> — so it is one method's cast, and its
    /// own change.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Math.pow(2, 31)")]
    [InlineData("4294967294")]
    [InlineData("2147483648.7")]
    public void AVeryLargeLengthStillYieldsItsFirstElement(string length)
    {
        _engine.Evaluate($$"""
            var iterator = Array.prototype.values.call({ length: {{length}}, 0: 'a' });
            var first = iterator.next();
            first.value + ' ' + first.done;
            """).AsString().Should().Be("a false");
    }

    /// <summary>
    /// A shrink all the way below the index the iterator has already reached completes it at that step:
    /// <c>index &lt; len</c> is false, so the closure returns without reading an element.
    /// </summary>
    [Fact]
    public void ALengthThatShrinksBelowTheCurrentIndexCompletesTheIteration()
    {
        _engine.Evaluate("""
            var reads = 0;
            var receiver = { 0: 'a', 1: 'b', 2: 'c', length: 4 };
            Object.defineProperty(receiver, '3', { get: function () { reads++; return 'd'; } });
            var iterator = Array.prototype.values.call(receiver);

            var first = iterator.next();
            var second = iterator.next();
            var third = iterator.next();   // position is now 3
            receiver.length = 1;           // …and the length drops below it
            var fourth = iterator.next();

            [first.value, second.value, third.value, String(fourth.value), fourth.done, reads].join(',');
            """).AsString().Should().Be("a,b,c,undefined,true,0");
    }

    /// <summary>
    /// Step 10.d.v is an ordinary <i>Get</i>, so a hole resolves up the prototype chain rather than
    /// answering <c>undefined</c> unconditionally — for an array-like…
    /// </summary>
    [Fact]
    public void AHoleInAnArrayLikeResolvesThroughThePrototypeChain()
    {
        _engine.Evaluate("""
            var receiver = Object.create({ 1: 'from proto' });
            receiver.length = 3;
            receiver[0] = 'a';
            receiver[2] = 'c';
            [...Array.prototype.values.call(receiver)].join('|');
            """).AsString().Should().Be("a|from proto|c");
    }

    /// <summary>
    /// …and for a real array, whose dense lane has to notice that <c>Array.prototype</c> gained an index and
    /// stop answering holes out of its backing store alone.
    /// </summary>
    [Fact]
    public void AHoleInARealArrayResolvesThroughThePrototypeChain()
    {
        _engine.Evaluate("""
            (function () {
                Array.prototype[1] = 'from proto';
                try {
                    return [...['a', , 'c']].join('|') + ' '
                        + [...['a', , 'c'].entries()].map(e => e.join(':')).join(',');
                } finally {
                    delete Array.prototype[1];
                }
            })();
            """).AsString().Should().Be("a|from proto|c 0:a,1:from proto,2:c");
    }

    /// <summary>
    /// An entry step yields <c>[index, Get(O, index)]</c>, so an absent index is a present pair carrying
    /// <c>undefined</c> — never a skipped pair.
    /// </summary>
    [Fact]
    public void EntriesYieldsAPairForEveryIndexIncludingAbsentOnes()
    {
        _engine.Evaluate("[...Array.prototype.entries.call({length: 2})].map(e => e[0] + ':' + e[1]).join(',')")
            .AsString().Should().Be("0:undefined,1:undefined");

        _engine.Evaluate("[...Array.prototype.entries.call({length: 3, 0: 'a', 2: 'c'})].map(e => e[0] + ':' + e[1]).join(',')")
            .AsString().Should().Be("0:a,1:undefined,2:c");
    }

    /// <summary>
    /// <c>Array.prototype[Symbol.iterator]</c> is the very same function object as
    /// <c>Array.prototype.values</c> (ECMA-262 23.1.3.41), which is why one gate broke both.
    /// </summary>
    [Fact]
    public void SymbolIteratorIsTheSameFunctionObjectAsValues()
    {
        _engine.Evaluate("Array.prototype[Symbol.iterator] === Array.prototype.values").AsBoolean().Should().BeTrue();
        _engine.Evaluate("[...{[Symbol.iterator]: Array.prototype[Symbol.iterator], length: 2, 0: 'a', 1: 'b'}].join(',')")
            .AsString().Should().Be("a,b");
    }

    /// <summary>
    /// The lane pin behind the "no performance regression" claim: dropping the gate must not have routed a
    /// real array through the generic array-like iterator. <i>CreateArrayIterator</i>'s dispatch is a type
    /// test on the receiver and <i>ToObject</i> hands an object straight back, so a <c>JsArray</c> still
    /// reaches the dense iterator and everything else the array-like one.
    /// </summary>
    [Fact]
    public void AnArrayReceiverStillReachesTheDenseArrayIterator()
    {
        _engine.Evaluate("[1, 2].values()").GetType().Name.Should().Be("ArrayIterator");
        _engine.Evaluate("[1, 2].keys()").GetType().Name.Should().Be("ArrayIterator");
        _engine.Evaluate("[1, 2].entries()").GetType().Name.Should().Be("ArrayIterator");
        _engine.Evaluate("Array.prototype.values.call([1, 2])").GetType().Name.Should().Be("ArrayIterator");
        _engine.Evaluate("Array.prototype.values.call({length: 2})").GetType().Name.Should().Be("ArrayLikeIterator");
    }

    /// <summary>
    /// <c>arguments</c> already satisfied the old gate; it must keep working through the ungated path.
    /// </summary>
    [Fact]
    public void ArgumentsObjectsStillIterate()
    {
        _engine.Evaluate("(function () { return [...Array.prototype.values.call(arguments)].join(','); })('p', 'q')")
            .AsString().Should().Be("p,q");
    }
}
