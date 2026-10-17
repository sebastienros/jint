#if NET8_0_OR_GREATER
using System.Threading;
using Jint;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>data:</c> URLs seen from outside the assembly: what a host's fetch policy does and does not decide about
/// them, and the one bound that still applies.
/// </summary>
/// <remarks>
/// The engines here name no <c>HttpClient</c>, and the ones that narrow the policy narrow it to nothing at
/// all. A <c>data:</c> URL is answered anyway, because the bytes are in the URL and scheme fetch never gets as
/// far as a transport — which is the behaviour a host that narrowed <c>AllowedSchemes</c> or set a
/// <c>UrlFilter</c> now sees for <c>data:</c>, as it always did for <c>blob:</c>.
/// </remarks>
public class WebApiDataUrlTests
{
    private static void Pump(Engine engine)
    {
        for (var i = 0; i < 8; i++)
        {
            engine.Tasks.ProcessTasks();
        }
    }

    /// <summary>
    /// The round trip an embedder writes, on an engine whose scheme list and URL filter refuse every request
    /// there is: the <c>data:</c> URL is still answered, and the filter is never asked about it.
    /// </summary>
    [Test]
    public void ANarrowedPolicyStillAnswersADataUrl()
    {
        var filterCalls = 0;
        var engine = new Engine(options => options.UseFetch(fetch =>
        {
            fetch.AllowedSchemes.Clear();
            fetch.UrlFilter = _ =>
            {
                Interlocked.Increment(ref filterCalls);
                return false;
            };
        }));

        engine.Execute("""
            var result = null;
            fetch('data:text/plain;base64,SGVsbG8=').then(async response => {
              result = [response.status, response.statusText, response.headers.get('Content-Type'), await response.text()].join('|');
            });
            """);

        Pump(engine);

        engine.Evaluate("result").AsString().Should().Be("200|OK|text/plain|Hello");
        filterCalls.Should().Be(0);
    }

    /// <summary>
    /// <c>MaxResponseBytes</c> is the bound a <c>data:</c> URL answers to, on its decoded body: a script cannot
    /// escape the host's size ceiling by inlining what it would otherwise have downloaded.
    /// </summary>
    [Test]
    public void MaxResponseBytesBoundsADataUrlsBody()
    {
        var engine = new Engine(options => options.UseFetch(fetch => fetch.MaxResponseBytes = 4));

        engine.Execute("""
            var outcomes = [];
            for (const url of ['data:,four', 'data:,fives']) {
              fetch(url).then(r => r.text()).then(text => outcomes.push(text), e => outcomes.push(e.constructor.name));
            }
            """);

        Pump(engine);
        engine.Evaluate("outcomes.slice().sort().join(',')").AsString().Should().Be("TypeError,four");
    }

    /// <summary>
    /// <c>XMLHttpRequest</c> takes the same path, so a <c>data:</c> URL is readable on an engine that has the
    /// interface and no network grant of any kind.
    /// </summary>
    [Test]
    public void XmlHttpRequestReadsADataUrlWithoutANetworkGrant()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Default | WebApiFeatures.XmlHttpRequest));

        engine.Execute("""
            var result = null;
            var xhr = new XMLHttpRequest();
            xhr.open('GET', 'data:application/json,{"a":1}');
            xhr.responseType = 'json';
            xhr.onload = function () {
              result = [xhr.status, xhr.getResponseHeader('Content-Type'), xhr.response.a].join('|');
            };
            xhr.send();
            """);

        Pump(engine);
        engine.Evaluate("result").AsString().Should().Be("200|application/json|1");
    }

    /// <summary>
    /// <c>EventSource</c> takes the same arm: on an engine whose scheme list and URL filter refuse every
    /// request, a <c>text/event-stream</c> <c>data:</c> URL is announced, dispatches its events and — because
    /// the end of a body reestablishes the connection — replays every reconnection time until it is closed.
    /// </summary>
    [Test]
    public void EventSourceReplaysADataUrlUnderANarrowedPolicy()
    {
        var filterCalls = 0;
        var engine = new Engine(options => options.UseEventSource(network =>
        {
            network.AllowedSchemes.Clear();
            network.UrlFilter = _ =>
            {
                Interlocked.Increment(ref filterCalls);
                return false;
            };
        }));

        engine.Execute("""
            var log = [];
            var es = new EventSource('data:text/event-stream,retry:%2010%0Adata:%20hi%0A%0A');
            es.onopen = () => log.push('open');
            es.onmessage = e => log.push(e.data + '@' + e.origin);
            es.onerror = () => { log.push('error' + es.readyState); if (log.length >= 6) { es.close(); } };
            """);

        // The replay waits a real ten milliseconds on the engine's timer queue, which only a pump can run.
        var deadline = DateTime.UtcNow + TestBudgets.WedgeCeiling;
        while (engine.Evaluate("es.readyState").AsNumber() != 2 && DateTime.UtcNow < deadline)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(1);
        }

        engine.Evaluate("log.join('|')").AsString().Should().Be("open|hi@null|error0|open|hi@null|error0");
        filterCalls.Should().Be(0);
    }
}
#endif
