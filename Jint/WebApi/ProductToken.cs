#if NET8_0_OR_GREATER
using System.Globalization;

namespace Jint.WebApi;

/// <summary>
/// The engine's own product token, <c>Jint/&lt;major.minor.patch&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two readers, one string, and that is the point.</b> It is what <c>navigator.userAgent</c> answers
/// (https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-useragent) and it is the
/// <i>default <c>User-Agent</c> value</i> a request carries
/// (https://fetch.spec.whatwg.org/#default-user-agent-value) — an engine that says one thing to script and
/// another on the wire is the defect this exists to prevent.
/// </para>
/// <para>
/// Built once for the process: the assembly version cannot change while it is loaded.
/// </para>
/// </remarks>
internal static class ProductToken
{
    /// <summary>The token itself.</summary>
    internal static string UserAgent { get; } = "Jint/" + ProductVersion();

    /// <summary>
    /// The same token as a <see cref="Native.JsString"/>, for the engines that took the default: the version
    /// cannot change while the assembly is loaded, so every realm of every such engine hands out this one.
    /// </summary>
    internal static Native.JsString UserAgentString { get; } = new(UserAgent);

    /// <summary>
    /// The <c>product-version</c> half of the token: Jint's own assembly version, as
    /// <c>major.minor.patch</c>. The fourth component is dropped because Jint never sets one, and the whole
    /// thing degrades to <c>"0.0.0"</c> rather than throwing if the assembly somehow carries no version.
    /// </summary>
    private static string ProductVersion()
    {
        var version = typeof(Engine).Assembly.GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        var build = version.Build < 0 ? 0 : version.Build;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{build}");
    }
}
#endif
