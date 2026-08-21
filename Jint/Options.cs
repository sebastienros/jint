using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Intl;
using Jint.Native.Object;
using Jint.Native.Temporal;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Coverage;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

public partial class Options
{
    private static readonly CultureInfo _defaultCulture = CultureInfo.CurrentCulture;
    private static readonly TimeZoneInfo _defaultTimeZone = TimeZoneInfo.Local;

    private ITimeSystem? _timeSystem;
    internal List<EngineConfiguration> _configurations { get; private set; } = new();
    internal bool HasUserHostFactory { get; set; }
    internal bool HasUserEngineConstructionCallback =>
        HasUserHostFactory
        || _configurations.Exists(static configuration => configuration.Provenance == EngineConfigurationProvenance.User);
    internal UntrustedCodeLimits? UntrustedCodeLimits { get; set; }

    public delegate JsValue? MemberAccessorDelegate(Engine engine, object target, string member);

    public delegate ObjectInstance? WrapObjectDelegate(Engine engine, object target, Type? type);

    public delegate bool ExceptionHandlerDelegate(Exception exception);

    public delegate void ClrExceptionErrorDecoratorDelegate(Engine engine, ObjectInstance error, Exception exception);

    public delegate void ClrResolutionErrorDecoratorDelegate(Engine engine, ObjectInstance error, ClrResolutionErrorInfo info);

    public delegate string? BuildCallStackDelegate(string shortDescription, SourceLocation location, string[]? arguments);

    public delegate string SerializeToJsonDelegate(object? target, string space, string? currentIndent);

    public delegate IEnumerable<JsValue>? ReportedPropertyKeysDelegate(Engine engine, object target);

    /// <summary>
    /// Execution constraints for the engine.
    /// </summary>
    public ConstraintOptions Constraints { get; private set; } = new();

    /// <summary>
    /// Resource limits applied while parsing scripts and modules.
    /// </summary>
    public ParsingOptions Parsing { get; private set; } = new();

    /// <summary>
    /// CLR interop related options.
    /// </summary>
    public InteropOptions Interop { get; private set; } = new();

    /// <summary>
    /// Debugger configuration.
    /// </summary>
    public DebuggerOptions Debugger { get; private set; } = new();

    /// <summary>
    /// Script code coverage configuration. Off by default.
    /// </summary>
    public CoverageOptions Coverage { get; } = new();

    /// <summary>
    /// Host options.
    /// </summary>
    public HostOptions Host { get; private set; } = new();

    /// <summary>
    /// Module options
    /// </summary>
    public ModuleOptions Modules { get; private set; } = new();

    /// <summary>
    /// Internationalization (Intl) options.
    /// </summary>
    public IntlOptions Intl { get; } = new();

    /// <summary>
    /// Temporal API options.
    /// </summary>
    public TemporalOptions Temporal { get; } = new();

    /// <summary>
    /// Whether the code should be always considered to be in strict mode. Can improve performance.
    /// </summary>
    public bool Strict { get; set; }

    /// <summary>
    /// Whether the engine's default parser retains the full source text of parsed functions so that
    /// <see cref="Native.Function.Function.ToString"><c>Function.prototype.toString()</c></see> can return
    /// the original source. Defaults to <see langword="false"/>, in which case <c>toString()</c> returns a
    /// <c>function name() { [native code] }</c> placeholder and the script source is not kept in memory.
    /// </summary>
    /// <remarks>
    /// This only affects scripts/modules parsed by the engine itself (e.g. <see cref="Engine"/>'s <c>Execute(string)</c>).
    /// When parsing via an explicit <see cref="ScriptParsingOptions"/>/<see cref="ModuleParsingOptions"/> or a
    /// prepared script/module, the corresponding <see cref="IParsingOptions.RetainFunctionSourceText"/> setting applies.
    /// </remarks>
    public bool RetainFunctionSourceText { get; set; }

    /// <summary>
    /// The culture the engine runs on, defaults to current culture.
    /// </summary>
    public CultureInfo Culture { get; set; } = _defaultCulture;

    /// <summary>
    /// Configures a time system to use. Defaults to DefaultTimeSystem using local time.
    /// </summary>
    public ITimeSystem TimeSystem
    {
        get => _timeSystem ??= new DefaultTimeSystem(TimeZone, Culture);
        set => _timeSystem = value;
    }

    /// <summary>
    /// The time zone the engine runs on, defaults to local. Same as setting DefaultTimeSystem with the time zone.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = _defaultTimeZone;

    /// <summary>
    /// Reference resolver allows customizing behavior for reference resolving. This can be useful in cases where
    /// you want to ignore long chain of property accesses that might throw if anything is null or undefined.
    /// An example of such is <code>var a = obj.field.subField.value</code>. Custom resolver could accept chain to return
    /// null/undefined on first occurrence.
    /// </summary>
    /// <remarks>
    /// Assigning a resolver resets <see cref="ReferenceResolverInterests"/> to
    /// <see cref="ReferenceResolverInterests.All"/>: interests describe one particular resolver, so a
    /// replacement never inherits the set narrowed for its predecessor. Assign the interests after the
    /// resolver — which is what
    /// <see cref="OptionsExtensions.SetReferencesResolver(Options, IReferenceResolver, ReferenceResolverInterests)"/>
    /// does — to register a resolver with a narrower set.
    /// </remarks>
    public IReferenceResolver ReferenceResolver
    {
        get => _referenceResolver;
        set
        {
            _referenceResolver = value;
            ReferenceResolverInterests = ReferenceResolverInterests.All;
        }
    }

    private IReferenceResolver _referenceResolver = DefaultReferenceResolver.Instance;

    /// <summary>
    /// The situations <see cref="ReferenceResolver"/> is consulted for. Defaults to
    /// <see cref="ReferenceResolverInterests.All"/>, which is how every resolver behaved before this filter
    /// existed. Narrowing it keeps interpreter fast paths armed for the situations the resolver opts out of;
    /// see <see cref="ReferenceResolverInterests"/> for what each flag costs. Ignored while
    /// <see cref="ReferenceResolver"/> is the built-in default.
    /// </summary>
    /// <remarks>
    /// Assigning <see cref="ReferenceResolver"/> — directly or through
    /// <see cref="OptionsExtensions.SetReferencesResolver(Options, IReferenceResolver)"/> — resets this to
    /// <see cref="ReferenceResolverInterests.All"/>, so a second resolver never silently inherits a narrower
    /// set from the first. Narrow it after registering the resolver it describes.
    /// </remarks>
    public ReferenceResolverInterests ReferenceResolverInterests { get; set; } = ReferenceResolverInterests.All;

    /// <summary>
    /// Whether calling 'eval' with custom code and function constructors taking function code as string is allowed.
    /// Defaults to true.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
    /// </remarks>
    [Obsolete("Use Options.Host.StringCompilationAllowed")]
    public bool StringCompilationAllowed
    {
        get => Host.StringCompilationAllowed;
        set => Host.StringCompilationAllowed = value;
    }

    /// <summary>
    /// Options for the built-in JSON (de)serializer which
    /// gets used using <c>JSON.parse</c> or <c>JSON.stringify</c>
    /// </summary>
    public JsonOptions Json { get; set; } = new();

    /// <summary>
    /// Limits applied by host-side result conversion and JSON serialization. Defaults to
    /// <see cref="ResultLimits.Unlimited"/> for compatibility.
    /// </summary>
    /// <remarks>
    /// Setting this also bounds script-visible <c>JSON.stringify</c>. Use per-call limits on
    /// <see cref="Engine.AdvancedOperations.ConvertResult"/> or <see cref="Native.Json.JsonSerializer"/> when
    /// different requests sharing one options object need different policies.
    /// </remarks>
    public ResultLimits ResultLimits
    {
        get => _resultLimits;
        set
        {
            if (value is null)
            {
                Throw.ArgumentNullException(nameof(value));
            }

            _resultLimits = value;
        }
    }

    private ResultLimits _resultLimits = ResultLimits.Unlimited;

    /// <summary>
    /// What experimental features are allowed, functionality may lacking or even plain wrong. Defaults to having none.
    /// </summary>
    public ExperimentalFeature ExperimentalFeatures { get; set; }

    /// <summary>
    /// Whether the agent can suspend (block) via Atomics.wait().
    /// Defaults to false. Worker-like hosts that deliberately allow blocking suspension must opt in.
    /// This option does not affect Atomics.waitAsync().
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma262/#sec-agentcansuspend
    /// </remarks>
    public bool AgentCanSuspend { get; set; }

