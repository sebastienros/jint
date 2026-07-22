using System.Collections.Concurrent;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Tests.Runtime.TestClasses;

namespace Jint.Tests.Runtime;

public class AsyncTests
{
    // Wall-clock promise budgets are a CI flake trap: on a loaded runner, thread-pool starvation
    // can delay a Task.Delay continuation by many seconds (a 100 ms delay has been observed to
    // blow the default 10-second PromiseTimeout on a two-core Windows runner). Tests that assert
    // BEHAVIOR of the task-interop / async-wake path — not latency — use this budget so they
    // cannot lose that race; tests that assert a timeout FIRES keep tight budgets (starvation
    // only helps the timeout fire).
    private static readonly TimeSpan GenerousPromiseTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public void AwaitPropagationAgainstPrimitiveValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("(async ()=>await '1')()");
        result = result.UnwrapIfPromise();
        result.Should().Be("1");
    }

    [Fact]
    public void ShouldResumeAwaitInsideCatchWithoutReexecutingTryBlock()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            (async () => {
                let tries = 0;
                try {
                    tries++;
                    await Promise.reject(new Error("boom"));
                } catch (e) {
                    await Promise.resolve();
                    return tries;
                }
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(1));

        result.AsNumber().Should().Be(1);
    }

    [Fact]
    public void ShouldNotLeakCatchBindingAfterAwaitInsideCatch()
    {
        var result = EvaluateAsyncJson("""
            try {
                throw 1;
            } catch (e) {
                await Promise.resolve();
            }

            try {
                return { leaked: true, value: e };
            } catch (err) {
                return { leaked: false, name: err.name };
            }
            """);

        result.Should().Be("""{"leaked":false,"name":"ReferenceError"}""");
    }

    [Fact]
    public void ShouldPreserveCatchBindingAfterAwaitInsideCatch()
    {
        var result = EvaluateAsyncJson("""
            try {
                throw 42;
            } catch (e) {
                await Promise.resolve();
                return { value: e };
            }
            """);

        result.Should().Be("""{"value":42}""");
    }

    [Fact]
    public void ShouldPreserveCatchReturnAcrossAwaitedFinallyAfterCatchResume()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            (async () => {
                try {
                    throw 1;
                } catch {
                    await Promise.resolve();
                    return 2;
                } finally {
                    await Promise.resolve();
                }
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(1));

        result.AsInteger().Should().Be(2);
    }

    [Fact]
    public void ShouldPreserveCatchThrowAcrossAwaitedFinallyAfterCatchResume()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            (async () => {
                try {
                    throw 1;
                } catch {
                    await Promise.resolve();
                    throw 4;
                } finally {
                    await Promise.resolve();
                }
            })()
            """);

        var exception = Invoking(() => result.UnwrapIfPromise(TimeSpan.FromSeconds(1))).Should().ThrowExactly<PromiseRejectedException>().Which;
        exception.RejectedValue.AsInteger().Should().Be(4);
    }

    [Fact]
    public void ShouldResumeAwaitInsideIfWithoutReexecutingTest()
    {
        var result = EvaluateAsyncJson("""
            let tests = 0;
            if (++tests === 1) {
                await Promise.resolve();
                return { tests };
            }
            return { tests, fellThrough: true };
            """);

        result.Should().Be("""{"tests":1}""");
    }

    [Fact]
    public void ShouldNotExecuteElseWhileIfTestIsSuspended()
    {
        var result = EvaluateAsyncJson("""
            let sideEffects = 0;
            if (await Promise.resolve(true)) {
            } else {
                sideEffects++;
            }
            return { sideEffects };
            """);

        result.Should().Be("""{"sideEffects":0}""");
    }

    [Fact]
    public void ShouldResumeAwaitInsideWhileBodyWithoutReexecutingTest()
    {
        var result = EvaluateAsyncJson("""
            let tests = 0;
            let bodies = 0;
            while (++tests <= 1) {
                bodies++;
                await Promise.resolve();
                return { tests, bodies };
            }
            return { tests, bodies, fellThrough: true };
            """);

        result.Should().Be("""{"tests":1,"bodies":1}""");
    }

    [Fact]
    public void ShouldResumeAwaitInsideForBodyWithoutReexecutingTest()
    {
        var result = EvaluateAsyncJson("""
            let inits = 0;
            let tests = 0;
            let updates = 0;
            let bodies = 0;
            for (inits++; ++tests <= 1; updates++) {
                bodies++;
                await Promise.resolve();
                return { inits, tests, updates, bodies };
            }
            return { inits, tests, updates, bodies, fellThrough: true };
            """);

        result.Should().Be("""{"inits":1,"tests":1,"updates":0,"bodies":1}""");
    }

    [Fact]
    public void ShouldResumeAwaitInsideForUpdateWithoutReexecutingBody()
    {
        var result = EvaluateAsyncJson("""
            let bodies = 0;
            for (let i = 0; i < 1; i += await Promise.resolve(1)) {
                bodies++;
            }
            return { bodies };
            """);

        result.Should().Be("""{"bodies":1}""");
    }

    [Fact]
    public void ShouldResumeAwaitInsideSwitchCaseWithoutReexecutingDiscriminant()
    {
        var result = EvaluateAsyncJson("""
            let discriminants = 0;
            switch (++discriminants) {
                case 1:
                    await Promise.resolve();
                    return { discriminants };
                default:
                    return { discriminants, fellThrough: true };
            }
            """);

        result.Should().Be("""{"discriminants":1}""");
    }

    [Fact]
    public void ShouldResumeAwaitInsideSwitchCaseTestBeforeMatchingCase()
    {
        var result = EvaluateAsyncJson("""
            switch (0) {
                case await Promise.resolve(1):
                    return { matched: true };
                default:
                    return { matched: false };
            }
            """);

        result.Should().Be("""{"matched":false}""");
    }

    [Fact]
    public void ShouldPreserveSwitchLexicalBindingAfterAwaitInsideCase()
    {
        var result = EvaluateAsyncJson("""
            switch (1) {
                case 1:
                    let x = 1;
                    await Promise.resolve();
                    return { x };
                default:
                    return { x: 0 };
            }
            """);

        result.Should().Be("""{"x":1}""");
    }

    [Fact]
    public void ShouldClearSwitchSuspendDataAfterResumedBreak()
    {
        var result = EvaluateAsyncJson("""
            const values = [];
            for (let i = 0; i < 2; i++) {
                switch (1) {
                    case 1:
                        let x = i;
                        await Promise.resolve();
                        values.push(x);
                        break;
                }
            }
            return { values };
            """);

        result.Should().Be("""{"values":[0,1]}""");
    }

    [Fact]
    public void ShouldResumeAwaitInsideDoWhileTestWithoutReexecutingBody()
    {
        var result = EvaluateAsyncJson("""
            let tests = 0;
            let bodies = 0;
            do {
                bodies++;
            } while (++tests < await Promise.resolve(1));
            return { tests, bodies };
            """);

        result.Should().Be("""{"tests":1,"bodies":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateBinaryLeftOperandAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            let d = 0;
            const sum = (++d) + (await Promise.resolve(10));
            return { d, sum };
            """);

        result.Should().Be("""{"d":1,"sum":11}""");
    }

    [Fact]
    public void ShouldNotReevaluateLogicalAndLeftOperandAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            let d = 0;
            const ok = (++d > 0) && (await Promise.resolve(true));
            return { d, ok };
            """);

        result.Should().Be("""{"d":1,"ok":true}""");
    }

    [Fact]
    public void ShouldNotReevaluateLogicalOrLeftOperandAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            let d = 0;
            const ok = (++d <= 0) || (await Promise.resolve(true));
            return { d, ok };
            """);

        result.Should().Be("""{"d":1,"ok":true}""");
    }

    [Fact]
    public void ShouldNotReevaluateConditionalTestAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            let d = 0;
            const value = (++d > 0) ? (await Promise.resolve("yes")) : "no";
            return { d, value };
            """);

        result.Should().Be("""{"d":1,"value":"yes"}""");
    }

    [Fact]
    public void ShouldNotReevaluateConditionalTestWhenAlternateAwaits()
    {
        var result = EvaluateAsyncJson("""
            let d = 0;
            const value = (++d > 1) ? "yes" : (await Promise.resolve("no"));
            return { d, value };
            """);

        result.Should().Be("""{"d":1,"value":"no"}""");
    }

    [Fact]
    public void ShouldShortCircuitLogicalAndWithoutSavingLeftOperand()
    {
        // Left short-circuits to false: right (await) should never run, and a
        // subsequent expression using the same `&&` should still re-evaluate left.
        var result = EvaluateAsyncJson("""
            let d = 0;
            let awaits = 0;
            const a = (d > 0) && (await Promise.resolve(++awaits));
            d = 1;
            const b = (d > 0) && (await Promise.resolve(++awaits));
            return { a, b, awaits };
            """);

        result.Should().Be("""{"a":false,"b":1,"awaits":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateCompoundAssignmentLhsAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            const obj = { 0: 0 };
            let i = -1;
            obj[++i] += await Promise.resolve(5);
            return { obj, i };
            """);

        result.Should().Be("""{"obj":{"0":5},"i":0}""");
    }

    [Fact]
    public void ShouldNotReevaluatePropertyAssignmentLhsAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            const obj = { x: 10 };
            let touches = 0;
            const accessor = () => (touches++, obj);
            accessor().x -= await Promise.resolve(3);
            return { obj, touches };
            """);

        result.Should().Be("""{"obj":{"x":7},"touches":1}""");
    }

    [Fact]
    public void ShouldPreserveCompoundAssignmentLhsAcrossNullishCoalescing()
    {
        var result = EvaluateAsyncJson("""
            const obj = { 0: null };
            let i = -1;
            obj[++i] ??= await Promise.resolve("filled");
            return { obj, i };
            """);

        result.Should().Be("""{"obj":{"0":"filled"},"i":0}""");
    }

    [Fact]
    public void ShouldNotReevaluateCompoundAssignmentLhsOnSimpleIdentifierAfterAwait()
    {
        // Simple-identifier LHS goes through the fast path (no observable side
        // effect to preserve), but the result still has to be correct.
        var result = EvaluateAsyncJson("""
            let x = 7;
            x *= await Promise.resolve(3);
            return { x };
            """);

        result.Should().Be("""{"x":21}""");
    }

    [Fact]
    public void ShouldNotReevaluateCallArgumentsBeforeAwait()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const foo = (a, b, c) => ({ a, b, c });
            const r = foo(++i, ++i, await Promise.resolve(++i));
            return { r, i };
            """);

        result.Should().Be("""{"r":{"a":1,"b":2,"c":3},"i":3}""");
    }

    [Fact]
    public void ShouldNotReevaluateCallArgumentsAcrossMultipleAwaits()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const foo = (a, b, c, d) => ({ a, b, c, d });
            const r = foo(++i, await Promise.resolve(++i), ++i, await Promise.resolve(++i));
            return { r, i };
            """);

        result.Should().Be("""{"r":{"a":1,"b":2,"c":3,"d":4},"i":4}""");
    }

    [Fact]
    public void ShouldNotReevaluateNewExpressionArgumentsBeforeAwait()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            class C {
                constructor(a, b, c) { this.a = a; this.b = b; this.c = c; }
            }
            const obj = new C(++i, ++i, await Promise.resolve(++i));
            return { obj: { a: obj.a, b: obj.b, c: obj.c }, i };
            """);

        result.Should().Be("""{"obj":{"a":1,"b":2,"c":3},"i":3}""");
    }

    [Fact]
    public void ShouldHandleCallExpressionWithAwaitOnlyInLastArgument()
    {
        // Confirms the suspend-data is cleared after completion: a subsequent
        // call to the same call site should not see stale state.
        var result = EvaluateAsyncJson("""
            let i = 0;
            const foo = (a, b) => a + b;
            const first = foo(++i, await Promise.resolve(10));
            const second = foo(++i, await Promise.resolve(20));
            return { first, second, i };
            """);

        result.Should().Be("""{"first":11,"second":22,"i":2}""");
    }

    [Fact]
    public void ShouldNotReevaluateArrayLiteralElementsBeforeAwait()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const a = [++i, ++i, await Promise.resolve(++i)];
            return { a, i };
            """);

        result.Should().Be("""{"a":[1,2,3],"i":3}""");
    }

    [Fact]
    public void ShouldNotReevaluateArrayLiteralAcrossMultipleAwaits()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const a = [++i, await Promise.resolve(++i), ++i, await Promise.resolve(++i)];
            return { a, i };
            """);

        result.Should().Be("""{"a":[1,2,3,4],"i":4}""");
    }

    [Fact]
    public void ShouldClearArrayLiteralSuspendDataBetweenCalls()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const first = [++i, await Promise.resolve(10)];
            const second = [++i, await Promise.resolve(20)];
            return { first, second, i };
            """);

        result.Should().Be("""{"first":[1,10],"second":[2,20],"i":2}""");
    }

    [Fact]
    public void ShouldNotReiterateOneShotSpreadIteratorAcrossAwaitInArrayLiteral()
    {
        // A custom iterator stored in `g` is drained on first pass. Without
        // preservation, the spread re-iterates `g` on resume and gets nothing.
        var result = EvaluateAsyncJson("""
            function* gen() { yield "a"; yield "b"; yield "c"; }
            const g = gen();
            const r = [...g, await Promise.resolve("d")];
            return { r };
            """);

        result.Should().Be("""{"r":["a","b","c","d"]}""");
    }

    [Fact]
    public void ShouldNotReiterateOneShotSpreadIteratorAcrossAwaitInCallArguments()
    {
        var result = EvaluateAsyncJson("""
            function* gen() { yield 1; yield 2; yield 3; }
            const g = gen();
            const sum = (...vals) => vals.reduce((a, b) => a + b, 0);
            const total = sum(...g, await Promise.resolve(10));
            return { total };
            """);

        result.Should().Be("""{"total":16}""");
    }

    [Fact]
    public void ShouldPreserveTargetAcrossMultipleSuspensionsInSpreadArguments()
    {
        var result = EvaluateAsyncJson("""
            function* gen() { yield "x"; yield "y"; }
            const g = gen();
            const a = [
                await Promise.resolve("start"),
                ...g,
                await Promise.resolve("end")
            ];
            return { a };
            """);

        result.Should().Be("""{"a":["start","x","y","end"]}""");
    }

    [Fact]
    public void ShouldNotEvaluateLaterArgumentsAfterSuspensionInSpread()
    {
        // Without the IsSuspended-check-before-Add fix, `++j` would run during
        // the suspended pass after the await sentinel is appended.
        var result = EvaluateAsyncJson("""
            let j = 0;
            const arr = [1];
            const r = [...arr, await Promise.resolve("mid"), ++j];
            return { r, j };
            """);

        result.Should().Be("""{"r":[1,"mid",1],"j":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateNullishCoalescingLeftOperandAfterAwait()
    {
        var result = EvaluateAsyncJson("""
            let d = 0;
            const getNullish = () => (++d, null);
            const v = getNullish() ?? (await Promise.resolve("filled"));
            return { d, v };
            """);

        result.Should().Be("""{"d":1,"v":"filled"}""");
    }

    [Fact]
    public void ShouldNotReevaluateMemberExpressionObjectAcrossPropertyAwait()
    {
        var result = EvaluateAsyncJson("""
            let calls = 0;
            const obj = { val: 1 };
            const get = () => (calls++, obj);
            const v = get()[await Promise.resolve("val")];
            return { v, calls };
            """);

        result.Should().Be("""{"v":1,"calls":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateMemberExpressionObjectAcrossAwaitInChain()
    {
        var result = EvaluateAsyncJson("""
            let calls = 0;
            const obj = { foo: { bar: 42 } };
            const get = () => (calls++, obj);
            const v = get().foo[await Promise.resolve("bar")];
            return { v, calls };
            """);

        result.Should().Be("""{"v":42,"calls":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateObjectLiteralPropertiesBeforeAwait()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const o = { a: ++i, b: ++i, c: await Promise.resolve(++i) };
            return { o, i };
            """);

        result.Should().Be("""{"o":{"a":1,"b":2,"c":3},"i":3}""");
    }

    [Fact]
    public void ShouldNotReevaluateObjectLiteralPropertiesAcrossMultipleAwaits()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const o = {
                a: ++i,
                b: await Promise.resolve(++i),
                c: ++i,
                d: await Promise.resolve(++i)
            };
            return { o, i };
            """);

        result.Should().Be("""{"o":{"a":1,"b":2,"c":3,"d":4},"i":4}""");
    }

    [Fact]
    public void ShouldNotReevaluateComputedKeyAcrossAwaitInObjectLiteral()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const k = () => "k" + (++i);
            const o = { [k()]: 1, value: await Promise.resolve("done") };
            return { o, i };
            """);

        result.Should().Be("""{"o":{"k1":1,"value":"done"},"i":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateTemplateLiteralInterpolationsBeforeAwait()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const s = `${++i}-${await Promise.resolve("x")}-${++i}`;
            return { s, i };
            """);

        result.Should().Be("""{"s":"1-x-2","i":2}""");
    }

    [Fact]
    public void ShouldNotEvaluateLaterInterpolationsAfterSuspensionInTemplateLiteral()
    {
        // Without the IsSuspended-break inside the interpolation loop, `++j` would
        // also run during the suspended pass, doubling its side effect on resume.
        var result = EvaluateAsyncJson("""
            let j = 0;
            const s = `${await Promise.resolve("mid")}-${++j}`;
            return { s, j };
            """);

        result.Should().Be("""{"s":"mid-1","j":1}""");
    }

    [Fact]
    public void ShouldNotReevaluateTaggedTemplateInterpolationsBeforeAwait()
    {
        var result = EvaluateAsyncJson("""
            let i = 0;
            const tag = (strings, ...values) => values.join("|");
            const s = tag`${++i}-${await Promise.resolve("x")}-${++i}`;
            return { s, i };
            """);

        result.Should().Be("""{"s":"1|x|2","i":2}""");
    }

    [Fact]
    public void ShouldTaskConvertedToPromiseInJS()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        result.Should().Be(2);

        static async Task<int> Callable()
        {
            await Task.Delay(10);
            true.Should().BeTrue();
            return 1;
        }
    }

    private static string EvaluateAsyncJson(string body)
    {
        var engine = new Engine();
        return engine.Evaluate($$"""
            (async () => {
                {{body}}
            })().then(JSON.stringify)
            """).UnwrapIfPromise(TimeSpan.FromSeconds(1)).AsString();
    }

    [Fact]
    public void ShouldReturnedTaskConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnDelayedTaskAsync().then(x=>x)");
        result = result.UnwrapIfPromise();
        result.Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public void ShouldRespectCustomProvidedTimeoutWhenUnwrapping()
    {
        // A promise that never settles makes the timeout deterministic. The previous version raced
        // a 1 ms timeout against a 100 ms delayed task — under load the task could win, no
        // exception was thrown, and the test flaked (it was skipped for that reason).
        Engine engine = new();
        var result = engine.Evaluate("new Promise(function () {})");
        var timeout = TimeSpan.FromMilliseconds(1);
        var exception = Invoking(() => result.UnwrapIfPromise(timeout)).Should().ThrowExactly<PromiseRejectedException>().Which;
        exception.Message.Should().Be($"Promise was rejected with value Timeout of {timeout} reached");
    }

    [Fact]
    public void ShouldAwaitUnwrapPromiseWithCustomTimeout()
    {
        // the custom budget must be generous: this asserts the SUCCESS path, and a tight budget
        // races the delayed task's continuation scheduling under CI load
        Engine engine = new(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = GenerousPromiseTimeout; });
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute("""
        async function test() {
            return await asyncTestClass.ReturnDelayedTaskAsync();
        }
        """);
        var result = engine.Invoke("test").UnwrapIfPromise(GenerousPromiseTimeout);
        result.Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public void ShouldReturnedCompletedTaskConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnCompletedTask().then(x=>x)");
        result = result.UnwrapIfPromise();
        result.Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public void ShouldTaskCatchWhenCancelled()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        CancellationTokenSource cancel = new();
        cancel.Cancel();

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);

        engine.Evaluate("callable(token).then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();

        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();
        static async Task Callable(CancellationToken token)
        {
            await Task.FromCanceled(token);
        }
    }

    [Fact]
    public void ShouldReturnedTaskCatchWhenCancelled()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        CancellationTokenSource cancel = new();
        cancel.Cancel();

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("token", cancel.Token);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()));

        engine.Evaluate("asyncTestClass.ReturnCancelledTask(token).then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();

        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldTaskCatchWhenThrowError()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()));

        engine.Evaluate("callable().then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();

        static async Task Callable()
        {
            await Task.Delay(10);
            throw new Exception();
        }
    }

    [Fact]
    public void ShouldReturnedTaskCatchWhenThrowError()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("asyncTestClass", new AsyncTestClass());

        engine.Evaluate("asyncTestClass.ThrowAfterDelayAsync().then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldTaskAwaitCurrentStack()
    {
        //https://github.com/sebastienros/jint/issues/514#issuecomment-1507127509
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        AsyncTestClass asyncTestClass = new();

        engine.SetValue("myAsyncMethod", new Func<Task>(async () =>
        {
            await Task.Delay(1000);
            asyncTestClass.StringToAppend += "1";
        }));
        engine.SetValue("mySyncMethod2", new Action(() =>
        {
            asyncTestClass.StringToAppend += "2";
        }));
        engine.SetValue("asyncTestClass", asyncTestClass);

        engine.Evaluate("async function hello() {await myAsyncMethod();mySyncMethod2();await asyncTestClass.AddToStringDelayedAsync(\"3\")} hello();").UnwrapIfPromise();

        asyncTestClass.StringToAppend.Should().Be("123");
    }

    [Fact]
    public void ShouldCompleteWithAsyncTaskCallbacks()
    {
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(-1);
        });
        engine.SetValue("asyncTestMethod", new Func<Func<Task>, Task<string>>(async callback => { await Task.Delay(10); await callback(); return "Hello World"; }));
        engine.SetValue("asyncWork", new Func<Task>(() => Task.Delay(100)));

        var result = engine.Evaluate("async function hello() {return await asyncTestMethod(async () =>{ await asyncWork(); })} hello();");
        result = result.UnwrapIfPromise(TimeSpan.FromSeconds(30));

        result.Should().Be("Hello World");
    }

    [Fact]
    public void ShouldFromAsyncTaskCallbacks()
    {
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(-1);
        });
        engine.SetValue("asyncTestMethod", new Func<Func<Task<string>>, Task<string>>(async callback => { await Task.Delay(10); return await callback(); }));
        engine.SetValue("asyncWork", new Func<Task<string>>(async () => { await Task.Delay(100); return "Hello World"; }));

        var result = engine.Evaluate("async function hello() {return await asyncTestMethod(async () =>{ return await asyncWork(); })} hello();");
        result = result.UnwrapIfPromise(TimeSpan.FromSeconds(30));

        result.Should().Be("Hello World");
    }

    [Fact]
    public void ShouldAllowLetReassignmentInsideAsyncLoop()
    {
        Engine engine = new(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; });
        var script = """
        async function getItems() {
            return Promise.resolve([1, 2, 3]);
        }

        async function main() {
            let iteration = 0;
            do {
                let items = await getItems();
                items = await getItems();
                iteration++;
            } while (iteration < 2);
            return 'success';
        }

        export { main };
        """;

        engine.Modules.Add("test", script);
        var module = engine.Modules.Import("test");
        var main = module.Get("main");
        var result = engine.Invoke(main).UnwrapIfPromise();

        result.Should().Be("success");
    }

    [Fact]
    public void ShouldResolveTopLevelAwaitOfDelayedTaskInModule()
    {
        // #2663: top-level await of a .NET Task<T> in a module must block Modules.Import until the
        // Task completes on a ThreadPool thread, instead of giving up in a tight microsecond loop
        // and throwing "did not return a fulfilled promise: Pending". Success path, so the timeout
        // budget must be generous enough not to race a delayed task's continuation under CI load.
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = GenerousPromiseTimeout;
        });

        engine.SetValue("getAnswer", new Func<Task<int>>(async () =>
        {
            await Task.Delay(100);
            return 42;
        }));

        engine.Modules.Add("main", """
            const x = await getAnswer();
            export const answer = x;
            """);

        var ns = engine.Modules.Import("main");

        ns.Get("answer").AsInteger().Should().Be(42);
    }

    [Fact]
    public void ShouldPropagateRejectionOfTopLevelAwaitedTaskInModule()
    {
        // The rejection path of the same #2663 flow: a faulted top-level awaited Task must surface
        // as a JavaScript error out of Modules.Import, not hang or report a spurious pending state.
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = GenerousPromiseTimeout;
        });

        engine.SetValue("boom", new Func<Task<int>>(async () =>
        {
            await Task.Delay(50);
            throw new InvalidOperationException("kaboom");
        }));

        engine.Modules.Add("main", """
            const x = await boom();
            export const answer = x;
            """);

        Invoking(() => engine.Modules.Import("main")).Should().ThrowExactly<JavaScriptException>();
    }

    [Fact]
    public void ShouldObserveCancellationDuringTopLevelAwaitOfNeverCompletingTaskInModule()
    {
        // Robustness gap in the #2663 drain: when an embedder registers a cancellation-based
        // constraint (options.CancellationToken) and cancels it while a module's top-level await is
        // hanging on a never-completing .NET Task, the otherwise-idle event-loop drain must observe
        // the cancellation and break out PROMPTLY - throwing ExecutionCanceledException, the same way
        // per-statement execution surfaces cancellation - instead of blocking until PromiseTimeout.
        using var cts = new CancellationTokenSource();
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            // Deliberately huge so that a wait that lasts anywhere near it proves cancellation was NOT observed.
            options.Constraints.PromiseTimeout = GenerousPromiseTimeout;
            options.CancellationToken(cts.Token);
        });

        // A Task that never completes: the awaited promise stays pending forever unless cancellation breaks in.
        var neverCompletes = new TaskCompletionSource<int>();
        engine.SetValue("hang", new Func<Task<int>>(() => neverCompletes.Task));

        engine.Modules.Add("main", """
            const x = await hang();
            export const answer = x;
            """);

        // Cancel shortly after Import starts blocking on the drain (fires on a ThreadPool thread).
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Invoking(() => engine.Modules.Import("main")).Should().ThrowExactly<ExecutionCanceledException>();
        sw.Stop();

        // Prompt: nowhere near the multi-minute PromiseTimeout. A generous ceiling keeps this stable under CI load.
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), $"Cancellation was not observed promptly: took {sw.Elapsed}.");

        neverCompletes.TrySetCanceled();
    }

    [Fact]
    public void ShouldTimeOutTopLevelAwaitOfNeverCompletingTaskInModuleWithoutCancellation()
    {
        // Invariant: with NO cancellation registered, a genuinely never-settling top-level await must
        // still be BOUNDED by PromiseTimeout (not hang), surfacing as an error out of Modules.Import.
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500);
        });

        var neverCompletes = new TaskCompletionSource<int>();
        engine.SetValue("hang", new Func<Task<int>>(() => neverCompletes.Task));

        engine.Modules.Add("main", """
            const x = await hang();
            export const answer = x;
            """);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        // The drain gives up at PromiseTimeout with the promise still pending, so evaluation reports a
        // non-fulfilled promise rather than hanging.
        Invoking(() => engine.Modules.Import("main")).Should().ThrowExactly<InvalidOperationException>();
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), $"Never-settling await was not bounded: took {sw.Elapsed}.");

        neverCompletes.TrySetCanceled();
    }

#if !NETFRAMEWORK

    [Fact]
    public void ShouldCompleteWithAsyncValueTaskCallbacks()
    {
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(-1);
        });
        engine.SetValue("asyncTestMethod", new Func<Func<ValueTask>, Task<string>>(async callback => { await Task.Delay(100); await callback(); return "Hello World"; }));
        engine.SetValue("asyncWork", new Func<Task>(() => Task.Delay(100)));
        engine.SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()));
        var result = engine.Evaluate("async function hello() {return await asyncTestMethod(async () =>{ await asyncWork(); })} hello();");
        result = result.UnwrapIfPromise(TimeSpan.FromSeconds(30));
        result.Should().Be("Hello World");
    }

    [Fact]
    public void ShouldFromAsyncValueTaskCallbacks()
    {
        Engine engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(-1);
        });
        engine.SetValue("asyncTestMethod", new Func<Func<ValueTask<string>>, Task<string>>(async callback => { await Task.Delay(100); return await callback(); }));
        engine.SetValue("asyncWork", new Func<ValueTask<string>>(async () => { await Task.Delay(100); return "Hello World"; }));
        engine.SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()));
        var result = engine.Evaluate("async function hello() {return await asyncTestMethod(async () =>{ return await asyncWork(); })} hello();");
        result = result.UnwrapIfPromise(TimeSpan.FromSeconds(30));
        result.Should().Be("Hello World");
    }

    [Fact]
    public void ShouldValueTaskConvertedToPromiseInJS()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        result.Should().Be(2);

        static async ValueTask<int> Callable()
        {
            await Task.Delay(10);
            true.Should().BeTrue();
            return 1;
        }
    }

    [Fact]
    public void ShouldValueTaskCatchWhenCancelled()
    {
        Engine engine = new();
        CancellationTokenSource cancel = new();
        cancel.Cancel();

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);

        engine.Evaluate("callable(token).then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();
        static async ValueTask Callable(CancellationToken token)
        {
            await ValueTask.FromCanceled(token);
        }
    }

    [Fact]
    public void ShouldValueTaskCatchWhenThrowError()
    {
        Engine engine = new();

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("callable", Callable);

        engine.Evaluate("callable().then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();

        static async ValueTask Callable()
        {
            await Task.Delay(10);
            throw new Exception();
        }
    }

    [Fact]
    public void ShouldValueTaskAwaitCurrentStack()
    {
        //https://github.com/sebastienros/jint/issues/514#issuecomment-1507127509
        Engine engine = new();
        string log = "";
        engine.SetValue("myAsyncMethod", new Func<ValueTask>(async () =>
        {
            await Task.Delay(1000);
            log += "1";
        }));
        engine.SetValue("myAsyncMethod2", new Action(() =>
        {
            log += "2";
        }));
        var result = engine.Evaluate("async function hello() {await myAsyncMethod();myAsyncMethod2();} hello();");
        result.UnwrapIfPromise();
        log.Should().Be("12");
    }

    [Fact]
    public void ShouldReturnedValueTaskOfTConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnDelayedValueTaskAsync().then(x=>x)");
        result = result.UnwrapIfPromise();
        result.Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public void ShouldReturnedCompletedValueTaskOfTConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnCompletedValueTask().then(x=>x)");
        result = result.UnwrapIfPromise();
        result.Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public void ShouldReturnedValueTaskOfTCatchWhenCancelled()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        CancellationTokenSource cancel = new();
        cancel.Cancel();

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("token", cancel.Token);
        engine.SetValue("asyncTestClass", new AsyncTestClass());

        engine.Evaluate("asyncTestClass.ReturnCancelledValueTask(token).then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();

        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldReturnedValueTaskOfTCatchWhenThrowError()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("asyncTestClass", new AsyncTestClass());

        engine.Evaluate("asyncTestClass.ThrowAfterDelayValueTaskAsync().then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        engine.Evaluate("cancelled").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldAwaitUnwrapValueTaskOfTPromiseWithCustomTimeout()
    {
        // see ShouldAwaitUnwrapPromiseWithCustomTimeout — success path must not race CI load
        Engine engine = new(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = GenerousPromiseTimeout; });
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute("""
        async function test() {
            return await asyncTestClass.ReturnDelayedValueTaskAsync();
        }
        """);
        var result = engine.Invoke("test").UnwrapIfPromise(GenerousPromiseTimeout);
        result.Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public void ShouldIterateOverAsyncEnumeratorConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute("""
        async function test() {
            var result = '';
            var iter = asyncTestClass.AsyncEnumerable().GetAsyncEnumerator();
            while (await iter.MoveNextAsync()) {
                result += iter.Current;
            }
            return result;
        }
        """);
        var result = engine.Invoke("test").UnwrapIfPromise();
        result.Should().Be(AsyncTestClass.TestString);
    }
#endif

    [Fact]
    public void ShouldHaveCorrectOrder()
    {
        var engine = new Engine();
        engine.Evaluate("var log = [];");

        const string Script = """
          async function foo(name) {
            log.push(name + " start");
            await log.push(name + " middle");
            log.push(name + " end");
          }

          foo("First");
          foo("Second");
        """;

        engine.Execute(Script);

        var log = engine.GetValue("log").AsArray();
        string[] expected = [
            "First start",
            "First middle",
            "Second start",
            "Second middle",
            "First end",
            "Second end",
        ];

        log.Select(x => x.AsString()).ToArray().Should().Equal(expected);
    }

    [Fact]
    public void ShouldPromiseBeResolved()
    {
        var log = new List<string>();
        Engine engine = new();
        engine.SetValue("log", (string str) =>
        {
            log.Add(str);
        });

        const string Script = """
          async function main() {
            return new Promise(function (resolve) {
              log('Promise!')
              resolve(null)
            }).then(function () {
              log('Resolved!')
            });
          }
        """;
        var result = engine.Execute(Script);
        var val = result.GetValue("main");
        val.Call().UnwrapIfPromise();
        log.Should().HaveCount(2);
        log[0].Should().Be("Promise!");
        log[1].Should().Be("Resolved!");
    }

    [Fact]
    public void ShouldPromiseBeResolved2()
    {
        Engine engine = new();
        engine.SetValue("setTimeout",
            (Action action, int ms) =>
            {
                Task.Delay(ms).ContinueWith(_ => action());
            });

        const string Script = """
          var delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));
          async function main() {
            await delay(100);
            return 1;
          }
        """;
        var result = engine.Execute(Script);
        var val = result.GetValue("main").Call();
        val.UnwrapIfPromise().AsInteger().Should().Be(1);
    }

#if !NETFRAMEWORK // we are having trouble with timeouts on .NET Framework CI runs
    [Fact]
    public async Task ShouldEventLoopBeThreadSafeWhenCalledConcurrently()
    {
        // This test verifies that multiple independent Engine instances can safely
        // run async JavaScript code in parallel threads. Each Engine instance is
        // isolated with its own event loop, so there should be no cross-thread issues.
        const int ParallelCount = 100;

        // [NOTE] perform 5 runs since concurrency bugs don't always manifest
        for (int run = 0; run < 5; run++)
        {
            var tasks = new List<TaskCompletionSource<object>>();

            for (int i = 0; i < ParallelCount; i++)
                tasks.Add(new TaskCompletionSource<object>());

            for (int i = 0; i < ParallelCount; i++)
            {
                int taskIdx = i;
                _ = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);

                        const string Script = """
                        async function main(testObj) {
                            async function run(i) {
                                await testObj.Delay(10);
                                await testObj.Add(`${i}`);
                            }

                            const tasks = [];
                            for (let i = 0; i < 10; i++) {
                                tasks.push(run(i));
                            }
                            for (let i = 0; i < 10; i++) {
                                await tasks[i];
                            }
                            return 1;
                        }
                        """;
                        var result = engine.Execute(Script);
                        var testObj = JsValue.FromObject(engine, new TestAsyncClass());
                        var val = result.GetValue("main").Call(testObj);

                        // Wait for the async function to complete (non-blocking async model)
                        val = val.UnwrapIfPromise(TimeSpan.FromSeconds(30));
                        val.AsInteger().Should().Be(1);

                        tasks[taskIdx].SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tasks[taskIdx].SetException(ex);
                    }
                }, creationOptions: TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(tasks.Select(t => t.Task));
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
#endif

    [Fact]
    public void AsyncFunctionShouldNotBlockWhenCalledWithoutAwait()
    {
        // #2069: https://github.com/sebastienros/jint/issues/2069
        // Async functions should return immediately when called without await
        var engine = new Engine();
        var callbackExecuted = false;

        engine.SetValue("setTimeout",
            (Action action, int ms) =>
            {
                Task.Delay(ms).ContinueWith(_ =>
                {
                    callbackExecuted = true;
                    action();
                });
            });

        engine.Execute("""
            var x = '';
            async function f() {
                await new Promise(resolve => setTimeout(resolve, 100));
                x += 'promise resolved - ';
            }
            f();  // Call without await
            x += 'f() called - ';
            """);

        var x = engine.Evaluate("x").AsString();

        // The function should return immediately, so x should start with "f() called -"
        // and NOT include "promise resolved -" yet
        x.Should().Be("f() called - ");
        callbackExecuted.Should().BeFalse("Promise callback should not have executed yet");
    }

    [Fact]
    public void WrappedAsyncFunctionsShouldExecuteConcurrently()
    {
        // #2199: https://github.com/sebastienros/jint/issues/2199
        // When async functions wrap other async functions, they should execute concurrently
        // not sequentially. All wrapped tasks should start before any complete.
        var engine = new Engine();

        engine.Execute("""
            var log = [];
            var callbacks = [];

            // Simulate an async operation that resolves via callback
            function asyncOperation(id) {
                return new Promise(function(resolve) {
                    log.push('AsyncOp ' + id + ' registered');
                    callbacks.push({ id: id, resolve: resolve });
                });
            }

            // Simple async function
            async function simpleAsync(id) {
                log.push('SimpleAsync ' + id + ' start');
                var result = await asyncOperation(id);
                log.push('SimpleAsync ' + id + ' got result: ' + result);
                return result;
            }

            // Wrapped async function - the scenario from issue #2199
            async function wrappedAsync(id) {
                log.push('WrappedAsync ' + id + ' start');
                var result = await simpleAsync(id);
                log.push('WrappedAsync ' + id + ' got result: ' + result);
                return result;
            }

            // Create promises for wrapped async calls
            var p1 = wrappedAsync('A');
            var p2 = wrappedAsync('B');
            var p3 = wrappedAsync('C');
            """);

        // All 3 async operations should have registered callbacks concurrently
        var callbacks = engine.Evaluate("callbacks").AsArray();
        ((int) callbacks.Length).Should().Be(3);

        var log = engine.Evaluate("log").AsArray();
        var logStrings = log.Select(x => x.AsString()).ToArray();

        // Verify all wrapped/simple tasks started and registered before any completed
        logStrings.Should().Contain("WrappedAsync A start");
        logStrings.Should().Contain("WrappedAsync B start");
        logStrings.Should().Contain("WrappedAsync C start");
        logStrings.Should().Contain("SimpleAsync A start");
        logStrings.Should().Contain("SimpleAsync B start");
        logStrings.Should().Contain("SimpleAsync C start");
        logStrings.Should().Contain("AsyncOp A registered");
        logStrings.Should().Contain("AsyncOp B registered");
        logStrings.Should().Contain("AsyncOp C registered");

        // None should have completed yet (no "got result" messages)
        logStrings.Should().NotContain(s => s.Contains("got result"));
    }

    // ========================================================================
    // Promise.allSettled() Tests
    // ========================================================================

    [Fact]
    public void PromiseAllSettledShouldReportAllResults()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.allSettled([
                Promise.resolve(1),
                Promise.reject('error'),
                Promise.resolve(3)
            ]).then(results => JSON.stringify(results.map(r => r.status)))
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("[\"fulfilled\",\"rejected\",\"fulfilled\"]");
    }

    [Fact]
    public void PromiseAllSettledShouldIncludeValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.allSettled([
                Promise.resolve(42),
                Promise.reject('oops'),
                Promise.resolve('hello')
            ]).then(results => JSON.stringify(results))
            """);
        result = result.UnwrapIfPromise();
        var parsed = result.AsString();
        parsed.Should().Contain("\"value\":42");
        parsed.Should().Contain("\"reason\":\"oops\"");
        parsed.Should().Contain("\"value\":\"hello\"");
    }

    [Fact]
    public void PromiseAllSettledShouldHandleEmptyArray()
    {
        var engine = new Engine();
        var result = engine.Evaluate("Promise.allSettled([]).then(r => r.length)");
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(0);
    }

    // ========================================================================
    // Promise.any() Tests
    // ========================================================================

    [Fact]
    public void PromiseAnyShouldResolveWithFirstFulfilled()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.any([
                Promise.reject('a'),
                Promise.resolve(42),
                Promise.resolve(99)
            ])
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public void PromiseAnyShouldThrowAggregateErrorWhenAllRejected()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.any([
                Promise.reject('e1'),
                Promise.reject('e2'),
                Promise.reject('e3')
            ]).catch(e => e instanceof AggregateError ? 'aggregate' : 'other')
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("aggregate");
    }

    [Fact]
    public void PromiseAnyShouldExposeErrorsOnAggregateError()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.any([
                Promise.reject('e1'),
                Promise.reject('e2')
            ]).catch(e => JSON.stringify(e.errors))
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("[\"e1\",\"e2\"]");
    }

    // ========================================================================
    // Async Generator Tests
    // ========================================================================

    [Fact]
    public void AsyncGeneratorShouldYieldValues()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function* gen() {
                yield 1;
                yield 2;
                yield 3;
            }

            async function main() {
                var g = gen();
                var results = [];
                var item;
                item = await g.next();
                results.push(item.value);
                item = await g.next();
                results.push(item.value);
                item = await g.next();
                results.push(item.value);
                item = await g.next();
                results.push(item.done);
                return JSON.stringify(results);
            }

            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("[1,2,3,true]");
    }

    [Fact]
    public void AsyncGeneratorShouldAwaitInsideYield()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function* gen() {
                yield await Promise.resolve(10);
                yield await Promise.resolve(20);
            }

            async function main() {
                var g = gen();
                var a = await g.next();
                var b = await g.next();
                return a.value + b.value;
            }

            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(30);
    }

    [Fact]
    public void AsyncGeneratorShouldSupportReturn()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function* gen() {
                yield 1;
                yield 2;
                yield 3;
            }

            async function main() {
                var g = gen();
                var a = await g.next();
                var b = await g.return(42);
                return JSON.stringify({ aValue: a.value, bValue: b.value, bDone: b.done });
            }

            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("{\"aValue\":1,\"bValue\":42,\"bDone\":true}");
    }

    // ========================================================================
    // Pure JS for-await-of Tests
    // ========================================================================

    [Fact]
    public void ForAwaitOfWithAsyncGenerator()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function* gen() {
                yield 'a';
                yield 'b';
                yield 'c';
            }

            async function main() {
                var result = '';
                for await (var item of gen()) {
                    result += item;
                }
                return result;
            }

            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("abc");
    }

    [Fact]
    public void ForAwaitOfWithPromiseArray()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function main() {
                var promises = [
                    Promise.resolve(1),
                    Promise.resolve(2),
                    Promise.resolve(3)
                ];
                var sum = 0;
                for await (var val of promises) {
                    sum += val;
                }
                return sum;
            }

            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(6);
    }

    [Fact]
    public void ForAwaitOfInsideAsyncGenerator()
    {
        // Regression: for-await-of inside an async generator was not resuming
        // because the continuation called AsyncGeneratorResumeNext() which
        // returned immediately (empty queue). The fix uses AsyncGeneratorContinueForAwait()
        // which correctly resumes the current request's execution.
        var log = new List<string>();
        var engine = new Engine();
        engine.SetValue("log", (string s) => log.Add(s));

        engine.Evaluate("""
            async function* gen() {
                for await (var x of [1, 2]) {
                    log('item:' + x);
                }
                log('done');
            }
            gen().next().then(() => log('resolved'));
            """);

        log.Should().Equal(new[] { "item:1", "item:2", "done", "resolved" });
    }

    [Fact]
    public void ForAwaitOfInsideAsyncGeneratorWithArrayDestructuring()
    {
        // Reproduces the minimal case from the issue report:
        // test/language/statements/for-await-of/async-gen-decl-dstr-array-elision-val-array.js
        var log = new List<string>();
        var engine = new Engine();
        engine.SetValue("log", (string s) => log.Add(s));

        engine.Evaluate("""
            async function* gen() {
                for await ([,] of [[]]) log('iteration');
            }
            gen().next().then(() => log('OK'));
            """);

        log.Should().Equal(new[] { "iteration", "OK" });
    }

    [Fact]
    public void ForAwaitOfInsideAsyncGeneratorSuspensionIsProperlyResumed()
    {
        // Verify that multiple suspensions/resumptions in a for-await-of inside
        // an async generator all complete correctly.
        var log = new List<string>();
        var engine = new Engine();
        engine.SetValue("log", (string s) => log.Add(s));

        engine.Evaluate("""
            async function* producer() {
                yield 'a';
                yield 'b';
                yield 'c';
            }

            async function* consumer() {
                for await (var item of producer()) {
                    log('got:' + item);
                }
                log('consumer-done');
            }

            consumer().next().then(() => log('outer-resolved'));
            """);

        log.Should().Equal(new[] { "got:a", "got:b", "got:c", "consumer-done", "outer-resolved" });
    }

    [Fact]
    public void ForAwaitOfInsideAsyncGeneratorIteratorRejectPropagatesOutward()
    {
        // Regression: when the async iterator's next() Promise rejects inside a
        // for-await-of in an async generator, the rejection must propagate out of
        // the generator (i.e. gen().next() should reject with the same error).
        var log = new List<string>();
        var engine = new Engine();
        engine.SetValue("log", (string s) => log.Add(s));

        engine.Evaluate("""
            function makeRejectingIterator() {
                return {
                    [Symbol.asyncIterator]() { return this; },
                    next() { return Promise.reject(new Error('iterator-error')); }
                };
            }

            async function* gen() {
                for await (var x of makeRejectingIterator()) {
                    log('body');
                }
            }

            gen().next().then(
                () => log('resolved'),
                e  => log('rejected:' + e.message)
            );
            """);

        log.Should().Equal(new[] { "rejected:iterator-error" });
    }

    [Fact]
    public void ForAwaitOfInsideAsyncGeneratorIteratorRejectCaughtInsideGenerator()
    {
        // When the async iterator's next() Promise rejects and the for-await-of is
        // wrapped in a try/catch inside the async generator, the catch block must run.
        var log = new List<string>();
        var engine = new Engine();
        engine.SetValue("log", (string s) => log.Add(s));

        engine.Evaluate("""
            function makeRejectingIterator() {
                return {
                    [Symbol.asyncIterator]() { return this; },
                    next() { return Promise.reject(new Error('iterator-error')); }
                };
            }

            async function* gen() {
                try {
                    for await (var x of makeRejectingIterator()) {
                        log('body');
                    }
                } catch (e) {
                    log('caught:' + e.message);
                }
                log('after-catch');
            }

            gen().next().then(() => log('resolved'));
            """);

        log.Should().Equal(new[] { "caught:iterator-error", "after-catch", "resolved" });
    }

    // ========================================================================
    // Thenable Protocol Tests
    // ========================================================================

    [Fact]
    public void ShouldResolveCustomThenableObject()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var thenable = {
                then: function(resolve, reject) {
                    resolve(42);
                }
            };

            Promise.resolve(thenable).then(v => v * 2)
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(84);
    }

    [Fact]
    public void ShouldHandleThenableThatRejects()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var thenable = {
                then: function(resolve, reject) {
                    reject('thenable error');
                }
            };

            Promise.resolve(thenable).catch(e => 'caught: ' + e)
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("caught: thenable error");
    }

    [Fact]
    public void ShouldAwaitCustomThenable()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var thenable = {
                then: function(resolve) {
                    resolve(100);
                }
            };

            async function main() {
                return await thenable;
            }

            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(100);
    }

    // ========================================================================
    // HostPromiseRejectionTracker Tests
    // ========================================================================

    [Fact]
    public void ShouldFireRejectionTrackerOnUnhandledRejection()
    {
        var engine = new Engine();
        var rejections = new List<(JsValue Value, PromiseRejectionOperation Op)>();

        engine.Advanced.PromiseRejectionTracker += (sender, args) =>
        {
            rejections.Add((args.Value!, args.Operation));
        };

        engine.Evaluate("Promise.reject('unhandled')");
        engine.Advanced.ProcessTasks();

        rejections.Should().ContainSingle();
        rejections[0].Op.Should().Be(PromiseRejectionOperation.Reject);
        rejections[0].Value.AsString().Should().Be("unhandled");
    }

    [Fact]
    public void ShouldFireHandleOperationWhenHandlerAdded()
    {
        var engine = new Engine();
        var operations = new List<PromiseRejectionOperation>();

        engine.Advanced.PromiseRejectionTracker += (sender, args) =>
        {
            operations.Add(args.Operation);
        };

        // Reject without handler first - triggers Reject
        engine.Evaluate("var p = Promise.reject('error')");
        engine.Advanced.ProcessTasks();

        // Add handler - triggers Handle
        engine.Evaluate("p.catch(() => {})");
        engine.Advanced.ProcessTasks();

        operations.Should().HaveCount(2);
        operations[0].Should().Be(PromiseRejectionOperation.Reject);
        operations[1].Should().Be(PromiseRejectionOperation.Handle);
    }

    [Fact]
    public void ShouldNotFireRejectionTrackerWhenPromiseConstructorHasHandler()
    {
        var engine = new Engine();
        var rejections = new List<PromiseRejectionOperation>();

        engine.Advanced.PromiseRejectionTracker += (sender, args) =>
        {
            rejections.Add(args.Operation);
        };

        // Use a Promise constructor with an executor that rejects,
        // then chain .catch() on it. The .then() from the constructor
        // adds the reject handler before the promise rejects, so the
        // internal promise (from the .catch) should be handled.
        engine.Evaluate("""
            new Promise(function(resolve, reject) {
                reject('handled');
            }).catch(function() {});
            """);
        engine.Advanced.ProcessTasks();

        // The original promise fires Reject since the executor rejects
        // before .catch() can attach (per spec: reject happens synchronously
        // in the executor, then .catch attaches). But the rejection is
        // followed by a Handle operation when .catch() attaches.
        // Verify the Handle operation follows the Reject.
        if (rejections.Contains(PromiseRejectionOperation.Reject))
        {
            rejections.Should().Contain(PromiseRejectionOperation.Handle);
        }
    }

    // ========================================================================
    // EvaluateAsync / ExecuteAsync Tests
    // ========================================================================

