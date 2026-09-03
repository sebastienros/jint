#if NET8_0_OR_GREATER
#nullable enable

using System.Net.Http;
using System.Text;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The script's half of <c>WebApiFeatures.FetchEvents</c>: the <c>FetchEvent</c> interface, and what
/// <c>respondWith()</c> and <c>waitUntil()</c> do inside a listener.
/// </summary>
/// <remarks>
/// <para>
/// The specification is Service Workers,
/// <see href="https://w3c.github.io/ServiceWorker/#fetchevent-interface">§4.6 FetchEvent</see> over
/// <see href="https://w3c.github.io/ServiceWorker/#extendableevent-interface">§4.4 ExtendableEvent</see>. Two
/// reductions are deliberate and asserted here rather than merely written down: there is <b>no
/// <c>ExtendableEvent</c> interface object</b> — <c>waitUntil</c> is a member of <c>FetchEvent.prototype</c>,
/// the flat shape Cloudflare Workers exposes — and there is <b>no timed out flag</b>, so an event stays
/// extendable exactly as long as its own lifetime promises are pending.
/// </para>
/// <para>
/// The host-facing contract — which route an invocation takes, and how a failure reaches the host — is pinned
/// from outside the assembly in <c>Jint.Tests.PublicInterface.WebApiFetchHandlerTests</c>.
/// </para>
/// </remarks>
public class FetchEventTests
{
    private sealed class RecordingSink : DiagnosticsSink
    {
        internal List<DiagnosticEvent> Reports { get; } = new();

        public override void Report(DiagnosticEvent report) => Reports.Add(report);
    }

