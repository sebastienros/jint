using System.Net.Http;
using System.Text;
using Jint.Browser.Runtime;
using Jint.Runtime;
using Jint.Runtime.Modules;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Workers;

/// <summary>
/// The module loader a page's workers resolve against: relative to the page's document, fetched over the
/// page's own network position.
/// </summary>
/// <remarks>
/// <para>
/// <b>It blocks, and that is what it is allowed to do.</b> Jint's <c>IModuleLoader</c> is synchronous, and a
/// worker's module graph is loaded on the worker's own thread inside the start job the engine queued there —
/// so the thread that waits is the worker's, and nothing else in the process is held up. The page's loop is
/// not this thread and never enters here.
/// </para>
/// <para>
/// <b>The policy is the context's.</b> The same <c>UrlFilter</c>, cookie jar and client every navigation,
/// subresource and <c>fetch</c> goes through, re-checked per redirect hop — a worker is not a way around the
/// filter that bounds the page that started it.
/// </para>
/// <para>
/// <b>A bare specifier is refused, deliberately.</b> There is no import map yet (campaign item R3), so
/// <c>import 'lodash'</c> has nothing to resolve against; refusing it with the reason is better than
/// resolving it to a URL nobody asked for.
/// </para>
/// </remarks>
internal sealed class PageModuleLoader : ModuleLoader
{
    private readonly PageNetwork _network;
    private readonly PageNetworkRecorder _requests;
    private readonly HttpClient _client;
    private readonly Uri _baseUrl;
    private readonly long _maxBytes;
    private readonly TimeSpan _timeout;

    internal PageModuleLoader(
        PageNetwork network,
        PageNetworkRecorder requests,
        HttpClient client,
        Uri baseUrl,
        long maxBytes,
        TimeSpan timeout)
    {
        _network = network;
        _requests = requests;
        _client = client;
        _baseUrl = baseUrl;
        _maxBytes = maxBytes;
        _timeout = timeout;
    }

    /// <inheritdoc />
    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var specifier = moduleRequest.Specifier;

        if (string.IsNullOrEmpty(specifier))
        {
            Throw.ModuleResolutionException("Invalid Module Specifier", specifier, referencingModuleLocation);
            return default!;
        }

        var relativeTo = referencingModuleLocation ?? _baseUrl.AbsoluteUri;
        var resolved = PageUrl.Parse(specifier, relativeTo);

        if (resolved is null || !PageUrl.IsNetworkScheme(resolved) || PageUrl.ToUri(resolved) is not { } uri)
        {
            Throw.ModuleResolutionException(
                "A worker resolves its modules over the page's network, so only an http(s) URL or a URL "
                + "relative to one can be imported",
                specifier,
                referencingModuleLocation);
            return default!;
        }

        return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
    }

    /// <inheritdoc />
    protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
    {
        if (resolved.Uri is not { } uri)
        {
            Throw.ModuleResolutionException("Invalid Module Specifier", resolved.ModuleRequest.Specifier, parent: null);
            return "";
        }

        if (!_network.UrlFilter(uri))
        {
            throw new InvalidOperationException("The page's URL filter refused '" + uri + "'.");
        }

        var url = Jint.WebApi.Url.Parsing.UrlParser.Parse(uri.AbsoluteUri)
            ?? throw new InvalidOperationException("'" + uri + "' is not a URL a page can load.");

        var request = new FetchRequestSnapshot
        {
            Method = "GET",
            Url = url,
            Headers = [],
            Body = null,
            BodyContent = null,
            Redirect = "follow",
            Credentials = JsRequest.CredentialsSameOrigin,
            Referrer = Jint.WebApi.Url.Parsing.UrlParser.Parse(_baseUrl.AbsoluteUri),
            ReferrerPolicy = ReferrerPolicy.StrictOriginWhenCrossOrigin,
        };

        var origin = Jint.WebApi.Url.Parsing.UrlParser.Parse(_baseUrl.AbsoluteUri);
        var policy = new FetchPolicy
        {
            AllowedSchemes = ["https", "http"],
            UrlFilter = _network.UrlFilter,
            MaxResponseBytes = _maxBytes,
            MaxRedirects = 20,
            Origin = origin,
            SameOriginReference = origin,
            CookieJar = _network.CookieJar,
        };

        using var cancellation = new CancellationTokenSource(_timeout);

        // The page's own network log sees a worker's module loads too, so Page.Requests is what the page
        // fetched rather than what its document fetched.
        var observation = _requests.Observe(RequestInitiator.Script, PageRequestKind.Script);

        try
        {
            // Blocking, on the worker's own thread; see the class remarks.
            using var exchange = FetchTransport
                .SendForStreamAsync(_client, request, policy, cancellation.Token, observation)
                .GetAwaiter()
                .GetResult();

            var bytes = exchange.Response.Content.ReadAsByteArrayAsync(cancellation.Token).GetAwaiter().GetResult();

            // The debt every SendForStreamAsync caller owes its observer; see FetchObservation.FinalResponse.
            observation?.FinalResponse(exchange);
            observation?.Data(bytes);
            observation?.Completed(bytes.Length);

            if (!exchange.Response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "'" + uri + "' answered " + ((int) exchange.Response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            }

            if (bytes.Length > _maxBytes)
            {
                throw new InvalidOperationException("'" + uri + "' is larger than a page may load.");
            }

            var text = Encoding.UTF8.GetString(bytes);
            return text.Length != 0 && text[0] == '﻿' ? text[1..] : text;
        }
        catch (Exception exception)
        {
            observation?.Failed(exception.Message, exception);
            throw;
        }
    }
}
