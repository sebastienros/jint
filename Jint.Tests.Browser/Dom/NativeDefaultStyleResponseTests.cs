using System.Net;
using System.Net.Http;
using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeDefaultStyleResponseTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task FramePreferenceIsFrozenBeforeResponseAndCompletionObservers(bool mutateAtCompletion)
    {
        using var handler = new PreferenceHandler();
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser();
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        var observer = new PreferenceObserver(handler, mutateAtCompletion);
        await page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.Requests.Listener = observer;
            return true;
        });
        await page.SetContentAsync("<iframe src=/child></iframe>", "https://preferences.test/root");
        observer.Mutated.Should().BeTrue();
        (await page.EvaluateAsync<string>("frames[0].firstPreference")).Should().Be("frozen");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("night, literal")]
    [TestCase("")]
    public async Task ResponsePreferencePrecedesFirstScriptAndBelongsOnlyToItsDocument(string lastValue)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/child", _ => LoopbackResponse.Html("<script>window.firstPreference=document.preferredStyleSheetSet;</script>")
                .With("Default-Style", "child-first").With("Default-Style", "child-last"))
            .Map("/", _ => LoopbackResponse.Html("""
                <script>window.firstPreference=document.preferredStyleSheetSet;</script>
                <iframe id=network src=/child></iframe>
                <iframe id=blank></iframe>
                <iframe id=inline srcdoc="<script>window.firstPreference=document.preferredStyleSheetSet;</script>"></iframe>
                """).With("Default-Style", "first").With("Default-Style", lastValue)));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("firstPreference")).Should().Be(lastValue);
        (await fixture.Page.EvaluateAsync<string>("frames[0].firstPreference")).Should().Be("child-last");
        (await fixture.Page.EvaluateAsync<string>("blank.contentDocument.preferredStyleSheetSet")).Should().Be("");
        (await fixture.Page.EvaluateAsync<string>("frames[2].firstPreference")).Should().Be("");
        (await fixture.Page.EvaluateAsync<string>("new Document().preferredStyleSheetSet")).Should().Be("");
        fixture.Page.Errors.Should().BeEmpty();
    }

    private sealed class PreferenceHandler : HttpMessageHandler
    {
        internal HttpResponseMessage? Response { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<script>window.firstPreference=document.preferredStyleSheetSet;</script>",
                    System.Text.Encoding.UTF8, "text/html"),
            };
            Response.Headers.TryAddWithoutValidation("Default-Style", new[] { "first", "frozen" });
            return Task.FromResult(Response);
        }
    }

    private sealed class PreferenceObserver(PreferenceHandler handler, bool mutateAtCompletion) : IPageNetworkListener
    {
        internal bool Mutated { get; private set; }
        private void Mutate()
        {
            handler.Response!.Headers.Remove("Default-Style");
            handler.Response.Headers.TryAddWithoutValidation("Default-Style", "mutated");
            Mutated = true;
        }

        public ValueTask<PageNetworkDecision> RequestWillBeSentAsync(PageNetworkRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(PageNetworkDecision.Proceed);
        public ValueTask<PageNetworkResponseDecision> ResponseWillBeDeliveredAsync(PageNetworkRequest request,
            PageNetworkResponse response, PageResponseBodyReader body, CancellationToken cancellationToken)
            => ValueTask.FromResult(PageNetworkResponseDecision.Proceed);
        public ValueTask<PageNetworkAuthDecision> AuthRequiredAsync(PageNetworkRequest request,
            PageNetworkAuthChallenge challenge, CancellationToken cancellationToken)
            => ValueTask.FromResult(PageNetworkAuthDecision.Proceed);
        public void ResponseReceived(PageNetworkRequest request, PageNetworkResponse response)
        {
            if (!mutateAtCompletion) Mutate();
        }
        public void LoadingFinished(string requestId, long encodedLength)
        {
            if (mutateAtCompletion) Mutate();
        }
        public void DataReceived(string requestId, int length) { }
        public void LoadingFailed(string requestId, PageRequestKind kind, string errorText, bool canceled, string? blockedReason) { }
        public void WebSocketCreated(string socketId, string url) { }
        public void WebSocketHandshakeRequest(string socketId, IReadOnlyList<PageHeader> headers) { }
        public void WebSocketHandshakeResponse(string socketId, int status, string statusText, IReadOnlyList<PageHeader> headers) { }
        public void WebSocketClosed(string socketId) { }
        public void NotFetched(PageNetworkRequest request, string reason) { }
    }
}
