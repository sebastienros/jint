using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint;

/// <summary>
/// Compatibility layer to allow fluent syntax against options object.
/// </summary>
public static class OptionsExtensions
{
    /// <summary>
    /// Run the script in strict mode.
    /// </summary>
    public static Options Strict(this Options options, bool strict = true)
    {
        options.Strict = strict;
        return options;
    }

    /// <summary>
    /// Whether the engine's default parser retains function source text so that
    /// <c>Function.prototype.toString()</c> returns the original source. Defaults to <see langword="false"/>
    /// (returns a <c>function name() { [native code] }</c> placeholder and avoids keeping the source in memory).
    /// </summary>
    public static Options RetainFunctionSourceText(this Options options, bool retain = true)
    {
        options.RetainFunctionSourceText = retain;
        return options;
    }

    /// <summary>
    /// Selects the handling for script <code>debugger</code> statements.
    /// </summary>
    /// <remarks>
    /// The <c>debugger</c> statement can either be ignored (default) trigger debugging at CLR level (e.g. Visual Studio),
    /// or trigger a break in Jint's DebugHandler.
    /// </remarks>
    public static Options DebuggerStatementHandling(this Options options,
        DebuggerStatementHandling debuggerStatementHandling)
    {
        options.Debugger.StatementHandling = debuggerStatementHandling;
        return options;
    }

    /// <summary>
    /// Allow to run the script in debug mode.
    /// </summary>
    public static Options DebugMode(this Options options, bool debugMode = true)
    {
        options.Debugger.Enabled = debugMode;
        return options;
    }

    /// <summary>
    /// Set initial step mode.
    /// </summary>
    public static Options InitialStepMode(this Options options, StepMode initialStepMode = StepMode.None)
    {
        options.Debugger.InitialStepMode = initialStepMode;
        return options;
    }

    /// <summary>
    /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>
    /// </summary>
    public static Options AddObjectConverter<T>(this Options options) where T : IObjectConverter, new()
    {
        return AddObjectConverter(options, new T());
    }

    /// <summary>
    /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>
    /// </summary>
    public static Options AddObjectConverter(this Options options, IObjectConverter objectConverter)
    {
        options.Interop.ObjectConverters.Add(objectConverter);
        return options;
    }

    /// <summary>
    /// Adds a <see cref="IObjectConverter"/> instance to convert CLR types to <see cref="JsValue"/>, declaring
    /// the CLR types the converter can handle.
    /// </summary>
    /// <remarks>
    /// The declaration is a promise: the converter is still offered every value, but the engine is free to keep
    /// its compiled member-read fast lanes for members whose declared type cannot produce a value of any of the
    /// <paramref name="handledTypes"/> — such a member never reaches the converter. Declare a base type, an
    /// interface or <see cref="System.Enum"/> to cover a family of values, or use the overload without
    /// <paramref name="handledTypes"/> for a converter that inspects everything.
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <param name="objectConverter">The converter to register.</param>
    /// <param name="handledTypes">
    /// The CLR types the converter handles. Values assignable to any of them (including through a declared
    /// base type or interface) are considered convertible by this converter.
    /// </param>
    public static Options AddObjectConverter(this Options options, IObjectConverter objectConverter, params Type[] handledTypes)
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
    /// <see cref="AddObjectConverter(Options, Runtime.Interop.IObjectConverter, Type[])"/>: declare an
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
    /// Sets maximum allowed depth of recursion.
    /// </summary>
    /// <param name="options">Options to modify</param>
    /// <param name="maxRecursionDepth">
    /// The allowed depth.
    /// a) In case max depth is zero no recursion is allowed.
    /// b) In case max depth is equal to n it means that in one scope function can be called no more than n times.
    /// c) A negative depth (the default of <see cref="Options.ConstraintOptions.MaxRecursionDepth"/>)
    /// disables the check, and with it the call-stack depth tracking that feeds it.
    /// </param>
    /// <remarks>
    /// Proper tail calls do not consume native or interpreter call-stack frames, but repeated tail
    /// transfers are still included in this limit so an infinite strict tail-recursive function
    /// terminates with <see cref="RecursionDepthOverflowException"/>. A frame a tail call replaced goes
    /// on counting for as long as the trampoline that replaced it is running, so a recursion that
    /// leaves and re-enters that trampoline by a route which is not a tail call — a getter,
    /// <c>new</c>, a coercion, a Proxy trap, a host callback — is bounded like any other.
    /// <para>
    /// What the limit counts is occurrences of one function <em>definition</em> on the call stack, per
    /// point (b) above, and not the depth of the stack. So a recursion whose every level is a function
    /// created for that level — through <c>eval</c>, <c>new Function</c>, or a host re-running a
    /// script — repeats no definition and is outside this limit however deep it goes. Nothing before
    /// or since has covered that shape; a host running script it did not write wants
    /// <see cref="Options.ConstraintOptions.StackOverflowGuard"/>, which measures the remaining native
    /// stack instead of counting calls.
    /// </para>
    /// <para>
    /// Unlike the constraint helpers in <see cref="ConstraintsOptionsExtensions"/>, this one does not
    /// treat a saturated value as "no limit": any non-negative depth — <see cref="int.MaxValue"/>
    /// included — turns depth tracking on, so a limit chosen to be unreachable still costs what
    /// enforcement costs while never failing. Pass a negative depth to mean unlimited.
    /// </para>
    /// </remarks>
    /// <returns>Options instance for fluent syntax</returns>
    public static Options LimitRecursion(this Options options, int maxRecursionDepth = 0)
    {
        options.Constraints.MaxRecursionDepth = maxRecursionDepth;
        return options;
    }

