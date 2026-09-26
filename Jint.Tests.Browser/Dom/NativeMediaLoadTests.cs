#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading.Channels;
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

    [Test]
    public async Task OverlappingLoadsInvalidateOldQueuedMediaEvents()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='data:audio/mpeg,old'></audio>");
        await page.RunOnLoopAsync(engine =>
        {
            engine.Execute("var mediaEvents=[]; for(const name of ['abort','emptied','loadstart','error']) document.getElementById('media').addEventListener(name,e=>mediaEvents.push(e.type));");
            var realm = DomRealm.Of(engine);
            var element = MediaElement(engine);
            var state = BrowserMediaState.Of(realm, element);
            state.Load(realm);
            // Raw attribute writes start no second request. The next explicit load wins.
            element.SetAttributeNS(null, "src", "data:audio/mpeg,new");
            state.Load(realm);
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<string>("JSON.stringify(mediaEvents)"))
            .Should().Be("[\"loadstart\",\"error\"]");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ANewLoadDiscardsAnErrorAlreadyQueuedByItsPredecessor()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='data:audio/mpeg,old'></audio>");
        await page.RunOnLoopAsync(engine =>
        {
            engine.Execute("var mediaEvents=[]; for(const name of ['abort','emptied','loadstart','error']) document.getElementById('media').addEventListener(name,e=>mediaEvents.push(e.type));");
            var realm = DomRealm.Of(engine);
            var state = BrowserMediaState.Of(realm, MediaElement(engine));
            state.Load(realm);
            engine.Tasks.ProcessTask(); // loadstart
            engine.Tasks.ProcessTask(); // completion queues an error, but does not deliver it
            state.Load(realm);
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<string>("JSON.stringify(mediaEvents)"))
            .Should().Be("[\"loadstart\",\"emptied\",\"loadstart\",\"error\"]");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("ftp://example.test/audio.mp3")]
    [TestCase("http://[")]
    public async Task InvalidSelectedSourcesBecomeMediaFailuresAfterReset(string source)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<audio id=media></audio>");
        var result = await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            var element = MediaElement(engine);
            var state = BrowserMediaState.Of(realm, element);
            state.SetCurrentTime(realm, 12);
            element.SetAttributeNS(null, "src", source);
            state.Load(realm);
            return $"{state.NetworkState}:{state.Error?.Code}:{state.CurrentTime}";
        });
        result.Should().Be("3:4:0");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CompletionCheckFailureLeavesCoherentStateAndANextLoadCanSucceed()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='data:audio/mpeg,x'></audio>");
        await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            var work = new DomReadWork(_ => throw new InvalidOperationException("completion budget sentinel"), default);
            BrowserMediaState.Of(realm, MediaElement(engine)).Load(realm, work);
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.RunOnLoopAsync(engine =>
        {
            var state = BrowserMediaState.Of(DomRealm.Of(engine), MediaElement(engine));
            return $"{state.NetworkState}:{state.CurrentSrc}:{state.Error?.Code}";
        })).Should().Be("0::");
        page.Errors.Should().ContainSingle();
        await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            BrowserMediaState.Of(realm, MediaElement(engine)).Load(realm);
            return true;
        });
        (await page.WaitForIdleAsync(Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.RunOnLoopAsync(engine => BrowserMediaState.Of(DomRealm.Of(engine), MediaElement(engine)).Error?.Code))
            .Should().Be(4);
        page.Errors.Should().ContainSingle();
    }

    [Test]
    public async Task UnrelatedFallbackAndTrackMutationsDoNotCancelTheSelectedRequest()
    {
        using var handler = new GatedHandler();
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser();
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='https://example.test/audio.mp3'><span></span></audio>");
        await StartLoad(page);
        var request = await handler.NextAsync();
        await page.RunOnLoopAsync(engine =>
        {
            var element = MediaElement(engine);
            element.FirstChild!.AppendChild(element.OwnerDocument!.CreateElement("img"));
            element.AppendChild(element.OwnerDocument!.CreateElement("track"));
            var state = BrowserMediaState.Of(DomRealm.Of(engine), element);
            state.NetworkState.Should().Be(2);
            return true;
        });
        request.Canceled.IsCompleted.Should().BeFalse();
        request.Complete();
        (await page.WaitForAsync("document.getElementById('media').networkState === 3", Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        request.Canceled.IsCompleted.Should().BeFalse();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task RepeatedLoadAndPageDisposalCancelTheActualPendingTransports()
    {
        using var handler = new GatedHandler();
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser();
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='https://example.test/audio.mp3'></audio>");
        await StartLoad(page);
        var first = await handler.NextAsync();
        await StartLoad(page);
        var second = await handler.NextAsync();
        await first.Canceled.WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
        await page.CloseAsync();
        await second.Canceled.WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);
    }

    [Test]
    public async Task AdoptionDropsTheOldDocumentResourceCompletion()
    {
        using var handler = new GatedHandler();
        using var client = new HttpClient(handler);
        await using var browser = new global::Jint.Browser.Browser();
        await using var context = await browser.NewContextAsync(new BrowserContextOptions { HttpClient = client });
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<audio id=media src='https://example.test/audio.mp3'></audio>");
        await StartLoad(page);
        var request = await handler.NextAsync();
        await page.RunOnLoopAsync(engine =>
        {
            var element = MediaElement(engine);
            Document.CreateHtml().AdoptNode(element);
            engine.SetValue("adoptedMedia", DomRealm.Of(engine).WrapNode(element));
            return true;
        });
        request.Complete();
        (await page.WaitForAsync("adoptedMedia.networkState === 0", Jint.Tests.TestBudgets.WedgeCeiling)).Should().BeTrue();
        (await page.EvaluateAsync<string>("adoptedMedia.currentSrc"))!.Should().BeEmpty();
        page.Errors.Should().BeEmpty();
    }

    private static Task<bool> StartLoad(Page page)
        => page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            BrowserMediaState.Of(realm, MediaElement(engine)).Load(realm);
            return true;
        });

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

    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly Channel<RequestGate> _started = Channel.CreateUnbounded<RequestGate>();
        internal Task<RequestGate> NextAsync()
            => _started.Reader.ReadAsync().AsTask().WaitAsync(Jint.Tests.TestBudgets.WedgeCeiling);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var gate = new RequestGate();
            _started.Writer.TryWrite(gate);
            try { return await gate.Response.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { gate.NotifyCanceled(); throw; }
        }
    }

    private sealed class RequestGate
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<HttpResponseMessage> Response => _response.Task;
        internal Task Canceled => _canceled.Task;
        internal void NotifyCanceled() => _canceled.TrySetResult();
        internal void Complete() => _response.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
        });
    }
}
