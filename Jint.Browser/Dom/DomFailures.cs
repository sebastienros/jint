using System.Diagnostics.CodeAnalysis;
using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// The one place a CLR exception from AngleSharp becomes the throw the standard prescribes: every generated
/// member body is wrapped by <see cref="Guard"/>, so a DOM operation that refuses is a JavaScript
/// <c>DOMException</c> with the right <c>name</c> and legacy <c>code</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one wrapper rather than a <c>catch</c> per body.</b> A page's whole vocabulary for a refused DOM
/// operation is <c>e.name</c> — <c>try { … } catch (e) { if (e.name === 'HierarchyRequestError') … }</c> —
/// and AngleSharp raises an <c>AngleSharp.Dom.DomException</c>, which is a CLR exception: it walks straight
/// through a script's <c>try</c>/<c>catch</c>, out of the page loop and into the host. The conversion is one
/// function because the two thousand generated members must not each carry a copy of it, and because the
/// mapping table is one table. <c>Dom/AGENTS.md</c> has the divergences it is written against.
/// </para>
/// <para>
/// <b>What is translated, and what is deliberately left alone.</b> An <c>AngleSharp.Dom.DomException</c>
/// becomes the <c>DOMException</c> its <see cref="DomError"/> names; an <see cref="ArgumentException"/> — a
/// WebIDL conversion the CLR signature refused — becomes a <c>TypeError</c>; a
/// <see cref="NotSupportedException"/> or <see cref="NotImplementedException"/> becomes
/// <c>NotSupportedError</c>. <b>Everything else keeps the engine's own interop behaviour</b>, which is a
/// frozen contract for embedders (<c>Jint/Runtime/Interop/AGENTS.md</c>): in particular a
/// <see cref="JavaScriptException"/> the body raised itself, and every constraint and cancellation signal,
/// are outside the filter and never touched.
/// </para>
/// <para>
/// <b>The message after the colon is AngleSharp's sentence, not the standard's.</b> DOM specifies a name and
/// nothing about wording, no page branches on the text, and AngleSharp's sentence at least says what went
/// wrong; the <c>Failed to execute '&lt;interface&gt;.&lt;member&gt;': </c> prefix is the one
/// <see cref="DomBindings"/> already puts in front of an illegal invocation and a bad argument.
/// </para>
/// </remarks>
internal static class DomFailures
{
    /// <summary>
    /// Wraps a generated member body so that the four CLR exceptions above cross into script as the throw
    /// the standard prescribes. The wrapping happens once per member, when the interface's shape is built.
    /// </summary>
    /// <param name="member">
    /// The qualified member name — <c>Node.appendChild</c> — the same label the body's brand check carries.
    /// </param>
    /// <param name="implementation">The member body.</param>
    /// <remarks>
    /// It costs one closure and one delegate per member, allocated when the shape is built and therefore once
    /// per process per interface a page actually reaches, and one delegate hop per member call — the
    /// <c>try</c> itself costs nothing until it fires. The alternative, a <c>try</c> inside every emitted
    /// body, saves the hop and buys two thousand copies of one decision.
    /// </remarks>
    internal static Func<JsValue, JsValue[], JsValue> Guard(
        string member,
        Func<JsValue, JsValue[], JsValue> implementation)
        => (thisObject, arguments) =>
        {
            try
            {
                return implementation(thisObject, arguments);
            }
            catch (Exception exception) when (Translates(thisObject, exception))
            {
                return Translate((ObjectInstance) thisObject, member, exception);
            }
        };

