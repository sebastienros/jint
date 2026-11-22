using System.Text.Json;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The order a session's replies and events leave in, which a client builds its model of the target from.
/// </summary>
public class ReplyOrderTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    /// <summary>
    /// A reply leaves before the events of the commands after it, as Chrome writes them: the engine answers the
    /// commands in order and the reply is written as the command finishes, not later from the thread that was
    /// waiting for it. Playwright sends <c>Page.getFrameTree</c> and then <c>Runtime.enable</c>; when the
    /// <c>executionContextCreated</c> of the second overtook the reply to the first, it dropped the context of a
    /// frame it did not know yet and waited for ever for the page's main world.
    /// </summary>
    [Test]
    public async Task AReplyLeavesBeforeTheEventsOfTheCommandsQueuedAfterIt()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();
        var sessionId = await session.AttachAsync(target);

        for (var round = 0; round < 20; round++)
        {
            var before = session.Sent.Count;

            // Both are queued before the host's turn answers them.
            var first = session.SendAsync("Runtime.evaluate", """{"expression":"1 + 1"}""", sessionId);
            var second = session.SendAsync("Runtime.enable", sessionId: sessionId);
            target.Pump();

            // Read at once, before the waiting threads have resumed: what the turn wrote is all there is.
            var sent = session.Sent.Skip(before).Select(Describe).ToList();
            sent.Should().ContainInOrder("reply", "Runtime.executionContextCreated", "reply");
            sent.IndexOf("reply").Should().BeLessThan(sent.IndexOf("Runtime.executionContextCreated"), "round {0} sent {1}", round, string.Join(", ", sent));

            await Task.WhenAll(first, second).WaitAsync(Bound);

            // Disabled again, so the next round's enable reports the context again.
            var disable = session.SendAsync("Runtime.disable", sessionId: sessionId);
            target.Pump();
            await disable.WaitAsync(Bound);
        }
    }

    private static string Describe(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.TryGetProperty("method", out var method) ? method.GetString()! : "reply";
    }
}
