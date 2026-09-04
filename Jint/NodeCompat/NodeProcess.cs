using System.Diagnostics;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.NodeCompat;

/// <summary>
/// Builds the opt-in Node-style <c>process</c> global.
/// <para>
/// https://nodejs.org/api/process.html
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The object is an ordinary <see cref="JsObject"/> with ordinary data properties, not a subclass and not an
/// <c>EventEmitter</c> as Node's own <c>process</c> is: nothing here needs to intercept a read, and building
/// it through <see cref="JsObject.CreateFromEntries(Engine, IEnumerable{KeyValuePair{string, JsValue}})"/>
/// puts it and <c>process.env</c> straight into the shaped representation, so a script reading
/// <c>process.env.NODE_ENV</c> in a loop keeps a monomorphic inline cache.
/// </para>
/// <para>
/// What is deliberately absent is as much of the design as what is present. There is no <c>exit</c>, no
/// <c>abort</c> and no <c>kill</c> — an embedded script must not be able to take the host process down, and
/// an absent function is a better answer than one that throws, because a script feature-detecting
/// <c>process.exit</c> can then take its other branch instead of walking into an exception. Nothing here
/// reaches the real process either: <c>argv</c> is empty rather than the host's command line, <c>cwd()</c>
/// answers a configured string rather than the real directory, and <c>env</c> carries only the variables the
/// host listed.
/// </para>
/// </remarks>
internal static class NodeProcess
{
    private const string GlobalName = "process";

    private const long NanosecondsPerSecond = 1_000_000_000;

    /// <summary>
    /// Jint's own assembly version, reported as <c>process.versions.jint</c>. Read once for the process.
    /// </summary>
    private static readonly string JintAssemblyVersion =
        typeof(Engine).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Installs the lazy <c>process</c> global, unless the global object already owns that name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The descriptor is a <see cref="LazyPropertyDescriptor{T}"/> installed through
    /// <c>GlobalObject.SetProperty</c>, which is what <c>Options.AddLazyGlobal</c> does and for its two
    /// reasons: <c>SetProperty</c> bumps the own-property version that every global-identifier and
    /// member-read inline cache revalidates against, and an unmaterialized lazy descriptor is a state
    /// <c>Engine.Advanced.RestoreGlobalSnapshot</c> can return to.
    /// </para>
    /// <para>
    /// Returning to that state is what actually rebuilds <c>process</c> for the next cycle — including
    /// whatever the previous cycle's script wrote into <c>process.env</c> — and the reason it works here is
    /// <see cref="Create"/>: the factory <i>constructs</i>, so <c>process</c> lives in the descriptor and
    /// nowhere else. That makes this the one global Jint installs which a restore genuinely makes fresh.
    /// Every web-API global, and every one of the 58 ECMAScript globals beside them, has a factory that
    /// reads a memoized realm intrinsic instead, so reverting their descriptor reverts a cache and the next
    /// read serves the previous cycle's object; <c>Jint.WebApi.WebApiRegistration</c> spells that out. Two
    /// conditions come with the freshness here: the snapshot has to have been captured <i>before</i>
    /// <c>process</c> was first read — a snapshot captured afterwards holds the object itself and reinstates
    /// it — and nothing may hold <c>process</c> across the restore and expect it to still be the global,
    /// because it will not be.
    /// </para>
    /// <para>
    /// Non-clobbering, like the web-API installs: a host that exposes its own <c>process</c> keeps it. The
    /// check probes rather than reads, so a host's own lazy global is not materialized merely by looking.
    /// Registration order does not change the outcome — a host global registered afterwards overwrites this
    /// descriptor, which has cost nothing but itself while its factory has not run.
    /// </para>
    /// </remarks>
    internal static void Install(Engine engine, NodeProcessConfiguration configuration)
    {
        var global = engine.Realm.GlobalObject;
        if (global.HasOwnProperty(JsString.Create(GlobalName)))
        {
            return;
        }

        // The state rides inside the descriptor rather than in a closure, so a host building engines per
        // request pays one descriptor per engine and nothing else.
        global.SetProperty(
            GlobalName,
            new LazyPropertyDescriptor<EngineAndState<NodeProcessConfiguration>>(
                new EngineAndState<NodeProcessConfiguration>(engine, configuration, static (e, c) => Create(e, c)),
                static s => s.Factory(s.Engine, s.State),
                PropertyFlag.ConfigurableEnumerableWritable));
    }

