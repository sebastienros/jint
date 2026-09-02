using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Constraints;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

/// <summary>
/// The configuration operations that are not a property assignment.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Options"/> and its groups are the configuration surface: every setting is a property, and a
/// property is the only place its value lives. A method exists here only when it does something an
/// assignment cannot — install a subsystem, append to a registry, register a factory the engine invokes
/// later, set two coupled values at once, or apply a named profile — and its verb says which of those it is:
/// </para>
/// <list type="table">
/// <item><term><c>Use*</c></term><description>installs a subsystem the engine otherwise does not have.</description></item>
/// <item><term><c>Add*</c> / <c>Remove*</c></term><description>appends to, or prunes, a registry that holds many.</description></item>
/// <item><term><c>Allow*</c></term><description>grants script a capability that is denied by default.</description></item>
/// <item><term><c>Limit*</c></term><description>registers a built-in budget constraint.</description></item>
/// <item><term><c>Observe*</c></term><description>registers a built-in constraint that watches host state.</description></item>
/// <item><term><c>Set*</c></term><description>replaces one host-supplied service whose registration a property assignment cannot express.</description></item>
/// <item><term><c>For*</c></term><description>applies a named profile.</description></item>
/// <item><term><c>Expose*</c></term><description>widens what script may see, across more than one group.</description></item>
/// <item><term><c>Catch*</c></term><description>routes CLR exceptions into script.</description></item>
/// <item><term><c>Configure</c></term><description>runs a callback against the engine being built.</description></item>
/// <item><term><c>Validate*</c> / <c>Ensure*</c></term><description>inspects configuration; never changes it.</description></item>
/// </list>
/// <para>
/// Through 4.16.x this was a "compatibility layer to allow fluent syntax", and about twenty of its members
/// mirrored a property one for one. Those are gone; assign the property.
/// </para>
/// </remarks>
public static class OptionsExtensions
{
    /// <summary>
    /// Shared by both <c>AllowClr</c> overloads, so the two cannot drift.
    /// </summary>
    private const string AllowClrRequiresUnreferencedCodeMessage =
        "AllowClr lets script name CLR types and namespaces as strings, so nothing in the host statically references what it exposes and trimming is free to remove all of it. When trimming, either root the assemblies you allow (<TrimmerRootAssembly Include=\"YourAssembly\" />) or do not call AllowClr at all and hand each type to script explicitly instead - Engine.SetValue<T>, TypeReference.CreateTypeReference<T> and ModuleBuilder.ExportType<T> carry [DynamicallyAccessedMembers] annotations that preserve exactly what Jint reflects over, and need no root.";

    /// <summary>
    /// Configures the engine to run untrusted JavaScript with a hardened static surface and explicit,
    /// request-appropriate resource limits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The profile disables CLR namespace and reflection access, registered extension methods, module loading,
    /// debugger handling, agent suspension, writes through projected CLR objects, live CLR array views and
    /// dynamic string compilation. It also enables the native stack-overflow guard.
    /// </para>
    /// <para>
    /// Every resource limit is required through <paramref name="limits"/>; there are no universal defaults
    /// suitable for every workload. Per-entry limits are enforced automatically. A host operation made from
    /// several entries must additionally be enclosed in
    /// <see cref="UntrustedCodeLimits.BeginOperation(Engine, System.Threading.CancellationToken)"/>.
    /// </para>
    /// <para>
    /// The profile declaration is immutable engine input: each engine receives a private effective options
    /// snapshot, applies the profile before construction, and reapplies it after user construction callbacks.
    /// The source <see cref="Options"/> remains unchanged and can be shared by concurrently constructed engines.
    /// User callbacks remain visible to security diagnostics but cannot reopen profile-controlled settings.
    /// </para>
    /// <para>
    /// Declare it before the engine is constructed. That first expansion is what hardens the realm, the host
    /// and every option-derived field, so a profile that first appears inside a callback registered with
    /// <see cref="Configure"/> is refused rather than half-applied.
    /// </para>
    /// </remarks>
    /// <param name="options">Options to harden.</param>
    /// <param name="limits">Finite limits chosen for the host request and workload.</param>
    /// <returns>The options instance for fluent configuration.</returns>
    public static Options ForUntrustedCode(this Options options, UntrustedCodeLimits limits)
    {
        if (limits is null)
        {
            Throw.ArgumentNullException(nameof(limits));
        }

        options.UntrustedCodeLimits = limits;
        return options;
    }

