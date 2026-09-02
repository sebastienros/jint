using System.Collections.Concurrent;
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

    private readonly JintParser _defaultParser;
    private readonly ParserOptions _defaultParserOptions;
    private readonly ParsingConstraints _parsingConstraints;
    internal ParsingConstraints? _parsingConstraintsOverride;
    internal ParserOptions? _parserOptionsOverride;
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
    /// Built after <see cref="Options.Apply"/>, with every field it snapshots — debug mode, coverage, the
    /// constraint partition — read from options the host's <c>options.Configure(e =&gt; …)</c> callbacks have
    /// finished with. Those callbacks cannot execute script, which is what makes that ordering available.
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

    // A reservation taken for one host call that outlives it: see SuspendHostCallForCallbacks. Written only
    // by the thread that owns the engine, and released when the scope that claimed the engine for that
    // entry unwinds - HostCallScope's entry-root flag, plus this thread id, is what keeps a callback taking
    // its turn under the reservation from releasing it out from under the entry.
    private object? _entryReservation;
    private int _entryReservationThreadId;
    private int _hostReentryThreadId;
    private MemoryLimitConstraint.OperationState? _transferredMemoryState;
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
        return new HostCallScope(this, callbackOwner, isEntryRoot: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal HostCallScope? EnterHostCallIfNeeded()
    {
        return Volatile.Read(ref _ownerThreadId) == System.Environment.CurrentManagedThreadId
            ? null
            : EnterHostCall();
    }

    internal HostCallScope EnterHostCallback()
        => EnterHostCall();

    /// <summary>
    /// The host-contract verifier for <b>thread affinity</b>: reports engine-affine state built on a thread
    /// while a <em>different</em> thread is inside this engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Jint guards <em>operations</em> against concurrent use and does not guard <em>value construction</em>:
    /// <see cref="Native.JsObject.Create(Engine, Native.JsObjectLayout, ReadOnlySpan{JsValue})"/>,
    /// <see cref="Native.JsObject.CreateFromEntries(Engine, ReadOnlySpan{KeyValuePair{string, JsValue}})"/>
    /// and the <see cref="Native.JsArray"/> constructors are per-object APIs on a bulk path, and an always-on
    /// <see cref="EnterHostCall"/> there would be paid by every host that never got this wrong. This check is
    /// where that line is enforced instead — folded out entirely in a process that never set the switch, and
    /// exact in one that did.
    /// </para>
    /// <para>
    /// It is deliberately the narrowest question that has no false positives: <em>another thread owns this
    /// engine right now, and it is not me</em>. An idle engine answers no, because a host building values
    /// between turns is the legitimate case this must never flag. A reserved-but-unclaimed engine — an
    /// outstanding <c>*Async</c> operation between continuations — answers no for the same reason: there is
    /// no thread to name, so there is nothing to report that is not a guess.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void VerifyValueConstructedOnOwningThread(Type constructedType)
    {
        var owner = Volatile.Read(ref _ownerThreadId);
        var current = System.Environment.CurrentManagedThreadId;
        if (owner == 0 || owner == current)
        {
            return;
        }

        HostContractVerification.Fail(
            $"{constructedType} was built for an Engine on managed thread {current} while managed thread {owner} "
            + "was using that engine. Value construction is outside the concurrency guard that rejects a "
            + "concurrent Evaluate or Invoke, so nothing refused this and the object is now sharing an engine "
            + "and a realm with another thread's work. Build the value on the thread that owns the engine, or "
            + "while no thread does.");
    }

    internal HostCallScope EnterTransferredHostCallback(object? expectedAuthorization)
    {
        if (expectedAuthorization is not HostCallbackAuthorization authorization)
        {
            return EnterHostCall();
        }

        var expectedOwner = authorization.Owner;
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
            return EnterTransferredHostCall(
                owner,
                callback: true,
                authorization.MemoryState);
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

        var authorization = authorizations.GetOrCreateValue(callback);
        authorization.Owner = owner;
        authorization.MemoryState = CaptureMemoryLimitState();
    }

    /// <summary>
    /// The authorization recorded for <paramref name="callback"/> by <see cref="AuthorizeHostCallback"/>, as
    /// an opaque token: the type is private, so the delegate shim that carries it across the thread hop holds
    /// it as <see cref="object"/> and hands it straight back to
    /// <see cref="EnterTransferredHostCallback"/>. It names both the owner the callback may re-enter under
    /// and the memory operation its turn is charged to.
    /// </summary>
    internal object? GetHostCallbackAuthorization(ObjectInstance callback)
    {
        var authorizations = _hostCallbackAuthorizations;
        return authorizations is not null && authorizations.TryGetValue(callback, out var authorization)
            ? authorization
            : null;
    }

    internal HostCallScope EnterTransferredHostCall(
        object owner,
        bool callback = false,
        MemoryLimitConstraint.OperationState? callbackMemoryState = null)
    {
        if (!ReferenceEquals(Volatile.Read(ref _asyncOwner), owner))
        {
            return EnterHostCall(callbackOwner: callback ? owner : null);
        }

        var threadId = System.Environment.CurrentManagedThreadId;
        if (Volatile.Read(ref _ownerThreadId) == threadId)
        {
            _ownerDepth++;
            return CreateTransferredHostCallScope(
                callback ? owner : null,
                callbackMemoryState);
        }

        AcquireHostCall(threadId);
        if (!ReferenceEquals(Volatile.Read(ref _asyncOwner), owner))
        {
            if (Volatile.Read(ref _asyncOwner) is null)
            {
                // The reservation vanished under this transfer, so what is left is an ordinary claim of a
                // free engine - and therefore an entry of its own, which may be handed a reservation later.
                _ownerDepth = 1;
                _ownerToken = null;
                return new HostCallScope(this, callback ? owner : null, isEntryRoot: true);
            }

            Volatile.Write(ref _ownerThreadId, 0);
            Volatile.Read(ref _ownershipReleased)?.Set();
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        _ownerDepth = 1;
        _ownerToken = owner;
        return CreateTransferredHostCallScope(
            callback ? owner : null,
            callbackMemoryState);
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
        scope = new HostCallScope(this, callbackOwner: null, isEntryRoot: true);
        return true;
    }

    /// <summary>
    /// The engine state that has to travel with <see cref="Runtime.CallStack.StackGuard"/>'s thread hop:
    /// which thread owns the engine, and where the memory-limit segment currently being charged left off.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct StackGuardHandoff(
        int OwnerThreadId,
        MemoryLimitConstraint.OperationState? MemoryState,
        MemoryLimitConstraint.SegmentToken SuspendedSegment,
        bool HasSuspendedSegment);

    /// <summary>
    /// Hands this evaluation over to the thread <see cref="Runtime.CallStack.StackGuard"/> is about to
    /// continue it on, and is called on the thread that will then block for the whole of that hop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a transfer rather than a release: <see cref="_ownerThreadId"/> is never written back to zero,
    /// so a third thread reaching a public entry during the hop is refused exactly as it was before the hop
    /// started. The operation is still in progress; only the thread running it changed.
    /// </para>
    /// <para>
    /// <see cref="_ownerDepth"/>, <see cref="_ownerToken"/> and <see cref="_hostEntryDepth"/> deliberately do
    /// not travel, because they do not change: the hop continues one entry rather than starting another, so
    /// a callback authorized under the outer frame still matches and the outer run's constraints still apply.
    /// </para>
    /// <para>
    /// The memory-limit segment does travel, and has to: it is charged from the runtime's per-thread
    /// allocation counter, so a segment opened here and accumulated over there would either mis-bill one
    /// thread's allocations to another or trip the constraint's own changed-thread guard.
    /// </para>
    /// </remarks>
    internal StackGuardHandoff BeginStackGuardHandoff()
    {
        MemoryLimitConstraint.OperationState? memoryState = null;
        MemoryLimitConstraint.SegmentToken suspendedSegment = default;
        var hasSuspendedSegment = _memoryLimitConstraint?.TrySuspendActiveSegment(
            out memoryState,
            out suspendedSegment) == true;

        return new StackGuardHandoff(
            Volatile.Read(ref _ownerThreadId),
            memoryState,
            suspendedSegment,
            hasSuspendedSegment);
    }

    /// <summary>
    /// Claims the engine on the thread <see cref="Runtime.CallStack.StackGuard"/> continued the evaluation
    /// on, for as long as the returned scope is alive.
    /// </summary>
    internal StackGuardClaim ClaimStackGuardHandoff(in StackGuardHandoff handoff)
    {
        Volatile.Write(ref _ownerThreadId, System.Environment.CurrentManagedThreadId);
        if (!handoff.HasSuspendedSegment)
        {
            return new StackGuardClaim(this, default, hasMemorySegment: false);
        }

        var segment = _memoryLimitConstraint!.BeginSegment(handoff.MemoryState);
        return new StackGuardClaim(this, segment, hasMemorySegment: true);
    }

    /// <summary>
    /// Takes the evaluation back once <see cref="Runtime.CallStack.StackGuard"/>'s hop has finished, on the
    /// thread that blocked for it.
    /// </summary>
    /// <remarks>
    /// Ownership is restored here rather than in <see cref="StackGuardClaim"/>'s disposal so that a hop which
    /// failed before it could claim, or after it claimed and before it released, still leaves
    /// <see cref="_ownerThreadId"/> naming the thread that is about to resume. Nothing can observe the gap:
    /// the only other thread that could is this one, and it is inside this call.
    /// </remarks>
    internal void EndStackGuardHandoff(in StackGuardHandoff handoff)
    {
        Volatile.Write(ref _ownerThreadId, handoff.OwnerThreadId);
        if (handoff.HasSuspendedSegment)
        {
            var suspendedSegment = handoff.SuspendedSegment;
            _memoryLimitConstraint!.EndSegment(in suspendedSegment);
        }
    }

    /// <summary>
    /// One thread's turn at a <see cref="StackGuardHandoff"/>, released when the hop's continuation returns.
    /// </summary>
    internal readonly struct StackGuardClaim : IDisposable
    {
        private readonly Engine _engine;
        private readonly MemoryLimitConstraint.SegmentToken _memorySegment;
        private readonly bool _hasMemorySegment;

        internal StackGuardClaim(
            Engine engine,
            MemoryLimitConstraint.SegmentToken memorySegment,
            bool hasMemorySegment)
        {
            _engine = engine;
            _memorySegment = memorySegment;
            _hasMemorySegment = hasMemorySegment;
        }

        public void Dispose()
        {
            if (_hasMemorySegment)
            {
                _engine._memoryLimitConstraint!.EndSegment(in _memorySegment);
            }
        }
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

    /// <summary>
    /// Opens the engine's callback-admission window for a blocking drain, and keeps it open for the whole
    /// of it: an authorized cross-thread callback arriving at any point during the drain is serialized
    /// behind the drain's own turn instead of being refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reservation (<see cref="_asyncOwner"/>) is what <see cref="EnterTransferredHostCallback"/> reads
    /// to tell an authorized transfer - one the host was handed by this engine and may dispatch to another
    /// thread - from an unrelated public caller. <see cref="DrainEventLoopUntil"/> used to take it for the
    /// duration of <c>WaitForWork</c> alone, so on every iteration it was dropped again across
    /// <see cref="RunAvailableContinuations"/> - which runs arbitrary script - and re-taken. A callback
    /// landing in that gap took the "no reservation" path, was treated as an unrelated caller and destroyed
    /// with <see cref="InvalidOperationException"/>, which contradicts what README.md's Thread-safety
    /// section and THREAT_MODEL.md TM-13 both promise: "such an authorized transfer may wait for the current
    /// callback turn". Whether a given callback was served or destroyed came down to where the OS scheduler
    /// happened to put it (sebastienros/jint#3206, sebastienros/jint#3220).
    /// </para>
    /// <para>
    /// This is the shape <c>AwaitPromiseSettlementAsync</c> has always had on the asynchronous side -
    /// reservation for the whole outstanding operation, the <em>thread</em> claimed only around each
    /// continuation cycle - and the drain is its synchronous twin. The per-iteration
    /// <see cref="SuspendHostCallForCallbacks"/> around the wait therefore no longer takes a reservation of
    /// its own: it finds this one and yields only the thread, which is all it ever needed to do.
    /// </para>
    /// <para>
    /// Nothing else about who may enter changes. An unrelated public caller on another thread was refused
    /// throughout the drain before - the drain thread held <see cref="_ownerThreadId"/> for all of it - and
    /// is refused throughout it now. The window is deliberately scoped to a frame whose entire purpose is to
    /// wait for outside work, because an admitted callback waits for the engine thread to yield and only
    /// such a frame provably does; that is why the fail-fast branches in
    /// <see cref="EnterTransferredHostCallback"/> stay fail-fast rather than becoming waits.
    /// </para>
    /// <para>
    /// Returns a no-op window when a reservation already exists - an outstanding async host operation, or an
    /// outer drain. That one is not ours to close, and the drain then behaves exactly as it did before.
    /// </para>
    /// <para>
    /// Opened by <em>every</em> drain, nested ones included: there is deliberately no guard on the scope
    /// that claimed the engine. What would justify one is a frame that <em>runs no script</em> - a park on
    /// <see cref="TaskOperations.WaitForScheduledWork"/>, whose <c>InspectScheduledWork</c> executes
    /// nothing - because there an admitted callback would be the only script interleaved into somebody
    /// else's evaluation. A drain is the opposite case: <see cref="RunAvailableContinuations"/> runs
    /// arbitrary script on every iteration whether a callback is admitted or not, so refusing the callback
    /// would remove the callback without removing the interleaving. Measured at a nested drain against the
    /// same probes at a top-level one, three candidate hazards behave identically: a host method reached
    /// from a drain continuation and blocking on a second authorized callback wedges either way, an
    /// admitted callback blocking on host state the outer frame holds is stalled either way, and a turn
    /// charged to the outer evaluation's statement budget can kill that evaluation either way - the last of
    /// which the drain's own continuations already do, with no callback involved at all.
    /// </para>
    /// <para>
    /// The claiming scope would in any case be the wrong question here, for a reason that has nothing to do
    /// with script: <see cref="ModuleOperations.Import(string)"/> claims the engine before this drain
    /// re-enters, so a blocking import <em>on an otherwise idle engine, from the host's own thread</em> is
    /// not a claiming scope either. That drain is one of the windows README.md's Thread-safety section
    /// enumerates, so keying the window on the claiming scope would close a documented top-level window
    /// rather than a nested one. Nor is there a middle option: reserving under a fresh token instead would
    /// admit only callbacks converted <em>inside</em> the drain, which is the "latent fourth" gap
    /// sebastienros/jint#3206 removed by opening one window before any conversion can happen.
    /// sebastienros/jint#3262.
    /// </para>
    /// </remarks>
    internal HostCallbackAdmissionWindow OpenHostCallbackAdmissionWindow()
    {
        // Same identity rule as SuspendHostCallForCallbacks, deliberately: a drain entered with no operation
        // token of its own reserves under the engine's ownership-released event, which is the anonymous
        // owner EnterTransferredHostCallback recognizes as "any authorized callback may take a turn here".
        var previousOwnerToken = _ownerToken;
        var owner = previousOwnerToken ?? OwnershipReleasedEvent;
        if (Interlocked.CompareExchange(ref _asyncOwner, owner, null) is not null)
        {
            return default;
        }

        _ownerToken = owner;
        return new HostCallbackAdmissionWindow(
            this,
            owner,
            previousOwnerToken,
            Volatile.Read(ref _hostReentryThreadId),
            Volatile.Read(ref _transferredMemoryState));
    }

    /// <summary>
    /// Closes a window opened by <see cref="OpenHostCallbackAdmissionWindow"/>, once every callback admitted
    /// through it has finished.
    /// </summary>
    private void CloseHostCallbackAdmissionWindow(
        object owner,
        object? previousOwnerToken,
        int previousReentryThreadId,
        MemoryLimitConstraint.OperationState? transferredMemoryState)
    {
        // Release the thread first. A callback admitted during the drain may be blocked in AcquireHostCall
        // waiting for exactly this thread, and the teardown below waits for every admitted callback to
        // finish - so closing while still holding the thread would wedge the two against each other. Every
        // other caller of ResumeHostCall reaches it from a suspension that already released the thread; this
        // one has to do it here, which is the whole difference between a window and a suspension.
        var depth = _ownerDepth;
        _ownerDepth = 0;
        _ownerToken = null;
        Volatile.Write(ref _ownerThreadId, 0);
        Volatile.Read(ref _ownershipReleased)?.Set();

        // The window suspends no memory segment of its own - it never yields the thread - so it hands back
        // the state it was opened with, which leaves that accounting exactly as it found it.
        ResumeHostCall(
            owner,
            previousOwnerToken,
            depth,
            previousReentryThreadId,
            ownsReservation: true,
            hasMemorySegment: false,
            memorySegment: default,
            transferredMemoryState);
    }

    /// <summary>
    /// Yields the engine for the duration of a host call that was handed a JavaScript callback, so the host
    /// may dispatch that callback to another thread, and reserves the engine against unrelated callers for
    /// as long as the callback may still arrive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <em>thread</em> is yielded for the host call only. The <em>reservation</em> outlives it: when this
    /// call is the one that took it, <see cref="ResumeHostCall"/> hands it to the host entry underneath
    /// rather than dropping it, and <see cref="ExitHostCall"/> releases it when that entry unwinds. Dropping
    /// it at the end of the host call is what sebastienros/jint#3206 reported: an <c>async</c> host method
    /// returns at its first <c>await</c>, so the callback it captured is invoked long after this scope has
    /// closed - and until the caller reaches something that reserves again (a blocking drain, an async
    /// engine API) the engine looked unreserved while the calling thread was still on it, so an authorized
    /// callback landing there was treated as an unrelated caller and destroyed.
    /// </para>
    /// <para>
    /// The entry is the right scope because it is one that provably ends. Keying the hand-over on the
    /// pending awaitable instead would tie the reservation to a <see cref="Task"/> the host may never
    /// complete; keying it on the entry bounds it by the engine's own stack, and the entry's operation token
    /// keeps a stale callback from an <em>earlier</em> entry refused exactly as before.
    /// </para>
    /// </remarks>
    /// <param name="yieldForCallbacks">
    /// Whether to yield the thread at all; <see langword="false"/> makes this a no-op scope. It says what
    /// the caller wants from the frame, deliberately not who handed a callback to whom: at the interop call
    /// sites it is "this host call was passed a JavaScript callback", while
    /// <see cref="DrainEventLoopUntil"/> passes a constant <see langword="true"/> for its idle wait, having
    /// been handed nothing - the reservation it finds is one it opened itself, and all it wants here is the
    /// thread released so a callback admitted through that window can take its turn.
    /// </param>
    internal HostCallSuspension SuspendHostCallForCallbacks(bool yieldForCallbacks)
    {
        if (!yieldForCallbacks)
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
        var previousTransferredMemoryState = Volatile.Read(ref _transferredMemoryState);
        MemoryLimitConstraint.OperationState? memoryState = null;
        MemoryLimitConstraint.SegmentToken memorySegment = default;
        var hasMemorySegment = _memoryLimitConstraint?.TrySuspendActiveSegment(
            out memoryState,
            out memorySegment) == true;
        Volatile.Write(ref _transferredMemoryState, memoryState);
        Volatile.Write(ref _hostReentryThreadId, System.Environment.CurrentManagedThreadId);
        _ownerDepth = 0;
        _ownerToken = null;
        Volatile.Write(ref _ownerThreadId, 0);
        Volatile.Read(ref _ownershipReleased)?.Set();
        return new HostCallSuspension(
            this,
            owner,
            previousOwnerToken,
            depth,
            previousReentryThreadId,
            ownsReservation,
            hasMemorySegment,
            memorySegment,
            previousTransferredMemoryState);
    }

    /// <summary>
    /// Ends a suspension taken by <see cref="SuspendHostCallForCallbacks"/>: reclaims the thread and, when
    /// this suspension is the one that reserved the engine, hands that reservation to the host entry
    /// underneath instead of dropping it.
    /// </summary>
    private void ResumeSuspendedHostCall(
        object owner,
        object? previousOwnerToken,
        int depth,
        int previousReentryThreadId,
        bool ownsReservation,
        bool hasMemorySegment,
        MemoryLimitConstraint.SegmentToken memorySegment,
        MemoryLimitConstraint.OperationState? previousTransferredMemoryState)
    {
        if (!ownsReservation)
        {
            ResumeHostCall(
                owner,
                previousOwnerToken,
                depth,
                previousReentryThreadId,
                ownsReservation: false,
                hasMemorySegment,
                memorySegment,
                previousTransferredMemoryState);
            return;
        }

        // No admission gate and no drain here, unlike the reservation-owning path in ResumeHostCall: the
        // reservation is not ending, so a callback admitted a moment ago simply waits for the thread the
        // line below reclaims, exactly as one arriving a moment later will.
        AcquireHostCall(System.Environment.CurrentManagedThreadId);
        Volatile.Write(ref _transferredMemoryState, previousTransferredMemoryState);
        if (hasMemorySegment)
        {
            _memoryLimitConstraint!.EndSegment(in memorySegment);
        }

        _ownerDepth = depth;
        _ownerToken = previousOwnerToken;
        Volatile.Write(ref _hostReentryThreadId, previousReentryThreadId);

        // Ordered so that a thread seeing the reservation also sees whose it is;
        // ReleaseEntryReservationIfHeld reads them the other way round.
        _entryReservationThreadId = System.Environment.CurrentManagedThreadId;
        Volatile.Write(ref _entryReservation, owner);
    }

    /// <summary>
    /// Called when a host entry's own scope unwinds, to release any reservation
    /// <see cref="ResumeSuspendedHostCall"/> handed to that entry.
    /// </summary>
    /// <remarks>
    /// Keyed on the scope that <em>claimed</em> the engine, deliberately not on <see cref="_ownerDepth"/>
    /// reaching zero: a suspension zeroes that depth, so a callback taking its turn while the entry is
    /// suspended also unwinds through zero. Releasing there would make the second callback-taking host call
    /// of a single script (<c>list.Where(...).Sum(...)</c>) wait, inside its own callback, for the very
    /// admission that callback still holds.
    /// </remarks>
    private void ReleaseEntryReservationIfHeld()
    {
        var reservation = Volatile.Read(ref _entryReservation);
        if (reservation is null || _entryReservationThreadId != System.Environment.CurrentManagedThreadId)
        {
            return;
        }

        ReleaseEntryReservation(reservation);
    }

    /// <summary>
    /// Drops a handed-over reservation once every callback admitted under it has finished. The thread is
    /// already released by the time this runs, which is what lets an admitted callback take its turn while
    /// this waits for it.
    /// </summary>
    private void ReleaseEntryReservation(object reservation)
    {
        _entryReservation = null;
        _entryReservationThreadId = 0;

        lock (reservation)
        {
            Volatile.Write(ref _hostCallbackAdmissionClosed, reservation);
            while (Volatile.Read(ref _hostCallbackAdmission) > 0)
            {
                Monitor.Wait(reservation);
            }

            Interlocked.CompareExchange(ref _asyncOwner, null, reservation);
            Interlocked.CompareExchange(ref _hostCallbackAdmissionClosed, null, reservation);
        }
    }

    /// <summary>
    /// Reclaims the engine for a suspended host call, once every callback admitted while it was suspended
    /// has finished.
    /// </summary>
    /// <remarks>
    /// The caller must not hold <see cref="_ownerThreadId"/>. The admission drain below blocks until no
    /// admitted callback is outstanding, and an admitted callback reaches its turn by acquiring that very
    /// field, so a caller still holding it would wedge the two against each other. Closing the admission
    /// gate before <see cref="AcquireHostCall"/> rather than after is the other half of the same rule.
    /// </remarks>
    private void ResumeHostCall(
        object owner,
        object? previousOwnerToken,
        int depth,
        int previousReentryThreadId,
        bool ownsReservation,
        bool hasMemorySegment,
        MemoryLimitConstraint.SegmentToken memorySegment,
        MemoryLimitConstraint.OperationState? previousTransferredMemoryState)
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

        Volatile.Write(ref _transferredMemoryState, previousTransferredMemoryState);
        if (hasMemorySegment)
        {
            _memoryLimitConstraint!.EndSegment(in memorySegment);
        }

        _ownerDepth = depth;
        _ownerToken = previousOwnerToken;
        Volatile.Write(ref _hostReentryThreadId, previousReentryThreadId);
    }

    private HostCallScope CreateTransferredHostCallScope(
        object? callbackOwner,
        MemoryLimitConstraint.OperationState? callbackMemoryState = null)
    {
        var memoryState = callbackMemoryState ?? Volatile.Read(ref _transferredMemoryState);
        if (memoryState is null || _memoryLimitConstraint is null)
        {
            return new HostCallScope(this, callbackOwner);
        }

        return new HostCallScope(
            this,
            callbackOwner,
            _memoryLimitConstraint.BeginSegment(
                _memoryLimitConstraint.ContinueOrBeginEntry(memoryState)));
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

    /// <summary>
    /// Reserves the engine for an asynchronous host operation and returns the reservation's identity.
    /// </summary>
    /// <param name="reservationOwner">
    /// The identity to reserve under, or <see langword="null"/> for a fresh token of this operation's own.
    /// <para>
    /// That identity is the whole of what <see cref="EnterTransferredHostCallback"/> matches an authorized
    /// cross-thread callback against, so it decides <em>which</em> callbacks may wait for a turn here. The
    /// rule is the one <see cref="OpenHostCallbackAdmissionWindow"/> and
    /// <see cref="SuspendHostCallForCallbacks"/> apply: a frame under which an operation token is in force
    /// keeps that token, so only a callback issued under it may wait; a frame with no token of its own
    /// reserves under <see cref="OwnershipReleasedEvent"/>, the anonymous wildcard that means "any authorized
    /// callback may take a turn here". A fresh token is right for a frame that <em>runs script</em> - every
    /// <c>*Async</c> entry - because a callback converted during it carries that very token. It is wrong for
    /// a frame that runs none, where nothing can ever carry it and the match is empty rather than narrow.
    /// </para>
    /// </param>
    internal object ReserveAsyncHostOperation(object? reservationOwner = null)
    {
        var activeOwner = Volatile.Read(ref _ownerThreadId);
        if (activeOwner != 0)
        {
            Throw.InvalidOperationException(ConcurrentUseMessage);
        }

        var owner = reservationOwner ?? new object();
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
        private readonly MemoryLimitConstraint.SegmentToken _memorySegment;
        private readonly bool _hasMemorySegment;

        /// <summary>
        /// Whether this scope is the one that claimed the engine for a host entry, as opposed to a nested
        /// re-entry or a callback taking its turn under a reservation. Only such a scope may release a
        /// reservation handed to the entry - see <see cref="ReleaseEntryReservationIfHeld"/>.
        /// </summary>
        private readonly bool _isEntryRoot;

        /// <summary>
        /// Whether this scope claimed the engine, rather than nesting inside a claim somebody else made. It
        /// is the same claiming-scope rule <see cref="ReleaseEntryReservationIfHeld"/> is keyed on, and never
        /// <see cref="_ownerDepth"/> reaching zero, for the reason recorded there. A frame that only opens an
        /// admission window while it is the outermost one - <see cref="WaitForScheduledWork"/> - asks this.
        /// </summary>
        internal bool IsEntryRoot => _isEntryRoot;

        internal HostCallScope(Engine engine)
        {
            _engine = engine;
            _callbackOwner = null;
            _memorySegment = default;
            _hasMemorySegment = false;
            _isEntryRoot = false;
        }

        internal HostCallScope(Engine engine, object? callbackOwner)
        {
            _engine = engine;
            _callbackOwner = callbackOwner;
            _memorySegment = default;
            _hasMemorySegment = false;
            _isEntryRoot = false;
        }

        internal HostCallScope(Engine engine, object? callbackOwner, bool isEntryRoot)
        {
            _engine = engine;
            _callbackOwner = callbackOwner;
            _memorySegment = default;
            _hasMemorySegment = false;
            _isEntryRoot = isEntryRoot;
        }

        /// <summary>
        /// A callback taking its turn under a reservation, charged to the memory operation it was authorized
        /// under. Never an entry root: the reservation it runs under belongs to the entry that yielded, and
        /// releasing it here would pull it out from under that entry.
        /// </summary>
        internal HostCallScope(
            Engine engine,
            object? callbackOwner,
            MemoryLimitConstraint.SegmentToken memorySegment)
        {
            _engine = engine;
            _callbackOwner = callbackOwner;
            _memorySegment = memorySegment;
            _hasMemorySegment = true;
            _isEntryRoot = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_hasMemorySegment)
            {
                _engine._memoryLimitConstraint!.EndSegment(in _memorySegment);
            }

            _engine.ExitHostCall();
            if (_callbackOwner is not null)
            {
                // Before the reservation release below, which waits for this very count to reach zero.
                _engine.ReleaseHostCallbackAdmission(_callbackOwner);
            }

            if (_isEntryRoot)
            {
                _engine.ReleaseEntryReservationIfHeld();
            }
        }
    }

    private sealed class HostCallbackAuthorization
    {
        private object? _owner;
        private MemoryLimitConstraint.OperationState? _memoryState;

        internal object? Owner
        {
            get => Volatile.Read(ref _owner);
            set => Volatile.Write(ref _owner, value);
        }

        internal MemoryLimitConstraint.OperationState? MemoryState
        {
            get => Volatile.Read(ref _memoryState);
            set => Volatile.Write(ref _memoryState, value);
        }
    }

    /// <summary>
    /// The scope <see cref="OpenHostCallbackAdmissionWindow"/> returns. Unlike
    /// <see cref="HostCallSuspension"/> it does <em>not</em> yield the thread: the engine stays claimed by
    /// the opener, and only the reservation an authorized callback needs in order to wait for its turn is
    /// held for the scope's duration.
    /// </summary>
    internal readonly struct HostCallbackAdmissionWindow : IDisposable
    {
        private readonly Engine? _engine;
        private readonly object _owner;
        private readonly object? _previousOwnerToken;
        private readonly int _previousReentryThreadId;
        private readonly MemoryLimitConstraint.OperationState? _transferredMemoryState;

        internal HostCallbackAdmissionWindow(
            Engine engine,
            object owner,
            object? previousOwnerToken,
            int previousReentryThreadId,
            MemoryLimitConstraint.OperationState? transferredMemoryState)
        {
            _engine = engine;
            _owner = owner;
            _previousOwnerToken = previousOwnerToken;
            _previousReentryThreadId = previousReentryThreadId;
            _transferredMemoryState = transferredMemoryState;
        }

        public void Dispose()
        {
            _engine?.CloseHostCallbackAdmissionWindow(
                _owner,
                _previousOwnerToken,
                _previousReentryThreadId,
                _transferredMemoryState);
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
        private readonly bool _hasMemorySegment;
        private readonly MemoryLimitConstraint.SegmentToken _memorySegment;
        private readonly MemoryLimitConstraint.OperationState? _previousTransferredMemoryState;

        internal HostCallSuspension(
            Engine engine,
            object owner,
            object? previousOwnerToken,
            int depth,
            int previousReentryThreadId,
            bool ownsReservation,
            bool hasMemorySegment,
            MemoryLimitConstraint.SegmentToken memorySegment,
            MemoryLimitConstraint.OperationState? previousTransferredMemoryState)
        {
            _engine = engine;
            _owner = owner;
            _previousOwnerToken = previousOwnerToken;
            _depth = depth;
            _previousReentryThreadId = previousReentryThreadId;
            _ownsReservation = ownsReservation;
            _hasMemorySegment = hasMemorySegment;
            _memorySegment = memorySegment;
            _previousTransferredMemoryState = previousTransferredMemoryState;
        }

        public void Dispose()
        {
            _engine?.ResumeSuspendedHostCall(
                _owner,
                _previousOwnerToken,
                _depth,
                _previousReentryThreadId,
                _ownsReservation,
                _hasMemorySegment,
                _memorySegment,
                _previousTransferredMemoryState);
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

    // The handler-tree cache sizes, for Engine.Diagnostics.GetMemoryReport. Read-only views over the
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

    // cached access. Not readonly: this and the three fields below are the interop-conversion group, the
    // only option-derived state the engine takes before Options.Apply - a configuration callback's
    // engine.SetValue(name, clrValue) converts, and conversion reads all four. TakeInteropConversionState
    // runs again after Apply, so what a callback registered (or a hardened profile cleared) is what stands.
    internal ObjectConverter[]? _objectConverters;

    // which declared CLR member types the registered converters can claim, null when none are registered.
    // Lets the compiled interop read lanes stay in play for members no converter can ever be handed.
    internal ObjectConverterTypeFilter? _objectConverterTypeFilter;

    // which CLR types the host declared immutable for the crossing, null when none were declared. Lets an
    // ObjectWrapper memoize what it reads through such a target. See Options.Interop.ImmutableCrossingTypes.
    internal ImmutableCrossingTypeFilter? _immutableCrossingFilter;

    // snapshot of Options.Interop.EnumConversion, read on every CLR value crossing into script
    internal bool _enumsAsStrings;

    // Built after Options.Apply, like every other option-derived field, and empty until then. The three
    // ConstraintOperations members refuse outright while that is the case rather than answering from an
    // empty set - a limit a configuration callback is still writing is not "no limit". The initializer is
    // Array.Empty and exists so that an internal reader added later fails safe instead of dereferencing null.
    internal readonly Constraint[] _constraints = [];

#if NET8_0_OR_GREATER
    /// <summary>
    /// The clock the engine's own bounded <em>waits</em> measure against — <c>Options.Constraints.TimeProvider</c>
    /// put through <see cref="ConstraintClock.Resolve"/>, so <see langword="null"/> means
    /// <see cref="Stopwatch"/>, which is what every engine that named no clock carries.
    /// </summary>
    private readonly TimeProvider? _waitClock;
#endif

    // _constraints partitioned once at construction (the set is fixed for the engine's lifetime):
    // exact constraints must run before every statement, amortized ones every N statements.
    // See Engine.Constraints.cs for the partitioning rationale.
    internal readonly Constraint[] _exactConstraints = [];
    internal readonly Constraint[] _amortizedConstraints = [];
    internal readonly MemoryLimitConstraint? _memoryLimitConstraint;
    private MemoryLimitConstraint.SegmentToken _implicitMemorySegment;
    private int _implicitMemoryContextDepth;

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

    /// <summary>
    /// The sampling session recording this engine's call stack, or <see langword="null"/> — which it is
    /// unless a host has called <see cref="DiagnosticOperations.StartSampling"/>. It rides the amortized
    /// cadence above rather than a timer or a second thread: the stack is thread-affine, so the only thread
    /// that may read it is the one running it, and the only place that thread reliably passes through is a
    /// check point.
    /// </summary>
    /// <remarks>
    /// The cost of not sampling is nothing the interpreter can see. The per-statement gate is
    /// <see cref="EvaluationContext"/>'s single "is there amortized work" flag, which this field is folded
    /// into when a session opens, so an engine with no constraints and no session runs the same instructions
    /// it ran before this existed.
    /// </remarks>
    internal Profiling.SamplingProfiler? _sampler;

    /// <summary>
    /// How many <c>try</c> blocks that have a <c>catch</c> clause are executing on this engine's stack, so
    /// that a throw can be told to be caught or uncaught at the point it is raised. Maintained by
    /// <see cref="Runtime.Interpreter.Statements.JintTryStatement"/>, and only while
    /// <see cref="_isDebugMode"/> is set — nothing but <see cref="DebugHandler.PauseOnExceptions"/> reads it.
    /// </summary>
    /// <remarks>
    /// It is reset for the duration of a host entry and of an async function body, because a throw crossing
    /// either boundary does not reach a <c>catch</c> below it: the host entry hands the CLR exception to its
    /// caller, and the async function turns it into a promise rejection.
    /// </remarks>
    internal int _tryCatchDepth;

    internal readonly bool _isDebugMode;
    internal readonly bool _isStrict;
    internal readonly UntrustedCodeLimits? _untrustedCodeLimits;

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

    internal Constraint[] ActiveConstraintsForSecurityDiagnostics => _constraints;
    internal readonly EngineSecurityConfigurationSnapshot _securityConfigurationSnapshot;

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
    /// <summary>
    /// The registered extension methods, keyed by the type they extend. Consulted by
    /// <see cref="Runtime.Interop.TypeResolver"/> and by the prototype attach in <see cref="Options.Apply"/>.
    /// </summary>
    /// <remarks>
    /// Not <c>readonly</c>: it is built at the top of construction, because a configuration callback can
    /// reach interop, and re-derived once from <see cref="Options.Apply"/> after every callback has run —
    /// registering a container type from <c>Options.Configure</c> has to reach both consumers, and the attach
    /// beside that refresh already reads the registration list live.
    /// </remarks>
    internal ExtensionMethodCache _extensionMethods;

    /// <summary>
    /// The converter every interop conversion actually goes through, which is what engine-internal call sites
    /// must read. With host-contract verification on it is a wrapper around what the host installed; the public
    /// <see cref="TypeConverter"/> unwraps it, so a host observes its own instance in every configuration.
    /// </summary>
    internal ClrTypeConverter _typeConverter = null!;

    // Which conversion *target* types the installed converter may answer differently from the stock one, and
    // therefore which member writes, argument bindings and indexer keys the compiled interop lanes must leave
    // to it. Null for the stock converter, which is what those lanes reproduce. Kept in sync by
    // InstallTypeConverter so a lane gates on a field read instead of a per-call GetType() comparison - and
    // so that a converter swapped in after construction is honoured, which a value captured once would not be.
    internal TypeConverterTargetFilter? _typeConverterTargetFilter;

    public ClrTypeConverter TypeConverter
    {
        get => _typeConverter is VerifyingClrTypeConverter verifying ? verifying.Inner : _typeConverter;
        internal set => InstallTypeConverter(value, declaredTargetTypes: null);
    }

    /// <summary>
    /// Installs <paramref name="converter"/> together with what its registration declared, and derives the
    /// filter the compiled lanes gate on.
    /// </summary>
    /// <remarks>
    /// The verifying wrapper is built only when a declaration was actually made and host-contract verification
    /// is on: an undeclared converter has promised nothing, and there is nothing to check.
    /// </remarks>
    internal void InstallTypeConverter(ClrTypeConverter converter, Type[]? declaredTargetTypes)
    {
        var filter = TypeConverterTargetFilter.Create(converter, declaredTargetTypes);
        _typeConverterTargetFilter = filter;
        _typeConverter = HostContractVerification.Enabled && filter is { IsRestricted: true }
            ? new VerifyingClrTypeConverter(converter, filter, new DefaultTypeConverter(this))
            : converter;
    }

    // cache of types used when resolving CLR type names
    internal readonly Dictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    // we use registered type reference as prototype if it's known
    internal Dictionary<Type, TypeReference>? _typeReferences;

    // cache for already wrapped CLR objects to keep object identity
    internal ConditionalWeakTable<object, ObjectInstance>? _objectWrapperCache;

    // Reused while CLR array snapshots are built so cyclic graphs can point back to an in-progress JsArray.
    internal ArrayCopyContext? _arrayCopyContext;

    // per-object private-element storage ([[PrivateElements]]). Kept off ObjectInstance in a weak table
    // (keyed by the owning object) so the 8-byte reference doesn't bloat every object on the heap — only
    // instances of JS classes that declare #private members ever allocate an entry here.
    internal ConditionalWeakTable<ObjectInstance, Dictionary<PrivateName, PrivateElement>>? _privateElementStore;

    // bounded cache of most recently wrapped CLR objects, see Options.Interop.CacheRecentObjectWrappers
    internal RecentObjectWrapperCache? _recentObjectWrapperCache;

    // Diagnostic counters for the two mode-governed CLR-array conversions, surfaced through
    // Engine.Diagnostics.GetInteropConversionDiagnostics. Bumped only inside DefaultObjectConverter, at the two
    // points that have just decided to build a live wrapper view or an N-element snapshot copy — so the cost
    // is one increment on a path that is already allocating, and exactly nothing on every path that converts
    // no array. Plain longs, not Interlocked: an engine is single-threaded by contract, like every other
    // per-engine counter here.
    internal long _arrayLiveViewConversions;
    internal long _arrayCopyConversions;

    internal readonly JintCallStack CallStack;
    internal readonly StackGuard _stackGuard;

    /// <summary>
    /// <see langword="false"/> until the constructor has returned. While it is, this engine cannot run
    /// script, and the entries that would have are refused by name instead of dereferencing a field that
    /// is still null.
    /// </summary>
    /// <remarks>
    /// A callback registered with <c>Options.Configure</c> runs from <see cref="Options.Apply"/>, which sits
    /// where it does deliberately: the host's registrations land before Jint installs its own globals — so a
    /// global the host wrote is never replaced by ours — and before an untrusted-code profile is re-expanded
    /// over whatever those callbacks wrote. A script running from there would therefore see a realm without
    /// <c>System</c>, <c>importNamespace</c>, <c>clrHelper</c>, <c>require</c>, the opt-in web APIs or the
    /// extension methods on its prototypes, and would run before the profile was re-applied. Moving the
    /// callbacks after all of that is not available: it is the re-expansion that keeps a callback from
    /// reopening a hardened setting (sebastienros/jint#3581).
    /// </remarks>
    private readonly bool _constructionComplete;

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
        Constraints = new ConstraintOperations(this);
        TypeConverter = new DefaultTypeConverter(this);

        _executionContexts = new ExecutionContextStack(2);

        // A hardened profile is applied to an engine-local snapshot. Options is documented as shareable
        // between concurrently constructed engines, so deferred rehardening must never mutate its source.
        var sourceOptions = options ?? (configure is not null ? new Options() : _defaultEngineOptions);
        configure?.Invoke(this, sourceOptions);
        Options = sourceOptions.CreateEngineOptions();
        _untrustedCodeLimits = Options.UntrustedCodeLimits;

        // A first reading, so that interop reached from a configuration callback has a lookup to consult.
        // Options.Apply takes it again once those callbacks have run - see RefreshExtensionMethods.
        _extensionMethods = ExtensionMethodCache.Build(Options.Interop.ExtensionMethodTypes);

        Reset();

        // The interop-conversion group is the only option-derived state this engine takes before
        // Options.Apply, and it is taken because a configuration callback can reach it: a callback's
        // engine.SetValue(name, clrValue) converts, and conversion reads all four fields. It is taken
        // again below, once Apply has run, so that a converter a callback registered is honoured and a
        // registry a hardened profile cleared is obeyed - the same two directions RefreshExtensionMethods
        // settles for the extension-method lookup (sebastienros/jint#3568).
        TakeInteropConversionState();

        _referencePool = new ReferencePool();
        _argumentsInstancePool = new ArgumentsInstancePool(this);
        _jsValueArrayPool = new JsValueArrayPool();
        _objectTraverseStackPool = new ObjectTraverseStackPool();

        Options.Apply(this);

        ValidateModuleOptions(Options.Modules);

        // Everything from here down is derived from an option, and every one of those readings is taken
        // here rather than above Options.Apply on purpose. Apply runs the host's Options.Configure
        // callbacks and then re-expands an untrusted-code profile over whatever they wrote, so this is the
        // first point at which the options are final; a reading hoisted above it silently ignores a
        // callback that writes it (sebastienros/jint#3583). See the Options.Configure rule in
        // Jint/AGENTS.md.
        _isDebugMode = Options.Debugger.Enabled;
        _isStrict = Options.Strict;

        // Must be settled before the EvaluationContext below is built: the context folds this into the
        // per-statement-lane decision, and that lane is the only thing that ever reaches the counters.
        _coverage = Options.Coverage.Enabled ? new CoverageCollector(Options.Coverage.Granularity) : null;

        TakeInteropConversionState();

#if NET8_0_OR_GREATER
        // Resolved once, here, rather than at each wait: Resolve is what rejects a clock that cannot measure
        // anything, and a named error at engine construction beats a promise budget that expires the instant
        // it is armed. Reading it costs nothing on an engine that named no clock — the fold hands back null.
        _waitClock = ConstraintClock.Resolve(Options.Constraints.TimeProvider);
#endif

        _constraints = BuildConstraints(Options.Constraints);
#if NET8_0_OR_GREATER
        // The same resolved provider the blocking promise drain reads, handed to the execution timeout, so
        // one engine cannot run its two time budgets against two clocks. It has to happen here rather than
        // in the factory that built the constraint: a factory is replayed onto a worker's own Options
        // (WorkerRequest.CreateDefaultOptions), and a closure over the configuring group then measured the
        // worker's timeout on its PARENT's clock (sebastienros/jint#3481).
        BindConstraintClocks(_constraints, _waitClock);
#endif
        var partitionedConstraints = PartitionConstraints(_constraints);
        _exactConstraints = partitionedConstraints.Exact;
        _amortizedConstraints = partitionedConstraints.Amortized;
        _inlineStatementCounter = partitionedConstraints.InlineStatementCounter;
        MemoryLimitConstraint? memoryLimitConstraint = null;
        foreach (var constraint in _constraints)
        {
            if (constraint is MemoryLimitConstraint candidate)
            {
                if (memoryLimitConstraint is not null)
                {
                    Throw.InvalidOperationException(
                        "Only one MemoryLimitConstraint can be registered per Engine. Use LimitMemory to replace the configured limit.");
                }

                memoryLimitConstraint = candidate;
            }
        }
        _memoryLimitConstraint = memoryLimitConstraint;
        memoryLimitConstraint?.Attach(this);

        // Everything the context snapshots is settled by now: debug mode, coverage and the constraint
        // partition are all read above, from options Apply has finished with.
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

        _maxRecursionDepth = Options.Constraints.EffectiveMaxRecursionDepth;

        _operatorOverloadingAllowed = Options.Interop.AllowOperatorOverloading;

        // The installed ClrTypeConverter is deliberately not part of this: the one
        // question resolution asks it is whether a member name converts to an index key, which only a type
        // carrying a non-int indexer is ever asked, and TypeResolver excludes that type's members from the
        // shared cache in both directions instead - see TypeResolver.IsConverterNeutral. Partitioning on the
        // converter here would have cost every engine with one the whole shared cache.
        _interopResolutionProfile = new InteropResolutionProfile(
            Options.Interop.AllowGetType,
            Options.Interop.ObjectWrapperReportedFieldBindingFlags,
            Options.Interop.ObjectWrapperReportedPropertyBindingFlags,
            Options.Interop.ObjectWrapperReportedMethodBindingFlags,
            _extensionMethods);

        CallStack = new JintCallStack(_maxRecursionDepth >= 0);
        _stackGuard = new StackGuard(this);

        var scriptParsingDefaults = Options.RetainFunctionSourceText
            ? ScriptParsingOptions.RetainingDefault
            : ScriptParsingOptions.Default;
        _parsingConstraints = ParsingConstraints.From(Options.Parsing);
        _defaultParserOptions = scriptParsingDefaults.GetParserOptions(Options);
        _defaultParser = JintParser.Create(_defaultParserOptions, in _parsingConstraints);
        _securityConfigurationSnapshot = EngineSecurityConfigurationSnapshot.Capture(this);

        // Last, and after everything Jint itself writes: Options.Apply has run the host's configuration
        // callbacks and the untrusted-code profile's re-expansion, and every field derived above has been
        // taken. From here the engine reads these options live — Interop.AllowWrite on every projected write,
        // Interop.ExceptionHandler on every CLR call, Intl.CldrProvider when a locale is first needed — so a
        // host write after this point would reach an engine that already exists. It cannot any more.
        //
        // The instance is usually the caller's own: CreateEngineOptions returns `this` unless a hardened
        // profile made a private clone, and cloning instead would cost every engine build. Freezing is what
        // makes the aliasing safe. Idempotent, so a second engine built from the same Options is unaffected.
        //
        // Both, and not only the one this engine kept: with a hardened profile they are different objects,
        // and a host that got `Options is read-only after you build an engine from it` for the ordinary case
        // and not for the profiled one would have to know which case it was in to know what its own object
        // does. The clone the engine kept is the one that must not move; the source is frozen so that the
        // rule can be stated without an exception.
        Options.MakeReadOnly();
        sourceOptions.MakeReadOnly();

        _constructionComplete = true;
    }

    /// <summary>
    /// Takes the four fields every CLR value crossing into script is converted through, from the options as
    /// they stand.
    /// </summary>
    /// <remarks>
    /// Called twice: once before <see cref="Options.Apply"/>, because a configuration callback's
    /// <c>engine.SetValue(name, clrValue)</c> converts and so must find them built, and once after, because
    /// what those callbacks left behind is what the engine keeps. Unconditional in both directions, like
    /// <see cref="RefreshExtensionMethods"/>: a callback that registers a converter is honoured, and a
    /// registry an untrusted-code profile cleared on its way through <c>Apply</c> is obeyed.
    /// <para>
    /// A wrapper the callback itself built keeps the reading it was made with —
    /// <see cref="Runtime.Interop.ObjectWrapper"/> folds <see cref="_immutableCrossingFilter"/> in at
    /// construction. The two other filters are consulted per access, so they follow the second reading.
    /// </para>
    /// </remarks>
    private void TakeInteropConversionState()
    {
        _objectConverters = Options.Interop.ObjectConverters.Count > 0
            ? Options.Interop.ObjectConverters.ToArray()
            : null;
        _objectConverterTypeFilter = ObjectConverterTypeFilter.Create(_objectConverters);
        _immutableCrossingFilter = ImmutableCrossingTypeFilter.Create(Options.Interop.ImmutableCrossingTypes);
        _enumsAsStrings = Options.Interop.EnumConversion == EnumConversionMode.String;
    }

    /// <summary>
    /// Refuses a host entry that would run script while this engine is still being constructed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfConstructionInFlight()
    {
        if (!_constructionComplete)
        {
            ThrowConstructionInFlight();
        }
    }

    /// <summary>
    /// Refuses a host reaching for this engine's constraint instances before they have been built.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfConstraintsNotBuilt()
    {
        if (!_constructionComplete)
        {
            ThrowConstraintsNotBuilt();
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowConstraintsNotBuilt()
    {
        Throw.InvalidOperationException(
            "This engine's constraints are not built yet. They are built from Options.Constraints once every "
            + "callback registered with Options.Configure has run, so that a limit one of those callbacks "
            + "registers or changes is the limit the engine gets. Write it on the options from the callback "
            + "(Options.LimitStatements and the rest of Options.Constraints), and reach the engine's own "
            + "constraint instances once the constructor has returned.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowConstructionInFlight()
    {
        Throw.InvalidOperationException(
            "This engine cannot run script yet, because it is still being constructed. A callback registered "
            + "with Options.Configure runs from Options.Apply, before the engine's own globals (System, "
            + "importNamespace, clrHelper, require, the opt-in web APIs) are installed and before an "
            + "untrusted-code profile is re-applied, so a script run from there would see an incomplete "
            + "realm. Run it once the constructor has returned instead.");
    }

    /// <summary>
    /// Re-derives <see cref="_extensionMethods"/> from the registration list as it stands once every
    /// configuration callback has run. Called by <see cref="Options.Apply"/>, immediately before the attach
    /// that reads that list.
    /// </summary>
    /// <remarks>
    /// A callback registered with <c>Options.Configure</c> is a documented place to finish configuring an
    /// engine, and a container type registered from one used to reach neither the prototypes nor
    /// <see cref="Runtime.Interop.TypeResolver"/> (sebastienros/jint#3568) — while the attach beside this call
    /// read the grown list and ran anyway, so one half of the feature already behaved as if it were supported.
    /// <para>
    /// Unconditional, so that a callback which <em>clears</em> the registry — which is what an untrusted-code
    /// profile does on its way through <c>Apply</c> — drops the lookup as well as the attach.
    /// <see cref="Runtime.Interop.Reflection.ExtensionMethodCache.Build"/> answers an empty registry from a
    /// count check and an unchanged one from its intern table, so the common engine pays nothing and an
    /// extension-method host pays one lookup against the prototype sweep that follows.
    /// </para>
    /// </remarks>
    internal void RefreshExtensionMethods()
    {
        _extensionMethods = ExtensionMethodCache.Build(Options.Interop.ExtensionMethodTypes);
    }

    private static void ValidateModuleOptions(Options.ModuleOptions modules)
    {
        if (modules.MaxModuleCount is <= 0 and not int.MaxValue)
        {
            throw new ArgumentException("MaxModuleCount must be positive or int.MaxValue.", nameof(modules));
        }

        if (modules.MaxTotalModuleSourceBytes is <= 0 and not long.MaxValue)
        {
            throw new ArgumentException("MaxTotalModuleSourceBytes must be positive or long.MaxValue.", nameof(modules));
        }

        if (modules.MaxModuleGraphDepth is <= 0 and not int.MaxValue)
        {
            throw new ArgumentException("MaxModuleGraphDepth must be positive or int.MaxValue.", nameof(modules));
        }

        if (modules.MaxModuleResolutionHops is <= 0 and not int.MaxValue)
        {
            throw new ArgumentException("MaxModuleResolutionHops must be positive or int.MaxValue.", nameof(modules));
        }
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

    /// <summary>
    /// Gets the configuration this engine was built from, frozen: every setter on it throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the instance the host handed to <see cref="Engine(Options)"/>, so engines built from one
    /// <see cref="Options"/> read back one object, and <c>new Engine()</c> reads back a process-wide default.
    /// </para>
    /// <para>
    /// An engine built through <see cref="OptionsExtensions.ForUntrustedCode"/> keeps a private hardened
    /// copy, so this answers what the engine actually runs under rather than what the host declared.
    /// </para>
    /// <para>
    /// <c>Engine.WebApi.Enable</c> replaces it with a copy owning its own web-API subtree, so read it again
    /// after that call rather than caching the reference across one.
    /// </para>
    /// <para>
    /// The freeze covers the settings, not the objects they name: a
    /// <see cref="Jint.Runtime.Interop.TypeResolver"/>, a module loader or an <c>HttpClient</c> read back
    /// here is still the host's own mutable object.
    /// </para>
    /// </remarks>
    public Options Options
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
        return _parserOptionsOverride
               ?? _executionContexts?.GetActiveScriptOrModule()?.ParserOptions
               ?? _executionContexts?.GetActiveParserOptions()
               ?? _defaultParserOptions;
    }

    internal ParsingConstraints GetActiveParsingConstraints()
    {
        if (_parsingConstraintsOverride is { } parsingConstraintsOverride)
        {
            return parsingConstraintsOverride;
        }

        return _executionContexts?.GetActiveScriptOrModule() is { } owner
            ? _parsingConstraints.Combine(owner.ParsingConstraints)
            : _parsingConstraints;
    }

    internal JintParser GetParserFor(ScriptParsingOptions parsingOptions)
    {
        var parsingConstraints = GetActiveParsingConstraints().Combine(parsingOptions);
        if (ReferenceEquals(parsingOptions, ScriptParsingOptions.Default))
        {
            return parsingConstraints.Equals(_parsingConstraints)
                ? _defaultParser
                : JintParser.Create(_defaultParserOptions, in parsingConstraints);
        }

        return JintParser.Create(parsingOptions.GetParserOptions(Options), in parsingConstraints);
    }

    internal JintParser GetParserFor(ParserOptions parserOptions)
    {
        var parsingConstraints = GetActiveParsingConstraints();
        return ReferenceEquals(parserOptions, _defaultParserOptions)
               && parsingConstraints.Equals(_parsingConstraints)
            ? _defaultParser
            : JintParser.Create(parserOptions, in parsingConstraints);
    }

    internal JintParser CreateModuleParser(ParserOptions parserOptions, ModuleParsingOptions parsingOptions)
        => JintParser.Create(parserOptions, GetActiveParsingConstraints().Combine(parsingOptions));

    internal void CheckParsingSourceLength(long length) => GetActiveParsingConstraints().CheckSourceLength(length);

    internal void CheckParsingSourceLength(long length, IParsingOptions parsingOptions)
        => GetActiveParsingConstraints().Combine(parsingOptions).CheckSourceLength(length);

    internal int? MaxParsingSourceLength => GetActiveParsingConstraints().MaxSourceLength;

    internal ParsingConstraints CombineParsingConstraints(in ParsingConstraints parsingConstraints)
        => GetActiveParsingConstraints().Combine(in parsingConstraints);

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
        if (_memoryLimitConstraint is not null)
        {
            BeginImplicitMemoryOperation();
        }
    }

    internal void EnterExecutionContext(in ExecutionContext context)
    {
        _executionContexts.Push(in context);
        if (_memoryLimitConstraint is not null)
        {
            BeginImplicitMemoryOperation();
        }
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
        if (_memoryLimitConstraint is not null)
        {
            BeginImplicitMemoryOperation();
        }
    }

    /// <summary>
    /// Registers a delegate with given name. Delegate becomes a JavaScript function that can be called.
    /// </summary>
    /// <remarks>
    /// <b>This is the one <c>SetValue</c> overload whose property attributes differ.</b> A delegate is
    /// installed with <see cref="PropertyFlag.NonEnumerable"/> — writable and configurable, but
    /// <em>not</em> enumerable, which is what ECMA-262 §17 gives a built-in global function — while every
    /// other overload goes through <see cref="ObjectInstance.Set(JsValue, JsValue)"/> and produces the ordinary
    /// configurable/enumerable/writable data property. So a delegate registered here does not appear in
    /// <c>Object.keys(globalThis)</c>, a <c>for..in</c> over the global object, or
    /// <c>JSON.stringify(globalThis)</c>, and every other value does. To install a host function with the
    /// ordinary attributes, build the function value yourself and use
    /// <see cref="SetValue(string, JsValue)"/>:
    /// <code>
    /// engine.SetValue("log", new ClrFunction(engine, "log", (_, args) => { /* … */ return JsValue.Undefined; }));
    /// </code>
    /// </remarks>
    public Engine SetValue(string name, Delegate value)
    {
        using var ownership = EnterHostCall();
        GlobalValueRegistration.RegisterDelegate(this, Realm.GlobalObject, name, value);
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
        GlobalValueRegistration.Register(Realm.GlobalObject, name, value);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    /// <remarks>
    /// This overload binds only where the argument's static type is <see cref="object"/> — a typed
    /// variable picks <see cref="SetValue{T}(string, T)"/>, whose type parameter carries
    /// <c>[DynamicallyAccessedMembers]</c> and therefore preserves what Jint reflects over. Here there is
    /// no type to annotate, which is the whole of the difference.
    /// </remarks>
    [RequiresUnreferencedCode(GlobalValueRegistration.RequiresUnreferencedCodeMessage)]
    public Engine SetValue(string name, object? obj)
    {
        using var ownership = EnterHostCall();
        GlobalValueRegistration.RegisterObject(this, Realm.GlobalObject, name, obj);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue(string name, [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        using var ownership = EnterHostCall();
        GlobalValueRegistration.RegisterType(this, Realm.GlobalObject, name, type);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable, creates an interop wrapper when needed.
    /// </summary>
    public Engine SetValue<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name, T? obj)
    {
        using var ownership = EnterHostCall();
        GlobalValueRegistration.RegisterTyped(this, Realm.GlobalObject, name, obj);
        return this;
    }

    /// <summary>
    /// Registers an array as variable, creates an interop wrapper when needed. Behaves exactly like
    /// <see cref="SetValue{T}(string, T)"/>; it exists so that the annotation lands on the
    /// <em>element</em> type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// C# picks this over <see cref="SetValue{T}(string, T)"/> for any array argument, because a parameter
    /// of type <c>T[]</c> is more specific than one of type <c>T</c>. Nothing about what the engine does
    /// changes — both bodies are the same call — so this is purely about what a trimmer is told.
    /// </para>
    /// <para>
    /// What it fixes is that <c>SetValue("items", companies)</c> used to infer <c>T = Company[]</c>, and
    /// <c>[DynamicallyAccessedMembers]</c> on an array type preserves the public members of
    /// <see cref="Array"/> — never <c>Company</c>'s. So the one thing script actually reaches,
    /// <c>items[0].name</c>, was the one thing not preserved. Inferring <c>T = Company</c> annotates the
    /// type whose members are resolved. It also spares an embedder four <c>IL3050</c> diagnostics per
    /// registration: preserving all public methods of an array type reaches
    /// <see cref="Array.CreateInstance(Type, int)"/> and its overloads, which are
    /// <c>[RequiresDynamicCode]</c> and have nothing to do with reading elements.
    /// </para>
    /// </remarks>
    public Engine SetValue<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name, T[]? obj)
    {
        using var ownership = EnterHostCall();
        GlobalValueRegistration.RegisterArray(this, Realm.GlobalObject, name, obj);
        return this;
    }

    internal void LeaveExecutionContext()
    {
        if (_implicitMemoryContextDepth != 0 && _implicitMemoryContextDepth == _executionContexts.Count)
        {
            LeaveImplicitMemoryOperation();
            return;
        }

        _executionContexts.Pop();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LeaveImplicitMemoryOperation()
    {
        var memoryLimit = _memoryLimitConstraint!;
        var segment = _implicitMemorySegment;
        try
        {
            memoryLimit.Check();
        }
        finally
        {
            try
            {
                memoryLimit.EndSegment(in segment);
            }
            finally
            {
                _implicitMemoryContextDepth = 0;
                _implicitMemorySegment = default;
                _executionContexts.Pop();
            }
        }
    }

    private void BeginImplicitMemoryOperation()
    {
        var memoryLimit = _memoryLimitConstraint;
        if (memoryLimit is null
            || _executionContexts.Count <= 1
            || memoryLimit.CurrentOperationState is not null
            || _implicitMemoryContextDepth != 0)
        {
            return;
        }

        try
        {
            var state = memoryLimit.BeginEntry();
            _implicitMemorySegment = memoryLimit.BeginSegment(state);
            _implicitMemoryContextDepth = _executionContexts.Count;
        }
        catch
        {
            _executionContexts.Pop();
            throw;
        }
    }

    /// <summary>
    /// Ends the implicit operation without its exit check, for a caller that is about to throw a failure of
    /// its own. <see cref="LeaveImplicitMemoryOperation"/> checks the memory budget on the way out, and a
    /// limit raised there would replace the failure being thrown — a memory error reported where the script
    /// actually ran out of time, or out of stack. The constraint that fired is the one that has to reach the
    /// host, so the operation is ended silently and the caller's exception propagates.
    /// </summary>
    internal void AbandonImplicitMemoryOperation()
    {
        if (_implicitMemoryContextDepth == 0)
        {
            return;
        }

        var segment = _implicitMemorySegment;
        _memoryLimitConstraint!.EndSegment(in segment);
        _implicitMemoryContextDepth = 0;
        _implicitMemorySegment = default;
    }

    /// <summary>
    /// Ends this evaluation cycle because an operation exceeded its allocation budget: the same teardown a
    /// <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> performs, minus the globals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="MemoryLimitExceededException"/> is not catchable by script, so the operation that raised
    /// it is over — but what it had scheduled is not. Its queued jobs, its pending module loads, its timers
    /// and its host-stream bridges would otherwise go on running, on an engine whose budget the very next
    /// check fails again, with an open file handle each. Discarding them here is what makes the limit end
    /// the work rather than merely report on it, and the generation bump is the fence for the completions
    /// that are still in flight on other threads.
    /// </para>
    /// <para>
    /// The teardown is best-effort: it runs host code (a worker host learning its connection ended, a host
    /// stream being disposed), and a host that throws there must not replace the memory failure with its own
    /// exception. It comes back to be attached as the inner exception instead, and the generation still
    /// moves — the same "always bump, never restore" rule the snapshot restore keeps, and for the same
    /// reason: half a teardown must not leave the previous cycle's completions looking current.
    /// </para>
    /// </remarks>
    /// <returns>Whatever the teardown threw, or <see langword="null"/>.</returns>
    internal Exception? AbortMemoryLimitedOperation()
    {
        Exception? cleanupFailure = null;
        try
        {
            ResetTransientEvaluationState();
        }
        catch (Exception exception)
        {
            cleanupFailure = exception;
        }
        finally
        {
            EventLoop.NextGeneration();
        }

        return cleanupFailure;
    }

    internal void ResetConstraints()
    {
        foreach (var constraint in _constraints)
        {
            if (!ReferenceEquals(constraint, _memoryLimitConstraint))
            {
                constraint.Reset();
            }
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
    /// Evaluates code and returns the completion value.
    /// </summary>
    /// <remarks>
    /// <see cref="Execute(string, string, ScriptParsingOptions)"/> is the same operation returning the
    /// engine instead, for chaining; it discards the completion value and differs in nothing else.
    /// </remarks>
    /// <param name="code">The JavaScript source to evaluate.</param>
    /// <param name="source">The name the source is known by in stack traces and the debugger. Defaults to <c>&lt;anonymous&gt;</c>.</param>
    /// <param name="parsingOptions">Parsing options for this evaluation. Defaults to the engine's <see cref="Options.Parsing"/>.</param>
    /// <returns>The completion value of the script.</returns>
    public JsValue Evaluate(string code, string? source = null, ScriptParsingOptions? parsingOptions = null)
    {
        using var ownership = EnterHostCall();
        return Evaluate(ParseForExecution(code, source, parsingOptions));
    }

    /// <summary>
    /// Evaluates a prepared script and returns the completion value.
    /// </summary>
    public JsValue Evaluate(in Prepared<Script> preparedScript)
        => ExecuteForCompletion(in preparedScript);

    /// <summary>
    /// Executes code into engine and returns the engine instance (useful for chaining).
    /// </summary>
    /// <remarks>
    /// <see cref="Evaluate(string, string, ScriptParsingOptions)"/> is the same operation returning the
    /// script's completion value instead; the two differ in nothing else.
    /// </remarks>
    /// <param name="code">The JavaScript source to execute.</param>
    /// <param name="source">The name the source is known by in stack traces and the debugger. Defaults to <c>&lt;anonymous&gt;</c>.</param>
    /// <param name="parsingOptions">Parsing options for this execution. Defaults to the engine's <see cref="Options.Parsing"/>.</param>
    /// <returns>This engine, for chaining.</returns>
    public Engine Execute(string code, string? source = null, ScriptParsingOptions? parsingOptions = null)
    {
        using var ownership = EnterHostCall();
        return Execute(ParseForExecution(code, source, parsingOptions));
    }

    /// <summary>
    /// Executes a prepared script into engine and returns the engine instance (useful for chaining).
    /// </summary>
    public Engine Execute(in Prepared<Script> preparedScript)
    {
        ExecuteForCompletion(in preparedScript);
        return this;
    }

    /// <summary>
    /// Shared parse for the source-taking <see cref="Evaluate(string, string, ScriptParsingOptions)"/> and
    /// <see cref="Execute(string, string, ScriptParsingOptions)"/>. Host-supplied parsing options carry a
    /// <see cref="ScriptParsingOptions.SourceOffset"/>, which the engine's own defaults never do, so the
    /// two cases reach the parser differently. Called with the host-call scope already held.
    /// </summary>
    private Prepared<Script> ParseForExecution(string code, string? source, ScriptParsingOptions? parsingOptions)
    {
        // Before the bracket below, because the string entries parse first and the default parser is one of
        // the option-derived fields taken after Options.Apply.
        ThrowIfConstructionInFlight();

        if (parsingOptions is null)
        {
            var defaultParser = GetParserFor(_defaultParserOptions);
            return new Prepared<Script>(
                defaultParser.ParseScriptGuarded(Realm, code, source: source ?? "<anonymous>", strict: _isStrict),
                _defaultParserOptions,
                parsingConstraints: defaultParser.Constraints);
        }

        var parser = GetParserFor(parsingOptions);
        return new Prepared<Script>(
            parser.ParseScriptGuarded(Realm, code, parsingOptions.SourceOffset, source ?? "<anonymous>", _isStrict),
            parser.Options,
            parsingConstraints: parser.Constraints);
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
        var parsingConstraints = _parsingConstraints.Combine(preparedScript.ParsingConstraints);
        var strict = _isStrict || script.Strict;
        // The lambda captures the locals (script, parserOptions), never the `in` parameter.
        return ExecuteWithConstraints(strict, () => ScriptEvaluation(
            new ScriptRecord(
                Realm,
                script,
                script.Location.SourceFile,
                parsingConstraints,
                parserOptions),
            parserOptions,
            in parsingConstraints));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-scriptevaluation
    /// </summary>
    private JsValue ScriptEvaluation(
        ScriptRecord scriptRecord,
        ParserOptions parserOptions,
        in ParsingConstraints parsingConstraints)
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

        // Depth of the call stack when THIS evaluation began. A nested evaluation (a host callback
        // inside a running script calling Execute/Evaluate) sits above live frames belonging to the
        // outer run; an unhandled throw here must unwind only the frames this evaluation pushed,
        // not clear the whole stack (which desynced the outer run's balanced pops and truncated its
        // subsequently captured stack traces).
        var callStackDepthOnEntry = CallStack.Count;
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
                CallStack.TrimTo(callStackDepthOnEntry);
                throw;
            }

            if (result.Type == CompletionType.Throw)
            {
                var ex = new JavaScriptException(result.GetValueOrDefault()).SetJavaScriptCallstack(this, result.Location);
                CallStack.TrimTo(callStackDepthOnEntry);
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
    /// Builds a promise this engine hands to script, together with the two functions that settle it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The settle functions take a CLR value and convert it inside the enqueued job — on the engine's
    /// thread — so a host settling from a background thread never has to build an engine-affine value where
    /// it stands. A <see cref="JsValue"/> passed in is returned by the conversion unchanged.
    /// </para>
    /// <para>
    /// <paramref name="drainInline"/> decides what a settle does after enqueueing. The host-facing
    /// registration drains when it can claim exclusive ownership, so a resolve from a host thread makes
    /// progress without waiting for the next pump. The CLR-task registration behind
    /// <see cref="ExperimentalFeature.TaskInterop"/> does not, because its continuation runs
    /// <c>ExecuteSynchronously</c> and can therefore fire on the engine's own thread in the middle of a
    /// running script — draining there would interleave queued jobs into the statement that started the
    /// task. Whether the two should converge is a separate question from what value they accept.
    /// </para>
    /// </remarks>
    /// <param name="drainInline">
    /// Whether a settle attempts to drain the event loop after enqueueing, when it can claim the engine.
    /// </param>
    /// <returns>a Promise instance and functions to either resolve or reject it</returns>
    internal ManualPromise RegisterPromise(bool drainInline = true)
    {
        var promise = new JsPromise(this)
        {
            _prototype = Realm.Intrinsics.Promise.PrototypeObject
        };

        var (resolve, reject) = promise.CreateResolvingFunctions();

        // The cycle this promise belongs to, read here on the engine thread. The settle below may run
        // arbitrarily later on a background thread, and stamping the job with the generation captured now
        // is what lets the drain tell "work this engine is still waiting for" from "work whose cycle a
        // RestoreGlobalSnapshot has since ended". For the task path this is also what carries a
        // fire-and-forget Task across a restore rather than settling it into the restored globals.
        var registration = CaptureEventLoopRegistration();

        Action<object?> SettleWith(Function settle) => value =>
        {
            // Both halves go on the event loop: the conversion as much as the call. The settle may be
            // invoked from a background thread (a Task.ContinueWith callback, an HTTP completion), and
            // JsValue.FromObject builds into this engine's realm, so running it here would be the very
            // race the enqueue exists to avoid.
            AddToEventLoop(() =>
            {
                settle.Call(JsValue.Undefined, [JsValue.FromObject(this, value)]);
            }, registration);

            // Signal the CompletedEvent so that UnwrapIfPromise knows there's work to process.
            promise.CompletedEvent.Set();

            // A completion may arrive on any thread. Drain inline only when this thread can claim
            // exclusive ownership; otherwise the next host turn or async waiter owns the drain.
            if (drainInline && TryEnterHostCall(out var ownership))
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
    /// Enqueues in-cycle work: the job joins the generation that is current right now, which is what every
    /// caller running on the engine thread wants. Work registered in one cycle and enqueued in a later one
    /// must use the overload below instead.
    /// </summary>
    internal void AddToEventLoop(Action continuation)
    {
        _eventLoop.Enqueue(new EventLoopJob(continuation, _eventLoop.Generation, CaptureMemoryLimitState()));
    }

    /// <summary>
    /// Enqueues work on behalf of an earlier registration, carrying both halves of what that registration
    /// captured: the cycle the work belongs to and the operation whose allocation budget its turn is charged
    /// to. Used by the promise settle closures, the timers and the web continuations, all of which can fire
    /// on a background thread long after the call that registered them returned.
    /// </summary>
    internal void AddToEventLoop(Action continuation, EventLoopRegistration registration)
    {
        _eventLoop.Enqueue(new EventLoopJob(
            continuation,
            registration.Generation,
            registration.MemoryState));
    }

    /// <summary>
    /// Enqueues work on behalf of an earlier registration that carries <em>no</em> allocation budget, so the
    /// turn it runs is outside every operation's memory accounting.
    /// </summary>
    /// <remarks>
    /// The accounted overload above is the one a new caller should reach for. This one exists for the
    /// persistent external events whose registration is not part of any single engine operation — a WebSocket
    /// or <c>EventSource</c> frame, a <c>MessagePort</c> or <c>BroadcastChannel</c> delivery, host
    /// cancellation, or finalization-registry cleanup. They keep the generation stamp for the reason every
    /// cross-cycle enqueue does; what they do not have is one originating operation whose budget the turn
    /// belongs to, and a source that delivers for as long as the script leaves it open must not accumulate
    /// against one either — each delivery begins a budget of its own. The finite continuations — a
    /// <c>setTimeout</c>, a <c>fetch</c>, one host-stream read or write — use
    /// <see cref="AddToEventLoop(Action, EventLoopRegistration)"/> instead, and so does a <c>setInterval</c>,
    /// whose registration carries the cycle but deliberately no budget (see <c>TimerEntry</c>).
    /// </remarks>
    internal void AddToEventLoop(Action continuation, int generation)
    {
        _eventLoop.Enqueue(new EventLoopJob(continuation, generation, memoryState: null));
    }

    /// <summary>
    /// Enqueues a promise reaction job without allocating a closure: the (reaction, argument)
    /// pair is the job's state. See <see cref="EventLoopJob"/>.
    /// </summary>
    internal void AddToEventLoop(PromiseReaction reaction, JsValue value)
    {
        _eventLoop.Enqueue(new EventLoopJob(reaction, value, _eventLoop.Generation, reaction.MemoryState));
    }

    /// <summary>
    /// The cycle a piece of work registered now belongs to. Read at registration time and carried by the
    /// work, so that a completion arriving from a background thread after the cycle ended is discarded
    /// rather than applied — see <see cref="EventLoop.Generation"/>.
    /// </summary>
    internal int EventLoopGeneration => _eventLoop.Generation;

    internal EventLoopRegistration CaptureEventLoopRegistration()
        => new(_eventLoop.Generation, CaptureMemoryLimitState());

    /// <summary>
    /// Queues the engine-thread half of an asynchronous module load. Separate from
    /// <see cref="AddToEventLoop(Action, EventLoopRegistration)"/> only in name, to keep the module loader's
    /// one cross-thread entry point findable.
    /// </summary>
    internal void EnqueueModuleLoadCompletion(Action continuation, EventLoopRegistration registration)
    {
        _eventLoop.Enqueue(new EventLoopJob(
            continuation,
            registration.Generation,
            registration.MemoryState));
    }

    internal MemoryLimitConstraint.OperationState? CaptureMemoryLimitState()
        => _memoryLimitConstraint?.CaptureOperationState();

    internal void RunEventLoopJob(in EventLoopJob job)
    {
        var memoryLimit = _memoryLimitConstraint;
        if (memoryLimit is null)
        {
            job.Run(this);
            return;
        }

        var state = memoryLimit.ContinueOrBeginEntry(job.MemoryState);
        var segment = memoryLimit.BeginSegment(state);
        try
        {
            memoryLimit.Check();
            job.Run(this);
            memoryLimit.Check();
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            memoryLimit.Check();
            throw;
        }
        finally
        {
            memoryLimit.EndSegment(in segment);
        }
    }

    /// <summary>
    /// Runs <paramref name="callback"/> as one accounted turn of <paramref name="operationState"/>, for the
    /// queues that promote their own work rather than enqueueing a job per item — the idle-callback queue
    /// and the scheduler's shared pump, whose one job runs whichever task is next and so cannot carry any
    /// single task's registration. A <see langword="null"/> state begins an ordinary budget, exactly as a
    /// job with no captured state does.
    /// </summary>
    internal void RunWithMemoryAccounting(
        MemoryLimitConstraint.OperationState? operationState,
        Action callback)
    {
        var memoryLimit = _memoryLimitConstraint;
        if (memoryLimit is null)
        {
            callback();
            return;
        }

        var state = memoryLimit.ContinueOrBeginEntry(operationState);
        var segment = memoryLimit.BeginSegment(state);
        try
        {
            memoryLimit.Check();
            callback();
            memoryLimit.Check();
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            memoryLimit.Check();
            throw;
        }
        finally
        {
            memoryLimit.EndSegment(in segment);
        }
    }

    /// <summary>
    /// Called by the host when a promise rejection is tracked/untracked.
    /// Raises the <see cref="TaskOperations.PromiseRejectionTracker"/> event.
    /// </summary>
    internal void OnPromiseRejectionTracker(JsPromise promise, PromiseRejectionOperation operation)
    {
        // Through the backing field, not the property: a host that never reached for the facet cannot have
        // subscribed to the event, so materializing it here would allocate on a path that has nothing to
        // raise — and a rejection with no handler is exactly the path a host under load is already on.
        _tasks?.RaisePromiseRejectionTracker(promise, operation);

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
    /// <see cref="Native.JsValue.UnwrapIfPromise(TimeSpan)"/>. A non-positive
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

    // CA1822: on the target frameworks without TimeProvider these two read no instance state. They stay
    // instance members on every one of them so that the wait loop can call them unconditionally and keep
    // conditional compilation out of it - the rule Engine.Pump.cs states once for the same reason.
#pragma warning disable CA1822

    /// <summary>
    /// A reading of the clock <see cref="DrainEventLoopUntil"/> bounds itself by. Declared on every target
    /// framework with the conditional compilation inside, the way every pump hook is, so the wait loop needs
    /// none of its own.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long GetWaitTimestamp()
    {
#if NET8_0_OR_GREATER
        return _waitClock is null ? Stopwatch.GetTimestamp() : _waitClock.GetTimestamp();
#else
        return Stopwatch.GetTimestamp();
#endif
    }

    /// <summary>
    /// The tick frequency <see cref="GetWaitTimestamp"/>'s readings are expressed in.
    /// </summary>
    internal long WaitTimestampFrequency
    {
        get
        {
#if NET8_0_OR_GREATER
            return ConstraintClock.FrequencyOf(_waitClock);
#else
            return Stopwatch.Frequency;
#endif
        }
    }

#pragma warning restore CA1822

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

        // Hold the callback-admission window for the whole drain, not only for the blocking wait
        // below. A drain is exactly the frame the thread-safety contract lets an authorized
        // cross-thread callback wait in, and dropping the reservation across RunAvailableContinuations
        // every iteration turned that promise into a scheduling coin flip (sebastienros/jint#3206).
        // Unconditionally, including for a nested drain: see the window's own remarks for why the
        // claiming-scope guard a park needs would be both unnecessary and, for a blocking import, wrong.
        // Note also that guarding *this line* alone would not even narrow admission. The per-iteration
        // suspension below would then own its reservation rather than finding this one, and a suspension
        // that owns one hands it to the enclosing entry instead of dropping it (ResumeSuspendedHostCall) -
        // so the window would survive the drain and stay open for the rest of that entry. Measured: a
        // callback dispatched after a nested drain returned is refused as things stand and admitted with
        // that guard in place. The guard is not a narrowing; it is a widening in duration.
        using var admission = OpenHostCallbackAdmissionWindow();

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
            // Monotonic, and on the clock the host named. Two things follow, and the second is the point of
            // the first. A deadline read off DateTime.UtcNow - which this was - is cut short by an NTP step
            // forwards and stretched by one backwards, which is exactly the reason the pump's own ceiling
            // has always been measured on Stopwatch instead (see Engine.Pump.ElapsedSince). And once the
            // reading goes through the engine's clock, Options.Constraints.TimeProvider reaches the promise
            // budget that sits beside it on the same options group, so "the wait ended on the budget it
            // names" is a claim a test can make exactly rather than by racing a stopwatch against the
            // ten-second default (sebastienros/jint#3406).
            var hasTimeout = timeout > TimeSpan.Zero;
            var frequency = WaitTimestampFrequency;
            var deadline = hasTimeout ? GetWaitTimestamp() + ConstraintClock.ToTimestampTicks(timeout, frequency) : 0;
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
                    var remainingTicks = deadline - GetWaitTimestamp();
                    if (remainingTicks <= 0)
                    {
                        break;
                    }

                    var remaining = ConstraintClock.ToTimeSpan(remainingTicks, frequency);
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
                    using (SuspendHostCallForCallbacks(yieldForCallbacks: true))
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
        // Avoid allocating the enumerator because we run this loop very often. The try costs nothing until
        // something throws; see the handler.
        try
        {
            foreach (var constraint in _constraints)
            {
                constraint.Check();
            }
        }
        catch (Exception exception) when (_implicitMemoryContextDepth != 0 && exception is not JavaScriptException)
        {
            // The implicit operation ends with a memory Check() of its own, which would raise a limit over
            // the top of the constraint that actually fired — a memory failure reported where the script ran
            // out of time. End it silently instead. The one exception left out is a user constraint raising a
            // JavaScriptException: script can catch that one and carry on, and abandoning the operation under
            // it would hand the script a fresh budget.
            AbandonImplicitMemoryOperation();
            throw;
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
        // Avoid allocating the enumerator because we run this loop very often. The try costs nothing until
        // something throws; see the handler.
        try
        {
            foreach (var constraint in _exactConstraints)
            {
                constraint.Check();
            }
        }
        catch (Exception exception) when (_implicitMemoryContextDepth != 0 && exception is not JavaScriptException)
        {
            // See RunBeforeExecuteStatementChecks.
            AbandonImplicitMemoryOperation();
            throw;
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

        // Before the constraints, so the sample that shows where a script ran out of its budget is taken
        // rather than lost to the throw.
        _sampler?.Check(this);

        try
        {
            foreach (var constraint in _amortizedConstraints)
            {
                constraint.Check();
            }
        }
        catch (Exception exception) when (_implicitMemoryContextDepth != 0 && exception is not JavaScriptException)
        {
            // See RunBeforeExecuteStatementChecks.
            AbandonImplicitMemoryOperation();
            throw;
        }
    }

    internal void CheckInteropProjectionConstraints()
    {
        _memoryLimitConstraint?.Check();

        if (_executionContexts.Count > 1)
        {
            CheckAmortizedConstraints();
            return;
        }

        // Host-side projection is not an execution entry, so an ordinary timeout has no active
        // budget to enforce. Cancellation and a host-armed operation deadline do span entries.
        foreach (var constraint in _amortizedConstraints)
        {
            if (constraint is CancellationConstraint or OperationDeadlineConstraint)
            {
                constraint.Check();
            }
        }
    }

    /// <summary>
    /// Re-checks constraints that can change materially while user CLR code is running. The per-statement
    /// amortization bounds timeout/cancellation detection latency in statement count, which tracks wall-clock
    /// time only while statements stay cheap; a single host call can take arbitrarily long, so interop call
    /// sites re-check after the host code returns. The memory constraint is exact rather than amortized, but
    /// belongs here too: a callback can allocate past the budget and return from the script's final statement,
    /// leaving no later before-statement check to observe it.
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
            _memoryLimitConstraint?.Check();
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
    /// Invokes a function held by a property of the global object.
    /// </summary>
    /// <inheritdoc cref="Invoke(string, object, object[])" path="/remarks"/>
    /// <param name="propertyName">The name of a property of the global object; see the remarks.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public JsValue Invoke(string propertyName, params object?[] arguments)
    {
        return Invoke(propertyName, thisObj: null, arguments);
    }

    /// <summary>
    /// Invokes a function held by a property of the global object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="propertyName"/> is read as a <b>single property name of the global object</b>, the
    /// same name <see cref="SetValue(string, JsValue)"/> writes. It is never parsed as JavaScript, and a
    /// dot in it is part of the name rather than a path separator — <c>Invoke("foo.bar")</c> looks for a
    /// global literally called <c>foo.bar</c>. To reach a nested callable, read it and use the
    /// <see cref="Invoke(JsValue, object[])"/> overload:
    /// </para>
    /// <code>
    /// var bar = engine.GetValue(engine.GetValue("foo"), "bar");
    /// engine.Invoke(bar, arguments);
    /// </code>
    /// <para>
    /// A property of the global object is what a <c>var</c> or <c>function</c> declaration at top level
    /// creates. A <c>class</c>, <c>let</c> or <c>const</c> declaration creates a <em>lexical</em> binding of
    /// the global environment instead, which is not a property of the global object and so is not reachable
    /// by name here — <c>engine.Evaluate("MyClass")</c> is what reads one.
    /// </para>
    /// <para>
    /// Arguments are CLR objects and are converted with <see cref="JsValue.FromObject"/>. Use
    /// <see cref="Call(JsValue, JsValue[])"/> when they are already <see cref="JsValue"/>s.
    /// </para>
    /// </remarks>
    /// <param name="propertyName">The name of a property of the global object; see the remarks.</param>
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
    /// Invokes the value as a function.
    /// </summary>
    /// <inheritdoc cref="Invoke(JsValue, object, object[])" path="/remarks"/>
    /// <param name="value">The function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    /// <seealso cref="Call(JsValue, JsValue[])"/>
    public JsValue Invoke(JsValue value, params object?[] arguments)
    {
        return Invoke(value, thisObj: null, arguments);
    }

    /// <summary>
    /// Invokes the value as a function.
    /// </summary>
    /// <remarks>
    /// This is the CLR-argument form: <paramref name="thisObj"/> and every element of
    /// <paramref name="arguments"/> are converted with <see cref="JsValue.FromObject"/>.
    /// <see cref="Call(JsValue, JsValue, JsValue[])"/> is the <see cref="JsValue"/>-argument form, which
    /// passes them through untouched. The two differ in nothing else.
    /// </remarks>
    /// <param name="value">The function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    /// <seealso cref="Call(JsValue, JsValue, JsValue[])"/>
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

    /// <summary>
    /// The body of a bracketed host entry, taking whatever it needs as parameters instead of closing over
    /// it — which is what lets an entry over source text the caller holds as a
    /// <see cref="ReadOnlySpan{T}"/> share one bracket with an entry over a closure.
    /// </summary>
    /// <remarks>
    /// A <see cref="ReadOnlySpan{T}"/> cannot be captured by a <see cref="Func{T}"/>, and that is the whole
    /// reason this delegate exists rather than a second copy of the bracket for span-taking entries. Two
    /// implementations of a bracket this subtle — the nesting rule, the reset pair, the memory segment —
    /// would drift, and the half that drifted would be the half nobody looks at.
    /// <see cref="ExecuteWithConstraints{T}(bool, Func{T})"/> is expressed in terms of this one and costs a
    /// single extra delegate invocation per host entry to be certain there is only one.
    /// </remarks>
    internal delegate TResult HostEntryCallback<TState, TResult>(ReadOnlySpan<char> source, TState state);

    internal T ExecuteWithConstraints<T>(bool strict, Func<T> callback)
        => ExecuteWithConstraints(strict, default, callback, static (_, c) => c());

    /// <summary>
    /// Runs <paramref name="callback"/> as one host entry into this engine: claims the engine, arms this
    /// run's execution constraints if it is not nested inside another, and accounts its allocations.
    /// </summary>
    internal TResult ExecuteWithConstraints<TState, TResult>(
        bool strict,
        ReadOnlySpan<char> source,
        TState state,
        HostEntryCallback<TState, TResult> callback)
    {
        // This is the one bracket every entry that runs script goes through, so it is the one place the
        // refusal has to be. A configuration callback reaches an engine whose call stack, default parser and
        // option-derived fields are still being taken (sebastienros/jint#3581).
        ThrowIfConstructionInFlight();

        using var ownership = EnterHostCall();

        // A nested call — a host callback inside a running script calling back into the engine —
        // must not re-arm the outer run's constraints: the outer budget (time/statements) keeps
        // applying to everything the outer script triggers, otherwise `while (true) hostCallback()`
        // where the callback re-enters the engine would reset the timeout on every iteration and
        // run forever. Host-entry depth covers callbacks that push no interpreter frame; execution-context
        // depth covers async and generator resumes that have no host entry.
        var isNested = _hostEntryDepth > 0 || _executionContexts.Count > 1;
        if (_memoryLimitConstraint is not null)
        {
            return ExecuteWithMemoryLimit(strict, source, state, callback, isNested);
        }

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

        // A throw inside this entry leaves it as a CLR exception for whoever called in, so a `catch` in the
        // script that called the host is not on its way out; see the field's own remarks.
        var previousTryCatchDepth = _tryCatchDepth;
        _tryCatchDepth = 0;
        try
        {
            return callback(source, state);
        }
        finally
        {
            _tryCatchDepth = previousTryCatchDepth;
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private TResult ExecuteWithMemoryLimit<TState, TResult>(
        bool strict,
        ReadOnlySpan<char> source,
        TState state,
        HostEntryCallback<TState, TResult> callback,
        bool isNested)
    {
        var memoryLimit = _memoryLimitConstraint!;
        if (!isNested)
        {
            ResetConstraints();
        }

        var memoryState = memoryLimit.CurrentOperationState ?? memoryLimit.BeginEntry();
        var memorySegment = memoryLimit.BeginSegment(memoryState);
        _hostEntryDepth++;

        var previousStrict = strict && ReplaceTopStrict(true);

        // Same host-entry boundary as the constraint-only path above.
        var previousTryCatchDepth = _tryCatchDepth;
        _tryCatchDepth = 0;
        try
        {
            memoryLimit.Check();
            var result = callback(source, state);
            memoryLimit.Check();
            return result;
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            memoryLimit.Check();
            throw;
        }
        finally
        {
            _tryCatchDepth = previousTryCatchDepth;
            if (strict)
            {
                ReplaceTopStrict(previousStrict);
            }
            _hostEntryDepth--;
            memoryLimit.EndSegment(in memorySegment);
            if (!isNested)
            {
                ResetConstraints();
            }
            _agent.ClearKeptObjects();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal T ExecuteWithMemoryAccounting<T>(Func<T> callback)
    {
        var memoryLimit = _memoryLimitConstraint;
        if (memoryLimit is null)
        {
            return callback();
        }

        var state = memoryLimit.CurrentOperationState ?? memoryLimit.BeginEntry();
        var segment = memoryLimit.BeginSegment(state);
        try
        {
            memoryLimit.Check();
            var result = callback();
            memoryLimit.Check();
            return result;
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            memoryLimit.Check();
            throw;
        }
        finally
        {
            memoryLimit.EndSegment(in segment);
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
    /// Converts a script result to a detached CLR graph while enforcing depth, size, and output limits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Arrays, typed arrays, array buffers, maps, sets, and enumerable own object properties are copied.
    /// Cycles are rejected, as are functions and symbols that cannot form a detached data result. CLR
    /// wrappers return their existing host-owned target without walking it. Property access may invoke
    /// getters and proxy traps, so the conversion runs under the engine's execution constraints; those
    /// constraints and an outer request deadline remain required for untrusted code.
    /// </para>
    /// <para>
    /// This is the bounded way out of the engine, and it sits here rather than behind a facet for that
    /// reason: the unbounded one, <see cref="JsValue.ToObject()"/>, is a method on every value a host
    /// already holds, so a host that never finds this one takes the unbounded route by default. Pass
    /// <paramref name="limits"/> to override <see cref="Options.ResultLimits"/> for one call.
    /// </para>
    /// </remarks>
    public object? ConvertResult(JsValue value, ResultLimits? limits = null)
    {
        if (value is null)
        {
            Throw.ArgumentNullException(nameof(value));
        }

        if (value is ObjectInstance instance && !ReferenceEquals(instance.Engine, this))
        {
            Throw.ArgumentException("The value belongs to a different engine.", nameof(value));
        }

        var effectiveLimits = limits ?? Options.ResultLimits;
        return ExecuteWithConstraints(
            Options.Strict,
            () => ResultConverter.Convert(this, value, effectiveLimits));
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
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <remarks>
    /// This is the <see cref="JsValue"/>-argument form: the arguments are already engine values and are
    /// passed through untouched. <see cref="Invoke(JsValue, object[])"/> is the CLR-argument form, which
    /// converts each argument through <see cref="JsValue.FromObject"/> first. The two differ in nothing
    /// else.
    /// </remarks>
    /// <param name="callable">The callable.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    /// <seealso cref="Invoke(JsValue, object[])"/>
    public JsValue Call(JsValue callable, params JsCallArguments arguments)
        => Call(callable, thisObject: JsValue.Undefined, arguments);

    /// <summary>
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <inheritdoc cref="Call(JsValue, JsValue[])" path="/remarks"/>
    /// <param name="callable">The callable.</param>
    /// <param name="thisObject">Value bound as this.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    /// <seealso cref="Invoke(JsValue, object, object[])"/>
    public JsValue Call(JsValue callable, JsValue thisObject, JsCallArguments arguments)
    {
        JsValue Callback()
        {
            if (!callable.HasCall)
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
    /// Calls the constructor and returns the resulting object.
    /// </summary>
    /// <remarks>
    /// To construct something the host knows only by name, read the value first —
    /// <c>engine.Construct(engine.GetValue("MyCtor"), arguments)</c> for a <c>function</c> declaration or
    /// anything <see cref="SetValue(string, JsValue)"/> installed, and
    /// <c>engine.Construct(engine.Evaluate("MyClass"), arguments)</c> for a <c>class</c> declaration, which
    /// is a lexical binding of the global environment rather than a property of the global object.
    /// </remarks>
    /// <param name="constructor">The constructor to call.</param>
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
            AbandonImplicitMemoryOperation();
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
            AbandonImplicitMemoryOperation();
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
        // was ever handed to reachable. The same call ends every worker connection this engine is a party to,
        // in both directions; the host's OnWorkerEnded callbacks come back as a list and are run at the very
        // bottom of this method, once the engine has finished letting go of everything else.
        var endedWorkers = _webApi?.Dispose();
#endif

        if (_objectWrapperCache is not null)
        {
#if SUPPORTS_WEAK_TABLE_CLEAR
            _objectWrapperCache.Clear();
#else
            // we can expect that reflection is OK as we've been generating object wrappers already
            var clearMethod = _objectWrapperCache.GetType().GetMethod("Clear", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            clearMethod?.Invoke(_objectWrapperCache, []);
#endif
        }

#if NET8_0_OR_GREATER
        WebApiEngineState.NotifyWorkerHosts(endedWorkers);
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