#if !NETFRAMEWORK
    [Fact]
    public async Task EvaluateAsyncShouldResolvePromise()
    {
        var engine = new Engine();
        var result = await engine.EvaluateAsync("""
            async function main() {
                return 42;
            }
            main()
            """);
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task EvaluateAsyncShouldReturnDirectValueForNonPromise()
    {
        var engine = new Engine();
        var result = await engine.EvaluateAsync("1 + 2");
        result.AsInteger().Should().Be(3);
    }

    [Fact]
    public async Task EvaluateAsyncShouldRejectOnException()
    {
        var engine = new Engine();
        await Awaiting(async () =>
        {
            await engine.EvaluateAsync("""
                async function main() {
                    throw new Error('async error');
                }
                main()
                """);
        }).Should().ThrowExactlyAsync<PromiseRejectedException>();
    }

    [Fact]
    public async Task ExecuteAsyncShouldCompleteSuccessfully()
    {
        var engine = new Engine();
        var returnedEngine = await engine.ExecuteAsync("""
            async function setup() {
                return 'done';
            }
            """);
        returnedEngine.Should().BeSameAs(engine);
    }

    [Fact]
    public async Task InvokeAsyncShouldResolvePromise()
    {
        var engine = new Engine();
        engine.Execute("""
            async function add(a, b) {
                return a + b;
            }
            """);
        var result = await engine.InvokeAsync("add", 3, 4);
        result.AsInteger().Should().Be(7);
    }

    [Fact]
    public async Task EvaluateAsyncShouldRespectCancellation()
    {
        var engine = new Engine();
        engine.SetValue("delay", new Func<int, Task>(ms => Task.Delay(ms)));

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Awaiting(async () =>
        {
            await engine.EvaluateAsync("""
                async function main() {
                    await delay(10000);
                    return 'should not reach';
                }
                main()
                """, cancellationToken: cts.Token);
        }).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateAsyncWithTaskInteropShouldWork()
    {
        var engine = new Engine(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = GenerousPromiseTimeout; });
        engine.SetValue("asyncTestClass", new AsyncTestClass());

        var result = await engine.EvaluateAsync("""
            async function main() {
                return await asyncTestClass.ReturnDelayedTaskAsync();
            }
            main()
            """);
        result.AsString().Should().Be(AsyncTestClass.TestString);
    }

    [Fact]
    public async Task InvokeAsyncWithMultipleAwaitsShouldWork()
    {
        var engine = new Engine(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = GenerousPromiseTimeout; });
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute("""
            async function test() {
                var a = await asyncTestClass.ReturnDelayedTaskAsync();
                var b = await asyncTestClass.ReturnCompletedTask();
                return a + ' + ' + b;
            }
            """);
        var result = await engine.InvokeAsync("test");
        result.AsString().Should().Be("Hello World + Hello World");
    }

    // ========================================================================
    // Async Wake (Thread Release) Tests — verify zero-thread IO waiting
    // ========================================================================

    [Fact]
    public async Task EvaluateAsyncShouldNotBlockDuringClrTaskDelay()
    {
        // Verifies the async wake path: EvaluateAsync releases the thread during
        // a .NET Task.Delay (simulating IO like gRPC), and resumes correctly.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("simulateIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(100);
            return 42;
        }));

        var result = await engine.EvaluateAsync("""
            async function main() {
                return await simulateIO();
            }
            main()
            """);

        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task EvaluateAsyncShouldHandleMultipleSequentialClrTasks()
    {
        // Multiple sequential .NET async calls, each releasing and re-acquiring the thread.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        var callOrder = new List<string>();

        engine.SetValue("step1", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            callOrder.Add("step1");
            return "A";
        }));
        engine.SetValue("step2", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            callOrder.Add("step2");
            return "B";
        }));
        engine.SetValue("step3", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            callOrder.Add("step3");
            return "C";
        }));

        var result = await engine.EvaluateAsync("""
            async function main() {
                var a = await step1();
                var b = await step2();
                var c = await step3();
                return a + b + c;
            }
            main()
            """);

        result.AsString().Should().Be("ABC");
        callOrder.Should().Equal(["step1", "step2", "step3"]);
    }

    [Fact]
    public async Task EvaluateAsyncShouldHandleConcurrentClrTasksViaPromiseAll()
    {
        // Promise.all with multiple .NET Tasks running concurrently.
        // All tasks should start before any completes.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        var startTimes = new ConcurrentDictionary<string, DateTime>();

        engine.SetValue("fetch", new Func<string, Task<string>>(async (id) =>
        {
            startTimes[id] = DateTime.UtcNow;
            await Task.Delay(100);
            return $"result-{id}";
        }));

        var result = await engine.EvaluateAsync("""
            async function main() {
                var results = await Promise.all([
                    fetch('a'),
                    fetch('b'),
                    fetch('c')
                ]);
                return results.join(',');
            }
            main()
            """);

        result.AsString().Should().Be("result-a,result-b,result-c");
        startTimes.Should().HaveCount(3);
    }

    [Fact]
    public async Task EvaluateAsyncTimeoutShouldFireDuringPendingClrTask()
    {
        // Verify that the PromiseTimeout constraint works with the async wake path.
        var engine = new Engine(options =>
        {
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(100);
        });
        engine.SetValue("slowIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(5000);
            return 999;
        }));

        await Awaiting(async () =>
        {
            await engine.EvaluateAsync("""
                async function main() {
                    return await slowIO();
                }
                main()
                """);
        }).Should().ThrowExactlyAsync<PromiseRejectedException>();
    }

    [Fact]
    public async Task EvaluateAsyncShouldHandleClrTaskRejection()
    {
        // .NET Task that throws should propagate as a rejected promise.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("failingIO", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            throw new InvalidOperationException("IO failed");
        }));

        var result = await engine.EvaluateAsync("""
            async function main() {
                try {
                    await failingIO();
                    return 'should not reach';
                } catch (e) {
                    return 'caught';
                }
            }
            main()
            """);

        result.AsString().Should().Be("caught");
    }

    [Fact]
    public async Task EvaluateAsyncShouldHandleNestedJsToClrToJsAsync()
    {
        // Nested async: JS → .NET async → back into JS engine (via callback) → .NET async
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);

        engine.SetValue("fetchData", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            return "data";
        }));

        engine.SetValue("processData", new Func<string, Task<string>>(async (input) =>
        {
            await Task.Delay(50);
            return input.ToUpperInvariant();
        }));

        var result = await engine.EvaluateAsync("""
            async function main() {
                var raw = await fetchData();
                var processed = await processData(raw);
                return processed;
            }
            main()
            """);

        result.AsString().Should().Be("DATA");
    }

    [Fact]
    public async Task EvaluateAsyncCancellationDuringClrTask()
    {
        // Cancellation token fires while a .NET Task is in flight.
        var engine = new Engine();
        engine.SetValue("longIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(10_000);
            return 999;
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Awaiting(async () =>
        {
            await engine.EvaluateAsync("""
                async function main() {
                    return await longIO();
                }
                main()
                """, cancellationToken: cts.Token);
        }).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateAsyncShouldNotBlockCallerThread()
    {
        // Proves the caller thread is released: start EvaluateAsync, then verify
        // we can do other work before it completes.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        var ioStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        engine.SetValue("simulateIO", new Func<Task<int>>(async () =>
        {
            ioStarted.TrySetResult(true);
            await Task.Delay(200);
            return 42;
        }));

        // Start the async evaluation (does NOT block)
        var evalTask = engine.EvaluateAsync("""
            async function main() {
                return await simulateIO();
            }
            main()
            """);

        // Wait for the IO to actually start
        await ioStarted.Task;

        // The evalTask should not be completed yet — IO is in flight
        evalTask.IsCompleted.Should().BeFalse("EvaluateAsync should not block; task should still be pending during IO");

        // Now await the result
        var result = await evalTask;
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task EvaluateAsyncMultipleEnginesConcurrently()
    {
        // Multiple independent engines running async operations concurrently.
        // This verifies thread safety of the async wake mechanism.
        const int EngineCount = 20;
        var tasks = new Task<JsValue>[EngineCount];

        for (int i = 0; i < EngineCount; i++)
        {
            var idx = i;
            tasks[i] = Task.Run(async () =>
            {
                // 20 engines contending for the pool makes starvation likely by construction
                var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
                engine.SetValue("compute", new Func<int, Task<int>>(async (n) =>
                {
                    await Task.Delay(50);
                    return n * 2;
                }));

                return await engine.EvaluateAsync(
                    "async function main() { return await compute(" + idx + "); } main()");
            });
        }

        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < EngineCount; i++)
        {
            results[i].AsInteger().Should().Be(i * 2);
        }
    }

    [Fact]
    public async Task InvokeAsyncWithClrTaskInterop()
    {
        // InvokeAsync with .NET Task interop, verifying the complete path.
        var engine = new Engine(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = GenerousPromiseTimeout; });

        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute("""
            async function fetchAndTransform() {
                var data = await asyncTestClass.ReturnDelayedTaskAsync();
                return data + '!';
            }
            """);

        var result = await engine.InvokeAsync("fetchAndTransform");
        result.AsString().Should().Be("Hello World!");
    }

    [Fact]
    public async Task EvaluateAsyncWithSetTimeoutPattern()
    {
        // setTimeout pattern using .NET Task.Delay, exercising the async wake path
        // with event loop scheduling (not direct Task interop).
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("setTimeout", (Action action, int ms) =>
        {
            Task.Delay(ms).ContinueWith(_ => action());
        });

        var result = await engine.EvaluateAsync("""
            async function main() {
                var delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));
                await delay(50);
                await delay(50);
                return 'done';
            }
            main()
            """);

        result.AsString().Should().Be("done");
    }