    private static Engine Worker(string source, DiagnosticsSink? sink = null)
    {
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.FetchEvents;
            webApi.Diagnostics.Sink = sink;
        }));

        engine.Execute(source);
        return engine;
    }

    private static HttpRequestMessage Get(string url = "https://example.org/hello") => new(HttpMethod.Get, url);

    /// <summary>
    /// Runs one request through whatever the engine has registered, pumping until it is done, and answers with
    /// the response body as text.
    /// </summary>
    private static string Answer(Engine engine, HttpRequestMessage? request = null)
    {
        var operation = engine.WebApi.InvokeFetchHandler(request ?? Get());
        for (var i = 0; i < 100 && !operation.IsCompleted; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        using var response = operation.GetResult();
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // The surface

    [Test]
    public void TheFeatureInstallsTheEventAndTheObjectModelButNeverFetch()
    {
        var engine = Worker("");

        engine.Evaluate("typeof FetchEvent").AsString().Should().Be("function");
        engine.Evaluate("typeof Response").AsString().Should().Be("function");
        engine.Evaluate("typeof Request").AsString().Should().Be("function");
        engine.Evaluate("typeof Headers").AsString().Should().Be("function");

        // The closure: addEventListener comes from GlobalEvents, URL from Url, Blob from Files.
        engine.Evaluate("typeof addEventListener").AsString().Should().Be("function");
        engine.Evaluate("typeof URL").AsString().Should().Be("function");
        engine.Evaluate("typeof Blob").AsString().Should().Be("function");

        // The whole point of the separate flag: routing requests IN is not a grant to reach OUT.
        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
        engine.WebApi.Features.Should().NotHaveFlag(WebApiFeatures.Fetch);
    }

    [Test]
    public void TheFeatureIsNotInDefaultAndFetchDoesNotBringIt()
    {
        WebApiFeatures.Default.Should().NotHaveFlag(WebApiFeatures.FetchEvents);

        var standard = new Engine(options => options.UseWebApis());
        standard.Evaluate("typeof FetchEvent").AsString().Should().Be("undefined");

        // Deliberately not implied in either direction — outbound network and inbound routing are two grants.
        var fetching = new Engine(options => options.UseFetch());
        fetching.WebApi.Features.Should().NotHaveFlag(WebApiFeatures.FetchEvents);
        fetching.Evaluate("typeof FetchEvent").AsString().Should().Be("undefined");
    }

    [Test]
    public void TheInterfaceObjectHasTheWebIdlShape()
    {
        var engine = Worker("");

        engine.Evaluate("FetchEvent.name").AsString().Should().Be("FetchEvent");

        // 2, not 1: FetchEventInit is declared without `optional` because it has a required member.
        engine.Evaluate("FetchEvent.length").AsNumber().Should().Be(2);

        // https://webidl.spec.whatwg.org/#es-interfaces — writable and configurable, not enumerable.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'FetchEvent')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeFalse();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();

        // The flat shape: the IDL puts ExtendableEvent between these two, and Jint does not materialize it.
        engine.Evaluate("Object.getPrototypeOf(FetchEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(FetchEvent.prototype) === Event.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof ExtendableEvent").AsString().Should().Be("undefined");

        // waitUntil therefore lives here rather than one level up.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(FetchEvent.prototype, 'waitUntil')").AsBoolean().Should().BeTrue();
        engine.Evaluate("FetchEvent.prototype.respondWith.length").AsNumber().Should().Be(1);
        engine.Evaluate("FetchEvent.prototype.waitUntil.length").AsNumber().Should().Be(1);
        engine.Evaluate("FetchEvent.prototype[Symbol.toStringTag]").AsString().Should().Be("FetchEvent");
    }

    [Test]
    public void TheMembersBrandCheckTheirReceiver()
    {
        var engine = Worker("");

        foreach (var member in new[] { "FetchEvent.prototype.respondWith.call({})", "FetchEvent.prototype.waitUntil.call({})", "Object.getOwnPropertyDescriptor(FetchEvent.prototype, 'request').get.call({})" })
        {
            var failure = Assert.Throws<JavaScriptException>(() => engine.Evaluate(member))!;
            failure.Error.Get("name").AsString().Should().Be("TypeError");
            failure.Message.Should().Contain("not a FetchEvent");
        }
    }

    // The constructor

    [Test]
    public void TheConstructorRequiresATypeAndARequestOfTheRightType()
    {
        var engine = Worker("var request = new Request('https://example.org/');");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new FetchEvent()"))!
            .Message.Should().Contain("1 argument required");

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new FetchEvent('fetch')"))!
            .Message.Should().Contain("2 arguments required");

        // undefined and null both convert to a dictionary with no members, which is missing a required one.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new FetchEvent('fetch', undefined)"))!
            .Message.Should().Contain("required member request is undefined");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new FetchEvent('fetch', {})"))!
            .Message.Should().Contain("required member request is undefined");

        // An interface type coerces nothing: https://webidl.spec.whatwg.org/#es-interface.
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new FetchEvent('fetch', { request: 'https://example.org/' })"))!
            .Message.Should().Contain("not of type 'Request'");

        engine.Evaluate("new FetchEvent('fetch', { request }).request === request").AsBoolean().Should().BeTrue();
        engine.Evaluate("new FetchEvent('fetch', { request }) instanceof Event").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheInheritedEventInitMembersAreConvertedBeforeTheDictionarysOwn()
    {
        var engine = Worker("""
            var seen = [];
            var request = new Request('https://example.org/');
            var init = {
                get bubbles() { seen.push('bubbles'); return true; },
                get cancelable() { seen.push('cancelable'); return true; },
                get composed() { seen.push('composed'); return false; },
                get request() { seen.push('request'); return request; },
            };
            var event = new FetchEvent('fetch', init);
            """);

        // https://webidl.spec.whatwg.org/#es-dictionary — inherited members first, and the order is observable.
        engine.Evaluate("seen.join(',')").AsString().Should().Be("bubbles,cancelable,composed,request");
        engine.Evaluate("event.bubbles && event.cancelable").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AScriptConstructedEventIsUntrustedAndCanNeitherRespondNorBeExtended()
    {
        var engine = Worker("""
            var log = [];
            addEventListener('fetch', event => {
                try { event.respondWith(new Response('nope')); } catch (e) { log.push('respondWith:' + e.name); }
                try { event.waitUntil(Promise.resolve()); } catch (e) { log.push('waitUntil:' + e.name); }
            });

            var request = new Request('https://example.org/');
            var returned = dispatchEvent(new FetchEvent('fetch', { request }));
            """);

        // Step 1 of "add lifetime promise": an untrusted event cannot be extended, so neither operation works
        // — only the event the engine created for an inbound request can be answered.
        engine.Evaluate("log.join(',')").AsString().Should().Be("respondWith:InvalidStateError,waitUntil:InvalidStateError");

        // ... and the dispatch itself behaved like any other: nothing cancelled it.
        engine.Evaluate("returned").AsBoolean().Should().BeTrue();
    }

    // The trusted event, through the hosting path

    [Test]
    public void TheDispatchedEventIsATrustedCancelableFetchEventCarryingTheRequest()
    {
        var engine = Worker("""
            addEventListener('fetch', event => {
                event.respondWith(new Response(JSON.stringify({
                    type: event.type,
                    isTrusted: event.isTrusted,
                    cancelable: event.cancelable,
                    bubbles: event.bubbles,
                    isFetchEvent: event instanceof FetchEvent,
                    isEvent: event instanceof Event,
                    target: event.target === globalThis,
                    thisIsGlobal: this === globalThis,
                    url: event.request.url,
                    method: event.request.method,
                    sameObject: event.request === event.request,
                })));
            });
            """);

        var json = Answer(engine, new HttpRequestMessage(HttpMethod.Post, "https://example.org/a?b=1"));

        json.Should().Contain("\"type\":\"fetch\"");
        json.Should().Contain("\"isTrusted\":true");

        // "Initialize e's cancelable attribute to true" — the on-fetch-request algorithm.
        json.Should().Contain("\"cancelable\":true");
        json.Should().Contain("\"bubbles\":false");
        json.Should().Contain("\"isFetchEvent\":true");
        json.Should().Contain("\"isEvent\":true");
        json.Should().Contain("\"target\":true");
        json.Should().Contain("\"thisIsGlobal\":true");
        json.Should().Contain("\"url\":\"https://example.org/a?b=1\"");
        json.Should().Contain("\"method\":\"POST\"");
        json.Should().Contain("\"sameObject\":true");
    }

    [Test]
    public void TheListenerSeesARealRequestWithTheWholeBodyMixin()
    {
        var engine = Worker("""
            addEventListener('fetch', event => {
                event.respondWith(event.request.json().then(body => new Response('n=' + body.n)));
            });
            """);

        var message = new HttpRequestMessage(HttpMethod.Post, "https://example.org/")
        {
            Content = new StringContent("{\"n\":7}", Encoding.UTF8, "application/json"),
        };

        Answer(engine, message).Should().Be("n=7");
    }

    [Test]
    public void RespondWithAcceptsAPlainResponseThroughThePromiseConversion()
    {
        // `Promise<Response> r` converts anything, so a Response goes in directly — which is what every
        // Workers script writes.
        var engine = Worker("addEventListener('fetch', e => e.respondWith(new Response('plain')));");
        Answer(engine).Should().Be("plain");
    }

    [Test]
    public void RespondWithWithNoArgumentIsTheWebIdlArityError()
    {
        var engine = Worker("""
            var log = [];
            addEventListener('fetch', event => {
                try { event.respondWith(); } catch (e) { log.push(e.name + ':' + (e instanceof TypeError)); }
                event.respondWith(new Response('after'));
            });
            """);

        // Without the arity check the Promise<T> conversion would happily resolve with undefined and the
        // request would fail a turn later complaining about the answer not being a Response.
        Answer(engine).Should().Be("after");
        engine.Evaluate("log.join(',')").AsString().Should().Be("TypeError:true");
    }

    // respondWith's state machine

    [Test]
    public void ASecondRespondWithIsAnInvalidStateError()
    {
        var engine = Worker("""
            var log = [];
            addEventListener('fetch', event => {
                event.respondWith(new Response('first'));
                try { event.respondWith(new Response('second')); } catch (e) { log.push(e.name); }
            });
            """);

        Answer(engine).Should().Be("first");
        engine.Evaluate("log.join(',')").AsString().Should().Be("InvalidStateError");
    }

    [Test]
    public void RespondWithAfterTheDispatchIsAnInvalidStateError()
    {
        var engine = Worker("""
            var captured = null;
            addEventListener('fetch', event => {
                captured = event;
                event.respondWith(new Response('answered'));
            });
            """);

        Answer(engine).Should().Be("answered");

        // The dispatch flag is unset, so step 2 refuses — before the entered flag is even looked at.
        var failure = Assert.Throws<JavaScriptException>(() => engine.Evaluate("captured.respondWith(new Response('late'))"))!;
        failure.Error.Get("name").AsString().Should().Be("InvalidStateError");
        failure.Message.Should().Contain("not being dispatched");
    }

    [Test]
    public void RespondWithEndsTheDispatchForEveryLaterListener()
    {
        var engine = Worker("""
            var log = [];
            addEventListener('fetch', event => { log.push('first'); event.respondWith(new Response('from first')); });
            addEventListener('fetch', () => { log.push('second'); });
            addEventListener('fetch', () => { log.push('capturing'); }, true);
            """);

        // Step 5 sets both the stop propagation and the stop immediate propagation flags, so the first
        // responder wins the dispatch outright. The capturing listener still ran: a capturing listener runs in
        // the first pass, before any non-capturing one.
        Answer(engine).Should().Be("from first");
        engine.Evaluate("log.join(',')").AsString().Should().Be("capturing,first");
    }

    [Test]
    public void RespondWithDoesNotCancelTheEvent()
    {
        var engine = Worker("""
            var prevented = null;
            addEventListener('fetch', event => {
                event.respondWith(new Response('x'));
                prevented = event.defaultPrevented;
            });
            """);

        Answer(engine).Should().Be("x");

        // The current respondWith(r) steps set the two propagation flags and nothing else — an older edition
        // of the specification, and MDN's prose, describe an implicit preventDefault() that the algorithm no
        // longer performs. Setting the canceled flag here would be observable and wrong.
        engine.Evaluate("prevented").AsBoolean().Should().BeFalse();
    }

    // waitUntil and the lifetime

    [Test]
    public void WaitUntilIsAllowedWhileTheEventIsStillActiveAndRefusedAfterwards()
    {
        var engine = Worker("""
            var log = [];
            var captured = null;
            var release = null;
            addEventListener('fetch', event => {
                captured = event;
                event.respondWith(new Promise(resolve => { release = resolve; }));
            });
            """);

        var operation = engine.WebApi.InvokeFetchHandler(Get());
        operation.IsCompleted.Should().BeFalse();

        // The response promise is a lifetime promise too, so the event is still active with the dispatch over.
        engine.Execute("captured.waitUntil(Promise.resolve('background'));");

        engine.Execute("release(new Response('done'));");
        for (var i = 0; i < 100 && !operation.IsCompleted; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        using (var response = operation.GetResult())
        {
            using var reader = new StreamReader(response.Content.ReadAsStream(), Encoding.UTF8);
            reader.ReadToEnd().Should().Be("done");
        }

        // Both lifetime promises have settled and the dispatch is long over, so the event is no longer active
        // — which is the specification's own note about calling waitUntil() from a later task.
        var failure = Assert.Throws<JavaScriptException>(() => engine.Evaluate("captured.waitUntil(Promise.resolve())"))!;
        failure.Error.Get("name").AsString().Should().Be("InvalidStateError");
        failure.Message.Should().Contain("no longer active");
    }

    [Test]
    public void WaitUntilWorkIsJustJobsTheHostPumps()
    {
        var engine = Worker("""
            var log = [];
            addEventListener('fetch', event => {
                event.waitUntil(Promise.resolve().then(() => { log.push('background'); }));
                event.respondWith(new Response('immediate'));
            });
            """);

        Answer(engine).Should().Be("immediate");

        // Nothing waited for it and nothing had to: it is a job like any other, and the pump the response
        // needed ran it.
        engine.Evaluate("log.join(',')").AsString().Should().Be("background");
    }

    [Test]
    public void ARejectedWaitUntilPromiseIsReportedOnceRatherThanLost()
    {
        var sink = new RecordingSink();
        var engine = Worker("""
            var log = [];
            addEventListener('unhandledrejection', e => { log.push('event:' + e.reason.message); });
            addEventListener('fetch', event => {
                event.waitUntil(Promise.resolve().then(() => { throw new Error('background failed'); }));
                event.respondWith(new Response('ok'));
            });
            """, sink);

        Answer(engine).Should().Be("ok");

        // The promise was pending and unhandled when waitUntil() took it, so the reaction the event installs
        // is what marks it handled — and without this report the failure would vanish entirely, because the
        // rejection tracker would never have seen an unhandled promise.
        engine.Evaluate("log.join(',')").AsString().Should().Be("event:background failed");

        var reports = sink.Reports.Where(report => report.Kind == DiagnosticEventKind.UnhandledPromiseRejection).ToArray();
        reports.Should().HaveCount(1, "the rejection is announced once, at the settle");
        reports[0].RejectionHandled.Should().BeFalse();
        reports[0].Value.AsObject().Get("message").AsString().Should().Be("background failed");
    }

    [Test]
    public void AnAlreadyRejectedWaitUntilPromiseIsNotAnnouncedAtAll()
    {
        var sink = new RecordingSink();
        var engine = Worker("""
            var log = [];
            addEventListener('unhandledrejection', e => { log.push('unhandled:' + e.reason.message); });
            addEventListener('rejectionhandled', () => { log.push('handled'); });
            addEventListener('fetch', event => {
                event.waitUntil(Promise.reject(new Error('already')));
                event.respondWith(new Response('ok'));
            });
            """, sink);

        Answer(engine).Should().Be("ok");

        // waitUntil() attaches its reaction one statement after Promise.reject() built the promise, which is
        // inside the same microtask checkpoint — so HTML's deferred notification finds it handled and
        // announces nothing. The failure is not lost: the event's own extend-lifetime promise is what
        // reports it, which is the sibling test above.
        engine.Evaluate("log.join(',')").AsString().Should().Be("");
        sink.Reports.Should().BeEmpty();
    }

    [Test]
    public void ARejectedResponsePromiseIsNotAlsoReportedAsAnUnhandledRejection()
    {
        var sink = new RecordingSink();
        var engine = Worker("""
            var log = [];
            addEventListener('unhandledrejection', e => { log.push('event'); });
            addEventListener('fetch', event => {
                event.respondWith(Promise.resolve().then(() => { throw new Error('handler failed'); }));
            });
            """, sink);

        var operation = engine.WebApi.InvokeFetchHandler(Get());
        for (var i = 0; i < 100 && !operation.IsCompleted; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        // The host already learns about it, as the operation's failure — reporting it a second time as a lost
        // background rejection would be noise about something nobody lost.
        operation.IsFaulted.Should().BeTrue();
        operation.Error.Should().BeOfType<PromiseRejectedException>().Which
            .RejectedValue.AsObject().Get("message").AsString().Should().Be("handler failed");

        engine.Evaluate("log.join(',')").AsString().Should().BeEmpty();
        sink.Reports.Should().NotContain(report => report.Kind == DiagnosticEventKind.UnhandledPromiseRejection);
    }

    [Test]
    public void TheListenerFormAlwaysNeedsAtLeastOneTurn()
    {
        var engine = Worker("addEventListener('fetch', e => e.respondWith(new Response('sync')));");

        var operation = engine.WebApi.InvokeFetchHandler(Get());

        // Unlike a SetFetchHandler handler that returns a Response, respondWith() puts its argument through
        // PromiseResolve, and a promise reaction is a job — so even the most synchronous listener needs a pump.
        operation.IsCompleted.Should().BeFalse();

        engine.Tasks.ProcessTasks();
        operation.IsCompleted.Should().BeTrue();
    }

    [Test]
    public void ARestoreDropsTheListenersWithTheRestOfTheCycle()
    {
        var engine = Worker("addEventListener('fetch', e => e.respondWith(new Response('before')));");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        Answer(engine).Should().Be("before");

        // The listener list is a closure over the ended cycle, so the restore takes it away with everything
        // else — and the invocation is then refused, which is the same answer an engine that never had one
        // gives.
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        Assert.Throws<InvalidOperationException>(() => engine.WebApi.InvokeFetchHandler(Get()))!
            .Message.Should().Contain("addEventListener('fetch'");
    }
}
#endif
