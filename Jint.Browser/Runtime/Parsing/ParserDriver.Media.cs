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
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        operationCancellation.ThrowIfCancellationRequested();
        _cancellationToken.ThrowIfCancellationRequested();
        _runtime.Engine.Constraints.Check();
        var document = source.OwnerDocument!;
        var url = PageUrl.Resolve(requested, BaseUrlOf(document));
        var target = url is null ? null : UrlParser.Parse(url);
        _runtime.Engine.Constraints.Check();
        operationCancellation.ThrowIfCancellationRequested();
        if (target is null) throw new InvalidOperationException("The media source is not a URL a page can load.");
        if (DataUrl.Is(target))
        {
            if (!DataUrl.TryProcess(target, out var content))
                throw new InvalidOperationException("The media source is not a valid data URL.");
            _runtime.Engine.Constraints.Check();
            operationCancellation.ThrowIfCancellationRequested();
            if (content.Body.LongLength > _maxBytes)
                throw new InvalidOperationException("The media source exceeds BrowserOptions.MaxSubresourceBytes.");
            return Task.FromResult(new MediaResourceResponse(content.Body, target.Serialize(), content.MimeType.Serialize()));
        }
        if (!PageUrl.IsNetworkScheme(target))
            throw new InvalidOperationException("The media source has a scheme a page cannot load.");
        var documentUrl = UrlParser.Parse(DomDocumentState.Of(document).Url);
        var request = new SubresourceRequest(target, documentUrl, documentUrl, _maxBytes, _maxRedirects,
            RequestInitiator.Subresource, _runtime.Emulation.EffectiveUserAgent, PageRequestKind.Other);
        return RequestMediaTransportAsync(request, operationCancellation);
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
