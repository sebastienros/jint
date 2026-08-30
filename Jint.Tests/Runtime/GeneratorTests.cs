namespace Jint.Tests.Runtime;

public class GeneratorTests
{
    private readonly Engine _engine;

    public GeneratorTests()
    {
        _engine = new Engine();
    }

    [Test, CancelAfter(10000)]
    public void YieldInForLoopUpdateExpression()
    {
        const string Script = """
            const foo = function*() {
                for(var i = 0; i < 5; yield i++) {}
            };

            let str = '';
            for (const val of foo()) {
                str += val;
            }
            return str;
        """;

        // A regression here spins forever, and [CancelAfter] cannot abort a synchronous test method on
        // its own. Handing the engine the test's cancellation token is what makes the timeout bite.
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));

        engine.Evaluate(Script).Should().Be("01234");
    }

    [Test]
    public void LoopYield()
    {
        const string Script = """
          const foo = function*() {
            yield 'a';
            yield 'b';
            yield 'c';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        _engine.Evaluate(Script).Should().Be("abc");
    }

    [Test]
    public void ReturnDuringYield()
    {
        const string Script = """
          const foo = function*() {
            yield 'a';
            return;
            yield 'c';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        _engine.Evaluate(Script).Should().Be("a");
    }

    [Test]
    public void LoneReturnInYield()
    {
        const string Script = """
          const foo = function*() {
            return;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        _engine.Evaluate(Script).Should().Be("");
    }

    [Test]
    public void LoneReturnValueInYield()
    {
        const string Script = """
          const foo = function*() {
            return 'a';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        _engine.Evaluate(Script).Should().Be("");
    }

    [Test]
    public void YieldUndefined()
    {
        const string Script = """
          const foo = function*() {
            yield undefined;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        _engine.Evaluate(Script).Should().Be("undefined");
    }

    [Test]
    public void ReturnUndefined()
    {
        const string Script = """
          const foo = function*() {
            return undefined;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        _engine.Evaluate(Script).Should().Be("");
    }

    [Test]
    public void Basic()
    {
        _engine.Execute("function * generator() { yield 5; yield 6; };");
        _engine.Execute("var iterator = generator(); var item = iterator.next();");
        _engine.Evaluate("item.value").Should().Be(5);
        _engine.Evaluate("item.done").AsBoolean().Should().BeFalse();
        _engine.Execute("item = iterator.next();");
        _engine.Evaluate("item.value").Should().Be(6);
        _engine.Evaluate("item.done").AsBoolean().Should().BeFalse();
        _engine.Execute("item = iterator.next();");
        _engine.Evaluate("item.value === void undefined").AsBoolean().Should().BeTrue();
        _engine.Evaluate("item.done").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void FunctionExpressions()
    {
        _engine.Execute("var generator = function * () { yield 5; yield 6; };");
        _engine.Execute("var iterator = generator(); var item = iterator.next();");
        _engine.Evaluate("item.value").Should().Be(5);
        _engine.Evaluate("item.done").AsBoolean().Should().BeFalse();
        _engine.Execute("item = iterator.next();");
        _engine.Evaluate("item.value").Should().Be(6);
        _engine.Evaluate("item.done").AsBoolean().Should().BeFalse();
        _engine.Execute("item = iterator.next();");
        _engine.Evaluate("item.value === void undefined").AsBoolean().Should().BeTrue();
        _engine.Evaluate("item.done").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void CorrectThisBinding()
    {
        _engine.Execute("var generator = function * () { yield 5; yield 6; };");
        _engine.Execute("var iterator = { g: generator, x: 5, y: 6 }.g(); var item = iterator.next();");
        _engine.Evaluate("item.value").Should().Be(5);
        _engine.Evaluate("item.done").AsBoolean().Should().BeFalse();
        _engine.Execute("item = iterator.next();");
        _engine.Evaluate("item.value").Should().Be(6);
        _engine.Evaluate("item.done").AsBoolean().Should().BeFalse();
        _engine.Execute("item = iterator.next();");
        _engine.Evaluate("item.value === void undefined").AsBoolean().Should().BeTrue();
        _engine.Evaluate("item.done").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void Sending()
    {
        const string Script = """
          var sent;
          function * generator() {
            sent = [yield 5, yield 6];
          };
          var iterator = generator();
          iterator.next();
          iterator.next("foo");
          iterator.next("bar");
        """;

        _engine.Execute(Script);

        _engine.Evaluate("sent[0]").Should().Be("foo");
        _engine.Evaluate("sent[1]").Should().Be("bar");
    }

    [Test]
    public void Sending2()
    {
        const string Script = """
        function* counter(value) {
          while (true) {
            const step = yield value++;
        
            if (step) {
              value += step;
            }
          }
        }
        
        const generatorFunc = counter(0);
        """;

        _engine.Execute(Script);

        _engine.Evaluate("generatorFunc.next().value").Should().Be(0); // 0
        _engine.Evaluate("generatorFunc.next().value").Should().Be(1); // 1
        _engine.Evaluate("generatorFunc.next().value").Should().Be(2); // 2
        _engine.Evaluate("generatorFunc.next().value").Should().Be(3); // 3
        _engine.Evaluate("generatorFunc.next(10).value").Should().Be(14); // 14
        _engine.Evaluate("generatorFunc.next().value").Should().Be(15); // 15
        _engine.Evaluate("generatorFunc.next(10).value").Should().Be(26); // 26
    }

    [Test]
    public void Fibonacci()
    {
        const string Script = """
            function* fibonacci() {
              let current = 0;
              let next = 1;
              while (true) {
                const reset = yield current;
                [current, next] = [next, next + current];
                if (reset) {
                  current = 0;
                  next = 1;
                }
              }
            }
            
            const sequence = fibonacci();
        """;

        _engine.Execute(Script);

        _engine.Evaluate("sequence.next().value").Should().Be(0);
        _engine.Evaluate("sequence.next().value").Should().Be(1);
        _engine.Evaluate("sequence.next().value").Should().Be(1);
        _engine.Evaluate("sequence.next().value").Should().Be(2);
        _engine.Evaluate("sequence.next().value").Should().Be(3);
        _engine.Evaluate("sequence.next().value").Should().Be(5);
        _engine.Evaluate("sequence.next().value").Should().Be(8);
        _engine.Evaluate("sequence.next(true).value").Should().Be(0);
        _engine.Evaluate("sequence.next().value").Should().Be(1);
        _engine.Evaluate("sequence.next().value").Should().Be(1);
        _engine.Evaluate("sequence.next().value").Should().Be(2);
    }

    // The following tests mirror PR #2469's AsyncTests for control-flow resume but
    // using yield in a sync generator. The PR's fix is shared infrastructure
    // (ISuspendable.Data, GetSuspensionNode, IsNodeInsideRange), so these are
    // regression guards against drift between the async and sync resume paths.

    [Test]
    public void ShouldResumeYieldInsideCatchWithoutReexecutingTryBlock()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let tries = 0;
                    try {
                        tries++;
                        throw 1;
                    } catch (e) {
                        yield;
                        return tries;
                    }
                }
                const g = gen();
                g.next();
                return g.next().value;
            })()
            """;

        _engine.Evaluate(Script).Should().Be(1);
    }

    [Test]
    public void ShouldResumeYieldInsideIfWithoutReexecutingTest()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let tests = 0;
                    if (++tests === 1) {
                        yield;
                        return tests;
                    }
                    return -1;
                }
                const g = gen();
                g.next();
                return g.next().value;
            })()
            """;

        _engine.Evaluate(Script).Should().Be(1);
    }

    [Test]
    public void ShouldResumeYieldInsideForBodyWithoutReexecutingTest()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let inits = 0, tests = 0, updates = 0, bodies = 0;
                    for (inits++; ++tests <= 1; updates++) {
                        bodies++;
                        yield;
                        return [inits, tests, updates, bodies];
                    }
                    return [inits, tests, updates, bodies, "fellThrough"];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next().value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("[1,1,0,1]");
    }

    [Test]
    public void ShouldResumeYieldInsideSwitchCaseWithoutReexecutingDiscriminant()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let discriminants = 0;
                    switch (++discriminants) {
                        case 1:
                            yield;
                            return discriminants;
                        default:
                            return -1;
                    }
                }
                const g = gen();
                g.next();
                return g.next().value;
            })()
            """;

        _engine.Evaluate(Script).Should().Be(1);
    }

    [Test]
    public void ShouldNotReevaluateBinaryLeftOperandAfterYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let d = 0;
                    const sum = (++d) + (yield 10);
                    return [d, sum];
                }
                const g = gen();
                g.next();              // yields 10
                return JSON.stringify(g.next(5).value);  // resume with 5 → sum = 1 + 5 = 6
            })()
            """;

        _engine.Evaluate(Script).Should().Be("[1,6]");
    }

    [Test]
    public void ShouldNotReevaluateLogicalAndLeftOperandAfterYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let d = 0;
                    const ok = (++d > 0) && (yield true);
                    return [d, ok];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next(7).value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("[1,7]");
    }

    [Test]
    public void ShouldNotReevaluateCallArgumentsBeforeYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let i = 0;
                    const foo = (a, b, c) => [a, b, c];
                    const r = foo(++i, ++i, yield ++i);
                    return [r, i];
                }
                const g = gen();
                g.next();                          // yields 3
                return JSON.stringify(g.next("done").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""[[1,2,"done"],3]""");
    }

    [Test]
    public void ShouldNotReevaluateCompoundAssignmentLhsAfterYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    const obj = { 0: 0 };
                    let i = -1;
                    obj[++i] += yield;
                    return [obj, i];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next(5).value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""[{"0":5},0]""");
    }

    [Test]
    public void ShouldPreserveSwitchLexicalBindingAfterYieldInsideCase()
    {
        const string Script = """
            (function () {
                function* gen() {
                    switch (1) {
                        case 1:
                            let x = 1;
                            yield;
                            return x;
                        default:
                            return 0;
                    }
                }
                const g = gen();
                g.next();
                return g.next().value;
            })()
            """;

        _engine.Evaluate(Script).Should().Be(1);
    }

    [Test]
    public void ShouldClearSwitchSuspendDataAfterResumedBreakInGenerator()
    {
        const string Script = """
            (function () {
                function* gen() {
                    const values = [];
                    for (let i = 0; i < 2; i++) {
                        switch (1) {
                            case 1:
                                let x = i;
                                yield;
                                values.push(x);
                                break;
                        }
                    }
                    return values;
                }
                const g = gen();
                g.next();         // yields, x=0 in iter 0
                g.next();         // resumes, pushes 0; yields, x=1 in iter 1
                return JSON.stringify(g.next().value);  // resumes, pushes 1; loop exits
            })()
            """;

        _engine.Evaluate(Script).Should().Be("[0,1]");
    }

    [Test]
    public void ShouldNotReevaluateArrayLiteralElementsBeforeYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let i = 0;
                    const a = [++i, ++i, yield ++i];
                    return [a, i];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next("done").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""[[1,2,"done"],3]""");
    }

    [Test]
    public void ShouldNotReiterateOneShotSpreadIteratorAcrossYield()
    {
        const string Script = """
            (function () {
                function* inner() { yield "a"; yield "b"; yield "c"; }
                function* outer() {
                    const g = inner();
                    const r = [...g, yield "wait"];
                    return r;
                }
                const o = outer();
                o.next();
                return JSON.stringify(o.next("d").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""["a","b","c","d"]""");
    }

    [Test]
    public void ShouldNotReevaluateTemplateLiteralInterpolationsBeforeYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let i = 0;
                    const s = `${++i}-${yield "wait"}-${++i}`;
                    return [s, i];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next("X").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""["1-X-2",2]""");
    }

    [Test]
    public void ShouldNotReevaluateObjectLiteralPropertiesBeforeYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let i = 0;
                    const o = { a: ++i, b: ++i, c: yield ++i };
                    return [o, i];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next("done").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""[{"a":1,"b":2,"c":"done"},3]""");
    }

    [Test]
    public void ShouldNotReevaluateMemberObjectAcrossPropertyYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let calls = 0;
                    const obj = { val: 1 };
                    const get = () => (calls++, obj);
                    const v = get()[yield "wait"];
                    return [v, calls];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next("val").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("[1,1]");
    }

    [Test]
    public void ShouldNotReevaluateNullishCoalescingLeftOperandAfterYield()
    {
        const string Script = """
            (function () {
                function* gen() {
                    let d = 0;
                    const getNullish = () => (++d, null);
                    const v = getNullish() ?? (yield "wait");
                    return [d, v];
                }
                const g = gen();
                g.next();
                return JSON.stringify(g.next("done").value);
            })()
            """;

        _engine.Evaluate(Script).Should().Be("""[1,"done"]""");
    }

    [Test]
    public void ShouldResumeYieldInsideCatchInAsyncGenerator()
    {
        // Async generators share ISuspendable.Data with sync generators / async
        // functions, so the same control-flow resume fixes apply.
        var result = _engine.Evaluate("""
            (async () => {
                async function* gen() {
                    let tries = 0;
                    try {
                        tries++;
                        throw 1;
                    } catch (e) {
                        yield;
                        return tries;
                    }
                }
                const g = gen();
                await g.next();
                return (await g.next()).value;
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(1));

        result.AsNumber().Should().Be(1);
    }

    [Test]
    public void InterleavedGeneratorsFromSameDeclarationKeepIndependentPositions()
    {
        // Each instance must track its own resume position: a shared statement list
        // let one generator's completion reset another's, silently restarting it.
        const string Script = """
            function* gen() {
                var log = [];
                log.push('s');
                yield 1;
                log.push('m');
                yield 2;
                return log.join('');
            }
            var g1 = gen(), g2 = gen();
            var out = [];
            var steps = [g1, g2, g1, g2, g1, g2];
            for (var i = 0; i < steps.length; i++) {
                var r = steps[i].next();
                out.push(r.value === undefined ? '-' : r.value, r.done);
            }
            return JSON.stringify(out);
            """;

        _engine.Evaluate(Script).AsString().Should().Be("""[1,false,1,false,2,false,2,false,"sm",true,"sm",true]""");
    }

    [Test]
    public void InterleavedGeneratorsKeepIndependentPositionsInsideNestedBlocks()
    {
        // Yields inside nested blocks exercise the nested statement lists' saved
        // positions, which must also be per generator instance.
        const string Script = """
            function* gen(flag) {
                var log = [];
                if (flag) {
                    log.push('a');
                    yield 1;
                    log.push('b');
                    yield 2;
                }
                return log.join('');
            }
            var g1 = gen(true), g2 = gen(true);
            var out = [];
            var steps = [g1, g2, g1, g2, g1, g2];
            for (var i = 0; i < steps.length; i++) {
                var r = steps[i].next();
                out.push(r.value === undefined ? '-' : r.value, r.done);
            }
            return JSON.stringify(out);
            """;

        _engine.Evaluate(Script).AsString().Should().Be("""[1,false,1,false,2,false,2,false,"ab",true,"ab",true]""");
    }

    [Test]
    public void InterleavedForOfOverGeneratorsFromSameDeclarationTerminates()
    {
        // With a shared statement list a second live instance restarted from the top
        // after the first completed, making the iteration produce duplicate values.
        const string Script = """
            function* gen() {
                yield 1;
                yield 2;
            }
            var g1 = gen(), g2 = gen();
            var out = [];
            for (const v of g1) {
                out.push(v);
                out.push(g2.next().value);
            }
            out.push(g2.next().done);
            return JSON.stringify(out);
            """;

        _engine.Evaluate(Script).AsString().Should().Be("[1,1,2,2,true]");
    }

    [Test]
    public void InterleavedAsyncGeneratorsFromSameDeclarationKeepIndependentPositions()
    {
        var result = _engine.Evaluate("""
            (async () => {
                async function* gen() {
                    var log = [];
                    log.push('s');
                    yield 1;
                    log.push('m');
                    yield 2;
                    return log.join('');
                }
                var g1 = gen(), g2 = gen();
                var out = [];
                var steps = [g1, g2, g1, g2, g1, g2];
                for (var i = 0; i < steps.length; i++) {
                    var r = await steps[i].next();
                    out.push(r.value === undefined ? '-' : r.value, r.done);
                }
                return JSON.stringify(out);
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsString().Should().Be("""[1,false,1,false,2,false,2,false,"sm",true,"sm",true]""");
    }

    [Test]
    public void ShouldNotDivertSuspendedAdditionChainResumeWhenSiblingInvocationLatchesNumericKind()
    {
        // A 3+-operand '+' chain is flattened onto one handler node shared by every live invocation
        // of the declaration. One invocation completing numerically must not send another
        // invocation's resume down the nested tree: that lane re-evaluates the left operand
        // (running inc() a second time) and reads the chain's suspend data under an incompatible
        // type.
        const string Script = """
            (function () {
                var n = 0;
                function inc() { n++; return n; }
                function* gen(flag) { return inc() + (flag ? (yield 0) : 0) + 100; }

                var a = gen(true);
                a.next();              // inc() -> n = 1, suspends at the yield
                var b = gen(false);
                b.next();              // completes numerically, latching the chain's kind

                return JSON.stringify([a.next(5).value, n]);
            })()
            """;

        _engine.Evaluate(Script).AsString().Should().Be("[106,2]");
    }

    [Test]
    public void ShouldResumeAdditionChainWithTwoSuspensionsAfterSiblingInvocationLatchesNumericKind()
    {
        // Same interleave with a second suspension point in the chain: the diverted resume asked
        // for its own suspend data type under the key the flattened lane already owns, which threw
        // an InvalidCastException straight out of the engine.
        const string Script = """
            (function () {
                var n = 0;
                function inc() { n++; return n; }
                function* gen(flag) { return inc() + (flag ? (yield 0) : 0) + (flag ? (yield 1) : 0); }

                var a = gen(true);
                a.next();              // inc() -> n = 1, suspends at the first yield
                var b = gen(false);
                b.next();              // completes numerically, latching the chain's kind

                a.next(5);             // resumes the first yield, suspends at the second
                return JSON.stringify([a.next(6).value, n]);
            })()
            """;

        _engine.Evaluate(Script).AsString().Should().Be("[12,2]");
    }

    [Test]
    public void ShouldNotDivertSuspendedAdditionChainResumeInAsyncFunction()
    {
        // The async twin of the generator case: two in-flight calls of one async function, the
        // second completing (without awaiting) while the first is suspended at its await.
        var result = _engine.Evaluate("""
            (async () => {
                var n = 0;
                function inc() { n++; return n; }
                var resolveFirst;
                var pending = new Promise(function (r) { resolveFirst = r; });
                async function f(flag, p) { return inc() + (flag ? await p : 0) + 100; }

                var a = f(true, pending);   // inc() -> n = 1, suspends at the await
                await f(false, null);       // completes numerically, latching the chain's kind

                resolveFirst(5);
                return JSON.stringify([await a, n]);
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsString().Should().Be("[106,2]");
    }

    [Test]
    public void ShouldRaiseAdditionChainSymbolCoercionErrorBeforeLaterOperandSideEffects()
    {
        // ApplyStringOrNumericBinaryOperator folds left-associatively, so ToString of the Symbol in
        // ("x" + Symbol.iterator) throws before the third operand is evaluated. Inside a generator
        // the chain takes the buffered lane, which must order the fold the same way.
        const string Script = """
            (function () {
                function* gen() {
                    var log = [];
                    try { ("x" + Symbol.iterator + (log.push(1), "y")); } catch (e) { }
                    yield log.length;
                }
                var insideGenerator = gen().next().value;

                var log = [];
                try { ("x" + Symbol.iterator + (log.push(1), "y")); } catch (e) { }
                return JSON.stringify([insideGenerator, log.length]);
            })()
            """;

        _engine.Evaluate(Script).AsString().Should().Be("[0,0]");
    }

    [Test]
    public void ShouldRaiseAdditionChainBigIntMixErrorBeforeLaterOperandSideEffects()
    {
        // Same ordering requirement for the numeric arm: mixing BigInt and Number throws while
        // folding the opening pair, before the third operand runs.
        const string Script = """
            (function () {
                function* gen() {
                    var log = [];
                    try { (1n + 1 + (log.push(1), 2)); } catch (e) { }
                    yield log.length;
                }
                var insideGenerator = gen().next().value;

                var log = [];
                try { (1n + 1 + (log.push(1), 2)); } catch (e) { }
                return JSON.stringify([insideGenerator, log.length]);
            })()
            """;

        _engine.Evaluate(Script).AsString().Should().Be("[0,0]");
    }

    [Test, CancelAfter(10000)]
    public void ADelegatingYieldInsideAYieldStartsOverOnEveryLoopIteration()
    {
        // https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation:
        // every evaluation of `yield * AssignmentExpression` evaluates its operand and drives the
        // resulting iterator to completion, so a loop that comes back round to the same yield* node
        // starts a fresh delegation. Jint replays a generator body from the top on each resume and
        // memoized what each yield node had already returned; the memo was never invalidated, so the
        // second iteration answered the outer yield from the first iteration's value without ever
        // evaluating the operand -- which abandoned the delegation and, because the operand carries
        // the loop's own decrement here, left n unchanged and the loop running forever.
        // staging/sm/generators/delegating-yield-9.js is this shape; SpiderMonkey and V8 both
        // report eight results for countdown(3).
        const string Script = """
            function* countdown(n) {
                while (n > 0) {
                    yield (yield* countdown(--n));
                }
                return 34;
            }

            var results = [];
            var it = countdown(3);
            var result;
            do {
                result = it.next();
                results.push(result.value + ':' + result.done);
            } while (!result.done && results.length < 100);
            return results.join(' ');
        """;

        // A regression here spins forever, and [CancelAfter] cannot abort a synchronous test method
        // on its own; the engine has to observe the test's token for the timeout to bite.
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));

        engine.Evaluate(Script).Should().Be("34:false 34:false 34:false 34:false 34:false 34:false 34:false 34:true");
    }

    [Test, CancelAfter(10000)]
    public void ADelegatingYieldInsideAYieldKeepsItsPlaceWhenTheDecrementIsElsewhere()
    {
        // The same defect without the runaway loop: with the decrement in its own statement the loop
        // still terminates, but the outer yield answered from the memo instead of yielding, so two of
        // the eight results went missing. Kept separate because a fix that only stopped the hang
        // would leave this one silently wrong.
        const string Script = """
            function* countdown(n) {
                while (n > 0) {
                    n = n - 1;
                    yield (yield* countdown(n));
                }
                return 34;
            }

            var results = [];
            var it = countdown(3);
            var result;
            do {
                result = it.next();
                results.push(result.value + ':' + result.done);
            } while (!result.done && results.length < 100);
            return results.join(' ');
        """;

        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));

        engine.Evaluate(Script).Should().Be("34:false 34:false 34:false 34:false 34:false 34:false 34:false 34:true");
    }

    [Test]
    public void GeneratorFunctionConstructorsInheritFromTheFunctionConstructor()
    {
        // https://tc39.es/ecma262/#sec-generatorfunction-constructor and
        // https://tc39.es/ecma262/#sec-asyncgeneratorfunction-constructor: each of these constructors
        // "has a [[Prototype]] internal slot whose value is %Function%" -- the Function constructor
        // itself, not its own .prototype object.
        _engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(function* () {}).constructor) === Function")
            .AsBoolean().Should().BeTrue();
        _engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(async function* () {}).constructor) === Function")
            .AsBoolean().Should().BeTrue();

        // AsyncFunction already had this right; pinned here so the three stay together.
        _engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(async function () {}).constructor) === Function")
            .AsBoolean().Should().BeTrue();

        // The prototype objects are unaffected: %GeneratorFunction.prototype%.[[Prototype]] stays
        // %Function.prototype% (https://tc39.es/ecma262/#sec-properties-of-the-generatorfunction-prototype-object).
        _engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(function* () {})) === Function.prototype")
            .AsBoolean().Should().BeTrue();
        _engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(async function* () {})) === Function.prototype")
            .AsBoolean().Should().BeTrue();

        // And a generator is still an instance of both.
        _engine.Evaluate("function* g() {} g instanceof Object.getPrototypeOf(g).constructor && g instanceof Function")
            .AsBoolean().Should().BeTrue();
    }
}
