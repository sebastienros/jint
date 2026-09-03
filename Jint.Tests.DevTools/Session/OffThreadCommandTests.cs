using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The one documented exception to the thread rule: a target names a command, and the gateway answers it on
/// the thread that read it instead of queueing it on the engine's mailbox.
/// </summary>
/// <remarks>
/// <para>
/// <b>An engine nothing pumps is what makes the property observable.</b> Every other way of asking the
/// question measures a clock; this one asks whether the command is answered at all, which it can only be if
/// it never reached the mailbox.
/// </para>
/// <para>
/// <b>The routing is per method, not per target.</b> The same target that answers its named method off the
/// thread still queues everything else, which is the half that keeps the exception one.
/// </para>
/// <para>
/// <b>Every wait is bounded.</b> A protocol test that can hang is a continuous-integration leg that can hang.
/// </para>
/// </remarks>
public class OffThreadCommandTests
{
    /// <summary>
    /// What a wait is allowed to take before the test calls it a hang, on the terms
    /// <see cref="ThreadModeTests"/> sets: a tight bound here measures the runner, not the server.
    /// </summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    /// <summary>How long a command the mailbox holds waits before it is told nothing is pumping.</summary>
    private static readonly TimeSpan ShortCommandTimeout = TimeSpan.FromMilliseconds(300);

    [Test]
    public async Task AMethodTheTargetNamesIsAnsweredWithTheEngineNeverPumped()
    {
        await using var session = ProtocolSession.Create(options: new DevToolsServerOptions
        {
            CommandTimeout = ShortCommandTimeout,
        });

        var target = new UnpumpedTarget(new Engine(), offThread: "Log.enable");
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        var reply = await session.SendAsync("Log.enable", null, attachment).WaitAsync(Bound);

        reply.TryGetProperty("error", out var error).Should().BeFalse(
            "a method the target declares off-thread is answered where it was read, and it answered {0}", error);
        target.Pumps.Should().Be(0, "nothing ran the engine's loop, which is the whole of the claim");
    }

    /// <summary>
    /// The same target, a method it did not name: still the mailbox, so an engine nobody pumps answers with
    /// the diagnosis a host needs rather than with the command.
    /// </summary>
    [Test]
    public async Task AMethodTheSameTargetDoesNotNameStillWaitsForThePump()
    {
        await using var session = ProtocolSession.Create(options: new DevToolsServerOptions
        {
            CommandTimeout = ShortCommandTimeout,
        });

        var target = new UnpumpedTarget(new Engine(), offThread: "Log.enable");
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        var error = (await session.SendAsync("Log.disable", null, attachment).WaitAsync(Bound)).GetProperty("error");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Engine is not being pumped");
    }

    /// <summary>
    /// A target that names nothing is unchanged: the command is queued, and the host's next turn answers it.
    /// </summary>
    [Test]
    public async Task ATargetThatNamesNothingAnswersOnceItIsPumped()
    {
        await using var session = ProtocolSession.Create();

        var target = new UnpumpedTarget(new Engine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        var pending = session.SendAsync("Log.enable", null, attachment);

        var deadline = DateTime.UtcNow + Bound;
        while (!pending.IsCompleted && DateTime.UtcNow < deadline)
        {
            target.Pump();
            await Task.Delay(5);
        }

        var reply = await pending.WaitAsync(Bound);
        reply.TryGetProperty("error", out var error).Should().BeFalse(
            "the command was answered by the pump rather than off it, and it answered {0}", error);
        target.Pumps.Should().BeGreaterThan(0, "the mailbox is what answered it");
    }
}