    internal static void ApplyUntrustedCodeOptions(Options options, UntrustedCodeLimits limits)
    {
        options.UntrustedCodeLimits = limits;
        options.Constraints.Constraints.Clear();
        options.Constraints.ConstraintFactories.Clear();
        options.Constraints.RequestedMaxStatements = null;
        options.Constraints.RequestedMemoryLimit = null;
        options.Constraints.RequestedTimeoutInterval = null;
        options.Constraints.CancellationConstraintRequested = false;
        options.Constraints.OperationDeadlineConstraintRequested = false;

        options.Interop.Enabled = false;
        options.Interop.AllowedAssemblies.Clear();
        options.Interop.AllowGetType = false;
        options.Interop.AllowSystemReflection = false;
        options.Interop.AllowWrite = false;
        options.Interop.AllowOperatorOverloading = false;
        options.Interop.ArrayConversion = ArrayConversionMode.Copy;
        options.Interop.ExtensionMethodTypes.Clear();
        options.Interop.ObjectConverters.Clear();
        options.Interop.ImmutableCrossingTypes.Clear();
        options.Interop.WrapObjectHandler = Options.InteropOptions._defaultWrapObjectHandler;
        options.Interop.MemberAccessor = Options.InteropOptions._defaultMemberAccessor;
        options.Interop.ExceptionHandler = Options.InteropOptions._defaultExceptionHandler;
        options.Interop.BuildCallStackHandler = null;
        options.Interop.SerializeToJson = null;
        options.Interop.CreateClrObject = Options.InteropOptions._defaultCreateClrObject;
        options.Interop.CreateTypeReferenceObject = Options.InteropOptions._defaultCreateTypeReferenceObject;
        options.Interop.ObjectWrapperReportedPropertyKeys = Options.InteropOptions._defaultReportedPropertyKeys;
        options.Interop.ExposeDetailedExceptionMessages = false;
        options.Interop.ExposeDetailedResolutionErrors = false;
        options.Interop.ChainClrExceptionAsInnerException = false;
        options.Interop.ClrExceptionErrorDecorator = null;
        options.Interop.ClrResolutionErrorDecorator = null;
        options.Interop.ObjectWrapperReportedFieldBindingFlags = BindingFlags.Instance | BindingFlags.Public;
        options.Interop.ObjectWrapperReportedPropertyBindingFlags = BindingFlags.Instance | BindingFlags.Public;
        options.Interop.ObjectWrapperReportedMethodBindingFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        options.Modules.ModuleLoader = FailFastModuleLoader.Instance;
        options.Modules.RegisterRequire = false;
        options.Modules.MaxModuleCount = limits.MaxModuleCount;
        options.Modules.MaxTotalModuleSourceBytes = limits.MaxTotalModuleSourceBytes;
        options.Modules.MaxModuleGraphDepth = limits.MaxModuleGraphDepth;
        options.Modules.MaxModuleResolutionHops = limits.MaxModuleResolutionHops;
        options.Modules.LoadPolicy = null;
        options.Modules.ExposeDetailedLoadErrors = false;

        options.Debugger.Enabled = false;
        options.Debugger.StatementHandling = Runtime.Debugger.DebuggerStatementHandling.Ignore;
        options.Debugger.InitialStepMode = StepMode.None;

        options.AgentCanSuspend = false;
        options.ExperimentalFeatures = ExperimentalFeature.None;
        options.RetainFunctionSourceText = false;
        options.Host.StringCompilationAllowed = false;
        options.Host.Factory = Options.HostOptions._defaultFactory;
        options.Host.FunctionToStringHandler = Options.HostOptions._defaultFunctionToStringHandler;
        options.SetReferenceResolverCore(DefaultReferenceResolver.Instance, ReferenceResolverInterests.None);
        options.Constraints.MaxExecutionStackCount = StackGuard.Disabled;
        options.Constraints.StackOverflowGuard = true;
        options.Constraints.CancellationConstraintRequested = true;
        options.Parsing.MaxSourceLength = limits.MaxSourceLength;
        options.Parsing.MaxNodeCount = limits.MaxNodeCount;
        options.ResultLimits = limits.ResultLimits;

        options.Constraints.MaxRecursionDepth = limits.MaxRecursionDepth;
        options.Constraints.MaxArraySize = limits.MaxArraySize;
        options.Constraints.RegexTimeout = limits.RegexTimeout;
        options.Constraints.PromiseTimeout = limits.PromiseTimeout;

        options.LimitExecutionTime(limits.TimeoutInterval);
        options.LimitStatements(limits.MaxStatements);
        options.LimitMemory(limits.MemoryLimit);
        options.AddConstraint(static () => new OperationDeadlineConstraint());
    }

