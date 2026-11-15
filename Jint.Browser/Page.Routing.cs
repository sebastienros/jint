using Jint.Browser.Runtime;

namespace Jint.Browser;

public sealed partial class Page
{
    /// <summary>This page's own routes; its context's are consulted after them.</summary>
    private readonly PageRouteTable _routes = new();

    /// <summary>
    /// Routes every request of this page whose URL <paramref name="match"/> accepts to
    /// <paramref name="handler"/>, which may fulfill, abort or continue it.
    /// </summary>
    /// <param name="match">Which URLs to route; called off the page's loop for every hop.</param>
    /// <param name="handler">What to do with a matching request; see <see cref="RoutedRequest"/>.</param>
    /// <returns>A task that completes once the route applies to every request sent after it.</returns>
    /// <remarks>
    /// <para>
    /// Documents, child frames, scripts, style sheets, images, fonts, <c>fetch</c>, <c>XMLHttpRequest</c> and a
    /// dedicated worker's loads are all routed, each redirect hop afresh. The newest matching route is asked
    /// first, then older ones, then the context's (<see cref="BrowserContext.RouteAsync"/>).
    /// </para>
    /// <para>
    /// <see cref="BrowserContextOptions.UrlFilter"/> still runs first: a URL the filter refuses never reaches
    /// a route, so the filter stays the cheap allow-list. <c>data:</c>, <c>blob:</c> and <c>about:</c> URLs
    /// make no request and are not routed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> or <paramref name="handler"/> is null.</exception>
    public Task RouteAsync(Func<Uri, bool> match, Func<RoutedRequest, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(handler);

        if (_closed)
        {
            return Task.FromException(new ObjectDisposedException(nameof(Page)));
        }

        _routes.Add(new PageRoute(match, handler));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the routes this page registered with <paramref name="match"/>, or only the one with
    /// <paramref name="handler"/> as well when it is named.
    /// </summary>
    /// <param name="match">The predicate a route was registered with, compared as a delegate.</param>
    /// <param name="handler">The handler to remove, or <see langword="null"/> for every handler of <paramref name="match"/>.</param>
    /// <returns>A task that completes once no request sent afterwards reaches the removed routes.</returns>
    /// <remarks>A request already being routed is decided by the routes that existed when it went out.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> is null.</exception>
    public Task UnrouteAsync(Func<Uri, bool> match, Func<RoutedRequest, Task>? handler = null)
    {
        ArgumentNullException.ThrowIfNull(match);
        _routes.Remove(match, handler);
        return Task.CompletedTask;
    }
}
