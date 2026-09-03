#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>XMLHttpRequest</c> against https://xhr.spec.whatwg.org/, driven by a stub
/// <see cref="HttpMessageHandler"/> so that nothing here touches a network.
/// </summary>
/// <remarks>
/// <para>
/// An asynchronous request delivers through event-loop jobs queued from a thread-pool continuation, so those
/// tests pump until they see what they are waiting for rather than asserting straight after <c>Execute</c>.
/// A synchronous one does not: <c>send()</c> has finished before it returns, which is what the sync tests
/// assert without pumping anything at all.
/// </para>
/// <para>
/// Every test that waits hands its body to <see cref="DedicatedThread.RunAsync"/>, so the wait is not itself
/// holding the pool worker the transport needs (sebastienros/jint#3213), and the window is
/// <see cref="TransportSignalCeiling"/> — a bound only a genuine failure to deliver can reach.
/// </para>
/// </remarks>
public class XmlHttpRequestTests
{
    private const string Url = "https://example.org/a";

    /// <summary>
    /// How long a test will wait for a job queued from the transport's own continuation. What is asserted is
    /// always what the request produced, never how long it took.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// What one request looked like when the transport sent it — a snapshot rather than the
    /// <see cref="HttpRequestMessage"/>, because <see cref="HttpClient"/> re-serializes a parsed header on
    /// its way to the socket and the message is disposed once the response is in hand.
    /// </summary>
    private sealed record RecordedRequest(string Method, string Url, Dictionary<string, string> Headers, string? Body)
    {
        internal string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        internal List<RecordedRequest> Requests { get; } = new();

