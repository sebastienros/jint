using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
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

public sealed partial class Options
{
    private static readonly CultureInfo _defaultCulture = CultureInfo.CurrentCulture;
    private static readonly TimeZoneInfo _defaultTimeZone = TimeZoneInfo.Local;

    private ITimeSystem? _timeSystem;
    internal List<EngineConfiguration> _configurations { get; private set; } = new();

    /// <summary>
    /// The one door onto <see cref="_configurations"/>, so that <c>Configure</c>, <c>AddLazyGlobal</c> and
    /// <c>SetTypeConverter</c> refuse after the freeze exactly as an assignment does.
    /// </summary>
    /// <param name="configuration">The callback to run against every engine built from these options.</param>
    /// <param name="verb">
    /// Filled in by the compiler with the extension method the host actually called, so that the refusal names
    /// that rather than this private door.
    /// </param>
    internal void AddConfiguration(EngineConfiguration configuration, [CallerMemberName] string? verb = null)
    {
        if (_readOnly)
        {
            Throw.OptionsReadOnlyCall("Options." + verb);
        }

        _configurations.Add(configuration);
    }

    internal bool HasUserHostFactory { get; set { ThrowIfReadOnly(); field = value; } }

    /// <summary>
    /// Whether <c>SetTypeConverter</c> was called on these options. The converter itself is installed by a
    /// configuration callback and so is only knowable from a built engine; this is what lets a report taken
    /// from <see cref="Options"/> alone say the same thing.
    /// </summary>
    internal bool HasUserTypeConverter { get; set { ThrowIfReadOnly(); field = value; } }
    internal bool HasUserEngineConstructionCallback =>
        HasUserHostFactory
        || _configurations.Exists(static configuration => configuration.Provenance == EngineConfigurationProvenance.User);
    internal UntrustedCodeLimits? UntrustedCodeLimits { get; set { ThrowIfReadOnly(); field = value; } }

    public delegate JsValue? MemberAccessorDelegate(Engine engine, object target, string member);

    public delegate ObjectInstance? WrapObjectDelegate(Engine engine, object target, Type? type);

    public delegate bool ExceptionHandlerDelegate(Exception exception);

    public delegate void ClrExceptionErrorDecoratorDelegate(Engine engine, ObjectInstance error, Exception exception);

    public delegate void ClrResolutionErrorDecoratorDelegate(Engine engine, ObjectInstance error, ClrResolutionErrorInfo info);

    public delegate string? BuildCallStackDelegate(string shortDescription, SourceLocation location, string[]? arguments);

    public delegate string SerializeToJsonDelegate(object? target, string space, string? currentIndent);

    public delegate IEnumerable<JsValue>? ReportedPropertyKeysDelegate(Engine engine, object target);

    /// <summary>
    /// Materializes an option group on first access.
    /// </summary>
    /// <remarks>
    /// Every group accessor on <see cref="Options"/> and on its web-API group has this one shape,
    /// so a default <see cref="Options"/> allocates no group at all and a host pays only for the groups it
    /// touches. The interlocked publication is not decoration: <see cref="Options"/> is documented as safe to
    /// share between engines being constructed concurrently, and every engine build reads several of these
    /// groups, so a plain <c>??=</c> would let two builds each get their own instance and only one of them
    /// see a later host mutation. Groups are read while an engine is being built and never on an execution
    /// path, so the null check costs nothing that matters.
    /// <para>
    /// <paramref name="readOnly"/> is the owner's own frozen state, and a group materialized after the freeze
    /// is born frozen. Without it the freeze would have a hole exactly where it matters least visibly: a group
    /// the engine build never touched — <see cref="Intl"/>, <see cref="Temporal"/> — would be allocated fresh
    /// and mutable by the very host write that was supposed to be refused, and the engine reads those groups
    /// lazily, so the write would land.
    /// </para>
    /// </remarks>
    private static T Materialize<T>(ref T? field, bool readOnly) where T : class, IOptionsGroup, new()
    {
        var existing = field;
        if (existing is not null)
        {
            return existing;
        }

        var created = new T();
        if (readOnly)
        {
            created.SetReadOnly(true);
        }

        return Interlocked.CompareExchange(ref field, created, null) ?? created;
    }

    /// <summary>
    /// Execution constraints for the engine.
    /// </summary>
    public ConstraintOptions Constraints => Materialize(ref _constraints, _readOnly);

    private ConstraintOptions? _constraints;

    /// <summary>
    /// Resource limits applied while parsing scripts and modules.
    /// </summary>
    public ParsingOptions Parsing => Materialize(ref _parsing, _readOnly);

    private ParsingOptions? _parsing;

    /// <summary>
    /// CLR interop related options.
    /// </summary>
    public InteropOptions Interop => Materialize(ref _interop, _readOnly);

    private InteropOptions? _interop;

    /// <summary>
    /// Debugger configuration.
    /// </summary>
    public DebuggerOptions Debugger => Materialize(ref _debugger, _readOnly);

    private DebuggerOptions? _debugger;

    /// <summary>
    /// Script code coverage configuration. Off by default.
    /// </summary>
    public CoverageOptions Coverage => Materialize(ref _coverage, _readOnly);

    private CoverageOptions? _coverage;

    /// <summary>
    /// Host options.
    /// </summary>
    public HostOptions Host => Materialize(ref _host, _readOnly);

    private HostOptions? _host;

    /// <summary>
    /// Module options
    /// </summary>
    public ModuleOptions Modules => Materialize(ref _modules, _readOnly);

    private ModuleOptions? _modules;

    /// <summary>
    /// Internationalization (Intl) options.
    /// </summary>
    public IntlOptions Intl => Materialize(ref _intl, _readOnly);

    private IntlOptions? _intl;

    /// <summary>
    /// Temporal API options.
    /// </summary>
    public TemporalOptions Temporal => Materialize(ref _temporal, _readOnly);

    private TemporalOptions? _temporal;

    /// <summary>
    /// Whether the code should be always considered to be in strict mode. Can improve performance.
    /// </summary>
    public bool Strict { get; set { ThrowIfReadOnly(); field = value; } }

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
    public bool RetainFunctionSourceText { get; set { ThrowIfReadOnly(); field = value; } }

    /// <summary>
    /// The culture the engine runs on, defaults to current culture.
    /// </summary>
    public CultureInfo Culture { get; set { ThrowIfReadOnly(); field = value; } } = _defaultCulture;