    private static JsObject Create(Engine engine, NodeProcessConfiguration configuration)
    {
        var realm = engine.Realm;

        // https://nodejs.org/api/process.html#processargv — "an array containing the command-line arguments
        // passed when the Node.js process was launched", whose first two entries are the executable and the
        // entry point. Jint is not a process launcher and the script it is running has no path of its own, so
        // there is nothing truthful to put in it; an empty array keeps `process.argv.slice(2)` — the one thing
        // scripts do with it — working and answering "no arguments".
        var argv = new JsArray(engine);

        // https://nodejs.org/api/process.html#processversions — "an object listing the version strings of
        // Node.js and its dependencies". There is no `node` key: this is not Node, and a script asking for one
        // deserves `undefined` rather than a number that would send it down a path Jint cannot honour.
        var versions = JsObject.CreateFromEntries(
            engine,
            [new KeyValuePair<string, JsValue>("jint", JsString.Create(JintAssemblyVersion))]);

        var hrtime = JsObject.CreateFromEntries(
            engine,
            [new KeyValuePair<string, JsValue>("bigint", Operation(engine, realm, "bigint", 0, HrtimeBigInt))]);

        var workingDirectory = JsString.Create(configuration.WorkingDirectory);

        return JsObject.CreateFromEntries(engine,
        [
            new KeyValuePair<string, JsValue>("platform", JsString.Create(configuration.Platform)),
            new KeyValuePair<string, JsValue>("version", JsString.Create(configuration.Version)),
            new KeyValuePair<string, JsValue>("versions", versions),
            new KeyValuePair<string, JsValue>("argv", argv),
            new KeyValuePair<string, JsValue>("env", CreateEnvironment(engine, configuration)),
            new KeyValuePair<string, JsValue>("cwd", Operation(engine, realm, "cwd", 0, (_, _) => workingDirectory)),
            new KeyValuePair<string, JsValue>("nextTick", Operation(engine, realm, "nextTick", 1, (_, arguments) => NextTick(engine, realm, arguments))),
            new KeyValuePair<string, JsValue>("hrtime", hrtime),
        ]);
    }

    /// <summary>
    /// One of <c>process</c>'s methods. The realm-pinned constructor, because these are built lazily: a
    /// shared-shape prototype can materialize a member while another realm is running, and the function must
    /// still belong to the realm that owns the object it came from. <c>length</c> counts the declared
    /// parameters and is configurable but neither writable nor enumerable, as an ordinary function's is.
    /// </summary>
    private static ClrFunction Operation(Engine engine, Realm realm, string name, int length, JsCallDelegate body)
        => new(engine, realm, name, body, length, PropertyFlag.Configurable);

    /// <summary>
    /// <c>process.env</c>: https://nodejs.org/api/process.html#processenv.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Materialized once, and never read again.</b> The object is built when <c>process</c> itself is
    /// first touched, from the allowed names' values at that instant; a variable that changes in the real
    /// environment afterwards does not change here. Node's <c>process.env</c> reads through to the operating
    /// system on every access, and the difference is deliberate: a live view would mean every property read a
    /// script performs turns into a native environment lookup, and the value a script read twice could differ
    /// for reasons nothing in the script explains.
    /// </para>
    /// <para>
    /// <b>Writes are script-local.</b> It is an ordinary object with ordinary writable properties, so
    /// <c>process.env.X = 'y'</c>, <c>delete process.env.X</c> and <c>Object.assign(process.env, …)</c> all
    /// work and none of them reaches
    /// <see cref="Environment.SetEnvironmentVariable(string, string)"/> — the script mutates its own copy.
    /// Node writes through to the process (its own documentation notes the value is coerced to a string on
    /// the way, which an ordinary object does not do either); letting a script rewrite the environment of the
    /// host it is embedded in is not something an embedder can want.
    /// </para>
    /// </remarks>
    private static JsObject CreateEnvironment(Engine engine, NodeProcessConfiguration configuration)
    {
        var allowlist = configuration.EnvironmentVariableAllowlist;
        if (allowlist.Length == 0)
        {
            return JsObject.CreateFromEntries(engine, ReadOnlySpan<KeyValuePair<string, JsValue>>.Empty);
        }

        var entries = new List<KeyValuePair<string, JsValue>>(allowlist.Length);
        foreach (var name in allowlist)
        {
            var value = configuration.ResolveEnvironmentVariable(name);
            if (value is not null)
            {
                entries.Add(new KeyValuePair<string, JsValue>(name, JsString.Create(value)));
            }
        }

        return JsObject.CreateFromEntries(engine, entries);
    }

