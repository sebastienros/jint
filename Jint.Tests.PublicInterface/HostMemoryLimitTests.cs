#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Modules;
#if NET8_0_OR_GREATER
using Jint.WebApi;
#endif

using Module = Jint.Runtime.Modules.Module;

namespace Jint.Tests.PublicInterface;

public class HostMemoryLimitTests
{
    private const string ConcurrentUseMessage = "*already in use by another thread or has an asynchronous operation in progress*";
    private const int AllocationSize = 2_000_000;
    private const int SingleAllocationBudget = 3_500_000;

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

        var pending = engine.EvaluateAsync("""
            (async () => {
                allocate();
                return await schedule(() => {
                    allocate();
                    return 42;
                });
            })()
            """);

        var exception = await Record.ExceptionAsync(() => pending);
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
    public void PromiseContinuationRetainsItsOriginatingBudgetWhenPumpedOnAnotherThread()
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

        var failure = RunOnNewThread(() =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                engine.Advanced.ProcessTasks();
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

        Invoking(engine.Advanced.ProcessTasks).Should().ThrowExactly<MemoryLimitExceededException>();
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

        Invoking(engine.Advanced.ProcessTasks).Should().ThrowExactly<MemoryLimitExceededException>();
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

        Invoking(listener.Advanced.ProcessTasks).Should().NotThrow();
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

        Invoking(engine.Advanced.ProcessTasks).Should().ThrowExactly<MemoryLimitExceededException>();
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
            options.EnableModules(loader);
        });
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[AllocationSize])));

        var import = engine.Modules.StartImport("module");
        import.IsCompleted.Should().BeFalse();

        loader.Complete("export const value = allocate();");

        var failure = RunOnNewThread(engine.Advanced.ProcessTasks);

        failure.Should().BeOfType<MemoryLimitExceededException>();
    }

    [Fact]
    public void SynchronousImportDoesNotChargeAsyncLoaderWaitToExecutionTimeout()
    {
        var loader = new DelayedModuleLoader(() => { });
        var engine = new Engine(options =>
        {
            options.LimitMemory(SingleAllocationBudget);
            options.TimeoutInterval(TimeSpan.FromMilliseconds(200));
            options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(5);
            options.EnableModules(loader);
        });

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            loader.Complete("export const value = 42;");
        });

        engine.Modules.Import("module").Get("value").AsNumber().Should().Be(42);
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
            options.MaxStatements(100_000);
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
            .Constraint(custom)
            .Constraint(new MemoryLimitConstraint(1_000_000));
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
        var running = Task.Run(() => engine.Execute("block()"));

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
            options.EnableModules(loader);
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
            options.EnableModules(loader);
        });
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait();
        }));
        var constraint = engine.Constraints.Find<MemoryLimitConstraint>()!;
        constraint.Begin();
        var running = Task.Run(() => engine.Execute("block()"));

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
        idlePromise = engine.Advanced.RegisterPromise();
        engine.SetValue("allocate", new Action<int>(size => allocations.Add(new byte[size])));
        engine.SetValue("drainOld", new Action(() =>
        {
            try
            {
                engine.Advanced.ProcessTasks();
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

        Invoking(engine.Advanced.ProcessTasks).Should().NotThrow();
        callbackRan.Should().BeFalse();
    }

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
        var options = new Options().Constraint(first).Constraint(duplicate);

        Invoking(() => new Engine(options))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Only one MemoryLimitConstraint*");

        options.WithoutConstraint(constraint => ReferenceEquals(constraint, duplicate));
        new Engine(options).Constraints.Find<MemoryLimitConstraint>().Should().BeSameAs(first);
    }

    [Fact]
    public void ADirectMemoryConstraintInstanceCannotBeSharedAcrossEngines()
    {
        var constraint = new MemoryLimitConstraint(SingleAllocationBudget);
        var options = new Options().Constraint(constraint);
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
        private ModuleLoadCompletion? _completion;

        public DelayedModuleLoader(Action onLoad) => _onLoad = onLoad;

        public bool Started { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The asynchronous loader path was expected.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            Started = true;
            _onLoad();
            _completion = completion;
        }

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
