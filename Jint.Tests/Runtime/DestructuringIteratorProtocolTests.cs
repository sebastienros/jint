using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The iterator protocol exactly as array destructuring and %ArrayIteratorPrototype% are specified to
/// drive it: every element of an array pattern steps only while the iterator record's [[Done]] is
/// still false (https://tc39.es/ecma262/#sec-runtime-semantics-iteratordestructuringassignmentevaluation),
/// a nested pattern is fed the stepped <em>value</em> rather than the iterator result object, and
/// GetIterator (https://tc39.es/ecma262/#sec-getiterator) reads <c>next</c> off whatever the
/// @@iterator method handed back.
/// </summary>
public class DestructuringIteratorProtocolTests
{
    /// <summary>
    /// A logging iterable. Every <c>next()</c> call, every <c>done</c> / <c>value</c> read of the
    /// result object and every <c>return()</c> close is appended to <c>log</c>. It is deliberately
    /// not an array, so <c>HandleArrayPattern</c>'s <c>ArrayOperations</c> fast path is bypassed and
    /// the real iterator protocol runs.
    /// </summary>
    private const string Prelude = """
        var log = [];
        function iterableOf(values) {
            var i = 0;
            return {
                [Symbol.iterator]() { return this; },
                next() {
                    log.push('next');
                    var has = i < values.length;
                    var v = has ? values[i++] : undefined;
                    return {
                        get done() { log.push('done'); return !has; },
                        get value() { log.push('value'); return v; }
                    };
                },
                return() { log.push('return'); return {}; }
            };
        }
        """;