    /// <summary>
    /// Copies the <b>restrictive</b> half of <paramref name="source"/>'s configuration onto
    /// <paramref name="target"/>: every value setting that narrows what script may do, and nothing that grants
    /// a capability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the rule behind a worker inheriting its creator's posture (<c>WorkerRequest.CreateDefaultOptions</c>),
    /// and it lives here, beside the properties it names, rather than in the feature that consumes it — so that
    /// a settings PR adding a new restriction has the classification in front of it. Two lists state that
    /// classification explicitly: <see cref="SecurityPostureInherited"/>, which must name exactly what this
    /// method copies, and <see cref="SecurityPostureNotInherited"/>, which names every other value-typed
    /// setting in scope together with the reason it stays behind. A reflective test fails when a new one
    /// belongs to neither.
    /// </para>
    /// <para>
    /// <b>Withholding a grant and dropping a restriction are opposites.</b> A worker may be given less than its
    /// creator and never more, which is the shape the web enforces structurally (a worker inherits its
    /// creator's origin, sandboxing flags and policy, and its cross-origin-isolated capability is floored at
    /// the owner's, never raised) and which Deno states outright: the permissions of a worker cannot be
    /// extended beyond its parent's reach. So a hardened parent's <c>StringCompilationAllowed = false</c>
    /// travels — otherwise <c>new Worker()</c> is an <c>eval</c> escape hatch — while its CLR interop grant,
    /// its module loader and its network flags do not.
    /// </para>
    /// <para>
    /// Only <i>value</i> settings are copied. Constraint <b>instances</b> are deliberately not: they carry
    /// per-execution state and are documented single-engine-only, so the caller replays the parent's constraint
    /// <i>factories</i> instead and each engine gets its own instances.
    /// </para>
    /// <para>
    /// It is a convenience for building a second engine from a first, not a security boundary — a host that
    /// builds its options from a hardening helper builds the second engine's from the same helper.
    /// </para>
    /// </remarks>
    /// <param name="source">The options whose posture is being inherited.</param>
    /// <param name="target">The fresh options being built. Every setting named here is overwritten.</param>
    internal static void CopySecurityPosture(Options source, Options target)
    {
        target.Constraints.MaxRecursionDepth = source.Constraints.MaxRecursionDepth;
        target.Constraints.MaxExecutionStackCount = source.Constraints.MaxExecutionStackCount;
        target.Constraints.StackOverflowGuard = source.Constraints.StackOverflowGuard;
        target.Constraints.RegexTimeout = source.Constraints.RegexTimeout;
        target.Constraints.PromiseTimeout = source.Constraints.PromiseTimeout;
        target.Constraints.MaxArraySize = source.Constraints.MaxArraySize;
        target.Constraints.MaxAtomicsPauseIterations = source.Constraints.MaxAtomicsPauseIterations;
        target.Host.StringCompilationAllowed = source.Host.StringCompilationAllowed;
        target.AgentCanSuspend = source.AgentCanSuspend;
        target.Json.MaxParseDepth = source.Json.MaxParseDepth;
    }

    /// <summary>
    /// Exactly what <see cref="CopySecurityPosture"/> copies, named the way the reflective test names a
    /// setting: <c>Group.Property</c> for a setting on an option group, the bare name for one on
    /// <see cref="Options"/> itself.
    /// </summary>
    internal static readonly string[] SecurityPostureInherited =
    [
        // Every value setting on ConstraintOptions. All seven bound something the script cannot see, and the
        // default of each is the permissive one, so a worker that did not inherit them would be a way around
        // a parent that had set them.
        "Constraints.MaxRecursionDepth",
        "Constraints.MaxExecutionStackCount",
        "Constraints.StackOverflowGuard",
        "Constraints.RegexTimeout",
        "Constraints.PromiseTimeout",
        "Constraints.MaxArraySize",
        "Constraints.MaxAtomicsPauseIterations",

        // eval and new Function. Without this one, building a second engine is an eval escape hatch from a
        // parent that turned it off.
        "Host.StringCompilationAllowed",

        // The obsolete alias of the property above; it forwards, so copying that one copies this one.
        "StringCompilationAllowed",

        // Whether Atomics.wait may block the thread. A second engine that can block a host thread its creator
        // could not is exactly the shape this rule exists to refuse.
        "AgentCanSuspend",

        // The JSON parser's depth bound.
        "Json.MaxParseDepth",
    ];

    /// <summary>
    /// Every other value-typed public setting on <see cref="Options"/> and on the groups
    /// <see cref="CopySecurityPosture"/> reads, with the reason it is not inherited. An entry here is a
    /// decision, not an omission.
    /// </summary>
    internal static readonly string[] SecurityPostureNotInherited =
    [
        // Meaningless fresh: module code is strict by specification, and a worker runs a module. The parent's
        // answer for its own sloppy scripts decides nothing about it.
        "Strict",

        // Not posture: it decides whether THIS engine keeps the source of the functions IT parsed, for
        // Function.prototype.toString. A second engine parses a different program, and the default — not
        // retaining — is the restrictive one, so a fresh engine already takes the safe answer.
        "RetainFunctionSourceText",

        // Describes a reference resolver, which is a host object rather than a restriction and is not copied.
        // Interests without their resolver mean nothing, and assigning a resolver resets them anyway.
        "ReferenceResolverInterests",

        // Grant-shaped: the only feature it carries today is CLR task interop, and interop grants are the
        // host's to make deliberately, per engine.
        "ExperimentalFeatures",
    ];

    /// <summary>
    /// The option groups <see cref="CopySecurityPosture"/> does not look at, and why. Named rather than left
    /// implicit so that a group added later has to be classified instead of quietly falling outside the rule.
    /// </summary>
    internal static readonly string[] SecurityPostureExcludedGroups =
    [
        // Grant-shaped throughout. CLR access is off by default and every setting in the group widens or
        // narrows a grant the host makes deliberately, per engine — a worker gets the interop its provider
        // decides to give it, which may be none, a read-only view, or the parent's own.
        "Interop",

        // Grant-shaped: the module loader IS the permission to read code, and it belongs to the thread and the
        // base of the engine that uses it. A second engine's loader is the host's to choose.
        "Modules",

        // Grant-shaped: the feature mask is the grant, and it is computed explicitly rather than copied
        // (network, storage, routing and the worker capability itself are subtracted). What is left in the
        // group is host wiring — providers, sinks, clocks, quotas — rather than restrictions.
        "WebApi",

        // Host diagnostics, and engine-affine: a debugger attached to one engine says nothing about another
        // nobody is stepping.
        "Debugger",

        // Host diagnostics: coverage counters are per engine, and a host collecting one engine's coverage did
        // not ask for a second engine's.
        "Coverage",

        // Host diagnostics again, and a capability script cannot reach at all — a profiling session is opened
        // by the host through Advanced.StartProfiling. Its gate defaults to refusing, so a second engine
        // already starts at the restrictive answer whatever the first one chose.
        "Profiling",

        // Locale data. Reference-typed providers, not restrictions; which data a second engine gets is the
        // host's to hand over.
        "Intl",

        // Time-zone and calendar data, for the reason above.
        "Temporal",
    ];

