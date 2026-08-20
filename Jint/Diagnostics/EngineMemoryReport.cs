namespace Jint.Diagnostics;

/// <summary>
/// A point-in-time count of what one <see cref="Engine"/> is holding on to, as reported by
/// <see cref="Engine.AdvancedOperations.GetMemoryReport(int)"/>. Written for the host that pools engines and
/// wants to know whether a pooled engine's retained set is growing between requests.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a diagnostic, and it is not part of Jint's compatibility contract.</b> Exactly which internal
/// collections are counted, and how a count is derived, may be refined in any release; this type and the
/// nested report types may gain members. Neither is a breaking change, and neither will be treated as one —
/// which is why every constructor here is internal: a host reads the properties it knows about and is
/// unaffected when another appears. Use it for audits, assertions, logging and regression tests; do not
/// branch on it in production code, and do not treat a number as a promise about a future release. The same
/// posture as <see cref="ObjectRepresentation"/> and <see cref="InteropConversionDiagnostics"/>.
/// </para>
/// <para>
/// <b>Everything here is a count, never a byte figure.</b> Jint does not track the allocation size of
/// anything it builds, and this report will not invent one: a "how many objects / entries / queued jobs"
/// answer is exact and checkable, whereas a byte total would have to guess at object headers, padding and
/// shared sub-objects. Pair the counts with a real memory profiler when the question is bytes.
/// </para>
/// <para>
/// <b>Nothing here is materialized by asking.</b> The walk reads values that already exist: it never invokes
/// a getter, never runs a lazy property factory, and never creates a built-in's function object. So a
/// built-in nobody has touched is reported as untouched, and calling this twice in a row on an idle engine
/// produces two equal reports — which, since these are records, is directly assertable.
/// </para>
/// </remarks>
public sealed record EngineMemoryReport
{
    internal EngineMemoryReport(
        int globalPropertyCount,
        int materializedGlobalPropertyCount,
        int lexicalGlobalBindingCount,
        int eventLoopQueueDepth,
        int pendingTimerCount,
        int pendingAtomicsWaiterCount,
        int registeredModuleCount,
        int pendingModuleLoadCount,
        HandlerTreeCacheReport handlerTreeCaches,
        InteropCacheReport interopCaches,
        PoolReport pools,
        ObjectCensusReport objectCensus)
    {
        GlobalPropertyCount = globalPropertyCount;
        MaterializedGlobalPropertyCount = materializedGlobalPropertyCount;
        LexicalGlobalBindingCount = lexicalGlobalBindingCount;
        EventLoopQueueDepth = eventLoopQueueDepth;
        PendingTimerCount = pendingTimerCount;
        PendingAtomicsWaiterCount = pendingAtomicsWaiterCount;
        RegisteredModuleCount = registeredModuleCount;
        PendingModuleLoadCount = pendingModuleLoadCount;
        HandlerTreeCaches = handlerTreeCaches;
        InteropCaches = interopCaches;
        Pools = pools;
        ObjectCensus = objectCensus;
    }

    /// <summary>
    /// Own properties of the principal realm's global object — every name and symbol <c>globalThis</c> itself
    /// carries, whether or not its value has been produced yet. On a fresh engine this is the built-in global
    /// surface (the intrinsic references, <c>NaN</c>, <c>Infinity</c>, <c>undefined</c>, <c>globalThis</c> and
    /// the global functions); every <see cref="Engine.SetValue(string, Native.JsValue)"/>, every
    /// <c>var</c>/<c>function</c> declaration a script evaluated at top level, and every
    /// <see cref="Engine.AdvancedOperations.AddLazyGlobal(string, Func{Engine, Native.JsValue}, Runtime.Descriptors.PropertyFlag)"/>
    /// adds one.
    /// </summary>
    /// <remarks>
    /// It counts <em>own</em> properties, so names a script can reach through the global object's prototype
    /// chain are not included, and neither are <c>let</c>/<c>const</c>/<c>class</c> declarations, which are
    /// bindings of the global environment record rather than properties — see
    /// <see cref="LexicalGlobalBindingCount"/>.
    /// </remarks>
    public int GlobalPropertyCount { get; }

