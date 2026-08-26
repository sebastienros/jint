#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// What a host writes in <c>Engine.WebApi.Enable</c>'s configuration callback reaches the engine it
/// called it on, and no other engine in the process.
/// </summary>
/// <remarks>
/// <para>
/// That callback is the one write to an engine's <see cref="Options"/> after construction, and the instance
/// behind it is shared: with every engine built from it, and — for <c>new Engine()</c>, which is what a host
/// writes when it has no options to give — with one instance Jint keeps process-wide. A per-tenant HTTP
/// client or URL policy set through the callback therefore used to become every default-built engine's, in
/// both directions in time.
/// </para>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so every assertion here is one an embedder could write:
/// the leak is observed through what a <i>second</i> engine's script does, not by reading the options back.
/// Nothing opens a socket — each engine that fetches is given a handler that answers from memory.
/// </para>
/// </remarks>
public class HostWebApiOptionsIsolationTests
{
    /// <summary>Answers every request from memory, recording what it was asked for.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Urls)
            {
                Urls.Add(request.RequestUri!.ToString());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }

    /// <summary>Keeps every line a console wrote, so a leak shows up as another engine's output arriving.</summary>
    private sealed class RecordingSink : ConsoleSink
    {
        internal List<string> Lines { get; } = new();

        public override void Write(ConsoleLogLevel level, string message)
        {
            lock (Lines)
            {
                Lines.Add(message);
            }
        }
    }

    /// <summary>
    /// The sample from the issue: two engines built by <c>new Engine()</c>, one of which configures a network
    /// policy through the live door. The other must not be governed by it.
    /// </summary>
    /// <remarks>
    /// The second engine is constructed <em>before</em> the first is configured and enables its own web APIs
    /// <em>after</em>, so the assertion covers both directions in time across the shared instance.
    /// </remarks>
    [Test]
    public void APolicyConfiguredThroughTheLiveDoorDoesNotGovernAnotherDefaultBuiltEngine()
    {
        var tenantHandler = new RecordingHandler();
        var otherHandler = new RecordingHandler();

        var tenant = new Engine();
        var other = new Engine();

        tenant.WebApi.Enable(WebApiFeatures.Fetch, webApi =>
        {
            webApi.Fetch.HttpClient = new HttpClient(tenantHandler);
            webApi.Fetch.UrlFilter = uri => uri.Host.Equals("tenant.example", StringComparison.OrdinalIgnoreCase);
        });

        other.WebApi.Enable(WebApiFeatures.Fetch, webApi => webApi.Fetch.HttpClient = new HttpClient(otherHandler));

        // The other engine never asked for a URL policy, so it has none.
        other.Evaluate("fetch('https://elsewhere.test/x').then(r => String(r.status), e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("200");

        otherHandler.Urls.Should().ContainSingle().Which.Should().Be("https://elsewhere.test/x");

        // ... and its request never went through the tenant's client.
        tenantHandler.Urls.Should().BeEmpty();

        // The engine that did ask for the policy still has it.
        tenant.Evaluate("fetch('https://elsewhere.test/x').then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");

        tenant.Evaluate("fetch('https://tenant.example/x').then(r => r.status)")
            .UnwrapIfPromise().AsNumber().Should().Be(200);

        tenantHandler.Urls.Should().ContainSingle().Which.Should().Be("https://tenant.example/x");
    }

    /// <summary>
    /// The same, for two engines a host deliberately built from one <see cref="Options"/> instance — the
    /// pooled shape, where the sharing is written down and the leak is no more wanted for it.
    /// </summary>
    [Test]
    public void APolicyConfiguredThroughTheLiveDoorDoesNotGovernASiblingBuiltFromTheSameOptions()
    {
        var tenantHandler = new RecordingHandler();
        var otherHandler = new RecordingHandler();

        var options = new Options();
        var tenant = new Engine(options);
        var other = new Engine(options);

        tenant.WebApi.Enable(WebApiFeatures.Fetch, webApi =>
        {
            webApi.Fetch.HttpClient = new HttpClient(tenantHandler);
            webApi.Fetch.UrlFilter = uri => uri.Host.Equals("tenant.example", StringComparison.OrdinalIgnoreCase);
        });

        other.WebApi.Enable(WebApiFeatures.Fetch, webApi => webApi.Fetch.HttpClient = new HttpClient(otherHandler));

        other.Evaluate("fetch('https://elsewhere.test/x').then(r => String(r.status), e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("200");

        tenantHandler.Urls.Should().BeEmpty();
    }

    /// <summary>
    /// A setting read live rather than at enable time leaks the same way, and an engine that passed no
    /// callback at all is enough to show it: its <c>console</c> output must not reach another engine's sink.
    /// </summary>
    [Test]
    public void AConsoleSinkConfiguredThroughTheLiveDoorDoesNotCaptureAnotherEnginesOutput()
    {
        var sink = new RecordingSink();

        var tenant = new Engine();
        tenant.WebApi.Enable(WebApiFeatures.Console, webApi => webApi.Console.Sink = sink);

        var other = new Engine();
        other.WebApi.Enable(WebApiFeatures.Console);
        other.Execute("console.log('the other engine');");

        sink.Lines.Should().BeEmpty();

        tenant.Execute("console.log('the tenant');");
        sink.Lines.Should().ContainSingle().Which.Should().Be("the tenant");
    }

    /// <summary>
    /// The engine's copy starts as a copy: what the host configured on the options up front is still in force
    /// after the live door has written one setting of its own.
    /// </summary>
    [Test]
    public void WhatTheHostConfiguredUpFrontSurvivesALiveConfiguration()
    {
        var handler = new RecordingHandler();

        var options = new Options();
        options.WebApi.Fetch.UrlFilter = uri => uri.Host.Equals("allowed.example", StringComparison.OrdinalIgnoreCase);

        var engine = new Engine(options);
        engine.WebApi.Enable(WebApiFeatures.Fetch, webApi => webApi.Fetch.HttpClient = new HttpClient(handler));

        engine.Evaluate("fetch('https://denied.test/x').then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");

        engine.Evaluate("fetch('https://allowed.example/x').then(r => r.status)")
            .UnwrapIfPromise().AsNumber().Should().Be(200);

        handler.Urls.Should().ContainSingle().Which.Should().Be("https://allowed.example/x");
    }

    /// <summary>
    /// Two live configurations on the same engine accumulate, so the copy is the engine's from the first call
    /// onwards rather than being taken afresh from the shared instance each time.
    /// </summary>
    [Test]
    public void ASecondLiveConfigurationSeesWhatTheFirstOneWrote()
    {
        var handler = new RecordingHandler();

        var engine = new Engine();
        engine.WebApi.Enable(WebApiFeatures.Fetch, webApi => webApi.Fetch.HttpClient = new HttpClient(handler));
        engine.WebApi.Enable(
            WebApiFeatures.EventSource,
            webApi => webApi.Fetch.UrlFilter = uri => uri.Host.Equals("allowed.example", StringComparison.OrdinalIgnoreCase));

        engine.Evaluate("fetch('https://denied.test/x').then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");

        // The client the first call supplied is still the one in use.
        engine.Evaluate("fetch('https://allowed.example/x').then(r => r.status)")
            .UnwrapIfPromise().AsNumber().Should().Be(200);

        handler.Urls.Should().ContainSingle().Which.Should().Be("https://allowed.example/x");
    }
}
#endif
