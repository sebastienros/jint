#if NET8_0_OR_GREATER
using System.Threading;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.Abort;

/// <summary>
/// An <c>AbortSignal</c> instance: the read-only half of an <c>AbortController</c>, and an
/// <see cref="JsEventTarget"/> so that a script can listen for the <c>abort</c> event.
/// <para>
/// https://dom.spec.whatwg.org/#interface-AbortSignal
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here runs on the engine's thread.</b> Aborting a signal runs abort algorithms and then
/// dispatches a JavaScript event, so it is not something a background thread may do — the one abort the
/// engine raises by itself, <c>AbortSignal.timeout()</c>, rides the timer queue and therefore happens inside
/// the pump like any other timer callback.
/// </para>
/// <para>
/// <b>The abort algorithms are the seam the rest of the web APIs use.</b> They run <i>before</i> the
/// <c>abort</c> event, which is what lets an in-flight operation stop before any script can observe the
/// abort, and they are emptied afterwards so nothing can hold an aborted signal's observers alive. The
/// engine registers one of its own ahead of them all: cancelling <see cref="CancellationToken"/>, the token a
/// CLR-side operation such as <c>fetch</c> links against.
/// </para>
/// <para>
/// One deliberate divergence: the specification's <i>dependent signals</i> and <i>source signals</i> are weak
/// sets, and these are ordinary lists. A composite built by <c>AbortSignal.any()</c> is therefore retained by
/// its sources until one of them aborts — at which point both lists are dropped, so an aborted signal
/// retains nothing and a source cannot accumulate composites across aborts. Making them weak would need the
/// specification's "must not be garbage collected while it has listeners" rule too, which .NET cannot express
/// as cheaply as a browser's garbage collector can.
/// </para>
/// <para>
/// The class is not sealed because <c>Jint.WebApi.Scheduling.JsTaskSignal</c> derives from it —
/// <c>TaskSignal</c> is an <c>AbortSignal</c> that also carries a priority,
/// https://wicg.github.io/scheduling-apis/#tasksignal.
/// </para>
/// </remarks>
internal class JsAbortSignal : JsEventTarget
{
    /// <summary>The event type an abort fires, and the one <c>onabort</c> is the handler for.</summary>
    internal const string AbortEventType = "abort";

    private static readonly JsString _abortEventName = new(AbortEventType);

    private List<Action>? _abortAlgorithms;
    private List<JsAbortSignal>? _dependentSignals;
    private List<JsAbortSignal>? _sourceSignals;
    private CancellationTokenSource? _cancellation;