    /// <summary>
    /// Called by the <see cref="Engine"/> instance that loads this <see cref="Options" />
    /// once it is loaded.
    /// </summary>
    internal void Apply(Engine engine)
    {
        // Modules must exist before the configuration callbacks run: a host's
        // options.Configure(e => e.Modules.Add(...)) is the documented way to
        // register programmatic modules at construction time.
        //
        // The opt-in node: builtins are a decorator over whatever loader the host configured, applied here
        // rather than in UseNodeBuiltinModules so that the order of that call and EnableModules cannot
        // matter, and so that options.Modules.ModuleLoader keeps reading back what the host set.
        var moduleLoader = _nodeBuiltinModules is null
            ? Modules.ModuleLoader
            : NodeCompat.NodeBuiltinModuleLoader.Wrap(Modules.ModuleLoader, _nodeBuiltinModules);

        engine.Modules = new Engine.ModuleOperations(engine, moduleLoader);

        foreach (var configuration in _configurations)
        {
            configuration.Apply(engine);
        }

        if (UntrustedCodeLimits is { } limits)
        {
            OptionsExtensions.ReapplyUntrustedCodeOptions(this, engine, limits);
        }

        // add missing bits if needed
        if (Interop.Enabled)
        {
#pragma warning disable IL2026
            var typeResolutionPolicy = new ClrTypeResolutionPolicy(Interop);

            engine.Realm.GlobalObject.SetProperty("System", new PropertyDescriptor(new NamespaceReference(engine, "System", typeResolutionPolicy), PropertyFlag.AllForbidden));

            engine.Realm.GlobalObject.SetProperty("importNamespace", new PropertyDescriptor(new ClrFunction(
                    engine,
                    "importNamespace",
                    (_, arguments) => new NamespaceReference(
                        engine,
                        arguments.At(0).IsNullOrUndefined() ? null : TypeConverter.ToString(arguments.At(0)),
                        typeResolutionPolicy)),
                PropertyFlag.AllForbidden));

            engine.Realm.GlobalObject.SetProperty("clrHelper", new PropertyDescriptor(ObjectWrapper.Create(engine, new ClrHelper(Interop, typeResolutionPolicy)), PropertyFlag.AllForbidden));

#pragma warning restore IL2026
        }

#if NET8_0_OR_GREATER
        // Opt-in WHATWG web APIs. Deliberately after the configuration callbacks above, so that a global a
        // host registered itself is never replaced by one of ours: WebApiRegistration skips every name the
        // global object already owns. Nothing is installed while Features is None, which is the default.
        // The backing field rather than the property, so an engine built from options that never mentioned a
        // web API does not allocate the group just to find out that it is empty. A diagnostics sink counts as
        // having asked for something even with no feature named: it installs no global, but it does arm the
        // channel unhandled promise rejections are reported through.
        if (_webApi is not null && (_webApi.Features != WebApiFeatures.None || _webApi.Diagnostics.Sink is not null))
        {
            Jint.WebApi.WebApiRegistration.Apply(this, engine);
        }
#endif

        if (Interop.ExtensionMethodTypes.Count > 0)
        {
            AttachExtensionMethodsToPrototypes(engine);
        }

        if (Modules.RegisterRequire)
        {
            // Node js like loading of modules
            engine.Realm.GlobalObject.SetProperty("require", new PropertyDescriptor(new ClrFunction(
                    engine,
                    "require",
                    (thisObj, arguments) =>
                    {
                        var specifier = TypeConverter.ToString(arguments.At(0));
                        return engine.Modules.Import(specifier);
                    }),
                PropertyFlag.AllForbidden));
        }
    }

    internal Options CreateEngineOptions()
    {
        if (UntrustedCodeLimits is null)
        {
            return this;
        }

        var clone = (Options) MemberwiseClone();
        clone._configurations = new List<EngineConfiguration>(_configurations);
        clone.Constraints = Constraints.Clone();
        clone.Parsing = Parsing.Clone();
        clone.Interop = Interop.Clone();
        clone.Debugger = Debugger.Clone();
        clone.Host = Host.Clone();
        clone.Modules = Modules.Clone();
        clone.Json = new JsonOptions { MaxParseDepth = Json.MaxParseDepth };
        OptionsExtensions.ApplyUntrustedCodeOptions(clone, UntrustedCodeLimits);
        return clone;
    }

