#if NET8_0_OR_GREATER
using Jint.Runtime;

namespace Jint.WebApi.Xhr;

/// <summary>
/// An <c>XMLHttpRequestUpload</c> instance — the object a request's own upload progress is fired at.
/// <para>
/// https://xhr.spec.whatwg.org/#xmlhttprequestupload
/// </para>
/// </summary>
/// <remarks>
/// The interface adds nothing to <c>XMLHttpRequestEventTarget</c>: it is a second event target so that
/// <c>upload.onprogress</c> and <c>onprogress</c> can be told apart. One is created per
/// <c>XMLHttpRequest</c>, eagerly, because whether it carries a listener is what decides — under the
/// specification's <i>upload listener flag</i> — whether the request reports its upload at all.
/// </remarks>
internal sealed class JsXmlHttpRequestUpload : JsXmlHttpRequestEventTarget
{
    internal JsXmlHttpRequestUpload(Engine engine, Realm realm) : base(engine, realm)
    {
    }
}
#endif