    /// <summary>
    /// Re-expands the profile over whatever the host's configuration callbacks wrote, and undoes the two
    /// engine-level installations such a callback can replace.
    /// </summary>
    /// <remarks>
    /// The engine's constraints are not repaired here any more: they are built from
    /// <see cref="Options.Constraints"/> after <see cref="Options.Apply"/> returns, so they are built from
    /// the re-expanded options and there is nothing a callback could have reached to mutate. The three
    /// <c>Find&lt;T&gt;()!</c> calls that used to do the repairing dereferenced null for a profile declared
    /// from a callback, which is now refused outright (sebastienros/jint#3582).
    /// </remarks>
    internal static void ReapplyUntrustedCodeOptions(
        Options options,
        Engine engine,
        UntrustedCodeLimits limits)
    {
        ApplyUntrustedCodeOptions(options, limits);
        engine.TypeConverter = new DefaultTypeConverter(engine);
        engine.Modules = new Engine.ModuleOperations(engine, FailFastModuleLoader.Instance);
    }

    /// <summary>
    /// Adds an <see cref="ObjectConverter"/> instance to convert CLR values to <see cref="JsValue"/>.
    /// </summary>
    /// <inheritdoc cref="AddObjectConverter(Options, ObjectConverter)" path="/remarks"/>
    public static Options AddObjectConverter<T>(this Options options) where T : ObjectConverter, new()
    {
        return AddObjectConverter(options, new T());
    }

    /// <summary>
    /// Adds an <see cref="ObjectConverter"/> instance to convert CLR values to <see cref="JsValue"/>.
    /// </summary>
    /// <remarks>
    /// A converter that does not declare the CLR types it handles — through
    /// <see cref="ObjectConverter.HandledTypes"/> or the overload below — can be handed any value, so this
    /// registration disarms the <b>compiled member-read lane</b> and the <b>compiled method-invoker lane</b>
    /// for every wrapped member and method: both produce the <see cref="JsValue"/> themselves, and a converter
    /// entitled to see every CLR value must be given the chance to.
    /// </remarks>
    public static Options AddObjectConverter(this Options options, ObjectConverter objectConverter)
    {
        options.Interop.ObjectConverters.Add(objectConverter);
        return options;
    }

    /// <summary>
    /// Adds an <see cref="ObjectConverter"/> instance to convert CLR values to <see cref="JsValue"/>, declaring
    /// the CLR types the converter can handle.
    /// </summary>
    /// <remarks>
    /// The declaration is a promise: the converter is still offered every value, but the engine is free to keep
    /// its compiled member-read fast lanes for members whose declared type cannot produce a value of any of the
    /// <paramref name="handledTypes"/> — such a member never reaches the converter. Declare a base type, an
    /// interface or <see cref="System.Enum"/> to cover a family of values, or use the overload without
    /// <paramref name="handledTypes"/> for a converter that inspects everything.
    /// <para>
    /// What it costs when it does not narrow: an undeclared registration disarms the compiled member-read and
    /// method-invoker lanes for every member and method. A declaration buys those back for the ones no declared
    /// type can reach — but never for a member or return type of <see cref="object"/>, which can hold anything.
    /// Every registered converter has to declare, since one that has not can be handed anything.
    /// </para>
    /// <para>
    /// A converter that declares through <see cref="ObjectConverter.HandledTypes"/> as well claims the union of
    /// the two, so this parameter can widen its declaration but never narrow it.
    /// </para>
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <param name="objectConverter">The converter to register.</param>
    /// <param name="handledTypes">
    /// The CLR types the converter handles. Values assignable to any of them (including through a declared
    /// base type or interface) are considered convertible by this converter.
    /// </param>
    public static Options AddObjectConverter(this Options options, ObjectConverter objectConverter, params Type[] handledTypes)
    {
        if (objectConverter is null)
        {
            Throw.ArgumentNullException(nameof(objectConverter));
        }

        if (handledTypes is null || handledTypes.Length == 0)
        {
            Throw.ArgumentException(
                "At least one handled type is required, use the overload without handled types for a converter that handles every value.",
                nameof(handledTypes));
        }

        foreach (var handledType in handledTypes!)
        {
            if (handledType is null)
            {
                Throw.ArgumentException("Handled types cannot contain null.", nameof(handledTypes));
            }
        }

        options.Interop.ObjectConverters.Add(new TypedObjectConverter(objectConverter!, handledTypes));
        return options;
    }

