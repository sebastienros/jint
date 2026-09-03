using Jint.Browser.Runtime;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Storage;
using Jint.DevTools.Session;

namespace Jint.Browser.DevTools;

/// <summary>
/// The <c>Storage</c> domain on the <b>browser</b> session, where a command names a context rather than a
/// page.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same three commands as <see cref="StorageDomain"/>, addressed differently, and both are needed.</b>
/// Puppeteer reads a page's cookies by sending <c>Storage.getCookies</c> on that page's own session;
/// Playwright reads a <i>context</i>'s by sending it on the browser session with a <c>browserContextId</c>.
/// Chrome answers both. Registering the domain on page sessions only answered the first client and gave the
/// second <c>-32601</c>, which is what <c>PlaywrightCourseTests</c> found the moment it called
/// <c>context.cookies()</c>.
/// </para>
/// <para>
/// <b>An absent <c>browserContextId</c> is the default context</b>, which is what a client that never created
/// one is asking about; an unknown one is the same refusal <c>Target</c> gives, in Chrome's wording.
/// </para>
/// </remarks>
internal sealed class BrowserStorageDomain : StorageDomainBase
{
    private readonly BrowserTargetHost _host;

    internal BrowserStorageDomain(BrowserTargetHost host)
    {
        _host = host;
    }

    /// <inheritdoc/>
    protected override ValueTask<GetCookiesResponse> GetCookiesAsync(GetCookiesRequest parameters, CommandContext context)
        => new(new GetCookiesResponse { Cookies = PageCookies.All(Network(parameters.BrowserContextId)) });

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> SetCookiesAsync(SetCookiesRequest parameters, CommandContext context)
    {
        var network = Network(parameters.BrowserContextId);

        foreach (var cookie in parameters.Cookies)
        {
            // No page to resolve a relative cookie against here, so a cookie with neither a domain nor a URL
            // is one this command cannot place — PageCookies refuses it rather than guessing an origin.
            PageCookies.Set(network, cookie, "");
        }

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> ClearCookiesAsync(ClearCookiesRequest parameters, CommandContext context)
    {
        PageCookies.Clear(Network(parameters.BrowserContextId));
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    private PageNetwork Network(string? browserContextId) => _host.NetworkOf(browserContextId);
}
