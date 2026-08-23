#nullable enable

using Jint.Constraints;
using Jint.Native.Json;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins the complete untrusted-code profile from the same public surface available to an embedder.
/// </summary>
public class UntrustedCodeProfileTests
{
    [Fact]
    public void ProfileOverridesPreviouslyEnabledStaticCapabilities()
    {
        var limits = CreateLimits();
        var options = new Options()
            .AllowClr(typeof(UntrustedCodeProfileTests).Assembly)
            .AddExtensionMethods(typeof(UntrustedCodeProfileStringExtensions))
            .UseModules(Environment.CurrentDirectory);
        options.Interop.AllowWrite = true;
        options.Debugger.Enabled = true;
        options.Debugger.StatementHandling = DebuggerStatementHandling.Clr;
        options.Debugger.InitialStepMode = StepMode.Into;
        options.Host.StringCompilationAllowed = true;

        options.AgentCanSuspend = true;
        options.Interop.AllowGetType = true;
        options.Interop.AllowSystemReflection = true;
        options.Interop.AllowOperatorOverloading = true;
        options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        options.Interop.ExposeDetailedExceptionMessages = true;
        options.Interop.ExposeDetailedResolutionErrors = true;
        options.Interop.ChainClrExceptionAsInnerException = true;
        options.Interop.ObjectWrapperReportedFieldBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Interop.ObjectWrapperReportedPropertyBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Interop.ObjectWrapperReportedMethodBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Modules.ExposeDetailedLoadErrors = true;
        options.Constraints.MaxExecutionStackCount = 1;
        options.Constraints.StackOverflowGuard = false;

        options.ForUntrustedCode(limits);

        var engine = new Engine(options);

        // Declaring the profile never mutates the shared source options.
        options.Interop.AllowWrite.Should().BeTrue();
        options.Interop.AllowOperatorOverloading.Should().BeTrue();
        options.Modules.ExposeDetailedLoadErrors.Should().BeTrue();
        engine.Constraints.Find<MaxStatementsConstraint>()!.MaxStatements.Should().Be(limits.MaxStatements);
        engine.Constraints.Find<MemoryLimitConstraint>().Should().NotBeNull();
        engine.Constraints.Find<OperationDeadlineConstraint>().Should().NotBeNull();
    }

    [Fact]
    public void ProfileIsReappliedAfterEarlierDeferredConfiguration()
    {
        var limits = CreateLimits();
        var options = new Options();
        options.Configure(engine =>
        {
            options.AllowClr(typeof(UntrustedCodeProfileTests).Assembly);
            options.Interop.AllowGetType = true;
            options.Interop.AllowSystemReflection = true;
            options.Interop.AllowWrite = true;
            options.Interop.AllowOperatorOverloading = true;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            options.Interop.ExposeDetailedExceptionMessages = true;
            options.Interop.ExposeDetailedResolutionErrors = true;
            options.Interop.ChainClrExceptionAsInnerException = true;
            options.AddExtensionMethods(typeof(UntrustedCodeProfileStringExtensions));
            options.Modules.RegisterRequire = true;
            options.Modules.ExposeDetailedLoadErrors = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Clr;
            options.Debugger.InitialStepMode = StepMode.Into;
            options.AgentCanSuspend = true;
            options.Host.StringCompilationAllowed = true;
            options.Constraints.MaxExecutionStackCount = 1;
            options.Constraints.StackOverflowGuard = false;
            options.Constraints.MaxRecursionDepth = int.MaxValue;
            options.Constraints.MaxArraySize = uint.MaxValue;
            options.Constraints.RegexTimeout = TimeSpan.MaxValue;
            options.Constraints.PromiseTimeout = TimeSpan.MaxValue;
            engine.Constraints.Find<MaxStatementsConstraint>()!.MaxStatements = int.MaxValue;
            engine.Constraints.Find<OperationDeadlineConstraint>()!.Begin(TimeSpan.MaxValue);
            engine.Constraints.Find<MemoryLimitConstraint>()!.Begin();
            engine.Modules.Add("configured", "export const reachable = true;");
        });
        options.ForUntrustedCode(limits);

        var engine = new Engine(options);

        engine.Constraints.Find<MaxStatementsConstraint>()!.MaxStatements.Should().Be(limits.MaxStatements);
        Invoking(engine.Constraints.Check).Should().NotThrow();
        engine.Constraints.Find<MemoryLimitConstraint>()!.IsOperationActive.Should().BeFalse();
        Invoking(() => engine.Modules.Import("configured"))
            .Should().Throw<InvalidOperationException>().WithMessage("*disabled*");
    }

