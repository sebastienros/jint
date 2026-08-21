#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.Fetch;
using Jint.WebApi.FetchEvents;
using Jint.WebApi.GlobalEvents;
using Jint.WebApi.Messaging;
using Jint.WebApi.Streams;
using Jint.WebApi;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;

namespace Jint;

public partial class Engine
{
    public partial class AdvancedOperations
    {
        /// <summary>
        /// The opt-in web APIs this engine carries, after the feature closure has been expanded. Requires
        /// .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what the engine actually has, which is deliberately not the same thing as
        /// <c>options.WebApi.Features</c>: that one reads back exactly what the host asked for, while a
        /// feature whose surface is built out of another's interfaces brings that one with it. An engine built
        /// with <c>options.UseFetch()</c> answers <c>Fetch | Events | Url | Files | Streams</c> here and
        /// <c>Fetch</c> there.
        /// </para>
        /// <para>
        /// Reading it is what makes <see cref="EnableWebApis(WebApiFeatures, Action{Options.WebApiOptions})"/>
        /// unnecessary to guard: that method is additive and a feature already present is a no-op, so a host
        /// only needs this to decide whether a call is <i>worth</i> making, or to assert what a pooled engine
        /// came back with.
        /// </para>
        /// </remarks>
        public WebApiFeatures WebApiFeatures => _engine._webApiFeatures;

        /// <summary>
        /// Turns on the web APIs a host normally wants — <see cref="WebApiFeatures.Default"/> — on an engine
        /// that already exists. The per-engine counterpart of
        /// <see cref="WebApiOptionsExtensions.UseWebApis(Options)"/>. Requires .NET 8 or higher.
        /// </summary>
        /// <returns>The features this call added; see the main overload.</returns>
        public WebApiFeatures EnableWebApis() => EnableWebApis(Jint.WebApiFeatures.Default, configure: null);

