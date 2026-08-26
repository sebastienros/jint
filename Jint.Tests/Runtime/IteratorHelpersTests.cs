namespace Jint.Tests.Runtime;

public class IteratorHelpersTests
{
    [Test]
    public void ToArrayCollectsAllValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 1; yield 2; yield 3; }
            JSON.stringify([
                [1, 2, 3].values().toArray(),
                gen().toArray(),
                new Set(['a', 'b']).values().toArray()
            ]);
            """).AsString();

        result.Should().Be("[[1,2,3],[1,2,3],[\"a\",\"b\"]]");
    }

    [Test]
    public void ToArrayWorksThroughHelperChain()
    {
        var engine = new Engine();
        var result = engine.Evaluate("JSON.stringify([1, 2, 3, 4, 5].values().drop(1).take(3).map(x => x * 10).toArray())").AsString();

        result.Should().Be("[20,30,40]");
    }

    [Test]
    public void ToArrayReturnsPlainArray()
    {
        var engine = new Engine();
        var result = engine.Evaluate("Array.isArray([].values().toArray()) && [].values().toArray().length === 0").AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void JoinConcatenatesUsingSeparator()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 'a'; yield 'b'; }
            JSON.stringify([
                [].values().join(),
                ['one'].values().join(),
                ['one', 'two', 'three'].values().join(),
                ['one', 'two', 'three'].values().join('&&'),
                ['one', 'two', 'three'].values().join(''),
                gen().join('-'),
                [1, 2, 3, 4, 5].values().drop(1).take(3).map(x => x * 10).join('/')
            ]);
            """).AsString();

        result.Should().Be("""["","one","one,two,three","one&&two&&three","onetwothree","a-b","20/30/40"]""");
    }

    [Test]
    public void JoinFormatsNullishValuesAsEmptyString()
    {
        var engine = new Engine();
        var result = engine.Evaluate("['one', null, 'two', undefined, 'three'].values().join()").AsString();

        result.Should().Be("one,,two,,three");
    }

    [Test]
    public void JoinCoercesSeparatorBeforeReadingNext()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const effects = [];
            const separator = { toString() { effects.push('toString'); return '&&'; } };
            let n = 0;
            const it = {
                get next() {
                    effects.push('get next');
                    return () => ++n <= 2 ? { done: false, value: n === 1 ? 'one' : 'two' } : { done: true };
                }
            };
            Iterator.prototype.join.call(it, separator) + '|' + effects.join(',');
            """).AsString();

        result.Should().Be("one&&two|toString,get next");
    }

    [Test]
    public void JoinClosesIteratorWhenCoercionThrows()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const throwy = { toString() { throw new Error('nope'); } };
            function makeIterator(value) {
                return {
                    closed: false,
                    next() { return this.done ? { done: true } : (this.done = true, { done: false, value }); },
                    return() { this.closed = true; }
                };
            }

            const onSeparator = makeIterator('x');
            try { Iterator.prototype.join.call(onSeparator, throwy); } catch { }

            const onContents = makeIterator(throwy);
            try { Iterator.prototype.join.call(onContents); } catch { }

            // an iterator that simply runs out must NOT be closed
            const onExhaustion = makeIterator('x');
            Iterator.prototype.join.call(onExhaustion);

            JSON.stringify([onSeparator.closed, onContents.closed, onExhaustion.closed]);
            """).AsString();

        result.Should().Be("[true,true,false]");
    }

    [Test]
    public void JoinThrowsOnNonObjectReceiver()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            [undefined, null, false, 0, 0n, '', Symbol()].every(receiver => {
                try {
                    Iterator.prototype.join.call(receiver);
                    return false;
                } catch (e) {
                    return e instanceof TypeError;
                }
            });
            """).AsBoolean();

        result.Should().BeTrue();
    }

    // IteratorClose step 4 - "If completion is a throw completion, return ? completion" - is reached
    // before innerResult is inspected, so the error that triggered the close outranks anything the
    // close itself raises. That covers the GetMethod of step 2, not just the call of step 3.c.
    private const string ClosingReceivers = """
        // "return" is a throwing accessor: GetMethod itself completes abruptly
        function throwingGetter() {
            return {
                __proto__: proto,
                get next() { throw new Error('next must not be read'); },
                get return() { throw new Error('SWALLOWED'); }
            };
        }

        // "return" is present but not callable: GetMethod throws a TypeError of its own
        function badReturnValue() {
            return {
                __proto__: proto,
                get next() { throw new Error('next must not be read'); },
                return: 42
            };
        }

        // the already-correct control: the close CALL throws
        function throwingCall() {
            return {
                __proto__: proto,
                get next() { throw new Error('next must not be read'); },
                return() { throw new Error('SWALLOWED'); }
            };
        }

        function raised(fn) {
            try {
                fn();
                return '<no throw>';
            } catch (e) {
                return e.constructor.name + ': ' + e.message;
            }
        }
        """;

    // every helper that validates an argument before building the iterator record closes the
    // receiver, and every one of them used to surrender its error to a throwing "return" getter
    [TestCase("map")]
    [TestCase("filter")]
    [TestCase("flatMap")]
    [TestCase("forEach")]
    [TestCase("some")]
    [TestCase("every")]
    [TestCase("find")]
    [TestCase("reduce")]
    public void ClosingTheReceiverNeverReplacesTheValidationError(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const proto = Iterator.prototype;
            {{ClosingReceivers}}
            JSON.stringify([
                raised(() => proto.{{method}}.call(throwingGetter())),
                raised(() => proto.{{method}}.call(badReturnValue())),
                raised(() => proto.{{method}}.call(throwingCall()))
            ]);
            """).AsString();

        result.Should().Be("""["TypeError: Argument must be callable","TypeError: Argument must be callable","TypeError: Argument must be callable"]""");
    }

    // take/drop raise a RangeError, so a close that leaks its own error changes the observable
    // error TYPE, not just its message
    [TestCase("take", "NaN")]
    [TestCase("take", "-1")]
    [TestCase("drop", "NaN")]
    [TestCase("drop", "-1")]
    public void ClosingTheReceiverNeverReplacesTheLimitRangeError(string method, string limit)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const proto = Iterator.prototype;
            {{ClosingReceivers}}
            JSON.stringify([
                raised(() => proto.{{method}}.call(throwingGetter(), {{limit}})),
                raised(() => proto.{{method}}.call(badReturnValue(), {{limit}})),
                raised(() => proto.{{method}}.call(throwingCall(), {{limit}}))
            ]);
            """).AsString();

        var expected = $"RangeError: {(limit == "NaN" ? "NaN" : "-1")} must be positive";
        result.Should().Be($"""["{expected}","{expected}","{expected}"]""");
    }

    [Test]
    public void ClosingTheReceiverNeverReplacesACoercionError()
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const proto = Iterator.prototype;
            {{ClosingReceivers}}
            const uncoercible = { valueOf() { throw new Error('FROM-VALUEOF'); } };
            const unstringifiable = { toString() { throw new Error('FROM-TOSTRING'); } };
            JSON.stringify([
                raised(() => proto.take.call(throwingGetter(), uncoercible)),
                raised(() => proto.drop.call(badReturnValue(), uncoercible)),
                // join closes on a separator whose ToString throws, before "next" is ever read
                raised(() => proto.join.call(throwingGetter(), unstringifiable)),
                raised(() => proto.join.call(badReturnValue(), unstringifiable))
            ]);
            """).AsString();

        result.Should().Be("""["Error: FROM-VALUEOF","Error: FROM-VALUEOF","Error: FROM-TOSTRING","Error: FROM-TOSTRING"]""");
    }

    [TestCase("map")]
    [TestCase("filter")]
    [TestCase("flatMap")]
    [TestCase("forEach")]
    [TestCase("some")]
    [TestCase("every")]
    [TestCase("find")]
    [TestCase("reduce")]
    public void AsyncClosingTheReceiverNeverReplacesTheValidationError(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const proto = AsyncIterator.prototype;
            {{ClosingReceivers}}
            JSON.stringify([
                raised(() => proto.{{method}}.call(throwingGetter())),
                raised(() => proto.{{method}}.call(badReturnValue())),
                raised(() => proto.{{method}}.call(throwingCall()))
            ]);
            """).AsString();

        result.Should().Be("""["TypeError: Argument must be callable","TypeError: Argument must be callable","TypeError: Argument must be callable"]""");
    }

    [TestCase("take")]
    [TestCase("drop")]
    public void AsyncClosingTheReceiverNeverReplacesTheLimitRangeError(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const proto = AsyncIterator.prototype;
            {{ClosingReceivers}}
            JSON.stringify([
                raised(() => proto.{{method}}.call(throwingGetter(), NaN)),
                raised(() => proto.{{method}}.call(badReturnValue(), NaN)),
                raised(() => proto.{{method}}.call(throwingCall(), NaN))
            ]);
            """).AsString();

        result.Should().Be("""["RangeError: NaN must be positive","RangeError: NaN must be positive","RangeError: NaN must be positive"]""");
    }

    [Test]
    public void ClosingTheReceiverStillHappens()
    {
        // the fix suppresses the close's own error, it must not suppress the close
        var engine = new Engine();
        var result = engine.Evaluate("""
            let closed = 0;
            const closable = {
                __proto__: Iterator.prototype,
                get next() { throw new Error('next must not be read'); },
                return() { closed++; return {}; }
            };

            try { closable.map(); } catch { }
            try { closable.take(NaN); } catch { }
            closed;
            """).AsNumber();

        result.Should().Be(2);
    }

    [TestCase("take")]
    [TestCase("drop")]
    public void LimitAtOrBelowMaxSafeIntegerIsAccepted(string method)
    {
        // Number.MAX_SAFE_INTEGER is the inclusive upper bound, and Infinity is exempt from the
        // bound entirely because the spec step only rejects a *finite* limit above it.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function* gen() {}
            const raised = f => { try { f(); return 'ok'; } catch (e) { return e.constructor.name; } };
            JSON.stringify([
                raised(() => gen().{{method}}(0)),
                raised(() => gen().{{method}}(Number.MAX_SAFE_INTEGER)),
                raised(() => gen().{{method}}(Infinity))
            ]);
            """).AsString();

        result.Should().Be("""["ok","ok","ok"]""");
    }

    [TestCase("take")]
    [TestCase("drop")]
    public void FiniteLimitAboveMaxSafeIntegerThrowsRangeError(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function* gen() {}
            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            JSON.stringify([
                raised(() => gen().{{method}}(Number.MAX_SAFE_INTEGER + 1)),
                raised(() => gen().{{method}}(Number.MAX_SAFE_INTEGER + 3)),
                raised(() => gen().{{method}}(2 ** 53))
            ]);
            """).AsString();

        result.Should().Be("""["RangeError","RangeError","RangeError"]""");
    }

    [TestCase("take")]
    [TestCase("drop")]
    public void AnOversizedLimitClosesTheReceiverWithoutReadingNext(string method)
    {
        // The spec validates against an Iterator Record whose [[NextMethod]] is still undefined, so
        // the close must reach "return" while "next" stays untouched.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            let closed = 0;
            const closable = {
                __proto__: Iterator.prototype,
                get next() { throw new Error('next must not be read'); },
                return() { closed++; return {}; }
            };

            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const error = raised(() => closable.{{method}}(Number.MAX_SAFE_INTEGER + 1));
            JSON.stringify([error, closed]);
            """).AsString();

        result.Should().Be("""["RangeError",1]""");
    }

    [TestCase("take")]
    [TestCase("drop")]
    public void AsyncFiniteLimitAboveMaxSafeIntegerThrowsRangeError(string method)
    {
        // Nothing in test262 covers AsyncIterator, so this is what keeps the two limit validators
        // from drifting apart.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const iter = { __proto__: AsyncIterator.prototype, next() { return Promise.resolve({ done: true }); } };
            JSON.stringify([
                raised(() => AsyncIterator.prototype.{{method}}.call(iter, Number.MAX_SAFE_INTEGER + 1)),
                raised(() => AsyncIterator.prototype.{{method}}.call(iter, Number.MAX_SAFE_INTEGER)),
                raised(() => AsyncIterator.prototype.{{method}}.call(iter, Infinity))
            ]);
            """).AsString();

        result.Should().Be("""["RangeError","no throw","no throw"]""");
    }

    [Test]
    public void ChunksYieldsConsecutiveNonOverlappingArrays()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 0; yield 1; yield 2; yield 3; yield 4; }
            JSON.stringify([
                Array.from(gen().chunks(2)),
                Array.from(gen().chunks(1)),
                Array.from(gen().chunks(5)),
                Array.from(gen().chunks(100))
            ]);
            """).AsString();

        // A final chunk shorter than chunkSize is still yielded.
        result.Should().Be("[[[0,1],[2,3],[4]],[[0],[1],[2],[3],[4]],[[0,1,2,3,4]],[[0,1,2,3,4]]]");
    }

    [Test]
    public void WindowsSlidesOneElementAtATime()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 0; yield 1; yield 2; yield 3; yield 4; }
            JSON.stringify([
                Array.from(gen().windows(2)),
                Array.from(gen().windows(1)),
                Array.from(gen().windows(3))
            ]);
            """).AsString();

        result.Should().Be("[[[0,1],[1,2],[2,3],[3,4]],[[0],[1],[2],[3],[4]],[[0,1,2],[1,2,3],[2,3,4]]]");
    }

    [Test]
    public void WindowsUndersizedDefaultsToOnlyFull()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 0; yield 1; yield 2; }
            JSON.stringify([
                Array.from(gen().windows(100)),
                Array.from(gen().windows(100, undefined)),
                Array.from(gen().windows(100, 'only-full')),
                Array.from(gen().windows(100, 'allow-partial'))
            ]);
            """).AsString();

        // Only "allow-partial" yields the never-filled trailing window.
        result.Should().Be("[[],[],[],[[0,1,2]]]");
    }

    [Test]
    public void ChunksAndWindowsYieldDistinctArrays()
    {
        // windows reuses its buffer across yields, so each yielded array has to be a copy.
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 0; yield 1; yield 2; yield 3; }
            const chunks = Array.from(gen().chunks(2));
            const windows = Array.from(gen().windows(2));
            JSON.stringify([
                chunks[0] !== chunks[1],
                windows[0] !== windows[1],
                windows.every(Array.isArray),
                JSON.stringify(windows[0])
            ]);
            """).AsString();

        result.Should().Be("""[true,true,true,"[0,1]"]""");
    }

    [Test]
    public void WindowsSlidesBeforeAppendingWhenTheUnderlyingIteratorAdvancesInParallel()
    {
        // Stealing an element from underneath the helper proves the drop-then-append ordering:
        // the buffer is [0,1], 2 goes to the outside caller, so the next window is [1,3] not [1,2].
        var engine = new Engine();
        var result = engine.Evaluate("""
            const iterator = (function* () { for (let i = 0; i < 6; ++i) yield i; })();
            const windowed = iterator.windows(2);
            const first = windowed.next().value;
            const stolen = iterator.next().value;
            JSON.stringify([first, stolen, windowed.next().value, windowed.next().value]);
            """).AsString();

        result.Should().Be("[[0,1],2,[1,3],[3,4]]");
    }

    [TestCase("chunks")]
    [TestCase("windows")]
    public void ChunkAndWindowSizeAreNotCoerced(string method)
    {
        // The size is taken verbatim - no ToNumber - so anything that is not already an integral
        // Number is a TypeError, which is where NaN and both infinities land too.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function* gen() {}
            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const sizes = ['1', true, null, undefined, {}, [2], Symbol(), NaN, 0.5, 1.5, Infinity, -Infinity];
            JSON.stringify(sizes.map(s => raised(() => gen().{{method}}(s))));
            """).AsString();

        result.Should().Be("""["TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError"]""");
    }

    [TestCase("chunks")]
    [TestCase("windows")]
    public void ChunkAndWindowSizeMustBeWithinTheValidRange(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            function* gen() {}
            const raised = f => { try { f(); return 'ok'; } catch (e) { return e.constructor.name; } };
            JSON.stringify([
                raised(() => gen().{{method}}(0)),
                raised(() => gen().{{method}}(-0)),
                raised(() => gen().{{method}}(-1)),
                raised(() => gen().{{method}}(2 ** 32)),
                raised(() => gen().{{method}}(2 ** 53)),
                raised(() => gen().{{method}}(1)),
                raised(() => gen().{{method}}(2 ** 32 - 1))
            ]);
            """).AsString();

        result.Should().Be("""["RangeError","RangeError","RangeError","RangeError","RangeError","ok","ok"]""");
    }

    [Test]
    public void WindowsRejectsAnInvalidUndersizedWithoutCoercingIt()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() {}
            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const values = [null, '', 'something else', 0, true, false, {}, Symbol(), new String('only-full')];
            JSON.stringify(values.map(v => raised(() => gen().windows(1, v))));
            """).AsString();

        result.Should().Be("""["TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError"]""");
    }

    [TestCase("chunks")]
    [TestCase("windows")]
    public void AnInvalidSizeClosesTheReceiverWithoutReadingNext(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            let closed = 0;
            const closable = () => ({
                __proto__: Iterator.prototype,
                get next() { throw new Error('next must not be read'); },
                return() { closed++; return {}; }
            });

            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const type = raised(() => closable().{{method}}('nope'));
            const range = raised(() => closable().{{method}}(0));
            JSON.stringify([type, range, closed]);
            """).AsString();

        result.Should().Be("""["TypeError","RangeError",2]""");
    }

    [Test]
    public void WindowsValidatesSizeBeforeUndersized()
    {
        // An invalid size wins even when undersized is also invalid, and an invalid undersized is
        // still caught before "next" is ever read.
        var engine = new Engine();
        var result = engine.Evaluate("""
            let reads = 0;
            const probe = () => ({ get next() { reads++; return function () { return { done: true }; }; } });
            const raised = f => { try { f(); return 'ok'; } catch (e) { return e.constructor.name; } };

            const both = raised(() => Iterator.prototype.windows.call(probe(), 0, 'bad'));
            const onlyUndersized = raised(() => Iterator.prototype.windows.call(probe(), 1, 'bad'));
            const valid = raised(() => Iterator.prototype.windows.call(probe(), 1));
            JSON.stringify([both, onlyUndersized, valid, reads]);
            """).AsString();

        // Only the fully valid call reaches GetIteratorDirect, so "next" is read exactly once.
        result.Should().Be("""["RangeError","TypeError","ok",1]""");
    }

    [TestCase("chunks(2)")]
    [TestCase("windows(2)")]
    public void ChunkingHelpersComposeWithTheOtherHelpers(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            JSON.stringify([1, 2, 3, 4, 5, 6, 7].values().drop(1).{{call}}.map(a => a.join('-')).toArray());
            """).AsString();

        var expected = call.StartsWith("chunks", StringComparison.Ordinal)
            ? """["2-3","4-5","6-7"]"""
            : """["2-3","3-4","4-5","5-6","6-7"]""";
        result.Should().Be(expected);
    }

    [TestCase("chunks")]
    [TestCase("windows")]
    public void ChunkingHelpersForwardReturnAndStopAfterExhaustion(string method)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            let closed = 0;
            const source = {
                __proto__: Iterator.prototype,
                i: 0,
                next() { return this.i < 4 ? { done: false, value: this.i++ } : { done: true }; },
                return() { closed++; return {}; }
            };

            const helper = source.{{method}}(2);
            helper.next();
            helper.return();
            const afterEarlyReturn = closed;

            const drained = {
                __proto__: Iterator.prototype,
                i: 0,
                next() { return this.i < 4 ? { done: false, value: this.i++ } : { done: true }; },
                return() { closed++; return {}; }
            };
            const all = drained.{{method}}(2);
            while (!all.next().done) { }
            all.return();

            JSON.stringify([afterEarlyReturn, closed]);
            """).AsString();

        // An early return() forwards to the underlying iterator; natural exhaustion does not, and a
        // return() after exhaustion must not forward either.
        result.Should().Be("[1,1]");
    }

    [Test]
    public void IncludesFindsAValueAndSkipsLeadingElements()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 1; yield 2; yield 3; }
            JSON.stringify([
                gen().includes(2),
                gen().includes(9),
                gen().includes(1),
                gen().includes(1, 1),
                gen().includes(3, 2),
                gen().includes(3, 3)
            ]);
            """).AsString();

        result.Should().Be("[true,false,true,false,true,false]");
    }

    [Test]
    public void IncludesComparesWithSameValueZero()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const sym = Symbol();
            JSON.stringify([
                [NaN].values().includes(NaN),
                [0].values().includes(-0),
                [-0].values().includes(0),
                [{}].values().includes({}),
                [sym].values().includes(sym)
            ]);
            """).AsString();

        // SameValueZero: NaN matches itself and the two zeroes match, but objects are by identity.
        result.Should().Be("[true,true,true,false,true]");
    }

    [Test]
    public void IncludesSkippedElementsIsNotCoerced()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 1; }
            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const values = [NaN, 0.1, -0.1, '1', true, null, {}, Symbol(), [2]];
            JSON.stringify(values.map(v => raised(() => gen().includes(0, v))));
            """).AsString();

        // Only +/-Infinity and integral Numbers are accepted, so NaN is a TypeError and not a
        // RangeError - there is no ToNumber step that could turn a string into a number first.
        result.Should().Be("""["TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError","TypeError"]""");
    }

    [Test]
    public void IncludesRejectsNegativeAndOversizedSkippedElements()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function* gen() { yield 1; }
            const raised = f => { try { f(); return 'ok'; } catch (e) { return e.constructor.name; } };
            JSON.stringify([
                raised(() => gen().includes(0, -1)),
                raised(() => gen().includes(0, -Infinity)),
                raised(() => gen().includes(0, Number.MAX_SAFE_INTEGER + 1)),
                raised(() => gen().includes(0, -0)),
                raised(() => gen().includes(0, 0)),
                raised(() => gen().includes(0, Infinity)),
                raised(() => gen().includes(0, Number.MAX_SAFE_INTEGER))
            ]);
            """).AsString();

        // -0 passes because -0 < -0 is false, and +Infinity is exempt from the upper bound.
        result.Should().Be("""["RangeError","RangeError","RangeError","ok","ok","ok","ok"]""");
    }

    [Test]
    public void IncludesClosesOnAMatchButNotOnExhaustion()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let closed = 0;
            const source = () => ({
                __proto__: Iterator.prototype,
                i: 0,
                next() { return this.i < 3 ? { done: false, value: this.i++ } : { done: true }; },
                return() { closed++; return {}; }
            });

            const matched = source().includes(1);
            const afterMatch = closed;
            const missed = source().includes(99);
            JSON.stringify([matched, afterMatch, missed, closed]);
            """).AsString();

        result.Should().Be("[true,1,false,1]");
    }

    [Test]
    public void IncludesValidationFailureClosesTheReceiverWithoutReadingNext()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let closed = 0;
            const closable = () => ({
                __proto__: Iterator.prototype,
                get next() { throw new Error('next must not be read'); },
                return() { closed++; return {}; }
            });

            const raised = f => { try { f(); return 'no throw'; } catch (e) { return e.constructor.name; } };
            const type = raised(() => closable().includes(0, NaN));
            const range = raised(() => closable().includes(0, -1));
            JSON.stringify([type, range, closed]);
            """).AsString();

        result.Should().Be("""["TypeError","RangeError",2]""");
    }

    [Test]
    public void IncludesWithInfiniteSkipNeverMatches()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            let returnCalls = 0;
            let count = 0;
            const iter = {
                __proto__: Iterator.prototype,
                next() { return ++count < 4 ? { done: false, value: count } : { done: true }; },
                return() { returnCalls++; return {}; }
            };

            JSON.stringify([iter.includes(1, Infinity), returnCalls, count]);
            """).AsString();

        // Every element is skipped, so the iterator runs to natural exhaustion and is not closed.
        result.Should().Be("[false,0,4]");
    }

    [Test]
    public void IncludesComposesWithTheOtherHelpers()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            JSON.stringify([
                [1, 2, 3, 4, 5].values().map(x => x * 2).includes(6),
                [1, 2, 3, 4, 5].values().drop(3).includes(1),
                [1, 2, 3, 4, 5].values().filter(x => x % 2 === 0).includes(4)
            ]);
            """).AsString();

        result.Should().Be("[true,false,true]");
    }

    // An early exit runs `Return ? IteratorClose(iterated, NormalCompletion(...))` as its final step,
    // which sits *outside* the IfAbruptCloseIterator guard covering the predicate call. "return" is
    // therefore invoked exactly once and whatever it raises is the error the caller sees - it must not
    // be re-entered by the close that guards the abrupt predicate.
    private const string EarlyExitReceiver = """
        let calls = 0;
        const it = {
            __proto__: Iterator.prototype,
            i: 0,
            next() { return this.i++ === 0 ? { done: false, value: 42 } : { done: true }; },
            return() { calls++; return RETURN_RESULT; }
        };
        let outcome = 'no throw';
        try { CALL; } catch (e) { outcome = e.constructor.name + ': ' + e.message; }
        JSON.stringify([calls, outcome]);
        """;

    private static string EarlyExitScript(string call, string returnResult) =>
        EarlyExitReceiver.Replace("RETURN_RESULT", returnResult).Replace("CALL", call);

    [TestCase("it.includes(42)")]
    [TestCase("it.some(() => true)")]
    [TestCase("it.every(() => false)")]
    [TestCase("it.find(() => true)")]
    [TestCase("it.take(0).next()")]
    public void AnEarlyExitCallsReturnExactlyOnce(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(EarlyExitScript(call, "{ done: true }")).AsString();

        result.Should().Be("""[1,"no throw"]""");
    }

    [TestCase("it.includes(42)")]
    [TestCase("it.some(() => true)")]
    [TestCase("it.every(() => false)")]
    [TestCase("it.find(() => true)")]
    [TestCase("it.take(0).next()")]
    public void AnEarlyExitDoesNotReenterAThrowingReturn(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(EarlyExitScript(call, "(() => { throw new Error('boom'); })()")).AsString();

        result.Should().Be("""[1,"Error: boom"]""");
    }

    [TestCase("it.includes(42)")]
    [TestCase("it.some(() => true)")]
    [TestCase("it.every(() => false)")]
    [TestCase("it.find(() => true)")]
    [TestCase("it.take(0).next()")]
    public void AnEarlyExitDoesNotReenterAReturnThatYieldsANonObject(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(EarlyExitScript(call, "7")).AsString();

        // IteratorClose step 6 raises the TypeError itself, so the close is still a single call.
        result.Should().Be("""[1,"TypeError: Iterator returned non-object"]""");
    }

    // IteratorStepValue marks the record done on every abrupt completion - a throwing next(), a
    // non-object result, a throwing "done" getter, a throwing "value" getter - and returns it. Every
    // helper spells the step as a bare `? IteratorStepValue(iterated)`, outside any
    // IfAbruptCloseIterator, so none of them may invoke "return".
    private const string AbruptStepReceiver = """
        let closed = 0;
        const it = {
            __proto__: Iterator.prototype,
            next() { return NEXT_RESULT; },
            return() { closed++; return { done: true }; },
            [Symbol.iterator]() { return this; }
        };
        let outcome = 'no throw';
        try { CALL; } catch (e) { outcome = e.message; }
        JSON.stringify([closed, outcome]);
        """;

    private static string AbruptStepScript(string call, string nextResult) =>
        AbruptStepReceiver.Replace("NEXT_RESULT", nextResult).Replace("CALL", call);

    // Every helper that consumes the underlying iterator, eager and lazy alike, plus Iterator.concat.
    // join was already right; it is kept as the control the others are brought level with.
    public static TestCases<string> ConsumingHelpers() =>
    [
        "it.includes(1)",
        "it.join(',')",
        "it.some(() => true)",
        "it.every(() => true)",
        "it.find(() => true)",
        "it.forEach(() => {})",
        "it.reduce((a, b) => a, 0)",
        "it.toArray()",
        "it.map(x => x).next()",
        "it.filter(() => true).next()",
        "it.take(5).next()",
        "it.drop(0).next()",
        "it.flatMap(x => [x]).next()",
        "it.chunks(2).next()",
        "it.windows(2).next()",
        "Iterator.concat(it).next()",
    ];

    [TestCaseSource(nameof(ConsumingHelpers))]
    public void AThrowingValueGetterDoesNotCloseTheIterator(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(
            AbruptStepScript(call, "{ done: false, get value() { throw new Error('value getter'); } }")).AsString();

        result.Should().Be("""[0,"value getter"]""");
    }

    [TestCaseSource(nameof(ConsumingHelpers))]
    public void AThrowingNextDoesNotCloseTheIterator(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(
            AbruptStepScript(call, "(() => { throw new Error('next'); })()")).AsString();

        result.Should().Be("""[0,"next"]""");
    }

    [TestCaseSource(nameof(ConsumingHelpers))]
    public void ANonObjectStepResultDoesNotCloseTheIterator(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(AbruptStepScript(call, "7")).AsString();

        result.Should().Be("""[0,"Iterator result 7 is not an object"]""");
    }

    [TestCaseSource(nameof(ConsumingHelpers))]
    public void AThrowingDoneGetterDoesNotCloseTheIterator(string call)
    {
        var engine = new Engine();
        var result = engine.Evaluate(
            AbruptStepScript(call, "{ get done() { throw new Error('done getter'); } }")).AsString();

        result.Should().Be("""[0,"done getter"]""");
    }

    // A step that completed abruptly left the record done and the helper's generator completed, so
    // %IteratorHelperPrototype%.return resumes nothing and forwards nothing either.
    [TestCase("it.map(x => x)")]
    [TestCase("it.filter(() => true)")]
    [TestCase("it.take(5)")]
    [TestCase("it.drop(0)")]
    [TestCase("it.flatMap(x => [x])")]
    [TestCase("it.chunks(2)")]
    [TestCase("it.windows(2)")]
    public void ReturnIsNotForwardedAfterAnAbruptStep(string helper)
    {
        var engine = new Engine();
        var result = engine.Evaluate(AbruptStepScript(
            $"const h = {helper}; try {{ h.next(); }} catch (e) {{ }} h.return()",
            "{ done: false, get value() { throw new Error('value getter'); } }")).AsString();

        result.Should().Be("""[0,"no throw"]""");
    }

    // The number in a range-check message came from script, so the message has to print it the way
    // JavaScript does. Interpolating the raw double instead formats it with the CLR's shortest
    // round-trip "G" rules and CultureInfo.CurrentCulture: 9007199254740992 comes out as
    // "9.007199254740992E+15", Number.MAX_VALUE as "1.7976931348623157E+308", and the separator and
    // the minus sign move with the ambient culture on top of that.
    public static TestCases<string, string> OutOfRangeLimits() =>
        new()
        {
            { "[].values().take(Number.MAX_SAFE_INTEGER + 1)", "9007199254740992 exceeds the maximum safe integer" },
            { "[].values().drop(Number.MAX_SAFE_INTEGER + 1)", "9007199254740992 exceeds the maximum safe integer" },
            { "[].values().includes(0, Number.MAX_SAFE_INTEGER + 1)", "9007199254740992 exceeds the maximum safe integer" },
            { "[].values().take(Number.MAX_VALUE)", "1.7976931348623157e+308 exceeds the maximum safe integer" },
            { "[].values().drop(Number.MAX_VALUE)", "1.7976931348623157e+308 exceeds the maximum safe integer" },
            { "[].values().includes(0, Number.MAX_VALUE)", "1.7976931348623157e+308 exceeds the maximum safe integer" },
            { "[].values().take(-1e21)", "-1e+21 must be positive" },
            { "[].values().drop(-1e21)", "-1e+21 must be positive" },
            // Nothing in test262 covers AsyncIterator, so its two validators are pinned here as well.
            { "AsyncIterator.prototype.take.call(asyncIter, Number.MAX_VALUE)", "1.7976931348623157e+308 exceeds the maximum safe integer" },
            { "AsyncIterator.prototype.drop.call(asyncIter, Number.MAX_VALUE)", "1.7976931348623157e+308 exceeds the maximum safe integer" },
            { "AsyncIterator.prototype.take.call(asyncIter, -1e21)", "-1e+21 must be positive" },
            { "AsyncIterator.prototype.drop.call(asyncIter, -1e21)", "-1e+21 must be positive" },
        };

    private static string OutOfRangeLimitMessage(string expression)
    {
        var engine = new Engine();
        return engine.Evaluate($$"""
            const asyncIter = { __proto__: AsyncIterator.prototype, next() { return Promise.resolve({ done: true }); } };
            (() => { try { {{expression}}; return 'no throw'; } catch (e) { return e.message; } })();
            """).AsString();
    }

    [TestCaseSource(nameof(OutOfRangeLimits))]
    public void AnOutOfRangeLimitPrintsTheNumberTheWayJavaScriptDoes(string expression, string expected)
    {
        OutOfRangeLimitMessage(expression).Should().Be(expected);
    }

    [TestCaseSource(nameof(OutOfRangeLimits))]
    [SetCulture("de-DE"), SetUICulture("de-DE")]
    public void AnOutOfRangeLimitIgnoresACommaDecimalSeparatorCulture(string expression, string expected)
    {
        OutOfRangeLimitMessage(expression).Should().Be(expected);
    }

    [TestCaseSource(nameof(OutOfRangeLimits))]
    [SetCulture("sv-SE"), SetUICulture("sv-SE")]
    public void AnOutOfRangeLimitIgnoresACultureWithANonAsciiMinusSign(string expression, string expected)
    {
        OutOfRangeLimitMessage(expression).Should().Be(expected);
    }

    [Test]
    public void EveryHelperObjectIsTaggedIteratorHelper()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const tag = v => Object.prototype.toString.call(v);
            JSON.stringify([
                tag([1].values().map(x => x)),
                tag([1].values().filter(() => true)),
                tag([1].values().take(1)),
                tag([1].values().drop(0)),
                tag([1].values().flatMap(x => [x])),
                tag([1].values().chunks(1)),
                tag([1].values().windows(1)),
                // concat and zip share %IteratorHelperPrototype%, so they are tagged with it too
                tag(Iterator.concat([1].values())),
                tag(Iterator.zip([[1].values()])),
                tag(Iterator.zipKeyed({ a: [1].values() }))
            ]);
            """).AsString();

        result.Should().Be(
            """["[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]","[object Iterator Helper]"]""");
    }

    [Test]
    public void AnAsyncHelperObjectIsTaggedAsyncIteratorHelper()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const asyncIter = { __proto__: AsyncIterator.prototype, next() { return Promise.resolve({ done: true }); } };
            const tag = v => Object.prototype.toString.call(v);
            const of = method => tag(AsyncIterator.prototype[method].call(asyncIter, x => x));
            JSON.stringify([of('map'), of('filter'), of('flatMap'), tag(AsyncIterator.prototype.take.call(asyncIter, 1))]);
            """).AsString();

        result.Should().Be(
            """["[object Async Iterator Helper]","[object Async Iterator Helper]","[object Async Iterator Helper]","[object Async Iterator Helper]"]""");
    }

    // The tag is a data property on the helper prototype, shadowing the accessor pair that
    // %Iterator.prototype% and %AsyncIterator.prototype% carry, with the usual toStringTag
    // attributes: { [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true }.
    [TestCase("Object.getPrototypeOf([1].values().map(x => x))", "Iterator Helper")]
    [TestCase("Object.getPrototypeOf(AsyncIterator.prototype.map.call(asyncIter, x => x))", "Async Iterator Helper")]
    public void TheHelperToStringTagIsANonWritableConfigurableDataProperty(string prototype, string tag)
    {
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const asyncIter = { __proto__: AsyncIterator.prototype, next() { return Promise.resolve({ done: true }); } };
            const proto = {{prototype}};
            const d = Object.getOwnPropertyDescriptor(proto, Symbol.toStringTag);
            JSON.stringify([
                Object.getOwnPropertySymbols(proto).length,
                d.value,
                d.writable,
                d.enumerable,
                d.configurable,
                typeof d.get,
                typeof d.set
            ]);
            """).AsString();

        result.Should().Be($$"""[1,"{{tag}}",false,false,true,"undefined","undefined"]""");
    }

    // %IteratorHelperPrototype%.return is a state machine over [[GeneratorState]]
    // (https://tc39.es/ecma262/#sec-%iteratorhelperprototype%.return), and the four tests below cover the
    // states it can be called in. Every expectation was taken from node (V8) running the same script.

    [TestCase("map", "x")]
    [TestCase("filter", "true")]
    [TestCase("flatMap", "[x]")]
    public void ReturnWhileExecutingIsATypeErrorAndClosesNothing(string method, string mapped)
    {
        // Step 6 hands an executing helper to GeneratorResumeAbrupt, whose GeneratorValidate rejects it.
        // Closing the underlying iterator instead would invoke "return" where the spec invokes nothing —
        // and leave the helper yielding from an iterator it had already closed.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            const trace = [];
            let i = 0;
            const src = {
                __proto__: Iterator.prototype,
                next() { trace.push('next'); return { value: ++i, done: false }; },
                return(v) { trace.push('return'); return { value: v, done: true }; }
            };
            let h;
            h = src.{{method}}(x => {
                try { h.return(); trace.push('inner-return-ok'); }
                catch (e) { trace.push('inner-' + e.constructor.name); }
                return {{mapped}};
            });
            trace.push('next1=' + JSON.stringify(h.next()));
            JSON.stringify(trace);
            """).AsString();

        result.Should().Be("""["next","inner-TypeError","next1={\"value\":1,\"done\":false}"]""");
    }

    [Test]
    public void ReturnWhileExecutingRejectsTheHelperBeingStepped()
    {
        // A helper stacked on another only makes the executing one observable from further away.
        var engine = new Engine();
        var result = engine.Evaluate("""
            const trace = [];
            let i = 0;
            const src = {
                __proto__: Iterator.prototype,
                next() { trace.push('next'); return { value: ++i, done: false }; },
                return(v) { trace.push('return'); return { value: v, done: true }; }
            };
            let inner;
            inner = src.map(x => {
                try { inner.return(); trace.push('inner-return-ok'); }
                catch (e) { trace.push('inner-' + e.constructor.name); }
                return x;
            });
            const outer = inner.map(x => x);
            trace.push('outer-next=' + JSON.stringify(outer.next()));
            JSON.stringify(trace);
            """).AsString();

        result.Should().Be("""["next","inner-TypeError","outer-next={\"value\":1,\"done\":false}"]""");
    }

    [Test]
    public void ReturnOnACompletedHelperClosesNothing()
    {
        // GeneratorResumeAbrupt answers a completed generator with a done result and resumes no body, so
        // nothing is left to close. flatMap is where that is observable: an inner iterator whose step threw
        // is already done — IteratorStepValue marked it so and closed nothing — and the abrupt step closed
        // the outer on its way out. A return afterwards must touch neither.
        var engine = new Engine();
        var result = engine.Evaluate("""
            const trace = [];
            let innerCount = 0;
            const inner = {
                next() { trace.push('inner-next'); if (++innerCount > 1) { throw new RangeError('inner boom'); } return { value: 'a', done: false }; },
                return(v) { trace.push('inner-return'); return { value: v, done: true }; },
                [Symbol.iterator]() { return this; }
            };
            const outer = {
                __proto__: Iterator.prototype,
                next() { trace.push('outer-next'); return { value: inner, done: false }; },
                return(v) { trace.push('outer-return'); return { value: v, done: true }; }
            };
            const h = outer.flatMap(x => x);
            trace.push('next1=' + JSON.stringify(h.next()));
            try { h.next(); } catch (e) { trace.push('next2-' + e.constructor.name); }
            trace.push('return=' + JSON.stringify(h.return()));
            trace.push('return2=' + JSON.stringify(h.return()));
            JSON.stringify(trace);
            """).AsString();

        result.Should().Be("""["outer-next","inner-next","next1={\"value\":\"a\",\"done\":false}","inner-next","outer-return","next2-RangeError","return={\"done\":true}","return2={\"done\":true}"]""");
    }

    [Test]
    public void ReturnBeforeTheFirstNextClosesTheUnderlyingIterator()
    {
        // Step 4's suspended-start branch: the body never ran, so the close is the whole of the work.
        var engine = new Engine();
        var result = engine.Evaluate("""
            const trace = [];
            const src = {
                __proto__: Iterator.prototype,
                next() { trace.push('next'); return { value: 1, done: false }; },
                return(v) { trace.push('return'); return { value: v, done: true }; }
            };
            const h = src.map(x => x);
            trace.push('return=' + JSON.stringify(h.return()));
            trace.push('next=' + JSON.stringify(h.next()));
            trace.push('return2=' + JSON.stringify(h.return()));
            JSON.stringify(trace);
            """).AsString();

        result.Should().Be("""["return","return={\"done\":true}","next={\"done\":true}","return2={\"done\":true}"]""");
    }

    [Test]
    public void ReturnFromInsideTheUnderlyingCloseSeesACompletedHelper()
    {
        // The state moves to completed *before* the close runs, so a "return" that re-enters the helper
        // from inside the underlying iterator's own "return" is answered rather than rejected. This is what
        // stops the executing check above from turning an ordinary close into a TypeError.
        var engine = new Engine();
        var result = engine.Evaluate("""
            const trace = [];
            let h;
            const src = {
                __proto__: Iterator.prototype,
                next() { trace.push('next'); return { value: 1, done: false }; },
                return(v) {
                    trace.push('src-return');
                    try { trace.push('nested=' + JSON.stringify(h.return())); }
                    catch (e) { trace.push('nested-' + e.constructor.name); }
                    return { value: v, done: true };
                }
            };
            h = src.map(x => x);
            trace.push('next1=' + JSON.stringify(h.next()));
            trace.push('outer-return=' + JSON.stringify(h.return()));
            JSON.stringify(trace);
            """).AsString();

        result.Should().Be("""["next","next1={\"value\":1,\"done\":false}","src-return","nested={\"done\":true}","outer-return={\"done\":true}"]""");
    }

    [Test]
    public void TheReceiverOfAHelperKeepsItsOwnToStringTag()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const tag = v => Object.prototype.toString.call(v);
            function* gen() { yield 1; }
            JSON.stringify([tag(Iterator.prototype), tag([1].values()), tag(gen()), tag(AsyncIterator.prototype)]);
            """).AsString();

        result.Should().Be("""["[object Iterator]","[object Array Iterator]","[object Generator]","[object AsyncIterator]"]""");
    }

    /// <summary>
    /// %WrapForValidIteratorPrototype%.next (https://tc39.es/ecma262/#sec-%wrapforvaliditeratorprototype%.next)
    /// is Call(nextMethod, iterator) and nothing else: it neither requires the result to be an Object
    /// nor reads `done` off it. Those belong to IteratorNext, performed by whoever consumes the
    /// wrapper as an iterator -- which calling .next() on it directly is not.
    /// </summary>
    [Test]
    public void WrapForValidIteratorNextHandsBackWhateverTheWrappedNextReturned()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const wrap = value => Iterator.from({ next: () => value });
            const values = [undefined, null, 0, false, 'test', Symbol('')];
            JSON.stringify(values.map(v => wrap(v).next() === v));
            """).AsString();

        result.Should().Be("[true,true,true,true,true,true]");
    }

    [Test]
    public void WrapForValidIteratorNextDoesNotReadDoneOffTheResult()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            const reads = [];
            const step = { get done() { reads.push('done'); return true; }, get value() { reads.push('value'); return 1; } };
            const wrapped = Iterator.from({ next: () => step });
            const returned = wrapped.next();
            JSON.stringify([returned === step, reads]);
            """).AsString();

        result.Should().Be("""[true,[]]""");
    }

    [Test]
    public void ConsumingAWrappedIteratorStillRequiresAnObjectResult()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (() => {
                try { Iterator.from({ next: () => undefined }).toArray(); return false; }
                catch (e) { return e instanceof TypeError; }
            })();
            """);

        result.AsBoolean().Should().BeTrue();
    }
}
