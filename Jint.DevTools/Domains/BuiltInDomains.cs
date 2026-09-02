using Jint.DevTools.Protocol.Browser;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// The domains this package answers, named in one place.
/// </summary>
/// <remarks>
/// <para>
/// This file and <c>manifest.json</c>'s <c>implementedMethods</c> are two statements of the same fact, and
/// <c>Jint.Tests.DevTools/Protocol/ProtocolManifestTests.cs</c> holds them to each other: every listed
/// method is overridden here, and nothing else is. Adding a domain without a manifest entry, or the other
/// way round, fails rather than ships.
/// </para>
/// <para>
/// There are two lists rather than one because there are two kinds of session. A browser session answers
/// about the server — which engines exist, what the product is — and touches no engine, so its commands run
/// on the transport thread. A target session answers about one engine and every command of it crosses to
/// that engine's thread first.
/// </para>
/// <para>
/// <b>A target may register more than these.</b> A page target adds <c>Page</c>, <c>Emulation</c> and their
/// kind through <see cref="DevToolsTarget.RegisterDomains"/>, over the same session core; what this file
/// settles is the five every target has whatever it is.
/// </para>
/// </remarks>
internal static class BuiltInDomains
{
    /// <summary>Registers what a connection to the browser endpoint answers.</summary>
    /// <param name="session">The root session of that connection.</param>
    /// <param name="version">What <c>Browser.getVersion</c> answers.</param>
    /// <param name="closeRequested">What to run when a client sends <c>Browser.close</c>, if anything.</param>
    /// <param name="targets">The <c>Target</c> domain the conversation keeps its discovery state on.</param>
    internal static DevToolsSession RegisterBrowserDomains(
        DevToolsSession session,
        GetVersionResponse version,
        Action? closeRequested,
        TargetDomain targets)
    {
        if (session is null)
        {
            Throw.ArgumentNull(nameof(session));
        }

        return session
            .Register(new SchemaDomain())
            .Register(new BrowserDomain(version, closeRequested))
            .Register(targets);
    }

    /// <summary>Registers what one attachment to one target answers.</summary>
    /// <param name="session">The session node the attachment answers on.</param>
    /// <param name="target">The target it speaks to.</param>
    /// <param name="browser">
    /// The conversation the attachment belongs to, or <see langword="null"/> for a direct
    /// <c>/devtools/page/</c> connection, which has no browser session and therefore no target tree.
    /// </param>
    /// <returns>
    /// The domains that hold engine state, which is what an attachment releases when it detaches: a handle
    /// is a promise to keep a value alive, and the client it was promised to has gone.
    /// </returns>
    internal static TargetDomains RegisterTargetDomains(DevToolsSession session, DevToolsTarget target, BrowserSession? browser)
    {
        if (session is null)
        {
            Throw.ArgumentNull(nameof(session));
        }

        var runtime = new RuntimeDomain(target);
        var console = new ConsoleDomain(target);
        var log = new LogDomain();
        var debugger = new DebuggerDomain(target, log);
        var profiler = new ProfilerDomain(target);

        session.Register(runtime).Register(console).Register(log).Register(debugger).Register(profiler);

        // Registered with the engine's own event sources in one place, so that the domains that hear about a
        // console call, an uncaught exception, an unhandled rejection or the engine being replaced under the
        // target are exactly the ones the attachment releases again when it detaches.
        target.Observe(runtime);
        target.Observe(console);
        target.Observe(log);
        target.Observe(debugger);
        target.Observe(profiler);

        if (browser is not null)
        {
            // Clients walk down the target tree by sending setAutoAttach on every session they are given.
            // A target here has no children, so the nested copy answers it as the success it is.
            session.Register(new TargetDomain(browser, nested: true));
        }

        return new TargetDomains(target, runtime, console, log, debugger, profiler);
    }
}

/// <summary>
/// One domain of an attachment that holds state of the target behind it, so that detaching can give it back.
/// </summary>
/// <remarks>
/// What a target's own domains implement so that <see cref="TargetDomains.Detach"/> reaches them without
/// knowing what they are. The built-in five are released by name, because what each of them owns is
/// different and the order between two of them matters.
/// </remarks>
internal interface IDetachableDomain
{
    /// <summary>Releases what this attachment holds, from whichever thread noticed the client had gone.</summary>
    void Detach();
}

/// <summary>
/// The domains of one attachment that hold state of the target behind it, so that detaching can give it
/// back.
/// </summary>
/// <remarks>
/// A record struct rather than a lookup through <c>session.Domains</c>: what an attachment owns is settled
/// where it is registered, and a domain added to the registration without a member here is one whose state
/// nothing releases.
/// </remarks>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TargetDomains(
    DevToolsTarget Target,
    RuntimeDomain Runtime,
    ConsoleDomain Console,
    LogDomain Log,
    DebuggerDomain Debugger,
    ProfilerDomain Profiler)
{
    /// <summary>Gets whatever the target registered next to the built-in five, or <see langword="null"/>.</summary>
    /// <remarks>
    /// Every one of them is observed and released with the rest; the property exists so that a target's own
    /// domains are part of the attachment rather than a second lifetime nothing tracks.
    /// </remarks>
    internal IReadOnlyList<DevToolsDomain>? Extra { get; init; }

    /// <summary>Releases everything this attachment holds of its target, and stops it hearing anything.</summary>
    /// <remarks>
    /// <b>The debugger goes last, and that ordering is load-bearing.</b> Its release resumes an engine that
    /// is paused, and the engine thread comes straight back out of the pause loop into whatever it was
    /// running — so everything else this attachment held has to be gone before it does.
    /// </remarks>
    internal void Detach()
    {
        Target.Unobserve(Runtime);
        Target.Unobserve(Console);
        Target.Unobserve(Log);
        Target.Unobserve(Debugger);
        Target.Unobserve(Profiler);

        if (Extra is { } extra)
        {
            foreach (var domain in extra)
            {
                if (domain is ITargetObserver observer)
                {
                    Target.Unobserve(observer);
                }

                (domain as IDetachableDomain)?.Detach();
            }
        }

        Runtime.Detach();
        Profiler.Detach();
        Debugger.Detach();
    }
}