    /// <summary>
    /// Declares that instances of the given CLR types are immutable while they are exposed to this engine —
    /// their member and key set, and the values behind them, do not change.
    /// </summary>
    /// <remarks>
    /// The declaration is a promise, and in exchange the engine may cache what it reads through such an
    /// instance: memoized member results and stable child wrappers. A walk like
    /// <c>record.value.customer.country</c> over a wrapped dictionary graph therefore wraps each node once
    /// per wrapper lifetime instead of once per access, and a repeated read of the same key answers without
    /// touching reflection or the indexer at all. For a declared type this supersedes
    /// <see cref="Options.InteropOptions.CacheRecentObjectWrappers"/>, whose bounded ring cannot keep a
    /// nested walk's nodes.
    /// <para>
    /// A host that breaks the promise gets stale reads — the same class of consequence
    /// <see cref="Options.InteropOptions.TrackObjectWrapperIdentity"/> and a shared
    /// <see cref="Runtime.Interop.TypeResolver"/> already carry, and owned by the host in the same way. The
    /// one concession the engine makes is that a write through the wrapper evicts that key's memo, so a
    /// script that writes and then reads stays coherent even when the promise was wrong.
    /// </para>
    /// <para>
    /// The memo lives on the wrapper and dies with it, so it holds what that wrapper's subtree resolved to
    /// for as long as the wrapper is reachable — a root bound through <see cref="Engine.SetValue(string, object)"/>
    /// therefore keeps its whole walked subtree alive, the same trade
    /// <see cref="Options.InteropOptions.TrackObjectWrapperIdentity"/> makes. It also means the memo only
    /// pays while the wrapper lives: a node reached through a transient wrapper, such as an element of a
    /// wrapped list, still re-resolves on each crossing.
    /// </para>
    /// <para>
    /// Assignability is the rule, exactly as for
    /// <see cref="AddObjectConverter(Options, Runtime.Interop.ObjectConverter, Type[])"/>: declare an
    /// interface or a base type to cover a whole family (<c>typeof(IReadOnlyDictionary&lt;string, object&gt;)</c>
    /// for every implementation of it). Unlike that filter this one never guesses in the permissive
    /// direction — an open generic type declaration claims nothing, since a wrong claim here would serve
    /// stale reads rather than merely cost a fast lane.
    /// </para>
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <param name="types">The CLR types whose instances are immutable for the crossing.</param>
    public static Options AddImmutableCrossing(this Options options, params Type[] types)
    {
        if (types is null || types.Length == 0)
        {
            Throw.ArgumentException(
                "At least one type is required.",
                nameof(types));
        }

        foreach (var type in types!)
        {
            if (type is null)
            {
                Throw.ArgumentException("Types cannot contain null.", nameof(types));
            }
        }

        options.Interop.ImmutableCrossingTypes.AddRange(types!);
        return options;
    }

    /// <summary>
    /// Registers types whose public static methods are offered to script as extension methods on their
    /// first parameter's type.
    /// </summary>
    /// <remarks>
    /// The types are scanned by reflection, and the parameter cannot say so to the trimmer:
    /// <c>[DynamicallyAccessedMembers]</c> is only honoured on a <see cref="Type"/> or a
    /// <see cref="string"/>, never on an array of them. So a trimmed build keeps this array's
    /// <em>types</em> and can still drop the very methods being registered — and a dropped one is not an
    /// error, it is <c>company.shout is not a function</c> with no diagnostic anywhere. Root the declaring
    /// types when trimming.
    /// </remarks>
    [RequiresUnreferencedCode("Extension methods are discovered by reflecting over the supplied types' public methods, which trimming can remove - and a removed one fails as 'not a function' rather than as an error. The parameter cannot carry DynamicallyAccessedMembers because it is an array, so root the declaring types yourself: <TrimmerRootAssembly Include=\"YourAssembly\" /> or a [DynamicDependency] on each registered type.")]
    public static Options AddExtensionMethods(this Options options, params Type[] types)
    {
        options.Interop.ExtensionMethodTypes.AddRange(types);
        return options;
    }

    /// <summary>
    /// Sets the converter that turns script values into the CLR types the host's members and methods ask for.
    /// </summary>
    /// <remarks>
    /// A converter that does not declare the target types it handles — through
    /// <see cref="ClrTypeConverter.HandledTargetTypes"/> or the overload below — could answer any conversion
    /// differently from <see cref="DefaultTypeConverter"/>, so this registration disarms the <b>compiled
    /// member-write lane</b> and the <b>compiled method-invoker lane</b> for every member and method, and keeps
    /// every non-integer-keyed indexer accessor out of the accessor cache a
    /// <see cref="Options.InteropOptions.TypeResolver"/> shares between engines.
    /// <para>
    /// Deriving from <see cref="DefaultTypeConverter"/> to adjust one conversion is enough to reach all of that:
    /// what the engine gates on is the <em>declaration</em>, not the base class. Use the overload below.
    /// </para>
    /// </remarks>
    public static Options SetTypeConverter(this Options options, Func<Engine, ClrTypeConverter> typeConverterFactory)
    {
        return AddTypeConverterConfiguration(options, typeConverterFactory, handledTargetTypes: null);
    }