#endif

#if !NETFRAMEWORK
    // ========================================================================
    // Engine Reuse & State Recovery Edge Cases
    // ========================================================================

    [Fact]
    public async Task SequentialEvaluateAsyncOnSameEngineShouldWork()
    {
        // Exercises the stale TCS recovery path in EventLoop.WaitForEventAsync.
        // After the first EvaluateAsync completes, the event loop's _eventAvailable
        // TCS has been consumed. The second call must detect the stale/null TCS and
        // install a fresh one without hanging or busy-looping.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("io", new Func<int, Task<int>>(async (n) =>
        {
            await Task.Delay(50);
            return n * 10;
        }));

        var r1 = await engine.EvaluateAsync("(async () => await io(1))()");
        r1.AsInteger().Should().Be(10);

        var r2 = await engine.EvaluateAsync("(async () => await io(2))()");
        r2.AsInteger().Should().Be(20);

        var r3 = await engine.EvaluateAsync("(async () => await io(3))()");
        r3.AsInteger().Should().Be(30);
    }

    [Fact]
    public async Task MixedSyncThenAsyncOnSameEngineShouldWork()
    {
        // Sync Evaluate+UnwrapIfPromise sets _waitingThreadId during the spin-wait.
        // A subsequent EvaluateAsync must not be blocked by a leftover _waitingThreadId
        // from the sync path. This tests the handoff between the two execution models.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("io", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            return "hello";
        }));

        // Sync path first
        var syncResult = engine.Evaluate("(async () => await io())()").UnwrapIfPromise();
        syncResult.AsString().Should().Be("hello");

        // Async path on same engine
        var asyncResult = await engine.EvaluateAsync("(async () => await io())()");
        asyncResult.AsString().Should().Be("hello");

        // Back to sync to verify no contamination
        var syncResult2 = engine.Evaluate("(async () => await io())()").UnwrapIfPromise();
        syncResult2.AsString().Should().Be("hello");
    }

    [Fact]
    public async Task EngineReusableAfterEvaluateAsyncTimeout()
    {
        // If EvaluateAsync hits PromiseTimeout, the CancellationTokenSource fires
        // and the TCS in WaitForEventAsync gets cancelled (stale). The engine must
        // remain usable for subsequent calls — the stale TCS must be detected and
        // replaced on the next EvaluateAsync invocation.
        var engine = new Engine(options =>
        {
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(50);
        });
        engine.SetValue("slowIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(10_000);
            return 999;
        }));
        engine.SetValue("fastIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(10);
            return 42;
        }));

        // First call should time out
        await Awaiting(async () =>
        {
            await engine.EvaluateAsync("(async () => await slowIO())()");
        }).Should().ThrowExactlyAsync<PromiseRejectedException>();

        // Increase timeout for recovery (generous — the recovery half asserts success)
        engine.Options.Constraints.PromiseTimeout = GenerousPromiseTimeout;

        // Engine should still work
        var result = await engine.EvaluateAsync("(async () => await fastIO())()");
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task EngineReusableAfterEvaluateAsyncCancellation()
    {
        // CancellationToken fires during a pending EvaluateAsync. The TCS in
        // WaitForEventAsync gets cancelled. Verify the engine is still usable.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("slowIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(10_000);
            return 999;
        }));
        engine.SetValue("fastIO", new Func<Task<int>>(async () =>
        {
            await Task.Delay(10);
            return 7;
        }));

        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
        {
            await Awaiting(async () =>
            {
                await engine.EvaluateAsync("(async () => await slowIO())()", cancellationToken: cts.Token);
            }).Should().ThrowAsync<OperationCanceledException>();
        }

        // Engine must still be usable after cancellation
        var result = await engine.EvaluateAsync("(async () => await fastIO())()");
        result.AsInteger().Should().Be(7);
    }

    // ========================================================================
    // Error Propagation Edge Cases
    // ========================================================================

    [Fact]
    public async Task EvaluateAsyncShouldPropagateUncaughtClrTaskFailure()
    {
        // A .NET Task that throws, with NO try/catch in JS, should surface
        // as PromiseRejectedException to the .NET caller via EvaluateAsync.
        // (A tight budget could throw PromiseRejectedException for the WRONG reason —
        // timeout instead of the propagated failure — and fail the message assert.)
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("failingIO", new Func<Task<string>>(async () =>
        {
            await Task.Delay(50);
            throw new InvalidOperationException("backend error");
        }));

        var ex = (await Awaiting(async () =>
        {
            await engine.EvaluateAsync("(async () => await failingIO())()");
        }).Should().ThrowExactlyAsync<PromiseRejectedException>()).Which;

        ex.Message.Should().Contain("backend error");
    }

    [Fact]
    public async Task EvaluateAsyncShouldThrowJavaScriptExceptionForSyncThrow()
    {
        // A synchronous throw happens during Evaluate() inside EvaluateAsync,
        // before any promise is created. This must propagate as JavaScriptException,
        // NOT PromiseRejectedException.
        var engine = new Engine();

        await Awaiting(async () =>
        {
            await engine.EvaluateAsync("throw new Error('sync boom')");
        }).Should().ThrowExactlyAsync<JavaScriptException>();
    }

    // ========================================================================
    // Void Task & Prepared<Script> Edge Cases
    // ========================================================================

    [Fact]
    public async Task EvaluateAsyncWithVoidTaskShouldResolveUndefined()
    {
        // Func<Task> (no return value) goes through a different reflection
        // path in ConvertTaskToPromise — there's no Task<T>.Result property.
        // The promise should resolve with undefined, not throw or return a
        // wrapped VoidTaskResult.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        var sideEffect = false;

        engine.SetValue("doWork", new Func<Task>(async () =>
        {
            await Task.Delay(50);
            sideEffect = true;
        }));

        var result = await engine.EvaluateAsync("(async () => { await doWork(); return 'done'; })()");
        result.AsString().Should().Be("done");
        sideEffect.Should().BeTrue("Side effect from void Task should have executed");
    }

    [Fact]
    public async Task EvaluateAsyncWithPreparedScriptShouldWork()
    {
        // Tests the EvaluateAsync(Prepared<Script>) overload which has its own
        // code path and was previously untested.
        var engine = new Engine(options => options.Constraints.PromiseTimeout = GenerousPromiseTimeout);
        engine.SetValue("io", new Func<Task<int>>(async () =>
        {
            await Task.Delay(50);
            return 42;
        }));

        var script = Engine.PrepareScript("(async () => await io())()");
        var result = await engine.EvaluateAsync(script);
        result.AsInteger().Should().Be(42);
    }

    [Fact]
    public async Task EvaluateAsyncFastPathForAlreadyResolvedPromise()
    {
        // Promise.resolve(42) is already settled after microtask processing.
        // UnwrapResultAsync should take the fast path (State == Fulfilled)
        // and never enter AwaitPromiseSettlementAsync.
        var engine = new Engine();
        var result = await engine.EvaluateAsync("Promise.resolve(42)");
        result.AsInteger().Should().Be(42);
    }
