#if NET8_0_OR_GREATER
#nullable enable
#pragma warning disable JINT0002 // the fetch observer is a preview surface; this suite is what pins its shape

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.WebApi.Fetch;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The four seams a browser built on Jint needs from <c>fetch</c>: an API base URL, a referrer and an
/// origin, a cookie jar, and an observer the request passes through.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party. The
/// transport is stubbed through <c>Options.WebApi.Fetch.HttpClient</c>, which is the door a host uses for a
/// <c>DelegatingHandler</c> anyway; the one test that needs a real socket runs its own loopback listener,
/// because keying a cookie on an IP-literal host and a port is exactly what a <see cref="CookieContainer"/>
/// could get wrong.
/// </para>
/// <para>
/// <b>Nothing here waits on a body from a thread-pool worker</b> except through
/// <c>DedicatedThread.RunAsync</c>: a response body is pumped from a <c>Task.Run</c> loop, and blocking a
/// pool worker to wait for one is the resource inversion that helper exists for.
/// </para>
/// </remarks>
public class WebApiFetchDocumentTests
{
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A handler that records every request it saw — URL, method and headers — and answers whatever the
    /// responder says.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal List<(string Url, string Method, Dictionary<string, string> Headers)> Requests { get; } = new();

        internal Func<string, HttpResponseMessage> Responder { get; init; } =
            static _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

        internal string? HeaderOf(int index, string name)
            => Requests[index].Headers.TryGetValue(name, out var value) ? value : null;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            if (request.Content is { } content)
            {
                foreach (var header in content.Headers)
                {
                    headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            lock (Requests)
            {
                Requests.Add((request.RequestUri!.ToString(), request.Method.Method, headers));
            }

            return Task.FromResult(Responder(request.RequestUri!.ToString()));
        }
    }

    private static Engine WebEngine(HttpMessageHandler handler, Action<Options.FetchOptions>? configure = null)
        => new(options => options.UseWebApis(WebApiFeatures.Timers).UseFetch(fetch =>
        {
            fetch.HttpClient = new HttpClient(handler);
            configure?.Invoke(fetch);
        }));

