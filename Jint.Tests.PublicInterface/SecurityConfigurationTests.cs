#nullable enable

using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Constraints;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

public class SecurityConfigurationTests
{
    [Test]
    public void CleanHandBuiltOptionsHaveNoDiagnostics()
    {
        var options = CreateSafeOptions();

        var report = options.ValidateSecurityConfiguration(SecurityConfigurationPolicy.UntrustedScripts);

        report.Diagnostics.Should().BeEmpty();
        report.HasErrors.Should().BeFalse();
        report.HasWarnings.Should().BeFalse();
        options.EnsureSecurityConfiguration().Should().BeSameAs(options);

        Invoking(() => new Engine(options.EnsureSecurityConfiguration())).Should().NotThrow();
    }

    [Test]
    public void EverySecurityConditionHasAStableDiagnostic()
    {
        var cases = new (string Code, Func<Options> CreateOptions)[]
        {
            (SecurityDiagnosticCodes.StatementLimitMissing, CreateWithoutStatementLimit),
            (SecurityDiagnosticCodes.StatementLimitNonPositive, () => CreateSafeOptions().LimitStatements(0)),
            (SecurityDiagnosticCodes.StatementLimitSaturated, () => CreateSafeOptions().LimitStatements(int.MaxValue)),
            (SecurityDiagnosticCodes.MemoryLimitMissing, CreateWithoutMemoryLimit),
            (SecurityDiagnosticCodes.MemoryLimitNonPositive, () => CreateSafeOptions().LimitMemory(0)),
            (SecurityDiagnosticCodes.MemoryLimitSaturated, () => CreateSafeOptions().LimitMemory(long.MaxValue)),
            (SecurityDiagnosticCodes.TimeoutMissing, CreateWithoutTimeout),
            (SecurityDiagnosticCodes.TimeoutNonPositive, () => CreateSafeOptions().LimitExecutionTime(TimeSpan.Zero)),
            (SecurityDiagnosticCodes.TimeoutSaturated, () => CreateSafeOptions().LimitExecutionTime(TimeSpan.MaxValue)),
            (SecurityDiagnosticCodes.RecursionLimitDisabled, () => Change(CreateSafeOptions(), x => x.Constraints.MaxRecursionDepth = -1)),
            (SecurityDiagnosticCodes.RecursionLimitSaturated, () => Change(CreateSafeOptions(), x => x.Constraints.MaxRecursionDepth = int.MaxValue)),
            (SecurityDiagnosticCodes.StackOverflowGuardDisabled, () => Change(CreateSafeOptions(), x => x.Constraints.StackOverflowGuard = false)),
            (SecurityDiagnosticCodes.StackOverflowGuardShadowed, () => Change(CreateSafeOptions(), x => x.Constraints.MaxExecutionStackCount = 100)),
            (SecurityDiagnosticCodes.AgentSuspensionEnabled, () => Change(CreateSafeOptions(), x => x.AgentCanSuspend = true)),
            (SecurityDiagnosticCodes.ClrAccessEnabled, CreateWithRestrictiveClr),
            (SecurityDiagnosticCodes.GetTypeEnabled, () => Change(CreateSafeOptions(), x => x.Interop.AllowGetType = true)),
            (SecurityDiagnosticCodes.SystemReflectionEnabled, () => Change(CreateSafeOptions(), x => x.Interop.AllowSystemReflection = true)),
            (SecurityDiagnosticCodes.ClrWriteEnabled, () => Change(CreateSafeOptions(), x => x.Interop.AllowWrite = true)),
            (SecurityDiagnosticCodes.LiveClrArraysEnabled, () => Change(CreateSafeOptions(), x => x.Interop.ArrayConversion = ArrayConversionMode.LiveView)),
            (SecurityDiagnosticCodes.StringCompilationEnabled, () => Change(CreateSafeOptions(), x => x.Host.StringCompilationAllowed = true)),
            (SecurityDiagnosticCodes.ModuleLoadingEnabled, () => CreateSafeOptions().UseModules(new EmptyModuleLoader())),
            (SecurityDiagnosticCodes.RequireWithoutModuleLoader, () => Change(CreateSafeOptions(), x => x.Modules.RegisterRequire = true)),
            (SecurityDiagnosticCodes.DebuggerEnabled, () => Change(CreateSafeOptions(), x => x.Debugger.Enabled = true)),
            (SecurityDiagnosticCodes.ArraySizeUnlimited, () => Change(CreateSafeOptions(), x => x.Constraints.MaxArraySize = uint.MaxValue)),
            (SecurityDiagnosticCodes.RegexTimeoutUnbounded, () => Change(CreateSafeOptions(), x => x.Constraints.RegexTimeout = TimeSpan.Zero)),
            (SecurityDiagnosticCodes.RegexTimeoutLong, () => Change(CreateSafeOptions(), x => x.Constraints.RegexTimeout = TimeSpan.FromSeconds(2))),
            (SecurityDiagnosticCodes.PromiseTimeoutUnbounded, () => Change(CreateSafeOptions(), x => x.Constraints.PromiseTimeout = TimeSpan.Zero)),
            (SecurityDiagnosticCodes.PromiseTimeoutLong, () => Change(CreateSafeOptions(), x => x.Constraints.PromiseTimeout = TimeSpan.FromSeconds(2))),
            (SecurityDiagnosticCodes.SharedConstraintInstance, () => CreateSafeOptions().AddConstraint(new NoOpConstraint())),
            (SecurityDiagnosticCodes.DetailedClrErrorsEnabled, () => Change(CreateSafeOptions(), x => x.Interop.ExposeDetailedResolutionErrors = true)),
            (SecurityDiagnosticCodes.EngineConstructionCallback, () => CreateSafeOptions().Configure(static _ => { })),
            (SecurityDiagnosticCodes.RegexTimeoutExcessive, () => Change(CreateSafeOptions(), x => x.Constraints.RegexTimeout = TimeSpan.MaxValue)),
            (SecurityDiagnosticCodes.CancellationMissing, CreateWithoutCancellation),
            (SecurityDiagnosticCodes.OperationDeadlineMissing, CreateWithoutOperationDeadline),
            (SecurityDiagnosticCodes.ParserSourceLengthUnlimited, () => Change(CreateSafeOptions(), x => x.Parsing.MaxSourceLength = null)),
            (SecurityDiagnosticCodes.ParserNodeCountUnlimited, () => Change(CreateSafeOptions(), x => x.Parsing.MaxNodeCount = null)),
            (SecurityDiagnosticCodes.FunctionSourceRetentionEnabled, () => Change(CreateSafeOptions(), x => x.RetainFunctionSourceText = true)),
            (SecurityDiagnosticCodes.ModuleCountUnlimited, () => Change(CreateSafeModuleOptions(), x => x.Modules.MaxModuleCount = int.MaxValue)),
            (SecurityDiagnosticCodes.ModuleSourceBytesUnlimited, () => Change(CreateSafeModuleOptions(), x => x.Modules.MaxTotalModuleSourceBytes = long.MaxValue)),
            (SecurityDiagnosticCodes.ModuleGraphDepthUnlimited, () => Change(CreateSafeModuleOptions(), x => x.Modules.MaxModuleGraphDepth = int.MaxValue)),
            (SecurityDiagnosticCodes.ModuleResolutionHopsUnlimited, () => Change(CreateSafeModuleOptions(), x => x.Modules.MaxModuleResolutionHops = int.MaxValue)),
            (SecurityDiagnosticCodes.ModuleDestinationPolicyMissing, () => Change(CreateSafeModuleOptions(), x => x.Modules.LoadPolicy = null)),
            (SecurityDiagnosticCodes.ModulePerLoadLimitsReset, CreateSafeModuleOptions),
            (SecurityDiagnosticCodes.ResultLimitsUnlimited, () => Change(CreateSafeOptions(), x => x.ResultLimits = ResultLimits.Unlimited)),
            (SecurityDiagnosticCodes.DetailedClrExceptionMessagesEnabled, () => Change(CreateSafeOptions(), x => x.Interop.ExposeDetailedExceptionMessages = true)),
            (SecurityDiagnosticCodes.ClrExceptionChainingEnabled, () => Change(CreateSafeOptions(), x => x.Interop.ChainClrExceptionAsInnerException = true)),
            (SecurityDiagnosticCodes.DetailedModuleErrorsEnabled, () => Change(CreateSafeModuleOptions(), x => x.Modules.ExposeDetailedLoadErrors = true)),
            (SecurityDiagnosticCodes.ClrErrorDecoratorConfigured, () => Change(CreateSafeOptions(), x => x.Interop.ClrExceptionErrorDecorator = static (_, _, _) => { })),
            (SecurityDiagnosticCodes.ClrCallbackConfigured, () => Change(CreateSafeOptions(), x => x.Interop.ExceptionHandler = static _ => false)),
            (SecurityDiagnosticCodes.ClrCompatibilityModeEnabled, () => CreateSafeOptions().AllowClr()),
            (SecurityDiagnosticCodes.ClrPolicyUnrestricted, () => CreateSafeOptions().AllowClr(Array.Empty<System.Reflection.Assembly>())),
            (SecurityDiagnosticCodes.LiveClrArrayWritesEnabled, () => Change(Change(CreateSafeOptions(), x => x.Interop.AllowWrite = true), x => x.Interop.ArrayConversion = ArrayConversionMode.LiveView)),
            (SecurityDiagnosticCodes.ModuleAllowlistInsufficient, CreateSchemeOnlyModuleOptions),
            (SecurityDiagnosticCodes.ClrExtensionMethodsConfigured, () => CreateSafeOptions().AddExtensionMethods(typeof(SecurityConfigurationUnsafeExtensions))),
            (SecurityDiagnosticCodes.ClrOperatorOverloadingEnabled, () => Change(CreateSafeOptions(), x => x.Interop.AllowOperatorOverloading = true)),
            (SecurityDiagnosticCodes.NonPublicClrMembersEnabled, () => Change(
                CreateSafeOptions(),
                x => x.Interop.ObjectWrapperReportedPropertyBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
        };

        foreach (var (code, createOptions) in cases)
        {
            var options = createOptions();
            var diagnostic = options.ValidateSecurityConfiguration().Diagnostics
                .Should().ContainSingle(candidate => candidate.Code == code).Which;
            diagnostic.Code.Should().Be(code);
            diagnostic.Severity.Should().Be(ExpectedSeverity(code));
            diagnostic.Message.Should().NotBeNullOrWhiteSpace();
            diagnostic.Guidance.Should().NotBeNullOrWhiteSpace();

            if (diagnostic.Severity == SecurityDiagnosticSeverity.Error)
            {
                Invoking(() => options.EnsureSecurityConfiguration()).Should().ThrowExactly<SecurityConfigurationException>();
            }

            else
            {
                if (!options.ValidateSecurityConfiguration().HasErrors)
                {
                    Invoking(() => options.EnsureSecurityConfiguration()).Should().NotThrow();
                }
            }
        }
    }

    [Test]
    public void EngineReportUsesCapturedClrBindingFlags()
    {
        var options = CreateSafeOptions();
        options.Interop.ObjectWrapperReportedPropertyBindingFlags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var engine = new Engine(options);

        // The options are read-only from here, so there is no longer a way to make the report and the
        // engine disagree: the write that used to be the interesting half of this test is refused.
        Invoking(() => options.Interop.ObjectWrapperReportedPropertyBindingFlags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Should().Throw<InvalidOperationException>();

        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.NonPublicClrMembersEnabled);
    }

    [Test]
    public void DiagnosticsAreOrderedByStableCode()
    {
        var report = new Options().ValidateSecurityConfiguration();

        report.Diagnostics.Select(static diagnostic => diagnostic.Code)
            .Should().BeInAscendingOrder(StringComparer.Ordinal);
        report.Diagnostics.Select(static diagnostic => diagnostic.Code)
            .Should().Equal(new Options().ValidateSecurityConfiguration().Diagnostics.Select(static diagnostic => diagnostic.Code));
    }

    [Test]
    public void EnsureThrowsBeforeEngineConstructionAndCarriesTheReport()
    {
        var options = new Options();

        var exception = Invoking(() => options.EnsureSecurityConfiguration())
            .Should().ThrowExactly<SecurityConfigurationException>().Which;

        exception.Report.HasErrors.Should().BeTrue();
        exception.Report.Diagnostics.Should().Contain(static diagnostic =>
            diagnostic.Code == SecurityDiagnosticCodes.StatementLimitMissing
            && diagnostic.Severity == SecurityDiagnosticSeverity.Error);
        exception.Message.Should().Contain(SecurityDiagnosticCodes.StatementLimitMissing);
    }

    [Test]
    public void WarningsRemainVisibleWithoutFailingExplicitValidation()
    {
        var options = CreateSafeModuleOptions();

        var report = options.ValidateSecurityConfiguration();

        report.HasErrors.Should().BeFalse();
        report.HasWarnings.Should().BeTrue();
        report.Diagnostics.Should().OnlyContain(
            static diagnostic => diagnostic.Severity == SecurityDiagnosticSeverity.Warning);
        options.EnsureSecurityConfiguration().Should().BeSameAs(options);
    }

    [Test]
    public void RemovingABuiltInConstraintUpdatesTheReport()
    {
        var options = CreateSafeOptions()
            .RemoveConstraints(static constraint => constraint is MaxStatementsConstraint);

        options.ValidateSecurityConfiguration().Diagnostics.Should().ContainSingle().Which.Code
            .Should().Be(SecurityDiagnosticCodes.StatementLimitMissing);
    }

    [Test]
    public void SideEffectFreeConstraintFactoriesRemainSupported()
    {
        var options = CreateSafeOptions().AddConstraint(static () => new NoOpConstraint());

        options.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void ExplicitParsingTimeoutIsValidatedWithEngineOptions()
    {
        var options = CreateSafeOptions();

        options.ValidateSecurityConfiguration(ScriptParsingOptions.Default).Diagnostics.Should().BeEmpty();

        var longTimeout = ScriptParsingOptions.Default with { RegexTimeout = TimeSpan.FromSeconds(2) };
        var longDiagnostic = options.ValidateSecurityConfiguration(longTimeout).Diagnostics.Should().ContainSingle().Which;
        longDiagnostic.Code.Should().Be(SecurityDiagnosticCodes.RegexTimeoutOverrideLong);
        longDiagnostic.Severity.Should().Be(SecurityDiagnosticSeverity.Warning);
        options.EnsureSecurityConfiguration(longTimeout).Should().BeSameAs(options);

        var unbounded = ScriptParsingOptions.Default with { RegexTimeout = TimeSpan.Zero };
        var unboundedDiagnostic = options.ValidateSecurityConfiguration(unbounded).Diagnostics.Should().ContainSingle().Which;
        unboundedDiagnostic.Code.Should().Be(SecurityDiagnosticCodes.RegexTimeoutOverrideUnbounded);
        unboundedDiagnostic.Severity.Should().Be(SecurityDiagnosticSeverity.Error);
        Invoking(() => options.EnsureSecurityConfiguration(unbounded))
            .Should().ThrowExactly<SecurityConfigurationException>();
    }

    [Test]
    public void PreparationTimeoutIsValidatedIndependentlyOfEngineOptions()
    {
        var scriptDefault = ScriptPreparationOptions.Default.ValidateSecurityConfiguration()
            .Diagnostics.Should().ContainSingle(
                diagnostic => diagnostic.Code == SecurityDiagnosticCodes.RegexTimeoutOverrideLong).Which;
        scriptDefault.Code.Should().Be(SecurityDiagnosticCodes.RegexTimeoutOverrideLong);
        scriptDefault.Severity.Should().Be(SecurityDiagnosticSeverity.Warning);

        var moduleDefault = ModulePreparationOptions.Default.ValidateSecurityConfiguration()
            .Diagnostics.Should().ContainSingle(
                diagnostic => diagnostic.Code == SecurityDiagnosticCodes.RegexTimeoutOverrideLong).Which;
        moduleDefault.Code.Should().Be(SecurityDiagnosticCodes.RegexTimeoutOverrideLong);

        var safeScript = ScriptPreparationOptions.Default with
        {
            ParsingOptions = ScriptParsingOptions.Default with
            {
                RegexTimeout = TimeSpan.FromSeconds(1),
                MaxSourceLength = 100_000,
                MaxNodeCount = 25_000
            }
        };
        safeScript.ValidateSecurityConfiguration().Diagnostics.Should().BeEmpty();
        safeScript.EnsureSecurityConfiguration().Should().BeSameAs(safeScript);

        var excessiveModule = ModulePreparationOptions.Default with
        {
            ParsingOptions = ModuleParsingOptions.Default with
            {
                RegexTimeout = TimeSpan.MaxValue,
                MaxSourceLength = 100_000,
                MaxNodeCount = 25_000
            }
        };
        var excessiveDiagnostic = excessiveModule.ValidateSecurityConfiguration()
            .Diagnostics.Should().ContainSingle(
                diagnostic => diagnostic.Code == SecurityDiagnosticCodes.RegexTimeoutOverrideExcessive).Which;
        excessiveDiagnostic.Code.Should().Be(SecurityDiagnosticCodes.RegexTimeoutOverrideExcessive);
        excessiveDiagnostic.Severity.Should().Be(SecurityDiagnosticSeverity.Error);
        Invoking(() => excessiveModule.EnsureSecurityConfiguration())
            .Should().ThrowExactly<SecurityConfigurationException>();
    }

    [Test]
    public void EveryEngineConstructionCallbackIsRejected()
    {
        var options = new[]
        {
            CreateSafeOptions().Configure(static _ => { }),
            CreateSafeOptions().UseHostFactory(static _ => new Host())
        };

        foreach (var configured in options)
        {
            configured.ValidateSecurityConfiguration().Diagnostics.Should().ContainSingle().Which.Code
                .Should().Be(SecurityDiagnosticCodes.EngineConstructionCallback);
        }

        // SetTypeConverter is one too, and additionally installs a CLR callback - the converter itself, which
        // runs host code for every value crossing out of script. It used to be absent from JINTSEC052's list.
        CreateSafeOptions().SetTypeConverter(static _ => new PassThroughTypeConverter())
            .ValidateSecurityConfiguration().Diagnostics.Select(static d => d.Code)
            .Should().BeEquivalentTo([
                SecurityDiagnosticCodes.EngineConstructionCallback,
                SecurityDiagnosticCodes.ClrCallbackConfigured
            ]);
    }

    [Test]
    public void AHostInstalledTypeConverterIsReportedFromABuiltEngineToo()
    {
        var options = CreateSafeOptions().SetTypeConverter(static _ => new PassThroughTypeConverter());
        var engine = new Engine(options);

        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics.Select(static d => d.Code)
            .Should().Contain(SecurityDiagnosticCodes.ClrCallbackConfigured);
    }

    [Test]
    public void EffectiveEngineReportRunsDeferredCallbacksOnceAndObservesFinalState()
    {
        var callbackCount = 0;
        var options = CreateSafeOptions();
        options.Configure(_ =>
        {
            callbackCount++;
            options.AgentCanSuspend = true;
        });

        var engine = new Engine(options);
        var report = engine.Diagnostics.ValidateSecurityConfiguration();

        callbackCount.Should().Be(1);
        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.EngineConstructionCallback);
        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.AgentSuspensionEnabled);
    }

    [Test]
    public void EffectiveEngineReportUsesEngineSnapshotsTakenBeforeDeferredCallbacks()
    {
        var options = CreateSafeOptions();
        options.Debugger.Enabled = true;
        options.Configure(_ => options.Debugger.Enabled = false);

        var report = new Engine(options).Diagnostics.ValidateSecurityConfiguration();

        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.DebuggerEnabled);
    }