    internal JsAbortSignal(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#abortsignal-aborted. The specification derives this from the abort reason
    /// being something other than <see langword="undefined"/>; it is an explicit flag here because that is
    /// the same answer without asking the reason a question about its identity.
    /// </summary>
    internal bool Aborted { get; private set; }

    /// <summary>https://dom.spec.whatwg.org/#abortsignal-abort-reason.</summary>
    internal JsValue Reason { get; private set; } = Undefined;

    /// <summary>
    /// https://dom.spec.whatwg.org/#abortsignal-dependent. Readable by <c>JsTaskSignal</c>, which needs it for
    /// https://wicg.github.io/scheduling-apis/#tasksignal-has-fixed-priority — "a TaskSignal has fixed
    /// priority if it is a dependent signal with a null source signal".
    /// </summary>
    internal bool Dependent { get; private protected set; }

    /// <summary>
    /// A token that is cancelled when this signal is aborted — the handle a CLR-side operation links against,
    /// so that aborting a signal cancels the HTTP request behind a <c>fetch</c> without any script running.
    /// </summary>
    /// <remarks>
    /// Created on first use, so a signal nobody hands to such an operation never allocates one. A signal that
    /// is already aborted answers with an already-cancelled token and still allocates nothing. The source is
    /// deliberately never disposed: it holds no timer and no handle unless someone registered a callback, and
    /// a consumer may still be reading the token after the abort.
    /// </remarks>
    internal CancellationToken CancellationToken
    {
        get
        {
            if (Aborted)
            {
                return new CancellationToken(canceled: true);
            }

            return (_cancellation ??= new CancellationTokenSource()).Token;
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#abortsignal-add — an algorithm to run when this signal is aborted. Adding
    /// one to a signal that has already aborted does nothing, exactly as the specification says: the moment to
    /// react has passed, and the caller is expected to have checked <see cref="Aborted"/> first.
    /// </summary>
    internal void AddAbortAlgorithm(Action algorithm)
    {
        if (Aborted)
        {
            return;
        }

        (_abortAlgorithms ??= new List<Action>()).Add(algorithm);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#abortsignal-remove.
    /// </summary>
    internal void RemoveAbortAlgorithm(Action algorithm) => _abortAlgorithms?.Remove(algorithm);

    /// <summary>
    /// https://dom.spec.whatwg.org/#abortsignal-signal-abort. Aborting an already-aborted signal is a no-op,
    /// which is what makes <c>controller.abort()</c> idempotent.
    /// </summary>
    internal void SignalAbort(JsValue reason)
    {
        if (Aborted)
        {
            return;
        }

        Aborted = true;
        Reason = reason;

        // Steps 4 and 5: every dependent takes this signal's reason and is marked aborted *before* any abort
        // steps run, so a listener on one dependent already sees every other one as aborted.
        List<JsAbortSignal>? dependentsToAbort = null;
        if (_dependentSignals is { } dependents)
        {
            foreach (var dependent in dependents)
            {
                if (dependent.Aborted)
                {
                    continue;
                }

                dependent.Aborted = true;
                dependent.Reason = reason;
                (dependentsToAbort ??= new List<JsAbortSignal>()).Add(dependent);
            }

            // Nothing can be added to the set once this signal has aborted, so dropping it here is what keeps
            // a long-lived source from retaining every composite ever built from it.
            _dependentSignals = null;
        }

        RunAbortSteps();

        if (dependentsToAbort is null)
        {
            return;
        }

        foreach (var dependent in dependentsToAbort)
        {
            dependent.RunAbortSteps();
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#run-the-abort-steps.
    /// </summary>
    private void RunAbortSteps()
    {
        _sourceSignals = null;

        // The engine's own abort algorithm, ahead of every registered one: an operation that only holds the
        // token — an HTTP request in flight — is stopped before a single line of script runs.
        _cancellation?.Cancel();

        // Steps 1 and 2. Emptied before the run rather than after it, which is equivalent because the signal
        // is already aborted and AddAbortAlgorithm therefore refuses everything from here on.
        var algorithms = _abortAlgorithms;
        _abortAlgorithms = null;
        if (algorithms is not null)
        {
            foreach (var algorithm in algorithms)
            {
                algorithm();
            }
        }

        // Step 3. A listener that throws is reported to the host's DiagnosticsSink, or — with no sink —
        // erupts from here, which for controller.abort() is its caller and for AbortSignal.timeout() is the
        // event-loop pump; see JsEventTarget's remarks. Every algorithm above has already run by then, so the
        // abort itself is complete in either case.
        FireEvent(_abortEventName);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#create-a-dependent-abort-signal, the algorithm behind
    /// <c>AbortSignal.any()</c>.
    /// </summary>
    internal static JsAbortSignal CreateDependent(Engine engine, Realm realm, List<JsAbortSignal> signals)
    {
        var result = new JsAbortSignal(engine, realm)
        {
            _prototype = realm.Intrinsics.AbortSignal.PrototypeObject,
        };

        return InitializeDependent(result, signals);
    }

    /// <summary>
    /// Steps 2 to 5 of https://dom.spec.whatwg.org/#create-a-dependent-abort-signal, applied to a signal the
    /// caller has already created. Split out so that <c>TaskSignal.any()</c>, whose result is a
    /// <c>TaskSignal</c> rather than a plain <c>AbortSignal</c>, runs exactly these steps rather than a copy
    /// of them — https://wicg.github.io/scheduling-apis/#create-a-dependent-task-signal step 1.
    /// </summary>
    internal static T InitializeDependent<T>(T result, List<JsAbortSignal> signals) where T : JsAbortSignal
    {
        // Step 2: an already-aborted source wins outright, and the result is not dependent on anything — so
        // it registers nowhere and is retained by nobody.
        foreach (var signal in signals)
        {
            if (signal.Aborted)
            {
                result.Aborted = true;
                result.Reason = signal.Reason;
                return result;
            }
        }

        result.Dependent = true;

        // Step 4: a dependent source is flattened into its own sources, so the graph is never more than one
        // level deep and an abort reaches every composite in a single pass.
        foreach (var signal in signals)
        {
            if (!signal.Dependent)
            {
                result.AddSource(signal);
                continue;
            }

            if (signal._sourceSignals is { } sources)
            {
                foreach (var source in sources)
                {
                    result.AddSource(source);
                }
            }
        }

        return result;
    }

    private void AddSource(JsAbortSignal source)
    {
        var sources = _sourceSignals ??= new List<JsAbortSignal>();

        // The specification's sets cannot hold a duplicate; `AbortSignal.any([s, s])` and two composites over
        // one source would both produce one otherwise.
        if (sources.Contains(source))
        {
            return;
        }

        sources.Add(source);
        (source._dependentSignals ??= new List<JsAbortSignal>()).Add(this);
    }
}
#endif
