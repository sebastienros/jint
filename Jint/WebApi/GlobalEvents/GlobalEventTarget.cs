#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.GlobalEvents;

/// <summary>
/// The engine's <b>synthetic global event target</b>: the listener list behind the global
/// <c>addEventListener</c>, <c>removeEventListener</c> and <c>dispatchEvent</c>, and what the engine fires
/// <c>error</c>, <c>unhandledrejection</c> and <c>rejectionhandled</c> at.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The global object itself is untouched.</b> Making it an <c>EventTarget</c> would mean giving it a
/// prototype chain it does not have, and every own-property and inline-cache promise the global object makes
/// to the engine and to a host would have to be re-argued. Nothing is gained by it: what a script needs is the
/// three operations and the events, and both are reachable with the list kept beside the timers instead.
/// This object is never handed to script — <see cref="EventTargetValue"/> answers with the global object, so
/// <c>event.target</c>, <c>event.currentTarget</c> and a listener's <c>this</c> are what a browser reports.
/// </para>
/// <para>
/// <b>Reporting does not recurse.</b> HTML's <i>report an exception</i> is reached from inside the dispatch it
/// may itself start — a global <c>error</c> listener that throws is an exception escaping an event listener,
/// which DOM's <i>inner invoke</i> step 2.10 says to report, which fires an <c>error</c> event. Neither
/// specification closes that loop, and every browser does with a re-entrancy guard;
/// <see cref="FireReport"/> is Jint's. While a report is being dispatched the next one fires nothing and goes
/// to the <see cref="DiagnosticsSink"/> alone. Without it the recursion is unbounded and, on an engine with
/// no recursion constraint, ends in a process-killing stack overflow rather than in a JavaScript error.
/// </para>
/// </remarks>
internal sealed class GlobalEventTarget : JsEventTarget
{
    internal GlobalEventTarget(Engine engine, Realm realm) : base(engine, realm)
    {
    }

    /// <summary>
    /// Whether a report is already being dispatched at this target, which is what stops one report starting
    /// another. Engine-thread state like everything else here, so no synchronization.
    /// </summary>
    private bool _reporting;

    /// <inheritdoc />
    internal override JsValue EventTargetValue => _realm.GlobalObject;

    /// <inheritdoc />
    /// <remarks>
    /// True here and nowhere else, which is what scopes HTML's <i>special error event handling</i> — the
    /// five-argument <c>onerror</c> that cancels by returning <see langword="true"/> — to the global scope. A
    /// <c>Worker</c> object's <c>onerror</c> is <c>AbstractWorker</c>'s plain <c>EventHandler</c> and takes the
    /// other branch.
    /// </remarks>
    internal override bool IsGlobalScope => true;

    /// <summary>
    /// Whether this global scope is a <c>Window</c> — a global object with a document behind it — rather than
    /// the bare global of an engine that has none.
    /// </summary>
    /// <remarks>
    /// False for every engine the box ships, and set by a host that installs a document on the global
    /// (<c>Jint.Browser</c>'s window installer). DOM's <i>default passive value</i> is the one rule that turns
    /// on it, which is why the property says what it is rather than what it is for: HTML's other
    /// <c>Window</c>-only rules — <i>special error event handling</i> and dispatch's "parent is a
    /// <c>Window</c> object" branch — already hold for a worker's global too and read
    /// <see cref="JsEventTarget.IsGlobalScope"/> instead.
    /// </remarks>
    internal bool IsWindow { get; set; }

    /// <inheritdoc />
    internal override bool IsDefaultPassiveTarget => IsWindow;

    /// <summary>
    /// Whether a report is being dispatched at this target right now — HTML's <i>in error reporting mode</i>,
    /// and the reason <see cref="FireReport"/> declines a second one.
    /// </summary>
    /// <remarks>
    /// Read by the worker error relay, which must decline for the same reason the dispatch does: an error
    /// raised <i>while</i> a previous one was being reported is not a second failure to tell a parent about,
    /// and propagating it would be the unbounded recursion the guard exists to stop, one engine further up.
    /// </remarks>
    internal bool IsReporting => _reporting;

    /// <summary>
    /// Fires one trusted event at the global scope, unless nothing is listening for it or a report is already
    /// in flight.
    /// </summary>
    /// <returns>
    /// False when a listener canceled the event, which is HTML's <i>notHandled</i>; true when it did not, when
    /// nothing was listening, and when the dispatch was declined as re-entrant.
    /// </returns>
    private bool FireReport(JsEvent ev)
    {
        if (_reporting)
        {
            return true;
        }

        _reporting = true;
        try
        {
            return DispatchEvent(ev);
        }
        finally
        {
            _reporting = false;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception step 5: "fire an event named
    /// <c>error</c> at global, using <c>ErrorEvent</c>, with the cancelable attribute initialized to true …".
    /// </summary>
    /// <returns>HTML's <i>notHandled</i> — false exactly when a listener called <c>preventDefault()</c>.</returns>
    internal bool FireError(in ErrorEventDetails details)
    {
        if (!HasListenerOfType(GlobalEventNames.ErrorName))
        {
            return true;
        }

        var ev = _realm.Intrinsics.ErrorEvent.CreateTrustedError(GlobalEventNames.Error, in details);
        return FireReport(ev);
    }

    /// <summary>
    /// The two events of
    /// https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections.
    /// </summary>
    /// <param name="handled">
    /// False for <c>unhandledrejection</c> — a rejection nobody handled, which a listener may cancel — and
    /// true for <c>rejectionhandled</c>, which reports that one already announced has since been handled and
    /// which HTML fires without a cancelable initializer.
    /// </param>
    /// <param name="promise">The promise the tracker reported.</param>
    /// <param name="reason">Its rejection reason, or <c>undefined</c> when it is somehow no longer rejected.</param>
    internal void FireRejection(bool handled, JsValue promise, JsValue reason)
    {
        var (type, typeName) = handled
            ? (GlobalEventNames.RejectionHandled, GlobalEventNames.RejectionHandledName)
            : (GlobalEventNames.UnhandledRejection, GlobalEventNames.UnhandledRejectionName);

        if (!HasListenerOfType(typeName))
        {
            return;
        }

        var ev = _realm.Intrinsics.PromiseRejectionEvent.CreateTrustedRejection(type, promise, reason, cancelable: !handled);
        FireReport(ev);
    }
}

/// <summary>
/// The three event types the engine fires at the global scope, interned once.
/// </summary>
internal static class GlobalEventNames
{
    internal const string ErrorName = "error";
    internal const string UnhandledRejectionName = "unhandledrejection";
    internal const string RejectionHandledName = "rejectionhandled";

    internal static readonly JsString Error = new(ErrorName);
    internal static readonly JsString UnhandledRejection = new(UnhandledRejectionName);
    internal static readonly JsString RejectionHandled = new(RejectionHandledName);
}
#endif
