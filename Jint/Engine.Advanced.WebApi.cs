#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.Fetch;
using Jint.WebApi.Messaging;
using Jint.WebApi.Streams;
using Jint.WebApi;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Jint;

public partial class Engine
{
    public partial class AdvancedOperations
    {
        /// <summary>
        /// Creates a pair of entangled <c>MessagePort</c> objects spanning this engine and
        /// <paramref name="other"/>, so a script on one can <c>postMessage</c> to a script on the other.
        /// Requires .NET 8 or higher, and <see cref="WebApiFeatures.Messaging"/> on <b>both</b> engines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the cross-engine form of <c>new MessageChannel()</c>. Each port is an ordinary
        /// <c>MessagePort</c> for its own engine; hand each half to the engine that owns it and the two
        /// scripts talk to each other:
        /// </para>
        /// <code>
        /// var pair = worker.Advanced.CreateMessagePortPair(host);
        /// worker.SetValue("hostPort", pair.Local);
        /// host.SetValue("workerPort", pair.Remote);
        /// </code>
        /// <para>
        /// <b>Threading — read this before using it across threads.</b>
        /// </para>
        /// <list type="number">
        /// <item><description>
        /// <b>Call this while neither engine is running.</b> It builds one port on each engine, which
        /// materializes that engine's <c>MessagePort</c> intrinsics — engine mutation like any other, and
        /// therefore not something to do to an engine another thread is currently executing. The natural place
        /// is host setup, before either engine has been handed a script.
        /// </description></item>
        /// <item><description>
        /// <b>Publish each half to its own engine's thread with your own synchronization.</b> A
        /// <c>JsValue</c> belongs to the engine that created it and may only be touched on that engine's
        /// thread; this method hands you two values belonging to two engines, and getting each one to the
        /// right thread is yours to arrange. <c>engine.SetValue(...)</c> called on that engine's thread is the
        /// usual way, and is itself an ordinary engine mutation with the ordinary rules.
        /// </description></item>
        /// <item><description>
        /// <b>After that, each engine only ever touches its own port.</b> <c>postMessage</c> serializes the
        /// message on the calling engine's thread into a record that holds nothing belonging to any engine,
        /// and enqueues a delivery job on the receiving engine's event loop — the same door a promise settle
        /// arriving from a background thread comes through. The job runs on whichever thread pumps that
        /// engine, and the deserialization, the <c>MessageEvent</c> and the listeners all happen there.
        /// <b>Nothing on the receiving engine is touched off its own pump.</b>
        /// </description></item>
        /// <item><description>
        /// <b>The receiving engine must actually be pumped.</b> A message is delivered on the receiver's next
        /// event-loop drain — the end of an <c>Execute</c>/<c>Evaluate</c>, a blocking
        /// <c>UnwrapIfPromise</c>, an <c>await</c> of <c>EvaluateAsync</c>, or the host's own
        /// <c>engine.Advanced.ProcessTasks()</c> loop. An engine nobody pumps never delivers a message, for
        /// the same reason it never fires a timer: Jint does not start threads.
        /// </description></item>
        /// <item><description>
        /// <b>A <c>RestoreGlobalSnapshot</c> on either engine ends the channel permanently.</b> A port's
        /// listeners are closures over the evaluation cycle it was created in, so delivering into it
        /// afterwards would run that dead cycle's code against the restored globals. Both ports record their
        /// engine's evaluation cycle when they are created, and every delivery job is dropped once that cycle
        /// is over. A pooled engine wants a fresh pair per cycle.
        /// </description></item>
        /// </list>
        /// <para>
        /// Passing this engine itself is allowed and gives a same-engine channel, which is exactly what
        /// <c>new MessageChannel()</c> produces.
        /// </para>
        /// </remarks>
        /// <param name="other">The engine that owns the other end of the channel.</param>
        /// <returns>The two ports, one for each engine.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Either engine was built without <see cref="WebApiFeatures.Messaging"/>. Channel messaging is opt-in
        /// like every other web API, and a host reaching for this method on an engine that did not ask for it
        /// is told so rather than handed a port whose interface object the script cannot even see.
        /// </exception>
        public MessagePortPair CreateMessagePortPair(Engine other)
        {
            if (other is null)
            {
                Throw.ArgumentNullException(nameof(other));
            }

            RequireMessaging(_engine, "this engine");
            RequireMessaging(other, "the engine passed to CreateMessagePortPair");

            var (local, remote) = MessagePortBridge.CreatePair(_engine, _engine.Realm, other, other.Realm);
            return new MessagePortPair(local, remote);
        }

