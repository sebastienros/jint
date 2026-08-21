#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.DomException;

/// <summary>
/// <c>QuotaExceededError.prototype</c> — the interface prototype object.
/// <para>
/// https://webidl.spec.whatwg.org/#quotaexceedederror
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>DOMException.prototype</c>, which is what gives an instance <c>name</c>,
/// <c>message</c>, <c>code</c> and the 25 legacy constants without declaring any of them here, and what makes
/// <c>new QuotaExceededError() instanceof DOMException</c> — and, one level further up,
/// <c>instanceof Error</c> — hold.
/// </para>
/// <para>
/// <c>quota</c> and <c>requested</c> are <c>readonly attribute double?</c>, so each is an accessor with no
/// setter that brand-checks its receiver and raises a <c>TypeError</c> for anything that is not a
/// <c>QuotaExceededError</c> — including <c>QuotaExceededError.prototype</c> itself, and including a plain
/// <c>DOMException</c>, which is a <i>base</i> instance and not one of these.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class QuotaExceededErrorPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly QuotaExceededErrorConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString QuotaExceededErrorToStringTag = new("QuotaExceededError");

    internal QuotaExceededErrorPrototype(
        Engine engine,
        Realm realm,
        QuotaExceededErrorConstructor constructor,
        ObjectInstance domExceptionPrototype) : base(engine, realm)
    {
        _prototype = domExceptionPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-quotaexceedederror-quota — "The quota getter steps are to return
    /// this's quota."
    /// </summary>
    [JsAccessor("quota", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue QuotaGet(JsValue thisObject)
    {
        return Brand(thisObject).Quota;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-quotaexceedederror-requested — "The requested getter steps are to
    /// return this's requested."
    /// </summary>
    [JsAccessor("requested", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue RequestedGet(JsValue thisObject)
    {
        return Brand(thisObject).Requested;
    }

    /// <summary>
    /// The WebIDL brand check every attribute getter performs: a receiver that is not a platform object
    /// implementing the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsQuotaExceededError Brand(JsValue thisObject)
    {
        if (thisObject is JsQuotaExceededError exception)
        {
            return exception;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a QuotaExceededError");
        return null!;
    }
}
#endif