    /// <summary>
    /// <c>process.nextTick(callback[, ...args])</c>:
    /// https://nodejs.org/api/process.html#processnexttickcallback-args — "adds the <c>callback</c> to the
    /// 'next tick queue'. Once the current phase of the event loop finishes, all callbacks currently in the
    /// next tick queue will be called", and "additional <c>args</c> passed after <c>callback</c> will be
    /// forwarded to the function when it is called".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Jint has one job queue and it is the microtask queue, so this is one enqueue onto it: the callback
    /// runs after everything already queued, before any timer is even considered, and on the engine's own
    /// thread when something pumps it. That satisfies "before the event loop continues" and the ordering
    /// against timers, and it differs from Node in the one place Node has two queues: there, the next-tick
    /// queue is drained ahead of the promise microtask queue, so a <c>nextTick</c> callback registered after a
    /// <c>Promise.resolve().then()</c> reaction still runs first. Here the two share a queue and run in
    /// registration order. Scripts that depend on that priority are rare and the alternative — a second queue
    /// in the engine's pump, on a hot path, for a Node compatibility shim — is not a trade worth making.
    /// </para>
    /// <para>
    /// The callback is invoked with <c>undefined</c> as its <c>this</c>, as Node's own
    /// <c>Reflect.apply(callback, undefined, args)</c> does.
    /// </para>
    /// </remarks>
    private static JsValue NextTick(Engine engine, Realm realm, JsCallArguments arguments)
    {
        if (arguments.At(0) is not ICallable callback)
        {
            // The shape of Node's ERR_INVALID_ARG_TYPE for this argument, which is a TypeError there too.
            Throw.TypeError(realm, "The \"callback\" argument must be of type function.");
            return JsValue.Undefined;
        }

        // Copied, because the caller's array belongs to the engine's argument pool and is reused as soon as
        // this call returns — the callback runs long after that.
        var extraArguments = Arguments.Slice(arguments, 1);

        // The current generation, since registering and enqueuing happen in one act here: there is no window
        // in which the cycle could have ended in between. A microtask, because Node drains the next-tick
        // queue *before* the microtask queue and both before returning to the loop: whatever else it is, it
        // is emphatically not a task.
        engine.AddToEventLoop(() => callback.Call(JsValue.Undefined, extraArguments), EventLoopJobKind.Microtask);
        return JsValue.Undefined;
    }

    /// <summary>
    /// <c>process.hrtime.bigint()</c>: https://nodejs.org/api/process.html#processhrtimebigint — "returns the
    /// current high-resolution real time in nanoseconds as a <c>bigint</c>".
    /// </summary>
    /// <remarks>
    /// <see cref="Stopwatch.GetTimestamp"/> is the monotonic clock the platform offers, so the difference
    /// between two of these readings is an elapsed time that no clock adjustment can make negative — which is
    /// the entire reason a script reaches for <c>hrtime</c> rather than <c>Date.now()</c>. Like Node's, the
    /// origin is arbitrary and the value means nothing on its own. Resolution is the platform's:
    /// <see cref="Stopwatch.Frequency"/> ticks per second scaled to nanoseconds, so successive readings may
    /// repeat, never decrease.
    /// <para>
    /// The legacy tuple form, <c>process.hrtime()</c>, is deliberately absent — <c>hrtime</c> is an object
    /// here, not a callable — so a script feature-detecting it takes its fallback branch instead of receiving
    /// a broken approximation.
    /// </para>
    /// </remarks>
    private static JsValue HrtimeBigInt(JsValue thisObject, JsCallArguments arguments)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var frequency = Stopwatch.Frequency;

        // Split rather than multiplying first: `timestamp * 1_000_000_000` overflows a long after about
        // fifteen minutes of uptime on a 10 MHz timer, while both terms below stay well inside it.
        var seconds = timestamp / frequency;
        var remainder = timestamp - seconds * frequency;
        return JsBigInt.Create(seconds * NanosecondsPerSecond + remainder * NanosecondsPerSecond / frequency);
    }
}