        private static void RequireMessaging(Engine engine, string which)
        {
            if ((engine._webApiFeatures & WebApiFeatures.Messaging) == WebApiFeatures.None)
            {
                Throw.InvalidOperationException(
                    $"CreateMessagePortPair requires WebApiFeatures.Messaging, and {which} was built without it. Enable it with options.UseWebApis(WebApiFeatures.Messaging).");
            }
        }

        /// <summary>
        /// The web APIs the fetch object model is built out of, and therefore the ones fetch-handler hosting
        /// needs an engine to carry. <see cref="WebApiFeatures.Fetch"/> implies all three, so an engine built
        /// with <c>options.UseFetch()</c> satisfies this too.
        /// </summary>
        private const WebApiFeatures FetchModelFeatures = WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;

        /// <summary>
        /// Whether a fetch handler is registered on this engine. Requires .NET 8 or higher.
        /// </summary>
        public bool HasFetchHandler => _engine._webApi?.FetchHandler is not null;

        /// <summary>
        /// Registers the script function inbound requests are routed to by
        /// <see cref="InvokeFetchHandler"/> and <see cref="InvokeFetchHandlerAsync"/>, turning the engine into
        /// a request handler in the style Cloudflare Workers established. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The three shapes accepted</b>, in the order they are tried:
        /// <list type="number">
        /// <item>a <b>callable</b> — used as the handler, called with <c>this</c> undefined. This is the shape
        /// for a script that has no modules: <c>engine.Execute("function handle(request) { … }")</c> followed
        /// by <c>SetFetchHandler(engine.GetValue("handle"))</c>.</item>
        /// <item>an <b>object with a callable <c>fetch</c> property</b> — called with that object as
        /// <c>this</c>, so a handler written as a method can reach its siblings.</item>
        /// <item>an object with a <b><c>default</c></b> property matching either of the two above — which is
        /// what a module namespace is, so the Workers convention
        /// <c>export default { fetch(request) { … } }</c> is registered with
        /// <c>SetFetchHandler(engine.Modules.Import("./worker.js"))</c> and nothing else.</item>
        /// </list>
        /// The value is resolved <em>here</em>, once, rather than on every request: a host that passed
        /// something unusable learns about it at this call rather than on the first request, and the property
        /// reads happen at a point the host chose.
        /// </para>
        /// <para>
        /// <b>Only the request is passed.</b> A Workers handler receives <c>(request, env, ctx)</c>; this one
        /// receives the request alone. Per-request host state reaches the script the way it always has — a
        /// global installed with <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, Runtime.Descriptors.PropertyFlag)"/>, or
        /// <see cref="HostDefined"/> read by a host function the script calls.
        /// </para>
        /// <para>
        /// <b>What this installs.</b> A handler that cannot build a <c>Response</c> is useless, so registering
        /// one also installs the <c>Headers</c>, <c>Request</c> and <c>Response</c> globals if they are not
        /// there already — lazily, and without replacing anything the host or the
        /// <see cref="WebApiFeatures.Fetch"/> feature already put under those names. It deliberately does
        /// <b>not</b> install <c>fetch</c>: handling an inbound request is not a reason to grant the script
        /// outbound network access, which stays behind <c>options.UseFetch()</c>. Since the install happens
        /// after construction, a <see cref="CaptureGlobalSnapshot"/> taken before this call does not carry
        /// those globals and <see cref="RestoreGlobalSnapshot"/> removes them again — register the handler
        /// after the restore, or enable the feature on the options so the globals are part of the engine's
        /// initial state.
        /// </para>
        /// <para>
        /// <b>Lifetime.</b> The handler is host state, like <see cref="HostDefined"/>: it survives
        /// <see cref="RestoreGlobalSnapshot"/> rather than being cleared by it, so a pooled engine that
        /// restores its globals between requests keeps the handler and decides for itself when to replace it.
        /// An invocation that was already in flight is a different matter and is fenced off — see
        /// <see cref="FetchHandlerOperation.IsCompleted"/>. Registering again replaces the previous handler;
        /// passing <see langword="null"/>, <c>undefined</c> or <c>null</c> clears it.
        /// </para>
        /// </remarks>
        /// <param name="handler">The handler, or <see langword="null"/> to clear it.</param>
        /// <exception cref="ArgumentException"><paramref name="handler"/> matches none of the three shapes.</exception>
        /// <exception cref="InvalidOperationException">
        /// The engine was not built with the web APIs the fetch object model needs.
        /// </exception>
        public void SetFetchHandler(JsValue? handler)
        {
            var state = RequireFetchModel();

            if (handler is null || handler.IsNullOrUndefined())
            {
                state.FetchHandler = null;
                return;
            }

            state.FetchHandler = FetchHandler.Resolve(handler, nameof(handler));

            // After the resolution, so that a host that passed something unusable has not had its global
            // object changed by the attempt.
            WebApiRegistration.InstallFetchModel(_engine);
        }