    private static void AttachExtensionMethodsToPrototypes(Engine engine)
    {
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Array.PrototypeObject, typeof(Array));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Boolean.PrototypeObject, typeof(bool));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Date.PrototypeObject, typeof(DateTime));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Number.PrototypeObject, typeof(double));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.Object.PrototypeObject, typeof(ExpandoObject));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.RegExp.PrototypeObject, typeof(System.Text.RegularExpressions.Regex));
        AttachExtensionMethodsToPrototype(engine, engine.Realm.Intrinsics.String.PrototypeObject, typeof(string));
    }

    private static void AttachExtensionMethodsToPrototype(Engine engine, ObjectInstance prototype, Type objectType)
    {
        if (!engine._extensionMethods.TryGetExtensionMethods(objectType, out var methods))
        {
            return;
        }

        foreach (var overloads in methods.GroupBy(x => x.Name, StringComparer.Ordinal))
        {
            PropertyDescriptor CreateMethodInstancePropertyDescriptor(Function? function)
            {
                var instance = new MethodInfoFunction(
                    engine,
                    objectType,
                    target: null,
                    overloads.Key,
                    methods: MethodDescriptor.Build(overloads.ToList()),
                    function);

                return new PropertyDescriptor(instance, PropertyFlag.AllForbidden);
            }

            JsValue key = overloads.Key;
            PropertyDescriptor? descriptorWithFallback = null;
            PropertyDescriptor? descriptorWithoutFallback = null;

            if (prototype.HasOwnProperty(key) &&
                prototype.GetOwnProperty(key).Value is Function functionInstance)
            {
                descriptorWithFallback = CreateMethodInstancePropertyDescriptor(functionInstance);
                prototype.SetOwnProperty(key, descriptorWithFallback);
            }
            else
            {
                descriptorWithoutFallback = CreateMethodInstancePropertyDescriptor(null);
                prototype.SetOwnProperty(key, descriptorWithoutFallback);
            }

            // make sure we register both lower case and upper case
            if (char.IsUpper(overloads.Key[0]))
            {
                key = char.ToLower(overloads.Key[0], CultureInfo.InvariantCulture) + overloads.Key.Substring(1);

                if (prototype.HasOwnProperty(key) &&
                    prototype.GetOwnProperty(key).Value is Function lowerFunctionInstance)
                {
                    descriptorWithFallback ??= CreateMethodInstancePropertyDescriptor(lowerFunctionInstance);
                    prototype.SetOwnProperty(key, descriptorWithFallback);
                }
                else
                {
                    descriptorWithoutFallback ??= CreateMethodInstancePropertyDescriptor(null);
                    prototype.SetOwnProperty(key, descriptorWithoutFallback);
                }
            }
        }
    }


    public class DebuggerOptions
    {
        /// <summary>
        /// Whether debugger functionality is enabled, defaults to false.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Configures the statement handling strategy, defaults to Ignore.
        /// </summary>
        public DebuggerStatementHandling StatementHandling { get; set; } = DebuggerStatementHandling.Ignore;

        /// <summary>
        /// Configures the step mode used when entering the script.
        /// </summary>
        public StepMode InitialStepMode { get; set; } = StepMode.None;

        internal DebuggerOptions Clone() => (DebuggerOptions) MemberwiseClone();
    }

    /// <summary>
    /// Script code coverage configuration. Read once, while the engine is being constructed; changing it
    /// afterwards only reaches engines built later.
    /// </summary>
    /// <remarks>
    /// Nothing here is engine-affine, so an <see cref="Options"/> instance carrying it stays shareable across
    /// engines — each engine gets its own counters, and reading one engine's report never sees another's.
    /// </remarks>
    public class CoverageOptions
    {
        /// <summary>
        /// Whether the engine counts what it executes, defaults to false. When set, the engine collects hit
        /// counts readable through <see cref="Engine.AdvancedOperations.GetCoverage"/>; when not set — the
        /// default — the interpreter contains no coverage work of any kind, since the per-statement lane the
        /// counting rides on is not armed.
        /// </summary>
        /// <remarks>
        /// Enabling it disarms the interpreter's tight-loop lane for the engine, the same way registering an
        /// exact execution constraint or enabling the debugger does, so measured code runs the instrumented
        /// path. See <see cref="Engine.AdvancedOperations.GetCoverage"/>.
        /// </remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// What is counted, defaults to <see cref="CoverageGranularity.Statements"/>. The granularity decides
        /// what the report contains, not how the engine executes: both values collect through the same lane.
        /// </summary>
        public CoverageGranularity Granularity { get; set; } = CoverageGranularity.Statements;
    }

    public class InteropOptions
    {
        /// <summary>
        /// Whether accessing CLR and it's types and methods is allowed from JS code, defaults to false.
        /// </summary>
        public bool Enabled { get; set; }

        internal ClrAccessConfiguration ClrAccessConfiguration { get; set; }

        /// <summary>
        /// Whether to expose instance <see cref="object.GetType"/> members and the type-widening operations
        /// <c>clrHelper.unwrap</c>, <c>typeOf</c>, <c>typeToObject</c>, and <c>objectToType</c>. Defaults to false.
        /// </summary>
        /// <remarks>
        /// Jint filters the static <see cref="Type.GetType(string)"/> family from ordinary member resolution.
        /// This is not a general type-resolution or reflection sandbox: other APIs admitted by the host can
        /// carry equivalent authority. Type-widening <c>clrHelper</c> operations additionally
        /// require the resulting type to satisfy the namespace-resolution assembly, reflection, and
        /// <see cref="TypeResolver.MemberFilter"/> policy. A <see cref="Jint.Runtime.Interop.TypeReference"/> converted
        /// to a <see cref="Type"/> object and back preserves that already-explicit capability.
        /// <para>
        /// Read once, while the engine is being constructed. Changing it afterwards has no effect on an
        /// engine that already exists: resolved members are cached under the value captured then, so a later
        /// change could only be honoured by some reads and not others. Enabling it grants powerful reflection
        /// capabilities and is not recommended for untrusted scripts.
        /// </para>
        /// </remarks>
        public bool AllowGetType { get; set; }

        /// <summary>
        /// Whether Jint should allow resolving or wrapping types from the <c>System.Reflection</c> namespace.
        /// Defaults to false. A <see cref="TypeReference"/> explicitly exported by the host remains an explicit
        /// capability and is not subject to namespace-resolution policy.
        /// </summary>
        /// <remarks>
        /// Namespace resolution reads this once when the engine installs its CLR globals. Configure it before
        /// constructing the engine.
        /// </remarks>
        public bool AllowSystemReflection { get; set; }

        /// <summary>
        /// Whether direct writes through projected CLR objects are allowed, including fields, properties,
        /// indexers, and collection entries. Defaults to false.
        /// </summary>
        /// <remarks>
        /// This option controls the wrapper's write operations. It does not prevent scripts from calling CLR
        /// methods or extension methods whose implementations mutate host state.
        /// </remarks>
        public bool AllowWrite { get; set; }

        /// <summary>
        /// Whether operator overloading resolution is allowed, defaults to false.
        /// </summary>
        /// <remarks>
        /// Read once per engine, at the end of construction and so after any callback registered with
        /// <c>Options.Configure</c> has run. Setting it on an <see cref="Options"/> instance an engine
        /// has already been built from does not reach that engine — only engines built afterwards.
        /// </remarks>
        public bool AllowOperatorOverloading { get; set; }

        /// <summary>
        /// Types holding extension methods that should be considered when resolving methods.
        /// </summary>
        public List<Type> ExtensionMethodTypes { get; private set; } = new();

        /// <summary>
        /// Object converters to try when build-in conversions.
        /// </summary>
        public List<IObjectConverter> ObjectConverters { get; private set; } = new();

        /// <summary>
        /// CLR types whose instances the host promises are immutable while they are exposed to the engine —
        /// their member and key set, and the values behind them, do not change. In exchange the engine may
        /// cache what it reads through them: memoized member results and stable child wrappers, so a walk
        /// such as <c>record.value.customer.country</c> wraps each node once per wrapper lifetime instead of
        /// once per access. Register through <see cref="OptionsExtensions.AddImmutableCrossing"/>, which
        /// validates the declaration; assignability is the rule, so an interface or base type covers a whole
        /// family. Empty by default, in which case nothing is memoized and behaviour is unchanged.
        /// </summary>
        /// <remarks>
        /// A host that breaks the promise gets stale reads — the same class of consequence
        /// <see cref="TrackObjectWrapperIdentity"/> and a shared <see cref="TypeResolver"/> already describe,
        /// and owned by the host in exactly the same way. Read once, while the engine is being constructed.
        /// </remarks>
        public List<Type> ImmutableCrossingTypes { get; private set; } = new();

        /// <summary>
        /// Whether identity map is persisted for object wrappers in order to maintain object identity. This can cause
        /// memory usage to grow when targeting large set and freeing of memory can be delayed due to ConditionalWeakTable semantics.
        /// Also covers CLR arrays: repeated conversions of the same array instance return the same result — the same
        /// JsArray snapshot under the default <see cref="ArrayConversionMode.Copy"/> mode (script-side mutations persist
        /// across reads; CLR-side mutations after the first conversion are not re-copied), or the live wrapper view
        /// under <see cref="ArrayConversionMode.LiveView"/> — instead of re-converting on every read.
        /// Defaults to false.
        /// </summary>
        public bool TrackObjectWrapperIdentity { get; set; }

        /// <summary>
        /// Whether a small bounded cache of most recently wrapped CLR objects is kept so that the same
        /// object crossing into script repeatedly reuses its wrapper (by reference identity). A lighter
        /// alternative to <see cref="TrackObjectWrapperIdentity"/> that cannot grow memory unbounded;
        /// identity is only preserved for objects still present in the cache. Covers CLR arrays the same
        /// way <see cref="TrackObjectWrapperIdentity"/> does.
        /// Defaults to true since 4.14: repeated crossings of a recently seen object return the same
        /// wrapper instance, so script-attached state (freeze/defineProperty/expandos) stays stable and
        /// the per-crossing wrapper allocation disappears. Note that under <see cref="ArrayConversionMode.Copy"/>
        /// this also means repeated reads of the same CLR array reuse the first JsArray snapshot while it
        /// stays cached (CLR-side mutations are not re-copied); set this to false for a fresh snapshot per
        /// crossing (the pre-4.14 behavior).
        /// </summary>
        public bool CacheRecentObjectWrappers { get; set; } = true;

        /// <summary>
        /// How CLR arrays (<c>T[]</c>) are exposed to script code. Defaults to
        /// <see cref="Jint.ArrayConversionMode.Copy"/>, which snapshots the contents into a native JavaScript array.
        /// <see cref="Jint.ArrayConversionMode.LiveView"/> remains available as an explicit opt-in for a fixed-size
        /// view that avoids the copy and its allocation, and whose element reads stay connected to the CLR array.
        /// Writes reach the CLR array only when <see cref="AllowWrite"/> is also enabled.
        /// This default is a breaking change for hosts that relied on the live-view default introduced in 4.14.
        /// See the enum members for the exact semantic differences (write-through, identity, wrapper caches,
        /// <c>Array.isArray</c>, resizing and multidimensional arrays).
        /// </summary>
        public ArrayConversionMode ArrayConversion { get; set; } = ArrayConversionMode.Copy;

        /// <summary>
        /// How a CLR sequence that is <em>only</em> an <see cref="System.Collections.Generic.IEnumerable{T}"/> — a LINQ
        /// iterator, a generator method's result, any lazily computed sequence with no <c>Count</c> and no indexer — is
        /// exposed to script code. Defaults to <see cref="Jint.EnumerableConversionMode.Lazy"/>, which changes nothing.
        /// Collections that already have a count (<see cref="System.Collections.Generic.ICollection{T}"/>,
        /// <see cref="System.Collections.Generic.IList{T}"/>, <c>T[]</c>) and dictionaries are unaffected by this
        /// option; they are exposed exactly as before. See the enum members.
        /// </summary>
        public EnumerableConversionMode EnumerableConversion { get; set; } = EnumerableConversionMode.Lazy;

        /// <summary>
        /// If no known type could be guessed, objects are by default wrapped as an
        /// ObjectInstance using class ObjectWrapper. This function can be used to
        /// change the behavior.
        /// </summary>
        internal static readonly WrapObjectDelegate _defaultWrapObjectHandler =
            static (engine, target, type) => ObjectWrapper.Create(engine, target, type);

        public WrapObjectDelegate WrapObjectHandler { get; set; } = _defaultWrapObjectHandler;

        /// <summary>
        /// The handler used to build stack traces. Changing this enables mapping
        /// stack traces to code different from the code being executed, eg. when
        /// executing code transpiled from TypeScript.
        /// </summary>
        public BuildCallStackDelegate? BuildCallStackHandler { get; set; }

        /// <summary>
        /// Named default so the interop member inline cache (see JintMemberExpression's wrapper lane) can
        /// detect by reference when a custom accessor has been configured and stay out of the way.
        /// </summary>
        internal static readonly MemberAccessorDelegate _defaultMemberAccessor = static (engine, target, member) => null;

        /// <summary>
        ///
        /// </summary>
        public MemberAccessorDelegate MemberAccessor { get; set; } = _defaultMemberAccessor;

        /// <summary>
        /// Exceptions that thrown from CLR code are converted to JavaScript errors and
        /// can be used in at try/catch statement. By default these exceptions are bubbled
        /// to the CLR host and interrupt the script execution. If handler returns true these exceptions are converted
        /// to JS errors that can be caught by the script.
        /// </summary>
        public ExceptionHandlerDelegate ExceptionHandler { get; set; } = _defaultExceptionHandler;

        /// <summary>
        /// Whether the CLR exception behind a caught interop error is also chained into the
        /// <see cref="Exception.InnerException"/> of the <see cref="JavaScriptException"/> the host catches, so
        /// that logging which walks the inner-exception chain surfaces it. Defaults to false, which leaves
        /// <see cref="object.ToString"/> on that exception rendering exactly the JavaScript error and stack it
        /// renders today.
        /// <para>
        /// Turning this on puts the host's own .NET stack traces into whatever consumes that string, so treat it
        /// the way an ASP.NET application treats developer exception details. It changes only how the exception
        /// presents itself to the host; the originating exception is recorded either way and can always be read
        /// with <see cref="JintException.TryGetClrException"/>, and a running script sees neither.
        /// </para>
        /// <para>
        /// <see cref="JavaScriptException.GetJavaScriptErrorString()"/> is exempt and renders the same string
        /// either way. It is the accessor a host uses to show a <em>script author</em> what went wrong, so the
        /// chain would put the host's .NET frames in front of the JavaScript ones there.
        /// </para>
        /// </summary>
        public bool ChainClrExceptionAsInnerException { get; set; }

        /// <summary>
        /// When <c>true</c>, JavaScript errors created from CLR exceptions expose the original exception
        /// message to script code. Defaults to <c>false</c>, so exception messages, paths, URLs and other host
        /// details are not disclosed to untrusted scripts.
        /// </summary>
        /// <remarks>
        /// The original exception is retained either way and can be read host-side through
        /// <see cref="JintException.TryGetClrException"/>. This setting only changes the script-visible
        /// message. Treat it as a development option.
        /// </remarks>
        public bool ExposeDetailedExceptionMessages { get; set; }

        /// <summary>
        /// Called after a JavaScript error object is created from a CLR exception, either because
        /// <see cref="ExceptionHandler"/> returned true or because a module loader exception became an import error.
        /// Allows decorating the error object with additional properties or modifying its state.
        /// The decorator receives the engine instance, the created error object, and the original CLR exception.
        /// </summary>
        public ClrExceptionErrorDecoratorDelegate? ClrExceptionErrorDecorator { get; set; }

        /// <summary>
        /// Called after the JavaScript error object for a failed CLR method or constructor resolution has been
        /// created, just before it is thrown into the script. The decorator may overwrite the script-visible
        /// <c>message</c> and add custom properties (e.g. an error code). It receives the full structured
        /// resolution information via <see cref="ClrResolutionErrorInfo"/> regardless of
        /// <see cref="ExposeDetailedResolutionErrors"/>, which only selects the default message.
        /// </summary>
        public ClrResolutionErrorDecoratorDelegate? ClrResolutionErrorDecorator { get; set; }

        /// <summary>
        /// Assemblies from which <see cref="Jint.Runtime.Interop.NamespaceReference"/> may resolve CLR types.
        /// </summary>
        /// <remarks>
        /// This is a closed allow-list: no calling, executing, or core runtime assembly is searched implicitly.
        /// <see cref="OptionsExtensions.AllowClr(Options)"/> adds the assembly containing <see cref="object"/>
        /// for compatibility with core CLR type access. Passing an empty array to
        /// <see cref="OptionsExtensions.AllowClr(Options, Assembly[])"/> adds no assemblies.
        /// The list is snapshotted when the engine installs its CLR globals. It does not restrict ordinary CLR
        /// objects, delegates, or <see cref="TypeReference"/> values explicitly exported by the host; those are
        /// capabilities in their own right. Namespace lookup admits only public top-level types and nested
        /// types whose complete declaring-type chain is public. Resolved types must also satisfy
        /// <see cref="TypeResolver.MemberFilter"/> and <see cref="AllowSystemReflection"/>.
        /// </remarks>
        public List<Assembly> AllowedAssemblies { get; set; } = new();

        /// <summary>
        /// Type and member resolving strategy, which allows filtering allowed members and configuring member
        /// name matching comparison.
        /// </summary>
        /// <remarks>
        /// As this object holds caching state same instance should be shared between engines, if possible.
        /// </remarks>
        public TypeResolver TypeResolver { get; set; } = TypeResolver.Default;

        /// <summary>
        /// When writing values to CLR objects, how should JS values be coerced to CLR types.
        /// Defaults to only coercing to string values when writing to string targets.
        /// </summary>
        public ValueCoercionType ValueCoercion { get; set; } = ValueCoercionType.String;

        /// <summary>
        /// How CLR enum values are exposed to script code when they cross into JavaScript, defaults to
        /// <see cref="Jint.EnumConversionMode.Number"/> (the underlying numeric value). This is the read direction;
        /// the write direction is governed by <see cref="ValueCoercion"/> and always accepts both the member name
        /// and the numeric value.
        /// </summary>
        public EnumConversionMode EnumConversion { get; set; } = EnumConversionMode.Number;

        /// <summary>
        /// Strategy to create a CLR object to hold converted <see cref="ObjectInstance"/>.
        /// </summary>
        internal static readonly Func<ObjectInstance, IDictionary<string, object?>> _defaultCreateClrObject =
            static _ => new ExpandoObject();

        public Func<ObjectInstance, IDictionary<string, object?>>? CreateClrObject { get; set; } = _defaultCreateClrObject;

        /// <summary>
        /// Strategy to create a CLR object from TypeReference.
        /// Defaults to retuning null which makes TypeReference attempt to find suitable constructor.
        /// </summary>
        internal static readonly Func<Engine, Type, JsValue[], object?> _defaultCreateTypeReferenceObject =
            static (_, _, _) => null;

        public Func<Engine, Type, JsValue[], object?> CreateTypeReferenceObject { get; set; } = _defaultCreateTypeReferenceObject;

        internal static readonly ExceptionHandlerDelegate _defaultExceptionHandler = static exception => false;

        /// <summary>
        /// When not null, is used to serialize any CLR object in an
        /// <see cref="IObjectWrapper"/> passing through 'JSON.stringify'.
        /// </summary>
        public SerializeToJsonDelegate? SerializeToJson { get; set; }

        /// <summary>
        /// What kind of date time should be produced when JavaScript date is converted to DateTime. If Local, uses <see cref="Options.TimeZone"/>.
        /// Defaults to <see cref="System.DateTimeKind.Utc"/>.
        /// </summary>
        public DateTimeKind DateTimeKind { get; set; } = DateTimeKind.Utc;

        /// <summary>
        /// Should the Array prototype be attached instead of Object prototype to the wrapped interop objects when type looks suitable. Defaults to true.
        /// </summary>
        public bool AttachArrayPrototype { get; set; } = true;

        /// <summary>
        /// When true, JavaScript prototype methods take precedence over CLR methods of the same name on wrapped CLR objects
        /// whose prototype is not the default Object prototype (e.g. <c>Array.prototype</c> attached to wrapped <see cref="System.Collections.Generic.IList{T}"/>).
        /// Avoids semantic mismatches such as <c>List&lt;T&gt;.Reverse()</c> returning <c>void</c> while
        /// <c>Array.prototype.reverse</c> returns the array. Has no effect when no JS prototype is attached
        /// (i.e. <see cref="AttachArrayPrototype"/> is false or the wrapped type is not array-like). Defaults to false for backward compatibility.
        /// </summary>
        /// <remarks>
        /// A member that resolves purely to registered extension methods (see <see cref="OptionsExtensions.AddExtensionMethods"/>)
        /// already defers to a same-named <c>Array.prototype</c> method on indexed array-like wrappers by default;
        /// this option additionally makes real CLR instance methods defer.
        /// </remarks>
        public bool PreferJsPrototypeMethods { get; set; }

        /// <summary>
        /// Whether the engine should throw an error when a member is not found on a CLR object. Defaults to false.
        /// </summary>
        public bool ThrowOnUnresolvedMember { get; set; }

        /// <summary>
        /// When <c>true</c>, the <see cref="Jint.Runtime.JavaScriptException"/> thrown when a CLR method or
        /// constructor call cannot be resolved gets a detailed message naming the target CLR type, the provided
        /// argument types, and the candidate signatures. When <c>false</c> (the default) a terse message is used
        /// instead, so that untrusted scripts cannot enumerate the host's CLR surface through the error text.
        /// <para>
        /// This only controls the script-visible message. The originating CLR <see cref="System.Type"/> is always
        /// attached to the exception regardless of this setting and can be read host-side via
        /// <see cref="JintException.TryGetClrType"/> (it is not exposed to the running script). To customize
        /// what the script sees (rewrite the message, attach an error code), use <see cref="ClrResolutionErrorDecorator"/>.
        /// </para>
        /// </summary>
        public bool ExposeDetailedResolutionErrors { get; set; }

        /// <summary>
        /// Types of CLR members reported by <see cref="ObjectWrapper"/> when enumerating properties/serializing <see cref="ObjectWrapper.ToObject"/>.
        /// Supported values are: <see cref="MemberTypes.Field"/>, <see cref="MemberTypes.Property"/>, <see cref="MemberTypes.Method"/>.
        /// All other values are ignored.
        /// </summary>
        public MemberTypes ObjectWrapperReportedMemberTypes { get; set; } = MemberTypes.Field | MemberTypes.Property | MemberTypes.Method;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" />.
        /// </summary>
        /// <inheritdoc cref="AllowGetType" path="/remarks"/>
        public BindingFlags ObjectWrapperReportedFieldBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" />.
        /// </summary>
        /// <inheritdoc cref="AllowGetType" path="/remarks"/>
        public BindingFlags ObjectWrapperReportedPropertyBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" /> | <see cref="BindingFlags.Static" />.
        /// </summary>
        /// <inheritdoc cref="AllowGetType" path="/remarks"/>
        public BindingFlags ObjectWrapperReportedMethodBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Customizes the property keys reported by <see cref="ObjectWrapper"/> when a wrapped CLR object is
        /// enumerated (JavaScript <c>for..in</c>, <c>Object.keys</c>, object spread, <c>JSON.stringify</c>).
        /// When the delegate returns a non-null sequence, those keys replace the default reflected/dictionary
        /// keys for the given target; returning <c>null</c> keeps the default behavior. Reported keys must be
        /// resolvable through the wrapper's normal member/indexer access for their values to be readable.
        /// Defaults to returning <c>null</c>.
        /// </summary>
        internal static readonly ReportedPropertyKeysDelegate _defaultReportedPropertyKeys = static (_, _) => null;

        public ReportedPropertyKeysDelegate ObjectWrapperReportedPropertyKeys { get; set; } = _defaultReportedPropertyKeys;

        internal InteropOptions Clone()
        {
            var clone = (InteropOptions) MemberwiseClone();
            clone.ExtensionMethodTypes = new List<Type>(ExtensionMethodTypes);
            clone.ObjectConverters = new List<IObjectConverter>(ObjectConverters);
            clone.ImmutableCrossingTypes = new List<Type>(ImmutableCrossingTypes);
            clone.AllowedAssemblies = new List<Assembly>(AllowedAssemblies);
            return clone;
        }
    }

    public class ConstraintOptions
    {
        /// <summary>
        /// Registered constraint instances.
        /// </summary>
        /// <remarks>
        /// Every engine built from these options observes these exact instances. Constraints normally
        /// carry per-execution state (a statement counter, a deadline), so a shared instance is only
        /// safe when a single engine is built from the options and it is never used concurrently.
        /// To share one <see cref="Options"/> across several engines, register a factory instead
        /// (<see cref="OptionsExtensions.Constraint(Options, Func{Constraint})"/>) so each engine gets
        /// its own instance; the built-in constraint extension methods already do that.
        /// </remarks>
        public List<Constraint> Constraints { get; private set; } = new();

        /// <summary>
        /// Registered constraint factories. Each engine built from these options invokes every factory
        /// exactly once while constructing, so the constraints — and the per-execution state they carry —
        /// belong to that engine alone.
        /// </summary>
        internal List<Func<Constraint>> ConstraintFactories { get; private set; } = new();

        internal int? RequestedMaxStatements { get; set; }

        internal long? RequestedMemoryLimit { get; set; }

        internal TimeSpan? RequestedTimeoutInterval { get; set; }

        internal bool CancellationConstraintRequested { get; set; }

        internal bool OperationDeadlineConstraintRequested { get; set; }

        /// <summary>
        /// Maximum recursion depth allowed, defaults to -1 (no checks).
        /// </summary>
        /// <remarks>
        /// Read once, while the engine is being constructed. Changing it afterwards has no effect on an
        /// engine that already exists — the call stack decides from the same value whether to track depth at
        /// all, so the limit could not be raised later even if every check re-read it.
        /// </remarks>
        public int MaxRecursionDepth { get; set; } = -1;

        /// <summary>
        /// Maximum recursion stack count, defaults to -1 (as-is dotnet stacktrace).
        /// </summary>
        /// <remarks>
        /// Chrome and V8 based engines (ClearScript) that can handle 13955.
        /// When set to a different value except -1, it can reduce slight performance/stack trace readability drawback. (after hitting the engine's own limit),
        /// When max stack size to be exceeded, Engine throws an exception <see cref="JavaScriptException" />.
        /// Read once, while the engine is being constructed; changing it afterwards has no effect on an
        /// engine that already exists.
        /// This lane is probed at the call expression only, so a recursion that reaches a function body by
        /// another route — <c>new</c>, an accessor, a coercion, a Proxy trap — still overflows the native
        /// stack; <see cref="StackOverflowGuard"/> is the one that covers those, and setting this property
        /// takes precedence over it.
        /// </remarks>
        public int MaxExecutionStackCount { get; set; } = StackGuard.Disabled;

        /// <summary>
        /// Whether every entry into an interpreted function probes the remaining native stack and throws a
        /// catchable <c>RangeError: Maximum call stack size exceeded</c> when it is nearly gone. Defaults to
        /// <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Turn this on when the engine runs script you did not write. Without it, an unbounded recursion
        /// ends the host process with a native stack overflow: no <c>catch</c> sees it, no exception is
        /// raised, nothing is logged, and the process is simply gone. With it, the same script raises an
        /// ordinary JavaScript error that the script itself can catch and the engine survives to be used
        /// again. That is the whole trade — a process kill converted into an error value.
        /// </para>
        /// <para>
        /// It measures the stack rather than counting calls, so it covers every route into a function body
        /// rather than only a call expression: <c>new</c>, a getter or setter, a <c>valueOf</c>/<c>toString</c>
        /// coercion, a Proxy trap, a callback a built-in invokes, and a host delegate that re-enters the
        /// engine. Eighteen distinct routes reproduce the overflow, and the engine's older probe sat in the
        /// call expression, so it saw one of them. Measuring also means the depth follows the stack of the
        /// thread the engine runs on, instead of a frame count the host had to guess.
        /// </para>
        /// <para>
        /// A proper tail call in a strict function is exempt, and needs to be: it replaces the caller's
        /// frame rather than stacking one on top, so the recursion runs on a trampoline and consumes no
        /// native stack at all. The probe sits on the entry points that add a frame, never on the loop the
        /// trampoline re-enters, so a strict tail recursion neither pays for it nor is stopped by it. What
        /// remains in scope is everything the trampoline does not carry: sloppy-mode recursion, a call out
        /// of tail position, and the non-call routes above.
        /// </para>
        /// <para>
        /// It is enabled by default because the alternative on an unbounded recursion is termination of the
        /// host process. The probe is not free: the benchmark gate measured the recursion rows (<c>Fib</c>,
        /// <c>DeepSum</c>, <c>Tak</c>) roughly 1.5–3% slower with it on, while hot shallow calls stayed within
        /// run-to-run noise. A host whose scripts are all trusted and independently bounded can explicitly
        /// set this property to <see langword="false"/> to recover that cost. A host sandboxing untrusted
        /// input generally cannot, and a couple of percent on deep recursion is the price of staying alive.
        /// </para>
        /// <para>
        /// It is a backstop, not a policy. <see cref="MaxRecursionDepth"/> counts frames and is checked
        /// before the callee is entered, so where both are configured the recursion limit is what fires;
        /// the probe answers when no limit was set, when the limit was set higher than the thread's stack
        /// can actually hold, and when the limit structurally cannot see the recursion — it counts
        /// occurrences of one function <em>definition</em>, so a recursion whose every level is a function
        /// created for that level (<c>eval</c>, <c>new Function</c>, a host re-running a script) repeats
        /// no definition and never reaches it. <see cref="MaxExecutionStackCount"/> selects the older
        /// continue-on-a-fresh-thread lane instead, and takes precedence over this flag when both are set,
        /// because the probe sits a few frames deeper and would reach the condition first — leaving that
        /// lane nothing to hop with.
        /// </para>
        /// <para>
        /// Read once, while the engine is being constructed; changing it afterwards has no effect on an
        /// engine that already exists.
        /// </para>
        /// </remarks>
        public bool StackOverflowGuard { get; set; } = true;

        /// <summary>
        /// Maximum time a Regex is allowed to run, defaults to 10 seconds.
        /// </summary>
        public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Maximum time allowed for unwrapping a Promise and getting its resolved/rejected value.
        /// Defaults to 10 seconds.
        /// </summary>
        public TimeSpan PromiseTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The maximum size for JavaScript array, defaults to <see cref="uint.MaxValue"/>.
        /// </summary>
        public uint MaxArraySize { get; set; } = uint.MaxValue;

        /// <summary>
        /// How many iterations is Atomics.pause allowed to instruct to wait using <see cref="System.Threading.Thread.SpinWait"/>, defaults to 10 000.
        /// </summary>
        public int MaxAtomicsPauseIterations { get; set; } = 10_000;

        internal ConstraintOptions Clone()
        {
            var clone = (ConstraintOptions) MemberwiseClone();
            clone.Constraints = new List<Constraint>(Constraints);
            clone.ConstraintFactories = new List<Func<Constraint>>(ConstraintFactories);
            return clone;
        }
    }

    /// <summary>
    /// Resource limits applied before and during parsing. These limits also apply to code compiled by
    /// <c>eval</c>, function constructors, ShadowRealm evaluation, and module loaders.
    /// </summary>
    public sealed class ParsingOptions
    {
        /// <summary>
        /// The maximum number of UTF-16 code units the parser may receive.
        /// <see langword="null"/> (the default) means no limit.
        /// </summary>
        /// <remarks>
        /// This counts <see cref="string.Length"/>, not encoded bytes. Module loaders which decode bytes are
        /// checked after decoding. Generated source-offset padding and function-constructor wrappers also count.
        /// </remarks>
        public int? MaxSourceLength { get; set; }

        /// <summary>
        /// The maximum number of AST nodes the parser may produce.
        /// <see langword="null"/> (the default) means no limit.
        /// </summary>
        public int? MaxNodeCount { get; set; }

        internal ParsingOptions Clone() => (ParsingOptions) MemberwiseClone();
    }

    /// <summary>
    /// Host related customization, still work in progress.
    /// </summary>
    public class HostOptions
    {
        internal static readonly Func<Engine, Host> _defaultFactory = static _ => new Host();

        internal Func<Engine, Host> Factory { get; set; } = _defaultFactory;

        /// <summary>
        /// Whether calling 'eval' with custom code and function constructors taking function code as string is allowed.
        /// Defaults to true.
        /// </summary>
        /// <remarks>
        /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
        /// </remarks>
        public bool StringCompilationAllowed { get; set; } = true;

        /// <summary>
        /// Possibility to override Jint's default <c>Function.prototype.toString()</c> implementation.
        /// If the callback returns <see langword="null"/>, Jint will use its own default logic.
        /// </summary>
        /// <remarks>
        /// In most cases, the AST node passed to the callback is of type <see cref="IFunction" />.
        /// However, there are some special cases:<br/>
        /// - For class constructors, a node of type <see cref="IClass"/> is passed
        ///   since <c>toString()</c> should return the code of the entire class as per specification.<br/>
        /// - For class methods and getters/setters, a node of type <see cref="MethodDefinition"/> is passed
        ///   since <c>toString()</c> should include the <c>get</c> or <c>set</c> tokens for getters/setters and the member name as per specification.<br/>
        /// - For object methods and getters/setters, a node of type <see cref="ObjectProperty"/> is passed
        ///   since <c>toString()</c> should include the <c>get</c> or <c>set</c> tokens for getters/setters and the member name as per specification.
        /// </remarks>
        internal static readonly Func<Function, Node, string?> _defaultFunctionToStringHandler = static (_, _) => null;

        public Func<Function, Node, string?> FunctionToStringHandler { get; set; } = _defaultFunctionToStringHandler;

        internal HostOptions Clone() => (HostOptions) MemberwiseClone();
    }

    /// <summary>
    /// Module related customization
    /// </summary>
    public class ModuleOptions
    {
        /// <summary>
        /// Whether to register require function to engine which will delegate to module loader, defaults to false.
        /// </summary>
        public bool RegisterRequire { get; set; }

        /// <summary>
        /// Module loader implementation, by default exception will be thrown if module loading is not enabled.
        /// </summary>
        public IModuleLoader ModuleLoader { get; set; } = FailFastModuleLoader.Instance;

        /// <summary>
        /// Maximum number of distinct module records an engine may register over its lifetime. Charged once per
        /// successfully registered module (cache key); cached, duplicate or coalesced loads do not recharge.
        /// Programmatic modules (<see cref="Engine.ModuleOperations.Add(string,string)"/>) participate.
        /// <see cref="int.MaxValue"/> means unlimited (the default). A non-positive finite value throws at
        /// engine construction.
        /// </summary>
        public int MaxModuleCount { get; set; } = int.MaxValue;

        /// <summary>
        /// Maximum cumulative UTF-8 encoded source bytes across all modules registered in an engine's lifetime.
        /// Charged once per distinct module at registration time. Modules whose original encoded source size is
        /// unknowable — exports-only builders, prepared modules, and custom <see cref="Module"/> records —
        /// charge 0 bytes; raw byte modules use their exact byte length; string sources use their UTF-8 byte
        /// count. <see cref="long.MaxValue"/> means unlimited (the default). A non-positive finite value throws
        /// at engine construction.
        /// </summary>
        public long MaxTotalModuleSourceBytes { get; set; } = long.MaxValue;

        /// <summary>
        /// Maximum conservative import-chain depth in a module graph load. Root is depth 1. Strongly connected
        /// cycles are collapsed for traversal but contribute every module in the cycle to the depth; the longest
        /// path through that acyclic component graph is enforced. The result is deterministic regardless of DFS
        /// sibling order or asynchronous completion order and never lets a cycle increase depth without bound.
        /// A depth that exceeds this limit throws <see cref="ModuleGraphLimitException"/>.
        /// <see cref="int.MaxValue"/> means unlimited (the default). A non-positive finite value throws at
        /// engine construction.
        /// </summary>
        public int MaxModuleGraphDepth { get; set; } = int.MaxValue;

        /// <summary>
        /// Maximum number of module-loader <see cref="IModuleLoader.Resolve"/> calls per top-level import or
        /// load operation. Cached <c>[[LoadedModules]]</c> hits do not count. The budget resets for each
        /// <see cref="Engine.ModuleOperations.Import(string)"/>,
        /// <see cref="Engine.ModuleOperations.StartImport(string)"/>, or host call to
        /// <see cref="Jint.Runtime.Modules.Module.LoadRequestedModules()"/>. Registration indexing does not consume hops, so pooled
        /// engines never fail from accumulated registrations. <see cref="int.MaxValue"/> means unlimited
        /// (the default). A non-positive finite value throws at engine construction.
        /// </summary>
        public int MaxModuleResolutionHops { get; set; } = int.MaxValue;

        /// <summary>
        /// An optional policy consulted after resolution but before a module is loaded or fetched. A denial
        /// throws <see cref="ModuleResolutionException"/> and follows existing sync/rejection behaviour.
        /// <c>null</c> means no policy (the default — everything the loader resolves is allowed).
        /// </summary>
        public IModuleLoadPolicy? LoadPolicy { get; set; }

        /// <summary>
        /// When <c>true</c>, module loading failures expose the original exception message to script code.
        /// Defaults to <c>false</c>, so paths, URLs, transport details and other host information are replaced
        /// with a generic message. The original exception remains available to the host through
        /// <see cref="JintException.TryGetClrException"/>.
        /// </summary>
        public bool ExposeDetailedLoadErrors { get; set; }

        internal ModuleOptions Clone() => (ModuleOptions) MemberwiseClone();
    }

    /// <summary>
    /// JSON.parse / JSON.stringify related customization
    /// </summary>
    public class JsonOptions
    {
        /// <summary>
        /// The maximum depth allowed when parsing JSON files using "JSON.parse",
        /// defaults to 64.
        /// </summary>
        public int MaxParseDepth { get; set; } = 64;
    }

    /// <summary>
    /// Internationalization (Intl) API related customization.
    /// </summary>
    public class IntlOptions
    {
        /// <summary>
        /// CLDR provider for locale data. Defaults to DefaultCldrProvider
        /// which provides basic English (en-US, en-GB) support.
        /// </summary>
        /// <remarks>
        /// Set this to a custom ICldrProvider implementation (e.g., ICU-based provider)
        /// to enable full locale support for the Intl API.
        /// </remarks>
        public ICldrProvider CldrProvider { get; set; } = DefaultCldrProvider.Instance;
    }

    /// <summary>
    /// Temporal API related customization.
    /// </summary>
    public class TemporalOptions
    {
        /// <summary>
        /// Time zone provider for Temporal operations. Defaults to DefaultTimeZoneProvider
        /// which uses .NET TimeZoneInfo for basic IANA time zone support.
        /// </summary>
        /// <remarks>
        /// Set this to a custom ITimeZoneProvider implementation (e.g., using TimeZoneConverter or NodaTime)
        /// for full IANA time zone support and better Windows compatibility.
        /// </remarks>
        public ITimeZoneProvider TimeZoneProvider { get; set; } = DefaultTimeZoneProvider.Instance;

        /// <summary>
        /// Calendar provider for non-ISO calendar arithmetic and field conversion.
        /// Defaults to <see cref="DefaultCalendarProvider"/> which uses .NET
        /// <see cref="System.Globalization.Calendar"/> subclasses.
        /// </summary>
        /// <remarks>
        /// Set this to a custom <see cref="ICalendarProvider"/> implementation
        /// (e.g. backed by ICU4N or NodaTime) for richer support of islamic-umalqura,
        /// Persian astronomical, or Chinese/Dangi calendars at extreme dates.
        /// </remarks>
        public ICalendarProvider CalendarProvider { get; set; } = DefaultCalendarProvider.Instance;
    }
}