        internal Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        /// <summary>
        /// How long the transport takes to answer, waited for on the cancellation token it was handed — so a
        /// request the engine terminates while this is running fails at once rather than after the delay.
        /// </summary>
        internal TimeSpan Delay { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Collect(headers, request.Headers);

            string? body = null;
            if (request.Content is { } content)
            {
                Collect(headers, content.Headers);

                // The bytes, decoded here rather than by ReadAsStringAsync: that overload honours the
                // Content-Type charset, and one of these tests deliberately sends a charset .NET has no
                // encoding registered for. What is being asserted is the bytes the engine produced.
                var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                body = System.Text.Encoding.UTF8.GetString(bytes);
            }

            Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.ToString(), headers, body));

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            }

            return Responder is { } responder
                ? responder(request)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }

        internal string? Header(string name) => Requests[0].Header(name);

        private static void Collect(Dictionary<string, string> headers, System.Net.Http.Headers.HttpHeaders source)
        {
            foreach (var header in source.NonValidated)
            {
                headers[header.Key] = header.Value.ToString();
            }
        }
    }

    /// <summary>
    /// An engine with the interface and a network grant, which is the ordinary shape: a host
    /// <see cref="HttpClient"/> is one of the two things that grant it.
    /// </summary>
    private static Engine XhrEngine(HttpMessageHandler handler, Action<Options.FetchOptions>? configure = null)
    {
        return new Engine(options => options.UseXmlHttpRequest(fetch =>
        {
            fetch.HttpClient = new HttpClient(handler);
            configure?.Invoke(fetch);
        }));
    }

    private static void Pump(Engine engine, Func<bool> until, string expectation)
    {
        var deadline = DateTime.UtcNow + TransportSignalCeiling;
        while (DateTime.UtcNow < deadline)
        {
            engine.Tasks.ProcessTasks();
            if (until())
            {
                return;
            }

            Thread.Sleep(2);
        }

        throw new TimeoutException($"Timed out waiting for {expectation}. The log holds: {engine.Evaluate("JSON.stringify(log)").AsString()}");
    }

    private static void PumpUntilDone(Engine engine)
        => Pump(engine, () => engine.Evaluate("xhr.readyState").AsNumber() == 4, "the request to reach DONE");

    // ---------------------------------------------------------------- the interface

    [Test]
    public void IsAbsentUnlessTheFeatureIsNamed()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof XMLHttpRequest").AsString().Should().Be("undefined");
        engine.Evaluate("typeof XMLHttpRequestUpload").AsString().Should().Be("undefined");
        engine.Evaluate("typeof XMLHttpRequestEventTarget").AsString().Should().Be("undefined");

        // ProgressEvent is not one of them any more: FileReader fires one too, so it arrives with
        // WebApiFeatures.Files, which WebApiFeatures.Default includes. Whichever feature brings the first
        // interface that fires one installs it, and the install is non-clobbering, so an engine with both
        // gets the one interface object.
        engine.Evaluate("typeof ProgressEvent").AsString().Should().Be("function");
    }

    /// <summary>
    /// The closure brings the fetch object model, and deliberately not <c>fetch</c> — installing an interface
    /// is not granting the network.
    /// </summary>
    [Test]
    public void BringsTheFetchObjectModelAndNotFetchItself()
    {
        var engine = new Engine(options => options.UseXmlHttpRequest());

        engine.Evaluate("typeof XMLHttpRequest").AsString().Should().Be("function");
        engine.Evaluate("typeof Headers").AsString().Should().Be("function");
        engine.Evaluate("typeof Request").AsString().Should().Be("function");
        engine.Evaluate("typeof Response").AsString().Should().Be("function");
        engine.Evaluate("typeof Blob").AsString().Should().Be("function");
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#interface-xmlhttprequest — the prototype chain and the constants both
    /// interface objects carry.
    /// </summary>
    [Test]
    public void HasTheSpecifiedPrototypeChainAndConstants()
    {
        var engine = new Engine(options => options.UseXmlHttpRequest());

        engine.Evaluate("Object.getPrototypeOf(XMLHttpRequest) === XMLHttpRequestEventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(XMLHttpRequestEventTarget) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(XMLHttpRequestUpload) === XMLHttpRequestEventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("new XMLHttpRequest() instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("new XMLHttpRequest().upload instanceof XMLHttpRequestUpload").AsBoolean().Should().BeTrue();

        engine.Evaluate("[XMLHttpRequest.UNSENT, XMLHttpRequest.OPENED, XMLHttpRequest.HEADERS_RECEIVED, XMLHttpRequest.LOADING, XMLHttpRequest.DONE].join()")
            .AsString().Should().Be("0,1,2,3,4");
        engine.Evaluate("XMLHttpRequest.prototype.DONE").AsNumber().Should().Be(4);
    }

    /// <summary>
    /// Neither <c>XMLHttpRequestEventTarget</c> nor <c>XMLHttpRequestUpload</c> declares a constructor
    /// operation — https://webidl.spec.whatwg.org/#es-interface-call.
    /// </summary>
    [Test]
    public void TheTwoInterfacesWithoutAConstructorRefuseToConstruct()
    {
        var engine = new Engine(options => options.UseXmlHttpRequest());

        engine.Evaluate("(() => { try { new XMLHttpRequestEventTarget(); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
        engine.Evaluate("(() => { try { new XMLHttpRequestUpload(); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    // ---------------------------------------------------------------- the grant

    /// <summary>
    /// The flag alone installs the interface and grants nothing: an asynchronous <c>send()</c> fails the way
    /// a blocked <c>fetch</c> does, as a network error reported through the <c>error</c> event.
    /// </summary>
    [Test]
    public void WithoutANetworkGrantAnAsynchronousSendReportsANetworkError()
    {
        var engine = new Engine(options => options.UseXmlHttpRequest());

        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            for (const t of ['error', 'load', 'loadend']) xhr.addEventListener(t, e => log.push(e.type));
            xhr.open('GET', '{Url}');
            xhr.send();");

        Pump(engine, () => engine.Evaluate("log.length").AsNumber() >= 2, "the error and loadend events");
        engine.Evaluate("log.join()").AsString().Should().Be("error,loadend");
        engine.Evaluate("xhr.readyState").AsNumber().Should().Be(4);
        engine.Evaluate("xhr.status").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// The synchronous half of the same rule: the request error steps throw for a synchronous request, so
    /// <c>send()</c> answers with a <c>NetworkError</c> <c>DOMException</c> the caller can catch.
    /// </summary>
    [Test]
    public void WithoutANetworkGrantASynchronousSendThrowsANetworkError()
    {
        var engine = new Engine(options => options.UseXmlHttpRequest());

        engine.Evaluate($@"(() => {{
            const xhr = new XMLHttpRequest();
            xhr.open('GET', '{Url}', false);
            try {{ xhr.send(); }} catch (e) {{ return e.name + ':' + (e instanceof DOMException); }}
            return 'no throw';
        }})()").AsString().Should().Be("NetworkError:true");
    }

    /// <summary>
    /// <see cref="WebApiFeatures.Fetch"/> is the other grant, and it is enough on its own — no host
    /// <c>HttpClient</c> needed for the interface to be willing to try.
    /// </summary>
    [Test]
    public void TheFetchFlagIsAlsoTheGrant()
    {
        var handler = new StubHandler();
        var engine = new Engine(options => options
            .UseFetch(fetch => fetch.HttpClient = new HttpClient(handler))
            .UseXmlHttpRequest());

        engine.Execute($"var log = []; var xhr = new XMLHttpRequest(); xhr.open('GET', '{Url}'); xhr.send();");
        PumpUntilDone(engine);

        engine.Evaluate("xhr.status").AsNumber().Should().Be(200);
    }

    // ---------------------------------------------------------------- open()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-open steps 2 and 3 — a grammar error and a refusal,
    /// reported as two different <c>DOMException</c>s.
    /// </summary>
    [Test]
    public void OpenRefusesABadMethodAndAForbiddenOne()
    {
        var engine = new Engine(options => options.UseXmlHttpRequest());

        engine.Evaluate($"(() => {{ try {{ new XMLHttpRequest().open('G E T', '{Url}'); }} catch (e) {{ return e.name; }} }})()")
            .AsString().Should().Be("SyntaxError");
        engine.Evaluate($"(() => {{ try {{ new XMLHttpRequest().open('CONNECT', '{Url}'); }} catch (e) {{ return e.name; }} }})()")
            .AsString().Should().Be("SecurityError");
        engine.Evaluate($"(() => {{ try {{ new XMLHttpRequest().open('TRACE', '{Url}'); }} catch (e) {{ return e.name; }} }})()")
            .AsString().Should().Be("SecurityError");
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#concept-method-normalize — six methods are case-corrected and no
    /// others, so <c>patch</c> stays lowercase exactly as it does in a browser.
    /// </summary>
    [Test]
    public void OpenNormalizesTheSixMethodsAndNoOthers()
    {
        var handler = new StubHandler();
        var engine = XhrEngine(handler);

        engine.Execute($"var log = []; var xhr = new XMLHttpRequest(); xhr.open('post', '{Url}'); xhr.send('x');");
        PumpUntilDone(engine);
        handler.Requests[0].Method.Should().Be("POST");

        engine.Execute($"xhr = new XMLHttpRequest(); xhr.open('patch', '{Url}'); xhr.send('x');");
        PumpUntilDone(engine);
        handler.Requests[1].Method.Should().Be("patch");
    }

    /// <summary>
    /// A relative URL needs an API base URL, which an engine has only when the host set
    /// <c>Options.WebApi.Fetch.BaseUrl</c>; without one it is the specification's parse failure.
    /// </summary>
    [Test]
    public void ARelativeUrlNeedsTheApiBaseUrl()
    {
        var handler = new StubHandler();

        XhrEngine(handler).Evaluate("(() => { try { new XMLHttpRequest().open('GET', '/a'); } catch (e) { return e.name; } return 'parsed'; })()")
            .AsString().Should().Be("SyntaxError");

        var engine = XhrEngine(handler, fetch => fetch.BaseUrl = new Uri("https://example.org/base/"));
        engine.Execute("var log = []; var xhr = new XMLHttpRequest(); xhr.open('GET', 'sub/a'); xhr.send();");
        PumpUntilDone(engine);

        handler.Requests[0].Url.Should().Be("https://example.org/base/sub/a");
    }

    // ---------------------------------------------------------------- setRequestHeader()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-setrequestheader step 6: a repeated name is
    /// <i>combined</i> rather than appended, so one header goes on the wire.
    /// </summary>
    [Test]
    public void SetRequestHeaderCombinesOnRepeat()
    {
        var handler = new StubHandler();
        var engine = XhrEngine(handler);

        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.open('GET', '{Url}');
            xhr.setRequestHeader('x-a', '1');
            xhr.setRequestHeader('X-A', '2');
            xhr.send();");
        PumpUntilDone(engine);

        handler.Header("x-a").Should().Be("1, 2");
    }

    /// <summary>
    /// The state checks: <c>setRequestHeader</c> before <c>open()</c> and after <c>send()</c> are both
    /// <c>InvalidStateError</c>, and a name or value that is not one is a <c>SyntaxError</c>.
    /// </summary>
    [Test]
    public void SetRequestHeaderChecksItsStateAndItsArguments()
    {
        var engine = XhrEngine(new StubHandler());

        engine.Evaluate("(() => { try { new XMLHttpRequest().setRequestHeader('a', 'b'); } catch (e) { return e.name; } })()")
            .AsString().Should().Be("InvalidStateError");
        engine.Evaluate($"(() => {{ const x = new XMLHttpRequest(); x.open('GET', '{Url}'); try {{ x.setRequestHeader('a b', 'c'); }} catch (e) {{ return e.name; }} }})()")
            .AsString().Should().Be("SyntaxError");
    }

    // ---------------------------------------------------------------- the event sequence

    /// <summary>
    /// https://xhr.spec.whatwg.org/#the-send()-method — the whole asynchronous sequence for a request with no
    /// upload, in the order the standard fires it.
    /// </summary>
    [Test]
    public Task FiresTheSpecifiedEventSequenceForAnAsynchronousRequest() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("hello") },
        };

        var engine = XhrEngine(handler);
        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.onreadystatechange = () => log.push('rsc:' + xhr.readyState);
            for (const t of ['loadstart', 'progress', 'load', 'loadend', 'error', 'abort', 'timeout']) {{
                xhr.addEventListener(t, e => log.push(t + ':' + (e instanceof ProgressEvent)));
            }}
            xhr.open('GET', '{Url}');
            xhr.send();");

        PumpUntilDone(engine);
        Pump(engine, () => engine.Evaluate("log.indexOf('loadend:true') >= 0").AsBoolean(), "loadend");

        var log = engine.Evaluate("log.join('|')").AsString();

        // readystatechange for OPENED comes from open(), then the head, then the body, then DONE.
        log.Should().StartWith("rsc:1|loadstart:true|rsc:2|");
        log.Should().EndWith("rsc:4|load:true|loadend:true");
        log.Should().Contain("progress:true");
        log.Should().NotContain("error");
        engine.Evaluate("xhr.responseText").AsString().Should().Be("hello");
    });

    /// <summary>
    /// https://xhr.spec.whatwg.org/#handle-response-end-of-body fires <c>load</c> and <c>loadend</c> from one
    /// task, and a browser still runs a microtask checkpoint in between, because each listener returns to an
    /// empty JavaScript execution context stack —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#clean-up-after-running-script. So an
    /// <c>await</c> resumed by the <c>load</c> handler runs before <c>loadend</c> is dispatched, which is
    /// what an <c>EventWatcher</c> asking for the two in order depends on (sebastienros/jint#3668).
    /// </summary>
    /// <remarks>
    /// Upstream's own XHR suites use plain handlers rather than an <c>EventWatcher</c>, which is why the
    /// divergence this pins was latent rather than red.
    /// </remarks>
    [Test]
    public Task AMicrotaskQueuedByTheLoadListenerRunsBeforeLoadend() => DedicatedThread.RunAsync(() =>
    {
        var engine = XhrEngine(new StubHandler());

        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.addEventListener('load', () => {{
                log.push('load');
                Promise.resolve().then(() => log.push('microtask'));
            }});
            xhr.addEventListener('loadend', () => log.push('loadend'));
            xhr.open('GET', '{Url}');
            xhr.send();");

        PumpUntilDone(engine);
        Pump(engine, () => engine.Evaluate("log.indexOf('loadend') >= 0").AsBoolean(), "loadend");

        engine.Evaluate("log.join('|')").AsString().Should().Be("load|microtask|loadend");
    });

    /// <summary>
    /// The upload object gets its own sequence, and only when it has a listener — the standard's
    /// <i>upload listener flag</i>.
    /// </summary>
    [Test]
    public Task FiresTheUploadSequenceWhenTheUploadObjectHasAListener() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler();
        var engine = XhrEngine(handler);

        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            for (const t of ['loadstart', 'progress', 'load', 'loadend']) {{
                xhr.upload.addEventListener(t, e => log.push('up:' + t));
            }}
            xhr.open('POST', '{Url}');
            xhr.send('hello');");

        PumpUntilDone(engine);
        Pump(engine, () => engine.Evaluate("log.indexOf('up:loadend') >= 0").AsBoolean(), "the upload loadend");

        var log = engine.Evaluate("log.join('|')").AsString();
        log.Should().StartWith("up:loadstart|");
        log.Should().EndWith("up:load|up:loadend");
    });

    // ---------------------------------------------------------------- abort()

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-abort — the request error steps run in the caller's
    /// own stack, so the events are already in the log when <c>abort()</c> returns.
    /// </summary>
    [Test]
    public void AbortFiresItsEventsSynchronously()
    {
        var handler = new StubHandler();
        var engine = XhrEngine(handler);

        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.onreadystatechange = () => log.push('rsc:' + xhr.readyState);
            for (const t of ['abort', 'loadend', 'error', 'load']) xhr.addEventListener(t, () => log.push(t));
            xhr.open('GET', '{Url}');
            xhr.send();
            xhr.abort();");

        engine.Evaluate("log.join('|')").AsString().Should().Be("rsc:1|rsc:4|abort|loadend");

        // Step 3: a DONE object goes back to UNSENT, and no event says so.
        engine.Evaluate("xhr.readyState").AsNumber().Should().Be(0);
    }

    // ---------------------------------------------------------------- responses

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-getallresponseheaders — sorted, combined and joined
    /// with CRLF, one line per distinct name.
    /// </summary>
    [Test]
    public void GetAllResponseHeadersIsSortedAndCrlfJoined()
    {
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("x") };
                response.Headers.TryAddWithoutValidation("X-Zed", "1");
                response.Headers.TryAddWithoutValidation("X-Alpha", "2");
                return response;
            },
        };

        var engine = XhrEngine(handler);
        engine.Execute($"var log = []; var xhr = new XMLHttpRequest(); xhr.open('GET', '{Url}'); xhr.send();");
        PumpUntilDone(engine);

        var headers = engine.Evaluate("xhr.getAllResponseHeaders()").AsString();
        headers.Should().Contain("x-alpha: 2\r\n");
        headers.Should().Contain("x-zed: 1\r\n");
        headers.IndexOf("x-alpha", StringComparison.Ordinal).Should().BeLessThan(headers.IndexOf("x-zed", StringComparison.Ordinal));

        engine.Evaluate("xhr.getResponseHeader('X-ALPHA')").AsString().Should().Be("2");
        engine.Evaluate("xhr.getResponseHeader('x-missing')").IsNull().Should().BeTrue();
    }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-response — each <c>responseType</c> and the object it
    /// answers with.
    /// </summary>
    [Test]
    public void ResponseTypeDecidesWhatResponseAnswers()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"a\":1}", System.Text.Encoding.UTF8, "application/json"),
            },
        };

        Read("json", "typeof xhr.response + ':' + xhr.response.a").Should().Be("object:1");
        Read("text", "xhr.response").Should().Be("{\"a\":1}");
        Read("arraybuffer", "xhr.response.constructor.name + ':' + xhr.response.byteLength").Should().Be("ArrayBuffer:7");
        Read("blob", "xhr.response.constructor.name + ':' + xhr.response.size + ':' + xhr.response.type").Should().Be("Blob:7:application/json;charset=utf-8");

        // No DocumentParser, so the document response is the specification's failure: null.
        Read("document", "String(xhr.response) + ':' + String(xhr.responseXML)").Should().Be("null:null");

        string Read(string responseType, string expression)
        {
            var engine = XhrEngine(handler);
            engine.Execute($"var log = []; var xhr = new XMLHttpRequest(); xhr.open('GET', '{Url}'); xhr.responseType = '{responseType}'; xhr.send();");
            PumpUntilDone(engine);
            return engine.Evaluate(expression).AsString();
        }
    }

    /// <summary>
    /// <c>responseText</c> and <c>responseXML</c> are readable only for the response types the standard
    /// names, and refuse the rest with an <c>InvalidStateError</c>.
    /// </summary>
    [Test]
    public void TheTwoLegacyResponseGettersRefuseTheWrongResponseType()
    {
        var engine = XhrEngine(new StubHandler());
        engine.Execute("var x = new XMLHttpRequest(); x.responseType = 'blob';");

        engine.Evaluate("(() => { try { x.responseText; } catch (e) { return e.name; } })()").AsString().Should().Be("InvalidStateError");
        engine.Evaluate("(() => { try { x.responseXML; } catch (e) { return e.name; } })()").AsString().Should().Be("InvalidStateError");
    }

    /// <summary>
    /// <c>Options.WebApi.Xhr.DocumentParser</c> is what makes <c>responseXML</c> answer anything at all; it
    /// receives the decoded body and the essence of the final MIME type.
    /// </summary>
    [Test]
    public void TheDocumentParserHookBuildsTheDocumentResponse()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<p>hi</p>", System.Text.Encoding.UTF8, "text/html"),
            },
        };

        var engine = new Engine(options =>
        {
            options.UseXmlHttpRequest(fetch => fetch.HttpClient = new HttpClient(handler));
            options.WebApi.Xhr.DocumentParser = (e, text, mime) => e.Evaluate($"({{ mime: {System.Text.Json.JsonSerializer.Serialize(mime)}, text: {System.Text.Json.JsonSerializer.Serialize(text)} }})");
        });

        engine.Execute($"var log = []; var xhr = new XMLHttpRequest(); xhr.open('GET', '{Url}'); xhr.send();");
        PumpUntilDone(engine);

        engine.Evaluate("xhr.responseXML.mime + '|' + xhr.responseXML.text").AsString().Should().Be("text/html|<p>hi</p>");

        // Steps 4 and 5: the object is cached, so two reads answer the same one.
        engine.Evaluate("xhr.responseXML === xhr.responseXML").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>overrideMimeType</c> replaces the final MIME type, which is what the response is decoded and typed
    /// with — and it is refused once the state is LOADING or DONE.
    /// </summary>
    [Test]
    public void OverrideMimeTypeReplacesTheFinalMimeType()
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("x", System.Text.Encoding.UTF8, "text/plain"),
            },
        };

        var engine = XhrEngine(handler);
        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.open('GET', '{Url}');
            xhr.overrideMimeType('application/octet-stream');
            xhr.responseType = 'blob';
            xhr.send();");
        PumpUntilDone(engine);

        engine.Evaluate("xhr.response.type").AsString().Should().Be("application/octet-stream");
        engine.Evaluate("(() => { try { xhr.overrideMimeType('text/plain'); } catch (e) { return e.name; } })()")
            .AsString().Should().Be("InvalidStateError");
    }

    // ---------------------------------------------------------------- send() bodies

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send step 5: the body decides the <c>Content-Type</c>
    /// when the author set none, and every <c>XMLHttpRequestBodyInit</c> arm goes through the fetch object
    /// model's own extract-a-body.
    /// </summary>
    [Test]
    public void EachBodyTypeIsExtractedAndTypedTheWayFetchExtractsIt()
    {
        Send("'hi'").Should().Be("hi|text/plain;charset=UTF-8");
        Send("new Blob(['hi'], { type: 'text/x-a' })").Should().Be("hi|text/x-a");
        Send("new URLSearchParams({ a: '1' })").Should().Be("a=1|application/x-www-form-urlencoded;charset=UTF-8");
        Send("new Uint8Array([104, 105])").Should().Be("hi|");

        // A GET drops the body outright rather than refusing it, which is the difference from `new Request`.
        SendWith("GET", "'hi'").Should().Be("|");

        string Send(string body) => SendWith("POST", body);

        string SendWith(string method, string body)
        {
            var handler = new StubHandler();
            var engine = XhrEngine(handler);
            engine.Execute($"var log = []; var xhr = new XMLHttpRequest(); xhr.open('{method}', '{Url}'); xhr.send({body});");
            PumpUntilDone(engine);

            return (handler.Requests[0].Body ?? string.Empty) + "|" + (handler.Header("content-type") ?? string.Empty);
        }
    }

    /// <summary>
    /// Step 5.5: an author <c>Content-Type</c> keeps its essence but has its charset forced to UTF-8 for a
    /// string body — and is left alone for every other body type.
    /// </summary>
    [Test]
    public void AnAuthorContentTypeIsForcedToUtf8ForAStringBodyOnly()
    {
        Send("'hi'", "text/plain;charset=windows-1252").Should().Be("text/plain;charset=UTF-8");
        Send("'hi'", "text/plain").Should().Be("text/plain");
        Send("new Blob(['hi'])", "text/plain;charset=windows-1252").Should().Be("text/plain;charset=windows-1252");

        string Send(string body, string contentType)
        {
            var handler = new StubHandler();
            var engine = XhrEngine(handler);
            engine.Execute($@"
                var log = [];
                var xhr = new XMLHttpRequest();
                xhr.open('POST', '{Url}');
                xhr.setRequestHeader('Content-Type', '{contentType}');
                xhr.send({body});");
            PumpUntilDone(engine);

            return handler.Header("content-type")!;
        }
    }

    // ---------------------------------------------------------------- synchronous

    /// <summary>
    /// <c>open(…, false)</c> finishes before <c>send()</c> returns: the response is readable with nothing
    /// pumped at all, which is the whole point of supporting it.
    /// </summary>
    [Test]
    public Task ASynchronousRequestCompletesWithoutAnyPump() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("sync") },
        };

        var engine = XhrEngine(handler);
        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.onreadystatechange = () => log.push('rsc:' + xhr.readyState);
            for (const t of ['loadstart', 'progress', 'load', 'loadend']) xhr.addEventListener(t, () => log.push(t));
            xhr.open('GET', '{Url}', false);
            xhr.send();");

        // Nothing has been pumped, and the request is over.
        engine.Evaluate("xhr.readyState").AsNumber().Should().Be(4);
        engine.Evaluate("xhr.status").AsNumber().Should().Be(201);
        engine.Evaluate("xhr.responseText").AsString().Should().Be("sync");

        // https://xhr.spec.whatwg.org/#handle-response-end-of-body: a synchronous request fires no
        // loadstart and no progress — only the three events that say it is over.
        engine.Evaluate("log.join('|')").AsString().Should().Be("rsc:1|rsc:4|load|loadend");
    });

    /// <summary>
    /// A synchronous request the transport refuses throws rather than firing anything, because the request
    /// error steps stop at "if the synchronous flag is set, throw exception".
    /// </summary>
    [Test]
    public Task ASynchronousRequestReportsAFailureByThrowing() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler { Responder = _ => throw new HttpRequestException("no route") };
        var engine = XhrEngine(handler);

        engine.Evaluate($@"(() => {{
            const log = [];
            const xhr = new XMLHttpRequest();
            for (const t of ['error', 'loadend']) xhr.addEventListener(t, () => log.push(t));
            xhr.open('GET', '{Url}', false);
            try {{ xhr.send(); }} catch (e) {{ return e.name + ':' + log.length + ':' + xhr.readyState; }}
            return 'no throw';
        }})()").AsString().Should().Be("NetworkError:0:4");
    });

    /// <summary>
    /// The synchronous wait is on the transport, which never touches the engine — so it does not need the
    /// engine to be pumped and cannot deadlock with a host loop that is not running.
    /// </summary>
    /// <remarks>
    /// The handler answers only once a background task has released it, which is the shape that would
    /// deadlock if the wait were a pump: nothing on the engine's own queue can make the response arrive.
    /// </remarks>
    [Test]
    public Task ASynchronousRequestDoesNotNeedTheEngineToBePumped() => DedicatedThread.RunAsync(() =>
    {
        using var released = new ManualResetEventSlim(false);
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                released.Wait(TransportSignalCeiling);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("late") };
            },
        };

        _ = Task.Run(async () =>
        {
            await Task.Delay(50).ConfigureAwait(false);
            released.Set();
        });

        var engine = XhrEngine(handler);
        engine.Evaluate($@"(() => {{
            const xhr = new XMLHttpRequest();
            xhr.open('GET', '{Url}', false);
            xhr.send();
            return xhr.responseText;
        }})()").AsString().Should().Be("late");
    });

    /// <summary>
    /// The two <c>InvalidAccessError</c> rules that hold only "if the current global object is a
    /// <c>Window</c>" do not apply here, because this global is not one — the position a worker is in.
    /// </summary>
    [Test]
    public Task ASynchronousRequestMaySetTimeoutAndResponseType() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"a\":1}") },
        };

        var engine = XhrEngine(handler);
        engine.Evaluate($@"(() => {{
            const xhr = new XMLHttpRequest();
            xhr.open('GET', '{Url}', false);
            xhr.timeout = 30000;
            xhr.responseType = 'json';
            xhr.send();
            return xhr.response.a;
        }})()").AsNumber().Should().Be(1);
    });

    // ---------------------------------------------------------------- timeout and withCredentials

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-timeout — the deadline fires the <c>timeout</c> event
    /// rather than <c>error</c>, from a task on the engine's own event loop. The transport here never answers
    /// at all, so what is pinned is that the deadline still settles the request.
    /// </summary>
    [Test]
    public Task TheTimeoutAttributeFiresTheTimeoutEvent() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler
        {
            Responder = _ =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(30));
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
        };

        var engine = XhrEngine(handler);
        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            for (const t of ['timeout', 'error', 'load', 'loadend']) xhr.addEventListener(t, () => log.push(t));
            xhr.open('GET', '{Url}');
            xhr.timeout = 50;
            xhr.send();");

        Pump(engine, () => engine.Evaluate("log.length").AsNumber() >= 2, "the timeout and loadend events");
        engine.Evaluate("log.join('|')").AsString().Should().Be("timeout|loadend");
        engine.Evaluate("xhr.readyState").AsNumber().Should().Be(4);
    });

    /// <summary>
    /// https://xhr.spec.whatwg.org/#the-timeout-attribute — the asynchronous deadline is a task on the
    /// engine's own event loop, so a long-running task holds it off exactly as it holds off a
    /// <c>setTimeout</c>: a response that arrives while the loop is busy is delivered, and the deadline that
    /// came due behind it finds nothing left to terminate.
    /// </summary>
    /// <remarks>
    /// This is web-platform-tests' <c>xhr/xhr-timeout-longtask.any.js</c> written as a unit test: a 50 ms
    /// <c>timeout</c>, a response 150 ms out, and a 600 ms busy loop entered from the very task <c>send()</c>
    /// was called in. Every number is a wide multiple of the one before it, so what the test asserts is the
    /// ordering and never the timing.
    /// </remarks>
    [Test]
    public Task ALongTaskHoldsOffTheTimeoutSoTheResponseStillArrives() => DedicatedThread.RunAsync(() =>
    {
        var handler = new StubHandler { Delay = TimeSpan.FromMilliseconds(150) };

        var engine = XhrEngine(handler);
        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            for (const t of ['timeout', 'error', 'load', 'loadend']) xhr.addEventListener(t, () => log.push(t));
            xhr.open('GET', '{Url}');
            xhr.timeout = 50;
            xhr.send();
            const start = Date.now();
            while (Date.now() - start < 600) {{ }}");

        Pump(engine, () => engine.Evaluate("log.length").AsNumber() >= 2, "the load and loadend events");
        engine.Evaluate("log.join('|')").AsString().Should().Be("load|loadend");
        engine.Evaluate("xhr.responseText").AsString().Should().Be("ok");
    });

    /// <summary>
    /// <c>withCredentials</c> is remembered, is refused once the request is in flight, and selects the
    /// <c>include</c> credentials mode — which is what makes a host cookie jar travel.
    /// </summary>
    [Test]
    public void WithCredentialsIsRememberedAndGuarded()
    {
        var engine = XhrEngine(new StubHandler());

        engine.Evaluate($@"(() => {{
            const xhr = new XMLHttpRequest();
            xhr.open('GET', '{Url}');
            xhr.withCredentials = true;
            xhr.send();
            try {{ xhr.withCredentials = false; }} catch (e) {{ return xhr.withCredentials + ':' + e.name; }}
            return 'no throw';
        }})()").AsString().Should().Be("true:InvalidStateError");
    }

    // ---------------------------------------------------------------- resource bounds

    /// <summary>
    /// The destination policy is <c>fetch</c>'s, so a <c>UrlFilter</c> a host wrote once refuses an
    /// <c>XMLHttpRequest</c> too — reported as a network error, with no detail about why.
    /// </summary>
    [Test]
    public void TheUrlFilterRefusesAnXmlHttpRequestToo()
    {
        var handler = new StubHandler();
        var engine = XhrEngine(handler, fetch => fetch.UrlFilter = uri => uri.Host == "allowed.example");

        engine.Execute($@"
            var log = [];
            var xhr = new XMLHttpRequest();
            xhr.addEventListener('error', () => log.push('error'));
            xhr.open('GET', '{Url}');
            xhr.send();");

        Pump(engine, () => engine.Evaluate("log.length").AsNumber() >= 1, "the error event");
        handler.Requests.Should().BeEmpty();
    }
}
#endif