        /// <summary>
        /// Turns the named web APIs on for <em>this</em> engine, after it has been constructed. The per-engine
        /// counterpart of <see cref="WebApiOptionsExtensions.UseWebApis(Options, WebApiFeatures)"/>, for the
        /// host that only discovers per request which APIs the script it is about to run needs — a pooled
        /// engine in a tenant host, a plugin runner — and would otherwise have to over-provision every engine
        /// or build a fresh one. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Additive only.</b> There is no way to turn a web API off again: a script that has already
        /// captured <c>fetch</c> keeps it whatever a later call says, so an API promising removal could not
        /// keep the promise. Naming a feature the engine already carries is a plain no-op — not an error, and
        /// not a re-install: the globals it owns keep their identity, an unread one stays unread, and
        /// <paramref name="configure"/> does not even run. The return value says what actually changed.
        /// </para>
        /// <para>
        /// <b>It runs the same code construction runs.</b> The feature closure is expanded by the one function
        /// that expands it at options time, so <see cref="WebApiFeatures.Fetch"/> brings
        /// <see cref="WebApiFeatures.Events"/>, <see cref="WebApiFeatures.Url"/>,
        /// <see cref="WebApiFeatures.Files"/> and <see cref="WebApiFeatures.Streams"/> here exactly as it does
        /// there; the per-engine state is created, or extended with the queues the new features need; and each
        /// global is installed as the same lazy, <b>non-clobbering</b> property — a name the host already owns
        /// is left exactly as the host left it, and the check probes rather than reads, so a host's own lazy
        /// global is not materialized merely by our looking. No closure ever pulls in a network feature:
        /// <see cref="WebApiFeatures.Fetch"/>, <see cref="WebApiFeatures.EventSource"/> and
        /// <see cref="WebApiFeatures.WebSocket"/> are only ever enabled by being named.
        /// </para>
        /// <para>
        /// <b>Warmed inline caches are invalidated.</b> Unlike a registration applied during construction this
        /// one lands on an engine whose handler trees may already hold a resolved binding for one of these
        /// names — a script that ran <c>typeof fetch</c> before the call. Every global goes in through the
        /// storage path that bumps the global object's own-property version, which is the sole thing the
        /// global-identifier and member-read inline caches revalidate against, so the next read of that name
        /// re-resolves. This is the same mechanism, and the same guarantee,
        /// <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, Runtime.Descriptors.PropertyFlag)"/> rests on.
        /// </para>
        /// <para>
        /// <b>What the settings come from.</b> The per-feature options are read from this engine's own
        /// <see cref="Options"/> — <c>options.WebApi.Fetch</c>, <c>.Storage</c>, <c>.Cache</c>,
        /// <c>.Timers</c> — at the moment of this call, with the same read-once semantics they have at
        /// construction, and <paramref name="configure"/> is handed that same group so a host can supply what
        /// it could not know earlier. Two settings are deliberately <b>not</b> re-read for an engine that
        /// already has web-API state: <c>Timers.TimeProvider</c>, because the timers,
        /// <c>performance.now()</c> and the time origin must stay on one clock for the engine's whole life;
        /// and <c>Diagnostics.Sink</c>, whose contract is that it holds still for an engine's lifetime because
        /// it also decides whether a callback's exception erupts. An engine that had no web-API state at all
        /// gets both read here, exactly as construction would have read them — including a time origin that is
        /// therefore <em>now</em> rather than the engine's birth.
        /// </para>
        /// <para>
        /// <b>Interaction with <see cref="CaptureGlobalSnapshot"/>.</b> A restore returns the global bindings
        /// to their state at capture, so globals installed by a call made <em>after</em> a capture are gone
        /// after the restore — the same thing that happens to an
        /// <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, Runtime.Descriptors.PropertyFlag)"/> global and to the ones
        /// <see cref="SetFetchHandler"/> installs. What a restore does not revert is the engine-side record:
        /// <see cref="WebApiFeatures"/> still reports the feature, and the state behind it (the timer queue,
        /// the cache provider) is still there, so the engine is left knowing about an API whose globals script
        /// can no longer name. <b>Enable after the restore, or enable on the options</b> so the globals are
        /// part of the engine's initial state and every snapshot carries them. Re-calling this method after a
        /// restore does not put them back either — the feature is already on, so the call is a no-op; a host
        /// that must reinstate them captures its snapshot after the enable.
        /// </para>
        /// <para>
        /// <b>The principal realm only</b>, exactly as at options time: a <c>ShadowRealm</c> carries none of
        /// these globals, and this method does not change that however it is reached.
        /// </para>
        /// <para>
        /// <b>When to call it.</b> Between evaluations, under the engine's usual single-thread contract; this
        /// is an ordinary mutation of the engine's global object. Calling it during an evaluation is not
        /// prevented, but a read already in flight may have resolved the old binding.
        /// </para>
        /// </remarks>
        /// <example>
        /// A pooled engine that learns per request what the script needs:
        /// <code>
        /// var engine = pool.Rent();                       // built with WebApiFeatures.Console only
        /// if (script.NeedsTimers)
        /// {
        ///     engine.Advanced.EnableWebApis(WebApiFeatures.Timers | WebApiFeatures.Events);
        /// }
        ///
        /// if (tenant.MayReachTheNetwork)
        /// {
        ///     engine.Advanced.EnableWebApis(WebApiFeatures.Fetch, w =>
        ///         w.Fetch.UrlFilter = uri => tenant.Allows(uri));
        /// }
        /// </code>
        /// </example>
        /// <param name="features">
        /// The features to turn on, in addition to those already present.
        /// <see cref="WebApiFeatures.None"/> does nothing and, in particular, disables nothing.
        /// </param>
        /// <param name="configure">
        /// Optional configuration of the per-feature settings, run once — before anything is installed, and
        /// only when this call is actually enabling something. It is handed this engine's own
        /// <c>Options.WebApi</c> group, which is the group the engine reads its settings from; <b>an
        /// <see cref="Options"/> instance shared with other engines is shared here too</b>, exactly as it
        /// already is at options time, so give an engine its own <see cref="Options"/> when its network policy
        /// has to be its own. Assigning <c>Features</c> inside the delegate does not enable anything for this
        /// call — <paramref name="features"/> is what this call enables — and only affects engines built from
        /// those options later.
        /// </param>
        /// <returns>
        /// The features this call actually added, after closure expansion, or
        /// <see cref="WebApiFeatures.None"/> when everything asked for was already present. A host granting
        /// network access can assert on it; a host that only wants the globals can ignore it.
        /// </returns>
        public WebApiFeatures EnableWebApis(WebApiFeatures features, Action<Options.WebApiOptions>? configure = null)
            => WebApiRegistration.ApplyLive(_engine, features, configure);

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
        /// is over; the restore additionally <i>closes</i> every port that engine still has, so the other end
        /// stops queueing messages into a channel side nothing can ever drain. A pooled engine wants a fresh
        /// pair per cycle.
        /// </description></item>
        /// <item><description>
        /// <b>A port can be transferred over a port.</b> <c>postMessage(value, [somePort])</c> detaches
        /// <c>somePort</c> and re-entangles its channel side with a fresh <c>MessagePort</c> in the receiving
        /// engine's realm, which arrives as <c>event.ports[0]</c> — messages already queued on it included,
        /// ahead of anything posted afterwards. The peer is untouched, so a port relayed through several
        /// engines still talks to the one that created it.
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
        /// Whether a fetch handler is registered on this engine <b>through <see cref="SetFetchHandler"/></b>.
        /// Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// It is deliberately about that registration alone, and answers <see langword="false"/> for an engine
        /// whose script registered a <c>fetch</c> listener instead (<see cref="WebApiFeatures.FetchEvents"/>) —
        /// it is the host's own record of what the host did, and a script must not be able to change the
        /// answer. It is therefore <b>not</b> the way to ask "can this engine serve a request": listeners come
        /// and go with the evaluation cycle, a script may add one after this was read, and one that exists may
        /// still decline to respond. Call <see cref="InvokeFetchHandler(HttpRequestMessage)"/> and let it
        /// fail — every other failure of an invocation arrives that way too.
        /// </remarks>
        public bool HasFetchHandler => _engine._webApi?.FetchHandler is not null;

