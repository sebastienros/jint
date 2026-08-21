using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Collections;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Generator;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Coverage;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;
using ExecutionContext = Jint.Runtime.Environments.ExecutionContext;

namespace Jint;

/// <summary>
/// Engine is the main API to JavaScript interpretation. An engine supports one host operation at a time;
/// concurrent public entries fail with <see cref="InvalidOperationException"/>.
/// </summary>
[DebuggerTypeProxy(typeof(EngineDebugView))]
public sealed partial class Engine : IDisposable
{
    /// <summary>
    /// How often (in loop iterations) bulk built-in operations should call <see cref="ConstraintOperations.Check"/>
    /// so that long native loops over JS-controlled sizes remain interruptible by execution constraints.
    /// Single source of truth shared by every built-in that uses the periodic-check idiom.
    /// </summary>
    internal const int ConstraintCheckInterval = 10_000;

    private static readonly Options _defaultEngineOptions = new();

    private readonly Parser _defaultParser;
    private ParserOptions? _defaultModuleParserOptions; // cache default ParserOptions for ModuleBuilder instances

    private readonly ExecutionContextStack _executionContexts;

    // Invariant for engine-level ambient mutable fields (e.g. _error, _lastSyntaxElement): they must
    // not hold an in-flight result across a call that can re-enter the engine (anything that may run
    // RunAvailableContinuations, or a CLR callback that calls Evaluate/Execute/Invoke). Either
    // save-and-restore around the re-entrant region, or carry the value out-of-band rather than in a
    // field. A script's completion value used to live here as a field and was clobbered by a
    // re-entrant drain (https://github.com/sebastienros/jint/issues/2492); it is now returned from
    // ScriptEvaluation as a per-frame local. _error and _lastSyntaxElement are safe because they are
    // produced and consumed synchronously with no re-entrant drain in between.

    /// <summary>
    /// The context threaded through every statement and expression handler. Per <em>engine</em>, not
    /// per evaluation: everything it carries is either engine-lifetime-constant configuration (the
    /// constraint and debug gates, operator overloading) or a facade over engine state, so a context
    /// built for one entry never differed from one already in flight — which is why the sites that
    /// could not find an ambient one simply built their own and threw it away.
    /// <para>
    /// Built before <see cref="Options.Apply"/>, because a host's <c>options.Configure(e =&gt; …)</c>
    /// callback runs from there and may execute script.
    /// </para>
    /// </summary>
    internal readonly EvaluationContext _evaluationContext;

    /// <summary>
    /// How many host entries into the engine are on the stack — every public entry that runs script
    /// funnels through <see cref="ExecuteWithConstraints{T}"/>, the internal spec <c>Invoke</c>, or
    /// module evaluation, and each brackets this counter.
    /// <para>
    /// It exists because "is this engine running something for someone" used to be answered by
    /// whether an evaluation context happened to be installed, which stopped being a question about
    /// the context at all once the context became per-engine — and was never a reliable answer even
    /// before that, since async and generator resume paths ran script without installing one. It is
    /// needed <em>in addition</em> to execution-context depth because <see cref="Call(JsValue, JsValue, JsCallArguments)"/>
    /// of a pure <see cref="Runtime.Interop.ClrFunction"/> pushes no execution context at all.
    /// </para>
    /// <para>
    /// A plain <see cref="int"/>: an engine is single-threaded by contract, and the settlement loop
    /// behind <c>EvaluateAsync</c> never touches
    /// this — it runs after the synchronous phase has already returned and decremented. Contrast
    /// <see cref="HasPendingAsyncOperations"/>, which is a volatile read precisely because it is not
    /// confined that way.
    /// </para>
    /// </summary>
    private int _hostEntryDepth;

    private const string ConcurrentUseMessage =
        "This Engine is already in use by another thread or has an asynchronous operation in progress. " +
        "Engine instances may only be used by one host operation at a time.";

