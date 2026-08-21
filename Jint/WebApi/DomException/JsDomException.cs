#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;

namespace Jint.WebApi.DomException;

/// <summary>
/// A <c>DOMException</c> instance.
/// <para>
/// https://webidl.spec.whatwg.org/#idl-DOMException
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// WebIDL exposes <c>name</c>, <c>message</c> and <c>code</c> as accessors on the interface prototype
/// object, not as own properties of the instance, so the three values live in CLR fields here and
/// <see cref="DomExceptionPrototype"/> reads them through a brand check. An instance therefore has no own
/// property but <c>stack</c>, matching what a browser reports for
/// <c>Object.getOwnPropertyNames(new DOMException())</c>.
/// </para>
/// <para>
/// It derives from <see cref="ErrorInstance"/> for the <c>[[ErrorData]]</c>-shaped behaviour a host expects
/// of an error object — notably <see cref="object.ToString"/> rendering as <c>name: message</c> — and it
/// asserts the slot itself through <see cref="IErrorData"/>, because
/// https://webidl.spec.whatwg.org/#internally-create-a-new-object-implementing-the-interface says "If
/// interface is DOMException, append [[ErrorData]] to slots". So <c>Error.isError(new DOMException())</c>
/// answers <see langword="true"/>, as it does in a browser, and the prototype chain,
/// <c>instanceof Error</c> and <c>stack</c> all behave as they should.
/// </para>
/// <para>
/// One deliberate divergence, in the direction of ECMAScript rather than WebIDL:
/// https://webidl.spec.whatwg.org/#js-DOMException-specialness also gives the <i>interface prototype
/// object</i> <c>[[ErrorData]]</c> and <c>[[Stack]]</c> slots, "like all built-in exceptions". Every
/// ECMAScript built-in exception prototype is in fact an ordinary object that "is not an Error instance and
/// does not have an [[ErrorData]] internal slot"
/// (https://tc39.es/ecma262/#sec-properties-of-the-nativeerror-prototype-objects), so the analogy argues the
/// other way; <c>DOMException.prototype</c> is left an ordinary object here and
/// <c>Error.isError(DOMException.prototype)</c> answers <see langword="false"/>, the same as
/// <c>Error.isError(Error.prototype)</c>.
/// </para>
/// <para>
/// It is the one type in <c>Jint/WebApi/</c> that is deliberately <b>not</b> sealed.
/// https://webidl.spec.whatwg.org/#idl-DOMException-derived-interfaces lets a standard derive an interface
/// from <c>DOMException</c> when an exception has to carry more than a name, and
/// <see cref="JsQuotaExceededError"/> is the one such interface WebIDL defines. A derived instance has to
/// answer the very same <c>name</c>/<c>message</c>/<c>code</c> accessors — the brand check in
/// <see cref="DomExceptionPrototype"/> is <c>is JsDomException</c>, which is exactly the WebIDL brand — so
/// sharing the base is what makes that true by construction rather than by duplication. The derived types
/// are themselves sealed.
/// </para>
/// </remarks>
internal class JsDomException : ErrorInstance, IErrorData
{
    internal JsDomException(Engine engine, JsString name, JsString message)
        : base(engine, ObjectClass.Error)
    {
        Name = name;
        Message = message;
        Code = JsNumber.Create(DomExceptionNames.CodeFor(name.ToString()));
    }

    /// <summary>The <c>[[Name]]</c> of the exception.</summary>
    internal JsString Name { get; }

    /// <summary>The <c>[[Message]]</c> of the exception.</summary>
    internal JsString Message { get; }

    /// <summary>The legacy code the name maps to, or <c>0</c>. Computed once, since the name never changes.</summary>
    internal JsNumber Code { get; }
}
#endif