        /// <summary>
        /// Registers the script function inbound requests are routed to by
        /// <see cref="InvokeFetchHandler(HttpRequestMessage)"/> and <see cref="InvokeFetchHandlerAsync"/>,
        /// turning the engine into a request handler in the style Cloudflare Workers established. Requires
        /// .NET 8 or higher.
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
        /// <para>
        /// <b>It outranks the script's own <c>fetch</c> listeners.</b> On an engine with
        /// <see cref="WebApiFeatures.FetchEvents"/> a script may register a handler itself with
        /// <c>addEventListener('fetch', …)</c>, and <see cref="InvokeFetchHandler(HttpRequestMessage)"/>
        /// dispatches to those listeners <em>only</em> when nothing was registered here. A handler the host named is never taken
        /// away from it by script — clear it with <see langword="null"/> to hand the route over deliberately.
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
        /// Routes an inbound request to the registered fetch handler — or, failing that, to the script's own
        /// <c>fetch</c> listeners — and hands back an operation the host drives with its own pump. Requires
        /// .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Two ways to be routed, in this order.</b> A handler registered with
        /// <see cref="SetFetchHandler"/> is called with the <c>Request</c>. With no such handler, and only on
        /// an engine carrying <see cref="WebApiFeatures.FetchEvents"/>, a trusted <c>FetchEvent</c> is
        /// dispatched at the global scope and the operation settles from whatever a listener passed to
        /// <c>event.respondWith()</c> — the shape a Cloudflare Workers or service-worker script is written in.
        /// The order is fixed and not a fallback chain a script can climb: what the host registered wins, and
        /// listeners are consulted only where there was nothing to win against. A dispatch that reaches no
        /// <c>respondWith()</c> — because no listener called it, or because the ones that ran threw — fails the
        /// operation, since an embedded engine has no network for a request to fall through to.
        /// </para>
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
        /// <exception cref="InvalidOperationException">
        /// Neither a fetch handler nor a <c>fetch</c> listener is registered on this engine.
        /// </exception>
        public FetchHandlerOperation InvokeFetchHandler(HttpRequestMessage request)
            => InvokeFetchHandler(request, CancellationToken.None);