    public static Options Culture(this Options options, CultureInfo cultureInfo)
    {
        options.Culture = cultureInfo;
        return options;
    }

    public static Options LocalTimeZone(this Options options, TimeZoneInfo timeZoneInfo)
    {
        options.TimeZone = timeZoneInfo;
        return options;
    }

    /// <summary>
    /// Disables calling 'eval' with custom code and function constructors taking function code as string.
    /// By default eval and function code parsing is allowed.
    /// </summary>
    public static Options DisableStringCompilation(this Options options, bool disable = true)
    {
        options.Host.StringCompilationAllowed = !disable;
        return options;
    }

    public static Options AddExtensionMethods(this Options options, params Type[] types)
    {
        options.Interop.ExtensionMethodTypes.AddRange(types);
        return options;
    }

    /// <summary>
    /// If no known type could be guessed, objects are normally wrapped as an
    /// ObjectInstance using class ObjectWrapper. This function can be used to
    /// register a handler for a customized handling.
    /// </summary>
    public static Options SetWrapObjectHandler(this Options options, Options.WrapObjectDelegate wrapObjectHandler)
    {
        options.Interop.WrapObjectHandler = wrapObjectHandler;
        return options;
    }

    /// <summary>
    /// Sets the handler used to build stack traces. This is useful if the code currently
    /// running was transpiled (eg. TypeScript) and the source map of original code is available.
    /// </summary>
    public static Options SetBuildCallStackHandler(this Options options, Options.BuildCallStackDelegate buildCallStackHandler)
    {
        options.Interop.BuildCallStackHandler = buildCallStackHandler;
        return options;
    }

    /// <summary>
    /// Sets the type converter to use.
    /// </summary>
    public static Options SetTypeConverter(this Options options, Func<Engine, ITypeConverter> typeConverterFactory)
    {
        options._configurations.Add(engine => engine.TypeConverter = typeConverterFactory(engine));
        return options;
    }

    /// <summary>
    /// Registers a delegate that is called when CLR members are invoked. This allows
    /// to change what values are returned for specific CLR objects, or if any value
    /// is returned at all.
    /// </summary>
    /// <param name="options">Options to modify</param>
    /// <param name="accessor">
    /// The delegate to invoke for each CLR member. If the delegate
    /// returns <c>null</c>, the standard evaluation is performed.
    /// </param>
    public static Options SetMemberAccessor(this Options options, Options.MemberAccessorDelegate accessor)
    {
        options.Interop.MemberAccessor = accessor;
        return options;
    }

    /// <summary>
    /// Allows scripts to call CLR types directly like <example>System.IO.File</example>
    /// </summary>
    public static Options AllowClr(this Options options, params Assembly[] assemblies)
    {
        options.Interop.Enabled = true;
        options.Interop.AllowedAssemblies.AddRange(assemblies);
        options.Interop.AllowedAssemblies = options.Interop.AllowedAssemblies.Distinct().ToList();
        return options;
    }

    public static Options AllowClrWrite(this Options options, bool allow = true)
    {
        options.Interop.AllowWrite = allow;
        return options;
    }

    public static Options AllowOperatorOverloading(this Options options, bool allow = true)
    {
        options.Interop.AllowOperatorOverloading = allow;
        return options;
    }