    /// <summary>
    /// How many of the <see cref="GlobalPropertyCount"/> own properties currently hold a value that has
    /// actually been produced. The gap between the two is the part of the global surface this engine has
    /// never touched: a built-in whose constructor object has not been created, a lazily registered global
    /// whose factory has not run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading <c>Array</c> from script moves one property from the unmaterialized side to this side, and
    /// nothing moves it back short of a <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot(GlobalSnapshot)"/> to a
    /// snapshot taken before the read. That makes the gap the cheapest available answer to "how much of the
    /// language does this pooled engine actually pay for".
    /// </para>
    /// <para>
    /// Accessor properties are deliberately excluded: their value is whatever their getter returns, and this
    /// report never invokes one. A global installed as an accessor therefore counts towards
    /// <see cref="GlobalPropertyCount"/> and never towards this.
    /// </para>
    /// </remarks>
    public int MaterializedGlobalPropertyCount { get; }

    /// <summary>
    /// Bindings of the principal realm's global <em>environment record</em> — the <c>let</c>, <c>const</c> and
    /// <c>class</c> declarations evaluated at top level, which the specification keeps beside the global
    /// object rather than on it. They are the half of the global surface
    /// <see cref="GlobalPropertyCount"/> cannot see, and the half a
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot(GlobalSnapshot)"/> exists to clear.
    /// </summary>
    public int LexicalGlobalBindingCount { get; }

    /// <summary>
    /// Jobs sitting in the event-loop queue right now: promise reactions, settle callbacks a background
    /// thread has enqueued, timer callbacks already promoted, asynchronous module-load completions. Zero on
    /// an engine that has finished draining, which is the state a host normally observes it in, since every
    /// public entry point drains before returning.
    /// </summary>
    /// <remarks>
    /// This is the one count that another thread can change while it is being read — a
    /// <see cref="System.Threading.Tasks.Task"/> completing on the thread pool enqueues its settle from
    /// there. Treat it as a sample, not as a value that stays true.
    /// </remarks>
    public int EventLoopQueueDepth { get; }

    /// <summary>
    /// Registered <c>setTimeout</c> / <c>setInterval</c> timers that have not fired and have not been
    /// cleared. Each one retains its callback function, the arguments it will be handed, and — through the
    /// callback's closure — whatever that closure captured.
    /// </summary>
    /// <remarks>
    /// Always zero where the timer globals do not exist: on target frameworks below .NET 8, which carry no
    /// web APIs at all, and on any engine that did not opt into <c>WebApiFeatures.Timers</c>. An interval is
    /// one timer however many times it has already fired.
    /// </remarks>
    public int PendingTimerCount { get; }

    /// <summary>
    /// Pending finite-timeout <c>Atomics.waitAsync</c> waits: the ones the event-loop pump is still watching
    /// the clock for. Each retains its promise capability and the realm it was registered in.
    /// </summary>
    /// <remarks>
    /// A wait asking for no timeout is never registered with the pump — only <c>Atomics.notify</c> can end
    /// one — so it is not counted here. Waits already settled by a notify but not yet swept out of the
    /// deadline heap are not counted either.
    /// </remarks>
    public int PendingAtomicsWaiterCount { get; }

    /// <summary>
    /// Module records this engine has loaded, keyed as the module map keys them — by resolved specifier and
    /// import attributes. Each retains its parsed source, its module environment and every value it exported.
    /// </summary>
    /// <remarks>
    /// The module registry is deliberately <em>not</em> reverted by
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot(GlobalSnapshot)"/>, so on a pooled engine this count only
    /// ever grows. A host that streams distinct module specifiers through one long-lived engine is the case
    /// worth watching here.
    /// </remarks>
    public int RegisteredModuleCount { get; }

    /// <summary>
    /// Module loads an asynchronous loader has started and not yet finished. Non-zero only between a
    /// <c>StartImport</c>/<c>ImportAsync</c> and the load settling, so a steady non-zero reading on an idle
    /// engine means a loader that never answered.
    /// </summary>
    public int PendingModuleLoadCount { get; }