        /// <summary>
        /// The same invocation, with the host's own "this request has been abandoned" token wired to the
        /// <c>Request</c>'s <c>signal</c>. Requires .NET 8 or higher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything <see cref="InvokeFetchHandler(HttpRequestMessage)"/> says holds here unchanged; this
        /// overload adds one thing, which is that <c>request.signal</c> is a real signal rather than one that
        /// can never fire. <c>HttpContext.RequestAborted</c> is the token this exists for.
        /// </para>
        /// <para>
        /// <b>The abort lands on the engine's thread, on a pump.</b> Cancelling a
        /// <see cref="CancellationTokenSource"/> runs its registrations on the cancelling thread, and aborting
        /// a signal dispatches a JavaScript <c>abort</c> event, which is script — so the token registration
        /// only enqueues an event-loop job and the abort happens when that job runs. It is the same contract
        /// <see cref="CreateAbortSignal"/>, <c>setTimeout</c> and <c>AbortSignal.timeout()</c> have, and the
        /// bridge is literally the same one: a host that cancels and then stops pumping has a signal that
        /// never aborted. A token that is <b>already</b> cancelled when this is called needs no job — this
        /// method runs on the engine's thread — so the handler is called with a request whose signal is
        /// already aborted, which is what lets it refuse without doing any work.
        /// </para>
        /// <para>
        /// <b>The abort is observational and settles nothing.</b> It does not complete or fail this operation,
        /// and it does not stop the handler: what it does is tell the script, which then rejects (an outbound
        /// <c>fetch</c> chained on <c>request.signal</c> rejects with the abort reason, and so does a handler
        /// that checks <c>signal.aborted</c> itself), and that rejection fails the operation through the
        /// ordinary contract — a <see cref="PromiseRejectedException"/> whose value is the <c>AbortError</c>
        /// <c>DOMException</c>. A handler that ignores the abort and answers anyway is served, because the
        /// host that went on pumping is the one that decided to let it finish. Stopping an invocation
        /// outright is the host's own lever, not the script's: stop pumping, or end the cycle with
        /// <see cref="RestoreGlobalSnapshot"/>.
        /// </para>
        /// <para>
        /// <b>It is not an execution constraint.</b> A handler that never yields — <c>while (true) {}</c> —
        /// never reaches a pump, so it never sees the abort; bounding the interpreter itself is what
        /// <c>Options.Constraints</c> is for, and
        /// <see cref="ConstraintsOptionsExtensions.CancellationToken"/> is the constraint that takes a token.
        /// The two compose: the constraint stops the run, this tells the script.
        /// </para>
        /// <para>
        /// <b>Lifetime.</b> The registration is released when this operation completes, however it completes,
        /// and again when <see cref="RestoreGlobalSnapshot"/> or <see cref="Engine.Dispose"/> releases every
        /// host bridge — so a long-lived token accumulates nothing across the requests a pooled engine serves,
        /// and retains no finished engine. What it deliberately does not do is chase an abort already on the
        /// event loop: a token that fired before the handler answered still aborts the signal on the next
        /// pump, which is what cancels the outbound work an abandoned handler had started.
        /// </para>
        /// </remarks>
        /// <param name="request">The inbound request; see <see cref="InvokeFetchHandler(HttpRequestMessage)"/>.</param>
        /// <param name="requestAborted">
        /// The host's "the client is gone" token. <see cref="CancellationToken.None"/> — and any other token
        /// that can never be cancelled — gives exactly the invocation the single-argument overload gives, and
        /// registers nothing.
        /// </param>
        /// <returns>The invocation in progress; see <see cref="FetchHandlerOperation"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Neither a fetch handler nor a <c>fetch</c> listener is registered on this engine.
        /// </exception>
        public FetchHandlerOperation InvokeFetchHandler(HttpRequestMessage request, CancellationToken requestAborted)
        {
            if (request is null)
            {
                Throw.ArgumentNullException(nameof(request));
            }

            var route = RequireFetchRoute();

            // Before the invocation, so that the evaluation cycle the operation is fenced against is the one
            // the script actually ran in — a RestoreGlobalSnapshot from inside a listener must abandon this
            // operation rather than be invisible to it.
            var operation = new FetchHandlerOperation(_engine);

            JsValue result;
            try
            {
                // Before the body is read, so that a token cancelled while a still-arriving content is being
                // pulled off the socket has somewhere to land, and an already-cancelled one produces a signal
                // the handler sees as aborted from its very first statement.
                var signal = CreateHostBridgedSignal(requestAborted, out var bridge);
                operation.AttachHostAbortBridge(bridge);

                result = InvokeRoute(in route, request, ReadBody(request), signal);
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
        /// but a host with a thread <em>affinity</em> wants
        /// <see cref="InvokeFetchHandler(HttpRequestMessage)"/> and its own pump.
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
        /// <b><paramref name="cancellationToken"/> is also wired to the <c>Request</c>'s <c>signal</c>.</b>
        /// This is an addition to what an existing parameter does, made deliberately rather than by adding a
        /// second one: a single token on a request invocation means "this request has been abandoned", which
        /// is exactly what <c>HttpContext.RequestAborted</c> means and exactly what
        /// <c>request.signal</c> is for, and a host holding one token for the two halves of one request would
        /// have nothing to put in a second parameter. Everything
        /// <see cref="InvokeFetchHandler(HttpRequestMessage, CancellationToken)"/> documents about that wiring
        /// holds here — the abort lands on a pump, it settles nothing by itself, an already-cancelled token
        /// gives a handler a request whose signal is already aborted, and the registration is released when
        /// this call returns however it returns.
        /// </para>
        /// <para>
        /// <b>One consequence is specific to this shape, and is why a host that wants a graceful answer on
        /// abort should prefer the polled one.</b> The token ends the <c>await</c> as well as aborting the
        /// signal, and the two are not ordered against each other: cancelling mid-flight normally ends this
        /// call with an <see cref="OperationCanceledException"/> before the abort job has had a turn, so the
        /// handler never gets to observe the abort and answer. The job is still there, and the abort lands on
        /// whatever pump the engine is given next — which is what cancels the outbound <c>fetch</c> the
        /// abandoned handler had in flight — but its response has nowhere left to go. With
        /// <see cref="InvokeFetchHandler(HttpRequestMessage, CancellationToken)"/> the host decides when to
        /// stop pumping, so a handler can be given the turn in which to notice and answer.
        /// </para>
        /// <para>
        /// The wait is additionally bounded by <c>Options.Constraints.PromiseTimeout</c> (ten seconds by
        /// default), exactly as an <c>await</c> of <see cref="EvaluateAsync(string, string, CancellationToken)"/>
        /// is: a handler whose promise never settles fails with a <see cref="PromiseRejectedException"/> naming
        /// the timeout rather than hanging the request forever.
        /// </para>
        /// </remarks>
        /// <param name="request">
        /// The inbound request; see <see cref="InvokeFetchHandler(HttpRequestMessage)"/> for what it must carry.
        /// </param>
        /// <param name="cancellationToken">
        /// The host's "the client is gone" token: it bounds the body read and the await, and it is what the
        /// <c>Request</c>'s <c>signal</c> aborts from. See the remarks — the signal half is a behaviour this
        /// parameter gained rather than one it always had.
        /// </param>
        /// <returns>The response the handler produced. The caller owns it and disposes it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Neither a fetch handler nor a <c>fetch</c> listener is registered, no listener answered the
        /// dispatched event with <c>respondWith()</c>, the request cannot be expressed as a <c>Request</c>, or
        /// the handler answered with something that is not a <c>Response</c>.
        /// </exception>
        /// <exception cref="JavaScriptException">The handler threw.</exception>
        /// <exception cref="PromiseRejectedException">The handler's promise rejected.</exception>
        public async Task<HttpResponseMessage> InvokeFetchHandlerAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                Throw.ArgumentNullException(nameof(request));
            }

            var route = RequireFetchRoute();

            var content = request.Content;
            var body = content is null
                ? null
                : await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            // After the body read rather than before it: the read observes the token itself and throws, so a
            // signal built ahead of it would be one nothing could ever release. From here on the engine's
            // single-thread contract puts us on the engine's thread, which is what building a signal needs.
            var signal = CreateHostBridgedSignal(cancellationToken, out var bridge);
            try
            {
                var result = InvokeRoute(in route, request, body, signal);
                var settled = await _engine.UnwrapResultAsync(result, cancellationToken).ConfigureAwait(false);
                return FetchHandlerHosting.CreateResponse(settled);
            }
            finally
            {
                // However this call ends — answered, thrown, cancelled — the host's registration goes back.
                // An abort already enqueued is not chased; see HostAbortSignalBridge.Release.
                bridge?.Release();
            }
        }

