using Jint.Constraints;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ExecutionConstraintTests
{
    [Test]
    public void ShouldThrowStatementCountOverflow()
    {
        Invoking(() => new Engine(cfg => cfg.LimitStatements(100)).Evaluate("while(true);")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void ShouldCountStatementsPrecisely()
    {
        var script = "var x = 0; x++; x + 5";

        // Should not throw if MaxStatements is not exceeded.
        new Engine(cfg => cfg.LimitStatements(3)).Execute(script);

        // Should throw if MaxStatements is exceeded.
        Invoking(() => new Engine(cfg => cfg.LimitStatements(2)).Evaluate(script)).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void ShouldCountStatementsInsideFunctionLocalLoop()
    {
        // The function-local expression-body for loop is the shape that arms the interpreter's
        // tight for-body lane; MaxStatements counts statements (its call frequency IS its
        // semantics), so it must keep the loop on the per-statement path and trip precisely.
        var engine = new Engine(cfg => cfg.LimitStatements(1_000));
        Invoking(() => engine.Evaluate("function f() { var x = 0; for (var i = 0; i < 100000; i++) { x += 1; } return x; } f();")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void UserDefinedConstraintIsCheckedOncePerStatement()
    {
        // Per-statement call frequency is part of the Constraint contract for user-derived
        // implementations; amortizing them would be a silent behavior break. Expected counts:
        // JintStatement.Execute checks once per non-block statement; the top-level program list
        // itself does not check (JintStatementList only checks when it represents a function
        // body or block statement node).
        var constraint = new CountingConstraint();
        var engine = new Engine(cfg => cfg.AddConstraint(constraint));

        engine.Evaluate("var x = 0; x++; x + 5");
        constraint.CheckCount.Should().Be(3);

        // one more top-level statement -> exactly one more check
        constraint.ResetCount();
        engine.Evaluate("var x = 0; x++; x++; x + 5");
        constraint.CheckCount.Should().Be(4);
    }

    [Test]
    public void UserDefinedConstraintIsCheckedPerStatementInsideFunctionLocalLoop()
    {
        // A user-derived constraint must also keep the tight for-body lane disarmed: every body
        // statement of every iteration goes through the per-statement checks.
        var constraint = new CountingConstraint();
        var engine = new Engine(cfg => cfg.AddConstraint(constraint));

        engine.Evaluate("function f() { var x = 0; for (var i = 0; i < 1000; i++) { x += 1; } return x; } f();");
        constraint.CheckCount.Should().BeGreaterThanOrEqualTo(1000, $"expected at least one check per loop-body statement, got {constraint.CheckCount}");
    }

    private sealed class CountingConstraint : Constraint
    {
        public int CheckCount { get; private set; }

        public void ResetCount() => CheckCount = 0;

        public override void Check() => CheckCount++;

        public override void Reset()
        {
        }
    }

    [Test]
    public void ShouldThrowMemoryLimitExceeded()
    {
        Invoking(() => new Engine(cfg => cfg.LimitMemory(2048)).Evaluate("a=[]; while(true){ a.push(0); }")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    private sealed class ThrowOnResetAfterCheckConstraint : Constraint
    {
        private bool _checked;

        public override void Check() => _checked = true;

        public override void Reset()
        {
            if (_checked)
            {
                throw new PlatformNotSupportedException("the constraint's own reset failed");
            }
        }
    }

    [Test]
    public void AConstraintResetThatThrowsOnTheWayOutReplacesTheScriptsOwnError()
    {
        // This is why a Reset() has to be total. ExecuteWithConstraints resets the constraints from a
        // finally, so an exception raised there unwinds in place of the one the run was already carrying:
        // the script's error never reaches the host, and what does reach it names the reset instead.
        var engine = new Engine(cfg => cfg.AddConstraint(new ThrowOnResetAfterCheckConstraint()));

        Invoking(() => engine.Evaluate("throw new Error('the script error');"))
            .Should().ThrowExactly<PlatformNotSupportedException>();
    }

    [Test]
    public void MemoryLimitConstraintResetIsTotal()
    {
        // Reset is called from ExecuteWithConstraints' finally, so it must remain total. The memory
        // constraint establishes its per-thread segment baseline only when the next engine entry begins;
        // resetting and checking an idle constraint therefore does not touch the runtime counter.
        var constraint = new MemoryLimitConstraint(4_000_000);

        Invoking(constraint.Reset).Should().NotThrow();
        Invoking(constraint.Reset).Should().NotThrow();
        Invoking(constraint.Check).Should().NotThrow();
    }

    [Test]
    public void AllocatedBytesForCurrentThreadIsReachableOnEveryExecutingTargetFramework()
    {
        // net472 resolves GC.GetAllocatedBytesForCurrentThread by reflection with the default binding
        // flags, which see public members only; net10.0 calls it outright. Both have to answer, or
        // LimitMemory is a no-op that reports itself as an unsupported platform.
        GCPolyfills.TryGetAllocatedBytesForCurrentThread(out var allocatedBytes).Should().BeTrue();
        allocatedBytes.Should().BePositive();
    }

    [Test]
    public void ShouldThrowTimeout()
    {
        Invoking(() => new Engine(cfg => cfg.LimitExecutionTime(new TimeSpan(0, 0, 0, 0, 500))).Evaluate("while(true);")).Should().ThrowExactly<TimeoutException>();
    }

    [Test]
    public void TimeoutIsNotObservedBeforeAnyExecutionStarts()
    {
        // The constraint is armed by Reset, which runs when a top-level execution begins. Probing
        // it before that must not fail, however long the engine has been sitting idle.
        var engine = new Engine(cfg => cfg.LimitExecutionTime(TimeSpan.FromMilliseconds(1)));
        Thread.Sleep(50);
        Invoking(() => engine.Constraints.Check()).Should().NotThrow();
    }

    [Test]
    public void TimeoutIsRearmedForEachTopLevelExecution()
    {
        // Each execution gets the full interval, so a sequence of short scripts separated by pauses
        // longer than the timeout must all succeed.
        var engine = new Engine(cfg => cfg.LimitExecutionTime(TimeSpan.FromMilliseconds(200)));
        for (var i = 0; i < 3; i++)
        {
            engine.Evaluate("var x = 1 + 1;");
            Thread.Sleep(250);
        }

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Test]
    public void TimeoutIsObservedWithoutWaitingForATimerCallback()
    {
        // The deadline is compared inline, so once the interval has elapsed the very next check
        // fails - detection does not wait on a thread-pool timer callback. The engine is kept out
        // of the picture between arming and checking so nothing but elapsed time can trip it.
        var engine = new Engine(cfg => cfg.LimitExecutionTime(TimeSpan.FromMilliseconds(20)));
        engine.Constraints.Reset();
        Thread.Sleep(60);
        Invoking(() => engine.Constraints.Check()).Should().ThrowExactly<TimeoutException>();
    }

    [Test]
    public void ShouldThrowTimeoutInsideFunctionLocalTightLoop()
    {
        // The function-local expression-body for loop is the shape that arms the interpreter's
        // tight for-body lane; a timeout is amortized (checked every N statements/iterations),
        // and must still fire inside the loop.
        var engine = new Engine(cfg => cfg.LimitExecutionTime(new TimeSpan(0, 0, 0, 0, 500)));
        Invoking(() => engine.Evaluate("function f() { var x = 0; for (var i = 0; i < 1; i += 0) { x += 1; } return x; } f();")).Should().ThrowExactly<TimeoutException>();
    }

    [Test]
    public void ShouldThrowMemoryLimitExceededInsideFunctionLocalTightLoop()
    {
        // Memory accounting is exact and must interrupt an allocating function-local loop.
        var engine = new Engine(cfg => cfg.LimitMemory(2_000_000));
        Invoking(() => engine.Evaluate("function f() { var s = ''; for (var i = 0; i < 1; i += 0) { s += 'aaaaaaaaaaaaaaaa'; } return s; } f();")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void TimeoutConstraintDoesNotChangeFunctionLocalLoopResults()
    {
        // With only amortized constraints registered the tight for-body lane stays armed;
        // results must be identical to an unconstrained engine's.
        const string script = "function f() { var s = 0; for (var i = 0; i < 100000; i++) { s += 2; } return s; } f();";
        var unconstrained = new Engine().Evaluate(script).AsNumber();
        var constrained = new Engine(cfg => cfg.LimitExecutionTime(TimeSpan.FromSeconds(30))).Evaluate(script).AsNumber();
        constrained.Should().Be(unconstrained);
        constrained.Should().Be(200_000);
    }

    [Test]
    public void OperationDeadlineConstraintIsRegisteredAsAnInstanceAndSurvivesThePerEntryReset()
    {
        // Unlike the built-in timeout there is no options extension method for this one: the host has to
        // keep the reference, because only the host knows where its operation begins and ends. The engine
        // still resets it before and after every entry — and it declines, which is the point.
        var deadline = new OperationDeadlineConstraint();
        var engine = new Engine(cfg => cfg.AddConstraint(deadline));

        engine.Constraints.Find<OperationDeadlineConstraint>().Should().BeSameAs(deadline);

        // disarmed: bounds nothing
        engine.Evaluate("var x = 1 + 1;");

        deadline.Begin(TimeSpan.FromMilliseconds(200));
        try
        {
            Invoking(() => engine.Evaluate("while (true);")).Should().ThrowExactly<TimeoutException>();
        }
        finally
        {
            deadline.End();
        }

        Invoking(() => engine.Evaluate("var y = 1 + 1;")).Should().NotThrow("End() disarms the constraint again");
    }

    [Test]
    public void ShouldThrowExecutionCanceled()
    {
        Invoking(() =>
            {
                using (var cts = new CancellationTokenSource())
                using (var waitHandle = new ManualResetEvent(false))
                using (var cancellationTokenSet = new ManualResetEvent(initialState: false))
                {
                    var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));

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
            }).Should().ThrowExactly<ExecutionCanceledException>();
    }

    [Test]
    public void CanDiscardRecursion()
    {
        var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(500);
            ";

        Invoking(() => new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 0).Execute(script)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Test]
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

        Invoking(() => new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 0).Execute(script)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Test]
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

        Invoking(() => new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 0).Execute(script)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Test]
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
            new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 0).Execute(script);
        }
        catch (RecursionDepthOverflowException ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception.CallChain.Should().Be("funcRoot->funcA->funcB->funcC->funcD");
        exception.CallExpressionReference.Should().Be("funcRoot");
    }

    [Test]
    public void ShouldAllowShallowRecursion()
    {
        var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(8);
            ";

        new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 20).Execute(script);
    }

    [Test]
    public void ShouldDiscardDeepRecursion()
    {
        var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(38);
            ";

        Invoking(() => new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 20).Execute(script)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Test]
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

        var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 20);
        Invoking(() => engine.Execute(input)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Test]
    public void ShouldLimitRecursionWithAllFunctionInstances()
    {
        var engine = new Engine(cfg =>
        {
            // Limit recursion to 5 invocations
            cfg.Constraints.MaxRecursionDepth = 5;
            cfg.Strict = true;
        });

        var ex = Invoking(() => engine.Evaluate(@"
var myarr = new Array(5000);
for (var i = 0; i < myarr.length; i++) {
    myarr[i] = function(i) {
        myarr[i + 1](i + 1);
    }
}

myarr[0](0);
")).Should().ThrowExactly<RecursionDepthOverflowException>().Which;
    }

    [Test]
    public void ShouldLimitRecursionWithGetters()
    {
        const string code = @"var obj = { get test() { return this.test + '2';  } }; obj.test;";
        var engine = new Engine(cfg => cfg.Constraints.MaxRecursionDepth = 10);

        Invoking(() => engine.Evaluate(code)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    // A constraint that fires deep in a recursion has to survive its own unwind. Every one of these
    // runs on a small dedicated stack, because that is the only place the failure shows: an exception
    // caught and rethrown at each interpreter level costs a nested exception dispatch per level, and
    // enough of those overflow the stack on the way out and take the host process down with them
    // (0xC00000FD, uncatchable). The 16 MB DedicatedThread default hides all of it, which is why CI
    // never saw it. Every depth below sits well above the pre-fix ceiling (~100 JavaScript frames on
    // a 1 MB stack) and well below the plain forward-recursion ceiling (~700), so a regression fails
    // the run rather than merely reporting a smaller number.
    private const int SmallStack = 1024 * 1024;

    [Test]
    public void RecursionLimitFiringAtTwoHundredUnwindsToTheHost()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 200);
                Invoking(() => engine.Execute("function f() { return f(); } f();"))
                    .Should().ThrowExactly<RecursionDepthOverflowException>();
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void RecursionLimitFiringAtAThousandUnwindsToTheHost()
    {
        // 4 MB: a thousand frames do not fit forward on 1 MB. The pre-fix unwind ceiling scales with
        // the stack too (~400 frames here), so a thousand still fails without the fix.
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 1000);
                Invoking(() => engine.Execute("function f() { return f(); } f();"))
                    .Should().ThrowExactly<RecursionDepthOverflowException>();
            },
            maxStackSize: 4 * 1024 * 1024);
    }

    [Test]
    public void EngineStaysUsableAfterARecursionLimitUnwind()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 300);
                engine.Execute("function f() { return f(); }");
                engine.Execute("function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }");

                for (var round = 0; round < 3; round++)
                {
                    Invoking(() => engine.Evaluate("f()")).Should().ThrowExactly<RecursionDepthOverflowException>();

                    // The frames the unwind passed through are what these assert: the call stack was
                    // popped back to nothing, so a 20-deep call is not measured against a stale depth.
                    engine.CallStack.Count.Should().Be(0);
                    engine.Evaluate("fib(20)").AsNumber().Should().Be(6765);

                    // A host constraint is not a JavaScript error, so a script-level catch never sees it
                    // — the same reason the mapping switch has no arm for it. Declining to catch changed
                    // where the frames go, not that.
                    Invoking(() => engine.Evaluate("(function () { try { f(); return 'no-throw'; } catch (e) { return 'caught'; } })()"))
                        .Should().ThrowExactly<RecursionDepthOverflowException>();
                    engine.CallStack.Count.Should().Be(0);
                }
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void CancellationRaisedDeepInARecursionUnwindsToTheHost()
    {
        DedicatedThread.Run(
            () =>
            {
                using var cts = new CancellationTokenSource();
                var engine = new Engine(o => o.ObserveCancellation(cts.Token));
                engine.SetValue("stop", new Action(() => cts.Cancel()));
                engine.Execute("function f(n) { if (n <= 0) { stop(); var s = 0; for (var i = 0; i < 500; i++) { s += i; } return s; } return f(n - 1); }");

                Invoking(() => engine.Evaluate("f(300)")).Should().ThrowExactly<ExecutionCanceledException>();
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void StatementBudgetExhaustedDeepInARecursionUnwindsToTheHost()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(o => o.LimitStatements(100_000));
                engine.Execute("function f(n) { if (n <= 0) { var s = 0; for (var i = 0; i < 200000; i++) { s += i; } return s; } return f(n - 1); }");

                Invoking(() => engine.Evaluate("f(300)")).Should().ThrowExactly<StatementsCountOverflowException>();
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void ClrExceptionFromHostCodeUnwindsADeepRecursionAndKeepsItsJavaScriptLocation()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine();
                engine.SetValue("bang", new Action(() => throw new InvalidOperationException("bang")));
                engine.Execute("function f(n) { if (n <= 0) { bang(); } return f(n - 1); }");

                var thrown = Invoking(() => engine.Evaluate("f(300)"))
                    .Should().ThrowExactly<InvalidOperationException>().Which;

                // The decoration now happens at the innermost interpreter frame only — every frame above
                // it declines to catch — so what a host reads back has to be unchanged.
                JintException.TryGetJavaScriptLocation(thrown, out _).Should().BeTrue();
                JintException.TryGetJavaScriptCallStack(thrown, out var callStack).Should().BeTrue();
                callStack.Should().Contain("at f");
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void JavaScriptThrowFromADeepRecursionStillBecomesAThrowCompletion()
    {
        // The control: a JavaScript throw was never the broken case, and the filter must not change how
        // it is mapped — at the host boundary or by a script-level catch.
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine();
                engine.Execute("function f(n) { if (n <= 0) { throw new RangeError('boom'); } return f(n - 1); }");

                var thrown = Invoking(() => engine.Evaluate("f(300)"))
                    .Should().ThrowExactly<JavaScriptException>().Which;
                thrown.Message.Should().Be("boom");
                thrown.Error.Get("name").AsString().Should().Be("RangeError");

                engine.Evaluate("(function () { try { f(300); } catch (e) { return e.name + ': ' + e.message; } })()")
                    .Should().Be("RangeError: boom");
            },
            maxStackSize: SmallStack);
    }

    // A for-of on the recursion path puts a JintForInForOfStatement frame on the stack once per
    // JavaScript frame, and that frame kept the catch { bookkeep; throw; } the statement lists shed —
    // so this shape went on dying at a fraction of the depth it can reach forward, long after the rest
    // of the unwind was fixed. Measured on the 1 MB stack these run on: the loop reaches 219 frames
    // forward, and the constraint's unwind used to survive only 78 of them. Every limit below sits
    // between the two, so a regression is a dead test host rather than a smaller number.
    private const string ForOfRecursion = "var depth = 0; function f() { depth++; for (const x of [1]) { return f(); } }";

    [Test]
    public void RecursionLimitFiringInsideAForOfBodyUnwindsToTheHost()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 130);
                engine.Execute(ForOfRecursion);

                Invoking(() => engine.Evaluate("f()")).Should().ThrowExactly<RecursionDepthOverflowException>();
                engine.CallStack.Count.Should().Be(0);
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void ClrExceptionFromHostCodeUnwindsADeepForOfRecursion()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine();
                engine.SetValue("bang", new Action(() => throw new InvalidOperationException("bang")));
                engine.Execute("function f(n) { if (n <= 0) { bang(); } for (const x of [1]) { return f(n - 1); } }");

                var thrown = Invoking(() => engine.Evaluate("f(130)"))
                    .Should().ThrowExactly<InvalidOperationException>().Which;

                JintException.TryGetJavaScriptCallStack(thrown, out var callStack).Should().BeTrue();
                callStack.Should().Contain("at f");
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void EngineStaysUsableAfterAForOfRecursionLimitUnwind()
    {
        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine(o => o.Constraints.MaxRecursionDepth = 130);
                engine.Execute(ForOfRecursion);
                engine.Execute("function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }");

                var depths = new List<double>();
                for (var round = 0; round < 3; round++)
                {
                    engine.Evaluate("depth = 0");
                    Invoking(() => engine.Evaluate("f()")).Should().ThrowExactly<RecursionDepthOverflowException>();

                    // Every one of those frames also owned an iteration environment and an iterator to
                    // close on the way out, so a leak here is more likely than in the plain-call shape.
                    depths.Add(engine.Evaluate("depth").AsNumber());
                    engine.CallStack.Count.Should().Be(0);
                    engine.Evaluate("fib(20)").AsNumber().Should().Be(6765);
                }

                depths.Should().AllBeEquivalentTo(depths[0], "an unwind that left anything behind would move the next round's number");
            },
            maxStackSize: SmallStack);
    }

    [Test]
    public void AnExceptionUnwindingAForOfStillClosesTheIteratorWithAThrowCompletion()
    {
        // The completion the loop's finally performs IteratorClose with is what the filter has to assign
        // before it declines: a throw completion swallows a failing return(), per step 5 of IteratorClose,
        // while any other completion would let that failure replace the exception actually in flight.
        // No recursion needed — one frame is enough to see which completion was recorded.
        var engine = new Engine();
        engine.SetValue("bang", new Action(() => throw new InvalidOperationException("bang")));
        engine.Execute("""
            var returned = false;
            var it = {
                [Symbol.iterator]: function () {
                    return {
                        next: function () { return { value: 1, done: false }; },
                        return: function () { returned = true; throw new Error('from return'); }
                    };
                }
            };
            """);

        Invoking(() => engine.Evaluate("(function () { for (const x of it) { bang(); } })()"))
            .Should().ThrowExactly<InvalidOperationException>().WithMessage("bang");

        engine.Evaluate("returned").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ShouldLimitArraySizeForConcat()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).Constraints.MaxArraySize = 1_000_000);
        Invoking(() => engine.Evaluate("for (let a = [1, 2, 3];; a = a.concat(a)) ;")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitArraySizeForFill()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).Constraints.MaxArraySize = 1_000_000);
        Invoking(() => engine.Evaluate("var arr = Array(1000000000).fill(new Array(1000000000));")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitArraySizeForJoin()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).Constraints.MaxArraySize = 1_000_000);
        Invoking(() => engine.Evaluate("new Array(2147483647).join('*')")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitTypedArraySizeForFill()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("var arr = new Uint8Array(100000000); arr.fill(255);")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitTypedArraySizeForCopyWithin()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("var arr = new Uint8Array(100000000); arr[0] = 1; arr.copyWithin(1, 0);")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitTypedArraySizeForReverse()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("var arr = new Uint8Array(100000000); arr.reverse();")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitTypedArraySizeForToReversed()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("var arr = new Uint8Array(100000000); arr.toReversed();")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitTypedArraySizeForWith()
    {
        var engine = new Engine(o => o.LimitStatements(1_000).LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("var arr = new Uint8Array(100000000); arr.with(0, 1);")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    // https://github.com/sebastienros/jint/issues/2486
    [Test]
    public void ShouldThrowRangeErrorWhenPadStartExceedsMaxStringLength()
    {
        // The result length (2147483647) exceeds JsString.MaxLength, so the size cap converts
        // a would-be OutOfMemoryException into a catchable RangeError without any constraints set.
        var engine = new Engine();
        var ex = Invoking(() => engine.Evaluate("'x'.padStart(2147483647)")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Invalid string length");
    }

    // https://github.com/sebastienros/jint/issues/2486
    [Test]
    public void ShouldLimitStringSizeForPadEnd()
    {
        // The result is exactly at the size cap rather than past it, so it must be built incrementally
        // and the memory limit must be able to interrupt it instead of allocating ~1 GB up front.
        var engine = new Engine(o => o.LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate($"'x'.padEnd({JsString.MaxLength}, 'ab')")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    // https://github.com/sebastienros/jint/issues/2486
    [Test]
    public void ShouldLimitArraySizeForArrayFrom()
    {
        var engine = new Engine(o => o.LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("Array.from({ length: 50000000 });")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitStringSizeForStringRaw()
    {
        var engine = new Engine(o => o.LimitMemory(4_000_000));
        Invoking(() => engine.Evaluate("String.raw({ raw: { length: 50000000 } });")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Test]
    public void ShouldLimitArraySizeForSort()
    {
        // The element-collection loop in sort is interruptible: a low statement budget aborts it.
        var engine = new Engine(o => o.LimitStatements(1_000));
        Invoking(() => engine.Evaluate("new Array(50000000).sort();")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void ShouldLimitArraySizeForForEach()
    {
        // forEach over a huge (sparse) array must be interruptible even though the callback never
        // runs for holes. A sparse array is used so the guard exercised is forEach's own loop, not
        // fill's (fill on a dense 50M array would trip the limit before forEach was ever reached).
        var engine = new Engine(o => o.LimitStatements(1_000));
        Invoking(() => engine.Evaluate("new Array(50000000).forEach(function () {});")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void PadStartAndPadEndProduceCorrectResults()
    {
        var engine = new Engine();
        engine.Evaluate("'5'.padStart(3, '0')").AsString().Should().Be("005");
        engine.Evaluate("'5'.padEnd(3, '0')").AsString().Should().Be("500");
        engine.Evaluate("'ab'.padStart(1)").AsString().Should().Be("ab");
        engine.Evaluate("'x'.padStart(5)").AsString().Should().Be("    x");
        engine.Evaluate("'x'.padEnd(5)").AsString().Should().Be("x    ");
        // Empty fill string returns the input unchanged.
        engine.Evaluate("'x'.padEnd(5, '')").AsString().Should().Be("x");
        engine.Evaluate("'abc'.padStart(10, '123')").AsString().Should().Be("1231231abc");
        engine.Evaluate("'abc'.padEnd(10, '123')").AsString().Should().Be("abc1231231");
    }

    [Test]
    public void PadStartEvaluatesFillStringAfterLengthCheck()
    {
        // Per spec (https://tc39.es/ecma262/#sec-stringpad) the fillString is resolved only after the
        // maxLength <= stringLength early return, so its ToString side effect must not run here.
        var engine = new Engine();
        var result = engine.Evaluate(
            "var sideEffect = false;" +
            "var fill = { toString() { sideEffect = true; return '0'; } };" +
            "'abc'.padStart(2, fill);" +
            "sideEffect;");
        result.AsBoolean().Should().BeFalse();
    }

    [Test]
    public void JoinReleasesJoinStackWhenInterrupted()
    {
        // Regression: when a constraint interrupts a large join mid-loop, the array must not be left
        // on the engine's long-lived join stack — otherwise a later join of the same array would
        // wrongly return "" via false cyclic-reference detection.
        var engine = new Engine(o => o.LimitStatements(10_000_000));
        engine.Evaluate("var a = []; for (var i = 0; i < 20000; i++) a[i] = i;");

        var maxStatements = engine.Constraints.Find<MaxStatementsConstraint>()!;
        maxStatements.MaxStatements = 1;
        engine.Constraints.Reset();
        Invoking(() => engine.Evaluate("a.join(',')")).Should().ThrowExactly<StatementsCountOverflowException>();

        // With a generous budget the same array must join correctly (not the empty string).
        maxStatements.MaxStatements = 10_000_000;
        engine.Constraints.Reset();
        engine.Evaluate("a.join(',')").AsString().Should().StartWith("0,1,2,3,");
    }

    [Test]
    public void ShouldLimitArrayFromWithNativeIterator()
    {
        // Array.from over a native (statement-free) string iterator must be interruptible via the
        // shared iterator-protocol guard; the string iterator runs no JS statements per element.
        var engine = new Engine(o => o.LimitStatements(10_000_000));
        engine.Evaluate("var s = 'x'; for (var i = 0; i < 17; i++) s += s;"); // 131072 chars

        var maxStatements = engine.Constraints.Find<MaxStatementsConstraint>()!;
        maxStatements.MaxStatements = 1;
        engine.Constraints.Reset();
        Invoking(() => engine.Evaluate("Array.from(s);")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void ShouldLimitObjectKeysForLargeArray()
    {
        // Object.keys is a pure native enumeration with no JS callback to self-throttle; it must be
        // interruptible by the constraint check in EnumerableOwnProperties.
        var engine = new Engine(o => o.LimitStatements(10_000_000));
        engine.Evaluate("var a = []; for (var i = 0; i < 30000; i++) a[i] = i;");

        var maxStatements = engine.Constraints.Find<MaxStatementsConstraint>()!;
        maxStatements.MaxStatements = 1;
        engine.Constraints.Reset();
        Invoking(() => engine.Evaluate("Object.keys(a);")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void ShouldLimitMapForEachWithNativeCallback()
    {
        // A native (CLR) callback does not self-throttle via statement checks, so Map.prototype.forEach
        // must be interruptible by its own constraint check.
        var engine = new Engine(o => o.LimitStatements(10_000_000));
        engine.Evaluate("var m = new Map(); for (var i = 0; i < 30000; i++) m.set(i, i);");

        var maxStatements = engine.Constraints.Find<MaxStatementsConstraint>()!;
        maxStatements.MaxStatements = 1;
        engine.Constraints.Reset();
        Invoking(() => engine.Evaluate("m.forEach(Math.max);")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Test]
    public void ShouldConsiderConstraintsWhenCallingInvoke()
    {
        var engine = new Engine(options =>
        {
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(1000));
        });
        var myApi = new MyApi();

        engine.SetValue("myApi", myApi);
        engine.Execute("myApi.addEventListener('DataReceived', (data) => { myApi.log(data) })");

        var dataReceivedCallbacks = myApi.Callbacks.Where(kvp => kvp.Key == "DataReceived");
        foreach (var callback in dataReceivedCallbacks)
        {
            engine.Invoke(callback.Value, "Data Received #1");
            Thread.Sleep(200);
            engine.Invoke(callback.Value, "Data Received #2");
        }
    }

    [Test]
    public void ResetConstraints()
    {
        void ExecuteAction(Engine engine) => engine.Execute("recursion()");
        void InvokeAction(Engine engine) => engine.Invoke("recursion");

        List<int> expected = [6, 6, 6, 6, 6];
        RunLoop(CreateEngine(), ExecuteAction).Should().Equal(expected);
        RunLoop(CreateEngine(), InvokeAction).Should().Equal(expected);

        var e1 = CreateEngine();
        RunLoop(e1, ExecuteAction).Should().Equal(expected);
        RunLoop(e1, InvokeAction).Should().Equal(expected);

        var e2 = CreateEngine();
        RunLoop(e2, InvokeAction).Should().Equal(expected);
        RunLoop(e2, ExecuteAction).Should().Equal(expected);

        var e3 = CreateEngine();
        RunLoop(e3, InvokeAction).Should().Equal(expected);
        RunLoop(e3, ExecuteAction).Should().Equal(expected);
        RunLoop(e3, InvokeAction).Should().Equal(expected);

        var e4 = CreateEngine();
        RunLoop(e4, InvokeAction).Should().Equal(expected);
        RunLoop(e4, InvokeAction).Should().Equal(expected);
    }

    [Test]
    public void ShouldThrowScriptPreparationExceptionForDeeplyNestedScript()
    {
        // Generate a script with more than MaxDepth (256) levels of AST nesting.
        // Each if-block pair adds 2 depth levels: IfStatement + BlockStatement.
        // 150 pairs → depth up to ~302, well above the 256 limit, and safe for the
        // Acornima parser on all platforms (Windows 1MB stack, macOS, Linux).
        const int nestingDepth = 150;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < nestingDepth; i++) sb.Append("if(true){");
        sb.Append("1");
        for (int i = 0; i < nestingDepth; i++) sb.Append("}");

        Invoking(() => new Engine().Execute(sb.ToString())).Should().ThrowExactly<ScriptPreparationException>();
    }

    // https://github.com/sebastienros/jint/discussions/2707
    // With only amortized constraints registered (here: a cancellation token), the
    // statement-count amortization must not defer cancellation across host calls that can each
    // take arbitrarily long; the host-call boundary re-check bounds detection latency to a
    // single call. Cancelling from inside the 3rd host call makes the assertion deterministic:
    // the check at that call's return must abort the loop before a 4th call starts (without the
    // boundary check the loop would continue until the 64-statement countdown fires, i.e.
    // dozens of calls later).
    private static void AssertCancelledDuringThirdHostCall(Func<CancellationTokenSource, Engine, Func<int>> setup, string script)
    {
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg
            .ObserveCancellation(cts.Token)
            .Interop.AllowWrite = true);
        var hostCallCount = setup(cts, engine);

        Invoking(() => engine.Execute(script)).Should().ThrowExactly<ExecutionCanceledException>();
        hostCallCount().Should().Be(3);
    }

    [Test]
    public void CancellationIsObservedBetweenSlowHostDelegateCalls()
    {
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var calls = 0;
            engine.SetValue("work", new Action(() => { if (++calls == 3) { cts.Cancel(); } }));
            return () => calls;
        }, "for (var i = 0; i < 40; i++) { work(); }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowHostDelegateCallsInFunctionLocalTightLoop()
    {
        // the function-local expression-body loop shape arms the interpreter's tight for-body lane
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var calls = 0;
            engine.SetValue("work", new Action(() => { if (++calls == 3) { cts.Cancel(); } }));
            return () => calls;
        }, "function f() { for (var i = 0; i < 40; i++) { work(); } } f();");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowClrMethodCalls()
    {
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var probe = new HostBoundaryProbe();
            probe.OnAccess = () => { if (probe.Accesses == 3) { cts.Cancel(); } };
            engine.SetValue("probe", probe);
            return () => probe.Accesses;
        }, "for (var i = 0; i < 40; i++) { probe.method(); }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowClrPropertyReads()
    {
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var probe = new HostBoundaryProbe();
            probe.OnAccess = () => { if (probe.Accesses == 3) { cts.Cancel(); } };
            engine.SetValue("probe", probe);
            return () => probe.Accesses;
        }, "for (var i = 0; i < 40; i++) { var x = probe.value; }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowClrPropertyWrites()
    {
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var probe = new HostBoundaryProbe();
            probe.OnAccess = () => { if (probe.Accesses == 3) { cts.Cancel(); } };
            engine.SetValue("probe", probe);
            return () => probe.Accesses;
        }, "for (var i = 0; i < 40; i++) { probe.value = i; }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowClrConstructorCalls()
    {
        try
        {
            AssertCancelledDuringThirdHostCall((cts, engine) =>
            {
                CtorProbe.Constructions = 0;
                CtorProbe.OnConstruct = () => { if (CtorProbe.Constructions == 3) { cts.Cancel(); } };
                engine.SetValue("Probe", Jint.Runtime.Interop.TypeReference.CreateTypeReference<CtorProbe>(engine));
                return () => CtorProbe.Constructions;
            }, "for (var i = 0; i < 40; i++) { new Probe(); }");
        }
        finally
        {
            CtorProbe.OnConstruct = static () => { };
        }
    }

    [Test]
    public void CancellationIsObservedBetweenSlowDictionaryReads()
    {
        // wrapped IDictionary<string, T> reads bypass ReflectionAccessor and route through
        // TypeDescriptor.TryGetDictionaryValue; that lane must observe the boundary too
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var dictionary = new SlowDictionary { { "someKey", 42 } };
            dictionary.OnAccess = () => { if (dictionary.Accesses == 3) { cts.Cancel(); } };
            engine.SetValue("cfg", dictionary);
            return () => dictionary.Accesses;
        }, "for (var i = 0; i < 40; i++) { var v = cfg.someKey; }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowEnumerableIterations()
    {
        // for-of over a wrapped IEnumerable drives the user's IEnumerator.MoveNext directly
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var enumerable = new SlowEnumerable();
            enumerable.OnMoveNext = () => { if (enumerable.MoveNextCalls == 3) { cts.Cancel(); } };
            engine.SetValue("items", enumerable);
            return () => enumerable.MoveNextCalls;
        }, "for (const x of items) { }");
    }

    [Test]
    public void CancellationDuringLoneHostCallIsObservedAtItsReturn()
    {
        // isolates the post-invoke check: no further host call or 64-statement countdown
        // follows, so only the check at the first call's own return can observe the cancellation
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));

        var calls = 0;
        engine.SetValue("work", new Action(() => { calls++; cts.Cancel(); }));

        Invoking(() => engine.Execute("work(); work();")).Should().ThrowExactly<ExecutionCanceledException>();
        calls.Should().Be(1);
    }

    [Test]
    public void HostSideAccessToWrappedObjectDoesNotObserveCancellationAfterExecution()
    {
        // constraint state is only meaningful inside an Execute/Invoke window; cancelling the
        // token during normal teardown must not make later C#-side reads of wrapped objects throw
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);
        engine.Execute("probe.value;");

        cts.Cancel();

        var wrapper = engine.GetValue("probe").AsObject();
        wrapper.Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void HostSideAccessToWrappedObjectDoesNotObserveExpiredTimeoutAfterExecution()
    {
        // the time constraint's CTS is re-armed at the end of every run and keeps counting while
        // the engine is idle; an expired timer must not make later C#-side reads throw.
        // the executed script deliberately does no interop so the run itself cannot race the timer
        var engine = new Engine(cfg => cfg.LimitExecutionTime(TimeSpan.FromMilliseconds(50)));
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);
        engine.Execute("42;");

        Thread.Sleep(200);

        var wrapper = engine.GetValue("probe").AsObject();
        wrapper.Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void TimeoutIsObservedAtHostCallReturn()
    {
        // TimeConstraint coverage for the boundary mechanism. The host call waits until the
        // time budget has observably expired (polling the public constraint check), so the
        // return-side boundary check must fire — immune to CTS timer scheduling delays on CI.
        var engine = new Engine(cfg => cfg.LimitExecutionTime(TimeSpan.FromMilliseconds(50)));

        var calls = 0;
        engine.SetValue("work", new Action(() =>
        {
            calls++;
            while (true)
            {
                try
                {
                    engine.Constraints.Check();
                }
                catch (TimeoutException)
                {
                    return;
                }

                Thread.Sleep(10);
            }
        }));

        Invoking(() => engine.Execute("work(); work();")).Should().ThrowExactly<TimeoutException>();
        calls.Should().Be(1);
    }

    [Test]
    public void CancellationIsObservedInHostDrivenAsyncContinuationHostCalls()
    {
        // resolving a promise from the host drains the async continuation outside any
        // Execute/Evaluate window: async resumes push execution contexts but do not install
        // the ambient evaluation context, so the boundary-check gate must key on the
        // execution-context stack depth, not the ambient field
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));

        var calls = 0;
        engine.SetValue("work", new Action(() => { if (++calls == 3) { cts.Cancel(); } }));

        var gate = engine.Tasks.RegisterPromise();
        engine.SetValue("gate", gate.Promise);
        engine.Evaluate("(async () => { await gate; for (var i = 0; i < 40; i++) { work(); } })()");
        calls.Should().Be(0);

        Invoking(() => gate.Resolve(JsValue.Undefined)).Should().ThrowExactly<ExecutionCanceledException>();
        calls.Should().Be(3);
    }

    [Test]
    public void CancellationIsObservedBetweenSlowNonStringKeyedDictionaryReads()
    {
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var dictionary = new SlowIntDictionary();
            dictionary.Prime(5, 42);
            dictionary.OnAccess = () => { if (dictionary.Accesses == 3) { cts.Cancel(); } };
            engine.SetValue("cfg", dictionary);
            return () => dictionary.Accesses;
        }, "for (var i = 0; i < 40; i++) { var v = cfg[5]; }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowNonStringKeyedDictionaryWrites()
    {
        AssertCancelledDuringThirdHostCall((cts, engine) =>
        {
            var dictionary = new SlowIntDictionary();
            dictionary.OnAccess = () => { if (dictionary.Accesses == 3) { cts.Cancel(); } };
            engine.SetValue("cfg", dictionary);
            return () => dictionary.Accesses;
        }, "for (var i = 0; i < 40; i++) { cfg[5] = i; }");
    }

    [Test]
    public void CancellationIsObservedBetweenSlowMemberAccessorCallbacks()
    {
        using var cts = new CancellationTokenSource();
        var accesses = 0;
        var engine = new Engine(cfg =>
        {
            cfg.ObserveCancellation(cts.Token);
            cfg.Interop.MemberAccessor = (_, _, _) =>
            {
                if (++accesses == 3)
                {
                    cts.Cancel();
                }

                return JsValue.Undefined;
            };
        });

        // a dictionary target keeps member resolution uncached, so the accessor runs per read
        engine.SetValue("cfg", new Dictionary<string, object>());

        Invoking(() => engine.Execute("for (var i = 0; i < 40; i++) { var v = cfg.missingKey; }")).Should().ThrowExactly<ExecutionCanceledException>();
        accesses.Should().Be(3);
    }

    [Test]
    public void DebugModeExecutionStillObservesCancellationAtHostBoundaries()
    {
        // debug mode must not weaken constraint enforcement during normal full-speed execution;
        // only debugger expression evaluation is exempt
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token).Debugger.Enabled = true);

        var calls = 0;
        engine.SetValue("work", new Action(() => { if (++calls == 3) { cts.Cancel(); } }));

        Invoking(() => engine.Execute("for (var i = 0; i < 40; i++) { work(); }")).Should().ThrowExactly<ExecutionCanceledException>();
        calls.Should().Be(3);
    }

    [Test]
    public void DebuggerExpressionEvaluationIsExemptFromHostBoundaryChecks()
    {
        // a watch-style evaluation with a pending cancellation must still be able to read
        // interop members; the boundary check fires once control returns to the script
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token).Debugger.Enabled = true);
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);

        JsValue evaluated = null;
        engine.SetValue("work", new Action(() =>
        {
            cts.Cancel();
            evaluated = engine.Debugger.Evaluate("probe.value");
        }));

        Invoking(() => engine.Execute("work(); work();")).Should().ThrowExactly<ExecutionCanceledException>();
        evaluated.AsNumber().Should().Be(42);
        probe.Accesses.Should().Be(1);
    }

    [Test]
    public void ConstraintThrowInsideGeneratorResumeDoesNotPoisonHostBoundaryGate()
    {
        // a raw constraint exception thrown inside a resumed generator frame used to skip the
        // execution-context pop, permanently satisfying the depth-keyed host-boundary gate: every
        // later C#-side read of a wrapped object then re-observed the constraints while idle
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);
        engine.SetValue("cancel", new Action(cts.Cancel));

        engine.Execute("function* g() { cancel(); while (true) { } } var it = g();");
        Invoking(() => engine.Execute("it.next();")).Should().ThrowExactly<ExecutionCanceledException>();

        // the token stays cancelled; only a leaked frame would make this idle read observe it
        var wrapper = engine.GetValue("probe").AsObject();
        wrapper.Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void ConstraintThrowInsideAsyncResumeDoesNotPoisonHostBoundaryGate()
    {
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);
        engine.SetValue("cancel", new Action(cts.Cancel));

        var gate = engine.Tasks.RegisterPromise();
        engine.SetValue("gate", gate.Promise);
        engine.Evaluate("(async () => { await gate; cancel(); })()");

        Invoking(() => gate.Resolve(JsValue.Undefined)).Should().ThrowExactly<ExecutionCanceledException>();

        var wrapper = engine.GetValue("probe").AsObject();
        wrapper.Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void ConstraintThrowInsideAsyncGeneratorResumeDoesNotPoisonHostBoundaryGate()
    {
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);
        engine.SetValue("cancel", new Action(cts.Cancel));

        engine.Execute("async function* ag() { cancel(); yield 1; } var it = ag();");
        Invoking(() => engine.Execute("it.next();")).Should().ThrowExactly<ExecutionCanceledException>();

        var wrapper = engine.GetValue("probe").AsObject();
        wrapper.Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void ShadowRealmImportFailureDoesNotPoisonHostBoundaryGate()
    {
        // module resolution failures surface as ModuleResolutionException (not JavaScriptException);
        // the ShadowRealm import lane must still pop the execution context it entered
        using var cts = new CancellationTokenSource();
        var engine = new Engine(cfg => cfg.ObserveCancellation(cts.Token));
        var probe = new HostBoundaryProbe();
        engine.SetValue("probe", probe);

        engine.Execute("var sr = new ShadowRealm();");
        Invoking(() => engine.Evaluate("sr.importValue('./does-not-exist.js', 'x')")).Should().Throw<Exception>();

        cts.Cancel();
        var wrapper = engine.GetValue("probe").AsObject();
        wrapper.Get("value").AsNumber().Should().Be(42);
    }

    [Test]
    public void NestedEngineReentryDoesNotResetOuterConstraints()
    {
        // a host callback calling back into the engine re-enters ExecuteWithConstraints; the nested
        // run must not re-arm the outer script's budget — "while (true) reenter();" would otherwise
        // reset the statement counter (and timeout) on every iteration and run forever
        var engine = new Engine(cfg => cfg.LimitStatements(5000));

        var calls = 0;
        engine.SetValue("reenter", new Action(() =>
        {
            if (++calls > 100_000)
            {
                throw new InvalidOperationException("outer budget was reset by nested evaluation");
            }
            engine.Evaluate("1;");
        }));

        Invoking(() => engine.Execute("while (true) { reenter(); }")).Should().ThrowExactly<StatementsCountOverflowException>();
        calls.Should().BeLessThan(5000);
    }

    [Test]
    public void SequentialTopLevelExecutionsStillResetConstraints()
    {
        // the nested-entry guard must not affect sequential top-level runs: each gets a fresh budget
        var engine = new Engine(cfg => cfg.LimitStatements(100));
        for (var i = 0; i < 5; i++)
        {
            engine.Evaluate("var x = 0; for (var j = 0; j < 30; j++) { x += 3; } x;").AsNumber().Should().Be(90);
        }
    }

    private sealed class HostBoundaryProbe
    {
        public int Accesses;
        public Action OnAccess = static () => { };

        public int Value
        {
            get
            {
                Accesses++;
                OnAccess();
                return 42;
            }
            set
            {
                Accesses++;
                OnAccess();
            }
        }

        public int Method()
        {
            Accesses++;
            OnAccess();
            return 42;
        }
    }

    private sealed class CtorProbe
    {
        public static int Constructions;
        public static Action OnConstruct = static () => { };

        public CtorProbe()
        {
            Constructions++;
            OnConstruct();
        }
    }

    private sealed class SlowDictionary : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _inner = new();

        public int Accesses;
        public Action OnAccess = static () => { };

        public object this[string key]
        {
            get
            {
                Accesses++;
                OnAccess();
                return _inner[key];
            }
            set
            {
                Accesses++;
                OnAccess();
                _inner[key] = value;
            }
        }

        public bool TryGetValue(string key, out object value)
        {
            Accesses++;
            OnAccess();
            return _inner.TryGetValue(key, out value);
        }

        public ICollection<string> Keys => _inner.Keys;
        public ICollection<object> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;

        public void Add(string key, object value) => _inner.Add(key, value);
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public bool Remove(string key) => _inner.Remove(key);
        public void Add(KeyValuePair<string, object> item) => _inner.Add(item.Key, item.Value);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<string, object> item) => _inner.ContainsKey(item.Key);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => ((IDictionary<string, object>) _inner).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, object> item) => _inner.Remove(item.Key);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    private sealed class SlowIntDictionary : IDictionary<int, object>
    {
        private readonly Dictionary<int, object> _inner = new();

        public int Accesses;
        public Action OnAccess = static () => { };

        public void Prime(int key, object value) => _inner[key] = value;

        public object this[int key]
        {
            get
            {
                Accesses++;
                OnAccess();
                return _inner[key];
            }
            set
            {
                Accesses++;
                OnAccess();
                _inner[key] = value;
            }
        }

        public bool TryGetValue(int key, out object value)
        {
            Accesses++;
            OnAccess();
            return _inner.TryGetValue(key, out value);
        }

        public ICollection<int> Keys => _inner.Keys;
        public ICollection<object> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;

        public void Add(int key, object value) => _inner.Add(key, value);
        public bool ContainsKey(int key) => _inner.ContainsKey(key);
        public bool Remove(int key) => _inner.Remove(key);
        public void Add(KeyValuePair<int, object> item) => _inner.Add(item.Key, item.Value);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<int, object> item) => _inner.ContainsKey(item.Key);
        public void CopyTo(KeyValuePair<int, object>[] array, int arrayIndex) => ((IDictionary<int, object>) _inner).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<int, object> item) => _inner.Remove(item.Key);
        public IEnumerator<KeyValuePair<int, object>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    private sealed class SlowEnumerable : System.Collections.IEnumerable
    {
        public int MoveNextCalls;
        public Action OnMoveNext = static () => { };

        public System.Collections.IEnumerator GetEnumerator() => new Enumerator(this);

        private sealed class Enumerator : System.Collections.IEnumerator
        {
            private readonly SlowEnumerable _owner;
            private int _index;

            public Enumerator(SlowEnumerable owner)
            {
                _owner = owner;
            }

            public object Current => _index;

            public bool MoveNext()
            {
                _owner.MoveNextCalls++;
                _owner.OnMoveNext();
                return _index++ < 40;
            }

            public void Reset()
            {
            }
        }
    }

    private static Engine CreateEngine()
    {
        Engine engine = new(options => options.Constraints.MaxRecursionDepth = 5);
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
