#nullable enable

using System.Net;
using System.Net.Http;
using Jint.Browser;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeMediaLoadTests
{
    [Test]
    public async Task MediaAccessAndPlaybackRefusalMakeNoResourceRequest()
    {
        using var handler = new CountingHandler();
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser();
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='https://example.test/audio.mp3' autoplay></audio>");
        await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            var state = BrowserMediaState.Of(realm, MediaElement(engine));
            state.SetVolume(realm, .5);
            state.SetCurrentTime(realm, 4);
            state.CanPlayType("audio/mpeg").Should().BeEmpty();
            engine.SetValue("attempt", state.Play(realm));
            engine.Execute("attempt.catch(()=>{});");
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        handler.Count.Should().Be(0);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ExplicitMediaLoadUsesTheActualResourceOwnerWithoutInventingMetadata()
    {
        using var handler = new CountingHandler();
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser();
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='https://example.test/audio.mp3'></audio>");
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RunOnLoopAsync(engine =>
        {
            engine.SetValue("mediaComplete", new Action(() => completed.TrySetResult()));
            engine.Execute("document.getElementById('media').addEventListener('error',mediaComplete,{once:true});");
            var realm = DomRealm.Of(engine);
            BrowserMediaState.Of(realm, MediaElement(engine)).Load(realm);
            return true;
        });
        await completed.Task.WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
        handler.Count.Should().Be(1);
        (await page.RunOnLoopAsync(engine =>
        {
            var state = BrowserMediaState.Of(DomRealm.Of(engine), MediaElement(engine));
            return $"{state.NetworkState}:{state.ReadyState}:{state.Error?.Code}:{state.VideoWidth}:{state.CurrentSrc}";
        })).Should().Be("3:0:4:0:https://example.test/audio.mp3");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SourceMutationDiscardsQueuedCompletionAndNextLoadUsesTheFreshSource()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='data:audio/mpeg,old'></audio>");
        await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            var element = MediaElement(engine);
            BrowserMediaState.Of(realm, element).Load(realm);
            element.SetAttributeNS(null, "src", "data:audio/mpeg,new");
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.RunOnLoopAsync(engine =>
        {
            var state = BrowserMediaState.Of(DomRealm.Of(engine), MediaElement(engine));
            return $"{state.NetworkState}:{state.CurrentSrc}:{state.Error?.Code}";
        })).Should().Be("0::");
        await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            BrowserMediaState.Of(realm, MediaElement(engine)).Load(realm);
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.RunOnLoopAsync(engine =>
        {
            var state = BrowserMediaState.Of(DomRealm.Of(engine), MediaElement(engine));
            return $"{state.NetworkState}:{state.CurrentSrc}:{state.Error?.Code}";
        })).Should().Be("3:data:audio/mpeg,new:4");
        page.Errors.Should().BeEmpty();
    }

    private static Element MediaElement(Engine engine)
        => (Element) ((DomNodeObject) engine.Evaluate("document.getElementById('media')")).Node;

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _count;
        internal int Count => Volatile.Read(ref _count);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            });
        }
    }
}
