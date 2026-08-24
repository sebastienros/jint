using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using Jint.Constraints;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Debugger;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

/// <summary>
/// A security policy evaluated against Jint configuration.
/// </summary>
public enum SecurityConfigurationPolicy
{
    /// <summary>Checks controls relevant to untrusted script source.</summary>
    UntrustedScripts = 0
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EngineSecurityConfigurationSnapshot(
    bool DebuggerEnabled,
    bool ClrEnabled,
    bool AllowGetType,
    bool AllowSystemReflection,
    bool DetailedClrResolutionErrors,
    int? MaxSourceLength,
    int? MaxNodeCount,
    bool RetainFunctionSourceText,
    TimeSpan RegexTimeout,
    int MaxRecursionDepth,
    bool StackOverflowGuard,
    int MaxExecutionStackCount,
    bool ClrCompatibilityMode,
    bool ClrPolicyUnrestricted,
    bool ExtensionMethodsConfigured,
    bool OperatorOverloadingAllowed,
    bool NonPublicBindingFlags,
    bool CustomTypeConverter,
    bool UntrustedProfileApplied)
{
    internal static EngineSecurityConfigurationSnapshot Capture(Engine engine)
    {
        var interop = engine.Options.Interop;
        return new EngineSecurityConfigurationSnapshot(
            engine._isDebugMode,
            interop.Enabled,
            interop.AllowGetType,
            interop.AllowSystemReflection,
            interop.ExposeDetailedResolutionErrors,
            engine.Options.Parsing.MaxSourceLength,
            engine.Options.Parsing.MaxNodeCount,
            engine.Options.RetainFunctionSourceText,
            engine.Options.Constraints.RegexTimeout,
            // The configured depth, not the engine's normalized one: a saturated limit and no limit at all
            // produce the same engine, and the two are still worth telling a host apart.
            engine.Options.Constraints.MaxRecursionDepth,
            engine.Options.Constraints.StackOverflowGuard,
            engine.Options.Constraints.MaxExecutionStackCount,
            interop.Enabled && interop.ClrAccessConfiguration == ClrAccessConfiguration.Compatibility,
            interop.Enabled && ReferenceEquals(interop.TypeResolver, TypeResolver.Default),
            !ReferenceEquals(
                engine._extensionMethods,
                Runtime.Interop.Reflection.ExtensionMethodCache.Empty),
            engine._operatorOverloadingAllowed,
            (interop.ObjectWrapperReportedFieldBindingFlags & BindingFlags.NonPublic) != BindingFlags.Default
                || (interop.ObjectWrapperReportedPropertyBindingFlags & BindingFlags.NonPublic) != BindingFlags.Default
                || (interop.ObjectWrapperReportedMethodBindingFlags & BindingFlags.NonPublic) != BindingFlags.Default,
            // Read from the engine rather than from the options: the converter is installed by a configuration
            // callback, and a Configure callback can install one without going through SetTypeConverter at all.
            engine._typeConverterTargetFilter is not null,
            engine._untrustedCodeLimits is not null);
    }
}

/// <summary>
/// The severity of a <see cref="SecurityDiagnostic"/>.
/// </summary>
public enum SecurityDiagnosticSeverity
{
    /// <summary>The host must review or compensate for the setting.</summary>
    Warning = 0,