/// <summary>
/// Rules for writing values to CLR fields.
/// </summary>
[Flags]
public enum ValueCoercionType
{
    /// <summary>
    /// No coercion will be done. If there's no type converter, and error will be thrown.
    /// </summary>
    None = 0,

    /// <summary>
    /// JS coercion using boolean rules "dog" == true, "" == false, 1 == true, 3 == true, 0 == false, { "prop": 1 } == true etc.
    /// </summary>
    Boolean = 1,

    /// <summary>
    /// JS coercion to numbers, false == 0, true == 1. valueOf functions will be used when available for object instances.
    /// Valid against targets of type: Decimal, Double, Int32, Int64.
    /// </summary>
    Number = 2,

    /// <summary>
    /// JS coercion to strings, toString function will be used when available for objects.
    /// </summary>
    String = 4,

    /// <summary>
    /// All coercion rules enabled.
    /// </summary>
    All = Boolean | Number | String
}

/// <summary>
/// Features that only work partially, if all.
/// </summary>
[Flags]
public enum ExperimentalFeature
{
    /// <summary>
    /// No experimental features enabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Generator support
    /// </summary>
    [Obsolete("This flag is no longer necessary as generators are fully supported.", error: true)]
    Generators = 1,

    /// <summary>
    /// Wrapping tasks to promises
    /// </summary>
    TaskInterop = 2,