    /// <summary>
    /// Exceptions thrown from CLR code are converted to JavaScript errors and
    /// can be used in at try/catch statement. By default these exceptions are bubbled
    /// to the CLR host and interrupt the script execution.
    /// </summary>
    public static Options CatchClrExceptions(this Options options)
    {
        CatchClrExceptions(options, _ => true);
        return options;
    }

    /// <summary>
    /// Exceptions that thrown from CLR code are converted to JavaScript errors and
    /// can be used in at try/catch statement. By default these exceptions are bubbled
    /// to the CLR host and interrupt the script execution.
    /// </summary>
    public static Options CatchClrExceptions(this Options options, Options.ExceptionHandlerDelegate handler)
    {
        options.Interop.ExceptionHandler = handler;
        return options;
    }

    /// <summary>
    /// Chains the CLR exception behind a caught interop error into the <see cref="Exception.InnerException"/> of
    /// the <see cref="JavaScriptException"/> the host catches, so that logging which walks the inner-exception
    /// chain surfaces it. Off by default, because it puts host .NET stack traces into whatever consumes the
    /// exception's string form. Only meaningful together with <see cref="CatchClrExceptions(Options)"/>.
    /// </summary>
    public static Options ChainClrExceptions(this Options options, bool chain = true)
    {
        options.Interop.ChainClrExceptionAsInnerException = chain;
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
    /// Sets a decorator function that is called after a JavaScript error object is created from a CLR exception.
    /// The decorator can add custom properties, modify the error message, or enrich the error with additional context.
    /// It is called when <see cref="CatchClrExceptions(Options)"/> converts a host exception and when a module
    /// loader exception becomes an import error.
    /// </summary>
    /// <param name="options">The engine options.</param>
    /// <param name="decorator">A function that receives the engine, the created error object, and the original CLR exception.</param>
    /// <returns>The options instance for fluent configuration.</returns>
    public static Options DecorateClrExceptionErrors(this Options options, Options.ClrExceptionErrorDecoratorDelegate decorator)
    {
        options.Interop.ClrExceptionErrorDecorator = decorator;
        return options;
    }

    /// <summary>
    /// Sets a decorator function that is called after the JavaScript error object for a failed CLR method
    /// or constructor resolution is created, before it is thrown into the script. The decorator can overwrite
    /// the script-visible message or add custom properties such as an error code.
    /// </summary>
    /// <param name="options">The engine options.</param>
    /// <param name="decorator">A function that receives the engine, the created error object, and the structured resolution information.</param>
    /// <returns>The options instance for fluent configuration.</returns>
    public static Options DecorateClrResolutionErrors(this Options options, Options.ClrResolutionErrorDecoratorDelegate decorator)
    {
        options.Interop.ClrResolutionErrorDecorator = decorator;
        return options;
    }

    /// <summary>
    /// Registers a single constraint instance.
    /// </summary>
    /// <remarks>
    /// The instance is shared by every engine built from these options. Constraints normally carry
    /// per-execution state, so this overload is only safe for options that build exactly one engine.
    /// Use <see cref="Constraint(Options, Func{Constraint})"/> when the same options are reused for
    /// several engines.
    /// </remarks>
    public static Options Constraint(this Options options, Constraint constraint)
    {
        if (constraint != null)
        {
            options.Constraints.Constraints.Add(constraint);
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
    /// side effect beyond creating the constraint: <see cref="WithoutConstraint"/> invokes it once more to
    /// classify the registration.
    /// </param>
    /// <returns>The options instance for fluent configuration.</returns>
    public static Options Constraint(this Options options, Func<Constraint> constraintFactory)
    {
        if (constraintFactory != null)
        {
            options.Constraints.ConstraintFactories.Add(constraintFactory);
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
    public static Options WithoutConstraint(this Options options, Predicate<Constraint> predicate)
    {
        options.Constraints.Constraints.RemoveAll(predicate);
        options.Constraints.ConstraintFactories.RemoveAll(factory =>
        {
            var probe = factory();
            return probe is not null && predicate(probe);
        });
        return options;
    }

    public static Options RegexTimeoutInterval(this Options options, TimeSpan regexTimeoutInterval)
    {
        options.Constraints.RegexTimeout = regexTimeoutInterval;
        return options;
    }


    public static Options MaxArraySize(this Options options, uint maxSize)
    {
        options.Constraints.MaxArraySize = maxSize;
        return options;
    }

    public static Options MaxJsonParseDepth(this Options options, int maxDepth)
    {
        options.Json.MaxParseDepth = maxDepth;
        return options;
    }

    /// <summary>
    /// Registers a reference resolver that is consulted in every situation
    /// (<see cref="ReferenceResolverInterests.All"/>).
    /// </summary>
    public static Options SetReferencesResolver(this Options options, IReferenceResolver resolver)
    {
        return SetReferencesResolver(options, resolver, ReferenceResolverInterests.All);
    }

    /// <summary>
    /// Registers a reference resolver together with the situations it wants to be consulted for. The engine
    /// does not call the resolver for anything outside <paramref name="interests"/>, and keeps the
    /// interpreter fast paths those situations would otherwise disable — see
    /// <see cref="ReferenceResolverInterests"/>.
    /// </summary>
    public static Options SetReferencesResolver(this Options options, IReferenceResolver resolver, ReferenceResolverInterests interests)
    {
        options.ReferenceResolver = resolver;
        options.ReferenceResolverInterests = interests;
        return options;
    }

    public static Options SetTypeResolver(this Options options, TypeResolver resolver)
    {
        options.Interop.TypeResolver = resolver;
        return options;
    }

    /// <summary>
    /// When enabled, JavaScript prototype methods take precedence over CLR methods of the same name on wrapped CLR objects
    /// whose prototype is not the default Object prototype (e.g. <c>Array.prototype</c> attached to wrapped <see cref="System.Collections.Generic.IList{T}"/>).
    /// Avoids semantic mismatches such as <c>List&lt;T&gt;.Reverse()</c> returning <c>void</c> while
    /// <c>Array.prototype.reverse</c> returns the array.
    /// </summary>
    public static Options PreferJsPrototypeMethods(this Options options, bool prefer = true)
    {
        options.Interop.PreferJsPrototypeMethods = prefer;
        return options;
    }

    /// <summary>
    /// Registers a global whose value is produced the first time script reads it, instead of when the
    /// engine is created. Hosts that install a large fixed set of globals — of which a given script
    /// typically touches a handful — pay only for the ones actually used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The property is installed eagerly, so existence checks and enumeration — <c>in</c>,
    /// <c>hasOwnProperty</c>, <c>Object.keys(globalThis)</c>, <c>Object.getOwnPropertyNames</c> — see it
    /// from the start without materializing anything. Only its value is left unresolved:
    /// <paramref name="valueFactory"/> runs at most once per engine, on the first read of that value, and
    /// the produced value is stored in the descriptor so subsequent reads are ordinary property reads.
    /// <c>typeof</c> counts as a read, since it has to inspect the value to name its type.
    /// </para>
    /// <para>
    /// A script that <b>deletes</b> the global before ever reading it (<c>delete globalThis.name</c>,
    /// requires <see cref="PropertyFlag.Configurable"/>) removes the descriptor outright, and the factory
    /// never runs. A script that <b>overwrites or redefines</b> it first does still run the factory once
    /// and then discard the result: <c>[[Set]]</c> on the global object funnels into
    /// <c>[[DefineOwnProperty]]</c>, whose ValidateAndApplyPropertyDescriptor step reads the current
    /// value before replacing it. The end state is correct in every case — the script's value wins — the
    /// laziness is simply not preserved through that particular sequence.
    /// </para>
    /// <para>
    /// Registration is recorded on the <see cref="Options"/> instance and applied per engine, so a single
    /// <see cref="Options"/> object may be shared by any number of engines: each gets its own descriptor
    /// and its own invocation of <paramref name="valueFactory"/>, with the engine being built passed in.
    /// The factory must not return <see langword="null"/>; <see cref="JsValue.Undefined"/> is substituted
    /// if it does, so that a null return cannot silently turn into a factory that re-runs on every read.
    /// </para>
    /// <para>
    /// <b>Sharing an <see cref="Options"/> instance is supported, not required.</b> Because the factory
    /// receives only the <see cref="Engine"/>, a host whose values depend on per-request or per-scope state
    /// (a scoped <c>IServiceProvider</c>, a workflow context) cannot express that through a process-wide
    /// <see cref="Options"/>. Constructing a fresh <see cref="Options"/> per scope or per evaluation and
    /// letting the factories close over that scope is a supported and cheap pattern — an
    /// <see cref="Options"/> object is a plain configuration record, and the caches that make repeated
    /// engine construction cheap (resolved CLR members on the <see cref="TypeResolver"/>, delegate
    /// metadata, compiled invokers) are keyed process-wide rather than on the <see cref="Options"/>
    /// instance. Share one instance when the configuration is genuinely global; build one per scope when
    /// it is not.
    /// </para>
    /// <para>
    /// <b>A restore re-arms an unread global, and this is a contract.</b> If
    /// <see cref="Engine.AdvancedOperations.CaptureGlobalSnapshot"/> is taken while this global has not yet
    /// been read, <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> returns it to that
    /// unmaterialized state, and <paramref name="valueFactory"/> <b>runs again</b> on the next read. It is
    /// not merely permitted to — a host pooling engines across requests depends on it, since the value the
    /// factory produced belongs to the request that read it. Capture before the first evaluation and the
    /// globals of every later one are rebuilt rather than inherited.
    /// <para>
    /// The failure this rules out is silent, which is why it is stated rather than left to the
    /// implementation: were the descriptor reinstated with its materialized value intact, a reused engine
    /// would serve the next request a value closed over the previous one's state, with nothing to observe
    /// but the wrong answer.
    /// </para>
    /// <para>
    /// The guarantee covers a global whose factory had <em>not</em> run at capture time. One already read by
    /// then is part of the captured surface and is restored to that value, which is the same rule seen from
    /// the other side.
    /// </para>
    /// </para>
    /// <para>
    /// <b>Flags differ from <see cref="Engine.SetValue(string, Delegate)"/>.</b> The default here matches
    /// <see cref="Engine.SetValue(string, JsValue)"/> — configurable, enumerable and writable — whereas
    /// registering a delegate global installs it as <see cref="PropertyFlag.NonEnumerable"/> (configurable
    /// and writable, but hidden from <c>Object.keys(globalThis)</c> and <c>for...in</c>). A host converting
    /// delegate registrations to lazy ones must pass <see cref="PropertyFlag.NonEnumerable"/> explicitly to
    /// keep global enumeration looking the same.
    /// </para>
    /// </remarks>
    /// <example>
    /// Deferring an expensive host object — here a CLR type projection, which otherwise builds an engine-affine
    /// <c>TypeReference</c> (and resolves its members) at engine-construction time whether or not the script
    /// mentions it:
    /// <code>
    /// options.AddLazyGlobal("DateTime", static engine => TypeReference.CreateTypeReference&lt;DateTime&gt;(engine));
    ///
    /// // A delegate global, deferred and given a real name: DelegateWrapper always reports
    /// // fn.name === "delegate", while a ClrFunction carries the name it was constructed with.
    /// options.AddLazyGlobal(
    ///     "log",
    ///     engine => new ClrFunction(engine, "log", (_, args) => { Console.WriteLine(args.At(0)); return JsValue.Undefined; }),
    ///     PropertyFlag.NonEnumerable);
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="name">The global property name.</param>
    /// <param name="valueFactory">
    /// Produces the value for a given engine. Invoked lazily, so it may use anything the engine exposes
    /// after construction — unlike <see cref="UseHostFactory{T}"/> callbacks, which observe a half-built engine.
    /// </param>
    /// <param name="flags">
    /// Property attributes; defaults to the configurable/enumerable/writable combination that
    /// <see cref="Engine.SetValue(string, JsValue)"/> produces — <b>not</b> the
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

        options._configurations.Add(engine => engine.Realm.GlobalObject.SetProperty(
            name,
            new LazyPropertyDescriptor<Engine>(engine, valueFactory, flags)));

        return options;
    }

    /// <summary>
    /// Registers some custom logic to apply on an <see cref="Engine"/> instance when the options
    /// are loaded.
    /// </summary>
    /// <param name="options">Options to modify</param>
    /// <param name="configuration">The action to register.</param>
    public static Options Configure(this Options options, Action<Engine> configuration)
    {
        options._configurations.Add(configuration);
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
        options.Host.Factory = factory;
        return options;
    }

    /// <summary>
    /// Enables module loading in the engine via the 'require' function. By default there's no sand-boxing and
    /// you need to trust the script loading the modules not doing bad things.
    /// </summary>
    public static Options EnableModules(this Options options, string basePath, bool restrictToBasePath = true)
    {
        return EnableModules(options, new DefaultModuleLoader(basePath, restrictToBasePath));
    }

    /// <summary>
    /// Enables module loading using a custom loader implementation.
    /// </summary>
    public static Options EnableModules(this Options options, IModuleLoader moduleLoader)
    {
        options.Modules.ModuleLoader = moduleLoader;
        return options;
    }
}
