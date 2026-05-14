namespace Jint.Tests.Runtime;

public class GeneratorTests
{
    private readonly Engine _engine;

    public GeneratorTests()
    {
        _engine = new Engine();
    }

    [Fact(Timeout = 10000)]
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

        Assert.Equal("01234", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("abc", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("a", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("undefined", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("", _engine.Evaluate(Script));
    }

    [Fact]
    public void Basic()
    {
        _engine.Execute("function * generator() { yield 5; yield 6; };");
        _engine.Execute("var iterator = generator(); var item = iterator.next();");
        Assert.Equal(5, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.Equal(6, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.True(_engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(_engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
    public void FunctionExpressions()
    {
        _engine.Execute("var generator = function * () { yield 5; yield 6; };");
        _engine.Execute("var iterator = generator(); var item = iterator.next();");
        Assert.Equal(5, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.Equal(6, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.True(_engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(_engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
    public void CorrectThisBinding()
    {
        _engine.Execute("var generator = function * () { yield 5; yield 6; };");
        _engine.Execute("var iterator = { g: generator, x: 5, y: 6 }.g(); var item = iterator.next();");
        Assert.Equal(5, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.Equal(6, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.True(_engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(_engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
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

        Assert.Equal("foo", _engine.Evaluate("sent[0]"));
        Assert.Equal("bar", _engine.Evaluate("sent[1]"));
    }

    [Fact]
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

        Assert.Equal(0, _engine.Evaluate("generatorFunc.next().value")); // 0
        Assert.Equal(1, _engine.Evaluate("generatorFunc.next().value")); // 1
        Assert.Equal(2, _engine.Evaluate("generatorFunc.next().value")); // 2
        Assert.Equal(3, _engine.Evaluate("generatorFunc.next().value")); // 3
        Assert.Equal(14, _engine.Evaluate("generatorFunc.next(10).value")); // 14
        Assert.Equal(15, _engine.Evaluate("generatorFunc.next().value")); // 15
        Assert.Equal(26, _engine.Evaluate("generatorFunc.next(10).value")); // 26
    }

    [Fact]
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

        Assert.Equal(0, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(2, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(3, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(5, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(8, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(0, _engine.Evaluate("sequence.next(true).value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(2, _engine.Evaluate("sequence.next().value"));
    }

    // The following tests mirror PR #2469's AsyncTests for control-flow resume but
    // using yield in a sync generator. The PR's fix is shared infrastructure
    // (ISuspendable.Data, GetSuspensionNode, IsNodeInsideRange), so these are
    // regression guards against drift between the async and sync resume paths.

    [Fact]
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

        Assert.Equal(1, _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal(1, _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("[1,1,0,1]", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal(1, _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("[1,6]", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("[1,7]", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""[[1,2,"done"],3]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""[{"0":5},0]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal(1, _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("[0,1]", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""[[1,2,"done"],3]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""["a","b","c","d"]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""["1-X-2",2]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""[{"a":1,"b":2,"c":"done"},3]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("[1,1]", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal("""[1,"done"]""", _engine.Evaluate(Script));
    }

    [Fact]
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

        Assert.Equal(1, result.AsNumber());
    }
}