    /// <summary>
    /// All coercion rules enabled.
    /// </summary>
    All = TaskInterop,
}

/// <summary>
/// Strategy for exposing CLR enum values to script code, see <see cref="Options.InteropOptions.EnumConversion"/>.
/// </summary>
public enum EnumConversionMode
{
    /// <summary>
    /// The enum's underlying numeric value is exposed. This is the default.
    /// </summary>
    Number,

    /// <summary>
    /// The enum member name is exposed as a string, as produced by <see cref="object.ToString"/> — a combination
    /// of names for a matching <see cref="FlagsAttribute"/> value, and the numeric value rendered as a string for
    /// a value that has no name. Values converted back to the CLR keep accepting both the name and the number.
    /// </summary>
    String,
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EngineConfiguration(
    Action<Engine> Apply,
    EngineConfigurationProvenance Provenance);

internal enum EngineConfigurationProvenance
{
    FirstParty,
    User
}

internal enum ClrAccessConfiguration
{
    None,
    Compatibility,
    Explicit
}

/// <summary>
/// Strategy for exposing CLR arrays (<c>T[]</c>) to script code, see <see cref="Options.InteropOptions.ArrayConversion"/>.
/// </summary>
public enum ArrayConversionMode
{
    /// <summary>
    /// The default. Each conversion copies the array contents into a native JavaScript array
    /// (<c>Array.isArray</c> returns <c>true</c>) that script can mutate and resize normally. Script-side mutations
    /// affect only the copy, and CLR-side mutations after the conversion are not visible through it. Each crossing
    /// produces a new independent snapshot unless
    /// <see cref="Options.InteropOptions.TrackObjectWrapperIdentity"/> or
    /// <see cref="Options.InteropOptions.CacheRecentObjectWrappers"/> reuses the earlier one; when reused, script-side
    /// mutations and object identity persist for that cached snapshot, but CLR-side changes are still not re-copied.
    /// Self- and mutually referential CLR arrays preserve those cycles in the JavaScript snapshot.
    /// Multidimensional and non-zero-based CLR arrays are unsupported: non-empty instances throw during
    /// conversion, while zero-length instances currently produce an empty snapshot.
    /// </summary>
    Copy,

