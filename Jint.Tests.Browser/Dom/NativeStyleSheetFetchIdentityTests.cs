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

    private sealed class ReleaseObserver(TaskCompletionSource release) : IPageObserver
    {
        public void DocumentCreated(PageRuntime runtime, string loaderId)
            => runtime.Engine.SetValue("releaseSheetBody", (Action) (() => release.TrySetResult()));
    }
}