        /// <summary>
        /// Routes an inbound request to the registered fetch handler and hands back an operation the host
        /// drives with its own pump. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing is pumped here.</b> A handler that answers a <c>Response</c> synchronously produces an
        /// operation that is already complete; one that answers a promise — every <c>async</c> handler, and
        /// any handler that awaits a timer or an outbound <c>fetch</c> — needs turns, which the host gives it
        /// by calling <see cref="ProcessTasks"/> until
        /// <see cref="FetchHandlerOperation.IsCompleted"/>. That is the whole point of the shape: every turn
        /// provably runs on the thread the host decided, which is what a game loop or a UI thread needs.
        /// <see cref="InvokeFetchHandlerAsync"/> is the <c>await</c>-shaped alternative.
        /// </para>
        /// <para>
        /// <b>The request body is read on this thread, in full, before the handler runs.</b> Bodies are
        /// buffered rather than streamed here — Jint has no <c>ReadableStream</c> — so a
        /// <see cref="HttpContent"/> reading from a live socket blocks the caller until it has all arrived.
        /// Buffer it yourself (a <see cref="ByteArrayContent"/>) or use
        /// <see cref="InvokeFetchHandlerAsync"/>, which reads it without blocking.
        /// </para>
        /// <para>
        /// <b>Every failure of the invocation arrives through the operation</b>, even the ones that happen
        /// before this method returns: the request being unrepresentable as a <c>Request</c>, the handler
        /// throwing, an execution constraint firing. A host written to the
        /// poll-then-<see cref="FetchHandlerOperation.GetResult"/> pattern must not also have to guard the
        /// start call, and a constraint reported this way has still done its work — it ended the run, and the
        /// host learns of it as an exception the script can neither see nor catch. Only the host's own
        /// mistakes are thrown from here, because there is no invocation for them to be part of: a
        /// <see langword="null"/> request, and no handler being registered.
        /// </para>
        /// <para>
        /// <b>The handler runs under this engine's execution constraints</b>, like every other host entry into
        /// the engine: <c>MaxStatements</c>, <c>TimeoutInterval</c>, <c>LimitMemory</c> and any custom
        /// <c>Constraint</c> bound one invocation. Read the constraints section of the README before relying
        /// on that: the budget is per entry into the engine, so the turns the host pumps afterwards each get
        /// their own — a wall-clock bound over the <em>whole</em> request wants
        /// <c>Jint.Constraints.OperationDeadlineConstraint</c>, which the host brackets around the pump.
        /// </para>
        /// </remarks>
        /// <param name="request">
        /// The inbound request. Its <see cref="HttpRequestMessage.RequestUri"/> must be absolute, because a
        /// <c>Request</c>'s URL is — an embedded engine has no document to resolve a relative one against.
        /// </param>
        /// <returns>The invocation in progress; see <see cref="FetchHandlerOperation"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">No fetch handler is registered on this engine.</exception>
        public FetchHandlerOperation InvokeFetchHandler(HttpRequestMessage request)
        {
            if (request is null)
            {
                Throw.ArgumentNullException(nameof(request));
            }

            var handler = RequireFetchHandler();
            var operation = new FetchHandlerOperation(_engine);

            JsValue result;
            try
            {
                result = CallHandler(handler, request, ReadBody(request));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                operation.Fail(ex);
                return operation;
            }

            operation.Settle(result);
            return operation;
        }

