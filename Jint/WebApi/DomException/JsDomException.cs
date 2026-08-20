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
/// of an error object — notably <see cref="object.ToString"/> rendering as <c>name: message</c>. It is not a
/// <see cref="JsError"/>, which is sealed, so <c>Error.isError(domException)</c> answers <see langword="false"/>
/// where a browser answers <see langword="true"/>; the prototype chain, <c>instanceof Error</c> and
/// <c>stack</c> all behave as they should.
/// </para>
/// </remarks>
internal sealed class JsDomException : ErrorInstance
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