    [Fact]
    public void SharedOptionsGiveEachEngineItsOwnOperationDeadline()
    {
        var options = new Options().ForUntrustedCode(CreateLimits());
        var first = new Engine(options);
        var second = new Engine(options);

        first.Constraints.Find<OperationDeadlineConstraint>().Should().NotBeSameAs(
            second.Constraints.Find<OperationDeadlineConstraint>());
    }

    [Fact]
    public async Task ConcurrentConstructionNeverMutatesSharedOptionsOrReopensAnExistingEngine()
    {
        var limits = CreateLimits();
        using var secondCallbackEntered = new ManualResetEventSlim(false);
        using var releaseSecondCallback = new ManualResetEventSlim(false);
        var callbackCount = 0;
        var options = new Options();
        options.Interop.AllowWrite = true;
        options.Configure(_ =>
        {
            if (Interlocked.Increment(ref callbackCount) == 2)
            {
                secondCallbackEntered.Set();
                releaseSecondCallback.Wait();
            }
        });
        options.ForUntrustedCode(limits);

        var first = new Engine(options).SetValue("host", new WritableHost());
        var secondTask = Task.Run(() => new Engine(options));
        secondCallbackEntered.Wait();

        options.Interop.AllowWrite.Should().BeTrue();
        Invoking(() => first.Execute("'use strict'; host.Value = 42;"))
            .Should().Throw<JavaScriptException>();

        releaseSecondCallback.Set();
        var second = await secondTask;
        second.SetValue("host", new WritableHost());
        Invoking(() => second.Execute("'use strict'; host.Value = 42;"))
            .Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void ProfileSettingsAreObservedByScriptAndProjectedObjects()
    {
        var host = new WritableHost();
        var array = new[] { 1, 2, 3 };
        var engine = new Engine(options => options.ForUntrustedCode(CreateLimits()))
            .SetValue("host", host)
            .SetValue("array", array);

        engine.Evaluate("typeof System").AsString().Should().Be("undefined");
        engine.Evaluate("typeof ''.UnsafeCapability").AsString().Should().Be("undefined");
        Invoking(() => engine.Modules.Import("blocked"))
            .Should().Throw<InvalidOperationException>().WithMessage("*disabled*");

        Invoking(() => engine.Execute("'use strict'; host.Value = 42;"))
            .Should().Throw<JavaScriptException>();
        host.Value.Should().Be(1);

        engine.Execute("array[0] = 42;");
        array[0].Should().Be(1);
        engine.Evaluate("Array.isArray(array)").AsBoolean().Should().BeTrue();

        Invoking(() => engine.Evaluate("eval('1 + 1')"))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage("String compilation has been disabled in engine options");
        Invoking(() => engine.Evaluate("new Function('return 1')"))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage("String compilation has been disabled in engine options");

        Invoking(() => engine.Evaluate(
                "Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 0)"))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage("Atomics.wait cannot be used in this agent");
    }

    [Fact]
    public void ProfileClearsHostFactoryAndOperatorCapabilities()
    {
        var factoryInvoked = false;
        OperatorHost.InvocationCount = 0;
        var options = new Options()
            .UseHostFactory(_ =>
            {
                factoryInvoked = true;
                return new Host();
            });
        options.Interop.AllowOperatorOverloading = true;
        options.ForUntrustedCode(CreateLimits());
        var engine = new Engine(options).SetValue("host", new OperatorHost());

        engine.Evaluate("host + host");

        factoryInvoked.Should().BeFalse();
        OperatorHost.InvocationCount.Should().Be(0);
    }

    [Fact]
    public void ProfileDoesNotExposeNonPublicClrMembers()
    {
        var options = new Options();
        options.Interop.ObjectWrapperReportedFieldBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Interop.ObjectWrapperReportedPropertyBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Interop.ObjectWrapperReportedMethodBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.ForUntrustedCode(CreateLimits());
        var engine = new Engine(options).SetValue("host", new PrivateHost());

        engine.Evaluate("host.Secret").IsUndefined().Should().BeTrue();
        engine.Advanced.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void CopiedClrArraysRespectTheProfileArrayLimit()
    {
        var engine = new Engine(options => options.ForUntrustedCode(CreateLimits(maxArraySize: 2)));

        Invoking(() => engine.SetValue("array", new[] { 1, 2, 3 }))
            .Should().Throw<MemoryLimitExceededException>()
            .WithMessage("*larger than maximum allowed*2*");
    }

    [Fact]
    public void PerEntryTimeoutIsEnforced()
    {
        var limits = CreateLimits(timeoutInterval: TimeSpan.FromMilliseconds(1));
        var engine = new Engine(options => options.ForUntrustedCode(limits))
            .SetValue("pause", new Action(() => Thread.Sleep(TimeSpan.FromMilliseconds(25))));

        Invoking(() => engine.Evaluate("pause(); 1;")).Should().Throw<TimeoutException>();
    }

    [Fact]
    public void OperationScopeEnforcesAndThenDisarmsTheMultiEntryDeadline()
    {
        var limits = CreateLimits(maxOperationDuration: TimeSpan.FromMilliseconds(1));
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        using var cancellation = new CancellationTokenSource();
        using (limits.BeginOperation(engine, cancellation.Token))
        {
            engine.Constraints.Find<MemoryLimitConstraint>()!.IsOperationActive.Should().BeTrue();
            Thread.Sleep(TimeSpan.FromMilliseconds(25));
            Invoking(engine.Constraints.Check).Should().Throw<TimeoutException>();
        }

        Invoking(engine.Constraints.Check).Should().NotThrow();
        engine.Constraints.Find<MemoryLimitConstraint>()!.IsOperationActive.Should().BeFalse();
    }

    [Fact]
    public void NestedOperationScopeIsRejectedWithoutDisarmingTheOuterScope()
    {
        var limits = CreateLimits(maxOperationDuration: TimeSpan.FromMilliseconds(1));
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        using var cancellation = new CancellationTokenSource();
        using (limits.BeginOperation(engine, cancellation.Token))
        {
            Invoking(() => limits.BeginOperation(engine, cancellation.Token))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*already active*");

            Thread.Sleep(TimeSpan.FromMilliseconds(25));
            Invoking(engine.Constraints.Check).Should().Throw<TimeoutException>();
        }

        Invoking(engine.Constraints.Check).Should().NotThrow();
    }

    [Fact]
    public async Task ConcurrentOperationScopesCannotBothAcquireTheDeadline()
    {
        var limits = CreateLimits();
        var engine = new Engine(options => options.ForUntrustedCode(limits));
        using var cancellation = new CancellationTokenSource();
        using var start = new Barrier(2);
        using var attempted = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(false);
        var acquired = 0;
        var rejected = 0;

        Task Attempt()
        {
            return Task.Run(() =>
            {
                start.SignalAndWait();
                IDisposable? scope = null;
                try
                {
                    scope = limits.BeginOperation(engine, cancellation.Token);
                    Interlocked.Increment(ref acquired);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref rejected);
                }
                finally
                {
                    attempted.Signal();
                    if (scope is not null)
                    {
                        release.Wait();
                        scope.Dispose();
                    }
                }
            });
        }

        var first = Attempt();
        var second = Attempt();
        attempted.Wait();
        release.Set();
        await Task.WhenAll(first, second);

        acquired.Should().Be(1);
        rejected.Should().Be(1);
        Invoking(engine.Constraints.Check).Should().NotThrow();
    }

    [Fact]
    public async Task OperationScopeCannotEndWhileAsyncWorkOwnsTheEngine()
    {
        var limits = CreateLimits();
        using var cancellation = new CancellationTokenSource();
        // TaskCompletionSource<bool> rather than the non-generic one, which only exists from .NET 5 and
        // this project also compiles for net472.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new Engine(options => options.ForUntrustedCode(limits))
            .SetValue("gate", new Func<Task>(() => gate.Task));
        var scope = limits.BeginOperation(engine, cancellation.Token);
        var pending = engine.EvaluateAsync("(async () => { await gate(); return 42; })()");

        Invoking(scope.Dispose)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*asynchronous operation in progress*");

        gate.SetResult(true);
        (await pending).AsNumber().Should().Be(42);
        Invoking(scope.Dispose).Should().NotThrow();
        engine.Constraints.Find<MemoryLimitConstraint>()!.IsOperationActive.Should().BeFalse();
    }

    [Fact]
    public void OperationScopeFailsClosedWhenTheProfileWasNotInstalled()
    {
        var limits = CreateLimits();

        using var cancellation = new CancellationTokenSource();
        Invoking(() => limits.BeginOperation(new Engine(), cancellation.Token))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*ForUntrustedCode*");
    }

    [Fact]
    public void OperationScopeRejectsAGeneralPurposeDeadlineWithoutTheProfile()
    {
        var limits = CreateLimits();
        var engine = new Engine(options =>
            options.AddConstraint(static () => new OperationDeadlineConstraint()));

        using var cancellation = new CancellationTokenSource();
        Invoking(() => limits.BeginOperation(engine, cancellation.Token))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*ForUntrustedCode*");
    }

    [Fact]
    public void OperationScopeRejectsADifferentLimitsInstance()
    {
        var configuredLimits = CreateLimits();
        var otherLimits = CreateLimits();
        var engine = new Engine(options => options.ForUntrustedCode(configuredLimits));

        using var cancellation = new CancellationTokenSource();
        Invoking(() => otherLimits.BeginOperation(engine, cancellation.Token))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*this UntrustedCodeLimits instance*");
    }

    [Fact]
    public void RejectsNonPositiveLimits()
    {
        AssertInvalid(() => CreateLimits(timeoutInterval: TimeSpan.Zero), "timeoutInterval");
        AssertInvalid(() => CreateLimits(maxStatements: 0), "maxStatements");
        AssertInvalid(() => CreateLimits(memoryLimit: 0), "memoryLimit");
        AssertInvalid(() => CreateLimits(maxRecursionDepth: -1), "maxRecursionDepth");
        AssertInvalid(() => CreateLimits(maxArraySize: 0), "maxArraySize");
        AssertInvalid(() => CreateLimits(regexTimeout: TimeSpan.Zero), "regexTimeout");
        AssertInvalid(() => CreateLimits(promiseTimeout: TimeSpan.Zero), "promiseTimeout");
        AssertInvalid(() => CreateLimits(maxOperationDuration: TimeSpan.Zero), "maxOperationDuration");
    }

    [Fact]
    public void RejectsSaturatedUnlimitedLimits()
    {
        AssertInvalid(() => CreateLimits(timeoutInterval: TimeSpan.MaxValue), "timeoutInterval");
        AssertInvalid(() => CreateLimits(maxStatements: int.MaxValue), "maxStatements");
        AssertInvalid(() => CreateLimits(memoryLimit: long.MaxValue), "memoryLimit");
        AssertInvalid(() => CreateLimits(maxRecursionDepth: int.MaxValue), "maxRecursionDepth");
        AssertInvalid(() => CreateLimits(maxArraySize: uint.MaxValue), "maxArraySize");
        AssertInvalid(() => CreateLimits(regexTimeout: TimeSpan.MaxValue), "regexTimeout");
        AssertInvalid(() => CreateLimits(promiseTimeout: TimeSpan.MaxValue), "promiseTimeout");
        AssertInvalid(() => CreateLimits(maxOperationDuration: TimeSpan.MaxValue), "maxOperationDuration");
    }

    [Fact]
    public void ProfileRequiresLimits()
    {
        Action act = () => new Options().ForUntrustedCode(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("limits");
    }

    [Fact]
    public void RejectsUnboundedResultLimits()
    {
        Invoking(() => new UntrustedCodeLimits(
                TimeSpan.FromSeconds(1),
                1_000,
                1_000_000,
                16,
                1_000,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(2),
                resultLimits: ResultLimits.Unlimited))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("resultLimits");
    }

    [Fact]
    public void EffectiveDiagnosticsDoNotRetainProfileState()
    {
        var (limits, options, engine, report) = CreateCollectibleProfileReport();

        ForceCollection();

        limits.IsAlive.Should().BeFalse();
        options.IsAlive.Should().BeFalse();
        engine.IsAlive.Should().BeFalse();
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void OperationScopeRequiresACancellableToken()
    {
        var limits = CreateLimits();
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        Invoking(() => limits.BeginOperation(engine, CancellationToken.None))
            .Should().Throw<ArgumentException>()
            .WithParameterName("cancellationToken");
    }

    [Fact]
    public void OperationScopeSpansEvaluationConversionSerializationAndErrorRendering()
    {
        var limits = CreateLimits();
        using var cancellation = new CancellationTokenSource();
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        using (limits.BeginOperation(engine, cancellation.Token))
        {
            var value = engine.Evaluate("({ answer: 42 })");
            engine.Advanced.ConvertResult(value).Should().NotBeNull();
            new JsonSerializer(engine).Serialize(value).AsString().Should().Be("""{"answer":42}""");

            var exception = Invoking(() => engine.Evaluate("throw new Error('safe')"))
                .Should().ThrowExactly<JavaScriptException>().Which;
            exception.GetJavaScriptErrorString(limits.ResultLimits).Should().Contain("safe");
        }
    }

    [Fact]
    public void OperationScopeCancellationIsHostVisibleAndCleansUp()
    {
        var limits = CreateLimits();
        using var cancellation = new CancellationTokenSource();
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        using (limits.BeginOperation(engine, cancellation.Token))
        {
            cancellation.Cancel();
            Invoking(engine.Constraints.Check)
                .Should().Throw<OperationCanceledException>()
                .Which.CancellationToken.Should().Be(cancellation.Token);
        }

        engine.Constraints.Find<MemoryLimitConstraint>()!.IsOperationActive.Should().BeFalse();
    }

    [Fact]
    public void ProfileDiagnosticsAreExactAndUserCallbacksRemainVisible()
    {
        var cleanOptions = new Options().ForUntrustedCode(CreateLimits());
        cleanOptions.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
        new Engine(cleanOptions).Advanced.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();

        var callbackOptions = new Options();
        callbackOptions.Configure(static _ => { });
        callbackOptions.ForUntrustedCode(CreateLimits());
        var report = new Engine(callbackOptions).Advanced.ValidateSecurityConfiguration();

        report.Diagnostics.Select(static diagnostic => diagnostic.Code)
            .Should().Equal(SecurityDiagnosticCodes.EngineConstructionCallback);
    }

    [Fact]
    public void ProfileBoundsParserAndResults()
    {
        var limits = new UntrustedCodeLimits(
            TimeSpan.FromSeconds(5),
            100_000,
            16_000_000,
            64,
            10_000,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(10),
            maxSourceLength: 30,
            maxNodeCount: 100,
            resultLimits: new ResultLimits(
                maxDepth: 2,
                maxPropertyCount: 1,
                maxStringLength: 5,
                maxOutputCharacters: 10,
                maxOutputBytes: 10));
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        Invoking(() => engine.Evaluate($"'{new string('1', 31)}'"))
            .Should().Throw<ParsingLimitException>();
        var value = engine.Evaluate("({ a: 1, b: 2 })");
        Invoking(() => engine.Advanced.ConvertResult(value))
            .Should().Throw<ResultLimitExceededException>();
    }

    private static UntrustedCodeLimits CreateLimits(
        TimeSpan? timeoutInterval = null,
        int? maxStatements = null,
        long? memoryLimit = null,
        int? maxRecursionDepth = null,
        uint? maxArraySize = null,
        TimeSpan? regexTimeout = null,
        TimeSpan? promiseTimeout = null,
        TimeSpan? maxOperationDuration = null)
    {
        return new UntrustedCodeLimits(
            timeoutInterval ?? TimeSpan.FromSeconds(5),
            maxStatements ?? 100_000,
            memoryLimit ?? 16_000_000,
            maxRecursionDepth ?? 64,
            maxArraySize ?? 10_000,
            regexTimeout ?? TimeSpan.FromMilliseconds(100),
            promiseTimeout ?? TimeSpan.FromMilliseconds(100),
            maxOperationDuration ?? TimeSpan.FromSeconds(10));
    }

    private static void AssertInvalid(Func<UntrustedCodeLimits> create, string parameterName)
    {
        create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Limits, WeakReference Options, WeakReference Engine, SecurityConfigurationReport Report)
        CreateCollectibleProfileReport()
    {
        var limits = CreateLimits();
        var options = new Options().ForUntrustedCode(limits);
        var engine = new Engine(options);
        var report = engine.Advanced.ValidateSecurityConfiguration();
        return (new WeakReference(limits), new WeakReference(options), new WeakReference(engine), report);
    }

    private static void ForceCollection()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private sealed class WritableHost
    {
        public int Value { get; set; } = 1;
    }

    private sealed class OperatorHost
    {
        public static int InvocationCount { get; set; }

        public static OperatorHost operator +(OperatorHost left, OperatorHost right)
        {
            InvocationCount++;
            return left;
        }

    }

    private sealed class PrivateHost
    {
        private string Secret => "secret";
    }
}

internal static class UntrustedCodeProfileStringExtensions
{
    public static string UnsafeCapability(this string value) => value;
}
