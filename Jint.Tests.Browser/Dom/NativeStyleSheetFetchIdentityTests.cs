using System.Net;
using System.Net.Http;
using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeStyleSheetFetchIdentityTests
{
    [Test]
    public async Task ALinkChangedDuringItsFetchDiscardsTheOldSheetAndCanLoadAgain()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        var delayed = new LoopbackResponse
        {
            Body = "p { color: red; }",
            WriteBodyAsync = async (stream, token) =>
            {
                await release.Task.WaitAsync(token);
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("p { color: red; }"), token);
            },
        }.With("Content-Type", "text/css");
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/sheet", _ => Interlocked.Increment(ref requests) == 1 ? delayed : LoopbackResponse.Css("p { color: blue; }"))
            .MapHtml("/", """
                <script>
                window.loads=0;
                setTimeout(() => {
                    document.getElementById('sheet').rel='preload';
                    releaseSheetBody();
                }, 0);
                </script>
                <link id=sheet rel=stylesheet href=/sheet onload='loads++'>
                """));
        fixture.Page.Observe(new ReleaseObserver(release));
        try
        {
            await fixture.Page.NavigateAsync(fixture.Url("/")).WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            (await fixture.Page.EvaluateAsync<int>("loads")).Should().Be(0);
            (await fixture.Page.EvaluateAsync<bool>("sheet.sheet === null")).Should().BeTrue();
            await fixture.Page.EvaluateAsync("sheet.rel='stylesheet'");
            (await fixture.Page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
            (await fixture.Page.EvaluateAsync<int>("loads")).Should().Be(1);
            requests.Should().Be(2);
            fixture.Page.Errors.Should().BeEmpty();
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestCase("success")]
    [TestCase("failure")]
    [TestCase("timeout")]
    public async Task ReturningToTheSameUrlKeepsTheNewestRequestSheetAndEvents(string firstResult)
    {
        using var handler = new ReplacedRequestHandler(firstResult);
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions
        {
            // The timeout case asserts this resource timeout, independently of the wedge ceiling.
            SubresourceTimeout = TimeSpan.FromMilliseconds(250),
        });
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        page.Observe(new ReleaseObserver(handler.Release));
        try
        {
            await page.SetContentAsync("""
                <script>
                window.loads=0; window.errors=0;
                setTimeout(() => {
                    sheet.href='/b';
                    sheet.href='/a';
                    releaseSheetBody();
                }, 0);
                </script>
                <link id=sheet rel=stylesheet href=/a onload='loads++' onerror='errors++'>
                <p id=target>text</p>
                """, "https://requests.test/root").WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
            (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
            (await page.EvaluateAsync<string>("getComputedStyle(target).color")).Should().Be("rgb(0, 0, 255)");
            (await page.EvaluateAsync<int>("loads")).Should().Be(1);
            (await page.EvaluateAsync<int>("errors")).Should().Be(0);
            handler.ARequests.Should().Be(2);
            handler.BRequests.Should().Be(1);
            page.Errors.Should().BeEmpty();
        }
        finally
        {
            handler.Release.TrySetResult();
        }
    }

    private sealed class ReplacedRequestHandler(string firstResult) : HttpMessageHandler
    {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int ARequests;
        internal int BRequests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string css;
            if (request.RequestUri!.AbsolutePath == "/a")
            {
                if (Interlocked.Increment(ref ARequests) == 1)
                {
                    if (firstResult == "timeout") await Task.Delay(Timeout.Infinite, cancellationToken);
                    else await Release.Task.WaitAsync(cancellationToken);
                    if (firstResult == "failure") throw new HttpRequestException("The first A request failed.");
                    css = "p { color: red; }";
                }
                else css = "p { color: blue; }";
            }
            else
            {
                Interlocked.Increment(ref BRequests);
                css = "p { color: green; }";
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(css, System.Text.Encoding.UTF8, "text/css"),
            };
        }
    }

    private sealed class ReleaseObserver(TaskCompletionSource release) : IPageObserver
    {
        public void DocumentCreated(PageRuntime runtime, string loaderId)
            => runtime.Engine.SetValue("releaseSheetBody", (Action) (() => release.TrySetResult()));
    }
}