    /// <summary>
    /// Sets the type converter to use, declaring the CLR target types it converts to.
    /// </summary>
    /// <remarks>
    /// The declaration is a promise that for every <em>other</em> target type the converter produces exactly
    /// what <see cref="DefaultTypeConverter"/> produces, and in exchange the engine keeps its compiled
    /// member-write and method-invoker lanes for members and parameters typed that way, and keeps sharing their
    /// resolved accessors with engines running the stock converter.
    /// <para>
    /// The question is exact, unlike
    /// <see cref="AddObjectConverter(Options, ObjectConverter, Type[])"/>'s: a converter is handed the target
    /// <see cref="Type"/> itself rather than a value, so a declared type claims itself and its subtypes and
    /// nothing above them. Declaring an interface or a base type covers the family under it.
    /// </para>
    /// <para>
    /// Run with host-contract verification on to have the promise checked: the stock conversion is run beside
    /// the converter's for every undeclared target type, and a disagreement is reported instead of being
    /// silently bypassed.
    /// </para>
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <param name="typeConverterFactory">Builds the converter for each engine.</param>
    /// <param name="handledTargetTypes">
    /// The CLR target types this converter answers differently from <see cref="DefaultTypeConverter"/>. A
    /// declared type covers itself and everything assignable to it. Unioned with the converter's own
    /// <see cref="ClrTypeConverter.HandledTargetTypes"/>, so this can widen that declaration but never narrow it.
    /// </param>
    public static Options SetTypeConverter(
        this Options options,
        Func<Engine, ClrTypeConverter> typeConverterFactory,
        params Type[] handledTargetTypes)
    {
        if (handledTargetTypes is null || handledTargetTypes.Length == 0)
        {
            Throw.ArgumentException(
                "At least one handled target type is required, use the overload without handled target types for a converter that answers for every target type.",
                nameof(handledTargetTypes));
        }

        foreach (var handledType in handledTargetTypes!)
        {
            if (handledType is null)
            {
                Throw.ArgumentException("Handled target types cannot contain null.", nameof(handledTargetTypes));
            }
        }

        return AddTypeConverterConfiguration(options, typeConverterFactory, handledTargetTypes);
    }

    private static Options AddTypeConverterConfiguration(
        Options options,
        Func<Engine, ClrTypeConverter> typeConverterFactory,
        Type[]? handledTargetTypes)
    {
        if (typeConverterFactory is null)
        {
            Throw.ArgumentNullException(nameof(typeConverterFactory));
        }

        options.AddConfiguration(
            new EngineConfiguration(
                engine => engine.InstallTypeConverter(typeConverterFactory!(engine), handledTargetTypes),
                EngineConfigurationProvenance.User),
            nameof(SetTypeConverter));
        options.HasUserTypeConverter = true;
        return options;
    }

    /// <summary>
    /// Allows scripts to resolve CLR types from the core assembly containing <see cref="object"/> through
    /// <c>System</c> and <c>importNamespace</c>.
    /// </summary>
    /// <remarks>
    /// Script names the types it wants as strings, so nothing in the host statically references what this
    /// exposes and a trimmed build is free to remove all of it. Native AOT is a different question and a
    /// milder one — most of what CLR interop does works there; see <c>docs/v5-migration.md</c>.
    /// </remarks>
    [RequiresUnreferencedCode(AllowClrRequiresUnreferencedCodeMessage)]
    public static Options AllowClr(this Options options)
    {
        options.Interop.Enabled = true;
        options.Interop.ClrAccessConfiguration = ClrAccessConfiguration.Compatibility;
        options.Interop.AllowedAssemblies.Add(typeof(object).Assembly);
        RemoveDuplicateAssemblies(options.Interop.AllowedAssemblies);
        return options;
    }

    /// <summary>
    /// Allows scripts to resolve CLR types from the supplied assemblies through <c>System</c> and
    /// <c>importNamespace</c>.
    /// </summary>
    /// <remarks>
    /// The supplied assemblies are a closed namespace-resolution allow-list. They do not restrict CLR objects,
    /// delegates, or <see cref="TypeReference"/> values explicitly exported by the host.
    /// </remarks>
    [RequiresUnreferencedCode(AllowClrRequiresUnreferencedCodeMessage)]
    public static Options AllowClr(this Options options, params Assembly[] assemblies)
    {
        options.Interop.Enabled = true;
        options.Interop.ClrAccessConfiguration = ClrAccessConfiguration.Explicit;
        options.Interop.AllowedAssemblies.AddRange(assemblies);
        RemoveDuplicateAssemblies(options.Interop.AllowedAssemblies);
        return options;
    }

    /// <summary>
    /// De-duplicates the allow-list in place, keeping the first occurrence of each assembly.
    /// </summary>
    /// <remarks>
    /// Both <c>AllowClr</c> overloads used to end with <c>AllowedAssemblies = AllowedAssemblies.Distinct().ToList()</c>.
    /// Replacing the registry wholesale is exactly what a registry that has to refuse changes cannot allow, so
    /// the de-duplication happens through the registry instead of around it.
    /// </remarks>
    private static void RemoveDuplicateAssemblies(OptionsList<Assembly> assemblies)
    {
        var seen = new HashSet<Assembly>();
        assemblies.RemoveAll(assembly => !seen.Add(assembly));
    }

