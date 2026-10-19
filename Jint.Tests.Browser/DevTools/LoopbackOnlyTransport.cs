using System.Collections.Concurrent;
using System.Net;
using Jint.Browser;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The transport every context of a client suite's browser sends through: a request addressed to a loopback
/// host goes over a real socket, and one addressed to any other host never leaves the process.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is installed on every context rather than on the one a test means.</b> A client opens a page in
/// whichever context it names, and PuppeteerSharp's <c>browser.NewPageAsync()</c> names none: its
/// <c>Target.createTarget</c> carries no <c>browserContextId</c>, so the page opens in the browser's
/// <b>default</b> context, as it would in Chrome. A <c>UrlFilter</c> set on a context created beside that one
/// bounds nothing the page does, which is how a test once sent a real GET to <c>evil.com</c> (#4306).
/// <see cref="Install"/> goes through <see cref="BrowserOptions.ConfigureContext"/>, which reaches the default
/// context, a context a host creates, and one a client creates with <c>Target.createBrowserContext</c> — so the
/// filter and this transport are the page's whichever context it opened in.
/// </para>
/// <para>
/// <b>A request for another host is refused and recorded, not sent.</b> No socket is opened and no name is
/// resolved, and <see cref="AssertNothingEscaped"/>, which each suite runs once a test is over, fails the test
/// that made it. A test that needs other hosts to answer opts in with
/// <see cref="AnswerOtherHostsInProcess"/>: they are answered here, in process, and listed in
/// <see cref="Answered"/>.
/// </para>
/// <para>
/// A <c>WebSocket</c> does not go through an <see cref="HttpClient"/>, so for a socket the context's filter is
/// the only bound. That is why <see cref="Install"/> sets the filter too, and why a test that admits every
/// host has to take the in-process answers.
/// </para>
/// </remarks>
internal sealed class LoopbackOnlyTransport : DelegatingHandler
{
    private readonly ConcurrentQueue<string> _refused = new();
    private readonly ConcurrentQueue<string> _answered = new();
    private int _contexts;

    /// <summary>Creates the transport, with the same handler settings as Jint's own shared client.</summary>
    internal LoopbackOnlyTransport()
        : base(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
            UseCookies = false,
        })
    {
        Client = new HttpClient(this, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>The client every context of the browser is given.</summary>
    internal HttpClient Client { get; }

    /// <summary>
    /// Whether a request for a non-loopback host is answered here — <c>200</c>, <c>went</c>, readable from any
    /// origin — instead of being refused. Off unless a test opts in.
    /// </summary>
    internal bool AnswerOtherHostsInProcess { get; set; }

    /// <summary>Every non-loopback URL answered in process, in the order it was asked for.</summary>
    internal IReadOnlyList<string> Answered => [.. _answered];

    /// <summary>
    /// Makes every context of a browser built from <paramref name="options"/> send through this transport,
    /// loading only what <paramref name="filter"/> admits unless the context names a filter of its own.
    /// </summary>
    /// <returns><paramref name="options"/>, so the browser can be built from it in one expression.</returns>
    internal BrowserOptions Install(BrowserOptions options, Func<Uri, bool> filter) => options.ConfigureContext(context =>
    {
        // The guard is the transport, so a context that brought its own would be a context it does not guard.
        if (context.HttpClient is not null || context.HttpClientFactory is not null)
        {
            throw new InvalidOperationException(
                "A client-suite context sends through LoopbackOnlyTransport. A test that needs another host to answer sets AnswerOtherHostsInProcess instead of supplying a client.");
        }

        Interlocked.Increment(ref _contexts);
        context.HttpClient = Client;
        context.UrlFilter ??= filter;
    });

    /// <summary>
    /// Fails the test if a browser it drove was not built through <see cref="Install"/>, or if a request for a
    /// non-loopback host reached this transport and was refused.
    /// </summary>
    internal void AssertNothingEscaped()
    {
        Volatile.Read(ref _contexts).Should().BePositive(
            "a client-suite test builds its browser through LoopbackOnlyTransport.Install, so that every context of it, the default one included, is bounded");

        _refused.Should().BeEmpty(
            "a client-suite test may reach loopback hosts only; each of these got past the context's filter and was refused here rather than sent");
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => request.RequestUri is { IsLoopback: true }
            ? base.SendAsync(request, cancellationToken)
            : Task.FromResult(Answer(request));

    /// <inheritdoc/>
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        => request.RequestUri is { IsLoopback: true }
            ? base.Send(request, cancellationToken)
            : Answer(request);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
        }

        base.Dispose(disposing);
    }

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var url = request.RequestUri?.AbsoluteUri ?? "(no URL)";

        if (!AnswerOtherHostsInProcess)
        {
            _refused.Enqueue(request.Method + " " + url);
            throw new HttpRequestException("LoopbackOnlyTransport refused " + url + ": a client-suite test may reach loopback hosts only.");
        }

        _answered.Enqueue(url);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("went"),
            RequestMessage = request,
        };

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        return response;
    }
}
