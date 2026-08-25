#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Modules;
#if NET8_0_OR_GREATER
using Jint.WebApi;
#endif


namespace Jint.Tests.PublicInterface;

public class HostMemoryLimitTests
{
    private const string ConcurrentUseMessage = "*already in use by another thread or has an asynchronous operation in progress*";
    private const int AllocationSize = 2_000_000;
    private const int SingleAllocationBudget = 3_500_000;

    /// <summary>
    /// How long <see cref="PromiseContinuationRetainsItsOriginatingBudgetWhenPumpedOnAnotherThread"/>
    /// keeps pumping before giving up. A ceiling only a genuine hang can reach rather than a budget the
    /// continuation has to beat: a healthy run exits on the first pump that observes the settled
    /// continuation, so widening this costs nothing except on a failure.
    /// </summary>
    private static readonly TimeSpan PumpCeiling = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the asynchronous loader keeps
    /// <see cref="SynchronousImportDoesNotChargeAsyncLoaderWaitToExecutionTimeout"/> waiting. Deliberately
    /// longer than that test's 200 ms execution timeout, since the whole claim is that the wait is not
    /// charged to it.
    /// </summary>
    private static readonly TimeSpan LoaderWait = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task EvaluateAsyncCarriesAllocationBudgetAcrossAThreadHop()
    {
        var allocations = new List<byte[]>();
        var threads = new List<int>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = CreateEngine(allocations);
        engine.SetValue("gate", new Func<Task>(() => gate.Task));
        engine.SetValue("recordThread", new Action(() => threads.Add(Environment.CurrentManagedThreadId)));

        Task<JsValue>? pending = null;
        Exception? startFailure = null;
        var startingThread = new Thread(() =>
        {
            try
            {
                pending = engine.EvaluateAsync("""
                    (async () => {
                        recordThread();
                        allocate();
                        await gate();
                        recordThread();
                        allocate();
                    })()
                    """);
            }
            catch (Exception exception)
            {
                startFailure = exception;
            }
        });

        startingThread.Start();
        startingThread.Join();
        startFailure.Should().BeNull();
        pending.Should().NotBeNull();
        pending!.IsCompleted.Should().BeFalse();

        gate.SetResult(true);

        var exception = await Record.ExceptionAsync(() => pending);
        exception.Should().BeOfType<MemoryLimitExceededException>();
        threads.Should().HaveCount(2);
        threads[1].Should().NotBe(threads[0], "the thread that began the evaluation has already exited");
    }

