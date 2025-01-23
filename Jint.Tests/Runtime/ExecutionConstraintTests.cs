using Jint.Native.Function;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ExecutionConstraintTests
{
    [Fact]
    public void ShouldThrowStatementCountOverflow()
    {
        Assert.Throws<StatementsCountOverflowException>(
            () => new Engine(cfg => cfg.MaxStatements(100)).Evaluate("while(true);")
        );
    }

    [Fact]
    public void ShouldCountStatementsPrecisely()
    {
        var script = "var x = 0; x++; x + 5";

        // Should not throw if MaxStatements is not exceeded.
        new Engine(cfg => cfg.MaxStatements(3)).Execute(script);

        // Should throw if MaxStatements is exceeded.
        Assert.Throws<StatementsCountOverflowException>(
            () => new Engine(cfg => cfg.MaxStatements(2)).Evaluate(script)
        );
    }

    [Fact]
    public void ShouldThrowMemoryLimitExceeded()
    {
        Assert.Throws<MemoryLimitExceededException>(
            () => new Engine(cfg => cfg.LimitMemory(2048)).Evaluate("a=[]; while(true){ a.push(0); }")
        );
    }

    [Fact]
    public void ShouldThrowTimeout()
    {
        Assert.Throws<TimeoutException>(
            () => new Engine(cfg => cfg.TimeoutInterval(new TimeSpan(0, 0, 0, 0, 500))).Evaluate("while(true);")
        );
    }

    [Fact]
    public void ShouldThrowExecutionCanceled()
    {
        Assert.Throws<ExecutionCanceledException>(
            () =>
            {
                using (var cts = new CancellationTokenSource())
                using (var waitHandle = new ManualResetEvent(false))
                using (var cancellationTokenSet = new ManualResetEvent(initialState: false))
                {
                    var engine = new Engine(cfg => cfg.CancellationToken(cts.Token));

                    /*
                     * To ensure that the action "threadPoolAction" has actually been called by the ThreadPool
                     * (which can happen very late, depending on the ThreadPool usage, etc.), the
                     * "cancellationTokenSet" event will be an indicator for that.
                     *
                     * 1. The "cancellationTokenSet" will be set after the "cts" has been cancelled.
                     * 2. Within the JS, the "test" code will only start as soon as the "cancellationTokenSet"
                     *    event will be set.
                     * 3. The cancellationToken and its source has not been used on purpose for this test to
                     *    not mix "test infrastructure" and "test code".
                     * 4. To verify that this test still works under heavy load, you can add
                     *    a "Thread.Sleep(10000)" call anywhere within the "threadPoolAction" action.
                     */

                    WaitCallback threadPoolAction = _ =>
                    {
                        waitHandle.WaitOne();
                        cts.Cancel();
                        cancellationTokenSet.Set();
                    };
                    ThreadPool.QueueUserWorkItem(threadPoolAction);

                    engine.SetValue("waitHandle", waitHandle);
                    engine.SetValue("waitForTokenToBeSet", new Action(() => cancellationTokenSet.WaitOne()));
                    engine.SetValue("mustNotBeCalled", new Action(() =>
                        throw new InvalidOperationException("A cancellation of the script execution has been requested, but the script did continue to run.")));
                    engine.Evaluate(@"
                            function sleep(millisecondsTimeout) {
                                var totalMilliseconds = new Date().getTime() + millisecondsTimeout;

                                while (new Date() < totalMilliseconds) { }
                            }

                            sleep(100);
                            waitHandle.Set();
                            waitForTokenToBeSet();

                            // Now it is ensured that the cancellationToken has been cancelled. 
                            // The following JS code execution should get aborted by the engine.
                            sleep(1000);
                            sleep(1000);
                            sleep(1000);
                            sleep(1000);
                            sleep(1000);
                            mustNotBeCalled();
                        ");
                }
            }
        );
    }

    [Fact]
    public void CanDiscardRecursion()
    {
        var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(500);
            ";

        Assert.Throws<RecursionDepthOverflowException>(
            () => new Engine(cfg => cfg.LimitRecursion()).Execute(script)
        );
    }

    [Fact]
    public void ShouldDiscardHiddenRecursion()
    {
        var script = @"var renamedFunc;
            var exec = function(callback) {
                renamedFunc = callback;
                callback();
            };

            var result = exec(function() {
                renamedFunc();
            });
            ";

        Assert.Throws<RecursionDepthOverflowException>(
            () => new Engine(cfg => cfg.LimitRecursion()).Execute(script)
        );
    }

    [Fact]
    public void ShouldRecognizeAndDiscardChainedRecursion()
    {
        var script = @" var funcRoot, funcA, funcB, funcC, funcD;

            var funcRoot = function() {
                funcA();
            };

            var funcA = function() {
                funcB();
            };

            var funcB = function() {
                funcC();
            };

            var funcC = function() {
                funcD();
            };

            var funcD = function() {
                funcRoot();
            };

            funcRoot();
            ";

        Assert.Throws<RecursionDepthOverflowException>(
            () => new Engine(cfg => cfg.LimitRecursion()).Execute(script)
        );
    }

    [Fact]
    public void ShouldProvideCallChainWhenDiscardRecursion()
    {
        var script = @" var funcRoot, funcA, funcB, funcC, funcD;

            var funcRoot = function() {
                funcA();
            };

            var funcA = function() {
                funcB();
            };

            var funcB = function() {
                funcC();
            };

            var funcC = function() {
                funcD();
            };

            var funcD = function() {
                funcRoot();
            };

            funcRoot();
            ";

        RecursionDepthOverflowException exception = null;

        try
        {
            new Engine(cfg => cfg.LimitRecursion()).Execute(script);
        }
        catch (RecursionDepthOverflowException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.Equal("funcRoot->funcA->funcB->funcC->funcD", exception.CallChain);
        Assert.Equal("funcRoot", exception.CallExpressionReference);
    }

    [Fact]
    public void ShouldAllowShallowRecursion()
    {
        var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(8);
            ";

        new Engine(cfg => cfg.LimitRecursion(20)).Execute(script);
    }

    [Fact]
    public void ShouldDiscardDeepRecursion()
    {
        var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(38);
            ";

        Assert.Throws<RecursionDepthOverflowException>(
            () => new Engine(cfg => cfg.LimitRecursion(20)).Execute(script)
        );
    }

    [Fact]
    public void ShouldAllowRecursionLimitWithoutReferencedName()
    {
        const string input = @"(function () {
                var factorial = function(n) {
                    if (n>1) {
                        return n * factorial(n - 1);
                    }
                };

                var result = factorial(38);
            })();
            ";

        var engine = new Engine(o => o.LimitRecursion(20));
        Assert.Throws<RecursionDepthOverflowException>(() => engine.Execute(input));
    }

    [Fact]
    public void ShouldLimitRecursionWithAllFunctionInstances()
    {
        var engine = new Engine(cfg =>
        {
            // Limit recursion to 5 invocations
            cfg.LimitRecursion(5);
            cfg.Strict();
        });

        var ex = Assert.Throws<RecursionDepthOverflowException>(() => engine.Evaluate(@"
var myarr = new Array(5000);
for (var i = 0; i < myarr.length; i++) {
    myarr[i] = function(i) {
        myarr[i + 1](i + 1);
    }
}

myarr[0](0);
"));
    }

    [Fact]
    public void ShouldLimitRecursionWithGetters()
    {
        const string code = @"var obj = { get test() { return this.test + '2';  } }; obj.test;";
        var engine = new Engine(cfg => cfg.LimitRecursion(10));

        Assert.Throws<RecursionDepthOverflowException>(() => engine.Evaluate(code));
    }

    [Fact]
    public void ShouldLimitArraySizeForConcat()
    {
        var engine = new Engine(o => o.MaxStatements(1_000).MaxArraySize(1_000_000));
        Assert.Throws<MemoryLimitExceededException>(() => engine.Evaluate("for (let a = [1, 2, 3];; a = a.concat(a)) ;"));
    }

    [Fact]
    public void ShouldLimitArraySizeForFill()
    {
        var engine = new Engine(o => o.MaxStatements(1_000).MaxArraySize(1_000_000));
        Assert.Throws<MemoryLimitExceededException>(() => engine.Evaluate("var arr = Array(1000000000).fill(new Array(1000000000));"));
    }

    [Fact]
    public void ShouldLimitArraySizeForJoin()
    {
        var engine = new Engine(o => o.MaxStatements(1_000).MaxArraySize(1_000_000));
        Assert.Throws<MemoryLimitExceededException>(() => engine.Evaluate("new Array(2147483647).join('*')"));
    }

    [Fact]
    public void ShouldConsiderConstraintsWhenCallingInvoke()
    {
        var engine = new Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromMilliseconds(100));
        });
        var myApi = new MyApi();

        engine.SetValue("myApi", myApi);
        engine.Execute("myApi.addEventListener('DataReceived', (data) => { myApi.log(data) })");

        var dataReceivedCallbacks = myApi.Callbacks.Where(kvp => kvp.Key == "DataReceived");
        foreach (var callback in dataReceivedCallbacks)
        {
            engine.Invoke(callback.Value, "Data Received #1");
            Thread.Sleep(101);
            engine.Invoke(callback.Value, "Data Received #2");
        }
    }

    [Fact]
    public void ResetConstraints()
    {
        void ExecuteAction(Engine engine) => engine.Execute("recursion()");
        void InvokeAction(Engine engine) => engine.Invoke("recursion");

        List<int> expected = [6, 6, 6, 6, 6];
        Assert.Equal(expected, RunLoop(CreateEngine(), ExecuteAction));
        Assert.Equal(expected, RunLoop(CreateEngine(), InvokeAction));

        var e1 = CreateEngine();
        Assert.Equal(expected, RunLoop(e1, ExecuteAction));
        Assert.Equal(expected, RunLoop(e1, InvokeAction));

        var e2 = CreateEngine();
        Assert.Equal(expected, RunLoop(e2, InvokeAction));
        Assert.Equal(expected, RunLoop(e2, ExecuteAction));

        var e3 = CreateEngine();
        Assert.Equal(expected, RunLoop(e3, InvokeAction));
        Assert.Equal(expected, RunLoop(e3, ExecuteAction));
        Assert.Equal(expected, RunLoop(e3, InvokeAction));

        var e4 = CreateEngine();
        Assert.Equal(expected, RunLoop(e4, InvokeAction));
        Assert.Equal(expected, RunLoop(e4, InvokeAction));
    }

    private static Engine CreateEngine()
    {
        Engine engine = new(options => options.LimitRecursion(5));
        return engine.Execute("""
                                  var num = 0;
                                  function recursion() {
                                      num++;
                                      recursion(num);
                                  }
                              """);
    }

    private static List<int> RunLoop(Engine engine, Action<Engine> engineAction)
    {
        List<int> results = new();
        for (var i = 0; i < 5; i++)
        {
            try
            {
                engine.SetValue("num", 0);
                engineAction.Invoke(engine);
            }
            catch (RecursionDepthOverflowException)
            {
                results.Add((int) engine.GetValue("num").AsNumber());
            }
        }

        return results;
    }

    private class MyApi
    {
        public readonly Dictionary<string, ScriptFunction> Callbacks = new Dictionary<string, ScriptFunction>();

        public void AddEventListener(string eventName, ScriptFunction callback)
        {
            Callbacks.Add(eventName, callback);
        }

        public void Log(string logMessage)
        {
            Console.WriteLine(logMessage);
        }
    }
}
