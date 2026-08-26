#if NET8_0_OR_GREATER
#nullable enable

using System.Net.Http;
using System.Text;
using Jint.Native;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// What a fetch handler <i>sees</i> when a host routes an inbound request into it: a real <c>Request</c>,
/// with the whole of the Body mixin working over the bytes the host handed over.
/// </summary>
/// <remarks>
/// The host-facing contract — the operation, the failure map, the header split on the way out — is pinned
/// from outside the assembly in <c>Jint.Tests.PublicInterface.WebApiFetchHandlerTests</c>. What is here is
/// the script's half.
/// </remarks>
public class FetchHandlerTests
{
    private const WebApiFeatures ModelFeatures = WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;

    private static Engine Handler(string source)
    {
        var engine = new Engine(options => options.UseWebApis(ModelFeatures));
        engine.Execute(source);
        engine.WebApi.SetFetchHandler(engine.GetValue("handler"));
        return engine;
    }

    /// <summary>Runs one request through the handler and answers with the response body as text.</summary>
    private static string Answer(Engine engine, HttpRequestMessage request)
    {
        var operation = engine.WebApi.InvokeFetchHandler(request);
        for (var i = 0; i < 100 && !operation.IsCompleted; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        using var response = operation.GetResult();
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static HttpRequestMessage Post(string body, string mediaType = "application/json")
        => new(HttpMethod.Post, "https://example.org/")
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

    [Test]
    public void TheRequestIsARealRequestWithTheWholeBodyMixin()
    {
        var engine = Handler("""
            globalThis.handler = {
                async fetch(request) {
                    const before = request.bodyUsed;
                    const parsed = await request.json();
                    return new Response([
                        request instanceof Request,
                        Object.getPrototypeOf(request) === Request.prototype,
                        before,
                        request.bodyUsed,
                        parsed.n,
                    ].join(','));
                }
            };
            """);

        // https://fetch.spec.whatwg.org/#dom-body-bodyused — the disturbed flag flips on the first consume.
        Answer(engine, Post("{\"n\":7}")).Should().Be("true,true,false,true,7");
    }

    [Test]
    public void ASecondReadOfTheBodyRejectsTheWayItWouldInABrowser()
    {
        var engine = Handler("""
            globalThis.handler = {
                async fetch(request) {
                    await request.text();
                    try {
                        await request.text();
                        return new Response('read twice');
                    } catch (e) {
                        return new Response(e.constructor.name + ': ' + e.message);
                    }
                }
            };
            """);

        Answer(engine, Post("{}")).Should().Be("TypeError: Body has already been consumed");
    }

    [Test]
    public void AContentWithNoBytesIsANullBodyRatherThanAnEmptyOne()
    {
        var engine = Handler("""
            globalThis.handler = {
                async fetch(request) {
                    const first = await request.text();
                    const second = await request.text();
                    return new Response([request.bodyUsed, first === '', second === ''].join(','));
                }
            };
            """);

        // The shape an ASP.NET Core host produces for a GET: a content that is there and empty. A null body
        // is not disturbed by being read, so bodyUsed stays false and a handler may read it as often as it
        // likes — where an empty body would flip the flag and reject the second read.
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/")
        {
            Content = new ByteArrayContent([]),
        };

        Answer(engine, request).Should().Be("false,true,true");
    }

    [Test]
    public void TheRequestSignalIsAnAbortSignalThatNothingFires()
    {
        var engine = Handler("""
            globalThis.handler = {
                fetch(request) {
                    let fired = false;
                    request.signal.addEventListener('abort', () => { fired = true; });
                    return new Response([
                        request.signal instanceof AbortSignal,
                        request.signal.aborted,
                        fired,
                    ].join(','));
                }
            };
            """);

        // An invocation given no cancellation token has no client-disconnect channel, so its signal exists to
        // be listened to and to be forwarded to an outbound fetch — never to fire by itself. The overload
        // that takes one is what makes it fire; see FetchHandlerAbortTests.
        Answer(engine, Post("{}")).Should().Be("true,false,false");
    }

    [Test]
    public void EquallyNamedHeadersCombineTheWayTheStandardCombinesThem()
    {
        var engine = Handler("globalThis.handler = request => new Response(request.headers.get('X-Trace') + '|' + [...request.headers.keys()].join(','));");

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/");
        request.Headers.Add("X-Trace", "a");
        request.Headers.Add("X-Trace", "b");
        request.Headers.Add("Accept", "*/*");

        // https://fetch.spec.whatwg.org/#concept-header-list-get combines with ", ", and iteration is over
        // lowercased names in ascending byte order.
        Answer(engine, request).Should().Be("a, b|accept,x-trace");
    }

    [Test]
    public void TheRequestCanBeCopiedTheWayTheConstructorCopiesOne()
    {
        var engine = Handler("""
            globalThis.handler = {
                async fetch(request) {
                    const copy = new Request(request, { method: 'PUT' });
                    return new Response([copy.url, copy.method, await copy.text()].join('|'));
                }
            };
            """);

        // Step 43 of https://fetch.spec.whatwg.org/#dom-request — the copy shares the bytes and carries its
        // own used flag, which is what lets a handler forward the request it was given.
        Answer(engine, Post("payload", "text/plain")).Should().Be("https://example.org/|PUT|payload");
    }

    [Test]
    public void AHandlerCanAnswerWithTheResponseStatics()
    {
        var engine = Handler("globalThis.handler = () => Response.json({ ok: true }, { status: 202 });");

        var operation = engine.WebApi.InvokeFetchHandler(new HttpRequestMessage(HttpMethod.Get, "https://example.org/"));
        using var response = operation.GetResult();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Test]
    public void TheHandlerIsNotVisibleAsAGlobalAndNeitherIsFetch()
    {
        var engine = Handler("globalThis.handler = () => new Response('x');");

        // Registering a handler installs the object model and nothing else: no fetch, and no name of Jint's
        // own invention on the global object.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
        engine.Evaluate("Object.getOwnPropertyNames(globalThis).filter(n => n.toLowerCase().includes('fetch')).join(',')")
            .AsString().Should().BeEmpty();

        // ... and a ShadowRealm never sees the model, like every other web API.
        engine.Evaluate("new ShadowRealm().evaluate('typeof Response')").AsString().Should().Be("undefined");
    }

    [Test]
    public void TheHandlerRunsWithTheEnginesOwnGlobals()
    {
        var engine = new Engine(options => options
            .UseWebApis(ModelFeatures)
            .AddLazyGlobal("tenant", static _ => JsString.Create("acme")));

        engine.Execute("globalThis.handler = () => new Response(tenant);");
        engine.WebApi.SetFetchHandler(engine.GetValue("handler"));

        // A Workers handler takes (request, env, ctx); this one takes the request alone, and per-request host
        // state reaches the script the way it always has.
        Answer(engine, new HttpRequestMessage(HttpMethod.Get, "https://example.org/")).Should().Be("acme");
    }
}
#endif