    /// <summary>
    /// Exceptions thrown from CLR code are converted to JavaScript errors and can be caught by a script's
    /// <c>try</c>/<c>catch</c>. By default they bubble to the CLR host and interrupt execution.
    /// </summary>
    /// <remarks>
    /// The preset for the predicate every host writes — <c>options.Interop.ExceptionHandler = static _ =&gt;
    /// true</c>. To convert only some exceptions, assign that property yourself; the overload that took a
    /// predicate was exactly that assignment and is gone.
    /// </remarks>
    public static Options CatchClrExceptions(this Options options)
    {
        options.Interop.ExceptionHandler = static _ => true;
        return options;
    }

    /// <summary>
    /// Exposes detailed CLR exception, CLR resolution and module loading messages to script code.
    /// Off by default because those messages can contain host types, signatures, paths, URLs and inner-system
    /// details. Intended for trusted development environments only.
    /// </summary>
    /// <remarks>
    /// This does not control host-side diagnostics. Original CLR and module exceptions remain available through
    /// <see cref="JintException.TryGetClrException"/>, and CLR resolution metadata through
    /// <see cref="JintException.TryGetClrType"/> and <see cref="JintException.TryGetClrMemberName"/>, whether
    /// detailed messages are exposed or not.
    /// </remarks>
    public static Options ExposeDetailedErrors(this Options options, bool expose = true)
    {
        options.Interop.ExposeDetailedExceptionMessages = expose;
        options.Interop.ExposeDetailedResolutionErrors = expose;
        options.Modules.ExposeDetailedLoadErrors = expose;
        return options;
    }

    /// <summary>
    /// Registers a single constraint instance.
    /// </summary>
    /// <remarks>
    /// The instance is shared by every engine built from these options. Constraints normally carry
    /// per-execution state, so this overload is only safe for options that build exactly one engine.
    /// Use <see cref="AddConstraint(Options, Func{Constraint})"/> when the same options are reused for
    /// several engines.
    /// </remarks>
    public static Options AddConstraint(this Options options, Constraint constraint)
    {
        if (constraint != null)
        {
            options.Constraints.Constraints.Add(constraint);
            options.Constraints.OperationDeadlineConstraintRequested |= constraint is Constraints.OperationDeadlineConstraint;
        }

        return options;
    }

    /// <summary>
    /// Registers a factory that produces a constraint. Each engine built from these options invokes the
    /// factory exactly once while constructing, so no per-execution constraint state is ever shared
    /// between engines — which makes it safe to build many engines, including concurrently running ones,
    /// from a single <see cref="Options"/> instance.
    /// </summary>
    /// <param name="options">The engine options.</param>
    /// <param name="constraintFactory">
    /// Produces a fresh, unconfigured constraint instance on every invocation. It must not hand out the
    /// same instance twice, otherwise the isolation this overload exists for is lost, and it must have no
    /// side effect beyond creating the constraint: <see cref="RemoveConstraints"/> invokes it once more to
    /// classify the registration.
    /// </param>
    /// <returns>The options instance for fluent configuration.</returns>
    public static Options AddConstraint(this Options options, Func<Constraint> constraintFactory)
    {
        if (constraintFactory != null)
        {
            options.Constraints.ConstraintFactories.Add(constraintFactory);
        }

        return options;
    }

    /// <summary>
    /// Registers a typed constraint factory while preserving the declared constraint kind for configuration
    /// diagnostics. The factory is still invoked exactly once per engine and is never invoked by diagnostics.
    /// </summary>
    public static Options AddConstraint<TConstraint>(this Options options, Func<TConstraint> constraintFactory)
        where TConstraint : Constraint
    {
        if (constraintFactory is not null)
        {
            options.Constraints.ConstraintFactories.Add(() => constraintFactory());
            options.Constraints.OperationDeadlineConstraintRequested |= typeof(TConstraint) == typeof(Constraints.OperationDeadlineConstraint);
        }

        return options;
    }