    /// <summary>
    /// The interpreter handler trees this engine has cached across evaluations. See
    /// <see cref="HandlerTreeCacheReport"/> — these are the roots from which every warmed call site's retained
    /// receiver and callee hang.
    /// </summary>
    public HandlerTreeCacheReport HandlerTreeCaches { get; }

    /// <summary>
    /// The per-engine CLR interop caches. See <see cref="InteropCacheReport"/>.
    /// </summary>
    public InteropCacheReport InteropCaches { get; }

    /// <summary>
    /// What the engine's object pools are holding between operations. See <see cref="PoolReport"/>.
    /// </summary>
    public PoolReport Pools { get; }

    /// <summary>
    /// A bounded breadth-first census of the object graph reachable from <c>globalThis</c>. See
    /// <see cref="ObjectCensusReport"/>, which documents exactly which edges are followed and which are not.
    /// </summary>
    public ObjectCensusReport ObjectCensus { get; }
}

/// <summary>
/// How many entries each of the engine's interpreter handler-tree caches holds, as reported inside an
/// <see cref="EngineMemoryReport"/>.
/// </summary>
/// <remarks>
/// <para>
/// These caches exist so that re-running a script on the same engine reuses its warm per-node inline caches
/// instead of rebuilding the tree. They are engine-owned and never shared through the AST, so they die with
/// the engine — but on a pooled engine they live as long as the engine does, and they are the roots of the
/// retention a host actually asks about: a warmed member-read site keeps a strong reference to the last
/// receiver it served, and a warmed call site keeps its last callee (and through a closure callee, the
/// environment that closure captured). Nothing clears them; a host whose receivers wrap large native state
/// that must not outlive a request drops the engine rather than pooling it.
/// </para>
/// <para>
/// <b>Why there is no per-call-site breakdown here.</b> The counts below are of cache <em>entries</em> —
/// tree roots — not of warmed sites inside those trees. The interpreter's node classes carry no child
/// enumeration: every node keeps its operands in private fields of its own shape, so enumerating the sites in
/// a tree would mean either a reflection walker (which would break silently whenever a node gained a field)
/// or a new traversal hook on all sixty-odd node types (which would break silently whenever someone forgot to
/// implement it on a new one). Both trade a real guarantee — every number in this report is exact — for a
/// number that merely looks more precise. If a clean internal traversal is ever added for another reason, a
/// per-site breakdown becomes a small addition on top of it.
/// </para>
/// </remarks>
public sealed record HandlerTreeCacheReport
{
    internal HandlerTreeCacheReport(int functionDefinitions, int scriptStatementLists, int evaluatedScripts)
    {
        FunctionDefinitions = functionDefinitions;
        ScriptStatementLists = scriptStatementLists;
        EvaluatedScripts = evaluatedScripts;
    }

    /// <summary>
    /// Cached interpreter function definitions, keyed on the AST node: hoisted declarations, class methods,
    /// class field initializers and static blocks. Each owns the lazily built body handler tree, and through
    /// it that body's per-node inline caches.
    /// </summary>
    /// <remarks>
    /// The cache resets itself wholesale once it reaches 2048 entries, which bounds an engine that is fed an
    /// endless stream of distinct sources; a reading at or just above that ceiling is that backstop working,
    /// not a leak.
    /// </remarks>
    public int FunctionDefinitions { get; }

    /// <summary>
    /// Cached top-level (<c>Program</c>) statement handler trees, keyed on the script's AST.
    /// </summary>
    /// <remarks>
    /// Populated only on <em>re-evaluation</em>: the first run of a given script on a given engine builds a
    /// tree and caches nothing, so a host that builds a fresh engine per operation reads zero here forever,
    /// by design. Like <see cref="FunctionDefinitions"/> it resets wholesale at 2048 entries.
    /// </remarks>
    public int ScriptStatementLists { get; }

    /// <summary>
    /// Distinct scripts this engine has run global declaration instantiation for. This is what decides
    /// whether the next run of a script counts as a re-evaluation, so it reaches one for a script before
    /// <see cref="ScriptStatementLists"/> does.
    /// </summary>
    public int EvaluatedScripts { get; }
}

