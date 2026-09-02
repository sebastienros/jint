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
    /// <summary>
    /// The suite's baseline profile, as a value rather than as a factory. The limits are named properties,
    /// so a case that needs one dimension changed writes <c>Fixture with { MaxArraySize = 2 }</c> instead of
    /// threading another optional parameter through a wrapper whose only job was to keep eight positional
    /// arguments in the right order.
    /// </summary>
    private static readonly UntrustedCodeLimits Fixture = new()
    {
        TimeoutInterval = TimeSpan.FromSeconds(5),
        MaxStatements = 100_000,
        MemoryLimit = 16_000_000,
        MaxRecursionDepth = 64,
        MaxArraySize = 10_000,
        RegexTimeout = TimeSpan.FromMilliseconds(100),
        PromiseTimeout = TimeSpan.FromMilliseconds(100),
        MaxOperationDuration = TimeSpan.FromSeconds(10),
    };

    [Test]
    public void ProfileOverridesPreviouslyEnabledStaticCapabilities()
    {
        var limits = Fixture;
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

    /// <summary>
    /// Hardening is the first expansion, not the last: it runs before the realm and the host exist, so a
    /// profile that first appears inside a configuration callback could only half-harden and is refused
    /// instead. https://github.com/sebastienros/jint/issues/3582
    /// </summary>
    [Test]
    public void ProfileDeclaredFromADeferredCallbackIsRefusedRatherThanHalfApplied()
    {
        var options = new Options();
        options.Configure(_ => options.ForUntrustedCode(Fixture));

        Invoking(() => new Engine(options))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*ForUntrustedCode*before the engine is constructed*");
    }

    [Test]
    public void ProfileReplacedFromADeferredCallbackIsRefusedRatherThanHalfApplied()
    {
        var options = new Options().ForUntrustedCode(Fixture);
        options.Configure(engine => engine.Options.ForUntrustedCode(Fixture with { MaxStatements = 1 }));

        Invoking(() => new Engine(options))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProfileIsReappliedAfterEarlierDeferredConfiguration()
    {
        var limits = Fixture;
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

            // The engine's own constraint instances do not exist yet, and reaching for them says so
            // rather than handing back something a callback could loosen - they are built from the
            // options below once every callback has run, i.e. after this profile is re-expanded.
            Invoking(() => engine.Constraints.Find<MaxStatementsConstraint>())
                .Should().Throw<InvalidOperationException>();
            Invoking(engine.Constraints.Check).Should().Throw<InvalidOperationException>();

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

    [Test]
    public void SharedOptionsGiveEachEngineItsOwnOperationDeadline()
    {
        var options = new Options().ForUntrustedCode(Fixture);
        var first = new Engine(options);
        var second = new Engine(options);

        first.Constraints.Find<OperationDeadlineConstraint>().Should().NotBeSameAs(
            second.Constraints.Find<OperationDeadlineConstraint>());
    }

    [Test]
    public async Task ConcurrentConstructionNeverMutatesSharedOptionsOrReopensAnExistingEngine()
    {
        var limits = Fixture;
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

    [Test]
    public void ProfileSettingsAreObservedByScriptAndProjectedObjects()
    {
        var host = new WritableHost();
        var array = new[] { 1, 2, 3 };
        var engine = new Engine(options => options.ForUntrustedCode(Fixture))
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

    [Test]
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
        options.ForUntrustedCode(Fixture);
        var engine = new Engine(options).SetValue("host", new OperatorHost());

        engine.Evaluate("host + host");

        factoryInvoked.Should().BeFalse();
        OperatorHost.InvocationCount.Should().Be(0);
    }

    [Test]
    public void ProfileDoesNotExposeNonPublicClrMembers()
    {
        var options = new Options();
        options.Interop.ObjectWrapperReportedFieldBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Interop.ObjectWrapperReportedPropertyBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.Interop.ObjectWrapperReportedMethodBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        options.ForUntrustedCode(Fixture);
        var engine = new Engine(options).SetValue("host", new PrivateHost());

        engine.Evaluate("host.Secret").IsUndefined().Should().BeTrue();
        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void CopiedClrArraysRespectTheProfileArrayLimit()
    {
        var engine = new Engine(options => options.ForUntrustedCode(Fixture with { MaxArraySize = 2 }));

        Invoking(() => engine.SetValue("array", new[] { 1, 2, 3 }))
            .Should().Throw<MemoryLimitExceededException>()
            .WithMessage("*larger than maximum allowed*2*");
    }

    [Test]
    public void PerEntryTimeoutIsEnforced()
    {
        var limits = Fixture with { TimeoutInterval = TimeSpan.FromMilliseconds(1) };
        var engine = new Engine(options => options.ForUntrustedCode(limits))
            .SetValue("pause", new Action(() => Thread.Sleep(TimeSpan.FromMilliseconds(25))));

        Invoking(() => engine.Evaluate("pause(); 1;")).Should().Throw<TimeoutException>();
    }

    [Test]
    public void OperationScopeEnforcesAndThenDisarmsTheMultiEntryDeadline()
    {
        var limits = Fixture with { MaxOperationDuration = TimeSpan.FromMilliseconds(1) };
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

    [Test]
    public void NestedOperationScopeIsRejectedWithoutDisarmingTheOuterScope()
    {
        var limits = Fixture with { MaxOperationDuration = TimeSpan.FromMilliseconds(1) };
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

    [Test]
    public async Task ConcurrentOperationScopesCannotBothAcquireTheDeadline()
    {
        var limits = Fixture;
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

    [Test]
    public async Task OperationScopeCannotEndWhileAsyncWorkOwnsTheEngine()
    {
        // Every wall-clock limit here is a wedge ceiling rather than part of the claim: what is asserted is
        // that a scope refuses to end while an evaluation is suspended, and then ends once it resumes. The
        // profile's own five-second timeout interval and ten-second operation deadline are budgets an
        // embedder would size for their own work, and leaving them at those defaults made this test fail on
        // a loaded runner as a TimeoutException from the gate below rather than as anything about scopes.
        var limits = Fixture with
        {
            TimeoutInterval = TestBudgets.WedgeCeiling,
            PromiseTimeout = TestBudgets.WedgeCeiling,
            MaxOperationDuration = TestBudgets.WedgeCeiling,
        };
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

    [Test]
    public void OperationScopeFailsClosedWhenTheProfileWasNotInstalled()
    {
        var limits = Fixture;

        using var cancellation = new CancellationTokenSource();
        Invoking(() => limits.BeginOperation(new Engine(), cancellation.Token))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*ForUntrustedCode*");
    }

    [Test]
    public void OperationScopeRejectsAGeneralPurposeDeadlineWithoutTheProfile()
    {
        var limits = Fixture;
        var engine = new Engine(options =>
            options.AddConstraint(static () => new OperationDeadlineConstraint()));

        using var cancellation = new CancellationTokenSource();
        Invoking(() => limits.BeginOperation(engine, cancellation.Token))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*ForUntrustedCode*");
    }

    [Test]
    public void OperationScopeRejectsADifferentLimitsInstance()
    {
        var configuredLimits = Fixture;
        // A value-equal copy is still a different object, and the scope wants the one the engine holds.
        var otherLimits = Fixture with { MaxStatements = Fixture.MaxStatements };
        otherLimits.Should().NotBeSameAs(configuredLimits);
        otherLimits.Should().Be(configuredLimits);
        var engine = new Engine(options => options.ForUntrustedCode(configuredLimits));

        using var cancellation = new CancellationTokenSource();
        Invoking(() => otherLimits.BeginOperation(engine, cancellation.Token))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*this UntrustedCodeLimits instance*");
    }

    [Test]
    public void RejectsNonPositiveLimits()
    {
        AssertInvalid(() => Fixture with { TimeoutInterval = TimeSpan.Zero }, nameof(UntrustedCodeLimits.TimeoutInterval));
        AssertInvalid(() => Fixture with { MaxStatements = 0 }, nameof(UntrustedCodeLimits.MaxStatements));
        AssertInvalid(() => Fixture with { MemoryLimit = 0 }, nameof(UntrustedCodeLimits.MemoryLimit));
        AssertInvalid(() => Fixture with { MaxRecursionDepth = -1 }, nameof(UntrustedCodeLimits.MaxRecursionDepth));
        AssertInvalid(() => Fixture with { MaxArraySize = 0 }, nameof(UntrustedCodeLimits.MaxArraySize));
        AssertInvalid(() => Fixture with { RegexTimeout = TimeSpan.Zero }, nameof(UntrustedCodeLimits.RegexTimeout));
        AssertInvalid(() => Fixture with { PromiseTimeout = TimeSpan.Zero }, nameof(UntrustedCodeLimits.PromiseTimeout));
        AssertInvalid(() => Fixture with { MaxOperationDuration = TimeSpan.Zero }, nameof(UntrustedCodeLimits.MaxOperationDuration));
    }

    [Test]
    public void RejectsSaturatedUnlimitedLimits()
    {
        AssertInvalid(() => Fixture with { TimeoutInterval = TimeSpan.MaxValue }, nameof(UntrustedCodeLimits.TimeoutInterval));
        AssertInvalid(() => Fixture with { MaxStatements = int.MaxValue }, nameof(UntrustedCodeLimits.MaxStatements));
        AssertInvalid(() => Fixture with { MemoryLimit = long.MaxValue }, nameof(UntrustedCodeLimits.MemoryLimit));
        AssertInvalid(() => Fixture with { MaxRecursionDepth = int.MaxValue }, nameof(UntrustedCodeLimits.MaxRecursionDepth));
        AssertInvalid(() => Fixture with { MaxArraySize = uint.MaxValue }, nameof(UntrustedCodeLimits.MaxArraySize));
        AssertInvalid(() => Fixture with { RegexTimeout = TimeSpan.MaxValue }, nameof(UntrustedCodeLimits.RegexTimeout));
        AssertInvalid(() => Fixture with { PromiseTimeout = TimeSpan.MaxValue }, nameof(UntrustedCodeLimits.PromiseTimeout));
        AssertInvalid(() => Fixture with { MaxOperationDuration = TimeSpan.MaxValue }, nameof(UntrustedCodeLimits.MaxOperationDuration));
    }

    [Test]
    public void ProfileRequiresLimits()
    {
        Action act = () => new Options().ForUntrustedCode(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("limits");
    }

    [Test]
    public void RejectsUnboundedResultLimits()
    {
        Invoking(() => Fixture with { ResultLimits = ResultLimits.Unlimited })
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(UntrustedCodeLimits.ResultLimits));
    }

    [Test]
    public void EffectiveDiagnosticsDoNotRetainProfileState()
    {
        var (limits, options, engine, report) = CreateCollectibleProfileReport();

        ForceCollection();

        limits.IsAlive.Should().BeFalse();
        options.IsAlive.Should().BeFalse();
        engine.IsAlive.Should().BeFalse();
        report.Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void OperationScopeRequiresACancellableToken()
    {
        var limits = Fixture;
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        Invoking(() => limits.BeginOperation(engine, CancellationToken.None))
            .Should().Throw<ArgumentException>()
            .WithParameterName("cancellationToken");
    }

    [Test]
    public void OperationScopeSpansEvaluationConversionSerializationAndErrorRendering()
    {
        var limits = Fixture;
        using var cancellation = new CancellationTokenSource();
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        using (limits.BeginOperation(engine, cancellation.Token))
        {
            var value = engine.Evaluate("({ answer: 42 })");
            engine.ConvertResult(value).Should().NotBeNull();
            new JsonSerializer(engine).Serialize(value).AsString().Should().Be("""{"answer":42}""");

            var exception = Invoking(() => engine.Evaluate("throw new Error('safe')"))
                .Should().ThrowExactly<JavaScriptException>().Which;
            exception.GetJavaScriptErrorString(limits.ResultLimits).Should().Contain("safe");
        }
    }

    [Test]
    public void OperationScopeCancellationIsHostVisibleAndCleansUp()
    {
        var limits = Fixture;
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

    [Test]
    public void ProfileDiagnosticsAreExactAndUserCallbacksRemainVisible()
    {
        var cleanOptions = new Options().ForUntrustedCode(Fixture);
        cleanOptions.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
        new Engine(cleanOptions).Diagnostics.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();

        var callbackOptions = new Options();
        callbackOptions.Configure(static _ => { });
        callbackOptions.ForUntrustedCode(Fixture);
        var report = new Engine(callbackOptions).Diagnostics.ValidateSecurityConfiguration();

        report.Diagnostics.Select(static diagnostic => diagnostic.Code)
            .Should().Equal(SecurityDiagnosticCodes.EngineConstructionCallback);
    }

    [Test]
    public void ProfileBoundsParserAndResults()
    {
        var limits = Fixture with
        {
            MaxSourceLength = 30,
            MaxNodeCount = 100,
            ResultLimits = new ResultLimits
            {
                MaxDepth = 2,
                MaxPropertyCount = 1,
                MaxStringLength = 5,
                MaxOutputCharacters = 10,
                MaxOutputBytes = 10,
            },
        };
        var engine = new Engine(options => options.ForUntrustedCode(limits));

        Invoking(() => engine.Evaluate($"'{new string('1', 31)}'"))
            .Should().Throw<ParsingLimitException>();
        var value = engine.Evaluate("({ a: 1, b: 2 })");
        Invoking(() => engine.ConvertResult(value))
            .Should().Throw<ResultLimitExceededException>();
    }

    /// <summary>
    /// A preset stays a preset: <c>with</c> satisfies the required members, keeps every dimension it does
    /// not name, and leaves the preset itself alone.
    /// </summary>
    [Test]
    public void TheDefaultPresetIsAdjustedWithoutRestatingTheRest()
    {
        var preset = UntrustedCodeLimits.Default;
        var tuned = preset with { MaxStatements = 5_000 };

        tuned.MaxStatements.Should().Be(5_000);
        tuned.TimeoutInterval.Should().Be(preset.TimeoutInterval);
        tuned.MemoryLimit.Should().Be(preset.MemoryLimit);
        tuned.MaxRecursionDepth.Should().Be(preset.MaxRecursionDepth);
        tuned.MaxArraySize.Should().Be(preset.MaxArraySize);
        tuned.RegexTimeout.Should().Be(preset.RegexTimeout);
        tuned.PromiseTimeout.Should().Be(preset.PromiseTimeout);
        tuned.MaxOperationDuration.Should().Be(preset.MaxOperationDuration);
        tuned.MaxSourceLength.Should().Be(preset.MaxSourceLength);
        tuned.MaxNodeCount.Should().Be(preset.MaxNodeCount);
        tuned.MaxModuleCount.Should().Be(preset.MaxModuleCount);
        tuned.MaxTotalModuleSourceBytes.Should().Be(preset.MaxTotalModuleSourceBytes);
        tuned.MaxModuleGraphDepth.Should().Be(preset.MaxModuleGraphDepth);
        tuned.MaxModuleResolutionHops.Should().Be(preset.MaxModuleResolutionHops);
        tuned.ResultLimits.Should().BeSameAs(preset.ResultLimits);

        UntrustedCodeLimits.Default.MaxStatements.Should().Be(50_000);
        UntrustedCodeLimits.Default.Should().BeSameAs(preset);
    }

    /// <summary>
    /// The dimensions that carry a default carry the same numbers the positional constructor's optional
    /// parameters did, so a host that never named them is bounded exactly as before.
    /// </summary>
    /// <remarks>
    /// This row exists because nothing else would notice a weaker default: the value is not in any
    /// signature a compiler checks, and a looser limit fails no test by failing to fire.
    /// </remarks>
    [Test]
    public void TheDimensionsThatCarryDefaultsCarryTheOnesTheyAlwaysCarried()
    {
        var minimal = new UntrustedCodeLimits
        {
            TimeoutInterval = TimeSpan.FromSeconds(1),
            MaxStatements = 1,
            MemoryLimit = 1,
            MaxRecursionDepth = 0,
            MaxArraySize = 1,
            RegexTimeout = TimeSpan.FromMilliseconds(1),
            PromiseTimeout = TimeSpan.FromMilliseconds(1),
            MaxOperationDuration = TimeSpan.FromMilliseconds(1),
        };

        minimal.MaxSourceLength.Should().Be(1_000_000);
        minimal.MaxNodeCount.Should().Be(250_000);
        minimal.MaxModuleCount.Should().Be(100);
        minimal.MaxTotalModuleSourceBytes.Should().Be(10_000_000);
        minimal.MaxModuleGraphDepth.Should().Be(32);
        minimal.MaxModuleResolutionHops.Should().Be(1_000);
        minimal.ResultLimits.Should().BeSameAs(ResultLimits.Conservative);
    }

    /// <summary>
    /// An engine built from the preset still refuses everything the profile refuses, and enforces the
    /// preset's own budget.
    /// </summary>
    [Test]
    public void ThePresetProfileStillBoundsWhatTheProfileBounds()
    {
        var limits = UntrustedCodeLimits.Default with { MaxStatements = 500 };
        var options = new Options().AllowClr(typeof(UntrustedCodeProfileTests).Assembly);
        options.Host.StringCompilationAllowed = true;
        options.Interop.AllowWrite = true;
        options.ForUntrustedCode(limits);
        var engine = new Engine(options);

        Invoking(() => engine.Evaluate("var i = 0; while (true) { i++; }"))
            .Should().Throw<StatementsCountOverflowException>();
        engine.Evaluate("typeof System").AsString().Should().Be("undefined");
        Invoking(() => engine.Evaluate("eval('1 + 1')"))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage("String compilation has been disabled in engine options");

        engine.Options.Constraints.MaxRecursionDepth.Should().Be(64);
        engine.Options.Constraints.MaxArraySize.Should().Be(10_000u);
        engine.Options.Constraints.RegexTimeout.Should().Be(TimeSpan.FromMilliseconds(250));
        engine.Options.ResultLimits.Should().BeSameAs(ResultLimits.Conservative);
        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
    }

    private static void AssertInvalid(Func<UntrustedCodeLimits> create, string parameterName)
    {
        create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Limits, WeakReference Options, WeakReference Engine, SecurityConfigurationReport Report)
        CreateCollectibleProfileReport()
    {
        // A fresh instance rather than the shared Fixture, which a static field would keep alive.
        var limits = Fixture with { MaxStatements = 99_999 };
        var options = new Options().ForUntrustedCode(limits);
        var engine = new Engine(options);
        var report = engine.Diagnostics.ValidateSecurityConfiguration();
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
