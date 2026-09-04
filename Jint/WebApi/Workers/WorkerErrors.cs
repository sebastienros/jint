#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Workers;

/// <summary>
/// The two refusals this feature raises at script.
/// </summary>
internal static class WorkerErrors
{
    /// <summary>
    /// https://webidl.spec.whatwg.org/#quotaexceedederror, with the ceiling and the count the refused
    /// operation would have reached — the same shape the timer queue and the socket ceiling already refuse
    /// with, so <c>e.constructor === QuotaExceededError</c> and <c>e.quota</c> answer as a script expects.
    /// </summary>
    internal static void ThrowQuotaExceededError(Engine engine, Realm realm, string message, double quota, double requested)
    {
        var exception = realm.Intrinsics.QuotaExceededError.CreateException(message, quota, requested);
        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
    }

    /// <summary>
    /// A <c>DOMException</c> of the named kind. <c>SecurityError</c> is what a provider's refusal reaches the
    /// script as: it is a policy decision rather than a fetch failure, and it is the shape a browser already
    /// throws synchronously from <c>new Worker()</c> for a script it will not run.
    /// </summary>
    internal static void ThrowDomException(Engine engine, Realm realm, string name, string message)
    {
        var exception = realm.Intrinsics.DomException.CreateException(name, message);
        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, exception, in location);
    }
}
#endif