    /// <summary>
    /// Configures a time system to use. Defaults to DefaultTimeSystem using local time.
    /// </summary>
    public ITimeSystem TimeSystem
    {
        get => _timeSystem ??= new DefaultTimeSystem(TimeZone, Culture);
        set
        {
            ThrowIfReadOnly();
            _timeSystem = value;
        }
    }

    /// <summary>
    /// The time zone the engine runs on, defaults to local. Same as setting DefaultTimeSystem with the time zone.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set { ThrowIfReadOnly(); field = value; } } = _defaultTimeZone;

    /// <summary>
    /// Reference resolver allows customizing behavior for reference resolving. This can be useful in cases where
    /// you want to ignore long chain of property accesses that might throw if anything is null or undefined.
    /// An example of such is <code>var a = obj.field.subField.value</code>. Custom resolver could accept chain to return
    /// null/undefined on first occurrence.
    /// </summary>
    /// <remarks>
    /// Read-only, because a resolver and the situations it wants to be consulted for are one registration:
    /// interests describe one particular resolver, so a replacement must never inherit the set narrowed for
    /// its predecessor, and a narrowed set must never survive the resolver it was written for. Register both
    /// together with <see cref="OptionsExtensions.SetReferenceResolver"/>.
    /// </remarks>
    public IReferenceResolver ReferenceResolver => _referenceResolver;

    private IReferenceResolver _referenceResolver = DefaultReferenceResolver.Instance;

    /// <summary>
    /// The situations <see cref="ReferenceResolver"/> is consulted for. Defaults to
    /// <see cref="ReferenceResolverInterests.All"/>, which is how every resolver behaved before this filter
    /// existed. Narrowing it keeps interpreter fast paths armed for the situations the resolver opts out of;
    /// see <see cref="ReferenceResolverInterests"/> for what each flag costs. Ignored while
    /// <see cref="ReferenceResolver"/> is the built-in default.
    /// </summary>
    /// <remarks>
    /// Read-only, and set together with the resolver it describes through
    /// <see cref="OptionsExtensions.SetReferenceResolver"/>. Through 4.16.x both were settable and assigning
    /// <see cref="ReferenceResolver"/> silently reset this to <see cref="ReferenceResolverInterests.All"/>, so
    /// the same two assignments meant different things depending on the order they were written in.
    /// </remarks>
    public ReferenceResolverInterests ReferenceResolverInterests => _referenceResolverInterests;

    private ReferenceResolverInterests _referenceResolverInterests = ReferenceResolverInterests.All;

    internal void SetReferenceResolverCore(IReferenceResolver resolver, ReferenceResolverInterests interests)
    {
        ThrowIfReadOnly(nameof(ReferenceResolver));
        _referenceResolver = resolver;
        _referenceResolverInterests = interests;
    }

    /// <summary>
    /// Options for the built-in JSON (de)serializer which
    /// gets used using <c>JSON.parse</c> or <c>JSON.stringify</c>
    /// </summary>
    public JsonOptions Json => Materialize(ref _json, _readOnly);

    private JsonOptions? _json;

