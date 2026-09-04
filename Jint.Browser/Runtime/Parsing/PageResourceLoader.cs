using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// The <c>IResourceLoader</c> AngleSharp's parser asks for everything a document references, answered over
/// the page's own network position and, for everything a headless browser has no use for, refused out loud.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registering one at all is what makes the parse asynchronous</b>, and therefore what makes the baton
/// necessary: without a loader AngleSharp never starts a download, an external <c>&lt;script src&gt;</c>
/// never produces a response, and the whole parse runs inline. See <see cref="ParserBaton"/>.
/// </para>
/// <para>
/// <b>Every fetch here finishes before this method returns</b>, so the download AngleSharp receives is
/// already complete and the parse never suspends on it. The waiting happens on the parser thread, inside
/// <see cref="ParserBaton.RunOnLoop{T}"/>, while the page loop holds the baton and pumps its timers — which
/// is the browser-correct timing the design asked for, reached from the opposite direction. The cost is that
/// a <c>defer</c> or <c>async</c> script's fetch is sequential with the parse rather than parallel to it:
/// they still <em>execute</em> when they should, but the download is not overlapped.
/// </para>
/// <para>
/// <b>What is refused, and why it is still recorded.</b> There is no rendering here, so an <c>&lt;img&gt;</c>,
/// a media element and a non-stylesheet <c>&lt;link&gt;</c> have nothing to be for; fetching them would be
/// traffic a page never sees the result of. The reference is written into <see cref="Page.Requests"/> with a
/// <see cref="PageRequest.NotFetchedReason"/> instead, so a caller sees everything the document asked for
/// rather than the subset something chose to answer.
/// </para>
/// <para>
/// <b>An <c>&lt;iframe&gt;</c> is answered</b>, because a frame's document is something a page can reach:
/// <see cref="ParserDriver.FetchFrame"/> fetches it and AngleSharp opens it into the nested browsing context
/// it already made for the element. A <c>&lt;frame&gt;</c> is not, and <c>ParserDriver.IsLegacyFrame</c>
/// says why.
/// </para>
/// <para>
/// A refusal and a failure are both a download that completes with a <see langword="null"/> response, which
/// is the shape AngleSharp's own processors already test for: the script processor runs nothing and the
/// resource processors fire their own <c>error</c> into their own listener lists. What a <em>page</em> sees
/// is dispatched through Jint's dispatcher by the driver instead.
/// </para>
/// </remarks>
internal sealed class PageResourceLoader : IResourceLoader
{
    private readonly ParserDriver _driver;

    internal PageResourceLoader(ParserDriver driver)
    {
        _driver = driver;
    }

    /// <inheritdoc />
    public IEnumerable<IDownload> GetDownloads() => [];

    /// <inheritdoc />
    public IDownload FetchAsync(ResourceRequest request)
    {
        var target = request.Target;
        var url = target.Href;

        var response = request.Source switch
        {
            IHtmlScriptElement script => _driver.FetchScript(script, url),
            IHtmlLinkElement link when IsStyleSheet(link) => _driver.FetchStyleSheet(link, url),
            IHtmlInlineFrameElement frame => _driver.FetchFrame(frame, url),
            _ => _driver.RefuseSubresource(request.Source, url),
        };

        return new CompletedDownload(response, target, request.Source);
    }

    private static bool IsStyleSheet(IHtmlLinkElement link)
    {
        foreach (var relation in link.RelationList)
        {
            if (string.Equals(relation, "stylesheet", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the response AngleSharp reads a fetched resource out of.</summary>
    internal static IResponse Answer(string url, byte[] bytes, string? contentType)
    {
        var response = new DefaultResponse
        {
            StatusCode = HttpStatusCode.OK,
            Address = Url.Create(url),
            Content = new MemoryStream(bytes, writable: false),
        };

        if (contentType is { Length: > 0 })
        {
            response.Headers[HeaderNames.ContentType] = contentType;
        }

        return response;
    }

    /// <summary>
    /// A download that was over before it was handed out: the whole point of the baton is that the parser
    /// never awaits one.
    /// </summary>
    private sealed class CompletedDownload(IResponse? response, Url target, object? source) : IDownload
    {
        public Task<IResponse> Task { get; } = System.Threading.Tasks.Task.FromResult(response!);

        public Url Target => target;

        public object? Source => source;

        public bool IsRunning => false;

        public bool IsCompleted => true;

        public void Cancel()
        {
        }
    }
}
