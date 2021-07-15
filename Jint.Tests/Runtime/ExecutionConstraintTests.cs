using System;
using System.Threading;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
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
                    using (var tcs = new CancellationTokenSource())
                    using (var waitHandle = new ManualResetEvent(false))
                    {
                        var engine = new Engine(cfg => cfg.CancellationToken(tcs.Token));

                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            waitHandle.WaitOne();
                            tcs.Cancel();
                        });

                        engine.SetValue("waitHandle", waitHandle);
                        engine.Evaluate(@"
                            function sleep(millisecondsTimeout) {
                                var totalMilliseconds = new Date().getTime() + millisecondsTimeout;

                                while (new Date() < totalMilliseconds) { }
                            }

                            sleep(100);
                            waitHandle.Set();
                            sleep(5000);
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
    }
}