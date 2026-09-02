using System.Text.Json;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Log;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.Tests.DevTools.Domains;

/// <summary>
/// The two things every domain that raises events needs from its base: an enable state, and a way back to
/// the session.
/// </summary>
/// <remarks>
/// <para>
/// Exercised through a domain of this suite's own, derived from a generated base exactly as a real one is.
/// That is deliberate: an untested extension point is a design nobody has tried, and the domains this
/// package ships do not between them use every part of it.
/// </para>
/// <para>
/// The commands are invoked directly rather than over the wire, and that is not a shortcut: dispatch is
/// gated on <c>manifest.json</c>, so a <c>Log.enable</c> nothing ships would answer <c>-32601</c> before
/// reaching this domain at all. <c>InProcessProtocolTests</c> is where that gate is pinned.
/// </para>
/// </remarks>
public class DomainLifecycleTests
{
    [Test]
    public async Task EnablingTwiceRunsTheHookOnce()
    {
        var (session, domain, _) = Create();
        var context = new CommandContext(session, session.SessionId, CancellationToken.None);

        await domain.HandleEnableAsync(context);
        await domain.HandleEnableAsync(context);

        domain.Enables.Should().Be(1, "clients enable a domain more than once, and the protocol's own domains treat the repeat as a success");
        domain.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task DisablingRunsTheHookOnlyWhenItWasEnabled()
    {
        var (session, domain, _) = Create();
        var context = new CommandContext(session, session.SessionId, CancellationToken.None);

        await domain.HandleDisableAsync(context);
        domain.Disables.Should().Be(0);

        await domain.HandleEnableAsync(context);
        await domain.HandleDisableAsync(context);

        domain.Disables.Should().Be(1);
        domain.IsEnabled.Should().BeFalse();
    }

    /// <summary>
    /// An event carries the identifier of the session that raised it, which is what lets a domain on an
    /// attached session emit without knowing which attachment it is part of.
    /// </summary>
    [Test]
    public async Task AnEventCarriesTheRaisingSessionsIdentifierAndNoRequestIdentifier()
    {
        var (root, _, connection) = Create();
        var child = root.CreateChild("S1");
        var domain = new CountingLogDomain();
        child.Register(domain);

        await domain.RaiseAsync();

        using var document = JsonDocument.Parse(connection.Sent[^1]);
        var message = document.RootElement;

        message.TryGetProperty("id", out _).Should().BeFalse("nothing is waiting on an event, so it carries no identifier");
        message.GetProperty("method").GetString().Should().Be("Log.entryAdded");
        message.GetProperty("sessionId").GetString().Should().Be("S1");
        message.GetProperty("params").GetProperty("entry").GetProperty("text").GetString().Should().Be("hello");
        message.GetProperty("params").GetProperty("entry").GetProperty("source").GetString().Should().Be("javascript");
    }

    /// <summary>
    /// An optional member the domain did not set is absent rather than <c>null</c>, which is what the
    /// protocol means by optional and what a client's own deserializer expects.
    /// </summary>
    [Test]
    public async Task AnUnsetOptionalMemberIsNotWritten()
    {
        var (_, domain, connection) = Create();

        await domain.RaiseAsync();

        using var document = JsonDocument.Parse(connection.Sent[^1]);
        var entry = document.RootElement.GetProperty("params").GetProperty("entry");

        entry.TryGetProperty("url", out _).Should().BeFalse();
        entry.TryGetProperty("stackTrace", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("sessionId", out _).Should().BeFalse("the root session has no identifier to put on its events");
    }

    private static (DevToolsSession Session, CountingLogDomain Domain, InProcessConnection Connection) Create()
    {
        var connection = new InProcessConnection();
        var domain = new CountingLogDomain();
        var session = new DevToolsSession(connection).Register(domain);
        return (session, domain, connection);
    }

    /// <summary>
    /// A <c>Log</c> domain that answers <c>enable</c> and <c>disable</c> and counts the hooks, built the way
    /// a real domain is: derive from the generated base, override the command's virtual, and let the base
    /// keep the state.
    /// </summary>
    private sealed class CountingLogDomain : LogDomainBase
    {
        internal int Enables { get; private set; }

        internal int Disables { get; private set; }

        internal ValueTask<EmptyResult> HandleEnableAsync(CommandContext context) => EnableAsync(EmptyParameters.Instance, context);

        internal ValueTask<EmptyResult> HandleDisableAsync(CommandContext context) => DisableAsync(EmptyParameters.Instance, context);

        internal ValueTask RaiseAsync()
        {
            var entry = new LogEntry
            {
                Source = LogEntrySourceValues.Javascript,
                Level = LogEntryLevelValues.Info,
                Text = "hello",
                Timestamp = 1,
            };

            return EmitAsync(LogEvents.EntryAdded(new EntryAddedEvent { Entry = entry }));
        }

        protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
        {
            await MarkEnabledAsync(context).ConfigureAwait(false);
            return EmptyResult.Instance;
        }

        protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
        {
            await MarkDisabledAsync(context).ConfigureAwait(false);
            return EmptyResult.Instance;
        }

        protected override ValueTask OnEnabledAsync(CommandContext context)
        {
            Enables++;
            return default;
        }

        protected override ValueTask OnDisabledAsync(CommandContext context)
        {
            Disables++;
            return default;
        }
    }
}
