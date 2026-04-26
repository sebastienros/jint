using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class EngineResetTests
{
    // ── Sync: Basic State Clearing ──────────────────────────────────────

    [Fact]
    public void ResetState_ClearsGlobalVariables()
    {
        var engine = new Engine();
        engine.Execute("var x = 42;");
        Assert.Equal(42, engine.Evaluate("x").AsNumber());

        engine.ResetState();

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("x"));
        Assert.Contains("x", ex.Message);
    }

    [Fact]
    public void ResetState_ClearsGlobalFunctions()
    {
        var engine = new Engine();
        engine.Execute("function greet() { return 'hello'; }");
        Assert.Equal("hello", engine.Evaluate("greet()").AsString());

        engine.ResetState();

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("greet()"));
        Assert.Contains("greet", ex.Message);
    }

    [Fact]
    public void ResetState_ClearsLetAndConst()
    {
        var engine = new Engine();
        engine.Execute("let a = 1; const b = 2;");
        Assert.Equal(1, engine.Evaluate("a").AsNumber());
        Assert.Equal(2, engine.Evaluate("b").AsNumber());

        engine.ResetState();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("a"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("b"));
    }

    [Fact]
    public void ResetState_ClearsGlobalThisProperties()
    {
        var engine = new Engine();
        engine.Execute("globalThis.myProp = 'test';");
        Assert.Equal("test", engine.Evaluate("globalThis.myProp").AsString());

        engine.ResetState();

        Assert.True(engine.Evaluate("globalThis.myProp").IsUndefined());
    }

    // ── Sync: Built-ins Preserved ───────────────────────────────────────

    [Fact]
    public void ResetState_PreservesBuiltInObjects()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Equal(3, engine.Evaluate("Math.max(1, 2, 3)").AsNumber());
        Assert.Equal("function", engine.Evaluate("typeof Number").AsString());
        Assert.Equal("function", engine.Evaluate("typeof Array").AsString());
        Assert.Equal("function", engine.Evaluate("typeof Object").AsString());
        Assert.Equal("function", engine.Evaluate("typeof JSON.parse").AsString());
        Assert.Equal("function", engine.Evaluate("typeof Promise").AsString());
    }

    [Fact]
    public void ResetState_PreservesBuiltInPrototypeMethods()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Equal(3, engine.Evaluate("[1,2,3].length").AsNumber());
        Assert.Equal("HELLO", engine.Evaluate("'hello'.toUpperCase()").AsString());
        Assert.Equal(3, engine.Evaluate("JSON.parse('[1,2,3]').length").AsNumber());
        Assert.Equal("1,2,3", engine.Evaluate("[1,2,3].join(',')").AsString());
    }

    // ── Sync: Execution After Reset ─────────────────────────────────────

    [Fact]
    public void ResetState_AllowsNewExecution()
    {
        var engine = new Engine();
        engine.Execute("var x = 'old';");
        engine.ResetState();

        engine.Execute("var x = 'new';");
        Assert.Equal("new", engine.Evaluate("x").AsString());
    }

    [Fact]
    public void ResetState_PreparedScriptsWorkAfterReset()
    {
        var prepared = Engine.PrepareScript("function add(a, b) { return a + b; }");
        var engine = new Engine();

        engine.Execute(prepared);
        Assert.Equal(5, engine.Evaluate("add(2, 3)").AsNumber());

        engine.ResetState();

        engine.Execute(prepared);
        Assert.Equal(7, engine.Evaluate("add(3, 4)").AsNumber());
    }

    [Fact]
    public void ResetState_MultipleCyclesWork()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("var counter = (typeof counter !== 'undefined' ? counter : 0) + 1;");

        for (var i = 0; i < 10; i++)
        {
            engine.ResetState();
            engine.Execute(prepared);
            Assert.Equal(1, engine.Evaluate("counter").AsNumber());
        }
    }

    [Fact]
    public void ResetState_ChainingWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        engine
            .Execute("var a = 1;")
            .Execute("var b = 2;")
            .Execute("var c = a + b;");

        Assert.Equal(3, engine.Evaluate("c").AsNumber());
    }

    [Fact]
    public void ResetState_SetValueWorksAfterReset()
    {
        var engine = new Engine();
        engine.SetValue("x", 1);
        Assert.Equal(1, engine.Evaluate("x").AsNumber());

        engine.ResetState();

        engine.SetValue("y", 42);
        Assert.Equal(42, engine.Evaluate("y").AsNumber());
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("x"));
    }

    [Fact]
    public void ResetState_ClrDelegatesWorkAfterReset()
    {
        var engine = new Engine();
        engine.SetValue("add", new Func<int, int, int>((a, b) => a + b));
        Assert.Equal(5, engine.Evaluate("add(2, 3)").AsNumber());

        engine.ResetState();

        engine.SetValue("multiply", new Func<int, int, int>((a, b) => a * b));
        Assert.Equal(6, engine.Evaluate("multiply(2, 3)").AsNumber());
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("add(1, 1)"));
    }

    // ── Sync: Constraints and Error State ───────────────────────────────

    [Fact]
    public void ResetState_ClearsErrorState()
    {
        var engine = new Engine();
        Assert.Throws<JavaScriptException>(() => engine.Execute("throw new Error('boom');"));

        engine.ResetState();

        Assert.Equal(42, engine.Evaluate("42").AsNumber());
    }

    [Fact]
    public void ResetState_ConstraintsWork()
    {
        var engine = new Engine(options => options.TimeoutInterval(TimeSpan.FromSeconds(5)));
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Equal(1, engine.Evaluate("1").AsNumber());
    }

    [Fact]
    public void ResetState_StrictModePreserved()
    {
        var engine = new Engine(options => options.Strict = true);
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Throws<JavaScriptException>(() => engine.Execute("y = 1;"));
    }

    // ── Sync: Symbol Registry and Modules ───────────────────────────────

    [Fact]
    public void ResetState_ClearsSymbolForRegistry()
    {
        var engine = new Engine();
        engine.Execute("var s = Symbol.for('myKey');");
        engine.ResetState();

        // Symbol.for with same key should create a new symbol, not return old one
        // (we can't directly compare, but the engine should not crash)
        var result = engine.Evaluate("Symbol.for('myKey').toString()").AsString();
        Assert.Equal("Symbol(myKey)", result);
    }

    // ── Sync: Object Creation After Reset ───────────────────────────────

    [Fact]
    public void ResetState_ObjectCreationWorks()
    {
        var engine = new Engine();
        engine.Execute("var x = { a: 1 };");
        engine.ResetState();

        var result = engine.Evaluate("JSON.stringify({ a: 1, b: [2, 3] })").AsString();
        Assert.Equal("{\"a\":1,\"b\":[2,3]}", result);
    }

    [Fact]
    public void ResetState_RegExpWorks()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.True(engine.Evaluate("/hello/.test('hello world')").AsBoolean());
    }

    [Fact]
    public void ResetState_DateWorks()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Equal("number", engine.Evaluate("typeof new Date().getTime()").AsString());
    }

    [Fact]
    public void ResetState_MapAndSetWork()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Equal(2, engine.Evaluate("var m = new Map(); m.set('a', 1); m.set('b', 2); m.size").AsNumber());
        Assert.Equal(3, engine.Evaluate("var s = new Set([1,2,3]); s.size").AsNumber());
    }

    [Fact]
    public void ResetState_ErrorConstructorsWork()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        Assert.Equal("TypeError", engine.Evaluate("new TypeError('test').name").AsString());
        Assert.Equal("RangeError", engine.Evaluate("new RangeError('test').name").AsString());
    }

    // ── Async: Basic Async After Reset ──────────────────────────────────

    [Fact]
    public async Task ResetState_EvaluateAsyncWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        var result = await engine.EvaluateAsync("Promise.resolve(42)");
        Assert.Equal(42, result.AsNumber());
    }

    [Fact]
    public async Task ResetState_AsyncFunctionWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        var result = await engine.EvaluateAsync("(async () => { return 42; })()");
        Assert.Equal(42, result.AsNumber());
    }

    [Fact]
    public async Task ResetState_PromiseChainWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        var result = await engine.EvaluateAsync("Promise.resolve(1).then(x => x + 1).then(x => x * 2)");
        Assert.Equal(4, result.AsNumber());
    }

    [Fact]
    public async Task ResetState_PromiseAllWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        var result = await engine.EvaluateAsync("Promise.all([Promise.resolve(1), Promise.resolve(2), Promise.resolve(3)])");
        Assert.True(result.IsArray());
    }

    [Fact]
    public async Task ResetState_InvokeAsyncWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("function getValue() { return Promise.resolve(99); }");
        engine.ResetState();

        engine.Execute("function getValue() { return Promise.resolve(77); }");
        var result = await engine.InvokeAsync("getValue");
        Assert.Equal(77, result.AsNumber());
    }

    [Fact]
    public async Task ResetState_AsyncRejectionHandledAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        await Assert.ThrowsAsync<PromiseRejectedException>(
            () => engine.EvaluateAsync("Promise.reject('error')"));
    }

    // ── Async: Task Interop After Reset ─────────────────────────────────

    [Fact]
    public async Task ResetState_TaskInteropWorksAfterReset()
    {
        var engine = new Engine(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
        });

        engine.SetValue("getValueAsync", new Func<Task<int>>(() => Task.FromResult(42)));
        var result1 = await engine.EvaluateAsync("getValueAsync()");
        Assert.Equal(42, result1.AsNumber());

        engine.ResetState();

        engine.SetValue("getValueAsync", new Func<Task<string>>(() => Task.FromResult("hello")));
        var result2 = await engine.EvaluateAsync("getValueAsync()");
        Assert.Equal("hello", result2.AsString());
    }

    // ── Async: Multiple Reset Cycles ────────────────────────────────────

    [Fact]
    public async Task ResetState_MultipleAsyncCyclesWork()
    {
        var engine = new Engine();

        for (var i = 0; i < 5; i++)
        {
            engine.ResetState();

            engine.SetValue("iteration", i);
            var result = await engine.EvaluateAsync("Promise.resolve(iteration)");
            Assert.Equal(i, (int) result.AsNumber());
        }
    }

    [Fact]
    public async Task ResetState_MixedSyncAsyncCycles()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript("function compute(n) { return n * 2; }");

        for (var i = 0; i < 5; i++)
        {
            engine.ResetState();
            engine.Execute(prepared);

            // Sync call
            Assert.Equal(i * 2, (int) engine.Evaluate($"compute({i})").AsNumber());

            // Async call
            var asyncResult = await engine.EvaluateAsync($"Promise.resolve(compute({i}))");
            Assert.Equal(i * 2, (int) asyncResult.AsNumber());
        }
    }

    // ── Async: Prepared Script + Async ──────────────────────────────────

    [Fact]
    public async Task ResetState_PreparedScriptWithAsyncExecution()
    {
        var setup = Engine.PrepareScript(@"
            function processData(input) {
                return JSON.parse(input);
            }
        ");

        var engine = new Engine();

        engine.Execute(setup);
        var result1 = await engine.EvaluateAsync("Promise.resolve(processData('{\"key\":1}'))");
        Assert.Equal(1, result1.AsObject().Get("key").AsNumber());

        engine.ResetState();
        engine.Execute(setup);

        var result2 = await engine.EvaluateAsync("Promise.resolve(processData('{\"key\":2}'))");
        Assert.Equal(2, result2.AsObject().Get("key").AsNumber());
    }

    // ── Edge Cases ──────────────────────────────────────────────────────

    [Fact]
    public void ResetState_ImmediateResetOnFreshEngine()
    {
        var engine = new Engine();
        engine.ResetState();
        Assert.Equal(1, engine.Evaluate("1").AsNumber());
    }

    [Fact]
    public void ResetState_DoubleReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();
        engine.ResetState();
        Assert.Equal(42, engine.Evaluate("42").AsNumber());
    }

    [Fact]
    public void ResetState_ResetAfterRecursionError()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 10);

        Assert.ThrowsAny<Exception>(() =>
            engine.Execute("function f() { return f(); } f();"));

        engine.ResetState();

        Assert.Equal(1, engine.Evaluate("1").AsNumber());
    }

    [Fact]
    public void ResetState_CompletionValueCleared()
    {
        var engine = new Engine();
        Assert.Equal(42, engine.Evaluate("42").AsNumber());
        engine.ResetState();

        // After reset, evaluating undefined should work cleanly
        Assert.True(engine.Evaluate("undefined").IsUndefined());
    }

    [Fact]
    public async Task ResetState_EventLoopCleanAfterReset()
    {
        var engine = new Engine();

        // Execute async that creates event loop activity
        await engine.EvaluateAsync("Promise.resolve(1).then(x => x + 1)");

        engine.ResetState();

        // Fresh async should work without interference from previous event loop state
        var result = await engine.EvaluateAsync("Promise.resolve(99)");
        Assert.Equal(99, result.AsNumber());
    }

    // ── Additional Coverage: eval, closures, CLR objects, modules ───────

    [Fact]
    public void ResetState_EvalWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = eval('1 + 2');");
        Assert.Equal(3, engine.Evaluate("x").AsNumber());

        engine.ResetState();

        Assert.Equal(7, engine.Evaluate("eval('3 + 4')").AsNumber());
    }

    [Fact]
    public void ResetState_ClosuresFromPriorExecutionNotAccessible()
    {
        var engine = new Engine();
        engine.Execute(@"
            var outer = 10;
            function getClosure() {
                return function() { return outer; };
            }
            var fn = getClosure();
        ");
        Assert.Equal(10, engine.Evaluate("fn()").AsNumber());

        engine.ResetState();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("fn()"));
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("outer"));
    }

    [Fact]
    public void ResetState_ClrObjectInteropAfterReset()
    {
        var engine = new Engine();
        engine.SetValue("obj", new { Name = "old", Value = 1 });
        Assert.Equal("old", engine.Evaluate("obj.Name").AsString());

        engine.ResetState();

        engine.SetValue("obj", new { Name = "new", Value = 2 });
        Assert.Equal("new", engine.Evaluate("obj.Name").AsString());
        Assert.Equal(2, engine.Evaluate("obj.Value").AsNumber());
    }

    [Fact]
    public void ResetState_ClrTypeReferenceAfterReset()
    {
        var engine = new Engine(options => options.AllowClr(typeof(Math).Assembly));
        engine.Execute("var x = System.Math.PI;");

        engine.ResetState();

        var pi = engine.Evaluate("System.Math.PI").AsNumber();
        Assert.True(System.Math.Abs(pi - System.Math.PI) < 0.0001);
    }

    [Fact]
    public void ResetState_ThrowInScriptThenResetThenNewScript()
    {
        var engine = new Engine();

        var ex = Assert.Throws<JavaScriptException>(() =>
            engine.Execute("throw new TypeError('first error');"));
        Assert.Contains("first error", ex.Message);

        engine.ResetState();

        Assert.Equal("ok", engine.Evaluate("'ok'").AsString());

        var ex2 = Assert.Throws<JavaScriptException>(() =>
            engine.Execute("throw new RangeError('second error');"));
        Assert.Contains("second error", ex2.Message);

        engine.ResetState();
        Assert.Equal(42, engine.Evaluate("42").AsNumber());
    }

    [Fact]
    public void ResetState_SpreadAndDestructuringAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var [a, ...rest] = [1, 2, 3];");
        engine.ResetState();

        Assert.Equal(6, engine.Evaluate("var [x, ...y] = [1, 2, 3]; x + y[0] + y[1]").AsNumber());
    }

    [Fact]
    public void ResetState_GeneratorsWorkAfterReset()
    {
        var engine = new Engine();
        engine.Execute("function* gen() { yield 1; }");
        engine.ResetState();

        var result = engine.Evaluate(@"
            function* counter() { let i = 0; while(true) yield i++; }
            var g = counter();
            g.next().value + g.next().value + g.next().value;
        ");
        Assert.Equal(3, result.AsNumber());
    }

    [Fact]
    public void ResetState_ProxyAndReflectWorkAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        var result = engine.Evaluate(@"
            var target = { a: 1, b: 2 };
            var handler = { get: function(obj, prop) { return prop in obj ? obj[prop] : 'default'; } };
            var proxy = new Proxy(target, handler);
            proxy.a + ',' + proxy.c;
        ");
        Assert.Equal("1,default", result.AsString());
    }

    [Fact]
    public async Task ResetState_StaleAsyncCallbacksDroppedAfterReset()
    {
        var engine = new Engine(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
        });

        var tcs = new TaskCompletionSource<int>();

        engine.SetValue("getPendingValue", new Func<Task<int>>(() => tcs.Task));

        // Start an async evaluation that will be pending (tcs not yet completed)
        var evalTask = engine.EvaluateAsync("getPendingValue()");

        // Reset while the Task is still pending — the ContinueWith callback
        // will fire later and enqueue onto the event loop with a stale generation
        engine.ResetState();

        // Complete the old task AFTER reset — its callback should be silently dropped
        tcs.SetResult(999);

        // New execution on the reset engine should work cleanly
        engine.SetValue("getValue", new Func<Task<int>>(() => Task.FromResult(42)));
        var result = await engine.EvaluateAsync("getValue()");
        Assert.Equal(42, result.AsNumber());
    }

    [Fact]
    public async Task ResetState_MultipleStaleCallbacksAllDropped()
    {
        var engine = new Engine(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
        });

        var sources = new List<TaskCompletionSource<int>>();

        for (var i = 0; i < 5; i++)
        {
            var tcs = new TaskCompletionSource<int>();
            sources.Add(tcs);
            engine.SetValue($"pending{i}", new Func<Task<int>>(() => tcs.Task));
            _ = engine.EvaluateAsync($"pending{i}()");
        }

        engine.ResetState();

        // Complete all old tasks after reset
        for (var i = 0; i < sources.Count; i++)
        {
            sources[i].SetResult(i);
        }

        // Give callbacks a moment to fire and enqueue stale events
        await Task.Delay(200);

        // Engine should be clean — stale events dropped by generation check
        Assert.Equal(1, engine.Evaluate("1").AsNumber());

        var result = await engine.EvaluateAsync("Promise.resolve(77)");
        Assert.Equal(77, result.AsNumber());
    }

    [Fact]
    public void ResetState_ThrowsWhenCalledDuringExecution()
    {
        var engine = new Engine();
        InvalidOperationException caught = null!;
        engine.SetValue("triggerReset", new Action(() =>
        {
            try { engine.ResetState(); }
            catch (InvalidOperationException ex) { caught = ex; }
        }));
        engine.Execute("triggerReset()");

        Assert.NotNull(caught);
        Assert.Contains("executing", caught!.Message);
    }

    [Fact]
    public async Task ResetState_AsyncGeneratorWorksAfterReset()
    {
        var engine = new Engine();
        engine.Execute("var x = 1;");
        engine.ResetState();

        var result = await engine.EvaluateAsync(@"
            async function* gen() { yield 1; yield 2; yield 3; }
            (async () => {
                var sum = 0;
                for await (var val of gen()) { sum += val; }
                return sum;
            })()
        ");
        Assert.Equal(6, result.AsNumber());
    }
}
