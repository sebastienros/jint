#nullable enable

using Jint.Constraints;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Modules;

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
}