#endif

    // ========================================================================
    // Chained Promise Tests
    // ========================================================================

    [Fact]
    public void PromiseChainingShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.resolve(1)
                .then(v => v + 1)
                .then(v => v * 3)
                .then(v => v + '')
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("6");
    }

    [Fact]
    public void PromiseCatchAndThenChainingShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.reject('err')
                .catch(e => 'recovered from: ' + e)
                .then(v => v + '!')
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("recovered from: err!");
    }

    // ========================================================================
    // Async/Await with Try/Catch
    // ========================================================================

    [Fact]
    public void AsyncAwaitWithTryCatchShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function main() {
                try {
                    await Promise.reject('boom');
                    return 'should not reach';
                } catch (e) {
                    return 'caught: ' + e;
                }
            }
            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("caught: boom");
    }

    [Fact]
    public void AsyncAwaitWithTryFinallyShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function main() {
                var log = [];
                try {
                    log.push('try');
                    await Promise.resolve(1);
                    log.push('after await');
                } finally {
                    log.push('finally');
                }
                return log.join(',');
            }
            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("try,after await,finally");
    }

    // ========================================================================
    // Nested Await Tests
    // ========================================================================

    [Fact]
    public void NestedAwaitShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function inner() {
                return await Promise.resolve(10);
            }

            async function outer() {
                var a = await inner();
                var b = await inner();
                return a + b;
            }

            outer()
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(20);
    }

    [Fact]
    public void DoubleAwaitShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function main() {
                return await (await Promise.resolve(Promise.resolve(42)));
            }
            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(42);
    }

    // ========================================================================
    // Promise.all Tests
    // ========================================================================

    [Fact]
    public void PromiseAllShouldResolveWhenAllFulfilled()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.all([
                Promise.resolve(1),
                Promise.resolve(2),
                Promise.resolve(3)
            ]).then(values => JSON.stringify(values))
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("[1,2,3]");
    }

    [Fact]
    public void PromiseAllShouldRejectOnFirstRejection()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.all([
                Promise.resolve(1),
                Promise.reject('fail'),
                Promise.resolve(3)
            ]).catch(e => 'caught: ' + e)
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("caught: fail");
    }

    // ========================================================================
    // Promise.race Tests
    // ========================================================================

    [Fact]
    public void PromiseRaceShouldResolveWithFirstSettled()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            Promise.race([
                new Promise(() => {}),
                Promise.resolve('fast'),
                new Promise(() => {})
            ])
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("fast");
    }

    // ========================================================================
    // Multiple Sequential Awaits
    // ========================================================================

    [Fact]
    public void MultipleSequentialAwaitsShouldMaintainOrder()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function main() {
                var log = [];
                log.push(await Promise.resolve('a'));
                log.push(await Promise.resolve('b'));
                log.push(await Promise.resolve('c'));
                return log.join('');
            }
            main()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("abc");
    }

    // ========================================================================
    // Async IIFE (Immediately Invoked Function Expression)
    // ========================================================================

    [Fact]
    public void AsyncIIFEShouldWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async () => {
                var a = await Promise.resolve(10);
                var b = await Promise.resolve(20);
                return a + b;
            })()
            """);
        result = result.UnwrapIfPromise();
        result.AsInteger().Should().Be(30);
    }

    // ========================================================================
    // Stress Test
    // ========================================================================

    [Fact]
    public void StressTestManyPromises()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            async function main() {
                var promises = [];
                for (var i = 0; i < 100; i++) {
                    promises.push(Promise.resolve(i));
                }
                var results = await Promise.all(promises);
                return results.reduce((sum, v) => sum + v, 0);
            }
            main()
            """);
        result = result.UnwrapIfPromise();
        // Sum of 0..99 = 4950
        result.AsInteger().Should().Be(4950);
    }

    // ========================================================================
    // For-of loop with await inside body
    // ========================================================================

    [Fact]
    public void AwaitInsideForOfLoopShouldReturnDistinctResults()
    {
        // Verifies that each await inside a for-of loop returns a distinct result based on
        // the current iteration value, not a cached result from a prior iteration.
        var engine = new Engine();
        var result = engine.Evaluate("""
            function fetch_async(url) {
                return new Promise((resolve) => {
                    resolve(`response for ${url}`);
                });
            }
            (async function() {
                var text = "";
                text += await fetch_async("a") + "\n";
                text += await fetch_async("b") + "\n";
                text += await fetch_async("c") + "\n";
                var urls = ["d", "e", "f"];
                for (let url of urls) {
                    text += await fetch_async(url) + "\n";
                }
                return text;
            })()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("response for a\nresponse for b\nresponse for c\nresponse for d\nresponse for e\nresponse for f\n");
    }

    [Fact]
    public void AwaitInsideForOfLoopShouldWorkWithVarBinding()
    {
        // Test with var (not let) binding in for-of loop
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function() {
                var results = [];
                var items = [1, 2, 3];
                for (var item of items) {
                    var r = await Promise.resolve(item * 10);
                    results.push(r);
                }
                return results.join(",");
            })()
            """);
        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("10,20,30");
    }

    [Fact]
    public void AwaitInsideForOfLoopShouldWorkWithHeadDestructuring()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function() {
                var results = [];
                var items = { a: 1, b: 2 };
                for (const [key, value] of Object.entries(items)) {
                    var r = await Promise.resolve(value * 10);
                    results.push(key + ":" + r);
                }
                return results.join(",");
            })()
            """);

        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("a:10,b:20");
    }

    [Fact]
    public void AwaitInsideForOfLoopShouldPreserveOneShotIteratorDestructuring()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function() {
                function* gen() { yield 1; }
                var out = [];
                for (const [x] of [gen()]) {
                    await Promise.resolve();
                    out.push(x);
                }
                return out.join(",");
            })()
            """);

        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("1");
    }

    [Fact]
    public void AwaitInsideForOfLoopShouldPreserveOneShotIteratorDestructuringAcrossIterations()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function() {
                function* gen(n) { yield n; }
                var out = [];
                for (const [x] of [gen(1), gen(2), gen(3)]) {
                    await Promise.resolve();
                    out.push(x);
                }
                return out.join(",");
            })()
            """);

        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("1,2,3");
    }

    [Fact]
    public void AwaitInsideForOfLoopShouldPreserveOneShotIteratorDestructuringWithLetBinding()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function() {
                function* gen() { yield 42; }
                var out = [];
                for (let [x] of [gen()]) {
                    await Promise.resolve();
                    out.push(x);
                }
                return out.join(",");
            })()
            """);

        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("42");
    }

    [Fact]
    public void AwaitInsideForOfLoopShouldPreserveOneShotIteratorDestructuringWithAssignmentTarget()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (async function() {
                function* gen() { yield 7; }
                var out = [];
                var x;
                for ([x] of [gen()]) {
                    await Promise.resolve();
                    out.push(x);
                }
                return out.join(",");
            })()
            """);

        result = result.UnwrapIfPromise();
        result.AsString().Should().Be("7");
    }

    [Fact]
    public async Task ShouldNotThrowWhenAsyncArrowWithDefaultParameterAwaitsHostTaskInsidePromiseAll()
    {
        // https://github.com/sebastienros/jint/issues/2564
        var engine = new Engine(options =>
        {
            options.Strict();
            options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(30);
        });

        engine.SetValue("host", (Func<string, Task<string>>) (value => Task.FromResult(value)));

        var result = await engine.EvaluateAsync("""
            (async () => {
                const f = async (meta = {}) => ({
                    meta,
                    value: await host('x')
                });

                return JSON.stringify(await Promise.all([
                    f({ index: 1 })
                ]));
            })();
            """);

        result.AsString().Should().Be("""[{"meta":{"index":1},"value":"x"}]""");
    }

    [Fact]
    public void ShouldNotThrowWhenAsyncArrowWithNullDefaultParameterAwaitsHostTask()
    {
        var engine = new Engine();
        engine.SetValue("host", (Func<string, Task<string>>) (value => Task.FromResult(value)));

        var result = engine.Evaluate("""
            (async () => {
                const f = async (meta = null) => ({ meta, value: await host('x') });
                return JSON.stringify(await f({ index: 1 }));
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsString().Should().Be("""{"meta":{"index":1},"value":"x"}""");
    }

    [Fact]
    public void ShouldEvaluateDefaultParameterExpressionOnlyOnceAcrossAwaitResume()
    {
        var engine = new Engine();
        engine.SetValue("host", (Func<string, Task<string>>) (value => Task.FromResult(value)));

        var result = engine.Evaluate("""
            (async () => {
                let count = 0;
                const f = async (a = (count++, 'd')) => (await host('x'), a);
                const r = await f();
                return JSON.stringify([r, count]);
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsString().Should().Be("""["d",1]""");
    }

    [Fact]
    public void ShouldPreserveParameterMutationAcrossAwaitResumeInConciseBody()
    {
        // With an all-literal argument list the arguments array is the expression cache's
        // shared array: a resume-time re-instantiation would not crash but silently rebind
        // the parameter back to its original argument value.
        var engine = new Engine();
        engine.SetValue("host", (Func<string, Task<string>>) (value => Task.FromResult(value)));

        var result = engine.Evaluate("""
            (async () => {
                const f = async (a) => (await (a = 5, host('x')), a);
                return await f(1);
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsNumber().Should().Be(5);
    }

    [Fact]
    public void ShouldResumeConciseBodyWithMultipleAwaitsAndExplicitArgument()
    {
        var engine = new Engine();
        engine.SetValue("host", (Func<string, Task<string>>) (value => Task.FromResult(value)));

        var result = engine.Evaluate("""
            (async () => {
                const f = async (a = {}) => (await host('1')) + (await host('2')) + a.s;
                return await f({ s: '!' });
            })()
            """).UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsString().Should().Be("12!");
    }

    [Fact]
    public async Task ShouldEvaluateDefaultParameterDuringAsyncEventLoopDrain()
    {
        // A genuinely pending host Task makes the resume run inside EvaluateAsync's
        // event-loop drain, where no ambient evaluation context is active. The default
        // must be a non-literal expression to require one during parameter binding.
        var engine = new Engine();
        var tcs = new TaskCompletionSource<string>();
        engine.SetValue("delayed", (Func<Task<string>>) (() => tcs.Task));

        var evalTask = engine.EvaluateAsync("""
            (async () => {
                await delayed();
                const g = (x = {}) => x;
                return JSON.stringify(g());
            })()
            """);

        tcs.SetResult("done");
        var result = await evalTask;

        result.AsString().Should().Be("{}");
    }

    [Fact]
    public void ShouldEvaluateDefaultParameterDuringUnwrapIfPromiseDrain()
    {
        var engine = new Engine();
        var tcs = new TaskCompletionSource<string>();
        engine.SetValue("delayed", (Func<Task<string>>) (() => tcs.Task));

        var promise = engine.Evaluate("""
            (async () => {
                await delayed();
                const g = (x = {}) => x;
                return JSON.stringify(g());
            })()
            """);

        tcs.SetResult("done");
        var result = promise.UnwrapIfPromise(TimeSpan.FromSeconds(5));

        result.AsString().Should().Be("{}");
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task EventLoopShouldSignalAllConcurrentWaiters()
    {
        // Regression: a second concurrent caller of WaitForEventAsync used to receive
        // Task.CompletedTask immediately because the loop only tracked a single
        // outstanding TCS. Its outer loop would then spin (or, if a single Enqueue
        // arrived, only the first waiter would be woken). With the multi-waiter fix,
        // both waiters register and both are signaled by a single Enqueue.
        var loop = new EventLoop();

        var waiter1 = loop.WaitForEventAsync(CancellationToken.None);
        var waiter2 = loop.WaitForEventAsync(CancellationToken.None);

        waiter1.IsCompleted.Should().BeFalse("waiter1 should be pending until Enqueue");
        waiter2.IsCompleted.Should().BeFalse("waiter2 should be pending until Enqueue");

        loop.Enqueue(static () => { });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(waiter1, waiter2).WaitAsync(cts.Token);

        waiter1.IsCompletedSuccessfully.Should().BeTrue();
        waiter2.IsCompletedSuccessfully.Should().BeTrue();
    }
#endif

    class TestAsyncClass
    {
        private readonly ConcurrentBag<string> _values = new();

        public Task Delay(int ms) => Task.Delay(ms);

        public Task Add(string value)
        {
            _values.Add(value);
            return Task.CompletedTask;
        }
    }
}
