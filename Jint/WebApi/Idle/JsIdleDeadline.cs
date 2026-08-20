#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Idle;

/// <summary>
/// An <c>IdleDeadline</c> instance — the object an idle callback is handed, telling it how much of the
/// engine's idle budget is left and whether it is running because that budget arrived or because its timeout
/// ran out.
/// <para>
/// https://w3c.github.io/requestidlecallback/#dom-idledeadline
/// </para>
/// </summary>
/// <remarks>
/// One per invocation, as the specification says ("let deadlineArg be a new IdleDeadline"): the deadline it
/// carries belongs to the idle period the callback is running in, and a callback that stores the object and
/// consults it later gets a <c>timeRemaining()</c> of zero rather than a stale positive number, because the
/// instant it names is in the past.
/// </remarks>
internal sealed class JsIdleDeadline : ObjectInstance
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The instant the idle period ends, on <see cref="TimeProvider.GetTimestamp"/>'s scale — the same clock
    /// the timers are scheduled against, so a fake one drives both coherently.
    /// </summary>
    private readonly long _deadlineTimestamp;

    internal JsIdleDeadline(
        Engine engine,
        ObjectInstance prototype,
        TimeProvider timeProvider,
        long deadlineTimestamp,
        bool didTimeout) : base(engine, ObjectClass.Object)
    {
        _prototype = prototype;
        _timeProvider = timeProvider;
        _deadlineTimestamp = deadlineTimestamp;
        DidTimeout = didTimeout;
    }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-idledeadline-didtimeout — "each IdleDeadline has an
    /// associated timeout, which is initially false", true only for a callback the timeout algorithm ran.
    /// </summary>
    internal bool DidTimeout { get; }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-idledeadline-timeremaining, whose steps this is exactly:
    /// let <i>now</i> be the current high resolution time, let <i>deadline</i> be the result of the get
    /// deadline time algorithm, let <i>timeRemaining</i> be <i>deadline</i> − <i>now</i>, and if it is negative
    /// set it to 0.
    /// </summary>
    /// <remarks>
    /// A timed-out callback's deadline is the moment it was invoked, so this answers zero for it — which is
    /// what the specification produces too, and what tells such a callback that it is running on borrowed time
    /// and should do the minimum.
    /// </remarks>
    internal double TimeRemaining()
    {
        var remaining = _timeProvider.GetElapsedTime(_timeProvider.GetTimestamp(), _deadlineTimestamp).TotalMilliseconds;
        return remaining > 0 ? remaining : 0;
    }
}
#endif