    /// <summary>
    /// The explicit opt-in. Single-rank arrays are exposed as live wrapper views over the underlying CLR array,
    /// avoiding the copy and its allocation, the same way wrapped
    /// <see cref="System.Collections.Generic.List{T}"/> instances already behave: element reads and writes go
    /// straight to the array in both directions when <see cref="Options.InteropOptions.AllowWrite"/> is enabled.
    /// With writes disabled, reads remain live but mutations that would reach the backing array are denied. Repeated
    /// views over the same CLR array compare strictly equal by wrapped-target identity even when caches are disabled;
    /// whether the same wrapper instance is returned follows
    /// the same caches as other wrapped objects (<see cref="Options.InteropOptions.TrackObjectWrapperIdentity"/> /
    /// <see cref="Options.InteropOptions.CacheRecentObjectWrappers"/>). An array crossing under a non-array declared
    /// type keeps honoring that contract (for example a member typed <c>IReadOnlyList&lt;T&gt;</c> produces a read-only
    /// view).
    /// <para>
    /// The view is array-like — iteration, <c>Array.prototype</c> methods, index-key enumeration
    /// (<c>Object.keys</c> / <c>for-in</c>), out-of-range reads yielding <c>undefined</c> and JSON
    /// serialization as an array all work — but it is not a JavaScript array: <c>Array.isArray</c> returns
    /// <c>false</c> (while <c>instanceof Array</c> is <c>true</c> through the attached prototype), and because
    /// CLR arrays are fixed-size the view behaves like an integer-indexed exotic object: resizing attempts
    /// (growing writes, <c>push</c>/<c>pop</c>, <c>length</c> writes) throw a <c>TypeError</c>, and like for
    /// typed arrays, <c>shift</c>/<c>splice</c> may move elements before their length change throws.
    /// Multidimensional and non-zero-based CLR arrays are unsupported in both modes. They fall back to the
    /// <see cref="Copy"/> path, where non-empty instances throw during conversion and zero-length instances
    /// currently produce an empty snapshot.
    /// </para>
    /// </summary>
    LiveView,
}

/// <summary>
/// Strategy for exposing a CLR sequence that is only an <see cref="System.Collections.Generic.IEnumerable{T}"/> to
/// script code, see <see cref="Options.InteropOptions.EnumerableConversion"/>.
/// </summary>
public enum EnumerableConversionMode
{
    /// <summary>
    /// The sequence is exposed as an opaque wrapper carrying <c>Symbol.iterator</c>. It is enumerated only when
    /// script asks it to be — <c>for-of</c>, spread, <c>Array.from</c> — and each of those enumerates it again,
    /// exactly as enumerating the sequence from CLR code would. It is not array-like: it has no <c>length</c>, no
    /// index access, and no <c>Array.prototype</c>, so the natives every JavaScript author expects on a list are
    /// absent unless a registered extension method supplies them. This is the default.
    /// </summary>
    Lazy,

    /// <summary>
    /// The sequence is enumerated once, when it crosses into script, and exposed as a fixed-size array-like view
    /// over that snapshot — <c>length</c>, index reads, <c>Array.prototype</c> and JSON serialization as an array
    /// all work, while <c>Array.isArray</c> stays <c>false</c> just as for a wrapped <c>T[]</c>.
    /// <para>
    /// Two consequences follow from enumerating eagerly, and they are the reason this is not the default. The
    /// sequence's side effects happen at the crossing rather than when script first looks, and a sequence that
    /// never ends never returns. Later CLR-side changes are not observed either: the view is a snapshot, and
    /// writes through it reach only the snapshot. Sequences that already have a count
    /// (<see cref="System.Collections.Generic.ICollection{T}"/>, <see cref="System.Collections.Generic.IList{T}"/>,
    /// <c>T[]</c>) and dictionaries are never snapshotted; they keep their existing, live exposure.
    /// </para>
    /// </summary>
    Snapshot,
}