    // Same-thread re-entry is allowed. Async APIs reserve the engine while suspended and claim
    // whichever thread resumes each continuation, without leaving a window for another host call.
    private int _ownerThreadId;
    private int _ownerDepth;
    private object? _ownerToken;
    private object? _asyncOwner;
    private object? _asyncReleasePending;
    private object? _hostCallbackAdmissionClosed;
    private int _hostCallbackAdmission;
    private int _hostReentryThreadId;
    private ManualResetEventSlim? _ownershipReleased;
    private ConditionalWeakTable<ObjectInstance, HostCallbackAuthorization>? _hostCallbackAuthorizations;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal HostCallScope EnterHostCall(object? asyncOwner = null, object? callbackOwner = null)
    {
        var threadId = System.Environment.CurrentManagedThreadId;
        if (Volatile.Read(ref _ownerThreadId) == threadId)
        {
            if (asyncOwner is not null)
            {
                if (_ownerToken is not null && !ReferenceEquals(_ownerToken, asyncOwner))
                {
                    Throw.InvalidOperationException(ConcurrentUseMessage);
                }

                _ownerToken = asyncOwner;
            }

            _ownerDepth++;
            return new HostCallScope(this, callbackOwner);
        }

        var reservedOwner = Volatile.Read(ref _asyncOwner);
        if (asyncOwner is null
            && reservedOwner is not null
            && Volatile.Read(ref _hostReentryThreadId) == threadId)
        {
            return EnterTransferredHostCall(reservedOwner);
        }

        if ((asyncOwner is null && reservedOwner is not null)
            || (asyncOwner is not null && !ReferenceEquals(asyncOwner, reservedOwner)))
        {
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        if (Interlocked.CompareExchange(ref _ownerThreadId, threadId, 0) != 0)
        {
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        // Close the race with an async operation reserving the engine after the first check.
        reservedOwner = Volatile.Read(ref _asyncOwner);
        if ((asyncOwner is null && reservedOwner is not null)
            || (asyncOwner is not null && !ReferenceEquals(asyncOwner, reservedOwner)))
        {
            Volatile.Write(ref _ownerThreadId, 0);
            Volatile.Read(ref _ownershipReleased)?.Set();
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        _ownerDepth = 1;
        _ownerToken = asyncOwner;
        return new HostCallScope(this, callbackOwner);
    }

    internal HostCallScope EnterHostCallback()
        => EnterHostCall();

    internal HostCallScope EnterTransferredHostCallback(object? expectedOwner)
    {
        if (expectedOwner is null)
        {
            return EnterHostCall();
        }

        var owner = Volatile.Read(ref _asyncOwner);
        if (owner is null
            || (!ReferenceEquals(owner, expectedOwner)
                && !ReferenceEquals(owner, Volatile.Read(ref _ownershipReleased))))
        {
            return EnterHostCall();
        }

        lock (owner)
        {
            if (!ReferenceEquals(Volatile.Read(ref _asyncOwner), owner)
                || ReferenceEquals(Volatile.Read(ref _hostCallbackAdmissionClosed), owner))
            {
                return EnterHostCall();
            }

            // The converted delegate is the capability. Count it under the operation-token lock so
            // completion cannot close and release the reservation between admission and transfer.
            Interlocked.Increment(ref _hostCallbackAdmission);
        }

        try
        {
            return EnterTransferredHostCall(owner, callback: true);
        }
        catch
        {
            ReleaseHostCallbackAdmission(owner);
            throw;
        }
    }

    internal object? CaptureHostCallbackOwner()
    {
        if (Volatile.Read(ref _ownerThreadId) != System.Environment.CurrentManagedThreadId)
        {
            return null;
        }

        return _ownerToken ??= Volatile.Read(ref _asyncOwner) ?? new object();
    }

    internal void AuthorizeHostCallback(ObjectInstance callback)
    {
        var owner = CaptureHostCallbackOwner();
        var authorizations = _hostCallbackAuthorizations;
        if (authorizations is null)
        {
            var newAuthorizations = new ConditionalWeakTable<ObjectInstance, HostCallbackAuthorization>();
            authorizations = Interlocked.CompareExchange(ref _hostCallbackAuthorizations, newAuthorizations, null)
                ?? newAuthorizations;
        }

        authorizations.GetOrCreateValue(callback).Owner = owner;
    }

    internal object? GetHostCallbackOwner(ObjectInstance callback)
    {
        var authorizations = _hostCallbackAuthorizations;
        return authorizations is not null && authorizations.TryGetValue(callback, out var authorization)
            ? authorization.Owner
            : null;
    }

    internal HostCallScope EnterTransferredHostCall(object owner, bool callback = false)
    {
        if (!ReferenceEquals(Volatile.Read(ref _asyncOwner), owner))
        {
            return EnterHostCall(callbackOwner: callback ? owner : null);
        }

        var threadId = System.Environment.CurrentManagedThreadId;
        if (Volatile.Read(ref _ownerThreadId) == threadId)
        {
            _ownerDepth++;
            return new HostCallScope(this, callback ? owner : null);
        }

        AcquireHostCall(threadId);
        if (!ReferenceEquals(Volatile.Read(ref _asyncOwner), owner))
        {
            if (Volatile.Read(ref _asyncOwner) is null)
            {
                _ownerDepth = 1;
                _ownerToken = null;
                return new HostCallScope(this, callback ? owner : null);
            }

            Volatile.Write(ref _ownerThreadId, 0);
            Volatile.Read(ref _ownershipReleased)?.Set();
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        _ownerDepth = 1;
        _ownerToken = owner;
        return new HostCallScope(this, callback ? owner : null);
    }

    private bool TryEnterHostCall(out HostCallScope scope)
    {
        var threadId = System.Environment.CurrentManagedThreadId;
        if (Volatile.Read(ref _ownerThreadId) == threadId)
        {
            _ownerDepth++;
            scope = new HostCallScope(this);
            return true;
        }

        if (Volatile.Read(ref _asyncOwner) is not null
            || Interlocked.CompareExchange(ref _ownerThreadId, threadId, 0) != 0)
        {
            scope = default;
            return false;
        }

        if (Volatile.Read(ref _asyncOwner) is not null)
        {
            Volatile.Write(ref _ownerThreadId, 0);
            Volatile.Read(ref _ownershipReleased)?.Set();
            scope = default;
            return false;
        }

        _ownerDepth = 1;
        _ownerToken = null;
        scope = new HostCallScope(this);
        return true;
    }

    private void AcquireHostCall(int threadId)
    {
        var spinWait = new SpinWait();
        while (Interlocked.CompareExchange(ref _ownerThreadId, threadId, 0) != 0)
        {
            if (spinWait.NextSpinWillYield)
            {
                var released = OwnershipReleasedEvent;
                released.Reset();
                if (Volatile.Read(ref _ownerThreadId) != 0)
                {
                    released.Wait();
                }
            }
            else
            {
                spinWait.SpinOnce();
            }
        }
    }

    private ManualResetEventSlim OwnershipReleasedEvent
    {
        get
        {
            if (_ownershipReleased is not null)
            {
                return _ownershipReleased;
            }

            var newEvent = new ManualResetEventSlim(false);
            var existing = Interlocked.CompareExchange(ref _ownershipReleased, newEvent, null);
            if (existing is not null)
            {
                newEvent.Dispose();
                return existing;
            }

            return newEvent;
        }
    }

    internal HostCallSuspension SuspendHostCallForCallbacks(bool hasTransferredCallback)
    {
        if (!hasTransferredCallback)
        {
            return default;
        }

        var previousOwnerToken = _ownerToken;
        var owner = previousOwnerToken ?? OwnershipReleasedEvent;
        var reservedOwner = Volatile.Read(ref _asyncOwner);
        var ownsReservation = reservedOwner is null;
        if (ownsReservation)
        {
            if (Interlocked.CompareExchange(ref _asyncOwner, owner, null) is not null)
            {
                Throw.InvalidOperationException(ConcurrentUseMessage);
            }
        }
        else if (!ReferenceEquals(reservedOwner, owner))
        {
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        var depth = _ownerDepth;
        var previousReentryThreadId = Volatile.Read(ref _hostReentryThreadId);
        Volatile.Write(ref _hostReentryThreadId, System.Environment.CurrentManagedThreadId);
        _ownerDepth = 0;
        _ownerToken = null;
        Volatile.Write(ref _ownerThreadId, 0);
        Volatile.Read(ref _ownershipReleased)?.Set();
        return new HostCallSuspension(this, owner, previousOwnerToken, depth, previousReentryThreadId, ownsReservation);
    }

    private void ResumeHostCall(
        object owner,
        object? previousOwnerToken,
        int depth,
        int previousReentryThreadId,
        bool ownsReservation)
    {
        if (ownsReservation)
        {
            lock (owner)
            {
                Volatile.Write(ref _hostCallbackAdmissionClosed, owner);
                while (Volatile.Read(ref _hostCallbackAdmission) > 0)
                {
                    Monitor.Wait(owner);
                }
            }
        }

        AcquireHostCall(System.Environment.CurrentManagedThreadId);
        if (ownsReservation)
        {
            lock (owner)
            {
                Interlocked.CompareExchange(ref _asyncOwner, null, owner);
                Interlocked.CompareExchange(ref _hostCallbackAdmissionClosed, null, owner);
            }
        }

        _ownerDepth = depth;
        _ownerToken = previousOwnerToken;
        Volatile.Write(ref _hostReentryThreadId, previousReentryThreadId);
    }

    private void ReleaseHostCallbackAdmission(object owner)
    {
        if (Interlocked.Decrement(ref _hostCallbackAdmission) != 0)
        {
            return;
        }

        lock (owner)
        {
            if (ReferenceEquals(Volatile.Read(ref _asyncReleasePending), owner))
            {
                Interlocked.CompareExchange(ref _asyncOwner, null, owner);
                Interlocked.CompareExchange(ref _asyncReleasePending, null, owner);
                Interlocked.CompareExchange(ref _hostCallbackAdmissionClosed, null, owner);
            }

            Monitor.PulseAll(owner);
        }
    }

    internal object ReserveAsyncHostOperation()
    {
        var activeOwner = Volatile.Read(ref _ownerThreadId);
        if (activeOwner != 0)
        {
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        var owner = new object();
        if (Interlocked.CompareExchange(ref _asyncOwner, owner, null) is not null)
        {
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        activeOwner = Volatile.Read(ref _ownerThreadId);
        if (activeOwner != 0)
        {
            Interlocked.CompareExchange(ref _asyncOwner, null, owner);
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        return owner;
    }

    internal void ReleaseAsyncHostOperation(object owner)
    {
        lock (owner)
        {
            if (!ReferenceEquals(Volatile.Read(ref _asyncOwner), owner))
            {
                return;
            }

            Volatile.Write(ref _hostCallbackAdmissionClosed, owner);
            if (Volatile.Read(ref _hostCallbackAdmission) > 0)
            {
                Volatile.Write(ref _asyncReleasePending, owner);
                return;
            }

            Interlocked.CompareExchange(ref _asyncOwner, null, owner);
            Interlocked.CompareExchange(ref _hostCallbackAdmissionClosed, null, owner);
            if (Volatile.Read(ref _ownerThreadId) == System.Environment.CurrentManagedThreadId
                && ReferenceEquals(_ownerToken, owner))
            {
                _ownerToken = null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitHostCall()
    {
        if (--_ownerDepth == 0)
        {
            _ownerToken = null;
            Volatile.Write(ref _ownerThreadId, 0);
            Volatile.Read(ref _ownershipReleased)?.Set();
        }
    }

    internal readonly struct HostCallScope : IDisposable
    {
        private readonly Engine _engine;
        private readonly object? _callbackOwner;

        internal HostCallScope(Engine engine)
        {
            _engine = engine;
            _callbackOwner = null;
        }

        internal HostCallScope(Engine engine, object? callbackOwner)
        {
            _engine = engine;
            _callbackOwner = callbackOwner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _engine.ExitHostCall();
            if (_callbackOwner is not null)
            {
                _engine.ReleaseHostCallbackAdmission(_callbackOwner);
            }
        }
    }

    private sealed class HostCallbackAuthorization
    {
        private object? _owner;

        internal object? Owner
        {
            get => Volatile.Read(ref _owner);
            set => Volatile.Write(ref _owner, value);
        }
    }

    internal readonly struct HostCallSuspension : IDisposable
    {
        private readonly Engine? _engine;
        private readonly object _owner;
        private readonly object? _previousOwnerToken;
        private readonly int _depth;
        private readonly int _previousReentryThreadId;
        private readonly bool _ownsReservation;

        internal HostCallSuspension(
            Engine engine,
            object owner,
            object? previousOwnerToken,
            int depth,
            int previousReentryThreadId,
            bool ownsReservation)
        {
            _engine = engine;
            _owner = owner;
            _previousOwnerToken = previousOwnerToken;
            _depth = depth;
            _previousReentryThreadId = previousReentryThreadId;
            _ownsReservation = ownsReservation;
        }

        public void Dispose()
        {
            _engine?.ResumeHostCall(
                _owner,
                _previousOwnerToken,
                _depth,
                _previousReentryThreadId,
                _ownsReservation);
        }
    }

    /// <summary>
    /// Whether the engine is running something for someone: a host entry is on the stack
    /// (<see cref="_hostEntryDepth"/>) or an interpreter frame is. The base global execution context
    /// is pushed once at realm initialization and never popped, so a depth above it means script.
    /// <para>
    /// Both terms are needed. A host <see cref="Call(JsValue, JsValue, JsCallArguments)"/> of a pure
    /// <see cref="Runtime.Interop.ClrFunction"/> pushes no execution context, and every async or
    /// generator resume — which runs script from an event-loop drain, with no host entry of its own —
    /// pushes one. Neither alone sees a suspended <c>EvaluateAsync</c>,
    /// whose synchronous phase has already unwound; callers that care about that add
    /// <see cref="HasPendingAsyncOperations"/>.
    /// </para>
    /// </summary>
    internal bool IsEvaluationInProgress => _hostEntryDepth > 0 || _executionContexts.Count > 1;

    // set by DebugHandler.Evaluate around watch/breakpoint-condition expression evaluation so the
    // host-boundary constraint checks can exempt it (see CheckAmortizedConstraintsAtHostBoundary)
    internal bool _debuggerEvaluating;
    internal ErrorDispatchInfo? _error;

    // Per-engine cache of interpreter function definitions — hoisted declarations, class methods,
    // class field initializers and static blocks — keyed on the stable AST node. The definition
    // owns the lazily-built body handler tree — and through it every per-node inline cache — so
    // reusing it across evaluations and calls keeps a prepared script's functions warm instead of
    // rebuilding the subtree each time declaration instantiation or class evaluation runs.
    //
    // Lifetime notes. Engine-owned rather than shared via the AST's UserData State because handler
    // trees accumulate engine-affine cache entries, and sharing them across engines retains dead
    // engines (GarbageCollectionTests.SharedPreparedScriptDoesNotRetainEngines pins this). A strong
    // Dictionary rather than a ConditionalWeakTable because a dependent handle keeps its value alive
    // on KEY reachability alone: with the key rooted by a live Prepared<Script>, value → inline
    // caches → engine → table becomes self-sustaining and pins the dropped engine — the exact
    // failure the weak table was meant to avoid. The strong form instead retains definitions for
    // scripts this engine evaluated until the engine dies, mirroring Realm._templateMap's existing
    // behavior.
    private readonly Dictionary<Node, JintFunctionDefinition> _functionDefinitions = new();

    // Scripts this engine has run global declaration instantiation for, so the definition cache
    // above only engages on RE-evaluation (see GlobalDeclarationInstantiation).
    private readonly HashSet<Script> _evaluatedScripts = new();

    // Per-engine cache of the top-level (Program) statement handler tree, keyed on the stable AST.
    // Engine-owned for the same lifetime reasons as _functionDefinitions: the tree accumulates
    // engine-affine per-node inline caches, so it must not be shared across engines via the AST.
    // Lazily allocated and only populated on RE-evaluation, so fresh-engine-per-op hosts never
    // touch it and stay byte-identical. See GetOrBuildScriptStatementList.
    private Dictionary<Script, JintStatementList>? _scriptStatementLists;

    private JintStatementList GetOrBuildScriptStatementList(Script script, bool reEvaluation)
    {
        if (!reEvaluation)
        {
            // First evaluation on this engine (fresh-engine-per-op hosts only ever hit this):
            // build fresh and cache nothing, byte-identical to the historical path.
            return new JintStatementList(null, script.Body);
        }

        var cache = _scriptStatementLists ??= new Dictionary<Script, JintStatementList>();
        if (!cache.TryGetValue(script, out var list))
        {
            // backstop mirroring CacheFunctionDefinition: bound growth for hosts streaming endless
            // distinct sources through one long-lived engine.
            if (cache.Count >= 2048)
            {
                cache.Clear();
            }

            cache[script] = list = new JintStatementList(null, script.Body);
        }

        return list;
    }

    // Per-engine cache of the interpreter handler built for a property key expression that has to be
    // evaluated — every computed key (`{ [k]: v }`, `class { [k]() {} }`, `({ [k]: v } = o)`) plus the
    // literal keys of a destructuring pattern. Keyed on the stable AST node and engine-owned for the same
    // lifetime reasons as _functionDefinitions above: a handler tree accumulates engine-affine per-node
    // inline caches, so it must not be published to the AST's UserData and shared across engines.
    //
    // Identity is what makes this correct, not merely cheaper. Everything a suspension parks for its
    // replay — LeftOperandSuspendData, AdditionChainSuspendData, the argument buffers — is keyed on the
    // handler instance that parked it, so a key expression rebuilt on every evaluation can never find its
    // own parked state. Building it afresh made `{ [f() + await g()]: 1 }` replay the whole key subtree
    // and call f twice (issue #3142).
    private Dictionary<Expression, JintExpression>? _propertyKeyExpressions;

    internal JintExpression GetOrBuildPropertyKeyExpression(Expression expression)
    {
        var cache = _propertyKeyExpressions ??= new Dictionary<Expression, JintExpression>();
        if (!cache.TryGetValue(expression, out var built))
        {
            // backstop mirroring CacheFunctionDefinition: bound growth for hosts streaming endless
            // distinct sources through one long-lived engine.
            if (cache.Count >= 2048)
            {
                cache.Clear();
            }

            cache[expression] = built = JintExpression.Build(expression);
        }

        return built;
    }

    // The handler-tree cache sizes, for Engine.Advanced.GetMemoryReport. Read-only views over the
    // collections above — nothing is recorded for them, so an engine that is never asked pays nothing.
    internal int FunctionDefinitionCacheCount => _functionDefinitions.Count;

    internal int ScriptStatementListCacheCount => _scriptStatementLists?.Count ?? 0;

    internal int EvaluatedScriptCount => _evaluatedScripts.Count;

    internal int PropertyKeyExpressionCacheCount => _propertyKeyExpressions?.Count ?? 0;

    internal JintFunctionDefinition GetOrCreateFunctionDefinition(FunctionDeclaration declaration)
    {
        if (!TryGetFunctionDefinition(declaration, out var definition))
        {
            CacheFunctionDefinition(declaration, definition = new JintFunctionDefinition(declaration));
        }

        return definition;
    }

    internal bool TryGetFunctionDefinition(Node key, [NotNullWhen(true)] out JintFunctionDefinition? definition)
        => _functionDefinitions.TryGetValue(key, out definition);

    internal void CacheFunctionDefinition(Node key, JintFunctionDefinition definition)
    {
        // backstop for hosts streaming endless distinct sources (unique eval/script texts)
        // through one engine: reset rather than grow without bound - steady-state reuse of a
        // sane number of scripts never reaches this
        if (_functionDefinitions.Count >= 2048)
        {
            _functionDefinitions.Clear();
        }

        _functionDefinitions[key] = definition;
    }

    private readonly EventLoop _eventLoop = new();
    internal EventLoop EventLoop => _eventLoop;

    private readonly Agent _agent = new();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-IncrementModuleAsyncEvaluationCount
    /// Agent-level counter for module async evaluation ordering.
    /// </summary>
    internal int ModuleAsyncEvaluationCount;

    /// <summary>
    /// How many <c>EvaluateAsync</c>/<c>ExecuteAsync</c>/<c>InvokeAsync</c> settlement loops are outstanding.
    /// Such a loop is an evaluation in progress from the host's point of view, but not from the engine's: the
    /// synchronous phase has completed, so the execution-context stack is back at base depth and the host
    /// entry has been popped — <see cref="IsEvaluationInProgress"/> is false — while the loop sits in its
    /// <c>await</c>. Only this counter can see it, which is why
    /// <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> consults it.
    /// Incremented before the first await and decremented in the loop's finally, both synchronously with
    /// respect to the Task the host holds.
    /// </summary>
    private int _pendingAsyncOperations;

    internal bool HasPendingAsyncOperations => System.Threading.Volatile.Read(ref _pendingAsyncOperations) > 0;

    // lazy properties
    private DebugHandler? _debugger;

    // cached access
    internal readonly IObjectConverter[]? _objectConverters;

    // which declared CLR member types the registered converters can claim, null when none are registered.
    // Lets the compiled interop read lanes stay in play for members no converter can ever be handed.
    internal readonly ObjectConverterTypeFilter? _objectConverterTypeFilter;

    // which CLR types the host declared immutable for the crossing, null when none were declared. Lets an
    // ObjectWrapper memoize what it reads through such a target. See Options.Interop.ImmutableCrossingTypes.
    internal readonly ImmutableCrossingTypeFilter? _immutableCrossingFilter;

    // snapshot of Options.Interop.EnumConversion, read on every CLR value crossing into script
    internal readonly bool _enumsAsStrings;

    internal readonly Constraint[] _constraints;

    // _constraints partitioned once at construction (the set is fixed for the engine's lifetime):
    // exact constraints must run before every statement, amortized ones every N statements.
    // See Engine.Constraints.cs for the partitioning rationale.
    internal readonly Constraint[] _exactConstraints;
    internal readonly Constraint[] _amortizedConstraints;

    /// <summary>
    /// Set when the exact partition consists of nothing but a <see cref="MaxStatementsConstraint"/>. That
    /// single constraint is one the interpreter can charge itself — a devirtualized call on a sealed type
    /// rather than a walk over <see cref="_exactConstraints"/> — which lets the tight-loop lanes stay armed
    /// while still hitting the counter once per executed statement, at exactly the points
    /// <see cref="RunPerStatementChecks"/> would have.
    /// </summary>
    internal readonly MaxStatementsConstraint? _inlineStatementCounter;

    /// <summary>
    /// Statements remaining before the next amortized constraint check. Driven by
    /// <see cref="EvaluationContext.RunAmortizedConstraintChecks"/>.
    /// <para>
    /// It lives on the engine rather than on the evaluation context because a top-level entry into the
    /// engine creates a fresh <see cref="EvaluationContext"/>, so a per-context countdown restarted at
    /// <see cref="EvaluationContext.AmortizedConstraintCheckInterval"/> for every entry — and a callee
    /// shorter than that interval then never reached a check at all, however many times a host loop
    /// invoked it. That silently defeated <see cref="Constraints.CancellationConstraint"/>, whose
    /// <see cref="Constraint.Reset"/> is deliberately a no-op precisely so cancellation <em>can</em> span
    /// host calls. A cadence is not a budget: the amortized constraints only observe external state, so
    /// carrying the countdown across entries (and across nested re-entries, which share it rather than
    /// restarting it) is what makes the documented bound — at most
    /// <see cref="EvaluationContext.AmortizedConstraintCheckInterval"/> statements of detection latency —
    /// hold for the engine as a whole instead of only within one entry.
    /// </para>
    /// <para>
    /// Deliberately <em>not</em> rewound by <see cref="ResetConstraints"/>: that runs on every non-nested
    /// entry and rewinding here would restore exactly the behaviour above.
    /// </para>
    /// </summary>
    internal int _amortizedConstraintCountdown = EvaluationContext.AmortizedConstraintCheckInterval;

    internal readonly bool _isDebugMode;
    internal readonly bool _isStrict;

    // Whether the reference resolver is consulted at all, plus the per-situation subscription flags
    // derived from Options.ReferenceResolverInterests. Splitting them is what lets a resolver that only
    // cares about, say, null/undefined bases keep the member-read, indexed-read and member-call fast
    // lanes armed instead of forcing every property reference through Reference + TryPropertyReference.
    internal readonly bool _customResolver;
    internal readonly bool _resolverWatchesObjectBase;
    internal readonly bool _resolverWatchesPrimitiveBase;

    /// <summary>
    /// <c>ObjectPropertyBase | PrimitivePropertyBase</c>: the gate for the read/call fast paths, which
    /// resolve a member without renting a <see cref="Reference"/> and therefore cannot offer a non-nullish
    /// base to <see cref="IReferenceResolver.TryPropertyReference"/>.
    /// </summary>
    internal readonly bool _resolverWatchesValueBase;
    internal readonly bool _resolverWatchesNullishBase;
    internal readonly bool _resolverWatchesUnresolvable;
    internal readonly bool _resolverWatchesCallee;

    /// <summary>
    /// Set when the registered resolver is <see cref="NullPropagatingReferenceResolver.Instance"/> <i>and</i>
    /// it is subscribed to nullish bases. That resolver's answer is fixed — a null/undefined base is its own
    /// result — so the engine can produce it directly instead of renting a <see cref="Reference"/> and making
    /// two interface calls to learn the same thing. It is purely a shortcut: the two sites that honour it
    /// (<c>JintMemberExpression.GetValue</c> and <see cref="GetValue(Reference, bool)"/>) must produce exactly
    /// what the interface lane would, which is what makes a recognized instance and a hand-written equivalent
    /// resolver indistinguishable to a script.
    /// </summary>
    internal readonly bool _nullishPropagatesInline;
    internal readonly IReferenceResolver _referenceResolver;

    // Snapshot of Options.Constraints.MaxRecursionDepth. Every call expression compares the pushed
    // depth against it, and reaching it through Options.Constraints costs two extra dereferences per
    // call. The limit is already treated as fixed at construction time — CallStack decides whether to
    // track depth at all from the same value (see the JintCallStack construction below).
    internal readonly int _maxRecursionDepth;

    // Snapshot of Options.Interop.AllowOperatorOverloading, consulted by the arithmetic, comparison,
    // update and assignment expressions to decide whether an operand may have a CLR operator behind
    // it. Like _maxRecursionDepth it is read after Options.Apply, so a Configure callback can still
    // set it; unlike the Options property it is fixed for the engine's lifetime, which is what lets
    // one Options instance be shared by engines that are already running.
    internal readonly bool _operatorOverloadingAllowed;

    internal readonly ReferencePool _referencePool;
    internal readonly ArgumentsInstancePool _argumentsInstancePool;
    internal readonly JsValueArrayPool _jsValueArrayPool;
    internal readonly ObjectTraverseStackPool _objectTraverseStackPool;
    internal TailCallRequest? _tailCallRequest;
    internal readonly ExtensionMethodCache _extensionMethods;

    private ITypeConverter _typeConverter = null!;

    // kept in sync by the TypeConverter setter so the interop fast lane, which is only valid for the
    // stock converter, can gate on a bool field instead of a per-call GetType() comparison
    internal bool _typeConverterIsDefault;

    public ITypeConverter TypeConverter
    {
        get => _typeConverter;
        internal set
        {
            _typeConverter = value;
            _typeConverterIsDefault = value.GetType() == typeof(DefaultTypeConverter);
        }
    }

    // cache of types used when resolving CLR type names
    internal readonly Dictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    // we use registered type reference as prototype if it's known
    internal Dictionary<Type, TypeReference>? _typeReferences;

    // cache for already wrapped CLR objects to keep object identity
    internal ConditionalWeakTable<object, ObjectInstance>? _objectWrapperCache;

    // per-object private-element storage ([[PrivateElements]]). Kept off ObjectInstance in a weak table
    // (keyed by the owning object) so the 8-byte reference doesn't bloat every object on the heap — only
    // instances of JS classes that declare #private members ever allocate an entry here.
    internal ConditionalWeakTable<ObjectInstance, Dictionary<PrivateName, PrivateElement>>? _privateElementStore;

    // bounded cache of most recently wrapped CLR objects, see Options.Interop.CacheRecentObjectWrappers
    internal RecentObjectWrapperCache? _recentObjectWrapperCache;

    // Diagnostic counters for the two mode-governed CLR-array conversions, surfaced through
    // Engine.Advanced.GetInteropConversionDiagnostics. Bumped only inside DefaultObjectConverter, at the two
    // points that have just decided to build a live wrapper view or an N-element snapshot copy — so the cost
    // is one increment on a path that is already allocating, and exactly nothing on every path that converts
    // no array. Plain longs, not Interlocked: an engine is single-threaded by contract, like every other
    // per-engine counter here.
    internal long _arrayLiveViewConversions;
    internal long _arrayCopyConversions;

    internal readonly JintCallStack CallStack;
    internal readonly StackGuard _stackGuard;

    // needed in initial engine setup, for example CLR function construction
    internal Intrinsics _originalIntrinsics = null!;
    internal Host _host = null!;

    // The resolved reflection accessors themselves live on Options.Interop.TypeResolver so that every engine
    // sharing a resolver shares the resolution (and the delegates compiled from it). The interop configuration
    // that steers resolution but does not live on the resolver is captured here, and partitions that cache so
    // an entry is only ever served back to an engine that would have resolved it identically.
    internal readonly InteropResolutionProfile _interopResolutionProfile;

    /// <summary>
    /// Constructs a new engine instance.
    /// </summary>
    public Engine() : this(null, null)
    {
    }

    /// <summary>
    /// Constructs a new engine instance and allows customizing options.
    /// </summary>
    public Engine(Action<Options>? options)
        : this(null, options != null ? (_, opts) => options.Invoke(opts) : null)
    {
    }

    /// <summary>
    /// Constructs a new engine with a custom <see cref="Options"/> instance.
    /// </summary>
    public Engine(Options options) : this(options, null)
    {
    }

    /// <summary>
    /// Constructs a new engine instance and allows customizing options.
    /// </summary>
    /// <remarks>The provided engine instance in callback is not guaranteed to be fully configured</remarks>
    public Engine(Action<Engine, Options> options) : this(null, options)
    {
    }

    private Engine(Options? options, Action<Engine, Options>? configure)
    {
        Advanced = new AdvancedOperations(this);
        Constraints = new ConstraintOperations(this);
        TypeConverter = new DefaultTypeConverter(this);

        _executionContexts = new ExecutionContextStack(2);

        // we can use default options if there's no action to modify it
        Options = options ?? (configure is not null ? new Options() : _defaultEngineOptions);

        configure?.Invoke(this, Options);

        _extensionMethods = ExtensionMethodCache.Build(Options.Interop.ExtensionMethodTypes);

        Reset();

        // gather some options as fields for faster checks
        _isDebugMode = Options.Debugger.Enabled;
        _isStrict = Options.Strict;

        // Must be settled before the EvaluationContext below is built: the context folds this into the
        // per-statement-lane decision, and that lane is the only thing that ever reaches the counters.
        _coverage = Options.Coverage.Enabled ? new CoverageCollector(Options.Coverage.Granularity) : null;

        _objectConverters = Options.Interop.ObjectConverters.Count > 0
            ? Options.Interop.ObjectConverters.ToArray()
            : null;
        _objectConverterTypeFilter = ObjectConverterTypeFilter.Create(_objectConverters);
        _immutableCrossingFilter = ImmutableCrossingTypeFilter.Create(Options.Interop.ImmutableCrossingTypes);
        _enumsAsStrings = Options.Interop.EnumConversion == EnumConversionMode.String;

        _constraints = BuildConstraints(Options.Constraints);
        var partitionedConstraints = PartitionConstraints(_constraints);
        _exactConstraints = partitionedConstraints.Exact;
        _amortizedConstraints = partitionedConstraints.Amortized;
        _inlineStatementCounter = partitionedConstraints.InlineStatementCounter;

        // Everything the context snapshots is settled by now (debug mode, the constraint partition),
        // and it must exist before Options.Apply below, whose configuration callbacks may execute
        // script. That is also why the context does not snapshot operator overloading itself: the
        // engine takes that reading after Apply and the context reads it from there.
        _evaluationContext = new EvaluationContext(this);

        _referenceResolver = Options.ReferenceResolver;
        var resolverInterests = ReferenceEquals(_referenceResolver, DefaultReferenceResolver.Instance)
            ? ReferenceResolverInterests.None
            : Options.ReferenceResolverInterests;
        _customResolver = resolverInterests != ReferenceResolverInterests.None;
        _resolverWatchesObjectBase = (resolverInterests & ReferenceResolverInterests.ObjectPropertyBase) != ReferenceResolverInterests.None;
        _resolverWatchesPrimitiveBase = (resolverInterests & ReferenceResolverInterests.PrimitivePropertyBase) != ReferenceResolverInterests.None;
        _resolverWatchesValueBase = _resolverWatchesObjectBase || _resolverWatchesPrimitiveBase;
        _resolverWatchesNullishBase = (resolverInterests & ReferenceResolverInterests.NullishPropertyBase) != ReferenceResolverInterests.None;
        _resolverWatchesUnresolvable = (resolverInterests & ReferenceResolverInterests.UnresolvableReference) != ReferenceResolverInterests.None;
        _resolverWatchesCallee = (resolverInterests & ReferenceResolverInterests.NonCallableCallee) != ReferenceResolverInterests.None;

        // The interest is part of the condition on purpose: a host that registers the shipped resolver but
        // does not subscribe to nullish bases has told the engine not to consult it for them, and the inline
        // lane must obey that filter exactly as the interface lane does.
        _nullishPropagatesInline = _resolverWatchesNullishBase
            && ReferenceEquals(_referenceResolver, NullPropagatingReferenceResolver.Instance);

        _referencePool = new ReferencePool();
        _argumentsInstancePool = new ArgumentsInstancePool(this);
        _jsValueArrayPool = new JsValueArrayPool();
        _objectTraverseStackPool = new ObjectTraverseStackPool();

        Options.Apply(this);

        // Read after Options.Apply so the snapshot observes the same value JintCallStack does
        // (Apply runs the user's configuration callbacks, which can still touch Constraints).
        _maxRecursionDepth = Options.Constraints.MaxRecursionDepth;

        // Likewise after Apply: a configuration callback registered with Options.Configure is a
        // documented place to finish configuring an engine, and enabling operator overloading from
        // one has to reach the expressions that consult it.
        _operatorOverloadingAllowed = Options.Interop.AllowOperatorOverloading;

        // likewise after Apply, which is where a custom ITypeConverter gets installed
        _interopResolutionProfile = new InteropResolutionProfile(
            Options.Interop.AllowGetType,
            Options.Interop.ObjectWrapperReportedFieldBindingFlags,
            Options.Interop.ObjectWrapperReportedPropertyBindingFlags,
            Options.Interop.ObjectWrapperReportedMethodBindingFlags,
            _extensionMethods,
            _typeConverterIsDefault);

        CallStack = new JintCallStack(_maxRecursionDepth >= 0);
        _stackGuard = new StackGuard(this);

        var scriptParsingDefaults = Options.RetainFunctionSourceText
            ? ScriptParsingOptions.RetainingDefault
            : ScriptParsingOptions.Default;
        var defaultParserOptions = scriptParsingDefaults.GetParserOptions(Options);
        _defaultParser = new Parser(defaultParserOptions);
    }

    private void Reset()
    {
        _host = Options.Host.Factory(this);
        _host.Initialize(this);

        // InitializeHostDefinedRealm has just pushed the base execution context, and nothing pops it, so the
        // realm it created is this engine's principal one for as long as the engine lives. It is otherwise
        // reachable only from the bottom of the execution context stack: `Realm` names the *current* realm,
        // which a ShadowRealm evaluation replaces for the duration of its own execution context.
        _mainRealm = ExecutionContext.Realm;
    }

    internal ref readonly ExecutionContext ExecutionContext
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _executionContexts.Peek();
    }

    // temporary state for realm so that we can easily pass it to functions while still not
    // having a proper execution context established
    internal Realm? _realmInConstruction;

    internal Node? _lastSyntaxElement;

    // Bumped when a binding is injected into a PRE-EXISTING environment mid-execution (sloppy
    // direct eval var/function hoisting, AnnexB block-function var-scope copies). Nested-scope
    // global-read memos pin environment chains by identity and re-validate against this — chain
    // identity alone cannot see a name added to a pinned env. Creates into fresh environments
    // never bump: a fresh environment cannot appear in a previously pinned chain.
    internal int _envBindingInjectionEpoch;

    /// <summary>
    /// The block-level function declarations of the sloppy direct or indirect eval whose body is
    /// currently running - the Annex B B.3.2 candidates its EvalDeclarationInstantiation considered.
    /// Null outside an eval body and for a strict one. Set and restored by
    /// <see cref="Jint.Native.Function.EvalFunction"/>; read by
    /// <c>JintFunctionDeclarationStatement</c>, which has no other route back to the eval's static
    /// analysis. Function code answers the same question from its own
    /// <c>JintFunctionDefinition.State</c> instead, and script/global-eval code from the global
    /// environment, so this covers only the eval-inside-a-function case.
    /// </summary>
    internal List<FunctionDeclaration>? _evalAnnexBFunctions;

    /// <summary>
    /// The realm <c>InitializeHostDefinedRealm</c> created for this engine, as opposed to
    /// <see cref="Realm"/>, which is whichever realm the running execution context names. Assigned once, in
    /// <c>Reset()</c>, and never replaced.
    /// </summary>
    internal Realm _mainRealm = null!;

    internal Realm Realm => _realmInConstruction ?? ExecutionContext.Realm;

    /// <summary>
    /// The well-known intrinsics for this engine instance.
    /// </summary>
    public Intrinsics Intrinsics
    {
        get
        {
            using var ownership = EnterHostCall();
            return Realm.Intrinsics;
        }
    }

    /// <summary>
    /// The global object for this engine instance.
    /// </summary>
    public ObjectInstance Global
    {
        get
        {
            using var ownership = EnterHostCall();
            return Realm.GlobalObject;
        }
    }

    internal GlobalSymbolRegistry GlobalSymbolRegistry { get; } = new();

    internal long CurrentMemoryUsage { get; private set; }

    internal Options Options
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }

    public DebugHandler Debugger
    {
        get
        {
            var debugger = Volatile.Read(ref _debugger);
            if (debugger is not null)
            {
                return debugger;
            }

            using var ownership = EnterHostCall();
            debugger = _debugger;
            if (debugger is null)
            {
                debugger = new DebugHandler(this, Options.Debugger.InitialStepMode);
                Volatile.Write(ref _debugger, debugger);
            }

            return debugger;
        }
    }

    internal ParserOptions DefaultModuleParserOptions => _defaultModuleParserOptions ??=
        (Options.RetainFunctionSourceText ? ModuleParsingOptions.RetainingDefault : ModuleParsingOptions.Default).GetParserOptions(Options);

    internal ParserOptions GetActiveParserOptions()
    {
        return _executionContexts?.GetActiveParserOptions() ?? _defaultParser.Options;
    }

    internal Parser GetParserFor(ScriptParsingOptions parsingOptions)
    {
        return ReferenceEquals(parsingOptions, ScriptParsingOptions.Default)
            ? _defaultParser
            : new Parser(parsingOptions.GetParserOptions(Options));
    }

    internal Parser GetParserFor(ParserOptions parserOptions)
    {
        return ReferenceEquals(parserOptions, _defaultParser.Options) ? _defaultParser : new Parser(parserOptions);
    }

    internal void EnterExecutionContext(
        Environment lexicalEnvironment,
        Environment variableEnvironment,
        Realm realm,
        PrivateEnvironment? privateEnvironment,
        bool strict)
    {
        var context = new ExecutionContext(
            null,
            lexicalEnvironment,
            variableEnvironment,
            privateEnvironment,
            realm,
            null,
            strict: strict);

        _executionContexts.Push(in context);
    }

    internal void EnterExecutionContext(in ExecutionContext context)
    {
        _executionContexts.Push(in context);
    }

    /// <summary>
    /// Pushes the execution context for an env-less leaf call (State.SupportsLeafCall): both
    /// environment slots point at the function's captured environment — no callee
    /// FunctionEnvironment exists for such frames.
    /// </summary>
    internal void EnterLeafCallExecutionContext(
        IScriptOrModule? scriptOrModule,
        Environment environment,
        PrivateEnvironment? privateEnvironment,
        Realm realm,
        Native.Function.Function function,
        bool strict)
    {
        _executionContexts.Push(new ExecutionContext(
            scriptOrModule,
            lexicalEnvironment: environment,
            variableEnvironment: environment,
            privateEnvironment,
            realm,
            generator: null,
            function: function,
            strict: strict));
    }

    /// <summary>
    /// Registers a delegate with given name. Delegate becomes a JavaScript function that can be called.
    /// </summary>
    public Engine SetValue(string name, Delegate value)
    {
        using var ownership = EnterHostCall();
        Realm.GlobalObject.FastSetProperty(name, new PropertyDescriptor(new DelegateWrapper(this, value), PropertyFlag.NonEnumerable));
        return this;
    }

    /// <summary>
    /// Registers a string value as variable.
    /// </summary>
    public Engine SetValue(string name, string? value)
    {
        return SetValue(name, value is null ? JsValue.Null : JsString.Create(value));
    }

    /// <summary>
    /// Registers a double value as variable.
    /// </summary>
    public Engine SetValue(string name, double value)
    {
        return SetValue(name, (JsValue) JsNumber.Create(value));
    }

    /// <summary>
    /// Registers an integer value as variable.
    /// </summary>
    public Engine SetValue(string name, int value)
    {
        return SetValue(name, (JsValue) JsNumber.Create(value));
    }

    /// <summary>
    /// Registers a boolean value as variable.
    /// </summary>
    public Engine SetValue(string name, bool value)
    {
        return SetValue(name, (JsValue) (value ? JsBoolean.True : JsBoolean.False));
    }

    /// <summary>
    /// Registers a native JS value as variable.
    /// </summary>
    public Engine SetValue(string name, JsValue value)
    {
        using var ownership = EnterHostCall();
        Realm.GlobalObject.Set(name, value);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue(string name, object? obj)
    {
        using var ownership = EnterHostCall();
        var value = obj is Type t
            ? TypeReference.CreateTypeReference(this, t)
            : JsValue.FromObject(this, obj);

        return SetValue(name, value);
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue(string name, [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        using var ownership = EnterHostCall();
#pragma warning disable IL2111
        return SetValue(name, TypeReference.CreateTypeReference(this, type));
#pragma warning restore IL2111
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name, T? obj)
    {
        using var ownership = EnterHostCall();
        return obj is Type t
            ? SetValue(name, t)
            : SetValue(name, JsValue.FromObject(this, obj));
    }

    internal void LeaveExecutionContext()
    {
        _executionContexts.Pop();
    }

    internal void ResetConstraints()
    {
        foreach (var constraint in _constraints)
        {
            constraint.Reset();
        }
    }

    /// <summary>
    /// Initializes list of references of called functions
    /// </summary>
    internal void ResetCallStack()
    {
        CallStack.Clear();
    }

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(string code, string? source = null)
    {
        using var ownership = EnterHostCall();
        var script = _defaultParser.ParseScriptGuarded(Realm, code, source: source ?? "<anonymous>", strict: _isStrict);
        return Evaluate(new Prepared<Script>(script, _defaultParser.Options));
    }

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(string code, ScriptParsingOptions parsingOptions)
        => Evaluate(code, "<anonymous>", parsingOptions);

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(string code, string source, ScriptParsingOptions parsingOptions)
    {
        using var ownership = EnterHostCall();
        var parser = GetParserFor(parsingOptions);
        var script = parser.ParseScriptGuarded(Realm, code, parsingOptions.SourceOffset, source, _isStrict);
        return Evaluate(new Prepared<Script>(script, parser.Options));
    }

    /// <summary>
    /// Evaluates code and returns last return value.
    /// </summary>
    public JsValue Evaluate(in Prepared<Script> preparedScript)
        => ExecuteForCompletion(in preparedScript);

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(string code, string? source = null)
    {
        using var ownership = EnterHostCall();
        var script = _defaultParser.ParseScriptGuarded(Realm, code, source: source ?? "<anonymous>", strict: _isStrict);
        return Execute(new Prepared<Script>(script, _defaultParser.Options));
    }

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(string code, ScriptParsingOptions parsingOptions)
        => Execute(code, "<anonymous>", parsingOptions);

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(string code, string source, ScriptParsingOptions parsingOptions)
    {
        using var ownership = EnterHostCall();
        var parser = GetParserFor(parsingOptions);
        var script = parser.ParseScriptGuarded(Realm, code, parsingOptions.SourceOffset, source, _isStrict);
        return Execute(new Prepared<Script>(script, parser.Options));
    }

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(in Prepared<Script> preparedScript)
    {
        ExecuteForCompletion(in preparedScript);
        return this;
    }

    /// <summary>
    /// Shared core for <see cref="Execute(in Prepared{Script})"/> and <see cref="Evaluate(in Prepared{Script})"/>.
    /// Returns the script's completion value directly (a per-frame local), so a re-entrant drain
    /// during <see cref="RunAvailableContinuations"/> cannot clobber it via shared engine state.
    /// </summary>
    private JsValue ExecuteForCompletion(in Prepared<Script> preparedScript)
    {
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var script = preparedScript.Program;
        var parserOptions = preparedScript.ParserOptions;
        var strict = _isStrict || script.Strict;
        // The lambda captures the locals (script, parserOptions), never the `in` parameter.
        return ExecuteWithConstraints(strict, () => ScriptEvaluation(new ScriptRecord(Realm, script, script.Location.SourceFile), parserOptions));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-scriptevaluation
    /// </summary>
    private JsValue ScriptEvaluation(ScriptRecord scriptRecord, ParserOptions parserOptions)
    {
        Debugger.OnBeforeEvaluate(scriptRecord.EcmaScriptCode);

        var globalEnv = Realm.GlobalEnv;

        var scriptContext = new ExecutionContext(
            scriptRecord,
            lexicalEnvironment: globalEnv,
            variableEnvironment: globalEnv,
            privateEnvironment: null,
            Realm,
            parserOptions: parserOptions,
            strict: _isStrict || scriptRecord.EcmaScriptCode.Strict);

        EnterExecutionContext(in scriptContext);
        try
        {
            var script = scriptRecord.EcmaScriptCode;
            var reEvaluation = GlobalDeclarationInstantiation(script, globalEnv);

            // On re-evaluation reuse this engine's cached top-level handler tree (and its warm
            // per-node inline caches) instead of rebuilding it; a first evaluation - the fresh-
            // engine-per-run shape - takes plain construction so those hosts stay byte-identical.
            // Engine-owned like the function-definition cache above (dies with the engine, so it
            // never pins a dropped engine through the shared prepared AST).
            var list = GetOrBuildScriptStatementList(script, reEvaluation);

            Completion result;
            try
            {
                result = list.Execute(_evaluationContext);
            }
            catch
            {
                // unhandled exception
                ResetCallStack();
                throw;
            }

            if (result.Type == CompletionType.Throw)
            {
                var ex = new JavaScriptException(result.GetValueOrDefault()).SetJavaScriptCallstack(this, result.Location);
                ResetCallStack();
                throw ex;
            }

            // Capture into a local BEFORE draining the event loop. RunAvailableContinuations can
            // re-enter the engine (e.g. an awaited dynamic import that invokes a CLR callback which
            // calls Evaluate()); a deeper ScriptEvaluation must not be able to clobber this frame's
            // result. A local is per-frame, so it survives re-entrancy (#2492). A completed script's
            // value is fixed by its last statement — continuations cannot change it.
            var completionValue = result.GetValueOrDefault();

            // TODO what about callstack and thrown exceptions?
            RunAvailableContinuations();

            return completionValue;
        }
        finally
        {
            LeaveExecutionContext();
        }
    }

    /// <summary>
    /// EXPERIMENTAL! Subject to change.
    ///
    /// Registers a promise within the currently running EventLoop (has to be called within "ExecuteWithEventLoop" call).
    /// Note that ExecuteWithEventLoop will not trigger "onFinished" callback until ALL manual promises are settled.
    ///
    /// Resolve and reject may be called from another thread. Settlement is enqueued safely and drains
    /// inline only when that thread can claim exclusive engine ownership.
    /// </summary>
    /// <returns>a Promise instance and functions to either resolve or reject it</returns>
    internal ManualPromise RegisterPromise()
    {
        var promise = new JsPromise(this)
        {
            _prototype = Realm.Intrinsics.Promise.PrototypeObject
        };

        var (resolve, reject) = promise.CreateResolvingFunctions();

        // The cycle this promise belongs to, read here on the engine thread. The settle below may run
        // arbitrarily later on a background thread, and stamping the job with the generation captured now
        // is what lets the drain tell "work this engine is still waiting for" from "work whose cycle a
        // RestoreGlobalSnapshot has since ended".
        var generation = _eventLoop.Generation;

        Action<JsValue> SettleWith(Function settle) => value =>
        {
            // Enqueue to event loop to ensure thread safety - the settle operation
            // may be called from a background thread (e.g., Task.ContinueWith callback)
            // but JavaScript execution must happen on the thread that owns the Engine.
            AddToEventLoop(() =>
            {
                settle.Call(JsValue.Undefined, [value]);
            }, generation);

            // Signal the CompletedEvent so that UnwrapIfPromise knows there's work to process.
            promise.CompletedEvent.Set();

            // A completion may arrive on any thread. Drain inline only when this thread can claim
            // exclusive ownership; otherwise the next host turn or async waiter owns the drain.
            if (TryEnterHostCall(out var ownership))
            {
                using (ownership)
                {
                    _eventLoop.RunAvailableContinuations(this);
                }
            }
        };

        return new ManualPromise(promise, SettleWith(resolve), SettleWith(reject));
    }

    /// <summary>
    /// Internal version of RegisterPromise that returns settle functions accepting CLR objects.
    /// The CLR to JsValue conversion happens on the event loop thread, not the caller thread.
    /// This is critical for thread safety when called from Task.ContinueWith callbacks.
    /// </summary>
    internal ManualPromiseWithClrValue RegisterPromiseWithClrValue()
    {
        var promise = new JsPromise(this)
        {
            _prototype = Realm.Intrinsics.Promise.PrototypeObject
        };

        var (resolve, reject) = promise.CreateResolvingFunctions();

        // Registration-time generation; see RegisterPromise. This is the path a CLR Task awaited from script
        // takes (JsValue.ConvertTaskToPromise), so it is the one that carries a fire-and-forget Task across a
        // restore if nothing stamps it.
        var generation = _eventLoop.Generation;

        Action<object?> SettleWithClr(Function settle) => clrValue =>
        {
            // Enqueue to event loop to ensure thread safety - both the FromObject conversion
            // and the settle operation are performed on the main thread, not the background
            // thread that completed the Task.
            AddToEventLoop(() =>
            {
                var jsValue = JsValue.FromObject(this, clrValue);
                settle.Call(JsValue.Undefined, [jsValue]);
            }, generation);

            // Signal the CompletedEvent so that UnwrapIfPromise knows there's work to process.
            // NOTE: We do NOT call RunAvailableContinuations() here because this method is
            // called from background threads (Task.ContinueWith callbacks) and the _waitingThreadId
            // protection might not be active yet. The waiting thread in UnwrapIfPromise will
            // process the event loop when it wakes up.
            promise.CompletedEvent.Set();
        };

        return new ManualPromiseWithClrValue(promise, SettleWithClr(resolve), SettleWithClr(reject));
    }

    /// <summary>
    /// Enqueues in-cycle work: the job joins the generation that is current right now, which is what every
    /// caller running on the engine thread wants. Work registered in one cycle and enqueued in a later one
    /// must use the overload below instead.
    /// </summary>
    internal void AddToEventLoop(Action continuation)
    {
        _eventLoop.Enqueue(continuation);
    }

    /// <summary>
    /// Enqueues work on behalf of an earlier registration, carrying that registration's generation. Used by
    /// the promise settle closures, which can fire on a background thread long after their cycle ended.
    /// </summary>
    internal void AddToEventLoop(Action continuation, int generation)
    {
        _eventLoop.Enqueue(new EventLoopJob(continuation, generation));
    }

    /// <summary>
    /// Enqueues a promise reaction job without allocating a closure: the (reaction, argument)
    /// pair is the job's state. See <see cref="EventLoopJob"/>.
    /// </summary>
    internal void AddToEventLoop(PromiseReaction reaction, JsValue value)
    {
        _eventLoop.Enqueue(new EventLoopJob(reaction, value, _eventLoop.Generation));
    }

    /// <summary>
    /// The cycle a piece of work registered now belongs to. Read at registration time and carried by the
    /// work, so that a completion arriving from a background thread after the cycle ended is discarded
    /// rather than applied — see <see cref="EventLoop.Generation"/>.
    /// </summary>
    internal int EventLoopGeneration => _eventLoop.Generation;

    /// <summary>
    /// Queues the engine-thread half of an asynchronous module load. Separate from
    /// <see cref="AddToEventLoop(Action, int)"/> only in name, to keep the module loader's one cross-thread
    /// entry point findable.
    /// </summary>
    internal void EnqueueModuleLoadCompletion(Action continuation, int generation)
    {
        _eventLoop.Enqueue(new EventLoopJob(continuation, generation));
    }

    /// <summary>
    /// Called by the host when a promise rejection is tracked/untracked.
    /// Raises the <see cref="AdvancedOperations.PromiseRejectionTracker"/> event.
    /// </summary>
    internal void OnPromiseRejectionTracker(JsPromise promise, PromiseRejectionOperation operation)
    {
        Advanced.RaisePromiseRejectionTracker(promise, operation);

#if NET8_0_OR_GREATER
        // Additively, and strictly after the event above: a host that wired both channels must see the
        // pre-existing one behave exactly as it always did, which it cannot if a sink can run first. Null on
        // every engine whose host set no diagnostics sink, which is one predictable null test on a path that
        // only runs when a rejection had no handler.
        _webApi?.ReportPromiseRejection(promise, operation);
#endif
    }

    internal void AddToKeptObjects(JsValue target)
    {
        _agent.AddToKeptObjects(target);
    }

    internal void RunAvailableContinuations()
    {
        using var ownership = EnterHostCall();
        _eventLoop.RunAvailableContinuations(this);
    }

    /// <summary>
    /// Synchronously drives the event loop until <paramref name="promise"/> leaves the pending state
    /// or <paramref name="timeout"/> elapses. Between drains it blocks until either the promise's
    /// completion event or the event loop's work-arrived signal fires, so a continuation enqueued from a
    /// background thread — a .NET <see cref="System.Threading.Tasks.Task"/> awaited at a module's top
    /// level via task interop, a <c>setTimeout</c> callback, an asynchronous module load settling — is
    /// run promptly rather than after an idle poll slice.
    /// </summary>
    /// <remarks>
    /// A tight spin loop gives up in microseconds and never observes a Task that only completes
    /// milliseconds later on a ThreadPool thread (issue #2663). This mirrors the polling in
    /// <see cref="JsValueExtensions.UnwrapIfPromise(Native.JsValue, TimeSpan)"/>. A non-positive
    /// <paramref name="timeout"/> means wait indefinitely. A registered
    /// <see cref="Constraints.CancellationConstraint"/> is observed during the idle waits so that a
    /// cancelled engine breaks out promptly (throwing <see cref="ExecutionCanceledException"/>, the
    /// same way per-statement execution surfaces cancellation) instead of blocking up to the full
    /// timeout while awaiting a never-settling Task.
    /// </remarks>
    /// <param name="promise">The promise to wait for.</param>
    /// <param name="timeout">Non-positive means wait indefinitely.</param>
    /// <param name="cancellationToken">
    /// A token supplied by the caller, distinct from the engine's own cancellation constraint; see
    /// <see cref="DrainEventLoopUntil"/>.
    /// </param>
    /// <returns>Whether the promise had settled when the drain ended, i.e. false on timeout.</returns>
    internal bool DrainEventLoopUntilSettled(JsPromise promise, TimeSpan timeout, System.Threading.CancellationToken cancellationToken = default)
        => DrainEventLoopUntil(static state => ((JsPromise) state).State != PromiseState.Pending, promise, promise.CompletedEvent, timeout, cancellationToken);

    /// <summary>
    /// The general form of <see cref="DrainEventLoopUntilSettled"/>: drives the event loop until
    /// <paramref name="isSettled"/> holds for <paramref name="state"/>. Used by the module load phase, where
    /// what is being waited for is a host load finishing rather than a promise.
    /// </summary>
    /// <param name="isSettled">The condition to wait for, evaluated on this thread between drains.</param>
    /// <param name="state">Passed to <paramref name="isSettled"/>, so the predicate need not be a closure.</param>
    /// <param name="completedEvent">
    /// Signalled when the awaited thing settles, so the idle waits end promptly instead of running out the
    /// poll interval. May be null; either way the wait also ends as soon as new work is enqueued, since
    /// running that work on this thread is the only way the condition can advance.
    /// </param>
    /// <param name="timeout">Non-positive means wait indefinitely.</param>
    /// <param name="cancellationToken">
    /// A token supplied by the caller, kept deliberately separate from the engine's own
    /// <see cref="Constraints.CancellationConstraint"/> because the two carry different contracts: this one
    /// belongs to whoever asked for the wait and is reported back to them as an
    /// <see cref="OperationCanceledException"/>, while the constraint means the engine itself was cancelled
    /// and surfaces as <see cref="ExecutionCanceledException"/>, exactly as per-statement execution reports
    /// it. Both end the wait; the catch below decides which contract applies. The two are linked only when
    /// both can actually fire, so every caller that passes nothing allocates nothing.
    /// </param>
    /// <returns>Whether the condition held when the drain ended, i.e. false on timeout.</returns>
    internal bool DrainEventLoopUntil(
        Func<object, bool> isSettled,
        object state,
        System.Threading.ManualResetEventSlim? completedEvent,
        TimeSpan timeout,
        System.Threading.CancellationToken cancellationToken = default)
    {
        using var ownership = EnterHostCall();

        // Claim this thread as the one draining the loop so background threads (Task completions)
        // don't race to execute JavaScript continuations on the engine. Save/restore to support
        // nesting (e.g. a re-entrant UnwrapIfPromise already waiting).
        var previousWaitingThreadId = _eventLoop._waitingThreadId;
        _eventLoop._waitingThreadId = System.Environment.CurrentManagedThreadId;

        // Observe a registered CancellationConstraint during the otherwise-idle waits. No statements
        // run while we block on the completion event, so the per-statement constraint checks never
        // fire; without this, cancelling the engine while a top-level await hangs on a never-settling
        // Task would go unnoticed until PromiseTimeout. Defaults to CancellationToken.None when no
        // such constraint is registered, which leaves the wait behavior unchanged.
        var constraintToken = Constraints.Find<CancellationConstraint>()?.Token ?? default;

        System.Threading.CancellationTokenSource? linkedTokenSource = null;
        var waitToken = constraintToken;
        if (cancellationToken.CanBeCanceled)
        {
            if (constraintToken.CanBeCanceled)
            {
                linkedTokenSource = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, constraintToken);
                waitToken = linkedTokenSource.Token;
            }
            else
            {
                waitToken = cancellationToken;
            }
        }

        try
        {
            var hasTimeout = timeout > TimeSpan.Zero;
            var deadline = hasTimeout ? DateTime.UtcNow + timeout : DateTime.MaxValue;
            var pollInterval = TimeSpan.FromMilliseconds(10);

            while (!isSettled(state))
            {
                // The caller's token is checked before any work is run, so an already-cancelled token
                // fails the wait rather than being masked by a drain that happens to settle the
                // condition on its first turn.
                cancellationToken.ThrowIfCancellationRequested();

                RunAvailableContinuations();

                if (isSettled(state))
                {
                    break;
                }

                var waitInterval = pollInterval;
                if (hasTimeout)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    if (remaining < waitInterval)
                    {
                        waitInterval = remaining;
                    }
                }

                // Work the engine scheduled for itself — an Atomics.waitAsync timeout, a web-API timer —
                // enqueues nothing, so nothing wakes this thread when it comes due: the wait has to end by
                // itself. The 10ms poll above already made anything of 10ms and more correct; clamping to the
                // next due time is what buys a 1ms deadline 1ms of latency instead of 10.
                var untilNextWork = TimeUntilNextPumpScheduledWork();
                if (untilNextWork is { } untilDue)
                {
                    if (untilDue <= TimeSpan.Zero)
                    {
                        // Already due. If this thread can pump, promoting it beats idling first, and the
                        // timeout deadline checked immediately above stops the loop running past its bound.
                        // Nested inside a job it cannot pump — the re-entrancy guard makes the queue
                        // unrunnable from here — so fall through to the bounded wait rather than spinning on
                        // work nobody present can promote.
                        if (!_eventLoop.IsRunningJob)
                        {
                            continue;
                        }
                    }
                    else if (untilDue < waitInterval)
                    {
                        waitInterval = untilDue;
                    }
                }

                try
                {
                    // Waking on enqueue is what keeps the interval a backstop rather than a latency floor: a
                    // settle arriving from a background thread only enqueues, and this thread is the one that
                    // has to run it — before the wake it idled out the rest of the poll slice first, which a
                    // chain of sequential asynchronous loads paid on every hop.
                    using (SuspendHostCallForCallbacks(hasTransferredCallback: true))
                    {
                        _eventLoop.WaitForWork(completedEvent, waitInterval, waitToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // The caller's own token keeps its own contract: whoever asked for the wait gets an
                    // OperationCanceledException back, which is what the public UnwrapIfPromise overload
                    // taking a token documents.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    // Otherwise the registered CancellationConstraint's token was cancelled during the idle
                    // wait. Surface it exactly as per-statement execution does (CancellationConstraint.Check),
                    // rather than swallowing it and letting the drain fall through to a silent timeout.
                    Throw.ExecutionCanceledException();
                }
            }

            return isSettled(state);
        }
        finally
        {
            linkedTokenSource?.Dispose();
            _eventLoop._waitingThreadId = previousWaitingThreadId;
        }
    }

    /// <summary>
    /// Checks every registered constraint plus the debugger step hook. Used by native callback
    /// call sites (e.g. sort comparers) where the callee may not run any JS statements itself;
    /// the interpreter's per-statement path uses the partitioned
    /// <see cref="RunPerStatementChecks"/> / <see cref="CheckAmortizedConstraints"/> pair instead.
    /// </summary>
    internal void RunBeforeExecuteStatementChecks(StatementOrExpression? statement)
    {
        // Avoid allocating the enumerator because we run this loop very often.
        foreach (var constraint in _constraints)
        {
            constraint.Check();
        }

        if (_isDebugMode && statement != null && statement.Type != NodeType.BlockStatement)
        {
            Debugger.OnStep(statement);
        }
    }

    /// <summary>
    /// The exactly-once-per-statement slice of the before-statement checks: constraints whose
    /// call frequency is observable (statement counting, user-derived), the code-coverage counters and
    /// the debugger step hook.
    /// <para>
    /// Nothing calls this unless <see cref="EvaluationContext.ShouldRunPerStatementChecks"/> is set, which
    /// takes an exact constraint, debug mode or coverage collection. That is what keeps the coverage counting
    /// below free for every engine that did not ask for it: not a folded branch, an uncalled method.
    /// </para>
    /// </summary>
    internal void RunPerStatementChecks(StatementOrExpression? statement)
    {
        // Avoid allocating the enumerator because we run this loop very often.
        foreach (var constraint in _exactConstraints)
        {
            constraint.Check();
        }

        // After the constraints on purpose: a statement a constraint refused is a statement that never ran.
        if (_coverage is not null && statement is not null)
        {
            _coverage.Record(statement);
        }

        if (_isDebugMode && statement != null && statement.Type != NodeType.BlockStatement)
        {
            Debugger.OnStep(statement);
        }
    }

    /// <summary>
    /// Checks the constraints that only observe external state; callers are responsible for the
    /// amortization cadence (see <see cref="EvaluationContext.RunAmortizedConstraintChecks"/>).
    /// </summary>
    internal void CheckAmortizedConstraints()
    {
        if (_debuggerEvaluating)
        {
            // a watch / conditional-breakpoint expression evaluated while paused past the timeout
            // must not fail; the same exemption the host-boundary gate applies (see
            // CheckAmortizedConstraintsAtHostBoundary), extended to the per-statement cadence so a
            // longer debugger expression doesn't trip the countdown either
            return;
        }

        foreach (var constraint in _amortizedConstraints)
        {
            constraint.Check();
        }
    }

    /// <summary>
    /// Re-checks the amortized constraints when control returns from user CLR code to the
    /// interpreter. The per-statement amortization bounds timeout/cancellation detection latency
    /// in statement count, which tracks wall-clock time only while statements stay cheap; a single
    /// host call can take arbitrarily long, so interop call sites re-check after the host code
    /// returns, keeping detection latency bounded by one host call instead of
    /// <see cref="EvaluationContext.AmortizedConstraintCheckInterval"/> statements' worth of them.
    /// The boundaries of the mechanism are deliberate:
    /// <list type="bullet">
    /// <item>Only after the host call returns, never on entry — an entry check would block host
    /// cleanup calls (e.g. a release-the-lock delegate inside a JS finally block) once
    /// cancellation is pending, skipping cleanup that ran before this mechanism existed. Host
    /// calls that throw re-check inside their TargetInvocationException conversion handlers, so
    /// a loop of throwing host calls stays bounded too. The residual cost of no entry check: a
    /// cancelled script can start at most one more host call per boundary.</item>
    /// <item>Only while evaluation is in progress — signalled by the execution-context stack
    /// being deeper than the engine's base frame, which covers every execution shape (script
    /// evaluation, function calls, generator/async resumes, module evaluation, event-loop drains
    /// driven by host APIs such as UnwrapIfPromise) because each pushes a context. Outside that
    /// window constraint state is meaningless: <see cref="Constraints.TimeConstraint"/>'s timer
    /// is re-armed at the end of every run and keeps counting while the engine is idle, and a
    /// cancellation token may be cancelled during normal teardown, so host-initiated access to
    /// wrapped objects from C# must not observe either. Deliberately execution-context depth alone
    /// rather than <see cref="IsEvaluationInProgress"/>: a host <see cref="Call(JsValue, JsValue, JsCallArguments)"/>
    /// of a pure <see cref="Runtime.Interop.ClrFunction"/> raises <see cref="_hostEntryDepth"/>
    /// without any script being on the stack, and that is exactly the host-initiated access this
    /// gate must not fire for.</item>
    /// <item>Not during debugger expression evaluation (<see cref="Runtime.Debugger.DebugHandler.Evaluate(string, ScriptParsingOptions)"/>)
    /// — a debugger paused longer than the timeout would otherwise deterministically fail every
    /// interop read in watch/conditional-breakpoint expressions. Normal debug-mode execution
    /// keeps full enforcement.</item>
    /// <item>Result conversion runs before the check wherever a host call can return an
    /// awaitable, so an in-flight Task always gets its promise continuation attached and is
    /// never left unobserved.</item>
    /// <item>Not wired into built-in <see cref="ClrFunction"/> dispatch (would reintroduce the
    /// per-call cost on hot built-ins that the amortization removed; long-running built-ins bound
    /// their own latency via <see cref="ConstraintCheckInterval"/> self-checks), nor into
    /// user-supplied converter/factory callbacks — embedder-authored code hosting long operations
    /// can observe the CancellationToken itself.</item>
    /// </list>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckAmortizedConstraintsAtHostBoundary()
    {
        if (_executionContexts.Count > 1 && !_debuggerEvaluating)
        {
            CheckAmortizedConstraints();
        }
    }

    internal JsValue GetValue(object value)
    {
        return GetValue(value, false);
    }

    internal JsValue GetValue(object value, bool returnReferenceToPool)
    {
        if (value is JsValue jsValue)
        {
            return jsValue;
        }

        if (value is not Reference reference)
        {
            return ((Completion) value).Value;
        }

        return GetValue(reference, returnReferenceToPool);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getvalue
    /// </summary>
    internal JsValue GetValue(Reference reference, bool returnReferenceToPool)
    {
        var baseValue = reference.Base;

        if (reference.IsUnresolvableReference)
        {
            if (_resolverWatchesUnresolvable)
            {
                reference.EvaluateAndCachePropertyKey();
                if (_referenceResolver.TryUnresolvableReference(this, reference, out var val))
                {
                    // Same contract as the exits below: the caller handed over ownership of a rented
                    // Reference, so a claimed read has to hand it back too. The resolver only inspects
                    // the reference for the duration of the call — the declined path already recycles
                    // this very instance a few lines down.
                    if (returnReferenceToPool)
                    {
                        _referencePool.Return(reference);
                    }

                    return val;
                }
            }

            Throw.ReferenceError(Realm, reference);
        }

        if (_customResolver
            && (baseValue._type & InternalTypes.ObjectEnvironmentRecord) == InternalTypes.Empty
            && ResolverWatchesBase(baseValue))
        {
            reference.EvaluateAndCachePropertyKey();

            // Inline lane for the recognized NullPropagatingReferenceResolver, whose TryPropertyReference is
            // exactly "a nullish base is its own result". The key is evaluated first either way, so a property
            // key with an observable ToPrimitive behaves the same on both lanes.
            var claimed = _nullishPropagatesInline
                ? baseValue.IsNullOrUndefined()
                : _referenceResolver.TryPropertyReference(this, reference, ref baseValue);

            if (claimed)
            {
                if (returnReferenceToPool)
                {
                    _referencePool.Return(reference);
                }

                return baseValue;
            }
        }

        if (reference.IsPropertyReference)
        {
            var property = reference.ReferencedName;
            if (returnReferenceToPool)
            {
                _referencePool.Return(reference);
            }

            if (baseValue.IsNullOrUndefined())
            {
                ThrowPropertyNotFound(property, baseValue);
            }

            if (baseValue.IsObject())
            {
                var baseObj = (ObjectInstance) baseValue;

                if (reference.IsPrivateReference)
                {
                    return baseObj.PrivateGet((PrivateName) reference.ReferencedName);
                }

                // An object-base-watching resolver already evaluated the key above (the branch's guard is
                // exactly this branch's precondition), so the key is cached and this call would be pure
                // overhead; everything else still needs it.
                if (!_resolverWatchesObjectBase)
                {
                    reference.EvaluateAndCachePropertyKey();
                }

                var v = baseObj.Get(reference.ReferencedName, reference.ThisValue);
                return v;
            }

            // check if we are accessing a string, boxing operation can be costly to do index access
            // we have good chance to have fast path with integer or string indexer
            ObjectInstance? o = null;
            if ((property._type & (InternalTypes.String | InternalTypes.Integer)) != InternalTypes.Empty
                && baseValue is JsString s
                && TryHandleStringValue(property, s, ref o, out var jsValue))
            {
                return jsValue;
            }

            // Number / Boolean / BigInt primitives box to a wrapper that owns no properties, so the member
            // resolves entirely on the intrinsic prototype chain. Read straight from that prototype with the
            // primitive as the receiver (reference.ThisValue) — identical to ToObject(base).Get(...) but with
            // no wrapper allocated. Private references keep the boxing path so their "not declared" error and
            // PrivateGet semantics are preserved.
            if (o is null && !reference.IsPrivateReference)
            {
                o = Runtime.TypeConverter.PrimitiveLookupPrototypeOrNull(Realm, baseValue);
            }

            if (o is null)
            {
                o = Runtime.TypeConverter.ToObject(Realm, baseValue);
            }

            if (reference.IsPrivateReference)
            {
                return o.PrivateGet((PrivateName) reference.ReferencedName);
            }

            return o.Get(property, reference.ThisValue);
        }

        var record = (Environment) baseValue;

        // A reference built for an identifier already carries its name as a JsString, and the global
        // environment is the one record whose miss path needs exactly that — a name resolving on the
        // global's prototype reaches [[Get]] with a JsValue key on every read. Hand the reference's
        // own string over instead of letting each read build a second one; everything else keeps the
        // Key-only entry point it always used.
        var bindingValue = record is GlobalEnvironment globalEnvironment && reference.ReferencedName is JsString bindingName
            ? globalEnvironment.GetBindingValue(bindingName, reference.Strict)
            : record.GetBindingValue(reference.ReferencedName.ToString(), reference.Strict);

        if (returnReferenceToPool)
        {
            _referencePool.Return(reference);
        }

        return bindingValue;
    }

    /// <summary>
    /// Whether the registered reference resolver subscribed to property references with this kind of base.
    /// The base's kind is only known once it has been evaluated, so this is the runtime half of the
    /// build-time <see cref="_resolverWatchesValueBase"/> gate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ResolverWatchesBase(JsValue baseValue)
    {
        if (baseValue.IsNullOrUndefined())
        {
            return _resolverWatchesNullishBase;
        }

        return baseValue.IsObject() ? _resolverWatchesObjectBase : _resolverWatchesPrimitiveBase;
    }

    private void ThrowPropertyNotFound(JsValue property, JsValue baseValue)
    {
        // The property may carry a custom toString that throws, so it goes through the safe renderer.
        // baseValue does not: the only caller guards this with baseValue.IsNullOrUndefined(), so it is
        // always JsUndefined or JsNull, whose ToString() is a constant. Interpolating it keeps the
        // message the one every JavaScript developer recognizes -- "... of undefined", not "... of
        // Undefined", which is what rendering the type would produce.
        Throw.TypeError(Realm, $"Cannot read property '{Throw.SafeToDisplayString(property)}' of {baseValue}");
    }

    private bool TryHandleStringValue(JsValue property, JsString s, ref ObjectInstance? o, out JsValue jsValue)
    {
        if (CommonProperties.Length.Equals(property))
        {
            jsValue = JsNumber.Create((uint) s.Length);
            return true;
        }

        if (property is JsNumber number && number.IsInteger())
        {
            var index = number.AsInteger();
            if (index < 0 || index >= s.Length)
            {
                jsValue = JsValue.Undefined;
                return true;
            }

            jsValue = JsString.Create(s[index]);
            return true;
        }

        if (property is JsString { Length: > 0 } propertyString && char.IsLower(propertyString[0]))
        {
            // trying to find property that's always in prototype
            o = Realm.Intrinsics.String.PrototypeObject;
        }

        jsValue = JsValue.Undefined;
        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-putvalue
    /// </summary>
    internal void PutValue(Reference reference, JsValue value)
    {
        var property = reference.ReferencedName;
        if (reference.IsUnresolvableReference)
        {
            if (reference.Strict && property != CommonProperties.Arguments)
            {
                Throw.ReferenceError(Realm, reference);
            }

            // Step 2.c, Set(globalObj, V.[[ReferencedName]], W, false) — plus, when the global object
            // is Jint's own, the mutable-binding marking every other route that creates a global
            // binding does. A host that substituted its own global through Host.CreateGlobalObject
            // has no such binding storage to mark and just takes the [[Set]]. The cast is the
            // reference's own invariant: an unresolvable reference is only ever built for an
            // identifier that resolved on no environment — GetIdentifierReference below and the two
            // JintIdentifierExpression sites — and all three carry the name as a JsString, the
            // former through JsString.Create and the latter two through Environment.BindingName.Value.
            if (Realm.GlobalObject is GlobalObject globalObject)
            {
                globalObject.SetFromUnresolvableAssignment((JsString) property, value);
            }
            else
            {
                Realm.GlobalObject.Set(property, value, throwOnError: false);
            }
        }
        else if (reference.IsPropertyReference)
        {
            var baseObject = Runtime.TypeConverter.ToObject(Realm, reference.Base);
            if (reference.IsPrivateReference)
            {
                baseObject.PrivateSet((PrivateName) property, value);
                return;
            }

            reference.EvaluateAndCachePropertyKey();
            var succeeded = baseObject.Set(reference.ReferencedName, value, reference.ThisValue);
            if (!succeeded && reference.Strict)
            {
                // reference.ReferencedName is the already-coerced key; the `property` local still holds the
                // pre-coercion value, so quoting it would run a user toString a second time. The base is
                // rendered as its type because rendering the object itself would run one too.
                Throw.TypeError(Realm, $"Cannot assign to read only property '{reference.ReferencedName}' of {baseObject.Type}");
            }
        }
        else
        {
            ((Environment) reference.Base).SetMutableBinding(Runtime.TypeConverter.ToString(property), value, reference.Strict);
        }
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(string propertyName, params object?[] arguments)
    {
        return Invoke(propertyName, thisObj: null, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(string propertyName, object? thisObj, object?[] arguments)
    {
        using var ownership = EnterHostCall();
        var value = GetValue(propertyName);

        return Invoke(value, thisObj, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="value">The function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(JsValue value, params object?[] arguments)
    {
        return Invoke(value, thisObj: null, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="value">The function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(JsValue value, object? thisObj, object?[] arguments)
    {
        using var ownership = EnterHostCall();
        var callable = value as ICallable;
        if (callable is null)
        {
            Throw.JavaScriptException(Realm.Intrinsics.TypeError, "Can only invoke functions");
        }

        JsValue DoInvoke()
        {
            var items = _jsValueArrayPool.RentArray(arguments.Length);
            for (var i = 0; i < arguments.Length; ++i)
            {
                items[i] = JsValue.FromObject(this, arguments[i]);
            }

            // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!
            JsValue result;
            var thisObject = JsValue.FromObject(this, thisObj);
            if (callable is Function functionInstance)
            {
                var callStack = CallStack;
                callStack.Push(functionInstance, expression: null, ExecutionContext);
                try
                {
                    result = functionInstance is ScriptFunction scriptFunction
                        ? scriptFunction.CallWithStackFrame(thisObject, items)
                        : functionInstance.Call(thisObject, items);
                }
                finally
                {
                    // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
                    callStack.TryPop(out _);
                }
            }
            else
            {
                result = callable.Call(thisObject, items);
            }

            _jsValueArrayPool.ReturnArray(items);
            return result;
        }

        return ExecuteWithConstraints(Options.Strict, DoInvoke);
    }

    internal T ExecuteWithConstraints<T>(bool strict, Func<T> callback)
    {
        using var ownership = EnterHostCall();

        // A nested call — a host callback inside a running script calling back into the engine —
        // must not re-arm the outer run's constraints: the outer budget (time/statements) keeps
        // applying to everything the outer script triggers, otherwise `while (true) hostCallback()`
        // where the callback re-enters the engine would reset the timeout on every iteration and
        // run forever. Keyed on execution-context depth, the same signal the host-boundary
        // constraint gate uses.
        var isNested = _executionContexts.Count > 1;
        if (!isNested)
        {
            ResetConstraints();
        }

        _hostEntryDepth++;

        // Establish the requested strictness on the active (entry) execution context for the
        // callback, mirroring the former non-force StrictModeScope(strict): it can only raise
        // strictness for the window before an inner frame is pushed, never lower it (strict:false
        // is a no-op). Inner function/script/module frames push their own contexts carrying their
        // own strictness, so this governs only code that runs before such a push (e.g. argument
        // handling in Invoke, or the base global context itself). The base context pushed at
        // realm init is never popped, so a top context is always present.
        var previousStrict = strict && ReplaceTopStrict(true);
        try
        {
            return callback();
        }
        finally
        {
            if (strict)
            {
                ReplaceTopStrict(previousStrict);
            }
            _hostEntryDepth--;
            if (!isNested)
            {
                ResetConstraints();
            }
            _agent.ClearKeptObjects();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-invoke
    /// </summary>
    internal JsValue Invoke(JsValue v, JsValue p, JsCallArguments arguments)
    {
        _hostEntryDepth++;
        try
        {
            var func = GetV(v, p);
            var callable = func as ICallable;
            if (callable is null)
            {
                Throw.TypeErrorNoEngine("Can only invoke functions");
            }

            return callable.Call(v, arguments);
        }
        finally
        {
            _hostEntryDepth--;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getv
    /// </summary>
    private JsValue GetV(JsValue v, JsValue p)
    {
        var o = Runtime.TypeConverter.ToObject(Realm, v);
        return o.Get(p);
    }

    /// <summary>
    /// Gets a named value from the Global scope.
    /// </summary>
    /// <remarks>
    /// Convenience overload: every call allocates a <see cref="JsString"/> for
    /// <paramref name="propertyName"/>. A host that reads the same name repeatedly — once per request,
    /// once per evaluation, in a loop — should hoist that <see cref="JsString"/> once and use
    /// <see cref="GetValue(JsValue, JsValue)"/>, or read straight off <see cref="Global"/> with
    /// <c>engine.Global.Get(cachedName)</c>.
    /// </remarks>
    /// <param name="propertyName">The name of the property to return.</param>
    public JsValue GetValue(string propertyName)
    {
        using var ownership = EnterHostCall();
        return GetValue(Realm.GlobalObject, new JsString(propertyName));
    }

    /// <summary>
    /// Gets the last evaluated <see cref="Node"/>.
    /// </summary>
    internal Node GetLastSyntaxElement()
    {
        return _lastSyntaxElement!;
    }

    /// <summary>
    /// Gets a named value from the specified scope.
    /// </summary>
    /// <param name="scope">The scope to get the property from.</param>
    /// <param name="property">The name of the property to return.</param>
    public JsValue GetValue(JsValue scope, JsValue property)
    {
        using var ownership = EnterHostCall();
        var reference = _referencePool.Rent(scope, property, _isStrict, thisValue: null);
        var jsValue = GetValue(reference, returnReferenceToPool: false);
        _referencePool.Return(reference);
        return jsValue;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-resolvebinding
    /// </summary>
    internal Reference ResolveBinding(string name, Environment? env = null)
    {
        env ??= ExecutionContext.LexicalEnvironment;
        return GetIdentifierReference(env, name, ExecutionContext.Strict);
    }

    private static Reference GetIdentifierReference(Environment? env, string name, bool strict)
    {
        Key key = name;

        // Every exit from this loop hands the name to a Reference as a JsString, so build it once up
        // front rather than at each return. The global environment — always the last link of the chain
        // walked here — then gets it for free on the probe that needs a JsValue key: its own-property
        // miss falls through to [[HasProperty]] on the global's prototype chain, and a name that lives
        // there (an inherited accessor or non-writable data property, which assignment never shadows)
        // is probed again on every single evaluation.
        var nameValue = JsString.Create(name);
        while (true)
        {
            if (env is null)
            {
                return new Reference(Reference.Unresolvable, nameValue, strict);
            }

            var found = env is GlobalEnvironment globalEnvironment
                ? globalEnvironment.HasBinding(key, nameValue)
                : env.HasBinding(key);

            if (found)
            {
                return new Reference(env, nameValue, strict);
            }

            env = env._outerEnv;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getnewtarget
    /// </summary>
    internal JsValue GetNewTarget(Environment? thisEnvironment = null)
    {
        // we can take as argument if caller site has already determined the value, otherwise resolve
        thisEnvironment ??= ExecutionContext.GetThisEnvironment();
        return thisEnvironment.NewTarget ?? JsValue.Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-resolvethisbinding
    /// </summary>
    internal JsValue ResolveThisBinding()
    {
        var envRec = ExecutionContext.GetThisEnvironment();
        return envRec.GetThisBinding();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-globaldeclarationinstantiation
    /// </summary>
    private bool GlobalDeclarationInstantiation(
        Script script,
        GlobalEnvironment env)
    {
        var hoistingScope = script.GetHoistingScope();
        var functionDeclarations = hoistingScope._functionDeclarations;

        var functionToInitialize = new List<JintFunctionDefinition>();
        var declaredFunctionNames = new HashSet<Key>();
        var declaredVarNames = new List<Key>();

        var realm = Realm;

        // The definition / statement-list caches pay only when the same script runs on this engine
        // again (the cached-Prepared<Script> embedding pattern); a first evaluation - fresh-engine-
        // per-run hosts never see a second one - takes plain construction so one-shot scripts stay
        // byte-identical to the uncached path.
        var reEvaluation = !_evaluatedScripts.Add(script);

        if (functionDeclarations != null)
        {
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                var fn = (Key) d.Id!.Name;
                if (!declaredFunctionNames.Contains(fn))
                {
                    var fnDefinable = env.CanDeclareGlobalFunction(fn);
                    if (!fnDefinable)
                    {
                        Throw.TypeError(realm, "Cannot declare global function " + fn);
                    }

                    declaredFunctionNames.Add(fn);
                    functionToInitialize.Add(reEvaluation ? GetOrCreateFunctionDefinition(d) : new JintFunctionDefinition(d));
                }
            }
        }

        var varNames = script.GetVarNames(hoistingScope);
        for (var j = 0; j < varNames.Count; j++)
        {
            var vn = varNames[j];
            if (env.HasLexicalDeclaration(vn))
            {
                Throw.SyntaxError(realm, $"Identifier '{vn}' has already been declared");
            }

            if (!declaredFunctionNames.Contains(vn))
            {
                var vnDefinable = env.CanDeclareGlobalVar(vn);
                if (!vnDefinable)
                {
                    Throw.TypeError(realm, $"Cannot define global variable '{vn}'");
                }

                declaredVarNames.Add(vn);
            }
        }

        PrivateEnvironment? privateEnv = null;
        var lexNames = script.GetLexNames(hoistingScope);
        for (var i = 0; i < lexNames.Count; i++)
        {
            var declaration = lexNames[i];
            foreach (var dn in declaration.BoundNames)
            {
                if (env.HasLexicalDeclaration(dn) || env.HasRestrictedGlobalProperty(dn))
                {
                    Throw.SyntaxError(realm, $"Identifier '{dn}' has already been declared");
                }

                if (declaration.IsConstantDeclaration)
                {
                    env.CreateImmutableBinding(dn, strict: true);
                }
                else
                {
                    env.CreateMutableBinding(dn, canBeDeleted: false);
                }
            }
        }

        // we need to go through in reverse order to handle the hoisting correctly
        for (var i = functionToInitialize.Count - 1; i > -1; i--)
        {
            var f = functionToInitialize[i];
            Key fn = f.Name!;

            if (env.HasLexicalDeclaration(fn))
            {
                Throw.SyntaxError(realm, $"Identifier '{fn}' has already been declared");
            }

            var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, env, privateEnv);
            env.CreateGlobalFunctionBinding(fn, fo, canBeDeleted: false);
        }

        env.CreateGlobalVarBindings(declaredVarNames, canBeDeleted: false);

        // B.3.2: Block-level function declarations in sloppy mode get var bindings at global scope
        if (!_isStrict && !script.Strict)
        {
            var annexBFunctions = hoistingScope._annexBFunctionDeclarations;
            if (annexBFunctions != null)
            {
                for (var i = 0; i < annexBFunctions.Count; i++)
                {
                    var f = annexBFunctions[i];
                    var fn = (Key) f.Id!.Name;
                    if (!env.HasLexicalDeclaration(fn)
                        && env.CanDeclareGlobalFunction(fn)
                        && !declaredFunctionNames.Contains(fn))
                    {
                        env.CreateGlobalVarBinding(fn, canBeDeleted: false);
                    }
                }
            }
        }

        return reEvaluation;
    }

    /// <summary>
    /// The <see cref="JintFunctionDefinition.State.CanUseFastFDI"/> arm of
    /// <see cref="FunctionDeclarationInstantiation"/>, generic over where the argument values live.
    /// Callers must have established the same gate the general method tests — <c>CanUseFastFDI</c>
    /// and <c>!_isDebugMode</c> — which is also what makes the arguments-object return value
    /// unnecessary here: fixed slots require <c>!ArgumentsObjectNeeded</c>, so there is never one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void FunctionDeclarationInstantiationFast<TArgs>(JintFunctionDefinition.State configuration, in TArgs argumentsList)
        where TArgs : struct, IArgumentSource
    {
        var fastEnv = (FunctionEnvironment) ExecutionContext.LexicalEnvironment;
        fastEnv.InitializeParametersFast(configuration.ParameterNames, in argumentsList);

        // Stamp every non-parameter slot from the template: undefined for a hoisted var, the temporal
        // dead zone with the declaration's mutability for a top-level let/const. One unconditional
        // copy for every fixed-slot function — see State.NonParameterSlotTemplate for why the arm has
        // no branch of its own to select.
        var slots = fastEnv._slots!;
        var template = configuration.NonParameterSlotTemplate!;
        var paramCount = configuration.ParameterSlotCount;
        for (var i = 0; i < template.Length; i++)
        {
            slots[paramCount + i] = template[i];
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-functiondeclarationinstantiation
    /// </summary>
    internal JsArguments? FunctionDeclarationInstantiation(
        EvaluationContext context,
        Function function,
        JsCallArguments argumentsList,
        JintFunctionDefinition.State? state = null)
    {
        var func = function._functionDefinition!;
        var configuration = state ?? func.Initialize();

        // Fastest path: nothing to instantiate at all (no parameters, vars, lexical declarations,
        // inner function declarations, arguments object or eval context).
        if (configuration.CanUseEmptyFDI && !_isDebugMode)
        {
            return null;
        }

        // Fast path for simple functions with fixed-slot storage
        if (configuration.CanUseFastFDI && !_isDebugMode)
        {
            var fastEnv = (FunctionEnvironment) ExecutionContext.LexicalEnvironment;
            fastEnv.InitializeParameters(configuration.ParameterNames, false, argumentsList);

            // Stamp every non-parameter slot from the template (see FunctionDeclarationInstantiationFast).
            var slots = fastEnv._slots!;
            var template = configuration.NonParameterSlotTemplate!;
            var paramCount = configuration.ParameterSlotCount;
            for (var i = 0; i < template.Length; i++)
            {
                slots[paramCount + i] = template[i];
            }

            return null;
        }

        var calleeContext = ExecutionContext;

        var env = (FunctionEnvironment) calleeContext.LexicalEnvironment;
        var strict = _isStrict || calleeContext.Strict;

        var parameterNames = configuration.ParameterNames;
        var hasDuplicates = configuration.HasDuplicates;
        var simpleParameterList = configuration.IsSimpleParameterList;
        var hasParameterExpressions = configuration.HasParameterExpressions;
        if (configuration.RequiresInputArgumentsOwnership)
        {
            argumentsList = [.. argumentsList];
        }

        var canInitializeParametersOnDeclaration = simpleParameterList && !configuration.HasDuplicates;
        var arguments = canInitializeParametersOnDeclaration ? argumentsList : null;
        env.InitializeParameters(parameterNames, hasDuplicates, arguments);

        JsArguments? ao = null;
        if (configuration.ArgumentsObjectNeeded || _isDebugMode)
        {
            if (strict || !simpleParameterList)
            {
                ao = CreateUnmappedArgumentsObject(argumentsList);
            }
            else
            {
                // NOTE: mapped argument object is only provided for non-strict functions that don't have a rest parameter,
                // any parameter default value initializers, or any destructured parameters — all three make the
                // parameter list non-simple, which is what the arm above already selected on.
                ao = CreateMappedArgumentsObject(function, parameterNames, argumentsList, env);
            }

            if (strict)
            {
                env.CreateImmutableBindingAndInitialize(KnownKeys.Arguments, strict: false, ao, DisposeHint.Normal);
            }
            else
            {
                env.CreateMutableBindingAndInitialize(KnownKeys.Arguments, canBeDeleted: false, ao, DisposeHint.Normal);
            }
        }

        // Per ECMAScript spec 10.2.11:
        // When hasParameterExpressions is true, we need to create varEnv BEFORE evaluating parameter defaults
        // so that eval called during parameter initialization uses the correct VariableEnvironment.
        // This ensures:
        // 1. Vars declared by eval in parameter expressions go to the right varEnv
        // 2. EvalDeclarationInstantiation can detect conflicts between eval vars and parameter names
        DeclarativeEnvironment varEnv;
        if (!hasParameterExpressions)
        {
            // NOTE: Only a single lexical environment is needed for the parameters and top-level vars.
            var varsToInitialize = configuration.VarsToInitialize!;
            if (env._slots is not null)
            {
                // Fast path: vars go directly into pre-allocated slots
                var paramCount = configuration.ParameterSlotCount;
                for (var i = 0; i < varsToInitialize.Count; i++)
                {
                    env._slots[paramCount + i] = new Binding(
                        JsValue.Undefined, canBeDeleted: false, mutable: true, strict: false);
                }
            }
            else if (varsToInitialize.Count > 0)
            {
                // Inlined bulk form of CreateMutableBindingAndInitialize (no slots, DisposeHint.Normal):
                // pre-size once so a declaration-heavy body doesn't cut the list over to a dictionary
                // insert by insert on every call (mirrors the lexical-declarations path below).
                var dictionary = env._dictionary ??= new HybridDictionary<Binding>(varsToInitialize.Count, checkExistingKeys: true);
                dictionary.EnsureCapacity(dictionary.Count + varsToInitialize.Count);
                for (var i = 0; i < varsToInitialize.Count; i++)
                {
                    dictionary[varsToInitialize[i].Name] = new Binding(JsValue.Undefined, canBeDeleted: false, mutable: true, strict: false);
                }
            }

            varEnv = env;
        }
        else
        {
            // Per ECMAScript spec 10.2.11 step 20:
            // Create a separate environment (paramEnv) for parameter evaluation.
            // This is where eval'd vars during parameter initialization go.
            // Closures created during parameter evaluation capture this environment.
            var paramEnv = JintEnvironment.NewDeclarativeEnvironment(this, env);

            // Step 20.f and 20.g: Set BOTH VariableEnvironment AND LexicalEnvironment to paramEnv
            // before evaluating parameter defaults. This ensures:
            // 1. Vars declared by eval in parameter expressions go to paramEnv
            // 2. Closures created during parameter evaluation capture paramEnv
            UpdateVariableEnvironment(paramEnv);
            UpdateLexicalEnvironment(paramEnv);

            // Per ECMAScript spec step 27-28:
            // Create another separate environment (varEnv) for function body vars.
            // This ensures closures in params do NOT have visibility of body declarations.
            varEnv = JintEnvironment.NewDeclarativeEnvironment(this, paramEnv);
        }

        if (!canInitializeParametersOnDeclaration)
        {
            // Slower path - evaluate parameter defaults.
            // At this point, if hasParameterExpressions:
            // - VariableEnvironment = paramEnv (for eval vars during param init)
            // - LexicalEnvironment = paramEnv (for closures to capture)
            env.AddFunctionParameters(context, func.Function, argumentsList);
        }

        // After parameter initialization, switch VariableEnvironment to varEnv for body vars
        // Per ECMAScript spec step 27-28
        if (hasParameterExpressions)
        {
            UpdateVariableEnvironment(varEnv);
        }

        // Let iteratorRecord be CreateListIteratorRecord(argumentsList).
        // If hasDuplicates is true, then
        //     Perform ? IteratorBindingInitialization for formals with iteratorRecord and undefined as arguments.
        // Else,
        //     Perform ? IteratorBindingInitialization for formals with iteratorRecord and env as arguments.

        // Now initialize var bindings in varEnv (after parameters are evaluated)
        if (hasParameterExpressions)
        {
            var varsToInitialize = configuration.VarsToInitialize!;
            for (var i = 0; i < varsToInitialize.Count; i++)
            {
                var pair = varsToInitialize[i];
                var initialValue = pair.InitialValue ?? env.GetBindingValue(pair.Name, strict: false);
                varEnv.CreateMutableBindingAndInitialize(pair.Name, canBeDeleted: false, initialValue, DisposeHint.Normal);
            }
        }

        // NOTE: Annex B.3.3.1 adds additional steps at this point.
        // A https://tc39.es/ecma262/#sec-web-compat-functiondeclarationinstantiation

        DeclarativeEnvironment lexEnv;
        if (configuration.NeedsEvalContext || _isDebugMode)
        {
            lexEnv = JintEnvironment.NewDeclarativeEnvironment(this, varEnv);
            // NOTE: Non-strict functions use a separate lexical Environment Record for top-level lexical declarations
            // so that a direct eval can determine whether any var scoped declarations introduced by the eval code conflict
            // with pre-existing top-level lexically scoped declarations. This is not needed for strict functions
            // because a strict direct eval always places all declarations into a new Environment Record.
        }
        else
        {
            lexEnv = varEnv;
        }

        UpdateLexicalEnvironment(lexEnv);

        var declarations = configuration.LexicalDeclarations;
        if (declarations?.Declarations.Count > 0)
        {
            if (lexEnv._slots is not null)
            {
                // Fast path: lexical bindings go directly into pre-allocated slots
                var lexOffset = configuration.ParameterSlotCount + configuration.VarSlotCount;
                var lexicalDeclarations = declarations.Value.Declarations;
                for (var i = 0; i < lexicalDeclarations.Count; i++)
                {
                    var declaration = lexicalDeclarations[i];
                    foreach (var bn in declaration.BoundNames)
                    {
                        lexEnv._slots[lexOffset++] = declaration.IsConstantDeclaration
                            ? new Binding(null!, canBeDeleted: false, mutable: false, strict: true)
                            : new Binding(null!, canBeDeleted: false, mutable: true, strict: false);
                    }
                }
            }
            else
            {
                var lexicalDeclarations = declarations.Value.Declarations;
                var checkExistingKeys = (lexEnv._dictionary is not null && lexEnv._dictionary.Count > 0) || !declarations.Value.AllLexicalScoped;
                var dictionary = lexEnv._dictionary ??= new HybridDictionary<Binding>(lexicalDeclarations.Count, checkExistingKeys);
                dictionary.EnsureCapacity(dictionary.Count + lexicalDeclarations.Count);

                for (var i = 0; i < lexicalDeclarations.Count; i++)
                {
                    var declaration = lexicalDeclarations[i];
                    foreach (var bn in declaration.BoundNames)
                    {
                        if (declaration.IsConstantDeclaration)
                        {
                            dictionary.CreateImmutableBinding(bn, strict);
                        }
                        else
                        {
                            dictionary.CreateMutableBinding(bn, canBeDeleted: false);
                        }
                    }
                }

                dictionary.CheckExistingKeys = true;
            }
        }

        if (configuration.FunctionsToInitialize != null)
        {
            var privateEnv = calleeContext.PrivateEnvironment;
            var intrinsicFunction = Realm.Intrinsics.Function;
            foreach (var f in configuration.FunctionsToInitialize)
            {
                var jintFunctionDefinition = GetOrCreateFunctionDefinition(f.Declaration);
                var fo = intrinsicFunction.InstantiateFunctionObject(jintFunctionDefinition, lexEnv, privateEnv);
                varEnv.SetMutableBinding(f.Name, fo, strict: false);
            }
        }

        return ao;
    }

    private JsArguments CreateMappedArgumentsObject(
        Function func,
        Key[] formals,
        JsCallArguments argumentsList,
        DeclarativeEnvironment envRec)
    {
        return _argumentsInstancePool.Rent(func, formals, argumentsList, envRec);
    }

    private JsArguments CreateUnmappedArgumentsObject(JsCallArguments argumentsList)
    {
        return _argumentsInstancePool.Rent(argumentsList);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-evaldeclarationinstantiation
    /// </summary>
    internal void EvalDeclarationInstantiation(
        Script script,
        HoistingScope hoistingScope,
        Environment varEnv,
        Environment lexEnv,
        PrivateEnvironment? privateEnv,
        bool strict,
        bool bindingsPreInitialized = false)
    {
        var lexEnvRec = (DeclarativeEnvironment) lexEnv;
        var varEnvRec = varEnv;

        var realm = Realm;

        if (!strict)
        {
            // Sloppy direct eval hoists vars/functions into the CALLER's pre-existing variable
            // environment — the one mutation that invalidates pinned nested-global chain memos.
            _envBindingInjectionEpoch++;
        }

        if (!strict && hoistingScope._variablesDeclarations != null)
        {
            void ThrowDuplicateBindingError(Key name) => Throw.SyntaxError(realm, $"Identifier '{name}' has already been declared");

            var varNames = script.GetVarNames(hoistingScope);
            if (varEnvRec is GlobalEnvironment globalEnvironmentRecord)
            {
                for (var i = 0; i < varNames.Count; i++)
                {
                    var varName = varNames[i];
                    if (globalEnvironmentRecord.HasLexicalDeclaration(varName))
                    {
                        ThrowDuplicateBindingError(varName);
                    }
                }
            }

            var thisLex = lexEnv;
            while (thisLex is not null && !ReferenceEquals(thisLex, varEnv))
            {
                var thisEnvRec = thisLex;
                // B.3.5: Skip catch clause environments - eval'd var/function declarations
                // are allowed to shadow catch parameters in non-strict mode
                if (thisEnvRec is not ObjectEnvironment && thisEnvRec is not DeclarativeEnvironment { _catchEnvironment: true })
                {
                    for (var i = 0; i < varNames.Count; i++)
                    {
                        var varName = varNames[i];
                        if (thisEnvRec.HasBinding(varName))
                        {
                            ThrowDuplicateBindingError(varName);
                        }
                    }
                }

                thisLex = thisLex._outerEnv;
            }

            // When varEnv is a separate parameter environment (hasParameterExpressions case),
            // we also need to check varEnv's outer for parameter bindings.
            // This handles the case where parameters are in the FunctionEnvironment (varEnv's outer)
            // but eval vars go to varEnv (the separate parameter environment).
            // The while loop above walks from lexEnv to varEnv, but doesn't check varEnv's outer.
            // Note: We only do this when varEnv is NOT a FunctionEnvironment - if it is, then
            // this is a simple function without hasParameterExpressions and we don't need this check.
            if (varEnv is DeclarativeEnvironment and not FunctionEnvironment && varEnv._outerEnv is FunctionEnvironment funcEnv)
            {
                for (var i = 0; i < varNames.Count; i++)
                {
                    var varName = varNames[i];
                    if (funcEnv.HasBinding(varName))
                    {
                        ThrowDuplicateBindingError(varName);
                    }

                    // Non-arrow functions always have an implicit "arguments" binding per spec
                    // (10.2.11 FunctionDeclarationInstantiation steps 17-21), even when the
                    // arguments object creation was optimized away because the function body
                    // doesn't reference "arguments". Detect this implicit binding conflict.
                    if (string.Equals(varName.Name, "arguments", StringComparison.Ordinal)
                        && funcEnv._functionObject._thisMode != FunctionThisMode.Lexical)
                    {
                        ThrowDuplicateBindingError(varName);
                    }
                }
            }
        }

        HashSet<PrivateIdentifier>? privateIdentifiers = null;
        var pointer = privateEnv;
        while (pointer is not null)
        {
            foreach (var name in pointer.Names)
            {
                privateIdentifiers ??= new HashSet<PrivateIdentifier>(PrivateIdentifierNameComparer._instance);
                privateIdentifiers.Add(name.Key);
            }

            pointer = pointer.OuterPrivateEnvironment;
        }

        script.AllPrivateIdentifiersValid(realm, privateIdentifiers);

        var functionDeclarations = hoistingScope._functionDeclarations;
        LinkedList<JintFunctionDefinition>? functionsToInitialize = null;
        HashSet<Key>? declaredFunctionNames = null;

        if (functionDeclarations != null)
        {
            functionsToInitialize = new LinkedList<JintFunctionDefinition>();
            declaredFunctionNames = new HashSet<Key>();
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                Key fn = d.Id!.Name;
                if (!declaredFunctionNames.Contains(fn))
                {
                    if (varEnvRec is GlobalEnvironment ger)
                    {
                        var fnDefinable = ger.CanDeclareGlobalFunction(fn);
                        if (!fnDefinable)
                        {
                            Throw.TypeError(realm, $"Cannot define global function '{fn}'");
                        }
                    }

                    declaredFunctionNames.Add(fn);
                    functionsToInitialize.AddFirst(GetOrCreateFunctionDefinition(d));
                }
            }
        }

        // When the caller pre-initialized the environment from a slot template (strict eval with
        // a cached layout), the var and lexical bindings already exist in their post-instantiation
        // state, so the gathering and creation passes below are skipped entirely.
        List<Key>? declaredVarNames = null;
        if (!bindingsPreInitialized)
        {
            var boundNames = new List<Key>();
            declaredVarNames = new List<Key>();
            var variableDeclarations = hoistingScope._variablesDeclarations;
            var variableDeclarationsCount = variableDeclarations?.Count;
            for (var i = 0; i < variableDeclarationsCount; i++)
            {
                var variableDeclaration = variableDeclarations![i];
                boundNames.Clear();
                variableDeclaration.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    var vn = boundNames[j];
                    if (declaredFunctionNames is null || !declaredFunctionNames.Contains(vn))
                    {
                        if (varEnvRec is GlobalEnvironment ger)
                        {
                            // https://tc39.es/ecma262/#sec-evaldeclarationinstantiation step 8.a.ii.1
                            var vnDefinable = ger.CanDeclareGlobalVar(vn);
                            if (!vnDefinable)
                            {
                                Throw.TypeError(realm, $"Cannot define global variable '{vn}'");
                            }
                        }

                        declaredVarNames.Add(vn);
                    }
                }
            }

            var lexicalDeclarations = hoistingScope._lexicalDeclarations;
            var lexicalDeclarationsCount = lexicalDeclarations?.Count;
            for (var i = 0; i < lexicalDeclarationsCount; i++)
            {
                boundNames.Clear();
                var d = lexicalDeclarations![i];
                d.GetBoundNames(boundNames);
                for (var j = 0; j < boundNames.Count; j++)
                {
                    Key dn = boundNames[j];
                    if (d.IsConstantDeclaration())
                    {
                        lexEnvRec.CreateImmutableBinding(dn, strict: true);
                    }
                    else
                    {
                        lexEnvRec.CreateMutableBinding(dn, canBeDeleted: false);
                    }
                }
            }
        }

        if (functionsToInitialize != null)
        {
            foreach (var f in functionsToInitialize)
            {
                var fo = realm.Intrinsics.Function.InstantiateFunctionObject(f, lexEnv, privateEnv);
                if (varEnvRec is GlobalEnvironment ger)
                {
                    ger.CreateGlobalFunctionBinding(f.Name!, fo, canBeDeleted: true);
                }
                else
                {
                    Key fn = f.Name!;
                    var bindingExists = varEnvRec.HasBinding(fn);
                    if (!bindingExists)
                    {
                        varEnvRec.CreateMutableBinding(fn, canBeDeleted: true);
                        varEnvRec.InitializeBinding(fn, fo, DisposeHint.Normal);
                    }
                    else
                    {
                        varEnvRec.SetMutableBinding(fn, fo, strict: false);
                    }
                }
            }
        }

        if (declaredVarNames != null)
        {
            foreach (var vn in declaredVarNames)
            {
                if (varEnvRec is GlobalEnvironment ger)
                {
                    ger.CreateGlobalVarBinding(vn, canBeDeleted: true);
                }
                else
                {
                    var bindingExists = varEnvRec.HasBinding(vn);
                    if (!bindingExists)
                    {
                        varEnvRec.CreateMutableBinding(vn, canBeDeleted: true);
                        varEnvRec.InitializeBinding(vn, JsValue.Undefined, DisposeHint.Normal);
                    }
                }
            }
        }

        // B.3.3: Block-level function declarations in sloppy eval get var bindings
        if (!strict)
        {
            var annexBFunctions = hoistingScope._annexBFunctionDeclarations;
            if (annexBFunctions != null)
            {
                for (var i = 0; i < annexBFunctions.Count; i++)
                {
                    var f = annexBFunctions[i];
                    Key fn = f.Id!.Name;
                    if (declaredFunctionNames is null || !declaredFunctionNames.Contains(fn))
                    {
                        if (varEnvRec is GlobalEnvironment ger)
                        {
                            if (!ger.HasLexicalDeclaration(fn) && ger.CanDeclareGlobalFunction(fn))
                            {
                                // B.3.3.3: CreateGlobalVarBinding only creates if binding doesn't exist,
                                // preserving existing property attributes and value.
                                ger.CreateGlobalVarBinding(fn, canBeDeleted: true);
                            }
                        }
                        else
                        {
                            var bindingExists = varEnvRec.HasBinding(fn);
                            if (!bindingExists)
                            {
                                varEnvRec.CreateMutableBinding(fn, canBeDeleted: true);
                                varEnvRec.InitializeBinding(fn, JsValue.Undefined, DisposeHint.Normal);
                            }
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateLexicalEnvironment(Environment newEnv)
    {
        _executionContexts.ReplaceTopLexicalEnvironment(newEnv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateVariableEnvironment(Environment newEnv)
    {
        _executionContexts.ReplaceTopVariableEnvironment(newEnv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdatePrivateEnvironment(PrivateEnvironment? newEnv)
    {
        _executionContexts.ReplaceTopPrivateEnvironment(newEnv);
    }

    /// <summary>
    /// Overrides the strictness of the currently active execution context and returns the previous
    /// value. Used only where strict-mode code runs without pushing its own context (a class body
    /// evaluated in the enclosing context - see ClassDefinition); the caller must restore the
    /// returned value in a finally.
    /// </summary>
    internal bool ReplaceTopStrict(bool strict)
    {
        return _executionContexts.ReplaceTopStrict(strict);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly ExecutionContext UpdateGenerator(GeneratorInstance generator)
    {
        return ref _executionContexts.ReplaceTopGenerator(generator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly ExecutionContext UpdateAsyncGenerator(Native.AsyncGenerator.AsyncGeneratorInstance asyncGenerator)
    {
        return ref _executionContexts.ReplaceTopAsyncGenerator(asyncGenerator);
    }

    /// <summary>
    /// Invokes the named callable and returns the resulting object.
    /// </summary>
    /// <param name="callableName">The name of the callable.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public JsValue Call(string callableName, params JsCallArguments arguments)
    {
        using var ownership = EnterHostCall();
        var callable = Evaluate(callableName);
        return Call(callable, arguments);
    }

    /// <summary>
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <param name="callable">The callable.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public JsValue Call(JsValue callable, params JsCallArguments arguments)
        => Call(callable, thisObject: JsValue.Undefined, arguments);

    /// <summary>
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <param name="callable">The callable.</param>
    /// <param name="thisObject">Value bound as this.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public JsValue Call(JsValue callable, JsValue thisObject, JsCallArguments arguments)
    {
        JsValue Callback()
        {
            if (!callable.IsCallable)
            {
                Throw.ArgumentException($"{callable.Type} is not callable");
            }

            return Call((ICallable) callable, thisObject, arguments, null);
        }

        return ExecuteWithConstraints(Options.Strict, Callback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue Call(ICallable callable, JsValue thisObject, JsCallArguments arguments, JintExpression? expression)
    {
        if (callable is Function functionInstance)
        {
            return Call(functionInstance, thisObject, arguments, expression);
        }

        return callable.Call(thisObject, arguments);
    }

    /// <summary>
    /// Calls the named constructor and returns the resulting object.
    /// </summary>
    /// <param name="constructorName">The name of the constructor to call.</param>
    /// <param name="arguments">The arguments of the constructor call.</param>
    /// <returns>The value returned by the constructor call.</returns>
    public ObjectInstance Construct(string constructorName, params JsCallArguments arguments)
    {
        using var ownership = EnterHostCall();
        var constructor = Evaluate(constructorName);
        return Construct(constructor, arguments);
    }

    /// <summary>
    /// Calls the constructor and returns the resulting object.
    /// </summary>
    /// <param name="constructor">The name of the constructor to call.</param>
    /// <param name="arguments">The arguments of the constructor call.</param>
    /// <returns>The value returned by the constructor call.</returns>
    public ObjectInstance Construct(JsValue constructor, params JsCallArguments arguments)
    {
        ObjectInstance Callback()
        {
            if (!constructor.IsConstructor)
            {
                Throw.ArgumentException($"{constructor.Type} is not a constructor");
            }

            return Construct(constructor, arguments, constructor, null);
        }

        return ExecuteWithConstraints(Options.Strict, Callback);
    }

    internal ObjectInstance Construct(
        JsValue constructor,
        JsCallArguments arguments,
        JsValue newTarget,
        JintExpression? expression)
    {
        if (constructor is Function functionInstance)
        {
            return Construct(functionInstance, arguments, newTarget, expression);
        }

        return ((IConstructor) constructor).Construct(arguments, newTarget);
    }

    internal JsValue Call(Function function, JsValue thisObject)
        => Call(function, thisObject, Arguments.Empty, null);

    internal JsValue Call(
        Function function,
        JsValue thisObject,
        JsCallArguments arguments,
        JintExpression? expression)
    {
        // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!

        var recursionDepth = CallStack.Push(function, expression, ExecutionContext);

        if (recursionDepth > _maxRecursionDepth)
        {
            // automatically pops the current element as it was never reached
            Throw.RecursionDepthOverflowException(CallStack);
        }

        JsValue result;
        try
        {
            result = function is ScriptFunction scriptFunction
                ? scriptFunction.CallWithStackFrame(thisObject, arguments)
                : function.Call(thisObject, arguments);
        }
        finally
        {
            // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
            CallStack.TryPop(out _);
        }

        return result;
    }

    private ObjectInstance Construct(
        Function function,
        JsCallArguments arguments,
        JsValue newTarget,
        JintExpression? expression)
    {
        // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!

        var recursionDepth = CallStack.Push(function, expression, ExecutionContext);

        if (recursionDepth > _maxRecursionDepth)
        {
            // automatically pops the current element as it was never reached
            Throw.RecursionDepthOverflowException(CallStack);
        }

        ObjectInstance result;
        try
        {
            result = function is ScriptFunction scriptFunction
                ? scriptFunction.ConstructWithStackFrame(arguments, newTarget)
                : ((IConstructor) function).Construct(arguments, newTarget);
        }
        finally
        {
            // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
            CallStack.TryPop(out _);
        }

        return result;
    }

    internal void SignalError(ErrorDispatchInfo error)
    {
        _error = error;
    }

    internal void RegisterTypeReference(TypeReference reference)
    {
        _typeReferences ??= new Dictionary<Type, TypeReference>();
        _typeReferences[reference.ReferenceType] = reference;
    }

    internal ref readonly ExecutionContext GetExecutionContext(int fromTop)
    {
        return ref _executionContexts.Peek(fromTop);
    }

    public void Dispose()
    {
        using var ownership = EnterHostCall();

        // the recent-wrapper ring (on by default since 4.14) strongly roots its targets and wrappers,
        // so a disposed-but-still-referenced engine must release them
        _recentObjectWrapperCache?.Clear();
        _recentObjectWrapperCache = null;

#if NET8_0_OR_GREATER
        // A host CancellationToken bridged to an AbortSignal holds a registration that reaches this engine, and
        // the token may well outlive it — an application-lifetime token would otherwise keep every engine it
        // was ever handed to reachable.
        _webApi?.Dispose();
#endif

        if (_objectWrapperCache is null)
        {
            return;
        }

#if SUPPORTS_WEAK_TABLE_CLEAR
        _objectWrapperCache.Clear();
#else
        // we can expect that reflection is OK as we've been generating object wrappers already
        var clearMethod = _objectWrapperCache.GetType().GetMethod("Clear", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        clearMethod?.Invoke(_objectWrapperCache, []);
#endif

    }

    [DebuggerDisplay("Engine")]
    private sealed class EngineDebugView
    {
        private readonly Engine _engine;

        public EngineDebugView(Engine engine)
        {
            _engine = engine;
        }

        public ObjectInstance Globals => _engine.Realm.GlobalObject;
        public Options Options => _engine.Options;

        public Environment VariableEnvironment => _engine.ExecutionContext.VariableEnvironment;
        public Environment LexicalEnvironment => _engine.ExecutionContext.LexicalEnvironment;
    }
}
