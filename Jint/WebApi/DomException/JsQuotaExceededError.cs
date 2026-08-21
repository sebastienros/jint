#if NET8_0_OR_GREATER
using Jint.Native;

namespace Jint.WebApi.DomException;

/// <summary>
/// A <c>QuotaExceededError</c> instance.
/// <para>
/// https://webidl.spec.whatwg.org/#quotaexceedederror
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// "Every <c>QuotaExceededError</c> instance has a <b>requested</b> and a <b>quota</b>, both numbers or null.
/// They are both initially null." The two live in CLR fields here and
/// <see cref="QuotaExceededErrorPrototype"/> reads them through a brand check, exactly as
/// <see cref="JsDomException"/> holds <c>name</c>, <c>message</c> and <c>code</c> — so an instance still has
/// no own property but <c>stack</c>.
/// </para>
/// <para>
/// They are stored as <see cref="JsValue"/> rather than as <c>double?</c> because that is what the getters
/// hand back: <see cref="JsValue.Null"/> or a <see cref="JsNumber"/>, decided once when the exception is
/// created rather than boxed afresh on every read.
/// </para>
/// <para>
/// <c>code</c> is inherited and answers <b>22</b>: the name is still in the legacy error-names table (marked
/// deprecated, pointing at this interface), so <see cref="DomExceptionNames.CodeFor"/> maps it exactly as it
/// did before there was an interface. The specification says so itself — "The QuotaExceededError interface
/// inherits the DOMException interface's code getter, which will always return 22" — which is the one place
/// it diverges from the general rule that a <c>DOMException</c>-derived interface reports 0.
/// </para>
/// </remarks>
internal sealed class JsQuotaExceededError : JsDomException
{
    internal JsQuotaExceededError(Engine engine, JsString message, JsValue quota, JsValue requested)
        : base(engine, QuotaExceededErrorConstructor.ErrorName, message)
    {
        Quota = quota;
        Requested = requested;
    }

    /// <summary>The <c>[[Quota]]</c>: <see cref="JsValue.Null"/> or a number.</summary>
    internal JsValue Quota { get; }

    /// <summary>The <c>[[Requested]]</c>: <see cref="JsValue.Null"/> or a number.</summary>
    internal JsValue Requested { get; }
}
#endif