    /// <summary>A required control is absent or an unsafe capability is enabled.</summary>
    Error = 1
}

/// <summary>
/// Stable machine-readable security diagnostic codes.
/// </summary>
public static class SecurityDiagnosticCodes
{
    public const string StatementLimitMissing = "JINTSEC001";
    public const string StatementLimitNonPositive = "JINTSEC002";
    public const string StatementLimitSaturated = "JINTSEC003";
    public const string MemoryLimitMissing = "JINTSEC004";
    public const string MemoryLimitNonPositive = "JINTSEC005";
    public const string MemoryLimitSaturated = "JINTSEC006";
    public const string TimeoutMissing = "JINTSEC007";
    public const string TimeoutNonPositive = "JINTSEC008";
    public const string TimeoutSaturated = "JINTSEC009";
    public const string RecursionLimitDisabled = "JINTSEC010";
    public const string RecursionLimitSaturated = "JINTSEC011";
    public const string StackOverflowGuardDisabled = "JINTSEC012";
    public const string StackOverflowGuardShadowed = "JINTSEC013";
    public const string AgentSuspensionEnabled = "JINTSEC014";
    public const string ClrAccessEnabled = "JINTSEC015";
    public const string GetTypeEnabled = "JINTSEC016";
    public const string SystemReflectionEnabled = "JINTSEC017";
    public const string ClrWriteEnabled = "JINTSEC018";
    public const string LiveClrArraysEnabled = "JINTSEC019";
    public const string StringCompilationEnabled = "JINTSEC020";
    public const string ModuleLoadingEnabled = "JINTSEC021";
    public const string RequireWithoutModuleLoader = "JINTSEC022";
    public const string DebuggerEnabled = "JINTSEC023";
    public const string ArraySizeUnlimited = "JINTSEC024";
    public const string RegexTimeoutUnbounded = "JINTSEC025";
    public const string RegexTimeoutLong = "JINTSEC026";
    public const string PromiseTimeoutUnbounded = "JINTSEC027";
    public const string PromiseTimeoutLong = "JINTSEC028";
    public const string SharedConstraintInstance = "JINTSEC029";
    public const string DetailedClrErrorsEnabled = "JINTSEC030";
    public const string EngineConstructionCallback = "JINTSEC031";
    public const string RegexTimeoutExcessive = "JINTSEC032";
    public const string RegexTimeoutOverrideUnbounded = "JINTSEC033";
    public const string RegexTimeoutOverrideLong = "JINTSEC034";
    public const string RegexTimeoutOverrideExcessive = "JINTSEC035";
    public const string CancellationMissing = "JINTSEC036";
    public const string OperationDeadlineMissing = "JINTSEC037";
    public const string ParserSourceLengthUnlimited = "JINTSEC038";
    public const string ParserNodeCountUnlimited = "JINTSEC039";
    public const string FunctionSourceRetentionEnabled = "JINTSEC040";
    public const string ModuleCountUnlimited = "JINTSEC041";
    public const string ModuleSourceBytesUnlimited = "JINTSEC042";
    public const string ModuleGraphDepthUnlimited = "JINTSEC043";
    public const string ModuleResolutionHopsUnlimited = "JINTSEC044";
    public const string ModuleDestinationPolicyMissing = "JINTSEC045";
    public const string ModulePerLoadLimitsReset = "JINTSEC046";
    public const string ResultLimitsUnlimited = "JINTSEC047";
    public const string DetailedClrExceptionMessagesEnabled = "JINTSEC048";
    public const string ClrExceptionChainingEnabled = "JINTSEC049";
    public const string DetailedModuleErrorsEnabled = "JINTSEC050";
    public const string ClrErrorDecoratorConfigured = "JINTSEC051";
    public const string ClrCallbackConfigured = "JINTSEC052";
    public const string ClrCompatibilityModeEnabled = "JINTSEC053";
    public const string ClrPolicyUnrestricted = "JINTSEC054";
    public const string LiveClrArrayWritesEnabled = "JINTSEC055";
    public const string ModuleAllowlistInsufficient = "JINTSEC056";
    public const string ClrExtensionMethodsConfigured = "JINTSEC057";
    public const string ClrOperatorOverloadingEnabled = "JINTSEC058";
    public const string NonPublicClrMembersEnabled = "JINTSEC059";
}

/// <summary>
/// One security-relevant observation.
/// </summary>
public sealed class SecurityDiagnostic
{
    internal SecurityDiagnostic(string code, SecurityDiagnosticSeverity severity, string message, string guidance)
    {
        Code = code;
        Severity = severity;
        Message = message;
        Guidance = guidance;
    }

    public string Code { get; }

    public SecurityDiagnosticSeverity Severity { get; }

    /// <remarks>Use <see cref="Code"/>, not this text, for policy decisions.</remarks>
    public string Message { get; }

    /// <remarks>Use <see cref="Code"/>, not this text, for policy decisions.</remarks>
    public string Guidance { get; }

    public override string ToString() => $"{Code} {Severity}: {Message}";
}

/// <summary>
/// An immutable, deterministically ordered security validation result.
/// </summary>
public sealed class SecurityConfigurationReport
{
    internal SecurityConfigurationReport(List<SecurityDiagnostic> diagnostics)
    {
        Diagnostics = new ReadOnlyCollection<SecurityDiagnostic>(diagnostics);
        HasErrors = diagnostics.Exists(static diagnostic => diagnostic.Severity == SecurityDiagnosticSeverity.Error);
        HasWarnings = diagnostics.Exists(static diagnostic => diagnostic.Severity == SecurityDiagnosticSeverity.Warning);
    }

    public IReadOnlyList<SecurityDiagnostic> Diagnostics { get; }

    public bool HasErrors { get; }

    public bool HasWarnings { get; }
}

/// <summary>
/// Thrown when security configuration validation finds an error.
/// </summary>
public sealed class SecurityConfigurationException : InvalidOperationException
{
    internal SecurityConfigurationException(SecurityConfigurationReport report)
        : base(CreateMessage(report))
    {
        Report = report;
    }

    public SecurityConfigurationReport Report { get; }

    private static string CreateMessage(SecurityConfigurationReport report)
    {
        var builder = new StringBuilder("The Jint options do not satisfy the requested security policy:");
        foreach (var diagnostic in report.Diagnostics)
        {
            if (diagnostic.Severity == SecurityDiagnosticSeverity.Error)
            {
                builder.Append(' ').Append(diagnostic.Code).Append('.');
            }
        }

        return builder.ToString();
    }
}

/// <summary>
/// Security diagnostics for Jint configuration.
/// </summary>
public static class OptionsSecurityExtensions
{
    private static readonly TimeSpan MaximumRecommendedWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumAllowedRegexTimeout = TimeSpan.FromDays(365);