/// <summary>
/// What the engine's own CLR interop caches hold, as reported inside an <see cref="EngineMemoryReport"/>.
/// </summary>
/// <remarks>
/// Only the caches that belong to <em>this engine</em> are counted. The resolved reflection accessors live on
/// the <c>TypeResolver</c>, which engines share — by default a single process-wide one — so they are not an
/// engine's retention and are deliberately absent here. The wrapper identity cache is a
/// <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey, TValue}"/>, which exposes no count
/// and which by construction retains nothing whose CLR target is otherwise dead; it is absent for that
/// reason rather than by oversight.
/// </remarks>
public sealed record InteropCacheReport
{
    internal InteropCacheReport(int typeNameCacheCount, int typeReferenceCount)
    {
        TypeNameCacheCount = typeNameCacheCount;
        TypeReferenceCount = typeReferenceCount;
    }

    /// <summary>
    /// Entries in this engine's CLR type-name lookup cache — the results, including the misses, of resolving
    /// a name through <c>System</c> or an assembly registered on the interop options. Each successful entry
    /// keeps a <see cref="Type"/> alive, and through it the assembly that defines it.
    /// </summary>
    public int TypeNameCacheCount { get; }

    /// <summary>
    /// CLR types this engine has built a script-visible <c>TypeReference</c> for — every type handed to
    /// <see cref="Engine.SetValue(string, Type)"/>, and every one a script named through an accessible
    /// namespace. A <c>TypeReference</c> is a JavaScript object owned by this engine, so unlike the shared
    /// accessor cache it is per-engine retention, and it keeps its <see cref="Type"/> — and the assembly
    /// behind it — alive for as long as the engine lives.
    /// </summary>
    public int TypeReferenceCount { get; }
}

/// <summary>
/// How many instances each of the engine's object pools is currently holding, as reported inside an
/// <see cref="EngineMemoryReport"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these pools has a small fixed capacity fixed at construction, so these counts are bounded by
/// construction and can never be the cause of unbounded growth. They are reported because "the engine is
/// idle and still holds this much" is a question a pooling host asks, and because a pool sitting empty after
/// a workload is a hint that something rents without returning.
/// </para>
/// <para>
/// The pooled instances are cleared on return — a returned argument array has its elements nulled out, a
/// returned <c>arguments</c> object is reset — so a full pool retains the container objects and not the
/// values that passed through them.
/// </para>
/// </remarks>
public sealed record PoolReport
{
    internal PoolReport(
        int pooledReferences,
        int pooledArgumentsObjects,
        int pooledObjectTraverseStacks,
        int pooledJsValueArrays,
        int pooledJsValueArraySlots)
    {
        PooledReferences = pooledReferences;
        PooledArgumentsObjects = pooledArgumentsObjects;
        PooledObjectTraverseStacks = pooledObjectTraverseStacks;
        PooledJsValueArrays = pooledJsValueArrays;
        PooledJsValueArraySlots = pooledJsValueArraySlots;
    }

    /// <summary>Reference objects held by the reference pool, at most its fixed capacity.</summary>
    public int PooledReferences { get; }

    /// <summary>
    /// <c>arguments</c> exotic objects held by the arguments pool, at most its fixed capacity.
    /// </summary>
    public int PooledArgumentsObjects { get; }

    /// <summary>
    /// Object-traversal stacks held by their pool — the scratch structure that converting a JavaScript
    /// object graph to CLR objects rents to keep track of where it has been.
    /// </summary>
    public int PooledObjectTraverseStacks { get; }

    /// <summary>
    /// Small argument arrays held by the argument-array pool. The pool covers lengths one to four and keeps a
    /// separate bucket for each, so this is the total across buckets.
    /// </summary>
    public int PooledJsValueArrays { get; }

    /// <summary>
    /// Total element slots across those arrays — the sum of their lengths, and the closest thing to a size
    /// this report offers, because it is exactly countable. Multiply by the platform's reference size for a
    /// lower bound on the bytes they hold; the report does not do that multiplication itself, because the
    /// array headers it would have to add are not something Jint knows.
    /// </summary>
    public int PooledJsValueArraySlots { get; }
}