    /// <summary>
    /// Removes every registered constraint matching <paramref name="predicate"/>.
    /// </summary>
    /// <remarks>
    /// A factory registration has no instance to test against, so the predicate is evaluated against a
    /// throw-away probe obtained from the factory — which is why a factory must not do anything beyond
    /// creating the constraint. Because a factory is required to return a fresh, unconfigured instance,
    /// the classification predicates this method is used with (<c>x =&gt; x is SomeConstraint</c>) select
    /// exactly the same registrations they would have selected before the registration became
    /// factory-based, which is what keeps the "replace the constraint of this kind" behaviour of
    /// <see cref="ConstraintsOptionsExtensions"/> intact.
    /// </remarks>
    public static Options RemoveConstraints(this Options options, Predicate<Constraint> predicate)
    {
        var removedMaxStatements = false;
        var removedMemoryLimit = false;
        var removedTimeout = false;
        var removedCancellation = false;
        var removedOperationDeadline = false;

        void RecordRemoval(Constraint constraint)
        {
            removedMaxStatements |= constraint is Constraints.MaxStatementsConstraint;
            removedMemoryLimit |= constraint is Constraints.MemoryLimitConstraint;
            removedTimeout |= constraint is Constraints.TimeConstraint;
            removedCancellation |= constraint is Constraints.CancellationConstraint;
            removedOperationDeadline |= constraint is Constraints.OperationDeadlineConstraint;
        }

        options.Constraints.Constraints.RemoveAll(constraint =>
        {
            if (!predicate(constraint))
            {
                return false;
            }

            RecordRemoval(constraint);
            return true;
        });
        options.Constraints.ConstraintFactories.RemoveAll(factory =>
        {
            var probe = factory();
            if (probe is null || !predicate(probe))
            {
                return false;
            }

            RecordRemoval(probe);
            return true;
        });

        if (removedMaxStatements)
        {
            options.Constraints.RequestedMaxStatements = null;
        }

        if (removedMemoryLimit)
        {
            options.Constraints.RequestedMemoryLimit = null;
        }

        if (removedTimeout)
        {
            options.Constraints.RequestedTimeoutInterval = null;
        }

        if (removedCancellation)
        {
            options.Constraints.CancellationConstraintRequested = false;
        }

        if (removedOperationDeadline)
        {
            options.Constraints.OperationDeadlineConstraintRequested = false;
        }

        return options;
    }


    /// <summary>
    /// Registers a reference resolver together with the situations it wants to be consulted for. The engine
    /// does not call the resolver for anything outside <paramref name="interests"/>, and keeps the
    /// interpreter fast paths those situations would otherwise disable — see
    /// <see cref="ReferenceResolverInterests"/>.
    /// </summary>
    /// <remarks>
    /// One call rather than two assignments, because the two values are one registration: an interest set
    /// describes one particular resolver, so it must neither survive it nor be inherited by its replacement.
    /// Through 4.16.x both were settable properties and assigning <see cref="Options.ReferenceResolver"/>
    /// silently reset the interests to <see cref="ReferenceResolverInterests.All"/>, which made the result
    /// depend on the order the two assignments happened to be written in.
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <param name="resolver">The resolver to register.</param>
    /// <param name="interests">
    /// The situations the resolver wants to be consulted for. Defaults to
    /// <see cref="ReferenceResolverInterests.All"/>, which is how every resolver behaved before the filter
    /// existed.
    /// </param>
    public static Options SetReferenceResolver(
        this Options options,
        IReferenceResolver resolver,
        ReferenceResolverInterests interests = ReferenceResolverInterests.All)
    {
        if (resolver is null)
        {
            Throw.ArgumentNullException(nameof(resolver));
        }

        options.SetReferenceResolverCore(resolver!, interests);
        return options;
    }