    private static HttpResponseMessage Redirect(string location, HttpStatusCode status = HttpStatusCode.Found)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.TryAddWithoutValidation("location", location);
        return response;
    }

    // ---- Fetch.BaseUrl ----

    [Test]
    public void WithoutABaseUrlARelativeUrlIsStillATypeError()
    {
        var engine = WebEngine(new RecordingHandler());

        engine.Evaluate("(() => { try { new Request('/api'); return 'built'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        engine.Evaluate("fetch('/api').then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("TypeError");
    }

    [Test]
    public void ABaseUrlResolvesARelativeRequestAndFetch()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler, f => f.BaseUrl = new Uri("https://example.org/app/page.html?q=1"));

        engine.Evaluate("new Request('/api').url").AsString().Should().Be("https://example.org/api");
        engine.Evaluate("new Request('other').url").AsString().Should().Be("https://example.org/app/other");
        engine.Evaluate("new Request('').url").AsString().Should().Be("https://example.org/app/page.html?q=1");
        engine.Evaluate("new Request('//cdn.example.net/x').url").AsString().Should().Be("https://cdn.example.net/x");

        engine.Evaluate("fetch('../up').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling).AsNumber().Should().Be(200);
        handler.Requests[0].Url.Should().Be("https://example.org/up");
    }

    [Test]
    public void ABaseUrlLeavesAnAbsoluteUrlAlone()
    {
        var engine = WebEngine(new RecordingHandler(), f => f.BaseUrl = new Uri("https://example.org/app/"));

        engine.Evaluate("new Request('https://other.test/x').url").AsString().Should().Be("https://other.test/x");
    }

    [Test]
    public void ARelativeBaseUrlIsRefusedWhereItIsWritten()
    {
        var options = new Options();

        var caught = Caught.Exception(() => options.WebApi.Fetch.BaseUrl = new Uri("/app/", UriKind.Relative));
        caught.Should().BeOfType<ArgumentException>();

        Caught.Exception(() => options.WebApi.Fetch.Referrer = new Uri("page.html", UriKind.Relative))
            .Should().BeOfType<ArgumentException>();
    }

    // ---- Fetch.Referrer and Fetch.ReferrerPolicy ----

    [Test]
    public void WithoutAConfiguredReferrerNoRefererHeaderIsSent()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler);

        engine.Evaluate("fetch('https://example.org/a').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(0, "Referer").Should().BeNull();
    }

    [TestCase(ReferrerPolicy.NoReferrer, "https://a.test/p?q=1", "https://a.test/x", null)]
    [TestCase(ReferrerPolicy.NoReferrer, "https://a.test/p?q=1", "http://a.test/x", null)]
    [TestCase(ReferrerPolicy.UnsafeUrl, "https://a.test/p?q=1", "http://b.test/x", "https://a.test/p?q=1")]
    [TestCase(ReferrerPolicy.Origin, "https://a.test/p?q=1", "https://a.test/x", "https://a.test/")]
    [TestCase(ReferrerPolicy.Origin, "https://a.test/p?q=1", "http://b.test/x", "https://a.test/")]
    [TestCase(ReferrerPolicy.SameOrigin, "https://a.test/p?q=1", "https://a.test/x", "https://a.test/p?q=1")]
    [TestCase(ReferrerPolicy.SameOrigin, "https://a.test/p?q=1", "https://b.test/x", null)]
    [TestCase(ReferrerPolicy.OriginWhenCrossOrigin, "https://a.test/p?q=1", "https://a.test/x", "https://a.test/p?q=1")]
    [TestCase(ReferrerPolicy.OriginWhenCrossOrigin, "https://a.test/p?q=1", "https://b.test/x", "https://a.test/")]
    [TestCase(ReferrerPolicy.OriginWhenCrossOrigin, "https://a.test/p?q=1", "http://b.test/x", "https://a.test/")]
    [TestCase(ReferrerPolicy.NoReferrerWhenDowngrade, "https://a.test/p?q=1", "https://b.test/x", "https://a.test/p?q=1")]
    [TestCase(ReferrerPolicy.NoReferrerWhenDowngrade, "https://a.test/p?q=1", "http://b.test/x", null)]
    [TestCase(ReferrerPolicy.NoReferrerWhenDowngrade, "http://a.test/p?q=1", "https://b.test/x", "http://a.test/p?q=1")]
    [TestCase(ReferrerPolicy.StrictOrigin, "https://a.test/p?q=1", "https://b.test/x", "https://a.test/")]
    [TestCase(ReferrerPolicy.StrictOrigin, "https://a.test/p?q=1", "http://b.test/x", null)]
    [TestCase(ReferrerPolicy.StrictOriginWhenCrossOrigin, "https://a.test/p?q=1", "https://a.test/x", "https://a.test/p?q=1")]
    [TestCase(ReferrerPolicy.StrictOriginWhenCrossOrigin, "https://a.test/p?q=1", "https://b.test/x", "https://a.test/")]
    [TestCase(ReferrerPolicy.StrictOriginWhenCrossOrigin, "https://a.test/p?q=1", "http://b.test/x", null)]
    [TestCase(ReferrerPolicy.StrictOriginWhenCrossOrigin, "http://a.test/p?q=1", "https://b.test/x", "http://a.test/")]
    public void TheReferrerPolicyDecidesHowMuchOfTheReferrerTravels(ReferrerPolicy policy, string referrer, string target, string? expected)
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler, f =>
        {
            f.Referrer = new Uri(referrer);
            f.ReferrerPolicy = policy;
        });

        engine.Evaluate($"fetch('{target}').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(0, "Referer").Should().Be(expected);
    }

    [Test]
    public void TheReferrerLosesItsCredentialsAndItsFragment()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler, f =>
        {
            f.Referrer = new Uri("https://user:secret@a.test/p?q=1#frag");
            f.ReferrerPolicy = ReferrerPolicy.UnsafeUrl;
        });

        engine.Evaluate("fetch('https://a.test/x').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(0, "Referer").Should().Be("https://a.test/p?q=1");
    }

    [Test]
    public void ARequestOverridesTheReferrerAndThePolicy()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler, f =>
        {
            f.BaseUrl = new Uri("https://a.test/app/");
            f.Referrer = new Uri("https://a.test/app/index.html");
            f.ReferrerPolicy = ReferrerPolicy.NoReferrer;
        });

        // The attributes read back what the request asked for, whatever the host's own settings are.
        engine.Evaluate("new Request('https://a.test/x').referrer").AsString().Should().Be("about:client");
        engine.Evaluate("new Request('https://a.test/x').referrerPolicy").AsString().Should().BeEmpty();
        engine.Evaluate("new Request('https://a.test/x', { referrer: '' }).referrer").AsString().Should().BeEmpty();
        engine.Evaluate("new Request('https://a.test/x', { referrer: 'sub/page' }).referrer").AsString().Should().Be("https://a.test/app/sub/page");
        engine.Evaluate("new Request('https://a.test/x', { referrerPolicy: 'origin' }).referrerPolicy").AsString().Should().Be("origin");

        // A referrer that is not this engine's own origin becomes "client", exactly as a browser refuses to
        // let a document claim a referrer it could not have been.
        engine.Evaluate("new Request('https://a.test/x', { referrer: 'https://elsewhere.test/p' }).referrer")
            .AsString().Should().Be("about:client");

        engine.Evaluate("fetch('https://a.test/x', { referrer: 'sub/page', referrerPolicy: 'unsafe-url' }).then(r => r.status)")
            .UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(0, "Referer").Should().Be("https://a.test/app/sub/page");
    }

    [Test]
    public void AnInvalidReferrerPolicyIsATypeError()
    {
        var engine = WebEngine(new RecordingHandler());

        engine.Evaluate("(() => { try { new Request('https://a.test/', { referrerPolicy: 'nonsense' }); return 'built'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        engine.Evaluate("(() => { try { new Request('https://a.test/', { credentials: 'nonsense' }); return 'built'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    [Test]
    public void TheRefererIsNarrowedAgainOnEveryRedirectHop()
    {
        var handler = new RecordingHandler
        {
            Responder = url => url.StartsWith("https://a.test/", StringComparison.Ordinal)
                ? Redirect("https://b.test/x")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") },
        };

        var engine = WebEngine(handler, f =>
        {
            f.Referrer = new Uri("https://a.test/p?q=1");
            f.ReferrerPolicy = ReferrerPolicy.StrictOriginWhenCrossOrigin;
        });

        engine.Evaluate("fetch('https://a.test/start').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling).AsNumber().Should().Be(200);

        // Same origin on the first hop, so the whole URL; cross origin on the second, so only the origin.
        handler.HeaderOf(0, "Referer").Should().Be("https://a.test/p?q=1");
        handler.HeaderOf(1, "Referer").Should().Be("https://a.test/");
    }

    [Test]
    public void AScriptSetRefererIsNotDuplicated()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler, f =>
        {
            f.Referrer = new Uri("https://a.test/p");
            f.ReferrerPolicy = ReferrerPolicy.UnsafeUrl;
        });

        engine.Evaluate("fetch('https://a.test/x', { headers: { referer: 'https://written.test/' } }).then(r => r.status)")
            .UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(0, "Referer").Should().Be("https://written.test/");
    }

    // ---- Fetch.Origin ----

    [Test]
    public void TheOriginHeaderFollowsTheMethodAndThePolicy()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler, f => f.Origin = "https://a.test/ignored/path");

        engine.Evaluate("fetch('https://b.test/x').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(0, "Origin").Should().BeNull("a GET carries no Origin outside CORS");

        engine.Evaluate("fetch('https://b.test/x', { method: 'POST', body: 'hi' }).then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(1, "Origin").Should().Be("https://a.test");

        // https://fetch.spec.whatwg.org/#append-a-request-origin-header step 3.1: a secure origin talking to
        // an insecure URL sends the opaque one under the three downgrade-aware policies.
        engine.Evaluate("fetch('http://b.test/x', { method: 'POST', body: 'hi' }).then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(2, "Origin").Should().Be("null");
    }

    [Test]
    public void WithoutAConfiguredOriginNoOriginHeaderIsSent()
    {
        var handler = new RecordingHandler();
        var engine = WebEngine(handler);

        engine.Evaluate("fetch('https://b.test/x', { method: 'POST', body: 'hi' }).then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(0, "Origin").Should().BeNull();
    }

    // ---- Fetch.CookieJar ----

    [Test]
    public void WithoutAJarNoCookieIsEverSentOrStored()
    {
        var handler = new RecordingHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.TryAddWithoutValidation("set-cookie", "a=1; Path=/");
                return response;
            },
        };

        var engine = WebEngine(handler);

        engine.Evaluate("fetch('https://a.test/1').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        engine.Evaluate("fetch('https://a.test/2').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        handler.HeaderOf(1, "Cookie").Should().BeNull();
    }

    [Test]
    public void AJarStoresOnOneResponseAndSendsOnTheNext()
    {
        var handler = new RecordingHandler
        {
            Responder = url =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                if (url.EndsWith("/login", StringComparison.Ordinal))
                {
                    response.Headers.TryAddWithoutValidation("set-cookie", "session=abc; Path=/; HttpOnly");
                    response.Headers.TryAddWithoutValidation("set-cookie", "theme=dark; Path=/");
                }

                return response;
            },
        };

        var jar = new CookieContainerCookieJar();
        var engine = WebEngine(handler, f =>
        {
            f.CookieJar = jar;
            f.Origin = "https://a.test";
        });

        engine.Evaluate("fetch('https://a.test/login').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(0, "Cookie").Should().BeNull();
        jar.Container.Count.Should().Be(2, "the login response's two Set-Cookie headers were stored");

        engine.Evaluate("fetch('https://a.test/next').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        // HttpOnly is about document.cookie, never about the wire: fetch still sends it.
        handler.HeaderOf(1, "Cookie").Should().Be("session=abc; theme=dark");
        jar.Container.Count.Should().Be(2);
    }

    [Test]
    public void TheCredentialsModeDecidesWhetherTheJarIsConsulted()
    {
        var handler = new RecordingHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.TryAddWithoutValidation("set-cookie", "a=1; Path=/");
                return response;
            },
        };

        var jar = new CookieContainerCookieJar();
        var engine = WebEngine(handler, f =>
        {
            f.CookieJar = jar;
            f.Origin = "https://a.test";
        });

        // omit: nothing is stored, so the second request has nothing to send either.
        engine.Evaluate("fetch('https://a.test/1', { credentials: 'omit' }).then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        jar.Container.Count.Should().Be(0);

        // same-origin, and the hop is this engine's own origin.
        engine.Evaluate("fetch('https://a.test/2').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        jar.Container.Count.Should().Be(1);
        engine.Evaluate("fetch('https://a.test/3').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(2, "Cookie").Should().Be("a=1");

        // same-origin, and the hop is not: neither sent nor stored.
        engine.Evaluate("fetch('https://b.test/1').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(3, "Cookie").Should().BeNull();
        jar.Container.Count.Should().Be(1);

        // include: both, wherever the hop goes.
        engine.Evaluate("fetch('https://b.test/2', { credentials: 'include' }).then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        jar.Container.Count.Should().Be(2);
        engine.Evaluate("fetch('https://b.test/3', { credentials: 'include' }).then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);
        handler.HeaderOf(5, "Cookie").Should().Be("a=1");

        engine.Evaluate("new Request('https://a.test/').credentials").AsString().Should().Be("same-origin");
        engine.Evaluate("new Request('https://a.test/', { credentials: 'include' }).credentials").AsString().Should().Be("include");
    }

    [Test]
    public void WithNoOriginAtAllASameOriginRequestCarriesNoCookies()
    {
        var handler = new RecordingHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
                response.Headers.TryAddWithoutValidation("set-cookie", "a=1; Path=/");
                return response;
            },
        };

        var jar = new CookieContainerCookieJar();
        var engine = WebEngine(handler, f => f.CookieJar = jar);

        engine.Evaluate("fetch('https://a.test/1').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling);

        jar.Container.Count.Should().Be(0);
    }

    [Test]
    public void ACrossHostRedirectRecomputesTheCookieForTheNewHost()
    {
        var handler = new RecordingHandler
        {
            Responder = url =>
            {
                var response = url.StartsWith("https://a.test/", StringComparison.Ordinal)
                    ? Redirect("https://b.test/landing")
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

                response.Headers.TryAddWithoutValidation(
                    "set-cookie",
                    url.StartsWith("https://a.test/", StringComparison.Ordinal) ? "from=a; Path=/" : "from=b; Path=/");

                return response;
            },
        };

        var jar = new CookieContainerCookieJar();
        jar.Container.Add(new Uri("https://a.test/"), new Cookie("who", "a") { Path = "/" });
        jar.Container.Add(new Uri("https://b.test/"), new Cookie("who", "b") { Path = "/" });

        var engine = WebEngine(handler, f => f.CookieJar = jar);

        engine.Evaluate("fetch('https://a.test/start', { credentials: 'include' }).then(r => r.status)")
            .UnwrapIfPromise(TransportSignalCeiling).AsNumber().Should().Be(200);

        // Each hop was asked for its own host's cookies, rather than carrying the first hop's forward — the
        // rule that used to be "strip Cookie on a cross-origin redirect" and is now a recomputation.
        handler.HeaderOf(0, "Cookie").Should().Be("who=a");
        handler.HeaderOf(1, "Cookie").Should().Be("who=b");

        // And the redirect's own Set-Cookie was stored, which is what a login flow depends on.
        jar.GetCookieHeader(new Uri("https://a.test/")).Should().Contain("from=a");
        jar.GetCookieHeader(new Uri("https://b.test/")).Should().Contain("from=b");
    }

    [Test]
    public Task CookiesRoundTripOverALoopbackSocket() => DedicatedThread.RunAsync(() =>
    {
        using var server = new LoopbackServer();
        var jar = new CookieContainerCookieJar();

        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Timers).UseFetch(fetch =>
        {
            fetch.CookieJar = jar;
            fetch.BaseUrl = new Uri(server.Origin + "/");
            fetch.UrlFilter = uri => uri.Port == server.Port;
        }));

        // A relative URL, resolved against the base URL, over a real socket to an IP-literal host and a
        // non-default port — the two things a CookieContainer key has to get right.
        engine.Evaluate("fetch('/login').then(r => r.text())").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("ok");
        engine.Evaluate("fetch('/next').then(r => r.text())").UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("ok");

        server.Received.Should().HaveCount(2);
        server.Received[0].Should().NotContain("Cookie:");
        server.Received[1].Should().Contain("Cookie: sid=42");
    });

    // ---- Fetch.Observer ----

    private sealed class RecordingObserver : FetchObserver
    {
        internal List<string> Events { get; } = new();

        internal Func<ObservedFetchRequest, FetchInterception?>? OnRequestHandler { get; init; }

        public override int RequestBodyPreviewBytes => 16;

        public override ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
        {
            lock (Events)
            {
                Events.Add($"request {request.Id} {request.Method} {request.Url} redirects={request.RedirectCount} after={request.RedirectResponse?.Status.ToString() ?? "-"} initiator={request.Initiator}");
            }

            return new ValueTask<FetchInterception?>(OnRequestHandler?.Invoke(request));
        }

        public override void OnResponse(ObservedFetchResponse response)
        {
            lock (Events)
            {
                Events.Add($"response {response.Id} {response.Status} redirect={response.IsRedirect} intercepted={response.FromInterception}");
            }
        }

        public override void OnData(FetchRequestId id, ReadOnlySpan<byte> chunk)
        {
            var length = chunk.Length;
            lock (Events)
            {
                Events.Add($"data {id} {length}");
            }
        }

        public override void OnCompleted(FetchRequestId id, long bodyLength)
        {
            lock (Events)
            {
                Events.Add($"completed {id} {bodyLength}");
            }
        }

        public override void OnFailed(FetchRequestId id, string reason, Exception? exception)
        {
            lock (Events)
            {
                Events.Add($"failed {id} {reason}");
            }
        }
    }

    [Test]
    public Task TheObserverSeesEveryHopInOrder() => DedicatedThread.RunAsync(() =>
    {
        var handler = new RecordingHandler
        {
            Responder = url => url.EndsWith("/start", StringComparison.Ordinal)
                ? Redirect("https://a.test/landing")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("hello") },
        };

        var observer = new RecordingObserver();
        var engine = WebEngine(handler, f => f.Observer = observer);

        engine.Evaluate("fetch('https://a.test/start').then(r => r.text())")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("hello");

        var id = observer.Events[0].Split(' ')[1];
        observer.Events.Should().Equal(
            $"request {id} GET https://a.test/start redirects=0 after=- initiator=Script",
            $"response {id} 302 redirect=True intercepted=False",
            $"request {id} GET https://a.test/landing redirects=1 after=302 initiator=Script",
            $"response {id} 200 redirect=False intercepted=False",
            $"data {id} 5",
            $"completed {id} 5");
    });

    [Test]
    public Task FulfillingShortCircuitsTheNetwork() => DedicatedThread.RunAsync(() =>
    {
        var handler = new RecordingHandler();
        var observer = new RecordingObserver
        {
            OnRequestHandler = _ => FetchInterception.Fulfill(
                418,
                [new FetchHeader("content-type", "text/plain"), new FetchHeader("x-from", "observer")],
                "brewed"u8.ToArray(),
                "I am a teapot"),
        };

        var engine = WebEngine(handler, f => f.Observer = observer);

        engine.Evaluate("fetch('https://a.test/x').then(r => r.status + '|' + r.statusText + '|' + r.headers.get('x-from') + '|' + r.headers.get('content-type'))")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("418|I am a teapot|observer|text/plain");

        engine.Evaluate("fetch('https://a.test/x').then(r => r.text())")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("brewed");

        handler.Requests.Should().BeEmpty();
        observer.Events.Should().Contain(e => e.Contains("intercepted=True", StringComparison.Ordinal));
    });

    [Test]
    public void FailingProducesTheSameTypeErrorAsTheNetworkDoes()
    {
        var handler = new RecordingHandler();
        var observer = new RecordingObserver { OnRequestHandler = _ => FetchInterception.Fail("blocked by policy") };
        var engine = WebEngine(handler, f => f.Observer = observer);

        engine.Evaluate("fetch('https://a.test/x').then(() => 'resolved', e => e.constructor.name + ': ' + e.message)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("TypeError: Failed to fetch");

        handler.Requests.Should().BeEmpty();
        observer.Events.Should().Contain(e => e.EndsWith("blocked by policy", StringComparison.Ordinal));
    }

    [Test]
    public void ContinuingRewritesTheHopItAnswers()
    {
        var handler = new RecordingHandler();
        var observer = new RecordingObserver
        {
            OnRequestHandler = _ => FetchInterception.Continue(
                url: new Uri("https://rewritten.test/y"),
                method: "PUT",
                headers: [new FetchHeader("x-added", "1")],
                body: "payload"u8.ToArray()),
        };

        var engine = WebEngine(handler, f => f.Observer = observer);

        engine.Evaluate("fetch('https://a.test/x').then(r => r.status)").UnwrapIfPromise(TransportSignalCeiling).AsNumber().Should().Be(200);

        handler.Requests[0].Url.Should().Be("https://rewritten.test/y");
        handler.Requests[0].Method.Should().Be("PUT");
        handler.HeaderOf(0, "x-added").Should().Be("1");
    }

    [Test]
    public void ARewrittenUrlIsStillHeldToTheHostFilter()
    {
        var handler = new RecordingHandler();
        var observer = new RecordingObserver
        {
            OnRequestHandler = _ => FetchInterception.Continue(url: new Uri("http://169.254.169.254/latest/meta-data/")),
        };

        var engine = WebEngine(handler, f =>
        {
            f.Observer = observer;
            f.UrlFilter = uri => uri.Host.EndsWith(".test", StringComparison.Ordinal);
        });

        engine.Evaluate("fetch('https://a.test/x').then(() => 'resolved', e => e.message)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("Failed to fetch");

        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void ARefusalBeforeTheTransportStillReachesOnFailed()
    {
        var observer = new RecordingObserver();
        var engine = WebEngine(new RecordingHandler(), f =>
        {
            f.Observer = observer;
            f.UrlFilter = _ => false;
        });

        engine.Evaluate("fetch('https://a.test/x').then(() => 'resolved', e => e.message)")
            .UnwrapIfPromise(TransportSignalCeiling).AsString().Should().Be("Failed to fetch");

        observer.Events.Should().ContainSingle().Which.Should().StartWith("failed ");
    }

    [Test]
    public void TheObserverSurfaceMentionsNoEngineType()
    {
        // The whole point of the seam: a protocol layer runs it from a transport thread, so nothing it is
        // handed may be a value only the engine thread may touch.
        var types = new[] { typeof(FetchObserver), typeof(ObservedFetchRequest), typeof(ObservedFetchResponse), typeof(FetchInterception), typeof(FetchRequestId), typeof(FetchHeader) };

        foreach (var type in types)
        {
            foreach (var member in type.GetMembers())
            {
                var mentioned = new List<Type>();
                if (member is System.Reflection.MethodInfo method)
                {
                    mentioned.Add(method.ReturnType);
                    foreach (var parameter in method.GetParameters())
                    {
                        mentioned.Add(parameter.ParameterType);
                    }
                }
                else if (member is System.Reflection.PropertyInfo property)
                {
                    mentioned.Add(property.PropertyType);
                }

                foreach (var mention in mentioned)
                {
                    var unwrapped = mention.IsByRef || mention.IsArray ? mention.GetElementType()! : mention;
                    var name = unwrapped.FullName ?? unwrapped.Name;
                    name.Should().NotStartWith("Jint.Native", $"{type.Name}.{member.Name} may not carry an engine value");
                    name.Should().NotStartWith("Jint.Runtime", $"{type.Name}.{member.Name} may not carry an engine value");
                    name.Should().NotBe("Jint.Engine", $"{type.Name}.{member.Name} may not carry the engine");
                }
            }
        }
    }

    [Test]
    public void TheObserverIsDeclaredAPreviewSurface()
    {
        var attribute = typeof(FetchObserver)
            .GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.ExperimentalAttribute), inherit: false);

        attribute.Should().ContainSingle()
            .Which.Should().BeOfType<System.Diagnostics.CodeAnalysis.ExperimentalAttribute>()
            .Which.DiagnosticId.Should().Be("JINT0002");
    }

    /// <summary>
    /// A one-connection-at-a-time HTTP/1.1 origin on the loopback interface: enough to prove a cookie makes
    /// it out onto a socket and back in again.
    /// </summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener;
        private readonly CancellationTokenSource _stopping = new();

        internal LoopbackServer()
        {
            _listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptAsync);
        }

        internal int Port { get; }

        internal string Origin => "http://127.0.0.1:" + Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        internal List<string> Received { get; } = new();

        private async Task AcceptAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                System.Net.Sockets.TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(System.Net.Sockets.TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[8192];
                var text = new System.Text.StringBuilder();

                while (!text.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer, _stopping.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    text.Append(System.Text.Encoding.Latin1.GetString(buffer, 0, read));
                }

                var request = text.ToString();
                lock (Received)
                {
                    Received.Add(request);
                }

                var setCookie = request.StartsWith("GET /login ", StringComparison.Ordinal)
                    ? "Set-Cookie: sid=42; Path=/\r\n"
                    : string.Empty;

                var response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Type: text/plain\r\n" + setCookie + "Connection: close\r\n\r\nok";
                await stream.WriteAsync(System.Text.Encoding.Latin1.GetBytes(response), _stopping.Token).ConfigureAwait(false);
                await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();
            _stopping.Dispose();
        }
    }
}
#endif