    /// <summary>
    /// Limits applied by host-side result conversion and JSON serialization. Defaults to
    /// <see cref="ResultLimits.Unlimited"/> for compatibility.
    /// </summary>
    /// <remarks>
    /// Setting this also bounds script-visible <c>JSON.stringify</c>. Use per-call limits on
    /// <see cref="Engine.ConvertResult"/> or <see cref="Native.Json.JsonSerializer"/> when
    /// different requests sharing one options object need different policies.
    /// </remarks>
    public ResultLimits ResultLimits
    {
        get => _resultLimits;
        set
        {
            ThrowIfReadOnly();
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
    public ExperimentalFeature ExperimentalFeatures { get; set { ThrowIfReadOnly(); field = value; } }

    /// <summary>
    /// Whether the agent can suspend (block) via Atomics.wait().
    /// Defaults to false. Worker-like hosts that deliberately allow blocking suspension must opt in.
    /// This option does not affect Atomics.waitAsync().
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma262/#sec-agentcansuspend
    /// </remarks>
    public bool AgentCanSuspend { get; set { ThrowIfReadOnly(); field = value; } }

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
    /// <remarks>
    /// The <see cref="UntrustedCodeLimits"/> marker is deliberately <b>not</b> propagated, although it is the
    /// most posture-shaped thing an <see cref="Options"/> can carry. An engine built from a marked options
    /// object re-applies the profile's expansion at construction, and that expansion clears the constraint
    /// registrations — including the cancellation constraint a worker's <c>terminate()</c> depends on. The
    /// profile still travels in full: an untrusted parent's <see cref="Engine.Options"/> is the expanded
    /// clone, so every value the expansion set is copied by this method, its budget constraints arrive
    /// through the factory replay, and the grants it revoked equal the fresh target's defaults. What a
    /// non-marked worker loses is only the label — a diagnostics report will not call it profiled.
    /// </remarks>
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
        target.Parsing.MaxSourceLength = source.Parsing.MaxSourceLength;
        target.Parsing.MaxNodeCount = source.Parsing.MaxNodeCount;
        target.Modules.MaxModuleCount = source.Modules.MaxModuleCount;
        target.Modules.MaxTotalModuleSourceBytes = source.Modules.MaxTotalModuleSourceBytes;
        target.Modules.MaxModuleGraphDepth = source.Modules.MaxModuleGraphDepth;
        target.Modules.MaxModuleResolutionHops = source.Modules.MaxModuleResolutionHops;

        // Class-typed, so outside the reflective pin's value-typed scan — classified here instead. The
        // limits bundle is sealed and shared by reference, exactly as sharing one Options between engines
        // already shares it; a bounded parent's conversions must not become unbounded in its worker.
        target.ResultLimits = source.ResultLimits;
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

        // Whether Atomics.wait may block the thread. A second engine that can block a host thread its creator
        // could not is exactly the shape this rule exists to refuse.
        "AgentCanSuspend",

        // The JSON parser's depth bound.
        "Json.MaxParseDepth",

        // The parser's own bounds: source length and AST size. A worker parses whatever its loader hands it,
        // so a parent bounded against parser abuse spawns workers bounded the same way.
        "Parsing.MaxSourceLength",
        "Parsing.MaxNodeCount",

        // The module-graph bounds. They restrict how much code a graph may pull in, whichever loader the
        // provider installs — the loader is a grant and stays behind, the limits are restrictions and travel.
        "Modules.MaxModuleCount",
        "Modules.MaxTotalModuleSourceBytes",
        "Modules.MaxModuleGraphDepth",
        "Modules.MaxModuleResolutionHops",
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

        // Grant-shaped: the only feature it carries today is CLR task interop, and interop grants are the
        // host's to make deliberately, per engine.
        "ExperimentalFeatures",

        // Grant-shaped: installs the CommonJS `require` shim into the second engine's global. Whether a
        // worker gets it is its provider's module story, not its parent's.
        "Modules.RegisterRequire",

        // Exposure, which is a grant: detailed load errors name paths and hosts a hardened parent may be
        // redacting. The restrictive default is false, and a worker starts there whatever the parent chose.
        "Modules.ExposeDetailedLoadErrors",
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

        // Modules is deliberately NOT here: it used to be excluded wholesale as grant-shaped, and stopped
        // being classifiable that way the day it gained value-typed graph limits beside its loader. The group
        // is scanned now — the loader and load policy are reference-typed host objects the scan never sees
        // (grants, per engine), the numeric limits travel, and the two boolean grants are named above.

        // Grant-shaped at the top: the feature mask IS the grant, and it is computed explicitly rather than
        // copied (network, storage, routing and the worker capability itself are subtracted), while the rest
        // of the group is host wiring — providers, sinks, clocks. What value settings it has are per-feature
        // caps living on the sub-groups (Timers.MaxActiveTimers, Storage.MaxTotalBytes, Fetch.MaxResponseBytes
        // …), and each of those rides with the feature it bounds: a second engine that was not granted the
        // feature cannot reach the cap, and one that was is being granted the capability deliberately, by a
        // host that is choosing the cap in the same breath. Named here rather than copied, so that this is a
        // decision on the record and not a group nobody looked at.
        "WebApi",

        // Host diagnostics, and engine-affine: a debugger attached to one engine says nothing about another
        // nobody is stepping.
        "Debugger",

        // Host diagnostics: coverage counters are per engine, and a host collecting one engine's coverage did
        // not ask for a second engine's.
        "Coverage",

        // Host diagnostics again, and a capability script cannot reach at all — a profiling session is opened
        // by the host through Diagnostics.StartProfiling. Its gate defaults to refusing, so a second engine
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
        // rather than in UseNodeBuiltinModules so that the order of that call and UseModules cannot
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

        // MemberwiseClone copies every group *reference*, so each one the profile may write to has to be
        // replaced by a copy of its own — otherwise hardening one engine mutates the host's shared options.
        // Every group is cloned rather than the handful the profile happens to touch today, so that adding a
        // group or widening the profile cannot silently reopen that hole; a group nobody materialized stays
        // unmaterialized, and costs nothing to "clone".
        var clone = (Options) MemberwiseClone();
        clone._configurations = new List<EngineConfiguration>(_configurations);
        clone._constraints = _constraints?.Clone();
        clone._parsing = _parsing?.Clone();
        clone._interop = _interop?.Clone();
        clone._debugger = _debugger?.Clone();
        clone._coverage = _coverage?.Clone();
        clone._host = _host?.Clone();
        clone._modules = _modules?.Clone();
        clone._intl = _intl?.Clone();
        clone._temporal = _temporal?.Clone();
        clone._json = _json?.Clone();
        clone._profiling = _profiling?.Clone();
#if NET8_0_OR_GREATER
        clone._webApi = _webApi?.Clone();
#endif
        // The clone inherits the source's frozen state through MemberwiseClone, and the profile below is a
        // long sequence of writes. A host may legitimately hand a frozen Options to a second engine, so the
        // private snapshot is thawed for the expansion; the engine freezes it again once it has read it.
        clone.SetReadOnly(false);
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


    public sealed partial class DebuggerOptions
    {
        /// <summary>
        /// Whether debugger functionality is enabled, defaults to false.
        /// </summary>
        public bool Enabled { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Configures the statement handling strategy, defaults to Ignore.
        /// </summary>
        public DebuggerStatementHandling StatementHandling { get; set { ThrowIfReadOnly(); field = value; } } = DebuggerStatementHandling.Ignore;

        /// <summary>
        /// Configures the step mode used when entering the script.
        /// </summary>
        public StepMode InitialStepMode { get; set { ThrowIfReadOnly(); field = value; } } = StepMode.None;

        internal DebuggerOptions Clone() => (DebuggerOptions) MemberwiseClone();
    }

    /// <summary>
    /// Script code coverage configuration.
    /// </summary>
    /// <remarks>
    /// Nothing here is engine-affine, so an <see cref="Options"/> instance carrying it stays shareable across
    /// engines — each engine gets its own counters, and reading one engine's report never sees another's.
    /// </remarks>
    public sealed partial class CoverageOptions
    {
        /// <summary>
        /// Whether the engine counts what it executes, defaults to false. When set, the engine collects hit
        /// counts readable through <see cref="Engine.DiagnosticOperations.GetCoverage"/>; when not set — the
        /// default — the interpreter contains no coverage work of any kind, since the per-statement lane the
        /// counting rides on is not armed.
        /// </summary>
        /// <remarks>
        /// Enabling it disarms the interpreter's tight-loop lane for the engine, the same way registering an
        /// exact execution constraint or enabling the debugger does, so measured code runs the instrumented
        /// path. See <see cref="Engine.DiagnosticOperations.GetCoverage"/>.
        /// </remarks>
        public bool Enabled { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// What is counted, defaults to <see cref="CoverageGranularity.Statements"/>. The granularity decides
        /// what the report contains, not how the engine executes: both values collect through the same lane.
        /// </summary>
        public CoverageGranularity Granularity { get; set { ThrowIfReadOnly(); field = value; } } = CoverageGranularity.Statements;

        internal CoverageOptions Clone() => (CoverageOptions) MemberwiseClone();
    }

    public sealed partial class InteropOptions
    {
        /// <summary>
        /// Whether the <c>System</c> namespace object, <c>importNamespace</c> and <c>clrHelper</c> are
        /// installed on the global object at all, defaults to <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <b>Setting this alone grants script nothing to resolve.</b> It is the gate, not the grant:
        /// <see cref="AllowedAssemblies"/> is a closed allow-list, and an engine with the gate open and an
        /// empty list has <c>importNamespace</c> present and every namespace it names unresolvable. The
        /// grant is <see cref="OptionsExtensions.AllowClr(Options, System.Reflection.Assembly[])"/>, which
        /// opens the gate <i>and</i> names the assemblies; the parameterless
        /// <see cref="OptionsExtensions.AllowClr(Options)"/> additionally adds the assembly containing
        /// <see cref="object"/> and selects the permissive resolution policy, so it is strictly more than
        /// this property, not a spelling of it.
        /// <para>
        /// Both are reported by
        /// <see cref="OptionsSecurityExtensions.ValidateSecurityConfiguration(Options, SecurityConfigurationPolicy)"/>
        /// — the gate as <c>JINTSEC015</c>, and <c>AllowClr()</c>'s compatibility policy additionally as its
        /// own diagnostic — so a host hardening an engine sees the difference rather than having to know it.
        /// </para>
        /// </remarks>
        public bool Enabled { get; set { ThrowIfReadOnly(); field = value; } }

        internal ClrAccessConfiguration ClrAccessConfiguration { get; set { ThrowIfReadOnly(); field = value; } }

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
        /// Read once, while the engine is being constructed: resolved members are cached under the value
        /// captured then. Enabling it grants powerful reflection capabilities and is not recommended for
        /// untrusted scripts.
        /// </para>
        /// </remarks>
        public bool AllowGetType { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Whether Jint should allow resolving or wrapping types from the <c>System.Reflection</c> namespace.
        /// Defaults to false. A <see cref="TypeReference"/> explicitly exported by the host remains an explicit
        /// capability and is not subject to namespace-resolution policy.
        /// </summary>
        /// <remarks>
        /// Namespace resolution reads this once when the engine installs its CLR globals. Configure it before
        /// constructing the engine.
        /// </remarks>
        public bool AllowSystemReflection { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Whether direct writes through projected CLR objects are allowed, including fields, properties,
        /// indexers, and collection entries. Defaults to false.
        /// </summary>
        /// <remarks>
        /// This option controls the wrapper's write operations. It does not prevent scripts from calling CLR
        /// methods or extension methods whose implementations mutate host state.
        /// </remarks>
        public bool AllowWrite { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Whether operator overloading resolution is allowed, defaults to false.
        /// </summary>
        /// <remarks>
        /// Read once per engine, at the end of construction and so after any callback registered with
        /// <c>Options.Configure</c> has run.
        /// </remarks>
        public bool AllowOperatorOverloading { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Types holding extension methods that should be considered when resolving methods.
        /// </summary>
        public OptionsList<Type> ExtensionMethodTypes { get; private set; } = new("Options.Interop.ExtensionMethodTypes");

        /// <summary>
        /// Object converters to try when build-in conversions.
        /// </summary>
        public OptionsList<ObjectConverter> ObjectConverters { get; private set; } = new("Options.Interop.ObjectConverters");

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
        public OptionsList<Type> ImmutableCrossingTypes { get; private set; } = new("Options.Interop.ImmutableCrossingTypes");

        /// <summary>
        /// Whether identity map is persisted for object wrappers in order to maintain object identity. This can cause
        /// memory usage to grow when targeting large set and freeing of memory can be delayed due to ConditionalWeakTable semantics.
        /// Also covers CLR arrays: repeated conversions of the same array instance return the same result — the same
        /// JsArray snapshot under the default <see cref="ArrayConversionMode.Copy"/> mode (script-side mutations persist
        /// across reads; CLR-side mutations after the first conversion are not re-copied), or the live wrapper view
        /// under <see cref="ArrayConversionMode.LiveView"/> — instead of re-converting on every read.
        /// Defaults to false.
        /// </summary>
        public bool TrackObjectWrapperIdentity { get; set { ThrowIfReadOnly(); field = value; } }

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
        public bool CacheRecentObjectWrappers { get; set { ThrowIfReadOnly(); field = value; } } = true;

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
        public ArrayConversionMode ArrayConversion { get; set { ThrowIfReadOnly(); field = value; } } = ArrayConversionMode.Copy;

        /// <summary>
        /// How a CLR sequence that is <em>only</em> an <see cref="System.Collections.Generic.IEnumerable{T}"/> — a LINQ
        /// iterator, a generator method's result, any lazily computed sequence with no <c>Count</c> and no indexer — is
        /// exposed to script code. Defaults to <see cref="Jint.EnumerableConversionMode.Lazy"/>, which changes nothing.
        /// Collections that already have a count (<see cref="System.Collections.Generic.ICollection{T}"/>,
        /// <see cref="System.Collections.Generic.IList{T}"/>, <c>T[]</c>) and dictionaries are unaffected by this
        /// option; they are exposed exactly as before. See the enum members.
        /// </summary>
        public EnumerableConversionMode EnumerableConversion { get; set { ThrowIfReadOnly(); field = value; } } = EnumerableConversionMode.Lazy;

        /// <summary>
        /// If no known type could be guessed, objects are by default wrapped as an
        /// ObjectInstance using class ObjectWrapper. This function can be used to
        /// change the behavior.
        /// </summary>
        internal static readonly WrapObjectDelegate _defaultWrapObjectHandler = DefaultWrapObject;

        /// <summary>
        /// The engine's own projection funnel: every host object that reaches script without a more
        /// specific conversion arrives here.
        /// </summary>
        /// <remarks>
        /// It cannot carry <c>[RequiresUnreferencedCode]</c> itself — it has to be assignable to the public
        /// <see cref="WrapObjectDelegate"/>, and a host replacing the handler would then be unable to call
        /// back into it. The requirement is declared where a host can act on it instead: on
        /// <see cref="OptionsExtensions.AllowClr(Options)"/>, on <see cref="Engine.SetValue(string, object)"/>,
        /// and on <see cref="ObjectWrapper.Create"/> itself, which is what a custom handler calls.
        /// The suppression is <see cref="UnconditionalSuppressMessageAttribute"/> rather than a
        /// <c>#pragma</c> because a pragma reaches Roslyn and not ILC, which re-derives every diagnostic
        /// over the closed program.
        /// </remarks>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Declared on the public entry points a host object crosses to get here: OptionsExtensions.AllowClr, Engine.SetValue(string, object), ObjectWrapper.Create.")]
        private static ObjectInstance DefaultWrapObject(Engine engine, object target, Type? type)
            => ObjectWrapper.Create(engine, target, type);

        public WrapObjectDelegate WrapObjectHandler { get; set { ThrowIfReadOnly(); field = value; } } = _defaultWrapObjectHandler;

        /// <summary>
        /// The handler used to build stack traces. Changing this enables mapping
        /// stack traces to code different from the code being executed, eg. when
        /// executing code transpiled from TypeScript.
        /// </summary>
        public BuildCallStackDelegate? BuildCallStackHandler { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Named default so the interop member inline cache (see JintMemberExpression's wrapper lane) can
        /// detect by reference when a custom accessor has been configured and stay out of the way.
        /// </summary>
        internal static readonly MemberAccessorDelegate _defaultMemberAccessor = static (engine, target, member) => null;

        /// <summary>
        ///
        /// </summary>
        public MemberAccessorDelegate MemberAccessor { get; set { ThrowIfReadOnly(); field = value; } } = _defaultMemberAccessor;

        /// <summary>
        /// Exceptions that thrown from CLR code are converted to JavaScript errors and
        /// can be used in at try/catch statement. By default these exceptions are bubbled
        /// to the CLR host and interrupt the script execution. If handler returns true these exceptions are converted
        /// to JS errors that can be caught by the script.
        /// </summary>
        public ExceptionHandlerDelegate ExceptionHandler { get; set { ThrowIfReadOnly(); field = value; } } = _defaultExceptionHandler;

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
        public bool ChainClrExceptionAsInnerException { get; set { ThrowIfReadOnly(); field = value; } }

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
        public bool ExposeDetailedExceptionMessages { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Called after a JavaScript error object is created from a CLR exception, either because
        /// <see cref="ExceptionHandler"/> returned true or because a module loader exception became an import error.
        /// Allows decorating the error object with additional properties or modifying its state.
        /// The decorator receives the engine instance, the created error object, and the original CLR exception.
        /// </summary>
        public ClrExceptionErrorDecoratorDelegate? ClrExceptionErrorDecorator { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Called after the JavaScript error object for a failed CLR method or constructor resolution has been
        /// created, just before it is thrown into the script. The decorator may overwrite the script-visible
        /// <c>message</c> and add custom properties (e.g. an error code). It receives the full structured
        /// resolution information via <see cref="ClrResolutionErrorInfo"/> regardless of
        /// <see cref="ExposeDetailedResolutionErrors"/>, which only selects the default message.
        /// </summary>
        public ClrResolutionErrorDecoratorDelegate? ClrResolutionErrorDecorator { get; set { ThrowIfReadOnly(); field = value; } }

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
        public OptionsList<Assembly> AllowedAssemblies { get; private set; } = new("Options.Interop.AllowedAssemblies");

        /// <summary>
        /// Type and member resolving strategy, which allows filtering allowed members and configuring member
        /// name matching comparison.
        /// </summary>
        /// <remarks>
        /// As this object holds caching state same instance should be shared between engines, if possible.
        /// </remarks>
        public TypeResolver TypeResolver { get; set { ThrowIfReadOnly(); field = value; } } = TypeResolver.Default;

        /// <summary>
        /// When writing values to CLR objects, how should JS values be coerced to CLR types.
        /// Defaults to only coercing to string values when writing to string targets.
        /// </summary>
        public ValueCoercionType ValueCoercion { get; set { ThrowIfReadOnly(); field = value; } } = ValueCoercionType.String;

        /// <summary>
        /// How CLR enum values are exposed to script code when they cross into JavaScript, defaults to
        /// <see cref="Jint.EnumConversionMode.Number"/> (the underlying numeric value). This is the read direction;
        /// the write direction is governed by <see cref="ValueCoercion"/> and always accepts both the member name
        /// and the numeric value.
        /// </summary>
        public EnumConversionMode EnumConversion { get; set { ThrowIfReadOnly(); field = value; } } = EnumConversionMode.Number;

        /// <summary>
        /// Strategy to create a CLR object to hold converted <see cref="ObjectInstance"/>.
        /// </summary>
        internal static readonly Func<ObjectInstance, IDictionary<string, object?>> _defaultCreateClrObject =
            static _ => new ExpandoObject();

        public Func<ObjectInstance, IDictionary<string, object?>>? CreateClrObject { get; set { ThrowIfReadOnly(); field = value; } } = _defaultCreateClrObject;

        /// <summary>
        /// Strategy to create a CLR object from TypeReference.
        /// Defaults to retuning null which makes TypeReference attempt to find suitable constructor.
        /// </summary>
        internal static readonly Func<Engine, Type, JsValue[], object?> _defaultCreateTypeReferenceObject =
            static (_, _, _) => null;

        public Func<Engine, Type, JsValue[], object?> CreateTypeReferenceObject { get; set { ThrowIfReadOnly(); field = value; } } = _defaultCreateTypeReferenceObject;

        internal static readonly ExceptionHandlerDelegate _defaultExceptionHandler = static exception => false;

        /// <summary>
        /// When not null, is used to serialize any CLR object in an
        /// <see cref="IObjectWrapper"/> passing through 'JSON.stringify'.
        /// </summary>
        public SerializeToJsonDelegate? SerializeToJson { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// What kind of date time should be produced when JavaScript date is converted to DateTime. If Local, uses <see cref="Options.TimeZone"/>.
        /// Defaults to <see cref="System.DateTimeKind.Utc"/>.
        /// </summary>
        public DateTimeKind DateTimeKind { get; set { ThrowIfReadOnly(); field = value; } } = DateTimeKind.Utc;

        /// <summary>
        /// Should the Array prototype be attached instead of Object prototype to the wrapped interop objects when type looks suitable. Defaults to true.
        /// </summary>
        public bool AttachArrayPrototype { get; set { ThrowIfReadOnly(); field = value; } } = true;

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
        public bool PreferJsPrototypeMethods { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Whether the engine should throw an error when a member is not found on a CLR object. Defaults to false.
        /// </summary>
        public bool ThrowOnUnresolvedMember { get; set { ThrowIfReadOnly(); field = value; } }

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
        public bool ExposeDetailedResolutionErrors { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Types of CLR members reported by <see cref="ObjectWrapper"/> when enumerating properties/serializing <see cref="ObjectWrapper.ToObject"/>.
        /// Supported values are: <see cref="MemberTypes.Field"/>, <see cref="MemberTypes.Property"/>, <see cref="MemberTypes.Method"/>.
        /// All other values are ignored.
        /// </summary>
        public MemberTypes ObjectWrapperReportedMemberTypes { get; set { ThrowIfReadOnly(); field = value; } } = MemberTypes.Field | MemberTypes.Property | MemberTypes.Method;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" />.
        /// </summary>
        /// <inheritdoc cref="AllowGetType" path="/remarks"/>
        public BindingFlags ObjectWrapperReportedFieldBindingFlags { get; set { ThrowIfReadOnly(); field = value; } } = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" />.
        /// </summary>
        /// <inheritdoc cref="AllowGetType" path="/remarks"/>
        public BindingFlags ObjectWrapperReportedPropertyBindingFlags { get; set { ThrowIfReadOnly(); field = value; } } = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// Reported member binding flags when reflecting, defaults to <see cref="BindingFlags.Instance" /> | <see cref="BindingFlags.Public" /> | <see cref="BindingFlags.Static" />.
        /// </summary>
        /// <inheritdoc cref="AllowGetType" path="/remarks"/>
        public BindingFlags ObjectWrapperReportedMethodBindingFlags { get; set { ThrowIfReadOnly(); field = value; } } = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Customizes the property keys reported by <see cref="ObjectWrapper"/> when a wrapped CLR object is
        /// enumerated (JavaScript <c>for..in</c>, <c>Object.keys</c>, object spread, <c>JSON.stringify</c>).
        /// When the delegate returns a non-null sequence, those keys replace the default reflected/dictionary
        /// keys for the given target; returning <c>null</c> keeps the default behavior. Reported keys must be
        /// resolvable through the wrapper's normal member/indexer access for their values to be readable.
        /// Defaults to returning <c>null</c>.
        /// </summary>
        internal static readonly ReportedPropertyKeysDelegate _defaultReportedPropertyKeys = static (_, _) => null;

        public ReportedPropertyKeysDelegate ObjectWrapperReportedPropertyKeys { get; set { ThrowIfReadOnly(); field = value; } } = _defaultReportedPropertyKeys;

        internal InteropOptions Clone()
        {
            var clone = (InteropOptions) MemberwiseClone();
            clone.ExtensionMethodTypes = ExtensionMethodTypes.Clone();
            clone.ObjectConverters = ObjectConverters.Clone();
            clone.ImmutableCrossingTypes = ImmutableCrossingTypes.Clone();
            clone.AllowedAssemblies = AllowedAssemblies.Clone();
            return clone;
        }
    }

    public sealed partial class ConstraintOptions
    {
        /// <summary>
        /// Registered constraint instances.
        /// </summary>
        /// <remarks>
        /// Every engine built from these options observes these exact instances. Constraints normally
        /// carry per-execution state (a statement counter, a deadline), so a shared instance is only
        /// safe when a single engine is built from the options and it is never used concurrently.
        /// To share one <see cref="Options"/> across several engines, register a factory instead
        /// (<see cref="OptionsExtensions.AddConstraint(Options, Func{Constraint})"/>) so each engine gets
        /// its own instance; the built-in constraint extension methods already do that.
        /// </remarks>
        public OptionsList<Constraint> Constraints { get; private set; } = new("Options.Constraints.Constraints");

        /// <summary>
        /// Registered constraint factories. Each engine built from these options invokes every factory
        /// exactly once while constructing, so the constraints — and the per-execution state they carry —
        /// belong to that engine alone.
        /// </summary>
        internal OptionsList<Func<Constraint>> ConstraintFactories { get; private set; } = new("Options.Constraints.Constraints");

        internal int? RequestedMaxStatements { get; set { ThrowIfReadOnly(); field = value; } }

        internal long? RequestedMemoryLimit { get; set { ThrowIfReadOnly(); field = value; } }

        internal TimeSpan? RequestedTimeoutInterval { get; set { ThrowIfReadOnly(); field = value; } }

        internal bool CancellationConstraintRequested { get; set { ThrowIfReadOnly(); field = value; } }

        internal bool OperationDeadlineConstraintRequested { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Maximum recursion depth allowed, defaults to -1 (no checks).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read once, while the engine is being constructed — the call stack decides from the same value
        /// whether to track depth at all, so the limit could not be raised later even if every check re-read it.
        /// </para>
        /// <para>
        /// A limit that cannot be reached is not a limit, so <see cref="int.MaxValue"/> arms nothing and gives
        /// exactly the engine <c>-1</c> gives — the depth tracking that feeds the check is not turned on
        /// either. Through 4.16.x the two differed: a saturated depth armed the tracking and then never fired,
        /// so it cost what enforcement costs and enforced nothing. The value still reads back as it was
        /// assigned, and <see cref="OptionsSecurityExtensions.ValidateSecurityConfiguration(Options, SecurityConfigurationPolicy)"/>
        /// still distinguishes the two spellings — a host that meant to set a limit is told which mistake it made.
        /// </para>
        /// </remarks>
        public int MaxRecursionDepth { get; set { ThrowIfReadOnly(); field = value; } } = -1;

        /// <summary>
        /// <see cref="MaxRecursionDepth"/> reduced to the two states the engine can be in: a non-negative depth
        /// to enforce, or <c>-1</c> for "do not track depth at all".
        /// </summary>
        internal int EffectiveMaxRecursionDepth => MaxRecursionDepth == int.MaxValue ? -1 : MaxRecursionDepth;

        /// <summary>
        /// Maximum recursion stack count, defaults to -1 (as-is dotnet stacktrace).
        /// </summary>
        /// <remarks>
        /// Chrome and V8 based engines (ClearScript) that can handle 13955.
        /// When set to a different value except -1, it can reduce slight performance/stack trace readability drawback. (after hitting the engine's own limit),
        /// When max stack size to be exceeded, Engine throws an exception <see cref="JavaScriptException" />.
        /// Read once, while the engine is being constructed.
        /// This lane is probed at the call expression only, so a recursion that reaches a function body by
        /// another route — <c>new</c>, an accessor, a coercion, a Proxy trap — still overflows the native
        /// stack; <see cref="StackOverflowGuard"/> is the one that covers those, and setting this property
        /// takes precedence over it.
        /// </remarks>
        public int MaxExecutionStackCount { get; set { ThrowIfReadOnly(); field = value; } } = StackGuard.Disabled;

        /// <summary>
        /// Whether every entry into an interpreted function probes the remaining native stack and throws a
        /// catchable <c>RangeError</c> when it runs low. Defaults to <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Leave it on for script you did not write. Without it an unbounded recursion ends the host process
        /// with a native stack overflow that no <c>catch</c> sees and nothing logs; with it the same script
        /// raises an error it can catch, and the engine survives.
        /// </para>
        /// <para>
        /// It measures the stack rather than counting calls, so it covers every route into a function body:
        /// <c>new</c>, an accessor, a <c>valueOf</c>/<c>toString</c> coercion, a Proxy trap, a callback a
        /// built-in invokes, and a host delegate that re-enters the engine.
        /// </para>
        /// <para>
        /// A proper tail call in a strict function is exempt, because it replaces the caller's frame instead
        /// of stacking one on top. Sloppy-mode recursion, a call out of tail position, and the non-call
        /// routes above all remain in scope.
        /// </para>
        /// <para>
        /// It is a backstop, not a policy: where <see cref="MaxRecursionDepth"/> is also set that limit fires
        /// first, and <see cref="MaxExecutionStackCount"/> takes precedence over this flag. Set it to
        /// <see langword="false"/> to recover the probe's cost when every script is trusted and independently
        /// bounded. Read once, while the engine is being constructed.
        /// </para>
        /// </remarks>
        public bool StackOverflowGuard { get; set { ThrowIfReadOnly(); field = value; } } = true;

        /// <summary>
        /// Maximum time a Regex is allowed to run, defaults to 10 seconds.
        /// </summary>
        /// <remarks>
        /// A limit that cannot be reached is not a limit, and .NET's <see cref="System.Text.RegularExpressions.Regex"/>
        /// can only enforce a timeout between one tick and <see cref="int.MaxValue"/> milliseconds. Anything
        /// outside that — a non-positive interval, <see cref="TimeSpan.MaxValue"/>, anything above the
        /// ceiling — therefore means untimed, on the .NET path and on Jint's own regex engine alike. Through
        /// 4.16.x it meant neither consistently: <see cref="TimeSpan.MaxValue"/> was silently untimed inside
        /// Jint's engine and an <see cref="ArgumentOutOfRangeException"/> out of the <c>Regex</c> constructor
        /// on the .NET one. The value reads back as it was assigned, and
        /// <see cref="OptionsSecurityExtensions.ValidateSecurityConfiguration(Options, SecurityConfigurationPolicy)"/>
        /// reports an untimed engine as an error.
        /// </remarks>
        public TimeSpan RegexTimeout { get; set { ThrowIfReadOnly(); field = value; } } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The largest interval .NET's <see cref="System.Text.RegularExpressions.Regex"/> accepts as a match
        /// timeout: <see cref="int.MaxValue"/> milliseconds, about 24.8 days.
        /// </summary>
        private static readonly TimeSpan _maximumEnforceableRegexTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        /// <summary>
        /// Reduces a configured regex timeout to the two states a regex engine can be in: an enforceable
        /// interval, or <see cref="System.Text.RegularExpressions.Regex.InfiniteMatchTimeout"/> for untimed.
        /// </summary>
        internal static TimeSpan NormalizeRegexTimeout(TimeSpan timeout)
            => timeout <= TimeSpan.Zero || timeout >= _maximumEnforceableRegexTimeout
                ? System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
                : timeout;

        /// <summary>
        /// Maximum time allowed for unwrapping a Promise and getting its resolved/rejected value.
        /// Defaults to 10 seconds.
        /// </summary>
        public TimeSpan PromiseTimeout { get; set { ThrowIfReadOnly(); field = value; } } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The maximum size for JavaScript array, defaults to <see cref="uint.MaxValue"/>.
        /// </summary>
        public uint MaxArraySize { get; set { ThrowIfReadOnly(); field = value; } } = uint.MaxValue;

        /// <summary>
        /// How many iterations is Atomics.pause allowed to instruct to wait using <see cref="System.Threading.Thread.SpinWait"/>, defaults to 10 000.
        /// </summary>
        public int MaxAtomicsPauseIterations { get; set { ThrowIfReadOnly(); field = value; } } = 10_000;

#if NET8_0_OR_GREATER
        /// <summary>
        /// The clock <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/>'s constraint measures against.
        /// Defaults to <see cref="TimeProvider.System"/>; a fake one makes a timeout test exact instead of a
        /// race against the machine it runs on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only <see cref="TimeProvider.GetTimestamp"/> and <see cref="TimeProvider.TimestampFrequency"/> are
        /// ever called. <see cref="TimeProvider.CreateTimer"/> deliberately is not: the constraint schedules
        /// nothing, it compares an inline deadline on the thread already running the script, which is what
        /// bounds detection by the timeout rather than by the thread pool.
        /// </para>
        /// <para>
        /// Read when the engine builds its constraints, so it may be set in any order relative to
        /// <see cref="ConstraintsOptionsExtensions.LimitExecutionTime"/>.
        /// Leaving it at <see cref="TimeProvider.System"/> costs exactly what it cost
        /// before this property existed — see <c>ConstraintClock</c> for the fold that makes that true.
        /// </para>
        /// <para>
        /// It governs the constraint the engine registers. <see cref="Constraints.OperationDeadlineConstraint"/>
        /// is created by the host rather than by the engine, so it takes its clock through its own
        /// constructor instead.
        /// </para>
        /// </remarks>
        public TimeProvider TimeProvider { get; set { ThrowIfReadOnly(); field = value; } } = TimeProvider.System;
#endif

        internal ConstraintOptions Clone()
        {
            var clone = (ConstraintOptions) MemberwiseClone();
            clone.Constraints = Constraints.Clone();
            clone.ConstraintFactories = ConstraintFactories.Clone();
            return clone;
        }
    }

    /// <summary>
    /// Resource limits applied before and during parsing. These limits also apply to code compiled by
    /// <c>eval</c>, function constructors, ShadowRealm evaluation, and module loaders.
    /// </summary>
    public sealed partial class ParsingOptions
    {
        /// <summary>
        /// The maximum number of UTF-16 code units the parser may receive.
        /// <see langword="null"/> (the default) means no limit.
        /// </summary>
        /// <remarks>
        /// This counts <see cref="string.Length"/>, not encoded bytes. Module loaders which decode bytes are
        /// checked after decoding. Generated source-offset padding and function-constructor wrappers also count.
        /// </remarks>
        public int? MaxSourceLength { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// The maximum number of AST nodes the parser may produce.
        /// <see langword="null"/> (the default) means no limit.
        /// </summary>
        public int? MaxNodeCount { get; set { ThrowIfReadOnly(); field = value; } }

        internal ParsingOptions Clone() => (ParsingOptions) MemberwiseClone();
    }

    /// <summary>
    /// Host related customization, still work in progress.
    /// </summary>
    public sealed partial class HostOptions
    {
        internal static readonly Func<Engine, Host> _defaultFactory = static _ => new Host();

        internal Func<Engine, Host> Factory { get; set { ThrowIfReadOnly(); field = value; } } = _defaultFactory;

        /// <summary>
        /// Whether calling 'eval' with custom code and function constructors taking function code as string is allowed.
        /// Defaults to true.
        /// </summary>
        /// <remarks>
        /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
        /// </remarks>
        public bool StringCompilationAllowed { get; set { ThrowIfReadOnly(); field = value; } } = true;

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

        public Func<Function, Node, string?> FunctionToStringHandler { get; set { ThrowIfReadOnly(); field = value; } } = _defaultFunctionToStringHandler;

        internal HostOptions Clone() => (HostOptions) MemberwiseClone();
    }

    /// <summary>
    /// Module related customization
    /// </summary>
    public sealed partial class ModuleOptions
    {
        /// <summary>
        /// Whether to register require function to engine which will delegate to module loader, defaults to false.
        /// </summary>
        public bool RegisterRequire { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// Module loader implementation, by default exception will be thrown if module loading is not enabled.
        /// </summary>
        public IModuleLoader ModuleLoader { get; set { ThrowIfReadOnly(); field = value; } } = FailFastModuleLoader.Instance;

        /// <summary>
        /// Maximum number of distinct module records an engine may register over its lifetime. Charged once per
        /// successfully registered module (cache key); cached, duplicate or coalesced loads do not recharge.
        /// Programmatic modules (<see cref="Engine.ModuleOperations.Add(string,string)"/>) participate.
        /// <see cref="int.MaxValue"/> means unlimited (the default). A non-positive finite value throws at
        /// engine construction.
        /// </summary>
        public int MaxModuleCount { get; set { ThrowIfReadOnly(); field = value; } } = int.MaxValue;

        /// <summary>
        /// Maximum cumulative UTF-8 encoded source bytes across all modules registered in an engine's lifetime.
        /// Charged once per distinct module at registration time. Modules whose original encoded source size is
        /// unknowable — exports-only builders, prepared modules, and custom <see cref="ModuleRecord"/>
        /// implementations — charge 0 bytes; raw byte modules use their exact byte length; string sources use their UTF-8 byte
        /// count. <see cref="long.MaxValue"/> means unlimited (the default). A non-positive finite value throws
        /// at engine construction.
        /// </summary>
        public long MaxTotalModuleSourceBytes { get; set { ThrowIfReadOnly(); field = value; } } = long.MaxValue;

        /// <summary>
        /// Maximum conservative import-chain depth in a module graph load. Root is depth 1. Strongly connected
        /// cycles are collapsed for traversal but contribute every module in the cycle to the depth; the longest
        /// path through that acyclic component graph is enforced. The result is deterministic regardless of DFS
        /// sibling order or asynchronous completion order and never lets a cycle increase depth without bound.
        /// A depth that exceeds this limit throws <see cref="ModuleGraphLimitException"/>.
        /// <see cref="int.MaxValue"/> means unlimited (the default). A non-positive finite value throws at
        /// engine construction.
        /// </summary>
        public int MaxModuleGraphDepth { get; set { ThrowIfReadOnly(); field = value; } } = int.MaxValue;

        /// <summary>
        /// Maximum number of module-loader <see cref="IModuleLoader.Resolve"/> calls per top-level import or
        /// load operation. Cached <c>[[LoadedModules]]</c> hits do not count. The budget resets for each
        /// <see cref="Engine.ModuleOperations.Import(string)"/>,
        /// <see cref="Engine.ModuleOperations.StartImport(string)"/>, or host call to
        /// <see cref="Jint.Runtime.Modules.ModuleRecord.LoadRequestedModules()"/>. Registration indexing does not consume hops, so pooled
        /// engines never fail from accumulated registrations. <see cref="int.MaxValue"/> means unlimited
        /// (the default). A non-positive finite value throws at engine construction.
        /// </summary>
        public int MaxModuleResolutionHops { get; set { ThrowIfReadOnly(); field = value; } } = int.MaxValue;

        /// <summary>
        /// An optional policy consulted after resolution but before a module is loaded or fetched. A denial
        /// throws <see cref="ModuleResolutionException"/> and follows existing sync/rejection behaviour.
        /// <c>null</c> means no policy (the default — everything the loader resolves is allowed).
        /// </summary>
        public IModuleLoadPolicy? LoadPolicy { get; set { ThrowIfReadOnly(); field = value; } }

        /// <summary>
        /// When <c>true</c>, module loading failures expose the original exception message to script code.
        /// Defaults to <c>false</c>, so paths, URLs, transport details and other host information are replaced
        /// with a generic message. The original exception remains available to the host through
        /// <see cref="JintException.TryGetClrException"/>.
        /// </summary>
        public bool ExposeDetailedLoadErrors { get; set { ThrowIfReadOnly(); field = value; } }

        internal ModuleOptions Clone() => (ModuleOptions) MemberwiseClone();
    }

    /// <summary>
    /// JSON.parse / JSON.stringify related customization
    /// </summary>
    public sealed partial class JsonOptions
    {
        /// <summary>
        /// The maximum depth allowed when parsing JSON files using "JSON.parse",
        /// defaults to 64.
        /// </summary>
        public int MaxParseDepth { get; set { ThrowIfReadOnly(); field = value; } } = 64;

        internal JsonOptions Clone() => (JsonOptions) MemberwiseClone();
    }

    /// <summary>
    /// Internationalization (Intl) API related customization.
    /// </summary>
    public sealed partial class IntlOptions
    {
        /// <summary>
        /// CLDR provider for locale data. Defaults to DefaultCldrProvider
        /// which provides basic English (en-US, en-GB) support.
        /// </summary>
        /// <remarks>
        /// Set this to a custom ICldrProvider implementation (e.g., ICU-based provider)
        /// to enable full locale support for the Intl API.
        /// </remarks>
        public ICldrProvider CldrProvider { get; set { ThrowIfReadOnly(); field = value; } } = DefaultCldrProvider.Instance;

        internal IntlOptions Clone() => (IntlOptions) MemberwiseClone();
    }

    /// <summary>
    /// Temporal API related customization.
    /// </summary>
    public sealed partial class TemporalOptions
    {
        /// <summary>
        /// Time zone provider for Temporal operations. Defaults to DefaultTimeZoneProvider
        /// which uses .NET TimeZoneInfo for basic IANA time zone support.
        /// </summary>
        /// <remarks>
        /// Set this to a custom ITimeZoneProvider implementation (e.g., using TimeZoneConverter or NodaTime)
        /// for full IANA time zone support and better Windows compatibility.
        /// </remarks>
        public ITimeZoneProvider TimeZoneProvider { get; set { ThrowIfReadOnly(); field = value; } } = DefaultTimeZoneProvider.Instance;

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
        public ICalendarProvider CalendarProvider { get; set { ThrowIfReadOnly(); field = value; } } = DefaultCalendarProvider.Instance;

        internal TemporalOptions Clone() => (TemporalOptions) MemberwiseClone();
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
    /// Wrapping tasks to promises
    /// </summary>
    /// <remarks>
    /// Bit 1 is deliberately not reused: it was <c>Generators</c>, removed in v5 once generators
    /// became unconditional. Keeping the layout stable keeps a persisted value meaning what it meant.
    /// </remarks>
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
