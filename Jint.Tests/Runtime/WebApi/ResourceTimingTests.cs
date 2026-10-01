#if NET8_0_OR_GREATER
#nullable enable
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.WebApi.Fetch;
using Jint.WebApi.Performance;

namespace Jint.Tests.Runtime.WebApi;

public sealed class ResourceTimingTests
{
    private static readonly TimeSpan CompletionCeiling = TestBudgets.WedgeCeiling;

    [Test]
    public Task RestoringGlobalsDiscardsTransportReportsFromThePreviousGeneration() => DedicatedThread.RunAsync(() =>
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Performance | WebApiFeatures.Fetch));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var timing = ResourceTiming.Start(engine, engine.Realm, "https://same.test/old", "fetch",
            "https://same.test", JsRequest.CredentialsSameOrigin)!;
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        Task.Run(() => timing.Complete(0)).GetAwaiter().GetResult();
        engine.Tasks.ProcessTasks();
        engine.Evaluate("performance.getEntriesByType('resource').length").AsNumber().Should().Be(0);
        AddEntries(engine, 1);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("performance.getEntriesByType('resource').length").AsNumber().Should().Be(1);
    });

    [Test]
    public void RestoringGlobalsResetsPendingBufferFullWork()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Performance | WebApiFeatures.Fetch));
        engine.Execute("performance.setResourceTimingBufferSize(0)");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        AddEntries(engine, 1);
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.Execute("var full = 0; performance.onresourcetimingbufferfull = () => full++");
        AddEntries(engine, 1);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("full").AsNumber().Should().Be(1);
    }

    [TestCase("fetch('/body').then(r => r.text())", "fetch")]
    [TestCase("new Promise(resolve => { const x = new XMLHttpRequest(); x.open('GET', '/body'); x.onload = () => resolve(x.responseText); x.send(); })", "xmlhttprequest")]
    [TestCase("(() => { const x = new XMLHttpRequest(); x.open('GET', '/body', false); x.send(); return x.responseText; })()", "xmlhttprequest")]
    public Task FetchAndXhrReportCompletedBodies(string script, string initiator) => DedicatedThread.RunAsync(() =>
    {
        using var client = new HttpClient(new Handler());
        using var engine = Create(client);
        engine.Evaluate(script).UnwrapIfPromise(CompletionCeiling).AsString().Should().Be("hello");
        engine.Tasks.ProcessTasks();
        engine.Evaluate("performance.getEntriesByType('resource').length").AsNumber().Should().Be(1);
        engine.Evaluate("performance.getEntriesByType('resource')[0].initiatorType").AsString().Should().Be(initiator);
        engine.Evaluate("""
            (() => {
                const e = performance.getEntriesByType('resource')[0], j = e.toJSON();
                return e instanceof PerformanceResourceTiming && e instanceof PerformanceEntry
                    && e.name === 'https://same.test/body' && e.entryType === 'resource'
                    && e.startTime >= 0 && e.fetchStart >= e.startTime
                    && e.responseStart >= e.fetchStart && e.responseEnd >= e.responseStart
                    && e.duration === e.responseEnd - e.startTime
                    && e.responseEnd <= performance.now() && e.nextHopProtocol === 'http/1.1'
                    && e.responseStatus === 200 && e.encodedBodySize === 5 && e.decodedBodySize === 5
                    && e.transferSize === 305 && e.deliveryType === ''
                    && j.name === e.name && j.duration === e.duration && j.responseStatus === 200
                    && Object.keys(e).length === 0 && e.serverTiming.length === 0
                    && Object.getOwnPropertyDescriptor(PerformanceResourceTiming.prototype, 'responseEnd').enumerable;
            })()
            """).AsBoolean().Should().BeTrue();
    });

    [TestCase(null, false)]
    [TestCase("https://same.test", true)]
    [TestCase("*", true)]
    [TestCase("https://wrong.test", false)]
    public Task CrossOriginDetailsRequireTimingAllowOrigin(string? tao, bool allowed) => DedicatedThread.RunAsync(() =>
    {
        using var client = new HttpClient(new Handler(tao));
        using var engine = Create(client);
        engine.Evaluate("fetch('https://other.test/body').then(r => r.text())").UnwrapIfPromise(CompletionCeiling);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("""
            (() => {
                const e = performance.getEntriesByType('resource')[0];
                return e.responseStart > 0 && e.transferSize === 305 && e.responseStatus === 200;
            })()
            """).AsBoolean().Should().Be(allowed);
        if (!allowed)
        {
            engine.Evaluate("""
                (() => {
                    const e = performance.getEntriesByType('resource')[0];
                    return e.responseEnd > 0 && e.duration >= 0
                        && e.responseStart === 0 && e.requestStart === 0 && e.domainLookupStart === 0
                        && e.connectStart === 0 && e.transferSize === 0 && e.encodedBodySize === 0
                        && e.decodedBodySize === 0 && e.responseStatus === 0 && e.nextHopProtocol === '';
                })()
                """).AsBoolean().Should().BeTrue();
        }
    });

    [Test]
    public void ResourceBufferDefaultsTo250AndObserversStillReceiveOverflow()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Performance | WebApiFeatures.Fetch));
        engine.Execute("""
            var full = 0, observed = 0;
            performance.onresourcetimingbufferfull = () => full++;
            new PerformanceObserver(list => observed += list.getEntries().length).observe({type: 'resource'});
            performance.mark('retained');
            """);
        AddEntries(engine, 251);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("performance.getEntriesByType('resource').length + ',' + full + ',' + observed")
            .AsString().Should().Be("250,1,251");
        engine.Execute("""
            var replayed = 0, dropped = -1;
            new PerformanceObserver((list, observer, options) => {
                replayed = list.getEntries().length; dropped = options.droppedEntriesCount;
            }).observe({type: 'resource', buffered: true});
            """);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("replayed + ',' + dropped").AsString().Should().Be("250,1");
        engine.Execute("performance.clearResourceTimings()");
        engine.Evaluate("performance.getEntries().map(e => e.name).join()").AsString().Should().Be("retained");
    }

    [TestCase("performance.clearResourceTimings()", 1)]
    [TestCase("performance.setResourceTimingBufferSize(3)", 3)]
    public void BufferFullHandlerCanRecoverTheSecondaryBuffer(string handler, int expected)
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Performance | WebApiFeatures.Fetch));
        engine.Execute("performance.setResourceTimingBufferSize(2); performance.onresourcetimingbufferfull = () => { " + handler + "; };");
        AddEntries(engine, 3);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("performance.getEntriesByType('resource').length").AsNumber().Should().Be(expected);
    }

    [Test]
    public void ResourceInterfacesBrandCheckAndRequireBufferSize()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.Performance | WebApiFeatures.Fetch));
        engine.Evaluate("""
            [
                () => new PerformanceResourceTiming(),
                () => PerformanceResourceTiming(),
                () => PerformanceResourceTiming.prototype.toJSON.call({}),
                () => Object.getOwnPropertyDescriptor(PerformanceResourceTiming.prototype, 'responseEnd').get.call({}),
                () => performance.setResourceTimingBufferSize(),
                () => Performance.prototype.clearResourceTimings.call({})
            ].every(f => { try { f(); return false; } catch(e) { return e instanceof TypeError; } })
            """).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DisabledPerformanceDoesNotInstallTimingInterfaces()
    {
        using var engine = new Engine(o => o.UseWebApis(WebApiFeatures.None));
        engine.Evaluate("typeof PerformanceResourceTiming + ',' + typeof PerformanceNavigationTiming")
            .AsString().Should().Be("undefined,undefined");
    }

    private static void AddEntries(Engine engine, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var info = new ResourceTimingInfo("https://same.test/" + i, "fetch", i, i, i + 1, i + 2,
                "http/1.1", 5, 5, 200, true, false, "text/plain");
            ResourceTiming.Add(engine, engine.Realm, in info);
        }
    }

    private static Engine Create(HttpClient client) => new(o =>
    {
        o.UseWebApis(WebApiFeatures.Performance | WebApiFeatures.Fetch | WebApiFeatures.XmlHttpRequest);
        o.WebApi.Fetch.HttpClient = client;
        o.WebApi.Fetch.BaseUrl = new Uri("https://same.test/");
        o.WebApi.Fetch.Origin = "https://same.test";
    });

    private sealed class Handler(string? tao = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello"),
                Version = HttpVersion.Version11,
            };
            if (tao is not null) response.Headers.TryAddWithoutValidation("Timing-Allow-Origin", tao);
            return Task.FromResult(response);
        }
    }
}
#endif