    [Fact]
    public async Task InvokeAsyncCarriesAllocationBudgetAcrossAThreadHop()
    {
        var allocations = new List<byte[]>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = CreateEngine(allocations);
        engine.SetValue("gate", new Func<Task>(() => gate.Task));
        engine.Execute("""
            async function work() {
                allocate();
                await gate();
                allocate();
            }
            """);

        Task<JsValue>? pending = null;
        var startingThread = new Thread(() => pending = engine.InvokeAsync("work"));
        startingThread.Start();
        startingThread.Join();
        pending.Should().NotBeNull();
        pending!.IsCompleted.Should().BeFalse();

        gate.SetResult(true);

        var exception = await Record.ExceptionAsync(() => pending);
        exception.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Fact]
    public async Task TransferredHostCallbackCountsAllocationsOnItsThread()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);
        engine.SetValue("schedule", new Func<Func<int>, Task<int>>(callback => Task.Run(callback)));

        // The call itself is asserted not to throw, not merely left unwrapped: whether the pool thread's
        // charge lands before or after the engine thread's post-script check decides where the failure is
        // raised, and only the task is an acceptable answer. See HostAsyncFailureChannelTests.
        Task<JsValue>? pending = null;
        Invoking(() =>
        {
            pending = engine.EvaluateAsync("""
                (async () => {
                    allocate();
                    return await schedule(() => {
                        allocate();
                        return 42;
                    });
                })()
                """);
        })
            .Should().NotThrow("a budget failure belongs on the returned task, whichever thread trips it");

        var exception = await Record.ExceptionAsync(() => pending!);
        exception.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Fact]
    public void NestedTransferredCallbacksKeepTheOuterMemoryOperation()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);
        engine.SetValue("outer", new Action<Action>(callback => callback()));
        engine.SetValue("inner", new Action<Action>(callback =>
            Task.Run(callback).GetAwaiter().GetResult()));

        Invoking(() => engine.Evaluate("""
            allocate();
            outer(() => inner(() => allocate()));
            """)).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public async Task HostTaskTimeoutRemainsACatchablePromiseRejection()
    {
        var engine = new Engine(options => options.LimitMemory(SingleAllocationBudget));
        engine.SetValue("fail", new Func<Task>(() =>
            Task.FromException(new TimeoutException("host timeout"))));

        var result = await engine.EvaluateAsync("""
            (async () => {
                try {
                    await fail();
                    return 'unexpected';
                } catch (e) {
                    return 'caught';
                }
            })()
            """);

        result.AsString().Should().Be("caught");
    }

    [Fact]
    public async Task AggregateTaskFaultPropagatesContainedMemoryFailure()
    {
        var allocations = new List<byte[]>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = CreateEngine(allocations);
        engine.SetValue("schedule", new Func<Action, Task>(callback => Task.WhenAll(
            Task.Run(async () =>
            {
                await gate.Task;
                callback();
            }),
            Task.FromException(new InvalidOperationException("ordinary")))));

        var pending = engine.EvaluateAsync("""
            (async () => {
                allocate();
                await schedule(() => allocate());
            })()
            """);

        gate.SetResult(true);
        var exception = await Record.ExceptionAsync(() => pending);
        exception.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Fact]
    public async Task PromiseContinuationRetainsItsOriginatingBudgetWhenPumpedOnAnotherThread()
    {
        var allocations = new List<byte[]>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = CreateEngine(allocations);
        engine.SetValue("gate", new Func<Task>(() => gate.Task));
        engine.Execute("""
            globalThis.pending = (async () => {
                allocate();
                await gate();
                allocate();
            })();
            """);

        gate.SetResult(true);

        // The pump waits for the gate's continuation, which itself needs a thread-pool worker, so it must
        // not hold one while it waits — a saturated pool would otherwise decide the outcome rather than
        // the engine. See DedicatedThread.RunAsync.
        Exception? failure = null;
        await DedicatedThread.RunAsync(() =>
        {
            var deadline = DateTime.UtcNow + PumpCeiling;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    engine.Tasks.ProcessTasks();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    return;
                }

                Thread.Sleep(1);
            }
        });

        failure.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Fact]
    public void PendingPureJsReactionRetainsItsRegistrationBudgetAcrossHostEntries()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);
        engine.Execute("""
            let resolve;
            const gate = new Promise(r => resolve = r);
            globalThis.pending = (async () => {
                allocate();
                await gate;
                allocate();
            })();
            """);

        Invoking(() => engine.Execute("resolve()"))
            .Should().ThrowExactly<MemoryLimitExceededException>();
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void TimerCallbackRetainsItsRegistrationBudget()
    {
        var allocations = new List<byte[]>();
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        engine.Execute("allocate(); setTimeout(() => allocate(), 5);");
        clock.Advance(5);

        Invoking(engine.Tasks.ProcessTasks).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void AnIntervalStartsAFreshBudgetOnEveryTick()
    {
        var allocations = new List<byte[]>();
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        engine.Execute("setInterval(() => allocate(), 5);");

        // Each tick allocates well inside the budget. An interval is not one finite continuation of the
        // operation that registered it, so ten of them must not add up to one.
        for (var i = 0; i < 10; i++)
        {
            clock.Advance(5);
            Invoking(engine.Tasks.ProcessTasks).Should().NotThrow();
        }

        allocations.Should().HaveCount(10);
    }

    [Fact]
    public void TimerAllocationLimitOutranksItsJavaScriptError()
    {
        var allocations = new List<byte[]>();
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.LimitMemory(1_000_000);
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        engine.Execute("setTimeout(() => { allocate(); throw new Error('ordinary'); }, 5);");
        clock.Advance(5);

        Invoking(engine.Tasks.ProcessTasks).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void PersistentEventDeliveryStartsAFreshOrdinaryBudget()
    {
        var broker = new BroadcastChannelBroker();
        var allocations = new List<byte[]>();
        var listener = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseWebApis(webApi => webApi.Messaging.Broker = broker);
        });
        listener.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        listener.Execute("""
            allocate();
            const listener = new BroadcastChannel('memory-room');
            listener.onmessage = () => allocate();
            """);

        var sender = new Engine(options => options.UseWebApis(
            webApi => webApi.Messaging.Broker = broker));
        sender.Execute("new BroadcastChannel('memory-room').postMessage('deliver');");

        Invoking(listener.Tasks.ProcessTasks).Should().NotThrow();
    }

    [Fact]
    public void NestedIdleCallbackRetainsItsRegistrationBudgetAcrossPumps()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseWebApis(WebApiFeatures.IdleCallback);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        engine.Execute("""
            allocate();
            requestIdleCallback(() => requestIdleCallback(() => allocate()));
            """);

        Invoking(engine.Tasks.ProcessTasks).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void SchedulerTasksKeepTheirIndividualRegistrationBudgets()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseWebApis(WebApiFeatures.Scheduler);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        engine.Execute("scheduler.postTask(() => 0);");

        Invoking(() => engine.Execute("allocate(); scheduler.postTask(() => allocate());"))
            .Should().ThrowExactly<MemoryLimitExceededException>();
    }
