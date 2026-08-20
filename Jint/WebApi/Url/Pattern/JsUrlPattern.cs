#if NET8_0_OR_GREATER
using Jint.Native.Object;

namespace Jint.WebApi.Url.Pattern;

/// <summary>
/// A <c>URLPattern</c> instance.
/// <para>
/// https://urlpattern.spec.whatwg.org/#urlpattern-class
/// </para>
/// </summary>
/// <remarks>
/// The object's only associated value is its
/// <a href="https://urlpattern.spec.whatwg.org/#urlpattern-associated-url-pattern">associated URL pattern</a>, and
/// every member is an accessor or method on <see cref="UrlPatternPrototype"/> that brand-checks its receiver — so
/// an instance has no own property at all, which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new URLPattern("https://example.com/*"))</c>.
/// </remarks>
internal sealed class JsUrlPattern : ObjectInstance
{
    internal JsUrlPattern(Engine engine, UrlPatternRecord pattern) : base(engine, ObjectClass.Object)
    {
        Pattern = pattern;
    }

    /// <summary>https://urlpattern.spec.whatwg.org/#urlpattern-associated-url-pattern</summary>
    internal UrlPatternRecord Pattern { get; }
}
#endif