    /// <summary>
    /// The <a href="https://webidl.spec.whatwg.org/#idl-DOMException-error-names">error name</a> AngleSharp's
    /// <see cref="DomError"/> stands for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AngleSharp's own <c>DomException.Name</c> is the <i>enum field name</i> —
    /// <c>HierarchyRequest</c>, not <c>HierarchyRequestError</c> — so it cannot be handed to a page, and this
    /// is the table that can. Its numeric values happen to be WebIDL's legacy codes, which is why the switch
    /// reads as one; <see cref="DomExceptionNames.CodeFor"/> is still what computes the code, from the name.
    /// </para>
    /// <para>
    /// <c>DomError.Validation</c> is absent because AngleSharp gives it 15, the value of
    /// <c>DomError.InvalidAccess</c>, so the two are one case and no <c>ValidationError</c> can be
    /// distinguished. It is recorded in <c>Dom/AGENTS.md</c>'s divergence table.
    /// </para>
    /// </remarks>
    internal static string NameOf(DomException exception) => (DomError) exception.Code switch
    {
        DomError.IndexSizeError => DomExceptionNames.IndexSize,

        // The two legacy-only names, spelled here rather than in DomExceptionNames because no web API in the
        // engine raises either and AngleSharp is the only thing that does.
        DomError.DomStringSize => "DOMStringSizeError",
        DomError.NoDataAllowed => "NoDataAllowedError",

        DomError.HierarchyRequest => DomExceptionNames.HierarchyRequest,
        DomError.WrongDocument => DomExceptionNames.WrongDocument,
        DomError.InvalidCharacter => DomExceptionNames.InvalidCharacter,
        DomError.NoModificationAllowed => DomExceptionNames.NoModificationAllowed,
        DomError.NotFound => DomExceptionNames.NotFound,
        DomError.NotSupported => DomExceptionNames.NotSupported,
        DomError.InUse => DomExceptionNames.InUseAttribute,
        DomError.InvalidState => DomExceptionNames.InvalidState,
        DomError.Syntax => DomExceptionNames.Syntax,
        DomError.InvalidModification => DomExceptionNames.InvalidModification,
        DomError.Namespace => DomExceptionNames.Namespace,
        DomError.InvalidAccess => DomExceptionNames.InvalidAccess,
        DomError.TypeMismatch => DomExceptionNames.TypeMismatch,
        DomError.Security => DomExceptionNames.Security,
        DomError.Network => DomExceptionNames.Network,
        DomError.Abort => DomExceptionNames.Abort,
        DomError.UrlMismatch => DomExceptionNames.UrlMismatch,
        DomError.QuotaExceeded => DomExceptionNames.QuotaExceeded,
        DomError.Timeout => DomExceptionNames.Timeout,
        DomError.InvalidNodeType => DomExceptionNames.InvalidNodeType,
        DomError.DataClone => DomExceptionNames.DataClone,

        // AngleSharp's other constructor leaves the code 0 and puts its argument in `Name`, and its one use
        // in the pinned assembly puts a *sentence* there ("The element has no parent."), so the name cannot
        // be forwarded. DOM's general-purpose refusal is what such an exception becomes instead, with the
        // sentence as the message. That one site is answered by hand — see DomHostHooks.InsertAdjacentHtml —
        // so nothing in the pinned AngleSharp reaches this arm; it is here for the next version that does.
        _ => DomExceptionNames.InvalidState,
    };

    /// <summary>
    /// Whether an exception is one of the four this file converts, on a receiver there is an engine to build
    /// the error in.
    /// </summary>
    /// <remarks>
    /// A primitive receiver cannot be reached with any AngleSharp work already done — the body's brand check
    /// refuses it first — so the receiver test is a guarantee that <see cref="Translate"/> has a realm, not a
    /// case that happens.
    /// </remarks>
    private static bool Translates(JsValue thisObject, Exception exception)
        => thisObject is ObjectInstance
           && exception is DomException or ArgumentException or NotSupportedException or NotImplementedException;

    /// <summary>
    /// Raises the <c>DOMException</c> a member refuses with, in the message shape every refusal in this
    /// binding wears. This is the door for a member whose refusal has to be written by hand, because the
    /// standard names an error AngleSharp raises differently or not at all.
    /// </summary>
    /// <param name="engine">The engine the error is built in.</param>
    /// <param name="member">The qualified member name — <c>Element.insertAdjacentHTML</c>.</param>
    /// <param name="name">The error name; <see cref="DomExceptionNames"/> holds the ones with a legacy code.</param>
    /// <param name="detail">What went wrong, as one sentence.</param>
    [DoesNotReturn]
    internal static JsValue Refuse(Engine engine, string member, string name, string detail)
    {
        // The PRINCIPAL realm, deliberately, for the reason DomRealm gives: a wrapper's prototypes come from
        // the engine's own realm whatever realm happens to be executing, so the DOMException a DOM member
        // raises has to come from there too or `e instanceof DOMException` in the page would answer false.
        var intrinsics = engine._mainRealm.Intrinsics;
        var message = "Failed to execute '" + member + "': " + detail;

        // https://webidl.spec.whatwg.org/#quotaexceedederror is an interface of its own rather than a name a
        // DOMException wears, and `e.constructor === QuotaExceededError` is how a page tells them apart.
        var error = string.Equals(name, DomExceptionNames.QuotaExceeded, StringComparison.Ordinal)
            ? intrinsics.QuotaExceededError.CreateException(message)
            : intrinsics.DomException.CreateException(name, message);

        Throw.JavaScriptException(engine, error, engine.GetLastSyntaxElement()?.Location ?? default);
        return JsValue.Undefined;
    }

    [DoesNotReturn]
    private static JsValue Translate(ObjectInstance receiver, string member, Exception exception)
    {
        var engine = receiver.Engine;

        if (exception is ArgumentException)
        {
            // WebIDL's answer for an argument no conversion accepts —
            // https://webidl.spec.whatwg.org/#es-type-mapping. A CLR signature that refused its argument is
            // that and nothing more specific; where the standard names a DOMException instead, the member is
            // written by hand and says so.
            Throw.TypeError(engine._mainRealm, "Failed to execute '" + member + "': " + exception.Message);
        }

        // A DomException built from a string carries no message of its own — Exception.Message is then the
        // CLR's "Exception of type … was thrown." — and the string it was given is in Name instead.
        return exception is DomException dom
            ? Refuse(engine, member, NameOf(dom), dom.Code == 0 ? dom.Name : dom.Message)
            : Refuse(engine, member, DomExceptionNames.NotSupported, exception.Message);
    }
}
