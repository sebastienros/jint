using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// A target whose engine nothing pumps unless a test says so, and which may name one method as answerable
/// off the engine thread.
/// </summary>
/// <remarks>
/// <para>
/// The suite declares one rather than waiting for <c>Jint.Browser</c> to bring one, for the reason
/// <see cref="NavigableTarget"/> exists: an extension point nothing exercises is a design nobody has tried.
/// This is the smallest thing that is a real target — an identity, an engine and a mailbox — and it is
/// deliberately <i>not</i> pumped, which is what makes "answered without the engine thread" an observation
/// rather than a claim about timing.
/// </para>
/// <para>
/// The method it names is <c>Log.enable</c>, which touches nothing of the engine's. That is the bar
/// <see cref="DevToolsTarget.RunsOffThread"/> sets, and a test target is not exempt from it.
/// </para>
/// </remarks>
internal sealed class UnpumpedTarget : DevToolsTarget
{
    private readonly string? _offThread;

    private int _closed;

    /// <summary>Creates a target over <paramref name="engine"/>, naming <paramref name="offThread"/>.</summary>
    /// <param name="engine">The engine the target answers about.</param>
    /// <param name="offThread">The one method answered on the reading thread, or <see langword="null"/>.</param>
    internal UnpumpedTarget(Engine engine, string? offThread = null)
        : base(
            type: "page",
            title: "Unpumped target",
            url: "about:blank",
            browserContextId: null,
            openerId: null,
            describer: null,
            waitForDebuggerOnStart: false)
    {
        _offThread = offThread;
        InstallRuntime(engine);
    }

    /// <summary>Gets how many turns of the mailbox a test has run.</summary>
    internal int Pumps { get; private set; }

    /// <inheritdoc/>
    internal override bool RunsOffThread(string method)
        => _offThread is not null && string.Equals(method, _offThread, StringComparison.Ordinal);

    /// <summary>Runs one turn of the mailbox, which is all a host's loop does for a command.</summary>
    internal void Pump()
    {
        Pumps++;
        Runtime.Dispatcher.Drain();
    }

    /// <inheritdoc/>
    internal override ValueTask CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            DisposeRuntime();
        }

        return default;
    }
}
