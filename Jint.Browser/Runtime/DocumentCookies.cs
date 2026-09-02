using System.Net;
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime;

/// <summary>
/// <c>document.cookie</c>, over the same jar every request of the context reads and writes.
/// <para>
/// https://html.spec.whatwg.org/multipage/dom.html#dom-document-cookie
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>One jar, two doors.</b> A cookie a response set is on the next request and in
/// <c>document.cookie</c>; a cookie a script set is on the next request. That is the whole point of the
/// context owning the jar rather than each engine owning one, and it is what makes a login that answers
/// <c>302 Set-Cookie</c> work the way a page expects.
/// </para>
/// <para>
/// <b><c>HttpOnly</c> is enforced here, and it is the reason this is not simply
/// <c>CookieJar.GetCookieHeader</c>.</b> RFC 6265bis §5.5 says a non-HTTP API must not see an
/// <c>HttpOnly</c> cookie and must not overwrite one, which is what makes the attribute worth setting at
/// all; the jar's own interface has no such distinction, because a request is an HTTP API and never needs
/// one. So the default jar is a <see cref="CookieContainerCookieJar"/>, whose container does carry the flag
/// per cookie, and both halves below read it.
/// </para>
/// <para>
/// <b>A host that supplies a jar of its own gets the honest degradation, not a silent one.</b> There is no
/// flag to read on an arbitrary <see cref="CookieJar"/>, so <c>document.cookie</c> falls back to the jar's
/// own two methods — every cookie is visible to script, and a script may overwrite any of them. A host that
/// needs the rule enforced supplies a <see cref="CookieContainerCookieJar"/> (which is also the default) or
/// keeps its <c>HttpOnly</c> cookies out of the jar it hands over.
/// </para>
/// </remarks>
internal static class DocumentCookies
{
    /// <summary>Adds the <c>cookie</c> accessor to the document wrapper as an own property.</summary>
    /// <remarks>
    /// An own property of <c>document</c> rather than an accessor on <c>Document.prototype</c>, for the
    /// reason <c>defaultView</c> and <c>currentScript</c> are: it is one object rather than a shared
    /// prototype, so nothing shaped is deoptimized. Non-enumerable for the same reason those two are.
    /// </remarks>
    internal static void Attach(PageRuntime runtime, ObjectInstance document)
    {
        var engine = runtime.Engine;

        document.DefineOwnPropertyUnchecked(
            "cookie",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get cookie", static (thisObject, _) =>
                    JsString.Create(Read(PageRuntime.Of(thisObject, "cookie")))),
                new ClrFunction(engine, "set cookie", static (thisObject, arguments) =>
                {
                    Write(PageRuntime.Of(thisObject, "cookie"), TypeConverter.ToString(arguments.At(0)));
                    return JsValue.Undefined;
                }),
                PropertyFlag.OnlyConfigurable));
    }

    /// <summary>
    /// The <c>name=value</c> pairs a script may see: everything the jar would send, minus the
    /// <c>HttpOnly</c> ones.
    /// </summary>
    internal static string Read(PageRuntime runtime)
    {
        if (runtime.Page.Network is not { } network || DocumentUri(runtime) is not { } uri)
        {
            return "";
        }

        if (network.CookieJar is CookieContainerCookieJar container)
        {
            var builder = new StringBuilder();

            foreach (Cookie cookie in Cookies(container, uri))
            {
                if (cookie.HttpOnly)
                {
                    continue;
                }

                if (builder.Length != 0)
                {
                    builder.Append("; ");
                }

                builder.Append(cookie.Name).Append('=').Append(cookie.Value);
            }

            return builder.ToString();
        }

        return network.CookieJar.GetCookieHeader(uri) ?? "";
    }

    /// <summary>
    /// Stores one <c>Set-Cookie</c>-shaped string, refusing what a non-HTTP API is not allowed to do.
    /// </summary>
    /// <remarks>
    /// Two refusals, both from RFC 6265bis §5.5: a value carrying <c>HttpOnly</c> is ignored outright, and a
    /// value that would replace an existing <c>HttpOnly</c> cookie of the same name is ignored too. Both are
    /// silent, as the storage model says — a cookie the browser declines to store is not an exception.
    /// </remarks>
    internal static void Write(PageRuntime runtime, string header)
    {
        if (runtime.Page.Network is not { } network || DocumentUri(runtime) is not { } uri)
        {
            return;
        }

        if (!SetCookieParser.TryParse(header, out var parsed) || parsed is null)
        {
            return;
        }

        if (network.CookieJar is CookieContainerCookieJar container)
        {
            // "If the cookie was received from a 'non-HTTP' API and the cookie's http-only-attribute is set,
            // abort these steps and ignore the cookie entirely."
            if (parsed.HttpOnly)
            {
                return;
            }

            // "If the newly created cookie was received from a 'non-HTTP' API and the old-cookie's
            // http-only-attribute is set, abort these steps and ignore the newly created cookie entirely."
            foreach (Cookie cookie in Cookies(container, uri))
            {
                if (cookie.HttpOnly && string.Equals(cookie.Name, parsed.Name, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        network.CookieJar.StoreResponseCookies(uri, [header]);
    }

    private static CookieCollection Cookies(CookieContainerCookieJar jar, Uri uri)
    {
        lock (jar.Container)
        {
            return jar.Container.GetCookies(uri);
        }
    }

    /// <summary>
    /// The document's URL as the request URL a cookie is stored against, or <see langword="null"/> when the
    /// document has no such URL — which is every document with an opaque origin, and which the storage
    /// model answers with no cookies rather than with an error.
    /// </summary>
    private static Uri? DocumentUri(PageRuntime runtime)
    {
        var url = runtime.Document?.Url;
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }
}
