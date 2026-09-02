using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// One protocol domain, as a session sees it: a name, a dispatch entry, and the enable state clients expect
/// a domain to keep.
/// </summary>
/// <remarks>
/// <para>
/// Nothing derives from this directly. Every domain derives from the generated
/// <c>&lt;Domain&gt;DomainBase</c>, which fills in <see cref="Name"/> and <see cref="DispatchAsync"/> from
/// the pinned protocol and leaves one virtual per command; overriding a virtual is the only way a command
/// stops answering <c>-32601</c>.
/// </para>
/// <para>
/// Every member of a domain runs on the engine thread. A domain may hold engine state; it may not be
/// touched from a transport thread, and nothing it returns may outlive the command that produced it.
/// </para>
/// </remarks>
internal abstract class DevToolsDomain
{
    private DevToolsSession? _session;

    /// <summary>Gets the domain's name, which is the part of a method before the dot.</summary>
    internal abstract string Name { get; }

    /// <summary>
    /// Gets whether the client has enabled this domain. A domain that raises events raises none until it is.
    /// </summary>
    internal bool IsEnabled { get; private set; }

    /// <summary>Answers one of the domain's commands, by the part of its method after the dot.</summary>
    internal abstract ValueTask<string> DispatchAsync(string method, JsonElement? parameters, CommandContext context);

    /// <summary>Tells the domain which session its events go out on.</summary>
    internal void Attach(DevToolsSession session) => _session = session;

    /// <summary>
    /// Marks the domain enabled, running <see cref="OnEnabledAsync"/> the first time and nothing on a repeat.
    /// </summary>
    /// <remarks>
    /// Clients enable a domain more than once — Puppeteer and the DevTools front end both do — and the
    /// protocol's own domains treat the second call as a success rather than an error.
    /// </remarks>
    protected ValueTask MarkEnabledAsync(CommandContext context)
    {
        if (IsEnabled)
        {
            return default;
        }

        IsEnabled = true;
        return OnEnabledAsync(context);
    }

    /// <summary>Marks the domain disabled, running <see cref="OnDisabledAsync"/> if it was enabled.</summary>
    /// <remarks>
    /// Named <c>Mark…</c> rather than <c>Enable…</c> so that it does not read as an overload of the
    /// generated <c>EnableAsync(EmptyParameters, CommandContext)</c> a domain's <c>enable</c> command
    /// overrides. That override is what calls this.
    /// </remarks>
    protected ValueTask MarkDisabledAsync(CommandContext context)
    {
        if (!IsEnabled)
        {
            return default;
        }

        IsEnabled = false;
        return OnDisabledAsync(context);
    }

    /// <summary>Runs once, when the client first enables the domain.</summary>
    protected virtual ValueTask OnEnabledAsync(CommandContext context) => default;

    /// <summary>Runs when the client disables a domain it had enabled.</summary>
    protected virtual ValueTask OnDisabledAsync(CommandContext context) => default;

    /// <summary>
    /// Refuses the one evaluation parameter this package cannot honour: <c>throwOnSideEffect</c>.
    /// </summary>
    /// <remarks>
    /// The front end sends it for the console's eager evaluation — the grey preview that appears as you
    /// type — and it means "throw rather than run anything observable". Answering it would need a
    /// side-effect analysis of the interpreter, which does not exist; answering the evaluation anyway would
    /// run the very code the client asked not to be run. No recorded client sends it, so the refusal is the
    /// answer, and a front end that gets one simply shows no preview. It lives here because every command
    /// that evaluates carries the flag, and a domain that answered it differently would make one request mean
    /// two things.
    /// </remarks>
    private protected static void RefuseSideEffectFreeEvaluation(bool? throwOnSideEffect)
    {
        if (throwOnSideEffect == true)
        {
            Throw.ServerError(
                "Side-effect free evaluation is not supported",
                "the engine has no side-effect analysis, so an evaluation that must throw rather than run anything observable cannot be answered");
        }
    }

    /// <summary>Gets the session this domain is registered with, once one has registered it.</summary>
    /// <remarks>
    /// A domain raising an event outside a command — a target appearing while nobody asked anything — needs
    /// the session without a <see cref="CommandContext"/> to reach it through.
    /// </remarks>
    private protected DevToolsSession? Session => _session;

    /// <summary>Sends one event on the session this domain is registered with.</summary>
    /// <remarks>
    /// The event carries that session's own <c>sessionId</c>, so a domain never has to know which
    /// attachment it is part of, and an event raised outside a command is addressed exactly as one raised
    /// inside it.
    /// </remarks>
    protected ValueTask EmitAsync(in ProtocolEvent @event, CancellationToken cancellationToken = default)
    {
        return _session is { } session ? session.SendEventAsync(in @event, cancellationToken) : default;
    }

    /// <summary>Sends one event from a path that cannot wait for it: inside a script call on the engine thread.</summary>
    /// <remarks>
    /// <para>
    /// A binding a script invoked, a <c>console</c> call, an exception nobody caught: each of them reaches a
    /// domain synchronously, from inside the engine, where there is nothing to <c>await</c> onto and no
    /// caller to hand a failure to. Both connections queue rather than write — a send is a channel insertion
    /// — so this completes synchronously in practice; what it guarantees is only that the engine thread is
    /// never blocked and that a failure to write cannot erupt out of the host's pump.
    /// </para>
    /// <para>
    /// A domain that <i>is</i> answering a command emits with <see cref="EmitAsync"/> instead, so that the
    /// event is on the wire before the reply that follows it.
    /// </para>
    /// </remarks>
    protected void EmitDetached(in ProtocolEvent @event)
    {
        if (_session is not { } session)
        {
            return;
        }

        ValueTask pending;
        try
        {
            pending = session.SendEventAsync(in @event);
        }
#pragma warning disable CA1031 // this runs inside the engine; an escape here erupts out of the host's pump
        catch (Exception)
#pragma warning restore CA1031
        {
            return;
        }

        if (!pending.IsCompletedSuccessfully)
        {
            _ = Observe(pending);
        }

        static async Task Observe(ValueTask pending)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // an unobserved task exception is worse than a swallowed write failure
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }
}
