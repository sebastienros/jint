namespace Jint.Tests.Runtime;

public class IteratorHelpersTests
{
    [Fact]
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

    [Fact]
    public void ToArrayWorksThroughHelperChain()
    {
        var engine = new Engine();
        var result = engine.Evaluate("JSON.stringify([1, 2, 3, 4, 5].values().drop(1).take(3).map(x => x * 10).toArray())").AsString();

        result.Should().Be("[20,30,40]");
    }

    [Fact]
    public void ToArrayReturnsPlainArray()
    {
        var engine = new Engine();
        var result = engine.Evaluate("Array.isArray([].values().toArray()) && [].values().toArray().length === 0").AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
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

    [Fact]
    public void JoinFormatsNullishValuesAsEmptyString()
    {
        var engine = new Engine();
        var result = engine.Evaluate("['one', null, 'two', undefined, 'three'].values().join()").AsString();

        result.Should().Be("one,,two,,three");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Theory]
    // every helper that validates an argument before building the iterator record closes the
    // receiver, and every one of them used to surrender its error to a throwing "return" getter
    [InlineData("map")]
    [InlineData("filter")]
    [InlineData("flatMap")]
    [InlineData("forEach")]
    [InlineData("some")]
    [InlineData("every")]
    [InlineData("find")]
    [InlineData("reduce")]
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

    [Theory]
    // take/drop raise a RangeError, so a close that leaks its own error changes the observable
    // error TYPE, not just its message
    [InlineData("take", "NaN")]
    [InlineData("take", "-1")]
    [InlineData("drop", "NaN")]
    [InlineData("drop", "-1")]
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

    [Fact]
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

    [Theory]
    [InlineData("map")]
    [InlineData("filter")]
    [InlineData("flatMap")]
    [InlineData("forEach")]
    [InlineData("some")]
    [InlineData("every")]
    [InlineData("find")]
    [InlineData("reduce")]
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

    [Theory]
    [InlineData("take")]
    [InlineData("drop")]
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

    [Fact]
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

    [Theory]
    [InlineData("take")]
    [InlineData("drop")]
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

    [Theory]
    [InlineData("take")]
    [InlineData("drop")]
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

    [Theory]
    [InlineData("take")]
    [InlineData("drop")]
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

    [Theory]
    [InlineData("take")]
    [InlineData("drop")]
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
}