    /// <summary>
    /// Inspects configured options without constructing an engine or invoking callbacks or factories.
    /// </summary>
    public static SecurityConfigurationReport ValidateSecurityConfiguration(
        this Options options,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ArgumentNull(options, nameof(options));
        return CreateReport(CreateOptionsDiagnostics(options!.CreateEngineOptions(), policy));
    }

    /// <summary>
    /// Inspects engine options together with explicit parsing options that override parser defaults.
    /// </summary>
    public static SecurityConfigurationReport ValidateSecurityConfiguration(
        this Options options,
        IParsingOptions parsingOptions,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ArgumentNull(options, nameof(options));
        ArgumentNull(parsingOptions, nameof(parsingOptions));

        var effectiveOptions = options!.CreateEngineOptions();
        var diagnostics = CreateOptionsDiagnostics(
            effectiveOptions,
            policy,
            includeParsing: false,
            includeEngineRegex: !parsingOptions!.RegexTimeout.HasValue);
        if (parsingOptions.RegexTimeout.HasValue)
        {
            AddRegexOverride(parsingOptions.RegexTimeout.Value, diagnostics);
        }

        var limits = parsingOptions as IParsingLimitOptions;
        AddParsing(
            Minimum(effectiveOptions.Parsing.MaxSourceLength, limits?.MaxSourceLength),
            Minimum(effectiveOptions.Parsing.MaxNodeCount, limits?.MaxNodeCount),
            parsingOptions.RetainFunctionSourceText,
            diagnostics);
        return CreateReport(diagnostics);
    }

    /// <summary>Inspects script preparation options.</summary>
    public static SecurityConfigurationReport ValidateSecurityConfiguration(
        this ScriptPreparationOptions options,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ArgumentNull(options, nameof(options));
        return CreatePreparationReport(options!.ParsingOptions, policy);
    }

    /// <summary>Inspects module preparation options.</summary>
    public static SecurityConfigurationReport ValidateSecurityConfiguration(
        this ModulePreparationOptions options,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ArgumentNull(options, nameof(options));
        return CreatePreparationReport(options!.ParsingOptions, policy);
    }

    public static Options EnsureSecurityConfiguration(
        this Options options,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ThrowIfErrors(options.ValidateSecurityConfiguration(policy));
        return options;
    }

    public static Options EnsureSecurityConfiguration(
        this Options options,
        IParsingOptions parsingOptions,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ThrowIfErrors(options.ValidateSecurityConfiguration(parsingOptions, policy));
        return options;
    }

    public static ScriptPreparationOptions EnsureSecurityConfiguration(
        this ScriptPreparationOptions options,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ThrowIfErrors(options.ValidateSecurityConfiguration(policy));
        return options;
    }

