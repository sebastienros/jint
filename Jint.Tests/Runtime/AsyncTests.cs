using System.Collections.Concurrent;
using Jint.Native;
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
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable(token).then(_ => assert(false)).catch(_ => assert(true))");
        result = result.UnwrapIfPromise();
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
        engine.SetValue("token", cancel.Token);
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("asyncTestClass.ReturnCancelledTask(token).then(_ => assert(false)).catch(_ => assert(true))");
        result = result.UnwrapIfPromise();
    }

    [Fact]
    public void ShouldTaskCatchWhenThrowError()
    {
        Engine engine = new(options => options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable().then(_ => assert(false)).catch(_ => assert(true))");

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
        engine.SetValue("asyncTestClass", new AsyncTestClass());
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("asyncTestClass.ThrowAfterDelayAsync().then(_ => assert(false)).catch(_ => assert(true))");
        result = result.UnwrapIfPromise();
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

        engine.Execute("async function hello() {await myAsyncMethod();mySyncMethod2();await asyncTestClass.AddToStringDelayedAsync(\"3\")} hello();");
        Assert.Equal("123", asyncTestClass.StringToAppend);
    }

#if NETFRAMEWORK == false
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
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable(token).then(_ => assert(false)).catch(_ => assert(true))");
        result = result.UnwrapIfPromise();
        static async ValueTask Callable(CancellationToken token)
        {
            await ValueTask.FromCanceled(token);
        }
    }

    [Fact]
    public void ShouldValueTaskCatchWhenThrowError()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable().then(_ => assert(false)).catch(_ => assert(true))");

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
        engine.Execute("async function hello() {await myAsyncMethod();myAsyncMethod2();} hello();");
        Assert.Equal("12", log);
    }
#endif

    [Fact(Skip = "TODO es6-await https://github.com/sebastienros/jint/issues/1385")]
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

    [Fact]
    public async Task ShouldEventLoopBeThreadSafeWhenCalledConcurrently()
    {
        const int ParallelCount = 1000;

        // [NOTE] perform 5 runs since the bug does not always happen
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
                                await testObj.Delay(100);
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
                        tasks[taskIdx].SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tasks[taskIdx].SetException(ex);
                    }
                }, creationOptions: TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(tasks.Select(t => t.Task));
            await Task.Delay(100);
        }
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