        /// <summary>
        /// Routes an inbound request to the registered fetch handler and awaits its response, without holding
        /// a thread while the handler's promise is outstanding. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Which thread runs what.</b> The body is read first; with an already-buffered
        /// <see cref="HttpContent"/> — a <see cref="ByteArrayContent"/>, which is what an ASP.NET Core host
        /// hands over — that read completes synchronously and the handler is invoked on the calling thread.
        /// With a content still arriving off a socket it does not, and the handler runs on whichever thread
        /// resumed the read; every continuation afterwards likewise runs on whichever thread resumed the
        /// await. The engine is single-threaded and this does not change that — the turns are serialized —
        /// but a host with a thread <em>affinity</em> wants <see cref="InvokeFetchHandler"/> and its own pump.
        /// </para>
        /// <para>
        /// <paramref name="cancellationToken"/> does <b>not</b> preempt the synchronous evaluation loop. The
        /// handler runs to completion first and the token is only observed afterwards, while awaiting the
        /// response promise — that is, at event-loop continuation boundaries. A handler that never yields
        /// (<c>while (true) { }</c>) is therefore not cancellable through this parameter. To bound the
        /// interpreter itself, register an execution constraint on the engine's <see cref="Options"/>:
        /// <see cref="ConstraintsOptionsExtensions.CancellationToken"/> for token-driven cancellation or
        /// <see cref="ConstraintsOptionsExtensions.TimeoutInterval"/> for a wall-clock bound. Both are
        /// amortizable, so neither disarms the interpreter's tight-loop lane.
        /// </para>
        /// <para>
        /// The wait is additionally bounded by <c>Options.Constraints.PromiseTimeout</c> (ten seconds by
        /// default), exactly as an <c>await</c> of <see cref="EvaluateAsync(string, string, CancellationToken)"/>
        /// is: a handler whose promise never settles fails with a <see cref="PromiseRejectedException"/> naming
        /// the timeout rather than hanging the request forever.
        /// </para>
        /// </remarks>
        /// <param name="request">The inbound request; see <see cref="InvokeFetchHandler"/> for what it must carry.</param>
        /// <param name="cancellationToken">Observed while awaiting the response promise; see the remarks.</param>
        /// <returns>The response the handler produced. The caller owns it and disposes it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// No fetch handler is registered, the request cannot be expressed as a <c>Request</c>, or the handler
        /// answered with something that is not a <c>Response</c>.
        /// </exception>
        /// <exception cref="JavaScriptException">The handler threw.</exception>
        /// <exception cref="PromiseRejectedException">The handler's promise rejected.</exception>
        public async Task<HttpResponseMessage> InvokeFetchHandlerAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                Throw.ArgumentNullException(nameof(request));
            }

            var handler = RequireFetchHandler();