/// <summary>
/// A bounded breadth-first census of the JavaScript objects reachable from the principal realm's global
/// object, as reported inside an <see cref="EngineMemoryReport"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it follows.</b> The already-stored values of an object's own properties, plus the elements of an
/// array. That is the graph the engine is holding through its globals, and it is walked without asking any
/// code to produce anything: an accessor property is counted as a property and its getter is never invoked, a
/// lazily installed property whose factory has not run contributes nothing, and a built-in whose function
/// object does not exist yet is not created in order to be counted. So the census cannot itself inflate the
/// figures beside it — running it twice on an idle engine produces the same numbers.
/// </para>
/// <para>
/// <b>What it does not follow</b>, and therefore under-counts: an object's <c>[[Prototype]]</c> link (the
/// built-in prototypes are still reached, through their constructors' <c>prototype</c> properties); the
/// entries of a <c>Map</c>, <c>Set</c>, <c>WeakMap</c> or <c>WeakSet</c>; a <c>Proxy</c>'s target and handler,
/// which are behind traps this walk must not fire; a promise's reactions; a function's captured environment;
/// the CLR object graph behind an interop wrapper; and anything reachable only from the interpreter's
/// handler-tree caches, whose retention <see cref="HandlerTreeCacheReport"/> describes instead. The census is
/// a shape-of-the-graph diagnostic, not a reachability analysis, and it is not a substitute for a heap
/// profiler.
/// </para>
/// </remarks>
public sealed record ObjectCensusReport
{
    internal ObjectCensusReport(
        int bound,
        bool boundReached,
        int objectCount,
        int plainObjects,
        int arrays,
        int functions,
        int hostWrappers,
        int otherObjects)
    {
        Bound = bound;
        BoundReached = boundReached;
        ObjectCount = objectCount;
        PlainObjects = plainObjects;
        Arrays = arrays;
        Functions = functions;
        HostWrappers = hostWrappers;
        OtherObjects = otherObjects;
    }

    /// <summary>
    /// The bound the caller asked for, echoed back so a report carries the terms it was produced under. A
    /// bound of zero or less means the census was skipped and every count below is zero.
    /// </summary>
    public int Bound { get; }

    /// <summary>
    /// Whether the walk stopped because it reached <see cref="Bound"/> while more objects were still to be
    /// visited. When this is <see langword="true"/> the counts are a lower bound and their proportions are
    /// those of the part of the graph nearest the global object, not of the whole graph; when it is
    /// <see langword="false"/> the walk ran to completion and the counts are exact for the edges it follows.
    /// </summary>
    public bool BoundReached { get; }

    /// <summary>
    /// Distinct objects visited, which is the sum of the five category counts below and never exceeds
    /// <see cref="Bound"/>. The global object itself is one of them.
    /// </summary>
    public int ObjectCount { get; }

    /// <summary>
    /// Ordinary JavaScript objects — what an object literal, <c>{}</c>, <c>Object.create</c> or
    /// <c>JsObject.Create</c> produces — regardless of whether their properties are stored in a shared
    /// layout or a per-object dictionary.
    /// </summary>
    public int PlainObjects { get; }

    /// <summary>
    /// JavaScript arrays, including the ones the engine's own built-ins hold. Typed arrays are not arrays and
    /// are counted under <see cref="OtherObjects"/>, exactly as <c>Array.isArray</c> answers.
    /// </summary>
    public int Arrays { get; }

    /// <summary>
    /// Callable objects that are not CLR bridges: script functions, the engine's own built-in functions,
    /// constructors, bound functions.
    /// </summary>
    public int Functions { get; }

    /// <summary>
    /// Objects that exist to expose CLR state to script — object wrappers, type and namespace references, and
    /// the function objects wrapping a delegate or a CLR method. Each one keeps its CLR target alive for as
    /// long as the engine keeps it, which is why they are called out separately from
    /// <see cref="PlainObjects"/> and <see cref="Functions"/>.
    /// </summary>
    public int HostWrappers { get; }

    /// <summary>
    /// Everything else: dates, regular expressions, maps, sets, promises, errors, typed arrays, array
    /// buffers, proxies, the built-in namespace objects, prototypes, and any host-defined
    /// <c>ObjectInstance</c> subclass that is none of the categories above.
    /// </summary>
    public int OtherObjects { get; }
}