    /// <summary>
    /// Registers a global whose value <paramref name="valueFactory"/> produces on the first read, rather
    /// than when the engine is created.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The property itself is installed eagerly, so <c>in</c>, <c>hasOwnProperty</c> and
    /// <c>Object.keys(globalThis)</c> see it without materializing anything. Only the value waits, and
    /// <c>typeof</c> counts as a read of it.
    /// </para>
    /// <para>
    /// The factory runs once per engine, receiving the engine being built, so one <see cref="Options"/>
    /// may be shared by any number of engines. It must not return <see langword="null"/>;
    /// <see cref="JsValue.Undefined"/> is stored if it does.
    /// </para>
    /// <para>
    /// Deleting the global before any read skips the factory. Overwriting or redefining it before any read
    /// still runs the factory once and discards the result, because <c>[[DefineOwnProperty]]</c> reads the
    /// current value before replacing it.
    /// </para>
    /// <para>
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> re-arms a global whose factory had not
    /// run when the snapshot was taken, so it runs again — a host pooling engines depends on that. Whether
    /// the second run produces a fresh value is the factory's business: one that hands back something it
    /// holds gives the next cycle exactly what the previous one mutated.
    /// </para>
    /// </remarks>
    /// <example>
    /// Deferring a CLR type projection, which otherwise resolves the type's members at engine-construction
    /// time whether or not the script mentions it:
    /// <code>
    /// options.AddLazyGlobal("DateTime", static engine => TypeReference.CreateTypeReference&lt;DateTime&gt;(engine));
    ///
    /// options.AddLazyGlobal(
    ///     "log",
    ///     engine => new ClrFunction(engine, "log", (_, args) => { Console.WriteLine(args.At(0)); return JsValue.Undefined; }),
    ///     PropertyFlag.NonEnumerable);
    /// </code>
    /// </example>
    /// <param name="options">The options to modify.</param>
    /// <param name="name">The global property name.</param>
    /// <param name="valueFactory">
    /// The factory producing the value for a given engine, invoked on the first read — so unlike a
    /// <see cref="UseHostFactory{T}"/> callback it sees a fully built engine.
    /// </param>
    /// <param name="flags">
    /// The property attributes. Defaults to configurable, enumerable and writable, which is what
    /// <see cref="Engine.SetValue(string, JsValue)"/> produces and <b>not</b> the
    /// <see cref="PropertyFlag.NonEnumerable"/> that <see cref="Engine.SetValue(string, Delegate)"/> uses.
    /// </param>
    public static Options AddLazyGlobal(
        this Options options,
        string name,
        Func<Engine, JsValue> valueFactory,
        PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
    {
        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        if (valueFactory is null)
        {
            Throw.ArgumentNullException(nameof(valueFactory));
        }

        options.AddConfiguration(new EngineConfiguration(
            engine => engine.Realm.GlobalObject.SetProperty(
                name,
                new LazyPropertyDescriptor<Engine>(engine, valueFactory, flags)),
            EngineConfigurationProvenance.FirstParty));

        return options;
    }

    /// <summary>
    /// Registers some custom logic to apply on an <see cref="Engine"/> instance when the options
    /// are loaded.
    /// </summary>
    /// <remarks>
    /// The callback runs from <c>Options.Apply</c>, with the engine mid-construction. Its realm, its module
    /// operations and CLR conversion are built by then, so registering values, globals and programmatic
    /// modules is what it is for.
    /// <para>
    /// It cannot run script: <c>Execute</c>, <c>Evaluate</c>, <c>Invoke</c> and module import are refused
    /// while the constructor is in flight, because the engine's own globals (<c>System</c>,
    /// <c>importNamespace</c>, <c>clrHelper</c>, <c>require</c>, the opt-in web APIs) are installed after
    /// these callbacks and an untrusted-code profile is re-applied after them. Run the script once the
    /// constructor has returned.
    /// </para>
    /// <para>
    /// Writing to the options from here <em>is</em> honoured — every field the engine derives from an option
    /// is taken once these callbacks have run. Two things are refused rather than ignored: declaring an
    /// untrusted-code profile (<see cref="ForUntrustedCode"/>), which has to be in force before the engine
    /// starts building, and reaching this engine's own constraint instances, which are built afterwards.
    /// </para>
    /// </remarks>
    /// <param name="options">Options to modify</param>
    /// <param name="configuration">The action to register.</param>
    public static Options Configure(this Options options, Action<Engine> configuration)
    {
        options.AddConfiguration(new EngineConfiguration(configuration, EngineConfigurationProvenance.User));
        return options;
    }

    internal static Options ConfigureFirstParty(this Options options, Action<Engine> configuration)
    {
        options.AddConfiguration(new EngineConfiguration(configuration, EngineConfigurationProvenance.FirstParty));
        return options;
    }

    /// <summary>
    /// Allows to configure how the host is constructed.
    /// </summary>
    /// <remarks>
    /// Passed Engine instance is still in construction and should not be used during call stage.
    /// </remarks>
    public static Options UseHostFactory<T>(this Options options, Func<Engine, T> factory) where T : Host
    {
        options.HasUserHostFactory = true;
        options.Host.Factory = factory;
        return options;
    }

    /// <summary>
    /// Installs the file-system module loader rooted at <paramref name="basePath"/>. There is no sand-boxing
    /// beyond <paramref name="restrictToBasePath"/>, so the script that names a specifier has to be trusted
    /// not to name something it should not reach.
    /// </summary>
    /// <remarks>
    /// Named <c>EnableModules</c> through 4.16.x. The engine has no module loader until one is installed, so
    /// this is <c>Use*</c> like every other subsystem installation; <c>Modules.RegisterRequire</c> is the
    /// separate switch that additionally exposes CommonJS <c>require</c> to script.
    /// </remarks>
    public static Options UseModules(this Options options, string basePath, bool restrictToBasePath = true)
    {
        return UseModules(options, new DefaultModuleLoader(basePath, restrictToBasePath));
    }

    /// <summary>
    /// Installs a custom module loader. Named <c>EnableModules</c> through 4.16.x.
    /// </summary>
    public static Options UseModules(this Options options, IModuleLoader moduleLoader)
    {
        options.Modules.ModuleLoader = moduleLoader;
        return options;
    }
}
