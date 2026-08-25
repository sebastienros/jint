using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// Installs a global on <em>this</em> engine whose value is produced the first time script reads it,
    /// instead of now. The per-engine counterpart of
    /// <see cref="OptionsExtensions.AddLazyGlobal(Options, string, Func{Engine, JsValue}, PropertyFlag)"/>,
    /// for the values a host cannot know until after the engine exists — per-request data, a scoped
    /// service provider, a workflow context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The property is installed eagerly, so existence checks and enumeration — <c>in</c>,
    /// <c>hasOwnProperty</c>, <c>Object.keys(globalThis)</c>, <c>Object.getOwnPropertyNames</c> — see it
    /// immediately without materializing anything. Only its value is left unresolved:
    /// <paramref name="valueFactory"/> runs on the first read of that value, and the produced value is
    /// stored in the descriptor so subsequent reads are ordinary property reads.
    /// <c>typeof</c> counts as a read, since it has to inspect the value to name its type. Once per
    /// engine, then — with the single exception of
    /// <see cref="AdvancedOperations.RestoreGlobalSnapshot"/>, which returns an unread global to its
    /// unmaterialized state on purpose, so the factory runs again on the next read. What that second run produces is the
    /// factory's business and not the restore's: one that constructs gives the next cycle a fresh value,
    /// one that hands back something it is holding gives the next cycle what the previous one mutated.
    /// </para>
    /// <para>
    /// A script that <b>deletes</b> the global before ever reading it (<c>delete globalThis.name</c>,
    /// requires <see cref="PropertyFlag.Configurable"/>) removes the descriptor outright, and the factory
    /// never runs. A script that <b>overwrites or redefines</b> it first does still run the factory once
    /// and then discard the result: <c>[[Set]]</c> on the global object funnels into
    /// <c>[[DefineOwnProperty]]</c>, whose ValidateAndApplyPropertyDescriptor step reads the current
    /// value before replacing it. The end state is correct in every case — the script's value wins — the
    /// laziness is simply not preserved through that particular sequence.
    /// </para>
    /// <para>
    /// <b>Differences from the <see cref="Options"/> registration.</b> That one is recorded on a
    /// configuration object which may be shared by any number of engines, so its factories must not
    /// capture anything engine-affine. This one belongs to one engine and receives it, so its factory
    /// <em>may</em> close over that engine's <see cref="JsValue"/>s and over per-request host state — the
    /// case the options-time API cannot express. It also replaces any existing global of that name,
    /// including a built-in, rather than being applied once during construction.
    /// </para>
    /// <para>
    /// <b>When to call it.</b> Any time between evaluations, under the engine's usual single-thread
    /// contract; an <see cref="Engine"/> is not thread-safe and this is an ordinary mutation of its
    /// global object. Installing during an evaluation — from a host callback the script invoked — is not
    /// prevented, but the read that is already in flight may have resolved the old binding.
    /// </para>
    /// <para>
    /// <b>Interaction with <see cref="AdvancedOperations.CaptureGlobalSnapshot"/>.</b> A restore returns the
    /// binding to its state at capture, which cuts both ways: a global installed after the capture is gone after the
    /// restore, and one installed before it whose factory had <em>not</em> yet run at capture time is
    /// returned to that unmaterialized state, so the factory <b>runs again</b> on the next read. That is
    /// a contract rather than an artifact — it is what lets a pooled engine keep the laziness across
    /// evaluations, and a host that reuses engines depends on it to stop one request's value being
    /// served to the next. A factory whose result must survive a restore has to be installed after it.
    /// </para>
    /// </remarks>
    /// <example>
    /// The per-request shape the <see cref="Options"/> registration cannot express, since the values are
    /// only known once the request is in hand:
    /// <code>
    /// engine.AddLazyGlobal("user", _ => JsValue.FromObject(engine, request.User));
    /// engine.AddLazyGlobal("db", e => ObjectWrapper.Create(e, scope.ServiceProvider.GetRequiredService&lt;IDb&gt;()));
    /// </code>
    /// Neither the user projection nor the database wrapper is built for a script that never mentions
    /// the name.
    /// </example>
    /// <param name="name">The global property name.</param>
    /// <param name="valueFactory">
    /// Produces the value, given this engine. Invoked lazily, so it may use anything the engine exposes.
    /// A <see langword="null"/> return is replaced by <see cref="JsValue.Undefined"/>, so that it cannot
    /// silently turn into a factory that re-runs on every read.
    /// </param>
    /// <param name="flags">
    /// Property attributes; defaults to the configurable/enumerable/writable combination that
    /// <see cref="Engine.SetValue(string, JsValue)"/> produces — <b>not</b> the
    /// <see cref="PropertyFlag.NonEnumerable"/> that <see cref="Engine.SetValue(string, Delegate)"/> uses.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or
    /// <paramref name="valueFactory"/> is <see langword="null"/>.</exception>
    public void AddLazyGlobal(
        string name,
        Func<Engine, JsValue> valueFactory,
        PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
    {
        using var ownership = EnterHostCall();
        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        if (valueFactory is null)
        {
            Throw.ArgumentNullException(nameof(valueFactory));
        }

        var engine = this;

        // SetProperty, exactly as the options-time registration uses, and for a reason that only bites
        // here: unlike a registration applied during construction, this one lands on an engine whose
        // handler trees may already hold a resolved binding for this very name. Every storage path
        // SetProperty can take — replacing a slot of the global's shared built-in layout, adding to the
        // hybrid side dictionary, plain dictionary store, and the deopt fallback — bumps
        // _propertiesVersion, which is the sole thing the global-identifier and member-read inline
        // caches revalidate against. Installing through anything that skipped that bump would leave a
        // warmed read site serving the previous binding forever.
        engine.Realm.GlobalObject.SetProperty(name, new LazyPropertyDescriptor<Engine>(engine, valueFactory, flags));
    }

    /// <summary>
    /// Declares a lazy global whose factory is handed a value the caller supplies, so that the factory can
    /// be a <see langword="static"/> lambda instead of a closure. Behaves in every other way exactly like
    /// <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, PropertyFlag)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload exists for allocation, not for expressiveness — anything it can express, a closure
    /// could. The case it serves is a host that installs its globals <em>per engine</em>, which is the
    /// whole reason the per-engine API exists: the values depend on request state, so the registration
    /// cannot be hoisted onto <see cref="Options"/> and runs again for every engine. A capturing lambda
    /// then costs a display class and a delegate per global per engine, which for a host exposing a few
    /// dozen globals is a per-request cost with nothing to show for it. Passing the state through instead
    /// leaves the descriptor as the only allocation.
    /// </para>
    /// <para>
    /// Use a value tuple to pass more than one thing:
    /// <c>AddLazyGlobal("db", (services, key), static (e, s) =&gt; Wrap(e, s.services, s.key))</c>. Keep
    /// the factory <see langword="static"/>; a lambda that still captures gives the allocation straight
    /// back and the overload buys nothing.
    /// </para>
    /// <para>
    /// There is deliberately no <see cref="Options"/> counterpart. A registration made there is recorded
    /// once and merely replayed per engine, so its closure is a one-off for the process and the
    /// per-engine cost is already just the descriptor.
    /// </para>
    /// </remarks>
    /// <typeparam name="TState">The type of the value handed back to the factory.</typeparam>
    /// <param name="name">The global property name.</param>
    /// <param name="state">Passed to <paramref name="valueFactory"/> unchanged when it runs.</param>
    /// <param name="valueFactory">
    /// Produces the value, given this engine and <paramref name="state"/>. Invoked lazily, once per
    /// materialization — so once, unless <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> re-arms the global. A
    /// <see langword="null"/> return is replaced by <see cref="JsValue.Undefined"/>.
    /// </param>
    /// <param name="flags">Property attributes; see the non-generic overload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or
    /// <paramref name="valueFactory"/> is <see langword="null"/>.</exception>
    public void AddLazyGlobal<TState>(
        string name,
        TState state,
        Func<Engine, TState, JsValue> valueFactory,
        PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
    {
        using var ownership = EnterHostCall();
        if (name is null)
        {
            Throw.ArgumentNullException(nameof(name));
        }

        if (valueFactory is null)
        {
            Throw.ArgumentNullException(nameof(valueFactory));
        }

        var engine = this;

        // The resolver is static, so it is cached once per TState instantiation rather than allocated
        // here, and the state rides inside the descriptor's own field. See the SetProperty note above for
        // why the installation has to go through it.
        engine.Realm.GlobalObject.SetProperty(
            name,
            new LazyPropertyDescriptor<EngineAndState<TState>>(
                new EngineAndState<TState>(engine, state, valueFactory),
                static s => s.Factory(s.Engine, s.State),
                flags));
    }

    /// <summary>
    /// The <c>[[HostDefined]]</c> field of this engine's principal Realm Record — the realm
    /// <c>InitializeHostDefinedRealm</c> created when the engine was constructed. The specification
    /// reserves it for "hosts that need to associate additional information with a Realm Record", and the
    /// engine never reads or interprets what is put there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The motivating case.</b> Every host-facing factory in this API receives the engine and nothing
    /// else. <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, PropertyFlag)"/> can close over
    /// per-request state because it runs on a live engine, but its
    /// <see cref="OptionsExtensions.AddLazyGlobal(Options, string, Func{Engine, JsValue}, PropertyFlag)"/>
    /// counterpart cannot: one <see cref="Options"/> instance is shared by every engine built from it, so a
    /// factory recorded there must not capture anything engine-affine. A host can sidestep that by building
    /// a fresh <see cref="Options"/> per scope and letting its factories close over it, which is supported
    /// and cheap — but it costs an <see cref="Options"/> per request and gives up the process-wide instance
    /// most embedders actually have. Keeping the shared instance used to leave only an out-of-band side
    /// table keyed by engine: real embedders keep a
    /// <c>static ConditionalWeakTable&lt;Engine, IServiceProvider&gt;</c> for precisely this, and pay its
    /// internal write lock on every evaluation of every tenant in the process.
    /// </para>
    /// <para>
    /// <b>Which realm.</b> The <em>principal</em> one, not the current one. <see cref="Engine.Intrinsics"/>
    /// and <see cref="Engine.Global"/> follow the running execution context's realm and therefore change
    /// identity inside a <c>ShadowRealm</c> evaluation, which is correct for them — they name the current
    /// Realm Record. This does not move: it answers the same value from inside a shadow-realm callback as
    /// outside, which is what a host attaching "the request this engine is serving" means.
    /// </para>
    /// <para>
    /// A shadow realm is a distinct Realm Record and gets its own <c>[[HostDefined]]</c>, empty by
    /// specification — <c>ShadowRealm</c> step 4 performs <c>InitializeHostDefinedRealm()</c>, whose new
    /// record defaults the field to <c>undefined</c>. That is deliberate isolation rather than an
    /// oversight: propagating the outer request's services into sandboxed code would be exactly the
    /// ambient authority a shadow realm exists to withhold. A host that does want to populate it has the
    /// hook the specification provides for it, <see cref="Runtime.Host.InitializeShadowRealm"/>.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> The principal realm is reachable only from the base execution context, which is
    /// pushed once and never popped, so the value dies with the engine — the difference from a
    /// process-wide side table, which keeps whatever bookkeeping its own entries need. Nothing in the
    /// engine clears it: <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> reverts global bindings,
    /// lexical declarations and transient per-evaluation state, and does not touch it, so an engine pooled across requests keeps
    /// its state through a restore and the host decides when to replace it — typically right after
    /// restoring. <see cref="Engine.Dispose"/> does not clear it either; it releases interop caches the
    /// engine built, not state the host attached.
    /// </para>
    /// <para>
    /// An <see cref="Engine"/> is not thread-safe and this is an ordinary reference field: set it from the
    /// thread that owns the engine, like everything else.
    /// </para>
    /// </remarks>
    /// <example>
    /// The shape the options-time registration cannot otherwise express — a process-wide
    /// <see cref="Options"/> whose factories capture nothing, with the per-request part supplied per engine:
    /// <code>
    /// // once per process
    /// var options = new Options().AddLazyGlobal("db", static e =>
    ///     JsValue.FromObject(e, ((IServiceProvider) e.HostDefined!).GetRequiredService&lt;IDb&gt;()));
    ///
    /// // once per request
    /// var engine = new Engine(options);
    /// engine.HostDefined = scope.ServiceProvider;
    /// </code>
    /// The ordering works because the factory is lazy: it runs on the first script read of <c>db</c>, which
    /// is necessarily after the constructor returned and the state was attached. An
    /// <see cref="OptionsExtensions.Configure"/> callback runs <em>during</em> construction, and can set
    /// this but must not expect a later request's value to be visible to it.
    /// <para>
    /// The engine stores the value verbatim, so the natural retrieval is a type pattern:
    /// <c>if (engine.HostDefined is RequestContext ctx)</c>.
    /// </para>
    /// </example>
    public object? HostDefined
    {
        get
        {
            using var ownership = EnterHostCall();
            return _mainRealm.HostDefined;
        }
        set
        {
            using var ownership = EnterHostCall();
            _mainRealm.HostDefined = value;
        }
    }
}
