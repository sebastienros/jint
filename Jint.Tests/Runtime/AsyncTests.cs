using System.Collections.Concurrent;
using Jint.Native;
using Jint.Runtime;
using Jint.Tests.Runtime.TestClasses;

namespace Jint.Tests.Runtime;

public class AsyncTests
{
    [Fact]
    public void AwaitPropagationAgainstPrimitiveValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("(async ()=>await '1')()");
        result = result.UnwrapIfPromise();
        Assert.Equal("1", result);
    }

    [Fact]
    public void ShouldTaskConvertedToPromiseInJS()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        Assert.Equal(2, result);

        static async Task<int> Callable()
        {
            await Task.Delay(10);
            Assert.True(true);
            return 1;
        }
    }

    [Fact]
    public void ShouldReturnedTaskConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnDelayedTaskAsync().then(x=>x)");
        result = result.UnwrapIfPromise();
        Assert.Equal(AsyncTestClass.TestString, result);
    }

    [Fact(Skip = "Flaky test")]
    public void ShouldRespectCustomProvidedTimeoutWhenUnwrapping()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnDelayedTaskAsync().then(x=>x)");
        var timeout = TimeSpan.FromMilliseconds(1);
        var exception = Assert.Throws<PromiseRejectedException>(() => result.UnwrapIfPromise(timeout));
        Assert.Equal($"Promise was rejected with value Timeout of {timeout} reached", exception.Message);
    }

    [Fact]
    public void ShouldAwaitUnwrapPromiseWithCustomTimeout()
    {
        Engine engine = new(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500); });
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute(""" 
        async function test() {
            return await asyncTestClass.ReturnDelayedTaskAsync();
        }
        """);
        var result = engine.Invoke("test").UnwrapIfPromise();
        Assert.Equal(AsyncTestClass.TestString, result);
    }

    [Fact]
    public void ShouldReturnedCompletedTaskConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnCompletedTask().then(x=>x)");
        result = result.UnwrapIfPromise();
        Assert.Equal(AsyncTestClass.TestString, result);
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

        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());
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
        engine.SetValue("assert", new Action<bool>(Assert.True));

        engine.Evaluate("asyncTestClass.ReturnCancelledTask(token).then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();

        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());
    }

    [Fact]
    public void ShouldTaskCatchWhenThrowError()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));

        engine.Evaluate("callable().then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());

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
        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());
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

        Assert.Equal("123", asyncTestClass.StringToAppend);
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

        Assert.Equal("Hello World", result);
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

        Assert.Equal("Hello World", result);
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

        Assert.Equal("success", result);
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
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("async function hello() {return await asyncTestMethod(async () =>{ await asyncWork(); })} hello();");
        result = result.UnwrapIfPromise(TimeSpan.FromSeconds(30));
        Assert.Equal("Hello World", result);
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
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("async function hello() {return await asyncTestMethod(async () =>{ return await asyncWork(); })} hello();");
        result = result.UnwrapIfPromise(TimeSpan.FromSeconds(30));
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ShouldValueTaskConvertedToPromiseInJS()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        Assert.Equal(2, result);

        static async ValueTask<int> Callable()
        {
            await Task.Delay(10);
            Assert.True(true);
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
        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());
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
        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());

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
        Assert.Equal("12", log);
    }

    [Fact]
    public void ShouldReturnedValueTaskOfTConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnDelayedValueTaskAsync().then(x=>x)");
        result = result.UnwrapIfPromise();
        Assert.Equal(AsyncTestClass.TestString, result);
    }

    [Fact]
    public void ShouldReturnedCompletedValueTaskOfTConvertedToPromiseInJS()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        var result = engine.Evaluate("asyncTestClass.ReturnCompletedValueTask().then(x=>x)");
        result = result.UnwrapIfPromise();
        Assert.Equal(AsyncTestClass.TestString, result);
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

        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());
    }

    [Fact]
    public void ShouldReturnedValueTaskOfTCatchWhenThrowError()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);

        engine.SetValue("cancelled", JsValue.Undefined);
        engine.SetValue("asyncTestClass", new AsyncTestClass());

        engine.Evaluate("asyncTestClass.ThrowAfterDelayValueTaskAsync().then(_ => cancelled = false).catch(_ => cancelled = true)").UnwrapIfPromise();
        Assert.Equal(true, engine.Evaluate("cancelled").AsBoolean());
    }

    [Fact]
    public void ShouldAwaitUnwrapValueTaskOfTPromiseWithCustomTimeout()
    {
        Engine engine = new(options => { options.ExperimentalFeatures = ExperimentalFeature.TaskInterop; options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500); });
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.Execute("""
        async function test() {
            return await asyncTestClass.ReturnDelayedValueTaskAsync();
        }
        """);
        var result = engine.Invoke("test").UnwrapIfPromise();
        Assert.Equal(AsyncTestClass.TestString, result);
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
        Assert.Equal(AsyncTestClass.TestString, result);
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

        Assert.Equal(expected, log.Select(x => x.AsString()).ToArray());
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
        Assert.Equal(2, log.Count);
        Assert.Equal("Promise!", log[0]);
        Assert.Equal("Resolved!", log[1]);
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
        Assert.Equal(1, val.UnwrapIfPromise().AsInteger());
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
                        Assert.Equal(1, val.AsInteger());

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
        Assert.Equal("f() called - ", x);
        Assert.False(callbackExecuted, "Promise callback should not have executed yet");
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
        Assert.Equal(3, (int) callbacks.Length);

        var log = engine.Evaluate("log").AsArray();
        var logStrings = log.Select(x => x.AsString()).ToArray();

        // Verify all wrapped/simple tasks started and registered before any completed
        Assert.Contains("WrappedAsync A start", logStrings);
        Assert.Contains("WrappedAsync B start", logStrings);
        Assert.Contains("WrappedAsync C start", logStrings);
        Assert.Contains("SimpleAsync A start", logStrings);
        Assert.Contains("SimpleAsync B start", logStrings);
        Assert.Contains("SimpleAsync C start", logStrings);
        Assert.Contains("AsyncOp A registered", logStrings);
        Assert.Contains("AsyncOp B registered", logStrings);
        Assert.Contains("AsyncOp C registered", logStrings);

        // None should have completed yet (no "got result" messages)
        Assert.DoesNotContain(logStrings, s => s.Contains("got result"));
    }

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
