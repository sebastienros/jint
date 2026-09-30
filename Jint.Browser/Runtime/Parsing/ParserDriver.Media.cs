using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.WebApi.Fetch;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime.Parsing;

internal sealed partial class ParserDriver
{
    // HTML §4.8.11.5: an explicit media load uses the document's actual resource owner.
    // Resolve and capture DOM/environment inputs on the loop; transport completion carries no DOM or JsValue.
    internal Task<MediaResourceResponse> RequestMediaAsync(Element source, string requested,
        CancellationToken operationCancellation)
        => RequestMediaAsync(source, requested, out _, operationCancellation);

    internal Task<MediaResourceResponse> RequestMediaAsync(Element source, string requested,
        out string selectedUrl, CancellationToken operationCancellation)
    {
        selectedUrl = "";
        ObjectDisposedException.ThrowIf(_disposed, this);
        operationCancellation.ThrowIfCancellationRequested();
        _cancellationToken.ThrowIfCancellationRequested();
        return RequestResourceAsync(source.OwnerDocument!, requested, PageRequestKind.Other, out selectedUrl, operationCancellation);
    }

    /// <summary>
    /// https://drafts.csswg.org/css-fonts-4/#font-fetching-requirements — one <c>src</c> URL of a
    /// <c>FontFace</c>, resolved against <paramref name="document"/>'s base URL and fetched through the same
    /// owner and budget an explicit media load uses.
    /// </summary>
    internal Task<MediaResourceResponse> RequestFontAsync(Document document, string requested,
        CancellationToken operationCancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        operationCancellation.ThrowIfCancellationRequested();
        _cancellationToken.ThrowIfCancellationRequested();
        return RequestResourceAsync(document, requested, PageRequestKind.Font, out _, operationCancellation);
    }

    private Task<MediaResourceResponse> RequestResourceAsync(Document document, string requested, PageRequestKind kind,
        out string selectedUrl, CancellationToken operationCancellation)
    {
        selectedUrl = "";
        _runtime.Engine.Constraints.Check();
        var url = PageUrl.Resolve(requested, BaseUrlOf(document));
        var target = url is null ? null : UrlParser.Parse(url);
        _runtime.Engine.Constraints.Check();
        operationCancellation.ThrowIfCancellationRequested();
        if (target is null) throw new MediaSourceException("The media source is not a URL a page can load.");
        selectedUrl = target.Serialize();
        _runtime.Engine.Constraints.Check();
        operationCancellation.ThrowIfCancellationRequested();
        if (DataUrl.Is(target))
        {
            if (!DataUrl.TryProcess(target, out var content))
                throw new MediaSourceException("The media source is not a valid data URL.");
            _runtime.Engine.Constraints.Check();
            operationCancellation.ThrowIfCancellationRequested();
            if (content.Body.LongLength > _maxBytes)
                throw new MediaSourceException("The media source exceeds BrowserOptions.MaxSubresourceBytes.");
            return Task.FromResult(new MediaResourceResponse(content.Body, target.Serialize(), content.MimeType.Serialize()));
        }
        if (!PageUrl.IsNetworkScheme(target))
            throw new MediaSourceException("The media source has a scheme a page cannot load.");
        var documentUrl = UrlParser.Parse(DomDocumentState.Of(document).Url);
        var request = new SubresourceRequest(target, documentUrl, documentUrl, _maxBytes, _maxRedirects,
            RequestInitiator.Subresource, _runtime.Emulation.EffectiveUserAgent, kind);
        return RequestMediaTransportAsync(request, operationCancellation);
    }

    // Engine.TaskOperations.Post is the existing thread-safe enqueue; capture it on the loop
    // so transport completion never initializes or reads an engine-affine service.
    internal bool TryPostResourceCompletion(Action completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        if (Volatile.Read(ref _disposed)) return false;
        try
        {
            _resourceTasks.Post(() => { if (!_disposed) completion(); });
            return true;
        }
        catch (ObjectDisposedException) { return false; }
    }

    private async Task<MediaResourceResponse> RequestMediaTransportAsync(SubresourceRequest request,
        CancellationToken operationCancellation)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, operationCancellation);
        cancellation.CancelAfter(_timeout);
        var response = await SubresourceFetch.LoadAsync(_network, _client, request, _requests, cancellation.Token)
            .ConfigureAwait(false);
        cancellation.Token.ThrowIfCancellationRequested();
        return new MediaResourceResponse(response.Bytes, ResponseUrl(response.Url, response.Fragment), response.ContentType);
    }
}

internal readonly record struct MediaResourceResponse(byte[] Bytes, string Url, string? ContentType);

internal sealed class MediaSourceException(string message) : Exception(message);
