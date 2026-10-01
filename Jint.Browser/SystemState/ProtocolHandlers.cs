using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.SystemState;

/// <summary>
/// <c>navigator.registerProtocolHandler</c> and <c>unregisterProtocolHandler</c>: validated exactly as HTML
/// says, and then declined.
/// </summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/system-state.html#custom-handlers — a user agent may ignore a
/// registration it has validated, and one with no user to ask whether a site may handle <c>mailto:</c> links
/// has nothing to do with it. The validation is not optional, though: a page that passes a bad scheme or a
/// cross-origin URL is told so with the <c>SecurityError</c> or <c>SyntaxError</c> a browser raises.
/// </remarks>
internal static class ProtocolHandlers
{
    /// <summary>https://html.spec.whatwg.org/multipage/system-state.html#safelisted-scheme</summary>
    private static readonly string[] _safelisted =
    [
        "bitcoin", "ftp", "ftps", "geo", "im", "irc", "ircs", "magnet", "mailto", "matrix", "mms", "news", "nntp",
        "openpgp4fpr", "sftp", "sip", "sms", "smsto", "ssh", "tel", "urn", "webcal", "wtai", "xmpp",
    ];

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#normalize-protocol-handler-parameters, then
    /// nothing.
    /// </summary>
    internal static JsValue Register(PageRuntime runtime, JsValue[] arguments, string member)
    {
        if (arguments.Length < 2)
        {
            Throw.TypeError(
                runtime.Engine._mainRealm,
                "Failed to execute '" + member + "' on 'Navigator': 2 arguments required, but only " + arguments.Length + " present.");
        }

        var scheme = TypeConverter.ToString(arguments[0]);
        var url = TypeConverter.ToString(arguments[1]);
        Normalize(runtime, scheme, url, member);
        return JsValue.Undefined;
    }

    private static void Normalize(PageRuntime runtime, string scheme, string url, string member)
    {
        // Step 1: ASCII lowercase.
        scheme = scheme.ToLowerInvariant();

        // Step 2: a safelisted scheme, or `web+` followed by one or more ASCII lower alphas.
        if (Array.IndexOf(_safelisted, scheme) < 0 && !IsWebPlusScheme(scheme))
        {
            DomFailures.Refuse(
                runtime.Dom,
                member + "' on 'Navigator",
                DomExceptionNames.Security,
                "The scheme '" + scheme + "' doesn't belong to the scheme allowlist. Please prefix non-allowlisted schemes with the string 'web+'.");
        }

        // Step 3.
        if (!url.Contains("%s", StringComparison.Ordinal))
        {
            DomFailures.Refuse(
                runtime.Dom,
                member + "' on 'Navigator",
                DomExceptionNames.Syntax,
                "The url provided ('" + url + "') does not contain '%s'.");
        }

        // Steps 4 and 5: it must parse, be HTTP(S), and share the document's origin.
        var record = PageUrl.Parse(url, runtime.BaseUri);
        if (record is null)
        {
            DomFailures.Refuse(
                runtime.Dom,
                member + "' on 'Navigator",
                DomExceptionNames.Syntax,
                "The custom handler URL created by removing '%s' and prepending '" + runtime.BaseUri + "' is invalid.");
            return;
        }

        if (record.Scheme is not ("http" or "https")
            || !string.Equals(record.SerializeOrigin(), PageUrl.OriginOf(runtime.DocumentUrl), StringComparison.Ordinal))
        {
            DomFailures.Refuse(
                runtime.Dom,
                member + "' on 'Navigator",
                DomExceptionNames.Security,
                "Can only register custom handler in the document's origin.");
        }
    }

    private static bool IsWebPlusScheme(string scheme)
    {
        if (scheme.Length <= 4 || !scheme.StartsWith("web+", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 4; i < scheme.Length; i++)
        {
            if (scheme[i] is < 'a' or > 'z')
            {
                return false;
            }
        }

        return true;
    }
}