    private static string Log(string script)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute(script);
        return engine.Evaluate("log.join(',')").AsString();
    }

    [Fact]
    public void AnExhaustedIteratorIsNeverSteppedAgain()
    {
        // 13.15.5.5: each element form is guarded by "If iteratorRecord.[[Done]] is false" — once a
        // step reported done, the remaining elements bind undefined without touching the iterator,
        // and the pattern does not close it either (IteratorClose only runs while [[Done]] is false).
        Log("var a, b, c; [a, b, c] = iterableOf([]);")
            .Should().Be("next,done");

        Log("var a, b, c; [a, b, c] = iterableOf([1]);")
            .Should().Be("next,done,value,next,done");

        Log("var a, b, c; [a, b, c] = iterableOf([1, 2]);")
            .Should().Be("next,done,value,next,done,value,next,done");

        // Every element consumed and none left over: three steps, no fourth, no close.
        Log("var a, b, c; [a, b, c] = iterableOf([1, 2, 3]);")
            .Should().Be("next,done,value,next,done,value,next,done,value,return");
    }

    [Fact]
    public void AnElisionNeverEndsThePattern()
    {
        // Elision is its own production and simply discards a step; the elements after it are still
        // evaluated, so they bind undefined — or run their initializer.
        var engine = new Engine();
        engine.Execute(Prelude);

        engine.Evaluate("var a; [, a] = iterableOf([]); String(a)").AsString().Should().Be("undefined");
        engine.Evaluate("var b; [, b = 5] = iterableOf([]); b").AsNumber().Should().Be(5);
        engine.Evaluate("var c; [, c = 5] = iterableOf([1]); c").AsNumber().Should().Be(5);
        engine.Evaluate("var d; [, d] = iterableOf([1, 2]); d").AsNumber().Should().Be(2);

        LogWithTarget("[, t.a] = iterableOf([]);").Should().Be("next,done,set a=undefined");
    }

    [Fact]
    public void APatternThatStopsShortClosesTheIterator()
    {
        // One step, then the pattern is exhausted while [[Done]] is still false, so IteratorClose runs.
        Log("var a; [a] = iterableOf([1, 2, 3]);")
            .Should().Be("next,done,value,return");
    }

    [Fact]
    public void ARestElementDoesNotStepAnAlreadyDoneIterator()
    {
        // AssignmentRestElement repeats "while iteratorRecord.[[Done]] is false", so an iterator that
        // the preceding element already drove to done yields an empty rest array with no further step,
        // and stays unclosed.
        Log("var a, rest; [a, ...rest] = iterableOf([]);")
            .Should().Be("next,done");

        Log("var a, rest; [a, ...rest] = iterableOf([1, 2]);")
            .Should().Be("next,done,value,next,done,value,next,done");
    }

    [Fact]
    public void ARestElementCollectsTheSteppedValues()
    {
        var engine = new Engine();
        engine.Execute(Prelude);

        engine.Evaluate("var a, rest; [a, ...rest] = iterableOf([]); a + '|' + rest.join(',') + '|' + rest.length")
            .AsString().Should().Be("undefined||0");

        engine.Evaluate("var b, more; [b, ...more] = iterableOf([1, 2, 3]); b + '|' + more.join(',')")
            .AsString().Should().Be("1|2,3");
    }

    [Fact]
    public void ANestedPatternReceivesTheSteppedValueNotTheIteratorResult()
    {
        // BindingElement : BindingPattern Initializer_opt binds the pattern to the *value* the step
        // produced. Feeding it the { value, done } result object instead is only invisible while the
        // right-hand side is a real array (which takes the ArrayOperations fast path), so every case
        // here iterates a non-array iterable.
        var engine = new Engine();
        engine.Execute(Prelude);

        engine.Evaluate("var [[a]] = iterableOf([[7]]); a").AsNumber().Should().Be(7);
        engine.Evaluate("var [[b, c]] = iterableOf([[7, 8]]); b + ',' + c").AsString().Should().Be("7,8");
        engine.Evaluate("var [{ p }] = iterableOf([{ p: 9 }]); p").AsNumber().Should().Be(9);
        engine.Evaluate("var [{ q: { r } }] = iterableOf([{ q: { r: 10 } }]); r").AsNumber().Should().Be(10);

        // Assignment (non-declaration) form of the same shapes.
        engine.Evaluate("var x; [[x]] = iterableOf([[11]]); x").AsNumber().Should().Be(11);
        engine.Evaluate("var y; [{ s: y }] = iterableOf([{ s: 12 }]); y").AsNumber().Should().Be(12);

        // A nested pattern also consumes exactly one step, and the done step is the value read the
        // guard above suppresses.
        Log("var a; [[a]] = iterableOf([[1]]);").Should().Be("next,done,value,return");
    }

    [Fact]
    public void ANestedPatternAgainstAnExhaustedIteratorThrows()
    {
        // The element gets undefined, and BindingInitialization of a pattern ToObject's it.
        var engine = new Engine();
        engine.Execute(Prelude);

        Invoking(() => engine.Evaluate("var [[a]] = iterableOf([]);"))
            .Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void TheOperationOrderOfAnArrayAssignmentPatternMatchesTheSpec()
    {
        // AssignmentElement evaluates its DestructuringAssignmentTarget reference first, then steps,
        // then PutValue's — and stops stepping the moment [[Done]] is true.
        LogWithTarget("[t.a, t.b, t.c] = iterableOf([1]);")
            .Should().Be("next,done,value,set a=1,next,done,set b=undefined,set c=undefined");

        // An elision steps (and reads the value, per IteratorStepValue) but assigns nothing; the
        // pattern then stops short of the iterator's end, so IteratorClose runs.
        LogWithTarget("[t.a, , t.b] = iterableOf([1, 2, 3, 4]);")
            .Should().Be("next,done,value,set a=1,next,done,value,next,done,value,set b=3,return");

        // A rest element drains what is left, and the drained iterator is not closed afterwards.
        LogWithTarget("[t.a, ...t.rest] = iterableOf([1, 2, 3]);")
            .Should().Be("next,done,value,set a=1,next,done,value,next,done,value,next,done,set rest=2,3");
    }

    private static string LogWithTarget(string script)
    {
        var engine = new Engine();
        engine.Execute(Prelude);
        engine.Execute("""
            var t = new Proxy({}, {
                set(target, key, value) {
                    log.push('set ' + key + '=' + String(value));
                    target[key] = value;
                    return true;
                }
            });
            """);
        engine.Execute(script);
        return engine.Evaluate("log.join(',')").AsString();
    }

    [Fact]
    public void AReplacedArrayIteratorNextKeepsIteratingTheRealObject()
    {
        // %ArrayIteratorPrototype%.next replaced by a wrapper that delegates to the original: the
        // object the array iterator was created for has to survive, so the delegating call still
        // steps the array rather than the prototype.
        new Engine().Evaluate("""
            var proto = Object.getPrototypeOf([][Symbol.iterator]());
            var original = proto.next;
            var calls = 0;
            proto.next = function () { calls++; return original.apply(this, arguments); };
            var out;
            try {
                out = Array.from([1, 2, 3]);
            } finally {
                proto.next = original;
            }
            out.join(',') + '|' + calls;
            """).AsString().Should().Be("1,2,3|4");

        // The shape staging/sm/Array/for_of_2.js exercises: the replacement is installed part-way
        // through, by a getter that runs while the iteration is already in flight.
        new Engine().Evaluate("""
            var proto = Object.getPrototypeOf([][Symbol.iterator]());
            var original = proto.next;
            var arr = [0, 1, 2];
            Object.defineProperty(arr, 1, { get: function () { proto.next = replacement; return 1; } });
            var replacement = function () { return original.apply(this, arguments); };
            var sum = 0;
            try {
                for (var i = 0; i < 10; i++) {
                    new Set(arr).forEach(function (v) { sum += v; });
                }
            } finally {
                proto.next = original;
            }
            sum;
            """).AsNumber().Should().Be(30);
    }

    [Fact]
    public void AnOwnNextOnAnArrayIteratorIsHonoured()
    {
        // GetIterator step 3 reads `next` off the object @@iterator returned, so an own `next`
        // shadowing %ArrayIteratorPrototype%.next is what drives the iteration.
        new Engine().Evaluate("""
            var it = [1, 2, 3][Symbol.iterator]();
            var original = Object.getPrototypeOf(it).next;
            var calls = 0;
            it.next = function () { calls++; return original.call(this); };
            var out = Array.from(it);
            out.join(',') + '|' + calls;
            """).AsString().Should().Be("1,2,3|4");

        // Same through spread, which resolves its iterator the same way.
        new Engine().Evaluate("""
            var it = [1, 2][Symbol.iterator]();
            var original = Object.getPrototypeOf(it).next;
            var calls = 0;
            it.next = function () { calls++; return original.call(this); };
            [...it].join(',') + '|' + calls;
            """).AsString().Should().Be("1,2|3");
    }

    [Fact]
    public void APristineArrayIteratorStillIteratesNatively()
    {
        // Guard against the wrap above engaging when nothing was replaced.
        var engine = new Engine();
        engine.Evaluate("Array.from([1, 2, 3]).join(',')").AsString().Should().Be("1,2,3");
        engine.Evaluate("[...[1, 2, 3]].join(',')").AsString().Should().Be("1,2,3");
        engine.Evaluate("Array.from([1, 2, 3][Symbol.iterator]()).join(',')").AsString().Should().Be("1,2,3");
        engine.Evaluate("Array.from([1, 2, 3].entries()).map(e => e.join(':')).join(',')").AsString().Should().Be("0:1,1:2,2:3");
        engine.Evaluate("Array.from([1, 2, 3].keys()).join(',')").AsString().Should().Be("0,1,2");
        engine.Evaluate("var s = 0; for (var v of [1, 2, 3].values()) s += v; s").AsNumber().Should().Be(6);
        engine.Evaluate("Array.from(new Set([1, 2, 3])).join(',')").AsString().Should().Be("1,2,3");
        engine.Evaluate("var [a, b] = new Int8Array([4, 5, 6]); a + ',' + b").AsString().Should().Be("4,5");
    }
}