#endif

    [Fact]
    public void AsyncModuleBuildAndEvaluationRetainTheLoadBudgetAcrossAThreadHop()
    {
        var allocations = new List<byte[]>();
        var loader = new DelayedModuleLoader(() => allocations.Add(new byte[AllocationSize]));
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseModules(loader);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        var import = engine.Modules.StartImport("module");
        import.IsCompleted.Should().BeFalse();

        loader.Complete("export const value = allocate();");

        var failure = RunOnNewThread(engine.Tasks.ProcessTasks);

        failure.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Fact]
    public async Task SynchronousImportDoesNotChargeAsyncLoaderWaitToExecutionTimeout()
    {
        var loader = new DelayedModuleLoader(() => { });
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.LimitExecutionTime(TimeSpan.FromMilliseconds(200));

            // A ceiling only a genuine hang can reach, rather than a budget racing the loader wait: the
            // wait resumes on a thread-pool worker, and on a saturated runner the pool's injection rate
            // would otherwise decide the outcome. Same treatment as #3123 and #3154.
            options.Constraints.PromiseTimeout = TimeSpan.FromMinutes(2);
            options.UseModules(loader);
        });

        // The blocking Import waits for a completion that arrives from another thread, so it must not
        // hold a pool worker while it does. See DedicatedThread.RunAsync.
        var import = DedicatedThread.RunAsync(
            () => engine.Modules.Import("module").Get("value").AsNumber().Should().Be(42));

        // The completion only exists once the engine has actually asked the loader for the module; the
        // wait that follows is what has to stay off the execution timeout.
        loader.WaitUntilStarted();
        await Task.Delay(LoaderWait);
        loader.Complete("export const value = 42;");

        await import;
    }

    [Fact]
    public async Task AllocationsWhileAnAsyncOperationIsSuspendedAreNotCharged()
    {
        var allocations = new List<byte[]>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new Engine(options => options.LimitMemory(SingleAllocationBudget));
        engine.SetValue("gate", new Func<Task>(() => gate.Task));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        var pending = engine.EvaluateAsync("(async () => { await gate(); allocate(); return 42; })()");
        pending.IsCompleted.Should().BeFalse();

        var unrelated = new byte[SingleAllocationBudget * 3];
        unrelated.Length.Should().Be(SingleAllocationBudget * 3);
        gate.SetResult(true);

        (await pending).AsNumber().Should().Be(42);
    }

    [Fact]
    public void AFinalHostCallbackCannotAllocatePastTheLimitUnobserved()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options => options.LimitMemory(1_000_000));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        Invoking(() => engine.Evaluate("allocate()")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void AllocationLimitOutranksAnOrdinaryExceptionalExit()
    {
        var engine = new Engine(options => options.LimitMemory(1_000_000));

        Invoking(() => engine.Evaluate("throw 'x'.repeat(2000000)"))
            .Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void ShadowRealmEvaluationStartsAnAccountedOperation()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options => options.LimitMemory(1_000_000));
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        Invoking(() => shadowRealm.Evaluate("allocate()"))
            .Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void DirectScriptAccessorInvocationStartsAnAccountedOperation()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options => options.LimitMemory(1_000_000));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        var target = engine.Evaluate("({ get value() { allocate(); return 1; } })").AsObject();

        Invoking(() => target.Get("value")).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void DirectAccessorMemoryTeardownDoesNotMaskAStatementFailure()
    {
        var engine = new Engine(options =>
        {
            options.LimitMemory(1_000_000);
            options.LimitStatements(100_000);
        });
        var target = engine.Evaluate("""
            ({
                get value() {
                    const large = 'x'.repeat(2000000);
                    return large.length;
                }
            })
            """).AsObject();
        var statements = engine.Constraints.Find<MaxStatementsConstraint>()!;
        statements.MaxStatements = 1;
        engine.Constraints.Reset();

        Invoking(() => target.Get("value")).Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Fact]
    public void DirectAccessorMemoryTeardownDoesNotMaskACustomConstraintFailure()
    {
        var custom = new ThrowOnSecondCheckConstraint();
        var options = new Options()
            .AddConstraint(custom)
            .AddConstraint(new MemoryLimitConstraint(1_000_000));
        var engine = new Engine(options);
        var target = engine.Evaluate("""
            ({
                get value() {
                    const large = 'x'.repeat(2000000);
                    return large.length;
                }
            })
            """).AsObject();
        custom.Arm();

        Invoking(() => target.Get("value"))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("custom constraint");
    }

    [Fact]
    public void AnExceededCompletedOperationIsNotReplayedByAnIdleCheck()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options => options.LimitMemory(1_000_000));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        Invoking(() => engine.Evaluate("allocate()")).Should().ThrowExactly<MemoryLimitExceededException>();

        Invoking(() => engine.Constraints.Check()).Should().NotThrow();
    }

    [Fact]
    public void NestedEntriesShareTheOutermostEntryBudget()
    {
        var allocations = new List<byte[]>();
        var engine = new Engine(options => options.LimitMemory(SingleAllocationBudget));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        engine.SetValue("reenter", new Action(() => engine.Evaluate("allocate()")));

        Invoking(() => engine.Evaluate("allocate(); reenter();"))
            .Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void ReusedEngineGetsAFreshBudgetForEveryTopLevelEntry()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);

        engine.Evaluate("allocate()");
        engine.Evaluate("allocate()");
        engine.Evaluate("allocate()");
    }

    [Fact]
    public void BeginAndEndProvideAnOperationDeadlineStyleMultiEntryBudget()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>();
        constraint.Should().NotBeNull();

        constraint!.Begin();
        try
        {
            engine.Evaluate("allocate()");
            Invoking(() => engine.Evaluate("allocate()")).Should().ThrowExactly<MemoryLimitExceededException>();
            constraint.AllocatedBytes.Should().BeGreaterThan(SingleAllocationBudget);
        }
        finally
        {
            constraint.End();
        }

        constraint.IsOperationActive.Should().BeFalse();
        engine.Evaluate("allocate()");
    }

    [Fact]
    public async Task OutstandingAsyncOperationOwnsTheMemoryScopeAndCarriesItsBudget()
    {
        var allocations = new List<byte[]>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = CreateEngine(allocations);
        engine.SetValue("gate", new Func<Task>(() => gate.Task));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;

        constraint.Begin();
        try
        {
            var pending = engine.EvaluateAsync("(async () => { allocate(); await gate(); allocate(); })()");

            Invoking(constraint.End)
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
            Invoking(() => _ = constraint.AllocatedBytes)
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
            Invoking(() => _ = constraint.IsOperationActive)
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);

            gate.SetResult(true);
            var exception = await Record.ExceptionAsync(() => pending);
            exception.Should().BeOfType<MemoryLimitExceededException>();
        }
        finally
        {
            constraint.End();
        }
    }

    [Fact]
    public async Task EveryMutableMemoryScopeSurfaceRejectsConcurrentHostAccess()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var engine = new Engine(options => options.LimitMemory(SingleAllocationBudget));
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait();
        }));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        // A dedicated thread rather than Task.Run: the script blocks until this test releases it, and
        // the test blocks until the script has entered, so putting either on the thread pool makes the
        // pool's injection rate part of the outcome. See DedicatedThread.RunAsync.
        var running = DedicatedThread.RunAsync(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        try
        {
            Action[] concurrentCalls =
            [
                constraint.Begin,
                constraint.End,
                constraint.Check,
                constraint.Reset,
                () => _ = constraint.AllocatedBytes,
                () => _ = constraint.IsOperationActive
            ];

            foreach (var concurrentCall in concurrentCalls)
            {
                Invoking(concurrentCall)
                    .Should().Throw<InvalidOperationException>()
                    .WithMessage(ConcurrentUseMessage);
            }
        }
        finally
        {
            release.Set();
        }

        await running;
        constraint.IsOperationActive.Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsyncTransfersOwnershipWithItsMemoryOperation()
    {
        var allocations = new List<byte[]>();
        var loader = new DelayedModuleLoader(() => allocations.Add(new byte[AllocationSize]));
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseModules(loader);

            // What this asserts is that the memory operation travels with the import and is released by it,
            // never how long the import takes. The loader is completed from a thread-pool worker, so on the
            // engine's default ten-second budget a saturated pool would decide the outcome instead of the
            // memory scope. Same treatment the file's SynchronousImport... sibling already gives it.
            options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling;
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;

        var pending = engine.Modules.ImportAsync("module");
        pending.IsCompleted.Should().BeFalse();

        Invoking(() => _ = constraint.AllocatedBytes)
            .Should().Throw<InvalidOperationException>()
            .WithMessage(ConcurrentUseMessage);
        Invoking(constraint.Begin)
            .Should().Throw<InvalidOperationException>()
            .WithMessage(ConcurrentUseMessage);

        await Task.Run(() => loader.Complete("export const value = allocate();"));

        var exception = await Record.ExceptionAsync(() => pending);
        exception.Should().BeOfType<MemoryLimitExceededException>();
        engine.Evaluate("1").AsNumber().Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentStartImportFailsBeforeTouchingTheActiveMemoryScope()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var loader = new DelayedModuleLoader(() => { });
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseModules(loader);
        });
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait();
        }));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        constraint.Begin();
        // A dedicated thread rather than Task.Run: the script blocks until this test releases it, and
        // the test blocks until the script has entered, so putting either on the thread pool makes the
        // pool's injection rate part of the outcome. See DedicatedThread.RunAsync.
        var running = DedicatedThread.RunAsync(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        try
        {
            Invoking(() => engine.Modules.StartImport("module"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
            loader.Started.Should().BeFalse();
        }
        finally
        {
            release.Set();
        }

        await running;
        constraint.IsOperationActive.Should().BeTrue();
        constraint.End();
    }

    [Fact]
    public void SameThreadCallbackCanInspectAndReenterTheActiveMemoryOperation()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        long observed = 0;
        engine.SetValue("reenter", new Action(() =>
        {
            observed = constraint.AllocatedBytes;
            engine.Evaluate("allocate()");
        }));

        Invoking(() => engine.Evaluate("allocate(); reenter();"))
            .Should().ThrowExactly<MemoryLimitExceededException>();
        observed.Should().BeGreaterThanOrEqualTo(AllocationSize);
    }

    [Fact]
    public void ReentrantNullStateJobCannotResetTheActiveOperation()
    {
        const int budget = 3_500_000;
        var allocations = new List<byte[]>();
        var idlePromise = default(ManualPromise);
        var engine = new Engine(options => options.LimitMemory(budget));
        idlePromise = engine.Tasks.RegisterPromise();
        engine.SetValue("allocate", new Action<int>(size => allocations.Add(new byte[size])));
        engine.SetValue("drainOld", new Action(() =>
        {
            try
            {
                engine.Tasks.ProcessTasks();
            }
            catch (MemoryLimitExceededException)
            {
            }
        }));
        engine.SetValue("resolveIdle", new Action(() => idlePromise.Resolve(JsValue.Undefined)));

        Invoking(() => engine.Evaluate("""
            Promise.resolve().then(() => allocate(4000000));
            Promise.resolve().then(() => 0);
            """)).Should().ThrowExactly<MemoryLimitExceededException>();

        Invoking(() => engine.Evaluate("""
            allocate(2000000);
            drainOld();
            resolveIdle();
            allocate(2000000);
            """)).Should().ThrowExactly<MemoryLimitExceededException>();
    }

    [Fact]
    public void ExceededOperationCannotRunItsNextQueuedCallback()
    {
        var allocations = new List<byte[]>();
        var callbackRan = false;
        var engine = new Engine(options => options.LimitMemory(1_000_000));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        engine.SetValue("mark", new Action(() => callbackRan = true));

        Invoking(() => engine.Execute("""
            Promise.resolve().then(() => allocate());
            Promise.resolve().then(() => mark());
            """)).Should().ThrowExactly<MemoryLimitExceededException>();

        Invoking(engine.Tasks.ProcessTasks).Should().NotThrow();
        callbackRan.Should().BeFalse();
    }

    [Fact]
    public void AnEngineIsUsableAgainAfterAnAllocationFailureToreDownTheCycle()
    {
        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);

        Invoking(() => engine.Execute("allocate(); allocate();"))
            .Should().ThrowExactly<MemoryLimitExceededException>();

        // The teardown ends the cycle, it does not end the engine: the next top-level entry is a new
        // operation with a budget of its own, and the globals the failed script defined are still there.
        engine.Execute("globalThis.survived = 1; allocate();");
        engine.Evaluate("survived").AsNumber().Should().Be(1);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void AnAllocationFailureDiscardsTheTimersTheFailedOperationScheduled()
    {
        var allocations = new List<byte[]>();
        var fired = false;
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        engine.SetValue("mark", new Action(() => fired = true));

        Invoking(() => engine.Execute("setTimeout(() => mark(), 5); allocate(); allocate();"))
            .Should().ThrowExactly<MemoryLimitExceededException>();

        clock.Advance(5);
        Invoking(engine.Tasks.ProcessTasks).Should().NotThrow();
        fired.Should().BeFalse();
    }
#endif

    [Fact]
    public async Task SharedOptionsCreateIsolatedMemoryAccountingPerEngine()
    {
        var options = new Options().LimitMemory(SingleAllocationBudget);
        var firstAllocations = new List<byte[]>();
        var secondAllocations = new List<byte[]>();
        var first = new Engine(options).SetValue("allocate", new Action(() => firstAllocations.Add(new byte[AllocationSize])));
        var second = new Engine(options).SetValue("allocate", new Action(() => secondAllocations.Add(new byte[AllocationSize])));

        var firstConstraint = first.Constraints.Find<MemoryLimitConstraint>();
        var secondConstraint = second.Constraints.Find<MemoryLimitConstraint>();
        firstConstraint.Should().NotBeSameAs(secondConstraint);

        await Task.WhenAll(
            Task.Run(() => first.Evaluate("allocate()")),
            Task.Run(() => second.Evaluate("allocate()")));
    }

    [Fact]
    public void ConstraintReportsItsAccountingAccuracyAndUsage()
    {
        MemoryLimitConstraint.Accuracy.Should().Be(MemoryLimitAccuracy.ExecutionThread);

        var allocations = new List<byte[]>();
        var engine = CreateEngine(allocations);
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;

        engine.Evaluate("allocate()");

        constraint.AllocatedBytes.Should().BeGreaterThanOrEqualTo(AllocationSize);
        constraint.MemoryLimit.Should().Be(SingleAllocationBudget);

        engine.Constraints.Reset();
        constraint.AllocatedBytes.Should().Be(0);
    }

    [Fact]
    public void MultipleMemoryConstraintsAreRejectedInsteadOfSilentlyDisablingOne()
    {
        var first = new MemoryLimitConstraint(1_000_000);
        var duplicate = new MemoryLimitConstraint(2_000_000);
        var options = new Options().AddConstraint(first).AddConstraint(duplicate);

        Invoking(() => new Engine(options))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Only one MemoryLimitConstraint*");

        options.RemoveConstraints(constraint => ReferenceEquals(constraint, duplicate));
        new Engine(options).Constraints.Find<MemoryLimitConstraint>().Should().BeSameAs(first);
    }

    [Fact]
    public void ADirectMemoryConstraintInstanceCannotBeSharedAcrossEngines()
    {
        var constraint = new MemoryLimitConstraint(SingleAllocationBudget);
        var options = new Options().AddConstraint(constraint);
        _ = new Engine(options);

        Invoking(() => new Engine(options))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*can only be registered with one Engine*");
    }

    private static Engine CreateEngine(List<byte[]> allocations)
    {
        var engine = new Engine(options => options.LimitMemory(SingleAllocationBudget));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));
        return engine;
    }

    private static Exception? RunOnNewThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();
        return failure;
    }

    private sealed class DelayedModuleLoader : IAsyncModuleLoader
    {
        private readonly Action _onLoad;
        private readonly ManualResetEventSlim _started = new();
        private ModuleLoadCompletion? _completion;

        public DelayedModuleLoader(Action onLoad) => _onLoad = onLoad;

        public bool Started { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The asynchronous loader path was expected.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            Started = true;
            _onLoad();
            _completion = completion;

            // Set last, so that a thread woken by this has a completion to settle.
            _started.Set();
        }

        /// <summary>
        /// Blocks until the engine has asked for the module, so that a host driving the load from another
        /// thread cannot reach <see cref="Complete"/> before there is a completion to settle.
        /// </summary>
        public void WaitUntilStarted() => _started
            .Wait(TimeSpan.FromSeconds(30))
            .Should().BeTrue("the engine never asked the loader for the module");

        public void Complete(string source) => _completion!.SetSource(source);
    }

    private sealed class ThrowOnSecondCheckConstraint : Constraint
    {
        private int _count;
        private bool _armed;

        internal void Arm()
        {
            _count = 0;
            _armed = true;
        }

        public override void Check()
        {
            if (_armed && ++_count == 2)
            {
                throw new InvalidOperationException("custom constraint");
            }
        }

        public override void Reset()
        {
            if (!_armed)
            {
                _count = 0;
            }
        }
    }

#if NET8_0_OR_GREATER
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        internal void Advance(int milliseconds)
            => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }
#endif
}
