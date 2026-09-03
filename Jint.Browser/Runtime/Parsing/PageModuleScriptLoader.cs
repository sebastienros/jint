using System.Net.Http;
using Jint.Runtime;
using Jint.Runtime.Modules;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// The module loader a page's own module scripts and its <c>import()</c> calls resolve against: the
/// document's base URL, its import map, and the page's network position.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is asynchronous, and that is the whole reason it is not
/// <see cref="Workers.PageModuleLoader"/>.</b> A worker loads its graph on its own thread inside its start
/// job, so blocking there holds nothing else up; a page loads its graph on the thread that owns the engine,
/// the DOM and every timer the page has running, so blocking there would stop the page. Implementing
/// <c>IAsyncModuleLoader</c> means the fetch is started and the engine given back, and the page loop drives
/// the load to completion by pumping — which is what lets a timer fire while a module graph is in flight.
/// </para>
/// <para>
/// <b>An inline <c>&lt;script type="module"&gt;</c> is a module with a URL of its own.</b> It is registered
/// here under the document's URL with a fragment that names it, so that its relative imports resolve against
/// the document exactly as an external module's would, its identity in the module map is its own, and a
/// stack trace names something a reader can find.
/// </para>
/// <para>
/// <b>A bare specifier needs an import map</b>, and without a matching entry it is refused with the reason
/// rather than quietly resolved to a path on the origin — which is what treating <c>lodash</c> as
/// <c>./lodash</c> would do.
/// </para>
/// </remarks>
internal sealed class PageModuleScriptLoader : AsyncModuleLoader
{
    private readonly PageNetwork _network;
    private readonly PageNetworkRecorder _requests;
    private readonly Dictionary<string, string> _inline = new(StringComparer.Ordinal);
    private readonly long _maxBytes;

    private int _inlineCount;

    internal PageModuleScriptLoader(PageNetwork network, PageNetworkRecorder requests, string documentUrl, long maxBytes)
    {
        _network = network;
        _requests = requests;
        _maxBytes = maxBytes;
        BaseUrl = documentUrl;
    }

    /// <summary>The document's base URL, which relative specifiers resolve against.</summary>
    internal string BaseUrl { get; set; }

    /// <summary>The document's import map, or <see langword="null"/> when it declared none.</summary>
    internal ImportMap? Map { get; set; }

    /// <summary>Registers an inline module's source and answers the specifier that names it.</summary>
    internal string AddInline(string source)
    {
        var url = UrlParser.Parse(BaseUrl);
        var key = BaseUrl;

        if (url is not null)
        {
            url.Fragment = "jint-inline-module-" + _inlineCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            key = url.Serialize();
        }
        else
        {
            key += "#jint-inline-module-" + _inlineCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        _inlineCount++;
        _inline[key] = source;
        return key;
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

        if (_inline.ContainsKey(specifier))
        {
            return new ResolvedSpecifier(moduleRequest, specifier, Uri: null, SpecifierType.RelativeOrAbsolute);
        }

        var referrer = referencingModuleLocation ?? BaseUrl;
        var mapped = Map?.Resolve(specifier, referencingModuleLocation);
        var absolute = mapped ?? (IsUrlLike(specifier) ? PageUrl.Resolve(specifier, referrer) : null);

        if (absolute is null)
        {
            Throw.ModuleResolutionException(
                IsUrlLike(specifier)
                    ? "The specifier could not be resolved against the document's base URL"
                    : "A bare specifier can only be resolved through an import map, and this document's map has no entry for it",
                specifier,
                referencingModuleLocation);
            return default!;
        }

        Uri.TryCreate(absolute, UriKind.Absolute, out var uri);
        return new ResolvedSpecifier(moduleRequest, absolute, uri, SpecifierType.RelativeOrAbsolute);
    }

    /// <inheritdoc />
    protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
    {
        if (_inline.TryGetValue(resolved.Key, out var source))
        {
            return source;
        }

        var url = UrlParser.Parse(resolved.Key)
            ?? throw new InvalidOperationException("'" + resolved.Key + "' is not a URL a page can load.");

        if (!PageUrl.IsNetworkScheme(url))
        {
            throw new InvalidOperationException(
                "A page resolves its modules over its own network, so only an http(s) URL can be imported; '"
                + resolved.Key + "' is not one.");
        }

        // Read on the engine thread, which is where a host's client factory is documented to be called.
        var client = _network.ClientFor(engine);
        var referrer = UrlParser.Parse(BaseUrl);

        var fetched = await SubresourceFetch.LoadAsync(
            _network,
            client,
            new SubresourceRequest(url, referrer, referrer, _maxBytes, MaxRedirects, RequestInitiator.Subresource, PageRequestKind.Script),
            _requests,
            cancellationToken).ConfigureAwait(false);

        // https://html.spec.whatwg.org/multipage/webappapis.html#fetch-a-single-module-script step 13: a
        // module whose MIME type is not a JavaScript one is a failure, unlike a classic script, which is
        // fetched with no type check at all.
        if (!IsJavaScript(fetched.ContentType))
        {
            throw new InvalidOperationException(
                "'" + fetched.Url + "' answered with a '" + (fetched.ContentType ?? "")
                + "' content type, and a module script has to be served as JavaScript.");
        }

        return fetched.Text();
    }

    /// <summary>How many hops a module's redirect chain may follow.</summary>
    private const int MaxRedirects = 20;

    private static bool IsUrlLike(string specifier)
        => specifier.StartsWith('/')
        || specifier.StartsWith("./", StringComparison.Ordinal)
        || specifier.StartsWith("../", StringComparison.Ordinal)
        || UrlParser.Parse(specifier) is not null;

    private static bool IsJavaScript(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var essence = MimeType.Parse(contentType)?.Essence;
        return essence is not null && AngleSharp.Io.MimeTypeNames.IsJavaScript(essence);
    }
}