            var content = request.Content;
            var body = content is null
                ? null
                : await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            var result = CallHandler(handler, request, body);
            var settled = await _engine.UnwrapResultAsync(result, cancellationToken).ConfigureAwait(false);
            return FetchHandlerHosting.CreateResponse(settled);
        }

        /// <summary>
        /// Builds the <c>Request</c> and calls the handler. The call goes through
        /// <see cref="Engine.Call(JsValue, JsValue, JsValue[])"/>, which is what brackets it in the engine's
        /// execution constraints the way every public entry is bracketed.
        /// </summary>
        /// <remarks>
        /// The request is built against the <b>principal</b> realm rather than the running one. This is a host
        /// entry, so the two are the same in every ordinary case; naming the principal realm is what keeps it
        /// true if a host ever invokes a handler from inside a <c>ShadowRealm</c> callback, whose intrinsics
        /// are a different set of objects and which deliberately carries none of these globals.
        /// </remarks>
        private JsValue CallHandler(FetchHandler handler, HttpRequestMessage request, byte[]? body)
        {
            var jsRequest = FetchHandlerHosting.CreateRequest(_engine, _engine._mainRealm, request, body);
            return _engine.Call(handler.Callable, handler.ThisObject, [jsRequest]);
        }

        /// <summary>
        /// Reads the request body on the calling thread. <see cref="HttpContent.ReadAsStream()"/> rather than
        /// a blocking wait on the asynchronous overload: it is a real synchronous API, so a buffered content —
        /// the ordinary case — is a copy out of memory rather than a task machine wrapped in a wait.
        /// </summary>
        private static byte[]? ReadBody(HttpRequestMessage request)
        {
            var content = request.Content;
            if (content is null)
            {
                return null;
            }

            using var stream = content.ReadAsStream();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        private FetchHandler RequireFetchHandler()
        {
            var handler = RequireFetchModel().FetchHandler;
            if (handler is null)
            {
                Throw.InvalidOperationException("No fetch handler is registered on this engine. Register one with engine.Advanced.SetFetchHandler — a function, an object with a callable 'fetch' property, or the namespace of a module whose default export is one of those.");
            }

            return handler;
        }

        private WebApiEngineState RequireFetchModel()
        {
            var state = _engine._webApi;
            if (state is null || (_engine._webApiFeatures & FetchModelFeatures) != FetchModelFeatures)
            {
                // Deliberately not the Fetch feature itself. Fetch grants the script outbound network access,
                // which handling an inbound request is no reason to want; what the object model actually needs
                // is the three features its own interfaces are built out of — a Request has an AbortSignal, its
                // URL is a WHATWG URL, and response.blob() answers with a Blob.
                Throw.InvalidOperationException("Fetch-handler hosting needs the web APIs the fetch object model is built out of. Build the engine with options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files) — or with options.UseFetch(), which implies all three and additionally grants the script outbound network access.");
            }

            return state;
        }

        /// <summary>
        /// Creates an <c>AbortSignal</c> that aborts when <paramref name="cancellationToken"/> is cancelled, so
        /// that a host's own cancellation reaches the script's <c>fetch</c>, <c>scheduler.postTask</c>,
        /// <c>addEventListener('abort', …)</c> and anything else that takes a signal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The bridge is one-way. Cancelling the token aborts the signal; nothing a script does to the signal
        /// can cancel the host's token, and there is deliberately no controller for it — a signal produced here
        /// is the host's to abort, and script's only to observe.
        /// </para>
        /// <para>
        /// <b>The abort is observed only while the engine is pumped.</b> Cancelling a
        /// <see cref="CancellationTokenSource"/> runs its registrations on the cancelling thread, and aborting
        /// a signal dispatches a JavaScript <c>abort</c> event — which is script, and Jint runs script on the
        /// thread that calls into it and on no other. So the registration this method installs never aborts
        /// anything itself: it enqueues an event-loop job, and the abort happens when that job runs. That is
        /// the same contract <c>setTimeout</c> and <c>AbortSignal.timeout()</c> have, and it means a host that
        /// cancels a token and never pumps again has a signal that stays unaborted. Pump — through
        /// <see cref="ProcessTasks"/>, a blocking <c>UnwrapIfPromise</c>, or an <c>EvaluateAsync</c> that is
        /// still running — for the cancellation to land.
        /// </para>
        /// <para>
        /// <b>A token that is already cancelled produces an already-aborted signal, synchronously.</b> There is
        /// no job and no pump involved: this method runs on the engine's own thread, which is the only thread
        /// that may abort a signal, and doing it here is what lets
        /// <c>fetch(url, { signal })</c> reject immediately instead of issuing a request it would have to
        /// cancel. The reason is the standard's default, a <c>DOMException</c> named <c>AbortError</c>, in both
        /// cases.
        /// </para>
        /// <para>
        /// <b>Lifetime.</b> The registration is released as soon as the abort lands, when
        /// <see cref="RestoreGlobalSnapshot"/> ends the evaluation cycle the signal was created in, and when
        /// the engine is disposed — so a long-lived host token (a web request's, an application lifetime's)
        /// does not accumulate registrations, and does not retain a finished engine. A signal created before a
        /// restore therefore never aborts into the restored engine, exactly as a timer scheduled before one
        /// never fires: it belongs to a cycle that has ended.
        /// </para>
        /// <para>
        /// The signal belongs to the engine's <b>principal</b> realm, like every other web API Jint installs; a
        /// <c>ShadowRealm</c> has none of them.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        ///
        /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        /// engine.SetValue("hostSignal", engine.Advanced.CreateAbortSignal(cts.Token));
        ///
        /// engine.Execute("hostSignal.addEventListener('abort', () => cleanUp())");
        /// </code>
        /// </example>
        /// <param name="cancellationToken">
        /// The host's token. <see cref="CancellationToken.None"/> — and any other token that can never be
        /// cancelled — yields a signal that can never abort, and registers nothing.
        /// </param>
        /// <returns>The <c>AbortSignal</c>, ready to hand to script.</returns>
        /// <exception cref="InvalidOperationException">
        /// This engine did not enable <see cref="WebApiFeatures.Events"/>, which is the feature
        /// <c>AbortSignal</c> belongs to.
        /// </exception>
        public JsValue CreateAbortSignal(CancellationToken cancellationToken)
        {
            var engine = _engine;

            // The state is built for every feature that keeps any, Events among them, so a null field settles
            // the question without asking the options — which matters because Options may be shared by other
            // engines being built right now, and its web-API group is allocated on first touch.
            if (engine._webApi is null || (engine.Options.WebApi.Features & WebApiFeatures.Events) == WebApiFeatures.None)
            {
                Throw.InvalidOperationException(
                    "Engine.Advanced.CreateAbortSignal requires the WebApiFeatures.Events web API, which this engine did not enable. Build the engine with options.UseWebApis(WebApiFeatures.Events) — or any feature set that includes it, such as WebApiFeatures.Default.");
            }

            // The principal realm, deliberately, and not Engine.Realm: that one follows the running execution
            // context and would hand back a ShadowRealm's intrinsics when called from inside one. A host
            // bridging its own cancellation means the engine's own realm, which is the only one these APIs are
            // installed in.
            var constructor = engine._mainRealm.Intrinsics.AbortSignal;
            var signal = constructor.CreateSignal();

            if (cancellationToken.IsCancellationRequested)
            {
                // Safe without a job: this runs on the engine's thread, because a host calling into the engine
                // is the engine's thread by definition.
                signal.SignalAbort(constructor.DefaultedReason(JsValue.Undefined));
                return signal;
            }

            if (cancellationToken.CanBeCanceled)
            {
                // The state exists because the Events feature was checked above, and that is one of the
                // features WebApiRegistration builds it for.
                var bridge = new HostAbortSignalBridge(engine, constructor, signal, cancellationToken);
                engine._webApi!.AddHostAbortBridge(bridge);
            }

            return signal;
        }

        /// <summary>
        /// Wraps a readable <see cref="Stream"/> as a <c>ReadableStream</c> a script can read, cancel, tee
        /// and pipe. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Threading.</b> Call this on the engine's thread, like every other engine API, and hand the
        /// result to script from that thread. From then on the split is fixed: the host's stream is read by
        /// the BCL's asynchronous I/O, on whichever thread that completes on, and each chunk is delivered to
        /// the engine as an event-loop job. <b>The engine must be given turns for the stream to make
        /// progress</b> — an <c>await</c> in script, <c>Engine.EvaluateAsync</c>, a blocking
        /// <c>UnwrapIfPromise</c>, or the host's own <c>engine.Advanced.ProcessTasks()</c> loop. An engine
        /// nobody pumps never reads a byte, which is the same contract the timers have.
        /// </para>
        /// <para>
        /// <b>The host must not touch the stream once it has handed it over</b> — not read it, not seek it,
        /// not dispose it — until the bridge is done with it, because a read is in flight whenever the script
        /// is asking for a chunk. Ownership comes back only when
        /// <see cref="HostReadableStreamOptions.LeaveOpen"/> says the host kept it, and even then only after
        /// the stream has been read to the end or cancelled.
        /// </para>
        /// <para>
        /// <b>Backpressure is the standard's.</b> Nothing is read until the stream's queue has room for it —
        /// see <see cref="HostReadableStreamOptions.HighWaterMark"/> — so a script that stops reading stops
        /// the host's stream being read too.
        /// </para>
        /// <para>
        /// <b>Failures.</b> A read that throws errors the stream with a <c>TypeError</c> whose message names
        /// the exception's type but not its text; the exception itself rides the error value and the host
        /// reads it with <see cref="JintException.TryGetClrException"/>. <c>stream.cancel()</c> stops reading
        /// and releases the host's stream. <c>Engine.Advanced.RestoreGlobalSnapshot</c> releases it too, and
        /// leaves the script's outstanding <c>read()</c> pending forever, which is the contract every
        /// completion registered before a restore has.
        /// </para>
        /// <para>
        /// The result carries the engine's own <c>ReadableStream</c> prototype whether or not
        /// <see cref="WebApiFeatures.Streams"/> installed the global, so all of its methods work either way;
        /// what needs the flag is a script saying <c>x instanceof ReadableStream</c>.
        /// </para>
        /// </remarks>
        /// <param name="source">The stream to read. Must be readable.</param>
        /// <param name="options">How to read it, or <see langword="null"/> for the defaults.</param>
        /// <returns>A <c>ReadableStream</c> whose chunks are <c>Uint8Array</c>s.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="source"/> cannot be read.</exception>
        public JsValue CreateReadableStream(Stream source, HostReadableStreamOptions? options = null)
        {
            if (source is null)
            {
                Throw.ArgumentNullException(nameof(source));
            }

            if (!source.CanRead)
            {
                Throw.ArgumentException("The stream cannot be read.", nameof(source));
            }

            return HostReadableStreamSource.Create(_engine, _engine.Realm, source, options ?? new HostReadableStreamOptions());
        }

        /// <summary>
        /// Wraps a writable <see cref="Stream"/> as a <c>WritableStream</c> a script can write, close, abort
        /// and pipe into. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Threading.</b> Call this on the engine's thread and hand the result to script from that thread.
        /// Each chunk is copied out of script's memory on the engine's thread and written by the BCL's
        /// asynchronous I/O; the write's completion comes back as an event-loop job, so <b>the engine must be
        /// given turns</b> for a write's promise — and therefore <c>writer.ready</c> and
        /// <c>writer.close()</c> — ever to settle.
        /// </para>
        /// <para>
        /// <b>The host must not touch the stream once it has handed it over</b>, for the same reason as the
        /// readable direction: a write may be in flight whenever the script has written a chunk.
        /// </para>
        /// <para>
        /// <b>What a script may write</b> is a <c>BufferSource</c>, a <c>Blob</c> or a string, which is UTF-8
        /// encoded. Anything else is a <c>TypeError</c> that errors the stream rather than being coerced —
        /// <c>[object Object]</c> silently appended to a host file is not a failure mode worth having.
        /// </para>
        /// <para>
        /// <b>Backpressure</b> is <c>writer.ready</c>, which stops being resolved once
        /// <see cref="HostWritableStreamOptions.HighWaterMark"/> chunks are queued.
        /// </para>
        /// <para>
        /// <b>Closing.</b> <c>await writer.close()</c> settles only once the host's stream has been flushed
        /// and — unless <see cref="HostWritableStreamOptions.LeaveOpen"/> says the host kept it — disposed,
        /// and rejects with whatever the flush failed with. So a script can prove its output landed.
        /// <c>abort()</c> and <c>Engine.Advanced.RestoreGlobalSnapshot</c> both release the stream without
        /// flushing.
        /// </para>
        /// </remarks>
        /// <param name="destination">The stream to write. Must be writable.</param>
        /// <param name="options">How to write it, or <see langword="null"/> for the defaults.</param>
        /// <returns>A <c>WritableStream</c> over <paramref name="destination"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="destination"/> cannot be written.</exception>
        public JsValue CreateWritableStream(Stream destination, HostWritableStreamOptions? options = null)
        {
            if (destination is null)
            {
                Throw.ArgumentNullException(nameof(destination));
            }

            if (!destination.CanWrite)
            {
                Throw.ArgumentException("The stream cannot be written.", nameof(destination));
            }

            return HostWritableStreamSink.Create(_engine, _engine.Realm, destination, options ?? new HostWritableStreamOptions());
        }

        /// <summary>
        /// Starts reading a script's <c>ReadableStream</c> into a host <see cref="Stream"/> and returns at
        /// once, without blocking and without a thread of its own. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The host pumps.</b> Every chunk comes out of the engine, so the copy advances only when the
        /// engine is given turns: call <c>engine.Advanced.ProcessTasks()</c> and watch
        /// <see cref="HostStreamCopyOperation.IsCompleted"/>. This is the one entry point of the three where
        /// every turn provably runs where the host wants it, which is what a game loop or a UI thread needs;
        /// <see cref="CopyReadableStreamAsync"/> is the same operation with the pumping done inside an
        /// <c>await</c>. Neither may be called from inside an event-loop job.
        /// </para>
        /// <para>
        /// <b>The source is locked</b> for the copy's duration, exactly as <c>pipeTo</c> locks it, and the
        /// reader is released however the copy ends. A copy that ends early also cancels the source unless
        /// <see cref="HostStreamCopyOptions.PreventCancel"/> says otherwise.
        /// </para>
        /// <para>
        /// <b>Backpressure</b> is one chunk in flight: the next <c>read()</c> is issued only once the
        /// previous chunk has reached the host's stream, so a script producing faster than the disk accepts
        /// feels it through its own stream's queue.
        /// </para>
        /// <para>
        /// <b>Failures</b> all arrive as <see cref="HostStreamCopyOperation.IsFaulted"/> and
        /// <see cref="HostStreamCopyOperation.Error"/> rather than as exceptions from this call: the source
        /// already being locked, the source erroring, a chunk that is not a byte sequence, a write that threw,
        /// a cancelled token (an <c>AbortError</c> <c>DOMException</c>), and a
        /// <c>RestoreGlobalSnapshot</c> that abandoned the cycle. The destination is released in every one of
        /// those cases, and flushed only in the successful one.
        /// </para>
        /// </remarks>
        /// <param name="readableStream">A <c>ReadableStream</c> this engine created.</param>
        /// <param name="destination">The stream to write it to. Must be writable.</param>
        /// <param name="options">How to copy, or <see langword="null"/> for the defaults.</param>
        /// <param name="cancellationToken">
        /// Stops the copy. May be cancelled from any thread; the copy ends on a later turn of the event loop,
        /// with an <c>AbortError</c> <c>DOMException</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="readableStream"/> is not a <c>ReadableStream</c>, or
        /// <paramref name="destination"/> cannot be written.
        /// </exception>
        public HostStreamCopyOperation StartReadableStreamCopy(
            JsValue readableStream,
            Stream destination,
            HostStreamCopyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (readableStream is null)
            {
                Throw.ArgumentNullException(nameof(readableStream));
            }

            if (destination is null)
            {
                Throw.ArgumentNullException(nameof(destination));
            }

            if (readableStream is not JsReadableStream source)
            {
                Throw.ArgumentException("The value is not a ReadableStream.", nameof(readableStream));
                return null!;
            }

            if (!destination.CanWrite)
            {
                Throw.ArgumentException("The stream cannot be written.", nameof(destination));
            }

            return HostStreamCopy.Start(
                _engine,
                _engine.Realm,
                source,
                destination,
                options ?? new HostStreamCopyOptions(),
                cancellationToken);
        }

        /// <summary>
        /// Reads a script's <c>ReadableStream</c> into a host <see cref="Stream"/>, driving the engine while
        /// it waits. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The engine is single-threaded and this does not change that: turns run one at a time, on whichever
        /// thread resumes the await. A host with a thread affinity — a game loop, a UI thread — wants
        /// <see cref="StartReadableStreamCopy"/> and its own pump instead, so that every turn runs where the
        /// host needs it to. Everything else about the copy is identical; see that method.
        /// </para>
        /// <para>
        /// <b>The wait is bounded by <c>Options.Constraints.PromiseTimeout</c></b>, which defaults to ten
        /// seconds — the same bound <c>Engine.EvaluateAsync</c> and <c>Engine.Modules.ImportAsync</c> carry.
        /// A copy that can legitimately take longer than that needs the host to raise it, or to poll
        /// <see cref="StartReadableStreamCopy"/> instead. The copy itself keeps running when the wait gives
        /// up.
        /// </para>
        /// </remarks>
        /// <returns>How many bytes reached <paramref name="destination"/>.</returns>
        /// <exception cref="PromiseRejectedException">
        /// The copy failed; see <see cref="HostStreamCopyOperation.Error"/> for what the value can be.
        /// </exception>
        public async Task<long> CopyReadableStreamAsync(
            JsValue readableStream,
            Stream destination,
            HostStreamCopyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var operation = StartReadableStreamCopy(readableStream, destination, options, cancellationToken);
            await _engine.UnwrapResultAsync(operation.Promise, CancellationToken.None).ConfigureAwait(false);
            return operation.BytesWritten;
        }
    }
}
#endif
