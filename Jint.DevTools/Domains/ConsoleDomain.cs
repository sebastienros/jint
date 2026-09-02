using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Console;
using Jint.DevTools.Session;
using Jint.WebApi;

namespace Jint.DevTools.Domains;

/// <summary>
/// The legacy <c>Console</c> domain: the same calls the <c>Runtime</c> domain reports, as the flat lines a
/// simpler client reads.
/// </summary>
/// <remarks>
/// <para>
/// Deprecated in the protocol and implemented anyway, because it is what the small clients use.
/// <c>chrome-remote-interface</c>'s own samples listen to <c>Console.messageAdded</c> and never touch
/// <c>Runtime.consoleAPICalled</c>, and a <c>ConsoleMessage</c> is one string with a level on it rather than
/// a list of remote objects — which is the whole of what a log-scraping client wants.
/// </para>
/// <para>
/// It reports what the engine's own printer produced, group indentation included, and mints no handles: a
/// client on this domain asked for text. The calls that print nothing — <c>groupEnd</c>, a <c>time</c> that
/// started a timer — are absent from it for the same reason, while <c>Runtime.consoleAPICalled</c> carries
/// them because a front end draws a group from them.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Console/"/>.
/// </para>
/// </remarks>
internal sealed class ConsoleDomain : ConsoleDomainBase, ITargetObserver
{
    private readonly DevToolsTarget _target;

    internal ConsoleDomain(DevToolsTarget target)
    {
        _target = target;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>Empties the journal, which is the same history <c>Runtime.discardConsoleEntries</c> discards.</summary>
    protected override ValueTask<EmptyResult> ClearMessagesAsync(EmptyParameters parameters, CommandContext context)
    {
        _target.Runtime.Console.Clear(_target.Runtime.RemoteObjects);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Sends what was logged before the client asked, which is what the command promises.</summary>
    protected override async ValueTask OnEnabledAsync(CommandContext context)
    {
        foreach (var entry in _target.Runtime.Console.Snapshot())
        {
            if (Message(entry) is { } message)
            {
                await EmitAsync(ConsoleEvents.MessageAdded(message), context.CancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    void ITargetObserver.ConsoleRecorded(ConsoleEntry entry)
    {
        if (!IsEnabled || Message(entry) is not { } message)
        {
            return;
        }

        EmitDetached(ConsoleEvents.MessageAdded(message));
    }

    /// <summary>
    /// One journalled call as a flat message, or <see langword="null"/> for a call that printed nothing.
    /// </summary>
    private static MessageAddedEvent? Message(ConsoleEntry entry)
    {
        if (entry.Message is not { } text)
        {
            return null;
        }

        return new MessageAddedEvent
        {
            Message = new ConsoleMessage
            {
                // Everything an engine target logs came through the console object, which is the one source
                // this domain has: there is no network, no storage and no renderer to attribute a line to.
                Source = ConsoleMessageSourceValues.ConsoleApi,
                Level = Level(entry.Level),
                Text = text,
            },
        };
    }

    private static string Level(ConsoleLogLevel level) => level switch
    {
        ConsoleLogLevel.Debug => ConsoleMessageLevelValues.Debug,
        ConsoleLogLevel.Info => ConsoleMessageLevelValues.Info,
        ConsoleLogLevel.Warn => ConsoleMessageLevelValues.Warning,
        ConsoleLogLevel.Error => ConsoleMessageLevelValues.Error,
        _ => ConsoleMessageLevelValues.Log,
    };
}