    public static ModulePreparationOptions EnsureSecurityConfiguration(
        this ModulePreparationOptions options,
        SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
    {
        ThrowIfErrors(options.ValidateSecurityConfiguration(policy));
        return options;
    }

    internal static SecurityConfigurationReport CreateEngineReport(
        Engine engine,
        SecurityConfigurationPolicy policy)
    {
        var diagnostics = CreateOptionsDiagnostics(
            engine.Options,
            policy,
            engineSnapshot: engine._securityConfigurationSnapshot,
            effectiveModuleLoader: engine.Modules.ModuleLoader);
        diagnostics.RemoveAll(static diagnostic => IsConstraintCode(diagnostic.Code));
        AddEffectiveConstraints(
            engine.ActiveConstraintsForSecurityDiagnostics,
            engine._securityConfigurationSnapshot.UntrustedProfileApplied,
            diagnostics);
        return CreateReport(diagnostics);
    }

    private static List<SecurityDiagnostic> CreateOptionsDiagnostics(
        Options options,
        SecurityConfigurationPolicy policy,
        bool includeParsing = true,
        bool includeEngineRegex = true,
        EngineSecurityConfigurationSnapshot? engineSnapshot = null,
        IModuleLoader? effectiveModuleLoader = null)
    {
        ValidatePolicy(policy);
        var diagnostics = new List<SecurityDiagnostic>();
        AddLimits(options, diagnostics);
        if (includeParsing)
        {
            AddParsing(
                engineSnapshot.HasValue ? engineSnapshot.Value.MaxSourceLength : options.Parsing.MaxSourceLength,
                engineSnapshot.HasValue ? engineSnapshot.Value.MaxNodeCount : options.Parsing.MaxNodeCount,
                engineSnapshot?.RetainFunctionSourceText ?? options.RetainFunctionSourceText,
                diagnostics);
        }

        AddStack(options, engineSnapshot, diagnostics);
        AddCapabilities(options, engineSnapshot, diagnostics);
        AddModules(options, effectiveModuleLoader, diagnostics);
        AddResources(options, includeEngineRegex, engineSnapshot, diagnostics);
        AddResultLimits(options.ResultLimits, diagnostics);
        AddConstraintRegistration(options, diagnostics);
        return diagnostics;
    }

    private static SecurityConfigurationReport CreatePreparationReport(
        IParsingOptions parsingOptions,
        SecurityConfigurationPolicy policy)
    {
        ValidatePolicy(policy);
        var diagnostics = new List<SecurityDiagnostic>();
        AddRegexOverride(parsingOptions.RegexTimeout ?? Engine.DefaultRegexTimeout, diagnostics);
        var limits = parsingOptions as IParsingLimitOptions;
        AddParsing(
            limits?.MaxSourceLength,
            limits?.MaxNodeCount,
            parsingOptions.RetainFunctionSourceText,
            diagnostics);
        return CreateReport(diagnostics);
    }

    private static void AddLimits(Options options, List<SecurityDiagnostic> diagnostics)
    {
        var constraints = options.Constraints;
        AddRequestedLimit(
            constraints.RequestedMaxStatements,
            int.MaxValue,
            SecurityDiagnosticCodes.StatementLimitMissing,
            SecurityDiagnosticCodes.StatementLimitNonPositive,
            SecurityDiagnosticCodes.StatementLimitSaturated,
            "statement-count",
            diagnostics);
        AddRequestedLimit(
            constraints.RequestedMemoryLimit,
            long.MaxValue,
            SecurityDiagnosticCodes.MemoryLimitMissing,
            SecurityDiagnosticCodes.MemoryLimitNonPositive,
            SecurityDiagnosticCodes.MemoryLimitSaturated,
            "managed-allocation",
            diagnostics);

        if (!constraints.RequestedTimeoutInterval.HasValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.TimeoutMissing,
                "No per-entry execution timeout was configured.",
                "Call TimeoutInterval with a positive finite interval.");
        }
        else if (constraints.RequestedTimeoutInterval <= TimeSpan.Zero)
        {
            Error(diagnostics, SecurityDiagnosticCodes.TimeoutNonPositive,
                "The requested execution timeout is non-positive and removes the timeout.",
                "Use a positive execution timeout.");
        }
        else if (constraints.RequestedTimeoutInterval == TimeSpan.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.TimeoutSaturated,
                "The saturated execution timeout removes the timeout.",
                "Use a finite interval below TimeSpan.MaxValue.");
        }

        if (!constraints.CancellationConstraintRequested)
        {
            Error(diagnostics, SecurityDiagnosticCodes.CancellationMissing,
                "No cancellation constraint was configured.",
                "Register a cancellable token tied to the host request.");
        }

        if (!constraints.OperationDeadlineConstraintRequested)
        {
            Error(diagnostics, SecurityDiagnosticCodes.OperationDeadlineMissing,
                "No cumulative operation deadline was configured.",
                "Register OperationDeadlineConstraint and bracket each complete host operation.");
        }
    }

    private static void AddRequestedLimit<T>(
        T? value,
        T saturated,
        string missingCode,
        string nonPositiveCode,
        string saturatedCode,
        string name,
        List<SecurityDiagnostic> diagnostics)
        where T : struct, IComparable<T>
    {
        if (!value.HasValue)
        {
            Error(diagnostics, missingCode,
                $"No {name} limit was configured.",
                "Configure a positive finite budget.");
        }
        else if (value.Value.CompareTo(default) <= 0)
        {
            Error(diagnostics, nonPositiveCode,
                $"The requested {name} limit is non-positive and removes the limit.",
                "Use a positive budget.");
        }
        else if (value.Value.CompareTo(saturated) == 0)
        {
            Error(diagnostics, saturatedCode,
                $"The saturated {name} value removes the limit.",
                "Use a finite value below the numeric maximum.");
        }
    }

    private static void AddEffectiveConstraints(
        Constraint[] constraints,
        bool untrustedProfileApplied,
        List<SecurityDiagnostic> diagnostics)
    {
        var hasStatements = false;
        var hasMemory = false;
        var hasTimeout = false;
        var hasCancellation = false;
        var hasOperationDeadline = false;
        foreach (var constraint in constraints)
        {
            hasStatements |= constraint is MaxStatementsConstraint;
            hasMemory |= constraint is MemoryLimitConstraint;
            hasTimeout |= constraint is TimeConstraint;
            hasCancellation |= constraint is CancellationConstraint;
            hasOperationDeadline |= constraint is OperationDeadlineConstraint;
        }

        if (!hasStatements)
        {
            Error(diagnostics, SecurityDiagnosticCodes.StatementLimitMissing,
                "The constructed engine has no statement-count limit.",
                "Register a positive finite MaxStatements constraint.");
        }

        if (!hasMemory)
        {
            Error(diagnostics, SecurityDiagnosticCodes.MemoryLimitMissing,
                "The constructed engine has no managed-allocation limit.",
                "Register a positive finite MemoryLimitConstraint.");
        }

        if (!hasTimeout)
        {
            Error(diagnostics, SecurityDiagnosticCodes.TimeoutMissing,
                "The constructed engine has no per-entry timeout.",
                "Register a positive finite TimeConstraint.");
        }

        if (!hasCancellation && !untrustedProfileApplied)
        {
            Error(diagnostics, SecurityDiagnosticCodes.CancellationMissing,
                "The constructed engine has no cancellation constraint.",
                "Register a cancellable token tied to the host request.");
        }

        if (!hasOperationDeadline)
        {
            Error(diagnostics, SecurityDiagnosticCodes.OperationDeadlineMissing,
                "The constructed engine has no cumulative operation deadline.",
                "Register OperationDeadlineConstraint and bracket each complete host operation.");
        }
    }