    [Test]
    public void EffectiveEngineReportCannotBeCleanedByMutatingFixedOptionsAfterConstruction()
    {
        var options = CreateSafeOptions().AllowClr();
        options.Interop.ExposeDetailedResolutionErrors = true;
        var engine = new Engine(options);

        // Every one of these used to be a silent no-op against this engine's report. They are now refused
        // outright, which is the same guarantee said out loud.
        Invoking(() => options.Interop.Enabled = false).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Interop.AllowGetType = false).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Interop.AllowSystemReflection = false).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Interop.ExposeDetailedResolutionErrors = false).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Interop.TypeResolver = new TypeResolver { MemberFilter = static _ => false })
            .Should().Throw<InvalidOperationException>();
        Invoking(() => options.Interop.AllowedAssemblies.Clear()).Should().Throw<InvalidOperationException>();

        var report = engine.Diagnostics.ValidateSecurityConfiguration();

        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.ClrAccessEnabled);
        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.ClrCompatibilityModeEnabled);
        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.ClrPolicyUnrestricted);
        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.DetailedClrErrorsEnabled);
    }

    [Test]
    public void EffectiveEngineReportUsesTheLoaderActuallyInstalled()
    {
        var options = CreateSafeModuleOptions();
        var engine = new Engine(options);
        Invoking(() => options.Modules.ModuleLoader = new Options().Modules.ModuleLoader)
            .Should().Throw<InvalidOperationException>();

        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.ModuleLoadingEnabled);
    }

    [Test]
    public void EffectiveEngineReportPreservesParserAndStackConstructionSettings()
    {
        var options = CreateSafeOptions();
        options.Parsing.MaxSourceLength = null;
        options.Parsing.MaxNodeCount = null;
        options.RetainFunctionSourceText = true;
        options.Constraints.RegexTimeout = TimeSpan.FromSeconds(2);
        options.Constraints.MaxRecursionDepth = -1;
        options.Constraints.StackOverflowGuard = false;
        var engine = new Engine(options);

        Invoking(() => options.Parsing.MaxSourceLength = 100_000).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Parsing.MaxNodeCount = 25_000).Should().Throw<InvalidOperationException>();
        Invoking(() => options.RetainFunctionSourceText = false).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Constraints.RegexTimeout = TimeSpan.FromSeconds(1)).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Constraints.MaxRecursionDepth = 64).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Constraints.StackOverflowGuard = true).Should().Throw<InvalidOperationException>();

        var codes = engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics
            .Select(static diagnostic => diagnostic.Code);
        codes.Should().Contain(SecurityDiagnosticCodes.ParserSourceLengthUnlimited);
        codes.Should().Contain(SecurityDiagnosticCodes.ParserNodeCountUnlimited);
        codes.Should().Contain(SecurityDiagnosticCodes.FunctionSourceRetentionEnabled);
        codes.Should().Contain(SecurityDiagnosticCodes.RegexTimeoutLong);
        codes.Should().Contain(SecurityDiagnosticCodes.RecursionLimitDisabled);
        codes.Should().Contain(SecurityDiagnosticCodes.StackOverflowGuardDisabled);
    }

    [Test]
    public void EffectiveEngineReportUsesInstalledExtensionMethodCache()
    {
        var options = CreateSafeOptions().AddExtensionMethods(typeof(SecurityConfigurationUnsafeExtensions));
        options.Configure(_ => options.Interop.ExtensionMethodTypes.Clear());

        var report = new Engine(options).Diagnostics.ValidateSecurityConfiguration();

        report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.ClrExtensionMethodsConfigured);
    }

    [Test]
    public void UserCallbackCannotSpoofFirstPartyProvenance()
    {
        var options = CreateSafeOptions().Configure(ConfigureFirstParty);

        options.ValidateSecurityConfiguration().Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.EngineConstructionCallback);

        static void ConfigureFirstParty(Engine _)
        {
        }
    }

    [Test]
    public void ConstraintFactoriesAreNotInvokedByDiagnostics()
    {
        var calls = 0;
        var options = CreateSafeOptions().AddConstraint(() =>
        {
            calls++;
            return new NoOpConstraint();
        });

        options.ValidateSecurityConfiguration();
        calls.Should().Be(0);

        var engine = new Engine(options);
        engine.Diagnostics.ValidateSecurityConfiguration();
        calls.Should().Be(1);
    }

    [Test]
    public void SharedOptionsProduceDeterministicReportsAcrossConcurrentEngines()
    {
        var options = CreateSafeOptions();
        var reports = new string[16];

        Parallel.For(0, reports.Length, index =>
        {
            var engine = new Engine(options);
            reports[index] = string.Join(",", engine.Diagnostics.ValidateSecurityConfiguration()
                .Diagnostics.Select(static diagnostic => diagnostic.Code));
        });

        reports.Should().OnlyContain(static report => report.Length == 0);
    }

    [Test]
    public void ReportsDoNotRetainOptionsEnginesCallbacksOrHostState()
    {
        var references = CreateCollectibleReport();

        ForceCollection();

        references.Options.IsAlive.Should().BeFalse();
        references.Engine.IsAlive.Should().BeFalse();
        references.HostState.IsAlive.Should().BeFalse();
        references.Report.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == SecurityDiagnosticCodes.EngineConstructionCallback);
    }

    [Test]
    public void DiagnosticTextDoesNotEchoSensitiveConfigurationValues()
    {
        const string secret = "/private/customer/acme-token";
        var options = CreateSafeModuleOptions();
        var policy = new ModuleAllowlistPolicy();
        policy.AllowedFileRoots.Add(secret);
        options.Modules.LoadPolicy = policy;
        options.Interop.ClrExceptionErrorDecorator = static (_, _, _) => { };

        var rendered = string.Join(
            "\n",
            options.ValidateSecurityConfiguration().Diagnostics.Select(
                static diagnostic => diagnostic.Message + "\n" + diagnostic.Guidance));

        rendered.Should().NotContain(secret);
        rendered.Should().NotContain(nameof(SecretHostState));
    }

    private static Options CreateSafeOptions()
    {
        var options = CreateBaseOptions();
        options.LimitStatements(100_000);
        options.LimitMemory(4_000_000);
        options.LimitExecutionTime(TimeSpan.FromSeconds(2));
        return options;
    }

    private static Options CreateWithoutStatementLimit()
    {
        var options = CreateBaseOptions();
        options.LimitMemory(4_000_000);
        options.LimitExecutionTime(TimeSpan.FromSeconds(2));
        return options;
    }

    private static Options CreateWithoutMemoryLimit()
    {
        var options = CreateBaseOptions();
        options.LimitStatements(100_000);
        options.LimitExecutionTime(TimeSpan.FromSeconds(2));
        return options;
    }

    private static Options CreateWithoutTimeout()
    {
        var options = CreateBaseOptions();
        options.LimitStatements(100_000);
        options.LimitMemory(4_000_000);
        return options;
    }

    private static Options CreateBaseOptions()
    {
        var options = new Options
        {
            AgentCanSuspend = false
        };

        options.Constraints.MaxRecursionDepth = 64;
        options.Constraints.StackOverflowGuard = true;
        options.Constraints.MaxArraySize = 100_000;
        options.Constraints.RegexTimeout = TimeSpan.FromSeconds(1);
        options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(1);
        options.Parsing.MaxSourceLength = 100_000;
        options.Parsing.MaxNodeCount = 25_000;
        options.Interop.AllowWrite = false;
        options.Interop.ArrayConversion = ArrayConversionMode.Copy;
        options.Host.StringCompilationAllowed = false;
        options.ResultLimits = ResultLimits.Conservative;
        options.ObserveCancellation(new CancellationTokenSource().Token);
        options.AddConstraint(static () => new OperationDeadlineConstraint());
        return options;
    }

    private static Options CreateWithoutCancellation()
    {
        var options = CreateSafeOptions();
        options.RemoveConstraints(static constraint => constraint is CancellationConstraint);
        return options;
    }

    private static Options CreateWithoutOperationDeadline()
    {
        var options = CreateSafeOptions();
        options.RemoveConstraints(static constraint => constraint is OperationDeadlineConstraint);
        return options;
    }

    private static Options CreateSafeModuleOptions()
    {
        var options = CreateSafeOptions().UseModules(new EmptyModuleLoader());
        options.Modules.MaxModuleCount = 100;
        options.Modules.MaxTotalModuleSourceBytes = 1_000_000;
        options.Modules.MaxModuleGraphDepth = 20;
        options.Modules.MaxModuleResolutionHops = 100;
        var policy = new ModuleAllowlistPolicy();
        policy.AllowedSchemes.Add("https");
        policy.AllowedHosts.Add("modules.example.invalid");
        options.Modules.LoadPolicy = policy;
        return options;
    }

    private static Options CreateWithRestrictiveClr()
    {
        var options = CreateSafeOptions().AllowClr(Array.Empty<System.Reflection.Assembly>());
        options.Interop.TypeResolver = new TypeResolver
        {
            MemberFilter = static _ => false
        };
        return options;
    }

    private static Options CreateSchemeOnlyModuleOptions()
    {
        var options = CreateSafeModuleOptions();
        var policy = new ModuleAllowlistPolicy();
        policy.AllowedSchemes.Add("file");
        options.Modules.LoadPolicy = policy;
        return options;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CollectibleReport CreateCollectibleReport()
    {
        var state = new SecretHostState();
        var options = CreateSafeOptions().Configure(_ => GC.KeepAlive(state));
        var engine = new Engine(options);
        var report = engine.Diagnostics.ValidateSecurityConfiguration();
        return new CollectibleReport(
            new WeakReference(options),
            new WeakReference(engine),
            new WeakReference(state),
            report);
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

    private static Options Change(Options options, Action<Options> change)
    {
        change(options);
        return options;
    }

    private static SecurityDiagnosticSeverity ExpectedSeverity(string code) => code switch
    {
        SecurityDiagnosticCodes.ModuleLoadingEnabled
            or SecurityDiagnosticCodes.RequireWithoutModuleLoader
            or SecurityDiagnosticCodes.RegexTimeoutLong
            or SecurityDiagnosticCodes.PromiseTimeoutLong
            or SecurityDiagnosticCodes.SharedConstraintInstance
            or SecurityDiagnosticCodes.DetailedClrErrorsEnabled
            or SecurityDiagnosticCodes.FunctionSourceRetentionEnabled
            or SecurityDiagnosticCodes.ModulePerLoadLimitsReset
            or SecurityDiagnosticCodes.ClrExceptionChainingEnabled
            or SecurityDiagnosticCodes.ClrErrorDecoratorConfigured
            or SecurityDiagnosticCodes.ClrCallbackConfigured => SecurityDiagnosticSeverity.Warning,
        _ => SecurityDiagnosticSeverity.Error
    };

    private sealed class NoOpConstraint : Constraint
    {
        public override void Check()
        {
        }

        public override void Reset()
        {
        }
    }

    private sealed class EmptyModuleLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved) => string.Empty;
    }

    private sealed class PassThroughTypeConverter : ClrTypeConverter
    {
        public override object? Convert(object? value, Type type, IFormatProvider formatProvider) => value;

        public override bool TryConvert(
            object? value,
            Type type,
            IFormatProvider formatProvider,
            [NotNullWhen(true)] out object? converted)
        {
            converted = value;
            return converted is not null;
        }
    }

    private sealed class SecretHostState;

    private sealed record CollectibleReport(
        WeakReference Options,
        WeakReference Engine,
        WeakReference HostState,
        SecurityConfigurationReport Report);
}

internal static class SecurityConfigurationUnsafeExtensions
{
    public static string ReadSecret(this string value) => value;
}