        /// <summary>
        /// Runs whichever of the two routes <see cref="RequireFetchRoute"/> chose, and answers with the value
        /// the operation settles from.
        /// </summary>
        private JsValue InvokeRoute(in FetchRoute route, HttpRequestMessage request, byte[]? body, JsAbortSignal signal)
        {
            return route.Handler is { } handler
                ? CallHandler(handler, request, body, signal)
                : DispatchFetchEvent(route.Listeners!, request, body, signal);
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
        private JsValue CallHandler(FetchHandler handler, HttpRequestMessage request, byte[]? body, JsAbortSignal signal)
        {
            var jsRequest = FetchHandlerHosting.CreateRequest(_engine, _engine._mainRealm, request, body, signal);
            return _engine.Call(handler.Callable, handler.ThisObject, [jsRequest]);
        }

        /// <summary>
        /// The script-facing route: build the same <c>Request</c> the handler route builds — the same
        /// host-bridged <c>signal</c> included, so a Workers-shaped script reading
        /// <c>event.request.signal</c> is told the truth about the client — fire a trusted <c>fetch</c> event
        /// at the global scope, and answer with the promise a listener responded with.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The dispatch is bracketed in <see cref="Engine.ExecuteWithConstraints{T}"/> — which is literally
        /// what <see cref="Engine.Call(JsValue, JsValue, JsValue[])"/> does for the handler route — so the
        /// whole dispatch is <b>one</b> entry into the engine and therefore one constraint budget, however many
        /// listeners run. Bracketing each listener instead would hand every one of them a fresh
        /// <c>MaxStatements</c> allowance and a freshly armed timeout, which is the trap the constraints
        /// section of the README warns hosts about.
        /// </para>
        /// <para>
        /// What a throwing listener does is the <c>EventTarget</c> contract unchanged: with a
        /// <see cref="DiagnosticsSink"/> it is reported and the dispatch carries on to the next listener, so a
        /// later one may still answer; with no sink the <c>JavaScriptException</c> propagates out of here and
        /// the caller turns it into the operation's failure. A constraint is neither — it is a
        /// <c>JintException</c> that is not a <c>JavaScriptException</c>, so it erupts past the sink either
        /// way and, again, becomes the operation's failure rather than a promise that never settles.
        /// </para>
        /// <para>
        /// One consequence is worth stating on its own, because it is the one place the two configurations
        /// disagree about a request that <i>was</i> answered: a listener that calls <c>respondWith()</c> and
        /// <b>then</b> throws serves its response on an engine with a sink — the throw is reported and the
        /// dispatch finishes normally, which is what a browser does — and fails the operation on an engine
        /// without one, where the exception is the only thing that can carry the failure anywhere at all. That
        /// follows from the sink contract rather than from anything decided here: with nowhere to report to,
        /// preferring the response would lose the exception entirely.
        /// </para>
        /// </remarks>
        private JsPromise DispatchFetchEvent(GlobalEventTarget listeners, HttpRequestMessage request, byte[]? body, JsAbortSignal signal)
        {
            var realm = _engine._mainRealm;
            var jsRequest = FetchHandlerHosting.CreateRequest(_engine, realm, request, body, signal);
            var fetchEvent = realm.Intrinsics.FetchEvent.CreateTrustedFetchEvent(jsRequest);

            _engine.ExecuteWithConstraints(_engine.Options.Strict, () =>
            {
                listeners.DispatchEvent(fetchEvent);
                return JsValue.Undefined;
            });

            if (fetchEvent.ResponsePromise is not { } response)
            {
                Throw.InvalidOperationException("The 'fetch' event was dispatched, but no listener called event.respondWith(...), so there is nothing to answer the request with. Call event.respondWith(new Response(...)) synchronously from the listener — an embedded engine has no network for an unanswered request to fall through to.");
                return null!;
            }

            return response;
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

        /// <summary>
        /// Which of the two registration forms this invocation goes to. Exactly one of the two is set.
        /// </summary>
        /// <param name="Handler">The handler <see cref="SetFetchHandler"/> registered, if there is one.</param>
        /// <param name="Listeners">
        /// The synthetic global event target to dispatch a <c>FetchEvent</c> at, when there is no handler and
        /// the script registered a <c>fetch</c> listener instead.
        /// </param>
        [StructLayout(LayoutKind.Auto)]
        private readonly record struct FetchRoute(FetchHandler? Handler, GlobalEventTarget? Listeners);

        /// <summary>
        /// Decides where an inbound request goes, and refuses the invocation when nothing can take it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The registered handler is looked at first and wins outright.</b> A script that also registered a
        /// <c>fetch</c> listener does not get to intercept requests the host had already routed somewhere:
        /// that would let evaluated script take over the engine's request handling by adding one line, which is
        /// not a decision a script may make for its host.
        /// </para>
        /// <para>
        /// <b>An engine with neither pays two reads.</b> The feature flag is tested before the listener list is
        /// even asked for, and the list is read through
        /// <c>WebApiEngineState.GlobalEventTargetIfCreated</c> — a plain field — so an engine that never
        /// enabled the feature, and one whose script never called <c>addEventListener</c>, both answer without
        /// allocating anything. There is nothing to race here: listeners are registered by script, and script
        /// runs on the engine's thread, which is the thread this is being called on.
        /// </para>
        /// </remarks>
        private FetchRoute RequireFetchRoute()
        {
            var state = RequireFetchModel();

            if (state.FetchHandler is { } handler)
            {
                return new FetchRoute(handler, null);
            }

            if ((_engine._webApiFeatures & WebApiFeatures.FetchEvents) != WebApiFeatures.None
                && state.GlobalEventTargetIfCreated is { } listeners
                && listeners.HasListenerOfType(FetchEventNames.FetchName))
            {
                return new FetchRoute(null, listeners);
            }

            Throw.InvalidOperationException("No fetch handler is registered on this engine, and no script listener is registered for the 'fetch' event. Register a handler with engine.Advanced.SetFetchHandler — a function, an object with a callable 'fetch' property, or the namespace of a module whose default export is one of those — or build the engine with WebApiFeatures.FetchEvents and let the script register one with addEventListener('fetch', event => event.respondWith(...)).");
            return default;
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

            // The engine's own record rather than the options': Options is shareable and mutable, so the set
            // an engine was actually built with is only knowable from the engine — and it is the set AFTER the
            // feature closure, so an engine built with options.UseFetch() (which implies Events) and one that
            // enabled Events through EnableWebApis are both recognized. The state is built for every feature
            // that keeps any, Events among them, so the null check is the belt to that brace.
            if (engine._webApi is null || (engine._webApiFeatures & WebApiFeatures.Events) == WebApiFeatures.None)
            {
                Throw.InvalidOperationException(
                    "Engine.Advanced.CreateAbortSignal requires the WebApiFeatures.Events web API, which this engine did not enable. Build the engine with options.UseWebApis(WebApiFeatures.Events) — or any feature set that includes it, such as WebApiFeatures.Default.");
            }

            // The bridge this hands back is nobody's to release here: a signal a host asked for by itself has
            // no operation to end, so its registration lives until the cycle does — which is exactly what the
            // remarks above promise, and what ResetTransientState and Dispose deliver.
            return CreateHostBridgedSignal(cancellationToken, out _);
        }

        /// <summary>
        /// Builds an <c>AbortSignal</c> in the principal realm and, when the token can still be cancelled,
        /// the <see cref="HostAbortSignalBridge"/> that aborts it. Shared by <see cref="CreateAbortSignal"/>
        /// and by the inbound-request signal a fetch-handler invocation is given, so that there is exactly one
        /// implementation of "a host token becomes a signal".
        /// </summary>
        /// <remarks>
        /// <para>
        /// The principal realm, deliberately, and not <see cref="Engine.Realm"/>: that one follows the running
        /// execution context and would hand back a <c>ShadowRealm</c>'s intrinsics when called from inside
        /// one. A host bridging its own cancellation means the engine's own realm, which is the only one these
        /// APIs are installed in.
        /// </para>
        /// <para>
        /// Both callers have already established that this engine carries
        /// <see cref="WebApiFeatures.Events"/> — <see cref="CreateAbortSignal"/> by testing it, the invocation
        /// through <see cref="RequireFetchModel"/> — which is one of the features
        /// <c>WebApiRegistration</c> builds the per-engine state for, so <c>_webApi</c> is not null here.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">The host's token.</param>
        /// <param name="bridge">
        /// The registration, so a caller that owns a bounded operation can release it when that operation
        /// ends; <see langword="null"/> when there is nothing to release, which is a token that can never be
        /// cancelled and one that already has been.
        /// </param>
        private JsAbortSignal CreateHostBridgedSignal(CancellationToken cancellationToken, out HostAbortSignalBridge? bridge)
        {
            var engine = _engine;
            var constructor = engine._mainRealm.Intrinsics.AbortSignal;
            var signal = constructor.CreateSignal();
            bridge = null;

            if (cancellationToken.IsCancellationRequested)
            {
                // Safe without a job: this runs on the engine's thread, because a host calling into the engine
                // is the engine's thread by definition.
                signal.SignalAbort(constructor.DefaultedReason(JsValue.Undefined));
                return signal;
            }

            if (cancellationToken.CanBeCanceled)
            {
                bridge = new HostAbortSignalBridge(engine, constructor, signal, cancellationToken);
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