    private static void AddParsing(
        int? maxSourceLength,
        int? maxNodeCount,
        bool retainSource,
        List<SecurityDiagnostic> diagnostics)
    {
        if (!maxSourceLength.HasValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ParserSourceLengthUnlimited,
                "The effective parser source-length limit is unbounded.",
                "Set MaxSourceLength on engine and explicit parsing or preparation options.");
        }

        if (!maxNodeCount.HasValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ParserNodeCountUnlimited,
                "The effective parser AST-node limit is unbounded.",
                "Set MaxNodeCount on engine and explicit parsing or preparation options.");
        }

        if (retainSource)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.FunctionSourceRetentionEnabled,
                "Parsed function source text is retained.",
                "Disable source retention unless its bounded memory lifetime is required.");
        }
    }

    private static void AddStack(
        Options options,
        EngineSecurityConfigurationSnapshot? engineSnapshot,
        List<SecurityDiagnostic> diagnostics)
    {
        var constraints = options.Constraints;
        var maxRecursionDepth = engineSnapshot?.MaxRecursionDepth ?? constraints.MaxRecursionDepth;
        var stackOverflowGuard = engineSnapshot?.StackOverflowGuard ?? constraints.StackOverflowGuard;
        var maxExecutionStackCount = engineSnapshot?.MaxExecutionStackCount ?? constraints.MaxExecutionStackCount;
        if (maxRecursionDepth < 0)
        {
            Error(diagnostics, SecurityDiagnosticCodes.RecursionLimitDisabled,
                "The per-function recursion limit is disabled.",
                "Set MaxRecursionDepth to a finite non-negative value.");
        }
        else if (maxRecursionDepth == int.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.RecursionLimitSaturated,
                "The recursion limit is effectively unreachable.",
                "Use a finite host-appropriate recursion depth.");
        }

        if (!stackOverflowGuard)
        {
            Error(diagnostics, SecurityDiagnosticCodes.StackOverflowGuardDisabled,
                "The native stack-overflow guard is disabled.",
                "Set Constraints.StackOverflowGuard to true.");
        }
        else if (maxExecutionStackCount != StackGuard.Disabled)
        {
            Error(diagnostics, SecurityDiagnosticCodes.StackOverflowGuardShadowed,
                "MaxExecutionStackCount shadows the broader native stack guard.",
                "Leave MaxExecutionStackCount disabled when using StackOverflowGuard.");
        }
    }

    private static void AddCapabilities(
        Options options,
        EngineSecurityConfigurationSnapshot? engineSnapshot,
        List<SecurityDiagnostic> diagnostics)
    {
        if (options.AgentCanSuspend)
        {
            Error(diagnostics, SecurityDiagnosticCodes.AgentSuspensionEnabled,
                "Atomics.wait can block the engine thread.",
                "Set AgentCanSuspend to false.");
        }

        var interop = options.Interop;
        if (engineSnapshot?.ClrEnabled ?? interop.Enabled)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ClrAccessEnabled,
                "Scripts can resolve CLR namespaces and types.",
                "Keep CLR access disabled or enforce an explicit least-authority policy.");
        }

        if (engineSnapshot?.AllowGetType ?? interop.AllowGetType)
        {
            Error(diagnostics, SecurityDiagnosticCodes.GetTypeEnabled,
                "Wrapped CLR objects expose type discovery and widening operations.",
                "Set Interop.AllowGetType to false.");
        }

        if (engineSnapshot?.AllowSystemReflection ?? interop.AllowSystemReflection)
        {
            Error(diagnostics, SecurityDiagnosticCodes.SystemReflectionEnabled,
                "System.Reflection values can cross into script.",
                "Set Interop.AllowSystemReflection to false.");
        }

        if (interop.AllowWrite)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ClrWriteEnabled,
                "Scripts can write through wrapped CLR objects.",
                "Disable CLR writes and expose immutable capability-scoped facades.");
        }

        if (interop.ArrayConversion == ArrayConversionMode.LiveView)
        {
            Error(diagnostics, SecurityDiagnosticCodes.LiveClrArraysEnabled,
                "CLR arrays cross into script as live views.",
                "Use ArrayConversionMode.Copy.");
        }

        if (interop.AllowWrite && interop.ArrayConversion == ArrayConversionMode.LiveView)
        {
            Error(diagnostics, SecurityDiagnosticCodes.LiveClrArrayWritesEnabled,
                "Live CLR arrays and CLR writes combine into direct array write-through.",
                "Use copied arrays or disable CLR writes.");
        }

        if (engineSnapshot?.DetailedClrResolutionErrors ?? interop.ExposeDetailedResolutionErrors)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.DetailedClrErrorsEnabled,
                "Detailed CLR resolution failures disclose host surface information.",
                "Disable detailed resolution errors for untrusted output.");
        }

        if (interop.ExposeDetailedExceptionMessages)
        {
            Error(diagnostics, SecurityDiagnosticCodes.DetailedClrExceptionMessagesEnabled,
                "CLR exception messages are exposed to script.",
                "Use generic script-visible errors and bounded host-only diagnostics.");
        }

        if (interop.ChainClrExceptionAsInnerException)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.ClrExceptionChainingEnabled,
                "CLR exceptions are chained into host-rendered exceptions.",
                "Keep chained exceptions away from untrusted responses and bounded logs.");
        }

        if (interop.ClrExceptionErrorDecorator is not null || interop.ClrResolutionErrorDecorator is not null)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.ClrErrorDecoratorConfigured,
                "A CLR error decorator can publish host-derived data to script.",
                "Emit only bounded non-sensitive constants.");
        }

        if (!ReferenceEquals(interop.ExceptionHandler, Options.InteropOptions._defaultExceptionHandler)
            || !ReferenceEquals(interop.WrapObjectHandler, Options.InteropOptions._defaultWrapObjectHandler)
            || !ReferenceEquals(interop.MemberAccessor, Options.InteropOptions._defaultMemberAccessor)
            || interop.BuildCallStackHandler is not null
            || interop.ObjectConverters.Count > 0
            || interop.SerializeToJson is not null
            || !ReferenceEquals(interop.CreateClrObject, Options.InteropOptions._defaultCreateClrObject)
            || !ReferenceEquals(interop.CreateTypeReferenceObject, Options.InteropOptions._defaultCreateTypeReferenceObject)
            || !ReferenceEquals(interop.ObjectWrapperReportedPropertyKeys, Options.InteropOptions._defaultReportedPropertyKeys)
            || !ReferenceEquals(options.Host.FunctionToStringHandler, Options.HostOptions._defaultFunctionToStringHandler)
            || (engineSnapshot?.CustomTypeConverter ?? options.HasUserTypeConverter)
            || !ReferenceEquals(options.ReferenceResolver, DefaultReferenceResolver.Instance))
        {
            Warning(diagnostics, SecurityDiagnosticCodes.ClrCallbackConfigured,
                "Custom CLR callbacks or converters can execute host code or expose values.",
                "Audit authority, cancellation, reentrancy, exceptions, and output bounds.");
        }

        if (engineSnapshot?.ExtensionMethodsConfigured ?? interop.ExtensionMethodTypes.Count > 0)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ClrExtensionMethodsConfigured,
                "CLR extension methods are installed on JavaScript prototypes.",
                "Remove extension methods from untrusted engines or audit every exposed method as a host capability.");
        }

        if (engineSnapshot?.OperatorOverloadingAllowed ?? interop.AllowOperatorOverloading)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ClrOperatorOverloadingEnabled,
                "CLR operator overloads can execute host methods.",
                "Disable Interop.AllowOperatorOverloading for untrusted engines.");
        }

        if (engineSnapshot?.NonPublicBindingFlags ?? HasNonPublicBindingFlags(interop))
        {
            Error(diagnostics, SecurityDiagnosticCodes.NonPublicClrMembersEnabled,
                "CLR member binding flags include non-public members.",
                "Use public instance fields/properties and public instance/static methods only.");
        }

        if (engineSnapshot?.ClrCompatibilityMode
            ?? (interop.Enabled && interop.ClrAccessConfiguration == ClrAccessConfiguration.Compatibility))
        {
            Error(diagnostics, SecurityDiagnosticCodes.ClrCompatibilityModeEnabled,
                "Parameterless AllowClr enabled compatibility-mode core CLR access.",
                "Use explicit assemblies with a positive member allowlist, or leave CLR access disabled.");
        }

        if (engineSnapshot?.ClrPolicyUnrestricted
            ?? (interop.Enabled && ReferenceEquals(interop.TypeResolver, TypeResolver.Default)))
        {
            Error(diagnostics, SecurityDiagnosticCodes.ClrPolicyUnrestricted,
                "CLR access uses the permissive default member-resolution policy.",
                "Use a dedicated TypeResolver with a positive member filter.");
        }

        if (options.Host.StringCompilationAllowed)
        {
            Error(diagnostics, SecurityDiagnosticCodes.StringCompilationEnabled,
                "Dynamic string compilation is enabled.",
                "Call DisableStringCompilation and keep parser limits for all remaining compilation paths.");
        }

        if (options.HasUserEngineConstructionCallback)
        {
            Error(diagnostics, SecurityDiagnosticCodes.EngineConstructionCallback,
                "A user callback runs during engine construction.",
                "Review the effective engine report after construction; do not replay the callback.");
        }

        if ((engineSnapshot?.DebuggerEnabled ?? options.Debugger.Enabled)
            || options.Debugger.StatementHandling != DebuggerStatementHandling.Ignore
            || options.Debugger.InitialStepMode != StepMode.None)
        {
            Error(diagnostics, SecurityDiagnosticCodes.DebuggerEnabled,
                "Debugger behavior is enabled.",
                "Disable debug mode, debugger statements, and initial stepping.");
        }
    }

    private static bool HasNonPublicBindingFlags(Options.InteropOptions interop)
        => (interop.ObjectWrapperReportedFieldBindingFlags & BindingFlags.NonPublic) != BindingFlags.Default
            || (interop.ObjectWrapperReportedPropertyBindingFlags & BindingFlags.NonPublic) != BindingFlags.Default
            || (interop.ObjectWrapperReportedMethodBindingFlags & BindingFlags.NonPublic) != BindingFlags.Default;

    private static void AddModules(
        Options options,
        IModuleLoader? effectiveModuleLoader,
        List<SecurityDiagnostic> diagnostics)
    {
        var modules = options.Modules;
        var moduleLoader = effectiveModuleLoader ?? modules.ModuleLoader;
        if (ReferenceEquals(moduleLoader, FailFastModuleLoader.Instance))
        {
            if (modules.RegisterRequire)
            {
                Warning(diagnostics, SecurityDiagnosticCodes.RequireWithoutModuleLoader,
                    "require is enabled with the fail-fast module loader.",
                    "Disable require or configure and secure a loader.");
            }

            return;
        }

        Warning(diagnostics, SecurityDiagnosticCodes.ModuleLoadingEnabled,
            "A module loader resolves script-controlled specifiers and executes host loader code.",
            "Bound loader I/O and use an explicit destination policy.");

        if (modules.MaxModuleCount == int.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ModuleCountUnlimited,
                "The aggregate module-count limit is unbounded.",
                "Set a finite positive engine-lifetime module count.");
        }

        if (modules.MaxTotalModuleSourceBytes == long.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ModuleSourceBytesUnlimited,
                "The aggregate module-source-byte limit is unbounded.",
                "Set a finite positive engine-lifetime source-byte budget.");
        }

        if (modules.MaxModuleGraphDepth == int.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ModuleGraphDepthUnlimited,
                "The per-load module graph-depth limit is unbounded.",
                "Set a finite positive graph-depth budget.");
        }

        if (modules.MaxModuleResolutionHops == int.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ModuleResolutionHopsUnlimited,
                "The per-load module resolution-hop limit is unbounded.",
                "Set a finite positive resolution-hop budget.");
        }

        var loaderRestricts = moduleLoader is DefaultModuleLoader { RestrictsToBasePath: true };
        if (!loaderRestricts && modules.LoadPolicy is null)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ModuleDestinationPolicyMissing,
                "The enabled module loader has no destination policy.",
                "Use a restricted default loader or an explicit scheme, origin, host, or root allowlist.");
        }
        else if (modules.LoadPolicy is ModuleAllowlistPolicy { HasDestinationBoundary: false })
        {
            Error(diagnostics, SecurityDiagnosticCodes.ModuleAllowlistInsufficient,
                "The assigned module allowlist has no host, origin, or filesystem-root boundary.",
                "Configure a host or origin for network modules, or a filesystem root for file modules.");
        }

        if (modules.MaxModuleGraphDepth != int.MaxValue || modules.MaxModuleResolutionHops != int.MaxValue)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.ModulePerLoadLimitsReset,
                "Depth and hop budgets reset per top-level load; count and bytes remain aggregate.",
                "Combine all four limits and bound top-level loads at the operation boundary.");
        }

        if (modules.ExposeDetailedLoadErrors)
        {
            Error(diagnostics, SecurityDiagnosticCodes.DetailedModuleErrorsEnabled,
                "Module failures expose detailed messages to script.",
                "Use generic script-visible errors and bounded host-only diagnostics.");
        }
    }

    private static void AddResources(
        Options options,
        bool includeEngineRegex,
        EngineSecurityConfigurationSnapshot? engineSnapshot,
        List<SecurityDiagnostic> diagnostics)
    {
        var constraints = options.Constraints;
        if (constraints.MaxArraySize == uint.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ArraySizeUnlimited,
                "JavaScript arrays can grow to the implementation maximum.",
                "Set a finite host-appropriate MaxArraySize.");
        }

        if (includeEngineRegex)
        {
            AddRegex(
                engineSnapshot?.RegexTimeout ?? constraints.RegexTimeout,
                SecurityDiagnosticCodes.RegexTimeoutUnbounded,
                SecurityDiagnosticCodes.RegexTimeoutLong,
                SecurityDiagnosticCodes.RegexTimeoutExcessive,
                "engine",
                diagnostics);
        }

        if (constraints.PromiseTimeout <= TimeSpan.Zero)
        {
            Error(diagnostics, SecurityDiagnosticCodes.PromiseTimeoutUnbounded,
                "Synchronous promise unwrapping can wait indefinitely.",
                "Set a positive finite PromiseTimeout.");
        }
        else if (constraints.PromiseTimeout > MaximumRecommendedWait)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.PromiseTimeoutLong,
                "The synchronous promise wait exceeds one second.",
                "Use one second or less, or prefer asynchronous host entry points.");
        }
    }

    private static void AddRegexOverride(TimeSpan timeout, List<SecurityDiagnostic> diagnostics)
        => AddRegex(
            timeout,
            SecurityDiagnosticCodes.RegexTimeoutOverrideUnbounded,
            SecurityDiagnosticCodes.RegexTimeoutOverrideLong,
            SecurityDiagnosticCodes.RegexTimeoutOverrideExcessive,
            "parsing or preparation",
            diagnostics);

    private static void AddRegex(
        TimeSpan timeout,
        string unboundedCode,
        string longCode,
        string excessiveCode,
        string surface,
        List<SecurityDiagnostic> diagnostics)
    {
        if (timeout <= TimeSpan.Zero)
        {
            Error(diagnostics, unboundedCode,
                $"The {surface} regex timeout is not positive.",
                "Use a positive finite regex timeout.");
        }
        else if (timeout >= MaximumAllowedRegexTimeout)
        {
            Error(diagnostics, excessiveCode,
                $"The {surface} regex timeout exceeds the fixed upper bound.",
                "Use less than one year; one second or less is recommended.");
        }
        else if (timeout > MaximumRecommendedWait)
        {
            Warning(diagnostics, longCode,
                $"The {surface} regex timeout exceeds one second.",
                "Use one second or less unless a measured workload requires more.");
        }
    }

    private static void AddResultLimits(ResultLimits limits, List<SecurityDiagnostic> diagnostics)
    {
        if (limits.MaxDepth == int.MaxValue
            || limits.MaxPropertyCount == long.MaxValue
            || limits.MaxStringLength == int.MaxValue
            || limits.MaxOutputCharacters == long.MaxValue
            || limits.MaxOutputBytes == long.MaxValue)
        {
            Error(diagnostics, SecurityDiagnosticCodes.ResultLimitsUnlimited,
                "One or more result conversion or JSON serialization limits are unbounded.",
                "Bound every ResultLimits dimension and external serializers, responses, and logs.");
        }
    }

    private static void AddConstraintRegistration(Options options, List<SecurityDiagnostic> diagnostics)
    {
        if (options.Constraints.Constraints.Count > 0)
        {
            Warning(diagnostics, SecurityDiagnosticCodes.SharedConstraintInstance,
                "A mutable Constraint instance is shared by engines built from these options.",
                "Register a factory when options can build more than one engine.");
        }
    }

    private static bool IsConstraintCode(string code)
        => code is
            SecurityDiagnosticCodes.StatementLimitMissing
            or SecurityDiagnosticCodes.StatementLimitNonPositive
            or SecurityDiagnosticCodes.StatementLimitSaturated
            or SecurityDiagnosticCodes.MemoryLimitMissing
            or SecurityDiagnosticCodes.MemoryLimitNonPositive
            or SecurityDiagnosticCodes.MemoryLimitSaturated
            or SecurityDiagnosticCodes.TimeoutMissing
            or SecurityDiagnosticCodes.TimeoutNonPositive
            or SecurityDiagnosticCodes.TimeoutSaturated
            or SecurityDiagnosticCodes.CancellationMissing
            or SecurityDiagnosticCodes.OperationDeadlineMissing;

    private static int? Minimum(int? left, int? right)
        => !left.HasValue ? right : !right.HasValue ? left : Math.Min(left.Value, right.Value);

    private static SecurityConfigurationReport CreateReport(List<SecurityDiagnostic> diagnostics)
    {
        diagnostics.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Code, right.Code));
        return new SecurityConfigurationReport(diagnostics);
    }

    private static void ValidatePolicy(SecurityConfigurationPolicy policy)
    {
        if (policy != SecurityConfigurationPolicy.UntrustedScripts)
        {
            Throw.ArgumentOutOfRangeException(nameof(policy), "Unknown security configuration policy.");
        }
    }

    private static void ThrowIfErrors(SecurityConfigurationReport report)
    {
        if (report.HasErrors)
        {
            throw new SecurityConfigurationException(report);
        }
    }

    private static void ArgumentNull(object? value, string paramName)
    {
        if (value is null)
        {
            Throw.ArgumentNullException(paramName);
        }
    }

    private static void Error(
        List<SecurityDiagnostic> diagnostics,
        string code,
        string message,
        string guidance)
        => diagnostics.Add(new SecurityDiagnostic(code, SecurityDiagnosticSeverity.Error, message, guidance));

    private static void Warning(
        List<SecurityDiagnostic> diagnostics,
        string code,
        string message,
        string guidance)
        => diagnostics.Add(new SecurityDiagnostic(code, SecurityDiagnosticSeverity.Warning, message, guidance));
}
